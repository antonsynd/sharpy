using System.IO;
using System.Collections.Generic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Generates a minimal .csproj scaffold for test projects so that 'dotnet test' can discover
/// and run xUnit tests from the compiled Sharpy assembly.
/// </summary>
internal static class TestProjectScaffold
{
    /// <summary>
    /// Required test infrastructure packages that must be present for 'dotnet test' to work.
    /// </summary>
    private static readonly (string Name, string Version)[] RequiredTestPackages =
    {
        ("Microsoft.NET.Test.Sdk", "17.12.0"),
        ("xunit.runner.visualstudio", "3.0.2"),
    };

    /// <summary>
    /// Generates a test project scaffold if the project has xUnit package references.
    /// The scaffold includes a .csproj with PackageReference, Compile, and Reference items
    /// that 'dotnet test' needs to discover and run tests.
    /// </summary>
    public static void GenerateIfNeeded(ProjectConfig config, string outputAssemblyPath, ICompilerLogger logger)
    {
        // Check if this is a test project (has xunit reference)
        bool isTestProject = config.PackageReferences.Any(p =>
            p.Name.Equals("xunit", StringComparison.OrdinalIgnoreCase) ||
            p.Name.StartsWith("xunit.", StringComparison.OrdinalIgnoreCase));

        if (!isTestProject)
            return;

        var outputDir = Path.GetDirectoryName(outputAssemblyPath);
        if (string.IsNullOrEmpty(outputDir))
            return;

        Directory.CreateDirectory(outputDir);

        var assemblyName = Path.GetFileNameWithoutExtension(outputAssemblyPath);
        var csprojPath = Path.Combine(outputDir, assemblyName + ".csproj");

        var csprojContent = GenerateCsprojContent(config, outputDir, assemblyName);

        File.WriteAllText(csprojPath, csprojContent);
        logger.LogDebug($"Generated test project scaffold: {csprojPath}");
    }

    /// <summary>
    /// Generates the .csproj XML content with package references, compile items,
    /// and runtime assembly references.
    /// </summary>
    internal static string GenerateCsprojContent(ProjectConfig config, string outputDir, string assemblyName)
    {
        // Merge user-declared packages with required test infrastructure packages
        var allPackages = MergeRequiredPackages(config.PackageReferences);

        var packageRefs = string.Join("\n    ",
            allPackages.Select(p =>
                $"<PackageReference Include=\"{p.Name}\" Version=\"{p.Version}\" />"));

        // Add Compile items for generated .cs files in the output directory
        var compileItems = $"<Compile Include=\"**/*.cs\" />";

        // Add Reference items for Sharpy runtime DLLs
        var runtimeReferences = BuildRuntimeReferences();

        var itemGroups = $@"  <ItemGroup>
    {packageRefs}
  </ItemGroup>
  <ItemGroup>
    {compileItems}
  </ItemGroup>";

        if (!string.IsNullOrEmpty(runtimeReferences))
        {
            itemGroups += $@"
  <ItemGroup>
    {runtimeReferences}
  </ItemGroup>";
        }

        return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{config.TargetFramework}</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
{itemGroups}
</Project>";
    }

    /// <summary>
    /// Merges user-declared package references with required test infrastructure packages.
    /// User-declared versions take precedence if they declare the same package.
    /// </summary>
    private static List<PackageRef> MergeRequiredPackages(List<PackageRef> userPackages)
    {
        var result = new List<PackageRef>(userPackages);
        var existingNames = new HashSet<string>(
            userPackages.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (name, version) in RequiredTestPackages)
        {
            if (!existingNames.Contains(name))
            {
                result.Add(new PackageRef(name, version));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds Reference items for Sharpy runtime DLLs (Sharpy.Core.dll, Sharpy.Stdlib.dll)
    /// resolved from the compiler's own assembly location.
    /// </summary>
    private static string BuildRuntimeReferences()
    {
        var compilerDir = Path.GetDirectoryName(typeof(TestProjectScaffold).Assembly.Location);
        if (string.IsNullOrEmpty(compilerDir))
            return string.Empty;

        var runtimeDlls = new[] { "Sharpy.Core.dll", "Sharpy.Stdlib.dll" };
        var references = new List<string>();

        foreach (var dll in runtimeDlls)
        {
            var dllPath = Path.Combine(compilerDir, dll);
            if (File.Exists(dllPath))
            {
                references.Add($"<Reference Include=\"{Path.GetFileNameWithoutExtension(dll)}\">\n      <HintPath>{dllPath}</HintPath>\n    </Reference>");
            }
        }

        return string.Join("\n    ", references);
    }
}
