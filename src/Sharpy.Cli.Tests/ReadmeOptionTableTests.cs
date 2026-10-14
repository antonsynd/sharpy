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
/// is prose the registry does not hold, so it is not checked here (ReferenceDeploymentMatrixTests
/// observes it).
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

    /// <summary>The table's rows: command path → (input, -r/-m, globals) cells.</summary>
    internal static Dictionary<string, (string Input, string PathOptions, string Globals)> ParseTable(string readme)
    {
        var rows = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal);
        var section = readme.IndexOf("### Options by Command", StringComparison.Ordinal);
        if (section < 0)
            return rows;

        foreach (var line in readme[section..].Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("#", StringComparison.Ordinal)))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
                continue;
            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 5)
                continue;
            var command = Backticked.Match(cells[0]).Groups[1].Value;
            rows[command] = (cells[1], cells[2], cells[4]);
        }

        return rows;
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
}
