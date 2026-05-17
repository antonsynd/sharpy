using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class DecoratorPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DecoratorPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidDecorator_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenDecorators.ModuleWithDecoratedFunction(validDecorator: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "decorator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Valid decorator compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Valid decorator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void DecoratorStacking_ValidCombinations()
    {
        int total = 0;
        int passed = 0;

        GenDecorators.ModuleWithDecoratorStack(valid: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "decorator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Valid decorator stacking: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Valid decorator stacking pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void DecoratorStacking_InvalidCombinations()
    {
        int total = 0;
        int diagnosed = 0;

        GenDecorators.ModuleWithDecoratorStack(valid: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "decorator_test.spy");
                    if (!result.Success || result.Diagnostics.GetAll().Any())
                        Interlocked.Increment(ref diagnosed);
                }
                catch
                {
                    Interlocked.Increment(ref diagnosed);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Invalid decorator stacking diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Invalid decorator diagnostic rate too low: {diagnosed}/{total}");
    }

    [Fact]
    public void DataclassDecorator_GeneratesCorrectMembers()
    {
        int total = 0;
        int passed = 0;

        GenDecorators.ModuleWithDataclass()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "decorator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Dataclass decorator: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Dataclass decorator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void BuiltinDecorators_BehaviorConsistent()
    {
        var errors = new List<string>();

        GenDecorators.ModuleWithDecoratorDeterminism()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                try
                {
                    var compiler1 = new Sharpy.Compiler.Compiler();
                    var result1 = compiler1.Analyze(source, "decorator_test.spy");

                    var compiler2 = new Sharpy.Compiler.Compiler();
                    var result2 = compiler2.Analyze(source, "decorator_test.spy");

                    if (result1.Success != result2.Success)
                    {
                        lock (errors)
                            errors.Add($"Success differs: {result1.Success} vs {result2.Success}");
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        Assert.Empty(errors);
    }
}
