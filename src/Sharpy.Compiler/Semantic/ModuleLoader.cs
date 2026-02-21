using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Loads, parses, and caches .spy modules. Extracts exported symbols from parsed AST.
/// Separated from ImportResolver to isolate module loading/caching/symbol-extraction concerns.
/// </summary>
internal class ModuleLoader
{
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly HashSet<string> _loadedModules = new();
    private readonly Stack<ImportChainEntry> _importChain = new();
    private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
    private CancellationToken _cancellationToken;

    /// <summary>
    /// All loaded .spy modules (excludes .NET modules).
    /// Key is the full file path, value is the ModuleInfo.
    /// </summary>
    public IReadOnlyDictionary<string, ModuleInfo> LoadedSpyModules =>
        _moduleCache
            .Where(kvp => !kvp.Value.IsNetModule && kvp.Value.Module != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// The current module path (set externally by ImportResolver).
    /// </summary>
    public string? CurrentModulePath { get; set; }

    public ModuleLoader(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Load and parse a module. Returns cached result if already loaded.
    /// </summary>
    /// <param name="modulePath">Full path to the .spy file.</param>
    /// <param name="lineStart">Source line of the import statement (for diagnostics).</param>
    /// <param name="columnStart">Source column of the import statement (for diagnostics).</param>
    /// <param name="resolveModuleImports">Callback to resolve imports within the loaded module.
    /// Parameters: (Module, ModuleInfo, searchPath). The ModuleInfo is provided so callers
    /// can add re-exported symbols before the module is cached.</param>
    public ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart,
        Action<Module, ModuleInfo, string?>? resolveModuleImports = null,
        CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        // Check cache first
        if (_moduleCache.TryGetValue(modulePath, out var cached))
        {
            _logger.LogDebug($"[ModuleLoader] LoadModule: {Path.GetFileName(modulePath)} (from cache)");
            return cached;
        }

        // Check for circular imports with detailed chain
        if (IsModuleInChain(modulePath))
        {
            var chainMessage = FormatCircularImportChain(modulePath);
            _logger.LogDebug($"[ModuleLoader] Circular import detected: {Path.GetFileName(modulePath)}");
            AddError(chainMessage, lineStart, columnStart, code: DiagnosticCodes.Semantic.CircularImport);
            return null;
        }

        // Check if already loaded
        if (_loadedModules.Contains(modulePath))
        {
            _logger.LogDebug($"[ModuleLoader] LoadModule: {Path.GetFileName(modulePath)} (already loaded)");
            return _moduleCache.GetValueOrDefault(modulePath);
        }

        _logger.LogInfo($"Loading module: {modulePath}");
        _logger.LogDebug($"[ModuleLoader] LoadModule: {Path.GetFileName(modulePath)} (parsing)");

        // Compute the canonical module name for this file (used for DefiningModule tracking)
        var canonicalModuleName = ComputeCanonicalModuleName(modulePath);
        _logger.LogDebug($"[ModuleLoader]   Canonical module name: {canonicalModuleName}");

        // Push to import chain before loading
        _importChain.Push(new ImportChainEntry(
            modulePath,
            lineStart,
            columnStart,
            CurrentModulePath
        ));

        try
        {
            // Read the source file
            if (!File.Exists(modulePath))
            {
                AddError($"Module file not found: {modulePath}", lineStart, columnStart, code: DiagnosticCodes.Semantic.ModuleNotFound);
                return null;
            }

            var source = File.ReadAllText(modulePath);

            _cancellationToken.ThrowIfCancellationRequested();

            // Parse the module
            var sourceText = new Text.SourceText(source, modulePath);
            var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: _cancellationToken);
            var tokens = lexer.TokenizeAll();
            var parser = new Parser.Parser(tokens, _logger, cancellationToken: _cancellationToken);
            var module = parser.ParseModule();

            // Create module info
            var moduleInfo = new ModuleInfo
            {
                Path = modulePath,
                Module = module,
                ExportedSymbols = new Dictionary<string, Symbol>(),
                CanonicalModuleName = canonicalModuleName
            };

            // Extract exported symbols (all top-level declarations)
            foreach (var statement in module.Body)
            {
                ExtractExportedSymbol(statement, moduleInfo);
            }

            // Recursively resolve imports within this module to detect transitive cycles
            resolveModuleImports?.Invoke(module, moduleInfo, Path.GetDirectoryName(modulePath));

            _moduleCache[modulePath] = moduleInfo;
            _loadedModules.Add(modulePath);

            // Log final exported symbols
            _logger.LogDebug($"[ModuleLoader] Module {Path.GetFileName(modulePath)} loaded with {moduleInfo.ExportedSymbols.Count} exports:");
            foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
            {
                if (symbol is TypeSymbol typeSymbol)
                {
                    _logger.LogDebug($"[ModuleLoader]   - {name} ({symbol.Kind}:{typeSymbol.TypeKind}), DefiningModule: {typeSymbol.DefiningModule ?? "null"}, IsReExport: {typeSymbol.IsReExport}");
                }
                else
                {
                    _logger.LogDebug($"[ModuleLoader]   - {name} ({symbol.Kind})");
                }
            }

            return moduleInfo;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AddError($"Error loading module '{modulePath}': {ex.Message}", lineStart, columnStart, code: DiagnosticCodes.Semantic.ModuleLoadError);
            return null;
        }
        finally
        {
            _importChain.Pop();
        }
    }

    /// <summary>
    /// Get a cached module by path. Returns null if not cached.
    /// </summary>
    public ModuleInfo? GetCachedModule(string modulePath)
    {
        return _moduleCache.GetValueOrDefault(modulePath);
    }

    /// <summary>
    /// Cache a module (used by ImportResolver for .NET modules).
    /// </summary>
    public void CacheModule(string key, ModuleInfo moduleInfo)
    {
        _moduleCache[key] = moduleInfo;
        _loadedModules.Add(key);
    }

    /// <summary>
    /// Search all loaded modules in the cache for a TypeSymbol with the given name.
    /// Used to discover transitive base types that were parsed but not explicitly imported.
    /// </summary>
    public TypeSymbol? FindTypeInLoadedModules(string typeName)
    {
        foreach (var (_, moduleInfo) in _moduleCache)
        {
            if (moduleInfo.ExportedSymbols.TryGetValue(typeName, out var symbol) && symbol is TypeSymbol typeSymbol)
            {
                return typeSymbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Extract exported symbols from a statement.
    /// All top-level symbols are added to ExportedSymbols, but visibility is enforced during import.
    /// </summary>
    internal void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                var accessLevel = GetAccessLevel(functionDef.Name);
                var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    Type = ConvertTypeAnnotationToSemanticType(p.Type),
                    HasDefault = p.DefaultValue != null,
                    DefaultValue = p.DefaultValue,
                    IsVariadic = p.IsVariadic
                }).ToList();

                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    Parameters = parameters,
                    ReturnType = functionDef.ReturnType != null
                        ? ConvertTypeAnnotationToSemanticType(functionDef.ReturnType)
                        : SemanticType.Void,
                    AccessLevel = accessLevel,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart
                };
                moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
                break;

            case ClassDef classDef:
                var classSymbol = ExtractFullClassSymbol(classDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                var structSymbol = ExtractFullStructSymbol(structDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols[interfaceDef.Name] = interfaceSymbol;
                break;

            case EnumDef enumDef:
                var enumAccessLevel = GetAccessLevel(enumDef.Name);
                var enumSymbol = new TypeSymbol
                {
                    Name = enumDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Enum,
                    AccessLevel = enumAccessLevel,
                    DeclarationLine = enumDef.LineStart,
                    DeclarationColumn = enumDef.ColumnStart,
                    DefiningModule = moduleInfo.CanonicalModuleName ?? moduleInfo.Path
                };
                moduleInfo.ExportedSymbols[enumDef.Name] = enumSymbol;
                break;

            case VariableDeclaration varDecl:
                var varAccessLevel = GetAccessLevel(varDecl.Name);
                var varSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = varAccessLevel,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                moduleInfo.ExportedSymbols[varDecl.Name] = varSymbol;
                break;

            default:
                // Other statements (imports, etc.) are handled by ImportResolver
                break;
        }
    }

    /// <summary>
    /// Extract full type information from a class definition.
    /// </summary>
    internal TypeSymbol ExtractFullClassSymbol(ClassDef classDef, string definingModulePath)
    {
        var accessLevel = GetAccessLevel(classDef.Name);
        bool isAbstract = classDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);

        string? unresolvedBase = classDef.BaseClasses.Length > 0 ? classDef.BaseClasses[0].Name : null;
        var unresolvedInterfaces = classDef.BaseClasses.Length > 1
            ? classDef.BaseClasses.Skip(1).Select(b => b.Name).ToList()
            : new List<string>();

        var fields = new List<VariableSymbol>();
        foreach (var stmt in classDef.Body)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                fields.Add(new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = GetAccessLevel(varDecl.Name),
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                });
            }
        }

        var methods = new List<FunctionSymbol>();
        var ctors = new List<FunctionSymbol>();
        foreach (var stmt in classDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                methods.Add(methodSymbol);
                if (method.Name == DunderNames.Init)
                    ctors.Add(methodSymbol);
            }
        }

        var classSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = accessLevel,
            IsAbstract = isAbstract,
            TypeParameters = classDef.TypeParameters.ToList(),
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart,
            DefiningModule = definingModulePath,
            UnresolvedBaseName = unresolvedBase,
            UnresolvedInterfaceNames = unresolvedInterfaces,
            Fields = fields,
            Methods = methods,
            Constructors = ctors
        };

        if (unresolvedBase != null)
        {
            _logger.LogDebug($"[ModuleLoader] Stored unresolved base for {classDef.Name}: {classSymbol.UnresolvedBaseName}");
        }

        return classSymbol;
    }

    /// <summary>
    /// Extract full type information from a struct definition.
    /// </summary>
    internal TypeSymbol ExtractFullStructSymbol(StructDef structDef, string definingModulePath)
    {
        var accessLevel = GetAccessLevel(structDef.Name);

        var fields = new List<VariableSymbol>();
        foreach (var stmt in structDef.Body)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                fields.Add(new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                    IsConstant = varDecl.IsConst,
                    AccessLevel = GetAccessLevel(varDecl.Name),
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                });
            }
        }

        var methods = new List<FunctionSymbol>();
        var ctors = new List<FunctionSymbol>();
        foreach (var stmt in structDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                methods.Add(methodSymbol);
                if (method.Name == DunderNames.Init)
                    ctors.Add(methodSymbol);
            }
        }

        return new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = accessLevel,
            TypeParameters = structDef.TypeParameters.ToList(),
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart,
            DefiningModule = definingModulePath,
            UnresolvedInterfaceNames = structDef.BaseClasses.Select(b => b.Name).ToList(),
            Fields = fields,
            Methods = methods,
            Constructors = ctors
        };
    }

    /// <summary>
    /// Extract full type information from an interface definition.
    /// </summary>
    internal TypeSymbol ExtractFullInterfaceSymbol(InterfaceDef interfaceDef, string definingModulePath)
    {
        var accessLevel = GetAccessLevel(interfaceDef.Name);

        var methods = new List<FunctionSymbol>();
        foreach (var stmt in interfaceDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method);
                if (!methodSymbol.IsAbstract)
                {
                    bool hasEllipsisBody = method.Body.Length == 1
                        && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
                    if (hasEllipsisBody)
                    {
                        methodSymbol = methodSymbol with { IsAbstract = true };
                    }
                }
                methods.Add(methodSymbol);
            }
        }

        return new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = accessLevel,
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart,
            DefiningModule = definingModulePath,
            UnresolvedInterfaceNames = interfaceDef.BaseInterfaces.Select(b => b.Name).ToList(),
            Methods = methods
        };
    }

    /// <summary>
    /// Extract method symbol with parameter and return type information.
    /// </summary>
    internal FunctionSymbol ExtractMethodSymbol(FunctionDef method)
    {
        var accessLevel = GetAccessLevel(method.Name);

        bool hasSelfParameter = method.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        bool hasStaticDecorator = method.Decorators.Any(d =>
            d.Name == DecoratorNames.Static);
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
            ReturnType = method.ReturnType != null
                ? ConvertTypeAnnotationToSemanticType(method.ReturnType)
                : SemanticType.Void,
            IsStatic = isStatic,
            IsAbstract = method.Decorators.Any(d => d.Name == DecoratorNames.Abstract),
            IsVirtual = method.Decorators.Any(d => d.Name == DecoratorNames.Virtual),
            IsOverride = method.Decorators.Any(d => d.Name == DecoratorNames.Override),
            TypeParameters = method.TypeParameters.ToList(),
            AccessLevel = accessLevel,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart
        };
    }

    /// <summary>
    /// Convert a type annotation to a semantic type.
    /// </summary>
    internal SemanticType ConvertTypeAnnotationToSemanticType(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return SemanticType.Unknown;

        SemanticType? baseType = typeAnnotation.Name switch
        {
            BuiltinNames.Int => SemanticType.Int,
            BuiltinNames.Long => SemanticType.Long,
            BuiltinNames.Float => SemanticType.Float,
            BuiltinNames.Double => SemanticType.Double,
            BuiltinNames.Float32 => SemanticType.Float32,
            BuiltinNames.Bool => SemanticType.Bool,
            BuiltinNames.Str or "string" => SemanticType.Str,
            "void" or BuiltinNames.None => SemanticType.Void,
            BuiltinNames.Object => SemanticType.Object,
            _ => null
        };

        if (baseType == null)
        {
            baseType = new UserDefinedType { Name = typeAnnotation.Name };
        }

        // Handle generic type arguments (list[int], dict[str, int], etc.)
        if (typeAnnotation.TypeArguments.Length > 0)
        {
            var typeArgs = typeAnnotation.TypeArguments
                .Select(ConvertTypeAnnotationToSemanticType)
                .ToList();

            if (typeAnnotation.Name == BuiltinNames.Tuple)
            {
                baseType = new TupleType { ElementTypes = typeArgs };
            }
            else if (typeAnnotation.Name == "Optional" && typeArgs.Count == 1)
            {
                baseType = new OptionalType { UnderlyingType = typeArgs[0] };
            }
            else if (typeAnnotation.Name == "Result" && typeArgs.Count == 2)
            {
                baseType = new ResultType { OkType = typeArgs[0], ErrorType = typeArgs[1] };
            }
            else if (typeAnnotation.Name == "function")
            {
                var returnType = typeArgs[^1];
                var paramTypes = typeArgs.Take(typeArgs.Count - 1).ToList();
                baseType = new FunctionType { ParameterTypes = paramTypes, ReturnType = returnType };
            }
            else
            {
                baseType = new GenericType { Name = typeAnnotation.Name, TypeArguments = typeArgs };
            }
        }

        // Handle T !E (Result type) syntax
        if (typeAnnotation.ErrorType != null)
        {
            var errorType = ConvertTypeAnnotationToSemanticType(typeAnnotation.ErrorType);
            baseType = new ResultType { OkType = baseType, ErrorType = errorType };
        }

        // Handle T? (Optional type) — Sharpy native optional
        if (typeAnnotation.IsOptional && baseType != SemanticType.Void)
        {
            baseType = new OptionalType { UnderlyingType = baseType };
        }

        // Handle T | None (C# nullable) — .NET interop
        if (typeAnnotation.IsCSharpNullable && baseType != SemanticType.Void)
        {
            baseType = new NullableType { UnderlyingType = baseType };
        }

        return baseType;
    }

    /// <summary>
    /// Determine access level based on naming convention.
    /// </summary>
    internal AccessLevel GetAccessLevel(string name)
    {
        if (name.StartsWith("__"))
            return AccessLevel.Private;
        if (name.StartsWith("_"))
            return AccessLevel.Protected;
        return AccessLevel.Public;
    }

    /// <summary>
    /// Compute the canonical (fully-qualified) module name from a file path.
    /// </summary>
    internal string ComputeCanonicalModuleName(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullPathDir = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileNameWithoutExtension(fullPath);

        if (fullPathDir == null)
            return fileName;

        var packageParts = new List<string>();
        var currentDir = fullPathDir;

        while (currentDir != null)
        {
            var dirName = Path.GetFileName(currentDir);
            var initFile = Path.Combine(currentDir, "__init__.spy");

            if (File.Exists(initFile))
            {
                packageParts.Insert(0, dirName);
                currentDir = Path.GetDirectoryName(currentDir);
            }
            else
            {
                break;
            }
        }

        if (fileName != DunderNames.Init)
        {
            packageParts.Add(fileName);
        }

        if (packageParts.Count == 0)
        {
            return fileName;
        }

        return string.Join(".", packageParts);
    }

    private bool IsModuleInChain(string modulePath)
    {
        return _importChain.Any(e => e.ModulePath == modulePath);
    }

    private string FormatCircularImportChain(string cycleStartModule)
    {
        var chain = new System.Text.StringBuilder();
        chain.AppendLine("Circular import detected:");

        var entries = _importChain.Reverse().ToList();
        var cycleStartIndex = entries.FindIndex(e => e.ModulePath == cycleStartModule);

        for (int i = cycleStartIndex; i < entries.Count; i++)
        {
            var entry = entries[i];
            chain.AppendLine($"  -> {Path.GetFileName(entry.ModulePath)}");
        }

        chain.AppendLine($"  -> {Path.GetFileName(cycleStartModule)} (cycle)");

        return chain.ToString().TrimEnd();
    }

    private void AddError(string message, int? line, int? column, string? code = null)
    {
        var errorMessage = CurrentModulePath != null
            ? $"{message} (in {Path.GetFileName(CurrentModulePath)})"
            : message;
        _diagnostics.AddError(errorMessage, line, column, CurrentModulePath, code, CompilerPhase.ImportResolution);
    }
}
