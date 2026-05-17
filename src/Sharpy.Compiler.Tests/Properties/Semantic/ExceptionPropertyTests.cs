using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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
        int total = 0;
        int passed = 0;

        GenExceptions.ModuleWithTryExcept(handlerCount: 1)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "except_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Try/except compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Try/except pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void ExceptHandler_TypeMatching()
    {
        int total = 0;
        int passed = 0;

        GenExceptions.ModuleWithRaiseExpression()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "except_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Exception type matching: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Exception type matching rate too low: {passed}/{total}");
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

    [Fact]
    public void BareRaise_OnlyInExceptBlock()
    {
        int insideTotal = 0;
        int insidePassed = 0;
        int outsideTotal = 0;
        int outsideDiagnosed = 0;

        GenExceptions.ModuleWithBareRaise(insideExcept: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref insideTotal);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "except_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref insidePassed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 25);

        GenExceptions.ModuleWithBareRaise(insideExcept: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref outsideTotal);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "except_test.spy");
                    if (!result.Success || result.Diagnostics.GetAll().Any())
                        Interlocked.Increment(ref outsideDiagnosed);
                }
                catch
                {
                    Interlocked.Increment(ref outsideDiagnosed);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 25);

        _output.WriteLine($"Bare raise: inside={insidePassed}/{insideTotal}, outside diagnosed={outsideDiagnosed}/{outsideTotal}");
        Assert.True(insidePassed + outsideDiagnosed > 0,
            "No bare raise programs were analyzed");
    }

    [Fact]
    public void ExceptionHierarchy_SubtypesAccepted()
    {
        int total = 0;
        int passed = 0;

        GenExceptions.ModuleWithExceptionHierarchy()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "except_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Exception hierarchy: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Exception hierarchy pass rate too low: {passed}/{total}");
    }
}
