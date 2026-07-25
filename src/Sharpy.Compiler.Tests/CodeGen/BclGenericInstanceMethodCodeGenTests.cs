using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #1136: a generic instance method of a raw BCL type (e.g. <c>List&lt;T&gt;.ConvertAll&lt;TOutput&gt;</c>)
/// is absent from the discovered <c>TypeSymbol</c> (empty <c>Methods</c>), so an explicit-type-argument
/// reference <c>lst.convert_all[str](...)</c> used to fall through to value-indexing and emit the silent
/// mis-emit <c>lst.ConvertAll[...]</c> (element access on a method group → CS0021 → SPY0908). The
/// reflection fallback in <c>TryResolveGenericInstanceMethod</c> now resolves it, so the call emits the
/// proper generic C# call <c>lst.ConvertAll&lt;string&gt;(...)</c>. This asserts the emitted C# string —
/// the end-to-end pin the semantic-level tests cannot cover, and the primary pin per the Batch D plan.
///
/// <para>
/// The receiver is threaded through a <em>parameter</em> (not constructed) so the emitted C# is free of
/// the separate, pre-existing BCL-generic construction/local-annotation qualification ambiguity that
/// makes <c>new List&lt;int&gt;()</c> a CS0104 ambiguous reference between <c>Sharpy.List</c> and
/// <c>System.Collections.Generic.List</c> (tracked as its own follow-up). In this form the whole program
/// compiles and links (<c>Success</c> holds), so the assertion pins both the emission and that the
/// generated C# is valid — including that the lambda → <c>Converter&lt;int,string&gt;</c> conversion binds.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class BclGenericInstanceMethodCodeGenTests : IntegrationTestBase
{
    public BclGenericInstanceMethodCodeGenTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void BclGenericInstanceMethod_ExplicitTypeArgs_EmitsGenericCallNotElementAccess()
    {
        // List<T>.ConvertAll<TOutput> is invisible in the discovered TypeSymbol; the reflection
        // fallback resolves it so the explicit-type-argument call emits a real generic method call.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def convert(lst: List[int]) -> int:
    strs = lst.convert_all[str](lambda x: str(x))
    return len(strs)

def main() -> None:
    pass
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("ConvertAll<string>", result.GeneratedCSharp);
        // The old silent value-indexing mis-emit (element access on a method group) must be gone.
        Assert.DoesNotContain("ConvertAll[", result.GeneratedCSharp);
    }
}
