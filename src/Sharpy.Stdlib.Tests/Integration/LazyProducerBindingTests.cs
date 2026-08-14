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
}
