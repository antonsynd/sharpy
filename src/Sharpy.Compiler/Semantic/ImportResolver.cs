using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves imports and loads symbols from imported modules (both .spy files and .NET assemblies)
/// </summary>
public class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly HashSet<string> _loadedModules = new();
    private readonly HashSet<string> _loadingModules = new(); // For circular import detection
    private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
    private readonly ModuleRegistry? _moduleRegistry;

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Set the current module path for resolving relative imports
    /// </summary>
    public void SetCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
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

        // Validate that imported names exist in the module (unless it's import *)
        if (moduleInfo != null && !fromImport.ImportAll)
        {
            foreach (var importAlias in fromImport.Names)
            {
                if (!moduleInfo.ExportedSymbols.ContainsKey(importAlias.Name))
                {
                    AddError($"Module '{fromImport.Module}' has no exported symbol '{importAlias.Name}'",
                        importAlias.LineStart, importAlias.ColumnStart);
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

        // Check for circular imports
        if (_loadingModules.Contains(modulePath))
        {
            AddError($"Circular import detected for module '{modulePath}'", lineStart, columnStart);
            return null;
        }

        // Check if already loaded
        if (_loadedModules.Contains(modulePath))
            return _moduleCache.GetValueOrDefault(modulePath);

        _logger.LogInfo($"Loading module: {modulePath}");
        _loadingModules.Add(modulePath);

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
            _loadingModules.Remove(modulePath);
        }
    }

    /// <summary>
    /// Extract exported symbols from a statement
    /// </summary>
    private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                // Functions are exported
                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
                break;

            case ClassDef classDef:
                // Classes are exported
                var classSymbol = new TypeSymbol
                {
                    Name = classDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Class,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = classDef.LineStart,
                    DeclarationColumn = classDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                // Structs are exported
                var structSymbol = new TypeSymbol
                {
                    Name = structDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Struct,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = structDef.LineStart,
                    DeclarationColumn = structDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                // Interfaces are exported
                var interfaceSymbol = new TypeSymbol
                {
                    Name = interfaceDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Interface,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = interfaceDef.LineStart,
                    DeclarationColumn = interfaceDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[interfaceDef.Name] = interfaceSymbol;
                break;

            case EnumDef enumDef:
                // Enums are exported
                var enumSymbol = new TypeSymbol
                {
                    Name = enumDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Enum,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = enumDef.LineStart,
                    DeclarationColumn = enumDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[enumDef.Name] = enumSymbol;
                break;

            case VariableDeclaration varDecl when varDecl.IsConst:
                // Constants are exported
                var constSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    IsConstant = true,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                moduleInfo.ExportedSymbols[varDecl.Name] = constSymbol;
                break;

            default:
                // Other statements don't export symbols
                break;
        }
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
        // Convert module.submodule to module/submodule.spy
        var relativePath = moduleName.Replace('.', Path.DirectorySeparatorChar) + ".spy";
        var searchedPaths = new List<string>();

        // Try current directory first
        if (_currentModulePath != null)
        {
            var currentDir = Path.GetDirectoryName(_currentModulePath);
            if (currentDir != null)
            {
                var path = Path.Combine(currentDir, relativePath);
                searchedPaths.Add(path);
                if (File.Exists(path))
                    return Path.GetFullPath(path);

                // Try as package directory with __init__.spy
                var packageDir = Path.Combine(currentDir, moduleName.Replace('.', Path.DirectorySeparatorChar));
                var initPath = Path.Combine(packageDir, "__init__.spy");
                searchedPaths.Add(initPath);
                if (File.Exists(initPath))
                    return Path.GetFullPath(initPath);
            }
        }

        // Try search path
        if (searchPath != null)
        {
            var path = Path.Combine(searchPath, relativePath);
            searchedPaths.Add(path);
            if (File.Exists(path))
                return Path.GetFullPath(path);

            // Try as package directory with __init__.spy
            var packageDir = Path.Combine(searchPath, moduleName.Replace('.', Path.DirectorySeparatorChar));
            var initPath = Path.Combine(packageDir, "__init__.spy");
            searchedPaths.Add(initPath);
            if (File.Exists(initPath))
                return Path.GetFullPath(initPath);
        }

        // Try current working directory
        searchedPaths.Add(Path.GetFullPath(relativePath));
        if (File.Exists(relativePath))
            return Path.GetFullPath(relativePath);

        // Try as package directory with __init__.spy in current working directory
        var cwd = Directory.GetCurrentDirectory();
        var cwdPackageDir = Path.Combine(cwd, moduleName.Replace('.', Path.DirectorySeparatorChar));
        var cwdInitPath = Path.Combine(cwdPackageDir, "__init__.spy");
        searchedPaths.Add(cwdInitPath);
        if (File.Exists(cwdInitPath))
            return Path.GetFullPath(cwdInitPath);

        // Log where we looked
        _logger.LogDebug($"Module '{moduleName}' not found. Searched: {string.Join(", ", searchedPaths)}");

        return null;
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
