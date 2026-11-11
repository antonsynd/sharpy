using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

public partial class CiFilterCoverageConformanceTests
{
    /// <summary>
    /// The test projects other than this one that must be found calling a python oracle seed, each with a step that runs
    /// it, so a scan that finds no project — or a workflow whose step stopped naming one — cannot pass. One line per
    /// project: <c>Sharpy.Core.Tests</c> holds <c>ExceptionHierarchyPythonMroTests</c>; P33's filesystem differential adds
    /// <c>Sharpy.Stdlib.Tests</c> here in the commit that lands its python twin.
    /// </summary>
    private static readonly string[] OracleProjectFloor =
    {
        "Sharpy.Core.Tests",
        "Sharpy.Stdlib.Tests",
    };

    /// <summary>The python oracle's entry points: every public static member of <see cref="PythonInterpreter"/> that
    /// returns an interpreter (<c>RequireOrSkip</c>, <c>TryLocate</c>, <c>Locate</c> and their overloads), read by
    /// reflection so a new seed is scanned without an edit here.</summary>
    private static List<MethodInfo> PythonOracleSeeds()
    {
        var seeds = typeof(PythonInterpreter).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(PythonInterpreter))
            .ToList();
        Assert.Contains(seeds, m => m.Name == nameof(PythonInterpreter.RequireOrSkip));
        Assert.Contains(seeds, m => m.Name == nameof(PythonInterpreter.TryLocate));
        Assert.Contains(seeds, m => m.Name == nameof(PythonInterpreter.Locate));
        return seeds;
    }

    /// <summary>
    /// The workflow steps judged per project: every <c>dotnet test</c> of a source root (other than this assembly, which
    /// <see cref="ScanPythonOracleClasses"/> judges per class) whose sources call a seed, a whole-solution run included.
    /// <c>Unrun</c> names a root that calls a seed and that no step runs — a test project CI never runs, or a shared helper
    /// in <c>Sharpy.TestInfrastructure</c> that this per-project reading cannot attribute.
    /// </summary>
    private (List<ProjectStep> Steps, List<string> Unrun) OracleProjectSteps()
    {
        var repoRoot = FindRepoRoot();
        var callers = ScanPythonOracleProjects(repoRoot);
        Assert.True(callers.ContainsKey(CompilerTestProject),
            $"The source scan found no seed call in {CompilerTestProject}, whose call graph reaches the oracle: the scan went blind.");
        foreach (var project in OracleProjectFloor)
        {
            Assert.True(callers.ContainsKey(project),
                $"The source scan found no python oracle seed call in {project} (floor): the scan went blind, or the twin lost its oracle.");
        }

        var all = ExtractTestSteps(File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "dotnet10.yml")));
        var steps = new List<ProjectStep>();
        var unrun = new List<string>();
        foreach (var (project, sites) in callers.Where(c => c.Key != CompilerTestProject).OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var running = all.Where(s => s.Project == project || s.Project == WholeSolution)
                .Select(s => new ProjectStep(project, s.Step))
                .ToList();
            if (running.Count == 0)
                unrun.Add($"{project} (calls at {string.Join(", ", sites.Take(3))})");
            steps.AddRange(running);
        }

        foreach (var project in OracleProjectFloor)
        {
            Assert.True(steps.Any(s => s.Project == project),
                $"No workflow step running {project} was judged (floor): the step was renamed or removed, or the extraction stopped reading it.");
        }

        return (steps, unrun);
    }

    /// <summary>Every source root <see cref="PythonOracleSkipCensusTests.TestSourceFiles"/> walks whose sources call a
    /// seed, with the calls (<c>file:line</c>).</summary>
    private static Dictionary<string, List<string>> ScanPythonOracleProjects(string repoRoot)
    {
        var seedNames = PythonOracleSeeds().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var src = Path.Combine(repoRoot, "src");
        var callers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in PythonOracleSkipCensusTests.TestSourceFiles(repoRoot))
        {
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
            foreach (var line in PythonOracleSeedCalls(File.ReadAllText(file), seedNames))
            {
                var project = relative[..relative.IndexOf('/')];
                if (!callers.TryGetValue(project, out var sites))
                    callers[project] = sites = new List<string>();
                sites.Add($"{relative}:{line}");
            }
        }

        return callers;
    }

    /// <summary>
    /// The lines of <paramref name="source"/> that reference a seed: <c>PythonInterpreter.Seed</c> however qualified or
    /// aliased (a call or a method group), and a bare <c>Seed</c> under <c>using static …PythonInterpreter</c>. A
    /// <c>nameof</c> operand names a seed without calling it; <c>TestPreconditions.RequireOrSkip</c> is another type's
    /// gate. Syntax only, so a doc-comment <c>cref</c> (trivia) is not read.
    /// </summary>
    private static List<int> PythonOracleSeedCalls(string source, IReadOnlySet<string> seedNames)
    {
        const string TypeName = nameof(PythonInterpreter);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        var typeNames = usings
            .Where(u => u.Alias != null && RightmostName(u.Name) == TypeName)
            .Select(u => u.Alias!.Name.Identifier.ValueText)
            .Append(TypeName)
            .ToHashSet(StringComparer.Ordinal);
        var bareSeeds = usings.Any(u => u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && RightmostName(u.Name) == TypeName);

        static bool InNameOf(SyntaxNode node) => node.Ancestors().OfType<InvocationExpressionSyntax>()
            .Any(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });

        var lines = new List<int>();
        foreach (var node in root.DescendantNodes())
        {
            var hit = node switch
            {
                MemberAccessExpressionSyntax member => seedNames.Contains(member.Name.Identifier.ValueText)
                    && RightmostName(member.Expression) is { } receiver && typeNames.Contains(receiver),
                IdentifierNameSyntax id => bareSeeds && seedNames.Contains(id.Identifier.ValueText)
                    && !(id.Parent is MemberAccessExpressionSyntax parent && parent.Name == id),
                _ => false,
            };
            if (hit && !InNameOf(node))
                lines.Add(node.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }

        return lines;
    }

    private static string? RightmostName(SyntaxNode? node) => node switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The seed scan over one source per spelling: each route to a seed is found, and each look-alike is not.
    /// </summary>
    [Theory]
    [InlineData("class T { void M() { PythonInterpreter.RequireOrSkip(12); } }", true)]
    [InlineData("class T { void M() { var p = Sharpy.TestInfrastructure.Oracles.PythonInterpreter.TryLocate(); } }", true)]
    [InlineData("class T { void M() { var p = global::Sharpy.TestInfrastructure.Oracles.PythonInterpreter.Locate(); } }", true)]
    [InlineData("class T { System.Func<int, PythonInterpreter?> F = PythonInterpreter.Locate; }", true)]
    [InlineData("using Py = Sharpy.TestInfrastructure.Oracles.PythonInterpreter;\nclass T { void M() { Py.RequireOrSkip(); } }", true)]
    [InlineData("using static Sharpy.TestInfrastructure.Oracles.PythonInterpreter;\nclass T { void M() { var p = TryLocate(); } }", true)]
    [InlineData("class T { void M() { TestPreconditions.RequireOrSkip(true, \"no asset\"); } }", false)]
    [InlineData("class T { string N = nameof(PythonInterpreter.RequireOrSkip); }", false)]
    [InlineData("class T { void M() { var p = TryLocate(); } }", false)]
    [InlineData("class T { void M() { var f = PythonInterpreter.FindRepoFile(\"x\"); } }", false)]
    [InlineData("/// <see cref=\"PythonInterpreter.RequireOrSkip(int)\"/>\nclass T { }", false)]
    public void PythonOracleSeedCalls_FindsEachRouteAndNoLookAlike(string source, bool calls)
    {
        var seedNames = PythonOracleSeeds().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(calls, PythonOracleSeedCalls(source, seedNames).Count > 0);
    }
}
