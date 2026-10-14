using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sharpy.Cli;

namespace Sharpy.Cli.Tests;

/// <summary>
/// The shared evidence for an honoured GLOBAL option: a probe the option-effect sweep runs on the
/// command itself (#2223). Each probe pairs presence with absence — the option changes something
/// observable against the same invocation without it — so an honoured global that a command
/// parses and drops is red, not "documented". Probes run in process through
/// <see cref="CliTestHarness.Invoke"/>, from one workspace per command and input kind.
/// </summary>
internal static class GlobalOptionProbes
{
    /// <summary>The programs a probe feeds a command.</summary>
    internal enum Fixture
    {
        /// <summary>A clean program that prints <c>plain</c>.</summary>
        Plain,

        /// <summary>A program with one SPY0451 warning (an unused local) and nothing else.</summary>
        Warn,

        /// <summary>A program with at least two errors in the phase the command reaches.</summary>
        Errors,

        /// <summary>A program using the <c>defer</c> statement (SPY0331 unless the feature is enabled).</summary>
        Defer,
    }

    const string PlainSource = "def main():\n    print(\"plain\")\n";
    const string WarnSource = "def main():\n    unused = 1\n    print(\"warn\")\n";
    const string SemanticErrorsSource = "def main():\n    print(aaa)\n    print(bbb)\n    print(ccc)\n";
    const string ParseErrorsSource = "def main():\n    x = (1\n\ndef f(:\n    pass\n\ndef g(:\n    pass\n";
    const string LexErrorsSource = "x = 1 ` \ny = 2 ` \nz = 3 ` \n";
    const string DeferSource = "def main():\n    defer print(\"b\")\n    print(\"a\")\n";

    /// <summary>
    /// How each command is invoked on a probe input: <c>{input}</c> is the <c>.spy</c> file or the
    /// <c>.spyproj</c>, <c>{out}</c> an output directory that does not exist beforehand. A command with
    /// no entry here (lsp, repl, server: they block or read stdin) has no probes; its honoured globals
    /// need a named evidence test instead.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Invocations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["build"] = "build \"{input}\" -o \"{out}/a.dll\"",
        ["compile"] = "compile \"{input}\" -o \"{out}/a.dll\" --no-deps",
        ["run"] = "run \"{input}\"",
        ["project"] = "project \"{input}\"",
        ["emit tokens"] = "emit tokens \"{input}\"",
        ["emit ast"] = "emit ast \"{input}\"",
        ["emit parse"] = "emit parse \"{input}\"",
        ["emit csharp"] = "emit csharp \"{input}\" -o \"{out}/main.cs\"",
        ["emit diagnostics"] = "emit diagnostics \"{input}\"",
        ["emit hover"] = "emit hover \"{input}\" --line 1 --col 1",
        ["cache clear"] = "cache clear --cache-dir \"{out}/cache\"",
        ["cache info"] = "cache info --cache-dir \"{out}/cache\"",
        ["explain"] = "explain SPY0300",
        ["format"] = "format --check \"{input}\"",
    };

    /// <summary>The globals a probe exists for; <c>--help</c> is probed on every command.</summary>
    internal static readonly IReadOnlyList<string> ProbedOptions = new[]
    {
        "--log-level", "--log-file", "--verbose", "--metrics-format", "--metrics-output",
        "--warn-as-error", "--nowarn", "--max-errors", "--enable-feature", "--help",
    };

    static readonly Regex ErrorLine = new(@"\berror[\[ ]SPY\d{4}", RegexOptions.Compiled);

    /// <summary>One probe workspace: the fixture written as a <c>.spy</c> or inside an exe project.</summary>
    internal sealed class ProbeInput : IDisposable
    {
        readonly TempWorkspace _ws = new();

        public string Input { get; }

        public string Out => _ws.PathFor("out");

        public string Root => _ws.Root;

        public ProbeInput(string source, InputKind kind)
        {
            if (kind == InputKind.SpyProject)
            {
                _ws.WriteFile(Path.Combine("src", "main.spy"), source);
                Input = _ws.WriteFile("App.spyproj",
                    "<Project>\n  <PropertyGroup>\n    <RootNamespace>App</RootNamespace>\n    <OutputType>exe</OutputType>\n"
                    + "  </PropertyGroup>\n  <ItemGroup>\n    <SpyFile Include=\"src/**/*.spy\" />\n  </ItemGroup>\n</Project>\n");
            }
            else
            {
                Input = _ws.WriteSpy(source);
            }
        }

        public string PathFor(string relative) => _ws.PathFor(relative);

        public void Dispose() => _ws.Dispose();
    }

    static string SourceFor(Fixture fixture, CommandPerforms performs) => fixture switch
    {
        Fixture.Plain => PlainSource,
        Fixture.Warn => WarnSource,
        Fixture.Defer => DeferSource,
        Fixture.Errors => performs switch
        {
            CommandPerforms.Lexes => LexErrorsSource,
            CommandPerforms.Parses => ParseErrorsSource,
            _ => SemanticErrorsSource,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(fixture)),
    };

    /// <summary>
    /// Runs <paramref name="commandPath"/>'s invocation on a fresh workspace holding
    /// <paramref name="fixture"/>, with <paramref name="extra"/> appended (each <c>{out}</c> /
    /// <c>{root}</c> in it substituted). Returns the invocation and the workspace (caller disposes).
    /// </summary>
    internal static (CliInvocation Invocation, ProbeInput Input) Invoke(
        string commandPath, InputKind kind, CommandPerforms performs, Fixture fixture, string extra,
        Func<string, CliInvocation>? invoke = null)
    {
        var input = new ProbeInput(SourceFor(fixture, performs), kind);
        var line = Invocations[commandPath].Replace("{input}", input.Input).Replace("{out}", input.Out);
        var extraLine = extra.Replace("{out}", input.Out).Replace("{root}", input.Root);
        var invocation = (invoke ?? CliTestHarness.Invoke)(string.IsNullOrEmpty(extraLine) ? line : $"{line} {extraLine}");
        return (invocation, input);
    }

    static readonly ConcurrentDictionary<(string, InputKind, Fixture), CliInvocation> Baselines = new();

    /// <summary>The command on <paramref name="fixture"/> with no extra option (cached per command, kind and fixture).</summary>
    static CliInvocation Baseline(string commandPath, InputKind kind, CommandPerforms performs, Fixture fixture) =>
        Baselines.GetOrAdd((commandPath, kind, fixture), _ =>
        {
            var (invocation, input) = Invoke(commandPath, kind, performs, fixture, string.Empty);
            input.Dispose();
            return invocation;
        });

    static string All(CliInvocation invocation) => invocation.StdOut + "\n" + invocation.StdErr;

    static string Show(CliInvocation invocation) =>
        $"exit {invocation.ExitCode}\n--- stdout ---\n{Tail(invocation.StdOut)}\n--- stderr ---\n{Tail(invocation.StdErr)}";

    static string Tail(string text) => text.Length <= 1500 ? text : "…" + text[^1500..];

    static string ReadShared(string path)
    {
        // A --log-file writer stays open (nothing disposes the logger), so read with sharing.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Runs the probe for <paramref name="option"/> on <paramref name="commandPath"/>. Returns null
    /// when the option is observed to take effect, or why it is not.
    /// </summary>
    internal static string? Run(string commandPath, InputKind kind, CommandPerforms performs, string option)
    {
        if (option == "--help")
        {
            var help = CliTestHarness.Invoke($"{commandPath} --help");
            return help.ExitCode == 0 && help.StdOut.Contains("Usage:", StringComparison.Ordinal)
                ? null
                : $"`{commandPath} --help` printed no usage:\n{Show(help)}";
        }

        if (!Invocations.ContainsKey(commandPath))
            return $"`{commandPath}` has no probe invocation; name an evidence test for {option}";

        switch (option)
        {
            case "--log-level":
            case "--verbose":
                {
                    var baseline = Baseline(commandPath, kind, performs, Fixture.Plain);
                    if (All(baseline).Contains("[INFO]", StringComparison.Ordinal))
                        return $"control: the baseline already logs [INFO]:\n{Show(baseline)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Plain, option == "--verbose" ? "--verbose" : "--log-level Info");
                    input.Dispose();
                    return All(probe).Contains("[INFO]", StringComparison.Ordinal)
                        ? null
                        : $"{option} produced no [INFO] log lines:\n{Show(probe)}";
                }

            case "--log-file":
                {
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Plain, "--log-level Info --log-file \"{root}/probe.log\"");
                    try
                    {
                        var log = input.PathFor("probe.log");
                        if (!File.Exists(log))
                            return $"--log-file wrote no file:\n{Show(probe)}";
                        if (!ReadShared(log).Contains("[INFO]", StringComparison.Ordinal))
                            return $"--log-file at --log-level Info holds no [INFO] line:\n{Show(probe)}";
                        return All(probe).Contains("[INFO]", StringComparison.Ordinal)
                            ? $"--log-file: the log still went to the console:\n{Show(probe)}"
                            : null;
                    }
                    finally
                    {
                        input.Dispose();
                    }
                }

            case "--metrics-format":
                {
                    var baseline = Baseline(commandPath, kind, performs, Fixture.Plain);
                    if (baseline.StdOut.Contains("\"total_duration_ms\"", StringComparison.Ordinal))
                        return $"control: the baseline already prints metrics:\n{Show(baseline)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Plain, "--metrics-format json");
                    input.Dispose();
                    return probe.StdOut.Contains("\"total_duration_ms\"", StringComparison.Ordinal)
                        ? null
                        : $"--metrics-format json printed no metrics object:\n{Show(probe)}";
                }

            case "--metrics-output":
                {
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Plain, "--metrics-output \"{root}/metrics.txt\"");
                    try
                    {
                        var file = input.PathFor("metrics.txt");
                        return File.Exists(file) && new FileInfo(file).Length > 0
                            ? null
                            : $"--metrics-output given alone wrote no metrics file:\n{Show(probe)}";
                    }
                    finally
                    {
                        input.Dispose();
                    }
                }

            case "--warn-as-error":
                {
                    var baseline = Baseline(commandPath, kind, performs, Fixture.Warn);
                    if (Regex.IsMatch(All(baseline), @"\berror[\[ ]SPY0451"))
                        return $"control: SPY0451 is already an error without the flag:\n{Show(baseline)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Warn, "--warn-as-error");
                    input.Dispose();
                    return Regex.IsMatch(All(probe), @"\berror[\[ ]SPY0451")
                        ? null
                        : $"--warn-as-error did not turn the SPY0451 warning into an error:\n{Show(probe)}";
                }

            case "--nowarn":
                {
                    var (control, controlInput) = Invoke(commandPath, kind, performs, Fixture.Warn, "--warn-as-error");
                    controlInput.Dispose();
                    if (!Regex.IsMatch(All(control), @"\berror[\[ ]SPY0451"))
                        return $"positive control: under --warn-as-error the SPY0451 warning is not reported, so --nowarn cannot be observed:\n{Show(control)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Warn, "--warn-as-error --nowarn SPY0451");
                    input.Dispose();
                    return All(probe).Contains("SPY0451", StringComparison.Ordinal)
                        ? $"--nowarn SPY0451 did not suppress the warning:\n{Show(probe)}"
                        : null;
                }

            case "--max-errors":
                {
                    var baseline = Baseline(commandPath, kind, performs, Fixture.Errors);
                    var before = ErrorLine.Matches(All(baseline)).Count;
                    if (before < 2)
                        return $"positive control: the errors fixture reports {before} error(s), fewer than two:\n{Show(baseline)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Errors, "--max-errors 1");
                    input.Dispose();
                    var after = ErrorLine.Matches(All(probe)).Count;
                    return after == 1
                        ? null
                        : $"--max-errors 1 reported {after} error(s) of {before}:\n{Show(probe)}";
                }

            case "--enable-feature":
                {
                    var baseline = Baseline(commandPath, kind, performs, Fixture.Defer);
                    if (!All(baseline).Contains("SPY0331", StringComparison.Ordinal))
                        return $"positive control: the defer program is not refused with SPY0331 without the flag:\n{Show(baseline)}";
                    var (probe, input) = Invoke(commandPath, kind, performs, Fixture.Defer, "--enable-feature defer");
                    input.Dispose();
                    return All(probe).Contains("SPY0331", StringComparison.Ordinal)
                        ? $"--enable-feature defer did not enable the feature:\n{Show(probe)}"
                        : null;
                }

            default:
                return $"no global probe for {option}";
        }
    }
}
