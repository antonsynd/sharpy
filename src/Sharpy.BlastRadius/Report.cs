using System.Text;
using System.Text.Json;
using Sharpy.BlastRadius.Probes;

namespace Sharpy.BlastRadius;

/// <summary>The measured result of one probe over one corpus.</summary>
internal sealed class BlastReport
{
    public required IRuleProbe Probe { get; init; }
    public required string Commit { get; init; }
    public required IReadOnlyList<string> AreaOrder { get; init; }
    public required IReadOnlyList<ItemOutcome> Outcomes { get; init; }

    public IEnumerable<(ItemOutcome Outcome, ProbeSite Site)> Sites
        => Outcomes.SelectMany(o => o.Sites.Select(s => (o, s)));

    public int Count(string? area, string verdict)
        => Sites.Count(x => (area == null || x.Outcome.Item.Area == area) && x.Site.Verdict == verdict);

    // ------------------------------------------------------------------ JSON

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("rule", Probe.Rule);
            json.WriteString("title", Probe.Title);
            json.WriteString("commit", Commit);

            json.WriteStartArray("areas");
            foreach (var area in AreaOrder)
            {
                var items = Outcomes.Where(o => o.Item.Area == area).ToList();
                json.WriteStartObject();
                json.WriteString("name", area);
                json.WriteNumber("programs", items.Count);
                json.WriteNumber("analysed", items.Count(o => o.Skipped == null));
                json.WriteStartObject("skipped");
                foreach (var group in items.Where(o => o.Skipped != null).GroupBy(o => o.Skipped!)
                             .OrderBy(g => g.Key, StringComparer.Ordinal))
                    json.WriteNumber(group.Key, group.Count());
                json.WriteEndObject();
                json.WriteStartObject("verdicts");
                foreach (var verdict in Probe.Verdicts)
                    json.WriteNumber(verdict.Name, Count(area, verdict.Name));
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("sites");
            foreach (var (outcome, site) in Sites)
            {
                json.WriteStartObject();
                json.WriteString("area", outcome.Item.Area);
                json.WriteString("program", outcome.Item.Label);
                json.WriteNumber("line", site.Line);
                json.WriteString("subject", site.Subject);
                json.WriteString("verdict", site.Verdict);
                json.WriteStartObject("coords");
                foreach (var (axis, value) in site.Coords)
                    json.WriteString(axis, value);
                json.WriteEndObject();
                json.WriteString("detail", site.Detail);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    // ------------------------------------------------------------------ Markdown

    public string ToMarkdown(int examples)
    {
        var md = new StringBuilder();
        // One writer for every line: '\n' on every platform, and no culture-sensitive formatting.
        void Line(string text = "") => md.Append(text).Append('\n');
        var verdicts = Probe.Verdicts.Select(v => v.Name).ToList();
        var total = Sites.Count();

        Line($"# Blast radius of {Probe.Rule}");
        Line();
        Line($"**Rule:** {Probe.Title}.");
        Line();
        Line($"Measured at `{Commit}` over {Outcomes.Count} programs "
            + $"({Outcomes.Count(o => o.Skipped == null)} analysed). One site is one {Probe.SiteNoun}; "
            + $"{total} sites were found.");
        Line();

        Line("## Sites by verdict");
        Line();
        Line("| area | programs | analysed | sites | " + string.Join(" | ", verdicts) + " |");
        Line("|---|---|---|---|" + string.Concat(verdicts.Select(_ => "---|")));
        foreach (var area in AreaOrder)
        {
            var items = Outcomes.Where(o => o.Item.Area == area).ToList();
            Line($"| {area} | {items.Count} | {items.Count(o => o.Skipped == null)} | "
                + $"{items.Sum(o => o.Sites.Count)} | "
                + string.Join(" | ", verdicts.Select(v => Count(area, v))) + " |");
        }
        Line($"| **all** | {Outcomes.Count} | {Outcomes.Count(o => o.Skipped == null)} | {total} | "
            + string.Join(" | ", verdicts.Select(v => Count(null, v))) + " |");
        Line();
        var files = Sites.GroupBy(x => x.Outcome.Item.Label).ToList();
        Line("Programs holding at least one site, by the worst verdict among their sites: "
            + string.Join(", ", verdicts.Select(v =>
                $"{files.Count(f => verdicts.First(w => f.Any(x => x.Site.Verdict == w)) == v)} {v}"))
            + $" ({files.Count} programs).");
        Line();

        Line("## What each verdict means");
        Line();
        foreach (var verdict in Probe.Verdicts)
            Line($"- **{verdict.Name}**: {verdict.Meaning}");
        Line();

        Line("## Sites by coordinates");
        Line();
        Line("| verdict | coordinates | sites | programs |");
        Line("|---|---|---|---|");
        foreach (var verdict in verdicts)
        {
            var groups = Sites.Where(x => x.Site.Verdict == verdict)
                .GroupBy(x => CoordText(x.Site))
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                Line($"| {verdict} | {group.Key} | {group.Count()} | "
                    + $"{group.Select(x => x.Outcome.Item.Label).Distinct().Count()} |");
            }
        }
        Line();
        Line("The axes:");
        Line();
        foreach (var axis in Probe.Axes)
        {
            Line($"- **{axis.Name}**: {axis.Meaning}");
            foreach (var (value, meaning) in axis.Values.Where(v => v.Meaning.Length > 0))
                Line($"  - `{value}`: {meaning}");
        }
        Line();

        if (examples > 0)
        {
            Line("## Examples");
            Line();
            foreach (var verdict in verdicts)
            {
                var groups = Sites.Where(x => x.Site.Verdict == verdict)
                    .GroupBy(x => CoordText(x.Site))
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
                foreach (var group in groups)
                {
                    Line($"**{verdict}** ({group.Key}), {Math.Min(examples, group.Count())} of {group.Count()}:");
                    Line();
                    foreach (var (outcome, site) in group.Take(examples))
                    {
                        var where = outcome.Item.Path != null
                            ? $"{outcome.Item.Label}:{site.Line}"
                            : $"{outcome.Item.Label} (line {site.Line})";
                        Line($"- `{where}` {site.Subject}"
                            + (site.Detail.Length > 0 ? $": {site.Detail}" : ""));
                    }
                    Line();
                }
            }
        }

        Line("## Programs not analysed");
        Line();
        Line("A probe reads only programs that compile today. The rest are counted here and say nothing about the rule.");
        Line();
        Line("| area | reason | programs |");
        Line("|---|---|---|");
        foreach (var area in AreaOrder)
        {
            var groups = Outcomes.Where(o => o.Item.Area == area && o.Skipped != null)
                .GroupBy(o => SkipFamily(o.Skipped!))
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in groups)
                Line($"| {area} | {group.Key} | {group.Count()} |");
        }
        Line();
        var codes = Outcomes.Where(o => o.Skipped != null && o.Skipped.StartsWith("does not compile", StringComparison.Ordinal))
            .GroupBy(o => o.Skipped!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(8).Select(g => $"{g.Key.Replace("does not compile today ", "", StringComparison.Ordinal).Trim('(', ')')} × {g.Count()}").ToList();
        if (codes.Count > 0)
        {
            Line("First error of the programs that do not compile, most common first: " + string.Join(", ", codes) + ".");
            Line();
        }

        Line("## What this probe cannot see");
        Line();
        foreach (var limit in Probe.Limits)
            Line($"- {limit}");
        Line("- Only the programs in this repository are read. They lean towards what the tests exercise.");
        return md.ToString();
    }

    private static string CoordText(ProbeSite site)
        => site.Coords.Count == 0 ? "" : string.Join(", ", site.Coords.Select(kv => $"{kv.Key}: {kv.Value}"));

    private static string SkipFamily(string reason)
        => reason.StartsWith("does not compile today", StringComparison.Ordinal) ? "does not compile today" : reason;
}
