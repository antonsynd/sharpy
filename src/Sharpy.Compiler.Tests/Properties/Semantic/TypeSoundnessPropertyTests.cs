using CsCheck;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class TypeSoundnessPropertyTests : IntegrationTestBase
{
    public TypeSoundnessPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TypedProgram_ExecutesWithoutRuntimeTypeErrors()
    {
        var errors = new List<string>();

        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true));

        var sink = new RejectionSink("TypeSoundness.TypedProgram_ExecutesWithoutRuntimeTypeErrors");
        var compilable = SemanticFilter.CompilableProgram(baseGen, sink);

        int total = 0;
        compilable.Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var result = CompileAndExecuteWithGC(source);

            // The sink admitted the program, so ANY failed run is a finding — a runtime throw, a
            // Roslyn emit failure (empty stderr) or a Sharpy runtime error — never a sample filtered
            // by the exception's name (#2288 verify: four .NET names admitted every other failure).
            if (!result.Success)
            {
                lock (errors)
                    errors.Add($"Compiled program failed to run: {string.Join("; ", result.CompilationErrors)} {result.Exception?.Message} {result.StandardError}\nSource:\n{source}");
            }
        }, print: m => m == null ? "<null>" : Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 15);

        Output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Output.WriteLine($"Type soundness: {total} programs executed, {errors.Count} failed runs");
        Assert.Empty(errors);
    }

    [Fact]
    public void TypedProgram_WithCollections_ExecutesWithoutRuntimeTypeErrors()
    {
        var errors = new List<string>();

        var baseGen = Gen.OneOfConst("int", "str", "list[int]").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2, withStatements: true));

        var sink = new RejectionSink("TypeSoundness.TypedProgram_WithCollections_ExecutesWithoutRuntimeTypeErrors");
        var compilable = SemanticFilter.CompilableProgram(baseGen, sink);

        int total = 0;
        compilable.Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var result = CompileAndExecuteWithGC(source);

            // The sink admitted the program, so ANY failed run is a finding — a runtime throw, a
            // Roslyn emit failure (empty stderr) or a Sharpy runtime error — never a sample filtered
            // by the exception's name (#2288 verify: four .NET names admitted every other failure).
            if (!result.Success)
            {
                lock (errors)
                    errors.Add($"Compiled program failed to run: {string.Join("; ", result.CompilationErrors)} {result.Exception?.Message} {result.StandardError}\nSource:\n{source}");
            }
        }, print: m => m == null ? "<null>" : Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 15);

        Output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Output.WriteLine($"Type soundness (collections): {total} programs executed, {errors.Count} failed runs");
        Assert.Empty(errors);
    }

    [Fact]
    public void TypedProgram_WithOptionals_AnalyzesCleanly()
    {
        var errors = new List<string>();

        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2));

        var sink = new RejectionSink("TypeSoundness.TypedProgram_WithOptionals_AnalyzesCleanly");
        var wellTyped = SemanticFilter.WellTypedProgram(baseGen, sink);

        int total = 0;
        wellTyped.Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "optional_test.spy");

            if (!result.Success)
            {
                lock (errors)
                    errors.Add($"Analysis failed:\n{source}\nDiagnostics: " +
                        string.Join("; ", result.Diagnostics?.GetAll().Select(d => d.Message) ?? Array.Empty<string>()));
            }
        }, print: m => m == null ? "<null>" : Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        Output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Output.WriteLine($"Type soundness (optionals): {total} programs analyzed, {errors.Count} failures");
        Assert.Empty(errors);
    }

    [Fact]
    public void FilteredTypedProgram_ProducesExpectedTypeOutput()
    {
        var errors = new List<string>();

        var sink = new RejectionSink("TypeSoundness.FilteredTypedProgram_ProducesExpectedTypeOutput");
        Gen.OneOfConst("int", "str", "bool").Sample(resultType =>
        {
            var gen = SemanticFilter.CompilableProgram(
                GenTyped.TypedProgram(TypeEnv.Default, resultType, fuel: 2), sink);

            gen.Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var result = CompileAndExecuteWithGC(source);

                // The filter already proved the program compiles, so a failed run is a runtime throw
                // of a well-typed generated program — a finding, never a sample to skip (#2288).
                if (!result.Success)
                {
                    lock (errors)
                        errors.Add($"Compiled {resultType} program failed to run: {string.Join("; ", result.CompilationErrors)} {result.Exception?.Message} {result.StandardError}\nSource:\n{source}");
                    return;
                }

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
            }, print: m => m == null ? "<null>" : Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 15);
        }, iter: 3);

        Output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Output.WriteLine($"Output type validation: {errors.Count} mismatches");
        Assert.Empty(errors);
    }
}
