using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;

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

    /// <summary>
    /// All loaded .spy modules (excludes .NET modules).
    /// Key is the full file path, value is the ModuleInfo.
    /// </summary>
    public IReadOnlyDictionary<string, ModuleInfo> LoadedSpyModules =>
        _moduleCache
            .Where(kvp => !kvp.Value.IsNetModule && kvp.Value.Module != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    private DependencyGraphBuilder? _graphBuilder;
    private SemanticBinding? _semanticBinding;

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null)
    {
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

    public IReadOnlyList<SemanticError> Errors => _errors;

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
                    fromImport.LineStart, fromImport.ColumnStart);
                return null;
            }

            _logger.LogDebug($"[ImportResolver]   Resolved to: {resolution.FullPath}");
            _logger.LogDebug($"[ImportResolver]   Canonical name: {resolution.CanonicalModuleName ?? resolution.ModuleName}");

            // Store the resolved module path for code generation
            // For relative imports like ".helpers", this gives the canonical name like "mypackage.helpers"
            var resolvedPath = resolution.CanonicalModuleName ?? resolution.ModuleName;
            if (_semanticBinding != null)
            {
                _semanticBinding.SetResolvedModulePath(fromImport, resolvedPath);
            }
            else
            {
                // Legacy fallback: store directly on AST (will be removed in future)
                fromImport.ResolvedModulePath = resolvedPath;
            }

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
                if (_semanticBinding != null)
                {
                    _semanticBinding.SetReExportedSymbols(fromImport, reExportedSymbols);
                }
                else
                {
                    // Legacy fallback: store directly on AST (will be removed in future)
                    fromImport.ReExportedSymbols = reExportedSymbols;
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
        {
            _logger.LogDebug($"[ImportResolver] LoadModule: {Path.GetFileName(modulePath)} (from cache)");
            return cached;
        }

        // Check for circular imports with detailed chain
        if (IsModuleInChain(modulePath))
        {
            var chainMessage = FormatCircularImportChain(modulePath);
            _logger.LogDebug($"[ImportResolver] Circular import detected: {Path.GetFileName(modulePath)}");
            AddError(chainMessage, lineStart, columnStart);
            return null;
        }

        // Check if already loaded
        if (_loadedModules.Contains(modulePath))
        {
            _logger.LogDebug($"[ImportResolver] LoadModule: {Path.GetFileName(modulePath)} (already loaded)");
            return _moduleCache.GetValueOrDefault(modulePath);
        }

        _logger.LogInfo($"Loading module: {modulePath}");
        _logger.LogDebug($"[ImportResolver] LoadModule: {Path.GetFileName(modulePath)} (parsing)");

        // Compute the canonical module name for this file (used for DefiningModule tracking)
        var canonicalModuleName = ComputeCanonicalModuleName(modulePath);
        _logger.LogDebug($"[ImportResolver]   Canonical module name: {canonicalModuleName}");

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
                ExportedSymbols = new Dictionary<string, Symbol>(),
                CanonicalModuleName = canonicalModuleName
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

            // Log final exported symbols
            _logger.LogDebug($"[ImportResolver] Module {Path.GetFileName(modulePath)} loaded with {moduleInfo.ExportedSymbols.Count} exports:");
            foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
            {
                if (symbol is TypeSymbol typeSymbol)
                {
                    _logger.LogDebug($"[ImportResolver]   - {name} ({symbol.Kind}:{typeSymbol.TypeKind}), DefiningModule: {typeSymbol.DefiningModule ?? "null"}, IsReExport: {typeSymbol.IsReExport}");
                }
                else
                {
                    _logger.LogDebug($"[ImportResolver]   - {name} ({symbol.Kind})");
                }
            }

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
                // Pass the canonical module name to set DefiningModule (critical for re-exports)
                var classSymbol = ExtractFullClassSymbol(classDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                // Extract full struct information including fields, methods, and type parameters
                // Pass the canonical module name to set DefiningModule (critical for re-exports)
                var structSymbol = ExtractFullStructSymbol(structDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                // Extract full interface information including methods and type parameters
                // Pass the canonical module name to set DefiningModule (critical for re-exports)
                var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef, moduleInfo.CanonicalModuleName ?? moduleInfo.Path);
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
                    DeclarationColumn = enumDef.ColumnStart,
                    // Set DefiningModule for enums (critical for re-exports)
                    DefiningModule = moduleInfo.CanonicalModuleName ?? moduleInfo.Path
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
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.Name));
        _logger.LogDebug($"[ImportResolver] ExtractReExportedSymbols: from {fromImport.Module} import {importedNames}");
        _logger.LogDebug($"[ImportResolver]   In module: {Path.GetFileName(moduleInfo.Path)}");

        // Resolve the source module to get its exported symbols
        var sourceModulePath = ResolveModulePath(fromImport.Module, Path.GetDirectoryName(moduleInfo.Path));
        if (sourceModulePath == null)
        {
            _logger.LogDebug($"[ImportResolver]   Source module '{fromImport.Module}' not found during re-export extraction");
            // Module not found - error will be reported during full import resolution
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
            // "from .module import *" - re-export all public symbols
            foreach (var (name, symbol) in sourceModule.ExportedSymbols)
            {
                if (!name.StartsWith("_"))
                {
                    // Create a re-export symbol that references the original
                    var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
                    moduleInfo.ExportedSymbols[name] = reExportSymbol;
                    reExportedSymbols[name] = reExportSymbol;
                    _logger.LogDebug($"[ImportResolver]     Re-exporting (wildcard): {name}");
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
                    reExportedSymbols[targetName] = reExportSymbol;

                    // Log detailed info for type symbols
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
                // If symbol not found, error will be reported during full import resolution
            }
        }

        // Store re-exported symbols
        if (reExportedSymbols.Count > 0)
        {
            _logger.LogDebug($"[ImportResolver]   Added {reExportedSymbols.Count} re-exported symbols to {Path.GetFileName(moduleInfo.Path)}");
            if (_semanticBinding != null)
            {
                _semanticBinding.SetReExportedSymbols(fromImport, reExportedSymbols);
            }
            else
            {
                // Legacy fallback: store directly on AST (will be removed in future)
                fromImport.ReExportedSymbols = reExportedSymbols;
            }
        }
    }

    /// <summary>
    /// Create a symbol for a re-exported item
    /// </summary>
    private Symbol CreateReExportSymbol(Symbol originalSymbol, FromImportStatement fromImport, string? newName = null)
    {
        var effectiveName = newName ?? originalSymbol.Name;

        // Clone the symbol with the new name if provided
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

        return result;
    }

    /// <summary>
    /// Create a re-exported type symbol, properly tracking the DefiningModule through the re-export chain.
    /// </summary>
    private TypeSymbol CreateReExportedTypeSymbol(TypeSymbol originalType, FromImportStatement fromImport, string effectiveName)
    {
        // Determine the defining module - preserve original if set, otherwise use the resolved import path
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
            TypeParameters = originalType.TypeParameters,
            Fields = originalType.Fields,
            Methods = originalType.Methods,
            Properties = originalType.Properties,
            Constructors = originalType.Constructors,
            BaseType = originalType.BaseType,
            Interfaces = originalType.Interfaces,
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
        if (_semanticBinding != null)
        {
            return _semanticBinding.GetResolvedModulePath(fromImport);
        }
        return fromImport.ResolvedModulePath;
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
    /// Convert a type annotation to a semantic type.
    /// This is used during import resolution to provide type information before
    /// full semantic analysis. For user-defined types, creates a UserDefinedType
    /// that can be resolved later during code generation via symbol table lookup.
    /// </summary>
    private SemanticType ConvertTypeAnnotationToSemanticType(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return SemanticType.Unknown;

        // Handle optional types (T? syntax) — during import resolution, we map to NullableType
        // since we're operating before full semantic analysis in a .NET interop context
        var isOptional = typeAnnotation.IsOptional;

        // Map primitive type names
        SemanticType? baseType = typeAnnotation.Name switch
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
            _ => null // Handle non-primitive types below
        };

        // For non-primitive types, create a UserDefinedType
        // The Symbol will be resolved during code generation via symbol table lookup
        if (baseType == null)
        {
            baseType = new UserDefinedType { Name = typeAnnotation.Name };
        }

        // Wrap in nullable if needed
        if (isOptional && baseType != SemanticType.Void)
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
    /// <param name="classDef">The class definition AST node.</param>
    /// <param name="definingModulePath">The file path of the module where this class is defined.
    /// This is critical for re-exports: when a class is re-exported through __init__.spy,
    /// the DefiningModule tracks the original definition location.</param>
    private TypeSymbol ExtractFullClassSymbol(ClassDef classDef, string definingModulePath)
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
            TypeParameters = classDef.TypeParameters.ToList(),
            DeclarationLine = classDef.LineStart,
            DeclarationColumn = classDef.ColumnStart,
            // Set DefiningModule to the actual file where this class is defined.
            // This is preserved through re-export chains to enable proper namespace resolution.
            DefiningModule = definingModulePath
        };

        // Store unresolved base class names for deferred resolution
        // The actual BaseType will be resolved after all types are registered
        if (classDef.BaseClasses.Length > 0)
        {
            foreach (var baseAnnot in classDef.BaseClasses)
            {
                if (classSymbol.UnresolvedBaseName == null)
                {
                    classSymbol.UnresolvedBaseName = baseAnnot.Name;
                }
                else
                {
                    classSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
                }
            }
            _logger.LogDebug($"[ImportResolver] Stored unresolved base for {classDef.Name}: {classSymbol.UnresolvedBaseName}");
        }

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
    /// <param name="structDef">The struct definition AST node.</param>
    /// <param name="definingModulePath">The file path of the module where this struct is defined.</param>
    private TypeSymbol ExtractFullStructSymbol(StructDef structDef, string definingModulePath)
    {
        var accessLevel = GetAccessLevel(structDef.Name);

        var structSymbol = new TypeSymbol
        {
            Name = structDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct,
            AccessLevel = accessLevel,
            TypeParameters = structDef.TypeParameters.ToList(),
            DeclarationLine = structDef.LineStart,
            DeclarationColumn = structDef.ColumnStart,
            // Set DefiningModule to the actual file where this struct is defined.
            DefiningModule = definingModulePath
        };

        // Store unresolved interface names for deferred resolution
        foreach (var baseAnnot in structDef.BaseClasses)
        {
            structSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
        }

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
    /// <param name="interfaceDef">The interface definition AST node.</param>
    /// <param name="definingModulePath">The file path of the module where this interface is defined.</param>
    private TypeSymbol ExtractFullInterfaceSymbol(InterfaceDef interfaceDef, string definingModulePath)
    {
        var accessLevel = GetAccessLevel(interfaceDef.Name);

        var interfaceSymbol = new TypeSymbol
        {
            Name = interfaceDef.Name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            AccessLevel = accessLevel,
            TypeParameters = interfaceDef.TypeParameters.ToList(),
            DeclarationLine = interfaceDef.LineStart,
            DeclarationColumn = interfaceDef.ColumnStart,
            // Set DefiningModule to the actual file where this interface is defined.
            DefiningModule = definingModulePath
        };

        // Store unresolved base interface names for deferred resolution
        foreach (var baseAnnot in interfaceDef.BaseInterfaces)
        {
            interfaceSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
        }

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
                    bool hasEllipsisBody = method.Body.Length == 1
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
            d.Name == "static");
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
            TypeParameters = method.TypeParameters.ToList(),
            AccessLevel = accessLevel,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart
        };
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
        if (_moduleCache.TryGetValue(cacheKey, out var cached))
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
    /// Resolve a .NET namespace as a module (e.g., "system" -> types from System namespace).
    /// </summary>
    private ModuleInfo? ResolveNetNamespaceModule(string moduleName, string cacheKey)
    {
        _logger.LogDebug($"Resolving .NET namespace module: {moduleName}");

        // Create ModuleInfo for the .NET namespace
        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!, // No AST module exists for .NET namespaces
            ExportedSymbols = new Dictionary<string, Symbol>(),
            IsNetModule = true
        };

        // Get all types from the namespace
        var types = _moduleRegistry!.GetNamespaceTypes(moduleName);
        foreach (var typeSymbol in types)
        {
            moduleInfo.ExportedSymbols[typeSymbol.Name] = typeSymbol;
        }

        // Also get any functions if this namespace has an Exports class
        if (_moduleRegistry.IsModuleLoaded(moduleName))
        {
            var functions = _moduleRegistry.GetModuleFunctions(moduleName);
            foreach (var function in functions)
            {
                moduleInfo.ExportedSymbols[function.Name] = function;
            }
        }

        _moduleCache[cacheKey] = moduleInfo;
        _loadedModules.Add(cacheKey);

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

    /// <summary>
    /// Compute the canonical (fully-qualified) module name from a file path.
    /// Uses directory structure and __init__.spy files to determine package path.
    /// For example: /path/to/mypackage/submodule.spy -> "mypackage.submodule"
    /// </summary>
    private string ComputeCanonicalModuleName(string filePath)
    {
        // Normalize the path
        var fullPath = Path.GetFullPath(filePath);
        var fullPathDir = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileNameWithoutExtension(fullPath);

        if (fullPathDir == null)
            return fileName;

        // Walk up the directory tree, collecting package names until we find a non-package directory.
        // A package directory is one that contains __init__.spy
        var packageParts = new List<string>();
        var currentDir = fullPathDir;

        while (currentDir != null)
        {
            var dirName = Path.GetFileName(currentDir);
            var initFile = Path.Combine(currentDir, "__init__.spy");

            // Check if this directory is a package (has __init__.spy)
            if (File.Exists(initFile))
            {
                packageParts.Insert(0, dirName);
                currentDir = Path.GetDirectoryName(currentDir);
            }
            else
            {
                // We've found the source root (non-package directory)
                break;
            }
        }

        // Build the canonical name
        if (fileName != "__init__")
        {
            packageParts.Add(fileName);
        }

        // If no packages were found, just return the filename
        if (packageParts.Count == 0)
        {
            return fileName;
        }

        return string.Join(".", packageParts);
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

    /// <summary>
    /// The canonical module name (e.g., "mypackage.submodule") derived from the file path.
    /// Used for DefiningModule tracking in re-exported symbols.
    /// </summary>
    public string? CanonicalModuleName { get; init; }
}
