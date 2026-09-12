using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// The warm/cold arm of the CLR member-type contract (#1705): a member's type must be the same on a
/// build whose symbols come from <c>obj/{Configuration}/.sharpy-symbols</c> as on a cold one.
/// Per-analysis state is never trusted after a restore — the standard cure for the warm ≠ cold
/// meta-class — and the three member shapes this exercises are exactly the ones whose types are
/// computed by reflection rather than written in the source: an ENUM property, a CHAR field, and a
/// method declared with a bare type parameter on a generic DEFINITION.
/// </summary>
public class ClrMemberTypeWarmColdTests
{
    private readonly ITestOutputHelper _output;

    public ClrMemberTypeWarmColdTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The library whose CLR member reads must survive a cache round-trip. Every value is
    /// deterministic: a fixed date (2026-01-01 is a Thursday), a separator compared with itself
    /// rather than with a platform literal, and a <c>pop</c> whose result is stored into a
    /// NON-nullable <c>str</c> — which only compiles while a bare <c>T</c> stays non-nullable.
    /// </summary>
    private const string Library = """
from system import DateTime, DayOfWeek
from system.io import Path as IoPath

def describe() -> str:
    d: DayOfWeek = DateTime(2026, 1, 1).day_of_week
    same: bool = IoPath.DirectorySeparatorChar == IoPath.DirectorySeparatorChar
    xs: list[str] = ["a", "b"]
    first: str = xs.pop(0)
    return str(d) + "|" + str(same) + "|" + first
""";

    private const string Expected = "Thursday|True|a\n";

    [Fact]
    public void ClrMemberTypes_AreTheSameWarmAsCold()
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ClrMemberWarmCold")
            .WithIncremental();

        helper.AddSourceFile("clr_member_lib.spy", Library);
        helper.AddSourceFile("main.spy", """
from clr_member_lib import describe

def main() -> None:
    print(describe())
""");
        helper.WithEntryPoint("main.spy");
        var cold = helper.CompileAndExecute();
        Assert.True(cold.Success, "cold: " + string.Join("; ", cold.CompilationErrors));
        Assert.Equal(Expected, cold.StandardOutput.Replace("\r\n", "\n"));

        // A REAL content edit to main.spy only — a no-op touch is ignored by the SHA-256 cache, so
        // without it the second build proves nothing about lib.spy being cache-served.
        helper.UpdateSourceFile("main.spy", """
from clr_member_lib import describe

def main() -> None:
    print(describe())
    print("warm")
""");

        var warm = helper.CompileAndExecute();
        Assert.True(warm.Success, "warm: " + string.Join("; ", warm.CompilationErrors));
        Assert.Equal(Expected + "warm\n", warm.StandardOutput.Replace("\r\n", "\n"));
        helper.AssertWarmBuildSkipped(helper.LastCompilationResult!, "clr_member_lib.spy");
    }

    /// <summary>
    /// The positive control for the test above: the same warm build REFUSES a wrong destination with
    /// the same type name a cold build does. Without it, "warm == cold" could hold because neither
    /// build has an opinion at all (a permissive Unknown accepts every destination).
    /// </summary>
    [Fact]
    public void ClrMemberTypes_RefuseTheSameWrongDestination_WarmAsCold()
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ClrMemberWarmColdRefusal")
            .WithIncremental();

        helper.AddSourceFile("clr_member_lib.spy", Library);
        helper.AddSourceFile("main.spy", """
from clr_member_lib import describe
from system import DateTime

def main() -> None:
    print(describe())
    b: bool = DateTime(2026, 1, 1).day_of_week
""");
        helper.WithEntryPoint("main.spy");
        var cold = helper.Compile();
        Assert.False(cold.Success);
        Assert.Contains(cold.Diagnostics.GetErrors(), d => d.Message.Contains("'DayOfWeek'", StringComparison.Ordinal));

        helper.UpdateSourceFile("main.spy", """
from clr_member_lib import describe
from system import DateTime

def main() -> None:
    print(describe())
    print("edited")
    b: bool = DateTime(2026, 1, 1).day_of_week
""");

        var warm = helper.Compile();
        Assert.False(warm.Success);
        Assert.Contains(warm.Diagnostics.GetErrors(), d => d.Message.Contains("'DayOfWeek'", StringComparison.Ordinal));
    }
}
