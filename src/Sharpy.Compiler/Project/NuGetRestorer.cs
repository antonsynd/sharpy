using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Project;

internal sealed class RestoreResult
{
    public bool Success { get; }
    public IReadOnlyDictionary<string, string> ResolvedVersions { get; }

    /// <summary>
    /// A failure the caller must render whatever the log level is — SPY0913 when the resolved
    /// <c>dotnet</c> could not be started (#2292). <c>null</c> when the log already carries the cause.
    /// </summary>
    public CompilerDiagnostic? Diagnostic { get; }

    private RestoreResult(bool success, IReadOnlyDictionary<string, string> resolvedVersions, CompilerDiagnostic? diagnostic = null)
    {
        Success = success;
        ResolvedVersions = resolvedVersions;
        Diagnostic = diagnostic;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyVersions =
        new Dictionary<string, string>();

    public static RestoreResult Failed(CompilerDiagnostic? diagnostic = null) => new(false, EmptyVersions, diagnostic);
    public static RestoreResult Succeeded(IReadOnlyDictionary<string, string> resolvedVersions) =>
        new(true, resolvedVersions);

    public static implicit operator bool(RestoreResult result) => result.Success;
}

internal static class NuGetRestorer
{
    // A cold-cache restore over a slow network can legitimately take minutes; this only
    // guards against a genuinely hung process (e.g. a private feed waiting for
    // interactive auth, or a dead connection).
    private const int RestoreTimeoutMs = 600_000;

    /// <summary>The clause SPY0913 opens with when restore has no <c>dotnet</c> to run (#2292).</summary>
    internal const string Purpose = "<PackageReference> restore runs `dotnet restore` from the .NET SDK";

    /// <param name="dotnetPath">
    /// The <c>dotnet</c> the caller resolved through <see cref="DotnetHost"/> before doing any work (#2292).
    /// </param>
    public static RestoreResult RestorePackages(
        IReadOnlyList<PackageRef> packageReferences,
        string targetFramework,
        ICompilerLogger? logger,
        string dotnetPath)
    {
        return RestorePackages(packageReferences, targetFramework, logger, dotnetPath, packagesDir: null);
    }

    internal static RestoreResult RestorePackages(
        IReadOnlyList<PackageRef> packageReferences,
        string targetFramework,
        ICompilerLogger? logger,
        string dotnetPath,
        string? packagesDir)
    {
        if (packageReferences.Count == 0)
            return RestoreResult.Succeeded(new Dictionary<string, string>());

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
                    FileName = dotnetPath,
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
                    return RestoreResult.Failed();
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
                    return RestoreResult.Failed();
                }

                var resolvedVersions = ParseProjectAssetsJson(
                    Path.Combine(tempDir, "obj", "project.assets.json"), logger);
                return RestoreResult.Succeeded(resolvedVersions);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // The caller resolved this path a moment ago (#2292), so this is a dotnet that exists
                // but cannot start — SPY0913, rendered by the caller at every log level.
                return RestoreResult.Failed(DotnetHost.StartFailed(Purpose, DotnetRequirement.Sdk, dotnetPath, ex.Message));
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

    internal static Dictionary<string, string> ParseProjectAssetsJson(
        string assetsJsonPath, ICompilerLogger? logger)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(assetsJsonPath))
            {
                logger?.LogDebug($"project.assets.json not found at {assetsJsonPath}");
                return result;
            }

            var json = File.ReadAllText(assetsJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("libraries", out var libraries))
                return result;

            foreach (var lib in libraries.EnumerateObject())
            {
                var slashIndex = lib.Name.IndexOf('/', StringComparison.Ordinal);
                if (slashIndex <= 0 || slashIndex >= lib.Name.Length - 1)
                    continue;

                var name = lib.Name[..slashIndex];
                var version = lib.Name[(slashIndex + 1)..];
                result[name] = version;
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug($"Failed to parse project.assets.json: {ex.Message}");
        }

        return result;
    }
}
