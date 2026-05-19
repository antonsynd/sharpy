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
    /// Generates a test project scaffold if the project has xUnit package references.
    /// The scaffold includes a .csproj and .runtimeconfig.json that dotnet test needs.
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

        var packageRefs = string.Join("\n    ",
            config.PackageReferences.Select(p =>
                $"<PackageReference Include=\"{p.Name}\" Version=\"{p.Version}\" />"));

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{config.TargetFramework}</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    {packageRefs}
  </ItemGroup>
</Project>";

        File.WriteAllText(csprojPath, csprojContent);
        logger.LogDebug($"Generated test project scaffold: {csprojPath}");
    }
}
