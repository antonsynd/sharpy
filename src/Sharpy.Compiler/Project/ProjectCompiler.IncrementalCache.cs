using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Save incremental compilation caches for all successfully compiled files.
    /// </summary>
    private void SaveIncrementalCaches(ProjectConfig config)
    {
        if (_incrementalCache == null)
            return;

        var savedCount = 0;

        foreach (var (_, unit) in _projectModel!.Units)
        {
            var sourceFile = unit.FilePath;

            // Update hash for all files
            _incrementalCache.UpdateHash(sourceFile);

            // Save file cache for newly compiled files (not skipped)
            if (unit.Phase == CompilationPhase.CodeGenerated && !_filesToSkip.Contains(sourceFile))
            {
                // Extract symbols declared in this file
                var fileSymbols = ExtractFileSymbols(sourceFile);

                // Extract dependencies from the dependency graph
                var dependencies = _dependencyGraph != null
                    ? _dependencyGraph.GetDirectDependencies(sourceFile).ToList()
                    : new List<string>();

                // Merge in generator file dependencies so a generator edit
                // invalidates every file it produced code for.
                if (_generatorDependencies.TryGetValue(
                    PathNormalizer.Normalize(sourceFile), out var generatorDeps))
                {
                    foreach (var dep in generatorDeps)
                    {
                        if (!dependencies.Contains(dep, StringComparer.OrdinalIgnoreCase))
                        {
                            dependencies.Add(dep);
                        }
                    }
                }

                // Capture per-file diagnostics for warm-build replay (#1553)
                var cachedDiagnostics = unit.Diagnostics.GetAll().Select(CachedDiagnostic.From).ToList();

                _incrementalCache.SaveFileCache(
                    sourceFile,
                    fileSymbols,
                    unit.GeneratedCSharp ?? string.Empty,
                    dependencies,
                    unit.ModulePath,
                    cachedDiagnostics.Count > 0 ? cachedDiagnostics : null,
                    _projectModel!.SemanticBinding,
                    unit.Ast != null ? SemanticInfo.GetModuleLayout(unit.Ast) : null,
                    unit.FileSemanticInfo?.ReferencedClrTypeNames());

                savedCount++;
            }
        }

        // The whole-build record: written on every successful save, so it is never older than the
        // hashes and file entries beside it (#2468).
        _incrementalCache.RecordLastBuild(config, _diagnostics.Reported);

        // Persist caches to disk
        _incrementalCache.SaveAllCaches();

        if (savedCount > 0)
        {
            _logger.LogInfo($"Saved incremental cache for {savedCount} file(s)");
        }
    }

    /// <summary>
    /// The whole-build up-to-date check (#2468): when every input of this build is the last successful
    /// build's (<see cref="IncrementalCompilationCache.UpToDateBuildDiagnostics"/>), the result of
    /// replaying that build — its policy-free report through this build's project sink, every source
    /// served from the cache, its C# under the keys a compile gives it, and the output it left in place.
    /// Null when the build has to run. Nothing here loads a reference or reads a symbol.
    /// </summary>
    internal static ProjectCompilationResult? TryReplayUpToDateBuild(
        ProjectConfig config, ProjectCompilerOptions options, ICompilerLogger logger)
    {
        var cache = new IncrementalCompilationCache(config, logger, options.Features);
        if (cache.UpToDateBuildDiagnostics(config) is not { } replayed)
            return null;

        logger.LogInfo($"Up to date: every input of {config.RootNamespace} is the last build's; nothing compiled");

        var diagnostics = ProjectSinkOf(options.WarningsAsErrors,
            options.SuppressedWarnings ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxErrors);
        foreach (var cached in replayed)
            diagnostics.Add(cached.ToDiagnostic());

        var metrics = new ProjectCompilationMetrics(config.RootNamespace, config.Configuration);
        metrics.MarkUpToDate();
        var generated = new Dictionary<string, string>();
        foreach (var sourceFile in config.SourceFiles.OrderBy(f => f, StringComparer.Ordinal))
        {
            metrics.AddSkippedFile(sourceFile);
            if (cache.GetFileCache(sourceFile)?.GeneratedCSharp is { Length: > 0 } csharp)
                generated[CSharpUnitKeyOf(config, sourceFile)] = csharp;
        }

        var success = !diagnostics.HasErrors;
        return new ProjectCompilationResult
        {
            Success = success,
            Diagnostics = diagnostics,
            OutputAssemblyPath = success ? config.OutputAssemblyPath : null,
            GeneratedCSharpFiles = generated,
            Metrics = metrics,
            EffectiveFeatures = options.Features ?? FeatureFlags.None,
        };
    }

    /// <summary>
    /// Extract all symbols declared in a specific file, each once.
    /// </summary>
    /// <remarks>
    /// A project import binds the declaring file's own symbol in the importing module's scope
    /// (<c>ResolveOwnExportedSymbol</c>), so the scopes enumerate a symbol once per module that
    /// names it. Written twice, it was restored twice: the restore resolves each entry's references
    /// onto the one registered symbol, so a class's interface list doubled and the warm build failed
    /// the inheritance consistency check where the cold build compiled (#2027).
    /// </remarks>
    private List<Symbol> ExtractFileSymbols(string filePath)
    {
        var symbols = new List<Symbol>();
        var seen = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);
        var normalizedPath = PathNormalizer.Normalize(filePath);

        // Search both module scopes (where project symbols live) and the global scope
        // (builtins, single-file). Previously only searched GlobalScope, which is non-recursive
        // and missed every module-scoped symbol — the cache serialized zero symbols per file (#1309).
        foreach (var symbol in SymbolTable.GetAllModuleScopeSymbols()
            .Concat(SymbolTable.GlobalScope.GetAllSymbols()))
        {
            var symbolFilePath = GetSymbolFilePath(symbol);
            if (symbolFilePath != null &&
                string.Equals(PathNormalizer.Normalize(symbolFilePath), normalizedPath, StringComparison.OrdinalIgnoreCase)
                && seen.Add(symbol))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Get the file path where a symbol was declared.
    /// </summary>
    private static string? GetSymbolFilePath(Symbol symbol)
    {
        // Prefer the base DeclaringFilePath (populated for all symbol types since name resolution).
        // Fall back to subclass-specific properties for symbols restored from cache or created externally.
        return symbol.DeclaringFilePath
            ?? (symbol switch
            {
                TypeSymbol ts => ts.DefiningFilePath,
                ModuleSymbol ms => ms.FilePath,
                _ => null
            });
    }
}
