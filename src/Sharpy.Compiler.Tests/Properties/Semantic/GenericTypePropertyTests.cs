using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class GenericTypePropertyTests
{
    private readonly ITestOutputHelper _output;

    public GenericTypePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenericClass_SubstitutesTypeParameter()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericClassProgram(TypeEnv.WithGenerics, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "generic_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic class substitution: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic class pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void GenericFunction_InfersTypeArgument()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "generic_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic function inference: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic function pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void MultipleTypeParams_SubstituteIndependently()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.MultiTypeParamProgram(TypeEnv.WithGenerics, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "generic_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Multi-type param substitution: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Multi-type param pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void GenericInstantiation_ProducesCorrectCSharp()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericClassProgram(TypeEnv.WithGenerics, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Compile(source, "generic_test.spy");
                    if (result.Success && result.GeneratedCSharpCode != null
                        && result.GeneratedCSharpCode.Contains("Box<"))
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic C# output: {passed}/{tested} produced correct generic syntax");
        Assert.True(passed > tested / 2,
            $"Generic C# output rate too low: {passed}/{tested}");
    }

    [Fact]
    public void GenericTypeConstraint_IsEnforced()
    {
        int tested = 0;
        int constrained = 0;

        GenGenerics.GenericConstraintProgram(TypeEnv.WithGenerics, fuel: 1)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "generic_test.spy");
                    if (result.Success || result.Diagnostics.GetAll().Any(
                        d => d.Code.StartsWith("SPY0")))
                        Interlocked.Increment(ref constrained);
                }
                catch
                {
                    Interlocked.Increment(ref constrained);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic constraint handling: {constrained}/{tested} handled");
        Assert.True(constrained > tested / 2,
            $"Generic constraint handling rate too low: {constrained}/{tested}");
    }

    /// <summary>
    /// <c>Box[int, str](…)</c> on a one-parameter generic class draws exactly SPY0224 on every
    /// sample (#2288; <c>type_parameter_defaults.md</c> documents the refusal). Before #2288 the lane
    /// asserted only that analysis did not throw an internal error.
    /// </summary>
    [Fact]
    public void WrongTypeArgCount_IsRejected()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenGenerics.WrongTypeArgCountProgram(TypeEnv.WithGenerics, fuel: 1),
            source => ErrorInjector.JudgeSource(source, "SPY0224", CompilerDiagnosticSeverity.Error, null,
                "wrong type-argument count"), iter: 50);
        _output.WriteLine($"Wrong type arg count: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0224");

        Assert.True(verdicts.Tested > 0, "No wrong type-argument count programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} wrong type-argument count programs misjudged; the first:\n{verdicts.First}");
    }

    /// <summary>
    /// Assigning a <c>str</c>/<c>bool</c> literal to the <c>T</c> field of a <c>Box[int]</c> draws
    /// exactly SPY0220 on every sample (#2288). Before #2288 the lane asserted only that analysis did
    /// not throw an internal error.
    /// </summary>
    [Fact]
    public void TypeMismatchOnGenericField_IsRejected()
    {
        var verdicts = ErrorInjector.JudgeSamples(
            GenGenerics.TypeMismatchOnGenericFieldProgram(TypeEnv.WithGenerics, fuel: 1),
            source => ErrorInjector.JudgeSource(source, "SPY0220", CompilerDiagnosticSeverity.Error, null,
                "type mismatch on a generic field"), iter: 50);
        _output.WriteLine($"Generic field mismatch: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0220");

        Assert.True(verdicts.Tested > 0, "No generic field mismatch programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} generic field mismatch programs misjudged; the first:\n{verdicts.First}");
    }

    [Fact]
    public void GenericWithInheritance_CompilesClean()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericWithInheritanceProgram(TypeEnv.WithGenerics, fuel: 2)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "generic_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic inheritance compilation: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic inheritance pass rate too low: {passed}/{tested}");
    }
}
