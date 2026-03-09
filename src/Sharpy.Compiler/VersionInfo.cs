using System.Reflection;

namespace Sharpy.Compiler;

/// <summary>
/// Provides version information for the Sharpy toolchain.
/// </summary>
public static class VersionInfo
{
    private static readonly Assembly CompilerAssembly = typeof(VersionInfo).Assembly;

    /// <summary>
    /// The toolchain version (e.g., "0.1.0").
    /// </summary>
    public static string Version { get; } =
        CompilerAssembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// The full informational version including commit hash (e.g., "0.1.0+abc1234").
    /// </summary>
    public static string InformationalVersion { get; } =
        CompilerAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? Version;

    /// <summary>
    /// The short commit hash, extracted from InformationalVersion.
    /// </summary>
    public static string CommitHash { get; } = ExtractCommitHash();

    /// <summary>
    /// Formats the full version display for CLI output.
    /// </summary>
    public static string GetDisplayString()
    {
        return $"sharpyc {InformationalVersion}";
    }

    /// <summary>
    /// Formats detailed version info including runtime.
    /// </summary>
    public static string GetDetailedDisplayString()
    {
        return $"""
            sharpyc {InformationalVersion}
            Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}
            OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}
            """;
    }

    private static string ExtractCommitHash()
    {
        var info = InformationalVersion;
        var plusIndex = info.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? info[(plusIndex + 1)..] : "unknown";
    }
}
