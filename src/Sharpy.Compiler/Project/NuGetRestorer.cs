using System.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

internal static class NuGetRestorer
{
    public static bool RestorePackages(
        IReadOnlyList<PackageRef> packageReferences,
        string targetFramework,
        ICompilerLogger? logger)
    {
        return RestorePackages(packageReferences, targetFramework, logger, packagesDir: null);
    }

    internal static bool RestorePackages(
        IReadOnlyList<PackageRef> packageReferences,
        string targetFramework,
        ICompilerLogger? logger,
        string? packagesDir)
    {
        if (packageReferences.Count == 0)
            return true;

        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);

            var csprojContent = GenerateCsproj(packageReferences, targetFramework);
            var csprojPath = Path.Combine(tempDir, "restore.csproj");
            File.WriteAllText(csprojPath, csprojContent);

            logger?.LogDebug($"Restoring {packageReferences.Count} NuGet package(s) via temp project at {csprojPath}");

            var args = $"restore \"{csprojPath}\"";
            if (packagesDir != null)
                args += $" --packages \"{packagesDir}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    logger?.LogError("Failed to start 'dotnet restore' process.", 0, 0);
                    return false;
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                    logger?.LogDebug(stdout.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderr))
                    logger?.LogDebug(stderr.TrimEnd());

                if (process.ExitCode != 0)
                {
                    logger?.LogError($"NuGet restore failed with exit code {process.ExitCode}.", 0, 0);
                    if (!string.IsNullOrWhiteSpace(stderr))
                        logger?.LogError(stderr.TrimEnd(), 0, 0);
                    return false;
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                logger?.LogError($"Could not find 'dotnet' on PATH: {ex.Message}", 0, 0);
                return false;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    private static string GenerateCsproj(IReadOnlyList<PackageRef> packageReferences, string targetFramework)
    {
        var packageRefs = string.Join(Environment.NewLine,
            packageReferences.Select(p =>
                $"    <PackageReference Include=\"{EscapeXml(p.Name)}\" Version=\"{EscapeXml(p.Version)}\" />"));

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{EscapeXml(targetFramework)}</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {packageRefs}
              </ItemGroup>
            </Project>
            """;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
