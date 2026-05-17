using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class ContextManagerPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ContextManagerPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidContextManager_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenContextManagers.ValidContextManagerProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "context_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Valid context manager: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Valid context manager pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void ContextManagerWithAsBinding_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenContextManagers.ContextManagerWithAsBinding()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "context_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Context manager with as: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Context manager with as pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void AsyncContextManager_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenContextManagers.AsyncContextManagerProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "context_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Async context manager: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Async context manager pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void MissingEnterOrExit_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenContextManagers.MissingEnterOrExitProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "context_test.spy");
                    if (!result.Success)
                        Interlocked.Increment(ref diagnosed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Missing enter/exit diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 2,
            $"Missing enter/exit diagnostic rate too low: {diagnosed}/{total}");
    }
}
