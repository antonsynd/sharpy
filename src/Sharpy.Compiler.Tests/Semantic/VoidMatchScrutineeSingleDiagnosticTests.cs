using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// SPY0275 (void-call match scrutinee, #1526) must be the ONLY error: after the refusal the
/// scrutinee type downgrades to <c>Unknown</c> so a non-wildcard arm doesn't cascade a second
/// pattern-type mismatch (the SPY0329 precedent). Covers both the statement and expression
/// forms — they share <c>ApplyVoidScrutineePolicy</c> but reach it through separate checkers.
/// </summary>
public class VoidMatchScrutineeSingleDiagnosticTests : IntegrationTestBase
{
    public VoidMatchScrutineeSingleDiagnosticTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void VoidCallScrutinee_StatementForm_LiteralArm_EmitsOnlySpy0275()
    {
        var result = CompileAndExecute(@"
def g():
    return None


def main() -> None:
    match g():
        case 5:
            pass
");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Semantic.VoidMatchScrutinee);
        Assert.Single(result.CompilationErrors);
    }

    [Fact]
    public void VoidCallScrutinee_ExpressionForm_LiteralArm_EmitsOnlySpy0275()
    {
        var result = CompileAndExecute(@"
def g():
    return None


def main() -> None:
    v: int = match g():
        case 5: 1
        case _: 2
    print(v)
");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Semantic.VoidMatchScrutinee);
        Assert.Single(result.CompilationErrors);
    }
}
