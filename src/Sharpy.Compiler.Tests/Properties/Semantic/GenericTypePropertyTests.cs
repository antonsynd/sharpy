using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
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

        GenGenerics.GenericClassProgram(TypeEnv.Default, fuel: 2)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Generic class substitution: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic class pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void GenericFunction_InfersTypeArgument()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericFunctionProgram(TypeEnv.Default, fuel: 2)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Generic function inference: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic function pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void MultipleTypeParams_SubstituteIndependently()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.MultiTypeParamProgram(TypeEnv.Default, fuel: 2)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Multi-type param substitution: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Multi-type param pass rate too low: {passed}/{tested}");
    }

    [Fact]
    public void GenericInstantiation_ProducesCorrectCSharp()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericClassProgram(TypeEnv.Default, fuel: 2)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Generic C# output: {passed}/{tested} produced correct generic syntax");
        Assert.True(passed > tested / 2,
            $"Generic C# output rate too low: {passed}/{tested}");
    }

    [Fact]
    public void WrongTypeArgCount_NeverCrashes()
    {
        // Note: Sharpy currently accepts extra type arguments without error.
        // This test documents that behavior and verifies no crashes occur.
        int tested = 0;

        GenGenerics.WrongTypeArgCountProgram(TypeEnv.Default, fuel: 1)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Wrong type arg count: {tested} programs processed without ICE");
    }

    [Fact]
    public void TypeMismatchOnGenericField_NeverCrashes()
    {
        // Note: Sharpy currently accepts field reassignment with wrong generic type.
        // This test documents that behavior and verifies no crashes occur.
        int tested = 0;

        GenGenerics.TypeMismatchOnGenericFieldProgram(TypeEnv.Default, fuel: 1)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Generic field mismatch: {tested} programs processed without ICE");
    }

    [Fact]
    public void GenericWithInheritance_CompilesClean()
    {
        int tested = 0;
        int passed = 0;

        GenGenerics.GenericWithInheritanceProgram(TypeEnv.Default, fuel: 2)
            .Sample(source =>
            {
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
            }, iter: 50);

        _output.WriteLine($"Generic inheritance compilation: {passed}/{tested} passed");
        Assert.True(passed > tested / 2,
            $"Generic inheritance pass rate too low: {passed}/{tested}");
    }
}
