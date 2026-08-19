using System.Diagnostics;
using System.Text;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

internal static class NuGetRestorer
{
    // A cold-cache restore over a slow network can legitimately take minutes; this only
    // guards against a genuinely hung process (e.g. a private feed waiting for
    // interactive auth, or a dead connection).
    private const int RestoreTimeoutMs = 600_000;

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

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("restore");
                psi.ArgumentList.Add(csprojPath);
                if (packagesDir != null)
                {
                    psi.ArgumentList.Add("--packages");
                    psi.ArgumentList.Add(packagesDir);
                }

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                // Both streams are redirected, so they must be drained asynchronously while
                // the child runs — sequential ReadToEnd() deadlocks once the unread pipe's
                // buffer fills (restore warnings alone can exceed it).
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(RestoreTimeoutMs))
                {
                    try
                    { process.Kill(entireProcessTree: true); }
                    catch { }
                    logger?.LogError($"NuGet restore timed out after {RestoreTimeoutMs / 1000} seconds.", 0, 0);
                    return false;
                }

                // Parameterless overload waits for the async output handlers to flush.
                process.WaitForExit();

                var stdoutText = stdout.ToString();
                var stderrText = stderr.ToString();
                if (!string.IsNullOrWhiteSpace(stdoutText))
                    logger?.LogDebug(stdoutText.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderrText))
                    logger?.LogDebug(stderrText.TrimEnd());

                if (process.ExitCode != 0)
                {
                    logger?.LogError($"NuGet restore failed with exit code {process.ExitCode}.", 0, 0);
                    if (!string.IsNullOrWhiteSpace(stderrText))
                        logger?.LogError(stderrText.TrimEnd(), 0, 0);
                    return false;
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                logger?.LogError($"Could not start 'dotnet' for NuGet restore: {ex.Message}. Package restore requires the .NET SDK on PATH.", 0, 0);
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
