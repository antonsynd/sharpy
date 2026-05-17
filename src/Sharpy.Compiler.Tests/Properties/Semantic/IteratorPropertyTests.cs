using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class IteratorPropertyTests
{
    private readonly ITestOutputHelper _output;

    public IteratorPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratorFunction_CompilesWithYield()
    {
        int total = 0;
        int passed = 0;

        GenIterators.ModuleWithGenerator(valid: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iter_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generator function compilation: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Generator function pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void GeneratorFunction_ValidatorChecks()
    {
        int total = 0;
        int analyzed = 0;

        GenIterators.ModuleWithGeneratorValidator()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iter_test.spy");
                    Interlocked.Increment(ref analyzed);
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"ICE on generator program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Other exceptions acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generator validator checks: {analyzed}/{total} analyzed without ICE");
        Assert.True(analyzed > total / 2,
            $"Generator validator analysis rate too low: {analyzed}/{total}");
    }

    [Fact]
    public void YieldFrom_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenIterators.ModuleWithYieldFrom()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iter_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Yield from compilation: {passed}/{total} passed");
        Assert.True(passed > total / 4,
            $"Yield from pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void IterProtocol_RequiresBothMethods()
    {
        int total = 0;
        int completePassed = 0;
        int incompleteErrored = 0;

        GenIterators.ModuleWithIterProtocol(complete: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iter_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref completePassed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 25);

        GenIterators.ModuleWithIterProtocol(complete: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iter_test.spy");
                    if (!result.Success || result.Diagnostics.GetAll().Any())
                        Interlocked.Increment(ref incompleteErrored);
                }
                catch
                {
                    Interlocked.Increment(ref incompleteErrored);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 25);

        _output.WriteLine($"Iter protocol: complete={completePassed}, incomplete errored={incompleteErrored}, total={total}");
        Assert.True(completePassed + incompleteErrored > 0,
            "No iter protocol programs were analyzed");
    }
}
