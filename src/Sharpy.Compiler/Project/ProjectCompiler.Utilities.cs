using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Forwards diagnostics wholesale into both the project-level and per-compilation bags,
    /// preserving Span, Phase, and all other record fields (#1280, #1077).
    /// </summary>
    private void ForwardDiagnostics(IEnumerable<CompilerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            _projectModel!.GlobalDiagnostics.Add(diagnostic);
            _diagnostics.Add(diagnostic);
        }
    }

    /// <summary>
    /// The reflection name of the entry module's members class — the one C# entry point of an exe
    /// (#2094): the layout semantic analysis recorded on the entry unit's root, under the project's
    /// root namespace (<c>Thing.ThingModule</c>, <c>SharpyApp.Main.MainModule</c>). Null for a
    /// library (no entry file) or when analysis did not reach the entry unit. Read by the assembly
    /// compiler (<c>WithMainTypeName</c>) and the CLI's self-contained publish (#2039).
    /// </summary>
    internal static string? EntryTypeNameOf(Model.ProjectModel model)
    {
        foreach (var unit in model.Units.Values)
        {
            if (!IsEntryPointFileForTypeCheck(unit.FilePath, model.Config))
                continue;
            // A cache-served entry unit has no AST; its cold build recorded the layout (#2039).
            var layout = (unit.Ast != null ? model.SemanticInfo?.GetModuleLayout(unit.Ast) : null)
                ?? unit.CachedModuleLayout;
            if (layout != null)
                return layout.MembersClassFullName(model.Config.RootNamespace);
        }
        return null;
    }

    /// <summary>
    /// Determine if a file is the entry point for validation and code generation.
    /// Used during type checking and code generation phases.
    /// </summary>
    private static bool IsEntryPointFileForTypeCheck(string file, ProjectConfig config)
    {
        // Library projects never have an entry point file
        if (config.OutputType.Equals("library", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(file);

        // If EntryPoint is specified in config, check against it
        if (!string.IsNullOrWhiteSpace(config.EntryPoint))
        {
            return fileName.Equals(config.EntryPoint, StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(Path.GetFileName(config.EntryPoint), StringComparison.OrdinalIgnoreCase);
        }

        // Otherwise, default to main.spy for executable projects
        var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
        return fileNameNoExt.Equals("main", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Create a failure result with accumulated errors
    /// </summary>
    private ProjectCompilationResult CreateFailureResult()
    {
        return new ProjectCompilationResult
        {
            Success = false,
            Diagnostics = _diagnostics,
            Metrics = ProjectMetrics,
            DependencyGraph = _dependencyGraph,
            ProjectModel = _projectModel,
            EffectiveFeatures = _features,
            ImportResolver = _importResolverBacking
        };
    }

    /// <summary>
    /// Compute the source root path from the project configuration.
    /// This is the common directory containing all source files, used for relative path calculation.
    /// </summary>
    private string ComputeSourceRootPath(ProjectConfig config)
    {
        if (config.SourceFiles.Count == 0)
        {
            return config.ProjectDirectory;
        }

        // Find the common directory prefix of all source files
        var directories = config.SourceFiles
            .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)))
            .Where(d => d != null)
            .Select(d => d!)
            .Distinct()
            .ToList();

        if (directories.Count == 0)
        {
            return config.ProjectDirectory;
        }

        if (directories.Count == 1)
        {
            // All files are in the same directory
            return directories[0];
        }

        // Find the longest common prefix path
        var commonPath = directories[0];
        foreach (var dir in directories.Skip(1))
        {
            commonPath = GetLongestCommonPath(commonPath, dir);
            if (string.IsNullOrEmpty(commonPath))
            {
                // No common path, fall back to project directory
                return config.ProjectDirectory;
            }
        }

        return commonPath;
    }

    /// <summary>
    /// Get the longest common path prefix between two paths.
    /// </summary>
    private string GetLongestCommonPath(string path1, string path2)
    {
        var parts1 = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts2 = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var commonParts = new List<string>();
        var minLength = System.Math.Min(parts1.Length, parts2.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (string.Equals(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase))
            {
                commonParts.Add(parts1[i]);
            }
            else
            {
                break;
            }
        }

        return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
    }
}
