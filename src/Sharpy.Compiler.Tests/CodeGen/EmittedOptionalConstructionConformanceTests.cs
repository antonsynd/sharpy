using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Guards the strict-Optional contract (#1720): emitted C# must never rely on the
/// <c>implicit operator Optional&lt;T&gt;(T value)</c> that <c>Sharpy.Core/Optional.cs</c>
/// retains for C# interop consumers. If the emitter produces <c>Optional&lt;int&gt; x = 42;</c>
/// instead of <c>Optional&lt;int&gt;.Some(42)</c>, this test catches it via Roslyn's
/// semantic-model conversion analysis over the full <c>.expected.cs</c> snapshot corpus.
/// </summary>
[Trait("Category", "Conformance")]
[Collection("HeavyCompilation")]
public class EmittedOptionalConstructionConformanceTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    public EmittedOptionalConstructionConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SnapshotCorpus_NoImplicitConversionToOptional()
    {
        var snapshotFiles = Directory.GetFiles(FixturesRoot, "*.expected.cs", SearchOption.AllDirectories);
        Assert.True(snapshotFiles.Length > 0,
            $"No .expected.cs files found under {FixturesRoot} — corpus is empty, guard is vacuous");

        var references = IntegrationTestBase.GetSharedReferences();
        var violations = new List<string>();
        int scannedFiles = 0;
        int scannedExpressions = 0;

        foreach (var file in snapshotFiles.OrderBy(f => f, StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: file);

            var compilation = CSharpCompilation.Create(
                "OptionalConformanceScan",
                new[] { tree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                    .WithNullableContextOptions(NullableContextOptions.Enable));

            var model = compilation.GetSemanticModel(tree);
            scannedFiles++;

            foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
            {
                scannedExpressions++;
                if (IsImplicitOptionalConversion(model, expr))
                {
                    var span = expr.GetLocation().GetLineSpan();
                    var relativePath = Path.GetRelativePath(FixturesRoot, file);
                    var text = expr.ToString().Replace("\n", " ");
                    if (text.Length > 80)
                        text = text[..80] + "...";
                    violations.Add(
                        $"{relativePath}:{span.StartLinePosition.Line + 1} " +
                        $"implicit T→Optional<T> on: {text}");
                }
            }
        }

        _output.WriteLine(
            $"Scanned {scannedFiles} snapshot file(s), {scannedExpressions} expression node(s).");

        Assert.True(violations.Count == 0,
            $"Emitted C# relies on Optional<T>'s implicit operator in {violations.Count} place(s) " +
            $"— use Optional<T>.Some(v) or Optional<T>.None instead (#1720):\n" +
            string.Join("\n", violations.Take(25)));
    }

    [Fact]
    public void PositiveControl_ImplicitConversionIsDetected()
    {
        var references = IntegrationTestBase.GetSharedReferences();

        const string controlSource = @"
using Sharpy;

public static class PositiveControl
{
    public static void Main()
    {
        Optional<int> x = 1;
    }
}
";
        var tree = CSharpSyntaxTree.ParseText(
            controlSource,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "PositiveControl.cs");

        var compilation = CSharpCompilation.Create(
            "OptionalConformanceControl",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var model = compilation.GetSemanticModel(tree);

        bool found = false;
        foreach (var expr in tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (IsImplicitOptionalConversion(model, expr))
            {
                found = true;
                _output.WriteLine($"Positive control detected implicit conversion on: {expr}");
                break;
            }
        }

        Assert.True(found,
            "Positive control failed: `Optional<int> x = 1;` was NOT detected as an implicit " +
            "user-defined conversion to Optional<T>. The scan infrastructure is broken — it would " +
            "pass vacuously on the corpus.");
    }

    private static bool IsImplicitOptionalConversion(SemanticModel model, ExpressionSyntax expr)
    {
        var conversion = model.GetConversion(expr);
        if (!conversion.IsImplicit || !conversion.IsUserDefined)
            return false;

        var method = conversion.MethodSymbol;
        if (method is null)
            return false;

        var containingType = method.ContainingType;
        if (containingType is null)
            return false;

        return containingType.Name == "Optional"
            && containingType.ContainingNamespace?.Name == "Sharpy"
            && containingType.IsGenericType;
    }
}
