using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Extension methods for <see cref="ModuleSymbol"/> that centralise module-qualified type
/// lookup logic previously duplicated in TypeResolver, NameResolver, and TypeSyntaxMapper.
/// </summary>
internal static class ModuleSymbolExtensions
{
    /// <summary>
    /// Looks up a member in a module's exports, applying a PascalCase fallback for .NET modules.
    /// </summary>
    /// <param name="moduleSymbol">The module whose exports to search.</param>
    /// <param name="memberName">The member name to look up (snake_case or as-is).</param>
    /// <param name="exportedSymbol">
    /// When this method returns <c>true</c>, the resolved symbol; otherwise <c>default!</c>.
    /// </param>
    /// <returns><c>true</c> if the member was found; otherwise <c>false</c>.</returns>
    public static bool TryGetExport(this ModuleSymbol moduleSymbol, string memberName, out Symbol exportedSymbol)
    {
        if (moduleSymbol.Exports.TryGetValue(memberName, out exportedSymbol!))
            return true;

        if (moduleSymbol.IsNetModule)
        {
            var pascalName = NameMangler.ToPascalCase(memberName);
            if (moduleSymbol.Exports.TryGetValue(pascalName, out exportedSymbol!))
                return true;
        }

        exportedSymbol = default!;
        return false;
    }

    /// <summary>
    /// Looks up a type in a module's types-only export lookup, applying a PascalCase fallback
    /// for .NET modules (mirrors <see cref="TryGetExport"/>). This is consulted before
    /// <see cref="ModuleSymbol.Exports"/> so a value-position export sharing a name with a type
    /// (e.g. <c>sqlite3.Row</c>'s <c>row_factory</c> field) cannot shadow the type (#1092).
    /// </summary>
    public static bool TryGetExportedType(this ModuleSymbol moduleSymbol, string typeName, out TypeSymbol typeSymbol)
    {
        if (moduleSymbol.ExportedTypes.TryGetValue(typeName, out typeSymbol!))
            return true;

        if (moduleSymbol.IsNetModule)
        {
            var pascalName = NameMangler.ToPascalCase(typeName);
            if (moduleSymbol.ExportedTypes.TryGetValue(pascalName, out typeSymbol!))
                return true;
        }

        typeSymbol = default!;
        return false;
    }

    /// <summary>
    /// Resolves a module-qualified type by walking <paramref name="parts"/> from
    /// <paramref name="startIndex"/> through intermediate module exports and resolving the final
    /// segment as a <see cref="TypeSymbol"/>.
    /// </summary>
    /// <param name="rootModule">The already-resolved root module symbol.</param>
    /// <param name="parts">The split segments of the dotted name (e.g., ["email", "message", "Message"]).</param>
    /// <param name="startIndex">
    /// Index of the first segment to resolve (typically 1, since index 0 is the root module itself).
    /// </param>
    /// <returns>The resolved <see cref="TypeSymbol"/>, or <c>null</c> if resolution fails at any step.</returns>
    public static TypeSymbol? ResolveQualifiedType(this ModuleSymbol rootModule, string[] parts, int startIndex)
    {
        var moduleSymbol = rootModule;

        for (int i = startIndex; i < parts.Length; i++)
        {
            // Try as a module export (the common module-qualified path).
            if (moduleSymbol.TryGetExportedType(parts[i], out var exportedType))
            {
                if (i == parts.Length - 1)
                    return exportedType;

                // Intermediate segment is a TYPE, not a module: hand off to NestedTypes
                // for the remaining segments (e.g., lib.Registry.Entry — #1523).
                return WalkNestedTypes(exportedType, parts, i + 1);
            }

            if (moduleSymbol.TryGetExport(parts[i], out var exportedSymbol))
            {
                if (exportedSymbol is TypeSymbol typeSymbol)
                {
                    if (i == parts.Length - 1)
                        return typeSymbol;
                    return WalkNestedTypes(typeSymbol, parts, i + 1);
                }

                if (exportedSymbol is ModuleSymbol nestedModule)
                {
                    moduleSymbol = nestedModule;
                    continue;
                }
            }

            return null;
        }

        return null;
    }

    /// <summary>
    /// Walks <paramref name="parts"/> from <paramref name="startIndex"/> down a
    /// <see cref="TypeSymbol.NestedTypes"/> chain (e.g., <c>Outer.Inner.Innermost</c>). The one
    /// shared home for the nested-type walk: used by the module-qualified walk above (#1523) and
    /// by <c>NameResolver.ResolveBaseReference</c>'s dotted base-name arm (#1528), so callers do
    /// not grow per-site copies of the same loop.
    /// </summary>
    internal static TypeSymbol? WalkNestedTypes(TypeSymbol outerType, string[] parts, int startIndex)
    {
        var current = outerType;
        for (int i = startIndex; i < parts.Length; i++)
        {
            var nested = current.NestedTypes.FirstOrDefault(n => n.Name == parts[i]);
            if (nested == null)
                return null;
            current = nested;
        }
        return current;
    }

    /// <summary>
    /// The type-ALIAS sibling of <see cref="ResolveQualifiedType"/>: same walk, final segment
    /// resolved as a <see cref="TypeAliasSymbol"/>.
    /// </summary>
    /// <remarks>
    /// A sibling rather than a widened return, deliberately. <c>ResolveQualifiedType</c>'s
    /// <c>TypeSymbol?</c> contract is what four callers depend on; widening it to <c>Symbol?</c>
    /// would force every one of them to branch on a case they do not handle. The type-alias case is
    /// additive: <c>type Handle = int</c> from-imports fine and <c>lib.Handle</c> reported SPY0202
    /// only because both final-segment arms above require a <see cref="TypeSymbol"/> and an alias
    /// is not one (#1436, #1363 having already made it an export).
    /// </remarks>
    public static TypeAliasSymbol? ResolveQualifiedAlias(this ModuleSymbol rootModule, string[] parts, int startIndex)
    {
        var moduleSymbol = WalkToOwningModule(rootModule, parts, startIndex);
        if (moduleSymbol == null)
            return null;

        return moduleSymbol.TryGetExport(parts[^1], out var exportedSymbol)
            && exportedSymbol is TypeAliasSymbol aliasSymbol
                ? aliasSymbol
                : null;
    }

    /// <summary>
    /// Walks the intermediate segments of a dotted name through nested module exports
    /// (<c>email.message.Message</c> → the <c>email.message</c> module), or null when a segment is
    /// not a module. Shared so the type and alias resolvers cannot disagree about which module owns
    /// a name.
    /// </summary>
    private static ModuleSymbol? WalkToOwningModule(ModuleSymbol rootModule, string[] parts, int startIndex)
    {
        var moduleSymbol = rootModule;

        for (int i = startIndex; i < parts.Length - 1; i++)
        {
            if (!moduleSymbol.TryGetExport(parts[i], out var nestedSymbol)
                || nestedSymbol is not ModuleSymbol nestedModule)
            {
                return null;
            }

            moduleSymbol = nestedModule;
        }

        return moduleSymbol;
    }
}
