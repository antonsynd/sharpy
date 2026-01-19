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
            var resolution = ResolveModuleWithResult(fromImport.Module, searchPath);
            if (resolution == null)
            {
                AddError($"Cannot find module '{fromImport.Module}'",
                    fromImport.LineStart, fromImport.ColumnStart);
                return null;
            }

            // Store the resolved module path for code generation
            // For relative imports like ".helpers", this gives the canonical name like "mypackage.helpers"
            fromImport.ResolvedModulePath = resolution.CanonicalModuleName ?? resolution.ModuleName;

            moduleInfo = LoadModule(resolution.FullPath, fromImport.LineStart, fromImport.ColumnStart);
        }

        // Validate imported names and populate re-export information for code generation
        if (moduleInfo != null)
        {
            // Initialize the re-exported symbols dictionary for code generation
            fromImport.ReExportedSymbols ??= new Dictionary<string, Symbol>();

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
                        fromImport.ReExportedSymbols[name] = reExportSymbol;
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

                    // Populate re-export symbols for code generation
                    if (moduleInfo.ExportedSymbols.TryGetValue(symbolName, out var symbol))
                    {
                        var reExportSymbol = CreateReExportSymbol(symbol, fromImport, targetName);
                        fromImport.ReExportedSymbols[targetName] = reExportSymbol;
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

            // Set current module path BEFORE extracting symbols (needed for relative imports in re-exports)
            var previousModulePath = _currentModulePath;
            _currentModulePath = modulePath;
            _moduleResolver.SetCurrentModulePath(modulePath);

            try
            {
                // Extract exported symbols (all top-level declarations)
                foreach (var statement in module.Body)
                {
                    ExtractExportedSymbol(statement, moduleInfo);
                }

                // Recursively resolve imports within this module to detect transitive cycles
                ResolveModuleImports(module, Path.GetDirectoryName(modulePath));
            }
            finally
            {
                _currentModulePath = previousModulePath;
                if (previousModulePath != null)
                {
                    _moduleResolver.SetCurrentModulePath(previousModulePath);
                }
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

                // Convert function parameters to parameter symbols
                var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    // Convert type annotation to semantic type for primitive types
                    Type = ConvertTypeAnnotationToSemanticType(p.Type),
                    HasDefault = p.DefaultValue != null,
                    DefaultValue = p.DefaultValue
                }).ToList();

                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    Parameters = parameters,
                    // Convert return type annotation to semantic type
                    ReturnType = ConvertTypeAnnotationToSemanticType(functionDef.ReturnType),
                    AccessLevel = accessLevel,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
                break;

            case ClassDef classDef:
                // Extract full class information including fields, methods, and type parameters
                var classSymbol = ExtractFullClassSymbol(classDef);
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                // Extract full struct information including fields, methods, and type parameters
                var structSymbol = ExtractFullStructSymbol(structDef);
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                // Extract full interface information including methods and type parameters
                var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef);
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
                    // Convert type annotation to semantic type for primitive types
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = varAccessLevel,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                moduleInfo.ExportedSymbols[varDecl.Name] = varSymbol;
                break;

            case FromImportStatement fromImport:
                // Re-export imported symbols from the module
                // This enables patterns like: from .submodule import func
                // which makes 'func' available as an export of this module
                ExtractReExportedSymbols(fromImport, moduleInfo);
                break;

            default:
                // Other statements don't export symbols
                break;
        }
    }

    /// <summary>
    /// Extract re-exported symbols from a from-import statement.
    /// When a module does "from .submodule import func", func becomes an export of that module.
    /// </summary>
    private void ExtractReExportedSymbols(FromImportStatement fromImport, ModuleInfo moduleInfo)
    {
        // Resolve the source module to get its exported symbols
        var sourceModulePath = ResolveModulePath(fromImport.Module, Path.GetDirectoryName(moduleInfo.Path));
        if (sourceModulePath == null)
        {
            // Module not found - error will be reported during full import resolution
            return;
        }

        // Load the source module to get its symbols
        var sourceModule = LoadModule(sourceModulePath, fromImport.LineStart, fromImport.ColumnStart);
        if (sourceModule == null)
        {
            return;
        }

        // Initialize the re-exported symbols dictionary for code generation
        fromImport.ReExportedSymbols ??= new Dictionary<string, Symbol>();

        if (fromImport.ImportAll)
        {
            // "from .module import *" - re-export all public symbols
            foreach (var (name, symbol) in sourceModule.ExportedSymbols)
            {
                if (!name.StartsWith("_"))
                {
                    // Create a re-export symbol that references the original
                    var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
                    moduleInfo.ExportedSymbols[name] = reExportSymbol;
                    fromImport.ReExportedSymbols[name] = reExportSymbol;
                }
            }
        }
        else
        {
            // "from .module import name1, name2" - re-export specific symbols
            foreach (var importAlias in fromImport.Names)
            {
                var sourceName = importAlias.Name;
                var targetName = importAlias.AsName ?? importAlias.Name;

                if (sourceModule.ExportedSymbols.TryGetValue(sourceName, out var symbol))
                {
                    // Create a re-export symbol, possibly with a different name (alias)
                    var reExportSymbol = CreateReExportSymbol(symbol, fromImport, targetName);
                    moduleInfo.ExportedSymbols[targetName] = reExportSymbol;
                    fromImport.ReExportedSymbols[targetName] = reExportSymbol;
                }
                // If symbol not found, error will be reported during full import resolution
            }
        }
    }

    /// <summary>
    /// Create a symbol for a re-exported item
    /// </summary>
    private Symbol CreateReExportSymbol(Symbol originalSymbol, FromImportStatement fromImport, string? newName = null)
    {
        // Clone the symbol with the new name if provided
        return originalSymbol switch
        {
            FunctionSymbol func => new FunctionSymbol
            {
                Name = newName ?? func.Name,
                Kind = func.Kind,
                Parameters = func.Parameters,
                ReturnType = func.ReturnType,
                AccessLevel = func.AccessLevel,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module
            },
            TypeSymbol type => new TypeSymbol
            {
                Name = newName ?? type.Name,
                Kind = type.Kind,
                TypeKind = type.TypeKind,
                AccessLevel = type.AccessLevel,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module
            },
            VariableSymbol var => new VariableSymbol
            {
                Name = newName ?? var.Name,
                Kind = var.Kind,
                Type = var.Type,  // Preserve the type from the original symbol
                IsConstant = var.IsConstant,
                AccessLevel = var.AccessLevel,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module
            },
            _ => originalSymbol // Fallback: use as-is
        };
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
    /// Convert a type annotation to a semantic type for simple/primitive types.
    /// This is used during import resolution to provide type information before
    /// full semantic analysis. Returns Unknown for complex types.
    /// </summary>
    private SemanticType ConvertTypeAnnotationToSemanticType(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return SemanticType.Unknown;

        // Handle nullable types
        var isNullable = typeAnnotation.IsNullable;

        // Map primitive type names
        var baseType = typeAnnotation.Name switch
        {
            "int" => SemanticType.Int,
            "long" => SemanticType.Long,
            "float" => SemanticType.Float,
            "double" => SemanticType.Double,
            "float32" => SemanticType.Float32,
            "bool" => SemanticType.Bool,
            "str" or "string" => SemanticType.Str,
            "void" or "None" => SemanticType.Void,
            "object" => SemanticType.Object,
            _ => SemanticType.Unknown // Complex types will be resolved during semantic analysis
        };

        // Wrap in nullable if needed
        if (isNullable && baseType != SemanticType.Unknown && baseType != SemanticType.Void)
        {
            return new NullableType { UnderlyingType = baseType };
        }

        return baseType;
    }

    /// <summary>
    /// Extract full type information from a class definition including
    /// fields, methods, constructors, and type parameters.
    /// Note: Base class resolution happens later in NameResolver.ResolveInheritance()
    /// after all types are registered in the symbol table.
    /// </summary>
    private TypeSymbol ExtractFullClassSymbol(ClassDef classDef)
    {
        var accessLevel = GetAccessLevel(classDef.Name);
        bool isAbstract = classDef.Decorators.Any(d => d.Name == "abstract");

        var classSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = accessLevel,
            IsAbstract = isAbstract,
            TypeParameters = classDef.TypeParameters,
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart
        };

        // Extract fields
        foreach (var stmt in classDef.Body)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                var fieldSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = GetAccessLevel(varDecl.Name),
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                classSymbol.Fields.Add(fieldSymbol);
            }
        }

        // Extract methods
        foreach (var stmt in classDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                classSymbol.Methods.Add(methodSymbol);

                // Track constructors separately
                if (method.Name == "__init__")
                {
                    classSymbol.Constructors.Add(methodSymbol);
                }
            }
        }

        return classSymbol;
    }

    /// <summary>
    /// Extract full type information from a struct definition including
    /// fields, methods, and type parameters.
    /// </summary>
    private TypeSymbol ExtractFullStructSymbol(StructDef structDef)
    {
        var accessLevel = GetAccessLevel(structDef.Name);

        var structSymbol = new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = accessLevel,
            TypeParameters = structDef.TypeParameters,
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart
        };

        // Extract fields
        foreach (var stmt in structDef.Body)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                var fieldSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = GetAccessLevel(varDecl.Name),
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                structSymbol.Fields.Add(fieldSymbol);
            }
        }

        // Extract methods
        foreach (var stmt in structDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                structSymbol.Methods.Add(methodSymbol);

                // Track constructors separately
                if (method.Name == "__init__")
                {
                    structSymbol.Constructors.Add(methodSymbol);
                }
            }
        }

        return structSymbol;
    }

    /// <summary>
    /// Extract full type information from an interface definition including
    /// methods and type parameters.
    /// </summary>
    private TypeSymbol ExtractFullInterfaceSymbol(InterfaceDef interfaceDef)
    {
        var accessLevel = GetAccessLevel(interfaceDef.Name);

        var interfaceSymbol = new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = accessLevel,
            TypeParameters = interfaceDef.TypeParameters,
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart
        };

        // Extract methods (interface methods are always abstract)
        foreach (var stmt in interfaceDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                // Interface methods are implicitly abstract unless they have an implementation
                if (!methodSymbol.IsAbstract)
                {
                    // Check if the method has an ellipsis body (abstract method signature)
                    bool hasEllipsisBody = method.Body.Count == 1
                        && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
                    if (hasEllipsisBody)
                    {
                        methodSymbol = methodSymbol with { IsAbstract = true };
                    }
                }
                interfaceSymbol.Methods.Add(methodSymbol);
            }
        }

        return interfaceSymbol;
    }

    /// <summary>
    /// Extract method symbol with parameter and return type information.
    /// </summary>
    private FunctionSymbol ExtractMethodSymbol(FunctionDef method)
    {
        var accessLevel = GetAccessLevel(method.Name);

        bool hasSelfParameter = method.Parameters.Any(p =>
            string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));
        bool hasStaticDecorator = method.Decorators.Any(d =>
            d.Name == "static" || d.Name == "staticmethod");
        bool isStatic = hasStaticDecorator || !hasSelfParameter;

        var parameters = method.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            Type = ConvertTypeAnnotationToSemanticType(p.Type),
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic
        }).ToList();

        return new FunctionSymbol
        {
            Name = method.Name,
            Kind = SymbolKind.Function,
            Parameters = parameters,
            ReturnType = ConvertTypeAnnotationToSemanticType(method.ReturnType),
            IsStatic = isStatic,
            IsAbstract = method.Decorators.Any(d => d.Name == "abstract"),
            IsVirtual = method.Decorators.Any(d => d.Name == "virtual"),
            IsOverride = method.Decorators.Any(d => d.Name == "override"),
            TypeParameters = method.TypeParameters,
            AccessLevel = accessLevel,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart
        };
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
        return ResolveModuleWithResult(moduleName, searchPath)?.FullPath;
    }

    /// <summary>
    /// Resolve a module name and return the full resolution result
    /// </summary>
    private ModuleResolutionResult? ResolveModuleWithResult(string moduleName, string? searchPath = null)
    {
        // Add the optional search path if provided
        if (searchPath != null)
        {
            _moduleResolver.AddSearchPath(searchPath);
        }

        // Use the ModuleResolver to find the module
        return _moduleResolver.Resolve(moduleName);
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
