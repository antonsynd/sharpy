namespace Sharpy.Compiler.Services;

/// <summary>
/// Read-only query interface for import resolution information.
/// Provides access to loaded module paths without exposing the
/// internal <see cref="Semantic.ImportResolver"/> class.
/// </summary>
public interface IImportQuery
{
    /// <summary>
    /// Gets the file paths of all loaded .spy modules.
    /// </summary>
    IReadOnlyCollection<string> LoadedModulePaths { get; }
}
