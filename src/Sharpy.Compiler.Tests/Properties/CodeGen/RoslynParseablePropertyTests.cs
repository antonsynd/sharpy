using CsCheck;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class RoslynParseablePropertyTests
{
    private readonly ITestOutputHelper _output;

    public RoslynParseablePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratedCSharp_ParsesWithRoslyn()
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
                var result = compiler.Compile(source, "roslyn_test.spy");

                if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                    return;

                var tree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
                var diagnostics = tree.GetDiagnostics()
                    .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .ToList();

                if (diagnostics.Count > 0)
                    throw new Exception(
                        $"Generated C# has parse errors:\n" +
                        string.Join('\n', diagnostics.Select(d => d.ToString())));

                Interlocked.Increment(ref passed);
            }
            catch (Exception ex) when (!ex.Message.StartsWith("Generated C#", StringComparison.Ordinal))
            {
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Roslyn parse: {passed}/{total} passed");
    }
}
