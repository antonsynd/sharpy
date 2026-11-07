using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

public partial class NameResolutionDifferentialTests
{
    /// <summary>Who may name the harness's runner, besides the helper the sweep goes through. Each of these judges one
    /// program of its own and is a fact (the last is the helper's real-hang fact, through the stub's seat).</summary>
    private static readonly HashSet<string> RunSharpyNamers = new(StringComparer.Ordinal)
    {
        "RunSharpyRetryingATimeout",
        "RunSharpy_BuffersNothingInTheTestResult",
        "ImportedOuter_CompilesInProcess",
        "Classifier_AThrowingProgram_IsRuntimeThrow",
        "Classifier_AGeneratedCSharpFailure_IsIce",
        "Classifier_ATimedOutProgram_IsHarness",
        "BinderSiteRoster_StoreOuter_IsTotalOverBindingKind",
        "Retry_ARealHang_StaysHarness",
    };

    /// <summary>Who may reach the compile path without <c>RunSharpy</c>: <c>RunSharpy</c> itself, and the fact lifting its mute.</summary>
    private static readonly HashSet<string> CompilePathNamers = new(StringComparer.Ordinal)
    {
        "RunSharpy",
        "RunSharpy_BuffersNothingInTheTestResult",
    };

    /// <summary>D4.3 and the reduced-run decision are facts about two methods, and the sweep is what they are for. So this
    /// reads the harness's source: the sweep's loop stores a run only through <c>RunSharpyRetryingATimeout</c> or the
    /// cache, nothing else names the runner or the compile path but the closed lists above, and the sweep feeds
    /// <see cref="ReducedRunFailures"/> the environment's <c>CI</c>. The floors are literals, so a scan that reads
    /// nothing is red.</summary>
    [Fact]
    public void TheSweepsLoop_ReachesSharpyOnlyThroughTheRetryingHelper()
    {
        var allowlist = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName)
            ?? throw new FileNotFoundException(AllowlistFileName);
        var roots = Directory.GetFiles(Path.GetDirectoryName(allowlist)!, "NameResolutionDifferentialTests*.cs")
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot())
            .ToList();
        Assert.True(roots.Count >= 10, $"Read {roots.Count} of the harness's source files.");

        var problems = new List<string>();
        var runnerMentions = 0;
        foreach (var id in roots.SelectMany(r => r.DescendantNodes().OfType<IdentifierNameSyntax>()))
        {
            var name = id.Identifier.ValueText;
            var isRunner = name == "RunSharpy";
            if (!isRunner && !name.StartsWith("CompileAndExecute", StringComparison.Ordinal))
                continue;

            var member = id.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "(not a method)";
            runnerMentions += isRunner ? 1 : 0;
            if (!(isRunner ? RunSharpyNamers : CompilePathNamers).Contains(member))
                problems.Add($"{member} names {name} (line {id.GetLocation().GetLineSpan().StartLinePosition.Line + 1})");
        }
        Assert.True(runnerMentions >= RunSharpyNamers.Count, $"Found {runnerMentions} mentions of the runner.");

        var sweep = roots.SelectMany(r => r.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(m => m.Identifier.ValueText == nameof(NameResolutionDifferential_Sweep));
        var stores = sweep.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Left is ElementAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "runs" } })
            .ToList();
        var throughTheHelper = stores
            .Where(s => s.Right is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: nameof(RunSharpyRetryingATimeout) } })
            .ToList();
        if (throughTheHelper.Count != 1 || !throughTheHelper[0].Ancestors().Any(a => a is ForStatementSyntax))
            problems.Add($"the sweep's loop stores a run through {nameof(RunSharpyRetryingATimeout)} {throughTheHelper.Count} time(s)");
        foreach (var other in stores.Except(throughTheHelper).Where(s => s.Right is not IdentifierNameSyntax { Identifier.ValueText: "hit" }))
            problems.Add($"the sweep stores a run from '{other.Right}'");

        var reducedRun = sweep.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: nameof(ReducedRunFailures) })
            .Select(i => i.Parent?.Parent?.Parent?.ToString())
            .ToList();
        if (!reducedRun.Contains("failures.AddRange(ReducedRunFailures(Environment.GetEnvironmentVariable(\"CI\"), partial, limit, cachePath))"))
            problems.Add($"the sweep does not add {nameof(ReducedRunFailures)} over the environment's CI to its failures");

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
