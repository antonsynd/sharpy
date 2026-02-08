using System.Collections.Immutable;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Read-only query interface for file dependency information in a multi-file project.
/// Provides build ordering, dependency analysis, and affected file queries
/// without exposing the internal <see cref="Project.DependencyGraph"/> class.
/// </summary>
public interface IDependencyQuery
{
    /// <summary>
    /// Gets all files in the dependency graph.
    /// </summary>
    IReadOnlySet<string> AllFiles { get; }

    /// <summary>
    /// Gets the files that the specified file directly depends on (imports).
    /// Returns an empty set if the file is not in the graph.
    /// </summary>
    ImmutableHashSet<string> GetDirectDependencies(string filePath);

    /// <summary>
    /// Gets the files that directly depend on the specified file.
    /// Returns an empty set if the file is not in the graph.
    /// </summary>
    ImmutableHashSet<string> GetDirectDependents(string filePath);

    /// <summary>
    /// Computes a valid build order (topological sort) for all files.
    /// Dependencies appear before their dependents.
    /// </summary>
    IReadOnlyList<string> GetBuildOrder();

    /// <summary>
    /// Gets all files affected by a change to the specified file,
    /// including the file itself and all transitive dependents.
    /// </summary>
    ImmutableHashSet<string> GetAffectedFiles(string changedFile);
}
