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
        var builtinRegistry = new BuiltinRegistry(_logger);
        _symbolTableBacking = new SymbolTable(builtinRegistry);
        _semanticInfoBacking = new SemanticInfo();
        _importResolverBacking = new ImportResolver(_logger, _moduleRegistry);

        // Create SemanticBinding for storing semantic data separate from AST
        var semanticBinding = new SemanticBinding();

        // Store in ProjectModel
        _projectModel!.GlobalSymbols = SymbolTable;
        _projectModel.SemanticInfo = SemanticInfo;
        _projectModel.SemanticBinding = semanticBinding;

        // Initialize dependency graph builder and connect to import resolver
        _graphBuilderBacking = new DependencyGraphBuilder();
        ImportResolver.SetDependencyRecorder(GraphBuilder);

        // Connect SemanticBinding to import resolver for storing import data
        ImportResolver.SetSemanticBinding(semanticBinding);

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
                if (_incrementalCache.RestoreSymbols(filePath, _restoredSymbols))
                {
                    // Register the restored symbols in the symbol table
                    foreach (var symbol in _restoredSymbols.Values)
                    {
                        // Only register top-level symbols (types, functions, variables)
                        // Skip parameters and other nested symbols
                        if (symbol is TypeSymbol typeSymbol)
                        {
                            SymbolTable.TryDefine(symbol);

                            // Register CodeGenInfo to maintain dual-write consistency
                            if (typeSymbol.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(typeSymbol, typeSymbol.CodeGenInfo);
                            }

                            // Also register variable types and CodeGenInfo for fields
                            // This ensures DualWriteAssertions pass for restored symbols
                            foreach (var field in typeSymbol.Fields)
                            {
                                if (field.Type != SemanticType.Unknown)
                                {
                                    semanticBinding.SetVariableType(field, field.Type);
                                }
                                if (field.CodeGenInfo != null)
                                {
                                    semanticBinding.SetCodeGenInfo(field, field.CodeGenInfo);
                                }
                            }

                            // Register CodeGenInfo for methods
                            foreach (var method in typeSymbol.Methods)
                            {
                                if (method.CodeGenInfo != null)
                                {
                                    semanticBinding.SetCodeGenInfo(method, method.CodeGenInfo);
                                }
                            }

                            // Register CodeGenInfo for constructors
                            foreach (var ctor in typeSymbol.Constructors)
                            {
                                if (ctor.CodeGenInfo != null)
                                {
                                    semanticBinding.SetCodeGenInfo(ctor, ctor.CodeGenInfo);
                                }
                            }
                        }
                        else if (symbol is FunctionSymbol fs)
                        {
                            SymbolTable.TryDefine(symbol);

                            // Register CodeGenInfo for functions
                            if (fs.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(fs, fs.CodeGenInfo);
                            }
                        }
                        else if (symbol is VariableSymbol vs && !vs.IsParameter)
                        {
                            SymbolTable.TryDefine(symbol);

                            // Register variable type and CodeGenInfo in SemanticBinding
                            if (vs.Type != SemanticType.Unknown)
                            {
                                semanticBinding.SetVariableType(vs, vs.Type);
                            }
                            if (vs.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(vs, vs.CodeGenInfo);
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
