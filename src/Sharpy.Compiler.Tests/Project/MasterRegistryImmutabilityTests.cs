using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #1633: the master <see cref="Semantic.Registry.BuiltinRegistry"/> held by the analysis
/// cache is never mutated — <c>MaterializeCodeGenInfo</c> writes to the
/// <see cref="Semantic.SemanticBinding"/>, not to symbols, and the per-compilation clone
/// has been retired. Sequential reuse of one <see cref="CompilerApi"/> proves the master
/// stays clean.
/// </summary>
public class MasterRegistryImmutabilityTests
{
    private readonly ITestOutputHelper _output;

    public MasterRegistryImmutabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SequentialReuse_NoClone_Succeeds()
    {
        var api = new CompilerApi();

        for (int i = 0; i < 4; i++)
        {
            var source = $"def main() -> None:\n    v{i}: int = len([{i}])\n    print(v{i})\n";
            var result = api.Analyze(source);
            Assert.True(result.Success,
                $"Analysis {i + 1} failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        }

        _output.WriteLine("4 sequential analyses through one CompilerApi: no exception");
    }

    [Fact]
    public void SequentialReuse_MixedPaths_Succeeds()
    {
        var api = new CompilerApi();

        var analyzeSource = "def main() -> None:\n    a: int = len([1])\n    print(a)\n";
        var analyzeResult = api.Analyze(analyzeSource);
        Assert.True(analyzeResult.Success,
            $"Analyze failed: {string.Join("; ", analyzeResult.Diagnostics.Select(d => d.Message))}");

        var compileSource = "def main() -> None:\n    b: int = len([2])\n    print(b)\n";
        var compileResult = api.Compile(compileSource);
        Assert.True(compileResult.Success,
            $"Compile after analyze failed: {string.Join("; ", compileResult.Diagnostics.Select(d => d.Message))}");

        var analyzeSource2 = "def main() -> None:\n    c: str = str(42)\n    print(c)\n";
        var analyzeResult2 = api.Analyze(analyzeSource2);
        Assert.True(analyzeResult2.Success,
            $"Second analyze failed: {string.Join("; ", analyzeResult2.Diagnostics.Select(d => d.Message))}");

        _output.WriteLine("Analyze → Compile → Analyze through one CompilerApi: no exception");
    }
}
