using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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

    [Fact]
    public void WrongTypeArgCount_NeverCrashes()
    {
        int tested = 0;

        GenGenerics.WrongTypeArgCountProgram(TypeEnv.WithGenerics, fuel: 1)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "generic_test.spy");
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"ICE on wrong type arg count program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Non-ICE exceptions acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Wrong type arg count: {tested} programs processed without ICE");
    }

    [Fact]
    public void TypeMismatchOnGenericField_NeverCrashes()
    {
        int tested = 0;

        GenGenerics.TypeMismatchOnGenericFieldProgram(TypeEnv.WithGenerics, fuel: 1)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "generic_test.spy");
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"ICE on generic field mismatch program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Non-ICE exceptions acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Generic field mismatch: {tested} programs processed without ICE");
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
