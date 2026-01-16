using System.Text;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Entry in the import chain for error reporting
/// </summary>
internal record ImportChainEntry(
    string ModulePath,
    int? LineStart,
    int? ColumnStart,
    string? ImportingModule
);

/// <summary>
/// Resolves imports and loads symbols from imported modules (both .spy files and .NET assemblies)
/// </summary>
public class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly HashSet<string> _loadedModules = new();
    private readonly Stack<ImportChainEntry> _importChain = new(); // For detailed circular import detection
    private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly ModuleResolver _moduleResolver;

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _moduleResolver = moduleResolver ?? new ModuleResolver(logger);
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Set the current module path for resolving relative imports
    /// </summary>
    public void SetCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
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
                        importAlias.LineStart, importAlias.ColumnStart);
                    continue;
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
        _logger.LogDebug($"Resolving from-import: from {fromImport.Module} import {string.Join(", ", fromImport.Names.Select(n => n.Name))}");

        // First, try to resolve as .NET assembly module
        var moduleInfo = TryResolveNetModule(fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

        // If not found in .NET assemblies, try .spy file
        if (moduleInfo == null)
        {
            var modulePath = ResolveModulePath(fromImport.Module, searchPath);
            if (modulePath == null)
            {
                AddError($"Cannot find module '{fromImport.Module}'",
                    fromImport.LineStart, fromImport.ColumnStart);
                return null;
            }

            moduleInfo = LoadModule(modulePath, fromImport.LineStart, fromImport.ColumnStart);
        }

        // Validate imported names
        if (moduleInfo != null)
        {
            if (fromImport.ImportAll)
            {
                // import * - only imports public symbols (no leading underscore)
                // This is handled during symbol table population, not here
                // We just validate the module exists
            }
            else
            {
                // Direct imports - validate each name exists and is importable
                foreach (var importAlias in fromImport.Names)
                {
                    var symbolName = importAlias.Name;

                    // Check if symbol exists in the module's exported symbols
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName))
                    {
                        AddError($"Module '{fromImport.Module}' has no exported symbol '{symbolName}'",
                            importAlias.LineStart, importAlias.ColumnStart);
                        continue;
                    }

                    // Check visibility rules for direct imports
                    if (!IsDirectlyImportable(symbolName))
                    {
                        AddError($"Cannot import private symbol '{symbolName}' from module '{fromImport.Module}'",
                            importAlias.LineStart, importAlias.ColumnStart);
                    }
                }
            }
        }

        return moduleInfo;
    }

    /// <summary>
    /// Load and parse a module
    /// </summary>
    private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
    {
        // Check cache first
        if (_moduleCache.TryGetValue(modulePath, out var cached))
            return cached;

        // Check for circular imports with detailed chain
        if (IsModuleInChain(modulePath))
        {
            var chainMessage = FormatCircularImportChain(modulePath);
            AddError(chainMessage, lineStart, columnStart);
            return null;
        }

        // Check if already loaded
        if (_loadedModules.Contains(modulePath))
            return _moduleCache.GetValueOrDefault(modulePath);

        _logger.LogInfo($"Loading module: {modulePath}");

        // Push to import chain before loading
        _importChain.Push(new ImportChainEntry(
            modulePath,
            lineStart,
            columnStart,
            _currentModulePath
        ));

        try
        {
            // Read the source file
            if (!File.Exists(modulePath))
            {
                AddError($"Module file not found: {modulePath}", lineStart, columnStart);
                return null;
            }

            var source = File.ReadAllText(modulePath);

            // Parse the module
            var lexer = new Lexer.Lexer(source, _logger);
            var tokens = lexer.TokenizeAll();
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();

            // Create module info
            var moduleInfo = new ModuleInfo
            {
                Path = modulePath,
                Module = module,
                ExportedSymbols = new Dictionary<string, Symbol>()
            };

            // Extract exported symbols (all top-level declarations)
            foreach (var statement in module.Body)
            {
                ExtractExportedSymbol(statement, moduleInfo);
            }

            // Recursively resolve imports within this module to detect transitive cycles
            var previousModulePath = _currentModulePath;
            _currentModulePath = modulePath;
            try
            {
                ResolveModuleImports(module, Path.GetDirectoryName(modulePath));
            }
            finally
            {
                _currentModulePath = previousModulePath;
            }

            _moduleCache[modulePath] = moduleInfo;
            _loadedModules.Add(modulePath);

            return moduleInfo;
        }
        catch (Exception ex)
        {
            AddError($"Error loading module '{modulePath}': {ex.Message}", lineStart, columnStart);
            return null;
        }
        finally
        {
            _importChain.Pop();
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
    /// Extract exported symbols from a statement.
    /// All top-level symbols are added to ExportedSymbols, but visibility is enforced during import.
    /// </summary>
    private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                // All functions are tracked (visibility checked at import time)
                var accessLevel = GetAccessLevel(functionDef.Name);
                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    AccessLevel = accessLevel,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
                break;

            case ClassDef classDef:
                // All classes are tracked
                var classAccessLevel = GetAccessLevel(classDef.Name);
                var classSymbol = new TypeSymbol
                {
                    Name = classDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Class,
                    AccessLevel = classAccessLevel,
                    DeclarationLine = classDef.LineStart,
                    DeclarationColumn = classDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                // All structs are tracked
                var structAccessLevel = GetAccessLevel(structDef.Name);
                var structSymbol = new TypeSymbol
                {
                    Name = structDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Struct,
                    AccessLevel = structAccessLevel,
                    DeclarationLine = structDef.LineStart,
                    DeclarationColumn = structDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                // All interfaces are tracked
                var interfaceAccessLevel = GetAccessLevel(interfaceDef.Name);
                var interfaceSymbol = new TypeSymbol
                {
                    Name = interfaceDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Interface,
                    AccessLevel = interfaceAccessLevel,
                    DeclarationLine = interfaceDef.LineStart,
                    DeclarationColumn = interfaceDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[interfaceDef.Name] = interfaceSymbol;
                break;

            case EnumDef enumDef:
                // All enums are tracked
                var enumAccessLevel = GetAccessLevel(enumDef.Name);
                var enumSymbol = new TypeSymbol
                {
                    Name = enumDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Enum,
                    AccessLevel = enumAccessLevel,
                    DeclarationLine = enumDef.LineStart,
                    DeclarationColumn = enumDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[enumDef.Name] = enumSymbol;
                break;

            case VariableDeclaration varDecl:
                // All module-level variables are tracked (constants and regular)
                var varAccessLevel = GetAccessLevel(varDecl.Name);
                var varSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    IsConstant = varDecl.IsConst,
                    AccessLevel = varAccessLevel,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                moduleInfo.ExportedSymbols[varDecl.Name] = varSymbol;
                break;

            default:
                // Other statements don't export symbols
                break;
        }
    }

    /// <summary>
    /// Determine access level based on naming convention
    /// </summary>
    private AccessLevel GetAccessLevel(string name)
    {
        if (name.StartsWith("__"))
            return AccessLevel.Private;
        if (name.StartsWith("_"))
            return AccessLevel.Protected;
        return AccessLevel.Public;
    }

    /// <summary>
    /// Try to resolve a module from loaded .NET assemblies through ModuleRegistry
    /// </summary>
    private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
    {
        if (_moduleRegistry == null)
            return null;

        // Check if this module is loaded in the registry
        if (!_moduleRegistry.IsModuleLoaded(moduleName))
            return null;

        // Check cache first
        var cacheKey = $".net:{moduleName}";
        if (_moduleCache.TryGetValue(cacheKey, out var cached))
            return cached;

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
            Module = null!, // No AST module exists for .NET assemblies; consumers must check IsNetModule before accessing Module
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true
        };

        // Add all functions as exported symbols
        foreach (var function in functions)
        {
            moduleInfo.ExportedSymbols[function.Name] = function;
        }

        _moduleCache[cacheKey] = moduleInfo;
        _loadedModules.Add(cacheKey);

        _logger.LogInfo($"Loaded .NET module '{moduleName}' with {functions.Count} functions");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a module name to a file path
    /// </summary>
    private string? ResolveModulePath(string moduleName, string? searchPath = null)
    {
        // Add the optional search path if provided
        if (searchPath != null)
        {
            _moduleResolver.AddSearchPath(searchPath);
        }

        // Use the ModuleResolver to find the module
        var result = _moduleResolver.Resolve(moduleName);
        return result?.FullPath;
    }

    /// <summary>
    /// Check if a symbol name can be directly imported (not double-underscore private)
    /// </summary>
    private bool IsDirectlyImportable(string symbolName)
    {
        // Private symbols (starting with __) cannot be directly imported
        return !symbolName.StartsWith("__");
    }

    /// <summary>
    /// Check if a symbol name is exported by 'import *' (public symbols only)
    /// </summary>
    private bool IsExportedByImportAll(string symbolName)
    {
        // Only public symbols (not starting with _) are exported by import *
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
    /// Check if a module is already in the current import chain
    /// </summary>
    private bool IsModuleInChain(string modulePath)
    {
        return _importChain.Any(e => e.ModulePath == modulePath);
    }

    /// <summary>
    /// Format a detailed circular import error message showing the full chain
    /// </summary>
    private string FormatCircularImportChain(string cycleStartModule)
    {
        var chain = new StringBuilder();
        chain.AppendLine("Circular import detected:");

        var entries = _importChain.Reverse().ToList();

        // Find where the cycle starts
        var cycleStartIndex = entries.FindIndex(e => e.ModulePath == cycleStartModule);

        // Show only the relevant part of the chain (from cycle start to current)
        for (int i = cycleStartIndex; i < entries.Count; i++)
        {
            var entry = entries[i];
            chain.AppendLine($"  -> {Path.GetFileName(entry.ModulePath)}");
        }

        // Show the closing of the cycle
        chain.AppendLine($"  -> {Path.GetFileName(cycleStartModule)} (cycle)");

        return chain.ToString().TrimEnd();
    }

    private void AddError(string message, int? line, int? column)
    {
        var errorMessage = _currentModulePath != null
            ? $"{message} (in {Path.GetFileName(_currentModulePath)})"
            : message;
        _errors.Add(new SemanticError(errorMessage, line, column));
    }
}

/// <summary>
/// Information about a loaded module
/// </summary>
/// <remarks>
/// When <see cref="IsNetModule"/> is true, the <see cref="Module"/> property will be null
/// because .NET assemblies don't have an AST representation. Always check <see cref="IsNetModule"/>
/// before accessing <see cref="Module"/> to avoid null reference errors.
/// </remarks>
public class ModuleInfo
{
    public string Path { get; init; } = string.Empty;
    public Module Module { get; init; } = null!;
    public Dictionary<string, Symbol> ExportedSymbols { get; init; } = new();
    public bool IsNetModule { get; init; } = false;
}
