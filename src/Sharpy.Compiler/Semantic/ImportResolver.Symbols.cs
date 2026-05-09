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
                NameDeclarationLine = func.NameDeclarationLine,
                NameDeclarationColumn = func.NameDeclarationColumn,
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
                NameDeclarationLine = var.NameDeclarationLine,
                NameDeclarationColumn = var.NameDeclarationColumn,
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

        var reExported = new TypeSymbol
        {
            Name = effectiveName,
            Kind = originalType.Kind,
            TypeKind = originalType.TypeKind,
            AccessLevel = originalType.AccessLevel,
            IsAbstract = originalType.IsAbstract,
            TypeParameters = originalType.TypeParameters,
            Fields = originalType.Fields,
            Methods = originalType.Methods,
            MethodOverloads = originalType.MethodOverloads,
            Properties = originalType.Properties,
            OperatorMethods = originalType.OperatorMethods,
            ProtocolMethods = originalType.ProtocolMethods,
            Constructors = originalType.Constructors,
            BaseType = originalType.BaseType,
            Interfaces = originalType.Interfaces,
            ClrType = originalType.ClrType,
            UnresolvedBaseName = originalType.UnresolvedBaseName,
            UnresolvedInterfaceNames = originalType.UnresolvedInterfaceNames,
            DeclarationLine = fromImport.LineStart,
            DeclarationColumn = fromImport.ColumnStart,
            NameDeclarationLine = originalType.NameDeclarationLine,
            NameDeclarationColumn = originalType.NameDeclarationColumn,
            IsReExport = true,
            OriginalModule = fromImport.Module,
            DefiningModule = definingModule
        };

        // When the type is aliased (effectiveName != originalType.Name), store the original
        // name so code generation uses the correct C# type name (e.g., "Config" not "Cfg").
        if (!string.Equals(effectiveName, originalType.Name, StringComparison.Ordinal))
        {
            reExported.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(originalType.Name),
                OriginalName = effectiveName,
                ImportKind = ImportKind.FromImportWithAlias,
                OriginalImportName = originalType.Name
            };
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
            FunctionSymbol func => func with { Name = newName },
            TypeSymbol type => type with { Name = newName },
            VariableSymbol var => var with { Name = newName },
            ModuleSymbol mod => mod with { Name = newName },
            _ => symbol
        };
    }
}
