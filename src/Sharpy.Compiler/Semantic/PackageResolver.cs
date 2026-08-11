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
/// <remarks>
/// <para>
/// Symbol extraction is <see cref="ModuleLoader.ExtractExportedSymbol"/>'s, not a second copy of
/// it (#1364). This class used to hold a third, thinner extraction whose types carried no methods,
/// fields, properties, events, nested types or <c>IsAbstract</c>, so a type re-exported through
/// <c>__init__.spy</c> had a strictly smaller surface than the same type imported directly — the
/// #1145 Class G shape, where each extractor is fixed independently and nothing fails when they
/// drift.
/// </para>
/// <para>
/// <b>The collapse deliberately widens what a package exports.</b> The deleted switch guarded its
/// variable arm with <c>when varDecl.IsConst</c>; the shared extractor has no such guard, so a
/// non-const module-level variable in <c>__init__.spy</c> is now an export. That is the intended
/// unification, on three grounds: a plain module has always exported its non-const module-level
/// variables (the same <c>VariableDeclaration</c> arm), CPython agrees (<c>name = "hello"</c> in
/// <c>__init__.py</c> is importable as <c>from pkg import name</c> — verified against python3), and
/// Design Decision 2 of this batch is that there is one extractor. A package that wanted to export
/// only constants would need an <c>__all__</c>, which is a separate feature. Pinned by
/// <c>PackageResolverTests.ResolvePackage_NonConstVariable_IsExported</c>.
/// </para>
/// </remarks>
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
                ExportedSymbols = new ModuleExports(),
                // Without this the shared extractor would stamp DefiningModule with the raw
                // __init__.spy path; the canonical name ("pkg", "pkg.sub") is what every other
                // module carries and what codegen resolves a module-qualified reference through.
                CanonicalModuleName = _importResolver.ModuleLoader.ComputeCanonicalModuleName(initPath)
            };

            // Extract top-level symbols from __init__.spy through the SHARED extractor (#1364).
            foreach (var statement in module.Body)
            {
                _importResolver.ModuleLoader.ExtractExportedSymbol(statement, moduleInfo);
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
            ExportedSymbols = new ModuleExports()
        };

        // 1. Add direct symbols defined in __init__.spy
        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
        {
            packageInfo.ExportedSymbols.Add(name, symbol);
        }

        // 2. Extract re-exported symbols from import statements
        // For module resolution, we need the parent of the package directory
        // so that sibling packages can be found (e.g., from utils.helpers import ...)
        var packageDir = Path.GetDirectoryName(initPath);
        var searchPath = packageDir != null ? Path.GetDirectoryName(packageDir) : null;

        foreach (var statement in moduleInfo.Module.Body)
        {
            switch (statement.UnwrapDecorated())
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
                    packageInfo.ExportedSymbols.Add(name, symbol);
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
                    packageInfo.ExportedSymbols.Add(exportName, symbol);
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
    /// All symbols exported by this package (direct + re-exported). Same
    /// <see cref="ModuleExports"/> unit the module layers use, so a package's exported types stay
    /// paired with its value exports here too (#1145).
    /// </summary>
    public ModuleExports ExportedSymbols { get; init; } = new();
}
