using Sharpy.Compiler.Semantic;
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
                var cachedDiagnostics = unit.Diagnostics.GetAll()
                    .Select(d => new CachedDiagnostic
                    {
                        Code = d.Code ?? string.Empty,
                        Message = d.Message,
                        Severity = d.Severity.ToString(),
                        Line = d.Line ?? 0,
                        Column = d.Column ?? 0,
                        FilePath = d.FilePath,
                        Phase = d.Phase.ToString(),
                        SpanStart = d.Span?.Start ?? 0,
                        SpanLength = d.Span?.Length ?? 0,
                        Data = d.Data?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                    })
                    .ToList();

                _incrementalCache.SaveFileCache(
                    sourceFile,
                    fileSymbols,
                    unit.GeneratedCSharp ?? string.Empty,
                    dependencies,
                    unit.ModulePath,
                    cachedDiagnostics.Count > 0 ? cachedDiagnostics : null);

                savedCount++;
            }
        }

        // Persist caches to disk
        _incrementalCache.SaveAllCaches();

        if (savedCount > 0)
        {
            _logger.LogInfo($"Saved incremental cache for {savedCount} file(s)");
        }
    }

    /// <summary>
    /// Extract all symbols declared in a specific file.
    /// </summary>
    private List<Symbol> ExtractFileSymbols(string filePath)
    {
        var symbols = new List<Symbol>();
        var normalizedPath = PathNormalizer.Normalize(filePath);

        // Search both module scopes (where project symbols live) and the global scope
        // (builtins, single-file). Previously only searched GlobalScope, which is non-recursive
        // and missed every module-scoped symbol — the cache serialized zero symbols per file (#1309).
        foreach (var symbol in SymbolTable.GetAllModuleScopeSymbols()
            .Concat(SymbolTable.GlobalScope.GetAllSymbols()))
        {
            var symbolFilePath = GetSymbolFilePath(symbol);
            if (symbolFilePath != null &&
                string.Equals(PathNormalizer.Normalize(symbolFilePath), normalizedPath, StringComparison.OrdinalIgnoreCase))
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
