using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// <c>project app.spyproj -r &lt;dll&gt;</c> (#2223): the option joins the project's
/// <c>&lt;Reference&gt;</c> items exactly as for <c>compile app.spyproj</c>, through the one merge
/// (<c>CliHelpers.MergeCommandLineIntoProject</c>). Before, <c>project</c> rejected <c>-r</c> as an
/// unknown option, so the two project commands diverged. Spawned, so the library is not already
/// loaded into the compiling process by another test.
/// </summary>
public class ProjectReferenceOptionTests : IClassFixture<ReferenceLibraryFixture>, IDisposable
{
    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    readonly ReferenceLibraryFixture _library;
    readonly TempWorkspace _ws = new();

    public ProjectReferenceOptionTests(ReferenceLibraryFixture library)
    {
        _library = library;
        _ws.WriteFile(Path.Combine("src", "main.spy"),
            "from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n");
        _ws.WriteFile("App.spyproj",
            "<Project>\n  <PropertyGroup>\n    <RootNamespace>App</RootNamespace>\n    <OutputType>exe</OutputType>\n"
            + "  </PropertyGroup>\n  <ItemGroup>\n    <SpyFile Include=\"src/**/*.spy\" />\n  </ItemGroup>\n</Project>\n");
    }

    public void Dispose() => _ws.Dispose();

    [Theory]
    [InlineData("absolute")]
    [InlineData("bare-name-on-module-path")]
    public void AReference_IsHonoured_AndAMissingOneIsSpy0305(string spelling)
    {
        var referenceArgs = spelling == "absolute"
            ? new[] { "-r", _library.DllPath }
            : new[] { "-r", Path.GetFileName(_library.DllPath), "-m", _library.Directory };

        var without = ExecCli("project", "App.spyproj");
        (without.StdOut + without.StdErr).Should().Contain("SPY0300", "control: the import needs the reference");
        DeleteBin();

        var with = ExecCli(new[] { "project", "App.spyproj" }.Concat(referenceArgs).ToArray());
        var combined = with.StdOut + with.StdErr;
        combined.Should().NotContain("was not matched").And.NotContain("SPY0300");
        with.ExitCode.Should().Be(0, combined);
        combined.Should().Contain("Build succeeded.");
        DeleteBin();

        var missing = _ws.PathFor(Path.Combine("does", "not", "exist.dll"));
        var refused = ExecCli("project", "App.spyproj", "-r", missing);
        refused.ExitCode.Should().Be(1, refused.StdOut + refused.StdErr);
        refused.StdErr.Should().Contain("SPY0305").And.Contain($"Assembly not found: {missing}");
        Directory.Exists(_ws.PathFor("bin")).Should().BeFalse("the refusal comes before anything is compiled");
    }

    void DeleteBin()
    {
        foreach (var dir in new[] { "bin", "obj" })
        {
            var path = _ws.PathFor(dir);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

    ProcessResult ExecCli(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _ws.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"sharpyc {string.Join(' ', args)} did not exit within 2 minutes");
        }

        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
