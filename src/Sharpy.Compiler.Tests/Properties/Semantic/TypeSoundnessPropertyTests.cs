using CsCheck;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
public class TypeSoundnessPropertyTests : IntegrationTestBase
{
    public TypeSoundnessPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TypedProgram_ExecutesWithoutRuntimeTypeErrors()
    {
        var errors = new List<string>();

        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true));

        var compilable = SemanticFilter.CompilableProgram(baseGen);

        int total = 0;
        compilable.Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var result = CompileAndExecute(source);

            if (!result.Success)
            {
                var stderr = result.StandardError ?? "";
                if (stderr.Contains("InvalidCastException") ||
                    stderr.Contains("NullReferenceException") ||
                    stderr.Contains("InvalidOperationException") ||
                    stderr.Contains("ArrayTypeMismatchException"))
                {
                    lock (errors)
                        errors.Add($"Runtime type error:\n{source}\nStderr: {stderr}");
                }
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        Output.WriteLine($"Type soundness: {total} programs executed, {errors.Count} runtime type errors");
        Assert.Empty(errors);
    }

    [Fact]
    public void FilteredTypedProgram_ProducesExpectedTypeOutput()
    {
        var errors = new List<string>();

        Gen.OneOfConst("int", "str", "bool").Sample(resultType =>
        {
            var gen = SemanticFilter.CompilableProgram(
                GenTyped.TypedProgram(TypeEnv.Default, resultType, fuel: 2));

            gen.Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var result = CompileAndExecute(source);

                if (!result.Success)
                    return;

                var output = result.StandardOutput.TrimEnd();
                var valid = resultType switch
                {
                    "int" => int.TryParse(output, out _) || long.TryParse(output, out _),
                    "str" => true,
                    "bool" => output is "True" or "False",
                    _ => true
                };

                if (!valid)
                {
                    lock (errors)
                        errors.Add($"Expected {resultType} output, got: '{output}'\nSource:\n{source}");
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 15);
        }, iter: 3);

        Output.WriteLine($"Output type validation: {errors.Count} mismatches");
        Assert.Empty(errors);
    }
}
