using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Roster guard for the emitter's diagnostics (#2032): every diagnostic the code generator reports
/// carries its file path and the reporting node's location, and a CodeGen-range (or Infrastructure)
/// code.
///
/// <para><b>What this guards.</b> The contract is "every diagnostic in a project bag carries the
/// file path and the reporting node's location". The emitter broke it in two ways: three sites
/// (SPY0340 and both SPY0520 arms in <c>RoslynEmitter.ModuleClass.cs</c>) passed no location, so
/// SPY0520 rendered with no line or caret, and two sites (<c>RoslynEmitter.TypeDeclarations.cs</c>,
/// <c>RoslynEmitter.Expressions.cs</c>) went around <c>CodeGenContext</c> to the raw bag and dropped
/// the path. The cure is one helper that takes the node, <c>CodeGenContext.ReportAt</c>. This scan
/// parses every file under <c>src/Sharpy.Compiler/CodeGen/</c> and classifies every reporting
/// invocation: <c>ReportAt</c> (a path and a position by construction), a <c>_context.Add*</c>
/// call or an <c>EmitNotImplemented*</c> call that passes a line and a column, or a violation.</para>
///
/// <para>The site count is pinned to a literal measured at the commit that introduced the guard,
/// so a new reporting site is a deliberate edit that has to be classified here.</para>
/// </summary>
public class EmitterDiagnosticLocationRosterTests
{
    private readonly ITestOutputHelper _output;

    public EmitterDiagnosticLocationRosterTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The number of reporting invocations under <c>CodeGen/</c>, measured when the guard landed. A
    /// new site must be classified (located, and in range or rostered below) and this literal bumped
    /// in the same edit. 28 → 26 at #2039: the two SPY0520 arms left with the module-class merge
    /// (every module is a namespace; a type named like its file is an ordinary sibling type).
    /// </summary>
    private const int ReportingSiteCount = 26;

    /// <summary>
    /// Reporting method names. <c>Add</c> counts only on a receiver that is a diagnostic bag.
    /// </summary>
    private static readonly HashSet<string> ReportingMethods = new(StringComparer.Ordinal)
    {
        "AddError", "AddWarning", "AddInfo", "AddErrorWithRelatedLocations", "AddPhaseError",
        "EmitNotImplementedExpression", "EmitNotImplementedStatement", "ReportAt",
    };

    /// <summary>
    /// The helper bodies themselves: they forward caller-supplied values, and each caller is a site.
    /// </summary>
    private static readonly HashSet<string> HelperDefinitions = new(StringComparer.Ordinal)
    {
        "CodeGenContext.cs/AddError", "CodeGenContext.cs/AddWarning", "CodeGenContext.cs/AddInfo",
        "CodeGenContext.cs/ReportAt",
        "RoslynEmitter.cs/EmitNotImplementedExpression", "RoslynEmitter.cs/EmitNotImplementedStatement",
    };

    /// <summary>
    /// Sites that report a code outside the CodeGen (SPY05xx) and Infrastructure (SPY09xx) ranges,
    /// keyed <c>"File/Method/code"</c>. Recorded, not renumbered: changing a code is a user-visible
    /// move of its own.
    /// </summary>
    private static readonly Dictionary<string, string> CodeRangeViolations = new(StringComparer.Ordinal)
    {
        ["RoslynEmitter.ModuleClass.cs/GenerateModuleMembers/DiagnosticCodes.Semantic.ModuleLevelExecutableStatement"] =
            "SPY0340, a semantic-range code, raised by the emitter for module-level statements beside main() " +
            "(ModuleLevelValidator reports the same code at semantic time). Recorded by plan-0ca7b7 Phase 1 Task 3 (#2032).",
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void ReportingSites_HaveThePinnedCount()
    {
        var sites = DiscoverSites();
        foreach (var site in sites)
            _output.WriteLine($"{site.File}:{site.Line} {site.Method} {site.Name} code={site.Code} located={site.Located} viaHelper={site.ViaHelper}");
        Assert.Equal(ReportingSiteCount, sites.Count);
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryEmitterDiagnostic_GoesThroughTheContextWithALocation()
    {
        var bad = DiscoverSites()
            .Where(s => !s.ViaHelper || !s.Located)
            .Select(s => $"{s.File}:{s.Line} {s.Method}: {s.Name}"
                + (!s.ViaHelper ? " bypasses CodeGenContext (no file path)" : "")
                + (!s.Located ? " passes no line/column" : ""))
            .ToList();

        Assert.True(bad.Count == 0,
            $"#2032: {bad.Count} emitter diagnostic site(s) would report without a file path or a location. "
            + "Report through CodeGenContext.ReportAt(node, message, code).\n  " + string.Join("\n  ", bad));
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryEmitterDiagnostic_ReportsACodeGenOrInfrastructureCode_OrIsRostered()
    {
        var sites = DiscoverSites();
        var outOfRange = sites
            .Where(s => !InRange(s.Code))
            .Select(s => $"{s.File}/{s.Method}/{s.Code}")
            .ToList();

        var unrostered = outOfRange.Where(k => !CodeRangeViolations.ContainsKey(k)).ToList();
        var stale = CodeRangeViolations.Keys.Where(k => !outOfRange.Contains(k)).ToList();

        Assert.True(unrostered.Count == 0,
            "An emitter diagnostic reports a code outside SPY05xx/SPY09xx (or none the scan can read):\n  "
            + string.Join("\n  ", unrostered));
        Assert.True(stale.Count == 0,
            "CodeRangeViolations entries that no longer describe a site — delete them (drain on fix):\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// The instrument: the classifier flags a location-less context call, a bag call that bypasses
    /// the context, and an out-of-range code, and passes a <c>ReportAt</c> call and a located one.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Classifier_FlagsEveryShapeItGuards()
    {
        const string source = """
            partial class RoslynEmitter
            {
                void M(Node n)
                {
                    _context.AddError("a", code: DiagnosticCodes.CodeGen.EmitError);
                    _context.Diagnostics.AddError("b", 1, 2, code: DiagnosticCodes.CodeGen.EmitError);
                    _context.AddError("c", DiagnosticCodes.CodeGen.EmitError, n.LineStart, n.ColumnStart);
                    _context.ReportAt(n, "d", DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);
                    EmitNotImplementedExpression("e", DiagnosticCodes.CodeGen.UnsupportedFeature);
                }
            }
            """;
        var sites = ScanFile("Probe.cs", source).ToList();

        Assert.Equal(5, sites.Count);
        Assert.False(sites[0].Located);
        Assert.True(sites[0].ViaHelper);
        Assert.False(sites[1].ViaHelper);
        Assert.True(sites[2].Located && sites[2].ViaHelper);
        Assert.True(sites[3].Located && sites[3].ViaHelper);
        Assert.False(InRange(sites[3].Code));
        Assert.False(sites[4].Located);
        Assert.True(InRange(sites[4].Code));
    }

    private readonly record struct Site(
        string File, int Line, string Method, string Name, string Code, bool ViaHelper, bool Located);

    private static bool InRange(string code)
        => code.StartsWith("DiagnosticCodes.CodeGen.", StringComparison.Ordinal)
            || code.StartsWith("DiagnosticCodes.Infrastructure.", StringComparison.Ordinal);

    private static List<Site> DiscoverSites()
    {
        var codegenDir = Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "CodeGen");
        if (!Directory.Exists(codegenDir))
            throw new DirectoryNotFoundException($"CodeGen directory not found at '{codegenDir}'.");

        return Directory.GetFiles(codegenDir, "*.cs")
            .OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(f => ScanFile(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
    }

    private static IEnumerable<Site> ScanFile(string fileName, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var (name, receiver) = invocation.Expression switch
            {
                IdentifierNameSyntax id => (id.Identifier.Text, (string?)null),
                MemberAccessExpressionSyntax ma => (ma.Name.Identifier.Text, ma.Expression.ToString()),
                _ => ((string?)null, (string?)null)
            };
            if (name == null)
                continue;
            var isBagAdd = name == "Add" && receiver != null
                && (receiver.EndsWith("Diagnostics", StringComparison.Ordinal)
                    || receiver.EndsWith("_diagnostics", StringComparison.Ordinal));
            if (!ReportingMethods.Contains(name) && !isBagAdd)
                continue;

            var method = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "<none>";
            if (HelperDefinitions.Contains($"{fileName}/{method}"))
                continue;

            var args = invocation.ArgumentList.Arguments;
            var positional = args.Where(a => a.NameColon == null).ToList();
            string Named(string n) => args.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == n)?.Expression.ToString() ?? "";

            bool viaHelper;
            bool located;
            string code;
            switch (name)
            {
                case "ReportAt":
                    viaHelper = receiver == "_context";
                    located = true;
                    code = positional.Count >= 3 ? positional[2].Expression.ToString() : Named("code");
                    break;
                case "EmitNotImplementedExpression":
                case "EmitNotImplementedStatement":
                    viaHelper = receiver == null;
                    located = positional.Count >= 4 || (Named("line") != "" && Named("column") != "");
                    code = positional.Count >= 2 ? positional[1].Expression.ToString() : Named("code");
                    break;
                default:
                    // The context's own Add* helpers: (message, code, line, column).
                    viaHelper = receiver == "_context";
                    located = viaHelper && (positional.Count >= 4 || (Named("line") != "" && Named("column") != ""));
                    code = viaHelper && positional.Count >= 2 ? positional[1].Expression.ToString() : Named("code");
                    break;
            }

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new Site(fileName, line, method, name, code, viaHelper, located);
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Could not find the repository root (no .git above the test binaries).");
    }
}
