using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves imports and loads symbols from imported modules (both .spy files and .NET assemblies).
/// Delegates module loading/caching/symbol-extraction to <see cref="ModuleLoader"/>.
/// </summary>
internal class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly ModuleLoader _moduleLoader;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly ModuleResolver _moduleResolver;

    /// <summary>
    /// All loaded .spy modules (excludes .NET modules).
    /// Key is the full file path, value is the ModuleInfo.
    /// </summary>
    public IReadOnlyDictionary<string, ModuleInfo> LoadedSpyModules => _moduleLoader.LoadedSpyModules;
    private IDependencyRecorder? _dependencyRecorder;
    private SemanticBinding _semanticBinding = new();

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null,
        ModuleResolver? moduleResolver = null,
        SemanticBinding? semanticBinding = null, IDependencyRecorder? dependencyRecorder = null)
        : this(new ModuleLoader(logger), logger, moduleRegistry, moduleResolver,
            semanticBinding, dependencyRecorder)
    {
    }

    public ImportResolver(ModuleLoader moduleLoader, ICompilerLogger? logger = null,
        ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null,
        SemanticBinding? semanticBinding = null, IDependencyRecorder? dependencyRecorder = null)
    {
        _moduleLoader = moduleLoader;
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _moduleResolver = moduleResolver ?? new ModuleResolver(logger);
        if (semanticBinding != null)
            _semanticBinding = semanticBinding;
        _dependencyRecorder = dependencyRecorder;
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    private CancellationToken _cancellationToken;

    /// <summary>
    /// Updates the current module path on all internal components.
    /// </summary>
    private void UpdateCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
        _moduleLoader.CurrentModulePath = modulePath;
        _moduleResolver.SetCurrentModulePath(modulePath);
    }

    /// <summary>
    /// Resolve all imports in a module and register the imported symbols in the symbol table.
    /// This is the main entry point for import resolution during compilation.
    /// </summary>
    public void ResolveAllImports(Module module, SymbolTable symbolTable, string? currentDir,
        CancellationToken cancellationToken = default, string? currentModulePath = null)
    {
        _cancellationToken = cancellationToken;
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _logger.LogInfo("Starting import resolution");
        var importCount = 0;
        // Tracks which module each symbol name was imported from, used only by
        // from-imports. Plain `import` statements register a ModuleSymbol whose
        // name is the module itself, so name collisions are module-vs-module (handled
        // by SymbolTable.TryDefine). From-imports pull individual symbols into the
        // current scope, where the same name can arrive from different modules —
        // importedSymbolSources detects that conflict and emits a diagnostic.
        var importedSymbolSources = new Dictionary<string, string>();

        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (statement is ImportStatement import)
            {
                importCount++;
                var modules = ResolveImport(import, currentDir);

                // Register module symbols and their exports
                for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                {
                    var importAlias = import.Names[i];
                    var moduleInfo = modules[i];

                    // Skip null entries (should not happen anymore, but defensive check)
                    if (moduleInfo == null)
                        continue;

                    // Handle aliased imports (import x as y)
                    if (importAlias.AsName != null)
                    {
                        var aliasedModule = new ModuleSymbol
                        {
                            Name = importAlias.AsName,
                            Kind = SymbolKind.Module,
                            FilePath = moduleInfo.Path,
                            Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols),
                            FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                            IsErrorRecovery = moduleInfo.IsErrorRecovery,
                            IsNetModule = moduleInfo.IsNetModule,
                            CanonicalModuleName = moduleInfo.CanonicalModuleName,
                            NetNamespaceName = moduleInfo.NetNamespaceName,
                            Documentation = moduleInfo.Module?.DocString
                                ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name)
                        };
                        symbolTable.TryDefine(aliasedModule);
                    }
                    else
                    {
                        // Handle non-aliased imports by building nested module structure
                        var parts = importAlias.Name.Split('.');

                        var leafModule = new ModuleSymbol
                        {
                            Name = parts[^1],
                            Kind = SymbolKind.Module,
                            FilePath = moduleInfo.Path,
                            Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols),
                            FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                            IsErrorRecovery = moduleInfo.IsErrorRecovery,
                            IsNetModule = moduleInfo.IsNetModule,
                            CanonicalModuleName = moduleInfo.CanonicalModuleName,
                            NetNamespaceName = moduleInfo.NetNamespaceName,
                            Documentation = moduleInfo.Module?.DocString
                                ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name)
                        };

                        ModuleSymbol currentModule = leafModule;
                        for (int j = parts.Length - 2; j >= 0; j--)
                        {
                            var parentModule = new ModuleSymbol
                            {
                                Name = parts[j],
                                Kind = SymbolKind.Module,
                                FilePath = "",
                                Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } },
                                IsErrorRecovery = moduleInfo.IsErrorRecovery,
                                IsNetModule = moduleInfo.IsNetModule
                            };
                            currentModule = parentModule;
                        }

                        symbolTable.TryDefine(currentModule);
                    }
                }
            }
            else if (statement is FromImportStatement fromImport)
            {
                importCount++;
                _logger.LogDebug($"Processing from-import: from {fromImport.Module} import {string.Join(", ", fromImport.Names.Select(n => n.Name))}");
                var moduleInfo = ResolveFromImport(fromImport, currentDir);
                if (moduleInfo != null)
                {
                    _logger.LogDebug($"  Module resolved: {moduleInfo.Path}");
                    _logger.LogDebug($"  Exported symbols: [{string.Join(", ", moduleInfo.ExportedSymbols.Keys)}]");
                    var reExportedSymbols = _semanticBinding.GetReExportedSymbols(fromImport) ?? moduleInfo.ExportedSymbols;
                    var sourceModule = moduleInfo.CanonicalModuleName ?? fromImport.Module;

                    if (fromImport.ImportAll)
                    {
                        foreach (var (name, symbol) in reExportedSymbols)
                        {
                            // Only import public symbols (Python convention: no leading underscore)
                            if (name.StartsWith("_"))
                                continue;

                            _logger.LogDebug($"  Defining symbol (import *): {name}");
                            TryDefineFromImport(symbolTable, symbol, name, sourceModule,
                                importedSymbolSources, fromImport, importAlias: null);

                            // Propagate function overloads for wildcard imports
                            if (moduleInfo.FunctionOverloads.TryGetValue(name, out var wildOverloads) && wildOverloads.Count > 1)
                            {
                                symbolTable.DefineFunctionOverloads(name, wildOverloads);
                            }
                        }
                    }
                    else
                    {
                        foreach (var importAlias in fromImport.Names)
                        {
                            var lookupName = importAlias.Name;
                            var registerName = importAlias.AsName ?? importAlias.Name;
                            if (reExportedSymbols.TryGetValue(lookupName, out var symbol))
                            {
                                _logger.LogDebug($"  Defining imported symbol: {lookupName} as {registerName} ({symbol.Kind})");
                                if (importAlias.AsName != null)
                                {
                                    symbol = CloneSymbolWithName(symbol, registerName);
                                }
                                TryDefineFromImport(symbolTable, symbol, registerName, sourceModule,
                                    importedSymbolSources, fromImport, importAlias);

                                // Propagate function overloads for named imports
                                if (moduleInfo.FunctionOverloads.TryGetValue(lookupName, out var overloads) && overloads.Count > 1)
                                {
                                    symbolTable.DefineFunctionOverloads(registerName, overloads);
                                }
                            }
                            else if (registerName != lookupName && reExportedSymbols.TryGetValue(registerName, out symbol))
                            {
                                // Fallback: error recovery modules may register symbols under alias name
                                _logger.LogDebug($"  Defining imported symbol (alias fallback): {registerName} ({symbol.Kind})");
                                TryDefineFromImport(symbolTable, symbol, registerName, sourceModule,
                                    importedSymbolSources, fromImport, importAlias);

                                // Propagate function overloads for alias fallback path
                                if (moduleInfo.FunctionOverloads.TryGetValue(registerName, out var fallbackOverloads) && fallbackOverloads.Count > 1)
                                {
                                    symbolTable.DefineFunctionOverloads(registerName, fallbackOverloads);
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Symbol '{lookupName}' not found in module exports",
                                    fromImport.LineStart, fromImport.ColumnStart);
                            }
                        }
                    }
                }
            }
        }

        _logger.LogInfo($"Completed import resolution ({importCount} imports processed)");
    }

    /// <summary>
    /// Resolve an import statement
    /// </summary>
    public List<ModuleInfo?> ResolveImport(ImportStatement importStmt, string? searchPath = null,
        string? currentModulePath = null, CancellationToken cancellationToken = default)
    {
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _cancellationToken = cancellationToken;
        _logger.LogDebug($"Resolving import: {string.Join(", ", importStmt.Names.Select(n => n.Name))}");

        var result = new List<ModuleInfo?>();

        foreach (var importAlias in importStmt.Names)
        {
            // First, try to resolve as .NET assembly module through ModuleRegistry
            var moduleInfo = TryResolveNetModule(importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);

            // Try synthetic modules (e.g., asyncio)
            moduleInfo ??= TryResolveSyntheticModule(importAlias.Name);

            // Track .NET module names for codegen to emit correct using directives
            if (moduleInfo is { IsNetModule: true })
                _semanticBinding.MarkAsNetModule(importAlias.Name);

            // If not found in .NET assemblies, try .spy file
            if (moduleInfo == null)
            {
                var modulePath = ResolveModulePath(importAlias.Name, searchPath);
                if (modulePath == null)
                {
                    // Mark the module name as a root cause to suppress cascading errors
                    // at the diagnostic level (complements symbol-level IsErrorRecovery)
                    _diagnostics.AddRootCauseError(importAlias.Name,
                        $"Cannot find module '{importAlias.Name}'" + (_currentModulePath != null ? $" (in {Path.GetFileName(_currentModulePath)})" : ""),
                        importAlias.LineStart, importAlias.ColumnStart, _currentModulePath,
                        DiagnosticCodes.Semantic.ModuleNotFound, CompilerPhase.ImportResolution);

                    // Create error recovery module to prevent cascading "undefined identifier" errors
                    // The module symbol will be registered in ResolveAllImports to suppress downstream errors
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);
                    result.Add(new ModuleInfo
                    {
                        Path = $"<error-recovery:{importAlias.Name}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsErrorRecovery = true
                    });
                    continue;
                }

                // Track the dependency (current module depends on imported module)
                // Note: .NET modules are not tracked in the file dependency graph
                if (_dependencyRecorder != null && _currentModulePath != null)
                {
                    _dependencyRecorder.AddDependency(_currentModulePath, modulePath);
                }

                moduleInfo = LoadModule(modulePath, importAlias.LineStart, importAlias.ColumnStart);
            }

            // Always add to maintain positional alignment with importStmt.Names
            result.Add(moduleInfo);
        }

        return result;
    }

    /// <summary>
    /// Resolve a from-import statement
    /// </summary>
    public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null,
        string? currentModulePath = null, CancellationToken cancellationToken = default)
    {
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _cancellationToken = cancellationToken;
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.AsName != null ? $"{n.Name} as {n.AsName}" : n.Name));
        _logger.LogDebug($"[ImportResolver] Resolving from-import: from {fromImport.Module} import {importedNames}");
        if (_currentModulePath != null)
        {
            _logger.LogDebug($"[ImportResolver]   Current module: {Path.GetFileName(_currentModulePath)}");
        }

        // Helpful error for unsupported Python constructs: intercept before module resolution
        // so the error fires even when the module has no remaining exported functions.
        if (fromImport.Module == "collections" && !fromImport.ImportAll)
        {
            foreach (var alias in fromImport.Names)
            {
                if (alias.Name == "namedtuple")
                {
                    AddError(
                        "collections.namedtuple is not supported in Sharpy. " +
                        "Use 'type Point = tuple[x: float, y: float]' for named tuples, " +
                        "or '@dataclass class Point: x: float; y: float' for data classes.",
                        alias.LineStart, alias.ColumnStart,
                        code: DiagnosticCodes.Validation.NamedtupleNotSupported,
                        span: alias.Span ?? fromImport.Span);

                    // Create error recovery module to suppress cascading errors
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);
                    foreach (var importAlias in fromImport.Names)
                    {
                        var targetName = importAlias.AsName ?? importAlias.Name;
                        errorRecoveryModule.Exports[targetName] = CreateErrorRecoverySymbol(
                            targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart);
                        _diagnostics.MarkAsRootCause(targetName);
                    }
                    return new ModuleInfo
                    {
                        Path = $"<error-recovery:{fromImport.Module}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsNetModule = false
                    };
                }
            }
        }

        // First, try to resolve as .NET assembly module
        var moduleInfo = TryResolveNetModule(fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

        // Try synthetic modules (e.g., asyncio)
        moduleInfo ??= TryResolveSyntheticModule(fromImport.Module);

        // Track .NET module names for codegen to emit correct using directives
        if (moduleInfo is { IsNetModule: true })
            _semanticBinding.MarkAsNetModule(fromImport.Module);

        // If not found in .NET assemblies or synthetic modules, try .spy file
        if (moduleInfo == null)
        {
            var resolution = ResolveModuleWithResult(fromImport.Module, searchPath);
            if (resolution == null)
            {
                _logger.LogDebug($"[ImportResolver]   Module '{fromImport.Module}' not found");

                // Mark the module name as a root cause to suppress cascading errors
                _diagnostics.AddRootCauseError(fromImport.Module,
                    $"Cannot find module '{fromImport.Module}'" + (_currentModulePath != null ? $" (in {Path.GetFileName(_currentModulePath)})" : ""),
                    fromImport.LineStart, fromImport.ColumnStart, _currentModulePath,
                    DiagnosticCodes.Semantic.ModuleNotFound, CompilerPhase.ImportResolution);

                // Create error recovery module with placeholder symbols for each imported name
                // This prevents cascading "undefined identifier" errors in TypeChecker
                if (!fromImport.ImportAll && fromImport.Names.Length > 0)
                {
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

                    foreach (var importAlias in fromImport.Names)
                    {
                        // The symbol name is the target (alias if present, otherwise original)
                        // Store by TARGET name (alias if present) since that's how ResolveAllImports looks up symbols
                        var targetName = importAlias.AsName ?? importAlias.Name;
                        var errorSymbol = CreateErrorRecoverySymbol(
                            targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart);
                        errorRecoveryModule.Exports[targetName] = errorSymbol;
                        _logger.LogDebug($"[ImportResolver]   Created error recovery symbol: {targetName}");

                        // Also mark each imported symbol name as a root cause
                        // This allows suppression of "undefined identifier" errors even if
                        // symbol-level error recovery doesn't catch them
                        _diagnostics.MarkAsRootCause(targetName);
                    }

                    // Return the error recovery module so symbols get registered
                    return new ModuleInfo
                    {
                        Path = $"<error-recovery:{fromImport.Module}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsErrorRecovery = true
                    };
                }

                return null;
            }

            _logger.LogDebug($"[ImportResolver]   Resolved to: {resolution.FullPath}");
            _logger.LogDebug($"[ImportResolver]   Canonical name: {resolution.CanonicalModuleName ?? resolution.ModuleName}");

            // Store the resolved module path for code generation
            // For relative imports like ".helpers", this gives the canonical name like "mypackage.helpers"
            var resolvedPath = resolution.CanonicalModuleName ?? resolution.ModuleName;
            _semanticBinding.SetResolvedModulePath(fromImport, resolvedPath);

            // Track the dependency (current module depends on imported module)
            // Note: .NET modules are not tracked in the file dependency graph
            if (_dependencyRecorder != null && _currentModulePath != null)
            {
                _dependencyRecorder.AddDependency(_currentModulePath, resolution.FullPath);
            }

            moduleInfo = LoadModule(resolution.FullPath, fromImport.LineStart, fromImport.ColumnStart);
        }
        else
        {
            _logger.LogDebug($"[ImportResolver]   Resolved as .NET module");
        }

        // Validate imported names and populate re-export information for code generation
        if (moduleInfo != null)
        {
            _logger.LogDebug($"[ImportResolver]   Module loaded, exported symbols: {string.Join(", ", moduleInfo.ExportedSymbols.Keys)}");

            // Initialize the re-exported symbols dictionary for code generation
            var reExportedSymbols = new Dictionary<string, Symbol>();

            if (fromImport.ImportAll)
            {
                // import * - only imports public symbols (no leading underscore)
                // This is handled during symbol table population, not here
                // We just validate the module exists

                // Populate re-export symbols for code generation
                foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                {
                    if (!name.StartsWith("_"))
                    {
                        var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
                        reExportedSymbols[name] = reExportSymbol;
                        _logger.LogDebug($"[ImportResolver]     Re-exporting (wildcard): {name} ({symbol.Kind})");
                    }
                }
            }
            else
            {
                // Direct imports - validate each name exists and is importable
                foreach (var importAlias in fromImport.Names)
                {
                    var symbolName = importAlias.Name;
                    var targetName = importAlias.AsName ?? importAlias.Name;

                    // For .NET modules, try PascalCase conversion if the exact name isn't found
                    // (e.g., from system import console -> System.Console)
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName) && moduleInfo.IsNetModule)
                    {
                        var pascalName = NameMangler.ToPascalCase(symbolName);
                        if (moduleInfo.ExportedSymbols.ContainsKey(pascalName))
                            symbolName = pascalName;
                    }

                    // Check if symbol exists in the module's exported symbols
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName))
                    {
                        _logger.LogDebug($"[ImportResolver]     Symbol '{symbolName}' NOT FOUND in module exports");
                        var importMessage = $"Module '{fromImport.Module}' has no exported symbol '{symbolName}'";
                        var importSuggestion = EditDistance.FindClosestMatch(symbolName, moduleInfo.ExportedSymbols.Keys);
                        if (importSuggestion != null)
                            importMessage += $". Did you mean '{importSuggestion}'?";
                        AddError(importMessage,
                            importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.ImportError,
                            span: importAlias.Span ?? fromImport.Span);
                        continue;
                    }

                    // Check visibility rules for direct imports
                    if (!IsDirectlyImportable(symbolName))
                    {
                        AddError($"Cannot import private symbol '{symbolName}' from module '{fromImport.Module}'",
                            importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                            span: importAlias.Span ?? fromImport.Span);
                    }

                    // Populate re-export symbols for code generation
                    if (moduleInfo.ExportedSymbols.TryGetValue(symbolName, out var symbol))
                    {
                        var reExportSymbol = CreateReExportSymbol(symbol, fromImport, targetName);
                        reExportedSymbols[targetName] = reExportSymbol;

                        // Log detailed information about re-exported symbols for debugging transitive imports
                        if (symbol is TypeSymbol typeSymbol)
                        {
                            _logger.LogDebug($"[ImportResolver]     Importing type: {symbolName} -> {targetName}, DefiningModule: {typeSymbol.DefiningModule ?? "null"}, IsReExport: {typeSymbol.IsReExport}");
                        }
                        else
                        {
                            _logger.LogDebug($"[ImportResolver]     Importing: {symbolName} -> {targetName} ({symbol.Kind})");
                        }
                    }
                }
            }

            // Store re-exported symbols
            if (reExportedSymbols.Count > 0)
            {
                _logger.LogDebug($"[ImportResolver]   Storing {reExportedSymbols.Count} re-exported symbols");
                _semanticBinding.SetReExportedSymbols(fromImport, reExportedSymbols);
            }
        }

        return moduleInfo;
    }

    /// <summary>
    /// Load and parse a module (delegates to ModuleLoader).
    /// </summary>
    private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
    {
        var previousModulePath = _currentModulePath;
        UpdateCurrentModule(modulePath);

        try
        {
            var moduleInfo = _moduleLoader.LoadModule(modulePath, lineStart, columnStart,
                resolveModuleImports: (module, loadedModuleInfo, searchPath) =>
                {
                    // Extract re-exported symbols from from-imports BEFORE resolving imports.
                    // This ensures ExportedSymbols is populated for transitive resolution.
                    foreach (var statement in module.Body)
                    {
                        if (statement is FromImportStatement fromImport)
                        {
                            ExtractReExportedSymbols(fromImport, loadedModuleInfo);
                        }
                    }

                    // Resolve imports to detect transitive circular dependencies
                    ResolveModuleImports(module, searchPath);
                },
                cancellationToken: _cancellationToken);

            // Merge any diagnostics from the module loader
            _diagnostics.Merge(_moduleLoader.Diagnostics);

            return moduleInfo;
        }
        finally
        {
            if (previousModulePath != null)
            {
                UpdateCurrentModule(previousModulePath);
            }
            else
            {
                _currentModulePath = null;
                _moduleLoader.CurrentModulePath = null;
            }
        }
    }

    /// <summary>
    /// Resolve all imports within a module to detect transitive circular dependencies
    /// </summary>
    private void ResolveModuleImports(Module module, string? searchPath)
    {
        foreach (var statement in module.Body)
        {
            switch (statement)
            {
                case ImportStatement import:
                    ResolveImport(import, searchPath);
                    break;
                case FromImportStatement fromImport:
                    ResolveFromImport(fromImport, searchPath);
                    break;
            }
        }
    }

    /// <summary>
    /// Extract re-exported symbols from a from-import statement.
    /// When a module does "from .submodule import func", func becomes an export of that module.
    /// </summary>
    private void ExtractReExportedSymbols(FromImportStatement fromImport, ModuleInfo moduleInfo)
    {
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.Name));
        _logger.LogDebug($"[ImportResolver] ExtractReExportedSymbols: from {fromImport.Module} import {importedNames}");
        _logger.LogDebug($"[ImportResolver]   In module: {Path.GetFileName(moduleInfo.Path)}");

        // Resolve the source module to get its exported symbols
        var sourceModulePath = ResolveModulePath(fromImport.Module, Path.GetDirectoryName(moduleInfo.Path));
        if (sourceModulePath == null)
        {
            _logger.LogDebug($"[ImportResolver]   Source module '{fromImport.Module}' not found during re-export extraction");
            return;
        }

        _logger.LogDebug($"[ImportResolver]   Source module path: {sourceModulePath}");

        // Load the source module to get its symbols
        var sourceModule = LoadModule(sourceModulePath, fromImport.LineStart, fromImport.ColumnStart);
        if (sourceModule == null)
        {
            _logger.LogDebug($"[ImportResolver]   Failed to load source module");
            return;
        }

        _logger.LogDebug($"[ImportResolver]   Source module exports: {string.Join(", ", sourceModule.ExportedSymbols.Keys)}");

        // Build re-exported symbols dictionary for code generation
        var reExportedSymbols = new Dictionary<string, Symbol>();

        if (fromImport.ImportAll)
        {
            foreach (var (name, symbol) in sourceModule.ExportedSymbols)
            {
                if (!name.StartsWith("_"))
                {
                    var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
                    moduleInfo.ExportedSymbols[name] = reExportSymbol;
                    reExportedSymbols[name] = reExportSymbol;
                    _logger.LogDebug($"[ImportResolver]     Re-exporting (wildcard): {name}");
                }
            }
        }
        else
        {
            foreach (var importAlias in fromImport.Names)
            {
                var sourceName = importAlias.Name;
                var targetName = importAlias.AsName ?? importAlias.Name;

                if (sourceModule.ExportedSymbols.TryGetValue(sourceName, out var symbol))
                {
                    var reExportSymbol = CreateReExportSymbol(symbol, fromImport, targetName);
                    moduleInfo.ExportedSymbols[targetName] = reExportSymbol;
                    reExportedSymbols[targetName] = reExportSymbol;

                    if (symbol is TypeSymbol typeSymbol)
                    {
                        _logger.LogDebug($"[ImportResolver]     Re-exporting type: {sourceName} -> {targetName}, Original DefiningModule: {typeSymbol.DefiningModule ?? "null"}");
                    }
                    else
                    {
                        _logger.LogDebug($"[ImportResolver]     Re-exporting: {sourceName} -> {targetName} ({symbol.Kind})");
                    }
                }
                else
                {
                    _logger.LogDebug($"[ImportResolver]     Symbol '{sourceName}' NOT FOUND in source module exports");
                }
            }
        }

        if (reExportedSymbols.Count > 0)
        {
            _logger.LogDebug($"[ImportResolver]   Added {reExportedSymbols.Count} re-exported symbols to {Path.GetFileName(moduleInfo.Path)}");
            _semanticBinding.SetReExportedSymbols(fromImport, reExportedSymbols);
        }
    }

    /// <summary>
    /// Create a symbol for a re-exported item
    /// </summary>
    private Symbol CreateReExportSymbol(Symbol originalSymbol, FromImportStatement fromImport, string? newName = null)
    {
        var effectiveName = newName ?? originalSymbol.Name;

        var result = originalSymbol switch
        {
            FunctionSymbol func => new FunctionSymbol
            {
                Name = effectiveName,
                Kind = func.Kind,
                Parameters = func.Parameters,
                ReturnType = func.ReturnType,
                AccessLevel = func.AccessLevel,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module
            },
            TypeSymbol type => CreateReExportedTypeSymbol(type, fromImport, effectiveName),
            VariableSymbol var => new VariableSymbol
            {
                Name = effectiveName,
                Kind = var.Kind,
                Type = var.Type,
                IsConstant = var.IsConstant,
                AccessLevel = var.AccessLevel,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module
            },
            _ => originalSymbol
        };

        return result;
    }

    /// <summary>
    /// Create a re-exported type symbol, properly tracking the DefiningModule through the re-export chain.
    /// </summary>
    private TypeSymbol CreateReExportedTypeSymbol(TypeSymbol originalType, FromImportStatement fromImport, string effectiveName)
    {
        var definingModule = originalType.DefiningModule ?? GetResolvedModulePath(fromImport) ?? fromImport.Module;

        _logger.LogDebug($"[ImportResolver] CreateReExportedTypeSymbol: {originalType.Name} -> {effectiveName}");
        _logger.LogDebug($"[ImportResolver]   Original DefiningModule: {originalType.DefiningModule ?? "null"}");
        _logger.LogDebug($"[ImportResolver]   Original IsReExport: {originalType.IsReExport}");
        _logger.LogDebug($"[ImportResolver]   New DefiningModule: {definingModule}");
        _logger.LogDebug($"[ImportResolver]   FromImport.Module: {fromImport.Module}");

        var reExported = new TypeSymbol
        {
            Name = effectiveName,
            Kind = originalType.Kind,
            TypeKind = originalType.TypeKind,
            AccessLevel = originalType.AccessLevel,
            IsAbstract = originalType.IsAbstract,
            TypeParameters = originalType.TypeParameters,
            Fields = originalType.Fields,
            Methods = originalType.Methods,
            MethodOverloads = originalType.MethodOverloads,
            Properties = originalType.Properties,
            OperatorMethods = originalType.OperatorMethods,
            ProtocolMethods = originalType.ProtocolMethods,
            Constructors = originalType.Constructors,
            BaseType = originalType.BaseType,
            Interfaces = originalType.Interfaces,
            ClrType = originalType.ClrType,
            UnresolvedBaseName = originalType.UnresolvedBaseName,
            UnresolvedInterfaceNames = originalType.UnresolvedInterfaceNames,
            DeclarationLine = fromImport.LineStart,
            DeclarationColumn = fromImport.ColumnStart,
            IsReExport = true,
            OriginalModule = fromImport.Module,
            DefiningModule = definingModule
        };

        // When the type is aliased (effectiveName != originalType.Name), store the original
        // name so code generation uses the correct C# type name (e.g., "Config" not "Cfg").
        if (!string.Equals(effectiveName, originalType.Name, StringComparison.Ordinal))
        {
            reExported.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(originalType.Name),
                OriginalName = effectiveName,
                ImportKind = ImportKind.FromImportWithAlias,
                OriginalImportName = originalType.Name
            };
        }

        return reExported;
    }

    /// <summary>
    /// Gets the resolved module path from SemanticBinding or the AST property.
    /// </summary>
    private string? GetResolvedModulePath(FromImportStatement fromImport)
    {
        return _semanticBinding.GetResolvedModulePath(fromImport)
            ?? fromImport.ResolvedModulePath;
    }

    /// <summary>
    /// Try to resolve a module from loaded .NET assemblies through ModuleRegistry,
    /// or from standard .NET namespaces (e.g., "system" -> "System").
    /// </summary>
    private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
    {
        if (_moduleRegistry == null)
            return null;

        // Check cache first
        var cacheKey = $".net:{moduleName}";
        var cached = _moduleLoader.GetCachedModule(cacheKey);
        if (cached != null)
            return cached;

        // Check if this is a .NET namespace (e.g., "system" -> "System")
        if (_moduleRegistry.IsNetNamespace(moduleName))
        {
            return ResolveNetNamespaceModule(moduleName, cacheKey);
        }

        // Check if this module is loaded in the registry (for module classes)
        if (!_moduleRegistry.IsModuleLoaded(moduleName))
            return null;

        _logger.LogDebug($"Resolving .NET module: {moduleName}");

        // Get functions, types, and fields from the module
        var functions = _moduleRegistry.GetModuleFunctions(moduleName);
        var types = _moduleRegistry.GetModuleTypes(moduleName);
        var fields = _moduleRegistry.GetModuleFields(moduleName);
        if (functions.Count == 0 && types.Count == 0 && fields.Count == 0)
        {
            _logger.LogWarning($".NET module '{moduleName}' has no exported functions, types, or fields", lineStart ?? 0, columnStart ?? 0);
            return null;
        }

        // Create ModuleInfo for the .NET module
        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!,
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true
        };

        foreach (var function in functions)
        {
            moduleInfo.ExportedSymbols[function.Name] = function;

            if (!moduleInfo.FunctionOverloads.TryGetValue(function.Name, out var overloadList))
            {
                overloadList = new List<FunctionSymbol>();
                moduleInfo.FunctionOverloads[function.Name] = overloadList;
            }
            overloadList.Add(function);
        }

        foreach (var type in types)
        {
            moduleInfo.ExportedSymbols[type.Name] = type;
        }

        foreach (var (fieldName, fieldType, isConst) in fields)
        {
            moduleInfo.ExportedSymbols[fieldName] = new VariableSymbol
            {
                Name = fieldName,
                Kind = SymbolKind.Variable,
                Type = fieldType,
                IsConstant = isConst,
                IsStatic = true,
                AccessLevel = AccessLevel.Public
            };
        }

        _moduleLoader.CacheModule(cacheKey, moduleInfo);

        _logger.LogInfo($"Loaded .NET module '{moduleName}' with {functions.Count} functions, {types.Count} types, and {fields.Count} fields");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a .NET namespace as a module (e.g., "system" -> types from System namespace).
    /// </summary>
    private ModuleInfo? ResolveNetNamespaceModule(string moduleName, string cacheKey)
    {
        _logger.LogDebug($"Resolving .NET namespace module: {moduleName}");

        var netNamespace = _moduleRegistry!.GetNetNamespace(moduleName);

        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!,
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true,
            NetNamespaceName = netNamespace
        };

        var types = _moduleRegistry!.GetNamespaceTypes(moduleName);
        foreach (var typeSymbol in types)
        {
            moduleInfo.ExportedSymbols[typeSymbol.Name] = typeSymbol;
        }

        if (_moduleRegistry.IsModuleLoaded(moduleName))
        {
            var functions = _moduleRegistry.GetModuleFunctions(moduleName);
            foreach (var function in functions)
            {
                moduleInfo.ExportedSymbols[function.Name] = function;

                if (!moduleInfo.FunctionOverloads.TryGetValue(function.Name, out var overloadList))
                {
                    overloadList = new List<FunctionSymbol>();
                    moduleInfo.FunctionOverloads[function.Name] = overloadList;
                }
                overloadList.Add(function);
            }
        }

        _moduleLoader.CacheModule(cacheKey, moduleInfo);

        _logger.LogInfo($"Loaded .NET namespace '{moduleName}' with {moduleInfo.ExportedSymbols.Count} exports");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a module name to a file path
    /// </summary>
    private string? ResolveModulePath(string moduleName, string? searchPath = null)
    {
        return ResolveModuleWithResult(moduleName, searchPath)?.FullPath;
    }

    /// <summary>
    /// Resolve a module name and return the full resolution result
    /// </summary>
    private ModuleResolutionResult? ResolveModuleWithResult(string moduleName, string? searchPath = null)
    {
        if (searchPath != null)
        {
            _moduleResolver.AddSearchPath(searchPath);
        }

        return _moduleResolver.Resolve(moduleName);
    }

    /// <summary>
    /// Try to resolve a synthetic (compiler-provided) module by name.
    /// Synthetic modules don't correspond to .spy files or .NET assemblies — they are
    /// built-in modules whose functions map to special codegen patterns.
    /// Currently supports: asyncio (gather → Task.WhenAll, sleep → Task.Delay).
    /// </summary>
    private ModuleInfo? TryResolveSyntheticModule(string moduleName)
    {
        if (moduleName != Shared.SyntheticModuleNames.Asyncio)
            return null;

        var cacheKey = $"synthetic:{moduleName}";
        var cached = _moduleLoader.GetCachedModule(cacheKey);
        if (cached != null)
            return cached;

        _logger.LogDebug($"Resolving synthetic module: {moduleName}");

        var exports = new Dictionary<string, Symbol>();

        // asyncio.gather(*tasks) -> Task.WhenAll(tasks)
        // Variadic, accepts Task arguments, returns Task (void result since WhenAll returns Task)
        exports["gather"] = new FunctionSymbol
        {
            Name = "gather",
            Kind = SymbolKind.Function,
            ReturnType = new TaskType { ResultType = null },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "tasks",
                    Type = new TaskType { ResultType = null },
                    IsVariadic = true
                }
            },
            AccessLevel = AccessLevel.Public,
            IsStatic = true
        };

        // asyncio.sleep(seconds) -> Task.Delay(TimeSpan.FromSeconds(seconds))
        // Accepts float (double), returns Task (void)
        exports["sleep"] = new FunctionSymbol
        {
            Name = "sleep",
            Kind = SymbolKind.Function,
            ReturnType = new TaskType { ResultType = null },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "seconds",
                    Type = SemanticType.Float
                }
            },
            AccessLevel = AccessLevel.Public,
            IsStatic = true
        };

        var moduleInfo = new ModuleInfo
        {
            Path = $"synthetic:{moduleName}",
            Module = null!,
            ExportedSymbols = exports,
            IsNetModule = true // Treat as non-file module for import resolution
        };

        _moduleLoader.CacheModule(cacheKey, moduleInfo);
        _logger.LogInfo($"Loaded synthetic module '{moduleName}' with {exports.Count} functions");

        return moduleInfo;
    }

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
    /// Checks whether a from-import symbol name was already imported from a different module.
    /// Returns the name of the existing source module if a duplicate is detected, or null otherwise.
    /// Same-module re-imports (idempotent) return null (not a conflict).
    /// Shared between <see cref="ImportResolver"/> (single-file) and ProjectCompiler (multi-file).
    /// </summary>
    internal static string? FindDuplicateFromImportSource(
        string registerName,
        string sourceModule,
        Dictionary<string, string> importedSources)
    {
        if (importedSources.TryGetValue(registerName, out var existingModule)
            && !string.Equals(existingModule, sourceModule, StringComparison.Ordinal))
        {
            return existingModule;
        }

        return null;
    }

    /// <summary>
    /// Attempts to define a from-imported symbol. If the symbol already exists and was
    /// imported from a different module, emits a DuplicateDefinition error.
    /// Same-module re-imports (idempotent) are silently skipped.
    /// </summary>
    private bool TryDefineFromImport(
        SymbolTable symbolTable,
        Symbol symbol,
        string registerName,
        string sourceModule,
        Dictionary<string, string> importedSources,
        FromImportStatement fromImport,
        ImportAlias? importAlias)
    {
        if (symbolTable.TryDefine(symbol))
        {
            importedSources[registerName] = sourceModule;
            return true;
        }

        // Duplicate — error only if from a different module
        var existingModule = FindDuplicateFromImportSource(registerName, sourceModule, importedSources);
        if (existingModule != null)
        {
            var line = importAlias?.LineStart ?? fromImport.LineStart;
            var column = importAlias?.ColumnStart ?? fromImport.ColumnStart;
            var span = importAlias?.Span ?? fromImport.Span;
            AddError($"'{registerName}' is already imported from '{existingModule}'",
                line, column,
                code: DiagnosticCodes.Semantic.DuplicateDefinition,
                span: span);
        }

        return false;
    }

    private void AddError(string message, int? line, int? column, string? code = null,
        Text.TextSpan? span = null)
    {
        var errorMessage = _currentModulePath != null
            ? $"{message} (in {Path.GetFileName(_currentModulePath)})"
            : message;
        _diagnostics.AddPhaseError(errorMessage, CompilerPhase.ImportResolution,
            span: span, line: line, column: column, filePath: _currentModulePath, code: code);
    }

    /// <summary>
    /// Clones a symbol with a new name, used for alias registration.
    /// </summary>
    private static Symbol CloneSymbolWithName(Symbol symbol, string newName)
    {
        return symbol switch
        {
            FunctionSymbol func => func with { Name = newName },
            TypeSymbol type => type with { Name = newName },
            VariableSymbol var => var with { Name = newName },
            ModuleSymbol mod => mod with { Name = newName },
            _ => symbol
        };
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
            DeclarationColumn = column
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
            DeclarationColumn = column
        };
    }
}
