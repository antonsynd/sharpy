namespace Sharpy.Compiler.Services;

/// <summary>
/// Write-only interface for recording file dependencies during compilation.
/// Used by <see cref="Semantic.ImportResolver"/> to track import relationships
/// without depending on the concrete <see cref="Project.DependencyGraphBuilder"/>.
/// </summary>
public interface IDependencyRecorder
{
    /// <summary>
    /// Registers a file in the dependency graph.
    /// </summary>
    /// <param name="filePath">The path of the file to register.</param>
    void AddFile(string filePath);

    /// <summary>
    /// Records a dependency between two files.
    /// </summary>
    /// <param name="fromFile">The file that has the import statement.</param>
    /// <param name="toFile">The file that is being imported.</param>
    void AddDependency(string fromFile, string toFile);

    /// <summary>
    /// Sets the content hash for a file (for staleness detection).
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="hash">The content hash (e.g., SHA-256).</param>
    void SetFileHash(string filePath, string hash);
}
