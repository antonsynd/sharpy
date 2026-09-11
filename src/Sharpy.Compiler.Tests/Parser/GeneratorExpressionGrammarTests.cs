using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// The generator-expression GRAMMAR (#1774): which spellings produce a
/// <see cref="GeneratorExpression"/> node, and which are refused by name.
///
/// <para><b>Contract under test:</b> a generator expression is legal (a) parenthesized wherever a
/// parenthesized expression is legal, and (b) bare as the SOLE argument of a call — Python's rule.
/// Every other bare spelling is SPY0147 with the parenthesize steer, and the refusal does not depend
/// on WHICH argument arm of the call the expression took: the positional arm and the keyword-VALUE
/// arm are two spellings of the same rule, and the keyword arm used to fall through to the generic
/// SPY0104 "Expected RightParen, got For".</para>
///
/// <para>These are PARSE assertions, so they are independent of typing: the node's presence or the
/// diagnostic's code is the whole answer. The executing counterparts live in
/// <c>Integration/TestFixtures/generators/genexp_*</c>.</para>
/// </summary>
public class GeneratorExpressionGrammarTests
{
    private static (Module Module, IReadOnlyList<CompilerDiagnostic> Errors) Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var parser = new ParserNs.Parser(lexer.TokenizeAll());
        var module = parser.ParseModule();
        return (module, parser.Diagnostics.GetErrors().ToList());
    }

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildNodes())
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static int CountGenerators(Module module)
        => Walk(module).OfType<GeneratorExpression>().Count();

    // ── accepted spellings ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Column: the spelling · how many generator nodes it must produce. Each row is a POSITION, and
    /// together they are the "wherever a parenthesized expression is legal" half of the rule.
    /// </summary>
    public static IEnumerable<object[]> AcceptedSpellings => new[]
    {
        new object[] { "print(sum(x for x in xs))", 1, "bare sole argument" },
        new object[] { "g = (x for x in xs)", 1, "parenthesized, assignment RHS" },
        new object[] { "print(sum((x for x in xs)))", 1, "parenthesized sole argument" },
        new object[] { "print(f((x for x in xs), 1))", 1, "parenthesized among several arguments" },
        new object[] { "print(f(a=(x for x in xs)))", 1, "parenthesized keyword value" },
        new object[] { "print(sum(x for x in xs if x > 1))", 1, "one for, one if" },
        new object[] { "print(sum(x + y for x in xs for y in xs))", 1, "two for clauses" },
        new object[] { "print(sum(x for x in xs if x > 1 if x < 9))", 1, "two if clauses" },
        new object[] { "print([(y for y in xs) for x in xs])", 1, "comprehension element" },
        new object[] { "for v in (x for x in xs):\n        print(v)", 1, "for iterator" },
        new object[] { "print(sum(x for x in (y for y in xs)))", 2, "nested generators" },
        new object[] { "print(f\"{sum(x for x in xs)}\")", 1, "f-string hole" },
    };

    [Theory]
    [MemberData(nameof(AcceptedSpellings))]
    public void AcceptedSpellingProducesGeneratorNodes(string body, int expectedCount, string label)
    {
        var (module, errors) = Parse(
            "def f(g: Iterator[int], a: Iterator[int]) -> int:\n"
            + "    return 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    xs: list[int] = [1, 2, 3]\n"
            + $"    {body}\n");

        errors.Should().BeEmpty(
            $"{label} must parse: " + string.Join("; ", errors.Select(e => $"{e.Code}:{e.Message}")));
        CountGenerators(module).Should().Be(expectedCount, label);
    }

    // ── refused spellings ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every bare spelling that is NOT the sole argument. python3 rejects each of these as a
    /// SyntaxError; Sharpy names the refusal and says what to do about it.
    /// </summary>
    public static IEnumerable<object[]> RefusedSpellings => new[]
    {
        new object[] { "print(sum(x for x in xs, 1))", "bare, followed by another argument" },
        new object[] { "print(f(1, x for x in xs))", "bare, preceded by another argument" },
        new object[] { "print(f(a=x for x in xs))", "bare as a keyword VALUE" },
        new object[] { "print(f(1, a=x for x in xs))", "bare keyword value after a positional" },
    };

    [Theory]
    [MemberData(nameof(RefusedSpellings))]
    public void RefusedSpellingIsNamedWithTheParenthesizeSteer(string body, string label)
    {
        var (_, errors) = Parse(
            "def f(g: Iterator[int], a: Iterator[int]) -> int:\n"
            + "    return 0\n"
            + "\n"
            + "def main() -> None:\n"
            + "    xs: list[int] = [1, 2, 3]\n"
            + $"    {body}\n");

        errors.Should().Contain(
            e => e.Code == DiagnosticCodes.Parser.GeneratorExpressionMustBeParenthesized,
            $"{label} must be SPY0147 with the parenthesize steer, not the generic expected-token "
            + "error; got " + string.Join("; ", errors.Select(e => $"{e.Code}:{e.Message}")));
    }

    /// <summary>
    /// The positive control for the refusals above: the SAME expression, parenthesized, parses. So
    /// the refusal is about the bare SPELLING and not about the expression being unparseable.
    /// </summary>
    [Fact]
    public void ParenthesizingTheRefusedSpellingsMakesThemLegal()
    {
        foreach (var body in new[]
                 {
                     "print(sum((x for x in xs), 1))",
                     "print(f((x for x in xs), 1))",
                     "print(f(a=(x for x in xs)))",
                 })
        {
            var (module, errors) = Parse(
                "def f(g: Iterator[int], a: Iterator[int]) -> int:\n"
                + "    return 0\n"
                + "\n"
                + "def main() -> None:\n"
                + "    xs: list[int] = [1, 2, 3]\n"
                + $"    {body}\n");

            errors.Should().BeEmpty(
                $"`{body}` is the parenthesized control and must parse: "
                + string.Join("; ", errors.Select(e => $"{e.Code}:{e.Message}")));
            CountGenerators(module).Should().Be(1, body);
        }
    }

    /// <summary>
    /// A comprehension is NOT a generator expression, and the brackets are what tells them apart.
    /// Without this the node-count assertions above could be satisfied by a parser that produced a
    /// generator for every comprehension.
    /// </summary>
    [Fact]
    public void BracketedComprehensionsAreNotGeneratorExpressions()
    {
        var (module, errors) = Parse(
            "def main() -> None:\n"
            + "    xs: list[int] = [1, 2, 3]\n"
            + "    print([x for x in xs])\n"
            + "    print({x for x in xs})\n"
            + "    print({x: x for x in xs})\n");

        errors.Should().BeEmpty();
        CountGenerators(module).Should().Be(0,
            "list, set and dict comprehensions are their own node kinds");
    }
}
