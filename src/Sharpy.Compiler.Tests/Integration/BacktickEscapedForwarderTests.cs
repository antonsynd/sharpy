using System;
using System.IO;

using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #1455: a backtick-escaped parameter must keep its verbatim spelling across the emitter's
/// SYMBOL-derived forwarders — the sites that name a parameter from a standalone
/// <see cref="Semantic.ParameterSymbol"/> rather than from an AST node. Before the fix the record
/// carried no <c>IsNameBacktickEscaped</c>, so <c>ParameterCSharpName(ParameterSymbol)</c>
/// camelCased a <c>`Zed`</c> parameter at every forwarder while the AST overload spelled it
/// verbatim at the declaration. Each test asserts the emitted C# both DECLARES and FORWARDS the
/// name verbatim, and the program RUNS — a spelling mismatch can pass emit yet still be observably
/// wrong (named-argument forwarding, IL) so both halves are checked.
/// </summary>
public class BacktickEscapedForwarderTests : IntegrationTestBase
{
    public BacktickEscapedForwarderTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// (a) constructor forwarder: <c>Derived</c> synthesizes a constructor from <c>Base</c>'s
    /// <c>__init__</c> ParameterSymbol vector and both declares and forwards <c>Zed</c> verbatim.
    /// </summary>
    [Fact]
    public void ConstructorForwarder_EscapedParam_DeclaresAndForwardsVerbatim()
    {
        var result = CompileAndExecute(
            "class Base:\n" +
            "    stored: int\n" +
            "\n" +
            "    def __init__(self, `Zed`: int):\n" +
            "        self.stored = `Zed`\n" +
            "\n" +
            "class Derived(Base):\n" +
            "    pass\n" +
            "\n" +
            "def main() -> None:\n" +
            "    d = Derived(41)\n" +
            "    print(d.stored)\n");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("41\n", result.StandardOutput);

        Assert.NotNull(result.GeneratedCSharp);
        // Declares AND forwards the escaped spelling verbatim, never camelCased.
        Assert.Contains("Derived(int Zed)", result.GeneratedCSharp);
        Assert.Contains("base(Zed)", result.GeneratedCSharp);
        Assert.DoesNotContain("Derived(int zed)", result.GeneratedCSharp);
    }

    /// <summary>
    /// (b) module-class re-export forwarder: a package <c>__init__</c> that re-exports a function
    /// with an escaped parameter emits a delegating method (<see cref="Semantic.FunctionSymbol"/>'s
    /// ParameterSymbol vector) that declares AND forwards <c>Zed</c> verbatim.
    /// </summary>
    [Fact]
    public void ModuleReExportForwarder_EscapedParam_DeclaresAndForwardsVerbatim()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"sharpy_reexport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "mypkg"));
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "mypkg", "helpers.spy"),
                "def compute(`Zed`: int) -> int:\n" +
                "    return `Zed` + 1\n");
            File.WriteAllText(Path.Combine(projectDir, "mypkg", "__init__.spy"),
                "from mypkg.helpers import compute\n");
            File.WriteAllText(Path.Combine(projectDir, "main.spy"),
                "from mypkg import compute\n" +
                "\n" +
                "def main() -> None:\n" +
                "    print(compute(41))\n");

            var result = CompileAndExecuteProject(projectDir, "main.spy");

            Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
            Assert.Equal("42\n", result.StandardOutput);

            // The re-export delegating method on the __init__ module class declares AND forwards the
            // escaped spelling verbatim (both ends derive from the same ParameterSymbol string).
            Assert.NotNull(result.GeneratedCSharp);
            Assert.Contains("int Zed", result.GeneratedCSharp);
            Assert.DoesNotContain("int zed", result.GeneratedCSharp);
        }
        finally
        {
            try
            { Directory.Delete(projectDir, recursive: true); }
            catch (IOException) { }
        }
    }
}
