namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE reader of dunder-synthesized interfaces (#1746, plan-499995 Design Decision 4). The
/// decision was made once, at inheritance resolution: <see cref="SynthesisAnalyzer.ClassifyDundersFromAst"/>
/// classified the dunders and <c>NameResolver</c> enqueued each row as an
/// <see cref="InterfaceReference"/> flagged with <see cref="InterfaceReference.SynthesizedVia"/>;
/// materialization applied its dedupe. Everything downstream — the emitter's base list on the cold
/// path (<c>CodeGenInfoComputer</c>) and the warm restore (<c>ProjectCompiler</c>, after the cached
/// references are back on the symbol) — READS that closure through this method and decides nothing.
/// The two earlier recomputations (one over <c>ProtocolMethods</c>, one over restored
/// <c>Methods</c>) each disagreed with the closure about at least one row, which is how an explicit
/// <c>ISized</c> plus <c>__len__</c> reached the base list twice (CS0528).
/// </summary>
internal static class SynthesizedInterfaceReader
{
    /// <summary>
    /// The synthesized rows of <paramref name="symbol"/>'s MATERIALIZED closure, in base-list
    /// order. A reference whose arguments cannot be resolved (no resolver and a leaf annotation
    /// that is not one of the declaring type's own parameters) is skipped rather than guessed.
    /// </summary>
    internal static List<SynthesizedInterfaceInfo> Read(TypeSymbol symbol, TypeResolver? resolver)
    {
        var result = new List<SynthesizedInterfaceInfo>();
        foreach (var iface in symbol.Interfaces)
        {
            if (iface.SynthesizedVia == null)
                continue;

            var typeArgs = ResolveArguments(symbol, iface, resolver);
            if (typeArgs == null)
                continue;

            result.Add(new SynthesizedInterfaceInfo(
                iface.Definition.Name,
                NamespaceOf(iface.Definition),
                typeArgs,
                iface.SynthesizedVia));
        }
        return result;
    }

    /// <summary>
    /// The namespace the emitter qualifies the interface with: the CLR definition's own for a
    /// CLR-backed definition (<c>Sharpy</c>, <c>System</c>, <c>System.Collections.Generic</c>),
    /// <c>Sharpy</c> for a Core interface reached through the symbol table.
    /// </summary>
    internal static string NamespaceOf(TypeSymbol definition)
        => definition.ClrType?.Namespace ?? "Sharpy";

    private static SemanticType[]? ResolveArguments(
        TypeSymbol declaringSymbol, InterfaceReference iface, TypeResolver? resolver)
    {
        if (!iface.ResolvedTypeArguments.IsDefaultOrEmpty)
            return iface.ResolvedTypeArguments.ToArray();
        if (iface.TypeArgAnnotations.IsDefaultOrEmpty)
            return Array.Empty<SemanticType>();

        var args = new SemanticType[iface.TypeArgAnnotations.Length];
        for (int i = 0; i < args.Length; i++)
        {
            // The walker's converter: the declaring type's own parameters stay open
            // (IReverseEnumerable<T> on Box[T]), modifiers are kept, everything else resolves.
            var converted = GenericInstantiationWalker.ConvertInterfaceArgument(
                iface.TypeArgAnnotations[i], declaringSymbol, resolver);
            if (converted == null)
                return null;
            args[i] = converted;
        }
        return args;
    }
}
