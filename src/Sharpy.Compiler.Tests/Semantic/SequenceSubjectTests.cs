using System.Linq;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Focused cells for a sequence (list) pattern's subject decision (Design Decision 6, #1702): the
/// subject is decided by the SAME classifier rule as a class pattern's head. An open subject is
/// refused with the <c>case list[int]([1, 2])</c> steer (SPY0345) and the refusal ABORTS emission —
/// the C# list pattern is never emitted against <c>object</c> (no CS8985 behind SPY0908). A
/// <c>T | None</c> list fills from its payload; a <c>T?</c> (Optional) list is matched through
/// <c>Some</c> (SPY0498).
///
/// <para>These seed the Phase 4 mutation controls; the full 80-cell matrix is
/// <c>SequencePatternSubjectMatrixTests</c> (Phase 7).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class SequenceSubjectTests : IntegrationTestBase
{
    public SequenceSubjectTests(ITestOutputHelper output) : base(output)
    {
    }

    // Mutation 4a control: restore the `object` pass-through (let object through silently as Unknown)
    // and this cell goes red — the C# list pattern is emitted against `object` → CS8985 behind SPY0908
    // (the ICE this refusal replaces). The refusal must be present AND emission must not run.
    [Fact]
    public void SequencePatternOnObject_RefusedWithClosedSpellingSteer_AbortsEmission()
    {
        var source = @"
def main() -> None:
    o: object = [1, 2]
    match o:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        Assert.False(result.Success,
            $"an open (object) subject must be refused, not compiled. Output: {result.StandardOutput}");
        Assert.Contains(result.RawDiagnostics, d => d.Code == "SPY0345");
        Assert.Contains(result.RawDiagnostics,
            d => d.Code == "SPY0345" && d.Message.Contains("case list[int]([1, 2])"));

        // Positive control that the refusal aborted emission: no C# stage ran, so no ICE.
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == "SPY0908");
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("CS8985"));
    }

    // Type-parameter subject: same open-subject refusal as `object` (Decision 6 arm 3, d14).
    [Fact]
    public void SequencePatternOnTypeParameter_RefusedWithClosedSpellingSteer()
    {
        var source = @"
def describe[T](v: T) -> None:
    match v:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2])
";
        var result = CompileAndExecute(source);

        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics,
            d => d.Code == "SPY0345" && d.Message.Contains("case list[int]([1, 2])"));
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == "SPY0908");
    }

    // Mutation 4b control: drop the `T | None` (NullableType) payload arm and this cell goes red —
    // the nullable list no longer fills and is wrongly refused SPY0220 (d10 regresses).
    [Fact]
    public void SequencePatternOnNullableList_FillsAndRuns()
    {
        var source = @"
def describe(xs: list[int] | None) -> None:
    match xs:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2])
    describe(None)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("seq\nother\n", result.StandardOutput);
    }

    // A `T?` (Optional) list is a tagged union — matched through Some, not a bare sequence pattern
    // (SPY0498, the sequence-pattern twin of the class-pattern a09 refusal).
    [Fact]
    public void SequencePatternOnOptionalList_RefusedMatchThroughSome()
    {
        var source = @"
def describe(xs: list[int]?) -> None:
    match xs:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    describe(Some([1, 2]))
";
        var result = CompileAndExecute(source);

        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == "SPY0498");
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == "SPY0908");
    }

    // A closed list[T] subject fills and runs (arm 1, the control that stays green).
    [Fact]
    public void SequencePatternOnClosedList_Runs()
    {
        var source = @"
def describe(xs: list[int]) -> None:
    match xs:
        case [a, b]:
            print(""seq"", a, b)
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2])
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("seq 1 2\n", result.StandardOutput);
    }

    // The explicit steer spelling `case list[int]([1, 2])` on an object subject parses (Phase 1),
    // classifies to list[int], and its inner ListPattern is checked against list[int] — d09 runs.
    [Fact]
    public void ExplicitSequenceHeadOnObject_Runs()
    {
        var source = @"
def main() -> None:
    o: object = [1, 2]
    match o:
        case list[int]([1, 2]):
            print(""seq"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("seq\n", result.StandardOutput);
    }

    // A concrete non-sequence type is still SPY0220 (unchanged; d06).
    [Fact]
    public void SequencePatternOnStr_RefusedNonSequence()
    {
        var source = @"
def main() -> None:
    s: str = ""hi""
    match s:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics,
            d => d.Code == "SPY0220" && d.Message.Contains("non-sequence type 'str'"));
    }
}
