using System.Collections.Concurrent;
using CsCheck;
using FluentAssertions;
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

        int examined = 0;
        var analyzedButNotCompiled = new ConcurrentBag<string>();

        wellTyped.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "cs_clean_test.spy");

            // WellTypedProgram gates on Analyze(); this calls Compile(). The gap between them is
            // codegen — so a sample landing here is a program semantic analysis ACCEPTED and
            // compilation then rejected, which is a defect of the same family this test hunts.
            // It used to `return` silently, which meant the interesting samples were the ones the
            // suite discarded (#1432's sibling). Collected and asserted below instead.
            if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
            {
                var first = result.Diagnostics.GetErrors().FirstOrDefault();
                analyzedButNotCompiled.Add(
                    $"{(first is null ? "(no error diagnostic)" : $"{first.Code}: {first.Message}")}\n{source}");
                return;
            }

            Interlocked.Increment(ref examined);

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

        _output.WriteLine($"Examined {examined} semantic-clean program(s) through Roslyn emit "
            + $"({analyzedButNotCompiled.Count} analyzed but did not compile).");

        // Same floor, same reason as ILCompilesPropertyTests: with every sample skipped, "no CS
        // leaks found" and "nothing was looked at" are indistinguishable greens (#1432).
        examined.Should().BeGreaterThanOrEqualTo(MinimumExaminedSamples,
            "a skipped sample witnesses nothing; this suite is one of the two the leak corpus "
            + "leans on, so an empty corpus must fail rather than pass");

        analyzedButNotCompiled.Should().BeEmpty(
            "semantic analysis accepted these programs and compilation rejected them — the gap "
            + "between the two is code generation");
    }

    /// <summary>
    /// Minimum number of semantic-clean programs that must actually reach Roslyn emit. Set well
    /// below the observed rate: the generator is random, so a floor next to the measured value
    /// would flake, while any collapse toward the vacuous 0 fails loudly.
    /// </summary>
    private const int MinimumExaminedSamples = 10;

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
