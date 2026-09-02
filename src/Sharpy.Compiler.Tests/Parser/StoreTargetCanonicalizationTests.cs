using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Pins the PARSER seam of the #1170 canonical-form contract for store targets: redundant
/// parentheses around a binding name are stripped once, in the parser
/// (<c>AstHelper.CanonicalizeStoreTarget</c>), so no semantic, codegen, or LSP consumer can
/// ever see a <see cref="Parenthesized"/> in a target position.
///
/// Every binding route below was measured @ 277f54543 (verify round of plan-950124): the
/// for / with / comprehension / nested-tuple / bare-tuple / starred / annotated spellings were
/// refused (SPY0200 "Undefined identifier", SPY0107, SPY0239) while <c>(a) = 1</c> compiled —
/// the one-arm shape the verification contract §1 forbids. python3 3.12 accepts every one.
///
/// Positive control: a parenthesized VALUE keeps its wrapper. Refusal controls: the python3
/// syntax errors <c>except E as (e)</c> and <c>((a) := 1)</c> stay parse errors, and
/// <c>(*a), b = xs</c> ("cannot use starred expression here") parses to a shape the checker's
/// target authority still refuses (pinned by the <c>.error</c> fixture).
///
/// mutation (verify round 2026-09-02): <c>ParseStoreTarget</c>'s <c>canonicalize ? … : expr</c>
/// replaced by <c>expr</c> → the for / with / comprehension cells red, the assignment cells
/// still green (they canonicalize at their own constructor); restored → green.
/// </summary>
public class StoreTargetCanonicalizationTests
{
    private static (Module Module, ParserNs.Parser Parser) Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        return (module, parser);
    }

    private static Module ParseClean(string source)
    {
        var (module, parser) = Parse(source);
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "the spelling is valid python3 and must parse cleanly: "
            + string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    private static bool ContainsParenthesized(Node node)
        => node is Parenthesized || node.GetChildNodes().Any(ContainsParenthesized);

    // --- Assignment routes (Assignment constructor seam) ---

    [Theory]
    [InlineData("(a) = 1\n")]
    [InlineData("((a)) = 1\n")]
    [InlineData("(a) += 1\n")]
    [InlineData("((a), b) = (1, 2)\n")]
    [InlineData("(a, (b)) = (1, 2)\n")]
    [InlineData("(c), d = 1, 2\n")]
    [InlineData("*(h), i = [1, 2, 3]\n")]
    public void AssignmentTarget_IsCanonical(string source)
    {
        var module = ParseClean(source);
        var assign = module.Body.OfType<Assignment>().Single();
        ContainsParenthesized(assign.Target).Should().BeFalse(
            $"the target of `{source.Trim()}` must be canonical, got {assign.Target}");
    }

    [Fact]
    public void AnnotatedDeclaration_ParenthesizedName_IsTheName()
    {
        var module = ParseClean("(a): int = 1\n");
        var decl = module.Body.OfType<VariableDeclaration>().Single();
        decl.Name.Should().Be("a");
    }

    // --- Loop and with routes (ParseStoreTarget seam) ---

    [Theory]
    [InlineData("for (x) in xs:\n    pass\n")]
    [InlineData("for ((a), b) in xs:\n    pass\n")]
    [InlineData("for a, (b) in xs:\n    pass\n")]
    [InlineData("for *(rest), last in xs:\n    pass\n")]
    public void ForTarget_IsCanonical(string source)
    {
        var module = ParseClean(source);
        var forStmt = module.Body.OfType<ForStatement>().Single();
        ContainsParenthesized(forStmt.Target).Should().BeFalse(
            $"the target of `{source.Split('\n')[0]}` must be canonical, got {forStmt.Target}");
    }

    [Fact]
    public void WithTarget_IsCanonical()
    {
        var module = ParseClean("with cm as (t):\n    pass\n");
        var with = module.Body.OfType<WithStatement>().Single();
        with.Items[0].Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("t");
    }

    [Theory]
    [InlineData("y = [x for (x) in xs]\n")]
    [InlineData("y = [x for ((x), z) in xs]\n")]
    [InlineData("y = {k: v for (k, (v)) in xs}\n")]
    public void ComprehensionTarget_IsCanonical(string source)
    {
        var module = ParseClean(source);
        var assign = module.Body.OfType<Assignment>().Single();
        var clauses = assign.Value switch
        {
            ListComprehension lc => lc.Clauses,
            DictComprehension dc => dc.Clauses,
            _ => throw new Xunit.Sdk.XunitException($"unexpected value node {assign.Value.GetType().Name}"),
        };
        var forClause = clauses.OfType<ForClause>().Single();
        ContainsParenthesized(forClause.Target).Should().BeFalse(
            $"the comprehension target of `{source.Trim()}` must be canonical, got {forClause.Target}");
    }

    // --- Controls ---

    [Fact]
    public void ParenthesizedValue_KeepsItsWrapper()
    {
        // Positive control for the absence assertions above: canonicalization is target-only.
        var module = ParseClean("x = (a)\n");
        var assign = module.Body.OfType<Assignment>().Single();
        assign.Value.Should().BeOfType<Parenthesized>();
    }

    [Fact]
    public void ParenthesizedStar_ParsesAsNestedTuple_LeftForTheAuthorityToRefuse()
    {
        // python3: `(*a), b = xs` is a SyntaxError ("cannot use starred expression here").
        // The parser yields a one-element tuple holding a SpreadElement (the `*x` of a collection
        // display — measured @ 277f54543), not a Parenthesized, so canonicalization never touches
        // it and the checker's target authority refuses it (SPY0225 — fixture
        // assignment_parenthesized_star_target.error).
        var module = ParseClean("(*a), b = xs\n");
        var assign = module.Body.OfType<Assignment>().Single();
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        var inner = tuple.Elements[0].Should().BeOfType<TupleLiteral>().Subject;
        inner.Elements.Should().ContainSingle().Which.Should().BeOfType<SpreadElement>();
    }

    [Fact]
    public void ExceptAs_ParenthesizedName_StaysRefused()
    {
        // python3: `except E as (e)` is a SyntaxError — the one ParseStoreTarget site that
        // opts out of canonicalization.
        var (_, parser) = Parse("try:\n    pass\nexcept ValueError as (e):\n    pass\n");
        parser.Diagnostics.HasErrors.Should().BeTrue("`except … as (e)` is not valid python3");
        parser.Diagnostics.GetErrors().Should().Contain(d => d.Message.Contains("simple name"));
    }

    [Fact]
    public void WalrusTarget_ParenthesizedName_StaysRefused()
    {
        // python3: `((a) := 1)` is a SyntaxError; the walrus parse site never canonicalizes.
        var (_, parser) = Parse("if ((a) := 1) > 0:\n    pass\n");
        parser.Diagnostics.HasErrors.Should().BeTrue("`((a) := 1)` is not valid python3");
        parser.Diagnostics.GetErrors().Should().Contain(d => d.Message.Contains("Walrus"));
    }
}
