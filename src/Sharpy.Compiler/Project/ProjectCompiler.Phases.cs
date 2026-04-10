using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 3: Collect type declarations from all files
    /// This is a preliminary pass that registers type names in the symbol table
    /// without resolving inheritance or members yet. This enables cross-file type references.
    ///
    /// IMPORTANT: We use a SINGLE NameResolver instance across all files so that the
    /// _classDefs, _structDefs, and _interfaceDefs lists are populated with ALL type
    /// definitions before resolving inheritance. This is critical for cross-module
    /// inheritance to work correctly.
    ///
    /// NOTE: Inheritance resolution is deferred to Phase 4b (after imports are resolved)
    /// so that imported base types are available in the symbol table.
    /// </summary>
    private void CollectTypeDeclarations(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 3: Collecting type declarations across all files");

        // Create a SINGLE NameResolver for ALL files to preserve type definition lists
        // across files for correct inheritance resolution
        _sharedNameResolver = new NameResolver(SymbolTable, _logger, _projectModel!.SemanticBinding);

        // Collect all type declarations (shells only)
        foreach (var (_, unit) in _projectModel!.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Set current file path so types know which file they're defined in
            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            _sharedNameResolver.SetCurrentFilePath(unit.FilePath);

            // Enter per-module scope so each file's declarations are isolated
            SymbolTable.EnterModuleScope(unit.ModulePath);
            _sharedNameResolver.SetCurrentModulePath(unit.ModulePath);
            try
            {
                // Only collect declarations - don't resolve inheritance yet
                // The NameResolver.ResolveDeclarations() method registers type names
                // and stores ClassDef/StructDef/InterfaceDef in internal lists
                _sharedNameResolver.ResolveDeclarations(unit.Ast, cancellationToken);

                // Capture the module scope on the CompilationUnit for later phases
                unit.ModuleScope = SymbolTable.CurrentScope;

                // Update phase
                unit.Phase = CompilationPhase.NamesResolved;
            }
            finally
            {
                // Exit module scope and clear module path
                SymbolTable.ExitScope();
                _sharedNameResolver.SetCurrentModulePath(null);
            }
        }

        // NOTE: Inheritance resolution is now done in ResolveInheritanceRelationships()
        // after imports are resolved (Phase 4b)

        // Collect declaration errors (inheritance errors will be collected in Phase 4b)
        if (_sharedNameResolver.Diagnostics.HasErrors)
        {
            foreach (var error in _sharedNameResolver.Diagnostics.GetErrors())
            {
                _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code);
                _diagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code, phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Phase 4b: Resolve inheritance relationships
    /// This is called AFTER imports are resolved so that imported base types
    /// are available in the symbol table for cross-module inheritance.
    /// </summary>
    private void ResolveInheritanceRelationships(CancellationToken cancellationToken = default)
    {
        if (_sharedNameResolver == null)
            return;

        _logger.LogInfo("Phase 4b: Resolving inheritance across all files");

        // Track previous error count so we only collect new inheritance errors
        // (declaration errors were already collected in Phase 3)
        var previousErrorCount = _sharedNameResolver.Diagnostics.ErrorCount;

        _sharedNameResolver.ResolveInheritance(cancellationToken);

        // Collect only new inheritance errors (skip already collected declaration errors)
        var newErrors = _sharedNameResolver.Diagnostics.GetErrors().Skip(previousErrorCount);
        foreach (var error in newErrors)
        {
            _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code);
            _diagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code, phase: CompilerPhase.NameResolution);
        }
    }

    /// <summary>
    /// Phase 4: Resolve imports and build symbol table with imported symbols
    /// </summary>
    private bool ResolveImports(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 4: Resolving imports and building symbol table");

        // Resolve imports for each module
        foreach (var (_, unit) in _projectModel!.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Enter per-module scope so imported symbols register in the correct scope
            SymbolTable.EnterModuleScope(unit.ModulePath);
            // Track from-imported symbol sources per-file for duplicate detection (#514)
            var importedSymbolSources = new Dictionary<string, string>();
            try
            {

                foreach (var statement in unit.Ast.Body)
                {
                    if (statement is ImportStatement import)
                    {
                        var modules = ImportResolver.ResolveImport(import, config.ProjectDirectory,
                            currentModulePath: unit.FilePath, cancellationToken: cancellationToken);

                        // Match each resolved module with its import alias to get the correct name/alias
                        for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                        {
                            var importAlias = import.Names[i];
                            var moduleInfo = modules[i];

                            // Skip failed imports (null entries maintain positional alignment)
                            if (moduleInfo == null)
                                continue;

                            // Handle aliased imports (import x as y)
                            if (importAlias.AsName != null)
                            {
                                // Create a single ModuleSymbol with the alias name
                                var aliasedModule = new ModuleSymbol
                                {
                                    Name = importAlias.AsName,
                                    Kind = SymbolKind.Module,
                                    FilePath = moduleInfo.Path,
                                    Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols),
                                    FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                                    CanonicalModuleName = moduleInfo.CanonicalModuleName,
                                    Documentation = moduleInfo.Module?.DocString
                                        ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name)
                                };
                                SymbolTable.TryDefine(aliasedModule);
                                continue;
                            }

                            // Handle non-aliased imports by building nested module structure
                            // For "import lib.math", we need lib -> math -> (exports)
                            var parts = importAlias.Name.Split('.');

                            // Create the leaf module with actual exports
                            var leafModule = new ModuleSymbol
                            {
                                Name = parts[^1], // Last part (e.g., "math")
                                Kind = SymbolKind.Module,
                                FilePath = moduleInfo.Path,
                                Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols),
                                FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                                CanonicalModuleName = moduleInfo.CanonicalModuleName,
                                Documentation = moduleInfo.Module?.DocString
                                    ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name)
                            };

                            // Build nested structure from inside out
                            ModuleSymbol currentModule = leafModule;
                            for (int j = parts.Length - 2; j >= 0; j--)
                            {
                                var parentModule = new ModuleSymbol
                                {
                                    Name = parts[j],
                                    Kind = SymbolKind.Module,
                                    FilePath = "", // Parent modules don't have their own file
                                    Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } }
                                };
                                currentModule = parentModule;
                            }

                            // Register the root module (or merge with existing if it exists)
                            var rootName = parts[0];
                            var existingSymbol = SymbolTable.Lookup(rootName, searchParents: false);
                            if (existingSymbol is ModuleSymbol existingModule)
                            {
                                // Merge: add the new nested exports to the existing module
                                MergeModuleExports(existingModule, currentModule);
                            }
                            else
                            {
                                SymbolTable.TryDefine(currentModule);
                            }
                        }
                    }
                    else if (statement is FromImportStatement fromImport)
                    {
                        var moduleInfo = ImportResolver.ResolveFromImport(fromImport, config.ProjectDirectory,
                            currentModulePath: unit.FilePath, cancellationToken: cancellationToken);
                        if (moduleInfo != null)
                        {
                            // Use ReExportedSymbols which have DefiningModule set for cross-module type references
                            // This is populated by ImportResolver.ResolveFromImport via CreateReExportSymbol
                            // Check SemanticBinding first, then fall back to AST property for backward compatibility
                            var reExportedSymbols = _projectModel!.SemanticBinding.GetReExportedSymbols(fromImport)
                                                    ?? fromImport.ReExportedSymbols;
                            var symbolsToImport = reExportedSymbols ?? moduleInfo.ExportedSymbols;

                            // For project-internal from-imports of TYPE symbols, prefer the Phase 3
                            // original over the re-exported copy. This ensures all modules reference
                            // the same TypeSymbol, so inheritance info set in Phase 4b is visible
                            // everywhere. Function symbols use re-exported copies because the TypeChecker
                            // updates them via record `with` expressions that create new instances.
                            var sourceModuleScope = SymbolTable.GetModuleScope(fromImport.Module);
                            var sourceModule = moduleInfo.CanonicalModuleName ?? fromImport.Module;

                            // Add specific imported symbols (skip if already defined from project files)
                            if (fromImport.ImportAll)
                            {
                                foreach (var (name, symbol) in symbolsToImport)
                                {
                                    var symbolToRegister = ResolveImportSymbol(symbol, name, sourceModuleScope);
                                    if (!SymbolTable.TryDefine(symbolToRegister))
                                    {
                                        ReportDuplicateFromImport(name, sourceModule, importedSymbolSources,
                                            fromImport, importAlias: null, unit.FilePath);
                                    }
                                    else
                                    {
                                        importedSymbolSources[name] = sourceModule;
                                    }
                                }
                            }
                            else
                            {
                                foreach (var importAlias in fromImport.Names)
                                {
                                    var symbolName = importAlias.AsName ?? importAlias.Name;
                                    if (symbolsToImport.TryGetValue(symbolName, out var symbol))
                                    {
                                        var originalName = importAlias.Name;
                                        var symbolToRegister = importAlias.AsName == null
                                            ? ResolveImportSymbol(symbol, originalName, sourceModuleScope)
                                            : symbol;
                                        if (!SymbolTable.TryDefine(symbolToRegister))
                                        {
                                            ReportDuplicateFromImport(symbolName, sourceModule, importedSymbolSources,
                                                fromImport, importAlias, unit.FilePath);
                                        }
                                        else
                                        {
                                            importedSymbolSources[symbolName] = sourceModule;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }
            finally
            {
                // Exit module scope after processing this file's imports
                SymbolTable.ExitScope();
            }
        }

        // Build the dependency graph after all imports are resolved
        _dependencyGraph = GraphBuilder.Build();

        // Store in ProjectModel
        _projectModel!.DependencyGraph = _dependencyGraph;

        // Detect circular dependencies first - this is more specific than generic import errors
        var cycles = _dependencyGraph.DetectCycles();
        if (cycles.Count > 0)
        {
            foreach (var cycle in cycles)
            {
                var cycleFiles = cycle.Select(Path.GetFileName).ToList();
                var cycleDescription = string.Join(" → ", cycleFiles);
                var errorMsg = $"Circular dependency detected: {cycleDescription}";
                _projectModel!.GlobalDiagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport);
                _diagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport, phase: CompilerPhase.ImportResolution);
            }
            // Don't add import resolver errors when we have circular dependencies
            // as they would be redundant/confusing (e.g., "module not found" errors
            // that are caused by the circular import)
            return false;
        }

        // Merge all import diagnostics (errors + warnings) so they appear in the
        // final result. Continue to type checking even if imports failed, so users
        // see the full picture (import errors + type errors) — matching the
        // single-file Compiler.Compile() behavior.
        foreach (var diag in ImportResolver.Diagnostics.GetAll())
        {
            if (diag.IsError)
            {
                _projectModel!.GlobalDiagnostics.AddError(diag.Message, code: diag.Code);
                _diagnostics.AddError(diag.Message, diag.Line, diag.Column, code: diag.Code, phase: CompilerPhase.ImportResolution);
            }
            else if (diag.IsWarning)
            {
                _projectModel!.GlobalDiagnostics.AddWarning(diag.Message, code: diag.Code);
                _diagnostics.AddWarning(diag.Message, diag.Line, diag.Column, code: diag.Code, phase: CompilerPhase.ImportResolution);
            }
        }

        // Transfer root cause identifiers from import resolution to project diagnostics
        // so TypeChecker can suppress cascading errors for failed imports
        foreach (var rootCause in ImportResolver.Diagnostics.GetRootCauses())
        {
            _diagnostics.MarkAsRootCause(rootCause);
        }

        // Continue to type checking even with non-circular import errors.
        // Missing imports produce Unknown types, which prevents cascading errors
        // in the type checker (UnknownType.IsAssignableTo returns true).
        return true;
    }

    /// <summary>
    /// Reports a duplicate from-import error if the symbol was previously imported from a different module.
    /// Same-module re-imports (idempotent) are silently skipped.
    /// Uses shared detection logic from <see cref="ImportResolver.FindDuplicateFromImportSource"/>.
    /// </summary>
    private void ReportDuplicateFromImport(
        string registerName,
        string sourceModule,
        Dictionary<string, string> importedSources,
        FromImportStatement fromImport,
        ImportAlias? importAlias,
        string filePath)
    {
        var existingModule = ImportResolver.FindDuplicateFromImportSource(
            registerName, sourceModule, importedSources);
        if (existingModule != null)
        {
            var line = importAlias?.LineStart ?? fromImport.LineStart;
            var column = importAlias?.ColumnStart ?? fromImport.ColumnStart;
            var message = $"'{registerName}' is already imported from '{existingModule}' (in {Path.GetFileName(filePath)})";
            _projectModel!.GlobalDiagnostics.AddError(message, code: DiagnosticCodes.Semantic.DuplicateDefinition);
            _diagnostics.AddError(message, line, column,
                code: DiagnosticCodes.Semantic.DuplicateDefinition, phase: CompilerPhase.ImportResolution);
        }
    }

    /// <summary>
    /// For project-internal from-imports, prefer the Phase 3 original symbol over the
    /// re-exported copy created by ImportResolver. This ensures all modules reference the
    /// same Symbol instance, so mutations during later phases (e.g., inheritance resolution
    /// in Phase 4b, return type resolution in Phase 5) are visible everywhere.
    ///
    /// First tries the source module scope. If not found there (e.g., transitive imports
    /// where the source module hasn't been processed yet), searches all module scopes for
    /// a matching Phase 3 declaration.
    /// </summary>
    private Symbol ResolveImportSymbol(Symbol reExported, string originalName, Scope? sourceModuleScope)
    {
        // Try direct source module scope first
        if (sourceModuleScope != null)
        {
            var original = sourceModuleScope.Lookup(originalName, searchParent: false);
            if (original != null)
                return original;
        }

        // For transitive imports, the source module may not have been processed yet.
        // Search all module scopes for the original Phase 3 declaration.
        if (_projectModel != null)
        {
            foreach (var (_, unit) in _projectModel.Units)
            {
                var moduleScope = SymbolTable.GetModuleScope(unit.ModulePath);
                if (moduleScope != null)
                {
                    var original = moduleScope.Lookup(originalName, searchParent: false);
                    if (original != null && original.GetType() == reExported.GetType())
                        return original;
                }
            }
        }

        return reExported;
    }

    /// <summary>
    /// Merge exports from a source module into a target module.
    /// Used to combine nested module structures when the same root is imported multiple times.
    /// </summary>
    private void MergeModuleExports(ModuleSymbol target, ModuleSymbol source)
    {
        foreach (var (name, symbol) in source.Exports)
        {
            if (target.Exports.TryGetValue(name, out var existing))
            {
                // If both are modules, merge recursively
                if (existing is ModuleSymbol existingModule && symbol is ModuleSymbol sourceModule)
                {
                    MergeModuleExports(existingModule, sourceModule);
                }
                // Otherwise, the existing symbol takes precedence (first import wins)
            }
            else
            {
                target.Exports[name] = symbol;
            }
        }
    }

    /// <summary>
    /// Phase 5: Perform semantic analysis (type checking) on all modules
    /// </summary>
    private bool PerformSemanticAnalysis(FileCompilationPipeline compilationPipeline, ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 5: Semantic Analysis");

        // Process modules in dependency order (dependencies before dependents)
        // This ensures proper symbol resolution across modules
        IEnumerable<string> modulesToProcess;
        if (_dependencyGraph != null)
        {
            // Build a mapping from normalized paths to original paths
            var normalizedToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _projectModel!.Units.Keys)
            {
                var normalized = PathNormalizer.Normalize(path);
                normalizedToOriginal[normalized] = path;
            }

            // Get build order and map back to original paths
            var buildOrder = _dependencyGraph.GetBuildOrder();
            modulesToProcess = buildOrder
                .Select(normalized => normalizedToOriginal.TryGetValue(normalized, out var original) ? original : null)
                .Where(path => path != null)!;
        }
        else
        {
            modulesToProcess = _projectModel!.Units.Keys;
        }

        foreach (var sourceFile in modulesToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var unit = _projectModel!.GetUnit(sourceFile);
            if (unit == null || unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Get the file metrics we created during parsing
            var fileMetrics = unit.Metrics;
            if (fileMetrics == null)
                continue;

            // Enter per-module scope so type checking resolves symbols from the correct scope
            SymbolTable.EnterModuleScope(unit.ModulePath);
            try
            {
                // Type checking via shared pipeline
                fileMetrics.StartPhase("Type Checking");
                var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);
                var typeCheckResult = compilationPipeline.TypeCheck(
                    unit.Ast, unit.FilePath, isEntryPoint, _maxErrors, _diagnostics,
                    computeCodeGenInfo: config.UsePrecomputedCodeGenInfo,
                    cancellationToken: cancellationToken);
                var typeChecker = typeCheckResult.TypeChecker;

                if (typeCheckResult.Aborted)
                {
                    // End the Type Checking phase even on error for consistent metrics
                    fileMetrics.EndPhase();

                    // Capture artifact counts even on error paths for better observability
                    fileMetrics.SymbolCount = SymbolTable.GlobalScope.GetAllSymbols().Count();
                    if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                    {
                        fileMetrics.SetValidatorTimes(errorValidatorDict);
                    }
                    fileMetrics.DiagnosticCount = unit.Diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;

                    // Preserve all accumulated diagnostics from the type checker
                    _diagnostics.Merge(typeChecker.Diagnostics);
                    unit.Phase = CompilationPhase.Failed;
                    continue;
                }
                fileMetrics.EndPhase();

                // Capture per-validator timing for performance analysis
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
                {
                    fileMetrics.SetValidatorTimes(validatorDict);
                }

                // Merge all type checking diagnostics to both unit and project level
                unit.Diagnostics.Merge(typeChecker.Diagnostics);
                _diagnostics.Merge(typeChecker.Diagnostics);

                // Capture per-file artifact counts
                fileMetrics.DiagnosticCount = unit.Diagnostics.GetAll().Count;
                fileMetrics.SymbolCount = SymbolTable.GlobalScope.GetAllSymbols().Count();

                if (typeChecker.Diagnostics.HasErrors)
                {
                    unit.Phase = CompilationPhase.Failed;
                }
                else
                {
                    unit.Phase = CompilationPhase.TypeChecked;
                    CompilerInvariants.AssertPostTypeChecking(SemanticInfo, typeChecker.Diagnostics);
                }

                // Log per-file semantic analysis metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug))
                {
                    _logger.LogDebug($"Analyzed {Path.GetFileName(unit.FilePath)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }
            }
            finally
            {
                SymbolTable.ExitScope();
            }
        }

        return !_diagnostics.HasErrors;
    }
}
