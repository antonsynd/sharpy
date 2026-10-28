using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The PROJECT-route twin of the admitted class-body sibling reads (P21h D8, R-JJ; #2384). An admitted
/// bare read in a class-body initializer is a node-keyed <c>SemanticInfo</c> fact
/// (<c>SetClassBodySiblingRead</c>) the emitter reads, and on the project route code generation reads
/// the per-file facts through <c>SemanticInfo.MergeFrom</c> — a fact the merge drops leaves the emitter
/// with a read that has no local behind it (SPY0909). <c>ClassBodyReadPositionMatrixTests</c> pins
/// every cell on one file; this pins that the fact survives the merge, for a class declared in an
/// imported module and for one in the entry module. The sibling is a <c>@static</c> property: it has
/// no symbol of its own to carry a <c>CodeGenInfo</c>, so the fact is the only route to its name.
/// </summary>
public class ClassBodySiblingReadProjectTests
{
    private readonly ITestOutputHelper _output;

    public ClassBodySiblingReadProjectTests(ITestOutputHelper output) => _output = output;

    private const string Lib = """
        class Box:
            @static
            property sp: int = 4

            b: int = sp
            property p: int = sp
        """;

    private const string Main = """
        from lib import Box


        class Local:
            @static
            property sp: int = 6

            b: int = sp


        def main() -> None:
            print(Box().b)
            print(Box().p)
            print(Local().b)
        """;

    [Fact]
    public void AdmittedSiblingRead_RunsOnTheProjectRoute()
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ClassBodySiblingRead")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", Lib);
        helper.AddSourceFile("main.spy", Main, isEntryPoint: true);

        var run = helper.CompileAndExecute();
        var diagnostics = string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ");
        _output.WriteLine($"success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}] {diagnostics}");

        Assert.DoesNotContain("internal compiler error", diagnostics, StringComparison.Ordinal);
        Assert.True(run.Success, $"the project does not run: {diagnostics}");
        Assert.Equal("4\n4\n6\n", run.StandardOutput.ReplaceLineEndings("\n"));
    }
}
