using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Structural tripwire for the augmented-assignment single-evaluation invariant (#1227, #1226,
/// #1228). Every subexpression of an augmented-assignment target must be spliced into the emitted
/// C# exactly once, no matter which operator sits on the assignment.
/// <para>
/// This complements — it does not replace — the behavioural fixtures
/// <c>single_evaluation_augmented_target.spy</c> and <c>single_evaluation_binary_operands.spy</c>,
/// which assert printed call counts at runtime. Two reasons a structural guard is needed as well.
/// First, a double-splice yields the CORRECT value whenever the operand is pure, so only an
/// operand with an observable side effect can expose it behaviourally; a structural count needs no
/// such staging and cannot be defeated by a lowering that happens to be idempotent. Second, and
/// the actual reason this file exists: the fixtures enumerate the operators that existed when they
/// were written. This test is driven off the <see cref="AssignmentOperator"/> enum, so adding a
/// new augmented operator without deciding what its target evaluation does fails
/// <see cref="EveryAugmentedOperator_IsCovered"/> rather than silently going unguarded.
/// </para>
/// <para>
/// That last point is the whole class of defect round 8 kept hitting — a construct handled at one
/// site and missed at its sibling. See umbrella #1145.
/// </para>
/// </summary>
public class AugmentedAssignmentSingleEvaluationTests
{
    private readonly ITestOutputHelper _output;

    public AugmentedAssignmentSingleEvaluationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Operators deliberately outside this matrix, each with the reason. Listed rather than merely
    /// absent so that <see cref="EveryAugmentedOperator_IsCovered"/> forces a decision when a new
    /// enum member appears instead of quietly passing.
    /// </summary>
    private static readonly Dictionary<AssignmentOperator, string> Excluded = new()
    {
        [AssignmentOperator.Assign] =
            "not augmented: plain '=' never reads its target, so there is nothing to duplicate",
        [AssignmentOperator.MatMulAssign] =
            "'@=' is gated behind the experimental 'matmul' feature (GatedConstruct.cs), so it "
            + "cannot be compiled by this harness without enabling the flag",
    };

    /// <summary>
    /// One case per augmented operator: the target declaration and the augmented statement. The
    /// index is always <c>probe()</c> — a call, which is exactly the shape
    /// <c>IsRepeatableTargetOperand</c> refuses to splice twice.
    /// </summary>
    public static TheoryData<AssignmentOperator, string, string> Cases() => new()
    {
        { AssignmentOperator.PlusAssign, "xs: list[int] = [10, 20]", "xs[probe()] += 1" },
        { AssignmentOperator.MinusAssign, "xs: list[int] = [10, 20]", "xs[probe()] -= 1" },
        { AssignmentOperator.StarAssign, "xs: list[int] = [10, 20]", "xs[probe()] *= 2" },
        { AssignmentOperator.SlashAssign, "xs: list[float] = [10.0, 20.0]", "xs[probe()] /= 4.0" },
        { AssignmentOperator.DoubleSlashAssign, "xs: list[int] = [10, 20]", "xs[probe()] //= 3" },
        { AssignmentOperator.PercentAssign, "xs: list[int] = [10, 20]", "xs[probe()] %= 7" },
        { AssignmentOperator.PowerAssign, "xs: list[int] = [10, 20]", "xs[probe()] **= 2" },
        { AssignmentOperator.AndAssign, "xs: list[int] = [10, 20]", "xs[probe()] &= 6" },
        { AssignmentOperator.OrAssign, "xs: list[int] = [10, 20]", "xs[probe()] |= 1" },
        { AssignmentOperator.XorAssign, "xs: list[int] = [10, 20]", "xs[probe()] ^= 2" },
        { AssignmentOperator.LeftShiftAssign, "xs: list[int] = [10, 20]", "xs[probe()] <<= 2" },
        { AssignmentOperator.RightShiftAssign, "xs: list[int] = [10, 20]", "xs[probe()] >>= 1" },
        {
            AssignmentOperator.NullCoalesceAssign,
            "xs: list[int | None] = [None, None]",
            "xs[probe()] ??= 7"
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AugmentedTarget_EvaluatesIndexExactlyOnce(
        AssignmentOperator op, string declaration, string statement)
    {
        var source = $@"
def probe() -> int:
    return 0


def main() -> None:
    {declaration}
    {statement}
    print(xs)
";

        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"'{op}' should compile, errors: "
            + string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        var code = result.GeneratedCSharpCode;
        code.Should().NotBeNull();
        _output.WriteLine(code);

        var lines = code!.Split('\n');

        // Verify the instrument before trusting its count. `probe` mangles to `Probe`, and the
        // DECLARATION line contains "Probe()" just as a call site does — counting the raw needle
        // over the whole file would score 2 for correct output and read as a passing "1 call plus
        // 1 declaration" only by coincidence. Assert the declaration is present and identifiable,
        // then count call sites in what remains. If mangling ever changes, this fails loudly here
        // rather than counting zero of a needle that no longer exists.
        var declarationLines = lines.Where(l => l.Contains("int Probe()")).ToList();
        declarationLines.Should().ContainSingle(
            "the emitted C# must contain exactly one 'probe' declaration for the call count below "
            + "to mean anything");

        var callSites = lines
            .Where(l => !l.Contains("int Probe()"))
            .Sum(l => CountOccurrences(l, "Probe()"));

        callSites.Should().Be(1,
            $"the index of an augmented '{op}' target must be evaluated exactly once (#1227): "
            + "generating it into both the read and the write makes a call in the target run "
            + "twice, which is invisible whenever the operand is pure");
    }

    /// <summary>
    /// Completeness guard. A new augmented operator must either get a row in <see cref="Cases"/>
    /// or an explicit entry in <see cref="Excluded"/> with a reason — it may not simply be absent.
    /// </summary>
    [Fact]
    public void EveryAugmentedOperator_IsCovered()
    {
        var covered = Cases().Select(row => (AssignmentOperator)row[0]!).ToHashSet();

        var missing = Enum.GetValues<AssignmentOperator>()
            .Where(op => !covered.Contains(op) && !Excluded.ContainsKey(op))
            .ToList();

        missing.Should().BeEmpty(
            "every AssignmentOperator must either be exercised by this matrix or be listed in "
            + "Excluded with a reason. Missing: "
            + string.Join(", ", missing)
            + ". A lowering added for a new operator that splices its target twice is exactly the "
            + "regression this file exists to catch, and an operator that is merely absent from "
            + "the matrix is unguarded without anything failing.");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
