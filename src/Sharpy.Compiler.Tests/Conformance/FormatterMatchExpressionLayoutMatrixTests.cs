using Sharpy.Compiler.Formatting;
using Xunit;
using Xunit.Abstractions;
using SLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Formatting writes a statement back with the blank-line layout a one-line statement in the same
/// place gets, whatever its expression kind (#2227). A match expression is the one expression that
/// owns indented lines: its last arm's line break also ends its host's line. At 21ca52b5d the host's
/// own terminator wrote a SECOND line break, so one blank line was invented after the arms of every
/// match-expression statement (and after every nested match expression's arms) — stable on a second
/// pass, so idempotence alone could not see it.
/// <para>Matrix: statement position {middle of a body, last in an inner body, last in a body before a
/// definition, last in the file, at module level, last at module level, followed by a comment,
/// followed by a blank line the user wrote, a blank line before the first arm and between two arms}
/// × host {assignment, annotated declaration, augmented
/// assignment, <c>let</c>, <c>return</c>, <c>yield</c>, expression statement, lambda body,
/// conditional's else branch, an enclosing match-expression arm}. A match expression as a call
/// ARGUMENT does not parse (ruling R-DT, #2210 — SPY0102 steers to a local), so it has no layout; its
/// cell pins the refusal and an unchanged text.</para>
/// <para>Oracles per cell P: <c>Format(P)</c> is clean and idempotent; its token stream (type and
/// value) equals P's; and its layout equals the layout of the PLAIN twin — P with the match expression
/// replaced by a one-line <c>"x"</c> — once the formatted arm lines are removed (the differential
/// oracle: no blank line is invented or lost by the expression kind). A user-written blank line between
/// two statements is kept as one line after any statement (R-ET, #2239 —
/// <c>FormattingTests.OneUserBlankLine_InsideFunctionIsKept</c>), and the plain twin pins exactly that;
/// inside the expression, a blank line between two arms is kept and one before the first arm (a body
/// start) is dropped — the plain-twin reading removes a blank line that precedes an arm with the arm.
/// Every cell except the lambda host (whose <c>lambda:</c> the unparser respells <c>lambda :</c>) is
/// also pinned as exact text: P itself, minus the blank line before the first arm.</para>
/// </summary>
public class FormatterMatchExpressionLayoutMatrixTests
{
    private readonly ITestOutputHelper _output;

    public FormatterMatchExpressionLayoutMatrixTests(ITestOutputHelper output) => _output = output;

    private const string Scrutinee = "match n:";
    private const string Plain = "\"x\"";

    /// <summary>Host label → the text written before the match expression on its first line.</summary>
    private static readonly (string Label, string Prefix)[] Hosts =
    {
        ("assignment", "s = "),
        ("annotated_declaration", "s: str = "),
        ("augmented_assignment", "s += "),
        ("let", "let s = "),
        ("return", "return "),
        ("yield", "yield "),
        ("expression_statement", "\"x\" + "),
        ("lambda_body", "g = lambda: "),
        ("conditional_else", "s = \"a\" if c else "),
        ("enclosing_arm", "s = "), // the arm's result is itself a match expression (below)
    };

    private static readonly string[] Positions =
    {
        "middle_of_body", "last_in_inner_body", "last_in_body_before_def", "last_in_file",
        "module_level", "module_level_last", "followed_by_comment", "followed_by_user_blank_line",
        "blank_between_arms",
    };

    public static TheoryData<string, string> Cells()
    {
        var data = new TheoryData<string, string>();
        foreach (var (host, _) in Hosts)
            foreach (var position in Positions)
                data.Add(host, position);
        return data;
    }

    /// <summary>The match-expression lines at <paramref name="indent"/>, or the plain one-liner.</summary>
    private static List<string> Statement(string host, int indent, bool plain)
    {
        var pad = new string(' ', indent);
        var prefix = Hosts.Single(h => h.Label == host).Prefix;
        if (plain)
            return new List<string> { pad + prefix + Plain };
        if (host == "enclosing_arm")
        {
            return new List<string>
            {
                pad + prefix + Scrutinee,
                pad + "    case 3: " + Scrutinee,
                pad + "        case 3: \"a\"",
                pad + "        case _: \"b\"",
                pad + "    case _: \"c\"",
            };
        }
        return new List<string> { pad + prefix + Scrutinee, pad + "    case 3: \"three\"", pad + "    case _: \"other\"" };
    }

    /// <summary>The cell program; <paramref name="droppedBlankLine"/> is the line index of a blank line the user wrote that formatting drops (or -1).</summary>
    private static string Program(string host, string position, bool plain, out int droppedBlankLine)
    {
        const string Def = "def f(n: int, c: bool) -> str:";
        droppedBlankLine = -1;
        var lines = new List<string>();
        switch (position)
        {
            case "middle_of_body":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.AddRange(Statement(host, 4, plain));
                lines.Add("    print(s)");
                lines.Add("    return s");
                break;
            case "last_in_inner_body":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.Add("    if n > 0:");
                lines.AddRange(Statement(host, 8, plain));
                lines.Add("    return s");
                break;
            case "last_in_body_before_def":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.AddRange(Statement(host, 4, plain));
                lines.Add("");
                lines.Add("");
                lines.Add("def g() -> int:");
                lines.Add("    return 1");
                break;
            case "last_in_file":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.AddRange(Statement(host, 4, plain));
                break;
            case "module_level":
                lines.Add("n = 3");
                lines.Add("c = True");
                lines.Add("s = \"\"");
                lines.AddRange(Statement(host, 0, plain));
                lines.Add("print(s)");
                break;
            case "module_level_last":
                lines.Add("n = 3");
                lines.Add("c = True");
                lines.Add("s = \"\"");
                lines.AddRange(Statement(host, 0, plain));
                break;
            case "followed_by_comment":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.AddRange(Statement(host, 4, plain));
                lines.Add("    # after");
                lines.Add("    return s");
                break;
            case "followed_by_user_blank_line":
                lines.Add(Def);
                lines.Add("    s = \"\"");
                lines.AddRange(Statement(host, 4, plain));
                lines.Add("");
                lines.Add("    return s");
                break;
            case "blank_between_arms":
                {
                    lines.Add(Def);
                    lines.Add("    s = \"\"");
                    var statement = Statement(host, 4, plain);
                    if (!plain)
                    {
                        // A blank line between the outermost expression's last two arms (kept) and one
                        // before its first arm (a body start: dropped).
                        statement.Insert(statement.Count - 1, "");
                        statement.Insert(1, "");
                        droppedBlankLine = lines.Count + 1;
                    }
                    lines.AddRange(statement);
                    lines.Add("    return s");
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(position), position, null);
        }
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// The formatted match-expression program read as its plain twin: arm lines — and a blank line
    /// right before an arm, which belongs to the expression — removed, the match header's
    /// <c>match n:</c> read as <c>"x"</c>.
    /// </summary>
    private static string AsPlainTwin(string formatted)
    {
        var lines = formatted.Split('\n');
        static bool IsArm(string line) => line.TrimStart().StartsWith("case ", StringComparison.Ordinal);
        return string.Join("\n", lines
            .Where((l, i) => !IsArm(l) && !(l.Length == 0 && i + 1 < lines.Length && IsArm(lines[i + 1])))
            .Select(l => l.EndsWith(Scrutinee, StringComparison.Ordinal) ? l[..^Scrutinee.Length] + Plain : l));
    }

    [Theory]
    [MemberData(nameof(Cells))]
    [Trait("Category", "Conformance")]
    public void Format_MatchExpressionStatement_GetsThePlainStatementLayout(string host, string position)
    {
        var label = $"{host}.{position}";
        var program = Program(host, position, plain: false, out var droppedBlankLine);
        var plainTwin = Program(host, position, plain: true, out _);

        var formatted = FormatterService.Format(program);
        Assert.True(formatted.Diagnostics.Count == 0,
            $"{label}: format reported " + string.Join("; ", formatted.Diagnostics.Select(d => $"{d.Code} {d.Message}")));
        var text = formatted.FormattedText;
        _output.WriteLine($"{label} formatted:\n{text}");

        var plainFormatted = FormatterService.Format(plainTwin);
        Assert.True(plainFormatted.Diagnostics.Count == 0, $"{label}: the plain twin must format clean");

        // Differential layout oracle: the expression kind changes no blank line around the statement.
        Assert.Equal(plainFormatted.FormattedText, AsPlainTwin(text));

        // Exact layout: the cell program is already in the ruled layout (R-ET, #2239: the user's blank
        // line between statements or arms is kept) apart from a blank line before the first arm.
        if (host != "lambda_body")
        {
            var expected = program.Split('\n').ToList();
            if (droppedBlankLine >= 0)
                expected.RemoveAt(droppedBlankLine);
            Assert.Equal(string.Join("\n", expected), text);
        }

        Assert.Equal(Tokens(program), Tokens(text));
        Assert.Equal(text, FormatterService.Format(text).FormattedText);
    }

    /// <summary>
    /// The argument host: a match expression inside a call's brackets does not parse (R-DT, #2210), so
    /// the formatter reports the parse error and leaves the text as written.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Format_MatchExpressionAsArgument_IsRefusedByTheParser_TextUnchanged()
    {
        var program = "def f(n: int) -> str:\n    print(match n:\n        case 3: \"three\"\n        case _: \"other\")\n    return \"x\"\n";
        var formatted = FormatterService.Format(program);
        Assert.Contains(formatted.Diagnostics, d => d.Code == "SPY0102");
        Assert.Equal(program, formatted.FormattedText);
        Assert.False(formatted.HasChanges);
    }

    private static List<(string Type, string Value)> Tokens(string text)
    {
        var lexer = new SLexer(text);
        var tokens = lexer.TokenizeAll();
        Assert.False(lexer.Diagnostics.HasErrors, $"lex failed:\n{text}");
        return tokens.Select(t => (t.Type.ToString(), t.Value)).ToList();
    }
}
