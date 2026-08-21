using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// Binding a lazy stdlib producer to a variable does NOT materialize it (#1354).
/// </summary>
/// <remarks>
/// <para>
/// THE ASSERTION IS ON THE EMITTED C#, ON PURPOSE. The behavioural half — that `iglob` sees a file
/// created after the call — lives in `Spy/glob/glob_tests.spy::test_iglob_is_lazily_evaluated`,
/// and it is the test that #1354 disabled. But a behavioural test alone could not have caught the
/// original regression: it passed for months against STALE generated C#, because the committed
/// artifact predated the materialization. Asserting the emitted form is the observation the stale
/// artifact could not fake.
/// </para>
/// <para>
/// The mechanism, so the next reader does not re-derive it: the bridge maps a CLR
/// <c>IEnumerable&lt;T&gt;</c> return onto <c>list[T]</c> and stamps it with its CLR origin, and
/// #1251 then binds <c>NativeCollectionForm</c> and records a materialization — so
/// <c>lazy = glob.iglob(...)</c> emitted <c>new Sharpy.List&lt;string&gt;(glob.Iglob(...))</c> and
/// walked the whole tree before the next statement ran. A type deriving from
/// <c>Sharpy.Iterator&lt;T&gt;</c> takes a DIFFERENT bridge arm — it maps to a <c>BuiltinType</c>,
/// not a <c>GenericType</c> — so <c>IsUnmaterializedClrSequence</c> rejects it at its first check
/// and the rule never fires. The exemption is structural, not a special case, which is why no
/// compiler change was needed to get it.
/// </para>
/// </remarks>
public class LazyProducerBindingTests : StdlibIntegrationTestBase
{
    public LazyProducerBindingTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void IglobBinding_IsNotMaterialized()
    {
        var result = CompileAndExecute(@"
import glob

def main() -> None:
    lazy = glob.iglob(""*.nonexistent-probe"")
    n: int = 0
    for p in lazy:
        n = n + 1
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);

        // The POSITIVE form is the load-bearing one. A bare `DoesNotContain("new Sharpy.List<...")`
        // would pass vacuously the moment the emitter spelled the wrapper differently — an absence
        // assertion that cannot tell "not materialized" from "materialized under another name".
        // Asserting the whole binding says what the emission must BE, so any drift fails loudly
        // rather than silently agreeing.
        //
        // Mutation-tested: reverting GlobModule.Iglob to `IEnumerable<string>` emits
        // `var lazy = new Sharpy.List<string>(glob.Iglob(...));` and fails this line.
        Assert.Contains("var lazy = glob.Iglob(", result.GeneratedCSharp);
        Assert.DoesNotContain("new Sharpy.List<string>(glob.Iglob", result.GeneratedCSharp);
    }

    /// <summary>
    /// The #1354 repro from the issue, verbatim: an INFINITE producer bound to a variable.
    /// Out of memory before the flip, 3 after it — and 3 is what CPython prints (measured, 3.12).
    /// </summary>
    /// <remarks>
    /// Mutation test (performed 2026-08-14, red, reverted): reverting <c>itertools.count</c> in
    /// <c>spy/itertools.spy</c> to its generator shape and regenerating emits
    /// <c>var c = new Sharpy.List&lt;int&gt;(itertools.Count(0));</c> — measured on the emitted C#
    /// rather than by running it, deliberately: executing that binding exhausts an infinite
    /// producer, so the mutated program cannot be run to observe a failure. That is precisely why
    /// the emitted-form assertion is the load-bearing one here and the output check is secondary.
    /// </remarks>
    [Fact]
    public void ItertoolsCountBinding_IsNotMaterialized()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    c = itertools.count(0)
    n: int = 0
    for v in c:
        n = n + 1
        if n >= 3:
            break
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var c = itertools.Count(", result.GeneratedCSharp);
        Assert.DoesNotContain("new Sharpy.List<int>(itertools.Count", result.GeneratedCSharp);
        Assert.Equal("3\n", result.StandardOutput);
    }

    /// <summary>
    /// What the #1354 flip changes for <c>os.walk</c> is its TYPE, not its laziness: the binding is
    /// now <c>Iterator[tuple[...]]</c>, so the list surface it never actually had is refused.
    /// <c>len(os.walk(root))</c> is a compile error here, matching CPython
    /// (<c>TypeError: object of type 'generator' has no len()</c>, measured, python3 3.12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS ASSERTION REPLACED A VACUOUS ONE, and the replacement is the point. The obvious test —
    /// "the binding is not materialized" — passes with or without the fix: measured 2026-08-14 by
    /// mutating <c>walk</c> in <c>spy/os_module.spy</c> back to its generator shape and
    /// regenerating, `w = os.walk(root)` STILL emitted the bare `var w = os.Walk(root)` and the
    /// laziness behaviour still held (the after-the-call directory was still seen, 2 not 1). #1251's
    /// materialization does not reach a tuple-element sequence, so os.walk never had the binding
    /// defect that `itertools.count` and `glob.iglob` had. A test asserting it would have been an
    /// absence assertion passing for a reason unrelated to the change.
    /// </para>
    /// <para>
    /// Mutation test (performed 2026-08-14, red, reverted): under that same mutation
    /// <c>len(w)</c> COMPILES — the bridge maps the generator's <c>IEnumerable&lt;T&gt;</c> onto
    /// <c>list[T]</c>, offering a length and an <c>append</c> the value cannot honour. That is the
    /// defect this fix actually removes, and this test is red without it.
    /// </para>
    /// </remarks>
    [Fact]
    public void OsWalkBinding_IsAnIteratorNotAList()
    {
        var result = CompileAndExecute(@"
import os
import tempfile

def main() -> None:
    root: str = tempfile.mkdtemp()
    w = os.walk(root)
    print(len(w))
");

        Assert.False(result.Success, "len() on os.walk must be refused — it is an Iterator, not a list");
    }

    /// <summary>
    /// os.walk is a generator in CPython, so a directory created between the call and the first
    /// iteration IS seen (measured, python3 3.12: both 'a' and 'b' are reported). A regression
    /// guard, not evidence for the flip — see <see cref="OsWalkBinding_IsAnIteratorNotAList"/> for
    /// why this behaviour held before the flip too.
    /// </summary>
    [Fact]
    public void OsWalkBinding_StaysLazy()
    {
        var result = CompileAndExecute(@"
import os
import tempfile

def main() -> None:
    root: str = tempfile.mkdtemp()
    os.mkdir(root + ""/a"")
    w = os.walk(root)
    os.mkdir(root + ""/b"")
    n: int = 0
    for dirpath, dirnames, filenames in w:
        n = n + len(dirnames)
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var w = os.Walk(", result.GeneratedCSharp);

        // 2, not 1: the directory created AFTER the call is still seen, because the top level is
        // read on the first MoveNext rather than at the binding.
        Assert.Equal("2\n", result.StandardOutput);
    }

    [Fact]
    public void EagerGlobBinding_IsStillTheSharpyList()
    {
        // The control that makes the assertion above mean something. `glob.glob` returns an eager
        // `List<string>` and must keep binding as a Sharpy list — if the exemption had been written
        // as "skip materialization for anything glob returns", this would go red.
        var result = CompileAndExecute(@"
import glob

def main() -> None:
    eager = glob.glob(""*.nonexistent-probe"")
    eager.append(""added"")
    print(len(eager))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n", result.StandardOutput);
    }

    // ---- Infinite producer binding cells (#1354 flip) ----

    [Fact]
    public void RepeatInfiniteBinding_TerminatesOnBreak()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.repeat(42)
    n: int = 0
    for v in lazy:
        n = n + 1
        if n >= 3:
            break
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var lazy = itertools.Repeat(", result.GeneratedCSharp);
        Assert.DoesNotContain("new Sharpy.List<int>(itertools.Repeat", result.GeneratedCSharp);
        Assert.Equal("3\n", result.StandardOutput);
    }

    [Fact]
    public void CycleBinding_TerminatesOnBreak()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.cycle([10, 20])
    n: int = 0
    for v in lazy:
        print(v)
        n = n + 1
        if n >= 5:
            break
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var lazy = itertools.Cycle(", result.GeneratedCSharp);
        Assert.Equal("10\n20\n10\n20\n10\n", result.StandardOutput);
    }

    // ---- Finite bare-T producer binding + differential cells ----

    [Fact]
    public void RepeatFiniteBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.repeat(7, 3)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("7\n7\n7\n", result.StandardOutput);
    }

    [Fact]
    public void CompressBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.compress([1, 2, 3, 4], [True, False, True, False])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var lazy = itertools.Compress(", result.GeneratedCSharp);
        Assert.Equal("1\n3\n", result.StandardOutput);
    }

    [Fact]
    public void DropwhileBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def less_than_3(x: int) -> bool:
    return x < 3

def main() -> None:
    lazy = itertools.dropwhile(less_than_3, [1, 2, 3, 4, 1])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n4\n1\n", result.StandardOutput);
    }

    [Fact]
    public void TakewhileBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def less_than_3(x: int) -> bool:
    return x < 3

def main() -> None:
    lazy = itertools.takewhile(less_than_3, [1, 2, 3, 4, 1])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void FilterfalseBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def is_odd(x: int) -> bool:
    return x % 2 != 0

def main() -> None:
    lazy = itertools.filterfalse(is_odd, [1, 2, 3, 4, 5])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("2\n4\n", result.StandardOutput);
    }

    [Fact]
    public void IsliceBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.islice([0, 1, 2, 3, 4, 5], 3)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void IsliceRangeBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.islice_range([0, 1, 2, 3, 4, 5, 6, 7, 8, 9], 2, 7, 2)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("2\n4\n6\n", result.StandardOutput);
    }

    [Fact]
    public void AccumulateFuncBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def multiply(a: int, b: int) -> int:
    return a * b

def main() -> None:
    lazy = itertools.accumulate([1, 2, 3, 4], multiply)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n6\n24\n", result.StandardOutput);
    }

    [Fact]
    public void AccumulateFuncInitialBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def add(a: int, b: int) -> int:
    return a + b

def main() -> None:
    lazy = itertools.accumulate([1, 2, 3], add, 10)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("10\n11\n13\n16\n", result.StandardOutput);
    }

    [Fact]
    public void ChainTwoBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.chain([1, 2], [3, 4])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ChainThreeBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.chain([1], [2], [3])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n3\n", result.StandardOutput);
    }

    [Fact]
    public void ChainFromIterableBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.chain_from_iterable([[1, 2], [3, 4]])
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void StarmapBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def add(x: int, y: int) -> int:
    return x + y

def main() -> None:
    pairs: list[tuple[int, int]] = [(1, 2), (3, 4)]
    lazy = itertools.starmap(add, pairs)
    for v in lazy:
        print(v)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n7\n", result.StandardOutput);
    }

    // ---- Tuple-shaped producer binding + differential cells ----

    [Fact]
    public void PairwiseBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.pairwise([1, 2, 3, 4])
    for a, b in lazy:
        print(a)
        print(b)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var lazy = itertools.Pairwise(", result.GeneratedCSharp);
        Assert.Equal("1\n2\n2\n3\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ZipLongestBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.zip_longest([1, 2, 3], [4, 5], 0)
    for a, b in lazy:
        print(a)
        print(b)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n4\n2\n5\n3\n0\n", result.StandardOutput);
    }

    [Fact]
    public void ProductTwoBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.product([1, 2], [3, 4])
    for a, b in lazy:
        print(a)
        print(b)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n3\n1\n4\n2\n3\n2\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ProductThreeBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.product([1], [2], [3])
    for a, b, c in lazy:
        print(a)
        print(b)
        print(c)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n2\n3\n", result.StandardOutput);
    }

    // ---- List[T]-shaped producer binding + differential cells ----

    [Fact]
    public void CombinationsBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.combinations([1, 2, 3], 2)
    n: int = 0
    for combo in lazy:
        n = n + 1
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("var lazy = itertools.Combinations(", result.GeneratedCSharp);
        Assert.Equal("3\n", result.StandardOutput);
    }

    [Fact]
    public void PermutationsBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.permutations([1, 2, 3], 2)
    n: int = 0
    for perm in lazy:
        n = n + 1
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("6\n", result.StandardOutput);
    }

    [Fact]
    public void CombinationsWithReplacementBinding_OutputMatchesCPython()
    {
        var result = CompileAndExecute(@"
import itertools

def main() -> None:
    lazy = itertools.combinations_with_replacement([1, 2], 2)
    n: int = 0
    for combo in lazy:
        n = n + 1
    print(n)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n", result.StandardOutput);
    }

    /// <summary>
    /// Type-level pin for the whole #1354 flip: every flipped itertools producer returns
    /// <c>Sharpy.Iterator&lt;T&gt;</c> in the COMPILED stdlib assembly — the artifact user
    /// code actually binds against.
    /// </summary>
    /// <remarks>
    /// The per-producer <c>*_OutputMatchesCPython</c> cells above prove CPython output parity,
    /// but for a finite, side-effect-free producer the final output is identical whether the
    /// sequence is lazily generated or eagerly materialized — output parity alone cannot pin
    /// laziness. The laziness fact is the return TYPE (an <c>Iterator&lt;T&gt;</c> takes the
    /// bridge arm that #1251's materialization rule never touches — see the class remarks), so
    /// this sweep asserts it directly for every producer at once, on the built assembly rather
    /// than on emitted C#: a stale committed artifact cannot fake it, and a future revert of
    /// any single producer fails here by name.
    /// </remarks>
    [Fact]
    public void EveryFlippedProducer_ReturnsIteratorInCompiledAssembly()
    {
        var flippedProducers = new[]
        {
            "Count", "Repeat", "Cycle", "Compress", "Dropwhile", "Takewhile", "Filterfalse",
            "Islice", "IsliceRange", "Pairwise", "Accumulate", "Chain", "ChainFromIterable",
            "Starmap", "ZipLongest", "Product", "Combinations", "Permutations",
            "CombinationsWithReplacement",
        };

        var publicStatic = typeof(Sharpy.Itertools).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Itertools/__Init__.cs (#835) deliberately keeps a handwritten .NET-interop adapter
        // layer next to the generated producers: overloads taking IEnumerable<T> (or, for
        // Repeat, uint) that delegate inward. They are not reachable from Sharpy source —
        // itertools.cycle(generator) is refused SPY0354 (measured at 445f0d80c) — so their
        // eager IEnumerable<T> return type cannot re-trigger #1251's materialization. The
        // exemption is SHAPE-keyed, not name-keyed, so it stays falsifiable: a generated
        // producer that regressed to IEnumerable<T> still takes Sharpy.List<T>/scalar
        // parameters and fails below.
        static bool IsInteropAdapterShape(System.Reflection.MethodInfo method)
            => method.GetParameters().Any(p =>
                (p.ParameterType.IsGenericType
                    && p.ParameterType.GetGenericTypeDefinition()
                        == typeof(System.Collections.Generic.IEnumerable<>))
                || (p.ParameterType.IsArray
                    && p.ParameterType.GetElementType() is { IsGenericType: true } elem
                    && elem.GetGenericTypeDefinition()
                        == typeof(System.Collections.Generic.IEnumerable<>))
                || p.ParameterType == typeof(uint));

        foreach (var name in flippedProducers)
        {
            var producerOverloads = publicStatic
                .Where(m => m.Name == name && !IsInteropAdapterShape(m))
                .ToList();
            Assert.True(
                producerOverloads.Count > 0,
                $"expected a public non-adapter producer named {name} on Sharpy.Itertools");
            foreach (var method in producerOverloads)
            {
                Assert.True(
                    method.ReturnType.IsGenericType
                    && method.ReturnType.GetGenericTypeDefinition() == typeof(Sharpy.Iterator<>),
                    $"{name} returns {method.ReturnType} — a flipped producer must return Sharpy.Iterator<T> (#1354)");
            }
        }

        // groupby is the one producer NOT flipped — iter[tuple[K, list[T]]] is refused when K
        // carries a type constraint (#1600). Pin its CURRENT eager shape positively (not by
        // skipping it) so the day #1600 lands this assertion goes red and Groupby moves into
        // flippedProducers above — the exemption drains on fix instead of going stale.
        var groupby = Assert.Single(publicStatic, m => m.Name == "Groupby");
        Assert.True(
            groupby.ReturnType.IsGenericType
            && groupby.ReturnType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>),
            $"Groupby returns {groupby.ReturnType} — if it now returns Iterator<T>, #1600 is fixed: move it into flippedProducers");
    }
}
