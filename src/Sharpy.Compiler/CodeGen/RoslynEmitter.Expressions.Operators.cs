using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Binary/unary operators, comparison chains,
/// conditional expressions, type casts/coercions/checks, pipe forward, string repetition
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Reads the equality lowering strategy for a <c>==</c>/<c>!=</c> operation from the lowering IR
    /// (E2 #1056, migrates <c>_binaryOpLowerings</c>). Returns <c>null</c> when the node has no
    /// <see cref="IrEqualityComparison"/> (the IR was not built, or the node is not an equality
    /// comparison), which callers treat the same as the default native operator.
    /// </summary>
    private BinaryOpLowering? GetIrBinaryOpLowering(Expression binaryOp)
    {
        return _context.Ir?.Index.TryGetValue(binaryOp, out var node) == true
            && node is IrEqualityComparison equality
            ? equality.Strategy
            : null;
    }

    private ExpressionSyntax GenerateBinaryOp(BinaryOp binOp)
    {
        // `and` emits a plain `left && right` (kind mapped below). Narrowing of RHS reads
        // (e.g. `x is not None and f(x)`, `isinstance(a, Dog) and a.bark()`) is materialized
        // per-read-node by the TypeChecker and applied at the read sites (#1081) — the emitter
        // no longer re-derives which variables the left operand narrows.
        //
        // Pipe-forward is decided BEFORE the operands are generated: its lowering re-generates
        // the left operand itself (the piped value becomes a call argument), so generating it
        // here first produced the value twice and discarded one. GenerateExpression is not pure —
        // it can push into `_hoistedStatements`, which are flushed unconditionally — so a
        // speculative generation is a duplicated side effect waiting for the right operand
        // (#1228's rule, found live by the re-entry tripwire, #1334).
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            // x |> f → f(x); x |> f(y) → f(x, y) (prepend to argument list)
            return GeneratePipeForward(binOp.Left, binOp.Right);
        }

        var left = GenerateExpression(binOp.Left);
        var right = GenerateExpression(binOp.Right);

        // Special cases that need method calls or casts
        switch (binOp.Operator)
        {
            case BinaryOperator.Power:
                {
                    // Constant-folded integer power (#905): the lowering pass re-derives the value
                    // (widened to int/long, or SPY0328 when it exceeds long) into an IrConstant, so
                    // emit the literal directly instead of a lossy Math.Pow round-trip. e.g.
                    // `y: long = 10 ** 18`. (E2 #1056: read from the IR, not SemanticInfo.)
                    // The literal's width is the folded constant's own recorded type — the same
                    // emitter the E3 fold uses, so there is one folded-literal spelling (#1623).
                    if (_context.Ir?.Index.TryGetValue(binOp, out var foldedNode) == true
                        && foldedNode is IrConstant foldedConst)
                    {
                        return EmitFoldedConstant(foldedConst);
                    }

                    // x ** y → ONE invocation, each operand spliced once (#1228): the recorded
                    // power tag picks CheckedIntPow (int/long width) or Math.Pow. The routing
                    // wrapper is shared with the augmented `**=` site (#1227), so the two cannot
                    // drift — the same arrangement `//` and `%` already use.
                    //
                    // CheckedIntPow absorbs the negative-exponent case itself (returning the
                    // truncating double-path value, so `2 ** -1` is still 0 per the spec), which
                    // is what lets the gate, the dispatch ternary and both regenerations go.
                    //
                    // What this replaced was unsound in two ways. It called
                    // GenerateExpression(binOp.Left) a second time with NO gate at all — the
                    // IsSideEffectFree check covered only the right operand — and
                    // GenerateExpression is not pure: it can push into _hoistedStatements
                    // (the #1198 tuple-spread hoist), so `sum(make_tuple()) ** 2` emitted the
                    // hoist twice and called make_tuple() twice. And when the gate DID fire it
                    // silently degraded the lowering to the saturating `(int)Math.Pow` cast, so
                    // `x ** f()` had different overflow behaviour from `x ** y` — a spelling
                    // difference changing semantics. Both spellings now raise OverflowError.
                    return GeneratePowerValue(left, right, binOp);
                }

            case BinaryOperator.Divide:
                if (_context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind
                    == OperatorLoweringKind.TrueDivisionCastLeft)
                {
                    return BinaryExpression(SyntaxKind.DivideExpression,
                        CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
                        right);
                }
                return BinaryExpression(SyntaxKind.DivideExpression, left, right);

            case BinaryOperator.FloorDivide:
                // x // y → floor division with Python semantics (toward negative infinity).
                // One Builtins.FloorDiv invocation for integer AND float operands (#1226) —
                // guard and dispatch live in the helper, each operand spliced once.
                // Decimal operands: native truncating quotient (routed inside the wrapper,
                // which both this site and the augmented `//=` site share).
                return GenerateFloorDivideValue(left, right, binOp.Left, binOp.Right);

            case BinaryOperator.Modulo:
                // x % y → Python floored modulo (result sign = divisor sign) for int/long/
                // float32/float64 operands: C#'s native `%` takes the sign of the dividend, which
                // diverges from Python. Decimal keeps the native truncating remainder but routes
                // through a zero-divisor guard (#1189). Both decisions live in the routing wrapper
                // this site shares with the augmented `%=` site, so the two cannot drift. User
                // types with `__mod__` (→ operator %) and other CLR `op_Modulus` types get null
                // back and MUST keep the native ModuloExpression map below.
                var moduloValue = GenerateModuloValue(left, right, binOp.Left, binOp.Right);
                if (moduloValue != null)
                    return moduloValue;
                break;

            case BinaryOperator.MatMul:
                // x @ y → x.MatMul(y). C# has no `@` operator, so matrix multiplication
                // (PEP 465) dispatches to the MatMul instance method that both user-defined
                // __matmul__ methods and stdlib NdArray expose. See DunderNameMapping.
                return GenerateMatMulCall(left, right);

            case BinaryOperator.In:
                // x in y → y.Contains(x)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        right,
                        IdentifierName("Contains")))
                    .AddArgumentListArguments(Argument(left));

            case BinaryOperator.NotIn:
                // x not in y → !y.Contains(x)
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            right,
                            IdentifierName("Contains")))
                        .AddArgumentListArguments(Argument(left)));

            case BinaryOperator.Is:
                if (binOp.Right is NoneLiteral)
                {
                    if (_context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind
                        == OperatorLoweringKind.OptionalNoneTest)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsNone"));
                    }
                    return BinaryExpression(SyntaxKind.EqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        IdentifierName("ReferenceEquals")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.IsNot:
                if (binOp.Right is NoneLiteral)
                {
                    if (_context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind
                        == OperatorLoweringKind.OptionalNoneTest)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsSome"));
                    }
                    return BinaryExpression(SyntaxKind.NotEqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                            IdentifierName("ReferenceEquals")))
                        .AddArgumentListArguments(
                            Argument(left),
                            Argument(right)));

            case BinaryOperator.NullCoalesce:
                {
                    var coalesceKind = _context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind;
                    if (coalesceKind == OperatorLoweringKind.OptionalCoalesceBothOptional)
                    {
                        var (safeLeft, captureLeft) = EnsureSingleEvaluation(left, binOp.Left);
                        ExpressionSyntax coalesceCondition = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            ParenthesizedExpression(safeLeft), IdentifierName("IsSome"));
                        if (captureLeft != null)
                            coalesceCondition = BinaryExpression(SyntaxKind.LogicalAndExpression, captureLeft, coalesceCondition);
                        return ConditionalExpression(coalesceCondition, safeLeft, right);
                    }
                    if (coalesceKind == OperatorLoweringKind.OptionalUnwrapOr)
                    {
                        return InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ParenthesizedExpression(left), IdentifierName("UnwrapOr")))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(right))));
                    }
                    return BinaryExpression(SyntaxKind.CoalesceExpression, left, right);
                }

            case BinaryOperator.Multiply:
                {
                    // String repetition: the tag says which operand is the string (#1623).
                    var repeatKind = _context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind;
                    if (repeatKind == OperatorLoweringKind.StringRepeatStrLeft)
                        return GenerateStringRepeat(left, right);
                    if (repeatKind == OperatorLoweringKind.StringRepeatStrRight)
                        return GenerateStringRepeat(right, left);
                    break;
                }

        }

        // and/or: wrap both operands through truthiness before emitting && / || (#1558)
        if (binOp.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            var wrappedLeft = WrapTruthinessIfNeeded(left, binOp.Left);
            var wrappedRight = WrapTruthinessIfNeeded(right, binOp.Right);
            var logicalKind = binOp.Operator == BinaryOperator.And
                ? SyntaxKind.LogicalAndExpression
                : SyntaxKind.LogicalOrExpression;
            return BinaryExpression(logicalKind, wrappedLeft, wrappedRight);
        }

        // Standard binary operators
        var kind = binOp.Operator switch
        {
            // Arithmetic (Divide is handled specially above for Python semantics)
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
            BinaryOperator.Modulo => SyntaxKind.ModuloExpression,

            // Comparison
            BinaryOperator.Equal => SyntaxKind.EqualsExpression,
            BinaryOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            BinaryOperator.LessThan => SyntaxKind.LessThanExpression,
            BinaryOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            BinaryOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            BinaryOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,

            // Bitwise
            BinaryOperator.BitwiseAnd => SyntaxKind.BitwiseAndExpression,
            BinaryOperator.BitwiseOr => SyntaxKind.BitwiseOrExpression,
            BinaryOperator.BitwiseXor => SyntaxKind.ExclusiveOrExpression,
            BinaryOperator.LeftShift => SyntaxKind.LeftShiftExpression,
            BinaryOperator.RightShift => SyntaxKind.RightShiftExpression,

            // NullCoalesce is handled in the special-cases switch above

            _ => SyntaxKind.None
        };

        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: binary operator '{binOp.Operator}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator, binOp.LineStart, binOp.ColumnStart);
        }

        // Comparisons lower through the SAME helper every comparison-chain link uses (#1642):
        // the equality strategy rides the IR transport, the ordering kind the OperatorLowering tag.
        if (IsComparisonSyntaxKind(kind))
        {
            return GenerateLoweredComparison(
                kind, left, right, binOp.Left, binOp.Right,
                _context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind ?? OperatorLoweringKind.Native,
                GetIrBinaryOpLowering(binOp) ?? BinaryOpLowering.NativeOperator);
        }

        if (_context.SemanticInfo?.GetOperatorLowering(binOp)?.Kind
            == OperatorLoweringKind.ShiftCountCastToInt)
        {
            right = CastExpression(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                ParenthesizedExpression(right));
        }

        return BinaryExpression(kind, left, right);
    }

    /// <summary>
    /// <c>global::Sharpy.StringHelpers.Repeat(str, count)</c> — the string-repetition lowering shared
    /// by the binary <c>*</c> and the augmented <c>*=</c> sites. The caller passes the operands in
    /// the order the recorded <see cref="OperatorLoweringKind.StringRepeatStrLeft"/> /
    /// <see cref="OperatorLoweringKind.StringRepeatStrRight"/> tag dictates (#1623).
    /// </summary>
    private ExpressionSyntax GenerateStringRepeat(ExpressionSyntax str, ExpressionSyntax count)
    {
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                IdentifierName("Repeat")))
            .AddArgumentListArguments(
                Argument(str),
                Argument(count));
    }

    /// <summary>
    /// Generate a matrix-multiplication call: <c>left @ right → left.MatMul(right)</c>.
    /// Shared by the binary <c>@</c> operator and the <c>@=</c> augmented assignment. C# has
    /// no matmul operator, so both lower to the <c>MatMul</c> instance method that user-defined
    /// <c>__matmul__</c> methods and stdlib NdArray expose (see <see cref="DunderNameMapping"/>).
    /// </summary>
    private ExpressionSyntax GenerateMatMulCall(ExpressionSyntax left, ExpressionSyntax right)
    {
        var methodName = DunderMapping.ResolveCSharpName(DunderNames.MatMul) ?? "MatMul";
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                left,
                IdentifierName(methodName)))
            .AddArgumentListArguments(Argument(right));
    }

    /// <summary>
    /// Generate code for pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity in parser)
    /// </summary>
    private ExpressionSyntax GeneratePipeForward(Expression leftExpr, Expression rightExpr)
    {
        var left = GenerateExpression(leftExpr);

        // A pipe target is a callee, so it unwraps like one — the same purely structural
        // normalization GenerateCall applies (#1147, #1170). Without it a parenthesized target
        // reaches GenerateExpression and emits `(Double)(...)`, which C# re-parses as a cast.
        rightExpr = Shared.AstHelper.UnwrapParenthesized(rightExpr);

        // Case 1: Right side is already a function call - prepend left to its arguments
        // x |> f(y, z) → f(x, y, z)
        if (rightExpr is FunctionCall funcCall)
        {
            // Generate the function name with proper name mangling (same as GenerateCall)
            var func = GeneratePipeCallTarget(funcCall.Function);

            // Resolve the callee's FunctionSymbol for parameter reordering
            var pipeFuncSymbol = _context.SemanticInfo?.GetCallTarget(funcCall);
            if (pipeFuncSymbol == null
                && Shared.AstHelper.UnwrapParenthesized(funcCall.Function) is Identifier pipeFuncId)
                pipeFuncSymbol = _context.LookupSymbol(pipeFuncId.Name) as FunctionSymbol;

            // Delegate to shared call-site reordering with the piped value prepended
            var allArgs = GenerateReorderedCallArguments(funcCall, pipeFuncSymbol, Argument(left));

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Case 2: Right side is a lambda from partial application lowering
        // x |> multiply(_, 3) → parser lowered to: x |> lambda __p0: multiply(__p0, 3)
        // Unwrap: substitute the pipe value for the placeholder and generate a direct call
        if (rightExpr is LambdaExpression partialLambda
            && partialLambda.Parameters.Length == 1
            && partialLambda.Body is FunctionCall partialCall)
        {
            var placeholderName = partialLambda.Parameters[0].Name;
            var func = GeneratePipeCallTarget(partialCall.Function);

            // Build arguments, replacing the placeholder identifier with the piped value
            var substitutedArgs = new List<ArgumentSyntax>();
            foreach (var arg in partialCall.Arguments)
            {
                if (arg is Identifier id && id.Name == placeholderName)
                    substitutedArgs.Add(Argument(left));
                else
                    substitutedArgs.Add(Argument(GenerateExpression(arg)));
            }
            foreach (var kw in partialCall.KeywordArguments)
            {
                substitutedArgs.Add(Argument(GenerateExpression(kw.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kw.Name)))));
            }

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(substitutedArgs)));
        }

        // Case 3: Right side is an identifier or member access - call it with left as the only argument
        // x |> f → f(x)
        var right = GeneratePipeCallTarget(rightExpr);
        return InvocationExpression(right)
            .AddArgumentListArguments(Argument(left));
    }

    /// <summary>
    /// Generate the call target expression for a pipe operator.
    /// Handles proper name mangling for function names (PascalCase) and builtin functions.
    /// </summary>
    private ExpressionSyntax GeneratePipeCallTarget(Expression expr)
    {
        expr = Shared.AstHelper.UnwrapParenthesized(expr);

        if (expr is Identifier funcName)
        {
            // User-defined functions shadow builtins (Python scoping rules)
            var isBuiltin = _context.IsBuiltinFunction(funcName.Name);
            var symbol = _context.LookupSymbol(funcName.Name);
            if (isBuiltin && symbol is FunctionSymbol { CodeGenInfo: not null })
                isBuiltin = false;

            if (isBuiltin)
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins", NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped));
            }
            return ParseQualifiedName(NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped));
        }

        // For member access and other expressions, use standard expression generation
        return GenerateExpression(expr);
    }

    private ExpressionSyntax GenerateUnaryOp(UnaryOp unaryOp)
    {
        // Minus over an integer literal: emit a single literal token of the width the TypeChecker
        // recorded (NegateLiteralInt / NegateLiteralLong, #1623) so -2147483648 is int.MinValue,
        // not -(2147483648L) which is CS0266 (#1304). The classifier that recorded the tag already
        // parsed the magnitude and proved it fits, so no overflow path exists here; any other
        // width (or no tag) takes the ordinary unary-minus path below.
        if (unaryOp.Operator == UnaryOperator.Minus && unaryOp.Operand is IntegerLiteral il)
        {
            var negateKind = _context.SemanticInfo?.GetOperatorLowering(unaryOp)?.Kind;
            if (negateKind is OperatorLoweringKind.NegateLiteralInt or OperatorLoweringKind.NegateLiteralLong)
            {
                var ulongMagnitude = ParseIntegerText(il.Value.Replace("_", "", StringComparison.Ordinal));
                if (negateKind == OperatorLoweringKind.NegateLiteralInt)
                {
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                        Literal((int)(-(long)ulongMagnitude)));
                }
                if (ulongMagnitude == (ulong)long.MaxValue + 1)
                    return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(long.MinValue));
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(-(long)ulongMagnitude));
            }
        }

        var operand = GenerateExpression(unaryOp.Operand);

        // `not` wraps the operand through truthiness before negating (#1558, #1570)
        if (unaryOp.Operator == UnaryOperator.Not)
        {
            var wrapped = WrapTruthinessIfNeeded(operand, unaryOp.Operand);
            if (wrapped is BinaryExpressionSyntax)
                wrapped = ParenthesizedExpression(wrapped);
            return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, wrapped);
        }

        var kind = unaryOp.Operator switch
        {
            UnaryOperator.Plus => SyntaxKind.UnaryPlusExpression,
            UnaryOperator.Minus => SyntaxKind.UnaryMinusExpression,
            UnaryOperator.BitwiseNot => SyntaxKind.BitwiseNotExpression,
            _ => SyntaxKind.None
        };

        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: unary operator '{unaryOp.Operator}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator, unaryOp.LineStart, unaryOp.ColumnStart);
        }

        return PrefixUnaryExpression(kind, operand);
    }

    private static bool IsComparisonSyntaxKind(SyntaxKind kind)
        => kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>
    /// Emits one comparison (<c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>) by the
    /// lowering semantic analysis recorded for it — the binary operator reads its
    /// <see cref="OperatorLowering"/> tag + IR equality strategy, a comparison-chain link its
    /// <see cref="ComparisonLinkLowering"/> — so the two positions share one emission and cannot drift (#1642).
    /// <para>
    /// Equality: <see cref="BinaryOpLowering.NoneCheck"/> (#901) is a C# null pattern on the non-None side
    /// (<c>x is null</c> / <c>x is not null</c>); the literal-shape guard is an invariant assertion mirroring
    /// ControlFlow.cs — the #911 semantic gate (SPY0329) rejects any non-literal VoidType comparand, so a
    /// NoneCheck always has exactly one NoneLiteral operand and a regression falls through to the loud native
    /// operator rather than dropping an operand. <see cref="BinaryOpLowering.EqualsCallInstance"/> /
    /// <see cref="BinaryOpLowering.EqualsCallStatic"/> (#886) are the tuple/CLR Equals calls whose
    /// instance-vs-static choice the TypeChecker materialized; <see cref="BinaryOpLowering.EqualityComparerDefault"/>
    /// names the comparand's recorded type as <c>EqualityComparer&lt;T&gt;</c>'s argument.
    /// </para>
    /// <para>
    /// Ordering: <see cref="OperatorLoweringKind.StringOrdinalCompare"/> is <c>string.Compare(l, r, Ordinal) op 0</c>,
    /// <see cref="OperatorLoweringKind.TypeParameterCompareTo"/> is <c>l.CompareTo(r) op 0</c>; anything else is the
    /// native C# operator.
    /// </para>
    /// </summary>
    private ExpressionSyntax GenerateLoweredComparison(
        SyntaxKind kind,
        ExpressionSyntax left,
        ExpressionSyntax right,
        Expression leftAst,
        Expression rightAst,
        OperatorLoweringKind orderingLowering,
        BinaryOpLowering equalityLowering)
    {
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            if (equalityLowering == BinaryOpLowering.NoneCheck
                && (leftAst is NoneLiteral) != (rightAst is NoneLiteral))
            {
                var operand = leftAst is NoneLiteral ? right : left;
                PatternSyntax nullPattern = ConstantPattern(
                    LiteralExpression(SyntaxKind.NullLiteralExpression));
                if (kind == SyntaxKind.NotEqualsExpression)
                    nullPattern = UnaryPattern(Token(SyntaxKind.NotKeyword), nullPattern);
                return IsPatternExpression(operand, nullPattern);
            }

            if (equalityLowering is BinaryOpLowering.NativeOperator or BinaryOpLowering.NoneCheck)
                return BinaryExpression(kind, left, right);

            ExpressionSyntax equalsInvocation;
            switch (equalityLowering)
            {
                case BinaryOpLowering.EqualityComparerDefault:
                    {
                        // The comparand's type is a recorded fact (the TypeChecker classified the pair as
                        // type-parameter equality); the type mapper spells it.
                        var comparandType = GetExpressionSemanticType(leftAst)
                            ?? throw new InvalidOperationException(
                                "No expression type recorded for the left operand of an EqualityComparerDefault "
                                + "comparison — the TypeChecker must type every comparand it classifies (#1623)");
                        equalsInvocation = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    GenericName("EqualityComparer")
                                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(
                                            _typeMapper.MapSemanticType(comparandType)))),
                                    IdentifierName("Default")),
                                IdentifierName("Equals")))
                            .AddArgumentListArguments(Argument(left), Argument(right));
                        break;
                    }
                case BinaryOpLowering.EqualsCallInstance:
                    equalsInvocation = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                left,
                                IdentifierName("Equals")))
                        .AddArgumentListArguments(Argument(right));
                    break;
                case BinaryOpLowering.EqualsCallStatic:
                    equalsInvocation = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                                IdentifierName("Equals")))
                        .AddArgumentListArguments(Argument(left), Argument(right));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unhandled equality lowering strategy '{equalityLowering}' — add its emission here");
            }

            return kind == SyntaxKind.NotEqualsExpression
                ? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(equalsInvocation))
                : equalsInvocation;
        }

        switch (orderingLowering)
        {
            case OperatorLoweringKind.StringOrdinalCompare:
                {
                    var compareCall = InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            PredefinedType(Token(SyntaxKind.StringKeyword)),
                            IdentifierName("Compare")))
                        .AddArgumentListArguments(
                            Argument(left),
                            Argument(right),
                            Argument(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("System"),
                                        IdentifierName("StringComparison")),
                                    IdentifierName("Ordinal"))));
                    return BinaryExpression(kind,
                        compareCall,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
                }
            case OperatorLoweringKind.TypeParameterCompareTo:
                {
                    var compareToCall = InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("CompareTo")))
                        .AddArgumentListArguments(Argument(right));
                    return BinaryExpression(kind,
                        compareToCall,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
                }
            default:
                return BinaryExpression(kind, left, right);
        }
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c
        // Python guarantees intermediate expressions are evaluated exactly once.
        // For non-trivial intermediate expressions (function calls, member access, etc.),
        // we use the C# "is var" pattern to capture the value inline:
        //   a < (f() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < c
        // Each link applies the lowering the TypeChecker recorded for it — the same
        // classification its binary form would get (#1642) — through GenerateLoweredComparison.

        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            throw new InvalidOperationException("Invalid comparison chain");
        }

        var chainLowering = _context.SemanticInfo?.GetComparisonChainLowering(chain)
            ?? throw new InvalidOperationException(
                "No ComparisonChainLowering recorded for comparison chain — CheckComparisonChain must "
                + "record one link per operator (#1642)");
        if (chainLowering.Links.Length != chain.Operators.Length)
        {
            throw new InvalidOperationException(
                $"ComparisonChainLowering has {chainLowering.Links.Length} link(s) for a chain with "
                + $"{chain.Operators.Length} operator(s) (#1642)");
        }

        // For intermediate operands (indices 1..n-2), decide if they need a temp variable.
        // First and last operands are only used once and don't need temps.
        var tempNames = new string?[chain.Operands.Length];
        for (int i = 1; i < chain.Operands.Length - 1; i++)
        {
            if (!IsTrivialExpression(chain.Operands[i]))
            {
                tempNames[i] = GenerateTempVarName("cmp");
            }
        }

        ExpressionSyntax? result = null;

        for (int i = 0; i < chain.Operators.Length; i++)
        {
            ExpressionSyntax left;
            ExpressionSyntax right;

            // Left operand: use temp name from previous iteration if available
            if (i > 0 && tempNames[i] != null)
            {
                left = IdentifierName(tempNames[i]!);
            }
            else
            {
                left = GenerateExpression(chain.Operands[i]);
            }

            // Right operand: capture into temp if this is an intermediate with side effects
            var rightExpr = GenerateExpression(chain.Operands[i + 1]);
            if (tempNames[i + 1] != null)
            {
                // Wrap in: (expr is var __cmp_N ? __cmp_N : __cmp_N)
                // This evaluates expr once, binds to __cmp_N, and returns the value
                right = ParenthesizedExpression(
                    ConditionalExpression(
                        IsPatternExpression(
                            rightExpr,
                            VarPattern(SingleVariableDesignation(Identifier(tempNames[i + 1]!)))),
                        IdentifierName(tempNames[i + 1]!),
                        IdentifierName(tempNames[i + 1]!)));
            }
            else
            {
                right = rightExpr;
            }

            var op = chain.Operators[i];
            var kind = MapComparisonOperator(op);

            if (kind == SyntaxKind.None)
            {
                return EmitNotImplementedExpression(
                    $"Unsupported operator in code generation: comparison operator '{op}' in chains",
                    DiagnosticCodes.CodeGen.UnsupportedOperator, chain.LineStart, chain.ColumnStart);
            }

            var link = chainLowering.Links[i];
            var comparison = GenerateLoweredComparison(
                kind, left, right, chain.Operands[i], chain.Operands[i + 1],
                link.Kind, link.Equality ?? BinaryOpLowering.NativeOperator);

            result = result == null
                ? comparison
                : BinaryExpression(SyntaxKind.LogicalAndExpression, result, comparison);
        }

        return result ?? throw new InvalidOperationException("Empty comparison chain");
    }

    /// <summary>
    /// Maps a comparison operator to the corresponding C# syntax kind.
    /// </summary>
    private SyntaxKind MapComparisonOperator(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equal => SyntaxKind.EqualsExpression,
            ComparisonOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            ComparisonOperator.LessThan => SyntaxKind.LessThanExpression,
            ComparisonOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            ComparisonOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            ComparisonOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
            _ => SyntaxKind.None
        };
    }

    /// <summary>
    /// Returns true if the expression is trivial (identifier, literal) and
    /// safe to evaluate multiple times without side effects.
    /// </summary>
    private static bool IsTrivialExpression(Expression expr)
    {
        return expr is Parser.Ast.Identifier
            or IntegerLiteral
            or FloatLiteral
            or StringLiteral
            or BooleanLiteral
            or NoneLiteral;
    }

    private ExpressionSyntax GenerateConditionalExpression(ConditionalExpression cond)
    {
        // value if test else other → test ? value : other
        var test = WrapTruthinessIfNeeded(GenerateExpression(cond.Test), cond.Test);
        var whenTrue = GenerateExpression(cond.ThenValue);
        var whenFalse = GenerateExpression(cond.ElseValue);

        return ConditionalExpression(test, whenTrue, whenFalse);
    }

    private ExpressionSyntax GenerateTypeCoercion(TypeCoercion coercion)
    {
        // The cast operators lower purely by their failure mode; the `to`/`to?` and
        // `as!`/`as?` spellings share one lowering (snapshot parity, #1029). Every shape below is
        // chosen in semantic analysis and materialized as a TypeCoercionLowering; the emitter only
        // applies it (#1110, #1306):
        // - Throw mode (value to T / value as! T):
        //     * NumericChecked (narrowing) → global::Sharpy.NumericCheckedCast.To{T}((hub)value)
        //     * absent (widening, enums, unboxing, __explicit__, reference downcasts) → (T)value
        // - Null  mode (value to T? / value as? T):
        //     * NumericRangeChecked (narrowing) → global::Sharpy.NumericSafeCast.To{T}OrNone((hub)value)
        //     * NumericAlwaysFits (widening/identity) → Optional<T>.Some((T)value)
        //     * absent (object/reference/optional/non-numeric source) → the type-pattern form
        //       value is T _temp ? Optional<T>.Some(_temp) : default
        // The type pattern is only correct for object/reference sources — a concrete numeric source
        // (`x is int` when x is double) is CS8121, which is exactly why the numeric shapes exist.
        // For `to`, Mode is set from the target's nullability at parse time, so this branch selection
        // is identical to the historical `TargetType.IsOptional` check.

        var value = GenerateExpression(coercion.Value);

        if (coercion.Mode == CastFailureMode.Null)
        {
            // The `as?` form writes T non-nullable while `to T?` carries IsOptional; stripping
            // IsOptional here yields the same base type T for both. When the checker classified the
            // target (#1235) that decision wins, so a bare generic name is the closed type the source
            // determined rather than an open one — CS0305, twice, since this syntax is used in both
            // the type pattern and the Optional<T> argument below.
            var baseTypeSyntax = _context.SemanticInfo?.GetTypeTestLowering(coercion.TargetType) is { } targetLowering
                ? MapTypeTestTarget(targetLowering)
                : _typeMapper.MapType(new TypeAnnotation
                {
                    Name = coercion.TargetType.Name,
                    IsNameBacktickEscaped = coercion.TargetType.IsNameBacktickEscaped,
                    TypeArguments = coercion.TargetType.TypeArguments,
                    IsOptional = false
                });

            var numericLowering = _context.SemanticInfo?.GetTypeCoercionLowering(coercion);
            if (numericLowering != null)
            {
                var optionalTypeName = GenericName("Optional")
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(baseTypeSyntax)));

                if (numericLowering.Kind == TypeCoercionLoweringKind.NumericRangeChecked)
                {
                    // Narrowing: global::Sharpy.NumericSafeCast.To{T}OrNone(value). The helper returns
                    // Optional<T> directly (Some in range, None for out-of-range/NaN/±inf).
                    return GenerateNumericCastHelperCall("NumericSafeCast", numericLowering, value);
                }

                // NumericAlwaysFits: Optional<T>.Some((T)value). The C# cast is a widening/identity
                // conversion (or double→float32, which maps overflow to ±inf), so it never overflows.
                var castOperand = IsTrivialExpression(coercion.Value) ? value : ParenthesizedExpression(value);
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        optionalTypeName,
                        IdentifierName("Some")))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                        Argument(CastExpression(baseTypeSyntax, castOperand)))));
            }

            // Default type-pattern form (object/reference/optional sources):
            // value is T _temp ? Optional<T>.Some(_temp) : default
            // default produces Optional<T>.None (struct with _hasValue = false).
            var tempName = $"__coerce_temp_{_tempVarCounter++}";

            var optionalType = GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(baseTypeSyntax)));
            var someExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    optionalType,
                    IdentifierName("Some")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(tempName)))));

            return ConditionalExpression(
                IsPatternExpression(
                    value,
                    DeclarationPattern(
                        baseTypeSyntax,
                        SingleVariableDesignation(Identifier(tempName)))),
                someExpr,
                LiteralExpression(SyntaxKind.DefaultLiteralExpression));
        }
        else
        {
            // Throwing form. A recorded checked lowering means the pair can lose data: emit the helper
            // so the failure is a catchable Sharpy.OverflowError/ValueError instead of C#'s unchecked
            // wrap (#1306). Without one — widening numerics, enums, unboxing, __explicit__, reference
            // downcasts — the bare cast stands, and a user-defined conversion is invoked by it exactly
            // as it is for `to`.
            var checkedLowering = _context.SemanticInfo?.GetTypeCoercionLowering(coercion);
            if (checkedLowering is { Kind: TypeCoercionLoweringKind.NumericChecked })
            {
                return GenerateNumericCastHelperCall("NumericCheckedCast", checkedLowering, value);
            }

            var targetType = MapClassifiedTypeOperand(coercion.TargetType);
            return CastExpression(targetType, value);
        }
    }

    /// <summary>
    /// Builds <c>global::Sharpy.{helperClass}.{HelperMethod}(({SourceHubType})value)</c>. The hub cast is
    /// emitted only when the checker recorded one — the helpers take <c>long</c>, <c>ulong</c> and
    /// <c>double</c> parameters, and an operand of any other numeric width must name which it converts
    /// to or the call is CS0121-ambiguous between the signed and unsigned 64-bit overloads.
    /// </summary>
    private ExpressionSyntax GenerateNumericCastHelperCall(
        string helperClass,
        TypeCoercionLowering lowering,
        ExpressionSyntax value)
    {
        var argument = lowering.SourceHubType == null
            ? value
            : CastExpression(
                PredefinedType(Token(HubTypeKeyword(lowering.SourceHubType))),
                IsTrivialExpression_ForHub(value) ? value : ParenthesizedExpression(value));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", helperClass),
                IdentifierName(lowering.HelperMethod!)))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(argument))));
    }

    /// <summary>Maps a recorded hub name to its C# keyword token.</summary>
    private static SyntaxKind HubTypeKeyword(string hub)
        => hub switch
        {
            "long" => SyntaxKind.LongKeyword,
            "ulong" => SyntaxKind.ULongKeyword,
            "double" => SyntaxKind.DoubleKeyword,
            _ => throw new System.InvalidOperationException(
                $"Unknown numeric cast hub type '{hub}' — the checker recorded a hub the emitter "
                + "cannot spell.")
        };

    /// <summary>
    /// Whether generated syntax is atomic enough to cast without parentheses. Unlike
    /// <see cref="IsTrivialExpression(Expression)"/> this inspects the emitted C#, because the hub cast
    /// sits between the operand and the helper call.
    /// </summary>
    private static bool IsTrivialExpression_ForHub(ExpressionSyntax syntax)
        => syntax is IdentifierNameSyntax
            or LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or InvocationExpressionSyntax
            or MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax;

    private ExpressionSyntax GenerateTypeCheck(TypeCheck check)
    {
        // UNREACHABLE, KEPT AS DEFENCE-IN-DEPTH (#1298): `x is TypeName` is retired as a type test,
        // so CheckTypeCheck refuses every TypeCheck node with SPY0349 and no compilation reaches
        // here. The parser still builds the node (tooling reads the shape), and this arm stays so a
        // future path to it emits the operand the semantic phase decided on rather than the written
        // annotation. That distinction is why it is written this way (Critical Rule 2): mapping the
        // annotation directly is what emitted the unspellable open generic `Box<T>` (CS0305 →
        // SPY0908, #1235) back when this arm was live.
        var value = GenerateExpression(check.Value);
        var checkType = MapClassifiedTypeOperand(check.CheckType);

        return BinaryExpression(
            SyntaxKind.IsExpression,
            value,
            checkType);
    }

    /// <summary>
    /// Renders a type operand written as a <see cref="TypeAnnotation"/>, applying the classification
    /// the TypeChecker recorded for it. Falls back to mapping the annotation only when there is no
    /// recorded decision — a synthesized node the checker never saw — mirroring the isinstance
    /// reader's shape (#1235).
    /// </summary>
    private TypeSyntax MapClassifiedTypeOperand(TypeAnnotation annotation)
        => _context.SemanticInfo?.GetTypeTestLowering(annotation) is { } lowering
            ? MapTypeTestTarget(lowering)
            : _typeMapper.MapType(annotation);

}
