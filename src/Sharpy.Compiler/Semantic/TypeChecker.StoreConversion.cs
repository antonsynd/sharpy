using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The store-conversion seam — every store position's type compatibility decision, narrowing
/// side-effects, and diagnostic reporting pass through the same three methods:
/// <see cref="ClassifyStore"/>, <see cref="CheckStore"/>, <see cref="EnterStore"/>.
/// </summary>
internal partial class TypeChecker
{
    internal enum StorePosition
    {
        Declaration,
        PlainStore,
        MemberStore,
        IndexStore,
        DictStore,
        Return,
        Yield,
        ParameterDefault,
        LambdaParameterDefault,
        PropertyDefault,
        ArgumentPositional,
        ArgumentKeyword,
        TupleElement,
        Walrus,
        CollectionElement,
        LambdaBody,
        Augmented,
    }

    internal enum StoreVerdict
    {
        Accepted,
        AcceptedWithNarrowing,
        AcceptedConstantConversion,
        AcceptedFloat32Narrowing,
        AcceptedDecimalNarrowing,
        AcceptedLiteralString,
        AcceptedConditional,

        /// <summary>
        /// A RemoveNone-narrowed read stored into a slot of its OWN declared wrapper type passes the
        /// wrapper through (R-T pass-through): `b = a` inside `if a is not None:` with both `int?`
        /// is a whole-slot store — no unwrap, no wrap fact — and the narrowing of the target does
        /// not enter into it. Applying the verdict removes the read's accessor so the emitter
        /// prints the raw read.
        /// </summary>
        AcceptedNarrowedPassThrough,
        Refused,
        RefusedNoneIntoNonNullable,
        RefusedOptionalConstruction,
        RefusedNullableIntoOptional,
    }

    /// <summary>
    /// Whether a verdict admits the value. The one place the accepted half of the lattice is
    /// enumerated — every consumer (the collection-literal arms, the argument routes, the
    /// augmented site) asks this rather than re-listing the arms, so a new accepted verdict
    /// cannot be missed at one position and honoured at another.
    /// </summary>
    private static bool IsAcceptedVerdict(StoreVerdict verdict)
        => verdict is StoreVerdict.Accepted
            or StoreVerdict.AcceptedWithNarrowing
            or StoreVerdict.AcceptedConstantConversion
            or StoreVerdict.AcceptedFloat32Narrowing
            or StoreVerdict.AcceptedDecimalNarrowing
            or StoreVerdict.AcceptedLiteralString
            or StoreVerdict.AcceptedConditional
            or StoreVerdict.AcceptedNarrowedPassThrough;

    private StoreVerdict ClassifyStore(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType,
        bool allowConstantConversion = true)
    {
        // 1. Direct assignability
        if (IsAssignable(valueType, targetType))
            return StoreVerdict.Accepted;

        // The SLOT the value-shape arms measure against. `T | None` is real C# nullability and
        // stays loose (Decision 7): `sbyte? x = 7;` and `float? f = 0.5f;` compile, so a value
        // shape admitted into `T` is admitted into `T | None`. `T?` (Optional) is NOT unwrapped —
        // it is a tagged union whose only constructors are Some(v)/None() (R-G, #1720).
        var slotType = targetType is NullableType nullableSlot ? nullableSlot.UnderlyingType : targetType;

        // 2. Integer constant conversion
        if (allowConstantConversion
            && ImplicitConversions.IsImplicitIntegerConstantConversion(
                value, valueType, slotType, MakeConstantResolver()))
            return StoreVerdict.AcceptedConstantConversion;

        // 3. Float32 literal narrowing — every store position (Decision 6 ruled A, #1688)
        if (ImplicitConversions.IsFloat32LiteralNarrowing(slotType, valueType, value))
            return StoreVerdict.AcceptedFloat32Narrowing;

        // 4. Decimal literal narrowing — every store position (same as float32)
        if (ImplicitConversions.IsDecimalLiteralNarrowing(slotType, valueType, value))
            return StoreVerdict.AcceptedDecimalNarrowing;

        // 5. Literal-derived string into LiteralString (#1731)
        if (slotType is LiteralStringType
            && valueType == SemanticType.Str
            && value != null
            && _semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(value)))
            return StoreVerdict.AcceptedLiteralString;

        // 6. Conditional-of-constants: both branches classified against the SAME slot, recursively.
        //    `x8 = 7 if c else 8` is admitted exactly when `x8 = 7` and `x8 = 8` are; the
        //    conditional's own recorded type stays its natural type and the emitter casts each
        //    branch admitted by the constant arm (ConditionalBranchNarrowing, Decision 1).
        if (value != null
            && AstHelper.UnwrapParenthesized(value) is ConditionalExpression conditional
            && ClassifyConditionalBranch(position, conditional.ThenValue, targetType, allowConstantConversion) is { } thenVerdict
            && ClassifyConditionalBranch(position, conditional.ElseValue, targetType, allowConstantConversion) is { } elseVerdict
            && IsAcceptedVerdict(thenVerdict)
            && IsAcceptedVerdict(elseVerdict))
        {
            return StoreVerdict.AcceptedConditional;
        }

        // 6b. Narrowed pass-through (R-T): the value is a read that a RemoveNone narrowing stripped
        //     of its wrapper, and the slot IS that wrapper. The read's un-narrowed type is
        //     reconstructed from the accessor kind the checker recorded for it — UnwrapOptional
        //     means the slot the read comes from is `Optional[valueType]`, the nullable accessors
        //     mean `valueType | None` — so an Identifier, MemberAccess or IndexAccess read is
        //     covered by one rule at every position (declaration, return, argument, member/index
        //     store, walrus, tuple element). Before this arm the read was refused SPY0604 as a bare
        //     payload into `T?`, which no spelling of the read could satisfy but `Some(a)` —
        //     re-wrapping a value that is already the wrapper (type_narrowing.md §Stores Use the
        //     Declared Type, "a narrowed read stored into a slot of its own declared type passes
        //     the Optional through").
        if (value != null
            && NarrowedReadPassesThrough(AstHelper.UnwrapParenthesized(value), valueType, targetType))
        {
            return StoreVerdict.AcceptedNarrowedPassThrough;
        }

        // 7. Strict Optional construction — bare values and bare None are refused into T?
        if (targetType is OptionalType)
        {
            // A `T | None` value into `T?` is a NOTATION mismatch, not a missing constructor: the
            // value already carries its absence. Steering it to Some(...) would be wrong advice.
            return valueType is NullableType
                ? StoreVerdict.RefusedNullableIntoOptional
                : StoreVerdict.RefusedOptionalConstruction;
        }

        // 8. VoidType into non-nullable
        if (valueType is VoidType && targetType is not NullableType)
            return StoreVerdict.RefusedNoneIntoNonNullable;

        // 9. Otherwise
        return StoreVerdict.Refused;
    }

    /// <summary>
    /// Whether <paramref name="read"/> is a RemoveNone-narrowed read whose un-narrowed wrapper is
    /// exactly <paramref name="targetType"/> (see <see cref="StoreVerdict.AcceptedNarrowedPassThrough"/>).
    /// Side-effect free — it runs during overload probing too. Idempotent across the two
    /// classifications a store site performs: once <see cref="ApplyAcceptedVerdict"/> has removed
    /// the read's accessor it records the pass-through, and the second classification reads that
    /// record instead of the (now absent) lowering.
    /// </summary>
    private bool NarrowedReadPassesThrough(Expression read, SemanticType narrowedType, SemanticType targetType)
    {
        if (read is not (Identifier or MemberAccess or IndexAccess))
            return false;

        if (_semanticInfo.GetNarrowedReadPassThrough(read) is { } passedAs)
            return passedAs.Equals(targetType);

        SemanticType? unnarrowed = _semanticInfo.GetNarrowedReadLowering(read)?.Kind switch
        {
            NarrowedReadKind.UnwrapOptional => new OptionalType { UnderlyingType = narrowedType },
            NarrowedReadKind.NullableValue or NarrowedReadKind.NullForgiving
                => new NullableType { UnderlyingType = narrowedType },
            _ => null,
        };

        return unnarrowed != null && unnarrowed.Equals(targetType);
    }

    /// <summary>
    /// Classifies one arm of a conditional-expression value against the store's slot, or null when
    /// the arm has no recorded type (it was never checked — error recovery). The arm's type comes
    /// from <see cref="SemanticInfo.GetExpressionType"/> because the conditional has already been
    /// checked by the time a store consults the seam.
    /// </summary>
    private StoreVerdict? ClassifyConditionalBranch(
        StorePosition position, Expression branch, SemanticType targetType, bool allowConstantConversion)
    {
        var branchType = _semanticInfo.GetExpressionType(branch);
        if (branchType == null || branchType is UnknownType)
            return null;

        return ClassifyStore(position, branch, branchType, targetType, allowConstantConversion);
    }

    /// <summary>
    /// The side effects an ACCEPTED verdict carries — the facts codegen reads. Factored out of
    /// <see cref="CheckStore"/> so every position that admits a value through
    /// <see cref="ClassifyStore"/> without going through <c>CheckStore</c> (collection-literal
    /// elements, the argument-binding routes, the augmented site) applies the SAME effects. A
    /// position that classifies but does not apply is the defect class this seam exists to close:
    /// the checker says `float32` and the emitter prints an unsuffixed `double`.
    /// </summary>
    private void ApplyAcceptedVerdict(
        StorePosition position,
        StoreVerdict verdict,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType)
    {
        if (!IsAcceptedVerdict(verdict))
            return;

        switch (verdict)
        {
            case StoreVerdict.AcceptedFloat32Narrowing when value != null:
                _semanticInfo.SetExpressionType(value, SemanticType.Float32);
                break;

            case StoreVerdict.AcceptedDecimalNarrowing when value != null:
                _semanticInfo.SetExpressionType(value, SemanticType.Decimal);
                break;

            case StoreVerdict.AcceptedConditional when value != null:
                ApplyConditionalBranchVerdicts(position, value, targetType);
                break;

            case StoreVerdict.AcceptedNarrowedPassThrough when value != null:
                // The read is the wrapper again: drop its accessor and narrowed type so the emitter
                // prints `a`, not `a.Unwrap()`, and record the pass-through so the store site's
                // second classification (the version-typing switch) reaches the same verdict.
                _semanticInfo.PassNarrowedReadThrough(AstHelper.UnwrapParenthesized(value), targetType);
                _semanticInfo.SetExpressionType(value, targetType);
                break;
        }

        RecordSequenceMaterialization(value, valueType, targetType);
    }

    /// <summary>
    /// Applies each arm's own accepted verdict, and records
    /// <see cref="SemanticInfo.SetConditionalBranchNarrowing"/> for an arm admitted by the integer
    /// constant arm. C# gives `c ? 7 : 8` the natural type `int`, so `sbyte b = c ? 7 : 8;` is
    /// CS0266 — the cast the emitter reads from that fact is what makes the store legal. A
    /// float32/decimal arm needs no fact: its literal is re-typed per node and prints its own suffix.
    /// </summary>
    private void ApplyConditionalBranchVerdicts(
        StorePosition position, Expression value, SemanticType targetType)
    {
        if (AstHelper.UnwrapParenthesized(value) is not ConditionalExpression conditional)
            return;

        foreach (var branch in new[] { conditional.ThenValue, conditional.ElseValue })
        {
            var branchType = _semanticInfo.GetExpressionType(branch);
            if (branchType == null)
                continue;

            var verdict = ClassifyStore(position, branch, branchType, targetType);
            ApplyAcceptedVerdict(position, verdict, branch, branchType, targetType);

            if (verdict == StoreVerdict.AcceptedConstantConversion)
            {
                var slotType = targetType is NullableType nullable ? nullable.UnderlyingType : targetType;
                _semanticInfo.SetConditionalBranchNarrowing(branch, slotType);
            }
        }
    }

    private bool CheckStore(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType,
        Node reportAt,
        TextSpan? span,
        string? slotName = null,
        string? extraSteer = null)
        => CheckStoreAt(position, value, valueType, targetType,
            reportAt.LineStart, reportAt.ColumnStart, span, slotName, extraSteer);

    /// <summary>
    /// <see cref="CheckStore"/> against an explicit report position, for the one store site whose
    /// reporting anchor is not a <see cref="Node"/>: a <see cref="KeywordArgument"/> carries its own
    /// line/column but is not an AST node.
    /// </summary>
    private bool CheckStoreAt(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType,
        int reportLine,
        int reportColumn,
        TextSpan? span,
        string? slotName = null,
        string? extraSteer = null)
    {
        var verdict = ClassifyStore(position, value, valueType, targetType);

        if (IsAcceptedVerdict(verdict))
        {
            ApplyAcceptedVerdict(position, verdict, value, valueType, targetType);
            return true;
        }

        switch (verdict)
        {
            case StoreVerdict.RefusedNoneIntoNonNullable:
                AddError(
                    $"Cannot assign 'None' to non-nullable type '{targetType.GetDisplayName()}'",
                    reportLine, reportColumn,
                    code: DiagnosticCodes.Semantic.NullabilityViolation,
                    span: value?.Span ?? span);
                return false;

            case StoreVerdict.RefusedOptionalConstruction:
                {
                    var underlying = ((OptionalType)targetType).UnderlyingType.GetDisplayName();
                    var steer = valueType is VoidType
                        ? $"bare None is not an Optional[{underlying}]; use None(), or declare the slot '{underlying} | None'"
                        : $"'{valueType.GetDisplayName()}' is not an Optional[{underlying}]; construct it with Some(...)";
                    AddError(
                        steer,
                        reportLine, reportColumn,
                        code: DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
                        span: span);
                    return false;
                }

            default:
                var refusalCode = position == StorePosition.Return
                    ? DiagnosticCodes.Semantic.MissingReturnValue
                    : DiagnosticCodes.Semantic.TypeMismatch;
                AddError(
                    FormatStoreError(position, valueType, targetType, slotName)
                        + DescribeStoreRefusalSteer(position, valueType, targetType)
                        + (extraSteer ?? string.Empty),
                    reportLine, reportColumn,
                    code: refusalCode,
                    span: span);
                return false;
        }
    }

    /// <summary>
    /// The seam's verdict WITHOUT the diagnostic: classify, apply the accepted verdict's side
    /// effects, and report the answer to a caller that owns the refusal (the lambda body, whose
    /// refusal is the enclosing declaration's function-type mismatch, and the argument routes,
    /// whose refusal carries site data). Never a second decision — the same
    /// <see cref="ClassifyStore"/> and the same <see cref="ApplyAcceptedVerdict"/>.
    /// </summary>
    private bool CheckStoreQuietly(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType)
    {
        var verdict = ClassifyStore(position, value, valueType, targetType);
        if (!IsAcceptedVerdict(verdict))
            return false;

        ApplyAcceptedVerdict(position, verdict, value, valueType, targetType);
        return true;
    }

    /// <summary>
    /// Applies the accepted verdict for an argument that a BOUND parameter slot has accepted — the
    /// argument-position twin of the side effects <see cref="CheckStore"/> applies everywhere else.
    ///
    /// <para>Argument acceptance is decided during overload probing by
    /// <see cref="IsArgumentAssignable"/>, which must stay side-effect free (no candidate has been
    /// chosen yet). That split is exactly how the facts went missing: five routes accepted a
    /// <c>1.0</c> into a <c>float</c> parameter and none of them re-typed the literal, so the
    /// emitter printed an unsuffixed <c>double</c> and Roslyn answered CS1503 behind SPY0908
    /// (#1688). Every final binding site calls this once the parameter is known.</para>
    /// </summary>
    private void ApplyArgumentConversion(
        StorePosition position, Expression? argument, SemanticType argumentType, SemanticType parameterType)
    {
        if (argument == null)
            return;

        CheckStoreQuietly(position, argument, argumentType, parameterType);
    }

    /// <summary>
    /// The steer a refused store carries, at EVERY position (Decision 1). Three shapes, in the
    /// order they can apply: an <c>Optional[T]</c> value at a non-Optional slot (narrow or unwrap),
    /// a <c>T | None</c> value at a <c>T?</c> slot (cross with <c>maybe</c>), and a CLR collection
    /// at a Sharpy-collection slot (convert inward). Owned here rather than at the sites so a new
    /// position gets the advice by construction — an <c>Optional</c> refused at <c>return</c> or
    /// <c>yield</c> had none before this.
    /// </summary>
    private static string DescribeStoreRefusalSteer(
        StorePosition position, SemanticType valueType, SemanticType targetType)
    {
        if (valueType is NullableType nullableValue && targetType is OptionalType optionalSlot)
        {
            return $" — the value is '{nullableValue.UnderlyingType.GetDisplayName()} | None' (C# nullability)"
                + $" and the slot is Optional[{optionalSlot.UnderlyingType.GetDisplayName()}];"
                + " cross with 'maybe' (e.g. 'z: int? = maybe y')";
        }

        var noun = position is StorePosition.ArgumentPositional or StorePosition.ArgumentKeyword
            ? "argument"
            : "value";

        return DescribeOptionalArgument(valueType, targetType, noun)
            + DescribeClrCollectionConversionSteer(valueType, targetType);
    }

    private static string FormatStoreError(
        StorePosition position,
        SemanticType valueType,
        SemanticType targetType,
        string? slotName)
    {
        var value = valueType.GetDisplayName();
        var target = targetType.GetDisplayName();

        return position switch
        {
            StorePosition.Declaration or StorePosition.PlainStore or StorePosition.Walrus
                => $"Cannot assign type '{value}' to variable of type '{target}'",

            StorePosition.MemberStore or StorePosition.IndexStore or StorePosition.DictStore
                => $"Cannot assign type '{value}' to '{target}'",

            StorePosition.Return
                => $"Cannot return type '{value}' from function expecting '{target}'",

            StorePosition.Yield
                => $"Yielded type '{value}' is not assignable to declared return type '{target}'",

            StorePosition.ParameterDefault
                => $"Default value type '{value}' is not assignable to parameter type '{target}'",

            StorePosition.LambdaParameterDefault
                => $"Default value of type '{value}' is not assignable to parameter type '{target}'",

            StorePosition.LambdaBody
                => $"Arrow lambda body type '{value}' is not assignable to declared return type '{target}'",

            StorePosition.PropertyDefault
                => $"Cannot assign type '{value}' to property of type '{target}'",

            StorePosition.ArgumentPositional
                => $"Cannot pass argument of type '{value}' to parameter of type '{target}'",

            StorePosition.ArgumentKeyword
                => $"Cannot pass argument of type '{value}' to parameter '{slotName}' of type '{target}'",

            StorePosition.Augmented
                => $"Result type '{value}' of augmented assignment is not assignable to target type '{target}'",

            StorePosition.TupleElement or StorePosition.CollectionElement
                => $"Cannot assign type '{value}' to '{target}'",

            _ => $"Cannot assign type '{value}' to '{target}'",
        };
    }

    private IDisposable EnterStore(StorePosition position, SemanticType targetType, Expression? valueNode)
    {
        var savedExpectedType = _expectedType;
        var savedParameterTypedArgument = _parameterTypedArgument;

        _expectedType = targetType is UnknownType ? null : targetType;
        _parameterTypedArgument = position switch
        {
            StorePosition.ArgumentPositional or StorePosition.ArgumentKeyword => valueNode,
            StorePosition.ParameterDefault or StorePosition.LambdaParameterDefault when valueNode != null
                => ParameterTypedArgumentOf(targetType, valueNode),
            _ => _parameterTypedArgument,
        };

        return new StoreScope(this, savedExpectedType, savedParameterTypedArgument);
    }

    private sealed class StoreScope : IDisposable
    {
        private readonly TypeChecker _checker;
        private readonly SemanticType? _savedExpectedType;
        private readonly Expression? _savedParameterTypedArgument;

        public StoreScope(
            TypeChecker checker,
            SemanticType? savedExpectedType,
            Expression? savedParameterTypedArgument)
        {
            _checker = checker;
            _savedExpectedType = savedExpectedType;
            _savedParameterTypedArgument = savedParameterTypedArgument;
        }

        public void Dispose()
        {
            _checker._expectedType = _savedExpectedType;
            _checker._parameterTypedArgument = _savedParameterTypedArgument;
        }
    }
}
