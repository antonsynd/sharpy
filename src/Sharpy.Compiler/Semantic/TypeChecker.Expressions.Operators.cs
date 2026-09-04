using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Binary/unary operators, comparisons, type casts, type checks, try/maybe expressions
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
        }

        if (binOp.Operator == BinaryOperator.And)
        {
            return CheckBooleanAndOp(binOp);
        }

        if (binOp.Operator == BinaryOperator.Or)
        {
            return CheckBooleanOrOp(binOp);
        }

        // The operand of an `is (not) None` test reads the honest, un-narrowed value: mark it so
        // the read sites skip narrowing for that node (see _typeTestOperand). The scope ends right
        // after the operand checks — the suppression must not leak into sibling expressions.
        // When this is NOT a type test the scope pushes the field's CURRENT value, so an enclosing
        // operand survives rather than being cleared for the duration of these operands.
        var typeTestOperand = _typeTestOperand;
        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            if (binOp.Right is NoneLiteral)
                typeTestOperand = UnwrapParenthesized(binOp.Left);
            else if (binOp.Left is NoneLiteral)
                typeTestOperand = UnwrapParenthesized(binOp.Right);
        }

        SemanticType leftType, rightType;
        using (ScopedValue.Push(ref _typeTestOperand, typeTestOperand))
        {
            leftType = CheckExpression(binOp.Left);
            rightType = CheckExpression(binOp.Right);
        }

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Reject void-returning call operands in equality comparisons (#911). A `None`-typed
        // (VoidType) operand that is NOT the literal `None` is a void-returning call used as a
        // comparand (e.g. `s == f()` where `f() -> None`). Python would evaluate the call and
        // compare against None, but that is almost certainly a bug, and Sharpy already rejects
        // void-call *assignment* (SPY0229). Gating here — before InferBinaryOpType — also
        // guarantees the literal-shape invariant the equality lowering relies on: any VoidType
        // operand that reaches inference/lowering is the `None` literal. Scope is ==/!= only;
        // other operators already fail inference with SPY0222. (Axiom 3 > Axiom 2.)
        if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            var voidOperandFound = false;
            if (leftType is VoidType && binOp.Left is not NoneLiteral)
            {
                AddError(
                    "Expression of type 'None' has no value and cannot be used as a comparison operand; call it as a statement and compare separately",
                    binOp.Left.LineStart,
                    binOp.Left.ColumnStart,
                    code: DiagnosticCodes.Semantic.VoidComparisonOperand,
                    span: binOp.Left.Span);
                voidOperandFound = true;
            }
            if (rightType is VoidType && binOp.Right is not NoneLiteral)
            {
                AddError(
                    "Expression of type 'None' has no value and cannot be used as a comparison operand; call it as a statement and compare separately",
                    binOp.Right.LineStart,
                    binOp.Right.ColumnStart,
                    code: DiagnosticCodes.Semantic.VoidComparisonOperand,
                    span: binOp.Right.Span);
                voidOperandFound = true;
            }
            if (voidOperandFound)
            {
                return SemanticType.Unknown;
            }
        }

        // A string-backed enum member compares equal to its backing string — CPython's StrEnum
        // does (`LogLevel.INFO == "INFO"` is True), and the emitted class's implicit conversion
        // makes the C# comparison bind. Answered here rather than in the operator registry, which
        // keys on type identity and would need an entry per enum (#1284).
        if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual
            && ((IsStringBackedEnum(leftType) && rightType is BuiltinType { Name: BuiltinNames.Str })
                || (IsStringBackedEnum(rightType) && leftType is BuiltinType { Name: BuiltinNames.Str })))
        {
            return SemanticType.Bool;
        }

        // §10.2.11: a constant operand converts to the other operand's type before promotion
        var (effectiveLeftType, effectiveRightType) = EffectiveOperandTypes(
            binOp.Operator, binOp.Left, leftType, binOp.Right, rightType);

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferBinaryOpType(binOp.Operator, effectiveLeftType, effectiveRightType);

        // If type inference fails, report the error directly
        // (validators may not catch all type incompatibilities)
        if (resultType == null)
        {
            // When comparing against the `None` literal with ==/!=, point the user at the
            // supported spelling: Sharpy rejects `x == None` (SPY0222) but accepts `x is None`
            // (#1079). Both operand orders (`x == None` and `None == x`) get the hint. The
            // suggested operator rides the diagnostic data payload for a future LSP quick-fix.
            IReadOnlyDictionary<string, string>? data = null;
            string? messageSuffix = null;
            if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual
                && (binOp.Left is NoneLiteral || binOp.Right is NoneLiteral))
            {
                var suggestedOperator = binOp.Operator == BinaryOperator.Equal ? "is None" : "is not None";
                messageSuffix = $". Did you mean '{suggestedOperator}'?";
                data = new Dictionary<string, string> { ["suggestedOperator"] = suggestedOperator };
            }

            ReportUnsupportedBinaryOperator(binOp, GetOperatorSymbol(binOp.Operator),
                leftType, rightType, data, messageSuffix);
            return SemanticType.Unknown;
        }

        // Record how a comparison should be lowered by codegen — the equality strategy (#886:
        // tuples and CLR types that resolve via Equals, no op_Equality, must emit an Equals call;
        // #901: reference-type ==/!= None is a null pattern) rides the IR transport, the ordering
        // kind (#1623: ordinal string compare, constrained type-parameter CompareTo) the
        // OperatorLowering tag. Both come from the ONE classifier every comparison-chain link also
        // uses, so the binary and chain positions cannot drift (#1642).
        //
        // Invariant (#911): any VoidType operand reaching this point is guaranteed to be the
        // `None` literal — void-returning call operands were rejected above with SPY0329. This
        // is what makes the NoneCheck lowering's AST-shape operand selection well-defined. Three
        // consumers rely on it: InferBinaryOpType/GetBinaryOpLowering (TypeInferenceService),
        // the emitter's NoneCheck branches (RoslynEmitter.Expressions.Operators / .Statements.
        // ControlFlow), and OperatorValidator (suppress-only, never selects operands).
        if (IsComparisonOperator(binOp.Operator))
        {
            var link = ClassifyComparisonLowering(binOp.Operator, leftType, rightType);
            if (link.Equality is { } equality && equality != BinaryOpLowering.NativeOperator)
            {
                _semanticInfo.SetBinaryOpLowering(binOp, equality);
            }
            if (link.Kind != OperatorLoweringKind.Native)
            {
                _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(link.Kind));
            }
        }

        // Constant-fold integer exponentiation so a result that fits a wider integer type
        // widens (e.g. `10 ** 18` → long) and an out-of-range result is diagnosed (SPY0328)
        // instead of being silently truncated by the runtime double cast. Only constant
        // non-negative integer powers are folded; negative exponents keep the existing path
        // (Python: `2 ** -1 == 0.5`). The folded value is recorded so codegen emits a literal (#905).
        if (binOp.Operator == BinaryOperator.Power
            && TypeUtils.IsInteger(leftType) && TypeUtils.IsInteger(rightType))
        {
            var folded = TryFoldIntegerPower(binOp);
            if (folded != null)
                return folded;
        }

        // Constant integer +, -, * whose exact result does not fit the expression's own result
        // type is refused here (SPY0348) rather than left for Roslyn (#1234). Roslyn evaluates
        // CONSTANT expressions in a checked context regardless of the unchecked runtime default,
        // so `3794 * 1973 * 948` reached the C# compiler as CS0220 ("the operation overflows at
        // compile time in checked mode") and surfaced as an SPY0908 internal error — a compiler
        // bug report for what is really a user-program fact.
        if (binOp.Operator is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                or BinaryOperator.LeftShift or BinaryOperator.RightShift
            && TypeUtils.IsInteger(leftType) && TypeUtils.IsInteger(rightType))
        {
            CheckNegativeConstantShiftCount(binOp);
            CheckConstantIntegerOverflow(binOp, resultType);
        }

        // Record operator lowering tags so the emitter never re-derives these decisions (#1623).
        if (binOp.Operator == BinaryOperator.Divide
            && resultType == SemanticType.Double
            && !PrimitiveCatalog.IsDecimal(leftType) && !PrimitiveCatalog.IsDecimal(rightType)
            && !PrimitiveCatalog.IsFloatingPoint(leftType) && !PrimitiveCatalog.IsFloatingPoint(rightType)
            && leftType is not UserDefinedType and not GenericType
            && rightType is not UserDefinedType and not GenericType)
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.TrueDivisionCastLeft));
        }

        if (binOp.Operator is BinaryOperator.LeftShift or BinaryOperator.RightShift
            && TypeUtils.IsInteger(rightType) && rightType != SemanticType.Int
            && leftType is not UserDefinedType and not GenericType)
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.ShiftCountCastToInt));
        }

        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot
            && (binOp.Right is NoneLiteral || binOp.Left is NoneLiteral)
            && (leftType is OptionalType || rightType is OptionalType))
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.OptionalNoneTest));
        }

        if (binOp.Operator == BinaryOperator.NullCoalesce && leftType is OptionalType)
        {
            var kind = rightType is OptionalType
                ? OperatorLoweringKind.OptionalCoalesceBothOptional
                : OperatorLoweringKind.OptionalUnwrapOr;
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(kind));
        }

        // `str * int` / `int * str` → StringHelpers.Repeat(str, count): which operand is the
        // string is decided HERE and carried by the tag, so the emitter never re-inspects types.
        if (binOp.Operator == BinaryOperator.Multiply && leftType == SemanticType.Str)
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.StringRepeatStrLeft));
        }
        else if (binOp.Operator == BinaryOperator.Multiply && rightType == SemanticType.Str)
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.StringRepeatStrRight));
        }

        if (binOp.Operator == BinaryOperator.Power
            && ClassifyIntegerPower(leftType, rightType) is { } powKind)
        {
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(powKind));
        }

        // `//` and `%` (#1658): ONE classifier shared with the augmented `//=` / `%=` site.
        if (ClassifyFlooredArithmetic(binOp.Operator, leftType, rightType) is { } flooredKind)
        {
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(flooredKind));
        }

        // Warn when is/is not is used with value types — identity comparison is
        // meaningless because value types are boxed, so the result is always False.
        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            var effectiveLeft = leftType is NullableType nl ? nl.UnderlyingType : leftType;
            var effectiveRight = rightType is NullableType nr ? nr.UnderlyingType : rightType;

            if (effectiveLeft.IsValueType && effectiveRight.IsValueType)
            {
                var opSymbol = binOp.Operator == BinaryOperator.Is ? "is" : "is not";
                _diagnostics.AddWarning(
                    $"'{opSymbol}' used with value types '{leftType.GetDisplayName()}' and " +
                    $"'{rightType.GetDisplayName()}' — identity comparison is always False " +
                    "due to boxing; use '==' or '!=' instead",
                    binOp.LineStart,
                    binOp.ColumnStart,
                    _currentFilePath,
                    code: DiagnosticCodes.Validation.IsWithValueTypes,
                    phase: CompilerPhase.TypeChecking);
            }
        }

        // #1731: propagate literal-derived string fact through str + str
        if (binOp.Operator == BinaryOperator.Add
            && resultType == SemanticType.Str
            && _semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(binOp.Left))
            && _semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(binOp.Right)))
        {
            _semanticInfo.SetLiteralDerived(binOp);
        }

        return resultType;
    }

    /// <summary>
    /// Classifies how a <c>**</c> lowers — the single classifier shared by the binary site
    /// (<see cref="CheckBinaryOp"/>) and the augmented <c>**=</c> site, so the two cannot
    /// drift (#1700, the #1623 shape). Returns <c>null</c> for user-defined / CLR operands and
    /// non-integer types (float/decimal are handled separately at the call site).
    /// </summary>
    private static OperatorLoweringKind? ClassifyIntegerPower(
        SemanticType leftType, SemanticType rightType)
    {
        if (leftType is UserDefinedType or GenericType
            || rightType is UserDefinedType or GenericType)
            return null;

        if (PrimitiveCatalog.IsDecimal(leftType) || PrimitiveCatalog.IsDecimal(rightType))
            return OperatorLoweringKind.DecimalPow;

        if (PrimitiveCatalog.IsFloatingPoint(leftType) || PrimitiveCatalog.IsFloatingPoint(rightType))
            return OperatorLoweringKind.FloatPow;

        if (!TypeUtils.IsInteger(leftType) || !TypeUtils.IsInteger(rightType))
            return null;

        var leftInfo = PrimitiveCatalog.GetPrimitiveInfo(leftType);
        var rightInfo = PrimitiveCatalog.GetPrimitiveInfo(rightType);
        if (leftInfo == null || rightInfo == null)
            return null;

        var leftIsULong = leftType == SemanticType.ULong;
        var rightIsULong = rightType == SemanticType.ULong;

        if (leftIsULong && (rightIsULong || !rightInfo.IsSigned))
            return OperatorLoweringKind.IntegerPowULong;
        if (leftIsULong && rightInfo.IsSigned)
            return OperatorLoweringKind.IntegerPowULongExponentLong;
        if (rightIsULong)
            return OperatorLoweringKind.IntegerPowLongExponentULong;

        var promoted = PrimitiveCatalog.GetPromotedType(leftType, rightType);
        if (promoted == SemanticType.Long || promoted == SemanticType.UInt)
            return OperatorLoweringKind.IntegerPowLong;

        return OperatorLoweringKind.IntegerPowInt;
    }

    /// <summary>
    /// Classifies how a <c>//</c> or <c>%</c> lowers, from the two operand types the ONE
    /// <c>InferBinaryOpType</c> call already produced — the single classifier shared by the
    /// binary site (<see cref="CheckBinaryOp"/>, node = the <c>BinaryOp</c>) and the augmented
    /// <c>//=</c> / <c>%=</c> site (node = the <c>Assignment</c>), so the two cannot drift (#1658,
    /// the #1623 shape). The emitter switches on the recorded tag alone and never re-derives it
    /// from operand types.
    /// <list type="bullet">
    /// <item><c>//</c>: a <c>decimal</c> operand → <see cref="OperatorLoweringKind.DecimalFloorDivide"/>;
    /// a float32/float64 operand → <see cref="OperatorLoweringKind.FloatFloorDivide"/>; otherwise
    /// <see cref="OperatorLoweringKind.IntegerFloorDivide"/> (int/long and the widened CLR
    /// integers — byte, uint, … — which C# overload resolution promotes). Exhaustive: a <c>//</c>
    /// that passed inference is always numeric ⊗ numeric (there is no <c>__floordiv__</c>
    /// mapping and no CLR <c>op_</c> name for it), so an unrecorded <c>//</c> is an emitter ICE.</item>
    /// <item><c>%</c>: a <c>decimal</c> operand → <see cref="OperatorLoweringKind.DecimalModulo"/>;
    /// both operands in {int, long, float32, float64} → <see cref="OperatorLoweringKind.FlooredModulo"/>;
    /// every other shape — user <c>__mod__</c> (→ <c>operator %</c>), CLR <c>op_Modulus</c>, a
    /// widened CLR integer operand — returns <c>null</c>: no record, native <c>%</c>, exactly as
    /// the other families spell "Native".</item>
    /// </list>
    /// Literal operands are classified from their TYPE like any other operand (the checker knows
    /// literal types); no AST-shape fallback exists here.
    /// </summary>
    private static OperatorLoweringKind? ClassifyFlooredArithmetic(
        BinaryOperator op, SemanticType leftType, SemanticType rightType)
    {
        var hasDecimal = PrimitiveCatalog.IsDecimal(leftType) || PrimitiveCatalog.IsDecimal(rightType);
        switch (op)
        {
            case BinaryOperator.FloorDivide:
                if (hasDecimal)
                    return OperatorLoweringKind.DecimalFloorDivide;
                if (PrimitiveCatalog.IsFloatingPoint(leftType) || PrimitiveCatalog.IsFloatingPoint(rightType))
                    return OperatorLoweringKind.FloatFloorDivide;
                return OperatorLoweringKind.IntegerFloorDivide;

            case BinaryOperator.Modulo:
                if (hasDecimal)
                    return OperatorLoweringKind.DecimalModulo;
                if (IsFlooredNumeric(leftType) && IsFlooredNumeric(rightType))
                    return OperatorLoweringKind.FlooredModulo;
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// The floored-<c>%</c> operand allowlist: every integer width (int, long, int8,
    /// uint8, uint64, …) and float32/float64, matching the <c>Builtins.FloorMod</c>
    /// overload set. Narrow integers widen to the <c>(int, int)</c> or <c>(long, long)</c>
    /// overload; <c>ulong</c> has its own overload (#1662). <c>decimal</c> is outside it
    /// (decimal modulo is truncating).
    /// </summary>
    private static bool IsFlooredNumeric(SemanticType type)
        => PrimitiveCatalog.IsInteger(type) || PrimitiveCatalog.IsFloatingPoint(type);

    /// <summary>
    /// Constant-folds a <c>base ** exponent</c> expression when both operands are constant
    /// integers and the exponent is non-negative. Returns <see cref="SemanticType.Int"/> or
    /// <see cref="SemanticType.Long"/> (whichever fits) and records the folded value in
    /// <see cref="SemanticInfo"/>; emits SPY0328 and returns <see cref="SemanticType.Unknown"/>
    /// when the result exceeds <c>long</c>. Returns <c>null</c> when the expression is not a
    /// constant non-negative integer power (caller keeps the regular inference result). (#905)
    /// </summary>
    private SemanticType? TryFoldIntegerPower(BinaryOp binOp)
    {
        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp.Left, out var baseValue)
            || !IntegerConstantEvaluator.TryGetConstantInteger(binOp.Right, out var exponent))
            return null;

        // Negative exponents are not folded — they keep the existing (double/runtime) path.
        if (exponent.Sign < 0)
            return null;

        // A constant exponent larger than int.MaxValue can never produce a fixed-width result
        // (and BigInteger.Pow takes an int exponent), so treat it as overflow.
        if (exponent > int.MaxValue)
        {
            ReportIntegerPowerOverflow(binOp);
            return SemanticType.Unknown;
        }

        var result = System.Numerics.BigInteger.Pow(baseValue, (int)exponent);

        // The folded VALUE is re-derived into an IrConstant by the lowering pass (E2 #1056); the
        // type checker keeps only the result-type + overflow decision here. Both sides read the same
        // pure IntegerConstantEvaluator, so they cannot diverge.
        if (result >= int.MinValue && result <= int.MaxValue)
            return SemanticType.Int;

        if (result >= long.MinValue && result <= long.MaxValue)
            return SemanticType.Long;

        // A ulong-typed operand widens the fold to ulong (#1700).
        var leftType = _semanticInfo.GetExpressionType(binOp.Left);
        var rightType = _semanticInfo.GetExpressionType(binOp.Right);
        if ((leftType == SemanticType.ULong || rightType == SemanticType.ULong)
            && result >= 0 && result <= ulong.MaxValue)
            return SemanticType.ULong;

        ReportIntegerPowerOverflow(binOp);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Refuses a constant integer <c>+</c>/<c>-</c>/<c>*</c> whose exact value does not fit
    /// <paramref name="resultType"/> (SPY0348, #1234). Does nothing when the expression is not a
    /// constant integer, or when its result type is neither <c>int</c> nor <c>long</c> — every
    /// other integer width keeps whatever behavior it has today.
    /// <para>
    /// <b>No widening</b>, deliberately, and unlike constant <c>**</c> (which widens int→long
    /// before reporting SPY0328). A constant must type exactly as the same expression would with
    /// variables in place of its literals: runtime <c>int * int</c> is <c>int</c>, so constant
    /// <c>int * int</c> is <c>int</c> too. The user-visible consequence is that
    /// <c>x: long = 3794 * 1973 * 948</c> is refused even though the value fits <c>long</c> —
    /// annotating one operand (<c>3794L * 1973 * 948</c>) types the whole expression <c>long</c>
    /// and compiles. Documented in arithmetic_operators.md.
    /// </para>
    /// <para>
    /// Constant-vs-runtime asymmetry is intentional too: the same overflow with variable operands
    /// wraps silently under .NET's unchecked default. That is C#'s own documented behavior
    /// (CS0220 is an error while the runtime wraps), so Axiom 1 settles it.
    /// </para>
    /// </summary>
    /// <summary>
    /// Refuses a shift whose count is a negative constant (#1315).
    /// </summary>
    /// <remarks>
    /// CPython raises <c>ValueError: negative shift count</c>. C# masks the count to the left
    /// operand's width — 5 bits for <c>int</c>, 6 for <c>long</c> — so <c>1 &lt;&lt; -1</c> is
    /// <c>1 &lt;&lt; 31</c> = −2147483648 and <c>256 &gt;&gt; -1</c> is 0. Both are silently wrong
    /// answers to a question the author cannot have meant, and both are visible at compile time.
    /// A <b>runtime</b> negative count keeps .NET's masking (Axiom 1) and is catalogued in
    /// <c>deviations.yaml</c> — this refusal is only for the constant case, where there is a value
    /// to inspect.
    /// </remarks>
    private void CheckNegativeConstantShiftCount(BinaryOp binOp)
    {
        if (binOp.Operator is not (BinaryOperator.LeftShift or BinaryOperator.RightShift))
        {
            return;
        }

        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp.Right, out var count) || count >= 0)
        {
            return;
        }

        var op = binOp.Operator == BinaryOperator.LeftShift ? "<<" : ">>";
        AddError(
            $"Shift count {count} is negative. Python raises ValueError; .NET masks the count to the "
            + $"operand's width, so '{op} {count}' would compute a different value silently. "
            + $"Use the opposite operator with a positive count.",
            binOp.Right.LineStart,
            binOp.Right.ColumnStart,
            code: DiagnosticCodes.Semantic.NegativeConstantShiftCount,
            span: binOp.Right.Span);
    }

    private void CheckConstantIntegerOverflow(BinaryOp binOp, SemanticType resultType)
    {
        // With literal typing correct (#1314, #1320), the expression's result type IS the
        // width the emitter will use — no workaround scan needed.
        System.Numerics.BigInteger min;
        System.Numerics.BigInteger max;
        string widthName;
        if (resultType == SemanticType.Long)
        {
            min = long.MinValue;
            max = long.MaxValue;
            widthName = "long";
        }
        else if (resultType == SemanticType.Int)
        {
            min = int.MinValue;
            max = int.MaxValue;
            widthName = "int";
        }
        else
        {
            return;
        }

        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp, out var value))
            return;

        if (value >= min && value <= max)
            return;

        if (OverflowedOperandAlreadyReported(binOp.Left, min, max)
            || OverflowedOperandAlreadyReported(binOp.Right, min, max))
        {
            return;
        }

        AddError(
            $"Constant expression evaluates to {value}, which does not fit " +
            $"'{widthName}'; Sharpy integers are fixed-width. " +
            (widthName == "long"
                ? "This expression is already computed as 'long' and the value exceeds 64 bits, " +
                  "so restructure the computation."
                : "Annotate an operand as 'long' (e.g. '3794L * 1973 * 948') so the whole " +
                  "expression is computed as 'long', or restructure the computation."),
            binOp.LineStart,
            binOp.ColumnStart,
            code: DiagnosticCodes.Semantic.ConstantIntegerOverflow,
            span: binOp.Span);
    }

    /// <summary>
    /// True when <paramref name="operand"/> is itself a foldable constant arithmetic operation
    /// whose value is already out of range — meaning it drew its own SPY0348 and the enclosing
    /// node must stay quiet. See <see cref="CheckConstantIntegerOverflow"/>.
    /// </summary>
    private static bool OverflowedOperandAlreadyReported(
        Expression operand, System.Numerics.BigInteger min, System.Numerics.BigInteger max)
    {
        while (operand is Parenthesized paren)
            operand = paren.Expression;

        return operand is BinaryOp
        {
            Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
        }
            && IntegerConstantEvaluator.TryGetConstantInteger(operand, out var value)
            && (value < min || value > max);
    }

    private void ReportIntegerPowerOverflow(BinaryOp binOp)
    {
        AddError(
            "Result of integer exponentiation does not fit a 64-bit integer; Sharpy integers " +
            "are fixed-width. Use a floating-point base (e.g. '10.0 ** 50') or restructure the computation.",
            binOp.LineStart,
            binOp.ColumnStart,
            code: DiagnosticCodes.Semantic.IntegerPowerOverflow,
            span: binOp.Span);
    }

    // Constant integer literal evaluation moved to the shared, pure IntegerConstantEvaluator so the
    // lowering pass can re-derive the folded value into an IrConstant without duplicating parsing
    // logic (E2 #1056).

    private SemanticType CheckBooleanAndOp(BinaryOp andOp)
    {
        var leftType = CheckExpression(andOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(andOp.Right);
            return SemanticType.Unknown;
        }

        var (leftTruthTestable, leftTruthLowering) = ClassifyTruthiness(leftType);
        if (!leftTruthTestable)
        {
            AddError(
                $"Operand of 'and' must be truth-testable, got '{leftType.GetDisplayName()}'",
                andOp.Left.LineStart, andOp.Left.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: andOp.Left.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(andOp.Left, leftTruthLowering);
        }

        var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);

        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNarrowed);
            rightType = CheckExpression(andOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        var (rightTruthTestable, rightTruthLowering) = ClassifyTruthiness(rightType);
        if (!rightTruthTestable)
        {
            AddError(
                $"Operand of 'and' must be truth-testable, got '{rightType.GetDisplayName()}'",
                andOp.Right.LineStart, andOp.Right.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: andOp.Right.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(andOp.Right, rightTruthLowering);
        }

        return SemanticType.Bool;
    }

    private SemanticType CheckBooleanOrOp(BinaryOp orOp)
    {
        var leftType = CheckExpression(orOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(orOp.Right);
            return SemanticType.Unknown;
        }

        var (leftTruthTestable, leftTruthLowering) = ClassifyTruthiness(leftType);
        if (!leftTruthTestable)
        {
            AddError(
                $"Operand of 'or' must be truth-testable, got '{leftType.GetDisplayName()}'",
                orOp.Left.LineStart, orOp.Left.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: orOp.Left.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(orOp.Left, leftTruthLowering);
        }

        // Expression-level narrowing (#1080): the right operand is evaluated only when the left is
        // falsy, so the left's NEGATIVE narrowings hold inside it (e.g. `x is None or use(x + 1)` —
        // the RHS sees x non-None). The narrowings do not leak past the operand.
        var leftNegativeNarrowed = ExtractNarrowedTypes(orOp.Left, false);

        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNegativeNarrowed);
            rightType = CheckExpression(orOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        var (rightTruthTestable, rightTruthLowering) = ClassifyTruthiness(rightType);
        if (!rightTruthTestable)
        {
            AddError(
                $"Operand of 'or' must be truth-testable, got '{rightType.GetDisplayName()}'",
                orOp.Right.LineStart, orOp.Right.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: orOp.Right.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(orOp.Right, rightTruthLowering);
        }

        return SemanticType.Bool;
    }

    /// <summary>
    /// Type-checks the pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend x to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity)
    /// </summary>
    private SemanticType CheckPipeForward(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);

        if (leftType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // The pipe target is a callee, so it normalizes like one: `x |> (f)` and `x |> (f)(y)`
        // resolve exactly as their unparenthesized forms (#1170). Without this a parenthesized
        // target missed the symbol-resolution arms below and fell through to "whatever the
        // expression evaluates to" — which silently reinterpreted `x |> (double)` as a conversion
        // to the `double` primitive instead of a call to a user function named `double`.
        var target = UnwrapParenthesized(binOp.Right);

        // Case 1: x |> f(y, z) - right side is already a function call
        // We need to re-validate with x prepended to arguments
        if (target is FunctionCall funcCall)
        {
            return CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
        }

        // Case 2: x |> f - right side is an identifier or expression that should be callable
        // Pre-check: if right side is an identifier that resolves to a TypeSymbol, emit the
        // constructor-pipe error immediately. CheckExpression returns UnknownType for non-primitive
        // TypeSymbols, which would hide this case behind a silent early return.
        if (target is Identifier preId)
        {
            var preSymbol = _symbolTable.Lookup(preId.Name);
            if (preSymbol is TypeSymbol)
            {
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }
        }

        var rightType = CheckExpression(binOp.Right);

        if (rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Check if right side is a function type
        if (rightType is FunctionType ft)
        {
            // Validate that the function accepts leftType as first argument
            if (ft.ParameterTypes.Count < 1)
            {
                AddError($"Pipe target function takes no arguments, cannot pipe a value to it",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (!leftType.IsAssignableTo(ft.ParameterTypes[0]))
            {
                AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: binOp.Span);
                return SemanticType.Unknown;
            }

            return ft.ReturnType;
        }

        // If right side is an identifier, look up the function symbol
        if (target is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);

                if (requiredParamCount < 1)
                {
                    AddError($"Pipe target function '{id.Name}' takes no required arguments, cannot pipe a value to it",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                // Validate the piped value type matches first parameter
                var firstParam = funcSymbol.Parameters[0];
                if (!leftType.IsAssignableTo(firstParam.Type))
                {
                    AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: binOp.Span);
                    return SemanticType.Unknown;
                }

                // Check if remaining required args are satisfied (they must all have defaults)
                if (requiredParamCount > 1)
                {
                    AddError($"Function '{id.Name}' requires {requiredParamCount} arguments but only 1 is provided via pipe",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass → SomeClass(x)
                // This is allowed, handled similarly to function call
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            AddError($"'{id.Name}' is not callable",
                binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: binOp.Right.Span);
            return SemanticType.Unknown;
        }

        // Right side is some other expression that's not callable
        AddError($"Pipe target must be callable, got '{rightType.GetDisplayName()}'",
            binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a pipe forward where the right side is a function call.
    /// x |> f(y, z) → f(x, y, z) - prepend piped value to argument list.
    /// </summary>
    private SemanticType CheckPipeForwardWithFunctionCall(SemanticType pipedType, FunctionCall call, BinaryOp binOp)
    {
        // Get the function being called, dispatching on the canonical (paren-stripped) callee (#1170)
        var callee = UnwrapParenthesized(call.Function);
        var calleeType = CheckExpression(call.Function);

        // Collect existing argument types
        var existingArgTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            existingArgTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Build the full argument list: piped value + existing args
        var allArgTypes = new List<SemanticType> { pipedType };
        allArgTypes.AddRange(existingArgTypes);

        // Total argument count includes piped value, positional args, and keyword args
        var totalArgCount = allArgTypes.Count + kwargTypes.Count;

        // Try to resolve the function symbol for better validation
        if (callee is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Record the resolved call target for codegen (and check deprecation) — #1438
                RecordResolvedCallTarget(call, funcSymbol);

                // Validate argument count considering variadic and keyword-only params
                var hasVariadicParam = funcSymbol.Parameters.Any(p => p.IsVariadic);
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                var totalParamCount = funcSymbol.Parameters.Count;
                var positionalParamCount = funcSymbol.Parameters.Count(p => !p.IsKeywordOnly);

                var tooFew = totalArgCount < requiredParamCount;
                var tooManyPositional = !hasVariadicParam && allArgTypes.Count > positionalParamCount;
                var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;

                if (tooFew || tooMany || tooManyPositional)
                {
                    if (hasVariadicParam)
                    {
                        AddError($"Function '{id.Name}' expects at least {requiredParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else if (requiredParamCount == totalParamCount)
                    {
                        AddError($"Function '{id.Name}' expects {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else
                    {
                        AddError($"Function '{id.Name}' expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    return SemanticType.Unknown;
                }

                // Validate positional argument types (piped value + call.Arguments)
                var variadicParamIndex = funcSymbol.Parameters.ToList().FindIndex(p => p.IsVariadic);
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    ParameterSymbol param;
                    if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                    {
                        param = funcSymbol.Parameters[variadicParamIndex];
                    }
                    else if (i < funcSymbol.Parameters.Count)
                    {
                        param = funcSymbol.Parameters[i];
                    }
                    else
                    {
                        break; // Shouldn't happen due to tooMany check
                    }

                    if (!argType.IsAssignableTo(param.Type))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{param.Name}' of type '{param.Type.GetDisplayName()}'",
                            argNode.LineStart,
                            argNode.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: argNode.Span);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                            span: kwarg.Span ?? kwarg.Value.Span);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally (including piped value)
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                                span: kwarg.Span ?? kwarg.Value.Span);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                                span: kwarg.Span ?? kwarg.Value.Span);
                        }
                    }
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol typeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass(y) → SomeClass(x, y)
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (symbol != null)
            {
                AddError($"'{id.Name}' is not callable",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                    span: call.Function.Span);
                return SemanticType.Unknown;
            }
        }

        // Fallback: check if callee is a FunctionType
        if (calleeType is FunctionType ft)
        {
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount} (including piped value)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                if (!allArgTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                    AddError($"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{ft.ParameterTypes[i].GetDisplayName()}' is expected",
                        argNode.LineStart,
                        argNode.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: argNode.Span);
                }
            }

            return ft.ReturnType;
        }

        AddError($"Pipe target must be callable, got '{calleeType.GetDisplayName()}'",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        // Constant unary minus over an integer literal: classify by the negated magnitude
        // so -2147483648 is int and -9223372036854775808 is long (#1304).
        if (unOp.Operator == UnaryOperator.Minus && unOp.Operand is IntegerLiteral il)
        {
            var result = Shared.IntegerLiteralClassifier.ClassifyNegated(il.Value, il.Suffix);
            if (result.IsError)
            {
                AddError(result.ErrorMessage!,
                    unOp.LineStart, unOp.ColumnStart,
                    code: DiagnosticCodes.Semantic.IntegerLiteralOutOfRange,
                    span: unOp.Span);
            }
            else if (result.Type == SemanticType.Int)
            {
                // The emitted literal's width is this classification, carried by the tag so the
                // emitter never re-inspects the CLR type (#1623): a single int literal token for
                // -2147483648, a single long token for -2147483649 / long.MinValue.
                _semanticInfo.SetOperatorLowering(unOp,
                    new OperatorLowering(OperatorLoweringKind.NegateLiteralInt));
            }
            else if (result.Type == SemanticType.Long)
            {
                _semanticInfo.SetOperatorLowering(unOp,
                    new OperatorLowering(OperatorLoweringKind.NegateLiteralLong));
            }
            _semanticInfo.SetExpressionType(unOp.Operand, result.Type);
            return result.Type;
        }

        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // `not` operands go through truthiness, not generic operator inference (#1558, #1570)
        if (unOp.Operator == UnaryOperator.Not)
        {
            var (notTruthTestable, notTruthLowering) = ClassifyTruthiness(operandType);
            if (!notTruthTestable)
            {
                AddError(
                    $"Operand of 'not' must be truth-testable, got '{operandType.GetDisplayName()}'",
                    unOp.Operand.LineStart, unOp.Operand.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: unOp.Operand.Span);
                return SemanticType.Unknown;
            }
            _semanticInfo.SetTruthinessLowering(unOp.Operand, notTruthLowering);
            return SemanticType.Bool;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferUnaryOpType(unOp.Operator, operandType);

        // If type inference fails, report the error directly
        if (resultType == null)
        {
            AddError(
                $"Type '{operandType.GetDisplayName()}' does not support unary operator '{GetOperatorSymbol(unOp.Operator)}'",
                unOp.LineStart,
                unOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidUnaryOperation,
                span: unOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // A comparison chain like "a < b < c" has:
        // - Operands: [a, b, c]
        // - Operators: [LessThan, LessThan]
        // We need to validate each adjacent pair: (a < b) and (b < c)

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // Check all operands and build their types
        var operandTypes = new List<SemanticType>();
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            operandTypes.Add(CheckExpression(chain.Operands[i]));
        }

        // Validate each comparison pair and record its lowering. Every link gets a record — an
        // Unknown-operand or refused link records the native form — so the emitter reads one
        // fact per operator and never needs a fallback (#1642).
        var links = ImmutableArray.CreateBuilder<ComparisonLinkLowering>(chain.Operators.Length);
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var leftType = operandTypes[i];
            var rightType = operandTypes[i + 1];

            // Skip validation if either operand is Unknown to avoid cascading errors
            if (leftType is UnknownType || rightType is UnknownType)
            {
                links.Add(new ComparisonLinkLowering(OperatorLoweringKind.Native, null));
                continue;
            }

            // Map ComparisonOperator to BinaryOperator and validate
            var binaryOp = TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[i]);
            var resultType = _typeInference.InferBinaryOpType(binaryOp, leftType, rightType);

            // If type inference fails, report the error directly
            if (resultType == null)
            {
                AddError(
                    $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binaryOp)}' with operand of type '{rightType.GetDisplayName()}'",
                    chain.Operands[i].LineStart,
                    chain.Operands[i].ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    span: chain.Span);
                links.Add(new ComparisonLinkLowering(OperatorLoweringKind.Native, null));
                continue;
            }

            // The SAME classifier the binary form `Operands[i] <op> Operands[i+1]` uses.
            links.Add(ClassifyComparisonLowering(binaryOp, leftType, rightType));
        }

        _semanticInfo.SetComparisonChainLowering(chain, new ComparisonChainLowering(links.MoveToImmutable()));

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    private static bool IsComparisonOperator(BinaryOperator op)
        => op is BinaryOperator.Equal or BinaryOperator.NotEqual
            or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual;

    /// <summary>
    /// The one classifier for a comparison's lowering, shared by <see cref="CheckBinaryOp"/> and every
    /// <see cref="CheckComparisonChain"/> link so the two positions cannot drift (#1642). Equality
    /// (<c>==</c>/<c>!=</c>) is answered by <see cref="TypeInferenceService.GetBinaryOpLowering"/> — the
    /// existing equality authority (#886, #901, EqualityComparerDefault for type parameters); ordering
    /// operators lower to an ordinal <c>string.Compare</c> for <c>str</c> operands and to <c>CompareTo</c>
    /// when either operand is a (Comparable-constrained) type parameter, both of which C# cannot express
    /// as a native operator (#1623). Everything else is the native C# operator.
    /// </summary>
    private ComparisonLinkLowering ClassifyComparisonLowering(
        BinaryOperator op, SemanticType leftType, SemanticType rightType)
    {
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            return new ComparisonLinkLowering(
                OperatorLoweringKind.Native,
                _typeInference.GetBinaryOpLowering(op, leftType, rightType));
        }

        if (op is BinaryOperator.LessThan or BinaryOperator.GreaterThan
            or BinaryOperator.LessThanOrEqual or BinaryOperator.GreaterThanOrEqual)
        {
            if (leftType == SemanticType.Str && rightType == SemanticType.Str)
                return new ComparisonLinkLowering(OperatorLoweringKind.StringOrdinalCompare, null);

            if (leftType is TypeParameterType || rightType is TypeParameterType)
                return new ComparisonLinkLowering(OperatorLoweringKind.TypeParameterCompareTo, null);
        }

        return new ComparisonLinkLowering(OperatorLoweringKind.Native, null);
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        var testType = CheckExpression(cond.Test);

        // The ternary's condition is a truthiness position like if/while/assert (#1603):
        // without this check a non-bool condition reaches Roslyn as `5 ? … : …`.
        var (ternaryTruthTestable, ternaryTruthLowering) = ClassifyTruthiness(testType);
        if (!ternaryTruthTestable)
        {
            AddError($"Conditional expression condition must be boolean, got '{testType.GetDisplayName()}'",
                cond.LineStart, cond.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                span: cond.Test.Span);
        }
        else
        {
            _semanticInfo.SetTruthinessLowering(cond.Test, ternaryTruthLowering);
        }

        // Expression-level narrowing (#1080): the true arm is evaluated only when the condition holds,
        // so it sees the condition's positive narrowings; the false arm sees the negative narrowings.
        // Reads inside each arm record their accessor lowering via the narrowing context, exactly as
        // `and`-RHS does — codegen needs no special handling. The narrowings do not leak past the arm.
        var thenEntries = ExtractNarrowedTypes(cond.Test, true);
        var elseEntries = ExtractNarrowedTypes(cond.Test, false);

        SemanticType thenType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(thenEntries);
            thenType = CheckExpression(cond.ThenValue);
        }

        SemanticType elseType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(elseEntries);
            elseType = CheckExpression(cond.ElseValue);
        }

        // Return common type
        if (thenType.IsAssignableTo(elseType))
            return elseType;
        if (elseType.IsAssignableTo(thenType))
            return thenType;

        // Intentional Unknown without error: when then/else branch types are incompatible
        // (e.g., `1 if cond else "str"`), we return Unknown rather than emitting an error
        // because the LCA (least common ancestor) logic is limited. Mark as error recovery
        // to suppress SPY0907 — a proper fix would compute LCA or emit a type mismatch error.
        MarkExpressionAsErrorRecovery(cond,
            ErrorRecoveryReason.DeliberatelyPermissive(
                "a conditional whose branch types have no computed LCA is not reported as a mismatch"));
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a type coercion expression (value to Type).
    /// Validates that the coercion is valid per the language specification.
    /// </summary>
    private SemanticType CheckTypeCoercion(TypeCoercion coercion)
    {
        var sourceType = CheckExpression(coercion.Value);

        // The `as?`/`as!` operators own their failure mode, so the target must be written
        // non-nullable — the operator supplies the optionality. Reject `x as? T?` / `x as! T?`.
        if (coercion.TargetType.IsOptional)
        {
            var op = coercion.Mode == CastFailureMode.Null ? "as?" : "as!";
            AddError(
                $"Redundant '?' on the target of '{op}': the operator already determines the " +
                $"failure mode. Drop the '?' — write '{op} {coercion.TargetType.Name}'.",
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.Semantic.RedundantNullableCastTarget,
                span: coercion.Span);
        }

        // Classify the target the same way a type test is classified: `as?`/`as!` name a runtime type
        // just as `is` does, and a bare generic name denotes none (#1235). Erasure is DISALLOWED here
        // because this site binds a VALUE — lowering `as? list` to the non-generic Sharpy.IList would
        // hand back something other than the type this expression is given, so a bare collection name
        // is filled from the source type or refused like any other open generic.
        //
        // The decided type also becomes the semantic result type below. Without that, filling
        // `b as? Box` to `Box[int]` in codegen while the checker still said `Box` would put the two
        // back into the disagreement this batch exists to remove.
        var decidedTarget = ClassifyTypeTestAnnotation(
            coercion.TargetType,
            lodgeOn: coercion.TargetType,
            subjectType: sourceType,
            siteNoun: "cast",
            erasure: CollectionErasure.Disallowed);

        // A nullable/optional/result SPELLING (`x as? str | None`, `x as? str !int`) is declined by
        // the classifier on purpose — the wrapper decides the value's shape, and the written name
        // adds nothing to that decision. The emitter still has to name the BASE type for the type
        // test it emits, so the base type is recorded here rather than re-derived from the
        // annotation in CodeGen: that was the last annotation-shaped read left in the expression
        // generators (Critical Rule 2, #1670).
        if (decidedTarget == null && coercion.Mode == CastFailureMode.Null)
        {
            var baseAnnotation = coercion.TargetType with
            {
                IsOptional = false,
                IsCSharpNullable = false,
                ErrorType = null
            };
            var baseTargetType = _typeResolver.ResolveTypeAnnotation(baseAnnotation);
            if (baseTargetType is not UnknownType)
            {
                _semanticInfo.SetTypeTestLowering(
                    coercion.TargetType,
                    new TypeTestLowering(TypeTestLoweringKind.ClosedType, baseTargetType));
            }
        }

        // Resolve the target type. For the Null failure mode (`as?`) the operator supplies the
        // optionality, so the non-nullable target is promoted to T? here to form the result type.
        var targetAnnotation = coercion.TargetType;
        if (coercion.Mode == CastFailureMode.Null && !targetAnnotation.IsOptional)
        {
            targetAnnotation = targetAnnotation with { IsOptional = true };
        }
        var targetType = decidedTarget != null
            ? (coercion.Mode == CastFailureMode.Null
                ? new OptionalType { UnderlyingType = decidedTarget }
                : decidedTarget)
            : _typeResolver.ResolveTypeAnnotation(targetAnnotation);

        // If either type is unknown, skip validation to avoid cascading errors
        if (sourceType is UnknownType || targetType is UnknownType)
        {
            return targetType;
        }

        // Get the underlying target type (strip nullable/optional wrapper if present)
        var underlyingTargetType = targetType switch
        {
            NullableType nullable => nullable.UnderlyingType,
            OptionalType optional => optional.UnderlyingType,
            _ => targetType
        };

        // Decide the emission shape here, for BOTH modes, so the emitter never inspects operand types.
        // When the source and stripped target are both plain numeric primitives, record a numeric
        // lowering: `as?` gets AlwaysFits for widening and a None-returning helper for narrowing (#1110);
        // `as!` gets a throwing helper for narrowing and NOTHING for widening, so a widening throw-mode
        // cast keeps its bare C# cast byte-for-byte (#1306).
        // Any other source keeps the emitter's mode default — the type pattern for `as?`, which is the
        // only correct shape for object/reference/optional sources but is uncompilable (CS8121) for
        // concrete numerics, and the bare cast for `as!`, correct for enums, unboxing, `__explicit__`
        // conversions and reference downcasts.
        var lowering = ClassifyNumericCoercion(sourceType, underlyingTargetType, coercion.Mode);
        if (lowering != null)
        {
            _semanticInfo.SetTypeCoercionLowering(coercion, lowering);
        }

        // Validate the coercion
        ValidateTypeCoercion(coercion, sourceType, underlyingTargetType);

        return targetType;
    }

    /// <summary>
    /// Classifies a numeric cast into its emission shape, or returns <c>null</c> when no numeric
    /// lowering applies and the emitter's mode default stands (the type pattern for <c>as?</c>, a bare
    /// C# cast for <c>as!</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applies when BOTH the source and the stripped target are integral or floating primitives —
    /// the full width family, not just int/long/float32/float64 (#1306). <c>decimal</c> is excluded
    /// (no helper overloads; the bare cast is unchanged), as are object, reference, optional/nullable,
    /// type-parameter, <c>bool</c>, <c>char</c> and <c>str</c> operands.
    /// </para>
    /// <para>
    /// The throwing mode records a lowering ONLY for pairs that can fail. A widening <c>as!</c> keeps
    /// its bare cast, so no existing generated C# moves; the narrowing ones stop wrapping silently.
    /// </para>
    /// </remarks>
    private static TypeCoercionLowering? ClassifyNumericCoercion(
        SemanticType source,
        SemanticType target,
        CastFailureMode mode)
    {
        var src = PrimitiveCatalog.GetPrimitiveInfo(source);
        var tgt = PrimitiveCatalog.GetPrimitiveInfo(target);

        if (!IsCoercionNumeric(src) || !IsCoercionNumeric(tgt))
        {
            return null;
        }

        if (!CanNarrowingFail(src!, tgt!))
        {
            // Widening/identity. `as?` still needs a lowering (Optional<T>.Some((T)value)) because its
            // default type pattern is CS8121 on a concrete numeric source; `as!` needs none.
            return mode == CastFailureMode.Null
                ? new TypeCoercionLowering(TypeCoercionLoweringKind.NumericAlwaysFits)
                : null;
        }

        var hub = SourceHubType(src!);
        var hubCast = src!.CSharpName == hub ? null : hub;
        var targetName = HelperTargetName(tgt!);

        return mode == CastFailureMode.Null
            ? new TypeCoercionLowering(
                TypeCoercionLoweringKind.NumericRangeChecked, $"To{targetName}OrNone", hubCast)
            : new TypeCoercionLowering(
                TypeCoercionLoweringKind.NumericChecked, $"To{targetName}", hubCast);
    }

    /// <summary>
    /// True for the integral and floating primitives the numeric cast helpers cover. <c>decimal</c> is
    /// deliberately out: it has no helper overloads and its bare cast must stay unchanged.
    /// </summary>
    private static bool IsCoercionNumeric(PrimitiveCatalog.PrimitiveInfo? info)
        => info != null
            && (info.Kind == PrimitiveCatalog.NumericKind.SignedInteger
                || info.Kind == PrimitiveCatalog.NumericKind.UnsignedInteger
                || info.Kind == PrimitiveCatalog.NumericKind.FloatingPoint);

    /// <summary>
    /// Whether converting <paramref name="src"/> to <paramref name="tgt"/> can fail at runtime — the
    /// question that decides whether a checked helper is emitted at all.
    /// </summary>
    /// <remarks>
    /// A floating target never fails (an out-of-range double→float32 becomes ±∞, and NaN survives).
    /// A floating source into any integral target always can (NaN, ±∞, magnitude). Integral→integral
    /// fails unless the source's whole value range sits inside the target's: same signedness compares
    /// widths; signed→unsigned always admits negatives; unsigned→signed needs a strictly wider target
    /// because the sign bit costs one bit of magnitude.
    /// </remarks>
    private static bool CanNarrowingFail(
        PrimitiveCatalog.PrimitiveInfo src,
        PrimitiveCatalog.PrimitiveInfo tgt)
    {
        if (tgt.Kind == PrimitiveCatalog.NumericKind.FloatingPoint)
        {
            return false;
        }

        if (src.Kind == PrimitiveCatalog.NumericKind.FloatingPoint)
        {
            return true;
        }

        if (src.IsSigned == tgt.IsSigned)
        {
            return src.SizeInBits > tgt.SizeInBits;
        }

        return src.IsSigned || src.SizeInBits >= tgt.SizeInBits;
    }

    /// <summary>
    /// The C# type the operand is cast to before invoking a helper: <c>double</c> for floating sources,
    /// <c>ulong</c> for the one integral source with no implicit conversion to <c>long</c>, and
    /// <c>long</c> for every other integral source.
    /// </summary>
    private static string SourceHubType(PrimitiveCatalog.PrimitiveInfo src)
        => src.Kind == PrimitiveCatalog.NumericKind.FloatingPoint ? "double"
            : src.Kind == PrimitiveCatalog.NumericKind.UnsignedInteger && src.SizeInBits == 64 ? "ulong"
            : "long";

    /// <summary>
    /// The helper-name fragment for a target width — <c>ToInt</c>/<c>ToIntOrNone</c> from <c>int</c>.
    /// </summary>
    private static string HelperTargetName(PrimitiveCatalog.PrimitiveInfo tgt)
        => tgt.CSharpName switch
        {
            "sbyte" => "SByte",
            "byte" => "Byte",
            "short" => "Short",
            "ushort" => "UShort",
            "int" => "Int",
            "uint" => "UInt",
            "long" => "Long",
            "ulong" => "ULong",
            _ => throw new System.InvalidOperationException(
                $"No numeric cast helper for target '{tgt.CSharpName}' — CanNarrowingFail admitted a "
                + "target the helper matrix does not cover.")
        };

    /// <summary>
    /// Validates that a type coercion is valid per the language specification.
    /// Reports errors for invalid casts.
    /// </summary>
    private void ValidateTypeCoercion(TypeCoercion coercion, SemanticType sourceType, SemanticType targetType)
    {
        // Unboxing: object to any type is valid (runtime check) - check this first
        if (IsObjectType(sourceType))
        {
            return; // Valid
        }

        // Numeric to numeric conversions are always valid (may throw at runtime for narrowing)
        if (PrimitiveCatalog.IsNumeric(sourceType) && PrimitiveCatalog.IsNumeric(targetType))
        {
            return; // Valid
        }

        // Check for invalid numeric/bool to string conversion
        // This is a common mistake - users should use str(x) instead
        if (IsStringType(targetType))
        {
            var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(sourceType);
            if (sourceInfo != null && sourceInfo.ClrType != typeof(string))
            {
                // Source is a primitive but not string - reject
                AddError(
                    $"Cannot cast '{sourceType.GetDisplayName()}' to 'str'. Use str(...) instead.",
                    coercion.LineStart, coercion.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidCast,
                    span: coercion.Span);
                return;
            }
        }

        // Check for user-defined __explicit__ conversion operators
        if (HasUserDefinedConversion(sourceType, targetType, DunderNames.Explicit)
            || HasUserDefinedConversion(targetType, sourceType, DunderNames.Explicit))
        {
            return; // Valid — C# will invoke the user-defined explicit operator
        }

        // Check for valid reference type casts (inheritance relationship or interface implementation)
        if (!CanPotentiallyCast(sourceType, targetType))
        {
            AddError(
                $"Cannot cast '{sourceType.GetDisplayName()}' to '{targetType.GetDisplayName()}' (no inheritance relationship).",
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidCast,
                span: coercion.Span);
        }
    }

    /// <summary>
    /// Returns true if the type is the str/string type.
    /// </summary>
    private static bool IsStringType(SemanticType type)
    {
        return type is BuiltinType builtin && (builtin.Name == BuiltinNames.Str || builtin.Name == "string");
    }

    /// <summary>
    /// Returns true if the type is the object type.
    /// </summary>
    private static bool IsObjectType(SemanticType type)
    {
        return type is BuiltinType { Name: "object" } or UserDefinedType { Name: "object" } or UnmappedClrType;
    }

    /// <summary>
    /// Determines if a cast between two types COULD potentially succeed at runtime.
    /// Returns true if there's an inheritance relationship, interface implementation, or unboxing potential.
    /// Returns false if the cast is statically impossible.
    /// </summary>
    private bool CanPotentiallyCast(SemanticType source, SemanticType target)
    {
        // Same type is always castable
        if (source.Equals(target))
            return true;

        // Both must be user-defined types for inheritance checks
        if (source is UserDefinedType sourceUdt && target is UserDefinedType targetUdt)
        {
            // Check if source inherits from target (downcast - always safe)
            if (InheritsFrom(sourceUdt.Symbol, targetUdt.Symbol))
                return true;

            // Check if target inherits from source (upcast - runtime check)
            if (InheritsFrom(targetUdt.Symbol, sourceUdt.Symbol))
                return true;

            // Check if target is an interface that could be implemented
            if (targetUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // Check if source is an interface that the target could implement
            if (sourceUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // No relationship found
            return false;
        }

        // Interface casting is always potentially valid at runtime
        if (source is UserDefinedType && target is UserDefinedType targetType && targetType.Symbol?.TypeKind == TypeKind.Interface)
            return true;

        // Unboxing from object is always valid
        if (IsObjectType(source))
            return true;

        // Boxing to object is always valid
        if (IsObjectType(target))
            return true;

        // For generic types, check the base definition
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            // Same generic definition with potentially different type args (#1330)
            if (sourceGeneric.GenericDefinition != null && targetGeneric.GenericDefinition != null
                && TypeHierarchyService.IsSameType(sourceGeneric.GenericDefinition, targetGeneric.GenericDefinition))
                return true;
            if (sourceGeneric.GenericDefinition == null && targetGeneric.GenericDefinition == null
                && sourceGeneric.Name == targetGeneric.Name)
                return true;
        }

        // Default: allow if types don't fit the checked categories (to be conservative)
        // This handles edge cases and allows the C# compiler to do final validation
        return true;
    }

    private bool HasUserDefinedConversion(SemanticType sourceType, SemanticType targetType, string dunderName)
    {
        var typeSymbol = sourceType switch
        {
            UserDefinedType udt => udt.Symbol,
            _ => null
        };

        if (typeSymbol == null)
            return false;

        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name != dunderName || !method.IsStatic)
                continue;

            if (method.Parameters.Count == 1 && method.ReturnType != null)
            {
                if (method.ReturnType.Equals(targetType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type symbol inherits from another type symbol (directly or indirectly).
    /// </summary>
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)
        => TypeHierarchyService.InheritsFrom(derived, baseType, SemanticBinding);

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        CheckExpression(typeCheck.Value);

        // #1298 (owner decision 2026-08-08): `x is TypeName` is retired as a type test.
        // `is` means reference identity only; the type-test spelling is isinstance.
        // The parser still produces a TypeCheck node (tooling sees the shape), but the
        // semantic phase refuses it. The emitter arm (GenerateTypeCheck) is kept as
        // defence-in-depth but never reached.
        var valueName = typeCheck.Value is Identifier id ? id.Name : "value";
        AddError(
            $"'is' compares references, not types. " +
            $"Use 'isinstance({valueName}, {typeCheck.CheckType.Name})' to test a value's type.",
            typeCheck.LineStart, typeCheck.ColumnStart,
            code: DiagnosticCodes.Semantic.IsTypeTestRetired,
            span: typeCheck.Span);

        return SemanticType.Bool;
    }

    /// <summary>
    /// Type-checks a maybe expression: maybe expr.
    /// The operand must be a NullableType (T | None). The result is OptionalType wrapping the underlying type.
    /// </summary>
    private SemanticType CheckMaybeExpression(MaybeExpression maybeExpr)
    {
        var operandType = CheckExpression(maybeExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        if (operandType is not NullableType nullable)
        {
            AddError(
                $"'maybe' expression requires a nullable type (T | None), but got '{operandType.GetDisplayName()}'",
                maybeExpr.LineStart, maybeExpr.ColumnStart, code: DiagnosticCodes.Semantic.InvalidMaybeExpression,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        if (nullable.UnderlyingType is TypeParameterType typeParam
            && !typeParam.Constraints.Any(c => c is ClassConstraint or StructConstraint))
        {
            AddError(
                $"'maybe' cannot be used with unconstrained generic type parameter '{typeParam.Name}'. The type parameter must be constrained to either a reference type or value type.",
                maybeExpr.LineStart, maybeExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.MaybeOnUnconstrainedTypeParameter,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        return new OptionalType { UnderlyingType = nullable.UnderlyingType };
    }

    /// <summary>
    /// Type-checks a try expression: try expr or try[ExceptionType] expr.
    /// Wraps the operand in Result[T, E] where T is the operand type and E is the exception type.
    /// Default E is Exception, except for 'to' expressions where it's InvalidCastException.
    /// </summary>
    private SemanticType CheckTryExpression(TryExpression tryExpr)
    {
        var operandType = CheckExpression(tryExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Determine the error type
        SemanticType errorType;
        if (tryExpr.ExceptionTypes.Length >= 1)
        {
            // Explicit exception type(s): try[E] expr or try[A | B | C] expr.
            // Resolve each, validate it inherits from Exception, then compute the
            // common base type (most specific shared ancestor) for the Result error.
            var resolved = new List<SemanticType>(tryExpr.ExceptionTypes.Length);
            var exceptionSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
            foreach (var typeAnnotation in tryExpr.ExceptionTypes)
            {
                var resolvedType = _typeResolver.ResolveTypeAnnotation(typeAnnotation);
                if (resolvedType is UnknownType)
                {
                    resolved.Add(resolvedType);
                    continue;
                }

                // Validate it is an Exception subclass (skip when we can't find Exception
                // symbol — fail open so unrelated environments still compile).
                if (exceptionSymbol != null && !IsExceptionSubtype(resolvedType, exceptionSymbol))
                {
                    AddError(
                        $"Type '{typeAnnotation.Name}' in 'try' expression must be a subclass of 'Exception'",
                        typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                        code: DiagnosticCodes.Semantic.TryExceptionTypeNotException,
                        span: typeAnnotation.Span);
                }

                resolved.Add(resolvedType);
            }

            errorType = resolved.Count == 1
                ? resolved[0]
                : FindCommonExceptionBase(resolved, exceptionSymbol);
        }
        else if (tryExpr.Operand is TypeCoercion coercionOperand
            && _semanticInfo.GetTypeCoercionLowering(coercionOperand)
                is not { Kind: TypeCoercionLoweringKind.NumericChecked })
        {
            // Special case: try x to Cat → Result[Cat, InvalidCastException]. That is the exception a
            // reference/unboxing cast throws — but a numeric narrowing throws Sharpy.OverflowError or
            // Sharpy.ValueError (#1306), and Result.Try catches only the error type named here, so
            // pinning InvalidCastException would let those escape the carrier that exists to hold them.
            // Numeric narrowings therefore fall through to the Exception default below, which is also
            // the most specific common base of the two they can throw.
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("InvalidCastException");
            errorType = new UserDefinedType { Name = "InvalidCastException", Symbol = clrSymbol };
        }
        else if (_expectedType is ResultType expectedResult)
        {
            // RFC 3721: infer error type from expected type context (return statement
            // or variable annotation with Result type)
            errorType = expectedResult.ErrorType;
        }
        else if (_currentFunctionReturnType is ResultType enclosingResult
            && _expectedType == null)
        {
            // RFC 3721: infer error type from enclosing function's Result return type
            // Only when there's no explicit type context (e.g., variable annotation)
            errorType = enclosingResult.ErrorType;
        }
        else
        {
            // Default: try expr → Result[T, Exception]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
            errorType = new UserDefinedType { Name = "Exception", Symbol = clrSymbol };
        }

        return new ResultType { OkType = operandType, ErrorType = errorType };
    }

    /// <summary>
    /// Returns true when <paramref name="type"/> is the Exception type itself or a
    /// subclass of it. Walks the base chain via <see cref="TypeHierarchyService"/>.
    /// <para>
    /// A closed generic exception — <c>MyError[int]</c> for <c>class MyError[T](Exception)</c> — is a
    /// <see cref="GenericType"/>, not a <see cref="UserDefinedType"/>, and its derivation is a property
    /// of the definition, so the check reads <see cref="GenericType.GenericDefinition"/>. Without this
    /// the closed spelling that <c>except</c>'s open-generic refusal (SPY0345) tells the user to write
    /// was itself rejected as not-an-Exception — found by running that message's own example.
    /// </para>
    /// </summary>
    private static bool IsExceptionSubtype(SemanticType type, TypeSymbol exceptionSymbol)
    {
        var symbol = type switch
        {
            UserDefinedType { Symbol: { } udtSymbol } => udtSymbol,
            GenericType { GenericDefinition: { } definition } => definition,
            _ => null
        };

        if (symbol == null)
        {
            return false;
        }

        if (TypeHierarchyService.IsSameType(symbol, exceptionSymbol))
        {
            return true;
        }

        return TypeHierarchyService.InheritsFrom(symbol, exceptionSymbol);
    }

    /// <summary>
    /// Computes the most specific common ancestor for a set of exception types used
    /// in a multi-exception try expression (try[A | B | C]). Falls back to the
    /// <c>Exception</c> base type when there is no shared ancestor lower in the chain.
    /// </summary>
    private SemanticType FindCommonExceptionBase(IReadOnlyList<SemanticType> exceptionTypes, TypeSymbol? exceptionSymbol)
    {
        SemanticType fallback = exceptionSymbol != null
            ? new UserDefinedType { Name = "Exception", Symbol = exceptionSymbol }
            : (exceptionTypes.FirstOrDefault(t => t is not UnknownType) ?? SemanticType.Unknown);

        // Start with the ancestor chain of the first type and intersect with each subsequent type's chain.
        IReadOnlyList<SemanticType>? commonChain = null;
        foreach (var t in exceptionTypes)
        {
            if (t is UnknownType)
            {
                continue;
            }

            var chain = GetExceptionAncestorChain(t);
            if (commonChain == null)
            {
                commonChain = chain;
                continue;
            }

            // Keep only ancestors that appear in 'chain' (preserving most-specific order from commonChain).
            var filtered = new List<SemanticType>();
            foreach (var ancestor in commonChain)
            {
                if (chain.Any(c => SemanticTypesAreSame(c, ancestor)))
                {
                    filtered.Add(ancestor);
                }
            }
            commonChain = filtered;
        }

        if (commonChain == null || commonChain.Count == 0)
        {
            return fallback;
        }

        // First element is the most specific common ancestor
        return commonChain[0];
    }

    /// <summary>
    /// Walks the ancestor chain for an exception SemanticType via
    /// <see cref="TypeHierarchyService.GetAncestorChain"/>. BaseType is populated on
    /// CLR-discovered exception symbols during discovery and BuiltinRegistry
    /// initialization (#1596), so no CLR fallback is needed.
    /// </summary>
    private static IReadOnlyList<SemanticType> GetExceptionAncestorChain(SemanticType type)
    {
        return TypeHierarchyService.GetAncestorChain(type);
    }

    /// <summary>
    /// Compares two semantic types for "same type" purposes during ancestor intersection.
    /// Uses <see cref="TypeHierarchyService.IsSameType"/> for user-defined types and
    /// falls back to record equality for others (BuiltinType, Object, etc).
    /// </summary>
    private static bool SemanticTypesAreSame(SemanticType a, SemanticType b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is UserDefinedType ua && b is UserDefinedType ub)
        {
            if (ua.Symbol != null && ub.Symbol != null)
            {
                return TypeHierarchyService.IsSameType(ua.Symbol, ub.Symbol);
            }
            return ua.Name == ub.Name;
        }
        return a.Equals(b);
    }

    /// <summary>
    /// Type-checks a postfix ? operator (expr?). Unwraps Result[T, E] to T (propagating E on error)
    /// or Optional[T] to T (propagating None on absence). The enclosing function must return a
    /// compatible Result or Optional type.
    /// </summary>
    private SemanticType CheckQuestionMarkExpression(QuestionMarkExpression qm)
    {
        // 1. Must be inside a function
        if (_currentFunctionReturnType == null)
        {
            AddError(
                "'?' operator can only be used inside a function",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Validation.QuestionMarkOutsideFunction,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 2. Disallow in finally blocks
        if (_inFinally)
        {
            AddError(
                "'?' operator cannot be used inside a 'finally' block",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Semantic.QuestionMarkInFinally,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 3. Type-check the operand
        var operandType = CheckExpression(qm.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // 4. Handle Result<T, E>
        if (operandType is ResultType result)
        {
            if (_currentFunctionReturnType is ResultType returnResult)
            {
                if (!IsAssignable(result.ErrorType, returnResult.ErrorType))
                {
                    AddError(
                        $"'?' error type '{result.ErrorType.GetDisplayName()}' is not assignable to function return error type '{returnResult.ErrorType.GetDisplayName()}'",
                        qm.LineStart, qm.ColumnStart,
                        code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                        span: qm.Span);
                    return SemanticType.Unknown;
                }
                return result.OkType;
            }
            else
            {
                AddError(
                    $"'?' on Result requires function to return Result, but return type is '{_currentFunctionReturnType.GetDisplayName()}'",
                    qm.LineStart, qm.ColumnStart,
                    code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                    span: qm.Span);
                return SemanticType.Unknown;
            }
        }

        // 5. Handle Optional<T>
        if (operandType is OptionalType optional)
        {
            if (_currentFunctionReturnType is OptionalType)
            {
                return optional.UnderlyingType;
            }
            else
            {
                AddError(
                    $"'?' on Optional requires function to return Optional, but return type is '{_currentFunctionReturnType.GetDisplayName()}'",
                    qm.LineStart, qm.ColumnStart,
                    code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                    span: qm.Span);
                return SemanticType.Unknown;
            }
        }

        // 6. Not Result or Optional
        AddError(
            $"'?' operator requires Result or Optional type, got '{operandType.GetDisplayName()}'",
            qm.LineStart, qm.ColumnStart,
            code: DiagnosticCodes.Validation.QuestionMarkNotResultOrOptional,
            span: qm.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Extract narrowed types from a conditional expression
    /// </summary>

    /// <summary>
    /// Gets the human-readable symbol for a binary operator.
    /// </summary>
    private static string GetOperatorSymbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.FloorDivide => "//",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "**",
        BinaryOperator.MatMul => "@",
        BinaryOperator.BitwiseAnd => "&",
        BinaryOperator.BitwiseOr => "|",
        BinaryOperator.BitwiseXor => "^",
        BinaryOperator.LeftShift => "<<",
        BinaryOperator.RightShift => ">>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.And => "and",
        BinaryOperator.Or => "or",
        BinaryOperator.Is => "is",
        BinaryOperator.IsNot => "is not",
        BinaryOperator.In => "in",
        BinaryOperator.NotIn => "not in",
        BinaryOperator.NullCoalesce => "??",
        BinaryOperator.PipeForward => "|>",
        _ => op.ToString()
    };

    /// <summary>
    /// Gets the human-readable symbol for a unary operator.
    /// </summary>
    private static string GetOperatorSymbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Minus => "-",
        UnaryOperator.Plus => "+",
        UnaryOperator.Not => "not",
        UnaryOperator.BitwiseNot => "~",
        _ => op.ToString()
    };

    private static string GetAssignmentOperatorSymbol(AssignmentOperator op) => op switch
    {
        AssignmentOperator.PlusAssign => "+=",
        AssignmentOperator.MinusAssign => "-=",
        AssignmentOperator.StarAssign => "*=",
        AssignmentOperator.SlashAssign => "/=",
        AssignmentOperator.DoubleSlashAssign => "//=",
        AssignmentOperator.PercentAssign => "%=",
        AssignmentOperator.PowerAssign => "**=",
        AssignmentOperator.AndAssign => "&=",
        AssignmentOperator.OrAssign => "|=",
        AssignmentOperator.XorAssign => "^=",
        AssignmentOperator.LeftShiftAssign => "<<=",
        AssignmentOperator.RightShiftAssign => ">>=",
        AssignmentOperator.MatMulAssign => "@=",
        AssignmentOperator.NullCoalesceAssign => "??=",
        _ => op.ToString()
    };

    private (SemanticType left, SemanticType right) EffectiveOperandTypes(
        BinaryOperator op, Expression left, SemanticType leftType,
        Expression right, SemanticType rightType)
    {
        if (op is BinaryOperator.LeftShift or BinaryOperator.RightShift)
            return (leftType, rightType);

        var resolver = MakeConstantResolver();
        var leftConverts = ImplicitConversions.IsImplicitIntegerConstantConversion(
            left, leftType, rightType, resolver);
        var rightConverts = ImplicitConversions.IsImplicitIntegerConstantConversion(
            right, rightType, leftType, resolver);

        if (leftConverts && !rightConverts)
            return (rightType, rightType);
        if (rightConverts && !leftConverts)
            return (leftType, leftType);

        return (leftType, rightType);
    }

    private void ReportUnsupportedBinaryOperator(
        Node node, string operatorSpelling,
        SemanticType left, SemanticType right,
        IReadOnlyDictionary<string, string>? data = null,
        string? messageSuffix = null)
    {
        var message = $"Type '{left.GetDisplayName()}' does not support operator '{operatorSpelling}' with operand of type '{right.GetDisplayName()}'";
        if (messageSuffix != null)
            message += messageSuffix;
        AddError(message, node.LineStart, node.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
            span: node.Span, data: data);
    }
}
