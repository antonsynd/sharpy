using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class ExceptionPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ExceptionPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TryExcept_CompilesWithValidHandlers()
    {
        var samples = PropertyCorpus.CompileAll(
            GenExceptions.ModuleWithTryExcept(handlerCount: 1),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "TryExcept_CompilesWithValidHandlers");
    }

    [Fact]
    public void ExceptHandler_TypeMatching()
    {
        var samples = PropertyCorpus.CompileAll(
            GenExceptions.ModuleWithRaiseExpression(),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ExceptHandler_TypeMatching");
    }

    [Fact]
    public void MultipleHandlers_OrderValidation()
    {
        int total = 0;
        int analyzed = 0;

        GenExceptions.ModuleWithMultipleHandlers()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "except_test.spy");
                    Interlocked.Increment(ref analyzed);
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"ICE on exception program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    Interlocked.Increment(ref analyzed);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Multiple handlers analysis: {analyzed}/{total} analyzed without ICE");
        Assert.True(analyzed > total / 2,
            $"Multiple handlers analysis rate too low: {analyzed}/{total}");
    }

    /// <summary>
    /// The two halves are judged separately, every sample (#2288): a bare <c>raise</c> inside an
    /// <c>except</c> handler analyses clean, and the same statement directly in <c>main</c> draws
    /// exactly SPY0242. Before #2288 one accepted-or-refused sample of either half passed, any
    /// diagnostic (or a throw) counted as the refusal.
    /// </summary>
    [Fact]
    public void BareRaise_OnlyInExceptBlock()
    {
        var inside = ErrorInjector.JudgeSamples(GenExceptions.ModuleWithBareRaise(insideExcept: true),
            source => ErrorInjector.JudgeClean(source, "bare raise inside except"), iter: 25);
        _output.WriteLine($"Bare raise (inside except): {inside.Tested - inside.Failures}/{inside.Tested} analysed clean");

        var outside = ErrorInjector.JudgeSamples(GenExceptions.ModuleWithBareRaise(insideExcept: false),
            source => ErrorInjector.JudgeSource(source, "SPY0242", CompilerDiagnosticSeverity.Error, null,
                "bare raise outside except"), iter: 25);
        _output.WriteLine($"Bare raise (outside except): {outside.Tested - outside.Failures}/{outside.Tested} drew SPY0242");

        Assert.True(inside.Tested > 0, "No inside-except bare raise programs were sampled");
        Assert.True(outside.Tested > 0, "No outside-except bare raise programs were sampled");
        Assert.True(inside.First is null,
            $"{inside.Failures}/{inside.Tested} inside-except bare raises refused; the first:\n{inside.First}");
        Assert.True(outside.First is null,
            $"{outside.Failures}/{outside.Tested} outside-except bare raises misjudged; the first:\n{outside.First}");
    }

    [Fact]
    public void ExceptionHierarchy_SubtypesAccepted()
    {
        var samples = PropertyCorpus.CompileAll(
            GenExceptions.ModuleWithExceptionHierarchy(),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ExceptionHierarchy_SubtypesAccepted");
    }
}
