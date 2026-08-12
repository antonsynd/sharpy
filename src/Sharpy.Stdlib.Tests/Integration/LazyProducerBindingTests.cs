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
