using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1141: a typo'd member on a raw BCL receiver with explicit type arguments is a semantic
/// member-not-found error (SPY0203) — the same answer user classes and user modules already give for
/// the identical shape — instead of reaching code generation, emitting <c>lst.NoSuchMethod&lt;...&gt;(1)</c>,
/// and leaking CS1061 through the SPY0908 net (#1146: un-lowerable shapes are semantic-time diagnostics).
///
/// <para>
/// The diagnostic requires an affirmative <b>proof of absence</b> (Anton's rule): reflection over the
/// receiver's <c>ClrType</c> finds no member of that name under any mangling candidate, AND no
/// extension method reachable from the generated C# declares it. Everything short of that proof keeps
/// the permissive name-only interop channel exactly as it was — the guards below pin both halves, since
/// a strictness regression here would silently break .NET interop the compiler cannot see.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class BclMemberAbsenceTests : IntegrationTestBase
{
    public BclMemberAbsenceTests(ITestOutputHelper output) : base(output) { }

    private static int CountDiagnostics(ExecutionResult result, string code) =>
        result.RawDiagnostics.Count(d => d.Code == code);

    [Fact]
    public void TypoedMember_WithExplicitTypeArgs_ReportsMemberNotFound()
    {
        var result = CompileAndExecute(@"
from system.collections.generic import List

def probe(lst: List[int]) -> None:
    lst.no_such_method[str](1)

def main() -> None:
    pass
");

        Assert.False(result.Success);
        Assert.Equal(1, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedMember));
        Assert.Contains(result.CompilationErrors, e => e.Contains("has no member 'no_such_method'"));
        // The whole point: the shape never reaches codegen, so the CS1061 the SPY0908 net used to
        // report (`'List<int>' does not contain a definition for 'NoSuchMethod'`) never happens.
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("does not contain a definition"));
    }

    [Fact]
    public void TypoedMember_NearMiss_SuggestsTheRealClrMember()
    {
        var result = CompileAndExecute(@"
from system.collections.generic import List

def probe(lst: List[int]) -> None:
    lst.convert_al[str](lambda x: str(x))

def main() -> None:
    pass
");

        Assert.False(result.Success);
        // The candidate pool is the reflected member surface, so the reverse-mangled CLR name is offered.
        Assert.Contains(result.CompilationErrors, e => e.Contains("Did you mean 'convert_all'?"));
    }

    [Fact]
    public void TypoedMember_RepeatedSites_BothReportViaMemo()
    {
        // The second site must hit the (TypeSymbol, memberName) absence memo that sits alongside the
        // #1136 negative-result memo; memoized absence must diagnose exactly like freshly-proved absence.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def first(lst: List[int]) -> None:
    lst.no_such_method[str](1)

def second(lst: List[int]) -> None:
    lst.no_such_method[str](2)

def main() -> None:
    pass
");

        Assert.False(result.Success);
        Assert.Equal(2, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedMember));
    }

    [Fact]
    public void ExtensionPlausibleMember_WithExplicitTypeArgs_ResolvesAndRuns()
    {
        // `Select` is a System.Linq extension method, not a member of List<int>: reflection cannot prove
        // absence, so this reference must NOT be rejected as member-not-found. This case used to pin only
        // that permissive fall-through, because the shape then failed downstream (CS0021 → SPY0908).
        // Since #1163 it resolves for real — the resolver computes the whole C# type-argument vector
        // (Select<int, string>, the element type inferred from the receiver) and the call runs — so the
        // pin asserts resolution instead of mere survival. Same contract, stronger: the absence proof
        // still declines, and now nothing downstream leaks.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def main() -> None:
    lst = List[int]()
    lst.add(3)
    lst.add(4)
    for s in lst.select[str](lambda x: str(x)):
        print(s)
");

        Assert.Equal(0, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedMember));
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n4\n", result.StandardOutput.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ExtensionMethodCall_OnBclReceiver_CompilesAndRuns()
    {
        // The interop channel end to end: real System.Linq extension methods on a raw BCL receiver.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def main() -> None:
    lst = List[int]()
    lst.add(3)
    lst.add(4)
    print(lst.first())
    print(lst.count())
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("3\n2\n", result.StandardOutput.Replace("\r\n", "\n"));
    }

    [Fact]
    public void UnknownMember_ValueIndexed_StaysPermissive()
    {
        // Not a generic reference: `[0]` is a value subscript, not type arguments. The absence proof is
        // scoped to the explicit-type-argument shape, so ordinary interop member access is untouched.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def probe(lst: List[int]) -> None:
    x = lst.no_such_member[0]

def main() -> None:
    pass
");

        Assert.Equal(0, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedMember));
    }

    [Fact]
    public void ExistingGenericMember_WithExplicitTypeArgs_StillResolves()
    {
        // The #1136 positive: absence proving must not disturb a member reflection DOES find.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def convert(lst: List[int]) -> int:
    strs = lst.convert_all[str](lambda x: str(x))
    return len(strs)

def main() -> None:
    pass
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal(0, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedMember));
    }
}
