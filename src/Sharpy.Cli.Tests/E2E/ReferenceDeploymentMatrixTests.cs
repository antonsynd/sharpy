using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Sharpy.Cli.Services;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #2223 deployment axis: a program compiled against a user reference runs from its output
/// directory. The deployment set is the used closure plus every explicit reference, because a
/// referenced Sharpy library's imports resolve through the CLR-namespace arms, which record no
/// usage. Before the fix no route copied the library, and <c>dotnet out/main.dll</c> died with
/// FileNotFoundException for it.
/// </summary>
/// <remarks>
/// <para>The matrix is reference source × route, wherever the source applies. The sources are
/// <c>-r</c> as an absolute path, <c>-r</c> as a bare name found on <c>-m</c>,
/// <c>&lt;Reference Include="abs"/&gt;</c>, and <c>&lt;Reference Include="Tablegreeter"&gt;</c> with a
/// <c>&lt;HintPath&gt;</c> both absolute and relative to the .spyproj directory. The routes are
/// <c>compile x.spy</c>, <c>compile x.spyproj</c>, <c>run</c> and <c>run --server</c> against a
/// <c>sharpyc server</c> started for the cell. The <c>&lt;Reference&gt;</c> sources apply only to a
/// .spyproj. Each cell asserts the output directory holds <c>Tablegreeter.dll</c> and that
/// <c>dotnet &lt;out&gt;</c> prints the library's line; a run cell also reads the line after its
/// banner.</para>
/// <para>The controls are the documented opt-outs. <c>--no-deps</c> on <c>compile</c> (both kinds),
/// <c>build</c> and <c>project</c> write the assembly only, so the library is absent although the
/// assembly is present.</para>
/// <para>The two self-contained cells shell out to <c>dotnet publish</c>, so they are slow. They are
/// untraited, like <see cref="SelfContainedDeploymentTests"/>, and run in the regular suite.</para>
/// <para>Every process is the real <c>sharpyc.dll</c>, as in <see cref="StandaloneDeploymentTests"/>,
/// so the library is loaded once per compile and never collides with the test process's copy.</para>
/// </remarks>
public sealed class ReferenceDeploymentMatrixTests : IClassFixture<ReferenceLibraryFixture>, IDisposable
{
    const string RunBanner = "=== Running Program ===";
    const string LibraryLine = "hello from lib";
    const string LibraryFile = "Tablegreeter.dll";
    const string GreeterMain = "from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n";

    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    readonly ReferenceLibraryFixture _library;
    readonly TempWorkspace _ws = new();

    public ReferenceDeploymentMatrixTests(ReferenceLibraryFixture library)
    {
        _library = library;
    }

    public void Dispose() => _ws.Dispose();

    public static TheoryData<string, string> Cells()
    {
        var data = new TheoryData<string, string>();
        foreach (var source in new[] { "r-absolute", "r-bare-name+m" })
        {
            foreach (var route in new[] { "compile .spy", "compile .spyproj", "run", "run --server" })
                data.Add(source, route);
        }

        foreach (var source in new[] { "Reference Include", "Reference HintPath absolute", "Reference HintPath relative" })
            data.Add(source, "compile .spyproj");

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void UserReference_IsDeployedBesideTheOutput_AndTheProgramRunsFromThere(string source, string route)
    {
        var cell = $"[{source} × {route}]";
        var outDir = _ws.PathFor("out");
        var (input, extraArgs) = Input(source, route);

        string assembly;
        if (route.StartsWith("compile", StringComparison.Ordinal))
        {
            assembly = Path.Combine(outDir, route == "compile .spy" ? "main.dll" : "App.dll");
            var compile = ExecCli(new[] { "compile", input, "-o", assembly }.Concat(extraArgs).ToArray());
            compile.ExitCode.Should().Be(0, $"{cell} compile\n{compile.StdOut}\n{compile.StdErr}");
        }
        else
        {
            assembly = Path.Combine(outDir, "main.exe");
            var args = new[] { "run", input, "-o", assembly }.Concat(extraArgs).ToList();
            ProcessResult run;
            if (route == "run --server")
            {
                var pipe = "spy-rd-" + Guid.NewGuid().ToString("N")[..8];
                using var server = StartServer(pipe);
                try
                {
                    WaitUntilListening(pipe, server);
                    args.Add($"--server={pipe}");
                    run = ExecCli(args.ToArray());
                }
                finally
                {
                    TryShutdown(pipe);
                    StopServer(server);
                }

                run.StdErr.Should().NotContain("compiling in-process", $"{cell} the server must serve the compile");
            }
            else
            {
                run = ExecCli(args.ToArray());
            }

            run.ExitCode.Should().Be(0, $"{cell} run\n{run.StdOut}\n{run.StdErr}");
            AfterBanner(run.StdOut).Should().Be(LibraryLine, $"{cell} the program's own line follows the banner");
        }

        File.Exists(assembly).Should().BeTrue($"{cell} the assembly is written");
        File.Exists(Path.Combine(outDir, LibraryFile)).Should().BeTrue($"{cell} the reference is deployed beside the output");

        var standalone = Exec(outDir, "dotnet", assembly);
        standalone.ExitCode.Should().Be(0, $"{cell} dotnet {Path.GetFileName(assembly)}\n{standalone.StdOut}\n{standalone.StdErr}");
        Normalize(standalone.StdOut).Should().Be(LibraryLine, cell);
    }

    /// <summary>
    /// The documented opt-outs write the assembly only. Each is the matching matrix cell with the
    /// opt-out applied, so the assembly's presence is the positive control for the library's absence.
    /// </summary>
    [Theory]
    [InlineData("compile .spy --no-deps")]
    [InlineData("compile .spyproj --no-deps")]
    [InlineData("build")]
    [InlineData("project")]
    public void Control_TheOptOuts_WriteTheAssemblyOnly(string control)
    {
        var outDir = _ws.PathFor("out");
        string assembly;
        string assemblyDir;
        ProcessResult result;
        switch (control)
        {
            case "compile .spy --no-deps":
                assembly = Path.Combine(outDir, "main.dll");
                result = ExecCli("compile", WriteMain(), "-r", _library.DllPath, "-o", assembly, "--no-deps");
                break;
            case "compile .spyproj --no-deps":
                assembly = Path.Combine(outDir, "App.dll");
                result = ExecCli("compile", WriteProject($"<Reference Include=\"{_library.DllPath}\" />"), "-o", assembly, "--no-deps");
                break;
            case "build":
                assembly = Path.Combine(outDir, "main.dll");
                result = ExecCli("build", WriteMain(), "-r", _library.DllPath, "-o", assembly);
                break;
            case "project":
                var project = WriteProject($"<Reference Include=\"{_library.DllPath}\" />");
                result = ExecCli("project", project);
                // An exe project writes bin/<Configuration>/<TFM>/App.exe (ProjectConfig.OutputAssemblyPath).
                var binDir = Path.Combine(Path.GetDirectoryName(project)!, "bin");
                assembly = Directory.Exists(binDir)
                    ? Directory.GetFiles(binDir, "App.exe", SearchOption.AllDirectories).FirstOrDefault() ?? Path.Combine(binDir, "App.exe")
                    : Path.Combine(binDir, "App.exe");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(control), control, null);
        }

        assemblyDir = Path.GetDirectoryName(assembly)!;
        result.ExitCode.Should().Be(0, $"[{control}]\n{result.StdOut}\n{result.StdErr}");
        File.Exists(assembly).Should().BeTrue($"[{control}] the assembly is written (positive control)");
        File.Exists(Path.Combine(assemblyDir, LibraryFile)).Should().BeFalse($"[{control}] writes the assembly only");
    }

    [Fact]
    public void SelfContained_Compile_PublishesTheReference()
    {
        var outDir = _ws.PathFor("sc");
        var compile = ExecCli("compile", "--self-contained", WriteMain(), "-r", _library.DllPath, "-o", Path.Combine(outDir, "main.dll"));
        compile.ExitCode.Should().Be(0, $"compile --self-contained\n{compile.StdOut}\n{compile.StdErr}");

        var exe = Path.Combine(outDir, OperatingSystem.IsWindows() ? "main.exe" : "main");
        File.Exists(exe).Should().BeTrue("the published executable is written");
        var run = Exec(outDir, exe);
        run.ExitCode.Should().Be(0, $"the published executable\n{run.StdOut}\n{run.StdErr}");
        Normalize(run.StdOut).Should().Be(LibraryLine);
    }

    [Fact]
    public void SelfContained_Run_PublishesTheReference()
    {
        var run = ExecCli("run", "--self-contained", WriteMain(), "-r", _library.DllPath);
        run.ExitCode.Should().Be(0, $"run --self-contained\n{run.StdOut}\n{run.StdErr}");
        AfterBanner(run.StdOut).Should().Be(LibraryLine);
    }

    // ── inputs ──────────────────────────────────────────────────────────────

    (string Input, string[] ExtraArgs) Input(string source, string route)
    {
        var commandLineReference = source switch
        {
            "r-absolute" => new[] { "-r", _library.DllPath },
            "r-bare-name+m" => new[] { "-r", "Tablegreeter", "-m", _library.Directory },
            _ => Array.Empty<string>(),
        };

        if (route is "compile .spy" or "run" or "run --server")
            return (WriteMain(), commandLineReference);

        var projectDir = _ws.PathFor("app");
        var item = source switch
        {
            "Reference Include" => $"<Reference Include=\"{_library.DllPath}\" />",
            "Reference HintPath absolute" => $"<Reference Include=\"Tablegreeter\"><HintPath>{_library.DllPath}</HintPath></Reference>",
            "Reference HintPath relative" =>
                $"<Reference Include=\"Tablegreeter\"><HintPath>{Path.GetRelativePath(projectDir, _library.DllPath)}</HintPath></Reference>",
            _ => "",
        };
        return (WriteProject(item), commandLineReference);
    }

    string WriteMain() => _ws.WriteSpy(GreeterMain, "main.spy");

    /// <summary>Writes <c>app/main.spy</c> plus an exe <c>app/App.spyproj</c> carrying <paramref name="referenceItem"/>.</summary>
    string WriteProject(string referenceItem)
    {
        _ws.WriteFile(Path.Combine("app", "main.spy"), GreeterMain);
        return _ws.WriteFile(Path.Combine("app", "App.spyproj"),
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>App</RootNamespace>\n" +
            "    <OutputType>exe</OutputType>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SpyFile Include=\"main.spy\" />\n" +
            $"    {referenceItem}\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
    }

    // ── processes ───────────────────────────────────────────────────────────

    /// <summary>The real <c>sharpyc.dll</c>, from the workspace root: never the .spyproj directory, so
    /// a relative <c>&lt;HintPath&gt;</c> resolves against the project, not the working directory.</summary>
    ProcessResult ExecCli(params string[] args) =>
        Exec(_ws.Root, "dotnet", new[] { CliDll }.Concat(args).ToArray());

    static ProcessResult Exec(string workingDirectory, string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // A self-contained publish restores and builds a runtime pack, so it needs a generous budget.
        process.WaitForExit(300_000).Should().BeTrue("the process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    static Process StartServer(string pipeName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        psi.ArgumentList.Add("server");
        psi.ArgumentList.Add("--pipe");
        psi.ArgumentList.Add(pipeName);
        // A small idle timeout, so a leaked server ends itself even if shutdown and kill are missed.
        psi.ArgumentList.Add("--idle-timeout");
        psi.ArgumentList.Add("60");

        var process = Process.Start(psi)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    static void WaitUntilListening(string pipeName, Process server)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            server.HasExited.Should().BeFalse("the server process must stay alive while starting");
            if (CompileServerClient.TrySend(
                    pipeName,
                    new CompileServerRequest { Command = CompileServerProtocol.CommandPing },
                    out var response,
                    out _)
                && response != null)
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new Xunit.Sdk.XunitException($"server on pipe '{pipeName}' never started listening");
    }

    static void TryShutdown(string pipeName) =>
        CompileServerClient.TrySend(
            pipeName,
            new CompileServerRequest { Command = CompileServerProtocol.CommandShutdown },
            out _,
            out _);

    static void StopServer(Process server)
    {
        try
        {
            if (!server.WaitForExit(10_000))
            {
                server.Kill(entireProcessTree: true);
                server.WaitForExit(5_000);
            }
        }
        catch
        {
            // Best-effort teardown.
        }
    }

    static string AfterBanner(string stdout)
    {
        var index = stdout.LastIndexOf(RunBanner, StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, $"run output must contain the '{RunBanner}' banner:\n{stdout}");
        return stdout[(index + RunBanner.Length)..].Trim('\r', '\n', ' ');
    }

    static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');

    sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
