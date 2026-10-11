using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Project;

/// <summary>What a command needs the <c>dotnet</c> host for, named in SPY0913.</summary>
internal enum DotnetRequirement
{
    /// <summary><c>dotnet &lt;program&gt;</c> — any .NET 10 install (runtime or SDK) carries it.</summary>
    Runtime,

    /// <summary><c>dotnet publish</c> / <c>dotnet restore</c> — only the .NET 10 SDK carries them.</summary>
    Sdk,
}

/// <summary>
/// The one authority for the <c>dotnet</c> executable a <c>sharpyc</c> command starts (#2292). Every
/// launch site resolves it here BEFORE doing any work and starts the path this returns, so the check
/// and the launch cannot disagree. A command that cannot find it refuses with SPY0913 instead of
/// compiling first and then dying on an unhandled <c>Win32Exception</c> — which is what the
/// self-contained release archive did on a machine with no .NET install.
/// </summary>
/// <remarks>
/// The search follows <c>Process.Start</c>'s own order for a bare file name minus its
/// current-directory step: the running executable's directory first (under
/// <c>dotnet sharpyc.dll</c> that is the muxer running us), then each <c>PATH</c> entry in order.
/// Starting a <c>dotnet</c> that happens to sit in the working directory is a binary-planting hazard,
/// not a way anyone installs .NET.
/// </remarks>
internal static class DotnetHost
{
    internal const string DownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";

    /// <summary>The <c>dotnet</c> this process would start, or <c>null</c> when there is none.</summary>
    internal static string? Resolve() =>
        Resolve(Environment.ProcessPath, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows());

    /// <param name="processPath">The running executable (<see cref="Environment.ProcessPath"/>).</param>
    /// <param name="pathVariable">The <c>PATH</c> value to search.</param>
    /// <param name="isWindows">Selects <c>dotnet.exe</c> and the <c>;</c> separator.</param>
    internal static string? Resolve(string? processPath, string? pathVariable, bool isWindows)
    {
        var fileName = isWindows ? "dotnet.exe" : "dotnet";

        var processDirectory = string.IsNullOrEmpty(processPath) ? null : Path.GetDirectoryName(processPath);
        if (!string.IsNullOrEmpty(processDirectory) && IsLaunchable(Path.Combine(processDirectory, fileName)))
        {
            return Path.GetFullPath(Path.Combine(processDirectory, fileName));
        }

        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (var rawEntry in pathVariable.Split(isWindows ? ';' : ':'))
        {
            var entry = isWindows ? rawEntry.Trim().Trim('"') : rawEntry;
            // An empty entry means the working directory on Unix; see the remarks.
            if (entry.Length == 0 || !Path.IsPathRooted(entry))
            {
                continue;
            }

            var candidate = Path.Combine(entry, fileName);
            if (IsLaunchable(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>A regular file that, off Windows, someone may execute (following a symlink).</summary>
    static bool IsLaunchable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        try
        {
            return (File.GetUnixFileMode(path) & anyExecute) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>SPY0913 for a command that found no <c>dotnet</c> and therefore did nothing.</summary>
    /// <param name="purpose">What the command uses <c>dotnet</c> for, as a clause: "`sharpyc run` starts the compiled program with `dotnet`".</param>
    internal static CompilerDiagnostic NotFound(string purpose, DotnetRequirement requirement) =>
        Create($"{purpose}, and no `dotnet` executable was found on PATH. {InstallAdvice(requirement)} Nothing was compiled.");

    /// <summary>SPY0913 for a <c>dotnet</c> that was found but could not be started (wrong architecture, removed mid-run).</summary>
    internal static CompilerDiagnostic StartFailed(string purpose, DotnetRequirement requirement, string dotnetPath, string reason) =>
        Create($"{purpose}, and '{dotnetPath}' could not be started: {reason.TrimEnd().TrimEnd('.')}. {InstallAdvice(requirement)}");

    static string InstallAdvice(DotnetRequirement requirement) => requirement switch
    {
        DotnetRequirement.Sdk => $"Install the .NET 10 SDK ({DownloadUrl}) and put `dotnet` on PATH.",
        _ => $"Install the .NET 10 runtime or SDK ({DownloadUrl}) and put `dotnet` on PATH.",
    };

    static CompilerDiagnostic Create(string message) => new(
        message,
        CompilerDiagnosticSeverity.Error,
        Code: DiagnosticCodes.Infrastructure.DotnetHostUnavailable,
        Phase: CompilerPhase.Assembly);
}
