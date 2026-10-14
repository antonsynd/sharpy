using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// Effect evidence for the honoured cells of the option-to-effect registry
/// (<c>CommandOptionEffects</c>, #2223) that no other test observed: each test invokes the command
/// with the option and asserts an observable effect against a CONTROL — the same invocation without
/// the option, or with another value — so a dropped option turns it red. The sweep
/// (<c>CliOptionEffectSweepTests</c>) references these methods by name.
/// <para>
/// A program started by <c>run</c> writes to the real console, not the captured writers, so a
/// <c>run</c> test's program reports through its exit code (<c>sys.exit(7)</c>).
/// </para>
/// </summary>
public class OptionEffectEvidenceTests
{
    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    const string PlainMain = "def main():\n    print(\"plain\")\n";

    /// <summary>A module with no <c>main</c>: an exe needs an entry point (SPY0403), a library does not.</summary>
    const string MainlessModule = "def helper() -> int:\n    return 1\n";

    /// <summary>A program whose only observable from an in-process <c>run</c> is its exit code.</summary>
    const string ExitSevenMain = "import sys\n\ndef main():\n    sys.exit(7)\n";

    /// <summary>Writes <c>src/main.spy</c> plus an exe <c>App.spyproj</c> including <c>src/**/*.spy</c>.</summary>
    static string WriteProject(TempWorkspace ws, string mainSource = PlainMain)
    {
        ws.WriteFile(Path.Combine("src", "main.spy"), mainSource);
        return ws.WriteFile("App.spyproj",
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>App</RootNamespace>\n" +
            "    <OutputType>exe</OutputType>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SpyFile Include=\"src/**/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
    }

    static string Show(CliInvocation invocation) => invocation.StdOut + invocation.StdErr;

    /// <summary>The files named <paramref name="fileName"/> anywhere under <paramref name="directory"/> (none if it is absent).</summary>
    static IReadOnlyList<string> FilesNamed(string directory, string fileName) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories).ToList()
            : Array.Empty<string>();

    /// <summary>Reads a log file the logger may still hold open (nothing disposes it).</summary>
    static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The namespaces of the types an assembly defines, read from metadata without loading it.</summary>
    static IReadOnlyList<string> DefinedNamespaces(string assemblyPath)
    {
        using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.TypeDefinitions
            .Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Namespace))
            .Where(ns => ns.Length > 0)
            .Distinct()
            .ToList();
    }

    // ---- build / compile --type ----

    [Fact]
    public void Build_TypeLibrary_CompilesAMainlessFile()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(MainlessModule, "helper.spy");
        var exe = ws.PathFor(Path.Combine("exe", "Helper.dll"));
        var lib = ws.PathFor(Path.Combine("lib", "Helper.dll"));

        var control = CliTestHarness.Invoke($"build \"{spy}\" -o \"{exe}\"");
        var library = CliTestHarness.Invoke($"build \"{spy}\" -t library -o \"{lib}\"");

        control.ExitCode.Should().NotBe(0, "control: an exe needs an entry point\n" + Show(control));
        control.StdErr.Should().Contain("SPY0403");
        File.Exists(exe).Should().BeFalse();
        library.ExitCode.Should().Be(0, Show(library));
        File.Exists(lib).Should().BeTrue("-t library compiles the main-less file to a library");
    }

    [Fact]
    public void Compile_TypeLibrary_CompilesAMainlessFile()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(MainlessModule, "helper.spy");
        var exe = ws.PathFor(Path.Combine("exe", "Helper.dll"));
        var lib = ws.PathFor(Path.Combine("lib", "Helper.dll"));

        var control = CliTestHarness.Invoke($"compile \"{spy}\" --no-deps -o \"{exe}\"");
        var library = CliTestHarness.Invoke($"compile \"{spy}\" --no-deps -t library -o \"{lib}\"");

        control.ExitCode.Should().NotBe(0, "control: an exe needs an entry point\n" + Show(control));
        control.StdErr.Should().Contain("SPY0403");
        File.Exists(exe).Should().BeFalse();
        library.ExitCode.Should().Be(0, Show(library));
        File.Exists(lib).Should().BeTrue("-t library compiles the main-less file to a library");
    }

    // ---- compile <project> --configuration / --incremental / --clean ----

    [Fact]
    public void Compile_Configuration_SelectsTheProjectOutputDirectory()
    {
        using var debugWs = new TempWorkspace();
        using var releaseWs = new TempWorkspace();
        var debugProject = WriteProject(debugWs);
        var releaseProject = WriteProject(releaseWs);

        var debug = CliTestHarness.Invoke($"compile \"{debugProject}\" --no-deps -c Debug");
        var release = CliTestHarness.Invoke($"compile \"{releaseProject}\" --no-deps -c Release");

        debug.ExitCode.Should().Be(0, Show(debug));
        release.ExitCode.Should().Be(0, Show(release));
        FilesNamed(debugWs.PathFor(Path.Combine("bin", "Debug")), "App.exe").Should().ContainSingle();
        FilesNamed(debugWs.PathFor(Path.Combine("bin", "Release")), "App.exe").Should().BeEmpty();
        FilesNamed(releaseWs.PathFor(Path.Combine("bin", "Release")), "App.exe").Should().ContainSingle();
        FilesNamed(releaseWs.PathFor(Path.Combine("bin", "Debug")), "App.exe").Should().BeEmpty();
    }

    [Fact]
    public void CompileProject_Incremental_WritesTheIncrementalCache()
    {
        using var controlWs = new TempWorkspace();
        using var incrementalWs = new TempWorkspace();
        var controlProject = WriteProject(controlWs);
        var incrementalProject = WriteProject(incrementalWs);

        var control = CliTestHarness.Invoke($"compile \"{controlProject}\" --no-deps");
        var incremental = CliTestHarness.Invoke($"compile \"{incrementalProject}\" --no-deps --incremental");

        control.ExitCode.Should().Be(0, Show(control));
        incremental.ExitCode.Should().Be(0, Show(incremental));
        FilesNamed(controlWs.PathFor("obj"), ".sharpy-cache").Should().BeEmpty("control: a full build keeps no cache");
        FilesNamed(incrementalWs.PathFor("obj"), ".sharpy-cache").Should().ContainSingle(
            "--incremental persists file hashes under the project's obj/{Configuration}");
    }

    [Fact]
    public void CompileProject_Clean_DeletesBinAndObjFirst()
    {
        using var controlWs = new TempWorkspace();
        using var cleanWs = new TempWorkspace();
        var controlProject = WriteProject(controlWs);
        var cleanProject = WriteProject(cleanWs);
        var sentinels = new[] { Path.Combine("obj", "sentinel.txt"), Path.Combine("bin", "sentinel.txt") };
        foreach (var sentinel in sentinels)
        {
            controlWs.WriteFile(sentinel, "stale");
            cleanWs.WriteFile(sentinel, "stale");
        }

        var control = CliTestHarness.Invoke($"compile \"{controlProject}\" --no-deps");
        var clean = CliTestHarness.Invoke($"compile \"{cleanProject}\" --no-deps --clean");

        control.ExitCode.Should().Be(0, Show(control));
        clean.ExitCode.Should().Be(0, Show(clean));
        foreach (var sentinel in sentinels)
        {
            File.Exists(controlWs.PathFor(sentinel)).Should().BeTrue($"control: {sentinel} survives a build without --clean");
            File.Exists(cleanWs.PathFor(sentinel)).Should().BeFalse($"--clean deletes {sentinel} before building");
        }

        FilesNamed(cleanWs.PathFor("bin"), "App.exe").Should().ContainSingle("the build still runs after the clean");
    }

    // ---- run --output / --namespace / --server ----

    [Fact]
    public void Run_Output_WritesTheAssemblyThere()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ExitSevenMain);
        var output = ws.PathFor(Path.Combine("out", "a.dll"));

        var control = CliTestHarness.Invoke($"run \"{spy}\"");
        control.ExitCode.Should().Be(7, "control: the program runs from a temporary assembly\n" + Show(control));
        File.Exists(output).Should().BeFalse("control: without -o nothing is written to the -o path");

        var invocation = CliTestHarness.Invoke($"run \"{spy}\" -o \"{output}\"");

        invocation.ExitCode.Should().Be(7, "the program ran\n" + Show(invocation));
        File.Exists(output).Should().BeTrue("-o names where run writes the assembly it executes");
        invocation.StdOut.Should().Contain($"Successfully compiled to: {output}");
    }

    [Fact]
    public void Run_Namespace_WrapsTheGeneratedCode()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ExitSevenMain);
        var plain = ws.PathFor(Path.Combine("plain", "a.dll"));
        var wrapped = ws.PathFor(Path.Combine("wrapped", "a.dll"));

        var control = CliTestHarness.Invoke($"run \"{spy}\" -o \"{plain}\"");
        var invocation = CliTestHarness.Invoke($"run \"{spy}\" -o \"{wrapped}\" -n Foo.Bar");

        control.ExitCode.Should().Be(7, Show(control));
        invocation.ExitCode.Should().Be(7, Show(invocation));
        var controlNamespaces = DefinedNamespaces(plain);
        controlNamespaces.Should().NotBeEmpty("positive control: the reader sees the program's namespaces");
        controlNamespaces.Should().NotContain(ns => ns.StartsWith("Foo.Bar", StringComparison.Ordinal));
        DefinedNamespaces(wrapped).Should().Contain(ns => ns.StartsWith("Foo.Bar", StringComparison.Ordinal),
            "-n wraps the generated code in that namespace");
    }

    [Fact]
    public void Run_Server_FallsBackToInProcessWhenNoServerRuns()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ExitSevenMain);
        // Short name: on macOS the pipe is a Unix domain socket whose path is capped at 104 chars.
        var deadPipe = "spy-absent-" + Guid.NewGuid().ToString("N")[..8];

        var control = CliTestHarness.Invoke($"run \"{spy}\"");
        var invocation = CliTestHarness.Invoke($"run \"{spy}\" --server={deadPipe}");

        control.ExitCode.Should().Be(7, Show(control));
        control.StdErr.Should().NotContain("no compile server", "control: without --server no server is tried");
        invocation.ExitCode.Should().Be(7, "the program still runs, compiled in-process\n" + Show(invocation));
        invocation.StdErr.Should().Contain($"no compile server on pipe '{deadPipe}'");
        invocation.StdErr.Should().Contain("compiling in-process");
    }

    // ---- project --configuration / --clean / --incremental ----

    [Fact]
    public void Project_Configuration_SelectsTheOutputDirectory()
    {
        using var defaultWs = new TempWorkspace();
        using var releaseWs = new TempWorkspace();
        var defaultProject = WriteProject(defaultWs);
        var releaseProject = WriteProject(releaseWs);

        var control = CliTestHarness.Invoke($"project \"{defaultProject}\"");
        var release = CliTestHarness.Invoke($"project \"{releaseProject}\" -c Release");

        control.ExitCode.Should().Be(0, Show(control));
        release.ExitCode.Should().Be(0, Show(release));
        FilesNamed(defaultWs.PathFor(Path.Combine("bin", "Debug")), "App.exe").Should().ContainSingle("the default is Debug");
        FilesNamed(defaultWs.PathFor(Path.Combine("bin", "Release")), "App.exe").Should().BeEmpty();
        FilesNamed(releaseWs.PathFor(Path.Combine("bin", "Release")), "App.exe").Should().ContainSingle();
        FilesNamed(releaseWs.PathFor(Path.Combine("bin", "Debug")), "App.exe").Should().BeEmpty();
    }

    [Fact]
    public void Project_Clean_DeletesBinAndObjFirst()
    {
        using var controlWs = new TempWorkspace();
        using var cleanWs = new TempWorkspace();
        var controlProject = WriteProject(controlWs);
        var cleanProject = WriteProject(cleanWs);
        var sentinels = new[] { Path.Combine("obj", "sentinel.txt"), Path.Combine("bin", "sentinel.txt") };
        foreach (var sentinel in sentinels)
        {
            controlWs.WriteFile(sentinel, "stale");
            cleanWs.WriteFile(sentinel, "stale");
        }

        var control = CliTestHarness.Invoke($"project \"{controlProject}\"");
        var clean = CliTestHarness.Invoke($"project \"{cleanProject}\" --clean");

        control.ExitCode.Should().Be(0, Show(control));
        clean.ExitCode.Should().Be(0, Show(clean));
        foreach (var sentinel in sentinels)
        {
            File.Exists(controlWs.PathFor(sentinel)).Should().BeTrue($"control: {sentinel} survives a build without --clean");
            File.Exists(cleanWs.PathFor(sentinel)).Should().BeFalse($"--clean deletes {sentinel} before building");
        }

        FilesNamed(cleanWs.PathFor("bin"), "App.exe").Should().ContainSingle("the build still runs after the clean");
    }

    [Fact]
    public void Project_Incremental_WritesTheIncrementalCache()
    {
        using var controlWs = new TempWorkspace();
        using var incrementalWs = new TempWorkspace();
        var controlProject = WriteProject(controlWs);
        var incrementalProject = WriteProject(incrementalWs);

        var control = CliTestHarness.Invoke($"project \"{controlProject}\"");
        var incremental = CliTestHarness.Invoke($"project \"{incrementalProject}\" --incremental");

        control.ExitCode.Should().Be(0, Show(control));
        incremental.ExitCode.Should().Be(0, Show(incremental));
        FilesNamed(controlWs.PathFor("obj"), ".sharpy-cache").Should().BeEmpty("control: a full build keeps no cache");
        FilesNamed(incrementalWs.PathFor("obj"), ".sharpy-cache").Should().ContainSingle(
            "--incremental persists file hashes under the project's obj/{Configuration}");
    }

    // ---- emit csharp --show-line-directives / --namespace / --type ----

    [Fact]
    public void EmitCSharp_ShowLineDirectives_KeepsLineDirectives()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(PlainMain);
        var stripped = ws.PathFor("stripped.cs");
        var kept = ws.PathFor("kept.cs");

        var control = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{stripped}\"");
        var invocation = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{kept}\" --show-line-directives");

        control.ExitCode.Should().Be(0, Show(control));
        invocation.ExitCode.Should().Be(0, Show(invocation));
        File.ReadAllText(stripped).Should().NotContain("#line", "control: directives are stripped by default");
        File.ReadAllText(kept).Should().Contain("#line", "--show-line-directives keeps the source mapping");
    }

    [Fact]
    public void EmitCSharp_Namespace_WrapsTheGeneratedCode()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(PlainMain);
        var plain = ws.PathFor("plain.cs");
        var wrapped = ws.PathFor("wrapped.cs");

        var control = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{plain}\"");
        var invocation = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{wrapped}\" -n Foo.Bar");

        control.ExitCode.Should().Be(0, Show(control));
        invocation.ExitCode.Should().Be(0, Show(invocation));
        var controlText = File.ReadAllText(plain);
        controlText.Should().Contain("namespace ", "positive control: the generated code declares a namespace");
        controlText.Should().NotContain("namespace Foo.Bar");
        File.ReadAllText(wrapped).Should().Contain("namespace Foo.Bar", "-n wraps the generated code in that namespace");
    }

    [Fact]
    public void EmitCSharp_TypeExe_RequiresAnEntryPoint()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(MainlessModule, "helper.spy");

        var byDefault = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{ws.PathFor("default.cs")}\"");
        var exe = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{ws.PathFor("exe.cs")}\" -t exe");
        var library = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{ws.PathFor("library.cs")}\" -t library");

        byDefault.ExitCode.Should().Be(1, Show(byDefault));
        byDefault.StdErr.Should().Contain("SPY0403", "the default type is exe, which needs main()");
        exe.ExitCode.Should().Be(1, Show(exe));
        exe.StdErr.Should().Contain("SPY0403");
        library.ExitCode.Should().Be(0, Show(library));
        File.Exists(ws.PathFor("library.cs")).Should().BeTrue("-t library needs no entry point");
    }

    // ---- emit diagnostics --include-codegen ----

    /// <summary>
    /// The analysis branch is library-typed (it never invents a missing-entry-point refusal for a file
    /// the user only asked to check); <c>--include-codegen</c> runs the compile pipeline, which is
    /// exe-typed, so a main-less file reports SPY0403 only with the flag.
    /// </summary>
    [Fact]
    public void EmitDiagnostics_IncludeCodegen_RunsTheCompilePipeline()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(MainlessModule, "helper.spy");

        var control = CliTestHarness.Invoke($"emit diagnostics \"{spy}\"");
        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\" --include-codegen");

        control.ExitCode.Should().Be(0, Show(control));
        control.StdOut.Should().Contain("No diagnostics.", "control: analysis alone finds nothing to report");
        invocation.ExitCode.Should().Be(1, Show(invocation));
        invocation.StdOut.Should().Contain("error SPY0403", "the compile pipeline requires an entry point");
    }

    // ---- emit hover --line / --col ----

    [Fact]
    public void EmitHover_LineAndCol_SelectTheHoveredSymbol()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(
            "def alpha_fn() -> int:\n    return 1\n\ndef beta_fn(x: str) -> str:\n    return x\n", "hover.spy");

        var line1Col5 = CliTestHarness.Invoke($"emit hover \"{spy}\" --line 1 --col 5");
        var line4Col5 = CliTestHarness.Invoke($"emit hover \"{spy}\" --line 4 --col 5");
        var line4Col13 = CliTestHarness.Invoke($"emit hover \"{spy}\" --line 4 --col 13");

        line1Col5.ExitCode.Should().Be(0, Show(line1Col5));
        line4Col5.ExitCode.Should().Be(0, Show(line4Col5));
        line4Col13.ExitCode.Should().Be(0, Show(line4Col13));

        // --line: the same column on two lines hovers two functions.
        line1Col5.StdOut.Should().Contain("alpha_fn").And.NotContain("beta_fn");
        line4Col5.StdOut.Should().Contain("def beta_fn(").And.NotContain("alpha_fn");
        // --col: two columns on the same line hover the function and its parameter.
        line4Col13.StdOut.Should().Contain("(parameter) x: str").And.NotContain("beta_fn");
    }

    // ---- server --idle-timeout / --log-file / --log-level / --verbose ----

    [Fact]
    public void Server_IdleTimeout_ExitsAfterIdleSeconds()
    {
        using var quick = SpawnedServer.Start("--idle-timeout", "1");
        using var patient = SpawnedServer.Start("--idle-timeout", "60");

        quick.ExitsWithin(30_000).Should().BeTrue(
            "--idle-timeout 1 shuts an unused server down by itself\n" + quick.StdErr);
        quick.StdErr.Should().Contain("idle timeout reached");

        patient.WaitForStderr("listening on pipe", TimeSpan.FromSeconds(60)).Should().BeTrue(
            "control: the 60 s server starts\n" + patient.StdErr);
        Thread.Sleep(3_000);
        patient.Process.HasExited.Should().BeFalse("control: --idle-timeout 60 is still alive 3 s after it started listening");
    }

    const string ServerLogsNothingReason =
        "Measured @ 27e261684: `sharpyc server --idle-timeout 1 --log-level Debug --log-file f` (and --verbose) " +
        "writes no [INFO]/[DEBUG] line to stdout, stderr or f (f is created empty). CompileServer stores the " +
        "logger ServerCommand builds but never calls it, and each compile request runs with NullLogger.Instance. " +
        "Reported to the P46 lead: the cell is not observable as Honoured.";

    [Fact(Skip = ServerLogsNothingReason)]
    public void Server_LogFile_ReceivesTheLog()
    {
        using var ws = new TempWorkspace();
        var log = ws.PathFor("server.log");
        using var server = SpawnedServer.Start("--idle-timeout", "1", "--log-level", "Info", "--log-file", log);

        server.ExitsWithin(30_000).Should().BeTrue(server.StdErr);
        File.Exists(log).Should().BeTrue();
        ReadShared(log).Should().Contain("[INFO]");
    }

    [Fact(Skip = ServerLogsNothingReason)]
    public void Server_LogLevel_EnablesTheLogger()
    {
        using var control = SpawnedServer.Start("--idle-timeout", "1");
        using var server = SpawnedServer.Start("--idle-timeout", "1", "--log-level", "Info");

        control.ExitsWithin(30_000).Should().BeTrue(control.StdErr);
        server.ExitsWithin(30_000).Should().BeTrue(server.StdErr);
        (control.StdOut + control.StdErr).Should().NotContain("[INFO]");
        (server.StdOut + server.StdErr).Should().Contain("[INFO]");
    }

    [Fact(Skip = ServerLogsNothingReason)]
    public void Server_Verbose_RaisesTheLogLevel()
    {
        using var control = SpawnedServer.Start("--idle-timeout", "1");
        using var server = SpawnedServer.Start("--idle-timeout", "1", "--verbose");

        control.ExitsWithin(30_000).Should().BeTrue(control.StdErr);
        server.ExitsWithin(30_000).Should().BeTrue(server.StdErr);
        (control.StdOut + control.StdErr).Should().NotContain("[INFO]");
        (server.StdOut + server.StdErr).Should().Contain("[INFO]");
    }

    // ---- repl --log-level / --log-file / --verbose / --enable-feature ----

    const string ReplPrintOne = "print(1)\nexit()\n";

    [Fact]
    public void Repl_LogLevel_EnablesTheLogger()
    {
        var control = CliTestHarness.InvokeWithInput("repl", ReplPrintOne);
        var invocation = CliTestHarness.InvokeWithInput("repl --log-level Info", ReplPrintOne);

        control.ExitCode.Should().Be(0, Show(control));
        control.StdOut.Should().Contain(">>> 1", "positive control: the snippet ran");
        Show(control).Should().NotContain("[INFO]", "control: the REPL logs nothing by default");
        invocation.ExitCode.Should().Be(0, Show(invocation));
        invocation.StdOut.Should().Contain("[INFO]", "--log-level Info enables the compiler logger");
    }

    [Fact]
    public void Repl_LogFile_ReceivesTheLog()
    {
        using var ws = new TempWorkspace();
        var log = ws.PathFor("repl.log");

        var invocation = CliTestHarness.InvokeWithInput($"repl --log-level Info --log-file \"{log}\"", ReplPrintOne);

        invocation.ExitCode.Should().Be(0, Show(invocation));
        File.Exists(log).Should().BeTrue("--log-file names where the log goes");
        ReadShared(log).Should().Contain("[INFO]", "the Info log lands in the file");
        Show(invocation).Should().NotContain("[INFO]", "the log goes to the file instead of the console");
    }

    [Fact]
    public void Repl_Verbose_RaisesTheLogLevel()
    {
        var control = CliTestHarness.InvokeWithInput("repl", ReplPrintOne);
        var invocation = CliTestHarness.InvokeWithInput("repl --verbose", ReplPrintOne);

        control.ExitCode.Should().Be(0, Show(control));
        Show(control).Should().NotContain("[INFO]", "control: the REPL logs nothing by default");
        invocation.ExitCode.Should().Be(0, Show(invocation));
        invocation.StdOut.Should().Contain("[INFO]", "--verbose raises the log level to Info");
    }

    [Fact]
    public void Repl_EnableFeature_AcceptsGatedSyntax()
    {
        // A block ends at the first empty line; the int return keeps the call printable (a bare call
        // of a void function at the prompt is a separate defect, see the P46 findings ledger).
        const string input =
            "def f() -> int:\n    defer print(\"deferred\")\n    print(\"body\")\n    return 3\n\nprint(f())\nexit()\n";

        var control = CliTestHarness.InvokeWithInput("repl", input);
        var invocation = CliTestHarness.InvokeWithInput("repl --enable-feature defer", input);

        control.ExitCode.Should().Be(0, Show(control));
        control.StdErr.Should().Contain("SPY0331", "control: defer is gated without the flag");
        control.StdOut.Should().NotContain("deferred");
        invocation.ExitCode.Should().Be(0, Show(invocation));
        invocation.StdErr.Should().NotContain("SPY0331");
        invocation.StdOut.Should().Contain("body\ndeferred\n3\n", "with the feature the deferred print runs at scope exit");
    }

    // ── a spawned `sharpyc server` ───────────────────────────────────────────

    /// <summary>
    /// A <c>sharpyc server</c> child process on a fresh pipe with its output captured, spawned the
    /// way <c>CompileServerE2ETests</c> spawns it; killed on dispose if it is still running.
    /// </summary>
    sealed class SpawnedServer : IDisposable
    {
        readonly StringBuilder _stdout = new();
        readonly StringBuilder _stderr = new();
        readonly object _gate = new();

        public Process Process { get; }

        public string PipeName { get; }

        SpawnedServer(Process process, string pipeName)
        {
            Process = process;
            PipeName = pipeName;
        }

        public string StdOut
        {
            get { lock (_gate) return _stdout.ToString(); }
        }

        public string StdErr
        {
            get { lock (_gate) return _stderr.ToString(); }
        }

        public static SpawnedServer Start(params string[] options)
        {
            // Short name: on macOS the pipe is a Unix domain socket whose path is capped at 104 chars.
            var pipeName = "spy-ev-" + Guid.NewGuid().ToString("N")[..8];
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
            foreach (var option in options)
                psi.ArgumentList.Add(option);

            var process = new Process { StartInfo = psi };
            var server = new SpawnedServer(process, pipeName);
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    lock (server._gate) server._stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    lock (server._gate) server._stderr.AppendLine(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return server;
        }

        /// <summary>Waits for the process to exit by itself, then drains the async readers.</summary>
        public bool ExitsWithin(int milliseconds)
        {
            if (!Process.WaitForExit(milliseconds))
                return false;
            Process.WaitForExit(); // the untimed overload waits for the redirected streams to reach EOF
            return true;
        }

        /// <summary>Polls until stderr contains <paramref name="text"/>; false on timeout or exit without it.</summary>
        public bool WaitForStderr(string text, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (StdErr.Contains(text, StringComparison.Ordinal))
                    return true;
                if (Process.HasExited)
                {
                    Process.WaitForExit(); // flush the async readers
                    return StdErr.Contains(text, StringComparison.Ordinal);
                }

                Thread.Sleep(100);
            }

            return false;
        }

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit(5_000);
                }
                else
                {
                    Process.WaitForExit(); // flush the async readers
                }
            }
            catch
            {
                // Best-effort teardown.
            }

            Process.Dispose();
        }
    }
}
