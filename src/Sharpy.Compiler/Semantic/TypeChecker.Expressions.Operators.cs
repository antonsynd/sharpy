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

        // #1750 (R-U), #1777: for in/not in the container (right) is checked FIRST so its
        // element type can be pushed as the expectation for the needle (left). This lets
        // Some(v)/None()/Ok(v)/Err(e) in the needle position infer from the container.
        if (binOp.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            // #1792: the container goes through the protocol-receiver seam, so a loose `T | None`
            // container dispatches like `T` AND carries the `.Value` the emitted `.Contains` needs
            // when the payload is a struct (a tuple: `(int, int)?` has no Contains — CS1929).
            var containerType = ProtocolReceiver(binOp.Right, CheckExpression(binOp.Right));
            if (containerType is UnknownType)
            {
                CheckExpression(binOp.Left);
                return SemanticType.Unknown;
            }

            var needleType = CheckMembershipNeedle(
                binOp.Left, MembershipNeedleSlot(binOp.Right, containerType));
            if (needleType is UnknownType)
                return SemanticType.Unknown;

            // The membership kind is the checker's fact (#2635, Rule 2), decided by the ONE link
            // routine every chain link also takes (P93 D3): the emitter's In/NotIn arm reads it and
            // never tests a type. Nothing below this return writes the node again.
            var membership = CheckOperatorLink(OperatorSite.Of(binOp), needleType, containerType, operandSlot: null);
            RecordOperatorLink(binOp, membership);
            return membership.Type;
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
        SemanticType? operandSlot;
        using (ScopedValue.Push(ref _typeTestOperand, typeTestOperand))
        {
            leftType = CheckExpression(binOp.Left);
            // The right operand of a user dunder is a store into the selected overload's slot
            // (#1719, StorePosition.OperatorOperand); every other operand is checked plainly.
            (rightType, operandSlot) = CheckOperatorOperand(binOp.Operator, leftType, binOp.Right, binOp);
        }

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // A comparison or identity operator is decided by the ONE link routine (P93 D3, #2648): every
        // link of a comparison chain is this operator's twin and takes the same call, so the two
        // positions cannot get different verdicts, refusals included.
        if (IsComparisonOperator(binOp.Operator) || binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            var link = CheckOperatorLink(OperatorSite.Of(binOp), leftType, rightType, operandSlot);
            RecordOperatorLink(binOp, link);
            return link.Type;
        }

        // #1766: a LiteralString is a str at every value-use route.
        leftType = OperandView(leftType);
        rightType = OperandView(rightType);

        // §10.2.11: a constant operand converts to the other operand's type before promotion
        // A seam-admitted operand converts to its slot before the operator resolves — §10.2.11's
        // constant rule generalized to every value shape the store seam admits.
        var (resultType, effectiveLeftType, effectiveRightType) = InferOperatorResult(
            OperatorSite.Of(binOp), leftType, rightType, operandSlot, isChainLink: false);
        if (resultType == null)
            return SemanticType.Unknown;

        // Constant-fold integer exponentiation so a result that fits a wider integer type
        // widens (e.g. `10 ** 18` → long) and an out-of-range result is diagnosed (SPY0328)
        // instead of being silently truncated by the runtime double cast. Only constant
        // non-negative integer powers are folded; negative exponents keep the existing path
        // (Python: `2 ** -1 == 0.5`). The folded value is recorded so codegen emits a literal (#905).
        if (binOp.Operator == BinaryOperator.Power
            && TypeUtils.IsInteger(leftType) && TypeUtils.IsInteger(rightType))
        {
            var folded = TryFoldIntegerPower(binOp, effectiveLeftType, effectiveRightType);
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
        // `/` (#2517): ONE classifier shared with the augmented `/=` site.
        if (binOp.Operator == BinaryOperator.Divide
            && ClassifyTrueDivision(leftType, rightType, resultType, binOp.Right, ConstantLookup) is { } divisionKind)
        {
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(divisionKind));
        }

        if (binOp.Operator is BinaryOperator.LeftShift or BinaryOperator.RightShift
            && TypeUtils.IsInteger(rightType) && rightType != SemanticType.Int
            && leftType is not UserDefinedType and not GenericType)
        {
            _semanticInfo.SetOperatorLowering(binOp,
                new OperatorLowering(OperatorLoweringKind.ShiftCountCastToInt));
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
            && ClassifyIntegerPower(effectiveLeftType, effectiveRightType) is { } powKind)
        {
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(powKind));
        }

        // `//` and `%` (#1658): ONE classifier shared with the augmented `//=` / `%=` site.
        if (ClassifyFlooredArithmetic(binOp.Operator, leftType, rightType) is { } flooredKind)
        {
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(flooredKind));
        }

        // #1731/#1741: literal-derived string + string (concatenation) and string * int (repetition)
        // — ONE rule (LiteralDerivation) over both PEP 675 forms.
        if (resultType == SemanticType.Str
            && LiteralDerivation.IsConcatOrRepeatDerived(
                _semanticInfo, binOp.Operator, leftType, rightType, binOp.Left, binOp.Right))
        {
            _semanticInfo.SetLiteralDerived(binOp);
        }

        return resultType;
    }

    /// <summary>
    /// Checks the RIGHT operand of a binary operator through the store seam when the LEFT operand's
    /// type declares (or inherits) a dunder for the operator (#1719, plan-499995 Design Decision 6):
    /// the operand is a store into the selected overload's parameter. Returns the operand's recorded
    /// type and the slot it was admitted into — null when no user dunder decides the operand (no
    /// receiver, no overloads, an open slot), or when the operand is a bare <c>None</c> (the #1079
    /// null check and the None-admitting dispatch are inference's decisions). A refusal is reported
    /// here in the operator's phrasing and yields <see cref="UnknownType"/>, so the caller reports
    /// nothing further — at <paramref name="reportAt"/>, the position the operator's own refusal
    /// uses (the binary expression; a chain link's left operand). Shared by the binary form and
    /// every comparison-chain link.
    /// </summary>
    private (SemanticType Type, SemanticType? Slot) CheckOperatorOperand(
        BinaryOperator op, SemanticType leftType, Expression right, Node reportAt)
    {
        var receiver = OperandView(leftType);
        var (candidates, dunder) = OperatorOperandCandidates(op, receiver, right);
        if (candidates.Count == 0)
            return (CheckExpression(right), null);

        var symbol = GetOperatorSymbol(op);

        SemanticType SlotOf(FunctionSymbol candidate)
            => TypeInferenceService.CloseOverReceiver(candidate.Parameters[^1].Type, receiver);

        // An operand with no natural type — `None()` — selects the unique overload whose slot is an
        // Optional; any other count is a refusal by name, with the steer the spelling needs.
        if (NoBindingInferenceType(right) is OptionalType)
        {
            var optional = candidates.Where(c => SlotOf(c) is OptionalType).ToList();
            if (optional.Count != 1)
            {
                var steer = optional.Count == 0
                    ? $"no '{dunder}' overload takes an Optional; declare 'other: T?' or compare with Some(v)"
                    : $"'{dunder}' is ambiguous for None() between "
                        + string.Join(" and ", optional.Select(c => $"'{SlotOf(c).GetDisplayName()}'"))
                        + "; bind it first ('x: T? = None()')";
                AddError(
                    $"Type '{receiver.GetDisplayName()}' does not support operator '{symbol}' with operand 'None()' — {steer}",
                    reportAt.LineStart, reportAt.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    span: reportAt.Span);
                return (SemanticType.Unknown, null);
            }
            return CheckOperandInSlot(receiver, right, SlotOf(optional[0]), dunder, symbol, reportAt);
        }

        // One overload: its slot is the expectation and the seam's verdict is the answer.
        if (candidates.Count == 1)
        {
            var slot = SlotOf(candidates[0]);
            return ContainsTypeParameterType(slot)
                ? (CheckExpression(right), null)
                : CheckOperandInSlot(receiver, right, slot, dunder, symbol, reportAt);
        }

        // Several overloads: the natural type first, then the seam's classification per candidate
        // (side-effect free) and the deterministic betterness core among the applicable ones. A
        // selected slot's accepted verdict is APPLIED here (the float32 re-typing, the narrowed
        // pass-through) so the operand converts to its slot before the operator resolves.
        var natural = CheckExpression(right);
        if (natural is UnknownType)
            return (natural, null);
        var view = OperandView(natural);
        var applicable = candidates
            .Where(c => !ContainsTypeParameterType(SlotOf(c))
                && IsAcceptedVerdict(ClassifyStore(StorePosition.OperatorOperand, right, view, SlotOf(c))))
            .ToList();
        var selected = applicable.Count switch
        {
            0 => null,
            1 => applicable[0],
            _ => ResolveDunderOverload(applicable, view, receiver),
        };
        if (selected == null)
            return (natural, null);

        var selectedSlot = SlotOf(selected);
        CheckStoreQuietly(StorePosition.OperatorOperand, right, view, selectedSlot);
        return (natural, selectedSlot);
    }

    /// <summary>
    /// Checks <paramref name="right"/> under <paramref name="slot"/> as the expectation and takes
    /// the seam's verdict on it; a refusal is reported at the operand in the operator's phrasing.
    /// </summary>
    private (SemanticType Type, SemanticType? Slot) CheckOperandInSlot(
        SemanticType receiver, Expression right, SemanticType slot, string dunder, string symbol, Node reportAt)
    {
        using (EnterStore(StorePosition.OperatorOperand, slot, right,
                   calleeDisplay: receiver.GetDisplayName(), operatorSymbol: symbol, operatorDunder: dunder))
        {
            var operandType = CheckExpression(right);
            if (operandType is UnknownType)
                return (operandType, null);

            return CheckStore(StorePosition.OperatorOperand, right, OperandView(operandType), slot, reportAt, reportAt.Span)
                ? (operandType, slot)
                : (SemanticType.Unknown, null);
        }
    }

    /// <summary>
    /// The dunder overloads (own and inherited) that decide the RIGHT operand of <paramref name="op"/>
    /// on a receiver of type <paramref name="leftType"/> — the equality complement's when the
    /// operator's own dunder is absent (<c>!=</c> answered by <c>__eq__</c>) — with the dunder they
    /// were found under. Empty when no user dunder decides the operand: no receiver, no overloads,
    /// or a bare <c>None</c> operand.
    /// </summary>
    private static (List<FunctionSymbol> Candidates, string Dunder) OperatorOperandCandidates(
        BinaryOperator op, SemanticType leftType, Expression right)
    {
        var none = (new List<FunctionSymbol>(), string.Empty);
        if (UnwrapParenthesized(right) is NoneLiteral)
            return none;

        // Source-declared dunders only: a CLR-discovered operator table (Sharpy.Bytes, List<T>,
        // a stdlib type) spells its parameters in CLR names (`Bytes`), which the store relation does
        // not identify with the builtin alias (`bytes`); those receivers keep the resolver's
        // natural-type path, which the builtin operator table answers first anyway.
        var dunder = TypeInferenceService.BinaryOperatorToDunder(op);
        if (dunder == null
            || TypeInferenceService.OperatorReceiverSymbol(leftType) is not { ClrType: null } receiver)
            return none;

        var candidates = TypeInferenceService.CollectOperatorOverloads(receiver, dunder);
        if (candidates.Count == 0 && TypeInferenceService.EqualityComplementDunder(op) is { } complement)
        {
            candidates = TypeInferenceService.CollectOperatorOverloads(receiver, complement);
            dunder = complement;
        }

        return (candidates.Where(c => c.Parameters.Count >= 2).ToList(), dunder);
    }

    /// <summary>
    /// How an <c>is None</c> / <c>is not None</c> test (the None literal on either side) observes its
    /// subject (#2224, ruling R-EJ). <c>is</c> never runs user code, so no arm may lower to
    /// <c>x == null</c>: that binds whatever <c>operator ==</c> the subject's type carries — the one
    /// synthesized from a Sharpy <c>__eq__</c> or a CLR overload — which threw on a None subject and
    /// answered True for a live one whose <c>__eq__</c> admits None.
    /// <list type="bullet">
    /// <item><see cref="OptionalType"/> → <see cref="OperatorLoweringKind.OptionalNoneTest"/>
    /// (<c>.IsNone</c> / <c>.IsSome</c> on the struct).</item>
    /// <item>a subject that can hold a C# null (<see cref="AdmitsNullPattern"/>) →
    /// <see cref="OperatorLoweringKind.ReferenceNoneTest"/> (<c>x is null</c>).</item>
    /// <item>any other subject — a non-nullable value type, or a type this classifier cannot prove
    /// nullable — → <see cref="OperatorLoweringKind.BoxedNoneTest"/> (<c>(object)x is null</c>). The
    /// boxed form is correct for EVERY type, so an unproven type errs toward it rather than toward a
    /// <c>x is null</c> that C# refuses on a value type.</item>
    /// <item>the None literal itself (<c>None is None</c>) → no record: <c>null == null</c> is a C#
    /// constant with no user operator in reach (ConstEligibility relies on it).</item>
    /// </list>
    /// </summary>
    private static OperatorLoweringKind? ClassifyNoneTest(SemanticType subject)
    {
        if (subject is VoidType)
            return null;
        if (subject is OptionalType)
            return OperatorLoweringKind.OptionalNoneTest;
        return AdmitsNullPattern(subject)
            ? OperatorLoweringKind.ReferenceNoneTest
            : OperatorLoweringKind.BoxedNoneTest;
    }

    /// <summary>
    /// Whether the C# null pattern <c>x is null</c> is legal on <paramref name="type"/>: a reference
    /// type, <c>T | None</c> (a nullable reference or <c>Nullable&lt;T&gt;</c>), or an unconstrained
    /// type parameter. False is always safe (the caller boxes); true must be certain.
    /// </summary>
    private static bool AdmitsNullPattern(SemanticType type)
    {
        type = OperandView(type);
        return type switch
        {
            NullableType => true,
            FunctionType => true,
            TypeParameterType tp => tp.Constraints.IsDefaultOrEmpty,
            BuiltinType builtin => builtin.ClrType is { IsValueType: false },
            // `object` is a symbol-less UserDefinedType singleton: System.Object, a reference.
            UserDefinedType when type == SemanticType.Object => true,
            UserDefinedType udt => udt.ClrType is { } clr
                ? !clr.IsValueType
                : udt.Symbol?.TypeKind is TypeKind.Class or TypeKind.Interface or TypeKind.Delegate,
            GenericType generic => generic.GenericDefinition is { } definition
                && (definition.ClrType is { } clrDefinition
                    ? !clrDefinition.IsValueType
                    : definition.TypeKind is TypeKind.Class or TypeKind.Interface or TypeKind.Delegate),
            _ => false,
        };
    }

    /// <summary>
    /// Classifies how a <c>**</c> lowers — the single classifier shared by the binary site
    /// (<see cref="CheckBinaryOp"/>) and the augmented <c>**=</c> site, so the two cannot
    /// drift (#1700, the #1623 shape). Returns <c>null</c> for user-defined / CLR operands and
    /// non-integer types (float/decimal are handled separately at the call site).
    /// <para>The integer arm is <see cref="IntegerPowerRules.Classify"/> — the SAME call whose
    /// <see cref="IntegerPowerRules.Classification.ResultType"/> the checker records for the
    /// expression, so the emitted overload and the recorded type are one decision, not two
    /// (#1700). Both sites pass the EFFECTIVE operand types (§10.2.11 constant conversion applied
    /// first), so <c>u64 ** 2</c> classifies as the <c>(ulong, ulong)</c> pair it emits.</para>
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
        {
            // The same predicate that typed the expression float32 (#2189): Math.Pow's double is
            // narrowed back to the recorded width.
            return TypeInferenceService.IsFloat32Arithmetic(leftType, rightType)
                ? OperatorLoweringKind.Float32Pow
                : OperatorLoweringKind.FloatPow;
        }

        return IntegerPowerRules.Classify(leftType, rightType)?.Kind;
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
    /// Classifies how a <c>/</c> lowers, shared by the binary site and the augmented <c>/=</c> site
    /// (target = left operand, value = divisor) so the two cannot drift (#2517). Python raises
    /// <c>ZeroDivisionError</c> where IEEE gives <c>inf</c>/<c>nan</c> (and where C# decimal throws
    /// <c>DivideByZeroException</c>, or refuses a constant zero with CS0020), so every numeric true
    /// division whose divisor might be zero routes through the guarded <c>Builtins.TrueDiv</c>:
    /// <list type="bullet">
    /// <item>integer ⊗ integer with a float64 result → <see cref="OperatorLoweringKind.GuardedTrueDivisionCastLeft"/>
    /// (<c>TrueDiv((double)(x), y)</c>);</item>
    /// <item>any other numeric-primitive pair (a float32/float64 or decimal operand) →
    /// <see cref="OperatorLoweringKind.GuardedTrueDivision"/> (<c>TrueDiv(x, y)</c>; C# overload
    /// resolution picks the float64, float32 or decimal overload from the promoted operand types,
    /// matching the inferred result type);</item>
    /// <item>a divisor that is a constant known non-zero — a literal or a folded const name
    /// (<see cref="TrueDivisionRules.IsNonZeroConstantDivisor"/>) — cannot trap,
    /// so the pair keeps the native operator — <see cref="OperatorLoweringKind.TrueDivisionCastLeft"/>
    /// for integers, no record otherwise. That keeps <c>1 / 4</c> and <c>2.0 / 8.0</c> C# constant
    /// expressions (a <c>Final</c> constant, a parameter default) exactly as before;</item>
    /// <item>a user <c>__truediv__</c> type, a CLR <c>op_Division</c> type or a generic → <c>null</c>:
    /// no record, native <c>/</c> (<c>Complex</c> guards its own <c>operator /</c>).</item>
    /// </list>
    /// </summary>
    private static OperatorLoweringKind? ClassifyTrueDivision(
        SemanticType leftType, SemanticType rightType, SemanticType resultType, Expression divisor,
        TrueDivisionRules.ConstantLookup lookup)
    {
        if (leftType is UserDefinedType or GenericType || rightType is UserDefinedType or GenericType)
            return null;

        var leftNonInteger = PrimitiveCatalog.IsDecimal(leftType) || PrimitiveCatalog.IsFloatingPoint(leftType);
        var rightNonInteger = PrimitiveCatalog.IsDecimal(rightType) || PrimitiveCatalog.IsFloatingPoint(rightType);
        if (!leftNonInteger && !rightNonInteger)
        {
            if (resultType != SemanticType.Double)
                return null;
            return TrueDivisionRules.IsNonZeroConstantDivisor(divisor, lookup)
                ? OperatorLoweringKind.TrueDivisionCastLeft
                : OperatorLoweringKind.GuardedTrueDivisionCastLeft;
        }

        if (!IsTrueDivisionNumeric(leftType) || !IsTrueDivisionNumeric(rightType))
            return null;

        return TrueDivisionRules.IsNonZeroConstantDivisor(divisor, lookup) ? null : OperatorLoweringKind.GuardedTrueDivision;
    }

    private static bool IsTrueDivisionNumeric(SemanticType type)
        => PrimitiveCatalog.IsInteger(type) || PrimitiveCatalog.IsFloatingPoint(type) || PrimitiveCatalog.IsDecimal(type);

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
    /// integers and the exponent is non-negative; emits SPY0328 and returns
    /// <see cref="SemanticType.Unknown"/> when the value fits no integer width the pair can
    /// produce. Returns <c>null</c> when the expression is not a constant non-negative integer
    /// power (caller keeps the regular inference result). (#905)
    /// <para><b>The folded type is the pair's type, not the value's type alone</b> (#1700): a
    /// <c>uint64</c>-classified pair folds to <c>uint64</c> even when the value would fit
    /// <c>int32</c>, because <c>k: uint64 = 4UL ** 2</c> must store — folding it to <c>int32</c>
    /// made the constant form refuse (SPY0220) a program whose variable form runs. For a signed
    /// pair the <c>int</c> → <c>long</c> ladder still widens by value, exactly as it does for
    /// <c>2 ** 62</c>; the trailing <c>ulong</c> rung keeps a ulong-operand fold whose value
    /// exceeds <c>long</c> (<c>2 ** 63UL</c>) accepted.</para>
    /// </summary>
    private SemanticType? TryFoldIntegerPower(
        BinaryOp binOp, SemanticType leftType, SemanticType rightType)
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

        // The folded VALUE is re-derived into an IrConstant by the lowering pass (E2 #1056), which
        // reads the recorded TYPE to choose the literal's suffix; the type checker keeps only the
        // result-type + overflow decision here. Both sides read the same pure
        // IntegerConstantEvaluator, so they cannot diverge.
        //
        // A ulong-classified pair folds at its own width: `4UL ** 2` is uint64, not int32.
        if (IntegerPowerRules.Classify(leftType, rightType)?.ResultType == SemanticType.ULong)
        {
            if (result >= 0 && result <= ulong.MaxValue)
                return SemanticType.ULong;

            ReportIntegerPowerOverflow(binOp);
            return SemanticType.Unknown;
        }

        if (result >= int.MinValue && result <= int.MaxValue)
            return SemanticType.Int;

        if (result >= long.MinValue && result <= long.MaxValue)
            return SemanticType.Long;

        // A ulong-typed operand widens a signed-classified fold past long (#1700) — the exponent
        // side of `2 ** 63UL`, whose value is a legal ulong literal.
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
        var (leftTruthTestable, leftType) = CheckTruthinessTest(andOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(andOp.Right);
            return SemanticType.Unknown;
        }

        if (!leftTruthTestable)
        {
            ReportNotTruthTestable(andOp.Left, leftType, "Operand of 'and' must be truth-testable");
        }

        var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);

        bool rightTruthTestable;
        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNarrowed);
            (rightTruthTestable, rightType) = CheckTruthinessTest(andOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        if (!rightTruthTestable)
        {
            ReportNotTruthTestable(andOp.Right, rightType, "Operand of 'and' must be truth-testable");
        }

        return SemanticType.Bool;
    }

    private SemanticType CheckBooleanOrOp(BinaryOp orOp)
    {
        var (leftTruthTestable, leftType) = CheckTruthinessTest(orOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(orOp.Right);
            return SemanticType.Unknown;
        }

        if (!leftTruthTestable)
        {
            ReportNotTruthTestable(orOp.Left, leftType, "Operand of 'or' must be truth-testable");
        }

        // Expression-level narrowing (#1080): the right operand is evaluated only when the left is
        // falsy, so the left's NEGATIVE narrowings hold inside it (e.g. `x is None or use(x + 1)` —
        // the RHS sees x non-None). The narrowings do not leak past the operand.
        var leftNegativeNarrowed = ExtractNarrowedTypes(orOp.Left, false);

        bool rightTruthTestable;
        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNegativeNarrowed);
            (rightTruthTestable, rightType) = CheckTruthinessTest(orOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        if (!rightTruthTestable)
        {
            ReportNotTruthTestable(orOp.Right, rightType, "Operand of 'or' must be truth-testable");
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
            var pipedCallType = CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
            // #1675, as CheckFunctionCall does once every route has had its chance: an inline
            // `out x: auto` / `out let x` of the pipe's call still Unknown never met a resolved
            // parameter, and is refused by name (SPY0203), never left to the emitter (plan-775c50 D6).
            RefuseUnresolvedAutoOutBindings(funcCall);
            return pipedCallType;
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

            if (!IsAssignable(leftType, ft.ParameterTypes[0]))
            {
                ReportValueTypeMismatch(
                    $"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.Left, ft.ParameterTypes[0],
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
                if (!IsAssignable(leftType, firstParam.Type))
                {
                    ReportValueTypeMismatch(
                        $"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.Left, firstParam.Type,
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
                // Record the resolved call target for codegen (and run the symbol-carried checks)
                // — #1438. The piped value is the call's implicit first positional argument, so
                // the argument-binding checks bind it exactly as this route does (#1956).
                RecordResolvedCallTarget(call, funcSymbol, implicitFirstArgument: binOp.Left);

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

                    // #1675 on the pipe route (plan-775c50 D6): an inline `out x: auto` / `out let x`
                    // argument takes this parameter's type, as on the direct-call route. A still-open
                    // generic parameter is left to the C# inference it has today.
                    if (i > 0 && SubstitutedParameterType(param.Type, null) is { } closedParamType)
                        WriteBackAutoOutBindingType(call.Arguments[i - 1], closedParamType);

                    if (!IsAssignable(argType, param.Type))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                        ReportValueTypeMismatch(
                            $"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{param.Name}' of type '{param.Type.GetDisplayName()}'",
                            argNode, param.Type,
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
                        if (SubstitutedParameterType(param.Type, null) is { } closedKwargType)
                            WriteBackAutoOutBindingType(kwarg.Value, closedKwargType);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                                span: kwarg.Span ?? kwarg.Value.Span);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            ReportValueTypeMismatch(
                                $"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.Value, param.Type,
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

            // The signature the call binds, as a direct call binds it (InferFunctionTypeSignature): a
            // method's receiver-substituted formals, its method-level type parameters inferred.
            var (pipeParamTypes, _) = InferFunctionTypeSignature(ft, allArgTypes);

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                // #1675 on the pipe route (plan-775c50 D6): an inline `out x: auto` / `out let x`
                // argument takes this parameter's type, as CheckLambdaCall writes it back on the
                // direct-call route for a method, a static method, a generic class's method and a
                // module-qualified function.
                if (i > 0)
                    WriteBackAutoOutBindingType(call.Arguments[i - 1], pipeParamTypes[i]);

                if (!IsAssignable(allArgTypes[i], pipeParamTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                    ReportValueTypeMismatch(
                        $"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{pipeParamTypes[i].GetDisplayName()}' is expected",
                        argNode, pipeParamTypes[i],
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
        // so -2147483648 is int and -9223372036854775808 is long (#1304). Only a SIGNED literal
        // negates as a literal; `-1U` / `-1UL` are the unary operator over an unsigned operand and
        // take the operator path below, exactly as `-v` does for a `uint32`/`uint64` variable —
        // int64 and SPY0223, C#'s own answers (#2340: this arm typed `-1U` 'uint32' while C# typed
        // it 'long', and passed `-1UL` to C# as CS0023, both SPY0908).
        if (unOp.Operator == UnaryOperator.Minus && unOp.Operand is IntegerLiteral il
            && Shared.IntegerLiteralClassifier.NegatesAsLiteral(il.Suffix))
        {
            var result = Shared.IntegerLiteralClassifier.ClassifyNegated(il.Value, il.Suffix);
            if (result.IsError)
            {
                AddError(result.ErrorMessage!,
                    unOp.LineStart, unOp.ColumnStart,
                    code: DiagnosticCodes.Semantic.NumericLiteralOutOfRange,
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

        // `not` operands go through truthiness, not generic operator inference (#1558, #1570)
        if (unOp.Operator == UnaryOperator.Not)
        {
            var (notTruthTestable, notOperandType) = CheckTruthinessTest(unOp.Operand);
            if (notOperandType is UnknownType)
                return SemanticType.Unknown;
            if (!notTruthTestable)
            {
                ReportNotTruthTestable(unOp.Operand, notOperandType, "Operand of 'not' must be truth-testable");
                return SemanticType.Unknown;
            }
            return SemanticType.Bool;
        }

        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
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
        // Each adjacent pair is a LINK, and a link is its binary twin (P93 D3, #2648): `a < b` and
        // `b < c` are decided by the ONE link routine the binary operator takes (CheckOperatorLink),
        // diagnostics and lowering alike. What only a chain has stays here: the operands are checked
        // once each, and a middle `None` operand read by two links takes the typed-null cast only when
        // both agree (noneSlots).
        //
        // An operand is typed as the twin that reads it FIRST types it (owner ruling 2026-11-15, plan
        // P93 R2, "own type, cast at needle"): a middle operand is link i-1's right operand, so it is
        // typed as `a op b` types its `b` — and when link i reads it as its `in` needle, that link
        // binds it at its slot (ClassifyMembership's needle binding), exactly as it binds a named
        // needle (`t == p in ts`). Only the head (operand 0) is read by one link alone, so only a head
        // needle is checked under its container's slot, as the twin `x in c` checks it.

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // The links that are membership tests (#1777): their containers take the twin's setup.
        var needleIndices = new HashSet<int>();
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var op = TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[i]);
            if (op is BinaryOperator.In or BinaryOperator.NotIn)
                needleIndices.Add(i);
        }

        var operandTypes = new SemanticType?[chain.Operands.Length];
        var operandSlots = new SemanticType?[chain.Operands.Length];

        // P93 D4: a head spelled like a type (`c is Outer.Inner is d`) whose dotted name is not an enum
        // member is refused by link 0 (SPY0349, CheckTypeCheck's rule). Its operand names a type, not a
        // value, so it is never checked as an expression: it is recorded Unknown, which every chain
        // walker below (and the validators) skips.
        if (chain.HeadCheckType is { } headCheckType && EnumOfMemberSpelledAsType(headCheckType) == null)
        {
            operandTypes[1] = SemanticType.Unknown;
            _semanticInfo.SetExpressionType(chain.Operands[1], SemanticType.Unknown);
            MarkExpressionAsErrorRecovery(chain.Operands[1], ErrorRecoveryReason.Propagated(
                "a chain head spelled like a type names no enum member (SPY0349); its operand is a type, not a value"));
        }

        // The setup the twin `x in c` gives link `ni`'s container: its protocol-receiver view and the
        // slot the binary form pushes (MembershipNeedleSlot), which RECORDS the container's facts (a
        // tuple's array bridge, a Direct projection). Called once per `in` link. The slot it returns is
        // used only for a head needle: a middle needle is never checked under it (R2).
        SemanticType? SetUpContainer(int ni)
        {
            if (operandTypes[ni + 1] is UnknownType)
                return null;
            var containerView = ProtocolReceiver(chain.Operands[ni + 1], operandTypes[ni + 1]!);
            return MembershipNeedleSlot(chain.Operands[ni + 1], containerView);
        }

        // Pass 1: every operand in source order, as the link that reads it first types it. The RIGHT
        // operand of a link whose left operand's type has a user dunder goes through the operand seam
        // exactly as the binary form does (#1719) — one decision for `d == v` and for `a == d == v`; a
        // container (the right operand of an `in` link) is checked plainly, as the twin `x in c` checks
        // it; and a middle operand that is also the next link's needle is no exception (R2):
        // `t == (1, 2) in ts` compares the `tuple[int, int]` its twin `t == (1, 2)` compares. A head
        // needle is checked as soon as its container is, under that container's slot, as the twin
        // `x in c` checks it — so before operands 2.., which may read a name it binds
        // (`(y := 2) in xs == [y]`).
        //
        // The subject of an `is (not) None` link reads the honest, un-narrowed value, as the twin
        // `x is None` reads it (CheckBinaryOp's _typeTestOperand): read narrowed, a narrowing made stale
        // earlier in the chain unwrapped an empty Optional at run time where the twin answers (#2648).
        // Owner ruling 2026-11-16: each link reads a shared operand as its twin reads it. Here that is
        // the operand every reading link tests against None — the head, the tail, or one between two
        // `None`s.
        // TODO(#2648): an operand another kind of link also reads is still read narrowed by both. Where
        // the None test reads it first (`None is not x < 5`) that is not its twin's read: the ruling
        // wants the honest value there and the narrowing at the other link's own read.
        bool IsNoneTestLink(int link, int noneOperand) =>
            chain.Operands[noneOperand] is NoneLiteral
            && TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[link])
                is BinaryOperator.Is or BinaryOperator.IsNot;
        bool ReadOnlyByNoneTests(int k) =>
            (k == 0 || IsNoneTestLink(k - 1, k - 1))
            && (k == chain.Operators.Length || IsNoneTestLink(k, k + 1));

        var headIsNeedle = needleIndices.Contains(0);
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            if (i == 0 && headIsNeedle)
                continue;

            if (operandTypes[i] == null)
            {
                using var honestRead = ScopedValue.Push(ref _typeTestOperand,
                    ReadOnlyByNoneTests(i) ? UnwrapParenthesized(chain.Operands[i]) : _typeTestOperand);
                if (i > 0 && !needleIndices.Contains(i - 1)
                    && operandTypes[i - 1] is { } previous && previous is not UnknownType)
                {
                    var linkOperator = TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[i - 1]);
                    (operandTypes[i], operandSlots[i]) = CheckOperatorOperand(
                        linkOperator, previous, chain.Operands[i], chain.Operands[i - 1]);
                }
                else
                {
                    operandTypes[i] = CheckExpression(chain.Operands[i]);
                }
            }

            if (i == 1 && headIsNeedle)
                operandTypes[0] = CheckMembershipNeedle(chain.Operands[0], SetUpContainer(0));
        }

        // Pass 2, in source order: every other `in` link's container takes the twin's setup whether or
        // not it is also a needle (`a in b in c`); the slot it returns is discarded, since a middle
        // needle is typed as its left twin types it (R2) and admitted at the slot by the link routine:
        // by its type, or by its value when it is a constant (R3).
        for (int ni = 1; ni < chain.Operators.Length; ni++)
        {
            if (needleIndices.Contains(ni))
                _ = SetUpContainer(ni);
        }

        // Each link through the binary operator's own routine, and its record. Every link gets a
        // record — an Unknown-operand or refused link records the native form under its operator's
        // shape — so the emitter reads one fact per operator and never needs a fallback (#1642).
        var links = ImmutableArray.CreateBuilder<ComparisonLinkLowering>(chain.Operators.Length);
        // The typed-null slot each link proposes for its None operand (#2238, cure A), per operand.
        var noneSlots = new SemanticType?[chain.Operators.Length, 2];
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var link = CheckOperatorLink(
                OperatorSite.OfLink(chain, i), operandTypes[i]!, operandTypes[i + 1]!, operandSlots[i + 1],
                headCheckType: i == 0 ? chain.HeadCheckType : null,
                isChainLink: true, needleByTypeAlone: i > 0 && !IsConstantNeedle(chain.Operands[i]));
            links.Add(link.Lowering);
            if (link.NoneSlot is { } slot)
                noneSlots[i, link.NoneOnLeft ? 0 : 1] = slot;
        }

        // A None operand is emitted ONCE and read by both adjacent links, so it is cast to the typed-null
        // slot only when every link reading it proposes that same slot (the binary form's fact, #2238).
        // A middle None between two different CLR types (or a CLR type and a non-CLR operand) keeps the
        // bare `null`, which reference-converts to both sides.
        for (int k = 0; k < chain.Operands.Length; k++)
        {
            var asRight = k > 0 ? noneSlots[k - 1, 1] : null;
            var asLeft = k < chain.Operators.Length ? noneSlots[k, 0] : null;
            var readers = (k > 0 ? 1 : 0) + (k < chain.Operators.Length ? 1 : 0);
            var proposals = new[] { asRight, asLeft }.Where(p => p != null).ToList();
            if (proposals.Count == readers && proposals.Distinct().Count() == 1)
                _semanticInfo.SetArgumentSlotCast(chain.Operands[k], proposals[0]!);
        }

        _semanticInfo.SetComparisonChainLowering(chain, new ComparisonChainLowering(links.MoveToImmutable()));

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    /// <summary>
    /// The verdict of one comparison, identity or membership operator application (P93 D3): the
    /// expression's type (<see cref="UnknownType"/> when the link was refused or an operand was
    /// unknown), its lowering, and the typed-null slot of a CLR-reference <c>== None</c> /
    /// <c>!= None</c> operand (#2238) with the side it is on.
    /// </summary>
    private readonly record struct LinkVerdict(
        SemanticType Type, ComparisonLinkLowering Lowering, SemanticType? NoneSlot = null, bool NoneOnLeft = false)
    {
        public static LinkVerdict Refused(ComparisonLinkShape shape)
            => new(SemanticType.Unknown, new ComparisonLinkLowering(OperatorLoweringKind.Native, null, shape));
    }

    /// <summary>The binary emitter a link of <paramref name="site"/>'s operator lowers through.</summary>
    private static ComparisonLinkShape LinkShapeOf(OperatorSite site) => site.Operator switch
    {
        BinaryOperator.In or BinaryOperator.NotIn => ComparisonLinkShape.Membership,
        BinaryOperator.Is or BinaryOperator.IsNot when site.Left is NoneLiteral || site.Right is NoneLiteral
            => ComparisonLinkShape.NoneTest,
        BinaryOperator.Is or BinaryOperator.IsNot => ComparisonLinkShape.Identity,
        _ => ComparisonLinkShape.Comparison,
    };

    /// <summary>
    /// THE link routine (P93 D3, #2648): decides one comparison (<c>==</c> <c>!=</c> <c>&lt;</c>
    /// <c>&lt;=</c> <c>&gt;</c> <c>&gt;=</c>), identity (<c>is</c>, <c>is not</c>) or membership
    /// (<c>in</c>, <c>not in</c>) operator over its already-checked operands, and reports every
    /// diagnostic the operator draws. Called by the binary site (<see cref="CheckBinaryOp"/>) and by
    /// every link of <see cref="CheckComparisonChain"/>, so a chain link is its binary twin in every
    /// outcome — a refusal included (owner ruling, #2648 comments 6865267508 and 6867353616). The
    /// classifiers it owns: <see cref="ClassifyMembership"/>, the void-comparand gate (SPY0329),
    /// <see cref="ClassifyComparisonLowering"/>, <see cref="ClassifyNoneTest"/>,
    /// <see cref="ClassifyEnumIdentity"/>, and the value-type <c>is</c> warning; neither site
    /// calls one of them itself.
    /// <para>For a membership link <paramref name="leftType"/> is the needle's type and
    /// <paramref name="rightType"/> the container's. A binary needle, or a chain's head needle, was
    /// already checked under the slot <see cref="MembershipNeedleSlot"/> gave it; a chain's MIDDLE
    /// needle was checked as its left link's right operand, its own type (owner ruling 2026-11-15,
    /// plan-67a01f R2). A non-constant middle (<paramref name="needleByTypeAlone"/>) is admitted at the
    /// slot by that type alone, as a named needle is; a constant one (<see cref="IsConstantNeedle"/>)
    /// by its value where it fits, as the binary <c>1 in xs</c> admits it (owner ruling 2026-11-15,
    /// plan-67a01f R3, "By value, cast"). <paramref name="operandSlot"/> is the user-dunder
    /// slot the right operand was admitted into (<see cref="CheckOperatorOperand"/>).
    /// <paramref name="headCheckType"/> is set for link 0 of a chain whose head was spelled like a
    /// type (P93 D4): that link takes <see cref="CheckTypeCheck"/>'s rule
    /// (<see cref="TypeSpelledIdentity"/>).</para>
    /// <para>The caller records the verdict: the binary site on its node
    /// (<see cref="RecordOperatorLink"/>), the chain in its <see cref="ComparisonLinkLowering"/>.</para>
    /// </summary>
    private LinkVerdict CheckOperatorLink(
        OperatorSite site, SemanticType leftType, SemanticType rightType, SemanticType? operandSlot,
        TypeAnnotation? headCheckType = null, bool isChainLink = false, bool needleByTypeAlone = false)
    {
        var op = site.Operator;
        var shape = LinkShapeOf(site);

        // P93 D4: `c is Color.RED is d` — link 0 of a chain whose head the parser read as a type test.
        if (headCheckType != null)
            return TypeSpelledIdentity(headCheckType, site, leftType, rightType);

        // Skip validation if either operand is Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
            return LinkVerdict.Refused(shape);

        // #1766: a LiteralString is a str at every value-use route.
        leftType = OperandView(leftType);
        rightType = OperandView(rightType);

        // #1750 (R-U): the needle-vs-slot check, the ONE place a membership kind is decided (#2635).
        // The container is asked through the protocol-receiver view (#1792): a loose `T | None`
        // container answers as `T`.
        if (op is BinaryOperator.In or BinaryOperator.NotIn)
        {
            var membershipKind = ClassifyMembership(site, leftType, ProtocolReceiverView(rightType), needleByTypeAlone);
            return new LinkVerdict(SemanticType.Bool, new ComparisonLinkLowering(membershipKind, null, shape));
        }

        // Reject void-returning call operands in equality comparisons (#911). A `None`-typed
        // (VoidType) operand that is NOT the literal `None` is a void-returning call used as a
        // comparand (e.g. `s == f()` where `f() -> None`). Python would evaluate the call and
        // compare against None, but that is almost certainly a bug, and Sharpy already rejects
        // void-call *assignment* (SPY0229). Gating here — before InferBinaryOpType — also
        // guarantees the literal-shape invariant the equality lowering relies on: any VoidType
        // operand that reaches inference/lowering is the `None` literal. Scope is ==/!= only;
        // other operators already fail inference with SPY0222. (Axiom 3 > Axiom 2.)
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            var voidOperandFound = false;
            foreach (var (operand, operandType) in new[] { (site.Left, leftType), (site.Right, rightType) })
            {
                if (operandType is not VoidType || operand is NoneLiteral)
                    continue;
                AddError(
                    "Expression of type 'None' has no value and cannot be used as a comparison operand; call it as a statement and compare separately",
                    operand.LineStart,
                    operand.ColumnStart,
                    code: DiagnosticCodes.Semantic.VoidComparisonOperand,
                    span: operand.Span);
                voidOperandFound = true;
            }
            if (voidOperandFound)
                return LinkVerdict.Refused(shape);
        }

        // A string-backed enum member compares equal to its backing string — CPython's StrEnum
        // does (`LogLevel.INFO == "INFO"` is True), and the emitted class's implicit conversion
        // makes the C# comparison bind. Answered here rather than in the operator registry, which
        // keys on type identity and would need an entry per enum (#1284).
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual
            && ((IsStringBackedEnum(leftType) && rightType is BuiltinType { Name: BuiltinNames.Str })
                || (IsStringBackedEnum(rightType) && leftType is BuiltinType { Name: BuiltinNames.Str })))
        {
            return new LinkVerdict(SemanticType.Bool,
                new ComparisonLinkLowering(OperatorLoweringKind.Native, BinaryOpLowering.NativeOperator, shape));
        }

        var (resultType, _, _) = InferOperatorResult(site, leftType, rightType, operandSlot, isChainLink);
        if (resultType == null)
            return LinkVerdict.Refused(shape);

        // Record how a comparison should be lowered by codegen — the equality strategy (#886:
        // tuples and CLR types that resolve via Equals, no op_Equality, must emit an Equals call;
        // #901: reference-type ==/!= None is a null pattern) rides the IR transport, the ordering
        // kind (#1623: ordinal string compare, constrained type-parameter CompareTo) the
        // OperatorLowering tag.
        //
        // Invariant (#911): any VoidType operand reaching this point is guaranteed to be the
        // `None` literal — void-returning call operands were rejected above with SPY0329. This
        // is what makes the NoneCheck lowering's AST-shape operand selection well-defined. Three
        // consumers rely on it: InferBinaryOpType/GetBinaryOpLowering (TypeInferenceService),
        // the emitter's NoneCheck branches (RoslynEmitter.Expressions.Operators / .Statements.
        // ControlFlow), and OperatorValidator (suppress-only, never selects operands).
        if (IsComparisonOperator(op))
        {
            var comparison = ClassifyComparisonLowering(op, leftType, rightType);

            // A CLR-reference `== None` / `!= None` emits its None operand as a typed null so C# binds
            // the type's same-type operator == (#2238, cure A) — the argument-slot cast fact (#1721).
            return _typeInference.ClrReferenceNoneComparandSlot(op, leftType, rightType) is { } slot
                ? new LinkVerdict(resultType, comparison, slot, NoneOnLeft: leftType is VoidType)
                : new LinkVerdict(resultType, comparison);
        }

        // `is` / `is not`.
        OperatorLoweringKind? identityKind;
        var isEnumIdentity = false;
        if (shape == ComparisonLinkShape.NoneTest)
        {
            // `x is None` / `x is not None` / `None is x` (#2224, ruling R-EJ): `is` never runs user
            // code — every None test records HOW its subject is tested, so the emitter never falls back
            // to a `x == null` that binds a user `operator ==` (synthesized from `__eq__`, or a CLR
            // overload).
            identityKind = ClassifyNoneTest(site.Right is NoneLiteral ? leftType : rightType);
        }
        else
        {
            // `is` / `is not` with an int-backed enum operand (#2669): identity is equality.
            identityKind = ClassifyEnumIdentity(leftType, rightType);
            isEnumIdentity = identityKind != null;
        }

        // Warn when is/is not is used with value types — identity comparison is
        // meaningless because value types are boxed, so the result is always False.
        // Never for an enum operand (#2669): its `is` is lowered to value equality above, so the
        // warning's claim would be false. The exemption reads the recorded decision, not
        // IsValueType, so it holds whether or not an enum reports itself as a value type.
        if (!isEnumIdentity)
        {
            var effectiveLeft = leftType is NullableType nl ? nl.UnderlyingType : leftType;
            var effectiveRight = rightType is NullableType nr ? nr.UnderlyingType : rightType;

            if (effectiveLeft.IsValueType && effectiveRight.IsValueType)
            {
                var opSymbol = op == BinaryOperator.Is ? "is" : "is not";
                _diagnostics.AddWarning(
                    $"'{opSymbol}' used with value types '{leftType.GetDisplayName()}' and " +
                    $"'{rightType.GetDisplayName()}' — identity comparison is always False " +
                    "due to boxing; use '==' or '!=' instead",
                    site.Line,
                    site.Column,
                    _currentFilePath,
                    code: DiagnosticCodes.Validation.IsWithValueTypes,
                    phase: CompilerPhase.TypeChecking);
            }
        }

        return new LinkVerdict(resultType,
            new ComparisonLinkLowering(identityKind ?? OperatorLoweringKind.Native, null, shape));
    }

    /// <summary>
    /// Records a <see cref="LinkVerdict"/> on the binary operator it was decided for, exactly as the
    /// binary site always recorded these facts: the operator lowering kind (absent = native), the
    /// equality strategy (absent = the native operator), and the typed-null cast of a CLR-reference
    /// <c>None</c> comparand.
    /// </summary>
    private void RecordOperatorLink(BinaryOp binOp, LinkVerdict verdict)
    {
        if (verdict.Lowering.Equality is { } equality && equality != BinaryOpLowering.NativeOperator)
            _semanticInfo.SetBinaryOpLowering(binOp, equality);
        if (verdict.Lowering.Kind != OperatorLoweringKind.Native)
            _semanticInfo.SetOperatorLowering(binOp, new OperatorLowering(verdict.Lowering.Kind));
        if (verdict.NoneSlot is { } slot)
            _semanticInfo.SetArgumentSlotCast(verdict.NoneOnLeft ? binOp.Left : binOp.Right, slot);
    }

    /// <summary>
    /// The result type of a binary operator over its checked operands, or null after reporting the
    /// refusal (SPY0222) — shared by the arithmetic path of <see cref="CheckBinaryOp"/> and the link
    /// routine. §10.2.11: a constant operand converts to the other operand's type before promotion,
    /// and a seam-admitted operand (<paramref name="operandSlot"/>) to its slot. The effective operand
    /// types are returned for the arithmetic classifiers. A chain link's middle operand is read by two
    /// links, so a chain link only ADDS a constant cast and never forgets one the other link recorded.
    /// </summary>
    private (SemanticType? Result, SemanticType EffectiveLeft, SemanticType EffectiveRight) InferOperatorResult(
        OperatorSite site, SemanticType leftType, SemanticType rightType, SemanticType? operandSlot, bool isChainLink)
    {
        var (effectiveLeftType, effectiveRightType) = EffectiveOperandTypes(
            site.Operator, site.Left, leftType, site.Right, operandSlot ?? rightType, forgetUnneededCasts: !isChainLink);

        var resultType = _typeInference.InferBinaryOpType(site.Operator, effectiveLeftType, effectiveRightType);
        if (resultType != null)
            return (resultType, effectiveLeftType, effectiveRightType);

        // When comparing against the `None` literal with ==/!=, point the user at the
        // supported spelling: Sharpy rejects `x == None` (SPY0222) but accepts `x is None`
        // (#1079). Both operand orders (`x == None` and `None == x`) get the hint. The
        // suggested operator rides the diagnostic data payload for a future LSP quick-fix.
        IReadOnlyDictionary<string, string>? data = null;
        string? messageSuffix = null;
        if (site.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual
            && (site.Left is NoneLiteral || site.Right is NoneLiteral))
        {
            var suggestedOperator = site.Operator == BinaryOperator.Equal ? "is None" : "is not None";
            messageSuffix = $". Did you mean '{suggestedOperator}'?";
            data = new Dictionary<string, string> { ["suggestedOperator"] = suggestedOperator };
        }

        ReportUnsupportedOperator(GetOperatorSymbol(site.Operator), site.Left, site.Right,
            site.Line, site.Column, site.Span, leftType, rightType, data, messageSuffix);
        return (null, effectiveLeftType, effectiveRightType);
    }

    /// <summary>
    /// The slot a membership needle is checked under, pushed as its expectation by the binary site and
    /// by every chain link (#1750, #1777), with the facts the container carries recorded on its node:
    /// a tuple's array bridge (#1771, the iterable ring's one tuple rule), and — P93 D1 — a user class
    /// the iteration ring enumerates (<see cref="IterationMembershipElement"/>) a <c>Direct</c>
    /// projection of its element, the fact the validator (SPY0320) and the emitter's LINQ
    /// <c>Enumerable.Contains&lt;E&gt;</c> read. <paramref name="containerView"/> is the
    /// protocol-receiver view of the container. Null when the container names no single slot.
    /// </summary>
    private SemanticType? MembershipNeedleSlot(Expression container, SemanticType containerView)
    {
        // Tuples need the array bridge for Contains; other containers (str, list, set,
        // dict) already have their own Contains semantics (#1771).
        if (containerView is TupleType)
        {
            var projection = ClassifyIterableSource(
                container, containerView, null, StorePosition.CollectionElement,
                MembershipContainerSiteNoun);
            if (projection != null)
            {
                _semanticInfo.SetIterableProjection(container, projection);
                return projection.ElementType;
            }
        }
        else if (IterationMembershipElement(containerView) is { } iterationElement)
        {
            _semanticInfo.SetIterableProjection(container,
                new IterableArgumentProjection(IterableProjectionKind.Direct, iterationElement));
        }

        return ResolveMembershipElementType(containerView);
    }

    /// <summary>
    /// Checks a membership needle under <paramref name="elementType"/> as its expectation (the
    /// argument position of <c>y.Contains(x)</c>), or plainly when the container names no slot.
    /// </summary>
    private SemanticType CheckMembershipNeedle(Expression needle, SemanticType? elementType)
    {
        if (elementType == null)
            return OperandView(CheckExpression(needle));

        using (EnterStore(StorePosition.ArgumentPositional, elementType, needle))
        {
            return OperandView(CheckExpression(needle));
        }
    }

    /// <summary>
    /// P93 D1 (R-NZ, R-OA): the element a Sharpy-declared container answers <c>in</c> with when it has
    /// no <c>__contains__</c> but the iteration ring enumerates it — or null. The host is
    /// <see cref="TryGetGenericHost"/>'s (a plain class, or a constructed generic at its substitution)
    /// whose declaration has no CLR type (a Sharpy source class), and no <c>__contains__</c> anywhere up
    /// its chain (<see cref="ProtocolMembership.HasDunderInChain"/>): a class that declares one answers
    /// by it, as Python's own protocol precedence does.
    /// <para>The element is the one of the <c>IEnumerable[T]</c> <see cref="SynthesisAnalyzer"/>
    /// synthesizes for the class — the interface LINQ's <c>Enumerable.Contains&lt;E&gt;</c> enumerates —
    /// asked of the nearest declaration up the chain that declares <c>__iter__</c>, with the synthesis
    /// rule: a declaration with both <c>__iter__</c> and <c>__next__</c> answers with <c>__next__</c>'s
    /// return type; one with <c>__iter__</c> alone answers with the unpeeled return of a generator or
    /// producer (<see cref="IterableElementDecider.IsRecognizedProducerType"/>) <c>__iter__</c>. No
    /// other <c>__iter__</c> answers (<c>-&gt; list[int]</c> synthesizes nothing, #1915) and none above
    /// it is asked: an override that does not iterate hides the base's that does. And
    /// <c>__getitem__</c> + <c>__len__</c> never does (R-OA): Python's legacy sequence protocol is not
    /// membership in Sharpy. Two declarations the rule would name do not answer, because the emitted
    /// class does not implement the interface the answer promises: a nested class (its base list lacks
    /// the synthesized row, #2702) and a <c>__next__</c> pair declared after the use (its
    /// <c>__next__</c> is unresolved there, #2249). An answer is typed at the receiver by
    /// <see cref="SubstituteDunderForHost"/>: through the host view for the host's own declaration,
    /// through the base clauses' written arguments for an inherited one (<c>class IntBox(Box[int])</c>,
    /// <c>class Mid[U](Box[U])</c> as <c>Mid[int]</c>).</para>
    /// </summary>
    internal SemanticType? IterationMembershipElement(SemanticType containerType)
    {
        if (containerType is NullableType nullable)
            containerType = nullable.UnderlyingType;

        if (TryGetGenericHost(containerType) is not { } host
            || host.Definition.ClrType != null
            || ProtocolMembership.HasDunderInChain(host.Definition, DunderNames.Contains))
        {
            return null;
        }

        for (var current = host.Definition; current is { ClrType: null }; current = current.BaseType)
        {
            var iter = current.Methods.FirstOrDefault(m => m.Name == DunderNames.Iter);
            if (iter != null)
            {
                // TODO(#2702): a nested class is emitted without its synthesized `IEnumerable<T>` base
                // — the interface the LINQ call enumerates — so an answer for an `__iter__` declared
                // on one reached Roslyn (CS1503 behind SPY0908). No answer until its base list has it.
                if (current.DeclaringType != null)
                    return null;

                // SynthesisAnalyzer: `__next__` + `__iter__` → IEnumerable[`__next__`'s annotation, or
                // object]; `__iter__` alone → IEnumerable[the unpeeled generator / producer return].
                var next = current.Methods.FirstOrDefault(m => m.Name == DunderNames.Next);
                if (next != null)
                {
                    // An unresolved `__next__` (a class declared after this use, #2249) is no answer:
                    // read as `object` it named an interface the class does not implement (CS1503).
                    if (next.ReturnType is UnknownType)
                        return null;

                    return SubstituteDunderForHost(host, containerType, current,
                        next.ReturnType is VoidType ? SemanticType.Object : next.ReturnType);
                }

                if (iter.IsGenerator || IterableElementDecider.IsRecognizedProducerType(iter.ReturnType))
                {
                    var element = IterableElementDecider.UnpeelProducerType(iter.ReturnType, iter.IsGenerator);
                    return element is UnknownType ? null : SubstituteDunderForHost(host, containerType, current, element);
                }

                // The nearest `__iter__` decides, as it does at run time: one that synthesizes nothing
                // (`-> list[int]`) hides an ancestor's, and the ancestor's elements are not this class's.
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// A dunder signature type declared on <paramref name="owner"/> — the host's own declaration or an
    /// ancestor up its base chain — typed at the receiver <paramref name="containerType"/>. The host's
    /// own declaration is closed by the host view (<see cref="TryGetGenericHost"/>); an inherited one
    /// by <see cref="SubstituteMemberForReceiver"/>, whose walk
    /// (<see cref="GenericInstantiationWalker.EnumerateBaseChain"/>) composes the receiver's own type
    /// arguments into each base clause's WRITTEN arguments — the one reader of
    /// <see cref="BaseTypeReference"/>. A source-declared base reference carries its arguments as
    /// annotations only (<c>ResolvedTypeArguments</c> is filled for CLR bases), so reading
    /// <c>ResolvedTypeArguments</c> here left <c>class IntBox(Box[int])</c>'s element at <c>Box</c>'s
    /// open <c>T</c> and refused every needle (SPY0222).
    /// </summary>
    private SemanticType SubstituteDunderForHost(
        GenericHostView host, SemanticType containerType, TypeSymbol owner, SemanticType type)
    {
        if (ReferenceEquals(owner, host.Definition))
            return host.Substitute(type);

        var receiverArguments = containerType is GenericType generic
            ? generic.TypeArguments
            : (IReadOnlyList<SemanticType>)Array.Empty<SemanticType>();
        return SubstituteMemberForReceiver(host.Definition, receiverArguments, owner, type);
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
        // The dunder lives on the RIGHT operand's type (#1719): the emitter swaps the operands and
        // mirrors an ordering token, so the equality strategy is moot for this link.
        if (_typeInference.IsReflectedUserComparison(op, leftType, rightType))
            return new ComparisonLinkLowering(OperatorLoweringKind.ReflectedOperands, null);

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

    /// <summary>
    /// A CONSTANT chain middle needle (owner ruling 2026-11-15, plan-67a01f R3, "By value, cast"): a
    /// literal, or unary and binary operators over literals (<c>-1</c>, <c>(1 + 1)</c>), parentheses
    /// aside — the shape the binary <c>x in c</c> admits by value. Such a middle is admitted at its slot by
    /// its value where it fits, as the twin <c>1 in xs</c> is; every other middle (a tuple or list literal
    /// included) by its type alone (R2). Deliberately partial: every other form answers <c>false</c> and
    /// is admitted by its type alone, as a named needle is.
    /// </summary>
    private static bool IsConstantNeedle(Expression expr) => expr switch
    {
        IntegerLiteral or FloatLiteral or StringLiteral or BooleanLiteral or NoneLiteral => true,
        Parenthesized parenthesized => IsConstantNeedle(parenthesized.Expression),
        UnaryOp unary => IsConstantNeedle(unary.Operand),
        BinaryOp binary => IsConstantNeedle(binary.Left) && IsConstantNeedle(binary.Right),
        _ => false,
    };

    /// <summary>
    /// The needle-vs-slot check for <c>in</c>/<c>not in</c> (#1750, R-U), shared by the binary site
    /// and every chain link, and the ONE place a membership lowering kind is decided (#2635). A
    /// container without a resolvable slot is oblivious — the container-presence check (SPY0320) in
    /// <see cref="Validation.ProtocolValidator"/> handles that stage.
    ///
    /// <para>Decided in this order, and the order is load-bearing:</para>
    /// <list type="number">
    /// <item>A SUBSEQUENCE container (<see cref="IsSubsequenceContainer"/>: <c>str</c>,
    /// <c>bytes</c>) with a <c>T | None</c> needle whose <c>T</c> fits a slot records
    /// <see cref="OperatorLoweringKind.MembershipNoneRaises"/>. First, because <c>str | None</c>
    /// is assignable to a <c>str</c> slot and the next step would return early.</item>
    /// <item>The plain path: the needle is assignable to a slot as it stands
    /// (<see cref="OperatorLoweringKind.Native"/>). Before the lift, so <c>int | None</c> into
    /// <c>list[int | None]</c> stays an equality scan that can FIND <c>None</c>.</item>
    /// <item>The lift: a <c>T | None</c> needle whose <c>T</c> fits a slot is accepted. A
    /// value-type nullable records <see cref="OperatorLoweringKind.MembershipNoneIsFalse"/> (a CLR
    /// <c>Contains(T)</c> takes no <c>T?</c>); a reference needle records nothing — the receiver's
    /// own <c>Contains(null)</c> answers.</item>
    /// </list>
    /// <para>The literal <c>None</c> is never lifted: it is refused (SPY0222) unless a slot is
    /// itself optional.</para>
    ///
    /// <para>"Fits a slot" is ONE question at all three steps: the store seam's
    /// (<see cref="IsArgumentAssignable"/>) for every container, and for a CLR-backed one the CLR
    /// call seam's as well (<see cref="ClrContainsTakesNeedle"/>), so <c>x in y</c> takes the
    /// needles <c>y.Contains(x)</c> takes (#2647).</para>
    /// <para>P93 D2 (#2645): an accepted needle's BINDING type is recorded on the needle node
    /// (<see cref="SemanticInfo.SetMembershipNeedleBinding"/>) — the needle's own type when it is a
    /// <c>T | None</c>, else the first slot the store seam takes it into — so the emitter can evaluate
    /// it before its container. No record for a container with no slot, a needle only the CLR call
    /// seam takes, or a refused needle; every check records or forgets, so a re-checked lambda body
    /// keeps no stale binding.</para>
    /// </summary>
    /// <param name="admitByTypeAlone">A chain's non-constant middle needle (R2, R3): admitted at a slot
    /// by its type, never by the store seam's value shapes.</param>
    /// <returns>The lowering kind of this membership test; <see cref="OperatorLoweringKind.Native"/>
    /// when nothing special applies, the container is oblivious, or the needle was refused.</returns>
    private OperatorLoweringKind ClassifyMembership(
        OperatorSite site, SemanticType needleType, SemanticType containerType, bool admitByTypeAlone = false)
    {
        var needle = site.Left;
        // The node the store seam reads a value SHAPE from (an in-range constant, a conditional of
        // admitted arms). None for a chain's non-constant middle needle (R2): it is read as a named
        // needle is (`t == (1, 2) in ts` by its tuple type). A constant middle keeps its node (R3, "By
        // value, cast"): `h == -1 in cs` over `cs: list[int8]` admits -1 by value, as `-1 in cs` does,
        // and the emitter casts the captured value to the binding at the `in` read.
        var shapeNode = admitByTypeAlone ? null : needle;
        var slots = ResolveMembershipSlots(containerType, out var clrFormals);
        if (slots.Count == 0)
        {
            // No slot (#2249): no binding record, and today's emission.
            _semanticInfo.SetMembershipNeedleBinding(needle, null);
            return OperatorLoweringKind.Native;
        }

        // The first slot the store seam takes a needle into — the type the needle is bound as
        // (P93 D2). Null when only the CLR call seam takes it (#2647): C# alone types that argument.
        SemanticType? FirstSlotTaking(SemanticType type, Expression? node)
            => slots.FirstOrDefault(slot => IsArgumentAssignable(type, slot, node));

        // The lift asks a TYPE question of the needle's underlying type, so no expression is
        // passed: the constant and conditional shapes of the store seam belong to the plain path.
        bool FitsASlot(SemanticType type, Expression? node)
            => FirstSlotTaking(type, node) != null
               || ClrContainsTakesNeedle(type, node, clrFormals);

        var lifted = needleType as NullableType;
        bool LiftFitsASlot()
            => lifted != null && FitsASlot(lifted.UnderlyingType, null);

        // P93 D2: a `T | None` needle is bound as its own type, so the emitter's cast never unwraps it
        // (#2674's value-type needles keep their own form); any other accepted needle as the first slot
        // the store seam takes it into.
        OperatorLoweringKind Accept(OperatorLoweringKind kind)
        {
            _semanticInfo.SetMembershipNeedleBinding(needle, lifted ?? FirstSlotTaking(needleType, shapeNode));
            return kind;
        }

        if (lifted != null && IsSubsequenceContainer(containerType) && LiftFitsASlot())
            return Accept(OperatorLoweringKind.MembershipNoneRaises);

        if (FitsASlot(needleType, shapeNode))
            return Accept(OperatorLoweringKind.Native);

        if (LiftFitsASlot())
        {
            return Accept(lifted!.IsValueType
                ? OperatorLoweringKind.MembershipNoneIsFalse
                : OperatorLoweringKind.Native);
        }

        _semanticInfo.SetMembershipNeedleBinding(needle, null);

        var message = $"Type '{needleType.GetDisplayName()}' does not support operator " +
            $"'{GetOperatorSymbol(site.Operator)}' with operand of type '{containerType.GetDisplayName()}'";

        // The steer names ONE slot: the first whose cast cures the needle, else the first.
        var needleSpelling = SpellOperand(needle, "x");
        var steerSlot = slots.FirstOrDefault(
            slot => MembershipNeedleSteer(needleType, slot, needleSpelling) != null) ?? slots[0];
        var steer = MembershipNeedleSteer(needleType, steerSlot, needleSpelling);
        steer ??= DescribeOptionalArgument(needleType, steerSlot, "needle");

        if (steer.Length > 0)
            message += steer;

        // The NEEDLE is the refused value and the container's element type is its slot, so
        // `(n or "z") in xs` names the #1819 rule like every other refused-value position.
        ReportValueTypeMismatch(message, needle, steerSlot,
            needle.LineStart, needle.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
            span: site.Span);
        return OperatorLoweringKind.Native;
    }

    /// <summary>
    /// The CLR half of "the needle fits a slot": whether a <c>Contains</c> overload of a CLR-backed
    /// container takes a needle the store seam refused.
    ///
    /// <para>A CLR-backed container (<paramref name="clrFormals"/> non-empty: the formals of its
    /// <c>Contains</c> overloads) is asked what a written <c>y.Contains(x)</c> is asked —
    /// <see cref="ClrFormalVerdict"/>, THE acceptance answer of the CLR call seam — so a needle C#
    /// converts into the formal is taken here as it is there: <c>Point in RectangleF</c> binds
    /// <c>Contains(PointF)</c> through <c>Point</c>'s <c>op_Implicit</c> (#2647). No conversion
    /// rule lives here.</para>
    ///
    /// <para>That verdict is "accepted, or nothing here can say it is not", and membership wants
    /// only the first half: a needle refused here is SPY0222, one waved through reaches Roslyn. So
    /// it is asked only of a needle that HAS a CLR type of its own, where each of its arms is a
    /// fact about that type (an inbound <c>op_Implicit</c> from it, CLR assignability from it).
    /// Without one — a user class, a string enum — the verdict's <c>op_Implicit</c> arm reads "the
    /// formal converts from SOMETHING" (<c>PointF</c> does, from <c>Vector2</c>) and would take
    /// every such needle; those stay with the store seam. So does the literal <c>None</c>, which
    /// has no CLR type and would be C#'s null literal to that verdict, applicable to every
    /// reference formal (ruling A). The formal carries no
    /// <see cref="System.Reflection.ParameterInfo"/>, so its declared nullability is not applied.</para>
    /// </summary>
    private bool ClrContainsTakesNeedle(
        SemanticType needleType, Expression? needle, IReadOnlyList<Type> clrFormals)
    {
        if (clrFormals.Count == 0 || TryGetClrType(needleType) == null)
            return false;

        return clrFormals.Any(clrFormal => ClrFormalVerdict(
            new ClrFormal(Parameter: null, clrFormal, IsParamsElement: false), needleType, needle).Accepted);
    }

    /// <summary>
    /// The slot SET a membership needle is checked against: the one element type
    /// <see cref="ResolveMembershipElementType"/> resolves, or — for a CLR-backed non-generic
    /// container no arm of it decides (#2647: <c>bytes</c>, the IP networks, argparse
    /// <c>Namespace</c>, <c>EmailMessage</c>) — the parameter type of EVERY public single-argument
    /// <c>Contains</c> overload of its CLR type, so <c>bytes</c> takes an <c>int</c> and a
    /// <c>bytes</c> needle. Empty when the container is oblivious.
    ///
    /// <para>A sibling of <see cref="ResolveMembershipElementType"/>, not an arm of it: that method
    /// stays silent for these containers, so the site pushes no single slot onto the needle's store
    /// (a conditional <c>bytes</c> needle would be refused against the <c>int</c> slot).</para>
    ///
    /// <para>Each CLR parameter type maps through the checker's <see cref="Discovery.ClrTypeBridge"/>
    /// (<c>MapClrParameterTypeToSemanticType</c>), the same instance that types every reflected
    /// member signature. For a registered Sharpy type (<c>Sharpy.Bytes</c>, <c>IPv4Address</c>) it
    /// yields a <see cref="UserDefinedType"/> carrying that CLR type, which the registered type is
    /// assignable to by CLR identity (<see cref="UserDefinedType.IsAssignableTo"/>, #890).
    /// <see cref="TypeResolver.ClrTypeToSemanticType"/> knows primitives only and would drop them.
    /// The declared nullability of the parameter is deliberately not applied: the literal
    /// <c>None</c> is refused here whatever the receiver does with a null.</para>
    /// </summary>
    /// <param name="clrFormals">The CLR parameter types the slots of a CLR-backed container were
    /// mapped from, in slot order — what <see cref="ClrContainsTakesNeedle"/> puts to the CLR call
    /// seam. Empty for every other container, and whenever the slot set is.</param>
    private IReadOnlyList<SemanticType> ResolveMembershipSlots(
        SemanticType containerType, out IReadOnlyList<Type> clrFormals)
    {
        clrFormals = Array.Empty<Type>();

        if (containerType is NullableType nullable)
            return ResolveMembershipSlots(nullable.UnderlyingType, out clrFormals);

        if (ResolveMembershipElementType(containerType) is { } elementType)
            return new[] { elementType };

        if (containerType is UserDefinedType { Symbol.ClrType: { } clrType })
        {
            var containsFormals = Discovery.ClrTypeHelper.GetContainsParameterTypes(clrType);
            var clrSlots = containsFormals
                .Select(_bclGenericMethodBridge.MapClrParameterTypeToSemanticType)
                .ToList();

            // A slot the bridge cannot name makes the whole set unknowable: stay oblivious rather
            // than refuse a needle that overload would have taken.
            if (clrSlots.Any(slot => slot is UnknownType or UnmappedClrType))
                return Array.Empty<SemanticType>();

            clrFormals = containsFormals;
            return clrSlots;
        }

        return Array.Empty<SemanticType>();
    }

    /// <summary>
    /// Whether <c>in</c> on <paramref name="containerType"/> is a SUBSEQUENCE test — its CLR type
    /// has a public <c>Contains</c> taking the type itself (<c>string.Contains(string)</c>,
    /// <c>Bytes.Contains(Bytes)</c>). Read by reflection through
    /// <see cref="Discovery.ClrTypeHelper.HasContainsOfOwnType"/>, never from the type's name.
    /// </summary>
    private static bool IsSubsequenceContainer(SemanticType containerType)
    {
        var clrType = containerType switch
        {
            NullableType nullable => (nullable.UnderlyingType as BuiltinType)?.ClrType
                ?? (nullable.UnderlyingType as UserDefinedType)?.Symbol?.ClrType,
            BuiltinType builtin => builtin.ClrType,
            UserDefinedType userDefined => userDefined.Symbol?.ClrType,
            _ => null
        };
        return clrType != null && Discovery.ClrTypeHelper.HasContainsOfOwnType(clrType);
    }

    /// <summary>
    /// Resolves the element type a membership needle is checked against. Builtin containers
    /// go through <see cref="TypeInferenceService.InferIterableElementType"/> (dict yields
    /// keys, str yields str); user types with <c>__contains__</c> yield its parameter type;
    /// CLR types with a typed <c>Contains</c> method yield that parameter type.
    /// Returns null when no element type can be determined.
    /// </summary>
    private SemanticType? ResolveMembershipElementType(SemanticType containerType)
    {
        // NullableType (T | None) exposes the underlying container's element type
        if (containerType is NullableType nullable)
            return ResolveMembershipElementType(nullable.UnderlyingType);

        // str in str is always str (substring test)
        if (containerType == SemanticType.Str)
            return SemanticType.Str;

        // Tuples: the element type is the best-common-type of all elements (#1771).
        if (containerType is TupleType tuple && tuple.ElementTypes.Count > 0)
            return _typeInference.InferIterableElementType(tuple);

        // A user host's own __contains__ (#1859) — checked BEFORE the iterate-based arm below so a
        // receiver declaring BOTH answers by __contains__ first, matching Python's own `in` protocol
        // precedence. TryGetGenericHost covers a constructed generic host (GenericType) and a plain
        // class alike (UserDefinedType, at the identity substitution) — ONE arm, not two copies.
        // Substituted through the view: b05b's `__contains__(x: str)` names `str`, not the receiver's
        // own first type argument.
        var host = TryGetGenericHost(containerType);
        if (host is { } hostView)
        {
            var typeSymbol = hostView.Definition;

            // Check ProtocolMethods first (where __contains__ is registered)
            if (typeSymbol.ProtocolMethods.TryGetValue(DunderNames.Contains, out var containsMethods))
            {
                var best = containsMethods.FirstOrDefault(m =>
                    m.Parameters.Count(p => p.Name != Shared.PythonNames.Self) >= 1);
                if (best != null)
                {
                    var itemParam = best.Parameters.FirstOrDefault(
                        p => p.Name != Shared.PythonNames.Self);
                    if (itemParam != null)
                        return hostView.Substitute(itemParam.Type);
                }
            }

            // Check regular Methods, walking the BASE CHAIN through the protocol authority so an
            // inherited __contains__ names the needle slot exactly as a declared one does (#1808).
            // The owner's type parameters are closed at THIS receiver: an inherited
            // `__contains__(self, x: T)` on `class IntBag(Bag[int])` takes an `int` (P93 D1).
            if (ProtocolMembership.FindDunderDeclarationInChain(typeSymbol, DunderNames.Contains)
                    is (var containsMethod, var containsOwner)
                && containsMethod.Parameters.FirstOrDefault(p => p.Name != Shared.PythonNames.Self)
                    is { } containsItem)
            {
                return SubstituteDunderForHost(hostView, containerType, containsOwner, containsItem.Type);
            }

            // P93 D1 (R-NZ, R-OA): a Sharpy-declared host with no __contains__ answers `in` with the
            // element of the IEnumerable[T] the iteration ring synthesizes for it, and with nothing
            // else — never with its __getitem__'s return, which the generic-container arm below would
            // read (the legacy sequence pair is not membership in Sharpy: a generic `SeqBag[T]` took a
            // needle slot from it and refused a mismatched needle with SPY0222 instead of SPY0320).
            if (typeSymbol.ClrType == null)
                return IterationMembershipElement(containerType);
        }

        // Generic containers and views (list[T], set[T], dict[K,V], .keys()/.values()/.items(), and
        // — after #1859 — a user generic host's own __iter__/__getitem__ when it has no __contains__
        // above): ONE authority for "what does iterating/indexing this yield", shared with `for`/
        // comprehension/etc., so `.values()`'s needle is the VALUE type and never the receiver's own
        // first type argument (d09/d09b).
        if (containerType is GenericType && _typeInference.InferIterableElementType(containerType) is { } elementType)
            return elementType;

        // BuiltinType with a CLR Contains(T) method: read the parameter type (#1778).
        if (containerType is BuiltinType builtin && builtin.ClrType != null)
        {
            var clrContainsMethod = builtin.ClrType
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Contains" && m.GetParameters().Length == 1);
            if (clrContainsMethod != null)
            {
                var paramType = TypeResolver.ClrTypeToSemanticType(clrContainsMethod.GetParameters()[0].ParameterType);
                if (paramType != null)
                    return paramType;
            }
        }

        return null;
    }

    /// <summary>
    /// The cast steer for a membership needle whose type C# cannot bind to the element type.
    /// Shaped for the needle position: only the needle is cast, not the container.
    /// </summary>
    private static string? MembershipNeedleSteer(
        SemanticType needleType, SemanticType elementType, string needleSpelling)
    {
        if (!PrimitiveCatalog.IsNumeric(needleType) || !PrimitiveCatalog.IsNumeric(elementType))
            return null;
        if (PrimitiveCatalog.GetPromotedType(needleType, elementType) != null)
            return null;

        return $" — cast the needle: '{elementType.GetDisplayName()}({needleSpelling})'";
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        // The ternary's condition is a truthiness position like if/while/assert (#1603).
        var (ternaryTruthTestable, testType) = CheckTruthinessTest(cond.Test);
        if (!ternaryTruthTestable)
        {
            ReportNotTruthTestable(cond.Test, testType, "Conditional expression condition must be boolean");
        }

        // R-K: a conditional in a truthiness position distributes the test per branch —
        // each branch's own truthiness is classified and recorded, and the conditional gets
        // TruthinessLowering.Distributed so the emitter wraps each branch individually.
        bool isTruthinessPosition = _storeContext is { Position: StorePosition.TruthinessTest } tsc
            && tsc.IsDirectOperand(cond);

        // R-W/R-AE: the conditional takes the enclosing store's slot when it IS the store's
        // value node. Each branch is checked under that slot inside its narrowing scope so the
        // seam refuses at the BRANCH's span, not the conditional's.
        SemanticType? condSlot = null;
        StorePosition condPosition = StorePosition.Declaration;
        string? condCallee = null;
        int? condOrdinal = null;
        string? condKeyword = null;
        if (!isTruthinessPosition && _storeContext is { Slot: not null and not UnknownType } sc && sc.IsDirectOperand(cond))
        {
            condSlot = sc.Slot;
            condPosition = sc.Position;
            condCallee = sc.CalleeDisplay;
            condOrdinal = sc.ArgumentOrdinal;
            condKeyword = sc.KeywordName;
        }

        // Expression-level narrowing (#1080): the true arm is evaluated only when the condition holds,
        // so it sees the condition's positive narrowings; the false arm sees the negative narrowings.
        // Reads inside each arm record their accessor lowering via the narrowing context, exactly as
        // `and`-RHS does — codegen needs no special handling. The narrowings do not leak past the arm.
        //
        // Each branch is checked under its OWN store push, so the branch — not the conditional — is
        // the store's value node. Two things follow that the ambient `_expectedType` leak could not
        // give: a fresh walrus directly under a branch is a DIRECT operand of a slot and takes it
        // (R-AE: `s: str | None = (q := None) if c else "a"`), and a nested conditional under a
        // truthiness test is itself in a truthiness position, so distribution RECURSES rather than
        // demanding a type of the inner conditional (R-K; `if ("a" if g else 0) if flag else 1:`).
        var thenEntries = ExtractNarrowedTypes(cond.Test, true);
        var elseEntries = ExtractNarrowedTypes(cond.Test, false);

        IDisposable? BranchStore(Expression branch)
            => isTruthinessPosition
                ? EnterStore(StorePosition.TruthinessTest, SemanticType.Unknown, branch)
                : condSlot != null
                    ? EnterStore(condPosition, condSlot, branch, condCallee, condOrdinal, condKeyword)
                    : null;

        SemanticType thenType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(thenEntries);
            using (BranchStore(cond.ThenValue))
                thenType = CheckExpression(cond.ThenValue);
        }

        SemanticType elseType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(elseEntries);
            using (BranchStore(cond.ElseValue))
                elseType = CheckExpression(cond.ElseValue);
        }

        // R-K: distribute truthiness per branch. Each branch gets its own TruthinessLowering
        // fact; the conditional itself records Distributed so the emitter wraps each branch.
        if (isTruthinessPosition)
        {
            RecordBranchTruthiness(cond.ThenValue, thenType);
            RecordBranchTruthiness(cond.ElseValue, elseType);

            _semanticInfo.SetTruthinessLowering(cond, TruthinessLowering.Distributed);
            return SemanticType.Bool;
        }

        // ARM 1 (R-W): the slot decides, and each branch is ADMITTED OR REFUSED AT ITS OWN SPAN.
        // Before this arm the slot-directed route did a pairwise `IsAssignable` and returned a
        // branch type, so a mistyped branch reached Roslyn untouched — `a: Animal = Dog() if c
        // else "x"` was CS0029 behind SPY0908 and `b: bool = y if flag else n` reported at the
        // declaration rather than at `y` (#1677, #1743).
        //
        // When EVERY branch is admitted the conditional keeps its natural branch type so the
        // enclosing store's own arm 6 (`ClassifyStore` → `ApplyConditionalBranchVerdicts`) still
        // records the per-branch cast the integer-constant arm needs — `x: int8 = 7 if c else 8`
        // is CS0266 without it (#1698). When a branch is refused the conditional reports there and
        // reads as the SLOT, so the enclosing store accepts slot-into-slot and the user sees
        // exactly one diagnostic, at the offending branch.
        if (condSlot != null)
        {
            return AdmitConditionalBranchesIntoSlot(
                cond, thenType, elseType, condSlot, condPosition);
        }

        // R-W: slot-less → BestCommonType arm 2 (one-accepts-all) or arm 3 (refuse by name).
        // The DP mark and data-level comparison are retired.
        return BestCommonType(
            new[] { ((Expression?)cond.ThenValue, thenType), ((Expression?)cond.ElseValue, elseType) },
            null, condPosition, cond, "conditional expression",
            new BestCommonTypeOptions(
                AnnotateSteer: "'x: T = ...' on the target",
                NoneAnnotateSteer: "'x: T | None = ...' on the target"));
    }

    /// <summary>
    /// ARM 1 of the R-W rule for a conditional expression — #1743's NAMED slot-directed check.
    /// Each branch is admitted or REFUSED AT ITS OWN SPAN by the store seam.
    ///
    /// <para>Before this arm the slot-directed route did a pairwise <c>IsAssignable</c> and
    /// returned a branch type, so a mistyped branch reached Roslyn untouched:
    /// <c>a: Animal = Dog() if c else "x"</c> was CS0029 behind SPY0908, and
    /// <c>b: bool = y if flag else n</c> reported at the declaration rather than at <c>y</c>.</para>
    ///
    /// <para>When EVERY branch is admitted the conditional keeps its natural branch type, so the
    /// ENCLOSING store's own arm 6 (<c>ClassifyStore</c> → <c>ApplyConditionalBranchVerdicts</c>)
    /// still records the per-branch cast the integer-constant arm needs — <c>x: int8 = 7 if c else
    /// 8</c> is CS0266 without it (#1698). When a branch is refused the conditional reads as the
    /// SLOT, so the enclosing store accepts slot-into-slot and the user sees exactly ONE
    /// diagnostic, at the offending branch.</para>
    /// </summary>
    private SemanticType AdmitConditionalBranchesIntoSlot(
        ConditionalExpression cond,
        SemanticType thenType,
        SemanticType elseType,
        SemanticType slot,
        StorePosition position)
    {
        var thenVerdict = ClassifyStore(position, cond.ThenValue, thenType, slot);
        var elseVerdict = ClassifyStore(position, cond.ElseValue, elseType, slot);

        if (!IsAcceptedVerdict(thenVerdict) || !IsAcceptedVerdict(elseVerdict))
        {
            if (!IsAcceptedVerdict(thenVerdict))
                CheckStore(position, cond.ThenValue, thenType, slot,
                    cond.ThenValue, cond.ThenValue.Span);
            if (!IsAcceptedVerdict(elseVerdict))
                CheckStore(position, cond.ElseValue, elseType, slot,
                    cond.ElseValue, cond.ElseValue.Span);
            return slot;
        }

        if (IsAssignable(thenType, elseType))
            return elseType;
        if (IsAssignable(elseType, thenType))
            return thenType;
        return thenType; // both admitted into the slot; the outer seam records the casts
    }

    /// <summary>
    /// Records one branch's own <see cref="TruthinessLowering"/> for a conditional that distributes
    /// its truthiness test (R-K, #1743).
    ///
    /// <para>A branch that is ITSELF a conditional in this truthiness position has already recorded
    /// <see cref="TruthinessLowering.Distributed"/> for its own branches, and the emitter's
    /// <c>WrapTruthinessIfNeeded</c> recurses on that fact. Classifying it again by its value type
    /// (<c>bool</c> → <c>NativeBool</c>) would OVERWRITE the fact and hand the emitter the raw inner
    /// ternary — `if ("a" if g else 0) if flag else 1:` was CS0173 behind SPY0908.</para>
    /// </summary>
    private void RecordBranchTruthiness(Expression branch, SemanticType branchType)
    {
        // The fact is recorded on the CONDITIONAL, which a parenthesized branch wraps; the
        // emitter looks it up on the branch node it holds, so the mark is propagated outward.
        var inner = Shared.AstHelper.UnwrapParenthesized(branch);
        if (_semanticInfo.GetTruthinessLowering(inner) == TruthinessLowering.Distributed)
        {
            if (!ReferenceEquals(inner, branch))
                _semanticInfo.SetTruthinessLowering(branch, TruthinessLowering.Distributed);
            return;
        }

        var (truthTestable, lowering) = ClassifyTruthiness(branchType);
        if (!truthTestable)
        {
            // Site 11 of 14 (#1861): the ONE call that does not come through CheckTruthinessTest —
            // ClassifyTruthiness is asked directly, per-branch, because R-K's distribution recurses
            // independently of the shared classifier's lowering-recording side effects. The refusal
            // itself still goes through the ONE helper, same as every other site.
            ReportNotTruthTestable(branch, branchType, "Conditional expression branch must be truth-testable");
            return;
        }

        _semanticInfo.SetTruthinessLowering(branch, lowering);
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
            site: TypeTestSite.Cast,
            out var targetRefused);

        // The classifier's arm-3 refusal (SPY0345) is TERMINAL: `o as? list` used to also run the
        // annotation through ResolveTypeAnnotation below and draw a SECOND diagnostic (SPY0224 "expects
        // 1 type arguments but got 0") on the same node. One refusal, one code (#1708) — return Unknown
        // so no further resolution or coercion validation runs.
        if (targetRefused)
            return SemanticType.Unknown;

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
            var baseTargetType = _typeResolver.ResolveTypeAnnotation(baseAnnotation, AnnotationPosition.Value);
            if (baseTargetType is not UnknownType)
            {
                _semanticInfo.SetTypeTestType(coercion.TargetType, baseTargetType);
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
            : _typeResolver.ResolveTypeAnnotation(targetAnnotation, AnnotationPosition.Value);

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
        // A coercion is refused at semantic time iff it is STATICALLY IMPOSSIBLE — no relationship the
        // runtime could satisfy (#1713, R-E). CoercionPossibility.Classify is total; only the Impossible
        // verdict is refused, by name with a steer, uniform across `as?`/`as!` and every consumption.
        // Before this seam existed the impossible pairs reached Roslyn as CS8121/CS0030 and surfaced as
        // SPY0908 (an internal-error net). SPY0228 (the old primitive→str and unrelated-class refusals)
        // is folded into SPY0610 here.
        //
        // Possibility is judged on the type the EMITTER actually casts FROM — not the flow-narrowed read
        // type (R-P5-2). See PossibilitySourceType.
        var possibilitySource = PossibilitySourceType(coercion.Value, sourceType);
        if (CoercionPossibility.Classify(possibilitySource, targetType, SemanticBinding) == CoercionPossibility.Verdict.Impossible)
        {
            AddError(
                BuildImpossibleCoercionMessage(possibilitySource, targetType),
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.ImpossibleCoercion,
                span: coercion.Span);
        }
    }

    /// <summary>
    /// The type the emitter will actually cast FROM at a coercion operand, which is what coercion
    /// possibility must be judged on (R-P5-2, #1712). The emitted C# local keeps its DECLARED type, so a
    /// reassignment-narrowed <c>object</c> variable is emitted as <c>object</c> — <c>o is long</c>,
    /// valid, false at runtime — and refusing it is a false positive. Only an isinstance/type-guard
    /// narrowing makes the emitter cast the read (<see cref="NarrowedReadKind.Cast"/> → <c>(str)o</c>),
    /// and THEN <c>(str)o is long</c> is the CS8121 the refusal must catch. So: use the narrowed type
    /// when a Cast read-lowering is recorded, otherwise the operand's declared binding type; fall back to
    /// the inferred type for non-identifier operands (whose inferred type is already their static type).
    /// </summary>
    private SemanticType PossibilitySourceType(Expression operand, SemanticType inferredType)
    {
        if (_semanticInfo.GetNarrowedReadLowering(operand) is { Kind: NarrowedReadKind.Cast })
            return inferredType;

        if (operand is Identifier id && _semanticInfo.GetIdentifierSymbol(id) is VariableSymbol symbol)
            return DeclaredBindingType(symbol);

        return inferredType;
    }

    /// <summary>
    /// Builds the SPY0610 steer for a statically-impossible coercion: it always names the pair, and adds
    /// a direction-specific hint — <c>str(...)</c>/<c>int(...)</c>/<c>try int(...)</c> for primitive pairs,
    /// "matching type arguments" for the same-collection-different-arguments pair, and the unrelated-types
    /// wording otherwise.
    /// </summary>
    private string BuildImpossibleCoercionMessage(SemanticType source, SemanticType target)
    {
        var s = source.GetDisplayName();
        var t = target.GetDisplayName();

        // A non-string primitive/bool going to str — the classic mistake; str(...) formats a value.
        if (IsStringType(target) && !IsStringType(source) && PrimitiveCatalog.GetPrimitiveInfo(source) != null)
        {
            return $"Cannot cast '{s}' to 'str' — there is no such coercion. Use str(x) to format a value as text.";
        }

        // A string going to a number — parse it, don't cast it.
        if (IsStringType(source) && PrimitiveCatalog.IsNumeric(target))
        {
            var conv = PrimitiveCatalog.IsInteger(target) ? "int" : "float";
            return $"Cannot cast 'str' to '{t}' — there is no such coercion. Use {conv}(s) to parse text, "
                + $"or 'try {conv}(s)' to handle a parse failure.";
        }

        // Two instantiations of the same collection with different type arguments (list[int] vs list[str]).
        if (source is GenericType sg && target is GenericType tg && sg.Name == tg.Name)
        {
            return $"Cannot cast '{s}' to '{t}' — same collection type with different type arguments. "
                + "Write the closed spelling with matching type arguments.";
        }

        return $"Cannot cast '{s}' to '{t}' — the types are unrelated (no inheritance relationship, "
            + "interface, or conversion).";
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
    /// The int-backed enum an <c>is</c> operand's static type names — through <c>E | None</c>, and
    /// through the symbol-less <see cref="UserDefinedType"/> a cross-module field type carries (the
    /// fallback <c>TypeInferenceService.IsEnumType</c> uses) — or null for every other operand. A
    /// string-backed enum is a reference singleton whose <c>is</c> already answers by reference.
    /// </summary>
    private TypeSymbol? IntBackedEnumOf(SemanticType type)
    {
        if (type is NullableType nullable)
            type = nullable.UnderlyingType;
        if (OperandView(type) is not UserDefinedType udt)
            return null;

        var symbol = udt.Symbol ?? (udt.Name != null ? _symbolTable.Lookup(udt.Name) as TypeSymbol : null);
        return symbol is { TypeKind: TypeKind.Enum, IsStringEnum: false } ? symbol : null;
    }

    /// <summary>
    /// How <c>is</c> / <c>is not</c> is answered when an operand is an int-backed enum (#2669, owner
    /// ruling 2026-11-12), or null when neither is — the ONE classifier, shared by the binary operator
    /// and the type-test spelling (<see cref="CheckTypeCheck"/>). Enum members are singletons in
    /// Python, so identity is equality; in C# an int-backed enum is a value type and
    /// <c>object.ReferenceEquals</c> boxes it, always False.
    /// <list type="bullet">
    /// <item>the same enum on both sides (either possibly <c>E | None</c>) →
    /// <see cref="OperatorLoweringKind.EnumIdentityEquals"/>, the native (lifted) <c>==</c>;</item>
    /// <item>an enum against anything else — <c>object</c>, another enum type, an untyped operand —
    /// → <see cref="OperatorLoweringKind.EnumIdentityObjectEquals"/>, <c>EnumHelpers.Identity</c>,
    /// which is right for every pair and runs no user code (#2680): a boxed member is identical only
    /// to the same member of the same enum, and any other pair compares by reference. So a
    /// same-enum pair this classifier fails to recognise as one (two import clones of one
    /// declaration) still gets the right answer, and a different-enum pair never reaches the native
    /// <c>==</c> C# refuses (CS0019).</item>
    /// </list>
    /// A <c>None</c> operand is never classified: the None test owns it (#2224).
    /// </summary>
    private OperatorLoweringKind? ClassifyEnumIdentity(SemanticType leftType, SemanticType rightType)
    {
        if (leftType is VoidType || rightType is VoidType)
            return null;

        var leftEnum = IntBackedEnumOf(leftType);
        var rightEnum = IntBackedEnumOf(rightType);
        if (leftEnum == null && rightEnum == null)
            return null;

        return leftEnum != null && rightEnum != null
            && TypeHierarchyService.IsSameEnumDeclaration(leftEnum, rightEnum)
            ? OperatorLoweringKind.EnumIdentityEquals
            : OperatorLoweringKind.EnumIdentityObjectEquals;
    }

    /// <summary>
    /// The enum whose MEMBER a type-test's dotted name spells (<c>Color.RED</c>,
    /// <c>Outer.Color.RED</c>, <c>mod.Color.RED</c>), or null (#2669). Asked of the type resolver, the
    /// same authority that would resolve the name as a type: the whole name must NOT resolve as a
    /// type, its qualifier must resolve to an enum, and that enum must declare the last segment. Any
    /// other name — a type, an unknown name, a class constant — keeps the SPY0349 refusal.
    /// </summary>
    private TypeSymbol? EnumOfMemberSpelledAsType(TypeAnnotation annotation)
    {
        var parts = annotation.NameParts;
        if (parts.Length < 2 || !annotation.TypeArguments.IsEmpty
            || _typeResolver.ResolveDottedTypeName(annotation.Name, escaped: false) != null)
            return null;

        var qualifier = string.Join(".", parts.Take(parts.Length - 1));
        return _typeResolver.ResolveDottedTypeName(qualifier, escaped: false) is { TypeKind: TypeKind.Enum } enumSymbol
            && enumSymbol.Fields.Any(f => f.Name == parts[^1])
            ? enumSymbol
            : null;
    }

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        var valueType = CheckExpression(typeCheck.Value);

        // `x is Color.RED` (#2669): the parser reads an uppercase name after `is` as a type, but an
        // enum MEMBER is a value, and this is the identity comparison CPython runs. The parser built
        // the expression reading of the dotted name (TypeCheck.IdentityOperand); it is checked — and
        // the node lowered as an identity comparison — exactly when the name is an enum member.
        if (typeCheck.IdentityOperand is { } identityOperand
            && EnumOfMemberSpelledAsType(typeCheck.CheckType) is { } memberEnum)
        {
            var memberType = CheckExpression(identityOperand);
            _semanticInfo.SetOperatorLowering(typeCheck,
                new OperatorLowering(TypeSpelledIdentityKind(memberEnum, valueType, memberType)));
            return SemanticType.Bool;
        }

        ReportIsTypeTestRetired(typeCheck.Value, typeCheck.CheckType,
            typeCheck.LineStart, typeCheck.ColumnStart, typeCheck.Span);
        return SemanticType.Bool;
    }

    /// <summary>
    /// The identity lowering of <c>x is E.M</c> spelled like a type, when <c>E.M</c> is a member of
    /// <paramref name="memberEnum"/> (#2669) — <see cref="CheckTypeCheck"/>'s rule, shared with link 0
    /// of a chain whose head was spelled that way (P93 D4). A string-backed member is a reference
    /// singleton; for an int-backed one the classifier answers from the operand types, and when it
    /// has no answer (the member's own access is left untyped — a nested enum's chain, #2038 — and the
    /// value is not an enum either), the member is still a member of <paramref name="memberEnum"/>,
    /// and <c>EnumHelpers.Identity</c> is right for every pair.
    /// </summary>
    private OperatorLoweringKind TypeSpelledIdentityKind(
        TypeSymbol memberEnum, SemanticType valueType, SemanticType memberType)
        => memberEnum.IsStringEnum
            ? OperatorLoweringKind.EnumMemberReferenceIdentity
            : ClassifyEnumIdentity(OperandView(valueType), OperandView(memberType))
                ?? OperatorLoweringKind.EnumIdentityObjectEquals;

    /// <summary>
    /// #1298 (owner decision 2026-08-08): <c>x is TypeName</c> is retired as a type test. <c>is</c>
    /// means reference identity only; the type-test spelling is isinstance. The parser still produces
    /// the type-test shape (tooling sees it), but the semantic phase refuses it (SPY0349) — the one
    /// refusal of <see cref="CheckTypeCheck"/> and of a chain head spelled the same way (P93 D4). The
    /// emitter arm (GenerateTypeCheck) is kept as defence-in-depth but never reached.
    /// </summary>
    private void ReportIsTypeTestRetired(
        Expression value, TypeAnnotation checkType, int line, int column, Text.TextSpan? span)
    {
        var valueName = value is Identifier id ? id.Name : "value";
        AddError(
            $"'is' compares references, not types. " +
            $"Use 'isinstance({valueName}, {checkType.Name})' to test a value's type.",
            line, column,
            code: DiagnosticCodes.Semantic.IsTypeTestRetired,
            span: span);
    }

    /// <summary>
    /// Link 0 of a comparison chain whose head the parser read as a type test (P93 D4:
    /// <c>c is Color.RED is d</c>, <c>c is Outer.Inner == d</c>) — <see cref="CheckTypeCheck"/>'s
    /// rule, by the same two helpers: an enum member is an identity link
    /// (<see cref="TypeSpelledIdentityKind"/>); any other dotted name is SPY0349 with the same steer
    /// (<see cref="ReportIsTypeTestRetired"/>), and its operand was recorded <see cref="UnknownType"/>
    /// unchecked (<see cref="CheckComparisonChain"/>), so the link after it is skipped.
    /// </summary>
    private LinkVerdict TypeSpelledIdentity(
        TypeAnnotation headCheckType, OperatorSite site, SemanticType valueType, SemanticType memberType)
    {
        if (EnumOfMemberSpelledAsType(headCheckType) is { } memberEnum)
        {
            return new LinkVerdict(SemanticType.Bool, new ComparisonLinkLowering(
                TypeSpelledIdentityKind(memberEnum, valueType, memberType), null, ComparisonLinkShape.Identity));
        }

        ReportIsTypeTestRetired(site.Left, headCheckType, site.Line, site.Column, site.Span);
        return LinkVerdict.Refused(ComparisonLinkShape.Identity);
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
                var resolvedType = _typeResolver.ResolveTypeAnnotation(typeAnnotation, AnnotationPosition.Value);
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
    internal static bool IsExceptionSubtype(SemanticType type, TypeSymbol exceptionSymbol)
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

        // 2. Disallow inside a lambda body (docs/language_specification/question_mark_operator.md:
        // "`?` is disallowed inside a lambda body because the lambda has no declared
        // Result/Optional return type for the early return to target"). Design Decision 8.
        if (_inLambdaBody)
        {
            AddError(
                "'?' operator can only be used inside a function, not inside a lambda body",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Validation.QuestionMarkOutsideFunction,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 3. Disallow in finally blocks
        if (_inFinally)
        {
            AddError(
                "'?' operator cannot be used inside a 'finally' block",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Semantic.QuestionMarkInFinally,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 4. Type-check the operand
        var operandType = CheckExpression(qm.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // 5. Handle Result<T, E>
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
        Expression right, SemanticType rightType, bool forgetUnneededCasts = true)
    {
        if (op is BinaryOperator.LeftShift or BinaryOperator.RightShift)
            return (leftType, rightType);

        var resolver = MakeConstantResolver();
        var leftConverts = ImplicitConversions.IsImplicitIntegerConstantConversion(
            left, leftType, rightType, resolver);
        var rightConverts = ImplicitConversions.IsImplicitIntegerConstantConversion(
            right, rightType, leftType, resolver);

        // The converted constant's cast (#2344) is recorded — or forgotten — on every check, so a
        // lambda body re-checked under other parameter types keeps no stale fact.
        // A comparison-chain link (forgetUnneededCasts false) shares its middle operand with the next
        // link, so it only adds a cast and never forgets the one the other link recorded.
        var leftCast = leftConverts && !rightConverts && ImplicitConversions.RequiresConstantCast(leftType, rightType) ? rightType : null;
        var rightCast = rightConverts && !leftConverts && ImplicitConversions.RequiresConstantCast(rightType, leftType) ? leftType : null;
        if (leftCast != null || forgetUnneededCasts)
            _semanticInfo.SetIntegerConstantCast(left, leftCast);
        if (rightCast != null || forgetUnneededCasts)
            _semanticInfo.SetIntegerConstantCast(right, rightCast);

        if (leftConverts && !rightConverts)
            return (rightType, rightType);
        if (rightConverts && !leftConverts)
            return (leftType, leftType);

        return (leftType, rightType);
    }

    /// <summary>
    /// The §10.2.11 constant-operand rule for the variadic <c>min</c>/<c>max</c> value form — the
    /// sibling of <see cref="EffectiveOperandTypes"/> for the OTHER consumer of
    /// <c>PrimitiveCatalog.GetPromotedType</c> (#1699). <c>min(u, 1)</c> with <c>u: uint64</c> is
    /// legal for the same reason <c>u + 1</c> is: the constant converts to <c>ulong</c> BEFORE the
    /// common type is computed. Promoting the declared types instead answers null and refuses the
    /// call ("Cannot determine common numeric type for 'min' with argument types 'uint64',
    /// 'int32'"), which is a false refusal.
    /// <para>The conversion target is the single type shared by the arguments that are NOT integer
    /// constant expressions; with none, or with several different ones, there is nothing to convert
    /// toward and the argument types are returned unchanged. A constant that does NOT convert
    /// (a negative literal against an unsigned type) also returns them unchanged, so the call is
    /// refused exactly as a signed variable operand would be — the refusal is preserved, not
    /// widened away. <c>min(2, 3.0)</c> is untouched: the predicate answers false for a
    /// floating-point target, so promotion still decides that pair.</para>
    /// <para><b>Wiring (#1699, pending — this helper has no caller yet).</b> The three builtin
    /// dispatch sites live in <c>TypeChecker.Expressions.Access.Calls.cs</c> and must pass the
    /// effective types to <c>BuiltinReturnTypeInference.InferReturnType</c> while keeping the
    /// ORIGINAL <paramref name="argTypes"/> for <c>ReportMinMaxPromotionFailure</c> (the message
    /// names what the user wrote) and for <c>RecordMinMaxTypeArguments</c> (the recorded type
    /// argument must differ from an argument's declared type for the emitted
    /// <c>Builtins.Min&lt;T&gt;</c> to bind — C# cannot infer one <c>T</c> from
    /// <c>(ulong, int)</c>).</para>
    /// </summary>
    private List<SemanticType> EffectiveMinMaxArgumentTypes(
        FunctionCall call, List<SemanticType> argTypes)
    {
        if (argTypes.Count < 2 || call.Arguments.Length != argTypes.Count)
            return argTypes;

        var resolver = MakeConstantResolver();

        SemanticType? target = null;
        List<int>? constantIndices = null;
        for (var i = 0; i < argTypes.Count; i++)
        {
            if (IntegerConstantEvaluator.TryGetConstantInteger(call.Arguments[i], out _, resolver))
            {
                (constantIndices ??= new List<int>()).Add(i);
                continue;
            }

            if (target == null)
                target = argTypes[i];
            else if (!Equals(target, argTypes[i]))
                return argTypes;
        }

        if (target == null || constantIndices == null)
            return argTypes;

        var effective = new List<SemanticType>(argTypes);
        foreach (var i in constantIndices)
        {
            if (!ImplicitConversions.IsImplicitIntegerConstantConversion(
                    call.Arguments[i], argTypes[i], target, resolver))
            {
                return argTypes;
            }

            effective[i] = target;
        }

        return effective;
    }

    /// <summary>
    /// The cure suffix for a pair binary numeric promotion refuses (#1699). SPY0222 names what is
    /// wrong; without this it does not name what to write instead, and the pairs promotion refuses
    /// are exactly the ones whose fix is not obvious — <c>uint64 ⊗ signed</c> has no common type in
    /// C# (§12.4.7, CS0034) and <c>decimal ⊗ float</c> has no conversion at all. The spellings are
    /// the ones <c>arithmetic_operators.md</c> documents, and each was executed: <c>int64(e) + f</c>
    /// and <c>e + uint64(f)</c> print 9, <c>decimal(g) &lt; d</c> and <c>float64(d) &lt; g</c> run.
    /// <para>Operand spellings come from the reported NODE — a <c>BinaryOp</c>'s two operands or an
    /// <c>Assignment</c>'s target and value — so the augmented site gets the same steer without a
    /// second call shape. A non-identifier operand falls back to <c>x</c>/<c>y</c>: the steer is
    /// advice about which conversion to write, not a quick-fix edit.</para>
    /// </summary>
    private static string? PromotionRefusalSteer(Node node, SemanticType left, SemanticType right)
    {
        var (leftNode, rightNode) = OperandNodes(node);
        return PromotionRefusalSteer(leftNode, rightNode, left, right);
    }

    private static string? PromotionRefusalSteer(
        Expression? leftNode, Expression? rightNode, SemanticType left, SemanticType right)
    {
        var leftInfo = PrimitiveCatalog.GetPrimitiveInfo(left);
        var rightInfo = PrimitiveCatalog.GetPrimitiveInfo(right);
        if (leftInfo == null || rightInfo == null)
            return null;
        if (!PrimitiveCatalog.IsNumeric(left) || !PrimitiveCatalog.IsNumeric(right))
            return null;
        if (PrimitiveCatalog.GetPromotedType(left, right) != null)
            return null;

        var (leftSpelling, rightSpelling) = (SpellOperand(leftNode, "x"), SpellOperand(rightNode, "y"));

        if (PrimitiveCatalog.IsDecimal(left) && PrimitiveCatalog.IsFloatingPoint(right))
        {
            return $" — cast one operand: 'decimal({rightSpelling})' "
                + $"or '{right.GetDisplayName()}({leftSpelling})'";
        }

        if (PrimitiveCatalog.IsFloatingPoint(left) && PrimitiveCatalog.IsDecimal(right))
        {
            return $" — cast one operand: 'decimal({leftSpelling})' "
                + $"or '{left.GetDisplayName()}({rightSpelling})'";
        }

        if (left == SemanticType.ULong && rightInfo.IsSigned)
            return $" — cast one operand: 'int64({leftSpelling})' or 'uint64({rightSpelling})'";

        if (right == SemanticType.ULong && leftInfo.IsSigned)
            return $" — cast one operand: 'int64({rightSpelling})' or 'uint64({leftSpelling})'";

        return null;
    }

    private static (Expression? Left, Expression? Right) OperandNodes(Node node)
        => node switch
        {
            BinaryOp binOp => (binOp.Left, binOp.Right),
            Assignment assignment => (assignment.Target, assignment.Value),
            _ => (null, null),
        };

    private static string SpellOperand(Expression? operand, string fallback)
        => operand != null && AstHelper.UnwrapParenthesized(operand) is Identifier id
            ? id.Name
            : fallback;

    private void ReportUnsupportedBinaryOperator(
        Node node, string operatorSpelling,
        SemanticType left, SemanticType right,
        IReadOnlyDictionary<string, string>? data = null,
        string? messageSuffix = null)
    {
        var (leftNode, rightNode) = OperandNodes(node);
        ReportUnsupportedOperator(operatorSpelling, leftNode, rightNode,
            node.LineStart, node.ColumnStart, node.Span, left, right, data, messageSuffix);
    }

    /// <summary>
    /// THE operator refusal (SPY0222) of a binary operator, an augmented assignment and every
    /// comparison-chain link (P93 D3: a link is refused by its twin's message): reported at
    /// (<paramref name="line"/>, <paramref name="column"/>) — the binary expression, the assignment,
    /// or the link's left operand.
    /// </summary>
    private void ReportUnsupportedOperator(
        string operatorSpelling, Expression? leftNode, Expression? rightNode,
        int line, int column, Text.TextSpan? span,
        SemanticType left, SemanticType right,
        IReadOnlyDictionary<string, string>? data = null,
        string? messageSuffix = null)
    {
        var message = $"Type '{left.GetDisplayName()}' does not support operator '{operatorSpelling}' with operand of type '{right.GetDisplayName()}'";
        messageSuffix ??= PromotionRefusalSteer(leftNode, rightNode, left, right);
        if (messageSuffix != null)
            message += messageSuffix;

        // The RIGHT operand is the refused value and the LEFT type is the slot it was measured
        // against, so `s += n or "z"` and `"a" + (n or "z")` carry the #1819 steer by construction
        // — the augmented form only ever reached this reporter, never the store seam.
        ReportValueTypeMismatch(message, rightNode, left,
            line, column,
            code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
            span: span, data: data);
    }
}
