using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class GenericTypePropertyTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public GenericTypePropertyTests(ITestOutputHelper output) : base(output)
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

    /// <summary>
    /// The constraint lane's positive control (#2288, P63 Phase 4): every
    /// <c>GenGenerics.GenericConstraintProgram</c> sample — a user interface constraint
    /// <c>T: Ordered[T]</c> reached at an inferred call and at an explicit instantiation — analyses
    /// clean, and a representative (<c>ConstraintModule(3, 7, 5)</c>) RUNS and prints <c>7</c> then
    /// <c>5</c>: <c>biggest</c> really dispatched through the constrained <c>less</c> (recorded is not
    /// applied). The refusals over the same generator are in <c>ErrorCompletenessPropertyTests</c>
    /// (SPY0237 at an inferred call; the explicit instantiation pinned to #2212). Before #2288 every
    /// sample imported <c>collections.Comparable</c> — no such module in the property host (SPY0300) —
    /// and the lane counted any <c>SPY0</c> code, or a throw, as "handled".
    /// </summary>
    [Fact]
    public void GenericTypeConstraint_IsEnforced()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenGenerics.GenericConstraintProgram(TypeEnv.WithGenerics, fuel: 1),
            source => ErrorInjector.JudgeClean(source, "constrained generic program"), iter: 50);
        _output.WriteLine($"Generic constraint (valid): {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed clean");
        Assert.True(verdicts.Tested > 0, "No constrained generic programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} constrained generic programs refused; the first:\n{verdicts.First}");

        var source = Sharpy.Compiler.Pretty.Unparser.Unparse(GenGenerics.ConstraintModule(3, 7, 5));
        var run = CompileAndExecute(source, "generic_constraint.spy");
        _output.WriteLine($"Generic constraint (run): success={run.Success}, stdout={run.StandardOutput.Replace("\n", "\\n")}");
        Assert.True(run.Success,
            $"The constrained generic program must run:\n{source}\n{string.Join("\n", run.CompilationErrors)}\n{run.StandardError}");
        Assert.Equal("7\n5\n", run.StandardOutput);
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
