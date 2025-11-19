using System.Xml.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Sharpy.Compiler;

/// <summary>
/// Configuration for a Sharpy project loaded from a .spyproj file
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Full path to the .spyproj file
    /// </summary>
    public string ProjectFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Directory containing the .spyproj file (project root)
    /// </summary>
    public string ProjectDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Root namespace for the project
    /// </summary>
    public string RootNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Output type: "library" or "exe"
    /// </summary>
    public string OutputType { get; init; } = "library";

    /// <summary>
    /// Target framework (e.g., "net8.0")
    /// </summary>
    public string TargetFramework { get; init; } = "net8.0";

    /// <summary>
    /// Output assembly name (defaults to root namespace if not specified)
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// List of Sharpy source files to compile (resolved from glob patterns)
    /// </summary>
    public List<string> SourceFiles { get; init; } = new();

    /// <summary>
    /// List of .NET assembly references
    /// </summary>
    public List<string> References { get; init; } = new();

    /// <summary>
    /// Module search paths for assembly resolution
    /// </summary>
    public List<string> ModulePaths { get; init; } = new();

    /// <summary>
    /// Build configuration (Debug or Release)
    /// </summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>
    /// Output directory for compiled assemblies (relative to project directory)
    /// </summary>
    public string OutputPath
    {
        get
        {
            var binPath = Path.Combine(ProjectDirectory, "bin", Configuration, TargetFramework);
            return binPath;
        }
    }

    /// <summary>
    /// Full path to the output assembly
    /// </summary>
    public string OutputAssemblyPath
    {
        get
        {
            var assemblyName = AssemblyName ?? RootNamespace;
            var extension = OutputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
            return Path.Combine(OutputPath, assemblyName + extension);
        }
    }
}

/// <summary>
/// Parser for .spyproj project files
/// </summary>
public class ProjectFileParser
{
    /// <summary>
    /// Load and parse a .spyproj file
    /// </summary>
    public static ProjectConfig Load(string projectFilePath, string? configuration = null)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");
        }

        var projectDirectory = Path.GetDirectoryName(projectFilePath) ?? Directory.GetCurrentDirectory();
        var document = XDocument.Load(projectFilePath);
        var root = document.Root;

        if (root == null || root.Name.LocalName != "Project")
        {
            throw new InvalidDataException("Invalid .spyproj file: missing <Project> root element");
        }

        // Parse PropertyGroup
        var propertyGroup = root.Element("PropertyGroup");
        if (propertyGroup == null)
        {
            throw new InvalidDataException("Invalid .spyproj file: missing <PropertyGroup> element");
        }

        var rootNamespace = propertyGroup.Element("RootNamespace")?.Value;
        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            throw new InvalidDataException("Invalid .spyproj file: <RootNamespace> is required");
        }

        var outputType = propertyGroup.Element("OutputType")?.Value ?? "library";
        var targetFramework = propertyGroup.Element("TargetFramework")?.Value ?? "net8.0";
        var assemblyName = propertyGroup.Element("AssemblyName")?.Value;

        // Parse ItemGroup for source files
        var sourceFiles = new List<string>();
        var references = new List<string>();
        var modulePaths = new List<string>();

        foreach (var itemGroup in root.Elements("ItemGroup"))
        {
            // Parse SpyFile includes
            foreach (var spyFile in itemGroup.Elements("SpyFile"))
            {
                var include = spyFile.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    var resolvedFiles = ResolveGlobPattern(projectDirectory, include);
                    sourceFiles.AddRange(resolvedFiles);
                }
            }

            // Parse Reference includes
            foreach (var reference in itemGroup.Elements("Reference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    // Resolve relative paths
                    var referencePath = Path.IsPathRooted(include)
                        ? include
                        : Path.Combine(projectDirectory, include);
                    references.Add(referencePath);
                }
            }

            // Parse ModulePath includes
            foreach (var modulePath in itemGroup.Elements("ModulePath"))
            {
                var include = modulePath.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    var modulePathResolved = Path.IsPathRooted(include)
                        ? include
                        : Path.Combine(projectDirectory, include);
                    modulePaths.Add(modulePathResolved);
                }
            }
        }

        if (sourceFiles.Count == 0)
        {
            throw new InvalidDataException("No source files found in project. Add <SpyFile Include=\"...\" /> elements.");
        }

        return new ProjectConfig
        {
            ProjectFilePath = Path.GetFullPath(projectFilePath),
            ProjectDirectory = projectDirectory,
            RootNamespace = rootNamespace,
            OutputType = outputType,
            TargetFramework = targetFramework,
            AssemblyName = assemblyName,
            SourceFiles = sourceFiles,
            References = references,
            ModulePaths = modulePaths,
            Configuration = configuration ?? "Debug"
        };
    }

    /// <summary>
    /// Find a .spyproj file in the specified directory
    /// </summary>
    public static string? FindProjectFile(string directory)
    {
        var projectFiles = Directory.GetFiles(directory, "*.spyproj", SearchOption.TopDirectoryOnly);

        if (projectFiles.Length == 0)
        {
            return null;
        }

        if (projectFiles.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple .spyproj files found in '{directory}'. Please specify which project to build:\n" +
                string.Join("\n", projectFiles.Select(f => $"  - {Path.GetFileName(f)}")));
        }

        return projectFiles[0];
    }

    /// <summary>
    /// Resolve glob patterns to actual file paths
    /// </summary>
    private static List<string> ResolveGlobPattern(string baseDirectory, string pattern)
    {
        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        var directoryInfo = new DirectoryInfo(baseDirectory);
        var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));

        return result.Files
            .Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)))
            .Where(File.Exists)
            .ToList();
    }
}
