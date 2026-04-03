using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Semantic;
using LspSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Shared helpers for creating <see cref="TypeHierarchyItem"/> instances
/// from Sharpy <see cref="TypeSymbol"/>s.
/// </summary>
internal static class TypeHierarchyHelper
{
    /// <summary>
    /// Creates a <see cref="TypeHierarchyItem"/> from a type symbol.
    /// Returns null if the symbol has no declaration location.
    /// </summary>
    public static TypeHierarchyItem? CreateItem(TypeSymbol type, string fallbackUri)
    {
        // Skip builtins/CLR types that have no source location.
        if (type.DeclarationSpan == null)
            return null;

        var startLine = System.Math.Max(0, (type.DeclarationLine ?? 1) - 1);
        var startCol = System.Math.Max(0, (type.DeclarationColumn ?? 1) - 1);
        var endCol = startCol + type.Name.Length;

        var filePath = type.DeclaringFilePath ?? type.DefiningFilePath ?? fallbackUri;
        var uri = filePath.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri.From(filePath)
            : DocumentUri.FromFileSystemPath(filePath);

        var selectionRange = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(startLine, startCol),
            new Position(startLine, endCol));

        return new TypeHierarchyItem
        {
            Name = type.Name,
            Kind = MapTypeKind(type.TypeKind),
            Uri = uri,
            Range = selectionRange,
            SelectionRange = selectionRange,
            Detail = type.DefiningModule,
            Data = new JObject
            {
                ["name"] = type.Name,
                ["filePath"] = filePath
            }
        };
    }

    /// <summary>
    /// Resolves a <see cref="TypeSymbol"/> from a <see cref="TypeHierarchyItem"/>'s Data field
    /// by looking up the type name in the symbol table.
    /// </summary>
    public static TypeSymbol? ResolveFromItem(TypeHierarchyItem item, SymbolTable? symbolTable)
    {
        if (symbolTable == null)
            return null;

        // Try to extract name from Data
        var name = (item.Data as JObject)?["name"]?.ToString() ?? item.Name;
        return (symbolTable.Lookup(name) ?? symbolTable.LookupInModuleScopes(name)) as TypeSymbol;
    }

    private static LspSymbolKind MapTypeKind(TypeKind kind) => kind switch
    {
        TypeKind.Class => LspSymbolKind.Class,
        TypeKind.Struct => LspSymbolKind.Struct,
        TypeKind.Interface => LspSymbolKind.Interface,
        TypeKind.Enum => LspSymbolKind.Enum,
        _ => LspSymbolKind.Class
    };
}
