using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Binary/unary operators, comparison chains,
/// conditional expressions, type casts/coercions/checks, pipe forward, string repetition
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Reads the equality lowering strategy for a <c>==</c>/<c>!=</c> operation from the lowering IR
    /// (E2 #1056, migrates <c>_binaryOpLowerings</c>). An <c>==</c>/<c>!=</c> always lowers to an
    /// <see cref="IrEqualityComparison"/>, so its node is required
    /// (<see cref="RequiredIrNodeFor{TIr}"/>); any other operator has none and returns <c>null</c>,
    /// which callers treat the same as the default native operator.
    /// </summary>
    private BinaryOpLowering? GetIrBinaryOpLowering(BinaryOp binaryOp)
    {
        return binaryOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual
            ? RequiredIrNodeFor<IrEqualityComparison>(binaryOp)?.Strategy
            : null;
    }

    /// <summary>
    /// The subject of an <c>is None</c> / <c>is not None</c> test, whichever side the None literal is
    /// on (#2171): the generated operand opposite the literal, or <c>null</c> when the node is not a
    /// None test. A right-hand literal wins when both sides are literals (<c>None is None</c>), which
    /// keeps that spelling's lowering unchanged.
    /// </summary>
    private static ExpressionSyntax? NoneTestSubject(
        Expression leftNode, Expression rightNode, ExpressionSyntax left, ExpressionSyntax right)
        => rightNode is NoneLiteral ? left
            : leftNode is NoneLiteral ? right
            : null;

    /// <summary>
    /// The lowering of one <c>is None</c> / <c>is not None</c> test, read from the kind the checker
    /// recorded for it (<c>TypeChecker.ClassifyNoneTest</c>, #2224 ruling R-EJ: <c>is</c> never runs
    /// user code). Every recorded arm is a form that cannot bind a user <c>operator ==</c>:
    /// <c>.IsNone</c>/<c>.IsSome</c> on an Optional, the null pattern on a C#-nullable subject, the
    /// null pattern on the boxed subject otherwise. Only the unrecorded <c>None is None</c> keeps
    /// <c>null == null</c> — two null literals, a C# constant, no operator in reach. The binary
    /// operator passes the kind recorded on its node, a chain link its
    /// <see cref="ComparisonLinkLowering.Kind"/> (P93 D3: a link is its binary twin).
    /// </summary>
    private static ExpressionSyntax GenerateNoneTest(OperatorLoweringKind kind, ExpressionSyntax subject, bool negated)
    {
        PatternSyntax NullPattern()
        {
            PatternSyntax pattern = ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression));
            return negated ? UnaryPattern(Token(SyntaxKind.NotKeyword), pattern) : pattern;
        }

        switch (kind)
        {
            case OperatorLoweringKind.OptionalNoneTest:
                return Member(subject, negated ? "IsSome" : "IsNone");
            case OperatorLoweringKind.ReferenceNoneTest:
                return IsPattern(subject, NullPattern());
            case OperatorLoweringKind.BoxedNoneTest:
                return IsPattern(Cast(PredefinedType(Token(SyntaxKind.ObjectKeyword)), subject), NullPattern());
            default:
                return Binary(negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression,
                    subject,
                    LiteralExpression(SyntaxKind.NullLiteralExpression));
        }
    }

    /// <summary>
    /// The lowering of one <c>is</c> / <c>is not</c> between two values (neither the None literal),
    /// read from the kind the checker recorded (<c>TypeChecker.ClassifyEnumIdentity</c>, #2669) — the
    /// ONE emission the binary operator and the enum-member type-test spelling
    /// (<see cref="GenerateTypeCheck"/>) share:
    /// <list type="bullet">
    /// <item><see cref="OperatorLoweringKind.EnumIdentityEquals"/> — the native <c>l == r</c> /
    /// <c>l != r</c> of one int-backed enum type (lifted over <c>E?</c>);</item>
    /// <item><see cref="OperatorLoweringKind.EnumIdentityObjectEquals"/> —
    /// <c>global::Sharpy.EnumHelpers.Identity(l, r)</c>, negated for <c>is not</c>;</item>
    /// <item>anything else — <c>object.ReferenceEquals(l, r)</c>, negated for <c>is not</c>.</item>
    /// </list>
    /// Each operand syntax is spliced exactly once in every arm.
    /// </summary>
    private static ExpressionSyntax GenerateIdentityComparison(
        OperatorLoweringKind kind, ExpressionSyntax left, ExpressionSyntax right, bool negated)
    {
        if (kind == OperatorLoweringKind.EnumIdentityEquals)
        {
            return Binary(negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression, left, right);
        }

        ExpressionSyntax comparer = kind == OperatorLoweringKind.EnumIdentityObjectEquals
            ? MakeGlobalQualifiedName("Sharpy", "EnumHelpers", "Identity")
            : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                IdentifierName("ReferenceEquals"));
        ExpressionSyntax call = InvocationExpression(comparer)
            .AddArgumentListArguments(
                Argument(left),
                Argument(right));
        return negated ? Prefix(SyntaxKind.LogicalNotExpression, call) : call;
    }

    /// <summary>
    /// Whether <paramref name="kind"/> is one of the identity lowerings the checker records for an
    /// <c>is</c> with an enum operand (#2669): the comparison is then a VALUE comparison, which a
    /// reference assertion (<c>Xunit.Assert.Same</c>) cannot observe.
    /// </summary>
    private static bool IsEnumIdentityLowering(OperatorLoweringKind kind)
        => kind is OperatorLoweringKind.EnumIdentityEquals
            or OperatorLoweringKind.EnumIdentityObjectEquals
            or OperatorLoweringKind.EnumMemberReferenceIdentity;

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
        // it can push into the enclosing sink, which is flushed unconditionally — so a
        // speculative generation is a duplicated side effect waiting for the right operand
        // (#1228's rule, found live by the re-entry tripwire, #1334).
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            // x |> f → f(x); x |> f(y) → f(x, y) (prepend to argument list)
            return GeneratePipeForward(binOp.Left, binOp.Right);
        }

        if (binOp.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            return GenerateShortCircuitOp(binOp);
        }

        // `??` evaluates its right operand ONLY when the left is absent, so it owns a conditional
        // evaluation and needs its own sink — decided before the operands are generated, like
        // and/or above (plan-0667c5 Design Decision 6).
        if (binOp.Operator == BinaryOperator.NullCoalesce)
        {
            return GenerateNullCoalesceOp(binOp);
        }

        // `in` / `not in` order their operands themselves (P93 D2, #2645): a recorded needle is hoisted
        // ahead of a hoisting container into a local of its binding type, not a `var` capture.
        if (binOp.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            return GenerateMembershipOperator(binOp);
        }

        // Left-to-right: a hoist producer in the right operand must not move its effects ahead
        // of an effectful left operand (plan-0667c5 Design Decision 6, ordering axis).
        var operandsInOrder = GenerateExpressionsInOrder(new[] { binOp.Left, binOp.Right });
        var left = operandsInOrder[0];
        var right = operandsInOrder[1];

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
                    if (IrNodeFor<IrConstant>(binOp) is { } foldedConst)
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
                    // GenerateExpression is not pure: it can push into the enclosing sink
                    // (the #1198 tuple-spread hoist), so `sum(make_tuple()) ** 2` emitted the
                    // hoist twice and called make_tuple() twice. And when the gate DID fire it
                    // silently degraded the lowering to the saturating `(int)Math.Pow` cast, so
                    // `x ** f()` had different overflow behaviour from `x ** y` — a spelling
                    // difference changing semantics. Both spellings now raise OverflowError.
                    return GeneratePowerValue(left, right, binOp);
                }

            case BinaryOperator.Divide:
                // x / y → Builtins.TrueDiv (zero-guarded, #2517) for numeric operands, native `/`
                // for user/CLR operator types — read from the recorded tag inside the routing
                // wrapper this site shares with the augmented `/=` site.
                return GenerateTrueDivideValue(left, right, binOp);

            case BinaryOperator.FloorDivide:
                // x // y → floor division with Python semantics (toward negative infinity).
                // One Builtins.FloorDiv invocation for integer AND float operands (#1226) —
                // guard and dispatch live in the helper, each operand spliced once.
                // Decimal operands: native truncating quotient. Which arm is the recorded
                // OperatorLowering tag on this node (#1658), read inside the wrapper that both
                // this site and the augmented `//=` site share.
                return GenerateFloorDivideValue(left, right, binOp);

            case BinaryOperator.Modulo:
                // x % y → Python floored modulo (result sign = divisor sign) for int/long/
                // float32/float64 operands: C#'s native `%` takes the sign of the dividend, which
                // diverges from Python. Decimal keeps the native truncating remainder but routes
                // through a zero-divisor guard (#1189). Which arm is the recorded OperatorLowering
                // tag on this node (#1658), read inside the routing wrapper this site shares with
                // the augmented `%=` site, so the two cannot drift. User types with `__mod__`
                // (→ operator %) and other CLR `op_Modulus` types have no record, get null back,
                // and MUST keep the native ModuloExpression map below.
                var moduloValue = GenerateModuloValue(left, right, binOp);
                if (moduloValue != null)
                    return moduloValue;
                break;

            case BinaryOperator.MatMul:
                // x @ y → x.MatMul(y). C# has no `@` operator, so matrix multiplication
                // (PEP 465) dispatches to the MatMul instance method that both user-defined
                // __matmul__ methods and stdlib NdArray expose. See DunderNameMapping.
                return GenerateMatMulCall(left, right);

            case BinaryOperator.Is:
                // `x is None` and `None is x` are one test (#2171): the subject is the operand on the
                // side that is NOT the None literal, and the recorded None-test kind — which the
                // checker records for EITHER side — selects the arm (#2224: never `x == null`, which
                // bound a user operator ==). Keying the arm on `Right is NoneLiteral` alone sent
                // `None is x` on an Optional to ReferenceEquals on a boxed struct: always false.
                if (NoneTestSubject(binOp.Left, binOp.Right, left, right) is { } isNoneSubject)
                    return GenerateNoneTest(OperatorLoweringOf(binOp).Kind, isNoneSubject, negated: false);
                return GenerateIdentityComparison(OperatorLoweringOf(binOp).Kind, left, right, negated: false);

            case BinaryOperator.IsNot:
                // Mirror of the Is arm (#2171): `None is not x` is `x is not None`.
                if (NoneTestSubject(binOp.Left, binOp.Right, left, right) is { } isNotNoneSubject)
                    return GenerateNoneTest(OperatorLoweringOf(binOp).Kind, isNotNoneSubject, negated: true);
                return GenerateIdentityComparison(OperatorLoweringOf(binOp).Kind, left, right, negated: true);

            case BinaryOperator.Multiply:
                {
                    // String repetition: the tag says which operand is the string (#1623). With the
                    // string on the right the count goes first (P93 D2, #2645): Core's count-first
                    // overload keeps the left operand's evaluation ahead of the right's.
                    var repeatKind = OperatorLoweringOf(binOp).Kind;
                    if (repeatKind == OperatorLoweringKind.StringRepeatStrLeft)
                        return GenerateStringRepeat(left, right);
                    if (repeatKind == OperatorLoweringKind.StringRepeatStrRight)
                        return GenerateCountFirstStringRepeat(left, right);
                    break;
                }

        }

        // and/or moved to GenerateShortCircuitOp above

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
                OperatorLoweringOf(binOp).Kind,
                GetIrBinaryOpLowering(binOp) ?? BinaryOpLowering.NativeOperator);
        }

        if (OperatorLoweringOf(binOp).Kind
            == OperatorLoweringKind.ShiftCountCastToInt)
        {
            right = CastExpression(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                ParenthesizedExpression(right));
        }

        // The parser groups by Python precedence; C# ranks `&`/`|`/`^` below `==` where Python ranks
        // them above, so `a & b == c` must print as `(a & b) == c` — the seam decides per edge.
        return Binary(kind, left, right);
    }

    /// <summary>
    /// The membership test of <c>x in y</c> (the caller negates it for <c>not in</c>), selected by
    /// the operator lowering <c>TypeChecker.ClassifyMembership</c> recorded on the node (#2635,
    /// plan-4e2867 D1/D2). The emitter decides nothing: it switches on the recorded kind.
    /// <list type="bullet">
    /// <item><see cref="OperatorLoweringKind.MembershipNoneIsFalse"/> →
    /// <c>global::Sharpy.Builtins.ContainsOptional(x, y, static (c, n) =&gt; c.Contains(n))</c>.</item>
    /// <item><see cref="OperatorLoweringKind.MembershipNoneRaises"/> →
    /// <c>global::Sharpy.Builtins.ContainsNoneRaises(x, y, static (c, n) =&gt; c.Contains(n))</c>.</item>
    /// <item>anything else → <c>y.Contains(x)</c>.</item>
    /// </list>
    /// The lambda body is <see cref="GenerateContainsCall"/> over the lambda's own parameters — the
    /// ONE spelling of the container test, so the helper form and the plain form cannot drift (the
    /// tuple array bridge and the dict key view of #1771 apply inside the lambda, to its container
    /// parameter). The needle is argument 1 and the container argument 2, so a helper cell
    /// evaluates <c>x</c> before <c>y</c>, as Python does (D4). The lambda is <c>static</c>: it
    /// captures nothing, so it allocates no closure.
    /// <para>The plain form reads its container first, so a recorded needle goes through
    /// <see cref="BindMembershipNeedle"/> (P93 D2, #2645). One emission for the binary operator and
    /// every chain link (P93 D3): <paramref name="kind"/> is the recorded kind, the operand nodes are
    /// the needle and the container, and <paramref name="needleEvaluated"/> says the needle syntax
    /// is a local its caller already bound (a hoist ahead of the container, a chain capture).
    /// <paramref name="negated"/> negates the whole test, the bind included.</para>
    /// </summary>
    private ExpressionSyntax GenerateMembershipTest(
        OperatorLoweringKind kind, Expression needleNode, Expression containerNode,
        ExpressionSyntax needle, ExpressionSyntax container, bool negated, bool needleEvaluated)
    {
        string helper;
        switch (kind)
        {
            case OperatorLoweringKind.MembershipNoneIsFalse:
                helper = "ContainsOptional";
                break;
            case OperatorLoweringKind.MembershipNoneRaises:
                helper = "ContainsNoneRaises";
                break;
            default:
                {
                    var binding = needleEvaluated ? null : BoundNeedleType(needleNode, containerNode);
                    var plain = binding != null
                        ? BindMembershipNeedle(binding, containerNode, needle, container)
                        : GenerateContainsCall(containerNode, container, needle);
                    return negated ? Prefix(SyntaxKind.LogicalNotExpression, plain) : plain;
                }
        }

        // Synthesized, counter-suffixed names (the `__callable_p_N` convention): no user identifier
        // is spelled with a leading double underscore and a numeric suffix the emitter hands out.
        var containerParam = GenerateTempVarName("in_container");
        var needleParam = GenerateTempVarName("in_needle");
        var test = ParenthesizedLambdaExpression(
                ParameterList(SeparatedList(new[]
                {
                    Parameter(Identifier(containerParam)),
                    Parameter(Identifier(needleParam))
                })),
                GenerateContainsCall(containerNode, IdentifierName(containerParam), IdentifierName(needleParam)))
            .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)));

        ExpressionSyntax call = InvocationExpression(MakeGlobalQualifiedName("Sharpy", "Builtins", helper))
            .AddArgumentListArguments(Argument(needle), Argument(container), Argument(test));
        // `not in`: the helper raises (or answers) before the negation.
        return negated ? Prefix(SyntaxKind.LogicalNotExpression, call) : call;
    }

    /// <summary>
    /// The binary <c>x in y</c> / <c>x not in y</c>: its operands in source order
    /// (<see cref="GenerateMembershipOperands"/>), then the one membership emission.
    /// </summary>
    private ExpressionSyntax GenerateMembershipOperator(BinaryOp binOp)
    {
        var (needle, container, needleEvaluated) = GenerateMembershipOperands(binOp.Left, binOp.Right);
        return GenerateMembershipTest(
            OperatorLoweringOf(binOp).Kind, binOp.Left, binOp.Right, needle, container,
            negated: binOp.Operator == BinaryOperator.NotIn, needleEvaluated);
    }

    /// <summary>
    /// The operands of a binary membership test, needle first (P93 D2, #2645). A bound needle
    /// (<see cref="BoundNeedleType"/>) beside a container that hoists statements (a comprehension, a
    /// spread, <c>?</c>, a walrus) is hoisted ahead of those statements into a local of its binding
    /// type, so the needle is evaluated first and keeps the type its argument position gave it (a
    /// <c>var</c> capture cannot type a conditional of narrower constants, a <c>None</c> arm, a lambda,
    /// a function name), and a bare name is read before the container rebinds it. Any other needle
    /// takes the shared operand order (<see cref="GenerateExpressionsInOrder"/>) — today's emission.
    /// </summary>
    private (ExpressionSyntax Needle, ExpressionSyntax Container, bool NeedleEvaluated) GenerateMembershipOperands(
        Expression needleNode, Expression containerNode)
    {
        var binding = BoundNeedleType(needleNode, containerNode);
        if (binding == null)
        {
            var inOrder = GenerateExpressionsInOrder(new[] { needleNode, containerNode });
            return (inOrder[0], inOrder[1], false);
        }

        ExpressionSyntax needle = null!;
        ExpressionSyntax container = null!;
        var needleEvals = WithEvaluationSink(() => needle = GenerateExpression(needleNode));
        var containerEvals = WithEvaluationSink(() => container = GenerateExpression(containerNode));

        foreach (var stmt in needleEvals)
            HoistEvaluation(stmt);

        var needleEvaluated = false;
        if (containerEvals.Count > 0)
        {
            needle = HoistBoundNeedle(binding, needle, GenerateTempVarName("in"));
            needleEvaluated = true;
        }

        foreach (var stmt in containerEvals)
            HoistEvaluation(stmt);

        return (needle, container, needleEvaluated);
    }

    /// <summary>
    /// The type a membership needle is bound as before its container is read (P93 D2, #2645), spelled
    /// by the type mapper, or null when it is not bound. Read from the checker's record
    /// (<see cref="SemanticInfo.TryGetMembershipNeedleBinding"/>: none for a slot-less container,
    /// #2249, or a needle only the CLR call seam takes, #2647). A recorded needle is still not bound
    /// when its order cannot be observed: a literal-only needle (<see cref="IsLiteralOnly"/>: it has
    /// no evaluation), or two trivial operands (<see cref="IsTrivialExpression"/>). A bare name IS
    /// bound beside a container that is not trivial: the container may rebind it.
    /// </summary>
    private TypeSyntax? BoundNeedleType(Expression needleNode, Expression containerNode)
    {
        if (IsLiteralOnly(needleNode)
            || (IsTrivialExpression(needleNode) && IsTrivialExpression(containerNode)))
        {
            return null;
        }

        return RecordedNeedleBindingType(needleNode);
    }

    /// <summary>
    /// The checker's recorded binding of a membership needle (<see cref="SemanticInfo.TryGetMembershipNeedleBinding"/>),
    /// spelled by the type mapper, with none of <see cref="BoundNeedleType"/>'s exemptions: the cast a
    /// chain's captured middle needle takes at its membership read (P93 D3, owner ruling R2).
    /// </summary>
    private TypeSyntax? RecordedNeedleBindingType(Expression needleNode)
        => _context.SemanticInfo is { } info && info.TryGetMembershipNeedleBinding(needleNode, out var binding)
            ? _typeMapper.MapSemanticType(binding)
            : null;

    /// <summary>
    /// A literal, or operators over literals (<c>-1</c>, <c>(2 + 3)</c>): an operand with no
    /// evaluation, whose order beside its container cannot be observed (P93 D2). Deliberately
    /// partial: every other form answers <c>false</c> and is bound, which is always correct. An
    /// operator that can raise over literals (<c>1 // 0</c>, <c>1 % 0</c>, <c>1 / 0</c>, <c>**</c>) is
    /// an evaluation whose exception must precede the container's (#2645), so it is not literal-only.
    /// </summary>
    private static bool IsLiteralOnly(Expression expr) => expr switch
    {
        IntegerLiteral or FloatLiteral or StringLiteral or BytesLiteralExpression or BooleanLiteral
            or NoneLiteral or EllipsisLiteral => true,
        Parenthesized parenthesized => IsLiteralOnly(parenthesized.Expression),
        UnaryOp unary => IsLiteralOnly(unary.Operand),
        BinaryOp binary => !CanRaiseOverLiterals(binary.Operator)
            && IsLiteralOnly(binary.Left) && IsLiteralOnly(binary.Right),
        _ => false,
    };

    /// <summary>
    /// A binary operator whose value over literal operands can be an exception: a division by zero
    /// (<c>/</c>, <c>//</c>, <c>%</c>) or a power that raises (<c>0 ** -1</c>) (P93 D2, #2645).
    /// </summary>
    private static bool CanRaiseOverLiterals(BinaryOperator op) => op is BinaryOperator.Divide
        or BinaryOperator.FloorDivide or BinaryOperator.Modulo or BinaryOperator.Power;

    /// <summary>
    /// Hoists a bound needle into <c>Binding name = needle;</c> and returns the local — the needle
    /// evaluated ahead of a container that hoists (P93 D2). The declared type is the recorded binding
    /// type, so the local has the type the argument position gave the needle.
    /// </summary>
    private ExpressionSyntax HoistBoundNeedle(TypeSyntax binding, ExpressionSyntax needle, string name)
    {
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(binding)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(name))
                        .WithInitializer(EqualsValueClause(needle))))));
        return IdentifierName(name);
    }

    /// <summary>
    /// THE needle-first bind of a membership test whose emitted form reads its container first
    /// (P93 D2, #2645): <c>((Binding)x is var t &amp;&amp; y.Contains(t))</c>. The cast gives C# the
    /// type the argument position gave the needle; since a <c>T | None</c> needle's binding is its own
    /// type, the cast never unwraps one. A var pattern always matches, so the container's test always
    /// runs. When the container test itself hoists a statement (a tuple's array bridge captures a
    /// non-name tuple), the needle is hoisted first, into a local of its binding type, and the test
    /// reads the local.
    /// </summary>
    private ExpressionSyntax BindMembershipNeedle(
        TypeSyntax binding, Expression containerNode, ExpressionSyntax needle, ExpressionSyntax container)
    {
        var bound = GenerateTempVarName("in");
        ExpressionSyntax test = null!;
        var containerTestEvals = WithEvaluationSink(
            () => test = GenerateContainsCall(containerNode, container, IdentifierName(bound)));

        if (containerTestEvals.Count > 0)
        {
            HoistBoundNeedle(binding, needle, bound);
            foreach (var stmt in containerTestEvals)
                HoistEvaluation(stmt);
            return test;
        }

        // Parenthesized: the bind is one primary operand wherever it stands (a chain's `&&`, a `!`).
        return ParenthesizedExpression(BinaryExpression(
            SyntaxKind.LogicalAndExpression,
            IsPattern(
                Cast(binding, needle),
                VarPattern(SingleVariableDesignation(Identifier(bound)))),
            test));
    }

    /// <summary>
    /// <c>container.Contains(needle)</c>, with the iterable projection recorded on the container's
    /// AST node applied to <paramref name="container"/> so tuple/dict containers are bridged to an
    /// <c>IEnumerable</c> with <c>Contains</c> (#1771). Shared by the plain membership test and the
    /// None-needle helper's lambda (<see cref="GenerateMembershipTest"/>).
    /// <para>A container the checker answers by iteration (P93 D1, R-NZ: a Sharpy class with
    /// <c>__iter__</c> and no <c>__contains__</c>) carries a <c>Direct</c> projection of its element,
    /// and is tested by LINQ over the <c>IEnumerable&lt;E&gt;</c> the class implements, named in full —
    /// <c>global::System.Linq.Enumerable.Contains&lt;E&gt;(container, needle)</c> — so a member the
    /// class happens to call <c>contains</c> (emitted <c>Contains</c>) is never bound.</para>
    /// </summary>
    private ExpressionSyntax GenerateContainsCall(
        Expression containerNode, ExpressionSyntax container, ExpressionSyntax needle)
    {
        if (_context.SemanticInfo?.GetIterableProjection(containerNode) is
            { Kind: IterableProjectionKind.Direct } iterated)
        {
            return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("System", "Linq", "Enumerable"),
                        GenericName(Identifier("Contains"))
                            .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(
                                _typeMapper.MapSemanticType(iterated.ElementType))))))
                .AddArgumentListArguments(Argument(container), Argument(needle));
        }

        var projected = ApplyIterableProjection(containerNode, container);
        return InvocationExpression(Member(projected, "Contains"))
            .AddArgumentListArguments(Argument(needle));
    }

    /// <summary>
    /// <c>global::Sharpy.StringHelpers.Repeat(str, count)</c> — the string-repetition lowering shared
    /// by the binary <c>*</c> and the augmented <c>*=</c> sites when the string is the left operand
    /// (<see cref="OperatorLoweringKind.StringRepeatStrLeft"/>, #1623).
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
    /// <c>global::Sharpy.StringHelpers.Repeat(count, str)</c> — <c>n * s</c> with the string on the
    /// right (<see cref="OperatorLoweringKind.StringRepeatStrRight"/>): Core's count-first overload
    /// (<c>int</c> and <c>long</c>), so the left operand is evaluated first (P93 D2, #2645).
    /// </summary>
    private ExpressionSyntax GenerateCountFirstStringRepeat(ExpressionSyntax count, ExpressionSyntax str)
    {
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                IdentifierName("Repeat")))
            .AddArgumentListArguments(
                Argument(count),
                Argument(str));
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
        return InvocationExpression(Member(left, methodName))
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

            // Delegate to shared call-site reordering with the piped value prepended. Passing the
            // un-generated expression lets it participate in left-to-right operand ordering with the
            // call's own arguments (#1853).
            var allArgs = GenerateReorderedCallArguments(funcCall, pipeFuncSymbol, leftExpr);

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
            var left = GenerateExpression(leftExpr);
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
        var singleArg = GenerateExpression(leftExpr);
        var right = GeneratePipeCallTarget(rightExpr);
        return InvocationExpression(right)
            .AddArgumentListArguments(Argument(singleArg));
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
            if (isBuiltin && symbol is FunctionSymbol opFs && GetCodeGenInfo(opFs) != null)
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
            var negateKind = OperatorLoweringOf(unaryOp).Kind;
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
            return Prefix(SyntaxKind.LogicalNotExpression, wrapped);
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

        return Prefix(kind, operand);
    }

    /// <summary>The token a swapped ordering comparison prints: <c>&lt;</c> ↔ <c>&gt;</c>, <c>&lt;=</c> ↔ <c>&gt;=</c>; equality is its own mirror.</summary>
    private static SyntaxKind MirrorComparisonKind(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        _ => kind,
    };

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
        // Reflected user comparison (#1719): the dunder is on the RIGHT operand's type, so the C#
        // operator binds with the operands swapped and an ordering token mirrored. Python evaluates
        // the left operand first, so a non-trivial left is bound to a pattern variable before the
        // swapped comparison reads it: `f() is var __refl_0 && d == __refl_0`.
        if (orderingLowering == OperatorLoweringKind.ReflectedOperands)
        {
            var mirrored = MirrorComparisonKind(kind);
            if (IsTrivialExpression(leftAst))
                return Binary(mirrored, right, left);

            var temp = GenerateTempVarName("refl");
            return Binary(
                SyntaxKind.LogicalAndExpression,
                IsPattern(left, VarPattern(SingleVariableDesignation(Identifier(temp)))),
                Binary(mirrored, right, IdentifierName(temp)));
        }

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
                return IsPattern(operand, nullPattern);
            }

            if (equalityLowering is BinaryOpLowering.NativeOperator or BinaryOpLowering.NoneCheck)
                return Binary(kind, left, right);

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
                    equalsInvocation = InvocationExpression(Member(left, "Equals"))
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
                    var compareToCall = InvocationExpression(Member(left, "CompareTo"))
                        .AddArgumentListArguments(Argument(right));
                    return BinaryExpression(kind,
                        compareToCall,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
                }
            default:
                return Binary(kind, left, right);
        }
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c
        // Python guarantees intermediate expressions are evaluated exactly once.
        // For non-trivial intermediate expressions (function calls, member access, etc.),
        // we use the C# "is var" pattern to capture the value inline:
        //   a < (f() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < c
        // A link is its binary twin (P93 D3, #2648): it lowers through the binary operator's own
        // emitter for the shape the TypeChecker recorded — GenerateLoweredComparison,
        // GenerateIdentityComparison, GenerateNoneTest or GenerateMembershipTest — with the kind the
        // binary form records on its node, so the two positions share one emission (#1642).

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

        var operandCount = chain.Operands.Length;
        var linkCount = chain.Operators.Length;

        // The type each membership link binds its needle as (P93 D2, #2645); null for a link that is
        // not a membership test or whose needle is not bound (BoundNeedleType).
        var needleBindings = new TypeSyntax?[linkCount];
        // The recorded binding of a membership link's needle with no exemption (RecordedNeedleBindingType):
        // the cast a CAPTURED middle needle takes at that link's read, literal-only or not, since the
        // capture holds the value at its own type (owner ruling R2).
        var captureBindings = new TypeSyntax?[linkCount];
        for (int i = 0; i < linkCount; i++)
        {
            if (chainLowering.Links[i].Shape == ComparisonLinkShape.Membership)
            {
                needleBindings[i] = BoundNeedleType(chain.Operands[i], chain.Operands[i + 1]);
                captureBindings[i] = RecordedNeedleBindingType(chain.Operands[i]);
            }
        }

        // For intermediate operands (indices 1..n-2), decide if they need a temp variable.
        // First and last operands are only used once and don't need temps.
        var tempNames = new string?[operandCount];
        for (int i = 1; i < operandCount - 1; i++)
        {
            if (!IsTrivialExpression(chain.Operands[i]))
            {
                tempNames[i] = GenerateTempVarName("cmp");
            }
        }

        // A chain short-circuits like `&&`: only operands 0 and 1 are evaluated
        // unconditionally. Generate every operand under an evaluation sink so a hoist producer
        // (comprehension, spread, `?`) inside operand k lands in the block that owns operand k's
        // evaluation, not above the whole chain (plan-0667c5 Design Decision 6).
        //
        // A middle operand is generated once and read by two links. Its receiver unwrap (#1792: the
        // container of a membership link over a value-type `T | None`) belongs to the read that is that
        // link's container, so it is deferred to link i-1's right read; link i reads the value as its
        // binary twin does (P93 D3).
        var operandExprs = new ExpressionSyntax[operandCount];
        var operandEvals = new List<StatementSyntax>[operandCount];
        var deferredUnwraps = new ReceiverUnwrapKind?[operandCount];
        for (int i = 0; i < operandCount; i++)
        {
            int captured = i;
            var isMiddle = i > 0 && i < operandCount - 1;
            operandEvals[i] = WithEvaluationSink(
                () => operandExprs[captured] = isMiddle
                    ? GenerateExpressionDeferringReceiverUnwrap(chain.Operands[captured], out deferredUnwraps[captured])
                    : GenerateExpression(chain.Operands[captured]));
        }

        // A bare-name middle operand is read by link i-1 and again by link i, after operand i+1's
        // hoists: when those rebind it (a walrus in a spread, `0 < y == len([*keep((y := 5), xs)])`),
        // it is captured by link i-1 like a non-trivial middle, so link i reads the value Python
        // evaluated once (P93 D2). Read from the emitted syntax alone (HoistRebindsARead).
        for (int i = 1; i < operandCount - 1; i++)
        {
            if (tempNames[i] == null
                && chain.Operands[i] is Parser.Ast.Identifier
                && HoistRebindsARead(operandEvals[i + 1], operandExprs[i]))
            {
                tempNames[i] = GenerateTempVarName("cmp");
            }
        }

        // Whether link i's left operand syntax is a local already holding its value: a middle operand
        // captured by link i-1, or operand 0 captured ahead of operand 1's hoists (below).
        var leftEvaluated = new bool[linkCount];
        for (int i = 1; i < linkCount; i++)
            leftEvaluated[i] = tempNames[i] != null;

        // Operands 0 and 1 always run, in that order. Operand 1's hoists are flushed as statements
        // before the chain expression, so operand 0 is captured after its own hoists and ahead of
        // operand 1's when it has an effect (the ordering rule of GenerateExpressionsInOrder, applied
        // to the chain) or is a bare name operand 1's hoists rebind, or when link 0 binds it as a
        // membership needle: then the local takes the needle's binding type, and a bare name is read
        // before the container can rebind it (P93 D2).
        foreach (var stmt in operandEvals[0])
            HoistEvaluation(stmt);
        if (operandEvals[1].Count > 0)
        {
            if (needleBindings[0] is { } headBinding)
            {
                operandExprs[0] = HoistBoundNeedle(headBinding, operandExprs[0], GenerateTempVarName("cmpOrder"));
                leftEvaluated[0] = true;
            }
            else if (!IsSideEffectFree(chain.Operands[0])
                || (chain.Operands[0] is Parser.Ast.Identifier && HoistRebindsARead(operandEvals[1], operandExprs[0])))
            {
                var orderTemp = GenerateTempVarName("cmpOrder");
                HoistEvaluation(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(orderTemp))
                                .WithInitializer(EqualsValueClause(operandExprs[0]))))));
                operandExprs[0] = IdentifierName(orderTemp);
            }
        }
        foreach (var stmt in operandEvals[1])
            HoistEvaluation(stmt);

        // Builds link i (operator between operands i and i+1) through its twin's emitter, with the
        // `is var` capture for an intermediate operand read twice. For i >= 1 it first places operand
        // i+1's own hoists, which run only when every earlier link held — preceded, for a membership
        // link whose needle is still a re-read (a bare-name middle operand), by the needle's hoist.
        ExpressionSyntax BuildLink(int i)
        {
            var leftNode = chain.Operands[i];
            var rightNode = chain.Operands[i + 1];
            ExpressionSyntax left = i > 0 && tempNames[i] != null
                ? IdentifierName(tempNames[i]!)
                : operandExprs[i];
            var needleEvaluated = leftEvaluated[i];

            // A middle operand captured by link i-1 holds its own value, the one link i-1 compared
            // (P93 D3: a link is its binary twin; owner ruling R2: the checker typed it as that twin
            // does). Link i, binding it as a membership needle, takes it through the recorded binding
            // cast here, at the read that is its needle (P93 D2), as it would a named needle.
            if (i > 0 && tempNames[i] != null && captureBindings[i] is { } capturedBinding)
                left = Cast(capturedBinding, left);

            if (i >= 1 && operandEvals[i + 1].Count > 0)
            {
                if (needleBindings[i] is { } binding && !needleEvaluated)
                {
                    left = HoistBoundNeedle(binding, left, GenerateTempVarName("in"));
                    needleEvaluated = true;
                }
                foreach (var stmt in operandEvals[i + 1])
                    HoistEvaluation(stmt);
            }

            ExpressionSyntax right;
            if (tempNames[i + 1] != null)
            {
                // Wrap in: (expr is var __cmp_N ? __cmp_N : __cmp_N)
                // This evaluates expr once, binds to __cmp_N, and returns the value. The capture is
                // uncast: link i reads the operand as its binary twin does, and a link i+1 that binds
                // it as a membership needle casts it at its own read (above), never here (P93 D3).
                right = ParenthesizedExpression(
                    ConditionalExpression(
                        IsPattern(
                            operandExprs[i + 1],
                            VarPattern(SingleVariableDesignation(Identifier(tempNames[i + 1]!)))),
                        IdentifierName(tempNames[i + 1]!),
                        IdentifierName(tempNames[i + 1]!)));
            }
            else
            {
                right = operandExprs[i + 1];
            }

            if (deferredUnwraps[i + 1] is { } unwrap)
                right = ApplyReceiverUnwrap(right, unwrap);

            var link = chainLowering.Links[i];
            var op = chain.Operators[i];
            switch (link.Shape)
            {
                case ComparisonLinkShape.Membership:
                    ExpectLinkOperator(op, link.Shape, ComparisonOperator.In, ComparisonOperator.NotIn);
                    return GenerateMembershipTest(link.Kind, leftNode, rightNode, left, right,
                        negated: op == ComparisonOperator.NotIn, needleEvaluated);
                case ComparisonLinkShape.NoneTest:
                    ExpectLinkOperator(op, link.Shape, ComparisonOperator.Is, ComparisonOperator.IsNot);
                    return GenerateNoneTest(link.Kind,
                        NoneTestSubject(leftNode, rightNode, left, right)
                            ?? throw new InvalidOperationException(
                                "A NoneTest chain link has no None literal operand (P93 D3)"),
                        negated: op == ComparisonOperator.IsNot);
                case ComparisonLinkShape.Identity:
                    ExpectLinkOperator(op, link.Shape, ComparisonOperator.Is, ComparisonOperator.IsNot);
                    return GenerateIdentityComparison(link.Kind, left, right, negated: op == ComparisonOperator.IsNot);
                default:
                    return GenerateLoweredComparison(
                        MapComparisonOperator(op), left, right, leftNode, rightNode,
                        link.Kind, link.Equality ?? BinaryOpLowering.NativeOperator);
            }
        }

        // Every link is built once, in order, under its own sink: what it hoists (link 0's lands
        // with operands 0 and 1; a later link's are operand i+1's hoists, the needle's and the
        // container test's own, such as a tuple's array bridge) runs where the link is evaluated.
        var linkExprs = new ExpressionSyntax[linkCount];
        var linkEvals = new List<StatementSyntax>[linkCount];
        for (int i = 0; i < linkCount; i++)
        {
            int captured = i;
            linkEvals[i] = WithEvaluationSink(() => linkExprs[captured] = BuildLink(captured));
        }

        foreach (var stmt in linkEvals[0])
            HoistEvaluation(stmt);

        // Links 1..n-1 are evaluated only if every earlier link held. A hoist there must run
        // inside the conditional block, so the chain becomes imperative.
        bool hasConditionalHoist = false;
        for (int i = 1; i < linkCount; i++)
        {
            if (linkEvals[i].Count > 0)
                hasConditionalHoist = true;
        }

        if (!hasConditionalHoist)
        {
            ExpressionSyntax? result = null;
            for (int i = 0; i < linkCount; i++)
            {
                result = result == null
                    ? linkExprs[i]
                    : BinaryExpression(SyntaxKind.LogicalAndExpression, result, linkExprs[i]);
            }

            return result ?? throw new InvalidOperationException("Empty comparison chain");
        }

        // Manufactured sink, nested so each operand's `is var` capture stays in scope:
        //   bool __chainTmp = link0;
        //   if (__chainTmp) { evals[1]; __chainTmp = link1; if (__chainTmp) { evals[2]; … } }
        var chainTmp = GenerateTempVarName("chain");

        StatementSyntax BuildTail(int linkIndex)
        {
            var body = new List<StatementSyntax>();
            body.AddRange(linkEvals[linkIndex]);
            body.Add(ExpressionStatement(
                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    EscapedIdentifierName(chainTmp), linkExprs[linkIndex])));
            if (linkIndex + 1 < linkCount)
                body.Add(BuildTail(linkIndex + 1));
            return IfStatement(EscapedIdentifierName(chainTmp), Block(body));
        }

        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(chainTmp))
                        .WithInitializer(EqualsValueClause(linkExprs[0]))))));

        HoistEvaluation(BuildTail(1));

        return EscapedIdentifierName(chainTmp);
    }

    /// <summary>
    /// The invariant of a recorded chain link (P93 D3): its shape names the binary emitter of its own
    /// operator. A shape recorded for another operator is a checker defect, reported loudly.
    /// </summary>
    private static void ExpectLinkOperator(
        ComparisonOperator op, ComparisonLinkShape shape, ComparisonOperator first, ComparisonOperator second)
    {
        if (op != first && op != second)
        {
            throw new InvalidOperationException(
                $"A {shape} chain link was recorded for operator '{op}' (P93 D3: the shape is the arm the "
                + "binary twin of the link takes)");
        }
    }

    /// <summary>
    /// Maps a comparison operator (<c>==</c> <c>!=</c> <c>&lt;</c> <c>&lt;=</c> <c>&gt;</c>
    /// <c>&gt;=</c>) to the corresponding C# syntax kind. An <c>in</c> or <c>is</c> link never
    /// reaches here: it lowers through its own twin's emitter (P93 D3).
    /// </summary>
    private static SyntaxKind MapComparisonOperator(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equal => SyntaxKind.EqualsExpression,
            ComparisonOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            ComparisonOperator.LessThan => SyntaxKind.LessThanExpression,
            ComparisonOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            ComparisonOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            ComparisonOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
            _ => throw new InvalidOperationException(
                $"'{op}' is not an ordering or equality operator: its chain link lowers through its "
                + "binary twin's emitter (P93 D3)")
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

    /// <summary>
    /// Emits <c>a ?? b</c>. The right operand is evaluated only when the left is absent, so it is
    /// generated under an evaluation sink; when it hoists, the expression becomes a manufactured
    /// conditional sink so those statements run only on the absent path. Without the sink a
    /// comprehension/spread/walrus in <c>b</c> ran on every evaluation, including when <c>a</c> had
    /// a value.
    /// </summary>
    private ExpressionSyntax GenerateNullCoalesceOp(BinaryOp binOp)
    {
        var left = GenerateExpression(binOp.Left);

        ExpressionSyntax right = null!;
        var rightEvals = WithEvaluationSink(() => { right = GenerateExpression(binOp.Right); });

        var coalesceKind = OperatorLoweringOf(binOp).Kind;

        if (rightEvals.Count == 0)
        {
            return BuildCoalesceValue(binOp, coalesceKind, left, right);
        }

        // Manufactured sink. The left operand is captured once into a temp so the absent-test and
        // the value read cannot evaluate it twice:
        //   T __coalesce_0 = <left-as-result-when-present>;
        //   if (<absent test>) { evals; __coalesce_0 = <right>; }
        var resultType = GetExpressionSemanticType(binOp);
        if (resultType == null)
        {
            throw new InvalidOperationException(
                "No semantic type recorded for a '??' expression whose right operand hoists; the "
                + "TypeChecker must type every coalesce node (plan-0667c5 Design Decision 6).");
        }

        var tmpName = GenerateTempVarName("coalesce");
        var leftTmpName = GenerateTempVarName("coalesceLeft");

        // var __coalesceLeft_0 = <left>;
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(leftTmpName))
                        .WithInitializer(EqualsValueClause(left))))));

        var leftRef = EscapedIdentifierName(leftTmpName);

        // The present-value expression and the absent test differ per lowering: Optional carries
        // IsSome/Unwrap, a nullable reference/value carries a null test.
        ExpressionSyntax presentValue;
        ExpressionSyntax absentTest;
        if (coalesceKind is OperatorLoweringKind.OptionalCoalesceBothOptional
            or OperatorLoweringKind.OptionalUnwrapOr)
        {
            absentTest = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(leftRef), IdentifierName("IsSome")));
            presentValue = coalesceKind == OperatorLoweringKind.OptionalCoalesceBothOptional
                ? leftRef
                : InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ParenthesizedExpression(leftRef), IdentifierName("Unwrap")));
        }
        else
        {
            // The null PATTERN, never `== null` (#2224): a `T | None` left whose T carries an
            // `operator ==` (synthesized from `__eq__`, or a CLR overload) must not run user code to
            // decide absence — C#'s native `??` does not either.
            absentTest = IsPatternExpression(leftRef,
                ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)));
            presentValue = leftRef;
        }

        // T __coalesce_0;  — bare, assigned on exactly one of the two paths below. An
        // initializer of `(left)!` would be correct as a TREE and reparse as a cast of a logical
        // not (`(T)!;`), which is the hazard AssertEmittedTreePrecedence exists for.
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(_typeMapper.MapSemanticType(resultType))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tmpName))))));

        var absentBody = new List<StatementSyntax>(rightEvals.Count + 1);
        absentBody.AddRange(rightEvals);
        absentBody.Add(ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(tmpName), right)));

        var presentBody = SingletonList<StatementSyntax>(ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(tmpName), presentValue)));

        HoistEvaluation(IfStatement(absentTest, Block(absentBody), ElseClause(Block(presentBody))));

        return EscapedIdentifierName(tmpName);
    }

    /// <summary>
    /// The expression form of <c>a ?? b</c> for the case where the right operand hoists nothing:
    /// Optional carries a ternary on <c>IsSome</c> or an <c>UnwrapOr</c> call, everything else the
    /// native <c>??</c>.
    /// </summary>
    private ExpressionSyntax BuildCoalesceValue(
        BinaryOp binOp, OperatorLoweringKind? coalesceKind,
        ExpressionSyntax left, ExpressionSyntax right)
    {
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

        return Binary(SyntaxKind.CoalesceExpression, left, right);
    }

    private ExpressionSyntax GenerateShortCircuitOp(BinaryOp binOp)
    {
        var isAnd = binOp.Operator == BinaryOperator.And;
        var left = GenerateExpression(binOp.Left);
        var wrappedLeft = WrapTruthinessIfNeeded(left, binOp.Left);

        ExpressionSyntax right = null!;
        var evals = WithEvaluationSink(() => { right = GenerateExpression(binOp.Right); });

        if (evals.Count == 0)
        {
            var wrappedRight = WrapTruthinessIfNeeded(right!, binOp.Right);
            var logicalKind = isAnd
                ? SyntaxKind.LogicalAndExpression
                : SyntaxKind.LogicalOrExpression;
            return Binary(logicalKind, wrappedLeft, wrappedRight);
        }

        // Manufactured sink: hoists from the right operand must only run when the
        // right operand is actually evaluated (short-circuit semantics).
        // bool __and_tmp = left; if (__and_tmp) { evals; __and_tmp = right; }
        // (for or: if (!__and_tmp) { evals; __and_tmp = right; })
        // Use HoistEvaluation for the declaration so it comes AFTER any hoists from
        // the left operand (e.g. walrus declarations).
        var tmpName = GenerateTempVarName(isAnd ? "and" : "or");

        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tmpName))
                        .WithInitializer(EqualsValueClause(wrappedLeft))))));

        var wrappedRight2 = WrapTruthinessIfNeeded(right!, binOp.Right);
        var bodyStatements = new List<StatementSyntax>();
        bodyStatements.AddRange(evals);
        bodyStatements.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(tmpName),
                wrappedRight2)));

        var condition = isAnd
            ? (ExpressionSyntax)EscapedIdentifierName(tmpName)
            : PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                ParenthesizedExpression(EscapedIdentifierName(tmpName)));

        HoistEvaluation(IfStatement(condition, Block(bodyStatements)));

        return EscapedIdentifierName(tmpName);
    }

    private ExpressionSyntax GenerateConditionalExpression(ConditionalExpression cond)
    {
        // A conditional the checker marked TruthinessLowering.Distributed is in a truthiness
        // position and has no type of its own (R-K, #1743): it emits as `test ? wrap(a) : wrap(b)`.
        // The fact is read HERE, once, at generation — reading it at the wrap site instead meant
        // the caller generated the whole ternary first and the wrap discarded it, so every
        // sub-expression was generated TWICE in one statement (#1334's re-entry sweep).
        if (_context.SemanticInfo?.GetTruthinessLowering(cond) == TruthinessLowering.Distributed)
            return WrapDistributedTruthiness(cond);

        // value if test else other → test ? value : other
        var test = WrapTruthinessIfNeeded(GenerateExpression(cond.Test), cond.Test);

        ExpressionSyntax whenTrue = null!;
        var trueEvals = WithEvaluationSink(() =>
        {
            whenTrue = ApplyConditionalBranchNarrowing(cond.ThenValue, GenerateExpression(cond.ThenValue));
        });

        ExpressionSyntax whenFalse = null!;
        var falseEvals = WithEvaluationSink(() =>
        {
            whenFalse = ApplyConditionalBranchNarrowing(cond.ElseValue, GenerateExpression(cond.ElseValue));
        });

        if (trueEvals.Count == 0 && falseEvals.Count == 0)
        {
            return Conditional(test, whenTrue!, whenFalse!);
        }

        // Manufactured sink: hoists from then/else must only run in their branch.
        var exprType = GetExpressionSemanticType(cond);
        var typeSyntax = exprType != null
            ? _typeMapper.MapSemanticType(exprType)
            : IdentifierName("var");
        var tmpName = GenerateTempVarName("cond");

        HoistDeclaration(LocalDeclarationStatement(
            VariableDeclaration(typeSyntax)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tmpName))))));

        var trueBody = new List<StatementSyntax>();
        trueBody.AddRange(trueEvals);
        trueBody.Add(ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(tmpName), whenTrue!)));

        var falseBody = new List<StatementSyntax>();
        falseBody.AddRange(falseEvals);
        falseBody.Add(ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(tmpName), whenFalse!)));

        HoistEvaluation(IfStatement(test, Block(trueBody), ElseClause(Block(falseBody))));

        return EscapedIdentifierName(tmpName);
    }

    /// <summary>
    /// Applies the per-branch narrowing the store seam recorded for an arm of a conditional
    /// expression (<c>SemanticInfo.SetConditionalBranchNarrowing</c>, plan-14853b Decision 1, #1698).
    ///
    /// <para>C# gives <c>c ? 7 : 8</c> the natural type <c>int</c>, so <c>sbyte b = c ? 7 : 8;</c> is
    /// CS0266 — the conditional-expression conversion applies only when the conditional has no
    /// natural type, and C# has no narrow-integer literal suffix. <c>c ? (sbyte)7 : (sbyte)8</c>
    /// compiles, so each admitted arm is cast to the slot type the checker recorded on it.</para>
    ///
    /// <para>Rule 2: the emitter reads a materialized fact and prints a cast — it does not ask
    /// whether the arm is a constant, whether it fits, or what the slot is. An absent fact leaves
    /// the arm untouched, which is also the whole behavior for the float32/decimal arms (their
    /// literals are re-typed per node and print their own suffix) and for every non-store
    /// conditional. Nested conditionals need nothing extra: the inner conditional's own arms carry
    /// their own facts and are reached by this same seam through
    /// <see cref="GenerateConditionalExpression"/>'s recursion.</para>
    /// </summary>
    private ExpressionSyntax ApplyConditionalBranchNarrowing(Expression branch, ExpressionSyntax generated)
    {
        if (_context.SemanticInfo?.GetConditionalBranchNarrowing(branch) is not { } narrowedTo)
            return generated;

        return Cast(_typeMapper.MapSemanticType(narrowedTo), generated);
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
            // The base type T of `value as? T` is decided in semantic analysis for EVERY spelling:
            // the classifier records it for a name that denotes a runtime type (#1235), and
            // CheckTypeCoercion records the stripped base type for the nullable/optional/result
            // spellings the classifier declines (#1670). This syntax is used twice below (the type
            // pattern and the Optional<T> argument), and re-deriving it from the annotation here is
            // what emitted the unspellable open generic (CS0305) — so a missing fact is an ICE, not
            // a fallback (Critical Rule 2).
            var baseTypeSyntax = TypeTestTypeSyntax(coercion.TargetType)
                ?? throw new InvalidOperationException(
                    $"No type-test lowering was recorded for the cast target "
                    + $"'{coercion.TargetType.Name}' at line {coercion.LineStart}, column "
                    + $"{coercion.ColumnStart}. CheckTypeCoercion records one for every target it "
                    + "accepts; a missing fact is a compiler bug, not a case for re-deriving the "
                    + "type from the written annotation (#1670).");

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
                IsPattern(
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
            return Cast(targetType, value);
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
        //
        // The one LIVE path (#2669): a type test the checker resolved as an identity comparison
        // against an enum member (`x is Color.RED`) — it recorded an identity lowering on the node
        // and type-checked the expression reading of the name, so this is the binary `is` emission
        // over (value, member), operands generated left to right.
        var identityKind = OperatorLoweringOf(check).Kind;
        if (check.IdentityOperand is { } identityOperand && IsEnumIdentityLowering(identityKind))
        {
            var identityOperands = GenerateExpressionsInOrder(new[] { check.Value, identityOperand });
            return GenerateIdentityComparison(identityKind, identityOperands[0], identityOperands[1], negated: false);
        }

        var value = GenerateExpression(check.Value);
        var checkType = MapClassifiedTypeOperand(check.CheckType);

        return Binary(
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
        => TypeTestTypeSyntax(annotation) ?? _typeMapper.MapType(annotation);

}
