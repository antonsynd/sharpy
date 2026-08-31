using CsCheck;
using Sharpy.TestInfrastructure;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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
        var samples = PropertyCorpus.CompileAll(GenVariance.CovariantInterfaceProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "CovariantInterface_CompilesClean");
    }

    [Fact]
    public void ContravariantInterface_CompilesClean()
    {
        var samples = PropertyCorpus.CompileAll(GenVariance.ContravariantInterfaceProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ContravariantInterface_CompilesClean");
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
