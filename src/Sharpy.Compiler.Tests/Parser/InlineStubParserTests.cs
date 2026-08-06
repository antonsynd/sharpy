using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Shared;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// The inline stub position — <c>def f(): ...</c>, <c>property get p(self) -> int: ...</c>,
/// <c>event add e(self, h: H): ...</c> — was the last place where <c>(...)</c> was not the same
/// stub as <c>...</c> (#1238). Three independently written
/// <c>Current.Type == TokenType.Ellipsis</c> guards sat *before* the <c>AstHelper</c> authority
/// could see anything, because the parser never built a body to classify. They are now one helper
/// that parses the inline body and asks <see cref="AstHelper.TryGetEllipsisStub"/>.
///
/// <para>Two obligations are pinned here. The first is the fix: every grouped spelling parses to a
/// structurally identical declaration. The second is that the fix widened nothing else — a
/// non-stub inline body still produces the exact SPY0102 it produced before the fold, message and
/// position included. Every such cell was measured on <c>dev</c> @ <c>6eeb329f2</c> before the
/// change and is reproduced verbatim below.</para>
/// </summary>
public class InlineStubParserTests
{
    private static Module ParseClean(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            $"`{source.TrimEnd('\n')}` must parse cleanly; got: "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    private static IReadOnlyList<CompilerDiagnostic> ParseErrors(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        parser.ParseModule();
        return parser.Diagnostics.GetErrors().ToList();
    }

    /// <summary>
    /// Structural equality modulo grouping and source positions — the same normalize-then-compare
    /// the unparser round-trip tests use. Grouping is exactly what #1214 declared transparent, so
    /// stripping it is the contract under test, not a weakening of it.
    /// </summary>
    private static bool SameShape(Module a, Module b)
        => StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(a),
            AstNormalizer.Instance.NormalizeModule(b));

    // --- The fix: grouped spellings are the same declaration -----------------------------------

    /// <summary>
    /// Function, property and event inline stubs, each in the bare and grouped spellings. The
    /// spellings differ as text and must not differ as trees.
    /// </summary>
    public static TheoryData<string, string, string> GroupedSpellingPairs => new()
    {
        {
            "function",
            "def f() -> str: ...\n",
            "def f() -> str: (...)\n"
        },
        {
            "property",
            "interface I:\n    property get x(self) -> int: ...\n",
            "interface I:\n    property get x(self) -> int: (...)\n"
        },
        {
            "event",
            "delegate H() -> None\n\ninterface I:\n    event add on_a(self, handler: H): ...\n    event remove on_a(self, handler: H): ...\n",
            "delegate H() -> None\n\ninterface I:\n    event add on_a(self, handler: H): (...)\n    event remove on_a(self, handler: H): (...)\n"
        },
    };

    [Theory]
    [MemberData(nameof(GroupedSpellingPairs))]
    public void GroupedInlineStub_ParsesToTheSameDeclarationAsTheBareSpelling(
        string form, string bare, string grouped)
    {
        // Arrangement guard: an equality assertion between two identical strings would pass
        // vacuously. These must actually be different source text.
        grouped.Should().NotBe(bare, $"the {form} pair must exercise two distinct spellings");

        var bareModule = ParseClean(bare);
        var groupedModule = ParseClean(grouped);

        SameShape(bareModule, groupedModule).Should().BeTrue(
            $"`(...)` is the same stub as `...` at every other seam since #1214; the inline {form} "
            + $"position was the last exception (#1238).\nbare:\n{bare}\ngrouped:\n{grouped}");
    }

    /// <summary>
    /// Negative control for the theory above: the comparer must reject a genuinely different body,
    /// or "the trees are equal" would mean nothing.
    /// </summary>
    [Fact]
    public void SameShape_RejectsADifferentBody()
    {
        SameShape(
            ParseClean("def f() -> str: ...\n"),
            ParseClean("def f() -> str:\n    return \"x\"\n"))
            .Should().BeFalse("a real body is not a stub; the comparer must be able to tell");
    }

    /// <summary>
    /// The declaration bodies reach the authority as stubs — the property the emitter and every
    /// validator downstream actually read.
    /// </summary>
    [Theory]
    [InlineData("def f() -> str: ...\n")]
    [InlineData("def f() -> str: (...)\n")]
    [InlineData("def f() -> str: ((...))\n")]
    public void InlineFunctionStub_IsAStubToTheAuthority(string source)
    {
        var fn = ParseClean(source).Body.OfType<FunctionDef>().Single();

        AstHelper.IsEllipsisStubBody(fn.Body).Should().BeTrue(
            "the inline position must classify through the same authority as every block position");
        AstHelper.TryGetEllipsisStub(fn.Body.Single(), out var ellipsis).Should().BeTrue();
        ellipsis!.Span.Should().NotBeNull(
            "a user-written `...` keeps its span; BodylessSyntaxValidator tells it from a "
            + "parser-synthesized stub (Span is null) that way");
    }

    /// <summary>
    /// Grouping is transparent, so it stays transparent across implicit line joining too — the
    /// group may span lines exactly as it may anywhere else. Deliberate: this is the one input the
    /// fold newly accepts beyond the single-line grouped spellings.
    /// </summary>
    [Fact]
    public void InlineStub_AcceptsAGroupSpanningLines()
    {
        SameShape(
            ParseClean("def f() -> str: (\n    ...\n)\n"),
            ParseClean("def f() -> str: ...\n"))
            .Should().BeTrue();
    }

    /// <summary>The parenthesized inline stub survives an unparse/reparse cycle.</summary>
    [Fact]
    public void GroupedInlineStub_RoundTripsThroughTheUnparser()
    {
        var module = ParseClean("def f() -> str: (...)\n");
        var reparsed = ParseClean(Unparser.Unparse(module));

        SameShape(module, reparsed).Should().BeTrue(
            "unparsing an inline stub must reparse to the same declaration; got:\n"
            + Unparser.Unparse(module));
    }

    // --- The floor: nothing else about the inline position moved -------------------------------

    /// <summary>
    /// Every non-stub inline body, with the diagnostic each produced on <c>dev</c> @
    /// <c>6eeb329f2</c> — before the three token guards were folded into one classifying helper.
    /// The fold parses ahead only when an inline body could begin at all, and rewinds when what it
    /// parsed is not a stub, so each of these still reports at the first token after the <c>:</c>.
    /// </summary>
    public static TheoryData<string, string, string, int, int> NonStubInlineBodies => new()
    {
        // source                                          message tail          line col
        { "1 + 1",       "def f(): 1 + 1\n",               "got Integer",         1, 10 },
        { "pass",        "def f() -> str: pass\n",         "got Pass",            1, 17 },
        { "return",      "def f() -> int: return 1\n",     "got Return",          1, 17 },
        { "call",        "def f(): g()\n",                 "got Identifier",      1, 10 },
        { "group",       "def f() -> str: (1 + 2)\n",      "got LeftParen",       1, 17 },
        { "empty group", "def f() -> str: ()\n",           "got LeftParen",       1, 17 },
        { "two groups",  "def f() -> str: (...) (...)\n",  "got LeftParen",       1, 17 },
        { "stray close", "def f() -> str: ...)\n",         "got RightParen",      1, 20 },
        {
            "property return",
            "class C:\n    property get x(self) -> int: return 1\n",
            "got Return", 2, 34
        },
        {
            "property expression",
            "class C:\n    property get x(self) -> int: 1 + 1\n",
            "got Integer", 2, 34
        },
        {
            "event call",
            "delegate H() -> None\n\nclass C:\n    event add on_a(self, handler: H): print(\"x\")\n",
            "got Identifier", 4, 39
        },
    };

    [Theory]
    [MemberData(nameof(NonStubInlineBodies))]
    public void NonStubInlineBody_KeepsItsPreFoldDiagnostic(
        string label, string source, string messageTail, int line, int column)
    {
        var errors = ParseErrors(source);

        // Arrangement guard: an "unchanged diagnostic" claim is worthless if no diagnostic was
        // produced at all.
        errors.Should().NotBeEmpty($"`{label}` must still be rejected");

        var first = errors[0];
        first.Code.Should().Be(DiagnosticCodes.Parser.ExpectedNewline);
        first.Message.Should().Be($"Expected newline, {messageTail}");
        (first.Line, first.Column).Should().Be((line, column),
            $"`{label}` reported at {line}:{column} before the fast paths were folded together");
    }

    /// <summary>
    /// Sibling audit (#1238 asked for it explicitly): there is no inline <c>class C: ...</c> form
    /// to route through the helper — a class body has always required an indented block, in either
    /// spelling. Pinned rather than asserted from a grep, so the answer stays true.
    /// </summary>
    [Theory]
    [InlineData("class C: ...\n", "got Ellipsis")]
    [InlineData("class C: (...)\n", "got LeftParen")]
    [InlineData("struct S: ...\n", "got Ellipsis")]
    [InlineData("interface I: ...\n", "got Ellipsis")]
    public void TypeDeclarations_HaveNoInlineBodyForm(string source, string messageTail)
    {
        var errors = ParseErrors(source);

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be(DiagnosticCodes.Parser.ExpectedNewline);
        errors[0].Message.Should().Be($"Expected newline, {messageTail}",
            "neither spelling is accepted inline, so there is no third fast path to fold in");
    }

    /// <summary>
    /// The one measured change: an unterminated group after the <c>:</c> used to report
    /// "Expected newline, got LeftParen" (the guard never looked inside) and now reports the
    /// missing <c>)</c>. It was an error before and is an error now, and the new diagnostic is the
    /// same one the identical malformed group produces in statement position — the inline position
    /// stopped being special here too.
    /// </summary>
    [Theory]
    [InlineData("def f() -> str: (\n", "x = (\n")]
    [InlineData("def f() -> str: (...\n", "x = (...\n")]
    public void UnterminatedGroup_ReportsWhatStatementPositionReports(string inline, string statement)
    {
        var inlineErrors = ParseErrors(inline);
        var statementErrors = ParseErrors(statement);

        inlineErrors.Should().NotBeEmpty("an unterminated group is still rejected");
        statementErrors.Should().NotBeEmpty("the statement-position control must also be rejected");

        (inlineErrors[0].Code, inlineErrors[0].Message)
            .Should().Be((statementErrors[0].Code, statementErrors[0].Message));
    }

    // --- Regression floor for the bare spelling ------------------------------------------------

    /// <summary>
    /// The bare `...` spelling keeps its exact node positions. The fold replaced hand-built
    /// span/position arithmetic with the ordinary expression parse, and 52 fixture ASTs came back
    /// byte-identical through <c>emit ast</c>; this pins the arithmetic itself.
    /// </summary>
    [Fact]
    public void BareInlineStub_KeepsItsNodePositions()
    {
        var fn = ParseClean("def f() -> str: ...\n").Body.OfType<FunctionDef>().Single();
        var stmt = fn.Body.Single();

        (stmt.LineStart, stmt.ColumnStart, stmt.LineEnd, stmt.ColumnEnd).Should().Be((1, 17, 1, 20));

        var ellipsis = ((ExpressionStatement)stmt).Expression;
        (ellipsis.LineStart, ellipsis.ColumnStart, ellipsis.LineEnd, ellipsis.ColumnEnd)
            .Should().Be((1, 17, 1, 20));
    }

    /// <summary>
    /// Inline stubs are equivalent to their block spelling — the fold must not have made the
    /// inline position mean something else.
    /// </summary>
    [Theory]
    [InlineData("def f() -> str: ...\n")]
    [InlineData("def f() -> str: (...)\n")]
    public void InlineStub_MatchesTheBlockSpelling(string inline)
    {
        SameShape(ParseClean(inline), ParseClean("def f() -> str:\n    ...\n"))
            .Should().BeTrue();
    }

    /// <summary>
    /// Immutability sanity: the helper hands back a single-statement body, not an empty or
    /// multi-statement one, at all three declaration forms.
    /// </summary>
    [Fact]
    public void InlineStub_ProducesASingleStatementBody()
    {
        var fn = ParseClean("def f() -> str: (...)\n").Body.OfType<FunctionDef>().Single();
        fn.Body.Should().ContainSingle();

        var iface = ParseClean("interface I:\n    property get x(self) -> int: (...)\n")
            .Body.OfType<InterfaceDef>().Single();
        var prop = iface.Body.OfType<PropertyDef>().Single();
        prop.IsFunctionStyle.Should().BeTrue();
        prop.Body.Should().ContainSingle();

        var evIface = ParseClean(
            "delegate H() -> None\n\ninterface I:\n"
            + "    event add on_a(self, handler: H): (...)\n"
            + "    event remove on_a(self, handler: H): (...)\n")
            .Body.OfType<InterfaceDef>().Single();
        foreach (var ev in evIface.Body.OfType<EventDef>())
        {
            ev.IsFunctionStyle.Should().BeTrue();
            ev.Body.Should().ContainSingle();
            AstHelper.IsEllipsisStubBody(ev.Body).Should().BeTrue();
        }
    }
}
