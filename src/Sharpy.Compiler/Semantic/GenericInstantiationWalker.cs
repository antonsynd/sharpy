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

            // Walk the base class chain so types like `class MyList[T](list[T])`
            // expose list (and list's interfaces) as instantiated supertypes.
            var baseSupertype = InstantiateBaseType(symbol, substitution, binding, typeResolver);
            if (baseSupertype == null)
                continue;

            if (!visited.Add(MakeKey(baseSupertype.Definition, baseSupertype.TypeArguments)))
                continue;

            yield return baseSupertype;

            var baseSubstitution = BuildSubstitution(
                baseSupertype.Definition.TypeParameters, baseSupertype.TypeArguments);
            if (baseSubstitution != null)
                queue.Enqueue((baseSupertype.Definition, baseSubstitution));
        }
    }

    /// <summary>
    /// Enumerates the BASE CLASS chain of <paramref name="symbol"/> instantiated at
    /// <paramref name="typeArguments"/>, nearest ancestor first, composing each level's
    /// <see cref="BaseTypeReference"/> substitution into the next (#1408, #1409, #1345).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the base-class half of <see cref="EnumerateSupertypes"/>, split out because its three
    /// consumers ask a different question. <c>EnumerateSupertypes</c> answers "is X assignable to Y",
    /// so it walks interfaces too and requires a <see cref="GenericType"/> receiver; member lookup,
    /// constructor forwarding and constructor-reference pinning ask "what does my ANCESTOR look like
    /// once my base clause's written arguments are substituted in", and their receiver is frequently
    /// a NON-generic <see cref="UserDefinedType"/> (<c>class IntList(List[int])</c> has no type
    /// parameters of its own). Both share <see cref="InstantiateBaseType"/>, so there is exactly one
    /// reader of <see cref="BaseTypeReference"/> in the compiler.
    /// </para>
    /// <para>
    /// Composition across levels is what makes this more than a one-step lookup:
    /// <c>class A(B[int])</c> over <c>class B[T](C[T])</c> yields <c>B[int]</c> then <c>C[int]</c>,
    /// because level 1's <c>{T -> int}</c> map is applied to level 2's written <c>[T]</c>.
    /// </para>
    /// <para>
    /// The walk stops (rather than guessing) wherever <see cref="InstantiateBaseType"/> declines:
    /// a generic base whose written arguments cannot be read is UNKNOWN, per #1287 Design Decision 2.
    /// </para>
    /// </remarks>
    /// <param name="symbol">The derived type whose ancestors are wanted.</param>
    /// <param name="typeArguments">
    /// The derived type's own type arguments. Empty for a non-generic derived type; an arity mismatch
    /// against <paramref name="symbol"/>'s type parameters yields nothing rather than a wrong answer.
    /// </param>
    /// <param name="binding">Optional binding for in-progress (pre-materialization) inheritance data.</param>
    /// <param name="typeResolver">Optional resolver for source-declared base type arguments.</param>
    internal static IEnumerable<InstantiatedSupertype> EnumerateBaseChain(
        TypeSymbol symbol,
        IReadOnlyList<SemanticType> typeArguments,
        SemanticBinding? binding = null,
        TypeResolver? typeResolver = null)
    {
        var substitution = BuildSubstitution(symbol.TypeParameters, typeArguments);
        if (substitution == null)
            yield break;

        var current = symbol;
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance) { symbol };

        while (true)
        {
            var next = InstantiateBaseType(current, substitution, binding, typeResolver);
            if (next == null)
                yield break;

            // Reference-identity cycle guard: a malformed hierarchy (A : B, B : A) must terminate.
            if (!visited.Add(next.Definition))
                yield break;

            yield return next;

            var nextSubstitution = BuildSubstitution(next.Definition.TypeParameters, next.TypeArguments);
            if (nextSubstitution == null)
                yield break;

            current = next.Definition;
            substitution = nextSubstitution;
        }
    }

    /// <summary>
    /// Enumerates every interface IMPLEMENTED by <paramref name="symbol"/> instantiated at
    /// <paramref name="typeArguments"/>, in BFS order (most direct first), composing each level's
    /// written interface arguments through the substitution — the interface-channel counterpart to
    /// <see cref="EnumerateBaseChain"/> for a <see cref="TypeSymbol"/> receiver (#1342).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EnumerateSupertypes"/> answers the same "what do my supertypes look like at my
    /// arguments" question, but it requires a <see cref="GenericType"/> receiver and additionally
    /// yields base classes; the Self-in-interface bridge asks it of a possibly NON-generic class
    /// (<c>class Box(IBuilder[int])</c> has no type parameters of its own) and wants only the
    /// interfaces. Interfaces reached THROUGH the base chain are included — an interface a base
    /// class implements is implemented by the derived class too — composed via
    /// <see cref="InstantiateBaseType"/>, so <c>class Leaf(Mid[int])</c> over
    /// <c>class Mid[T](IBuilder[T])</c> reaches <c>IBuilder[int]</c>. The base class itself is
    /// walked but not yielded: this channel answers "which interfaces", not "which ancestors".
    /// </para>
    /// <para>
    /// Shares <see cref="GetInterfaceReferences"/>/<see cref="ResolveReferenceArguments"/> with
    /// <see cref="EnumerateSupertypes"/>, so the compiler still reads an
    /// <see cref="InterfaceReference"/>'s arguments in exactly one place, mirroring the "exactly
    /// one reader of BaseTypeReference" contract on <see cref="EnumerateBaseChain"/>.
    /// </para>
    /// </remarks>
    /// <param name="symbol">The implementing type whose interfaces are wanted.</param>
    /// <param name="typeArguments">
    /// The implementing type's own type arguments. Empty for a non-generic type; an arity mismatch
    /// against <paramref name="symbol"/>'s type parameters yields nothing rather than a wrong answer.
    /// </param>
    /// <param name="binding">Optional binding for in-progress (pre-materialization) inheritance data.</param>
    /// <param name="typeResolver">Optional resolver for source-declared interface type arguments.</param>
    internal static IEnumerable<InstantiatedSupertype> EnumerateImplementedInterfaces(
        TypeSymbol symbol,
        IReadOnlyList<SemanticType> typeArguments,
        SemanticBinding? binding = null,
        TypeResolver? typeResolver = null)
    {
        var initialSubstitution = BuildSubstitution(symbol.TypeParameters, typeArguments);
        if (initialSubstitution == null)
            yield break;

        var queue = new Queue<(TypeSymbol Symbol, Dictionary<string, SemanticType> Substitution)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((symbol, initialSubstitution));
        visited.Add(MakeKey(symbol, typeArguments));

        while (queue.Count > 0)
        {
            var (current, substitution) = queue.Dequeue();

            foreach (var interfaceRef in GetInterfaceReferences(current, binding))
            {
                var rawArguments = ResolveReferenceArguments(interfaceRef, current, typeResolver);
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

            // Follow the base chain to reach interfaces implemented by ancestors, but do NOT yield
            // the base class itself — this channel produces interfaces only.
            var baseSupertype = InstantiateBaseType(current, substitution, binding, typeResolver);
            if (baseSupertype == null)
                continue;

            if (!visited.Add(MakeKey(baseSupertype.Definition, baseSupertype.TypeArguments)))
                continue;

            var baseSubstitution = BuildSubstitution(
                baseSupertype.Definition.TypeParameters, baseSupertype.TypeArguments);
            if (baseSubstitution != null)
                queue.Enqueue((baseSupertype.Definition, baseSubstitution));
        }
    }

    /// <summary>
    /// The nearest ancestor of <paramref name="symbol"/> that is backed by a CLR type, instantiated
    /// at the arguments written down the base chain (#1409). Returns null when no ancestor is
    /// CLR-backed, or when the chain's arguments cannot be read.
    /// </summary>
    internal static InstantiatedSupertype? FindClrAncestor(
        TypeSymbol symbol,
        IReadOnlyList<SemanticType> typeArguments,
        SemanticBinding? binding = null,
        TypeResolver? typeResolver = null)
        => EnumerateBaseChain(symbol, typeArguments, binding, typeResolver)
            .FirstOrDefault(a => a.Definition.ClrType != null);

    /// <summary>
    /// The nearest ancestor of <paramref name="symbol"/> that declares constructors, paired with
    /// those constructors' parameters re-typed through the composed base-clause substitution
    /// (#1408). Returns null when no ancestor declares a constructor.
    /// </summary>
    /// <remarks>
    /// The ancestor's <see cref="TypeSymbol.Constructors"/> are built once from the OPEN definition
    /// and shared across every instantiation, so <c>List[T].List(IEnumerable[T])</c> is the same
    /// <see cref="FunctionSymbol"/> for <c>IntList</c> and for <c>StrList</c>. The substitution
    /// therefore has to be applied per derived class, here, and not stored back on the ancestor.
    /// </remarks>
    internal static (TypeSymbol Ancestor, IReadOnlyList<FunctionSymbol> Constructors)? FindSubstitutedConstructors(
        TypeSymbol symbol,
        IReadOnlyList<SemanticType> typeArguments,
        SemanticBinding? binding = null,
        TypeResolver? typeResolver = null)
    {
        foreach (var ancestor in EnumerateBaseChain(symbol, typeArguments, binding, typeResolver))
        {
            if (ancestor.Definition.Constructors.Count == 0)
                continue;

            var map = BuildSubstitution(ancestor.Definition.TypeParameters, ancestor.TypeArguments);
            var substituted = ancestor.Definition.Constructors
                .Select(ctor => map == null || map.Count == 0
                    ? ctor
                    : ctor with
                    {
                        Parameters = ctor.Parameters
                            .Select(p => p with { Type = TypeSubstitution.Apply(p.Type, map) })
                            .ToList()
                    })
                .ToList();

            return (ancestor.Definition, substituted);
        }

        return null;
    }

    /// <summary>
    /// Instantiates a symbol's direct base class with the current substitution (#1287).
    /// Reads the base type's written or CLR-resolved arguments from
    /// <see cref="BaseTypeReference"/> and substitutes through them, mirroring
    /// the interface arm's shape. Returns null when there is no walkable base.
    /// </summary>
    private static InstantiatedSupertype? InstantiateBaseType(
        TypeSymbol symbol,
        Dictionary<string, SemanticType> substitution,
        SemanticBinding? binding,
        TypeResolver? typeResolver)
    {
        var baseSymbol = binding?.GetBaseType(symbol) ?? symbol.BaseType;
        if (baseSymbol == null || ReferenceEquals(baseSymbol, symbol))
            return null;

        if (!baseSymbol.IsGeneric)
            return new InstantiatedSupertype(baseSymbol, Array.Empty<SemanticType>());

        var baseRef = binding?.GetBaseTypeReference(symbol) ?? symbol.BaseTypeRef;
        if (baseRef != null)
        {
            var rawArguments = ResolveBaseReferenceArguments(baseRef, symbol, typeResolver);
            if (rawArguments != null && rawArguments.Count > 0)
            {
                var concreteArguments = rawArguments
                    .Select(arg => TypeSubstitution.Apply(arg, substitution))
                    .ToList();
                return new InstantiatedSupertype(baseSymbol, concreteArguments);
            }
        }

        // No fallback. A generic base whose arguments we cannot read is UNKNOWN, not "the derived
        // class's own parameters in order" (#1287, Design Decision 2).
        //
        // The deleted fallback copied `symbol`'s type parameters into the base positionally whenever
        // the two arities happened to coincide. That is right only for `class MyList[T](list[T])`,
        // where the written arguments are the parameters in order — and it is silently WRONG for
        // every other coincidence: `class MyList[T](List[int])` came back as `List[T]`, which both
        // accepts `List[str] = MyList[str]()` and refuses the correct `List[int] = MyList[str]()`.
        // Guessing from arity is not a fallback, it is a second, worse answer to the question
        // ResolveBaseReferenceArguments already answers from the written arguments.
        //
        // Returning null degrades to "this base contributes no supertype", which the callers already
        // handle: assignability declines rather than inventing a match, and inference leaves the
        // parameter open. A wrong supertype has no such recovery.
        return null;
    }

    /// <summary>
    /// Resolves a base type reference's type arguments to SemanticTypes, mirroring
    /// <see cref="ResolveReferenceArguments"/> for interfaces (#1287).
    /// </summary>
    private static IReadOnlyList<SemanticType>? ResolveBaseReferenceArguments(
        BaseTypeReference baseRef, TypeSymbol declaringSymbol, TypeResolver? typeResolver)
    {
        if (!baseRef.ResolvedTypeArguments.IsDefaultOrEmpty)
            return baseRef.ResolvedTypeArguments;

        if (baseRef.TypeArgAnnotations.IsDefaultOrEmpty)
            return null;

        var result = new List<SemanticType>(baseRef.TypeArgAnnotations.Length);
        foreach (var annotation in baseRef.TypeArgAnnotations)
        {
            var converted = ConvertAnnotation(annotation, declaringSymbol, typeResolver);
            if (converted == null)
                return null;
            result.Add(converted);
        }
        return result;
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
