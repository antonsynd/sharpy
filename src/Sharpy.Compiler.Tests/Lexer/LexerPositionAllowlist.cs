using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The ratchet for the position instrument (<see cref="LexerCorpusTwinTests"/>, <see cref="EofConstructRosterTests"/>;
/// #2300 C2/C3/C6): <c>lexer-position-allowlist.txt</c>, rows
/// <c>&lt;scope&gt; &lt;where&gt; :: &lt;TokenType&gt; '&lt;Value&gt;'@L&lt;line&gt;:C&lt;column&gt; &lt;R-rules&gt; # #NNNN reason</c>.
/// A row suppresses the violations whose SUBJECT is that one token — its type and Value at that (line, column) — and whose
/// rule is one the row names (<c>R3,R7</c>), in that one corpus file
/// (<c>corpus &lt;relative path&gt;</c>) or roster construct (<c>roster &lt;construct name&gt;</c>), in every twin or
/// ending — a lexer position defect found at base, filed by the lead, drained when the lexer is fixed. A row without a
/// <c>#\d+</c> cite fails; a row that suppresses nothing, or names a rule that no longer fires, fails as stale (drain on
/// fix). Every other violation in the same file or construct still fails, including another token of the same type, the
/// same token with a different Value, and a rule the row does not name: the row admits today's defect, never a second one.
/// </summary>
public static partial class LexerPositionAllowlist
{
    public const string FileName = "lexer-position-allowlist.txt";

    public sealed record Row(int Line, string Scope, string TokenType, IReadOnlySet<string> Rules, string Rest);

    [GeneratedRegex(@"^(?<scope>(corpus|roster) .+?) :: (?<type>[A-Za-z]+ '.*?'@L\d+:C\d+) (?<rules>R\d(?:,R\d)*) +(?<rest>#.*)$")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"#\d+")]
    private static partial Regex Cite();

    public static string Path => System.IO.Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler.Tests", "Lexer", FileName);

    /// <summary>The rows, and one failure line per malformed row (no cite, or not the row shape).</summary>
    public static (List<Row> Rows, List<string> Malformed) Read(IEnumerable<string> lines)
    {
        var rows = new List<Row>();
        var malformed = new List<string>();
        var number = 0;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var match = RowPattern().Match(line);
            if (!match.Success)
            {
                malformed.Add($"{FileName} line {number}: not '<corpus|roster> <where> :: <TokenType> '<Value>'@L<line>:C<column> <R-rules> # #NNNN reason': {line}");
                continue;
            }
            if (!Cite().IsMatch(match.Groups["rest"].Value))
            {
                malformed.Add($"{FileName} line {number}: no #NNNN issue cite: {line}");
                continue;
            }
            var rules = match.Groups["rules"].Value.Split(',').ToHashSet(StringComparer.Ordinal);
            rows.Add(new Row(number, match.Groups["scope"].Value, match.Groups["type"].Value, rules, match.Groups["rest"].Value));
        }
        return (rows, malformed);
    }

    public static (List<Row> Rows, List<string> Malformed) Read() => Read(File.ReadAllLines(Path));

    [GeneratedRegex(@"^(?<type>[A-Za-z]+) (?<value>'.*?') L(?<line>\d+):C(?<column>-?\d+)@")]
    private static partial Regex Described();

    /// <summary>
    /// The token a violation line is about, as <c>Type 'Value'@L&lt;line&gt;:C&lt;column&gt;</c>: the token a rule line names
    /// (<c>R1 Eof '' L1:C11@10 …</c>), the earlier token of an R7 gap (<c>R7 gap […) between FStringEnd '…' L5:C24@88 …</c>),
    /// or the category of a twin disagreement (TOKENS/DIAGNOSTICS/FACTS/CODES/THROW, R6 — never a token, so never
    /// allowlistable: they always fail).
    /// </summary>
    public static string Subject(string violation)
    {
        var words = violation.Split(' ', 3);
        if (words.Length == 3 && words[0].Length == 2 && words[0][0] == 'R' && char.IsDigit(words[0][1]))
        {
            var described = words[1] == "gap"
                ? violation.IndexOf(" between ", StringComparison.Ordinal) is var at and >= 0 ? violation[(at + " between ".Length)..] : ""
                : violation[(words[0].Length + 1)..];
            var match = Described().Match(described);
            if (match.Success)
                return $"{match.Groups["type"].Value} {match.Groups["value"].Value}@L{match.Groups["line"].Value}:C{match.Groups["column"].Value}";
        }
        return words[0];
    }

    /// <summary>
    /// Splits <paramref name="violations"/> (each paired with its scope) into the ones no row covers and the count each row
    /// covered; a row covering none, or naming a rule that covered nothing, is stale. Only rows whose scope starts with
    /// <paramref name="scopeKind"/> are judged.
    /// </summary>
    public static (List<string> Remaining, List<string> Stale, int Allowlisted) Apply(
        IReadOnlyList<Row> rows, string scopeKind, IEnumerable<(string Scope, string Violation)> violations)
    {
        var mine = rows.Where(r => r.Scope.StartsWith(scopeKind + " ", StringComparison.Ordinal)).ToList();
        var used = new HashSet<(Row, string)>();
        var remaining = new List<string>();
        var allowlisted = 0;
        foreach (var (scope, violation) in violations)
        {
            var rule = violation.Split(' ', 2)[0];
            var row = mine.FirstOrDefault(r => r.Scope == scope && r.TokenType == Subject(violation) && r.Rules.Contains(rule));
            if (row == null)
            {
                remaining.Add(violation);
                continue;
            }
            used.Add((row, rule));
            allowlisted++;
        }
        var stale = mine
            .SelectMany(r => r.Rules.Where(rule => !used.Contains((r, rule))).Select(rule => (Row: r, Rule: rule)))
            .Select(x => $"STALE {FileName} line {x.Row.Line}: '{x.Row.Scope} :: {x.Row.TokenType}' rule {x.Rule} suppresses nothing — "
                + "delete the rule or the row (drain on fix)")
            .ToList();
        return (remaining, stale, allowlisted);
    }
}

public class LexerPositionAllowlistTests
{
    [Fact]
    public void EveryRow_HasTheShapeAndAnIssueCite()
    {
        var (_, malformed) = LexerPositionAllowlist.Read();
        malformed.Should().BeEmpty();
    }

    [Fact]
    public void TheRatchet_RefusesAnUncitedRow_SuppressesOnlyItsSubject_AndFailsWhenStale()
    {
        var (rows, malformed) = LexerPositionAllowlist.Read(new[]
        {
            "corpus a.spy :: FStringEnd '\"\"\"'@L5:C24 R3,R7 # #2338 cited",
            "corpus b.spy :: FStringEnd '\"\"\"'@L5:C24 R3 # no cite",
            "corpus d.spy :: FStringEnd R3 # #2338 a kind, not a token",
            "corpus e.spy :: FStringEnd '\"\"\"'@L5:C24 # #2338 no rule named",
            "roster gone :: Eof ''@L1:C11 R1 # #2338 stale",
            "roster half :: Eof ''@L1:C11 R1,R6 # #2338 R6 never fires",
        });
        malformed.Should().HaveCount(3);
        malformed.Should().ContainSingle(m => m.Contains("no #NNNN issue cite"));
        malformed.Where(m => m.Contains("not '<corpus|roster>")).Should().HaveCount(2);

        var (remaining, stale, allowlisted) = LexerPositionAllowlist.Apply(rows, "corpus", new[]
        {
            ("corpus a.spy", "R3 FStringEnd '\"\"\"' L5:C24@88: slice ≠ Value"),
            ("corpus a.spy", "R7 gap [91,100) between FStringEnd '\"\"\"' L5:C24@88 and Newline '\\n' L6:C12@100 is not trivia"),
            ("corpus a.spy", "R1 Identifier 'x' L1:C2@0: off"),
            ("corpus a.spy", "R3 FStringEnd '\"\"\"' L9:C24@130: slice ≠ Value"),
            ("corpus a.spy", "R3 FStringEnd '}' L5:C24@88: slice ≠ Value"),
            ("corpus a.spy", "R1 FStringEnd '\"\"\"' L5:C24@88: position 88 is not on line 5"),
            ("corpus a.spy", "TOKENS a.spy [cr/as-is]: first difference"),
            ("corpus c.spy", "R3 FStringEnd '\"\"\"' L5:C24@88: slice ≠ Value"),
        });
        allowlisted.Should().Be(2, "the row suppresses its subject token type in its own file only");
        remaining.Should().HaveCount(6, "another FStringEnd in the same file, the same token with another Value, the same token "
            + "under a rule the row does not name, another token, a disagreement and another file all still fail");
        stale.Should().BeEmpty();

        var (_, rosterStale, rosterAllowlisted) = LexerPositionAllowlist.Apply(rows, "roster", new[]
        {
            ("roster half", "R1 Eof '' L1:C11@10: position 10 is not on line 1"),
        });
        rosterAllowlisted.Should().Be(1);
        rosterStale.Should().HaveCount(2, "the row that matched nothing, and the rule R6 that fired nowhere");
        rosterStale.Should().AllSatisfy(m => m.Should().StartWith("STALE"));
        rosterStale.Should().ContainSingle(m => m.Contains("'roster half") && m.Contains("rule R6"));
    }
}
