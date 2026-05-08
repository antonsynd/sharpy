using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
public class TypedAnalyzePropertyTests
{
    private readonly ITestOutputHelper _output;

    public TypedAnalyzePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TypedProgram_AnalyzesClean()
    {
        int total = 0;
        int passed = 0;

        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
        {
            var env = new TypeEnv()
                .WithBinding("x", "int")
                .WithBinding("s", "str")
                .WithBinding("flag", "bool");
            return GenTyped.TypedProgram(env, type, fuel: 2);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "typed_test.spy");

                if (result.Success)
                    Interlocked.Increment(ref passed);
            }
            catch
            {
                // Swallow — focus on pass rate, not crashes
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);

        _output.WriteLine($"Typed analysis: {passed}/{total} analyzed clean");
        Assert.True(passed > total / 2,
            $"Typed analysis pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void TypedProgram_NeverThrowsInternalError()
    {
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
        {
            var env = new TypeEnv();
            return GenTyped.TypedProgram(env, type, fuel: 3);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                compiler.Analyze(source, "typed_test.spy");
            }
            catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
            {
                throw new Exception(
                    $"InternalCompilerErrorException on typed program:\n{source}\n{ex.Message}");
            }
            catch
            {
                // Other exceptions are acceptable for generated code
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }
}
