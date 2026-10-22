using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Cli;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// The README's "Options by Command" table is hand-written from <see cref="CommandOptionEffects"/>;
/// this keeps it from fossilising (#2223). Both ways: every command path of the registry has a row
/// and every row names a registry command, and each row's input kinds, <c>-r</c>/<c>-m</c> column and
/// "global options without effect" column say what the command's table says. The deployment column
/// is prose the registry does not hold: it is pinned against the literal set of deploying commands
/// (<see cref="DeployingCommands"/>, the callers of RuntimeDependencyHelper.CopyRuntimeDependencies),
/// as are the `build`/`project` help sentences that say they deploy unless `--no-deps`
/// (ReferenceDeploymentMatrixTests observes the behaviour itself).
/// </summary>
public class ReadmeOptionTableTests
{
    static string ReadmeText()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Cli", "README.md");
            if (File.Exists(path))
                return File.ReadAllText(path);
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("src/Sharpy.Cli/README.md not found above the test binary");
    }

    static readonly Regex Backticked = new(@"`([^`]+)`", RegexOptions.Compiled);

    /// <summary>The table's data rows: command path → its cells after the command column.</summary>
    static IEnumerable<(string Command, string[] Cells)> TableRows(string readme)
    {
        var section = readme.IndexOf("### Options by Command", StringComparison.Ordinal);
        if (section < 0)
            yield break;

        foreach (var line in readme[section..].Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("#", StringComparison.Ordinal)))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
                continue;
            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 5)
                continue;
            yield return (Backticked.Match(cells[0]).Groups[1].Value, cells);
        }
    }

    /// <summary>The table's rows: command path → (input, -r/-m, globals) cells.</summary>
    internal static Dictionary<string, (string Input, string PathOptions, string Globals)> ParseTable(string readme)
    {
        var rows = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal);
        foreach (var (command, cells) in TableRows(readme))
            rows[command] = (cells[1], cells[2], cells[4]);
        return rows;
    }

    /// <summary>The table's "Deploys references" cell per command path.</summary>
    internal static Dictionary<string, string> ParseDeployColumn(string readme)
    {
        var deploys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (command, cells) in TableRows(readme))
            deploys[command] = cells[3];
        return deploys;
    }

    /// <summary>
    /// The commands whose action copies the runtime dependencies and user references beside the
    /// output: the callers of RuntimeDependencyHelper.CopyRuntimeDependencies (#2223) — every
    /// producing command since `build` and `project` joined (#2313 cell 0, R-HF c). A literal, not
    /// derived from the registry, so the README and help text are pinned against something other
    /// than themselves.
    /// </summary>
    internal static readonly IReadOnlySet<string> DeployingCommands = new HashSet<string>(StringComparer.Ordinal) { "build", "compile", "project", "run" };

    /// <summary>The deploying commands that opt out with <c>--no-deps</c>; `run` deploys beside the program it runs.</summary>
    internal static readonly IReadOnlySet<string> NoDepsCommands = new HashSet<string>(StringComparer.Ordinal) { "build", "compile", "project" };

    /// <summary>The help sentence `build` and `project` carried while they wrote the assembly only (P46, 10d0b3d9a).</summary>
    const string AssemblyOnlySentence = "Writes the assembly only; `compile` deploys its runtime dependencies and references beside the output";

    /// <summary>The help sentence of `build` and `project` now that they deploy.</summary>
    const string DeploysSentence = "Copies its runtime dependencies and references beside the output, so it runs from there (--no-deps writes the assembly only)";

    /// <summary>Every way the README's "Deploys references" column disagrees with <see cref="DeployingCommands"/>.</summary>
    internal static System.Collections.Generic.List<string> DeployMismatches(string readme)
    {
        var deploys = ParseDeployColumn(readme);
        var problems = new System.Collections.Generic.List<string>();
        foreach (var (path, cell) in deploys)
        {
            var expected = DeployingCommands.Contains(path) ? "yes" : "—";
            if (!cell.StartsWith(expected, StringComparison.Ordinal))
                problems.Add($"`{path}`: the Deploys references cell says '{cell}', expected it to start with '{expected}'");
            if (NoDepsCommands.Contains(path) && !cell.Contains("unless `--no-deps`", StringComparison.Ordinal))
                problems.Add($"`{path}`: the Deploys references cell does not name the --no-deps opt-out: '{cell}'");
        }

        return problems;
    }

    static IReadOnlySet<string> GlobalsIn(string cell) => cell switch
    {
        "none" => new HashSet<string>(),
        "all nine" => CommandOptionEffects.GlobalOptionNames.ToHashSet(),
        _ => Backticked.Matches(cell).Select(m => m.Groups[1].Value).ToHashSet(),
    };

    static string KindName(InputKind kind) => kind == InputKind.SpyFile ? ".spy" : ".spyproj";

    /// <summary>Every way the README table disagrees with <paramref name="tables"/>.</summary>
    internal static System.Collections.Generic.List<string> Mismatches(string readme, IReadOnlyDictionary<string, CommandEffectTable> tables)
    {
        var rows = ParseTable(readme);
        var problems = new System.Collections.Generic.List<string>();
        foreach (var path in tables.Keys.Where(p => !rows.ContainsKey(p)))
            problems.Add($"`{path}` has no row in the README's Options by Command table");
        foreach (var path in rows.Keys.Where(p => !tables.ContainsKey(p)))
            problems.Add($"the README row `{path}` names no command of the registry");

        foreach (var (path, (input, pathOptions, globals)) in rows.Where(r => tables.ContainsKey(r.Key)))
        {
            var table = tables[path];
            var kinds = Backticked.Matches(input).Select(m => m.Groups[1].Value).Where(v => v.StartsWith('.')).ToHashSet();
            if (!kinds.SetEquals(table.Columns.Select(KindName)))
                problems.Add($"`{path}`: Input says {{{string.Join(", ", kinds)}}}, the registry {{{string.Join(", ", table.Columns.Select(KindName))}}}");

            var takesPaths = table.Rows.ContainsKey("--reference") && table.Rows.ContainsKey("--module-path");
            if (pathOptions.StartsWith("yes", StringComparison.Ordinal) != takesPaths)
                problems.Add($"`{path}`: the -r/-m column says '{pathOptions}', the registry {(takesPaths ? "has" : "lacks")} them");

            var ignored = CommandOptionEffects.GlobalOptionNames
                .Where(g => table.Rows.TryGetValue(g, out var row) && row.IsUniform && row.SpyFile == OptionEffect.Ignored)
                .ToHashSet();
            if (!GlobalsIn(globals).SetEquals(ignored))
                problems.Add($"`{path}`: the README lists [{string.Join(", ", GlobalsIn(globals))}] without effect, the registry [{string.Join(", ", ignored)}]");
        }

        return problems;
    }

    [Fact]
    public void TheReadmeTable_MatchesTheRegistry_BothWays()
    {
        var readme = ReadmeText();

        ParseTable(readme).Should().HaveCount(CommandOptionEffects.Tables.Count, "positive control: the parser reads every row");
        Mismatches(readme, CommandOptionEffects.Tables).Should().BeEmpty();
    }

    [Fact]
    public void AFakeCommandRow_AndAMissingRow_AreReported()
    {
        var readme = ReadmeText();
        var withFake = readme.Replace("| `build` |", "| `nope` |", StringComparison.Ordinal);

        Mismatches(withFake, CommandOptionEffects.Tables).Should()
            .Contain("`build` has no row in the README's Options by Command table")
            .And.Contain("the README row `nope` names no command of the registry");
    }

    [Fact]
    public void AWrongGlobalsCell_IsReported()
    {
        var readme = ReadmeText();
        var line = readme.Split('\n').Single(l => l.StartsWith("| `emit diagnostics` |", StringComparison.Ordinal));
        var wrong = readme.Replace(line, line.Replace("`--metrics-format`, `--metrics-output`", "none", StringComparison.Ordinal), StringComparison.Ordinal);

        Mismatches(wrong, CommandOptionEffects.Tables).Should().ContainSingle()
            .Which.Should().StartWith("`emit diagnostics`: the README lists");
    }

    [Fact]
    public void TheDeploysColumn_MatchesTheDeployingCommands()
    {
        var readme = ReadmeText();

        ParseDeployColumn(readme).Should().HaveCount(CommandOptionEffects.Tables.Count, "positive control: the parser reads every row's deploy cell");
        DeployMismatches(readme).Should().BeEmpty();
    }

    [Fact]
    public void AWrongDeploysCell_IsReported()
    {
        var readme = ReadmeText();
        var line = readme.Split('\n').Single(l => l.StartsWith("| `project` |", StringComparison.Ordinal));
        var wrong = readme.Replace(line, line.Replace("| yes, unless `--no-deps` |", "| no — writes the assembly only |", StringComparison.Ordinal), StringComparison.Ordinal);

        DeployMismatches(wrong).Should().HaveCount(2)
            .And.AllSatisfy(p => p.Should().StartWith("`project`:"));
    }

    /// <summary>
    /// `build --help` and `project --help` say they deploy and name the `--no-deps` opt-out. No
    /// deploying command's help carries the assembly-only sentence or the "not deployed" `-r` reason
    /// `build` and `project` carried before #2313 cell 0; each one that takes `--no-deps` lists it
    /// (the positive control for those absences).
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("project")]
    public void ABuildCommand_SaysItDeploysInItsHelp(string command)
    {
        var help = CliTestHarness.Invoke($"{command} --help");

        help.ExitCode.Should().Be(0);
        help.StdOut.Should().Contain(DeploysSentence);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("compile")]
    [InlineData("project")]
    [InlineData("run")]
    public void ADeployingCommand_HasNoAssemblyOnlySentence(string command)
    {
        var help = CliTestHarness.Invoke($"{command} --help");

        help.ExitCode.Should().Be(0);
        help.StdOut.Should().NotContain(AssemblyOnlySentence);
        help.StdOut.Should().NotContain("not deployed beside the output");
        if (NoDepsCommands.Contains(command))
            help.StdOut.Should().Contain("--no-deps", $"`{command}` takes the opt-out");
    }
}
