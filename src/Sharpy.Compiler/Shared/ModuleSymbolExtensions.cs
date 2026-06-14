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

        // Walk intermediate parts through nested module exports (e.g., email.message.Message).
        for (int i = startIndex; i < parts.Length - 1; i++)
        {
            if (!moduleSymbol.TryGetExport(parts[i], out var nestedSymbol)
                || nestedSymbol is not ModuleSymbol nestedModule)
            {
                return null;
            }

            moduleSymbol = nestedModule;
        }

        // The final part must resolve to an exported type.
        if (moduleSymbol.TryGetExport(parts[^1], out var exportedSymbol)
            && exportedSymbol is TypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        return null;
    }
}
