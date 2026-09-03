using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
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
        Refused,
        RefusedNoneIntoNonNullable,
        RefusedOptionalConstruction,
    }

    private StoreVerdict ClassifyStore(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType)
    {
        // 1. Direct assignability
        if (IsAssignable(valueType, targetType))
            return StoreVerdict.Accepted;

        // 2. Integer constant conversion
        if (ImplicitConversions.IsImplicitIntegerConstantConversion(
                value, valueType, targetType, MakeConstantResolver()))
            return StoreVerdict.AcceptedConstantConversion;

        // 3. Float32 literal narrowing — scoped to store-like positions; return and parameter
        //    signatures are part of the contract the caller reads, so `0.1` stays double (#1301).
        if (position is not StorePosition.Return
            and not StorePosition.ParameterDefault and not StorePosition.LambdaParameterDefault
            && ImplicitConversions.IsFloat32LiteralNarrowing(targetType, valueType, value))
            return StoreVerdict.AcceptedFloat32Narrowing;

        // 4. Decimal literal narrowing — same scope as float32
        if (position is not StorePosition.Return
            and not StorePosition.ParameterDefault and not StorePosition.LambdaParameterDefault
            && ImplicitConversions.IsDecimalLiteralNarrowing(targetType, valueType, value))
            return StoreVerdict.AcceptedDecimalNarrowing;

        // 5. Literal-derived string into LiteralString (placeholder — Phase 7)

        // 6. ConditionalExpression per-branch recursion (placeholder — Phase 2 Task 2)

        // 7. Strict Optional construction — bare values and bare None are refused into T?
        if (targetType is OptionalType)
            return StoreVerdict.RefusedOptionalConstruction;

        // 8. VoidType into non-nullable
        if (valueType is VoidType && targetType is not NullableType)
            return StoreVerdict.RefusedNoneIntoNonNullable;

        // 9. Otherwise
        return StoreVerdict.Refused;
    }

    private bool CheckStore(
        StorePosition position,
        Expression? value,
        SemanticType valueType,
        SemanticType targetType,
        Node reportAt,
        TextSpan? span,
        string? slotName = null)
    {
        var verdict = ClassifyStore(position, value, valueType, targetType);

        switch (verdict)
        {
            case StoreVerdict.Accepted:
            case StoreVerdict.AcceptedWithNarrowing:
                RecordSequenceMaterialization(value, valueType, targetType);
                return true;

            case StoreVerdict.AcceptedConstantConversion:
                RecordSequenceMaterialization(value, valueType, targetType);
                return true;

            case StoreVerdict.AcceptedFloat32Narrowing:
                _semanticInfo.SetExpressionType(value!, SemanticType.Float32);
                RecordSequenceMaterialization(value, valueType, targetType);
                return true;

            case StoreVerdict.AcceptedDecimalNarrowing:
                _semanticInfo.SetExpressionType(value!, SemanticType.Decimal);
                RecordSequenceMaterialization(value, valueType, targetType);
                return true;

            case StoreVerdict.AcceptedLiteralString:
                RecordSequenceMaterialization(value, valueType, targetType);
                return true;

            case StoreVerdict.RefusedNoneIntoNonNullable:
                AddError(
                    $"Cannot assign 'None' to non-nullable type '{targetType.GetDisplayName()}'",
                    reportAt.LineStart, reportAt.ColumnStart,
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
                    reportAt.LineStart, reportAt.ColumnStart,
                    code: DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
                    span: span);
                return false;
            }

            case StoreVerdict.Refused:
                var refusalCode = position == StorePosition.Return
                    ? DiagnosticCodes.Semantic.MissingReturnValue
                    : DiagnosticCodes.Semantic.TypeMismatch;
                AddError(
                    FormatStoreError(position, valueType, targetType, slotName)
                        + DescribeClrCollectionConversionSteer(valueType, targetType),
                    reportAt.LineStart, reportAt.ColumnStart,
                    code: refusalCode,
                    span: span);
                return false;

            default:
                return false;
        }
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
