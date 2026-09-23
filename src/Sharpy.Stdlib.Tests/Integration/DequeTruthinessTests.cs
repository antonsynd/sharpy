using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// <c>collections.deque</c> at every truth position (#1972): the nine positions of
/// <c>TruthinessConformanceTests</c> (if, while, assert, ternary, and, or, not, comprehension
/// filter, match guard) × {empty, non-empty}, executed. Before <c>Deque&lt;T&gt;</c> implemented
/// <see cref="ISized"/> each position was SPY0220 "has no falsy case" even though
/// <c>len(dq)</c> and <c>bool(dq)</c> worked. Expected stdout is python3 3.12.13 running the same
/// program over <c>collections.deque</c> (with <c>bool(...)</c> around the <c>and</c>/<c>or</c>
/// operands, since Sharpy's <c>and</c>/<c>or</c> on a truth-tested operand yield <c>bool</c>).
/// </summary>
public class DequeTruthinessTests : StdlibIntegrationTestBase
{
    public DequeTruthinessTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string Program = @"import collections

def probe(label: str, x: collections.deque[int]) -> None:
    if x:
        print(label, ""if"", ""T"")
    else:
        print(label, ""if"", ""F"")
    w: str = ""F""
    while x:
        w = ""T""
        break
    print(label, ""while"", w)
    try:
        assert x
        print(label, ""assert"", ""T"")
    except AssertionError:
        print(label, ""assert"", ""F"")
    print(label, ""ternary"", ""T"" if x else ""F"")
    a: bool = x and True
    print(label, ""and"", a)
    o: bool = x or False
    print(label, ""or"", o)
    print(label, ""not"", not x)
    print(label, ""filter"", len([1 for _ in range(1) if x]))
    val: int = 1
    match val:
        case v if x:
            print(label, ""guard"", ""T"")
        case _:
            print(label, ""guard"", ""F"")

def main() -> None:
    probe(""empty"", collections.deque[int]())
    probe(""full"", collections.deque[int]([1]))
";

    // python3 3.12.13, same program over collections.deque.
    private const string Expected =
        "empty if F\nempty while F\nempty assert F\nempty ternary F\nempty and False\nempty or False\n" +
        "empty not True\nempty filter 0\nempty guard F\n" +
        "full if T\nfull while T\nfull assert T\nfull ternary T\nfull and True\nfull or True\n" +
        "full not False\nfull filter 1\nfull guard T";

    [Fact]
    public void Deque_IsTruthTestable_AtEveryPosition_MatchesPython()
    {
        var result = CompileAndExecute(Program);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(Expected, result.StandardOutput.TrimEnd('\n', '\r'));
    }
}
