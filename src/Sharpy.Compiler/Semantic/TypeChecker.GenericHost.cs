namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE host view for a constructed generic receiver (#1859 Design Decision 1): what
/// declaration a dunder/member route searches, and how to turn that declaration's signature into
/// the closed type at THIS receiver. Every dunder route — <c>__call__</c>, <c>__iter__</c>,
/// <c>__getitem__</c>, <c>__contains__</c>, <c>__reversed__</c>, <c>__len__</c>, <c>__bool__</c> —
/// asks this FIRST; the builtin-container arms (<c>GenericType.TypeArguments[0]</c>, the registry's
/// own <c>__call__</c>-bearing types) are reached only when it answers <c>null</c>. Before this, a
/// <c>GenericType</c> naming a USER declaration could fall into a builtin arm that has no opinion
/// about it — <c>ResolveBuiltinTypeInfo</c> asks the <c>BuiltinRegistry</c> alone, so <c>C[int]</c>'s
/// own <c>__call__</c> was never found (SPY0230, b01/b08), and <c>InferIterableElementType</c>'s
/// <c>TypeArguments[0]</c> arm answered the receiver's OWN first type argument for a non-generator
/// <c>__iter__</c> whose declared element is something else entirely (b04: <c>int32</c>, not the
/// declared <c>str</c>).
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// A constructed generic host's declaration and the substitution closure. <c>Definition</c> is
    /// what a dunder/member search walks (its own methods, its base chain, its interfaces);
    /// <c>Substitute</c> turns one of the definition's signature types (a return type, a parameter
    /// type) into the type it denotes AT THIS receiver — <c>T</c> becomes <c>int</c> for
    /// <c>Box[int]</c>, and is the identity function for a non-generic host (the same zero-argument
    /// view <see cref="AsInstantiatedGeneric"/> already gives a non-generic assignability SOURCE,
    /// #1244).
    /// </summary>
    internal readonly record struct GenericHostView(TypeSymbol Definition, Func<SemanticType, SemanticType> Substitute);

    /// <summary>Instance entry point — the checker's own symbol table.</summary>
    internal GenericHostView? TryGetGenericHost(SemanticType type) => TryGetGenericHostCore(type, _symbolTable);

    /// <summary>
    /// The static core, reachable from <see cref="TypeInferenceService"/> (which has its own
    /// <c>_symbolTable</c> but no <see cref="TypeChecker"/> host) so there is ONE implementation of
    /// "declaration + substitution" behind both callers — a second, independent read of the same
    /// question would be the parallel-site hazard the generic-variance walk already names (#1145).
    ///
    /// <para>A <see cref="GenericType"/> whose definition (<see cref="GenericType.GenericDefinition"/>
    /// or a symbol-table lookup by name) is NOT the symbol <see cref="Registry.BuiltinRegistry"/>
    /// would return for that same name is a constructed USER generic host — <c>Box[int]</c>. A
    /// <see cref="UserDefinedType"/> backed by a resolved symbol is a non-generic host, at the
    /// identity substitution. Everything else — a builtin container (<c>list[int]</c>,
    /// <c>dict[K,V]</c>, ...), a CLR-discovered type with no Sharpy declaration, an unresolved name —
    /// answers <c>null</c>, and the caller's existing builtin/CLR arm decides.</para>
    /// </summary>
    internal static GenericHostView? TryGetGenericHostCore(SemanticType type, SymbolTable symbolTable)
    {
        switch (type)
        {
            case GenericType gt:
            {
                var definition = gt.GenericDefinition ?? symbolTable.LookupType(gt.Name);
                if (definition == null)
                    return null;

                // Builtin-owned: the caller's own TypeArguments[0]/registry arm has the opinion here,
                // not a substituted walk of the definition's (registry-synthesized) methods.
                if (ReferenceEquals(definition, symbolTable.BuiltinRegistry.GetType(gt.Name)))
                    return null;

                var typeArgs = gt.TypeArguments;
                return new GenericHostView(definition, t => SubstituteThroughDefinition(t, definition, typeArgs));
            }

            case UserDefinedType { Symbol: { } symbol }:
                return new GenericHostView(symbol, t => t);

            default:
                return null;
        }
    }

    /// <summary>
    /// Substitutes <paramref name="definition"/>'s type parameters with <paramref name="typeArgs"/>
    /// in <paramref name="type"/> — the static twin of <see cref="SubstituteTypeParameters"/>, needed
    /// here because <see cref="TryGetGenericHostCore"/> has no <see cref="TypeChecker"/> instance to
    /// call it on. An arity mismatch (a bare generic definition referenced without arguments) leaves
    /// <paramref name="type"/> unchanged, matching the instance method's own "no opinion" behavior.
    /// </summary>
    private static SemanticType SubstituteThroughDefinition(
        SemanticType type, TypeSymbol definition, IReadOnlyList<SemanticType> typeArgs)
    {
        if (definition.TypeParameters.Count == 0 || definition.TypeParameters.Count != typeArgs.Count)
            return type;

        var substitutions = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
        for (int i = 0; i < definition.TypeParameters.Count; i++)
        {
            substitutions[definition.TypeParameters[i].Name] = typeArgs[i];
        }

        return TypeSubstitution.Apply(type, substitutions);
    }
}
