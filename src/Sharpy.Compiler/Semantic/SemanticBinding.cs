using System.Collections.Concurrent;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Stores semantic information that is computed after AST creation.
/// This is the sole write target for semantic data during analysis; Symbol properties
/// are populated later by materialization at phase boundaries.
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
/// All writes go exclusively to SemanticBinding stores. At phase boundaries (freeze points),
/// MaterializeXxx() methods copy data onto Symbol properties for downstream consumers
/// (SemanticType.cs, ImportResolver clones) that read Symbol properties directly.
/// </para>
/// <para>
/// By storing this information in SemanticBinding instead of on the AST/Symbol directly,
/// we enable:
/// - Multiple bindings per AST (useful for LSP with incremental edits)
/// - Thread-safe parallel compilation (ConcurrentDictionary)
/// - Clear separation between parsing and semantic analysis
/// - Phase-gating: freeze assertions prevent writes after a phase completes
/// </para>
/// <para>
/// <b>Threading:</b> Internal stores use <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for cross-file symbol materialization, but the freeze-gate bool flags
/// (<c>_inheritanceFrozen</c>, etc.) are plain fields — not volatile. Under the current
/// single-threaded-per-phase execution model this is safe, but concurrent freeze/write
/// races would require marking the flags <c>volatile</c> or using <c>Interlocked</c>.
/// </para>
/// </remarks>
public class SemanticBinding
{
    // Use ReferenceEqualityComparer for all symbol-keyed dictionaries.
    // Symbol types are records with mutable properties (BaseType, CodeGenInfo, etc.),
    // making their value-based GetHashCode/Equals unstable. Reference equality
    // ensures dictionary lookups remain correct even as symbols are mutated during
    // semantic analysis.

    // Maps symbols to their CodeGenInfo
    private readonly ConcurrentDictionary<Symbol, CodeGenInfo> _codeGenInfo =
        new(ReferenceEqualityComparer.Instance);

    // Maps variable symbols to their resolved types
    private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes =
        new(ReferenceEqualityComparer.Instance);

    // Maps type symbols to their resolved base types
    private readonly ConcurrentDictionary<TypeSymbol, TypeSymbol> _baseTypes =
        new(ReferenceEqualityComparer.Instance);

    // Maps type symbols to their resolved interface lists (ConcurrentQueue preserves insertion order)
    private readonly ConcurrentDictionary<TypeSymbol, ConcurrentQueue<InterfaceReference>> _interfaces =
        new(ReferenceEqualityComparer.Instance);

    // Maps FromImportStatement nodes to their resolved module paths
    private readonly ConcurrentDictionary<FromImportStatement, string> _resolvedModulePaths = new();

    // Maps FromImportStatement nodes to their re-exported symbols
    private readonly ConcurrentDictionary<FromImportStatement, Dictionary<string, Symbol>> _reExportedSymbols = new();

    // Tracks which import module names refer to .NET stdlib modules
    private readonly ConcurrentDictionary<string, bool> _netModuleNames = new();

    // Phase-gating freeze flags - prevent mutations after a phase completes.
    // Volatile for thread safety when per-file bindings are frozen concurrently.
    private volatile bool _inheritanceFrozen;
    private volatile bool _variableTypesFrozen;
    private volatile bool _codeGenInfoFrozen;
    private volatile bool _netModuleNamesFrozen;

    /// <summary>
    /// Throws when a frozen store is written to after the corresponding phase has completed.
    /// This is always active (not DEBUG-only) to catch phase violations in production.
    /// </summary>
    private static void AssertNotFrozen(string storeName, string symbolName)
    {
        var operation = storeName switch
        {
            "CodeGenInfo" => "set CodeGenInfo",
            "VariableTypes" => "set variable type",
            "Inheritance" => "set inheritance data",
            "NetModuleNames" => "mark .NET module",
            _ => $"write {storeName}"
        };

        var phase = storeName switch
        {
            "CodeGenInfo" => "code generation info",
            "VariableTypes" => "type checking",
            "Inheritance" => "inheritance resolution",
            "NetModuleNames" => "import resolution",
            _ => storeName
        };

        throw new PhaseViolationException(operation, phase, symbolName);
    }

    /// <summary>
    /// Freeze inheritance data (BaseType, Interfaces) after inheritance resolution completes.
    /// Any subsequent SetBaseType/AddInterface calls will emit a logged warning.
    /// </summary>
    internal void FreezeInheritance() => _inheritanceFrozen = true;

    /// <summary>
    /// Freeze variable type data after type checking completes.
    /// Any subsequent SetVariableType calls will emit a logged warning.
    /// </summary>
    internal void FreezeVariableTypes() => _variableTypesFrozen = true;

    /// <summary>
    /// Freeze CodeGenInfo data after type checking completes.
    /// Any subsequent SetCodeGenInfo calls will emit a logged warning.
    /// </summary>
    internal void FreezeCodeGenInfo() => _codeGenInfoFrozen = true;

    /// <summary>
    /// Freeze .NET module name data after import resolution completes.
    /// Any subsequent MarkAsNetModule calls will throw.
    /// </summary>
    internal void FreezeNetModules() => _netModuleNamesFrozen = true;

    #region CodeGenInfo

    /// <summary>
    /// Sets the CodeGenInfo for a symbol.
    /// </summary>
    public void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
    {
        if (_codeGenInfoFrozen)
        {
            AssertNotFrozen("CodeGenInfo", symbol.Name);
        }
        _codeGenInfo[symbol] = info;
    }

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

    /// <summary>
    /// Removes the CodeGenInfo for a symbol.
    /// Used during incremental compilation to invalidate stale cached symbols.
    /// </summary>
    public bool RemoveCodeGenInfo(Symbol symbol)
        => _codeGenInfo.TryRemove(symbol, out _);

    #endregion

    #region Variable Types

    /// <summary>
    /// Sets the resolved type for a variable symbol.
    /// </summary>
    public void SetVariableType(VariableSymbol symbol, SemanticType type)
    {
        if (_variableTypesFrozen)
        {
            AssertNotFrozen("VariableTypes", symbol.Name);
        }
        _variableTypes[symbol] = type;
    }

    /// <summary>
    /// Gets the resolved type for a variable symbol.
    /// Returns SemanticType.Unknown if not set.
    /// </summary>
    public SemanticType GetVariableType(VariableSymbol symbol)
        => _variableTypes.TryGetValue(symbol, out var type) ? type : SemanticType.Unknown;

    /// <summary>
    /// Removes the resolved type for a variable symbol.
    /// Used during incremental compilation to invalidate stale cached symbols.
    /// </summary>
    public bool RemoveVariableType(VariableSymbol symbol)
        => _variableTypes.TryRemove(symbol, out _);

    #endregion

    #region Base Types

    /// <summary>
    /// Sets the base type for a type symbol.
    /// </summary>
    public void SetBaseType(TypeSymbol symbol, TypeSymbol baseType)
    {
        if (_inheritanceFrozen)
        {
            AssertNotFrozen("Inheritance", symbol.Name);
        }
        _baseTypes[symbol] = baseType;
    }

    /// <summary>
    /// Gets the base type for a type symbol, or null if not set.
    /// </summary>
    public TypeSymbol? GetBaseType(TypeSymbol symbol)
        => _baseTypes.TryGetValue(symbol, out var bt) ? bt : null;

    #endregion

    #region Interfaces

    /// <summary>
    /// Adds a resolved interface to a type symbol with full type argument information.
    /// </summary>
    public void AddInterface(TypeSymbol symbol, InterfaceReference iface)
    {
        if (_inheritanceFrozen)
        {
            AssertNotFrozen("Inheritance", symbol.Name);
        }
        var queue = _interfaces.GetOrAdd(symbol, _ => new ConcurrentQueue<InterfaceReference>());
        queue.Enqueue(iface);
    }

    /// <summary>
    /// Adds a resolved interface to a type symbol (convenience overload without type arguments).
    /// </summary>
    public void AddInterface(TypeSymbol symbol, TypeSymbol iface)
    {
        AddInterface(symbol, new InterfaceReference { Definition = iface });
    }

    /// <summary>
    /// Gets the resolved interfaces for a type symbol.
    /// Returns null if the symbol has no interfaces registered in this binding.
    /// </summary>
    public IReadOnlyList<InterfaceReference>? GetInterfaces(TypeSymbol symbol)
        => _interfaces.TryGetValue(symbol, out var queue) ? queue.ToList() : null;

    #endregion

    #region Materialization

    /// <summary>
    /// Copy inheritance data (BaseType, Interfaces) from SemanticBinding stores onto Symbol properties.
    /// Called at the inheritance freeze point so downstream consumers that read Symbol properties directly
    /// (e.g., SemanticType.cs, ImportResolver clones) see the correct values.
    /// </summary>
    internal void MaterializeInheritance()
    {
        foreach (var (symbol, baseType) in _baseTypes)
            symbol.BaseType = baseType;
        foreach (var (symbol, queue) in _interfaces)
            foreach (var iface in queue)
                if (!symbol.Interfaces.Any(i => ReferenceEquals(i, iface) || i.Definition.Name == iface.Definition.Name))
                    symbol.Interfaces.Add(iface);
    }

    /// <summary>
    /// Copy variable type data from SemanticBinding stores onto VariableSymbol.Type properties.
    /// Called at the variable types freeze point.
    /// </summary>
    internal void MaterializeVariableTypes()
    {
        foreach (var (symbol, type) in _variableTypes)
            symbol.Type = type;
    }

    /// <summary>
    /// Copy CodeGenInfo data from SemanticBinding stores onto Symbol.CodeGenInfo properties.
    /// Called at the CodeGenInfo freeze point.
    /// </summary>
    internal void MaterializeCodeGenInfo()
    {
        foreach (var (symbol, info) in _codeGenInfo)
            symbol.CodeGenInfo = info;
    }

    #endregion

    /// <summary>
    /// Merges all entries from another SemanticBinding into this instance.
    /// Used to combine per-file SemanticBinding back into a project-level instance.
    /// Skips inheritance data (BaseTypes, Interfaces) which are set during Phase 4b
    /// and already present in the shared binding.
    /// </summary>
    public void MergeFrom(SemanticBinding other)
    {
        foreach (var (symbol, info) in other._codeGenInfo)
            _codeGenInfo.TryAdd(symbol, info);

        foreach (var (symbol, type) in other._variableTypes)
            _variableTypes.TryAdd(symbol, type);

        foreach (var (stmt, path) in other._resolvedModulePaths)
            _resolvedModulePaths.TryAdd(stmt, path);

        foreach (var (stmt, symbols) in other._reExportedSymbols)
            _reExportedSymbols.TryAdd(stmt, symbols);
    }

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

    /// <summary>
    /// Marks a module name as a .NET stdlib module for codegen to emit correct using directives.
    /// </summary>
    public void MarkAsNetModule(string moduleName)
    {
        if (_netModuleNamesFrozen)
        {
            AssertNotFrozen("NetModuleNames", moduleName);
        }
        _netModuleNames[moduleName] = true;
    }

    /// <summary>
    /// Checks if a module name was marked as a .NET stdlib module during import resolution.
    /// </summary>
    public bool IsNetModule(string moduleName)
        => _netModuleNames.ContainsKey(moduleName);

    #endregion
}
