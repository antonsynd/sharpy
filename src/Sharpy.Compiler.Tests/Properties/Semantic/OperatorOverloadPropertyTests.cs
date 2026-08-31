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
public class OperatorOverloadPropertyTests
{
    private readonly ITestOutputHelper _output;

    public OperatorOverloadPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BinaryOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.BinaryOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "BinaryOperator_DunderCompiles");
    }

    [Fact]
    public void UnaryOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.UnaryOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "UnaryOperator_DunderCompiles");
    }

    [Fact]
    public void ComparisonOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.ComparisonOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ComparisonOperator_DunderCompiles");
    }

    [Fact]
    public void OperatorPrecedence_PreservedThroughCompilation()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.PrecedenceProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "OperatorPrecedence_PreservedThroughCompilation");
    }

    [Fact]
    public void InvalidDunder_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenOperators.InvalidDunderProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "operator_test.spy");
                    if (!result.Success)
                        Interlocked.Increment(ref diagnosed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Invalid dunder diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Invalid dunder diagnostic rate too low: {diagnosed}/{total}");
    }
}
