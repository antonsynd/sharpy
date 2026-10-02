using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Xunit;
using Xunit.Abstractions;
using Oracle = Sharpy.Compiler.Tests.Conformance.ContextualKeywordEscapeOracle;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The P40 class harness (#2166): <b>an escaped contextual keyword parses as a fresh identifier
/// would</b>, over the fixture corpus. For every fixture of
/// <see cref="FormatterMeaningPreservationSweepTests.CorpusCensus"/> and every unescaped identifier
/// token whose value is in <see cref="ContextualKeywords.All"/> — at EVERY position, not only the
/// keyword's site: where the word is an ordinary identifier the twins agree by construction, so
/// those tokens are the sweep's own green controls and no position predicate can go stale or miss a
/// site — the escaped twin (that one token in backticks) and the renamed twin (that token replaced
/// by a fresh identifier of the escaped spelling's length) must have the same
/// <see cref="Oracle.Outcome"/>. One cell per (fixture, token value); a cell is red at its first
/// differing token. The node-kind shapes the corpus may lack are pinned by
/// <c>Parser/ContextualKeywordEscapeMatrixTests</c>.
///
/// <para><b>Ratchet.</b> <c>Conformance/contextual-keyword-escape-allowlist.txt</c> lists
/// <c>stem token # #issue reason</c> rows: a red cell must be listed, a listed cell that is green
/// fails (drain on fix). Every row cites #2166, the class tracker; the file is empty at the end of
/// P40 Phase 2.</para>
/// </summary>
public class ContextualKeywordEscapeSweepTests
{
    private const string AllowlistFileName = "contextual-keyword-escape-allowlist.txt";

    private readonly ITestOutputHelper _output;

    public ContextualKeywordEscapeSweepTests(ITestOutputHelper output) => _output = output;

    // ================================================================
    // Cells
    // ================================================================

    /// <summary>One cell's verdict: the twinned token count and the first red token's account, if any.</summary>
    internal sealed record Verdict(int Tokens, string? Red);

    /// <summary>Every (stem, token value) with at least one unescaped occurrence, with the occurrences.</summary>
    private static readonly Lazy<IReadOnlyDictionary<(string Stem, string Token), IReadOnlyList<Token>>> Cells = new(BuildCells);

    private static IReadOnlyDictionary<(string Stem, string Token), IReadOnlyList<Token>> BuildCells()
    {
        var cells = new SortedDictionary<(string, string), IReadOnlyList<Token>>();
        foreach (var fixture in FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus.Values)
        {
            var tokens = FormatterTwins.Lex(fixture.Source, out _)
                .Where(t => t.Type == TokenType.Identifier && !t.IsBacktickEscaped && t.Position >= 0
                    && ContextualKeywords.All.Contains(t.Value));
            foreach (var group in tokens.GroupBy(t => t.Value, StringComparer.Ordinal))
                cells[(fixture.Name, group.Key)] = group.ToList();
        }

        return cells;
    }

    public static IEnumerable<object[]> CellNames()
        => Cells.Value.Keys.Select(cell => new object[] { cell.Stem, cell.Token });

    private static readonly ConcurrentDictionary<(string Stem, string Token), Verdict> Verdicts = new();

    internal static Verdict Evaluate(string stem, string token)
        => Verdicts.GetOrAdd((stem, token), cell => Evaluate(
            FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus[cell.Stem].Source,
            Cells.Value[cell]));

    /// <summary>Twins each occurrence in turn; the renamed twin is the ground truth.</summary>
    private static Verdict Evaluate(string source, IReadOnlyList<Token> occurrences)
    {
        foreach (var token in occurrences)
        {
            // Instrument: the token's offsets are its text, so a twin replaces exactly it.
            if (source.Substring(token.Position, token.Length) != token.Value)
                return new Verdict(occurrences.Count, $"instrument: L{token.Line}:{token.Column} offsets do not cover '{token.Value}'");
            var (escaped, renamed) = Oracle.Twins(source, token);
            var e = Oracle.Observe(escaped);
            var r = Oracle.Observe(renamed);
            if (!e.SameAs(r))
                return new Verdict(occurrences.Count, $"L{token.Line}:{token.Column} — {Oracle.Difference(e, r)}");
        }

        return new Verdict(occurrences.Count, null);
    }

    [Theory]
    [MemberData(nameof(CellNames))]
    public void Escaped_ParsesAsTheRenamedTwin(string stem, string token)
    {
        var verdict = Evaluate(stem, token);
        var listed = Allowlist.Value.ContainsKey((stem, token));
        _output.WriteLine($"CKE {stem} {token} tokens={verdict.Tokens} {(verdict.Red == null ? "ok" : "red")}{(listed ? " allowlisted" : "")}");

        if (verdict.Red != null && !listed)
        {
            Assert.Fail($"{stem}: an escaped `{token}` must parse as a fresh identifier would: {verdict.Red}\n"
                + $"  (allowlist row: `{stem} {token} # #2166 escape read as the keyword`)");
        }

        if (verdict.Red == null && listed)
        {
            Assert.Fail($"{stem}: the escaped `{token}` parses as a fresh identifier now — {Allowlist.Value[(stem, token)]} is fixed for this cell: "
                + $"drain: delete the row `{stem} {token}` from Conformance/{AllowlistFileName}");
        }
    }

    // ================================================================
    // Controls and census
    // ================================================================

    /// <summary>
    /// The oracle discriminates, on spellings whose meaning no fix changes: the BARE placeholder
    /// (<c>f(1, _)</c> is a lambda) and the BARE accessor (<c>property get p</c> parses) each differ
    /// from their renamed twin — one by AST shape, one by diagnostics; a twin equals itself.
    /// </summary>
    [Fact]
    public void PositiveControl_TheOracleSeesAShapeAndADiagnosticDifference()
    {
        const string call = "f(1, _)\n";
        var bare = Oracle.Observe(call);
        var renamed = Oracle.Observe(call.Replace("_", Oracle.RenamedSpelling("_", call), StringComparison.Ordinal));
        Assert.False(bare.HasErrors || renamed.HasErrors);
        Assert.False(bare.SameAs(renamed), "a placeholder lambda and a call must differ in shape");
        Assert.True(renamed.SameAs(Oracle.Observe(call.Replace("_", Oracle.RenamedSpelling("_", call), StringComparison.Ordinal))));

        const string property = "class C:\n    property get p(self) -> int:\n        return 1\n";
        var accessor = Oracle.Observe(property);
        var named = Oracle.Observe(property.Replace("get", Oracle.RenamedSpelling("get", property), StringComparison.Ordinal));
        Assert.False(accessor.HasErrors);
        Assert.True(named.HasErrors);
        Assert.False(accessor.SameAs(named), "a parse and a parse error must differ");
    }

    /// <summary>
    /// Counts tokens, cells, red cells and allowlist rows per token and in total, for the record; the
    /// allowlist names only existing cells and cites #2166 first.
    /// </summary>
    [Fact]
    public void Census_TokensCellsAndRedCellsAreCounted()
    {
        var verdicts = Cells.Value.Keys.ToDictionary(cell => cell, cell => Evaluate(cell.Stem, cell.Token));
        var allowlist = Allowlist.Value;
        foreach (var token in ContextualKeywords.All.OrderBy(k => k, StringComparer.Ordinal))
        {
            var mine = verdicts.Where(kv => kv.Key.Token == token).ToList();
            _output.WriteLine($"CKE-CENSUS token {token} tokens={mine.Sum(kv => kv.Value.Tokens)} cells={mine.Count} "
                + $"red={mine.Count(kv => kv.Value.Red != null)} allowlisted={allowlist.Keys.Count(k => k.Token == token)}");
        }

        _output.WriteLine($"CKE-CENSUS tokens={verdicts.Values.Sum(v => v.Tokens)} cells={verdicts.Count} "
            + $"red={verdicts.Values.Count(v => v.Red != null)} allowlisted={allowlist.Count}");

        var unknown = allowlist.Keys.Where(k => !Cells.Value.ContainsKey(k)).ToList();
        Assert.True(unknown.Count == 0, "allowlist rows name no corpus cell: " + string.Join(", ", unknown.Select(k => $"{k.Stem} {k.Token}")));
        Assert.True(verdicts.Count > 0, "the sweep enumerates no cell");
    }

    // ================================================================
    // Allowlist
    // ================================================================

    private static readonly Lazy<IReadOnlyDictionary<(string Stem, string Token), string>> Allowlist = new(ReadAllowlist);

    /// <summary><c>stem token # #issue reason</c>; <c>#</c> starts the cite only after whitespace.</summary>
    private static IReadOnlyDictionary<(string Stem, string Token), string> ReadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var rows = new Dictionary<(string, string), string>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line.Substring(0, split)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cite = split < 0 ? "" : line.Substring(split + 2).Trim();
            if (fields.Length != 2 || !ContextualKeywords.All.Contains(fields[1]))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must read `stem token # #issue reason` with token a contextual keyword.");
            if (!Regex.IsMatch(cite, @"^#\d+\b"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must cite an issue first (# #N reason).");
            if (!rows.TryAdd((fields[0], fields[1]), cite))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' is listed twice.");
        }

        return rows;
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }
}
