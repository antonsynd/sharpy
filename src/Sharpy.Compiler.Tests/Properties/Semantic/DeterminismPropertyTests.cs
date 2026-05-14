using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
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

            var result1 = Compile(source);
            var result2 = Compile(source);

            if (result1.diagCount != result2.diagCount)
                throw new Exception(
                    $"Non-deterministic diagnostic count: {result1.diagCount} vs {result2.diagCount}");

            if (result1.success != result2.success)
                throw new Exception(
                    $"Non-deterministic compilation: {result1.success} vs {result2.success}");
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
        var gen = SemanticFilter.CompilableProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var result1 = CompileFull(source);
            var result2 = CompileFull(source);

            if (result1.csharp != null && result2.csharp != null &&
                result1.csharp != result2.csharp)
            {
                throw new Exception(
                    $"Generated C# differs between runs:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    private static (bool success, int diagCount) Compile(string source)
    {
        try
        {
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "test.spy");
            return (result.Success, result.Diagnostics?.ErrorCount ?? 0);
        }
        catch
        {
            return (false, -1);
        }
    }

    private static (bool success, List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diagnostics, string? csharp) CompileFull(string source)
    {
        try
        {
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "test.spy");
            return (result.Success, result.Diagnostics?.GetAll().ToList() ?? new(), result.GeneratedCSharpCode);
        }
        catch
        {
            return (false, new(), null);
        }
    }
}
