using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #1633: a compilation must never observe another's writes to a builtin symbol.
/// The <see cref="BuiltinRegistry"/> cached in <see cref="CompilerApi"/>'s analysis cache is
/// cloned per compilation so that <c>MaterializeCodeGenInfo</c> writes are isolated.
/// </summary>
public class SharedRegistryOwnershipTests
{
    private readonly ITestOutputHelper _output;

    public SharedRegistryOwnershipTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SequentialReuse_SecondAnalysis_DoesNotThrow()
    {
        var api = new CompilerApi();

        var source1 = "def main() -> None:\n    x: int = len([1, 2, 3])\n    print(x)\n";
        var result1 = api.Analyze(source1);
        Assert.True(result1.Success, $"First analysis failed: {string.Join("; ", result1.Diagnostics.Select(d => d.Message))}");

        // Before #1633, this threw DualWriteAssertions because the shared BuiltinRegistry's
        // len symbol already had CodeGenInfo from the first analysis.
        var source2 = "def main() -> None:\n    y: int = len('hello')\n    print(y)\n";
        var result2 = api.Analyze(source2);
        Assert.True(result2.Success, $"Second analysis failed: {string.Join("; ", result2.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void SequentialReuse_MasterRegistry_CodeGenInfoStaysNull()
    {
        var api = new CompilerApi();

        for (int i = 0; i < 3; i++)
        {
            var source = $"def main() -> None:\n    v{i}: int = len([{i}])\n    print(v{i})\n";
            var result = api.Analyze(source);
            Assert.True(result.Success, $"Analysis {i} failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        }

        // If the master were mutated, a fresh clone would inherit non-null CodeGenInfo and
        // a subsequent analysis would throw. Four clean analyses prove the master is pristine.
        var source4 = "def main() -> None:\n    z: int = len(b'abc')\n    print(z)\n";
        var result4 = api.Analyze(source4);
        Assert.True(result4.Success, "Fourth analysis failed — master registry may be mutated");
    }

    [Fact]
    public void SequentialReuse_CompilePath_NeverUsesSharedRegistry()
    {
        var api = new CompilerApi();

        var analyzeSource = "def main() -> None:\n    a: int = len([1])\n    print(a)\n";
        var analyzeResult = api.Analyze(analyzeSource);
        Assert.True(analyzeResult.Success, $"Analyze failed: {string.Join("; ", analyzeResult.Diagnostics.Select(d => d.Message))}");

        var compileSource = "def main() -> None:\n    b: int = len([2])\n    print(b)\n";
        var compileResult = api.Compile(compileSource);
        Assert.True(compileResult.Success, $"Compile after analyze failed: {string.Join("; ", compileResult.Diagnostics.Select(d => d.Message))}");
    }
}
