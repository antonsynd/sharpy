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
/// for cross-file symbol materialization. The freeze-gate bool flags
/// (<c>_inheritanceFrozen</c>, etc.) are <c>volatile</c> for thread safety
/// when per-file bindings are frozen concurrently.
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

    // Method symbols that override an abstract/virtual member of a CLR-backed base type (#1122).
    // Written by TypeChecker during type checking; bridged onto CodeGenInfo.OverridesClrBaseMember
    // at MaterializeCodeGenInfo. Symbol-keyed (Rule 2a) with reference equality.
    private readonly ConcurrentDictionary<Symbol, bool> _clrBaseOverrides =
        new(ReferenceEqualityComparer.Instance);

    // Constructor forwarders synthesized for a class with no __init__, with the base clause's written
    // type arguments already substituted into every parameter (#1408). Written by TypeChecker during
    // type checking; bridged onto CodeGenInfo.ForwardingConstructors at MaterializeCodeGenInfo.
    // Symbol-keyed (Rule 2a) with reference equality, mirroring _clrBaseOverrides (#1122).
    private readonly ConcurrentDictionary<Symbol, IReadOnlyList<FunctionSymbol>> _forwardingConstructors =
        new(ReferenceEqualityComparer.Instance);

    // Self-interface bridge specs for a class, fully resolved through the walker's interface channel
    // (#1342). Written by TypeChecker during type checking; bridged onto
    // CodeGenInfo.SelfInterfaceBridges at MaterializeCodeGenInfo. Symbol-keyed (Rule 2a) with
    // reference equality, mirroring _forwardingConstructors (#1408).
    private readonly ConcurrentDictionary<Symbol, IReadOnlyList<SelfInterfaceBridgeSpec>> _selfInterfaceBridges =
        new(ReferenceEqualityComparer.Instance);

    // #1521: synthesized protocol interfaces per type declaration. Written by CodeGenInfoComputer
    // during the compute pass; bridged onto CodeGenInfo.SynthesizedInterfaces at MaterializeCodeGenInfo.
    private readonly ConcurrentDictionary<Symbol, IReadOnlyList<SynthesizedInterfaceInfo>> _synthesizedInterfaces =
        new(ReferenceEqualityComparer.Instance);

    // Maps variable symbols to their resolved types
    private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes =
        new(ReferenceEqualityComparer.Instance);

    // Maps type symbols to their resolved base types
    private readonly ConcurrentDictionary<TypeSymbol, TypeSymbol> _baseTypes =
        new(ReferenceEqualityComparer.Instance);

    // Maps type symbols to their base type references (definition + type arguments) (#1287)
    private readonly ConcurrentDictionary<TypeSymbol, BaseTypeReference> _baseTypeRefs =
        new(ReferenceEqualityComparer.Instance);

    // Maps type symbols to their resolved interface lists (ConcurrentQueue preserves insertion order)
    private readonly ConcurrentDictionary<TypeSymbol, ConcurrentQueue<InterfaceReference>> _interfaces =
        new(ReferenceEqualityComparer.Instance);

    // Maps FromImportStatement nodes to their resolved module paths
    private readonly ConcurrentDictionary<FromImportStatement, string> _resolvedModulePaths = new();

    // Maps FromImportStatement nodes to their re-exported symbols
    private readonly ConcurrentDictionary<FromImportStatement, Dictionary<string, Symbol>> _reExportedSymbols = new();

    // Tracks which import module names refer to .NET stdlib modules.
    // Value is the C# namespace of the [SharpyModule] class (null if not available).
    private readonly ConcurrentDictionary<string, string?> _netModuleNames = new();

    // Maps a .NET stdlib module name to the simple class name of its [SharpyModule]
    // class (e.g. "email" -> "EmailModule"), null if not available from discovery.
    private readonly ConcurrentDictionary<string, string?> _netModuleClassNames = new();

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

    /// <summary>
    /// Marks a method symbol as overriding an abstract/virtual member of a CLR-backed base
    /// type (#1122). The fact is bridged onto <see cref="CodeGenInfo.OverridesClrBaseMember"/>
    /// at <see cref="MaterializeCodeGenInfo"/> so code generation emits the <c>override</c>
    /// modifier from a frozen fact.
    /// </summary>
    public void MarkOverridesClrBaseMember(Symbol symbol)
    {
        if (_codeGenInfoFrozen)
        {
            AssertNotFrozen("CodeGenInfo", symbol.Name);
        }
        _clrBaseOverrides[symbol] = true;
    }

    /// <summary>
    /// Whether a method symbol was marked as overriding a CLR-backed base member.
    /// </summary>
    public bool OverridesClrBaseMember(Symbol symbol)
        => _clrBaseOverrides.ContainsKey(symbol);

    /// <summary>
    /// Records the constructor forwarders a derived class inherits, with the base clause's written
    /// type arguments already substituted into every parameter (#1408). Bridged onto
    /// <see cref="CodeGenInfo.ForwardingConstructors"/> at <see cref="MaterializeCodeGenInfo"/> so
    /// code generation emits them from a frozen fact and makes no substitution of its own.
    /// </summary>
    public void SetForwardingConstructors(Symbol symbol, IReadOnlyList<FunctionSymbol> constructors)
    {
        if (_codeGenInfoFrozen)
        {
            AssertNotFrozen("CodeGenInfo", symbol.Name);
        }
        _forwardingConstructors[symbol] = constructors;
    }

    /// <summary>
    /// The recorded constructor forwarders for a derived class, or null if none were recorded.
    /// </summary>
    public IReadOnlyList<FunctionSymbol>? GetForwardingConstructors(Symbol symbol)
        => _forwardingConstructors.TryGetValue(symbol, out var ctors) ? ctors : null;

    /// <summary>
    /// Records the Self-interface bridge specs a class must emit, fully resolved through the walker's
    /// interface channel (#1342). Bridged onto <see cref="CodeGenInfo.SelfInterfaceBridges"/> at
    /// <see cref="MaterializeCodeGenInfo"/> so code generation emits them from a frozen fact and
    /// re-derives no interface instantiation or implementing-member match of its own.
    /// </summary>
    public void SetSelfInterfaceBridges(Symbol symbol, IReadOnlyList<SelfInterfaceBridgeSpec> bridges)
    {
        if (_codeGenInfoFrozen)
        {
            AssertNotFrozen("CodeGenInfo", symbol.Name);
        }
        _selfInterfaceBridges[symbol] = bridges;
    }

    /// <summary>
    /// The recorded Self-interface bridge specs for a class, or null if none were recorded.
    /// </summary>
    public IReadOnlyList<SelfInterfaceBridgeSpec>? GetSelfInterfaceBridges(Symbol symbol)
        => _selfInterfaceBridges.TryGetValue(symbol, out var bridges) ? bridges : null;

    public void SetSynthesizedInterfaces(Symbol symbol, IReadOnlyList<SynthesizedInterfaceInfo> interfaces)
    {
        if (_codeGenInfoFrozen)
        {
            AssertNotFrozen("CodeGenInfo", symbol.Name);
        }
        _synthesizedInterfaces[symbol] = interfaces;
    }

    public IReadOnlyList<SynthesizedInterfaceInfo>? GetSynthesizedInterfaces(Symbol symbol)
        => _synthesizedInterfaces.TryGetValue(symbol, out var ifaces) ? ifaces : null;

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

    /// <summary>
    /// Sets the base type reference (definition + type arguments) for a type symbol (#1287).
    /// </summary>
    public void SetBaseTypeReference(TypeSymbol symbol, BaseTypeReference baseRef)
    {
        if (_inheritanceFrozen)
        {
            AssertNotFrozen("Inheritance", symbol.Name);
        }
        _baseTypeRefs[symbol] = baseRef;
    }

    /// <summary>
    /// Gets the base type reference for a type symbol, or null if not set.
    /// </summary>
    public BaseTypeReference? GetBaseTypeReference(TypeSymbol symbol)
        => _baseTypeRefs.TryGetValue(symbol, out var br) ? br : null;

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
        // Cross-analysis safety (#1140 H3): shared (registry-cached) module TypeSymbols are
        // constructed fully resolved by discovery — they never carry UnresolvedBaseName /
        // UnresolvedInterfaces — so the writes below target only per-analysis symbols
        // (deferred-inheritance symbols come from the per-analysis ModuleLoader, not the shared
        // registry). The duplicate check on Interfaces makes residual write-backs no-ops. If
        // deferred-inheritance symbols ever enter the shared registry, this materialization
        // becomes a cross-analysis write hazard.
        foreach (var (symbol, baseType) in _baseTypes)
            symbol.BaseType = baseType;
        foreach (var (symbol, baseRef) in _baseTypeRefs)
            symbol.BaseTypeRef = baseRef;
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
        // Ordering invariant: the bridging below only fires for symbols that already have a
        // _codeGenInfo entry. CodeGenInfoComputer unconditionally records CodeGenInfo for every
        // method before this freeze point runs, so a _clrBaseOverrides mark (#1122) always finds
        // its entry — if SetCodeGenInfo ever becomes conditional for methods, marks would be
        // silently dropped here.
        System.Diagnostics.Debug.Assert(
            System.Linq.Enumerable.All(_clrBaseOverrides.Keys, s => _codeGenInfo.ContainsKey(s)),
            "A CLR-base-override mark exists for a symbol with no CodeGenInfo entry; " +
            "the fact would be silently dropped at materialization (#1122).");

        System.Diagnostics.Debug.Assert(
            System.Linq.Enumerable.All(_forwardingConstructors.Keys, s => _codeGenInfo.ContainsKey(s)),
            "Constructor forwarders were recorded for a symbol with no CodeGenInfo entry; " +
            "the fact would be silently dropped at materialization and the emitter would fall back " +
            "to the ancestor's OPEN signatures (#1408).");

        System.Diagnostics.Debug.Assert(
            System.Linq.Enumerable.All(_selfInterfaceBridges.Keys, s => _codeGenInfo.ContainsKey(s)),
            "Self-interface bridge specs were recorded for a symbol with no CodeGenInfo entry; " +
            "the fact would be silently dropped at materialization and the Self member would leak " +
            "CS0535/CS0738 behind SPY0908 (#1342).");

        System.Diagnostics.Debug.Assert(
            System.Linq.Enumerable.All(_synthesizedInterfaces.Keys, s => _codeGenInfo.ContainsKey(s)),
            "Synthesized-interface lists were recorded for a symbol with no CodeGenInfo entry; " +
            "the fact would be silently dropped at materialization and the emitter would omit the " +
            "protocol interfaces the dunders promised (#1521).");

        foreach (var (symbol, info) in _codeGenInfo)
        {
            var effective = info;

            // Preserve the original CLR method name on the CodeGenInfo so code
            // generation can emit it verbatim (avoids name-mangling acronym loss).
            if (symbol is FunctionSymbol { ClrMethodName: { } clrName } && effective.ClrMethodName is null)
                effective = effective with { ClrMethodName = clrName };

            // Bridge the CLR-base-override fact (#1122) so code generation emits `override`
            // from a frozen fact without re-deriving the base-member match.
            if (_clrBaseOverrides.ContainsKey(symbol) && !effective.OverridesClrBaseMember)
                effective = effective with { OverridesClrBaseMember = true };

            // Bridge the substituted constructor forwarders (#1408) for the same reason: the base's
            // written type arguments are a semantic fact, and the emitter must not re-derive them.
            if (effective.ForwardingConstructors is null
                && _forwardingConstructors.TryGetValue(symbol, out var forwarders))
                effective = effective with { ForwardingConstructors = forwarders };

            // Bridge the Self-interface bridge specs (#1342) for the same reason: the composed
            // interface instantiation and the resolved implementing member are semantic facts.
            if (effective.SelfInterfaceBridges is null
                && _selfInterfaceBridges.TryGetValue(symbol, out var bridges))
                effective = effective with { SelfInterfaceBridges = bridges };

            // Bridge synthesized protocol interfaces (#1521): the analyzer's result is frozen so
            // the emitter reads it without re-running SynthesisAnalyzer.
            if (effective.SynthesizedInterfaces is null
                && _synthesizedInterfaces.TryGetValue(symbol, out var synthesized))
                effective = effective with { SynthesizedInterfaces = synthesized };

            symbol.CodeGenInfo = effective;
        }
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

        // CLR-base-override marks are consumed at MaterializeCodeGenInfo, which runs on the
        // project-level binding after this merge — so per-file marks must carry across (#1122).
        foreach (var (symbol, _) in other._clrBaseOverrides)
            _clrBaseOverrides.TryAdd(symbol, true);

        // Same consumption point, same obligation (#1408): the forwarders are bridged at
        // MaterializeCodeGenInfo on the project-level binding, after this merge.
        foreach (var (symbol, forwarders) in other._forwardingConstructors)
            _forwardingConstructors.TryAdd(symbol, forwarders);

        // Same consumption point, same obligation (#1342): the Self-interface bridge specs are
        // bridged at MaterializeCodeGenInfo on the project-level binding, after this merge.
        foreach (var (symbol, bridges) in other._selfInterfaceBridges)
            _selfInterfaceBridges.TryAdd(symbol, bridges);

        foreach (var (symbol, ifaces) in other._synthesizedInterfaces)
            _synthesizedInterfaces.TryAdd(symbol, ifaces);

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
    public void MarkAsNetModule(string moduleName, string? csharpNamespace = null, string? csharpClassName = null)
    {
        if (_netModuleNamesFrozen)
        {
            AssertNotFrozen("NetModuleNames", moduleName);
        }
        _netModuleNames[moduleName] = csharpNamespace;
        if (csharpClassName != null)
        {
            _netModuleClassNames[moduleName] = csharpClassName;
        }
    }

    /// <summary>
    /// Checks if a module name was marked as a .NET stdlib module during import resolution.
    /// </summary>
    public bool IsNetModule(string moduleName)
        => _netModuleNames.ContainsKey(moduleName);

    /// <summary>
    /// Gets the C# namespace of a .NET module's [SharpyModule] class, or null if not available.
    /// </summary>
    public string? GetNetModuleCSharpNamespace(string moduleName)
        => _netModuleNames.TryGetValue(moduleName, out var ns) ? ns : null;

    /// <summary>
    /// Gets the simple C# class name of a .NET module's [SharpyModule] class
    /// (e.g. "EmailModule" for "email"), or null if not available from discovery.
    /// </summary>
    public string? GetNetModuleCSharpClassName(string moduleName)
        => _netModuleClassNames.TryGetValue(moduleName, out var name) ? name : null;

    #endregion
}
