using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Totality guard for the emitter's operand-order seam (#1739, #1849, plan-0667c5 Phase 3 Task 4).
///
/// <para><b>The class contract.</b> Operands that are siblings in one C# expression evaluate left to
/// right, and the lowering preserves that. <c>GenerateExpressionsInOrder</c> is the one seam that
/// enforces it: each operand is generated under its own evaluation sink, and an earlier
/// side-effecting operand is captured into a temp when a later one hoists. Without it a hoist
/// producer (comprehension, spread, walrus, <c>?</c>) in operand k flushes its statements above the
/// whole expression and runs before operands 0..k-1 — a silently wrong result, not an ICE:
/// <c>ys[lo(xs) : len([v for v in xs])]</c> printed <c>[10, 20, 30, 40]</c> where python3 prints
/// <c>[10, 20, 30]</c> (#1849, measured at f84701e04).</para>
///
/// <para><b>Why the axes are independent.</b> The universe is discovered by parsing
/// <c>CodeGen/RoslynEmitter*.cs</c>: an <b>entry point</b> is any emitter method that generates more
/// than one user <c>Expression</c> — two or more <c>GenerateExpression</c> calls, or one inside a
/// loop or a LINQ projection. The requirement is the <b>helper</b>. Neither side is derived from the
/// other, so a new lowering that builds its own operand list is a failure here even though every
/// existing site is correct — which is the case the previous state of this class could not cover:
/// the ordering fix was wired into call arguments only, and displays, binary operands and slice
/// bounds each had to be found by hand.</para>
///
/// <para><b>Mutation (recorded in the commit body).</b> Deleting the
/// <c>GenerateOptionalExpressionsInOrder</c> call from <c>GenerateGetSliceCall</c> makes
/// <see cref="EveryOperandListEntryPoint_RoutesThroughTheOrderingHelper"/> red on that method.</para>
/// </summary>
public class OperandOrderEntryPointTotalityTests
{
    private readonly ITestOutputHelper _output;

    public OperandOrderEntryPointTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>The ordering seam, and the two helpers whose whole job is to push it.</summary>
    private static readonly string[] OrderingHelpers =
    {
        "GenerateExpressionsInOrder",
        "GenerateOptionalExpressionsInOrder",
    };

    /// <summary>
    /// Methods that generate several user expressions which are NOT sibling operands of one C#
    /// expression, so no ordering is owed. Each reason says which: the operands land in different
    /// statements, or in different branches of a construct that evaluates only one of them, or only
    /// one of the generated expressions is a user operand at all.
    /// </summary>
    private static readonly Dictionary<string, string> ExemptSites = new()
    {
        // --- Conditional by design: ordering these operands would be WRONG. The later operand
        // must not be evaluated at all unless its branch is taken; each pushes its own sink. ---
        ["GenerateNullCoalesceOp"] = "the rhs evaluates only on the absent path; it has its own evaluation sink",
        ["GenerateShortCircuitOp"] = "the rhs evaluates only when the lhs does not decide; own sink",
        ["GenerateConditionalExpression"] = "exactly one arm evaluates; each arm has its own sink",
        ["GenerateAssert"] = "the message evaluates only on failure; it has its own evaluation sink",
        ["GenerateIf"] = "a test and its elif tests are evaluated conditionally, each under its own sink",
        ["GenerateMatchExpression"] = "the scrutinee and the arm results are per-arm; arms have their own sinks",
        ["GenerateMatchPattern"] = "pattern literals and a guard, evaluated per pattern test, not as siblings",
        ["GenerateComparisonChain"] = "each operand has its own sink because the chain short-circuits",
        ["GenerateWith"] = "each with item's context expression has its own sink, placed in its own try",

        // --- One user operand only; the other generations are synthesized or compile-time. ---
        ["GenerateExpressionCore"] = "the dispatcher: one user expression per arm",
        ["GenerateReturn"] = "one user operand",
        ["GenerateYield"] = "one user operand (the yielded value or the delegated iterable)",
        ["GenerateLateBoundPreamble"] = "one user operand per parameter default, each its own statement",
        ["GenerateStringEnumClass"] = "one string literal per member, each its own field declaration",
        ["GenerateAttributeArgumentExpression"] = "one compile-time constant attribute argument",
        ["GenerateConstructor"] = "field initializers, each its own statement in the constructor body",
        ["GenerateStructAutoConstructors"] = "one field default per statement",
        ["GenerateAssertThrowsStatements"] = "one user operand (the match pattern)",
        ["GenerateNestedLinqChain"] = "one operand per comprehension clause; CaptureHoisted owns each clause's sink",
        ["GenerateDictSpreadComprehension"] = "one operand per clause; CaptureHoisted owns the iterator's sink",
        ["GenerateSpreadCollectionBuilder"] = "each spread/element becomes its own Add statement, not a sibling operand",
        ["BuildCombinedVariadicArray"] = "each spread becomes its own Concat call over already-generated arrays",
        ["GeneratePositionalArguments"] = "the callers' argument list is ordered by GenerateReorderedCallArgumentsCore",
        ["GenerateCall"] = "routes its argument list through GenerateReorderedCallArgumentsCore",

        // --- Conditional / deferred, found by the first run of this scan. ---
        ["GenerateWhile"] = "the loop test is re-evaluated per iteration under its own sink",
        ["GenerateLambdaExpression"] = "the body is a deferred scope with its own scope sink",
        ["GenerateTypedLambdaExpression"] = "the body is a deferred scope with its own scope sink",
        ["GenerateAssignment"] = "a store's target and value are not siblings of one expression; the `??=` value has its own sink (#1835)",
    };

    /// <summary>
    /// Entry points that DO build a sibling operand list and do not route through the helper — live
    /// defects of this class, parked with their issue until fixed. Deleted in the commit that routes
    /// the method through the helper (drain on fix).
    /// </summary>
    private static readonly Dictionary<string, string> KnownRedSites = new()
    {
        ["GenerateTestAssert"] = "#1853 — the two compared operands, generated right-then-left for xUnit's (expected, actual)",
        ["GenerateAssertAlmostEqual"] = "#1853 — actual, expected and the tolerance are siblings of one call",
        ["GenerateAssertRegex"] = "#1853 — text and pattern are siblings of one call",
        ["GenerateComparisonAssert"] = "#1853 — lhs and rhs are siblings of one call",
        ["GenerateContainsAssert"] = "#1853 — item and collection are siblings of one call",
        ["GenerateStore"] = "#1853 — an index target's receiver and index are siblings of one subscript",
        ["GeneratePipeForward"] = "#1853 — the piped value and the call's own arguments are siblings",
        ["GenerateIndexAccess"] = "#1853 — a tuple/spread index's elements are siblings of one subscript",
        ["GenerateMultiAxisAccess"] = "#1853 — the per-dimension indices are siblings of one subscript",
        ["GenerateSpreadDictBuilder"] = "#1853 — a spread builder's key and value are siblings of one entry",
        ["GenerateImperativeComprehension"] = "#1853 — a dict comprehension's key and value are siblings of one entry",
        ["TryGetApproxParts"] = "#1853 — expected, actual and the tolerance are siblings of one call",
        ["BuildProductCapacityArgs"] = "#1853 — one clause iterator per factor of the capacity product, all siblings",
        ["GenerateFString"] = "#1862 — interpolation holes are siblings; a later hole's hoists run first (measured wrong)",
        ["GenerateTString"] = "#1862 — the t-string twin of GenerateFString",
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void ExemptionReasons_AreNonEmpty()
    {
        foreach (var (site, reason) in ExemptSites)
            Assert.False(string.IsNullOrWhiteSpace(reason), $"Exempt site '{site}' has no reason.");
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedReasons_CiteAnIssue()
    {
        Assert.NotEmpty(KnownRedSites);
        foreach (var (site, reason) in KnownRedSites)
            Assert.Contains("#", reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void ExemptAndKnownRedRosters_DoNotOverlap()
    {
        var overlap = ExemptSites.Keys.Intersect(KnownRedSites.Keys, StringComparer.Ordinal).ToList();
        Assert.True(overlap.Count == 0,
            "A site owes ordering or it does not, never both: " + string.Join(", ", overlap));
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryOperandListEntryPoint_RoutesThroughTheOrderingHelper()
    {
        var sites = DiscoverEntryPoints();

        _output.WriteLine($"{sites.Count} multi-operand emitter entry point(s):");
        var unordered = new List<string>();
        foreach (var site in sites.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var status = site.Value.UsesHelper ? "ORDERED"
                : ExemptSites.ContainsKey(site.Key) ? "EXEMPT"
                : KnownRedSites.ContainsKey(site.Key) ? "KNOWN-RED"
                : "*** UNORDERED ***";
            _output.WriteLine($"  {site.Key,-46} {status}  ({site.Value.File}, {site.Value.Calls} call(s))");
            if (!site.Value.UsesHelper
                && !ExemptSites.ContainsKey(site.Key)
                && !KnownRedSites.ContainsKey(site.Key))
            {
                unordered.Add($"{site.Key} in {site.Value.File} ({site.Value.Calls} GenerateExpression call(s))");
            }
        }

        var stale = ExemptSites.Keys.Concat(KnownRedSites.Keys)
            .Where(k => !sites.ContainsKey(k) || sites[k].UsesHelper)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(unordered.Count == 0,
            $"Operand-order totality (#1739, #1849): {unordered.Count} emitter method(s) generate "
            + "more than one user expression without routing through GenerateExpressionsInOrder. "
            + "Route the operands through the helper, or roster the method in ExemptSites with the "
            + "reason its generations are not sibling operands of one C# expression.\n  "
            + string.Join("\n  ", unordered));

        Assert.True(stale.Count == 0,
            $"Operand-order totality: {stale.Count} roster entr(ies) no longer describe a site that "
            + "skips the helper — delete them (drain on fix).\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// The scan finds the sites that ARE ordered, so a discovery predicate that stopped matching
    /// cannot leave the guard passing over nothing. Anchored to literal method names, not to the
    /// scan's own output (verification-contract §2).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Scan_SeesTheOrderedSites()
    {
        var sites = DiscoverEntryPoints();
        string[] mustBeOrdered =
        {
            "GenerateGetSliceCall",
            "GenerateNewSlice",
            "GenerateSliceSpec",
            "GenerateReorderedCallArgumentsCore",
            "GenerateTupleLiteral",
        };

        var missing = mustBeOrdered
            .Where(m => !sites.TryGetValue(m, out var s) || !s.UsesHelper)
            .ToList();
        Assert.True(missing.Count == 0,
            "The operand-order scan no longer sees these sites as routed through the helper — "
            + "either the fix was reverted or the discovery predicate has gone stale:\n  "
            + string.Join("\n  ", missing));
    }

    private readonly record struct EntryPoint(string File, int Calls, bool UsesHelper);

    /// <summary>
    /// An entry point is an emitter method that generates more than one user expression: two or more
    /// <c>GenerateExpression</c> invocations, or one inside a loop or LINQ projection (which
    /// generates an unbounded number).
    /// </summary>
    private static Dictionary<string, EntryPoint> DiscoverEntryPoints()
    {
        var result = new Dictionary<string, EntryPoint>(StringComparer.Ordinal);

        foreach (var file in FindEmitterSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            var fileName = Path.GetFileName(file);

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(i => (Node: i, Name: InvokedName(i)))
                    .Where(x => x.Name != null)
                    .ToList();

                var generateCalls = invocations
                    .Where(x => x.Name == "GenerateExpression")
                    .ToList();

                var usesHelper = invocations.Any(x => OrderingHelpers.Contains(x.Name!, StringComparer.Ordinal));

                var repeated = generateCalls.Count >= 2;
                var inLoopOrProjection = generateCalls.Any(x =>
                    x.Node.Ancestors().TakeWhile(a => a != method)
                        .Any(a => a is ForEachStatementSyntax or ForStatementSyntax
                                    or WhileStatementSyntax or SimpleLambdaExpressionSyntax
                                    or ParenthesizedLambdaExpressionSyntax));

                if (!repeated && !inLoopOrProjection && !usesHelper)
                    continue;
                if (generateCalls.Count == 0 && !usesHelper)
                    continue;

                // A method may be split across partials; the first group carrying the method name
                // wins and the flag is per-method, so re-keying is idempotent.
                if (!result.ContainsKey(method.Identifier.Text) || usesHelper)
                {
                    result[method.Identifier.Text] =
                        new EntryPoint(fileName, generateCalls.Count, usesHelper);
                }
            }
        }

        return result;
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => null
        };

    private static IReadOnlyList<string> FindEmitterSourceFiles()
    {
        var repoRoot = FindRepoRoot();
        var codegenDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "CodeGen");
        if (!Directory.Exists(codegenDir))
            throw new DirectoryNotFoundException($"CodeGen directory not found at '{codegenDir}'.");

        return Directory.GetFiles(codegenDir, "RoslynEmitter*.cs")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException(
            $"Could not find repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
