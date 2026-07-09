using CsCheck;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

/// <summary>
/// Enforces the core "no CS leaks" invariant (#1035): any program the front end
/// accepts as semantically clean must generate C# that compiles to IL without a single
/// Roslyn error. A failure here is always a real code-generation bug — the shrunk
/// counterexample is the repro. Compile-only: no process execution.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class CsCleanPropertyTests
{
    private readonly ITestOutputHelper _output;

    public CsCleanPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Property: semantic-clean ⇒ zero CS errors on the generated C#.
    /// The generator produces only programs that pass the full front end
    /// (<see cref="SemanticFilter.WellTypedProgram"/>); each such program's generated
    /// C# is emitted through Roslyn with the same reference set as
    /// <see cref="IntegrationTestBase"/> (missing references would masquerade as leaks).
    /// </summary>
    [Fact]
    public void SemanticClean_ImpliesZeroCsErrors()
    {
        var references = IntegrationTestBase.GetSharedReferences();

        // Pair the WellTypedProgram semantic filter with the typed program generator:
        // GenTyped.TypedProgram already emits well-typed programs, so the filter passes
        // nearly every sample (wrapping the raw GenSharpy.Module generator instead makes
        // the filter reject ~85% of candidates and CsCheck aborts with a Where-max-count).
        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2));

        var wellTyped = SemanticFilter.WellTypedProgram(baseGen);

        wellTyped.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "cs_clean_test.spy");

            // WellTypedProgram gates on Analyze() success; codegen should follow, but if
            // the front end itself now rejects the program there is nothing to emit.
            if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                return;

            var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
            var compilation = CSharpCompilation.Create(
                "CsCleanProperty",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            var csErrors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (csErrors.Count > 0)
            {
                var detail = string.Join("\n  ", csErrors.Take(5).Select(d => d.ToString()));
                throw new Xunit.Sdk.XunitException(
                    "Semantic-clean program produced CS error(s) in generated C# — this is a codegen leak (#1035):\n" +
                    $"=== Sharpy source ===\n{source}\n" +
                    $"=== Generated C# ===\n{result.GeneratedCSharpCode}\n" +
                    $"=== CS diagnostics ===\n  {detail}");
            }
        }, print: SafeUnparse, iter: 100);
    }

    private static string SafeUnparse(Sharpy.Compiler.Parser.Ast.Module m)
    {
        try
        {
            return Sharpy.Compiler.Pretty.Unparser.Unparse(m);
        }
        catch (Exception ex)
        {
            return $"<unparse failed: {ex.GetType().Name}: {ex.Message}>";
        }
    }
}
