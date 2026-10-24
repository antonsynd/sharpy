using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class DeterminismPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DeterminismPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compilation_IsDeterministic()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            // Two full compiles agree on success, every diagnostic and the C# (a throw fails the sample).
            var first = PropertyAnalysis.CompileFingerprint(source);
            var second = PropertyAnalysis.CompileFingerprint(source);
            if (first != second)
                throw new Exception($"Non-deterministic compilation of:\n{source}\nrun 1: {first}\nrun 2: {second}");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    [Fact]
    public void TypedCompilation_ProducesIdenticalDiagnostics()
    {
        var gen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var result1 = CompileFull(source);
            var result2 = CompileFull(source);

            var diags1 = result1.diagnostics;
            var diags2 = result2.diagnostics;

            if (diags1.Count != diags2.Count)
                throw new Exception(
                    $"Diagnostic count differs: {diags1.Count} vs {diags2.Count}\n{source}");

            for (int i = 0; i < diags1.Count; i++)
            {
                if (diags1[i].Code != diags2[i].Code ||
                    diags1[i].Message != diags2[i].Message ||
                    diags1[i].Line != diags2[i].Line ||
                    diags1[i].Column != diags2[i].Column)
                {
                    throw new Exception(
                        $"Diagnostic {i} differs:\n  Run 1: {diags1[i]}\n  Run 2: {diags2[i]}\n{source}");
                }
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    [Fact]
    public void TypedCompilation_ProducesIdenticalCSharp()
    {
        var sink = new RejectionSink("Determinism.TypedCompilation_ProducesIdenticalCSharp");
        var gen = SemanticFilter.CompilableProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)), sink);

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var result1 = CompileFull(source);
            var result2 = CompileFull(source);

            // The filter admitted the sample, so both runs must produce C# — a run without it (a
            // throw or a refusal) is a failure, not a skipped comparison (#2288).
            if (result1.csharp is null || result2.csharp is null)
            {
                throw new Exception($"An admitted program produced no C# in one of two runs:\n{source}");
            }

            if (result1.csharp != result2.csharp)
            {
                throw new Exception(
                    $"Generated C# differs between runs:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    // A throw propagates and fails the sample: these used to return a sentinel, so two runs that
    // both threw compared equal and an admitted program that threw was never compared (#2288).
    private static (List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diagnostics, string? csharp) CompileFull(string source)
    {
        var compiler = new Sharpy.Compiler.Compiler();
        var result = compiler.Compile(source, "test.spy");
        return (result.Diagnostics?.GetAll().ToList() ?? new(), result.GeneratedCSharpCode);
    }
}
