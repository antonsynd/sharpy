using System.Collections.Immutable;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 3: Collect type declarations from all files using per-file SymbolTables.
    /// Each file gets its own SymbolTable and NameResolver during declaration collection,
    /// then results are merged into the shared SymbolTable. This enables future
    /// parallelization of name resolution.
    ///
    /// NOTE: Inheritance resolution is deferred to Phase 4b (after imports are resolved)
    /// so that imported base types are available in the symbol table.
    /// </summary>
    private void CollectTypeDeclarations(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 3: Collecting type declarations across all files");

        _perFileResolvers = new List<NameResolver>();

        foreach (var (_, unit) in _projectModel!.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Create per-file SymbolTable and NameResolver
            var fileTable = new SymbolTable(SymbolTable.BuiltinRegistry);
            var fileResolver = new NameResolver(fileTable, _logger, _projectModel!.SemanticBinding);
            // Single-file analyze (#1087) strips the entry file's path identity so its symbols
            // get a null DeclaringFilePath, matching the historical single-file contract that
            // LSP rename/hierarchy handlers rely on (they fall back to the request document URI).
            fileResolver.SetCurrentFilePath(IsNullPathEntryFile(config, unit) ? null : unit.FilePath);

            fileTable.EnterModuleScope(unit.ModulePath);
            fileResolver.SetCurrentModulePath(unit.ModulePath);
            try
            {
                fileResolver.ResolveDeclarations(unit.Ast, cancellationToken);

                unit.FileSymbolTable = fileTable;
                unit.Phase = CompilationPhase.NamesResolved;
            }
            finally
            {
                fileTable.ExitScope();
                fileResolver.SetCurrentModulePath(null);
            }

            _perFileResolvers.Add(fileResolver);

            // Merge per-file symbols into the shared SymbolTable (preserves cached symbols)
            var fileModuleScope = fileTable.GetModuleScope(unit.ModulePath);
            if (fileModuleScope != null)
            {
                SymbolTable.EnterModuleScope(unit.ModulePath);
                try
                {
                    foreach (var symbol in fileModuleScope.GetAllSymbols())
                    {
                        if (!SymbolTable.TryDefine(symbol))
                        {
                            // Carry the symbol's declaration span so the cross-file duplicate points
                            // at the redefinition, not just a bare line/column (SPY0204 parity, #1077).
                            _diagnostics.AddError(
                                $"Duplicate definition '{symbol.Name}' across files",
                                symbol.DeclarationSpan,
                                symbol.DeclarationLine, symbol.DeclarationColumn,
                                code: DiagnosticCodes.Semantic.DuplicateDefinition,
                                phase: CompilerPhase.NameResolution);
                        }
                    }

                    foreach (var (name, overloads) in fileModuleScope.GetAllFunctionOverloads())
                    {
                        SymbolTable.DefineFunctionOverloads(name, overloads);
                    }
                }
                finally
                {
                    SymbolTable.ExitScope();
                }
            }

            // Capture the module scope from the shared table
            unit.ModuleScope = SymbolTable.GetModuleScope(unit.ModulePath);
            unit.FileSymbolTable = null; // Clear per-file table after merge

            // Forward all per-file name-resolution diagnostics (errors AND warnings) wholesale so
            // their Span and all other fields survive the per-file → project merge (#1280, #1077).
            ForwardDiagnostics(fileResolver.Diagnostics.GetAll());
        }

        // Create aggregated NameResolver for inheritance resolution (Phase 4b)
        _sharedNameResolver = new NameResolver(SymbolTable, _logger, _projectModel!.SemanticBinding);
        _sharedNameResolver.AggregateTypeDefinitionsFrom(_perFileResolvers);
    }

    /// <summary>
    /// True when <paramref name="unit"/> is the entry file of a single-file analyze project
    /// whose path identity should be stripped (<see cref="ProjectConfig.NullifyEntryFilePath"/>).
    /// Its symbols and references then carry a null path so LSP handlers fall back to the request
    /// document URI — the historical single-file analyze contract (#1087).
    /// </summary>
    private static bool IsNullPathEntryFile(ProjectConfig config, Model.CompilationUnit unit)
        => config.NullifyEntryFilePath
            && config.EntryPoint != null
            && SyntheticProject.PathsEqual(unit.FilePath, config.EntryPoint);

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

        // Snapshot the diagnostic count before inheritance resolution so we forward only
        // new diagnostics (declaration diagnostics were already forwarded in Phase 3).
        var previousDiagnosticCount = _sharedNameResolver.Diagnostics.GetAll().Count;

        _sharedNameResolver.ResolveInheritance(cancellationToken);

        // Forward new inheritance diagnostics (errors + warnings) wholesale, preserving
        // spans — the old reconstruction dropped location fidelity (#1280, #1077).
        ForwardDiagnostics(_sharedNameResolver.Diagnostics.GetAll().Skip(previousDiagnosticCount));
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
                    // Unwrap suppress-decorated imports (#1124) so they register in module scope.
                    var scanned = statement.UnwrapDecorated();
                    if (scanned is ImportStatement import)
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

                            // Plain imports of stub modules can't be deferred
                            if (moduleInfo.IsStub)
                                ImportResolver.MarkFailedDeferral(moduleInfo.Path);

                            // Handle aliased imports (import x as y)
                            if (importAlias.AsName != null)
                            {
                                // Create a single ModuleSymbol with the alias name
                                var aliasedModule = new ModuleSymbol
                                {
                                    Name = importAlias.AsName,
                                    Kind = SymbolKind.Module,
                                    FilePath = moduleInfo.Path,
                                    Exports = ImportResolver.BuildExportsFor(moduleInfo),
                                    FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                                    IsNetModule = moduleInfo.IsNetModule,
                                    CanonicalModuleName = moduleInfo.CanonicalModuleName,
                                    NetNamespaceName = moduleInfo.NetNamespaceName,
                                    CSharpNamespace = moduleInfo.CSharpNamespace,
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
                                Exports = ImportResolver.BuildExportsFor(moduleInfo),
                                FunctionOverloads = new Dictionary<string, List<FunctionSymbol>>(moduleInfo.FunctionOverloads),
                                IsNetModule = moduleInfo.IsNetModule,
                                CanonicalModuleName = moduleInfo.CanonicalModuleName,
                                NetNamespaceName = moduleInfo.NetNamespaceName,
                                CSharpNamespace = moduleInfo.CSharpNamespace,
                                Documentation = moduleInfo.Module?.DocString
                                    ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name)
                            };

                            // Build nested structure from inside out. The structural parents
                            // export exactly one thing — the nested ModuleSymbol — so they hold
                            // no extraction copy and need no ownership substitution; the leaf's
                            // exports ride along on that symbol.
                            ModuleSymbol currentModule = leafModule;
                            for (int j = parts.Length - 2; j >= 0; j--)
                            {
                                var parentModule = new ModuleSymbol
                                {
                                    Name = parts[j],
                                    Kind = SymbolKind.Module,
                                    FilePath = "", // Parent modules don't have their own file
                                    Exports = new ModuleExports { { currentModule.Name, currentModule } },
                                    // Only IsNetModule needed — parent is structural; NetNamespaceName/CSharpNamespace live on the leaf
                                    IsNetModule = moduleInfo.IsNetModule
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
                    else if (scanned is FromImportStatement fromImport)
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
                            IReadOnlyDictionary<string, Symbol> symbolsToImport =
                                (IReadOnlyDictionary<string, Symbol>?)reExportedSymbols
                                ?? moduleInfo.ExportedSymbols;

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
                                // Star-imported names that displace a builtin are RECORDED here and
                                // reported where they are used, not here (#1324, C#'s CS0104 rule).
                                // This has to happen on this path as well as in ImportResolver:
                                // multi-file compilation binds star-imported symbols here and never
                                // runs the single-file one, so recording only there would cover
                                // exactly the case that cannot arise — a library and its consumer
                                // are by definition two files. Same one-site-of-two shape as #1145.
                                foreach (var (name, symbol) in symbolsToImport)
                                {
                                    if (BuiltinNameShadowing.ShadowsBuiltin(SymbolTable.BuiltinRegistry, name))
                                        SymbolTable.AmbiguousGlobImports[name] = fromImport.Module;

                                    var symbolToRegister = ResolveImportSymbol(symbol, name, sourceModuleScope);
                                    if (!SymbolTable.TryDefine(symbolToRegister))
                                    {
                                        ReportDuplicateFromImport(name, sourceModule, importedSymbolSources,
                                            fromImport, importAlias: null, unit.FilePath);
                                    }
                                    else
                                    {
                                        importedSymbolSources[name] = sourceModule;

                                        // Only register when there are actual overloads; single functions are already in the symbol table via TryDefine
                                        if (moduleInfo.FunctionOverloads.TryGetValue(name, out var overloads) && overloads.Count > 1)
                                        {
                                            SymbolTable.DefineFunctionOverloads(name, overloads);
                                        }
                                    }
                                }

                            }
                            else
                            {
                                foreach (var importAlias in fromImport.Names)
                                {
                                    var lookupName = importAlias.Name;
                                    var symbolName = importAlias.AsName ?? importAlias.Name;

                                    // Say the rebinding in the file where it takes effect (#1324).
                                    // SPY0483 fires at the DECLARATION, which lives in the library's
                                    // file — and a library and its consumer are by definition two
                                    // files, so the consumer may never see it. Emitted on this path
                                    // for the same reason the star-import recording above is: this
                                    // is the loop multi-file compilation actually runs.
                                    if (BuiltinNameShadowing.ShadowsBuiltin(SymbolTable.BuiltinRegistry, symbolName))
                                    {
                                        _diagnostics.AddWarning(
                                            $"'{symbolName}' is a builtin name; this import rebinds it "
                                            + $"in this file, so a bare '{symbolName}' here calls "
                                            + $"'{fromImport.Module}.{symbolName}' and not the builtin. "
                                            + $"The builtin stays reachable as 'builtins.{symbolName}' "
                                            + "(add 'import builtins'), or import under an alias to "
                                            + "keep both.",
                                            importAlias.Span,
                                            importAlias.LineStart,
                                            importAlias.ColumnStart,
                                            unit.FilePath,
                                            code: DiagnosticCodes.Validation.BuiltinRebornByExplicitImport,
                                            phase: CompilerPhase.ImportResolution);
                                    }

                                    if (symbolsToImport.TryGetValue(lookupName, out var symbol) ||
                                        (lookupName != symbolName && symbolsToImport.TryGetValue(symbolName, out symbol)))
                                    {
                                        var originalName = importAlias.Name;

                                        // A name imported out of the builtins module binds the
                                        // REGISTRY's own symbol, not the CLR-discovered export that
                                        // happens to implement it — identity is what every builtin
                                        // dispatch decision reads (#1322). Applied on this path as
                                        // well as in ImportResolver, for the reason the two notes
                                        // above give: this is the loop compilation actually runs.
                                        // Unaliased only — an alias binds a different name, which no
                                        // name-keyed builtin path answers to, so substituting the
                                        // registry's own (differently named) symbol would leave the
                                        // alias undefined.
                                        var registryBinding = importAlias.AsName == null
                                            ? BuiltinNameShadowing.RegistryBindingFor(
                                                SymbolTable.BuiltinRegistry, moduleInfo, lookupName)
                                            : null;
                                        if (registryBinding != null)
                                            symbol = registryBinding.Value.Symbol;

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

                                            // Only register when there are actual overloads; single functions are already in the symbol table via TryDefine
                                            var importedOverloads = registryBinding?.Overloads
                                                ?? (moduleInfo.FunctionOverloads.TryGetValue(lookupName, out var overloads)
                                                    ? overloads
                                                    : null);
                                            if (importedOverloads is { Count: > 1 })
                                            {
                                                SymbolTable.DefineFunctionOverloads(symbolName, importedOverloads);
                                            }
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

        // Detect circular dependencies — filter out cycles that can be deferred
        // (all files loaded as stubs with no missing symbols)
        var cycles = _dependencyGraph.DetectCycles();
        // Normalize deferred module paths to match the dependency graph's normalized paths
        var deferredModulesNormalized = new HashSet<string>(
            ImportResolver.ModuleLoader.DeferredCycleModules.Select(PathNormalizer.Normalize));
        var failedDeferralsNormalized = new HashSet<string>(
            ImportResolver.FailedDeferredModules.Select(PathNormalizer.Normalize));
        var nonDeferrableCycles = new List<ImmutableArray<string>>();
        _deferredCycleFiles = new HashSet<string>();

        foreach (var cycle in cycles)
        {
            // A cycle is deferrable when at least one file was loaded as a stub
            // (in a 2-file cycle A↔B, only one side gets deferred during recursive loading)
            // AND none of the cycle files had failed deferrals (missing symbols or plain imports)
            var distinctFiles = cycle.Distinct().ToList();
            bool anyDeferred = distinctFiles.Any(f => deferredModulesNormalized.Contains(f));
            bool anyFailed = distinctFiles.Any(f => failedDeferralsNormalized.Contains(f));

            if (anyDeferred && !anyFailed)
            {
                _logger.LogDebug($"[ProjectCompiler] Deferring cycle: {string.Join(" → ", distinctFiles.Select(Path.GetFileName))}");
                _deferredCycleFiles.UnionWith(distinctFiles);
            }
            else
            {
                nonDeferrableCycles.Add(cycle);
            }
        }

        if (nonDeferrableCycles.Count > 0)
        {
            foreach (var cycle in nonDeferrableCycles)
            {
                var cycleFiles = cycle.Select(Path.GetFileName).ToList();
                var cycleDescription = string.Join(" → ", cycleFiles);
                var errorMsg = $"Circular dependency detected: {cycleDescription}";
                _projectModel!.GlobalDiagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport);
                _diagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport, phase: CompilerPhase.ImportResolution);
            }
            return false;
        }

        // Compute the full set of cross-cycle symbols for the usage validator.
        // Both sides of a cycle need checking, not just the stub side.
        if (_deferredCycleFiles.Count > 0)
        {
            ComputeCrossCycleDeferredSymbols(_deferredCycleFiles);
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
    /// The plain-import twin of <see cref="ResolveImportSymbol"/>: given a name that
    /// <paramref name="moduleInfo"/> exports, returns THIS compilation's own symbol for that
    /// declaration, or null when the module is not one of ours (a .NET module, a stdlib module,
    /// any file outside the source set) — in which case the <see cref="ModuleLoader"/> extraction
    /// stands, because it is the only symbol that exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A from-import already prefers the Phase 3 original (that is what
    /// <see cref="ResolveImportSymbol"/> does), which is exactly why <c>from lib import Child</c>
    /// sees <c>Child</c>'s base chain while <c>import lib</c> + <c>lib.Child</c> did not: only the
    /// qualified spelling went through <c>Exports</c>, and those were re-extracted copies that no
    /// pass ever materialises <c>BaseType</c>/<c>Interfaces</c> onto. Re-pointing them makes both
    /// spellings name one object, so identity, inheritance and every other symbol fact agree by
    /// construction rather than by threading (#1366, #1407, #1410).
    /// </para>
    /// <para>
    /// <b>Cache-served files:</b> a file skipped by <c>--incremental</c> has no fresh
    /// <c>NameResolver</c> run, but <see cref="RestoreCachedSymbols"/> defines its symbols into
    /// the module scope during Phase 2 — before Phase 3 and well before this runs in Phase 4 — so
    /// the lookup below answers with the restored symbol on a warm build exactly as it answers
    /// with the freshly-resolved one on a cold build.
    /// </para>
    /// <para>
    /// The runtime-type guard mirrors <see cref="ResolveImportSymbol"/>'s: a module scope can hold
    /// a name the extraction produced a different KIND of symbol for (an imported re-export, say),
    /// and substituting across kinds would change what the name means rather than which object
    /// answers to it.
    /// </para>
    /// </remarks>
    private Symbol? ResolveOwnExportedSymbol(ModuleInfo moduleInfo, string name, Symbol extracted)
    {
        if (moduleInfo.IsNetModule || string.IsNullOrEmpty(moduleInfo.Path))
            return null;

        var unit = _projectModel?.GetUnit(moduleInfo.Path);
        if (unit == null)
            return null;

        var moduleScope = SymbolTable.GetModuleScope(unit.ModulePath);
        var own = moduleScope?.Lookup(name, searchParent: false);
        return own != null && own.GetType() == extracted.GetType() ? own : null;
    }

    /// <summary>
    /// Merge exports from a source module into a target module.
    /// Used to combine nested module structures when the same root is imported multiple times.
    ///
    /// The merge rule (first import wins, recursing into nested modules present on both sides)
    /// lives in <see cref="ModuleExports.MergeFrom"/>, which moves the value-position lookup and
    /// the types-only lookup together — the mirror cannot be dropped here because there is no
    /// second dictionary to forget (#1135, #1145).
    ///
    /// Internal (not private) so the merge seam can be unit-tested directly; static because it
    /// touches no instance state.
    /// </summary>
    internal static void MergeModuleExports(ModuleSymbol target, ModuleSymbol source)
        => target.Exports.MergeFrom(source.Exports, firstImportWins: true);

    /// <summary>
    /// After cycle filtering, compute all symbol names imported across cycle boundaries.
    /// The ImportResolver only tracks symbols from stubs (one side of the cycle),
    /// but we need to flag runtime usage on BOTH sides.
    /// </summary>
    private void ComputeCrossCycleDeferredSymbols(HashSet<string> deferredCycleFiles)
    {
        // Build a lookup from canonical module name to normalized file path
        var moduleNameToPath = new Dictionary<string, string>();
        foreach (var (_, unit) in _projectModel!.Units)
        {
            var normalizedPath = PathNormalizer.Normalize(unit.FilePath);
            var moduleName = unit.ModulePath;
            moduleNameToPath[moduleName] = normalizedPath;
        }

        // For each file in a deferred cycle, find its from-imports from other cycle files
        foreach (var (_, unit) in _projectModel.Units)
        {
            if (unit.Ast == null)
                continue;

            var normalizedPath = PathNormalizer.Normalize(unit.FilePath);
            if (!deferredCycleFiles.Contains(normalizedPath))
                continue;

            foreach (var statement in unit.Ast.Body)
            {
                // Unwrap suppress-decorated imports (#1124) so a cycle routed through one is seen.
                if (statement.UnwrapDecorated() is not FromImportStatement fromImport)
                    continue;

                // Check if this import targets another file in the cycle
                if (!moduleNameToPath.TryGetValue(fromImport.Module, out var targetPath))
                    continue;
                if (!deferredCycleFiles.Contains(targetPath))
                    continue;

                // This from-import crosses a cycle boundary — add all imported symbols
                if (fromImport.ImportAll)
                {
                    var reExported = _projectModel.SemanticBinding.GetReExportedSymbols(fromImport);
                    if (reExported != null)
                    {
                        foreach (var (name, _) in reExported)
                            ImportResolver.AddDeferredCycleSymbol(name);
                    }
                }
                else
                {
                    foreach (var alias in fromImport.Names)
                    {
                        var registerName = alias.AsName ?? alias.Name;
                        ImportResolver.AddDeferredCycleSymbol(registerName);
                    }
                }
            }
        }

        _logger.LogDebug($"[ProjectCompiler] Cross-cycle deferred symbols: {string.Join(", ", ImportResolver.DeferredCycleSymbols)}");
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

            // Get build order and map back to original paths.
            // Kahn's algorithm excludes files in cycles from the build order,
            // so we append deferred cycle files at the end.
            var buildOrder = _dependencyGraph.GetBuildOrder();
            var orderedFiles = buildOrder
                .Select(normalized => normalizedToOriginal.TryGetValue(normalized, out var original) ? original : null)
                .Where(path => path != null)
                .ToList();

            // Add any files not in the build order (cycle files and their dependents)
            var orderedSet = new HashSet<string>(
                orderedFiles!.Select(f => PathNormalizer.Normalize(f!)));
            foreach (var (path, _) in _projectModel.Units)
            {
                var normalized = PathNormalizer.Normalize(path);
                if (!orderedSet.Contains(normalized))
                {
                    orderedFiles.Add(path);
                    orderedSet.Add(normalized);
                }
            }

            modulesToProcess = orderedFiles!;
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
                // Create per-file SemanticInfo for isolation
                var localSemanticInfo = new SemanticInfo();
                localSemanticInfo.SetSymbolTable(SymbolTable);
                // For the single-file analyze path (#1087), the entry file's references carry a
                // null path so LSP handlers treat them as the current document — the historical
                // single-file contract, where the main file recorded no reference path. Imported
                // closure files keep their real paths for cross-file navigation.
                localSemanticInfo.CurrentFilePath =
                    IsNullPathEntryFile(config, unit) ? null : unit.FilePath;
                unit.FileSemanticInfo = localSemanticInfo;

                // Create per-file SemanticBinding for isolation
                // Inheritance data is already materialized onto Symbol properties (Phase 4b),
                // so the per-file binding only captures CodeGenInfo and VariableTypes.
                var localBinding = new SemanticBinding();

                // Type checking via shared pipeline with per-file state
                fileMetrics.StartPhase(CompilerPhaseNames.TypeChecking);
                LogPhaseStartEvent(CompilerPhaseNames.TypeChecking, unit.FilePath, unit.Ast.Body.Length);
                var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);
                var deferredSymbols = ImportResolver.DeferredCycleSymbols.Count > 0
                    ? ImportResolver.DeferredCycleSymbols
                    : null;
                var deferredFiles = _deferredCycleFiles is { Count: > 0 }
                    ? (IReadOnlySet<string>)_deferredCycleFiles
                    : null;
                // Union compilation-wide features with this file's `from __future__ import`
                // features (per-file, keyed by path on the shared ImportResolver).
                var fileFeatures = ImportResolver.GetEffectiveFeatures(_features, unit.FilePath);
                // Reject uses of constructs gated behind experimental features that are not
                // enabled, before type resolution runs. No-op until a gated construct exists.
                var gateDiagnostics = new DiagnosticBag();
                compilationPipeline.CheckFeatureGates(
                    unit.Ast, unit.FilePath, fileFeatures, gateDiagnostics);
                if (gateDiagnostics.GetAll().Count > 0)
                {
                    unit.Diagnostics.Merge(gateDiagnostics);
                    _diagnostics.Merge(gateDiagnostics);
                }
                // Advance the project bag's phase high-water mark to TypeChecking for the whole
                // type-check window. The ICE handler reads _diagnostics.LastEnteredPhase; before
                // the first MergeWithPhase runs (below) the mark is still name/import resolution,
                // so a crash in the first file's type check would otherwise be mis-attributed
                // (#1083).
                using var typeCheckPhaseScope = _diagnostics.BeginPhaseScope(CompilerPhase.TypeChecking);
                var typeCheckFilePath = IsNullPathEntryFile(config, unit) ? null : unit.FilePath;
                var typeCheckResult = compilationPipeline.TypeCheck(
                    unit.Ast, typeCheckFilePath, isEntryPoint, _maxErrors, _diagnostics,
                    computeCodeGenInfo: config.UsePrecomputedCodeGenInfo,
                    cancellationToken: cancellationToken,
                    fileSemanticInfo: localSemanticInfo,
                    fileSemanticBinding: localBinding,
                    deferredCycleSymbols: deferredSymbols,
                    deferredCycleFiles: deferredFiles,
                    moduleRegistry: _moduleRegistry,
                    features: fileFeatures);
                var typeChecker = typeCheckResult.TypeChecker;

                if (typeCheckResult.Aborted)
                {
                    // End the Type Checking phase even on error for consistent metrics
                    fileMetrics.EndPhase();
                    LogPhaseEndEvent(fileMetrics, unit.FilePath, typeChecker.Diagnostics.ErrorCount);

                    // Capture artifact counts even on error paths for better observability
                    fileMetrics.SymbolCount = SymbolTable.GlobalScope.GetAllSymbols().Count();
                    if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                    {
                        fileMetrics.SetValidatorTimes(errorValidatorDict);
                    }
                    fileMetrics.DiagnosticCount = unit.Diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;

                    // Preserve all accumulated diagnostics from the type checker
                    MergeWithPhase(_diagnostics, typeChecker.Diagnostics, CompilerPhase.TypeChecking);
                    unit.Phase = CompilationPhase.Failed;
                    continue;
                }
                fileMetrics.EndPhase();
                LogPhaseEndEvent(fileMetrics, unit.FilePath, typeChecker.Diagnostics.ErrorCount);

                // Capture per-validator timing for performance analysis
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
                {
                    fileMetrics.SetValidatorTimes(validatorDict);
                }

                // Merge all type checking diagnostics to both unit and project level
                MergeWithPhase(unit.Diagnostics, typeChecker.Diagnostics, CompilerPhase.TypeChecking);
                MergeWithPhase(_diagnostics, typeChecker.Diagnostics, CompilerPhase.TypeChecking);

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
                    CompilerInvariants.AssertPostTypeChecking(localSemanticInfo, typeChecker.Diagnostics);
                }

                // Merge per-file state into shared project-level instances
                SemanticInfo.MergeFrom(localSemanticInfo);
                _projectModel!.SemanticBinding.MergeFrom(localBinding);

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
