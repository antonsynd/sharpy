using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
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

    /// <summary>
    /// The two halves are judged separately, every sample (#2288): the complete class
    /// (<c>__next__</c> + <c>__iter__ -> Self</c>, iterated by <c>for v in c</c>) analyses clean,
    /// and the same class without <c>__iter__</c> draws exactly SPY0320 (not iterable). Before
    /// #2288 one accepted-or-refused sample of either half passed the property, and the complete
    /// half had never once analysed clean (an undeclared field, an unannotated <c>__iter__</c>).
    /// </summary>
    [Fact]
    public void IterProtocol_RequiresBothMethods()
    {
        var complete = ErrorInjector.JudgeSamples(GenIterators.ModuleWithIterProtocol(complete: true),
            source => ErrorInjector.JudgeClean(source, "complete iterator class"), iter: 25);
        _output.WriteLine($"Iter protocol (complete): {complete.Tested - complete.Failures}/{complete.Tested} analysed clean");

        var incomplete = ErrorInjector.JudgeSamples(GenIterators.ModuleWithIterProtocol(complete: false),
            source => ErrorInjector.JudgeSource(source, "SPY0320", CompilerDiagnosticSeverity.Error, null,
                "iterated class without __iter__"), iter: 25);
        _output.WriteLine($"Iter protocol (no __iter__): {incomplete.Tested - incomplete.Failures}/{incomplete.Tested} drew SPY0320");

        Assert.True(complete.Tested > 0, "No complete iterator programs were sampled");
        Assert.True(incomplete.Tested > 0, "No incomplete iterator programs were sampled");
        Assert.True(complete.First is null,
            $"{complete.Failures}/{complete.Tested} complete iterator programs refused; the first:\n{complete.First}");
        Assert.True(incomplete.First is null,
            $"{incomplete.Failures}/{incomplete.Tested} incomplete iterator programs misjudged; the first:\n{incomplete.First}");
    }
}
