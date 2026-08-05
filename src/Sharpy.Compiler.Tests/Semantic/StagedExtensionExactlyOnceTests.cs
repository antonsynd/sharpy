using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1206 D6: the staged extension-method inference must check every lambda argument exactly once. A
/// double-check would double every diagnostic the body produces — the two-round shapes because the
/// staged path only supplies an expected type consumed by the single ordinary-loop check, the
/// three-round shapes because the #1161 deferral pass's phase-2 loop (now extended with the
/// substitution fold-back) owns each deferred body once. There is no diagnostic-suppression facility
/// in the TypeChecker, so exactly-once is the only thing standing between a staged call and
/// duplicated errors.
/// </summary>
[Collection("HeavyCompilation")]
public class StagedExtensionExactlyOnceTests : IntegrationTestBase
{
    public StagedExtensionExactlyOnceTests(ITestOutputHelper output) : base(output) { }

    private static int CountDiagnostics(ExecutionResult result, string code) =>
        result.RawDiagnostics.Count(d => d.Code == code);

    [Fact]
    public void TwoRoundShape_LambdaBodyError_ReportedOnce()
    {
        // select: the deferral pass declines (TResult sits in return position), so the ordinary
        // argument loop performs the single check with the synthesized expected type.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def main() -> None:
    lst = List[int]()
    lst.add(3)
    print(list(lst.select(lambda x: str(nope))))
");

        Assert.False(result.Success);
        Assert.Equal(1, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedVariable));
    }

    [Fact]
    public void ThreeRoundShape_DeferredLambdaBodyError_ReportedOnce()
    {
        // select_many: the deferral pass fires (TCollection in parameter position) and its phase-2
        // loop — the one the fold-back extension lives in — checks the deferred body exactly once.
        var result = CompileAndExecute(@"
from system.collections.generic import List

def main() -> None:
    xs = List[int]()
    xs.add(2)
    print(list(xs.select_many(lambda x: [str(missing_here)])))
");

        Assert.False(result.Success);
        Assert.Equal(1, CountDiagnostics(result, DiagnosticCodes.Semantic.UndefinedVariable));
    }
}
