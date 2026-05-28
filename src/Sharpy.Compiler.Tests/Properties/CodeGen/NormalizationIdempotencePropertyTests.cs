using CsCheck;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class NormalizationIdempotencePropertyTests
{
    private readonly ITestOutputHelper _output;

    public NormalizationIdempotencePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NormalizeWhitespace_IsFixpoint()
    {
        int total = 0;
        int passed = 0;

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Compile(source, "idempotence_test.spy");

                if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                    return;

                var tree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
                var normalized1 = tree.GetRoot().NormalizeWhitespace().ToFullString();
                var tree2 = CSharpSyntaxTree.ParseText(normalized1);
                var normalized2 = tree2.GetRoot().NormalizeWhitespace().ToFullString();

                if (normalized1 != normalized2)
                    throw new Exception(
                        $"NormalizeWhitespace is not a fixpoint!\n" +
                        $"First normalization length: {normalized1.Length}\n" +
                        $"Second normalization length: {normalized2.Length}");

                Interlocked.Increment(ref passed);
            }
            catch (Exception ex) when (!ex.Message.StartsWith("NormalizeWhitespace", StringComparison.Ordinal))
            {
                // Compilation failures are expected for random programs
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Normalization idempotence: {passed}/{total} passed");
    }
}
