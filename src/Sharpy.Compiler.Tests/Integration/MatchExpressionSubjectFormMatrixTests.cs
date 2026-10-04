using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2226 — a match EXPRESSION accepts every subject expression form the match STATEMENT accepts,
/// with the same result. Matrix: <b>subject form</b> (arithmetic, floor-division, power, bitwise,
/// shift, comparison, equality, chained comparison, <c>and</c>, <c>or</c>, <c>not</c>, unary minus,
/// conditional, call, index, attribute, string concatenation, <c>is None</c>, <c>in</c>, <c>??</c>,
/// parenthesized and bare-name controls, <c>await</c>) <b>× match-expression position</b>
/// {assignment right-hand side, return value, nested as an operand of <c>+</c>}, each differential
/// against the statement twin over the same subject and pinned to the python3-verified selection.
/// The call-argument position is refused by the spec (SPY0102, "not directly inside brackets",
/// match_statement.md) for every subject, so it is not a run cell.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at 21ca52b5d):</b> every subject
/// whose C# lowering ranks below range level (binary, comparison, <c>and</c>/<c>or</c>, chained
/// comparison, conditional, <c>is None</c>, <c>??</c>, string <c>+</c>) was SPY0524 in all three
/// positions — the switch-expression governing slot did not route through the precedence seam
/// (<c>EmittedTreePrecedence.Switch</c>). Floor-division, power, <c>not</c>, unary minus, call,
/// index, attribute, <c>in</c> and the controls lower to unary-or-primary C# and already ran.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class MatchExpressionSubjectFormMatrixTests : IntegrationTestBase
{
    public MatchExpressionSubjectFormMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// (subject, literal pattern, selection for input A, selection for input B). Input A is
    /// n=1, b=True, xs=[1, 2], p.x=1, s="a", o=1; input B is n=9, b=False, xs=[5], p.x=7, s="b",
    /// o=None. Selections verified with python3 over the same subjects (the statement form).
    /// </summary>
    private static readonly Dictionary<string, (string Subject, string Pattern, string A, string B)> Forms = new()
    {
        ["add"] = ("n + 2", "3", "hit", "miss"),
        ["multiply"] = ("n * 3", "3", "hit", "miss"),
        ["floordiv"] = ("n // 1", "1", "hit", "miss"),
        ["power"] = ("n ** 2", "1", "hit", "miss"),
        ["bitor"] = ("n | 2", "3", "hit", "miss"),
        ["shift"] = ("n << 1", "2", "hit", "miss"),
        ["compare"] = ("n > 5", "True", "miss", "hit"),
        ["equality"] = ("n == 1", "True", "hit", "miss"),
        ["chained"] = ("0 < n < 5", "True", "hit", "miss"),
        ["and"] = ("n > 0 and n < 5", "True", "hit", "miss"),
        ["or"] = ("n > 5 or n < 0", "False", "hit", "miss"),
        ["not"] = ("not b", "False", "hit", "miss"),
        ["negate"] = ("-n", "-1", "hit", "miss"),
        ["conditional"] = ("n if b else 0", "1", "hit", "miss"),
        ["call"] = ("abs(n)", "1", "hit", "miss"),
        ["index"] = ("xs[0]", "1", "hit", "miss"),
        ["attribute"] = ("p.x", "1", "hit", "miss"),
        ["concat"] = ("s + \"!\"", "\"a!\"", "hit", "miss"),
        ["is_none"] = ("o is None", "False", "hit", "miss"),
        ["in"] = ("n in xs", "True", "hit", "miss"),
        ["coalesce"] = ("o ?? 0", "1", "hit", "miss"),
        ["parenthesized"] = ("(n + 2)", "3", "hit", "miss"),
        ["name"] = ("n", "1", "hit", "miss"),
    };

    public static IEnumerable<object[]> SubjectForms() => Forms.Keys.Select(k => new object[] { k });

    private const string Params = "n: int, b: bool, xs: list[int], p: Pt, s: str, o: int | None";

    private static string Arms(string pattern, string indent) =>
        $"\n{indent}    case {pattern}: \"hit\"\n{indent}    case _: \"miss\"";

    private static string Program(string subject, string pattern) =>
        "class Pt:\n    x: int\n    def __init__(self, x: int):\n        self.x = x\n\n"
        + $"def by_statement({Params}) -> str:\n    match {subject}:\n        case {pattern}:\n            return \"hit\"\n"
        + "        case _:\n            return \"miss\"\n\n"
        + $"def by_assignment({Params}) -> str:\n    r = match {subject}:{Arms(pattern, "    ")}\n    return r\n\n"
        + $"def by_return({Params}) -> str:\n    return match {subject}:{Arms(pattern, "    ")}\n\n"
        + $"def by_nested({Params}) -> str:\n    r = \"<\" + match {subject}:{Arms(pattern, "    ")}\n    return r\n\n"
        + "def run(n: int, b: bool, xs: list[int], p: Pt, s: str, o: int | None):\n"
        + "    print(by_statement(n, b, xs, p, s, o), by_assignment(n, b, xs, p, s, o), "
        + "by_return(n, b, xs, p, s, o), by_nested(n, b, xs, p, s, o))\n\n"
        + "def main():\n    run(1, True, [1, 2], Pt(1), \"a\", 1)\n    run(9, False, [5], Pt(7), \"b\", None)\n";

    [Theory]
    [MemberData(nameof(SubjectForms))]
    public void MatchExpression_SubjectForm_MatchesStatementTwin_InEveryPosition(string form)
    {
        var (subject, pattern, a, b) = Forms[form];
        var source = Program(subject, pattern);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)}\n{source}");
        // Statement twin, assignment, return, nested — the same selection in every position.
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Should().Be($"{a} {a} {a} <{a}\n{b} {b} {b} <{b}\n", source);
    }

    /// <summary>The <c>await</c> subject: bare (unary, already ran) and as a binary operand.</summary>
    [Fact]
    public void MatchExpression_AwaitSubject_MatchesStatementTwin()
    {
        var source = "async def get(n: int) -> int:\n    return n\n\n"
            + "async def by_statement(n: int) -> str:\n    match await get(n) + 2:\n        case 3:\n            return \"hit\"\n"
            + "        case _:\n            return \"miss\"\n\n"
            + "async def by_assignment(n: int) -> str:\n    r = match await get(n) + 2:\n        case 3: \"hit\"\n        case _: \"miss\"\n    return r\n\n"
            + "async def by_return(n: int) -> str:\n    return match await get(n):\n        case 1: \"hit\"\n        case _: \"miss\"\n\n"
            + "async def main():\n    for n in [1, 9]:\n        print(await by_statement(n), await by_assignment(n), await by_return(n))\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Should().Be("hit hit hit\nmiss miss miss\n", source);
    }
}
