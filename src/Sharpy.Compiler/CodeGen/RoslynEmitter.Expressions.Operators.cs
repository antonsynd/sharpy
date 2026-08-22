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
                    if (_context.Ir?.Index.TryGetValue(binOp, out var foldedNode) == true
                        && foldedNode is IrConstant foldedConst)
                    {
                        var foldedPow = (long)foldedConst.Value;
                        return GetExpressionSemanticType(binOp) == SemanticType.Long
                            ? LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(foldedPow))
                            : LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)foldedPow));
                    }

                    // x ** y → ONE invocation, each operand spliced once (#1228): integer
                    // operands take Sharpy.Builtins.CheckedIntPow, everything else Math.Pow.
                    // Both decisions live in the routing wrapper this site shares with the
                    // augmented `**=` site (#1227), so the two cannot drift — the same
                    // arrangement `//` and `%` already use.
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
                    return GeneratePowerValue(
                        left, right, binOp.Left, binOp.Right, GetExpressionSemanticType(binOp));
                }

            case BinaryOperator.Divide:
                // User-defined/generic types with operator/: emit plain left / right (C# operator overload)
                var leftDivType = _context.SemanticInfo?.GetExpressionType(binOp.Left);
                var rightDivType = _context.SemanticInfo?.GetExpressionType(binOp.Right);
                if (leftDivType is UserDefinedType or GenericType || rightDivType is UserDefinedType or GenericType)
                    return BinaryExpression(SyntaxKind.DivideExpression, left, right);

                // x / y → true division with Python semantics (always returns float64)
                // Cast at least one operand to double to ensure float result
                // If either operand is already float, the division will naturally produce float
                // Decimal division: no cast needed, C# decimal / decimal works natively
                if (IsDecimalExpression(binOp.Left) || IsDecimalExpression(binOp.Right))
                    return BinaryExpression(SyntaxKind.DivideExpression, left, right);
                var leftIsFloat = IsFloatExpression(binOp.Left);
                var rightIsFloat = IsFloatExpression(binOp.Right);
                if (!leftIsFloat && !rightIsFloat)
                {
                    // Both operands are integers: cast left to double
                    return BinaryExpression(SyntaxKind.DivideExpression,
                        CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
                        right);
                }
                // At least one operand is float, so result will be float naturally
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
                // x is y → object.ReferenceEquals(x, y)
                // Special optimization for None: x is None
                if (binOp.Right is NoneLiteral)
                {
                    // For Optional<T> (struct): emit x.IsNone (property access)
                    if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsNone"));
                    }
                    // For nullable/reference types: x == null
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
                // x is not y → !object.ReferenceEquals(x, y)
                // Special optimization for None: x is not None
                if (binOp.Right is NoneLiteral)
                {
                    // For Optional<T> (struct): emit x.IsSome (property access)
                    if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                    {
                        return MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("IsSome"));
                    }
                    // For nullable/reference types: x != null
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
                // x ?? y — for Optional<T>, lower to UnwrapOr or ternary
                if (GetExpressionSemanticType(binOp.Left) is OptionalType)
                {
                    if (GetExpressionSemanticType(binOp.Right) is OptionalType)
                    {
                        // Both Optional: safeLeft.IsSome ? safeLeft : right
                        // Ensure left is only evaluated once for complex expressions
                        var (safeLeft, captureLeft) = EnsureSingleEvaluation(left, binOp.Left);
                        ExpressionSyntax coalesceCondition = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            ParenthesizedExpression(safeLeft), IdentifierName("IsSome"));
                        if (captureLeft != null)
                            coalesceCondition = BinaryExpression(SyntaxKind.LogicalAndExpression, captureLeft, coalesceCondition);
                        return ConditionalExpression(coalesceCondition, safeLeft, right);
                    }
                    // Left Optional, right raw T: left.UnwrapOr(right)
                    // UnwrapOr only evaluates left once (method call on the struct)
                    return InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            ParenthesizedExpression(left), IdentifierName("UnwrapOr")))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(right))));
                }
                // For nullable/reference types: C# ?? operator
                return BinaryExpression(SyntaxKind.CoalesceExpression, left, right);

            case BinaryOperator.Multiply:
                {
                    // String repetition: str * int, int * str, str * long
                    var leftMulType = GetExpressionSemanticType(binOp.Left);
                    var rightMulType = GetExpressionSemanticType(binOp.Right);
                    if (leftMulType == SemanticType.Str || rightMulType == SemanticType.Str)
                    {
                        // Ensure string is always the first argument
                        var strArg = leftMulType == SemanticType.Str ? left : right;
                        var countArg = leftMulType == SemanticType.Str ? right : left;
                        return InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                                IdentifierName("Repeat")))
                            .AddArgumentListArguments(
                                Argument(strArg),
                                Argument(countArg));
                    }
                    // Not string repetition — fall through to standard multiply
                    break;
                }

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

            // Logical (with short-circuit)
            BinaryOperator.And => SyntaxKind.LogicalAndExpression,
            BinaryOperator.Or => SyntaxKind.LogicalOrExpression,

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

        // Honor the semantic-recorded NoneCheck lowering (#901): `x == None` / `x != None`
        // on reference-semantics types lowers to a C# null pattern check (`x is null` /
        // `x is not null`). This bypasses any overloaded op_Equality and matches Python's
        // identity fallback (a live object == None is False). Operand order is irrelevant —
        // detect the non-None side from the AST.
        // The literal-shape guard `(Left is NoneLiteral) != (Right is NoneLiteral)` is an
        // invariant assertion, mirroring the one in ControlFlow.cs: the #911 semantic gate
        // (SPY0329) rejects any non-literal VoidType comparison operand before lowering, so
        // a NoneCheck lowering always has exactly one NoneLiteral operand. The guard is
        // defense-in-depth — a future regression that violated the invariant would fall
        // through to the native `==`/`!=` operator (a loud C# compile error) rather than
        // silently dropping the non-literal operand.
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            && (binOp.Left is NoneLiteral) != (binOp.Right is NoneLiteral)
            && GetIrBinaryOpLowering(binOp) == BinaryOpLowering.NoneCheck)
        {
            var operand = binOp.Left is NoneLiteral ? right : left;
            PatternSyntax nullPattern = ConstantPattern(
                LiteralExpression(SyntaxKind.NullLiteralExpression));
            if (kind == SyntaxKind.NotEqualsExpression)
                nullPattern = UnaryPattern(Token(SyntaxKind.NotKeyword), nullPattern);
            return IsPatternExpression(operand, nullPattern);
        }

        // For == and != on generic type parameters, use EqualityComparer<T>.Default.Equals()
        // because C# does not allow == on unconstrained generic types
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            var leftType = GetExpressionSemanticType(binOp.Left);
            var rightType = GetExpressionSemanticType(binOp.Right);
            if (leftType is Semantic.TypeParameterType || rightType is Semantic.TypeParameterType)
            {
                // EqualityComparer<T>.Default.Equals(left, right)
                var typeParamType = (leftType as Semantic.TypeParameterType ?? rightType as Semantic.TypeParameterType)!;
                var equalsCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            GenericName("EqualityComparer")
                                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(
                                    (TypeSyntax)IdentifierName(typeParamType.Name)))),
                            IdentifierName("Default")),
                        IdentifierName("Equals")))
                    .AddArgumentListArguments(Argument(left), Argument(right));

                return kind == SyntaxKind.NotEqualsExpression
                    ? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(equalsCall))
                    : (ExpressionSyntax)equalsCall;
            }
        }

        // Honor the semantic-recorded Equals-call lowering (#886): tuples and CLR types that
        // implement Equals/IEquatable but define no op_Equality. A native C# == would either be
        // reference equality (wrong) or fail to compile (struct without op_Equality). The
        // instance-vs-static choice was materialized by the TypeChecker; the emitter switches on
        // the tag alone (value types -> left.Equals(right); reference types -> object.Equals(...)).
        var equalsLowering = GetIrBinaryOpLowering(binOp);
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            && equalsLowering is BinaryOpLowering.EqualsCallInstance or BinaryOpLowering.EqualsCallStatic)
        {
            ExpressionSyntax equalsInvocation = equalsLowering == BinaryOpLowering.EqualsCallInstance
                ? InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            IdentifierName("Equals")))
                    .AddArgumentListArguments(Argument(right))
                : InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                            IdentifierName("Equals")))
                    .AddArgumentListArguments(Argument(left), Argument(right));

            return kind == SyntaxKind.NotEqualsExpression
                ? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(equalsInvocation))
                : equalsInvocation;
        }

        // For <, >, <=, >= on strings, emit string.Compare(left, right, StringComparison.Ordinal) <op> 0
        // because System.String does not define relational operators
        if (kind is SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression)
        {
            var leftCmpType = GetExpressionSemanticType(binOp.Left);
            var rightCmpType = GetExpressionSemanticType(binOp.Right);
            if (leftCmpType == SemanticType.Str && rightCmpType == SemanticType.Str)
            {
                // string.Compare(left, right, System.StringComparison.Ordinal)
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
        }

        // For <, >, <=, >= on generic type parameters with IComparable constraint,
        // emit x.CompareTo(y) <op> 0 because C# does not allow relational operators
        // on unconstrained generic types
        if (kind is SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression)
        {
            var leftType = GetExpressionSemanticType(binOp.Left);
            var rightType = GetExpressionSemanticType(binOp.Right);
            if ((leftType is Semantic.TypeParameterType leftTp && HasComparableConstraint(leftTp))
                || (rightType is Semantic.TypeParameterType rightTp && HasComparableConstraint(rightTp)))
            {
                // left.CompareTo(right) <op> 0
                var compareToCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        left,
                        IdentifierName("CompareTo")))
                    .AddArgumentListArguments(Argument(right));

                return BinaryExpression(kind,
                    compareToCall,
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
            }
        }

        // Shift operators: C# requires the count to be int. When the semantic type of the
        // right operand is wider (long), emit an explicit cast (#1315).
        if (kind is SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression)
        {
            var rightType = _context.SemanticInfo?.GetExpressionType(binOp.Right);
            if (rightType is BuiltinType { ClrType: var clr } && clr != typeof(int))
            {
                right = CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    ParenthesizedExpression(right));
            }
        }

        return BinaryExpression(kind, left, right);
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
        // Minus over an integer literal: emit a single literal token from the semantic type
        // so -2147483648 is int.MinValue, not -(2147483648L) which is CS0266 (#1304).
        if (unaryOp.Operator == UnaryOperator.Minus && unaryOp.Operand is IntegerLiteral il)
        {
            var semanticType = _context.SemanticInfo?.GetExpressionType(unaryOp);
            if (semanticType is BuiltinType bt)
            {
                var text = il.Value.Replace("_", "", StringComparison.Ordinal);
                try
                {
                    var ulongMagnitude = ParseIntegerText(text);
                    if (bt.ClrType == typeof(int) && ulongMagnitude <= (ulong)int.MaxValue + 1)
                    {
                        return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                            Literal(-(int)(long)ulongMagnitude));
                    }
                    if (bt.ClrType == typeof(long))
                    {
                        if (ulongMagnitude == (ulong)long.MaxValue + 1)
                            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(long.MinValue));
                        return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                            Literal(-(long)ulongMagnitude));
                    }
                }
                catch (OverflowException)
                {
                    // Fall through to normal path
                }
            }
        }

        var operand = GenerateExpression(unaryOp.Operand);

        var kind = unaryOp.Operator switch
        {
            UnaryOperator.Plus => SyntaxKind.UnaryPlusExpression,
            UnaryOperator.Minus => SyntaxKind.UnaryMinusExpression,
            UnaryOperator.Not => SyntaxKind.LogicalNotExpression,
            UnaryOperator.BitwiseNot => SyntaxKind.BitwiseNotExpression,
            _ => SyntaxKind.None
        };

        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: unary operator '{unaryOp.Operator}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator, unaryOp.LineStart, unaryOp.ColumnStart);
        }

        // Wrap binary expressions in parentheses when negated to avoid precedence issues.
        // e.g., `not isinstance(x, T)` → `!(x is T)` not `!x is T`
        if (kind == SyntaxKind.LogicalNotExpression && operand is BinaryExpressionSyntax)
        {
            operand = ParenthesizedExpression(operand);
        }

        return PrefixUnaryExpression(kind, operand);
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c
        // Python guarantees intermediate expressions are evaluated exactly once.
        // For non-trivial intermediate expressions (function calls, member access, etc.),
        // we use the C# "is var" pattern to capture the value inline:
        //   a < (f() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < c

        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            throw new InvalidOperationException("Invalid comparison chain");
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

            var comparison = BinaryExpression(kind, left, right);

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

    /// <summary>
    /// Returns true if the type parameter has an IComparable constraint.
    /// Matches IComparable, IComparable[T], System.IComparable, etc.
    /// </summary>
    private static bool HasComparableConstraint(Semantic.TypeParameterType typeParam)
    {
        foreach (var constraint in typeParam.Constraints)
        {
            if (constraint is TypeConstraint tc
                && (tc.Type.Name == "IComparable" || tc.Type.Name == "System.IComparable"
                    || tc.Type.Name.StartsWith("IComparable<", System.StringComparison.Ordinal)
                    || tc.Type.Name.StartsWith("System.IComparable<", System.StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }
}
