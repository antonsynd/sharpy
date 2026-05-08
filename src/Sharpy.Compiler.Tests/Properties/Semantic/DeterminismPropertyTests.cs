using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
public class DeterminismPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DeterminismPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compilation_IsDeterministic()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var result1 = Compile(source);
            var result2 = Compile(source);

            if (result1.diagCount != result2.diagCount)
                throw new Exception(
                    $"Non-deterministic diagnostic count: {result1.diagCount} vs {result2.diagCount}");

            if (result1.success != result2.success)
                throw new Exception(
                    $"Non-deterministic compilation: {result1.success} vs {result2.success}");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    private static (bool success, int diagCount) Compile(string source)
    {
        try
        {
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "test.spy");
            return (result.Success, result.Diagnostics?.ErrorCount ?? 0);
        }
        catch
        {
            return (false, -1);
        }
    }
}
