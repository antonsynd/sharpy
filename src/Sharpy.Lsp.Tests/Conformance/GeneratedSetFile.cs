using Sharpy.TestInfrastructure.Integration;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The sweeps' per-stem set files (<c>formatting-route-census.txt</c>, <c>code-action-shape-stems.txt</c>) are generated:
/// a run with <see cref="Variable"/><c>=1</c> (what <see cref="Command"/> sets) rewrites each twin's rows from what
/// the twin measured, instead of comparing. A file is its hand-written comment block, then the generated totals lines
/// (<see cref="TotalsPrefix"/>: a reviewer sees each twin's volume at a glance), then one row per line in twin blocks
/// (the sweep's twin order), each block sorted by the sweep, so two changes that add different fixtures add rows at
/// different lines. The twin is a row's second field.
/// </summary>
internal static class GeneratedSetFile
{
    /// <summary>
    /// The regeneration switch. Only <see cref="Command"/> sets it, so a shell that still exports another regeneration flag
    /// (the snapshot tests' <c>UPDATE_SNAPSHOTS</c>) never rewrites these files.
    /// </summary>
    internal const string Variable = "SHARPY_REGENERATE_CORPUS_PINS";

    /// <summary>The one command that regenerates the set files (it runs both sweeps with <see cref="Variable"/> set).</summary>
    internal const string Command = "bash build_tools/regenerate_corpus_pins.sh";

    /// <summary>What a red comparison tells the contributor to do.</summary>
    internal const string Remedy = "merge the base branch (a fixture that landed there has no rows here yet), then run `" + Command
        + "` and review the row diff";

    internal static bool Regenerating => IsRegenerating(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Whether <see cref="Variable"/> is <c>1</c>. On CI (<c>CI=true</c> or <c>GITHUB_ACTIONS</c> set) it throws instead:
    /// a test there must compare against its committed rows, never rewrite them.
    /// </summary>
    internal static bool IsRegenerating(Func<string, string?> environment)
    {
        if (environment(Variable) != "1")
            return false;
        if (string.Equals(environment("CI"), "true", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(environment("GITHUB_ACTIONS")))
            throw new InvalidOperationException($"{Variable}=1 on CI: a test may not rewrite its own expected rows there; run `{Command}` locally.");
        return true;
    }

    internal static string PathOf(string fileName)
        => Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Lsp.Tests", "Conformance", fileName);

    /// <summary>The prefix of a generated totals line: <c># total &lt;measure&gt; &lt;twin&gt; …</c>, the twin its fourth word.</summary>
    internal const string TotalsPrefix = "# total ";

    private static string TwinOfTotals(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 3 ? words[3] : "";
    }

    /// <summary>The generated totals lines of <paramref name="twin"/> in a set file's leading block.</summary>
    internal static SCG.List<string> TotalsOf(SCG.IEnumerable<string> lines, string twin)
        => lines.TakeWhile(l => l.Length == 0 || l.StartsWith('#'))
            .Where(l => l.StartsWith(TotalsPrefix, StringComparison.Ordinal) && TwinOfTotals(l) == twin).ToList();

    /// <summary>
    /// Replaces <paramref name="twin"/>'s rows of <paramref name="lines"/> with <paramref name="rows"/> and its totals lines with
    /// <paramref name="totals"/>, keeping the hand-written comment block, then every twin's totals, then every twin's rows, the
    /// blocks in <paramref name="twinOrder"/>.
    /// </summary>
    internal static SCG.List<string> ReplaceTwinRows(SCG.IReadOnlyList<string> lines, string twin, SCG.IReadOnlyList<string> twinOrder,
        SCG.IEnumerable<string> rows, SCG.IEnumerable<string> totals)
    {
        var leading = lines.TakeWhile(l => l.Length == 0 || l.StartsWith('#')).ToList();
        var header = leading.Where(l => !l.StartsWith(TotalsPrefix, StringComparison.Ordinal)).ToList();
        var totalsByTwin = new SCG.Dictionary<string, SCG.List<string>>(StringComparer.Ordinal);
        foreach (var line in leading.Where(l => l.StartsWith(TotalsPrefix, StringComparison.Ordinal)))
        {
            var t = TwinOfTotals(line);
            if (!twinOrder.Contains(t))
                throw new InvalidOperationException($"totals line '{line}' names no twin of {string.Join(", ", twinOrder)}");
            if (!totalsByTwin.TryGetValue(t, out var block))
                totalsByTwin[t] = block = new SCG.List<string>();
            block.Add(line);
        }

        var byTwin = new SCG.Dictionary<string, SCG.List<string>>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(leading.Count))
        {
            if (line.Length == 0)
                continue;
            if (line.StartsWith('#'))
                throw new InvalidOperationException($"a generated set file keeps its comments in the leading block: '{line}'");
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rowTwin = fields.Length > 1 ? fields[1] : "";
            if (!twinOrder.Contains(rowTwin))
                throw new InvalidOperationException($"row '{line}' names no twin of {string.Join(", ", twinOrder)}");
            if (!byTwin.TryGetValue(rowTwin, out var block))
                byTwin[rowTwin] = block = new SCG.List<string>();
            block.Add(line);
        }

        byTwin[twin] = rows.ToList();
        totalsByTwin[twin] = totals.ToList();
        var result = new SCG.List<string>(header);
        foreach (var t in twinOrder)
        {
            if (totalsByTwin.TryGetValue(t, out var block))
                result.AddRange(block);
        }

        foreach (var t in twinOrder)
        {
            if (byTwin.TryGetValue(t, out var block))
                result.AddRange(block);
        }

        return result;
    }

    /// <summary>
    /// Rewrites <paramref name="fileName"/> with <see cref="ReplaceTwinRows"/>, <c>\n</c> line ends: written to a temporary file
    /// beside it and moved into place, so an interrupted run never leaves a half-written file.
    /// </summary>
    internal static void Rewrite(string fileName, string twin, SCG.IReadOnlyList<string> twinOrder, SCG.IEnumerable<string> rows,
        SCG.IEnumerable<string> totals)
    {
        var path = PathOf(fileName);
        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        var kept = ReplaceTwinRows(lines, twin, twinOrder, rows, totals);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, string.Concat(kept.Select(l => l + "\n")));
        File.Move(temporary, path, overwrite: true);
    }
}
