using System.Collections.Concurrent;
using System.Collections.Immutable;
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
    private readonly ConcurrentDictionary<string, bool> _loadedModules = new();
    private readonly Stack<ImportChainEntry> _importChain = new();
    private readonly object _importChainLock = new();
    private readonly ConcurrentDictionary<string, ModuleInfo> _moduleCache = new();
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

        // Check for circular imports — defer rejection by creating a stub
        if (IsModuleInChain(modulePath))
        {
            _logger.LogDebug($"[ModuleLoader] Circular import detected, creating stub: {Path.GetFileName(modulePath)}");
            try
            {
                var stub = CreateStubModuleInfo(modulePath);
                _moduleCache[modulePath] = stub;
                _deferredCycleModules.TryAdd(modulePath, true);
                return stub;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[ModuleLoader] Failed to create stub: {ex.Message}");
                var chainMessage = FormatCircularImportChain(modulePath);
                AddError(chainMessage, lineStart, columnStart, code: DiagnosticCodes.Semantic.CircularImportStubError);
                return null;
            }
        }

        // Check if already loaded
        if (_loadedModules.ContainsKey(modulePath))
        {
            _logger.LogDebug($"[ModuleLoader] LoadModule: {Path.GetFileName(modulePath)} (already loaded)");
            _moduleCache.TryGetValue(modulePath, out var loaded);
            return loaded;
        }

        _logger.LogInfo($"Loading module: {modulePath}");
        _logger.LogDebug($"[ModuleLoader] LoadModule: {Path.GetFileName(modulePath)} (parsing)");

        // Compute the canonical module name for this file (used for DefiningModule tracking)
        var canonicalModuleName = ComputeCanonicalModuleName(modulePath);
        _logger.LogDebug($"[ModuleLoader]   Canonical module name: {canonicalModuleName}");

        // Push to import chain before loading
        lock (_importChainLock)
        {
            _importChain.Push(new ImportChainEntry(
                modulePath,
                lineStart,
                columnStart,
                CurrentModulePath
            ));
        }

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
                ExportedSymbols = new ModuleExports(),
                CanonicalModuleName = canonicalModuleName
            };

            // Extract exported symbols (all top-level declarations). The module's own import
            // qualifiers are in scope for the duration, so an annotation this module wrote as
            // `inner.Box[int]` converts to the same type `Box[int]` does (#1448). Saved and restored
            // because the import-resolution callback below re-enters LoadModule for other modules.
            var previousQualifiers = _moduleImportQualifiers;
            _moduleImportQualifiers = CollectImportQualifiers(module);
            try
            {
                foreach (var statement in module.Body)
                {
                    ExtractExportedSymbol(statement, moduleInfo);
                }
            }
            finally
            {
                _moduleImportQualifiers = previousQualifiers;
            }

            // Recursively resolve imports within this module to detect transitive cycles
            resolveModuleImports?.Invoke(module, moduleInfo, Path.GetDirectoryName(modulePath));

            _moduleCache[modulePath] = moduleInfo;
            _loadedModules.TryAdd(modulePath, true);

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
            lock (_importChainLock)
            {
                _importChain.Pop();
            }
        }
    }

    /// <summary>
    /// Get a cached module by path. Returns null if not cached.
    /// </summary>
    public ModuleInfo? GetCachedModule(string modulePath)
    {
        _moduleCache.TryGetValue(modulePath, out var moduleInfo);
        return moduleInfo;
    }

    /// <summary>
    /// Cache a module (used by ImportResolver for .NET modules).
    /// </summary>
    public void CacheModule(string key, ModuleInfo moduleInfo)
    {
        _moduleCache[key] = moduleInfo;
        _loadedModules.TryAdd(key, true);
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
                // Names of this function's own type parameters (T, U in identity[T]/pair[T, U]) so
                // annotations naming them convert to TypeParameterType, enabling cross-module
                // generic inference to unify them like a bare same-file call (#1142).
                ISet<string>? funcTypeParamNames = functionDef.TypeParameters.Length > 0
                    ? new HashSet<string>(functionDef.TypeParameters.Select(tp => tp.Name), StringComparer.Ordinal)
                    : null;
                var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
                {
                    Name = p.Name,
                    IsNameBacktickEscaped = p.IsNameBacktickEscaped,
                    Type = ConvertTypeAnnotationToSemanticType(p.Type, funcTypeParamNames),
                    HasDefault = p.DefaultValue != null,
                    DefaultValue = p.DefaultValue,
                    IsVariadic = p.IsVariadic,
                    IsPositionalOnly = p.Kind == Parser.Ast.ParameterKind.PositionalOnly,
                    IsKeywordOnly = p.Kind == Parser.Ast.ParameterKind.KeywordOnly,
                    Modifier = p.Modifier
                }).ToList();

                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    IsNameBacktickEscaped = functionDef.IsNameBacktickEscaped,
                    Parameters = parameters,
                    ReturnType = functionDef.ReturnType != null
                        ? ConvertTypeAnnotationToSemanticType(functionDef.ReturnType, funcTypeParamNames)
                        : SemanticType.Void,
                    // Module-level generic functions must export their type parameters, exactly as
                    // ExtractMethodSymbol and the class/struct/interface extractions do. Without
                    // this a cross-module generic export reports IsGeneric == false, defeating
                    // explicit-type-args resolution and inference (#1142). Note this is the SYMBOL's
                    // TypeParameters list, which is a different thing from the type-parameter NAMES
                    // threaded into ConvertTypeAnnotationToSemanticType just above — a symbol can
                    // carry the first and still convert `key: K` to a UserDefinedType without the
                    // second, which is precisely what #1208 was.
                    TypeParameters = functionDef.TypeParameters.ToList(),
                    AccessLevel = accessLevel,
                    DeclaringFilePath = CurrentModulePath,
                    DeclarationSpan = functionDef.Span,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart,
                    NameDeclarationLine = functionDef.NameLineStart,
                    NameDeclarationColumn = functionDef.NameColumnStart,
                    NameDeclarationColumnEnd = functionDef.NameColumnEnd,
                    // #1365 — the facts NameResolver stamps on the same declaration. An imported
                    // symbol that lacks them is not the same symbol: overload dedup compares
                    // SignatureKey, hover reads Documentation, SPY0466 reads DeprecationMessage and
                    // SPY0480 reads IsMustUse. SignatureKey CALLS NameResolver's formula rather
                    // than restating it, so the two can never disagree about one signature.
                    SignatureKey = NameResolver.GetMethodSignatureKey(functionDef),
                    Documentation = functionDef.DocString,
                    DeprecationMessage = NameResolver.GetDeprecationMessage(functionDef.Decorators),
                    IsMustUse = NameResolver.HasMustUse(functionDef.Decorators)
                };
                moduleInfo.ExportedSymbols.Add(functionDef.Name, funcSymbol);

                if (!moduleInfo.FunctionOverloads.TryGetValue(functionDef.Name, out var overloadList))
                {
                    overloadList = new List<FunctionSymbol>();
                    moduleInfo.FunctionOverloads[functionDef.Name] = overloadList;
                }
                overloadList.Add(funcSymbol);
                break;

            case ClassDef classDef:
                var classSymbol = ExtractFullClassSymbol(classDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols.Add(classDef.Name, classSymbol);
                break;

            case StructDef structDef:
                var structSymbol = ExtractFullStructSymbol(structDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols.Add(structDef.Name, structSymbol);
                break;

            case InterfaceDef interfaceDef:
                var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols.Add(interfaceDef.Name, interfaceSymbol);
                break;

            case EnumDef enumDef:
                var enumSymbol = ExtractFullEnumSymbol(enumDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols.Add(enumDef.Name, enumSymbol);
                break;

            case VariableDeclaration varDecl:
                var varAccessLevel = GetAccessLevel(varDecl.Name);
                var varType = ConvertTypeAnnotationToSemanticType(varDecl.Type);
                var varConstValue = varDecl.IsConst
                    ? IntegerConstantEvaluator.TryFoldConstDeclaration(varType, varDecl.InitialValue)
                    : null;
                var varSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    Type = varType,
                    IsConstant = varDecl.IsConst,
                    IsFinal = varDecl.Decorators.Any(d => d.Name == DecoratorNames.Final),
                    HasDefaultValue = !varDecl.IsConst && varDecl.InitialValue != null,
                    ConstantValue = varConstValue,
                    AccessLevel = varAccessLevel,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart,
                    NameDeclarationLine = varDecl.NameLineStart,
                    NameDeclarationColumn = varDecl.NameColumnStart,
                    NameDeclarationColumnEnd = varDecl.NameColumnEnd,
                    DeclarationSpan = varDecl.Span,
                    DeclaringFilePath = CurrentModulePath
                };
                moduleInfo.ExportedSymbols.Add(varDecl.Name, varSymbol);
                break;

            case TypeAlias typeAlias:
                // A module-level alias is an export like any other declaration; without this arm
                // `from lib import Handle` reported SPY0202 for a name the module plainly declares
                // (#1363). Built by the same factory NameResolver and the TypeChecker use, so the
                // imported alias and the same-file one are the same construction.
                //
                // Note the qualified spelling `lib.Handle` still does NOT resolve: ModuleSymbol's
                // ResolveQualifiedType requires a TypeSymbol for the final segment and an alias is
                // not one (Shared/ModuleSymbolExtensions.cs:88-97). That is tracked separately
                // (#1436); the export itself is the prerequisite for either spelling.
                moduleInfo.ExportedSymbols.Add(typeAlias.Name, TypeAliasSymbol.CreateFrom(typeAlias));
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
        bool isAbstract = Shared.MemberClassification.HasAbstractDecorator(classDef.Decorators);

        string? unresolvedBase = classDef.BaseClasses.Length > 0 ? classDef.BaseClasses[0].Name : null;
        var unresolvedBaseTypeArgs = classDef.BaseClasses.Length > 0
            ? classDef.BaseClasses[0].TypeArguments
            : ImmutableArray<TypeAnnotation>.Empty;
        // The written annotations, not their names (#1403): `class Repo(Base, Comparable[int])`
        // means Comparable AT int, and a name-only record cannot say so.
        var unresolvedInterfaces = classDef.BaseClasses.Length > 1
            ? classDef.BaseClasses.Skip(1).ToList()
            : new List<TypeAnnotation>();

        var fields = ExtractFields(classDef.Body);

        var methods = new List<FunctionSymbol>();
        var ctors = new List<FunctionSymbol>();
        foreach (var stmt in classDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method, classDef.TypeParameters,
                    ownerKind: TypeKind.Class, ownerIsAbstract: isAbstract,
                    ownerTypeName: classDef.Name);

                methods.Add(methodSymbol);
                if (method.Name == DunderNames.Init)
                    ctors.Add(methodSymbol);
            }
        }

        // Extract properties and events so imported types carry them — without this,
        // TypeChecker.Utilities resolves event/property access by walking TypeSymbol.Events
        // and .Properties, so imported types with these members silently dropped them (#1267).
        var properties = ExtractProperties(classDef.Body, TypeKind.Class, isAbstract);
        var events = ExtractEvents(classDef.Body, TypeKind.Class, isAbstract);
        var nestedTypes = ExtractNestedTypes(classDef.Body, definingModulePath);

        var classSymbol = new TypeSymbol
        {
            Name = classDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = accessLevel,
            IsAbstract = isAbstract,
            IsNameBacktickEscaped = classDef.IsNameBacktickEscaped,
            TypeParameters = classDef.TypeParameters.ToList(),
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart,
            NameDeclarationLine = classDef.NameLineStart,
            NameDeclarationColumn = classDef.NameColumnStart,
            NameDeclarationColumnEnd = classDef.NameColumnEnd,
            DeclarationSpan = classDef.Span,
            // Which file declared this is what LSP go-to-definition, rename and document-highlight
            // all resolve through, and no extracted symbol carried it — the extractor has the path
            // its own diagnostics already use (#1441).
            DeclaringFilePath = CurrentModulePath,
            DefiningFilePath = CurrentModulePath,
            DefiningModule = definingModulePath,
            UnresolvedBaseName = unresolvedBase,
            UnresolvedBaseTypeArgs = unresolvedBaseTypeArgs,
            UnresolvedInterfaces = unresolvedInterfaces,
            Fields = fields,
            Methods = methods,
            Constructors = ctors,
            Properties = properties,
            Events = events,
            NestedTypes = nestedTypes,
            MethodOverloads = TypeSymbol.BuildMethodOverloads(methods),
            // #1365 — same three facts NameResolver stamps on the same declaration.
            Documentation = classDef.DocString,
            DeprecationMessage = NameResolver.GetDeprecationMessage(classDef.Decorators),
            IsMustUse = NameResolver.HasMustUse(classDef.Decorators)
        };

        LinkNestedTypes(classSymbol, nestedTypes);

        // An imported @dataclass is a dataclass (#1442). Without this it arrived as an ordinary
        // class — IsDataclass false and none of __init__/__eq__/__hash__/__repr__ present — so two
        // equal imported values compared unequal and printing one produced a type name. The
        // decorator is in the AST this extractor already walks, exactly as @must_use and @deprecated
        // are; the meaning comes from the authority the declaring pass uses, not from a second copy
        // of it. A field contributes the type of the annotation just converted, which is what the
        // extraction has in place of the checker's resolved binding.
        if (DataclassSynthesis.ReadOptions(classDef) is { } dataclassOptions)
        {
            classSymbol.IsDataclass = true;
            classSymbol.DataclassInfo = dataclassOptions;
            var dataclassFields = DataclassSynthesis.CollectFields(classSymbol, classDef);
            classSymbol.DataclassFields = dataclassFields;
            DataclassSynthesis.SynthesizeMembers(
                classSymbol, classDef, dataclassFields, dataclassOptions, field => field.Type);
        }

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

        var fields = ExtractFields(structDef.Body);

        var methods = new List<FunctionSymbol>();
        var ctors = new List<FunctionSymbol>();
        foreach (var stmt in structDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                var methodSymbol = ExtractMethodSymbol(method, structDef.TypeParameters,
                    ownerKind: TypeKind.Struct, ownerIsAbstract: false,
                    ownerTypeName: structDef.Name);
                methods.Add(methodSymbol);
                if (method.Name == DunderNames.Init)
                    ctors.Add(methodSymbol);
            }
        }

        var properties = ExtractProperties(structDef.Body, TypeKind.Struct, false);
        var events = ExtractEvents(structDef.Body, TypeKind.Struct, false);
        var nestedTypes = ExtractNestedTypes(structDef.Body, definingModulePath);

        var structSymbol = new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = accessLevel,
            IsNameBacktickEscaped = structDef.IsNameBacktickEscaped,
            TypeParameters = structDef.TypeParameters.ToList(),
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart,
            NameDeclarationLine = structDef.NameLineStart,
            NameDeclarationColumn = structDef.NameColumnStart,
            NameDeclarationColumnEnd = structDef.NameColumnEnd,
            DeclarationSpan = structDef.Span,
            DeclaringFilePath = CurrentModulePath,
            DefiningFilePath = CurrentModulePath,
            DefiningModule = definingModulePath,
            UnresolvedInterfaces = structDef.BaseClasses.ToList(),
            Fields = fields,
            Methods = methods,
            Constructors = ctors,
            Properties = properties,
            Events = events,
            NestedTypes = nestedTypes,
            MethodOverloads = TypeSymbol.BuildMethodOverloads(methods),
            Documentation = structDef.DocString,
            DeprecationMessage = NameResolver.GetDeprecationMessage(structDef.Decorators),
            IsMustUse = NameResolver.HasMustUse(structDef.Decorators)
        };

        LinkNestedTypes(structSymbol, nestedTypes);
        return structSymbol;
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
                var methodSymbol = ExtractMethodSymbol(method, interfaceDef.TypeParameters,
                    ownerKind: TypeKind.Interface, ownerIsAbstract: true,
                    ownerTypeName: interfaceDef.Name);
                methods.Add(methodSymbol);
            }
        }

        var properties = ExtractProperties(interfaceDef.Body, TypeKind.Interface, true);
        var events = ExtractEvents(interfaceDef.Body, TypeKind.Interface, true);
        var nestedTypes = ExtractNestedTypes(interfaceDef.Body, definingModulePath);

        var interfaceSymbol = new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = accessLevel,
            IsNameBacktickEscaped = interfaceDef.IsNameBacktickEscaped,
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart,
            NameDeclarationLine = interfaceDef.NameLineStart,
            NameDeclarationColumn = interfaceDef.NameColumnStart,
            NameDeclarationColumnEnd = interfaceDef.NameColumnEnd,
            DeclarationSpan = interfaceDef.Span,
            DeclaringFilePath = CurrentModulePath,
            DefiningFilePath = CurrentModulePath,
            DefiningModule = definingModulePath,
            UnresolvedInterfaces = interfaceDef.BaseInterfaces.ToList(),
            Methods = methods,
            Properties = properties,
            Events = events,
            NestedTypes = nestedTypes,
            Documentation = interfaceDef.DocString,
            DeprecationMessage = NameResolver.GetDeprecationMessage(interfaceDef.Decorators),
            IsMustUse = NameResolver.HasMustUse(interfaceDef.Decorators)
        };

        LinkNestedTypes(interfaceSymbol, nestedTypes);
        return interfaceSymbol;
    }

    /// <summary>
    /// Extracts a type body's field declarations. One loop shared by the class and struct
    /// extractors, which held byte-identical copies of it.
    /// </summary>
    /// <remarks>
    /// <c>IsFinal</c> and <c>HasDefaultValue</c> are threaded here (#1365): the first decides
    /// whether an assignment outside a constructor is legal and whether the emitted field is
    /// <c>readonly</c>, the second whether a constructor must initialize it. Dropping them made an
    /// imported <c>@final</c> field freely assignable and an imported defaulted field look
    /// mandatory — both on the import path only.
    /// </remarks>
    private List<VariableSymbol> ExtractFields(ImmutableArray<Statement> body)
    {
        var fields = new List<VariableSymbol>();
        foreach (var stmt in body)
        {
            if (stmt is not VariableDeclaration varDecl)
                continue;

            // Classified by the declaring pass's own authority rather than by a restated formula
            // (#1441): the local `Any(@static)`/`Any(@final)`/name-convention triple this replaces
            // had no notion of @private, so an explicitly-private imported field read as public and
            // the decorator that said so was not recorded at all.
            var classification = Shared.MemberClassification.ClassifyField(varDecl);

            fields.Add(new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                IsNameBacktickEscaped = varDecl.IsNameBacktickEscaped,
                Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                IsConstant = varDecl.IsConst,
                IsStatic = classification.IsStatic,
                IsFinal = classification.IsFinal,
                HasDefaultValue = varDecl.InitialValue != null,
                AccessLevel = classification.Access,
                ExplicitAccessLevel = classification.ExplicitAccess,
                DeclaringFilePath = CurrentModulePath,
                DeclarationSpan = varDecl.Span,
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart,
                NameDeclarationLine = varDecl.NameLineStart,
                NameDeclarationColumn = varDecl.NameColumnStart,
                NameDeclarationColumnEnd = varDecl.NameColumnEnd
            });
        }
        return fields;
    }

    /// <summary>
    /// Extracts nested type declarations (class/struct/interface/enum inside a type body) into
    /// <see cref="TypeSymbol.NestedTypes"/>, the same destination
    /// <c>NameResolver.ResolveNestedTypeDeclaration</c> fills for a same-file declaration (#1363).
    /// Without it an imported <c>Outer</c> arrived childless, so <c>Outer.Inner</c> — which
    /// <c>TypeResolver.LookupNestedType</c> answers by walking exactly this list — reported SPY0202
    /// for a type the module plainly declares. Recursion is free: the three extractors call back
    /// into this method, so an inner type's own nested types come along.
    /// </summary>
    private List<TypeSymbol> ExtractNestedTypes(
        ImmutableArray<Statement> body, string definingModulePath)
    {
        List<TypeSymbol>? nested = null;
        foreach (var stmt in body)
        {
            TypeSymbol? symbol = stmt switch
            {
                ClassDef classDef => ExtractFullClassSymbol(classDef, definingModulePath),
                StructDef structDef => ExtractFullStructSymbol(structDef, definingModulePath),
                InterfaceDef interfaceDef => ExtractFullInterfaceSymbol(interfaceDef, definingModulePath),
                EnumDef enumDef => ExtractFullEnumSymbol(enumDef, definingModulePath),
                _ => null
            };

            if (symbol == null)
                continue;

            (nested ??= new List<TypeSymbol>()).Add(symbol);
        }

        return nested ?? new List<TypeSymbol>();
    }

    /// <summary>
    /// Points each nested type back at its enclosing declaration, mirroring
    /// <c>NameResolver.ResolveNestedTypeDeclaration</c>'s <c>DeclaringType</c> assignment. Separate
    /// from the initializer because <see cref="TypeSymbol.DeclaringType"/> is <c>internal set</c>,
    /// not <c>init</c> — the enclosing symbol does not exist yet while its own initializer runs.
    /// </summary>
    private static void LinkNestedTypes(TypeSymbol enclosing, List<TypeSymbol> nestedTypes)
    {
        foreach (var nested in nestedTypes)
            nested.DeclaringType = enclosing;
    }

    /// <summary>
    /// Extract full type information from an enum definition. One construction shared by the
    /// module-level export path, the circular-import stub, and nested extraction (#1363) — the
    /// first two used to hold byte-identical copies of it.
    /// </summary>
    internal TypeSymbol ExtractFullEnumSymbol(EnumDef enumDef, string definingModulePath)
    {
        var enumSymbol = new TypeSymbol
        {
            Name = enumDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Enum,
            AccessLevel = GetAccessLevel(enumDef.Name),
            IsNameBacktickEscaped = enumDef.IsNameBacktickEscaped,
            DeclarationLine = enumDef.LineStart,
            DeclarationColumn = enumDef.ColumnStart,
            NameDeclarationLine = enumDef.NameLineStart,
            NameDeclarationColumn = enumDef.NameColumnStart,
            NameDeclarationColumnEnd = enumDef.NameColumnEnd,
            DeclarationSpan = enumDef.Span,
            DeclaringFilePath = CurrentModulePath,
            DefiningFilePath = CurrentModulePath,
            DefiningModule = definingModulePath,
            // The same predicate NameResolver applies at the declaration, shared rather than
            // restated (#1442/#1284): without it an imported string enum is checked as an int enum,
            // so `.value` types int, `str` assignment is refused and iteration answers wrong.
            IsStringEnum = NameResolver.IsStringEnum(enumDef),
            Documentation = enumDef.DocString,
            DeprecationMessage = NameResolver.GetDeprecationMessage(enumDef.Decorators),
            IsMustUse = NameResolver.HasMustUse(enumDef.Decorators)
        };

        // Register enum members as static fields for pattern matching resolution
        foreach (var member in enumDef.Members)
        {
            enumSymbol.Fields.Add(new VariableSymbol
            {
                Name = member.Name,
                Kind = SymbolKind.Variable,
                IsStatic = true,
                IsConstant = true,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = member.LineStart,
                DeclarationColumn = member.ColumnStart,
                NameDeclarationLine = member.LineStart,
                NameDeclarationColumn = member.ColumnStart,
                DeclarationSpan = member.Span
            });
        }

        return enumSymbol;
    }

    /// <summary>
    /// The set of type-parameter names in scope for a method's annotations: the enclosing
    /// declaration's parameters unioned with the method's own. Returns null when there are none, so
    /// the non-generic path allocates nothing and behaves exactly as before (#1208).
    /// </summary>
    private static ISet<string>? CollectTypeParameterNames(
        ImmutableArray<TypeParameterDef> enclosing, ImmutableArray<TypeParameterDef> own)
    {
        bool hasEnclosing = !enclosing.IsDefaultOrEmpty;
        bool hasOwn = !own.IsDefaultOrEmpty;
        if (!hasEnclosing && !hasOwn)
            return null;

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (hasEnclosing)
        {
            foreach (var tp in enclosing)
                names.Add(tp.Name);
        }

        if (hasOwn)
        {
            foreach (var tp in own)
                names.Add(tp.Name);
        }

        return names;
    }

    /// <summary>
    /// Extract method symbol with parameter and return type information.
    /// </summary>
    /// <param name="ownerTypeName">
    /// The declaring type's name, so <c>self</c> can be typed as the type it denotes. The declaring
    /// pass types it <c>UserDefinedType { Name = _currentClass.Name }</c> when it checks the body
    /// (<c>TypeChecker.Definitions</c>); the extraction left it <c>Unknown</c>, which is the whole
    /// of the <c>FunctionSymbol.Parameters</c> divergence the mirror reported for every method role
    /// (#1441). Null only where no owner exists.
    /// </param>
    internal FunctionSymbol ExtractMethodSymbol(
        FunctionDef method,
        ImmutableArray<TypeParameterDef> enclosingTypeParameters = default,
        TypeKind ownerKind = TypeKind.Class,
        bool ownerIsAbstract = false,
        string? ownerTypeName = null)
    {
        var classification = Shared.MemberClassification.Classify(method, ownerKind, ownerIsAbstract);

        // The type-parameter names in scope for this method's annotations: the declaring
        // class/struct/interface's, UNIONED with the method's own. Without them
        // ConvertTypeAnnotationToSemanticType turns `key: K` into a UserDefinedType named "K"
        // rather than a TypeParameterType, so cross-module inference has nothing to unify and
        // `genlib.Slot(1, "a")` reports SPY0237 where the from-import spelling infers (#1208).
        // Module-level functions have threaded their own names since #1142 (:233); methods
        // threaded NEITHER set until now.
        var typeParamNames = CollectTypeParameterNames(enclosingTypeParameters, method.TypeParameters);

        var parameters = method.Parameters.Select((p, index) => new ParameterSymbol
        {
            Name = p.Name,
            IsNameBacktickEscaped = p.IsNameBacktickEscaped,
            // `self` denotes the declaring type, which the extractor knows because it is extracting
            // that type's body. Same rule and same shape as TypeChecker.Definitions' body check
            // (#1441); leaving it Unknown made every imported method's parameter vector differ from
            // the declaration's in its first slot.
            Type = index == 0 && p.Name == PythonNames.Self && ownerTypeName != null
                ? new UserDefinedType { Name = ownerTypeName }
                : ConvertTypeAnnotationToSemanticType(p.Type, typeParamNames),
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == Parser.Ast.ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == Parser.Ast.ParameterKind.KeywordOnly,
            Modifier = p.Modifier
        }).ToList();

        return new FunctionSymbol
        {
            Name = method.Name,
            Kind = SymbolKind.Function,
            // The escape is part of what the name denotes (#1325/#1328): a `def `class`` method
            // arriving unescaped emits the mangled spelling and the program is CS0103.
            IsNameBacktickEscaped = method.IsNameBacktickEscaped,
            Parameters = parameters,
            ReturnType = method.ReturnType != null
                ? ConvertTypeAnnotationToSemanticType(method.ReturnType, typeParamNames)
                : SemanticType.Void,
            IsStatic = classification.IsStatic,
            IsAbstract = classification.IsAbstract,
            IsVirtual = classification.IsVirtual,
            IsOverride = classification.IsOverride,
            TypeParameters = method.TypeParameters.ToList(),
            AccessLevel = classification.Access,
            // Classify already computed it; only the copy onto the symbol was missing, which left a
            // @private method indistinguishable from a convention-private one (#1441).
            ExplicitAccessLevel = classification.ExplicitAccess,
            DeclaringFilePath = CurrentModulePath,
            DeclarationSpan = method.Span,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart,
            NameDeclarationLine = method.NameLineStart,
            NameDeclarationColumn = method.NameColumnStart,
            NameDeclarationColumnEnd = method.NameColumnEnd,
            // #1365 — see the module-level function arm; SignatureKey is what overload dedup
            // compares, so an imported overload set without it cannot tell two signatures apart.
            SignatureKey = NameResolver.GetMethodSignatureKey(method),
            Documentation = method.DocString,
            DeprecationMessage = NameResolver.GetDeprecationMessage(method.Decorators),
            IsMustUse = NameResolver.HasMustUse(method.Decorators)
        };
    }

    /// <summary>
    /// Convert a type annotation to a semantic type.
    /// </summary>
    internal SemanticType ConvertTypeAnnotationToSemanticType(TypeAnnotation? typeAnnotation)
        => ConvertTypeAnnotationToSemanticType(typeAnnotation, typeParameterNames: null);

    /// <summary>
    /// Convert a type annotation to a semantic type, resolving bare names that match one of
    /// <paramref name="typeParameterNames"/> to a <see cref="TypeParameterType"/> rather than a
    /// <see cref="UserDefinedType"/>. This mirrors <c>TypeResolver</c>'s <c>TypeParameterSymbol</c>
    /// path so a module-level generic export (e.g. <c>def identity[T](x: T)</c>) carries genuine
    /// type-parameter references in its parameter/return types. Without it, a plain call through
    /// the module member (<c>mathlib.identity(5)</c>) cannot unify the parameter — inference sees
    /// <c>UserDefinedType T</c>, not a type parameter — and reports SPY0237 (#1142).
    /// </summary>
    internal SemanticType ConvertTypeAnnotationToSemanticType(
        TypeAnnotation? typeAnnotation, ISet<string>? typeParameterNames)
    {
        if (typeAnnotation == null)
            return SemanticType.Unknown;

        // A module qualifier is a lookup instruction, not part of the type's name — the rule
        // TypeResolver.ResolveGenericType states at TypeResolver.cs:716-721, applied here so the
        // extraction reaches the same representation. Before it, an annotation written INSIDE an
        // extracted module kept its qualifier (`inner.Box[int]` where the resolver produces
        // `Box[int]`), and the two spellings of one program named two different types (#1448).
        var annotationName = NormalizeQualifiedAnnotationName(typeAnnotation.Name);

        SemanticType? baseType = annotationName switch
        {
            BuiltinNames.Int => SemanticType.Int,
            BuiltinNames.Long => SemanticType.Long,
            BuiltinNames.Float => SemanticType.Float,
            BuiltinNames.Double => SemanticType.Double,
            BuiltinNames.Float32 => SemanticType.Float32,
            BuiltinNames.Decimal => SemanticType.Decimal,
            BuiltinNames.Bool => SemanticType.Bool,
            BuiltinNames.Str or "string" => SemanticType.Str,
            "void" or BuiltinNames.None => SemanticType.Void,
            BuiltinNames.Object => SemanticType.Object,
            _ => null
        };

        if (baseType == null)
        {
            // A bare name matching an enclosing declaration's type parameter (T in identity[T])
            // is a type-parameter reference, not a user-defined type. Type-argument-bearing names
            // (list[T]) fall through to the generic handling below, which threads the same set into
            // the element conversions.
            if (typeParameterNames != null
                && typeAnnotation.TypeArguments.Length == 0
                && typeParameterNames.Contains(annotationName))
            {
                baseType = new TypeParameterType { Name = annotationName };
            }
            else
            {
                baseType = new UserDefinedType { Name = annotationName };
            }
        }

        // Handle generic type arguments (list[int], dict[str, int], etc.)
        if (typeAnnotation.TypeArguments.Length > 0)
        {
            var typeArgs = typeAnnotation.TypeArguments
                .Select(a => ConvertTypeAnnotationToSemanticType(a, typeParameterNames))
                .ToList();

            if (annotationName == BuiltinNames.Tuple)
            {
                baseType = new TupleType { ElementTypes = typeArgs };
            }
            else if (annotationName == BuiltinNames.Optional && typeArgs.Count == 1)
            {
                baseType = new OptionalType { UnderlyingType = typeArgs[0] };
            }
            else if (annotationName == BuiltinNames.Result && typeArgs.Count == 2)
            {
                baseType = new ResultType { OkType = typeArgs[0], ErrorType = typeArgs[1] };
            }
            else if (annotationName == BuiltinNames.Function)
            {
                var returnType = typeArgs[^1];
                var paramTypes = typeArgs.Take(typeArgs.Count - 1).ToList();
                baseType = new FunctionType { ParameterTypes = paramTypes, ReturnType = returnType };
            }
            else
            {
                baseType = new GenericType { Name = annotationName, TypeArguments = typeArgs };
            }
        }

        // Handle T !E (Result type) syntax
        if (typeAnnotation.ErrorType != null)
        {
            var errorType = ConvertTypeAnnotationToSemanticType(typeAnnotation.ErrorType, typeParameterNames);
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
    /// The module qualifiers in scope while the module currently being extracted is walked — the
    /// names its own <c>import</c> statements bind. Null outside an extraction.
    /// </summary>
    private ISet<string>? _moduleImportQualifiers;

    /// <summary>
    /// The names a module's <c>import</c> statements make usable as a qualifier: the alias for
    /// <c>import a.b as c</c>, and for <c>import a.b</c> both the bound root <c>a</c> and the full
    /// <c>a.b</c>, either of which can lead a written annotation.
    /// </summary>
    private static HashSet<string> CollectImportQualifiers(Module module)
    {
        var qualifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in module.Body)
        {
            if (statement.UnwrapDecorated() is not ImportStatement import)
                continue;

            foreach (var alias in import.Names)
            {
                if (alias.AsName != null)
                {
                    qualifiers.Add(alias.AsName);
                    continue;
                }

                qualifiers.Add(alias.Name);
                var firstDot = alias.Name.IndexOf('.', StringComparison.Ordinal);
                if (firstDot > 0)
                    qualifiers.Add(alias.Name[..firstDot]);
            }
        }

        return qualifiers;
    }

    /// <summary>
    /// Strips a leading MODULE qualifier from a written annotation name, leaving anything else
    /// dotted alone. <c>inner.Box</c> is <c>Box</c> when this module imports <c>inner</c>;
    /// <c>Outer.Inner</c> is not, because <c>Outer</c> is a type and the dotted name is the nested
    /// type's own (#1448).
    /// </summary>
    private string NormalizeQualifiedAnnotationName(string name)
    {
        var lastDot = name.LastIndexOf('.');
        if (lastDot <= 0 || _moduleImportQualifiers == null)
            return name;

        return _moduleImportQualifiers.Contains(name[..lastDot]) ? name[(lastDot + 1)..] : name;
    }

    /// <summary>
    /// Determine access level based on naming convention.
    /// </summary>
    internal AccessLevel GetAccessLevel(string name)
        => Shared.AccessLevelConventions.FromName(name);

    /// <summary>
    /// Extracts <see cref="PropertySymbol"/>s from a type body so imported types carry them (#1267).
    /// Handles accessor merging (getter + setter defined separately).
    /// </summary>
    private List<PropertySymbol> ExtractProperties(
        ImmutableArray<Statement> body, TypeKind ownerKind, bool ownerIsAbstract)
    {
        var properties = new List<PropertySymbol>();
        foreach (var stmt in body)
        {
            if (stmt is not PropertyDef propDef)
                continue;

            var accessLevel = GetAccessLevel(propDef.Name);
            var explicitAccess = Shared.MemberClassification.GetExplicitAccessLevel(propDef.Decorators);
            if (explicitAccess != null)
                accessLevel = explicitAccess.Value;

            // The SAME authority NameResolver consumes, not a mirror of it (#1374). This site and
            // NameResolver.ResolvePropertyDeclaration used to hold copies of one rule, which is how
            // #1258/#1266/#1368 happened; a copy cannot drift if it does not exist.
            bool isAbstract = Shared.MemberClassification.IsAbstract(propDef, ownerKind, ownerIsAbstract);
            bool hasGetter = propDef.Accessor == PropertyAccessor.Get || propDef.Accessor == PropertyAccessor.None;
            bool hasSetter = propDef.Accessor == PropertyAccessor.Set;
            bool hasInit = propDef.Accessor == PropertyAccessor.Init;

            var existing = properties.FirstOrDefault(p => p.Name == propDef.Name);
            if (existing != null)
            {
                var merged = existing with
                {
                    HasGetter = existing.HasGetter || hasGetter,
                    HasSetter = existing.HasSetter || hasSetter,
                    HasInit = existing.HasInit || hasInit,
                    SetterAccess = hasSetter || hasInit ? accessLevel : existing.SetterAccess,
                };
                properties[properties.IndexOf(existing)] = merged;
            }
            else
            {
                properties.Add(new PropertySymbol
                {
                    Name = propDef.Name,
                    IsNameBacktickEscaped = propDef.IsNameBacktickEscaped,
                    HasGetter = hasGetter,
                    HasSetter = hasSetter,
                    HasInit = hasInit,
                    IsStatic = propDef.Decorators.Any(d => d.Name == DecoratorNames.Static),
                    IsVirtual = propDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual),
                    IsAbstract = isAbstract,
                    IsOverride = propDef.Decorators.Any(d => d.Name == DecoratorNames.Override),
                    IsFinal = propDef.Decorators.Any(d => d.Name == DecoratorNames.Final),
                    GetterAccess = hasGetter ? accessLevel : AccessLevel.Public,
                    SetterAccess = hasSetter || hasInit ? accessLevel : AccessLevel.Public,
                    ExplicitInterface = propDef.ExplicitInterface,
                    Observers = propDef.Observers,
                });
            }
        }
        return properties;
    }

    /// <summary>
    /// Extracts <see cref="EventSymbol"/>s from a type body so imported types carry them (#1267).
    /// Handles accessor merging (add + remove defined separately).
    /// </summary>
    private List<EventSymbol> ExtractEvents(
        ImmutableArray<Statement> body, TypeKind ownerKind, bool ownerIsAbstract)
    {
        var events = new List<EventSymbol>();
        foreach (var stmt in body)
        {
            if (stmt is not EventDef eventDef)
                continue;

            var accessLevel = GetAccessLevel(eventDef.Name);
            var explicitAccess = Shared.MemberClassification.GetExplicitAccessLevel(eventDef.Decorators);
            if (explicitAccess != null)
                accessLevel = explicitAccess.Value;

            // Same authority as NameResolver.ResolveEventDeclaration (#1374) — see the property
            // extractor above for why this is a call and not a copy.
            bool isAbstract = Shared.MemberClassification.IsAbstract(eventDef, ownerKind, ownerIsAbstract);
            bool hasAdd = eventDef.Accessor == EventAccessor.Add;
            bool hasRemove = eventDef.Accessor == EventAccessor.Remove;
            if (!eventDef.IsFunctionStyle)
            {
                hasAdd = true;
                hasRemove = true;
            }

            var existing = events.FirstOrDefault(e => e.Name == eventDef.Name);
            if (existing != null)
            {
                var merged = existing with
                {
                    HasAdd = existing.HasAdd || hasAdd,
                    HasRemove = existing.HasRemove || hasRemove,
                    AddAccessLevel = hasAdd ? accessLevel : existing.AddAccessLevel,
                    RemoveAccessLevel = hasRemove ? accessLevel : existing.RemoveAccessLevel,
                };
                events[events.IndexOf(existing)] = merged;
            }
            else
            {
                events.Add(new EventSymbol
                {
                    Name = eventDef.Name,
                    IsNameBacktickEscaped = eventDef.IsNameBacktickEscaped,
                    HasAdd = hasAdd,
                    HasRemove = hasRemove,
                    IsStatic = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Static),
                    IsVirtual = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual),
                    IsAbstract = isAbstract,
                    IsOverride = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Override),
                    IsFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final),
                    AccessLevel = accessLevel,
                    AddAccessLevel = hasAdd ? accessLevel : AccessLevel.Public,
                    RemoveAccessLevel = hasRemove ? accessLevel : AccessLevel.Public,
                });
            }
        }
        return events;
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

    /// <summary>
    /// Modules that were loaded as stubs due to circular import deferral.
    /// Uses ConcurrentDictionary for thread safety since LoadModule may be called concurrently.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _deferredCycleModules = new();

    /// <summary>
    /// Modules whose circular imports have been deferred (loaded as stubs).
    /// </summary>
    public IReadOnlySet<string> DeferredCycleModules =>
        _deferredCycleModules.Keys.ToHashSet();

    /// <summary>
    /// Create a stub ModuleInfo containing only type declarations (class/struct/interface/enum).
    /// Used when a circular import is detected — the stub provides enough information for
    /// type annotation resolution while deferring full module loading.
    /// </summary>
    internal ModuleInfo CreateStubModuleInfo(string modulePath)
    {
        var source = File.ReadAllText(modulePath);
        var sourceText = new Text.SourceText(source, modulePath);
        var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: _cancellationToken);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser.Parser(tokens, _logger, cancellationToken: _cancellationToken);
        var module = parser.ParseModule();

        var canonicalModuleName = ComputeCanonicalModuleName(modulePath);

        var moduleInfo = new ModuleInfo
        {
            Path = modulePath,
            Module = module,
            ExportedSymbols = new ModuleExports(),
            CanonicalModuleName = canonicalModuleName,
            IsStub = true,
            StubSourcePath = CurrentModulePath
        };

        foreach (var statement in module.Body)
        {
            switch (statement)
            {
                case ClassDef classDef:
                    var classSymbol = ExtractFullClassSymbol(classDef, canonicalModuleName);
                    moduleInfo.ExportedSymbols.Add(classDef.Name, classSymbol);
                    break;
                case StructDef structDef:
                    var structSymbol = ExtractFullStructSymbol(structDef, canonicalModuleName);
                    moduleInfo.ExportedSymbols.Add(structDef.Name, structSymbol);
                    break;
                case InterfaceDef interfaceDef:
                    var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef, canonicalModuleName);
                    moduleInfo.ExportedSymbols.Add(interfaceDef.Name, interfaceSymbol);
                    break;
                case EnumDef enumDef:
                    moduleInfo.ExportedSymbols.Add(
                        enumDef.Name, ExtractFullEnumSymbol(enumDef, canonicalModuleName));
                    break;
            }
        }

        _logger.LogDebug($"[ModuleLoader] Created stub for {Path.GetFileName(modulePath)} with {moduleInfo.ExportedSymbols.Count} type declarations");

        return moduleInfo;
    }

    private bool IsModuleInChain(string modulePath)
    {
        lock (_importChainLock)
        {
            return _importChain.Any(e => e.ModulePath == modulePath);
        }
    }

    private string FormatCircularImportChain(string cycleStartModule)
    {
        lock (_importChainLock)
        {
            var chain = new System.Text.StringBuilder();
            chain.AppendLine("Circular import detected:");

            var entries = _importChain.Reverse().ToList();
            var cycleStartIndex = entries.FindIndex(e => e.ModulePath == cycleStartModule);

            for (int i = cycleStartIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                chain.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  -> {Path.GetFileName(entry.ModulePath)}");
            }

            chain.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  -> {Path.GetFileName(cycleStartModule)} (cycle)");

            return chain.ToString().TrimEnd();
        }
    }

    private void AddError(string message, int? line, int? column, string? code = null)
    {
        var errorMessage = CurrentModulePath != null
            ? $"{message} (in {Path.GetFileName(CurrentModulePath)})"
            : message;
        _diagnostics.AddPhaseError(errorMessage, CompilerPhase.ImportResolution,
            line: line, column: column, filePath: CurrentModulePath, code: code);
    }
}
