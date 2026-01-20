using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Builder for constructing a <see cref="DependencyGraph"/> during compilation.
/// </summary>
/// <remarks>
/// <para>
/// This class is thread-safe and can be used from multiple threads during
/// parallel import resolution. Use <see cref="AddFile"/> to register files
/// and <see cref="AddDependency"/> to record import relationships.
/// </para>
/// <para>
/// After all dependencies are recorded, call <see cref="Build"/> to create
/// the immutable <see cref="DependencyGraph"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = new DependencyGraphBuilder();
///
/// // Register files
/// builder.AddFile("main.spy");
/// builder.AddFile("utils.spy");
/// builder.AddFile("models.spy");
///
/// // Record dependencies (main imports utils and models)
/// builder.AddDependency("main.spy", "utils.spy");
/// builder.AddDependency("main.spy", "models.spy");
/// builder.AddDependency("models.spy", "utils.spy");
///
/// // Build the immutable graph
/// var graph = builder.Build();
/// </code>
/// </example>
public class DependencyGraphBuilder
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _dependencies = new();
    private readonly ConcurrentDictionary<string, string> _fileHashes = new();
    private readonly object _buildLock = new();
    private DependencyGraph? _cachedGraph;

    /// <summary>
    /// Registers a file in the dependency graph.
    /// </summary>
    /// <param name="filePath">The path of the file to register.</param>
    /// <remarks>
    /// This method is thread-safe. Duplicate registrations are ignored.
    /// Files are automatically registered when used in <see cref="AddDependency"/>.
    /// </remarks>
    public void AddFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var normalized = NormalizePath(filePath);
        _dependencies.TryAdd(normalized, new ConcurrentBag<string>());
        _cachedGraph = null; // Invalidate cache
    }

    /// <summary>
    /// Records a dependency between two files.
    /// </summary>
    /// <param name="fromFile">The file that has the import statement.</param>
    /// <param name="toFile">The file that is being imported.</param>
    /// <remarks>
    /// <para>
    /// This method is thread-safe. Both files are automatically registered
    /// if they haven't been added with <see cref="AddFile"/>.
    /// </para>
    /// <para>
    /// Duplicate dependencies are allowed (de-duplicated during build).
    /// </para>
    /// </remarks>
    public void AddDependency(string fromFile, string toFile)
    {
        ArgumentNullException.ThrowIfNull(fromFile);
        ArgumentNullException.ThrowIfNull(toFile);

        var normalizedFrom = NormalizePath(fromFile);
        var normalizedTo = NormalizePath(toFile);

        // Ensure both files exist in the dictionary
        var deps = _dependencies.GetOrAdd(normalizedFrom, _ => new ConcurrentBag<string>());
        _dependencies.TryAdd(normalizedTo, new ConcurrentBag<string>());

        deps.Add(normalizedTo);
        _cachedGraph = null; // Invalidate cache
    }

    /// <summary>
    /// Sets the content hash for a file (for staleness detection).
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="hash">The content hash (e.g., SHA-256).</param>
    /// <remarks>
    /// This method is thread-safe. Hashes are optional and used for
    /// incremental compilation to detect which files have changed.
    /// </remarks>
    public void SetFileHash(string filePath, string hash)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(hash);

        var normalized = NormalizePath(filePath);
        _fileHashes[normalized] = hash;
        _cachedGraph = null; // Invalidate cache
    }

    /// <summary>
    /// Builds an immutable <see cref="DependencyGraph"/> from the recorded dependencies.
    /// </summary>
    /// <param name="validateTargets">
    /// If true, validates that all dependency targets exist in the graph.
    /// Throws <see cref="InvalidOperationException"/> if validation fails.
    /// </param>
    /// <returns>The constructed dependency graph.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="validateTargets"/> is true and a dependency
    /// target does not exist as a registered file.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is thread-safe. Multiple calls with the same builder state
    /// return the cached graph.
    /// </para>
    /// <para>
    /// After calling <see cref="Build"/>, you can continue adding dependencies
    /// and call <see cref="Build"/> again to get an updated graph.
    /// </para>
    /// </remarks>
    public DependencyGraph Build(bool validateTargets = false)
    {
        lock (_buildLock)
        {
            // Return cached graph if available
            if (_cachedGraph != null)
            {
                return _cachedGraph;
            }

            // Collect all files (both sources and targets)
            var allFiles = new HashSet<string>();
            foreach (var (file, deps) in _dependencies)
            {
                allFiles.Add(file);
                foreach (var dep in deps)
                {
                    allFiles.Add(dep);
                }
            }

            // Validate targets if requested
            if (validateTargets)
            {
                var registeredFiles = new HashSet<string>(_dependencies.Keys);
                foreach (var (file, deps) in _dependencies)
                {
                    foreach (var dep in deps)
                    {
                        if (!registeredFiles.Contains(dep))
                        {
                            throw new InvalidOperationException(
                                $"Dependency target '{dep}' (from '{file}') was not registered as a file. " +
                                "Call AddFile() for all files before calling Build(validateTargets: true).");
                        }
                    }
                }
            }

            // Build the dependency dictionary (de-duplicating dependencies)
            var fileDependencies = new Dictionary<string, ImmutableHashSet<string>>();
            foreach (var (file, deps) in _dependencies)
            {
                fileDependencies[file] = deps.ToImmutableHashSet();
            }

            // Ensure all target files have an entry
            foreach (var file in allFiles)
            {
                if (!fileDependencies.ContainsKey(file))
                {
                    fileDependencies[file] = ImmutableHashSet<string>.Empty;
                }
            }

            // Build file hashes dictionary (if any hashes were set)
            Dictionary<string, string>? hashes = null;
            if (!_fileHashes.IsEmpty)
            {
                hashes = new Dictionary<string, string>(_fileHashes);
            }

            _cachedGraph = new DependencyGraph(fileDependencies, hashes);
            return _cachedGraph;
        }
    }

    /// <summary>
    /// Clears all recorded dependencies and files.
    /// </summary>
    public void Clear()
    {
        _dependencies.Clear();
        _fileHashes.Clear();
        _cachedGraph = null;
    }

    /// <summary>
    /// Gets the number of registered files.
    /// </summary>
    public int FileCount => _dependencies.Count;

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
