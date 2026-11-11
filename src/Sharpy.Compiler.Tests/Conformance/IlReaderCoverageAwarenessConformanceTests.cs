using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Every test source that reads method IL is aware of the coverage-on test mode (#2512): on a mainline push the
/// collector statically instruments the product assemblies (<c>build_tools/coverage.runsettings</c>), injecting a tracker
/// type and a hit probe into every body, so an IL roster over the running assembly reads IL that is not the shipped IL.
/// Each such reader must either see through the probe or skip under the detector, and both go through
/// <c>Sharpy.TestInfrastructure.CoverageInstrumentation</c>. The Stdlib exception-escape sweep was the fourth reflection or
/// IL roster to meet that run unaware (after the inlinability roster and the two static-state rosters).
///
/// <para><b>Population.</b> Every <c>*.cs</c> under <c>src/*Tests/</c> and <c>src/Sharpy.TestInfrastructure/</c>, outside
/// <c>bin</c> and <c>obj</c>. <b>Reader</b>: the file's syntax holds an invocation of a member named
/// <c>GetMethodBody</c> or <c>GetILAsByteArray</c> (the only routes to a body's IL in .NET reflection and
/// <c>System.Reflection.Metadata</c>'s <c>PEReader</c>). <b>Aware</b>: the file's syntax holds a member access
/// <c>CoverageInstrumentation.IsInjected</c> or <c>CoverageInstrumentation.IsInstrumented</c>. Both are read from the
/// Roslyn syntax tree, never from text, so a comment or a string literal satisfies neither. The check is per file: a
/// helper that reads IL is itself a reader and carries the check, so a caller of an aware helper needs none.</para>
///
/// <para><b>Exemptions</b> (<see cref="Exempt"/>): a reader whose IL comes only from assemblies the collector never
/// instruments, each named with those assemblies; the guard checks each against the include list of
/// <c>coverage.runsettings</c>, so an exemption whose reason stops holding is red, as is one that is no longer an
/// unaware reader.</para>
/// </summary>
public sealed class IlReaderCoverageAwarenessConformanceTests
{
    private static readonly HashSet<string> IlReadingMembers = new(StringComparer.Ordinal) { "GetMethodBody", "GetILAsByteArray" };
    private static readonly HashSet<string> DetectorMembers = new(StringComparer.Ordinal) { "IsInjected", "IsInstrumented" };
    private const string DetectorType = "CoverageInstrumentation";

    /// <summary>
    /// Unaware readers that need no check: repo-relative path → (reason, the assemblies whose IL the file reads).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Reason, string[] Assemblies)> Exempt =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            ["src/Sharpy.Compiler.Tests/Conformance/CiFilterCoverageConformanceTests.OracleReach.cs"] = (
                "the python-oracle call graph reads IL only of the test assemblies' own types (the executing assembly and "
                + "IntegrationTestBase's); product methods are resolved by token, never walked",
                new[] { "Sharpy.Compiler.Tests", "Sharpy.TestInfrastructure" }),
        };

    private readonly ITestOutputHelper _output;

    public IlReaderCoverageAwarenessConformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EveryTestSourceThatReadsIl_ChecksForCoverageInstrumentation()
    {
        var root = RepositoryRoot();
        var readers = new List<string>();
        var unaware = new List<string>();
        foreach (var path in PolicedSources(root))
        {
            var (reads, aware) = Classify(File.ReadAllText(path));
            if (!reads)
                continue;
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            readers.Add(relative);
            _output.WriteLine($"{(aware ? "aware  " : Exempt.ContainsKey(relative) ? "exempt " : "UNAWARE")} {relative}");
            if (!aware)
                unaware.Add(relative);
        }

        // Positive control on the real tree: the known aware readers are found, so the population is not vacuous.
        Assert.Contains("src/Sharpy.Core.Tests/ElementAccessInlinabilityConformanceTests.cs", readers);
        Assert.Contains("src/Sharpy.Stdlib.Tests/Conformance/ExceptionEscape/SideEffectRule.cs", readers);

        var unexempted = unaware.Where(p => !Exempt.ContainsKey(p)).ToList();
        var stale = Exempt.Keys.Where(p => !unaware.Contains(p)).ToList();
        Assert.True(unexempted.Count == 0 && stale.Count == 0,
            "Test sources reading method IL must handle the coverage-on run (#2512): see through the collector's probe "
            + "(CoverageInstrumentation.IsInjected on a reference's owner) or skip under CoverageInstrumentation.IsInstrumented, "
            + "with the IL held to the detector so a plain run cannot take the skip.\nUnaware readers:\n  "
            + string.Join("\n  ", unexempted)
            + "\nStale exemptions (no longer an unaware IL reader: delete the row):\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Each exemption's reason holds: no assembly it names is instrumented by <c>coverage.runsettings</c>.</summary>
    [Fact]
    public void EveryExemption_ReadsOnlyAssembliesTheCollectorNeverInstruments()
    {
        var includes = CoverageIncludes(RepositoryRoot());
        Assert.NotEmpty(includes);
        // Positive control: the include list does match a product assembly.
        Assert.Contains(includes, r => r.IsMatch("/bin/Sharpy.Core.dll"));
        foreach (var (path, (_, assemblies)) in Exempt)
        {
            Assert.NotEmpty(assemblies);
            foreach (var assembly in assemblies)
            {
                Assert.False(includes.Any(r => r.IsMatch($"/bin/{assembly}.dll")),
                    $"{path} is exempt because it reads IL only of {assembly}, but coverage.runsettings instruments {assembly}.dll");
            }
        }
    }

    [Theory]
    [InlineData("m.GetMethodBody()?.GetILAsByteArray();", true, false)]
    [InlineData("var b = m.GetMethodBody(); // CoverageInstrumentation.IsInjected(t)", true, false)]
    [InlineData("var b = m.GetMethodBody(); /* CoverageInstrumentation.IsInstrumented(a) */", true, false)]
    [InlineData("var b = m.GetMethodBody(); var s = \"CoverageInstrumentation.IsInstrumented(a)\";", true, false)]
    [InlineData("var b = m.GetMethodBody(); var s = nameof(CoverageInstrumentation);", true, false)]
    [InlineData("var b = m.GetMethodBody(); bool i = CoverageInstrumentation.IsInstrumented(a);", true, true)]
    [InlineData("var b = pe.GetMethodBody(rva); bool i = types.Any(Sharpy.TestInfrastructure.CoverageInstrumentation.IsInjected);", true, true)]
    [InlineData("// m.GetMethodBody()\nvar s = \"x.GetILAsByteArray()\";", false, false)]
    [InlineData("var t = typeof(MethodBody);", false, false)]
    public void Classify_ReadsSyntaxNotText(string body, bool reads, bool aware)
    {
        var source = "class C { void M(System.Reflection.MethodBase m, dynamic pe, int rva, System.Type[] types, System.Reflection.Assembly a, System.Type t) { "
            + body + "\n} }";
        Assert.Equal((reads, aware), Classify(source));
    }

    /// <summary>Whether <paramref name="source"/> reads method IL, and whether it calls the coverage detector.</summary>
    internal static (bool Reads, bool Aware) Classify(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        bool reads = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(i => InvokedName(i.Expression) is { } name && IlReadingMembers.Contains(name));
        bool aware = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(m => DetectorMembers.Contains(m.Name.Identifier.Text) && RightmostName(m.Expression) == DetectorType);
        return (reads, aware);
    }

    private static string? InvokedName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        MemberBindingExpressionSyntax b => b.Name.Identifier.Text,
        SimpleNameSyntax s => s.Identifier.Text,
        _ => null,
    };

    private static string? RightmostName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        AliasQualifiedNameSyntax a => a.Name.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        SimpleNameSyntax s => s.Identifier.Text,
        _ => null,
    };

    private static IEnumerable<string> PolicedSources(string root)
    {
        var src = Path.Combine(root, "src");
        var projects = Directory.GetDirectories(src)
            .Where(d => Path.GetFileName(d).EndsWith("Tests", StringComparison.Ordinal) || Path.GetFileName(d) == "Sharpy.TestInfrastructure")
            .ToList();
        Assert.Contains(projects, d => Path.GetFileName(d) == "Sharpy.Stdlib.Tests");
        foreach (var project in projects.OrderBy(p => p, StringComparer.Ordinal))
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var segments = Path.GetRelativePath(project, file).Split(Path.DirectorySeparatorChar);
                if (segments.Contains("bin") || segments.Contains("obj"))
                    continue;
                yield return file;
            }
        }
    }

    private static List<Regex> CoverageIncludes(string root)
    {
        var settings = XDocument.Load(Path.Combine(root, "build_tools", "coverage.runsettings"));
        return settings.Descendants("ModulePaths").Elements("Include").Elements("ModulePath")
            .Select(e => new Regex(e.Value.Trim(), RegexOptions.IgnoreCase))
            .ToList();
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        Assert.True(dir != null, "no sharpy.sln above the test assembly");
        return dir!.FullName;
    }
}
