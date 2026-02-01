using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves imports and loads symbols from imported modules (both .spy files and .NET assemblies).
/// Delegates module loading/caching/symbol-extraction to <see cref="ModuleLoader"/>.
/// </summary>
public class ImportResolver
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
    private DependencyGraphBuilder? _graphBuilder;
    private SemanticBinding _semanticBinding = new();

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null)
        : this(new ModuleLoader(logger), logger, moduleRegistry, moduleResolver)
    {
    }

    public ImportResolver(ModuleLoader moduleLoader, ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null)
    {
        _moduleLoader = moduleLoader;
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _moduleResolver = moduleResolver ?? new ModuleResolver(logger);
    }

    /// <summary>
    /// Sets the semantic binding for storing semantic data separate from AST nodes.
    /// When set, import resolution data will be stored in SemanticBinding instead of
    /// directly on AST nodes, enabling immutable AST.
    /// </summary>
    public void SetSemanticBinding(SemanticBinding binding)
    {
        _semanticBinding = binding;
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Set the dependency graph builder for tracking file dependencies.
    /// When set, the resolver will call AddDependency for each import.
    /// </summary>
    /// <param name="builder">The builder to use for tracking dependencies.</param>
    public void SetDependencyGraphBuilder(DependencyGraphBuilder builder)
    {
        _graphBuilder = builder;
    }

    /// <summary>
    /// Set the current module path for resolving relative imports
    /// </summary>
    public void SetCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
        _moduleLoader.CurrentModulePath = modulePath;
        _moduleResolver.SetCurrentModulePath(modulePath);
    }

    /// <summary>
    /// Resolve an import statement
    /// </summary>
    public List<ModuleInfo> ResolveImport(ImportStatement importStmt, string? searchPath = null)
    {
        _logger.LogDebug($"Resolving import: {string.Join(", ", importStmt.Names.Select(n => n.Name))}");

        var result = new List<ModuleInfo>();

        foreach (var importAlias in importStmt.Names)
        {
            // First, try to resolve as .NET assembly module through ModuleRegistry
            var moduleInfo = TryResolveNetModule(importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);

            // If not found in .NET assemblies, try .spy file
            if (moduleInfo == null)
            {
                var modulePath = ResolveModulePath(importAlias.Name, searchPath);
                if (modulePath == null)
                {
                    AddError($"Cannot find module '{importAlias.Name}'",
                        importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.ModuleNotFound);
                    continue;
                }

                // Track the dependency (current module depends on imported module)
                // Note: .NET modules are not tracked in the file dependency graph
                if (_graphBuilder != null && _currentModulePath != null)
                {
                    _graphBuilder.AddDependency(_currentModulePath, modulePath);
                }

                moduleInfo = LoadModule(modulePath, importAlias.LineStart, importAlias.ColumnStart);
            }

            if (moduleInfo != null)
            {
                result.Add(moduleInfo);
            }
        }

        return result;
    }

    /// <summary>
    /// Resolve a from-import statement
    /// </summary>
    public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null)
    {
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.AsName != null ? $"{n.Name} as {n.AsName}" : n.Name));
        _logger.LogDebug($"[ImportResolver] Resolving from-import: from {fromImport.Module} import {importedNames}");
        if (_currentModulePath != null)
        {
            _logger.LogDebug($"[ImportResolver]   Current module: {Path.GetFileName(_currentModulePath)}");
        }

        // First, try to resolve as .NET assembly module
        var moduleInfo = TryResolveNetModule(fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

        // If not found in .NET assemblies, try .spy file
        if (moduleInfo == null)
        {
            var resolution = ResolveModuleWithResult(fromImport.Module, searchPath);
            if (resolution == null)
            {
                _logger.LogDebug($"[ImportResolver]   Module '{fromImport.Module}' not found");
                AddError($"Cannot find module '{fromImport.Module}'",
                    fromImport.LineStart, fromImport.ColumnStart, code: DiagnosticCodes.Semantic.ModuleNotFound);
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
            if (_graphBuilder != null && _currentModulePath != null)
            {
                _graphBuilder.AddDependency(_currentModulePath, resolution.FullPath);
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

                    // Check if symbol exists in the module's exported symbols
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName))
                    {
                        _logger.LogDebug($"[ImportResolver]     Symbol '{symbolName}' NOT FOUND in module exports");
                        AddError($"Module '{fromImport.Module}' has no exported symbol '{symbolName}'",
                            importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.ImportError);
                        continue;
                    }

                    // Check visibility rules for direct imports
                    if (!IsDirectlyImportable(symbolName))
                    {
                        AddError($"Cannot import private symbol '{symbolName}' from module '{fromImport.Module}'",
                            importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.AccessViolation);
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
        _currentModulePath = modulePath;
        _moduleLoader.CurrentModulePath = modulePath;
        _moduleResolver.SetCurrentModulePath(modulePath);

        try
        {
            var moduleInfo = _moduleLoader.LoadModule(modulePath, lineStart, columnStart, ResolveModuleImports);

            // Handle from-import re-exports that need ImportResolver context
            if (moduleInfo != null && moduleInfo.Module != null)
            {
                foreach (var statement in moduleInfo.Module.Body)
                {
                    if (statement is FromImportStatement fromImport)
                    {
                        ExtractReExportedSymbols(fromImport, moduleInfo);
                    }
                }
            }

            // Merge any diagnostics from the module loader
            _diagnostics.Merge(_moduleLoader.Diagnostics);

            return moduleInfo;
        }
        finally
        {
            _currentModulePath = previousModulePath;
            _moduleLoader.CurrentModulePath = previousModulePath;
            if (previousModulePath != null)
            {
                _moduleResolver.SetCurrentModulePath(previousModulePath);
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

        return new TypeSymbol
        {
            Name = effectiveName,
            Kind = originalType.Kind,
            TypeKind = originalType.TypeKind,
            AccessLevel = originalType.AccessLevel,
            IsAbstract = originalType.IsAbstract,
            TypeParameters = originalType.TypeParameters,
            Fields = originalType.Fields,
            Methods = originalType.Methods,
            Properties = originalType.Properties,
            Constructors = originalType.Constructors,
            BaseType = originalType.BaseType,
            Interfaces = originalType.Interfaces,
            UnresolvedBaseName = originalType.UnresolvedBaseName,
            UnresolvedInterfaceNames = originalType.UnresolvedInterfaceNames,
            DeclarationLine = fromImport.LineStart,
            DeclarationColumn = fromImport.ColumnStart,
            IsReExport = true,
            OriginalModule = fromImport.Module,
            DefiningModule = definingModule
        };
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

        // Check if this module is loaded in the registry (for Exports classes)
        if (!_moduleRegistry.IsModuleLoaded(moduleName))
            return null;

        _logger.LogDebug($"Resolving .NET module: {moduleName}");

        // Get functions from the module
        var functions = _moduleRegistry.GetModuleFunctions(moduleName);
        if (functions.Count == 0)
        {
            _logger.LogWarning($".NET module '{moduleName}' has no exported functions", lineStart ?? 0, columnStart ?? 0);
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
        }

        _moduleLoader.CacheModule(cacheKey, moduleInfo);

        _logger.LogInfo($"Loaded .NET module '{moduleName}' with {functions.Count} functions");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a .NET namespace as a module (e.g., "system" -> types from System namespace).
    /// </summary>
    private ModuleInfo? ResolveNetNamespaceModule(string moduleName, string cacheKey)
    {
        _logger.LogDebug($"Resolving .NET namespace module: {moduleName}");

        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!,
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true
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

    private void AddError(string message, int? line, int? column, string? code = null)
    {
        var errorMessage = _currentModulePath != null
            ? $"{message} (in {Path.GetFileName(_currentModulePath)})"
            : message;
        _diagnostics.AddError(errorMessage, line, column, _currentModulePath, code, CompilerPhase.ImportResolution);
    }
}
