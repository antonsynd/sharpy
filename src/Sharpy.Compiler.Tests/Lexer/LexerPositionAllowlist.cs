using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The ratchet for the position instrument (<see cref="LexerCorpusTwinTests"/>, <see cref="EofConstructRosterTests"/>;
/// #2300 C2/C3/C6): <c>lexer-position-allowlist.txt</c>, rows <c>&lt;scope&gt; &lt;where&gt; :: &lt;TokenType&gt;@L&lt;line&gt;:C&lt;column&gt; # #NNNN reason</c>.
/// A row suppresses the violations whose SUBJECT is that one token — its type at that (line, column) — in that one corpus file
/// (<c>corpus &lt;relative path&gt;</c>) or roster construct (<c>roster &lt;construct name&gt;</c>), in every twin or
/// ending — a lexer position defect found at base, filed by the lead, drained when the lexer is fixed. A row without a
/// <c>#\d+</c> cite fails; a row that suppresses nothing fails as stale (drain on fix). Every other violation in the same
/// file or construct still fails, including another token of the same type: the row names one token, not a file or a kind.
/// </summary>
public static partial class LexerPositionAllowlist
{
    public const string FileName = "lexer-position-allowlist.txt";

    public sealed record Row(int Line, string Scope, string TokenType, string Rest);

    [GeneratedRegex(@"^(?<scope>(corpus|roster) .+?) :: (?<type>[A-Za-z]+@L\d+:C\d+) +(?<rest>#.*)$")]
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
                malformed.Add($"{FileName} line {number}: not '<corpus|roster> <where> :: <TokenType>@L<line>:C<column> # #NNNN reason': {line}");
                continue;
            }
            if (!Cite().IsMatch(match.Groups["rest"].Value))
            {
                malformed.Add($"{FileName} line {number}: no #NNNN issue cite: {line}");
                continue;
            }
            rows.Add(new Row(number, match.Groups["scope"].Value, match.Groups["type"].Value, match.Groups["rest"].Value));
        }
        return (rows, malformed);
    }

    public static (List<Row> Rows, List<string> Malformed) Read() => Read(File.ReadAllLines(Path));

    [GeneratedRegex(@"^(?<type>[A-Za-z]+) '.*?' L(?<line>\d+):C(?<column>-?\d+)@")]
    private static partial Regex Described();

    /// <summary>
    /// The token a violation line is about, as <c>Type@L&lt;line&gt;:C&lt;column&gt;</c>: the token a rule line names
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
                return $"{match.Groups["type"].Value}@L{match.Groups["line"].Value}:C{match.Groups["column"].Value}";
        }
        return words[0];
    }

    /// <summary>
    /// Splits <paramref name="violations"/> (each paired with its scope) into the ones no row covers and the count each row
    /// covered; a row covering none is stale. Only rows whose scope starts with <paramref name="scopeKind"/> are judged.
    /// </summary>
    public static (List<string> Remaining, List<string> Stale, int Allowlisted) Apply(
        IReadOnlyList<Row> rows, string scopeKind, IEnumerable<(string Scope, string Violation)> violations)
    {
        var mine = rows.Where(r => r.Scope.StartsWith(scopeKind + " ", StringComparison.Ordinal)).ToList();
        var used = new HashSet<Row>();
        var remaining = new List<string>();
        var allowlisted = 0;
        foreach (var (scope, violation) in violations)
        {
            var row = mine.FirstOrDefault(r => r.Scope == scope && r.TokenType == Subject(violation));
            if (row == null)
            {
                remaining.Add(violation);
                continue;
            }
            used.Add(row);
            allowlisted++;
        }
        var stale = mine.Where(r => !used.Contains(r))
            .Select(r => $"STALE {FileName} line {r.Line}: '{r.Scope} :: {r.TokenType}' suppresses nothing — delete the row (drain on fix)")
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
            "corpus a.spy :: FStringEnd@L5:C24 # #2338 cited",
            "corpus b.spy :: FStringEnd@L5:C24 # no cite",
            "corpus d.spy :: FStringEnd # #2338 a kind, not a token",
            "roster gone :: Eof@L1:C11 # #2338 stale",
        });
        malformed.Should().HaveCount(2);
        malformed.Should().ContainSingle(m => m.Contains("no #NNNN issue cite"));
        malformed.Should().ContainSingle(m => m.Contains("not '<corpus|roster>"));

        var (remaining, stale, allowlisted) = LexerPositionAllowlist.Apply(rows, "corpus", new[]
        {
            ("corpus a.spy", "R3 FStringEnd '\"\"\"' L5:C24@88: slice ≠ Value"),
            ("corpus a.spy", "R7 gap [91,100) between FStringEnd '\"\"\"' L5:C24@88 and Newline '\\n' L6:C12@100 is not trivia"),
            ("corpus a.spy", "R1 Identifier 'x' L1:C2@0: off"),
            ("corpus a.spy", "R3 FStringEnd '\"\"\"' L9:C24@130: slice ≠ Value"),
            ("corpus a.spy", "TOKENS a.spy [cr/as-is]: first difference"),
            ("corpus c.spy", "R3 FStringEnd '\"\"\"' L5:C24@88: slice ≠ Value"),
        });
        allowlisted.Should().Be(2, "the row suppresses its subject token type in its own file only");
        remaining.Should().HaveCount(4, "another FStringEnd in the same file, another token, a disagreement and another file all still fail");
        stale.Should().BeEmpty();

        var (_, rosterStale, _) = LexerPositionAllowlist.Apply(rows, "roster", Array.Empty<(string, string)>());
        rosterStale.Should().ContainSingle().Which.Should().StartWith("STALE");
    }
}
