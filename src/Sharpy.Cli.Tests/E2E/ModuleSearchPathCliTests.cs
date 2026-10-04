using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #2233: a module search path given on the command line (<c>-m</c>) or in a <c>.spyproj</c>
/// (<c>&lt;ModulePath&gt;</c>) reaches import resolution on every command.
/// </summary>
/// <remarks>
/// <para>
/// At 21ca52b5d, <c>sharpyc run main.spy -m mods</c> was SPY0300 for <c>mods/helper.spy</c>
/// (measured), because the path reached single-file discovery and the module registry but not
/// the project pipeline's import resolver. The same was true for both spellings on every
/// command, and for #2216's dotted import (<c>emit csharp src/eng/b.spy -m src -t library</c>).
/// </para>
/// <para>
/// Cells: the spelling {<c>-m</c> relative to the working directory, <c>-m</c> absolute} × the command
/// {run, compile, emit csharp} × the import form {<c>from m import f</c>, <c>import m</c>}. Each
/// program imports a flat module and a dotted package module. Run and compile cells execute the
/// program. The <c>.spyproj</c> cells (<c>project</c> with <c>&lt;ModulePath&gt;</c>,
/// <c>compile app.spyproj -m mods</c>) and #2216's repro sit beside them, and so does a no-path
/// control per command. The process is spawned so the working directory a relative <c>-m</c>
/// resolves against is the workspace and not the test host's. The compiler-level
/// <c>&lt;ModulePath&gt;</c>/<c>&lt;SourceRoot&gt;</c> cells are in
/// <c>Sharpy.Compiler.Tests.Project.ModuleSearchPathTests</c>.
/// </para>
/// </remarks>
public class ModuleSearchPathCliTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private const string Helper = "def helper_value() -> int:\n    return 42\n";
    private const string PkgMod = "def f() -> int:\n    return 7\n";

    private const string FromImports =
        "from helper import helper_value\nfrom pkg.mod import f\n\n"
        + "def main():\n    print(helper_value())\n    print(f())\n";

    private const string PlainImports =
        "import helper\nimport pkg.mod\n\n"
        + "def main():\n    print(helper.helper_value())\n    print(pkg.mod.f())\n";

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private void WriteLayout(string form)
    {
        _ws.WriteSpy(form == "from" ? FromImports : PlainImports);
        _ws.WriteFile(Path.Combine("mods", "helper.spy"), Helper);
        _ws.WriteFile(Path.Combine("mods", "pkg", "mod.spy"), PkgMod);
    }

    private string[] ModulePathArgs(string spelling) => spelling switch
    {
        "relative" => new[] { "-m", "mods" },
        // The workspace's canonical spelling: the spawned compiler roots the entry at its working
        // directory, which the OS reports with every symlink resolved (macOS: /var -> /private/var).
        // A search path spelled through a symlink is a different cell, pinned known-red below.
        "absolute" => new[] { "-m", Path.Combine(RealPath(_ws.Root), "mods") },
        _ => Array.Empty<string>(),
    };

    [Theory]
    [InlineData("relative", "run", "from")]
    [InlineData("relative", "run", "plain")]
    [InlineData("absolute", "run", "from")]
    [InlineData("absolute", "run", "plain")]
    [InlineData("relative", "compile", "from")]
    [InlineData("relative", "compile", "plain")]
    [InlineData("absolute", "compile", "from")]
    [InlineData("absolute", "compile", "plain")]
    [InlineData("relative", "emit", "from")]
    [InlineData("relative", "emit", "plain")]
    [InlineData("absolute", "emit", "from")]
    [InlineData("absolute", "emit", "plain")]
    public void ModulePathOption_ReachesImportResolution(string spelling, string command, string form)
    {
        WriteLayout(form);
        var cell = $"[-m {spelling} · {command} · {form}]";

        var result = InvokeCommand(command, ModulePathArgs(spelling));
        var combined = result.StdOut + "\n" + result.StdErr;

        combined.Should().NotContain("SPY0300", $"{cell} the module is found through -m");
        result.ExitCode.Should().Be(0, $"{cell} {combined}");
        if (command == "emit")
        {
            File.Exists(_ws.PathFor(Path.Combine("mods", "helper.cs"))).Should().BeTrue($"{cell} the module is compiled");
            File.Exists(_ws.PathFor(Path.Combine("mods", "pkg", "mod.cs"))).Should().BeTrue($"{cell} the module is compiled");
        }
        else if (command == "run")
        {
            // `run` may report the compile ("Successfully compiled to: …") before the program's output.
            Normalize(result.StdOut).Split('\n').TakeLast(3).Should().Equal(new[] { "42", "7", "" }, $"{cell} {combined}");
        }
        else
        {
            var run = Exec("dotnet", _ws.PathFor(Path.Combine("out", "app.dll")));
            Normalize(run.StdOut).Should().Be("42\n7\n", $"{cell} {run.StdErr}");
        }
    }

    /// <summary>Control: with no <c>-m</c> the same layout is SPY0300 on every command.</summary>
    [Theory]
    [InlineData("run")]
    [InlineData("compile")]
    [InlineData("emit")]
    public void WithoutModulePath_TheSameImportIsSPY0300(string command)
    {
        WriteLayout("from");

        var result = InvokeCommand(command, Array.Empty<string>());
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().NotBe(0, combined);
        combined.Should().Contain("error[SPY0300]: Cannot find module 'helper'", $"[{command}]");
    }

    [Theory]
    [InlineData("from")]
    [InlineData("plain")]
    public void ProjectModulePathItem_ReachesImportResolution(string form)
    {
        WriteLayout(form);
        WriteProject(modulePathItem: true);

        var result = ExecCli("project", _ws.PathFor("app.spyproj"));
        var combined = result.StdOut + "\n" + result.StdErr;

        combined.Should().NotContain("SPY0300", $"[<ModulePath> · project · {form}]");
        result.ExitCode.Should().Be(0, combined);
        combined.Should().Contain("Build succeeded.");
    }

    [Theory]
    [InlineData("from", true)]
    [InlineData("plain", true)]
    [InlineData("from", false)]
    [InlineData("plain", false)]
    public void CompileSpyproj_ModulePathItemOrOption_ReachesImportResolution(string form, bool viaItem)
    {
        WriteLayout(form);
        WriteProject(modulePathItem: viaItem);
        var cell = viaItem ? $"[<ModulePath> · compile · {form}]" : $"[compile app.spyproj -m mods · {form}]";
        var output = _ws.PathFor(Path.Combine("out", "app.dll"));

        var args = new System.Collections.Generic.List<string> { "compile", "app.spyproj", "-o", output };
        if (!viaItem)
            args.AddRange(new[] { "-m", "mods" });
        var result = ExecCli(args.ToArray());
        var combined = result.StdOut + "\n" + result.StdErr;

        combined.Should().NotContain("SPY0300", cell);
        result.ExitCode.Should().Be(0, $"{cell} {combined}");
        var run = Exec("dotnet", output);
        Normalize(run.StdOut).Should().Be("42\n7\n", $"{cell} {run.StdErr}");
    }

    /// <summary>Control: the same project with neither the item nor the option is SPY0300.</summary>
    [Fact]
    public void CompileSpyproj_WithoutAnySearchPath_IsSPY0300()
    {
        WriteLayout("from");
        WriteProject(modulePathItem: false);

        var result = ExecCli("compile", "app.spyproj", "-o", _ws.PathFor(Path.Combine("out", "app.dll")));
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().NotBe(0, combined);
        combined.Should().Contain("error[SPY0300]: Cannot find module 'helper'");
    }

    /// <summary>
    /// #2216's repro: a package module emitted as its own entry, its sibling imported by the dotted
    /// name spelled from the <c>-m</c> root. The no-<c>-m</c> control stays SPY0300.
    /// </summary>
    [Theory]
    [InlineData("emit", true)]
    [InlineData("compile", true)]
    [InlineData("emit", false)]
    public void Issue2216_DottedImportFromPackageEntry_ResolvesThroughModulePath(string command, bool withModulePath)
    {
        _ws.WriteFile(Path.Combine("src", "eng", "a.spy"), "def one() -> int:\n    return 1\n");
        _ws.WriteFile(Path.Combine("src", "eng", "b.spy"),
            "from eng.a import one\n\ndef two() -> int:\n    return one() + 1\n");
        var entry = Path.Combine("src", "eng", "b.spy");

        var args = command == "emit"
            ? new System.Collections.Generic.List<string> { "emit", "csharp", entry, "-t", "library" }
            : new System.Collections.Generic.List<string> { "compile", entry, "-t", "library", "-o", _ws.PathFor(Path.Combine("out", "eng.dll")) };
        if (withModulePath)
            args.AddRange(new[] { "-m", "src" });
        var result = ExecCli(args.ToArray());
        var combined = result.StdOut + "\n" + result.StdErr;

        if (withModulePath)
        {
            combined.Should().NotContain("SPY0300", $"[#2216 · {command}]");
            result.ExitCode.Should().Be(0, combined);
        }
        else
        {
            result.ExitCode.Should().NotBe(0, combined);
            combined.Should().Contain("error[SPY0300]: Cannot find module 'eng.a'");
        }
    }

    /// <summary>
    /// Known-red row, deliberately not asserted: an absolute <c>-m</c> spelled through a symlink while
    /// the entry's directory is the canonical spelling. The import resolves and the program runs, but
    /// the module's name is derived from a path relative to the entry's directory across the two
    /// spellings, so <c>emit csharp</c> writes <c>var/folders/…/mods/helper.cs</c> under the entry's
    /// directory and the namespace is <c>SharpyApp._._._….Var.Folders….Mods.Helper</c> (measured on
    /// macOS, where the temp directory sits below the <c>/var</c> symlink). Path identity belongs to
    /// the one path authority (#2127); recorded in the #2233 round's findings ledger.
    /// </summary>
    [Fact(Skip = "Known red, not asserted: a module path spelled through a symlink derives the module name across two path spellings (#2127; #2233 findings ledger)")]
    public void AbsoluteModulePath_SpelledThroughASymlink_KnownRed()
    {
        WriteLayout("from");
        var link = _ws.PathFor("link");
        Directory.CreateSymbolicLink(link, RealPath(_ws.Root));

        var result = ExecCli("emit", "csharp", "main.spy", "-m", Path.Combine(link, "mods"));

        result.ExitCode.Should().Be(0, result.StdOut + result.StdErr);
        File.ReadAllText(_ws.PathFor(Path.Combine("mods", "helper.cs"))).Should().Contain("namespace SharpyApp.Mods.Helper");
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <paramref name="path"/> with every symlinked component resolved: the spelling a process
    /// started in that directory reports as its working directory.
    /// </summary>
    private static string RealPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
            if (target != null)
                current = target.FullName;
        }
        return current;
    }

    private ProcessResult InvokeCommand(string command, string[] modulePathArgs)
    {
        var args = command switch
        {
            "run" => new System.Collections.Generic.List<string> { "run", "main.spy" },
            "compile" => new System.Collections.Generic.List<string> { "compile", "main.spy", "-o", _ws.PathFor(Path.Combine("out", "app.dll")) },
            _ => new System.Collections.Generic.List<string> { "emit", "csharp", "main.spy" },
        };
        args.AddRange(modulePathArgs);
        return ExecCli(args.ToArray());
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private void WriteProject(bool modulePathItem) => _ws.WriteFile("app.spyproj",
        "<Project>\n"
        + "  <PropertyGroup>\n"
        + "    <RootNamespace>App</RootNamespace>\n"
        + "    <OutputType>exe</OutputType>\n"
        + "    <TargetFramework>net10.0</TargetFramework>\n"
        + "    <EntryPoint>main.spy</EntryPoint>\n"
        + "  </PropertyGroup>\n"
        + "  <ItemGroup>\n"
        + "    <SpyFile Include=\"**/*.spy\" />\n"
        + (modulePathItem ? "    <ModulePath Include=\"mods\" />\n" : "")
        + "  </ItemGroup>\n"
        + "</Project>\n");

    private ProcessResult ExecCli(params string[] args)
        => Exec("dotnet", new[] { CliDll }.Concat(args).ToArray());

    private ProcessResult Exec(string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = _ws.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(180_000).Should().BeTrue("the process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
