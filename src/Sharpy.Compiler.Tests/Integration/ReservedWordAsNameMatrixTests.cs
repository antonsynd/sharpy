using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2209 (ruling R-ED) — a reserved word written where a name is introduced is refused with a
/// diagnostic that NAMES the word and its backtick escape. Matrix: <b>every keyword of the lexer's
/// table × position {local <c>=</c>, local annotation, class field, decorated class field,
/// parameter, function name}</b> = 53 × 6 = 318 parse cells, plus the backtick positive control
/// (every keyword escaped, in every position, compiled and RUN).
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 6b208066e):</b> the refusals
/// were SPY0100 "Unexpected token: Auto", SPY0101 "Expected identifier, got Colon", SPY0102,
/// SPY0103, SPY0104, SPY0116, SPY0134, SPY0225 "Cannot assign to boolean literal", SPY0107, and
/// under a decorator SPY0105 "Decorators can only be applied to ..." (all 53) — none said the word
/// is reserved, except the three undecorated <c>let</c> statement cells (own steer, kept). Nine
/// cells were ACCEPTED and stay accepted (<see cref="AcceptedCells"/>): <c>case</c> in every
/// undecorated position, and <c>type</c>/<c>match</c> as a parameter or function name (soft in
/// <c>ExpectIdentifier</c>).</para>
///
/// <para><b>Roster anchoring.</b> <see cref="Keywords"/> is spelled as literals and pinned
/// set-equal to <c>Lexer.KeywordNames</c> (minus the contextual <c>self</c>) — a roster read from
/// the table under test would make a missing keyword invisible.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ReservedWordAsNameMatrixTests : IntegrationTestBase
{
    public ReservedWordAsNameMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly string[] Keywords =
    {
        "def", "class", "struct", "interface", "enum", "union", "if", "else", "elif", "while", "for",
        "in", "return", "break", "continue", "pass", "try", "except", "finally", "raise", "assert",
        "with", "import", "from", "as", "auto", "const", "let", "lambda", "type", "match", "case",
        "async", "await", "yield", "property", "event", "delegate", "del", "maybe", "super", "Self",
        "defer", "do", "global", "nonlocal", "True", "False", "None", "and", "or", "not", "is",
    };

    private static readonly Dictionary<string, string> Positions = new()
    {
        ["local"] = "def main():\n    {0} = 1\n    print(1)\n",
        ["local_annot"] = "def main():\n    {0}: int = 1\n    print(1)\n",
        ["field"] = "class C:\n    {0}: int = 0\n\ndef main():\n    print(1)\n",
        ["decorated_field"] = "class C:\n    @static\n    {0}: int = 0\n\ndef main():\n    print(1)\n",
        ["param"] = "def f({0}: int) -> int:\n    return 1\n\ndef main():\n    print(f(1))\n",
        ["funcname"] = "def {0}() -> int:\n    return 1\n\ndef main():\n    print(1)\n",
    };

    /// <summary>The cells the parser accepted at the base commit — they must keep parsing.</summary>
    private static readonly HashSet<(string Keyword, string Position)> AcceptedCells = new()
    {
        ("case", "local"), ("case", "local_annot"), ("case", "field"),
        ("case", "param"), ("case", "funcname"),
        ("type", "param"), ("type", "funcname"),
        ("match", "param"), ("match", "funcname"),
    };

    [Fact]
    public void Roster_IsTheLexerKeywordTable()
    {
        Keywords.Should().HaveCount(53).And.OnlyHaveUniqueItems();
        LexerNs.Lexer.KeywordNames.Where(k => k != "self")
            .Should().BeEquivalentTo(Keywords, "the matrix must cover every keyword of the lexer's table");
    }

    public static IEnumerable<object[]> Cells() =>
        from kw in Keywords from pos in Positions.Keys select new object[] { kw, pos };

    [Theory]
    [MemberData(nameof(Cells))]
    public void ReservedWord_InNamePosition_NamesTheWordAndTheEscape(string keyword, string position)
    {
        var source = string.Format(System.Globalization.CultureInfo.InvariantCulture, Positions[position], keyword);
        var errors = ParseErrors(source);

        if (AcceptedCells.Contains((keyword, position)))
        {
            errors.Should().BeEmpty($"`{keyword}` in position {position} parsed at the base commit and must still parse:\n{source}");
            return;
        }

        errors.Should().NotBeEmpty($"`{keyword}` in position {position} is a reserved word and must be refused:\n{source}");
        var first = errors[0];
        first.Code.Should().Be("SPY0101", $"source:\n{source}\nmessage: {first.Message}");
        first.Message.Should().Contain($"'{keyword}' is a", $"source:\n{source}");
        first.Message.Should().Contain($"write `{keyword}` in backticks to use it as a name", $"source:\n{source}");
    }

    /// <summary>
    /// Statements that START with a keyword followed by <c>=</c>/<c>:</c> but are NOT a binding
    /// must keep parsing — the absence half of the steer's trigger.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    try:\n        print(1)\n    except:\n        print(2)\n    finally:\n        print(3)\n")]
    [InlineData("def main():\n    if True:\n        print(1)\n    else:\n        print(2)\n")]
    [InlineData("def main():\n    lambda: 1\n")]
    [InlineData("def main():\n    lambda x: x == 1\n")]
    [InlineData("def main():\n    lambda: print(sep='')\n")]
    [InlineData("type Alias = int\n")]
    [InlineData("def main():\n    case = 1\n    print(case)\n")]
    [InlineData("def main():\n    `type`: int = 0\n    print(`type`)\n")]
    public void KeywordLedStatements_ThatAreNotBindings_StillParse(string source)
    {
        ParseErrors(source).Should().BeEmpty(source);
    }

    /// <summary>
    /// The positive control: the escape the steer names WORKS — every keyword, backtick-escaped,
    /// binds and reads back in every position. One program per position, RUN.
    /// <c>Self</c> is bound but not read as a value in the local/parameter programs: reading a
    /// backtick-escaped <c>Self</c> emits C# <c>this</c> (CS0026), a findings-ledger cell of this
    /// round outside the #2209 contract.
    /// </summary>
    [Theory]
    [InlineData("local")]
    [InlineData("local_annot")]
    [InlineData("field")]
    [InlineData("param")]
    [InlineData("funcname")]
    public void BacktickEscape_OfEveryKeyword_CompilesAndRuns(string position)
    {
        // The value read back: the escaped name itself, except `Self` (see the summary).
        static string Read(string kw, int i) => kw == "Self" ? $"{i}" : $"`{kw}`";
        var indexed = Keywords.Select((kw, i) => (kw, i)).ToList();
        string source = position switch
        {
            "local" => "def main():\n" + string.Concat(indexed.Select(c =>
                $"    `{c.kw}` = {c.i}\n    print({Read(c.kw, c.i)})\n")),
            "local_annot" => "def main():\n" + string.Concat(indexed.Select(c =>
                $"    `{c.kw}`: int = {c.i}\n    print({Read(c.kw, c.i)})\n")),
            "field" => "class C:\n" + string.Concat(indexed.Select(c => $"    `{c.kw}`: int = {c.i}\n"))
                + "\ndef main():\n    c = C()\n" + string.Concat(indexed.Select(c => $"    print(c.`{c.kw}`)\n")),
            "param" => string.Concat(indexed.Select(c =>
                    $"def f{c.i}(`{c.kw}`: int) -> int:\n    return {Read(c.kw, c.i)}\n\n"))
                + "def main():\n" + string.Concat(indexed.Select(c => $"    print(f{c.i}({c.i}))\n")),
            "funcname" => string.Concat(indexed.Select(c => $"def `{c.kw}`() -> int:\n    return {c.i}\n\n"))
                + "def main():\n" + string.Concat(indexed.Select(c => $"    print(`{c.kw}`())\n")),
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)}\n{source}");
        var expected = string.Concat(indexed.Select(c => c.i + "\n"));
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Should().Be(expected);
    }

    private static List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> ParseErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        return lexer.Diagnostics.GetErrors().Concat(parser.Diagnostics.GetErrors()).ToList();
    }
}
