using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The shared three-arm type-test decider (<c>DecideBoundTypeTest</c>) once the #912 erasure arm is
/// retired (#1708, #1619). Every runtime type test names a CLOSED CLR type or is refused with SPY0345;
/// there is no longer an erased-to-protocol-interface middle answer. These cells exercise the sites
/// that route through the shared decider — a match class pattern and an <c>as?</c> cast — so a
/// re-introduced erasure arm, an ignored explicit type-argument vector, or a resurrected
/// double-report is caught by output rather than by a lowering-shape assertion (Rule 12).
///
/// <para>
/// The <c>isinstance</c> form joins the shared decider in Task 2.2; until then it keeps its own copy,
/// so its cells live in <see cref="TypeTestLoweringTests"/>.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class SharedTypeTestDeciderTests : IntegrationTestBase
{
    public SharedTypeTestDeciderTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void BareCollectionPattern_OnObjectSubject_IsRefusedWithSPY0345()
    {
        // The erasure arm is gone: a bare `list` on an `object` subject determines no element type, so
        // it names no runtime type and is refused (arm 3). Mutation guard 2b: re-adding an erasure arm
        // that records `list[object]` makes this compile and run, turning the assertion red.
        var result = CompileAndExecute(@"
def describe(o: object) -> None:
    match o:
        case list(xs):
            print(""list"")
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2, 3])
");

        result.Success.Should().BeFalse("a bare collection on an object subject names no closed type");
        result.RawDiagnostics.Select(d => d.Code)
            .Should().Contain(DiagnosticCodes.Semantic.OpenGenericTypeTest);
    }

    [Fact]
    public void BareCollectionPattern_OnClosedSubject_FillsFromTheSubjectAndRuns()
    {
        // Positive control the old erased test lacked: a `list[int]` subject determines the vector, so
        // arm 1 fills the capture to `list[int]` and the typed read `xs[0]` type-checks as `int`.
        var result = CompileAndExecute(@"
def describe(o: list[int]) -> None:
    match o:
        case list(xs):
            v: int = xs[0]
            print(v)

def main() -> None:
    describe([7, 2, 3])
");

        result.Success.Should().BeTrue(
            string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("7\n");
    }

    [Fact]
    public void ExplicitTypeArgumentPattern_OnObjectSubject_ClassifiesClosedAndRuns()
    {
        // Arm 2: the head spells its own arguments, so the test is exact (`o is Sharpy.List<int>`).
        // Mutation guard 2a: ignoring `annotation.TypeArguments` collapses this to a bare `list` on an
        // object subject → SPY0345 → compilation fails → red.
        var result = CompileAndExecute(@"
def describe(o: object) -> None:
    match o:
        case list[int](xs):
            print(len(xs))
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2, 3])
");

        result.Success.Should().BeTrue(
            string.Join(", ", result.CompilationErrors));
        result.StandardOutput.Should().Be("3\n");
    }

    [Fact]
    public void BareCollectionPattern_OnTypeParameterSubject_IsSPY0345_NotSPY0361()
    {
        // A type-parameter scrutinee is OPEN, exactly as it is for isinstance (#1619): it reaches arm 3
        // and draws SPY0345, and NEVER the pattern-compatibility check (SPY0361) that only a decided
        // closed type reaches. This is where the two classifiers used to disagree.
        var result = CompileAndExecute(@"
def describe[T](o: T) -> None:
    match o:
        case list(xs):
            print(""list"")
        case _:
            print(""other"")

def main() -> None:
    describe([1, 2, 3])
");

        result.Success.Should().BeFalse("a type-parameter subject determines no vector");
        var codes = result.RawDiagnostics.Select(d => d.Code).ToList();
        codes.Should().Contain(DiagnosticCodes.Semantic.OpenGenericTypeTest);
        codes.Should().NotContain(DiagnosticCodes.Semantic.TypePatternIncompatible,
            "the type-parameter subject must be refused as open, not as incompatible");
    }

    [Fact]
    public void CastToBareCollection_ReportsExactlyOneDiagnostic()
    {
        // `o as? list` is refused (arm 3). The refusal is TERMINAL: it used to ALSO run the annotation
        // through ResolveTypeAnnotation and draw a second SPY0224 ("expects 1 type arguments but got
        // 0") on the same node. One refused cell, one code (#1708).
        var result = CompileAndExecute(@"
def f(o: object) -> None:
    x = o as? list
    print(""done"")

def main() -> None:
    f(5)
");

        result.Success.Should().BeFalse();
        var codes = result.RawDiagnostics.Select(d => d.Code).ToList();
        codes.Count(c => c == DiagnosticCodes.Semantic.OpenGenericTypeTest)
            .Should().Be(1, "the cast refusal is emitted exactly once");
        codes.Should().NotContain(DiagnosticCodes.Semantic.WrongArgumentCount,
            "the terminal refusal must not fall through to the arity check");
    }
}
