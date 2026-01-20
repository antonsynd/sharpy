using System.Collections.Concurrent;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Stores semantic information that is computed after AST creation.
/// This separates mutable semantic data from immutable syntax.
/// </summary>
/// <remarks>
/// <para>
/// The AST represents pure syntax - it's computed during parsing and should be immutable.
/// However, semantic analysis needs to attach additional information to AST nodes and symbols:
/// - Resolved types for expressions and variables
/// - CodeGen information for naming conventions
/// - Resolved module paths for imports
/// - Base types for class inheritance
/// </para>
/// <para>
/// By storing this information in SemanticBinding instead of on the AST/Symbol directly,
/// we enable:
/// - Multiple bindings per AST (useful for LSP with incremental edits)
/// - Thread-safe parallel compilation (ConcurrentDictionary)
/// - Clear separation between parsing and semantic analysis
/// </para>
/// </remarks>
public class SemanticBinding
{
    // Maps symbols to their CodeGenInfo
    private readonly ConcurrentDictionary<Symbol, CodeGenInfo> _codeGenInfo = new();

    // Maps variable symbols to their resolved types
    private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes = new();

    // Maps type symbols to their resolved base types
    private readonly ConcurrentDictionary<TypeSymbol, TypeSymbol> _baseTypes = new();

    // Maps FromImportStatement nodes to their resolved module paths
    private readonly ConcurrentDictionary<FromImportStatement, string> _resolvedModulePaths = new();

    // Maps FromImportStatement nodes to their re-exported symbols
    private readonly ConcurrentDictionary<FromImportStatement, Dictionary<string, Symbol>> _reExportedSymbols = new();

    #region CodeGenInfo

    /// <summary>
    /// Sets the CodeGenInfo for a symbol.
    /// </summary>
    public void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
        => _codeGenInfo[symbol] = info;

    /// <summary>
    /// Gets the CodeGenInfo for a symbol, or null if not set.
    /// </summary>
    public CodeGenInfo? GetCodeGenInfo(Symbol symbol)
        => _codeGenInfo.TryGetValue(symbol, out var info) ? info : null;

    /// <summary>
    /// Checks if a symbol has CodeGenInfo.
    /// </summary>
    public bool HasCodeGenInfo(Symbol symbol)
        => _codeGenInfo.ContainsKey(symbol);

    #endregion

    #region Variable Types

    /// <summary>
    /// Sets the resolved type for a variable symbol.
    /// </summary>
    public void SetVariableType(VariableSymbol symbol, SemanticType type)
        => _variableTypes[symbol] = type;

    /// <summary>
    /// Gets the resolved type for a variable symbol.
    /// Returns SemanticType.Unknown if not set.
    /// </summary>
    public SemanticType GetVariableType(VariableSymbol symbol)
        => _variableTypes.TryGetValue(symbol, out var type) ? type : SemanticType.Unknown;

    #endregion

    #region Base Types

    /// <summary>
    /// Sets the base type for a type symbol.
    /// </summary>
    public void SetBaseType(TypeSymbol symbol, TypeSymbol baseType)
        => _baseTypes[symbol] = baseType;

    /// <summary>
    /// Gets the base type for a type symbol, or null if not set.
    /// </summary>
    public TypeSymbol? GetBaseType(TypeSymbol symbol)
        => _baseTypes.TryGetValue(symbol, out var bt) ? bt : null;

    #endregion

    #region Module Resolution

    /// <summary>
    /// Sets the resolved module path for a FromImportStatement.
    /// </summary>
    public void SetResolvedModulePath(FromImportStatement stmt, string path)
        => _resolvedModulePaths[stmt] = path;

    /// <summary>
    /// Gets the resolved module path for a FromImportStatement, or null if not set.
    /// </summary>
    public string? GetResolvedModulePath(FromImportStatement stmt)
        => _resolvedModulePaths.TryGetValue(stmt, out var path) ? path : null;

    /// <summary>
    /// Sets the re-exported symbols for a FromImportStatement.
    /// </summary>
    public void SetReExportedSymbols(FromImportStatement stmt, Dictionary<string, Symbol> symbols)
        => _reExportedSymbols[stmt] = symbols;

    /// <summary>
    /// Gets the re-exported symbols for a FromImportStatement, or null if not set.
    /// </summary>
    public Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement stmt)
        => _reExportedSymbols.TryGetValue(stmt, out var symbols) ? symbols : null;

    #endregion
}
