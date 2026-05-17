using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class VariancePropertyTests
{
    private readonly ITestOutputHelper _output;

    public VariancePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CovariantInterface_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenVariance.CovariantInterfaceProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "variance_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Covariant interface: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Covariant interface pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void ContravariantInterface_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenVariance.ContravariantInterfaceProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "variance_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Contravariant interface: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Contravariant interface pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void VarianceOnClass_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenVariance.VarianceOnClassProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "variance_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code == "SPY0417"))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Variance on class diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Variance on class diagnostic rate too low: {diagnosed}/{total}");
    }

    [Fact]
    public void CovariantInInputPosition_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenVariance.CovariantInInputPositionProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "variance_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code == "SPY0418"))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Covariant in input position: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Covariant in input position diagnostic rate too low: {diagnosed}/{total}");
    }

    [Fact]
    public void ContravariantInOutputPosition_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenVariance.ContravariantInOutputPositionProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "variance_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code == "SPY0419"))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Contravariant in output position: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Contravariant in output position diagnostic rate too low: {diagnosed}/{total}");
    }
}
