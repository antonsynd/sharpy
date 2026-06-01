extern alias SharpyRT;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharpy.Cli;

/// <summary>
/// Publishes a self-contained executable (no .NET runtime required) from an already
/// compiled Sharpy assembly. Creates a temporary wrapper project that references the
/// compiled assembly plus its runtime dependencies and shells out to
/// <c>dotnet publish</c>. Shared by the <c>run</c> and <c>compile</c> commands —
/// it writes the published artifact but does NOT execute it.
/// </summary>
internal static class SelfContainedPublisher
{
    /// <summary>
    /// Publishes a self-contained executable for the current runtime into
    /// <paramref name="outputDir"/>.
    /// </summary>
    /// <param name="compiledAssemblyPath">Path to the already-compiled Sharpy assembly.</param>
    /// <param name="entryTypeName">
    /// Name of the generated module class containing the <c>Main()</c> entry point
    /// (the Sharpy source file's base name).
    /// </param>
    /// <param name="outputDir">Directory to publish the self-contained executable into.</param>
    /// <param name="usedAssemblyPaths">Stdlib assemblies referenced by the program.</param>
    /// <returns>The path to the published executable, or <c>null</c> if publishing failed.</returns>
    internal static string? Publish(
        string compiledAssemblyPath,
        string entryTypeName,
        string outputDir,
        IReadOnlySet<string> usedAssemblyPaths)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var cliDir = Path.GetDirectoryName(sharpyCorePath)!;

        Directory.CreateDirectory(outputDir);

        var tempProjDir = Path.Combine(Path.GetTempPath(), $"sharpy_proj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempProjDir);

        try
        {
            var csprojPath = Path.Combine(tempProjDir, $"{entryTypeName}.csproj");

            var stdlibRefs = new StringBuilder();
            foreach (var assemblyPath in usedAssemblyPaths)
            {
                var fileName = Path.GetFileName(assemblyPath);
                if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fullPath = Path.Combine(cliDir, fileName);
                if (File.Exists(fullPath))
                {
                    var includeName = Path.GetFileNameWithoutExtension(fileName);
                    stdlibRefs.AppendLine($@"    <Reference Include=""{includeName}"">
      <HintPath>{fullPath}</HintPath>
    </Reference>");
                }
            }

            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>{entryTypeName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""{Path.GetFileNameWithoutExtension(compiledAssemblyPath)}"">
      <HintPath>{compiledAssemblyPath}</HintPath>
    </Reference>
    <Reference Include=""Sharpy.Core"">
      <HintPath>{sharpyCorePath}</HintPath>
    </Reference>
{stdlibRefs}  </ItemGroup>
</Project>";

            File.WriteAllText(csprojPath, csprojContent);
            File.WriteAllText(
                Path.Combine(tempProjDir, "Program.cs"),
                $"// Auto-generated entry point\n{entryTypeName}.Main();\n");

            Console.WriteLine($"Publishing self-contained executable for {rid}...");
            var publishInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "publish",
                    csprojPath,
                    "--self-contained",
                    "-r", rid,
                    "-o", outputDir,
                    "--nologo",
                    "-v", "q"
                },
                UseShellExecute = false,
                RedirectStandardError = true
            };

            var publishProcess = System.Diagnostics.Process.Start(publishInfo);
            if (publishProcess != null)
            {
                var stderr = publishProcess.StandardError.ReadToEnd();
                publishProcess.WaitForExit();

                if (publishProcess.ExitCode != 0)
                {
                    Console.Error.WriteLine("Self-contained publish failed:");
                    Console.Error.WriteLine(stderr);
                    return null;
                }
            }

            var publishedExe = Path.Combine(outputDir, entryTypeName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                publishedExe += ".exe";

            if (!File.Exists(publishedExe))
            {
                Console.Error.WriteLine($"Published executable not found: {publishedExe}");
                return null;
            }

            return publishedExe;
        }
        finally
        {
            try
            { Directory.Delete(tempProjDir, recursive: true); }
            catch { }
        }
    }
}
