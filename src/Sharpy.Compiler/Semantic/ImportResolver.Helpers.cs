using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// ImportResolver partial class: small visibility/predicate helpers, public lookup helpers,
/// and error-recovery symbol/module factories.
/// </summary>
internal partial class ImportResolver
{
    /// <summary>
    /// Check if a symbol name can be directly imported (not double-underscore private)
    /// </summary>
    private bool IsDirectlyImportable(string symbolName)
    {
        return !symbolName.StartsWith("__");
    }

    /// <summary>
    /// Check if a symbol name is exported by 'import *' (public symbols only)
    /// </summary>
    private bool IsExportedByImportAll(string symbolName)
    {
        return !symbolName.StartsWith("_");
    }

    /// <summary>
    /// Filter symbols for 'import *' - only includes public symbols
    /// </summary>
    public Dictionary<string, Symbol> GetImportAllSymbols(ModuleInfo moduleInfo)
    {
        return moduleInfo.ExportedSymbols
            .Where(kvp => IsExportedByImportAll(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Search all loaded modules in the cache for a TypeSymbol with the given name.
    /// Used to discover transitive base types that were parsed but not explicitly imported.
    /// </summary>
    public TypeSymbol? FindTypeInLoadedModules(string typeName)
    {
        return _moduleLoader.FindTypeInLoadedModules(typeName);
    }

    /// <summary>
    /// Creates an error recovery symbol for a failed import.
    /// Error recovery symbols are placeholders that prevent cascading "undefined identifier"
    /// errors in the TypeChecker when the root cause (import failure) has already been reported.
    /// </summary>
    /// <param name="name">The name of the imported symbol that couldn't be resolved</param>
    /// <param name="moduleName">The module name that failed to resolve</param>
    /// <param name="line">Declaration line for diagnostics</param>
    /// <param name="column">Declaration column for diagnostics</param>
    /// <returns>A VariableSymbol marked as error recovery with UnknownType</returns>
    private static Symbol CreateErrorRecoverySymbol(string name, string moduleName, int? line, int? column)
    {
        // Use VariableSymbol with UnknownType as a safe placeholder
        // This allows the symbol to be found in the symbol table without crashing
        // the TypeChecker, while IsErrorRecovery suppresses cascading error messages
        return new VariableSymbol
        {
            Name = name,
            Kind = SymbolKind.Variable,
            Type = SemanticType.Unknown,
            IsErrorRecovery = true,
            OriginalModule = moduleName,
            DeclarationLine = line,
            DeclarationColumn = column,
            NameDeclarationLine = null,
            NameDeclarationColumn = null
        };
    }

    /// <summary>
    /// Creates an error recovery ModuleSymbol for a module that couldn't be loaded.
    /// </summary>
    /// <param name="moduleName">The module name that failed to resolve</param>
    /// <param name="line">Declaration line for diagnostics</param>
    /// <param name="column">Declaration column for diagnostics</param>
    /// <returns>A ModuleSymbol marked as error recovery</returns>
    private static ModuleSymbol CreateErrorRecoveryModule(string moduleName, int? line, int? column)
    {
        return new ModuleSymbol
        {
            Name = moduleName,
            Kind = SymbolKind.Module,
            FilePath = "<error-recovery>",
            Exports = new Dictionary<string, Symbol>(),
            IsErrorRecovery = true,
            DeclarationLine = line,
            DeclarationColumn = column,
            NameDeclarationLine = null,
            NameDeclarationColumn = null
        };
    }
}
