using System;
using System.Collections.Generic;
using System.IO;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves package-level symbols from __init__.spy files.
/// Handles package initialization and re-exports.
/// </summary>
internal class PackageResolver
{
    private readonly ICompilerLogger _logger;
    private readonly ImportResolver _importResolver;
    private readonly Dictionary<string, PackageInfo> _packageCache = new();

    public PackageResolver(ICompilerLogger? logger = null, ImportResolver? importResolver = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _importResolver = importResolver ?? new ImportResolver(logger);
    }

    /// <summary>
    /// Resolve a package from its __init__.spy file.
    /// Extracts both direct symbols and re-exported symbols from imports.
    /// </summary>
    /// <param name="packageName">The package name (dotted notation)</param>
    /// <param name="initPath">Path to the __init__.spy file</param>
    /// <returns>PackageInfo with all exported symbols, or null if resolution fails</returns>
    public PackageInfo? ResolvePackage(string packageName, string initPath)
    {
        // Check cache first
        if (_packageCache.TryGetValue(packageName, out var cached))
            return cached;

        _logger.LogDebug($"Resolving package: {packageName} from {initPath}");

        // Parse the __init__.spy file directly
        ModuleInfo moduleInfo;
        try
        {
            if (!File.Exists(initPath))
            {
                _logger.LogError($"Package __init__.spy not found: {initPath}", 0, 0);
                return null;
            }

            var source = File.ReadAllText(initPath);
            var sourceText = new Text.SourceText(source, initPath);
            var lexer = new Lexer.Lexer(sourceText, _logger);
            var tokens = lexer.TokenizeAll();
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();

            moduleInfo = new ModuleInfo
            {
                Path = initPath,
                Module = module,
                ExportedSymbols = new Dictionary<string, Symbol>()
            };

            // Extract top-level symbols from __init__.spy
            foreach (var statement in module.Body)
            {
                ExtractSymbolFromStatement(statement, moduleInfo);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error parsing package {packageName}: {ex.Message}", 0, 0);
            return null;
        }

        // Create package info
        var packageInfo = new PackageInfo
        {
            Name = packageName,
            InitPath = initPath,
            Module = moduleInfo.Module,
            ExportedSymbols = new Dictionary<string, Symbol>()
        };

        // 1. Add direct symbols defined in __init__.spy
        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
        {
            packageInfo.ExportedSymbols[name] = symbol;
        }

        // 2. Extract re-exported symbols from import statements
        // For module resolution, we need the parent of the package directory
        // so that sibling packages can be found (e.g., from utils.helpers import ...)
        var packageDir = Path.GetDirectoryName(initPath);
        var searchPath = packageDir != null ? Path.GetDirectoryName(packageDir) : null;

        foreach (var statement in moduleInfo.Module.Body)
        {
            switch (statement)
            {
                case FromImportStatement fromImport:
                    ProcessFromImport(fromImport, packageInfo, searchPath, initPath);
                    break;

                case ImportStatement import:
                    ProcessImport(import, packageInfo);
                    break;
            }
        }

        _packageCache[packageName] = packageInfo;
        return packageInfo;
    }

    /// <summary>
    /// Process a "from X import Y" statement for re-exports.
    /// Makes imported symbols available at package level.
    /// </summary>
    private void ProcessFromImport(FromImportStatement fromImport, PackageInfo packageInfo,
        string? searchPath, string currentModulePath)
    {
        var importedModule = _importResolver.ResolveFromImport(fromImport, searchPath,
            currentModulePath: currentModulePath);
        if (importedModule == null)
            return;

        // Don't re-export error recovery symbols - they're placeholders for failed imports
        // and should only be used for suppressing cascading errors, not for actual exports
        if (importedModule.IsErrorRecovery)
            return;

        if (fromImport.ImportAll)
        {
            // from X import * - re-export all public symbols
            var publicSymbols = _importResolver.GetImportAllSymbols(importedModule);
            foreach (var (name, symbol) in publicSymbols)
            {
                // Only re-export if not already defined
                if (!packageInfo.ExportedSymbols.ContainsKey(name))
                {
                    packageInfo.ExportedSymbols[name] = symbol;
                    _logger.LogDebug($"  Re-exporting {name} from {fromImport.Module}");
                }
            }
        }
        else
        {
            // from X import Y, Z - re-export specific symbols
            foreach (var importAlias in fromImport.Names)
            {
                var sourceName = importAlias.Name;
                var exportName = importAlias.AsName ?? sourceName;

                if (importedModule.ExportedSymbols.TryGetValue(sourceName, out var symbol))
                {
                    // Re-export with alias if specified
                    packageInfo.ExportedSymbols[exportName] = symbol;
                    _logger.LogDebug($"  Re-exporting {sourceName} as {exportName} from {fromImport.Module}");
                }
            }
        }
    }

    /// <summary>
    /// Process a regular "import X" statement.
    /// These typically don't contribute to re-exports unless explicitly assigned.
    /// </summary>
    private void ProcessImport(ImportStatement import, PackageInfo packageInfo)
    {
        // Regular imports (import X) don't automatically re-export
        // They're used within __init__.spy but not exposed at package level
        // unless explicitly assigned to __all__ or similar (future feature)
    }

    /// <summary>
    /// Extract exported symbols from a top-level statement.
    /// Mirrors the logic in ImportResolver.ExtractExportedSymbol.
    /// </summary>
    private void ExtractSymbolFromStatement(Statement statement, ModuleInfo moduleInfo)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                var funcAccessLevel = GetAccessLevel(functionDef.Name);
                var funcSymbol = new FunctionSymbol
                {
                    Name = functionDef.Name,
                    Kind = SymbolKind.Function,
                    AccessLevel = funcAccessLevel,
                    DeclarationLine = functionDef.LineStart,
                    DeclarationColumn = functionDef.ColumnStart,
                    NameDeclarationLine = functionDef.NameLineStart,
                    NameDeclarationColumn = functionDef.NameColumnStart
                };
                moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
                break;

            case ClassDef classDef:
                var classAccessLevel = GetAccessLevel(classDef.Name);
                var classSymbol = new TypeSymbol
                {
                    Name = classDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Class,
                    AccessLevel = classAccessLevel,
                    DeclarationLine = classDef.LineStart,
                    DeclarationColumn = classDef.ColumnStart,
                    NameDeclarationLine = classDef.NameLineStart,
                    NameDeclarationColumn = classDef.NameColumnStart
                };
                moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
                break;

            case StructDef structDef:
                var structAccessLevel = GetAccessLevel(structDef.Name);
                var structSymbol = new TypeSymbol
                {
                    Name = structDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Struct,
                    AccessLevel = structAccessLevel,
                    DeclarationLine = structDef.LineStart,
                    DeclarationColumn = structDef.ColumnStart,
                    NameDeclarationLine = structDef.NameLineStart,
                    NameDeclarationColumn = structDef.NameColumnStart
                };
                moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
                break;

            case InterfaceDef interfaceDef:
                var interfaceAccessLevel = GetAccessLevel(interfaceDef.Name);
                var interfaceSymbol = new TypeSymbol
                {
                    Name = interfaceDef.Name,
                    Kind = SymbolKind.Type,
                    TypeKind = TypeKind.Interface,
                    AccessLevel = interfaceAccessLevel,
                    DeclarationLine = interfaceDef.LineStart,
                    DeclarationColumn = interfaceDef.ColumnStart,
                    NameDeclarationLine = interfaceDef.NameLineStart,
                    NameDeclarationColumn = interfaceDef.NameColumnStart
                };
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
                    NameDeclarationLine = enumDef.NameLineStart,
                    NameDeclarationColumn = enumDef.NameColumnStart
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
                        DeclarationSpan = member.Span
                    });
                }
                moduleInfo.ExportedSymbols[enumDef.Name] = enumSymbol;
                break;

            case VariableDeclaration varDecl when varDecl.IsConst:
                var constAccessLevel = GetAccessLevel(varDecl.Name);
                var constSymbol = new VariableSymbol
                {
                    Name = varDecl.Name,
                    Kind = SymbolKind.Variable,
                    IsConstant = true,
                    AccessLevel = constAccessLevel,
                    DeclarationLine = varDecl.LineStart,
                    DeclarationColumn = varDecl.ColumnStart
                };
                moduleInfo.ExportedSymbols[varDecl.Name] = constSymbol;
                break;
        }
    }

    /// <summary>
    /// Determine access level based on naming convention.
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
    /// Clear the package cache.
    /// </summary>
    public void ClearCache()
    {
        _packageCache.Clear();
    }
}

/// <summary>
/// Information about a resolved package.
/// </summary>
internal class PackageInfo
{
    /// <summary>
    /// Package name (dotted notation, e.g., "utils.math")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Full path to the __init__.spy file
    /// </summary>
    public string InitPath { get; init; } = string.Empty;

    /// <summary>
    /// The parsed Module AST from __init__.spy
    /// </summary>
    public Module Module { get; init; } = null!;

    /// <summary>
    /// All symbols exported by this package (direct + re-exported)
    /// </summary>
    public Dictionary<string, Symbol> ExportedSymbols { get; init; } = new();
}
