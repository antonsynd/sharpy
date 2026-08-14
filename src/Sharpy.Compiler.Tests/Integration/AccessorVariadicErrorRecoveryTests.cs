using System.Linq;

using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #1462: a variadic property/event accessor parameter is refused at declaration (SPY0496, #1406),
/// but the three accessor binding loops used to bind it as its ELEMENT type, so <c>*values: int</c>
/// bound as <c>int</c> and <c>len(values)</c> drew a true-but-stale SPY0320 cascade against the
/// already-refused program. The loops now bind such a parameter through error recovery (Unknown +
/// IsErrorRecovery), so the refusal stands alone.
///
/// <para>Pinned programmatically because #1457 means a <c>.error</c> fixture cannot assert the
/// ABSENCE of the second diagnostic: these assert the EXACT error-code set. The three cells exercise
/// the three separate loops (CheckClassProperty, CheckModuleProperty, CheckEvent); the control proves
/// recovery does not eat unrelated diagnostics.</para>
/// </summary>
public class AccessorVariadicErrorRecoveryTests : IntegrationTestBase
{
    public AccessorVariadicErrorRecoveryTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] ErrorCodes(ExecutionResult result)
        => result.RawDiagnostics
            .Where(d => d.IsError)
            .Select(d => d.Code)
            .Distinct()
            .OrderBy(c => c, System.StringComparer.Ordinal)
            .ToArray()!;

    /// <summary>CheckClassProperty loop: the refused variadic is the ONLY diagnostic — no SPY0320.</summary>
    [Fact]
    public void ClassPropertySetter_RefusedVariadic_IsExactlySPY0496()
    {
        var result = CompileAndExecute(
            "class C:\n" +
            "    property set samples(self, *values: int):\n" +
            "        print(len(values))\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(\"ok\")\n");

        Assert.False(result.Success);
        Assert.Equal(new[] { "SPY0496" }, ErrorCodes(result));
    }

    /// <summary>CheckModuleProperty loop: same, at module scope (no self).</summary>
    [Fact]
    public void ModuleProperty_RefusedVariadic_IsExactlySPY0496()
    {
        var result = CompileAndExecute(
            "property set samples(*values: int):\n" +
            "    print(len(values))\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(\"ok\")\n");

        Assert.False(result.Success);
        Assert.Equal(new[] { "SPY0496" }, ErrorCodes(result));
    }

    /// <summary>
    /// CheckEvent loop: the SPY0320 cascade is gone. The event shape carries its own unrelated
    /// diagnostics (SPY0373 non-delegate type, SPY0420 add-without-remove), so this asserts the
    /// refusal is present and the stale cascade absent rather than an exact set.
    /// </summary>
    [Fact]
    public void EventAccessor_RefusedVariadic_HasNoSPY0320Cascade()
    {
        var result = CompileAndExecute(
            "class C:\n" +
            "    event add on_change(self, *handlers: int):\n" +
            "        print(len(handlers))\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(\"ok\")\n");

        Assert.False(result.Success);
        var codes = ErrorCodes(result);
        Assert.Contains("SPY0496", codes);
        Assert.DoesNotContain("SPY0320", codes);
    }

    /// <summary>
    /// Control: a NON-variadic accessor with a genuine type error still reports it — the recovery
    /// binding is scoped to variadic parameters and must not swallow unrelated diagnostics.
    /// </summary>
    [Fact]
    public void NonVariadicAccessor_GenuineTypeError_IsStillReported()
    {
        var result = CompileAndExecute(
            "class C:\n" +
            "    property set samples(self, value: int):\n" +
            "        probe: str = value\n" +
            "        print(probe)\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(\"ok\")\n");

        Assert.False(result.Success);
        var codes = ErrorCodes(result);
        Assert.Contains("SPY0220", codes);
        Assert.DoesNotContain("SPY0496", codes);
    }
}
