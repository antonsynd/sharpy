using Microsoft.CodeAnalysis;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// G1 axis 2 (plan-638e8b C2): the path POSITIONS of an entry point, read off its signature — every
/// parameter whose bound type is <c>string</c>, the Stdlib <c>Path</c>, or a type either converts to
/// (<c>object</c>; the Stdlib declares no path union @ this commit), plus the receiver
/// (<see cref="Receiver"/>) of an instance member. No list: <c>os.rename</c>'s second parameter is a
/// position by its signature, not by an author's memory.
/// </summary>
internal static class PathPositions
{
    public const string Receiver = "self";

    public static IReadOnlyList<string> Of(ISymbol entry)
    {
        var positions = new System.Collections.Generic.List<string>();
        var parameters = entry switch
        {
            IMethodSymbol m => m.Parameters,
            IPropertySymbol p => p.Parameters,
            _ => System.Collections.Immutable.ImmutableArray<IParameterSymbol>.Empty,
        };
        var isInstance = !entry.IsStatic && entry is not IMethodSymbol { MethodKind: MethodKind.Constructor };
        if (isInstance)
            positions.Add(Receiver);
        foreach (var parameter in parameters)
        {
            if (IsPositionType(parameter.Type))
                positions.Add(parameter.Name);
        }
        if (entry is IPropertySymbol { SetMethod: not null } property && IsPositionType(property.Type))
            positions.Add("value");
        return positions;
    }

    private static bool IsPositionType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type.SpecialType is SpecialType.System_String or SpecialType.System_Object)
            return true;
        return type is INamedTypeSymbol { Name: "Path", ContainingNamespace.Name: "Sharpy", ContainingNamespace.ContainingNamespace.IsGlobalNamespace: true };
    }
}
