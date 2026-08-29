using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 2: Initialize shared state (symbol table, semantic info)
    /// </summary>
    private void InitializeSharedState()
    {
        var builtinRegistry = _sharedBuiltinRegistry ?? new BuiltinRegistry(_logger);
        _symbolTableBacking = new SymbolTable(builtinRegistry);
        _semanticInfoBacking = new SemanticInfo();
        _semanticInfoBacking.SetSymbolTable(_symbolTableBacking);
        // Create SemanticBinding for storing semantic data separate from AST
        var semanticBinding = new SemanticBinding();

        // Store in ProjectModel
        _projectModel!.GlobalSymbols = SymbolTable;
        _projectModel.SemanticInfo = SemanticInfo;
        _projectModel.SemanticBinding = semanticBinding;

        // Initialize dependency graph builder
        _graphBuilderBacking = new DependencyGraphBuilder();

        // Create import resolver with all dependencies injected via constructor
        _importResolverBacking = new ImportResolver(_logger, _moduleRegistry,
            semanticBinding: semanticBinding, dependencyRecorder: GraphBuilder);
        // An import of a file this compilation owns must export THIS compilation's symbols, not
        // ModuleLoader's re-extraction of them (#1366, #1407, #1410).
        _importResolverBacking.OwnSymbolResolver = ResolveOwnExportedSymbol;
        // …and the overload list beside it, which is a second channel with its own dictionary and
        // was left holding extractions when the single-symbol channel was re-pointed (#1491).
        _importResolverBacking.OwnOverloadResolver = ResolveOwnExportedOverloads;

        // Register all parsed files in the dependency graph
        foreach (var sourceFile in _projectModel!.Units.Keys)
        {
            GraphBuilder.AddFile(sourceFile);
        }

        // Restore cached symbols for skipped files (incremental compilation)
        if (_incremental && _incrementalCache != null && _filesToSkip.Count > 0)
        {
            RestoreCachedSymbols();
        }
    }

    /// <summary>
    /// Restore symbols from cache for files that were skipped during incremental compilation.
    /// </summary>
    private void RestoreCachedSymbols()
    {
        if (_incrementalCache == null)
            return;

        var semanticBinding = _projectModel!.SemanticBinding;
        var restoredCount = 0;

        foreach (var filePath in _filesToSkip)
        {
            // Enter the file's module scope so restored symbols register in the correct scope
            var unit = _projectModel!.GetUnit(filePath);
            if (unit != null)
                SymbolTable.EnterModuleScope(unit.ModulePath);

            try
            {
                // Snapshot keys before restoring this file's symbols so we only
                // define the newly-added ones — the old code iterated the accumulated
                // _restoredSymbols.Values, re-TryDefine-ing earlier files' symbols
                // into every later file's module scope (#1309).
                var keysBefore = new HashSet<string>(_restoredSymbols.Keys);
                if (_incrementalCache.RestoreSymbols(filePath, _restoredSymbols, semanticBinding))
                {
                    var newSymbols = _restoredSymbols
                        .Where(kv => !keysBefore.Contains(kv.Key))
                        .Select(kv => kv.Value)
                        .ToList();
                    foreach (var symbol in newSymbols)
                    {
                        if (symbol is TypeSymbol typeSymbol)
                        {
                            SymbolTable.TryDefine(symbol);

                            // Write resolved inheritance into SemanticBinding so Phase 4c's
                            // MaterializeInheritance handles it — restore runs at Phase 2,
                            // freeze at 4c, so this is pre-freeze and safe (#1309).
                            if (typeSymbol.BaseType != null)
                            {
                                semanticBinding.SetBaseType(typeSymbol, typeSymbol.BaseType);
                                if (typeSymbol.BaseTypeRef != null)
                                {
                                    semanticBinding.SetBaseTypeReference(typeSymbol, typeSymbol.BaseTypeRef);
                                }
                            }
                            foreach (var iface in typeSymbol.Interfaces)
                            {
                                semanticBinding.AddInterface(typeSymbol, iface.Definition);
                            }

                            // Register variable types for fields
                            foreach (var field in typeSymbol.Fields)
                            {
                                if (field.Type != SemanticType.Unknown)
                                {
                                    semanticBinding.SetVariableType(field, field.Type);
                                }
                            }
                        }
                        else if (symbol is FunctionSymbol)
                        {
                            SymbolTable.TryDefine(symbol);
                        }
                        else if (symbol is VariableSymbol vs && !vs.IsParameter)
                        {
                            SymbolTable.TryDefine(symbol);

                            // Register variable type in SemanticBinding
                            if (vs.Type != SemanticType.Unknown)
                            {
                                semanticBinding.SetVariableType(vs, vs.Type);
                            }
                        }
                    }
                    restoredCount++;
                }
            }
            finally
            {
                if (unit != null)
                    SymbolTable.ExitScope();
            }
        }

        if (restoredCount > 0)
        {
            _logger.LogInfo($"Restored symbols from {restoredCount} cached file(s)");
        }
    }
}
