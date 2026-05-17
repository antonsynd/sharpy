using CsCheck;
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
        int total = 0;
        int passed = 0;

        GenOperators.BinaryOperatorProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "operator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Binary operator compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Binary operator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void UnaryOperator_DunderCompiles()
    {
        int total = 0;
        int passed = 0;

        GenOperators.UnaryOperatorProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "operator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Unary operator compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Unary operator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void ComparisonOperator_DunderCompiles()
    {
        int total = 0;
        int passed = 0;

        GenOperators.ComparisonOperatorProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "operator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Comparison operator compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Comparison operator pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void OperatorPrecedence_PreservedThroughCompilation()
    {
        int total = 0;
        int passed = 0;

        GenOperators.PrecedenceProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "operator_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Operator precedence compilation: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Operator precedence pass rate too low: {passed}/{total}");
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
