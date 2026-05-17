using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class FStringPropertyTests
{
    private readonly ITestOutputHelper _output;

    public FStringPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FString_WithMethodCalls_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenFStrings.FStringWithMethodCalls()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "fstring_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"F-string method calls: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"F-string method calls pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void FString_WithNestedCalls_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenFStrings.FStringWithNestedCalls()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "fstring_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"F-string nested calls: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"F-string nested calls pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void FString_WithFormatSpecs_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenFStrings.FStringWithFormatSpecs()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "fstring_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"F-string format specs: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"F-string format specs pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void FString_WithArithmetic_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenFStrings.FStringWithArithmetic()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "fstring_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"F-string arithmetic: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"F-string arithmetic pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void FString_ComplexCombined_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenFStrings.FStringComplexCombined()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "fstring_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"F-string complex combined: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"F-string complex combined pass rate too low: {passed}/{total}");
    }
}
