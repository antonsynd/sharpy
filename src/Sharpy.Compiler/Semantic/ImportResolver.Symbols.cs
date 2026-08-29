using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// ImportResolver partial class: symbol-level operations for from-imports and re-exports
/// (extracting re-exported symbols, building re-export symbols, cloning, and conflict-aware
/// symbol-table registration).
/// </summary>
internal partial class ImportResolver
{
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
                    moduleInfo.ExportedSymbols.Add(name, reExportSymbol);
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
                    moduleInfo.ExportedSymbols.Add(targetName, reExportSymbol);
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

        // For the builtins module, preserve the registry's own symbol so
        // BuiltinRegistry.IsBuiltinSymbol stays true via reference identity (#1322).
        if (string.Equals(effectiveName, originalSymbol.Name, StringComparison.Ordinal)
            && string.Equals(fromImport.Module, "builtins", StringComparison.Ordinal))
            return originalSymbol;

        var result = originalSymbol switch
        {
            FunctionSymbol func => func with
            {
                Name = effectiveName,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module,
                OriginSymbol = originalSymbol
            },
            TypeSymbol type => CreateReExportedTypeSymbol(type, fromImport, effectiveName),
            // Carry unless listed — the same shape as the function arm above (#1440). The hand-built
            // initializer this replaces restated fourteen of the twenty-one VariableSymbol facts, and
            // the seven it forgot (IsFinal, IsStatic, HasDefaultValue, Documentation,
            // DeprecationMessage, DeclarationSpan, DeclaringFilePath) were each a user-visible defect
            // on the from-import spelling alone: an imported @final field freely assignable, hover
            // with no docs, go-to-definition with nowhere to go.
            VariableSymbol var => var with
            {
                Name = effectiveName,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module,
                OriginSymbol = originalSymbol
            },
            _ => originalSymbol
        };

        return result;
    }

    /// <summary>
    /// Create a re-exported type symbol, properly tracking the DefiningModule through the re-export chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>with</c> expression restating ONLY what a re-export genuinely changes — its name, the
    /// position of the import statement that binds it, and the provenance fields. Everything else
    /// carries because a re-export IS the declaration, seen from another module.
    /// </para>
    /// <para>
    /// This inverts the default deliberately (#1440). The hand-built initializer it replaces named
    /// twenty-odd facts and dropped sixteen — <c>NestedTypes</c> (which silently un-did #1363 on this
    /// spelling), <c>Documentation</c>, <c>DeprecationMessage</c> (SPY0466 never fired),
    /// <c>IsMustUse</c> (SPY0480 never fired), <c>DeclarationSpan</c> (go-to-definition landed
    /// nowhere), <c>Events</c>, <c>IsStringEnum</c>, <c>IsDataclass</c>, the file paths and the rest.
    /// Each drop was invisible: nothing fails when a fact is simply not restated, and the same is
    /// true of every fact ADDED to <see cref="TypeSymbol"/> later. "Carry unless listed" is what
    /// makes that class of bug unrepresentable rather than merely fixed.
    /// </para>
    /// <para>
    /// Reference-typed members are ALIASED by <c>with</c>, not copied — <c>Interfaces</c> above all,
    /// and equally <c>Fields</c>/<c>Methods</c>/<c>Constructors</c>/<c>Properties</c>/
    /// <c>UnresolvedInterfaces</c> (#1403). That is the required behaviour, not a hazard: inheritance
    /// is materialized ONCE, onto whichever object <c>InheritanceResolver</c> reaches, so a defensive
    /// copy here would freeze the list at re-export time and leave the alias permanently
    /// interface-less when materialization runs afterwards. The lists are append-only during
    /// resolution and never mutated per alias, so sharing is the correctness condition.
    /// </para>
    /// <para>
    /// The result is still a NEW object, which is correct: a re-export is a distinct binding with its
    /// own name and position. Identity with the declaration is the in-source-set path's job
    /// (<c>ProjectCompiler.ResolveOwnExportedSymbol</c>, #1366/#1407/#1491), and where that applies
    /// this clone is never reached.
    /// </para>
    /// <para>
    /// <b>Mutation test</b> (performed 2026-08-14): replacing this <c>with</c> by a hand-built
    /// <c>new TypeSymbol { … }</c> restating eighteen fields reddens
    /// <c>SymbolFactMirrorConformanceTests.FactMirror_AgreesForEveryCell</c> on exactly the
    /// <c>::fromImport</c> cells of the facts the initializer forgot — <c>Documentation</c>,
    /// <c>DeprecationMessage</c>, <c>IsMustUse</c>, <c>IsDataclass</c>, <c>Events</c>,
    /// <c>Properties</c>, <c>IsStringEnum</c>. That is the guard measuring the property this
    /// expression exists for: not "the facts are right today" but "forgetting one is caught".
    /// </para>
    /// </remarks>
    private TypeSymbol CreateReExportedTypeSymbol(TypeSymbol originalType, FromImportStatement fromImport, string effectiveName)
    {
        var definingModule = originalType.DefiningModule ?? GetResolvedModulePath(fromImport) ?? fromImport.Module;

        _logger.LogDebug($"[ImportResolver] CreateReExportedTypeSymbol: {originalType.Name} -> {effectiveName}");
        _logger.LogDebug($"[ImportResolver]   Original DefiningModule: {originalType.DefiningModule ?? "null"}");
        _logger.LogDebug($"[ImportResolver]   Original IsReExport: {originalType.IsReExport}");
        _logger.LogDebug($"[ImportResolver]   New DefiningModule: {definingModule}");
        _logger.LogDebug($"[ImportResolver]   FromImport.Module: {fromImport.Module}");

        var reExported = originalType with
        {
            Name = effectiveName,
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
            _semanticBinding.SetCodeGenInfo(reExported, new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(originalType.Name),
                OriginalName = effectiveName,
                ImportKind = ImportKind.FromImportWithAlias,
                OriginalImportName = originalType.Name
            });
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

    /// <summary>
    /// Clones a symbol with a new name, used for alias registration.
    /// </summary>
    private static Symbol CloneSymbolWithName(Symbol symbol, string newName)
    {
        return symbol switch
        {
            FunctionSymbol func => func with { Name = newName, OriginSymbol = symbol },
            TypeSymbol type => type with { Name = newName, OriginSymbol = symbol },
            VariableSymbol var => var with { Name = newName, OriginSymbol = symbol },
            ModuleSymbol mod => mod with { Name = newName, OriginSymbol = symbol },
            _ => symbol
        };
    }
}
