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

    private IReadOnlyList<(ItemOutcome Outcome, ProbeSite Site)>? _sites;

    public IReadOnlyList<(ItemOutcome Outcome, ProbeSite Site)> Sites
        => _sites ??= Outcomes.SelectMany(o => o.Sites.Select(s => (o, s))).ToList();

    /// <summary>Sites with this verdict, in one area or (null) in all of them.</summary>
    public int Count(string? area, string verdict)
        => Sites.Count(x => (area == null || x.Outcome.Item.Area == area) && x.Site.Verdict == verdict);

    private IEnumerable<ItemOutcome> In(string? area)
        => area == null ? Outcomes : Outcomes.Where(o => o.Item.Area == area);

    private int Analysed(string? area) => In(area).Count(o => o.Skipped == null);

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
                json.WriteStartObject();
                json.WriteString("name", area);
                json.WriteNumber("programs", In(area).Count());
                json.WriteNumber("analysed", Analysed(area));
                json.WriteStartObject("skipped");
                var reasons = In(area).Where(o => o.Skipped != null)
                    .GroupBy(o => o.ErrorCode == null ? o.Skipped! : $"{o.Skipped} ({o.ErrorCode})")
                    .OrderBy(g => g.Key, StringComparer.Ordinal);
                foreach (var group in reasons)
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
        // Sites of one verdict that share every coordinate, the largest group first.
        var groups = verdicts.SelectMany(verdict => Sites.Where(x => x.Site.Verdict == verdict)
            .GroupBy(x => string.Join(", ", x.Site.Coords.Select(kv => $"{kv.Key}: {kv.Value}")))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Verdict: verdict, Coords: g.Key, Sites: g.ToList()))).ToList();

        Line($"# Blast radius of {Probe.Rule}");
        Line();
        Line($"**Rule:** {Probe.Title}.");
        Line();
        Line($"Measured at `{Commit}` over {Outcomes.Count} programs "
            + $"({Analysed(null)} analysed). One site is one {Probe.SiteNoun}; "
            + $"{Sites.Count} sites were found.");
        Line();

        Line("## Sites by verdict");
        Line();
        Line("| area | programs | analysed | sites | " + string.Join(" | ", verdicts) + " |");
        Line("|---|---|---|---|" + string.Concat(verdicts.Select(_ => "---|")));
        foreach (var area in AreaOrder)
        {
            Line($"| {area} | {In(area).Count()} | {Analysed(area)} | {In(area).Sum(o => o.Sites.Count)} | "
                + string.Join(" | ", verdicts.Select(v => Count(area, v))) + " |");
        }
        Line($"| **all** | {Outcomes.Count} | {Analysed(null)} | {Sites.Count} | "
            + string.Join(" | ", verdicts.Select(v => Count(null, v))) + " |");
        Line();
        // A probe lists its verdicts worst first, so a program's worst is the first one it holds.
        var worst = Outcomes.Where(o => o.Sites.Count > 0)
            .Select(o => verdicts.First(v => o.Sites.Any(s => s.Verdict == v))).ToList();
        Line("Programs holding at least one site, by the worst verdict among their sites: "
            + string.Join(", ", verdicts.Select(v => $"{worst.Count(w => w == v)} {v}"))
            + $" ({worst.Count} programs).");
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
        foreach (var group in groups)
        {
            Line($"| {group.Verdict} | {group.Coords} | {group.Sites.Count} | "
                + $"{group.Sites.Select(x => x.Outcome.Item.Label).Distinct().Count()} |");
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
            foreach (var group in groups)
            {
                Line($"**{group.Verdict}** ({group.Coords}), {Math.Min(examples, group.Sites.Count)} of {group.Sites.Count}:");
                Line();
                foreach (var (outcome, site) in group.Sites.Take(examples))
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

        Line("## Programs not analysed");
        Line();
        Line("A probe reads only programs that compile today. The rest are counted here and say nothing about the rule.");
        Line();
        Line("| area | reason | programs |");
        Line("|---|---|---|");
        foreach (var area in AreaOrder)
        {
            var reasons = In(area).Where(o => o.Skipped != null).GroupBy(o => o.Skipped!)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in reasons)
                Line($"| {area} | {group.Key} | {group.Count()} |");
        }
        Line();
        var codes = Outcomes.Where(o => o.ErrorCode != null).GroupBy(o => o.ErrorCode!)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(8).Select(g => $"{g.Key} × {g.Count()}").ToList();
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
}
