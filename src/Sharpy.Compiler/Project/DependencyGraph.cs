using System.Collections.Immutable;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Represents the dependency graph of source files in a Sharpy project.
/// This is an immutable structure that provides queries for build ordering,
/// cycle detection, affected file analysis, and parallel compilation planning.
/// </summary>
/// <remarks>
/// <para>
/// The dependency graph tracks file-level dependencies discovered during import resolution.
/// Each edge in the graph represents an import statement where one file depends on another.
/// </para>
/// <para>
/// This class is immutable once constructed. Use <see cref="DependencyGraphBuilder"/> to
/// create instances during compilation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Building a graph
/// var builder = new DependencyGraphBuilder();
/// builder.AddDependency("main.spy", "utils.spy");
/// builder.AddDependency("main.spy", "models.spy");
/// builder.AddDependency("models.spy", "utils.spy");
/// var graph = builder.Build();
///
/// // Query build order (dependencies first)
/// var buildOrder = graph.GetBuildOrder(); // [utils.spy, models.spy, main.spy]
///
/// // Find affected files when a file changes
/// var affected = graph.GetAffectedFiles("utils.spy"); // [utils.spy, models.spy, main.spy]
///
/// // Get groups for parallel compilation
/// var groups = graph.GetParallelizableGroups(); // [{utils.spy}, {models.spy}, {main.spy}]
/// </code>
/// </example>
public class DependencyGraph
{
    private static readonly ImmutableHashSet<string> EmptySet = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Gets the forward dependency map: file → files it depends on (imports).
    /// </summary>
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> FileDependencies { get; }

    /// <summary>
    /// Gets the reverse dependency map: file → files that depend on it (dependents).
    /// </summary>
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> ReverseDependencies { get; }

    /// <summary>
    /// Gets all files in the dependency graph.
    /// </summary>
    public IReadOnlySet<string> AllFiles { get; }

    /// <summary>
    /// Gets the optional file content hashes for staleness detection.
    /// Null if hashes were not provided during construction.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FileHashes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyGraph"/> class.
    /// </summary>
    /// <param name="fileDependencies">The forward dependency map (file → its dependencies).</param>
    /// <param name="fileHashes">Optional map of file paths to content hashes.</param>
    /// <exception cref="ArgumentNullException">Thrown when fileDependencies is null.</exception>
    public DependencyGraph(
        IReadOnlyDictionary<string, ImmutableHashSet<string>> fileDependencies,
        IReadOnlyDictionary<string, string>? fileHashes = null)
    {
        ArgumentNullException.ThrowIfNull(fileDependencies);

        // Normalize paths and create immutable copies
        var normalizedDeps = new Dictionary<string, ImmutableHashSet<string>>();
        var allFiles = new HashSet<string>();

        foreach (var (file, deps) in fileDependencies)
        {
            var normalizedFile = NormalizePath(file);
            var normalizedDepSet = deps.Select(NormalizePath).ToImmutableHashSet();
            normalizedDeps[normalizedFile] = normalizedDepSet;
            allFiles.Add(normalizedFile);
            allFiles.UnionWith(normalizedDepSet);
        }

        // Ensure all files have an entry (even if empty dependencies)
        foreach (var file in allFiles)
        {
            if (!normalizedDeps.ContainsKey(file))
            {
                normalizedDeps[file] = EmptySet;
            }
        }

        FileDependencies = normalizedDeps;
        AllFiles = allFiles.ToImmutableHashSet();

        // Build reverse dependencies
        var reverseDeps = new Dictionary<string, ImmutableHashSet<string>.Builder>();
        foreach (var file in allFiles)
        {
            reverseDeps[file] = ImmutableHashSet.CreateBuilder<string>();
        }

        foreach (var (file, deps) in FileDependencies)
        {
            foreach (var dep in deps)
            {
                reverseDeps[dep].Add(file);
            }
        }

        ReverseDependencies = reverseDeps.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToImmutable());

        // Handle file hashes
        if (fileHashes != null)
        {
            FileHashes = fileHashes
                .ToDictionary(kvp => NormalizePath(kvp.Key), kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Gets the direct dependencies of the specified file.
    /// </summary>
    /// <param name="filePath">The path of the file to query.</param>
    /// <returns>
    /// The set of files that the specified file directly depends on,
    /// or an empty set if the file is not in the graph.
    /// </returns>
    public ImmutableHashSet<string> GetDirectDependencies(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return FileDependencies.TryGetValue(normalized, out var deps) ? deps : EmptySet;
    }

    /// <summary>
    /// Gets the direct dependents of the specified file (files that depend on it).
    /// </summary>
    /// <param name="filePath">The path of the file to query.</param>
    /// <returns>
    /// The set of files that directly depend on the specified file,
    /// or an empty set if the file is not in the graph.
    /// </returns>
    public ImmutableHashSet<string> GetDirectDependents(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return ReverseDependencies.TryGetValue(normalized, out var deps) ? deps : EmptySet;
    }

    /// <summary>
    /// Computes a valid build order for all files in the graph.
    /// Files with no dependencies appear first, followed by files that depend on them.
    /// </summary>
    /// <returns>
    /// A list of file paths in topological order (dependencies before dependents).
    /// Returns an empty list if the graph is empty.
    /// </returns>
    /// <remarks>
    /// This method uses Kahn's algorithm for topological sorting.
    /// If the graph contains cycles, some files will not appear in the result.
    /// Use <see cref="DetectCycles"/> to check for cycles before calling this method.
    /// </remarks>
    public IReadOnlyList<string> GetBuildOrder()
    {
        if (AllFiles.Count == 0)
        {
            return Array.Empty<string>();
        }

        // Kahn's algorithm for topological sort
        var result = new List<string>();
        var inDegree = new Dictionary<string, int>();
        var queue = new Queue<string>();

        // Initialize in-degree for all files
        foreach (var file in AllFiles)
        {
            inDegree[file] = FileDependencies.TryGetValue(file, out var deps) ? deps.Count : 0;
        }

        // Find all files with no dependencies (in-degree = 0)
        foreach (var (file, degree) in inDegree)
        {
            if (degree == 0)
            {
                queue.Enqueue(file);
            }
        }

        // Process files in order
        while (queue.Count > 0)
        {
            var file = queue.Dequeue();
            result.Add(file);

            // Reduce in-degree of all dependents
            foreach (var dependent in GetDirectDependents(file))
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Detects all cycles in the dependency graph.
    /// </summary>
    /// <returns>
    /// A list of cycles, where each cycle is an array of file paths
    /// representing the import chain. Returns an empty list if no cycles exist.
    /// </returns>
    /// <remarks>
    /// This method uses depth-first search with path tracking to find cycles.
    /// Each returned cycle shows the path of imports that forms a circular dependency.
    /// </remarks>
    public IReadOnlyList<ImmutableArray<string>> DetectCycles()
    {
        var cycles = new List<ImmutableArray<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var file in AllFiles)
        {
            if (!visited.Contains(file))
            {
                DetectCyclesDfs(file, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    private void DetectCyclesDfs(
        string file,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<ImmutableArray<string>> cycles)
    {
        visited.Add(file);
        recursionStack.Add(file);
        path.Add(file);

        foreach (var dep in GetDirectDependencies(file))
        {
            if (!visited.Contains(dep))
            {
                DetectCyclesDfs(dep, visited, recursionStack, path, cycles);
            }
            else if (recursionStack.Contains(dep))
            {
                // Found a cycle - extract it from the path
                var cycleStart = path.IndexOf(dep);
                var cycle = path.Skip(cycleStart).Append(dep).ToImmutableArray();
                cycles.Add(cycle);
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(file);
    }

    /// <summary>
    /// Gets all files affected by a change to the specified file.
    /// </summary>
    /// <param name="changedFile">The file that has changed.</param>
    /// <returns>
    /// The set of all files that need to be recompiled, including the changed file itself
    /// and all files that transitively depend on it.
    /// </returns>
    public ImmutableHashSet<string> GetAffectedFiles(string changedFile)
    {
        return GetAffectedFiles(new[] { changedFile });
    }

    /// <summary>
    /// Gets all files affected by changes to the specified files.
    /// </summary>
    /// <param name="changedFiles">The files that have changed.</param>
    /// <returns>
    /// The set of all files that need to be recompiled, including the changed files themselves
    /// and all files that transitively depend on them.
    /// </returns>
    public ImmutableHashSet<string> GetAffectedFiles(IEnumerable<string> changedFiles)
    {
        var affected = ImmutableHashSet.CreateBuilder<string>();
        var queue = new Queue<string>();

        // Initialize with changed files
        foreach (var file in changedFiles)
        {
            var normalized = NormalizePath(file);
            if (AllFiles.Contains(normalized))
            {
                affected.Add(normalized);
                queue.Enqueue(normalized);
            }
        }

        // BFS to find transitive dependents
        while (queue.Count > 0)
        {
            var file = queue.Dequeue();
            foreach (var dependent in GetDirectDependents(file))
            {
                if (affected.Add(dependent))
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        return affected.ToImmutable();
    }

    /// <summary>
    /// Computes groups of files that can be compiled in parallel.
    /// </summary>
    /// <returns>
    /// A list of groups, where each group contains files that can be compiled
    /// simultaneously. Groups are ordered by dependency level - all files in
    /// group N must be compiled before any file in group N+1.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Files with no dependencies are in group 0. A file is in group N where
    /// N is one plus the maximum group number of any of its dependencies.
    /// </para>
    /// <para>
    /// This is useful for parallel compilation: all files in a group can be
    /// compiled concurrently once all previous groups are complete.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ImmutableHashSet<string>> GetParallelizableGroups()
    {
        if (AllFiles.Count == 0)
        {
            return Array.Empty<ImmutableHashSet<string>>();
        }

        // Calculate the group (level) for each file
        var fileLevel = new Dictionary<string, int>();
        var queue = new Queue<string>();

        // Initialize: files with no dependencies are at level 0
        foreach (var file in AllFiles)
        {
            var deps = GetDirectDependencies(file);
            if (deps.IsEmpty)
            {
                fileLevel[file] = 0;
                queue.Enqueue(file);
            }
        }

        // Process files in topological order
        while (queue.Count > 0)
        {
            var file = queue.Dequeue();
            var myLevel = fileLevel[file];

            foreach (var dependent in GetDirectDependents(file))
            {
                // Calculate the minimum level for this dependent
                var deps = GetDirectDependencies(dependent);
                var allDepsResolved = deps.All(d => fileLevel.ContainsKey(d));

                if (allDepsResolved)
                {
                    var maxDepLevel = deps.Max(d => fileLevel[d]);
                    var newLevel = maxDepLevel + 1;

                    if (!fileLevel.ContainsKey(dependent) || fileLevel[dependent] < newLevel)
                    {
                        fileLevel[dependent] = newLevel;
                        queue.Enqueue(dependent);
                    }
                }
            }
        }

        // Group files by level
        var groups = new Dictionary<int, ImmutableHashSet<string>.Builder>();
        foreach (var (file, level) in fileLevel)
        {
            if (!groups.ContainsKey(level))
            {
                groups[level] = ImmutableHashSet.CreateBuilder<string>();
            }
            groups[level].Add(file);
        }

        // Convert to ordered list
        var maxLevel = groups.Keys.DefaultIfEmpty(-1).Max();
        var result = new List<ImmutableHashSet<string>>();
        for (int i = 0; i <= maxLevel; i++)
        {
            result.Add(groups.TryGetValue(i, out var builder)
                ? builder.ToImmutable()
                : ImmutableHashSet<string>.Empty);
        }

        return result;
    }

    /// <summary>
    /// Checks if a file is stale based on its content hash.
    /// </summary>
    /// <param name="filePath">The path of the file to check.</param>
    /// <param name="currentHash">The current content hash of the file.</param>
    /// <returns>
    /// True if the file is stale (hash differs from stored hash or file not in graph),
    /// false if the file is up-to-date.
    /// </returns>
    /// <remarks>
    /// This method is intended for future incremental compilation support.
    /// It requires that hashes were provided when the graph was constructed.
    /// </remarks>
    public bool IsStale(string filePath, string currentHash)
    {
        if (FileHashes == null)
        {
            return true; // No hashes available, assume stale
        }

        var normalized = NormalizePath(filePath);
        if (!FileHashes.TryGetValue(normalized, out var storedHash))
        {
            return true; // File not in graph, assume stale
        }

        return !string.Equals(storedHash, currentHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes a file path for consistent comparison.
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Normalize directory separators to forward slash for cross-platform consistency
        var normalized = path.Replace('\\', '/');

        // Normalize to lowercase on case-insensitive systems (Windows/macOS)
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }

        return normalized;
    }
}
