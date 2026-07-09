using CsCheck;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class ILCompilesPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ILCompilesPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// For every generated program that compiles through the front end, emit the
    /// generated C# all the way to IL (<c>compilation.Emit</c>) and assert Roslyn
    /// reports no errors. This test previously only checked front-end
    /// <c>Compile().Success</c> and never invoked Roslyn Emit, so its name did not
    /// match its behaviour; it now actually validates IL emission.
    /// </summary>
    [Fact]
    public void GeneratedCSharp_CompilesToValidIL()
    {
        var references = IntegrationTestBase.GetSharedReferences();
        int total = 0;
        int emitted = 0;

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "il_test.spy");

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                return;

            Interlocked.Increment(ref total);

            var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
            var compilation = CSharpCompilation.Create(
                "ILCompilesProperty",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errors.Count > 0)
            {
                var detail = string.Join("\n  ", errors.Take(5).Select(d => d.ToString()));
                throw new Xunit.Sdk.XunitException(
                    "Generated C# failed to emit to IL (#1035):\n" +
                    $"=== Sharpy source ===\n{source}\n" +
                    $"=== Generated C# ===\n{result.GeneratedCSharpCode}\n" +
                    $"=== CS diagnostics ===\n  {detail}");
            }

            Interlocked.Increment(ref emitted);
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"IL emission: {emitted}/{total} emitted to valid IL");
    }
}
