using System.Linq;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Shared analysis of where a <c>Self</c> annotation sits in a type (#1342). A TOP-LEVEL
/// <c>Self</c> is bridgeable — the class implements the interface, and <c>Self</c> resolves to each
/// side's own type, so an explicit-interface bridge forwards soundly. A <c>Self</c> NESTED inside a
/// type argument has no sound bridge (<c>list[Box]</c> is not <c>list[IGroupable]</c>, C# generics
/// being invariant); in an interface member it is refused (SPY0497, shape 2).
/// </summary>
internal static class SelfTypeAnalysis
{
    /// <summary>
    /// True when a <c>Self</c> appears strictly INSIDE a composite type (a generic argument, tuple
    /// element, nullable/optional/result payload, or function-type position) rather than as the type
    /// itself.
    /// </summary>
    public static bool ContainsNestedSelf(SemanticType? type) => type switch
    {
        GenericType g => g.TypeArguments.Any(ContainsSelfAnywhere),
        TupleType t => t.ElementTypes.Any(ContainsSelfAnywhere),
        NullableType n => ContainsSelfAnywhere(n.UnderlyingType),
        OptionalType o => ContainsSelfAnywhere(o.UnderlyingType),
        ResultType r => ContainsSelfAnywhere(r.OkType) || ContainsSelfAnywhere(r.ErrorType),
        FunctionType f => f.ParameterTypes.Any(ContainsSelfAnywhere) || ContainsSelfAnywhere(f.ReturnType),
        _ => false
    };

    /// <summary>True when a <c>Self</c> appears anywhere in the type, including as the type itself.</summary>
    public static bool ContainsSelfAnywhere(SemanticType? type) => type switch
    {
        null => false,
        SelfType => true,
        GenericType g => g.TypeArguments.Any(ContainsSelfAnywhere),
        TupleType t => t.ElementTypes.Any(ContainsSelfAnywhere),
        NullableType n => ContainsSelfAnywhere(n.UnderlyingType),
        OptionalType o => ContainsSelfAnywhere(o.UnderlyingType),
        ResultType r => ContainsSelfAnywhere(r.OkType) || ContainsSelfAnywhere(r.ErrorType),
        FunctionType f => f.ParameterTypes.Any(ContainsSelfAnywhere) || ContainsSelfAnywhere(f.ReturnType),
        _ => false
    };
}
