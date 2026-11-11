using Sharpy.TestInfrastructure.Integration;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The sweeps' per-stem set files (<c>formatting-route-census.txt</c>, <c>code-action-shape-stems.txt</c>) are generated:
/// a run with <see cref="Variable"/><c>=1</c> (what <see cref="Command"/> sets) rewrites each twin's rows from what
/// the twin measured, instead of comparing. A file is its leading comment block, then one row per line in twin blocks
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

    /// <summary>
    /// Replaces <paramref name="twin"/>'s rows of <paramref name="lines"/> with <paramref name="rows"/>, keeping the leading
    /// comment block and every other twin's rows, blocks in <paramref name="twinOrder"/>.
    /// </summary>
    internal static SCG.List<string> ReplaceTwinRows(SCG.IReadOnlyList<string> lines, string twin, SCG.IReadOnlyList<string> twinOrder,
        SCG.IEnumerable<string> rows)
    {
        var header = lines.TakeWhile(l => l.Length == 0 || l.StartsWith('#')).ToList();
        var byTwin = new SCG.Dictionary<string, SCG.List<string>>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(header.Count))
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
        var result = new SCG.List<string>(header);
        foreach (var t in twinOrder)
        {
            if (byTwin.TryGetValue(t, out var block))
                result.AddRange(block);
        }

        return result;
    }

    /// <summary>Rewrites <paramref name="fileName"/> with <see cref="ReplaceTwinRows"/>, <c>\n</c> line ends.</summary>
    internal static void Rewrite(string fileName, string twin, SCG.IReadOnlyList<string> twinOrder, SCG.IEnumerable<string> rows)
    {
        var path = PathOf(fileName);
        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        var kept = ReplaceTwinRows(lines, twin, twinOrder, rows);
        File.WriteAllText(path, string.Concat(kept.Select(l => l + "\n")));
    }
}
