using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The binding-position matrix of the contract "a Sharpy local named <c>_</c> is a real local in the
/// emitted C#" (#2166, plan-be89c1 Task 4b). Each position is run twice: with an escaped read
/// <c>x = `_`</c>, which must print the bound value, and with no read, which must still compile and run.
/// </summary>
/// <remarks>
/// <para>C# reads a <i>designation</i> spelled <c>_</c> — <c>var (a, _)</c>, <c>out var _</c>,
/// <c>case var _</c>, <c>.. var _</c> — as a discard, while a <i>declaration</i> <c>var _ = 3;</c> is a
/// local. The deconstruction, for-target, comprehension, <c>out</c> and as-pattern rows emitted the
/// former and failed CS0103 behind SPY0908; the assignment, <c>for _</c>, parameter and lambda rows
/// emitted the latter and are the positive controls that already passed. The cure is one spelling
/// decision (<c>NameCasing.ResolveVariable</c>), not a per-arm patch, so every position is a row.</para>
/// <para>The read is an assignment, not <c>print(`_`)</c>, so the rows do not lean on the #2166
/// parser fix (an escaped <c>_</c> call argument was the partial-application placeholder).</para>
/// <para>The second axis is the emitter's own discards: a bare expression statement lowers to
/// <c>_ = expr;</c>, and C# binds that to any local named <c>_</c> in scope — the first spelling
/// of the fix, <c>@_</c>, is that same identifier, so <c>a, _ = 1, 2</c> followed by a bare
/// <c>"s"</c> was CS0029 and a bare <c>10 + 1</c> silently overwrote <c>_</c>. Every position runs
/// again with two such statements before the read (<see cref="DiscardRows"/>).</para>
/// </remarks>
public class UnderscoreLocalDesignationTests : IntegrationTestBase
{
    public UnderscoreLocalDesignationTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string Prelude =
        "class Pair:\n"
        + "    def __enter__(self) -> tuple[int, int]:\n"
        + "        return (1, 2)\n"
        + "\n"
        + "    def __exit__(self, a: object?, b: Exception?, c: object?) -> bool:\n"
        + "        return False\n"
        + "\n"
        + "\n"
        + "class P:\n"
        + "    x: int\n"
        + "\n"
        + "    def __init__(self, x: int):\n"
        + "        self.x = x\n"
        + "\n"
        + "\n"
        + "def try_parse(s: str, result: out int) -> bool:\n"
        + "    result = int(s)\n"
        + "    return True\n"
        + "\n"
        + "\n"
        + "def parameter(_: int) -> int:\n"
        + "    {READ}\n"
        + "    return 0\n"
        + "\n"
        + "\n";

    /// <summary>
    /// Module-level declarations only the operator rows compile, so a broken operator arm reddens
    /// those rows and no other.
    /// </summary>
    private const string OperatorPrelude =
        "class V:\n"
        + "    n: int\n"
        + "\n"
        + "    def __init__(self, n: int):\n"
        + "        self.n = n\n"
        + "\n"
        + "    def __add__(self, _: V) -> V:\n"
        + "        return V(self.n + {OPERAND_READ})\n"
        + "\n"
        + "    def __lt__(self, _: V) -> bool:\n"
        + "        return self.n < {OPERAND_READ}\n"
        + "\n"
        + "\n";

    /// <summary>
    /// Position → the body of <c>main</c> (with <c>{READ}</c> on its own line where the read goes) and
    /// the value the escaped read prints. The parameter and operator rows' reads are in
    /// <see cref="Prelude"/> (<c>{OPERAND_READ}</c>); the comprehension and lambda rows read inside
    /// their own scope (<c>{READ_EXPR}</c>) and print the result (<c>{PRINT_Y}</c>).
    /// </summary>
    private static readonly Dictionary<string, (string Body, string Value)> Positions = new()
    {
        ["deconstruction"] = ("a, _ = 1, 2\n{READ}", "2"),
        ["deconstruction_rebinds"] = ("_, a = 1, 2\n_, b = 3, 4\n{READ}", "3"),
        ["nested_deconstruction"] = ("(a, (b, _)) = (1, (2, 3))\n{READ}", "3"),
        ["starred_deconstruction"] = ("a, *_ = 1, 2, 3\n{READ}", "[2, 3]"),
        ["for_tuple_target"] = ("for a, _ in [(1, 2)]:\n    {READ}", "2"),
        ["comprehension_tuple_target"] = ("y = [{READ_EXPR} for a, _ in [(1, 2)]]\n{PRINT_Y}", "[2]"),
        ["with_tuple_target"] = ("with Pair() as (a, _):\n    {READ}", "2"),
        ["out_let"] = ("if try_parse(\"7\", out let _):\n    {READ}", "7"),
        ["out_typed"] = ("if try_parse(\"7\", out _: int):\n    {READ}", "7"),
        ["as_pattern"] = ("match 5:\n    case 5 as _:\n        {READ}\n    case _:\n        pass", "5"),
        // Declaration positions: already locals before the fix (positive controls).
        ["assignment"] = ("_ = 5\n{READ}", "5"),
        ["for_target"] = ("for _ in range(1, 2):\n    {READ}", "1"),
        ["parameter"] = ("parameter(3)", "3"),
        // An inlined operator renames its operand parameter to `right` by the parameter's C# name.
        ["operator_parameter"] = ("y = (V(1) + V(2)).n\n{PRINT_Y}", "3"),
        ["comparison_operator_parameter"] = ("y = V(1) < V(2)\n{PRINT_Y}", "True"),
        ["lambda_parameter"] = ("inc: (int) -> int = lambda _: {READ_EXPR}\ny = inc(2)\n{PRINT_Y}", "2"),
        // Reachable through the #2166 parser fix: an escaped `_` is a binding, not the wildcard.
        ["capture_pattern"] = ("match 5:\n    case `_`:\n        {READ}", "5"),
        ["star_capture"] = ("match [1, 2, 3]:\n    case [1, *`_`]:\n        {READ}\n    case _:\n        pass", "[2, 3]"),
        ["tuple_pattern_capture"] = ("match (1, 2):\n    case (`_`, z):\n        {READ}\n    case _:\n        pass", "1"),
        ["keyword_pattern_capture"] = ("match P(4):\n    case P(x=`_`):\n        {READ}\n    case _:\n        pass", "4"),
    };

    public static TheoryData<string, bool> Rows()
    {
        var data = new TheoryData<string, bool>();
        foreach (var position in new[]
                 {
                     "deconstruction", "deconstruction_rebinds", "nested_deconstruction",
                     "starred_deconstruction", "for_tuple_target", "comprehension_tuple_target",
                     "with_tuple_target", "out_let", "out_typed", "as_pattern",
                     "assignment", "for_target", "parameter", "operator_parameter",
                     "comparison_operator_parameter", "lambda_parameter",
                 })
        {
            data.Add(position, true);
            data.Add(position, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void LocalNamedUnderscore_IsARealLocal_AtEveryBindingPosition(string position, bool read)
        => AssertPosition(position, read);

    /// <summary>
    /// Every position with a statement-level read, with an int-valued and a string-valued bare
    /// expression statement before it: the emitter's <c>_ = expr;</c> discards must not bind to the
    /// local (the int one would overwrite it silently, the string one is CS0029).
    /// </summary>
    public static TheoryData<string, bool> DiscardRows()
    {
        var data = new TheoryData<string, bool>();
        foreach (var position in new[]
                 {
                     "deconstruction", "deconstruction_rebinds", "nested_deconstruction",
                     "starred_deconstruction", "for_tuple_target", "with_tuple_target", "out_let",
                     "out_typed", "as_pattern", "assignment", "for_target", "parameter",
                     "capture_pattern", "star_capture", "tuple_pattern_capture",
                     "keyword_pattern_capture",
                 })
        {
            data.Add(position, true);
            data.Add(position, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DiscardRows))]
    public void LocalNamedUnderscore_IsNotCapturedByAnEmitterDiscard_AtAnyBindingPosition(string position, bool read)
        => AssertPosition(position, read, discardsBefore: true);

    [Theory]
    [InlineData("capture_pattern", true)]
    [InlineData("capture_pattern", false)]
    [InlineData("star_capture", true)]
    [InlineData("star_capture", false)]
    [InlineData("tuple_pattern_capture", true)]
    [InlineData("tuple_pattern_capture", false)]
    [InlineData("keyword_pattern_capture", true)]
    [InlineData("keyword_pattern_capture", false)]
    public void LocalNamedUnderscore_IsARealLocal_AtEveryPatternCapture(string position, bool read)
        => AssertPosition(position, read);

    /// <summary>
    /// The inlined-operator rename (operand parameter → <c>right</c>) is keyed on the parameter's
    /// materialized C# name, the name its references resolve to. It was keyed on a re-derived
    /// camelCase spelling, which missed the <c>_</c> spelling above and a backtick-escaped operand
    /// too (CS0103 behind SPY0908).
    /// </summary>
    [Fact]
    public void InlinedOperator_EscapedOperandParameter_IsRenamedToTheOperatorParameter()
    {
        var result = CompileAndExecute(
            "class V:\n"
            + "    n: int\n"
            + "\n"
            + "    def __init__(self, n: int):\n"
            + "        self.n = n\n"
            + "\n"
            + "    def __add__(self, `Other`: V) -> V:\n"
            + "        return V(self.n + `Other`.n)\n"
            + "\n"
            + "\n"
            + "def main():\n"
            + "    print((V(1) + V(2)).n)\n");

        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.Equal("3\n", result.StandardOutput.Replace("\r\n", "\n"));
    }

    private void AssertPosition(string position, bool read, bool discardsBefore = false)
    {
        var (body, value) = Positions[position];
        var prelude = position.EndsWith("operator_parameter", StringComparison.Ordinal)
            ? Prelude + OperatorPrelude
            : Prelude;
        var source = prelude + "def main():\n" + Indent(body, "    ") + "\n    print(\"done\")\n";
        source = ExpandRead(source, read, discardsBefore)
            .Replace("{READ_EXPR}", read ? "`_`" : "0", StringComparison.Ordinal)
            .Replace("{PRINT_Y}", read ? "print(y)" : "pass", StringComparison.Ordinal)
            .Replace("{OPERAND_READ}", read ? "`_`.n" : "2", StringComparison.Ordinal);

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"{position} (read: {read}) must compile and run; errors: "
            + string.Join("; ", result.CompilationErrors) + "\n" + result.StandardError);
        var expected = (read ? value + "\n" : "") + "done\n";
        Assert.Equal(expected, result.StandardOutput.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Replaces each <c>{READ}</c> line with the escaped read (<c>x = `_`</c> then <c>print(x)</c>) at the
    /// line's own indentation, or with <c>pass</c> when the row has no read; with
    /// <paramref name="discardsBefore"/>, two bare expression statements precede it.
    /// </summary>
    private static string ExpandRead(string source, bool read, bool discardsBefore)
    {
        var lines = source.Split('\n');
        var output = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed != "{READ}")
            {
                output.Add(line);
                continue;
            }

            var indent = line[..^trimmed.Length];
            if (discardsBefore)
            {
                output.Add(indent + "10 + 1");
                output.Add(indent + "\"unused\"");
            }

            if (read)
            {
                output.Add(indent + "x = `_`");
                output.Add(indent + "print(x)");
            }
            else
            {
                output.Add(indent + "pass");
            }
        }

        return string.Join("\n", output);
    }

    private static string Indent(string body, string indent)
        => string.Join("\n", body.Split('\n').Select(l => l.Length == 0 ? l : indent + l));
}
