using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The one resolver every <c>dotnet</c> launch site starts through (#2292): the running executable's
/// directory, then <c>PATH</c> in order; never the working directory; only a launchable file.
/// Each row builds its own directories, so no row reads the test host's PATH except the last,
/// which is the positive control that the real resolver sees the dotnet running these tests.
/// </summary>
public class DotnetHostTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpy-dotnethost-" + Guid.NewGuid().ToString("N")[..8]);

    public DotnetHostTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static readonly bool IsWindows = OperatingSystem.IsWindows();
    private static string HostName => IsWindows ? "dotnet.exe" : "dotnet";
    private static string HostPath(params string[] directories) => string.Join(Path.PathSeparator, directories);

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Executable(string directory, string fileName, bool executable = true)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, executable
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    [Fact]
    public void Path_FirstEntryHoldingDotnetWins()
    {
        var empty = Dir("empty");
        var first = Dir("first");
        var second = Dir("second");
        var expected = Executable(first, HostName);
        Executable(second, HostName);

        Assert.Equal(expected, DotnetHost.Resolve(processPath: null, HostPath(empty, first, second), IsWindows));
    }

    [Fact]
    public void NoEntryHoldsDotnet_ResolvesNothing()
    {
        var a = Dir("a");
        var b = Dir("b");
        Executable(a, "not-dotnet");

        Assert.Null(DotnetHost.Resolve(processPath: null, HostPath(a, b), IsWindows));
        Assert.Null(DotnetHost.Resolve(processPath: null, pathVariable: null, IsWindows));
        Assert.Null(DotnetHost.Resolve(processPath: null, pathVariable: "", IsWindows));
    }

    [Fact]
    public void RunningExecutablesDirectory_IsSearchedBeforePath()
    {
        // Under `dotnet sharpyc.dll` the running executable IS the muxer; its directory answers even
        // with no PATH at all, exactly as Process.Start's own lookup did before #2292.
        var muxerDir = Dir("muxer");
        var onPath = Dir("onpath");
        var expected = Executable(muxerDir, HostName);
        Executable(onPath, HostName);
        var processPath = Path.Combine(muxerDir, HostName);

        Assert.Equal(expected, DotnetHost.Resolve(processPath, HostPath(onPath), IsWindows));
        Assert.Equal(expected, DotnetHost.Resolve(processPath, pathVariable: null, IsWindows));
        // An apphost's directory with no dotnet beside it falls through to PATH.
        var apphostDir = Dir("apphost");
        Assert.Equal(Path.Combine(onPath, HostName),
            DotnetHost.Resolve(Path.Combine(apphostDir, "sharpyc"), HostPath(onPath), IsWindows));
    }

    [Fact]
    public void RelativeAndEmptyEntries_AreNotTheWorkingDirectory()
    {
        // Under the working directory, not the temp root: on Windows the two can sit on different
        // drives, where no relative spelling exists and GetRelativePath answers with an absolute path.
        var relative = "sharpy-dotnethost-rel-" + Guid.NewGuid().ToString("N")[..8];
        var dir = Path.Combine(Directory.GetCurrentDirectory(), relative);
        Directory.CreateDirectory(dir);
        try
        {
            Executable(dir, HostName);
            Assert.False(Path.IsPathRooted(relative), "the entry under test must be relative");

            // Positive control: the same directory spelled absolutely resolves.
            Assert.NotNull(DotnetHost.Resolve(processPath: null, HostPath(dir), IsWindows));
            Assert.Null(DotnetHost.Resolve(processPath: null, HostPath(relative), IsWindows));
            Assert.Null(DotnetHost.Resolve(processPath: null, HostPath("", relative, ""), IsWindows));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ADirectoryNamedDotnet_IsNotLaunchable()
    {
        var dir = Dir("holder");
        Directory.CreateDirectory(Path.Combine(dir, HostName));

        Assert.Null(DotnetHost.Resolve(processPath: null, HostPath(dir), IsWindows));
    }

    [Fact]
    public void WithoutAnExecuteBit_ItIsSkipped_AndALaterEntryAnswers()
    {
        if (OperatingSystem.IsWindows())
            return; // no execute bit to clear

        var plain = Dir("plain");
        var real = Dir("real");
        Executable(plain, "dotnet", executable: false);
        var expected = Executable(real, "dotnet");

        Assert.Null(DotnetHost.Resolve(processPath: null, HostPath(plain), isWindows: false));
        Assert.Equal(expected, DotnetHost.Resolve(processPath: null, HostPath(plain, real), isWindows: false));
    }

    [Fact]
    public void ASymlinkToAnExecutable_Resolves()
    {
        if (OperatingSystem.IsWindows())
            return; // symlink creation needs a privilege the Windows runner may not grant

        // Homebrew's /opt/homebrew/bin/dotnet is a symlink into the Cellar.
        var cellar = Dir("cellar");
        var bin = Dir("bin");
        var target = Executable(cellar, "dotnet");
        var link = Path.Combine(bin, "dotnet");
        File.CreateSymbolicLink(link, target);

        Assert.Equal(link, DotnetHost.Resolve(processPath: null, HostPath(bin), isWindows: false));
    }

    [Fact]
    public void WindowsSpelling_DotnetExe_SemicolonSeparated_QuotedEntries()
    {
        var a = Dir("win-a");
        var b = Dir("win-b");
        Executable(a, "dotnet"); // the Unix name is not the Windows name
        var expected = Executable(b, "dotnet.exe");

        Assert.Equal(expected, DotnetHost.Resolve(processPath: null, $"{a}; \"{b}\" ", isWindows: true));
    }

    [Fact]
    public void TheTestHost_ResolvesTheDotnetRunningIt()
    {
        // Positive control for every null above: the real lookup (process directory, then this
        // host's PATH) finds a dotnet on a machine that is running .NET tests.
        var resolved = DotnetHost.Resolve();

        Assert.NotNull(resolved);
        Assert.True(File.Exists(resolved));
        Assert.Equal(HostName, Path.GetFileName(resolved));
    }
}
