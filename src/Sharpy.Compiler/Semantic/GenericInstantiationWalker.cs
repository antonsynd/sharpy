using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Walks the supertype hierarchy (implemented interfaces) of an instantiated generic type,
/// producing concrete instantiations of each supertype by substituting the type's actual
/// type arguments into the hierarchy's type-parameter references (#827).
/// </summary>
/// <remarks>
/// Example: <c>list[int]</c> — the <c>list</c> TypeSymbol records that it implements
/// <c>IEnumerable[T0]</c> (with <c>T0</c> mapping positionally to list's own type
/// parameter). Substituting <c>T0 → int</c> yields the instantiated supertype
/// <c>IEnumerable[int]</c>.
/// </remarks>
internal static class GenericInstantiationWalker
{
    /// <summary>
    /// A supertype definition paired with the concrete type arguments produced by
    /// substituting the walked type's arguments through the hierarchy.
    /// </summary>
    internal sealed record InstantiatedSupertype(
        TypeSymbol Definition,
        IReadOnlyList<SemanticType> TypeArguments);

    /// <summary>
    /// Enumerates all instantiated supertypes of <paramref name="type"/> in BFS order
    /// (most direct first). Interfaces declared on the type and inherited transitively
    /// through interface extension are included. A visited set guards against cycles
    /// in the interface graph (e.g., self-referential <c>IComparable[Self]</c> patterns).
    /// </summary>
    /// <param name="type">The instantiated generic type to walk (e.g., <c>list[int]</c>).</param>
    /// <param name="symbolTable">Symbol table used to resolve the type's definition symbol.</param>
    /// <param name="binding">Optional binding for in-progress (pre-materialization) inheritance data.</param>
    /// <param name="typeResolver">Optional resolver for source-declared interface type arguments.</param>
    internal static IEnumerable<InstantiatedSupertype> EnumerateSupertypes(
        GenericType type,
        SymbolTable symbolTable,
        SemanticBinding? binding = null,
        TypeResolver? typeResolver = null)
    {
        var definition = ResolveDefinition(type, symbolTable);
        if (definition == null)
            yield break;

        var initialSubstitution = BuildSubstitution(definition.TypeParameters, type.TypeArguments);
        if (initialSubstitution == null)
            yield break;

        var queue = new Queue<(TypeSymbol Symbol, Dictionary<string, SemanticType> Substitution)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((definition, initialSubstitution));
        visited.Add(MakeKey(definition, type.TypeArguments));

        while (queue.Count > 0)
        {
            var (symbol, substitution) = queue.Dequeue();

            foreach (var interfaceRef in GetInterfaceReferences(symbol, binding))
            {
                var rawArguments = ResolveReferenceArguments(interfaceRef, symbol, typeResolver);
                if (rawArguments == null)
                    continue;

                var concreteArguments = rawArguments
                    .Select(arg => TypeSubstitution.Apply(arg, substitution))
                    .ToList();

                if (!visited.Add(MakeKey(interfaceRef.Definition, concreteArguments)))
                    continue;

                yield return new InstantiatedSupertype(interfaceRef.Definition, concreteArguments);

                var nextSubstitution = BuildSubstitution(interfaceRef.Definition.TypeParameters, concreteArguments);
                if (nextSubstitution != null)
                    queue.Enqueue((interfaceRef.Definition, nextSubstitution));
            }
        }
    }

    /// <summary>
    /// Resolves the defining TypeSymbol for an instantiated generic type. Prefers the
    /// explicit <see cref="GenericType.GenericDefinition"/>, then builtin registrations
    /// (list, dict, set, ...), then user-defined types in the symbol table.
    /// </summary>
    internal static TypeSymbol? ResolveDefinition(GenericType type, SymbolTable symbolTable)
        => type.GenericDefinition
           ?? symbolTable.BuiltinRegistry.GetType(type.Name)
           ?? symbolTable.LookupType(type.Name);

    /// <summary>
    /// Builds a name-based substitution map from a definition's type parameters to
    /// concrete type arguments. Returns null when the arity does not match.
    /// </summary>
    private static Dictionary<string, SemanticType>? BuildSubstitution(
        IReadOnlyList<TypeParameterDef> typeParameters,
        IReadOnlyList<SemanticType> typeArguments)
    {
        if (typeParameters.Count != typeArguments.Count)
            return null;

        var map = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
        for (int i = 0; i < typeParameters.Count; i++)
        {
            map[typeParameters[i].Name] = typeArguments[i];
        }
        return map;
    }

    /// <summary>
    /// Returns the direct interface references for a symbol, preferring in-progress
    /// binding data over materialized symbol properties (mirrors
    /// <see cref="TypeHierarchyService.GetDirectInterfaces"/> but keeps the full
    /// <see cref="InterfaceReference"/> so type arguments are preserved).
    /// </summary>
    private static IReadOnlyList<InterfaceReference> GetInterfaceReferences(
        TypeSymbol symbol, SemanticBinding? binding)
        => binding?.GetInterfaces(symbol) ?? (IReadOnlyList<InterfaceReference>)symbol.Interfaces;

    /// <summary>
    /// Resolves an interface reference's type arguments to SemanticTypes. CLR-discovered
    /// references carry <see cref="InterfaceReference.ResolvedTypeArguments"/> directly;
    /// source-declared references are converted from their AST annotations. Returns null
    /// when the arguments cannot be represented (the reference is then skipped).
    /// </summary>
    private static IReadOnlyList<SemanticType>? ResolveReferenceArguments(
        InterfaceReference interfaceRef, TypeSymbol declaringSymbol, TypeResolver? typeResolver)
    {
        if (!interfaceRef.ResolvedTypeArguments.IsDefaultOrEmpty)
            return interfaceRef.ResolvedTypeArguments;

        if (interfaceRef.TypeArgAnnotations.IsDefaultOrEmpty)
            return Array.Empty<SemanticType>();

        var result = new List<SemanticType>(interfaceRef.TypeArgAnnotations.Length);
        foreach (var annotation in interfaceRef.TypeArgAnnotations)
        {
            var converted = ConvertAnnotation(annotation, declaringSymbol, typeResolver);
            if (converted == null)
                return null;
            result.Add(converted);
        }
        return result;
    }

    /// <summary>
    /// Converts an interface type-argument annotation to a SemanticType. References to the
    /// declaring type's own type parameters become <see cref="TypeParameterType"/> so they
    /// participate in substitution; everything else falls back to the TypeResolver.
    /// </summary>
    private static SemanticType? ConvertAnnotation(
        TypeAnnotation annotation, TypeSymbol declaringSymbol, TypeResolver? typeResolver)
    {
        if (annotation.TypeArguments.Length == 0
            && declaringSymbol.TypeParameters.Any(tp => tp.Name == annotation.Name))
        {
            return new TypeParameterType { Name = annotation.Name };
        }

        if (annotation.TypeArguments.Length > 0)
        {
            var arguments = new List<SemanticType>(annotation.TypeArguments.Length);
            foreach (var argAnnotation in annotation.TypeArguments)
            {
                var converted = ConvertAnnotation(argAnnotation, declaringSymbol, typeResolver);
                if (converted == null)
                    return null;
                arguments.Add(converted);
            }
            return new GenericType { Name = annotation.Name, TypeArguments = arguments };
        }

        var resolved = typeResolver?.ResolveTypeAnnotation(annotation);
        return resolved is null or UnknownType ? null : resolved;
    }

    /// <summary>
    /// Builds a visited-set key from a definition symbol's identity and the display form
    /// of the instantiated type arguments, so the same definition can be revisited with a
    /// different instantiation (diamond hierarchies) but cycles terminate.
    /// </summary>
    private static string MakeKey(TypeSymbol definition, IReadOnlyList<SemanticType> typeArguments)
    {
        var identity = RuntimeHelpers.GetHashCode(definition);
        if (typeArguments.Count == 0)
            return $"{identity:X}|{definition.Name}";

        var args = string.Join(",", typeArguments.Select(arg => arg.GetDisplayName()));
        return $"{identity:X}|{definition.Name}[{args}]";
    }
}
