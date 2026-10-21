using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// #2300 row C4 (P68): every f/t/df-string lexer arm × prefix {<c>f</c>, <c>t</c>, <c>df</c>} ×
/// quoting {single, triple}. Each cell lexes one source and asserts the WHOLE token (Type, Value)
/// sequence, or the code and line of the FIRST diagnostic it is refused with (later codes are
/// recovery resuming on the next line). The arm roster is read from
/// <c>NextFStringToken</c>, <c>NextFStringSpecToken</c> and <c>PrescanDedentedTripleFString</c>
/// (<c>Lexer.FStrings.cs</c> @ 220259b51).
///
/// <para><b>Why hand cells (plan Design Decision 5).</b> The arms are PEP 701 holes, PEP 750
/// <c>t</c> and PEP 822 <c>df</c> plus the SPY0020–SPY0022/SPY0030 refusals; CPython 3.12 (the
/// pinned oracle) has no <c>t</c>/<c>d</c> prefix, so a generator would reach only <c>f</c>. The
/// f-prefix VALUE surface already has generators (<c>FormatSpecDifferentialTests</c>,
/// <c>GenExpressions.FStringLiteralExpr</c>).</para>
///
/// <para><b>Applicability is a literal.</b> Every arm runs under all three prefixes (a t-string
/// "supports the same interpolation syntax as f-strings", template_strings.md; a single-quoted
/// <c>df"…"</c> is an f-string, dstrings.md "Single-line d-strings"). An arm's
/// <see cref="Arm.Single"/> or <see cref="Arm.Triple"/> outcome is null only with a
/// <see cref="Arm.NotApplicable"/> reason, and <see cref="Arm.DfTriple"/> overrides the triple
/// outcome for <c>df"""</c> where PEP 822 dedent makes it differ.</para>
///
/// <para><b>Source markers.</b> <c>«</c> opens, <c>»</c> closes, <c>§</c> is the cell's bare quote
/// (<c>"</c> or <c>"""</c>), <c>⇥</c> is the cell's dedent indent. A <c>df"""</c> cell wraps the body
/// in the spec's canonical dedent shape — <c>df"""\n    BODY\n    """</c> — so every df-triple cell
/// also exercises the dropped first line, the dedent skip and the swallowed pre-close newline
/// (dstrings.md §df). A <see cref="Arm.Raw"/> arm writes the whole literal itself (« = prefix +
/// quote, » = quote): the PEP 822 contrast arms, whose f/t outcome keeps the indentation and whose
/// df outcome strips it.</para>
///
/// <para><b>Oracles.</b> Every f-prefix value is CPython 3.12.13's, probed before it was written with
/// <c>python3 -c 'x=5;w=4;a=b=1;s="s";xs=[1,2,3];y=4;z="d";f=dict;print(repr(SRC))'</c> and with
/// <c>tokenize.generate_tokens</c> for the FSTRING_MIDDLE text (the lexer's FStringText /
/// FStringFormatSpec value); each arm's <see cref="Arm.Python"/> records the result. <c>t</c>/<c>df</c>
/// outcomes come from executed spec examples (template_strings.md "Format Specs and Nested Fields"
/// and "Conversions and Self-Documenting Fields", dstrings.md §df — run with the built sharpyc @
/// 220259b51, printed exactly the spec's output).</para>
///
/// <para><b>Left to the findings ledger, not asserted (P68 Phase 4):</b> <c>{{</c> inside a spec
/// (the lexer reads a literal <c>{</c>; CPython 3.12 refuses it; fstrings.md silent) and a line break
/// in a SINGLE-quoted spec (the lexer keeps <c>\n</c>; CPython 3.12's FSTRING_MIDDLE drops it, 3.13
/// refuses it) — the lexer's token differs from CPython's, so no cell pins it either way. The
/// <c>\"</c> spec cell IS asserted: its token matches CPython's FSTRING_MIDDLE (it is refused SPY0609
/// downstream, a ledger line outside the lexer).</para>
///
/// <para><b>PEP 822 refusal arms (#2337).</b> dstrings.md refuses a content line shallower than the
/// close and a non-whitespace close line (SPY0029), and drops the first and pre-close newlines for any
/// strip amount. The <c>d"""</c> cells (<see cref="DedentRule_DString_IsTheSpecControl"/>) are live; the
/// <c>df"""</c> twins assert the SPEC's verdict and are skipped citing #2337, so they un-skip when it
/// closes.</para>
/// </summary>
public class FStringArmMatrixTests
{
    private readonly ITestOutputHelper _output;

    public FStringArmMatrixTests(ITestOutputHelper output) => _output = output;

    private sealed class Outcome
    {
        // The AsSingle sentinel carries neither; a real outcome carries exactly one.
        public (TokenType Type, string Value)[]? Inner { get; private init; }
        public string? Code { get; private init; }

        public static Outcome Lex(params (TokenType, string)[] inner) => new() { Inner = inner };
        public static Outcome Refuse(string code) => new() { Code = code };
    }

    private sealed record Arm(
        string Name,
        string Body,
        Outcome? Single,
        Outcome? Triple,
        string Python,
        Outcome? DfTriple = null,
        bool Raw = false,
        string? NotApplicable = null,
        string? NestedPrefix = null);

    private static readonly string[] Prefixes = { "f", "t", "df" };
    private static readonly bool[] Quotings = { false, true };

    private const string SPY0002 = DiagnosticCodes.Lexer.UnterminatedFString;
    private const string SPY0020 = DiagnosticCodes.Lexer.UnterminatedFStringExpression;
    private const string SPY0021 = DiagnosticCodes.Lexer.UnmatchedBraceInFString;
    private const string SPY0022 = DiagnosticCodes.Lexer.UnterminatedFormatSpec;
    private const string SPY0030 = DiagnosticCodes.Lexer.InvalidFStringConversion;

    private const string NoLineBreakInSingle = "a single-quoted string's text cannot hold a line break (the 'text: a line break' arm refuses it, SPY0002)";

    private static (TokenType, string) T(TokenType type, string value) => (type, value);
    private static readonly (TokenType, string) Es = (TokenType.FStringExprStart, "{");
    private static readonly (TokenType, string) Ee = (TokenType.FStringExprEnd, "}");
    private static (TokenType, string) Id(string name) => (TokenType.Identifier, name);
    private static (TokenType, string) Text(string text) => (TokenType.FStringText, text);
    private static (TokenType, string) Spec(string text) => (TokenType.FStringFormatSpec, text);

    /// <summary>A <see cref="Arm.Triple"/> of <c>AsSingle</c>: the triple cell's outcome is the single cell's.</summary>
    private static readonly Outcome AsSingle = new();

    /// <summary>The arm roster. One row per arm; both outcomes, or a reason.</summary>
    private static readonly Arm[] Arms =
    {
        // ── literal text ──────────────────────────────────────────────────────────────────────
        new("text: {{ and }} escape to a brace", "«a{{b}}c»",
            Lex(Text("a{b}c")), Lex(Text("a{b}c")), "f\"a{{b}}c\" → 'a{b}c'"),
        new("text: a lone } is refused", "«a}b»",
            Refuse(SPY0021), Refuse(SPY0021), "f\"a}b\" → SyntaxError: f-string: single '}' is not allowed"),
        new("text: a backslash escape is decoded", "«a\\tb»",
            Lex(Text("a\tb")), Lex(Text("a\tb")), "f\"a\\tb\" → 'a\\tb' (a TAB)"),
        new("text: a backslash at EOF", "«abc\\",
            Refuse(SPY0002), Refuse(SPY0002), "f\"abc\\ → SyntaxError: unterminated f-string literal"),
        new("text: the other quote character is text", "«a'b»",
            Lex(Text("a'b")), Lex(Text("a'b")), "f\"a'b\" → \"a'b\""),
        new("text: one or two quotes do not end a triple-quoted string", "«a\"b\"\"c»",
            null, Lex(Text("a\"b\"\"c")), "f\"\"\"a\"b\"\"c\"\"\" → 'a\"b\"\"c'",
            NotApplicable: "a lone quote ends a single-quoted string — that close is every other arm's »"),
        new("text: a line break", "«a\n⇥b»",
            Refuse(SPY0002), Lex(Text("a\nb")), "f\"a⏎b\" → SyntaxError: unterminated; f\"\"\"a⏎b\"\"\" → 'a\\nb'"),
        new("text: CRLF reads as \\n", "«a\r\n⇥b»",
            Refuse(SPY0002), Lex(Text("a\nb")), "f\"\"\"a\\r\\nb\"\"\" → 'a\\nb'"),
        new("text: a bare CR reads as \\n", "«a\r⇥b»",
            Refuse(SPY0002), Lex(Text("a\nb")), "f\"\"\"a\\rb\"\"\" → 'a\\nb'"),
        new("text: text after a hole continues across a line", "«a {x}\n⇥b»",
            Refuse(SPY0002), Lex(Text("a "), Es, Id("x"), Ee, Text("\nb")), "f\"\"\"a {x}⏎b\"\"\" → 'a 5\\nb', MIDDLE ['a ', '\\nb']"),

        // ── replacement field: '=' and '!' ────────────────────────────────────────────────────
        new("field: '=' at the top level is self-doc", "«{x=}»",
            Lex(Es, Id("x"), T(TokenType.FStringSelfDoc, "x="), Ee), AsSingle, "f\"{x=}\" → 'x=5'"),
        new("field: self-doc keeps the whitespace around '='", "«{x = }»",
            Lex(Es, Id("x"), T(TokenType.FStringSelfDoc, "x = "), Ee), AsSingle, "f\"{x = }\" → 'x = 5'"),
        new("field: '=' inside () is a keyword argument", "«{f(a=1)}»",
            Lex(Es, Id("f"), T(TokenType.LeftParen, "("), Id("a"), T(TokenType.Assign, "="), T(TokenType.Integer, "1"), T(TokenType.RightParen, ")"), Ee), AsSingle,
            "f\"{f(a=1)}\" (f=dict) → \"{'a': 1}\""),
        new("field: '==' is not self-doc", "«{a==b}»",
            Lex(Es, Id("a"), T(TokenType.Equal, "=="), Id("b"), Ee), AsSingle, "f\"{a==b}\" → 'True'"),
        new("field: !r", "«{x!r}»",
            Lex(Es, Id("x"), T(TokenType.FStringConversion, "r"), Ee), AsSingle, "f\"{x!r}\" → '5'"),
        new("field: !s followed by a spec", "«{x!s:>4}»",
            Lex(Es, Id("x"), T(TokenType.FStringConversion, "s"), Spec(">4"), Ee), AsSingle, "f\"{x!s:>4}\" → '   5', MIDDLE ['>4']"),
        new("field: !a", "«{x!a}»",
            Lex(Es, Id("x"), T(TokenType.FStringConversion, "a"), Ee), AsSingle, "f\"{x!a}\" → '5'"),
        new("field: '!=' is an operator", "«{a!=b}»",
            Lex(Es, Id("a"), T(TokenType.NotEqual, "!="), Id("b"), Ee), AsSingle, "f\"{a!=b}\" → 'False'"),
        new("field: an invalid conversion !q", "«{x!q}»",
            Refuse(SPY0030), AsSingle, "f\"{x!q}\" → SyntaxError: f-string: invalid conversion character 'q'"),
        new("field: a valid conversion at EOF", "«{x!r",
            Refuse(SPY0020), AsSingle, "f\"{x!r → SyntaxError: '{' was never closed"),
        new("field: a valid conversion followed by neither ':' nor '}'", "«{x!r x}»",
            Refuse(SPY0020), AsSingle, "f\"{x!r x}\" → SyntaxError: f-string: expecting ':' or '}'"),

        // ── format spec ───────────────────────────────────────────────────────────────────────
        new("spec: ':' starts a spec", "«{x:>4}»",
            Lex(Es, Id("x"), Spec(">4"), Ee), AsSingle, "f\"{x:>4}\" → '   5', MIDDLE ['>4']"),
        new("spec: an empty spec", "«{x:}»",
            Lex(Es, Id("x"), Spec(""), Ee), AsSingle, "f\"{x:}\" → '5', MIDDLE ['']"),
        new("spec: a nested field", "«{x:{w}}»",
            Lex(Es, Id("x"), Spec(""), Es, Id("w"), Ee, Ee), AsSingle, "f\"{x:{w}}\" → '   5'"),
        new("spec: '}}}' closes three fields — no '}}' escape in a spec (#1884)", "«{x:{y:{z}}}»",
            Lex(Es, Id("x"), Spec(""), Es, Id("y"), Spec(""), Es, Id("z"), Ee, Ee, Ee), AsSingle,
            "f\"{x:{y:{z}}}\" → '   5' (y=4, z='d'), MIDDLE ['', '']"),
        new("spec: a backslash-escaped quote is spec text", "«{x:\\\">4}»",
            Lex(Es, Id("x"), Spec("\\\">4"), Ee), AsSingle, "f\"{x:\\\">4}\" → '\"\"\"5', MIDDLE ['\\\\\">4'] (single and triple)"),
        new("spec: the enclosing quote ends the string inside a spec", "«{x:>4§",
            Refuse(SPY0022), AsSingle, "f\"{x:>4\" → SyntaxError: f-string: expecting '}', or format specs"),
        new("spec: a line break in a spec is spec text", "«{x:>4\n}»",
            null, Lex(Es, Id("x"), Spec(">4\n"), Ee), "f\"\"\"{x:>4⏎}\"\"\" → MIDDLE ['>4\\n']",
            NotApplicable: "single-quoted: the lexer keeps the \\n, CPython 3.12's FSTRING_MIDDLE drops it and 3.13 refuses it — a findings-ledger line, not pinned"),
        new("spec: EOF inside a spec", "«{x:>4",
            Refuse(SPY0022), AsSingle, "f\"{x:>4 → SyntaxError: unterminated f-string literal"),

        // ── a string literal inside a field: every prefix of the one dispatch (#2010) ─────────
        new("field: a nested plain string", "«{'q'}»",
            Lex(Es, T(TokenType.String, "q"), Ee), AsSingle, "f\"{'q'}\" → 'q'", NestedPrefix: ""),
        new("field: a nested r-string", "«{r'\\d'}»",
            Lex(Es, T(TokenType.RawString, "\\d"), Ee), AsSingle, "f\"{r'\\d'}\" → '\\\\d'", NestedPrefix: "r"),
        new("field: a nested b-string", "«{b'ab'}»",
            Lex(Es, T(TokenType.ByteString, "ab"), Ee), AsSingle, "f\"{b'ab'}\" → \"b'ab'\"", NestedPrefix: "b"),
        new("field: a nested d-string", "«{d'q'}»",
            Lex(Es, T(TokenType.String, "q"), Ee), AsSingle, "no d prefix in CPython 3.12; dstrings.md: d'…' is a plain string", NestedPrefix: "d"),
        new("field: a nested dr-string", "«{dr'q'}»",
            Lex(Es, T(TokenType.RawString, "q"), Ee), AsSingle, "no d prefix in CPython 3.12; dstrings.md: dr'…' is a raw string", NestedPrefix: "dr"),
        new("field: a nested f-string", "«{f'{s}'}»",
            Lex(Es, T(TokenType.FStringStart, "f'"), Es, Id("s"), Ee, T(TokenType.FStringEnd, "'"), Ee), AsSingle,
            "f\"{f'{s}'}\" → 's'", NestedPrefix: "f"),
        new("field: a nested t-string", "«{t'{s}'}»",
            Lex(Es, T(TokenType.FStringStart, "t'"), Es, Id("s"), Ee, T(TokenType.FStringEnd, "'"), Ee), AsSingle,
            "no t prefix in CPython 3.12; fstrings.md F-String Nesting Rules (executed)", NestedPrefix: "t"),
        new("field: a nested df-string", "«{df'{s}'}»",
            Lex(Es, T(TokenType.FStringStart, "df'"), Es, Id("s"), Ee, T(TokenType.FStringEnd, "'"), Ee), AsSingle,
            "no d prefix in CPython 3.12; fstrings.md F-String Nesting Rules", NestedPrefix: "df"),
        new("field: a nested literal reusing the enclosing quote (PEP 701)", "«{§q§}»",
            Lex(Es, T(TokenType.String, "q"), Ee), AsSingle, "f\"{\"q\"}\" → 'q'; f\"\"\"{\"\"\"q\"\"\"}\"\"\" → 'q'"),
        new("field: an unterminated literal opening with the enclosing quote is the missing '}'", "«{x§",
            Refuse(SPY0020), AsSingle, "f\"{x\" and f\"\"\"{x\"\"\" → SyntaxError: f-string: expecting '}'"),

        // ── other expression tokens in a field ───────────────────────────────────────────────
        new("field: a backtick name", "«{`x`}»",
            Lex(Es, Id("x"), Ee), AsSingle, "Sharpy-only (identifiers.md literal names); CPython refuses the backtick"),
        new("field: a number", "«{42}»",
            Lex(Es, T(TokenType.Integer, "42"), Ee), AsSingle, "f\"{42}\" → '42'"),
        new("field: ':' inside [] is not a spec", "«{xs[0:2]}»",
            Lex(Es, Id("xs"), T(TokenType.LeftBracket, "["), T(TokenType.Integer, "0"), T(TokenType.Colon, ":"), T(TokenType.Integer, "2"), T(TokenType.RightBracket, "]"), Ee), AsSingle,
            "f\"{xs[0:2]}\" → '[1, 2]'"),
        new("field: set/dict braces inside a field are tracked", "«{ {1} }»",
            Lex(Es, T(TokenType.LeftBrace, "{"), T(TokenType.Integer, "1"), T(TokenType.RightBrace, "}"), Ee), AsSingle, "f\"{ {1} }\" → '{1}'"),
        new("field: a ')' with nothing open", "«{x)}»",
            Refuse(SPY0021), AsSingle, "f\"{x)}\" → SyntaxError: f-string: unmatched ')'"),
        new("field: a ']' with nothing open", "«{x]}»",
            Refuse(SPY0021), AsSingle, "f\"{x]}\" → SyntaxError: f-string: unmatched ']'"),

        // ── hole trivia (PEP 701, #2022) ─────────────────────────────────────────────────────
        new("hole trivia: a comment", "«{x # c\n⇥}»",
            Lex(Es, Id("x"), Ee), AsSingle, "f\"{x # c⏎}\" and the triple twin → '5'"),
        new("hole trivia: line breaks and spaces", "«{ x\n⇥ }»",
            Lex(Es, Id("x"), Ee), AsSingle, "f\"{ x⏎ }\" → '5'"),
        new("hole trivia: a tab", "«{\tx}»",
            Lex(Es, Id("x"), Ee), AsSingle, "f\"{\\tx}\" → '5'"),

        // ── end of file ──────────────────────────────────────────────────────────────────────
        new("EOF: inside the text", "«abc",
            Refuse(SPY0002), AsSingle, "f\"abc → SyntaxError: unterminated f-string literal"),
        new("EOF: inside an open field", "«{x",
            Refuse(SPY0020), AsSingle, "f\"{x → SyntaxError: '{' was never closed"),

        // ── PEP 822 dedent (Raw: the literal is written whole; f/t keep the indentation) ─────
        new("dedent: the close line's indent is stripped, deeper indent kept", "«\n    a\n      b\n    »",
            null, Lex(Text("\n    a\n      b\n    ")), "f\"\"\"⏎    a⏎      b⏎    \"\"\" → '\\n    a\\n      b\\n    '",
            DfTriple: Lex(Text("a\n  b")), Raw: true, NotApplicable: NoLineBreakInSingle),
        new("dedent: a tab counts as one character", "«\n\ta\n\t»",
            null, Lex(Text("\n\ta\n\t")), "f\"\"\"⏎\\ta⏎\\t\"\"\" → '\\n\\ta\\n\\t'",
            DfTriple: Lex(Text("a")), Raw: true, NotApplicable: NoLineBreakInSingle),
        new("dedent: a blank line before the close is a trailing newline", "«\n    a\n\n    »",
            null, Lex(Text("\n    a\n\n    ")), "f\"\"\"⏎    a⏎⏎    \"\"\" → '\\n    a\\n\\n    '",
            DfTriple: Lex(Text("a\n")), Raw: true, NotApplicable: NoLineBreakInSingle),
        new("dedent: CRLF line breaks", "«\r\n    a\r\n    »",
            null, Lex(Text("\n    a\n    ")), "f\"\"\"\\r\\n    a\\r\\n    \"\"\" → '\\n    a\\n    '",
            DfTriple: Lex(Text("a")), Raw: true, NotApplicable: NoLineBreakInSingle),
        new("dedent: the pre-close newline after a hole is swallowed", "«\n    {x}\n    »",
            null, Lex(Text("\n    "), Es, Id("x"), Ee, Text("\n    ")), "f\"\"\"⏎    {x}⏎    \"\"\" → '\\n    5\\n    '",
            DfTriple: Lex(Es, Id("x"), Ee), Raw: true, NotApplicable: NoLineBreakInSingle),
    };

    private static Outcome Lex(params (TokenType, string)[] inner) => Outcome.Lex(inner);
    private static Outcome Refuse(string code) => Outcome.Refuse(code);

    private static Outcome? TripleOf(Arm arm) => ReferenceEquals(arm.Triple, AsSingle) ? arm.Single : arm.Triple;

    private static Outcome? OutcomeOf(Arm arm, string prefix, bool triple)
    {
        if (!triple)
            return arm.Single;
        return prefix == "df" && arm.DfTriple != null ? arm.DfTriple : TripleOf(arm);
    }

    private static string SourceOf(Arm arm, string prefix, bool triple)
    {
        var quote = triple ? "\"\"\"" : "\"";
        var dedented = !arm.Raw && triple && prefix == "df";
        var open = prefix + quote + (dedented ? "\n    " : "");
        var close = (dedented ? "\n    " : "") + quote;
        return arm.Body
            .Replace("«", open, StringComparison.Ordinal)
            .Replace("»", close, StringComparison.Ordinal)
            .Replace("§", quote, StringComparison.Ordinal)
            .Replace("⇥", dedented ? "    " : "", StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> Cells() =>
        from arm in Arms
        from prefix in Prefixes
        from triple in Quotings
        where OutcomeOf(arm, prefix, triple) != null
        select new object[] { arm.Name, prefix, triple ? "triple" : "single" };

    [Theory]
    [MemberData(nameof(Cells))]
    public void Arm_LexesAsTheSpecAndCPythonSay(string armName, string prefix, string quoting)
    {
        var arm = Arms.Single(a => a.Name == armName);
        var triple = quoting == "triple";
        var source = SourceOf(arm, prefix, triple);
        var outcome = OutcomeOf(arm, prefix, triple)!;

        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var errors = lexer.Diagnostics.GetErrors();
        var codes = errors.Select(d => d.Code).ToList();
        var actual = tokens.Select(t => (t.Type, t.Value)).ToList();
        var because = $"arm '{arm.Name}' × {prefix} × {quoting}\nsource: {Show(source)}\ntokens: {Show(actual)}\nerrors: [{string.Join(", ", errors.Select(e => $"{e.Code}@{e.Line}:{e.Column}"))}]\npython3: {arm.Python}";

        if (outcome.Code != null)
        {
            // The FIRST diagnostic is the arm's refusal (CPython stops there), on the line the body
            // starts on — so a recovery code cannot stand in for it. Later codes are recovery resuming
            // on the next line and reading the rest as code (`b"` → SPY0026, `    """` → SPY0001;
            // findings ledger, "lexer recovery cascade after a line-break refusal").
            var bodyLine = !arm.Raw && triple && prefix == "df" ? 2 : 1;
            Assert.True(errors.Count > 0 && errors[0].Code == outcome.Code && errors[0].Line == bodyLine,
                $"expected the first diagnostic to be {outcome.Code} on line {bodyLine}\n{because}");
            return;
        }

        Assert.True(codes.Count == 0, $"expected no diagnostics\n{because}");
        var quote = triple ? "\"\"\"" : "\"";
        var expected = new List<(TokenType, string)> { (TokenType.FStringStart, prefix + quote) };
        expected.AddRange(outcome.Inner!);
        expected.Add((TokenType.FStringEnd, quote));
        expected.Add((TokenType.Eof, ""));
        Assert.True(actual.SequenceEqual(expected), $"expected tokens: {Show(expected)}\n{because}");
    }

    [Fact]
    public void Roster_EveryArmHasACellAndEveryGapItsReason()
    {
        Arms.Select(a => a.Name).Should().OnlyHaveUniqueItems();
        foreach (var arm in Arms)
        {
            var applicable = (from p in Prefixes from q in Quotings where OutcomeOf(arm, p, q) != null select 1).Count();
            Assert.True(applicable > 0, $"arm '{arm.Name}' must run in at least one cell");
            var full = applicable == Prefixes.Length * Quotings.Length;
            Assert.True((arm.NotApplicable != null) == !full,
                $"arm '{arm.Name}' runs in {applicable} of 6 cells — a gap needs a reason, a full row must not carry one");
            Assert.False(string.IsNullOrWhiteSpace(arm.Python), $"arm '{arm.Name}' records its python3 probe or why there is none");
        }

        var cells = Cells().Count();
        _output.WriteLine($"FSTRING-ARMS arms {Arms.Length} cells {cells}");
    }

    /// <summary>
    /// PEP 822 refusal/normalization arms, d-string side: dstrings.md step 6 and Errors (SPY0029 for a
    /// shallow content line and for a non-whitespace close line) and steps 4–5 (the empty first line
    /// and the close line are dropped for ANY strip amount, zero included). Live — the control the
    /// skipped df twins below must match.
    /// </summary>
    [Theory]
    [InlineData("d\"\"\"\n    a\n  b\n    \"\"\"", "SPY0029", null)]
    [InlineData("d\"\"\"\n    a\n    b\"\"\"", "SPY0029", null)]
    [InlineData("d\"\"\"\nabc\n\"\"\"", null, "abc")]
    public void DedentRule_DString_IsTheSpecControl(string source, string? code, string? value)
    {
        var (codes, tokens) = LexAll(source);
        if (code != null)
        {
            Assert.True(codes.SequenceEqual(new[] { code }), $"expected exactly [{code}] for {Show(source)}; got [{string.Join(", ", codes)}]");
            return;
        }
        Assert.True(codes.Count == 0, $"expected no diagnostics for {Show(source)}; got [{string.Join(", ", codes)}]");
        var expected = new List<(TokenType, string)> { (TokenType.String, value!), (TokenType.Eof, "") };
        Assert.True(tokens.SequenceEqual(expected), $"expected {Show(expected)} for {Show(source)}; got {Show(tokens)}");
    }

    [Fact(Skip = "#2337 df skips the SPY0029 dedent refusals that d applies")]
    public void DedentRule_DfString_ShallowContentLine_IsSpy0029()
    {
        var (codes, _) = LexAll("df\"\"\"\n    a {x}\n  b\n    \"\"\"");
        Assert.True(codes.SequenceEqual(new[] { DiagnosticCodes.Lexer.DedentedStringIndentationError }), $"got [{string.Join(", ", codes)}]");
    }

    [Fact(Skip = "#2337 df skips the SPY0029 dedent refusals that d applies")]
    public void DedentRule_DfString_NonWhitespaceCloseLine_IsSpy0029()
    {
        var (codes, _) = LexAll("df\"\"\"\n    a {x}\n    b\"\"\"");
        Assert.True(codes.SequenceEqual(new[] { DiagnosticCodes.Lexer.DedentedStringIndentationError }), $"got [{string.Join(", ", codes)}]");
    }

    [Fact(Skip = "#2337 df keeps the first and pre-close newlines when the close has zero indent; d drops them")]
    public void DedentRule_DfString_ZeroIndentClose_DropsTheFirstAndPreCloseNewlines()
    {
        var (codes, tokens) = LexAll("df\"\"\"\nabc\n\"\"\"");
        Assert.True(codes.Count == 0, $"got [{string.Join(", ", codes)}]");
        var expected = new List<(TokenType, string)>
        {
            (TokenType.FStringStart, "df\"\"\""), (TokenType.FStringText, "abc"), (TokenType.FStringEnd, "\"\"\""), (TokenType.Eof, ""),
        };
        Assert.True(tokens.SequenceEqual(expected), $"expected {Show(expected)}; got {Show(tokens)}");
    }

    private static (List<string> Codes, List<(TokenType Type, string Value)> Tokens) LexAll(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        return (lexer.Diagnostics.GetErrors().Select(d => d.Code ?? "").ToList(), tokens.Select(t => (t.Type, t.Value)).ToList());
    }

    /// <summary>
    /// The nested-literal arms cover the lexer's ONE prefix dispatch (#2010) — read from the
    /// lexer's table as the positive control that the literal arm list is complete, never as the
    /// roster itself.
    /// </summary>
    [Fact]
    public void NestedLiteralArms_CoverEveryStringPrefix()
    {
        Arms.Where(a => a.NestedPrefix != null).Select(a => a.NestedPrefix)
            .Should().BeEquivalentTo(LexerNs.Lexer.StringLiteralPrefixes,
                "every string prefix the lexer dispatches must have a nested-in-a-field arm");
    }

    private static string Show(string source) =>
        source.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    private static string Show(IEnumerable<(TokenType Type, string Value)> tokens) =>
        string.Join(" ", tokens.Select(t => $"{t.Type}'{Show(t.Value)}'"));
}
