using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Totality guard for the emitter's hoist sinks (#1680, #1739, plan-0667c5 Design Decision 6).
///
/// <para><b>What this guards.</b> The class contract is: every emitted C# construct that evaluates a
/// sub-expression <i>conditionally</i>, <i>repeatedly</i>, <i>deferredly</i> or in its own
/// <i>scope</i> owns a hoist sink, so a hoist producer inside that sub-expression lands where the
/// construct evaluates it. The failure mode is a lowering that builds such a construct and lets its
/// operand's hoists flush flat above the whole statement, where they run unconditionally, on the
/// wrong iteration, or out of order.</para>
///
/// <para><b>Why the axes are independent.</b> The previous version of this test scanned the emitter
/// for <c>WithSink(</c> call sites and compared them against a hardcoded list of the same method
/// names. Both axes derived from one source, so it could only fire on a bookkeeping slip (a new
/// <c>WithSink</c> call, or a rostered method that stopped calling it) and was structurally unable
/// to fire on the defect class itself — it was green at <c>311252e33</c> while six user-reachable
/// contexts had no sink at all (chained comparison, match statement guard, match expression guard
/// and arm result, the <c>@test</c> assert message, multi-item <c>with</c>, and operand order
/// outside call arguments). The axes here are the <b>emitted C# construct kind</b> (a literal
/// roster, below) crossed with the <b>emitter method that builds it</b> (discovered by parsing
/// <c>CodeGen/RoslynEmitter*.cs</c>). A new construct site in a method that pushes no sink is a
/// failure that must be answered with either a sink or a rostered exemption.</para>
/// </summary>
public class SinkPushingTotalityTests
{
    private readonly ITestOutputHelper _output;

    public SinkPushingTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The C# construct kinds that evaluate an operand conditionally, repeatedly, deferredly or in
    /// their own scope. Literal roster: the axis size is pinned by
    /// <see cref="ConstructRoster_HasThePinnedSize"/> so the matrix cannot silently shrink to the
    /// set the emitter happens to use.
    /// </summary>
    private static readonly Dictionary<string, string> ConditionalConstructs = new()
    {
        ["ConditionalExpression"] = "conditional — exactly one arm evaluates",
        ["BinaryExpression:LogicalAndExpression"] = "conditional — rhs evaluates only if lhs is true",
        ["BinaryExpression:LogicalOrExpression"] = "conditional — rhs evaluates only if lhs is false",
        ["BinaryExpression:CoalesceExpression"] = "conditional — rhs evaluates only if lhs is null",
        ["ConditionalAccessExpression"] = "conditional — the whenNotNull arm evaluates only if non-null",
        ["IfStatement"] = "conditional — the branch bodies evaluate on their own path",
        ["WhileStatement"] = "repeated — the condition and body re-evaluate per iteration",
        ["DoStatement"] = "repeated — the condition and body re-evaluate per iteration",
        ["ForStatement"] = "repeated — the condition, incrementors and body re-evaluate per iteration",
        ["ForEachStatement"] = "repeated — the body re-evaluates per element",
        ["WhenClause"] = "conditional — a match guard evaluates only for its own pattern",
        ["SwitchExpression"] = "conditional — one arm's result evaluates",
        ["SwitchStatement"] = "conditional — one section's body evaluates",
        ["ParenthesizedLambdaExpression"] = "deferred — the body evaluates when invoked",
        ["SimpleLambdaExpression"] = "deferred — the body evaluates when invoked",
        ["AnonymousMethodExpression"] = "deferred — the body evaluates when invoked",
        ["CatchClause"] = "conditional — the handler body evaluates only on a throw",
        ["FinallyClause"] = "deferred — the body evaluates on every exit path",
    };

    /// <summary>The pinned axis size. A construct kind added or dropped must be a deliberate edit.</summary>
    private const int ConditionalConstructCount = 18;

    /// <summary>The methods that push a hoist sink. Discovered, not rostered.</summary>
    private static readonly string[] SinkPushingCalls =
    {
        "WithEvaluationSink",
        "WithScopeSink",
        // Helpers whose whole job is to push a sink on the caller's behalf. A method that routes its
        // operands through one of these owns a sink just as directly as one that pushes inline.
        "GenerateExpressionsInOrder",
        "CaptureHoisted",
    };

    /// <summary>
    /// Sites that build a conditional construct and correctly need no sink of their own, keyed
    /// <c>"Method/ConstructKind"</c>. Each value states why the construct evaluates no user
    /// sub-expression conditionally: either it is the OUTPUT of a sink its caller pushed, or the
    /// sub-expressions it wraps were already generated as statements at their own boundary.
    /// A defect parked here instead belongs in <see cref="KnownRedSites"/>.
    /// </summary>
    private static readonly Dictionary<string, string> ExemptSites = new()
    {
        // --- The manufactured sinks themselves: these methods CONSUME a sink's drained output. ---
        ["GenerateMatchAsIsChain/IfStatement"] = "builds the manufactured sink GenerateMatch pushed",
        ["GenerateMatchAsIsChain/BinaryExpression:LogicalAndExpression"] = "the arm's own !taken && pattern test",
        ["GenerateMatchExpressionAsIsChain/IfStatement"] = "builds the manufactured sink GenerateMatchExpression pushed",
        ["GenerateMatchExpressionAsIsChain/BinaryExpression:LogicalAndExpression"] = "the arm's own !taken && pattern test",
        ["GenerateMatchAsSwitch/SwitchStatement"] = "reached only when no guard hoists; C#'s `when` already evaluates per arm",
        ["GenerateMatchAsSwitch/WhenClause"] = "reached only when no guard hoists; see GenerateMatch",
        ["BuildCoalesceValue/ConditionalExpression"] = "reached only when the rhs hoists nothing; see GenerateNullCoalesceOp",
        ["GenerateNullCoalesceAssignStatement/IfStatement"] = "builds the manufactured sink GenerateAssignment pushed for the `??=` rhs (#1835)",
        ["BuildCoalesceValue/BinaryExpression:LogicalAndExpression"] = "the EnsureSingleEvaluation capture condition, not a user operand",
        ["GenerateWhileWithElse/WhileStatement"] = "reached only when the test hoists nothing; see GenerateWhile",
        ["GenerateWhileWithElse/IfStatement"] = "the for/while-else completion flag test, not a user expression",

        // --- try / except / finally / with: the bodies are already generated STATEMENTS at their
        // own statement boundaries, so nothing conditional is left to sink. ---
        ["GenerateCatchClauses/CatchClause"] = "handler bodies are generated statements",
        ["GenerateAlternationCatchClause/CatchClause"] = "handler bodies are generated statements",
        ["GenerateAlternationCatchClause/BinaryExpression:LogicalAndExpression"] = "exception-type filter over generated type tests",
        ["GenerateAlternationCatchClause/BinaryExpression:LogicalOrExpression"] = "exception-type filter over generated type tests",
        ["GenerateExceptStarCatchClauses/CatchClause"] = "handler bodies are generated statements",
        ["GenerateExceptStarCatchClauses/IfStatement"] = "exception-group partition over generated statements",
        ["GenerateExceptStarCatchClauses/SimpleLambdaExpression"] = "partition predicate over a generated type test",
        ["GenerateFinallyClause/FinallyClause"] = "finally body is generated statements",
        ["GenerateScopeGuard/FinallyClause"] = "defer body is generated statements",
        ["GenerateSuppressionCapableExitTry/CatchClause"] = "__exit__ protocol scaffolding over generated statements",
        ["GenerateSuppressionCapableExitTry/FinallyClause"] = "__exit__ protocol scaffolding over generated statements",
        ["GenerateSuppressionCapableExitTry/IfStatement"] = "suppression-flag test, not a user expression",
        ["GenerateWithDunderProtocol/FinallyClause"] = "__exit__ call scaffolding; the context expression is sunk by GenerateWith",
        ["GenerateMultiExceptionTryExpression/CatchClause"] = "try-expression handler over a generated fallback",
        ["GenerateMultiExceptionTryExpression/ParenthesizedLambdaExpression"] = "wraps an already-generated operand once",
        ["GenerateTryExpression/ParenthesizedLambdaExpression"] = "wraps an already-generated operand once",
        ["GenerateTryWithElse/IfStatement"] = "the try-else completion flag test, not a user expression",
        ["GenerateIteratorProtocolMembers/CatchClause"] = "synthesized protocol member, no user sub-expression",
        ["GenerateQuestionMarkExpression/IfStatement"] = "the `?` early-return test over its own generated operand",

        // --- Deferred lambdas built around an ALREADY-GENERATED expression: the body is that one
        // expression, so there is no operand left to place. ---
        ["GenerateNestedLinqChain/SimpleLambdaExpression"] = "comprehension clause lambdas; CaptureHoisted owns the sinks",
        ["GenerateCallableReferenceLambda/ParenthesizedLambdaExpression"] = "wraps a resolved callable, no user sub-expression",
        ["GenerateCallableReferenceLambda/SimpleLambdaExpression"] = "wraps a resolved callable, no user sub-expression",
        ["GenerateConstructorReference/ParenthesizedLambdaExpression"] = "wraps a constructor call, no user sub-expression",
        ["GenerateConstructorReference/SimpleLambdaExpression"] = "wraps a constructor call, no user sub-expression",
        ["GenerateFunctoolsPartialCall/ParenthesizedLambdaExpression"] = "binds already-generated arguments",
        ["GenerateLruCacheWrapper/SimpleLambdaExpression"] = "cache factory over the generated call",
        ["WrapDefaultDictFactoryArgs/ParenthesizedLambdaExpression"] = "defaultdict factory over an already-generated default",

        // --- Null-safety and coercion lowerings over already-generated operands. ---
        ["GenerateNullSafeEqualsExpression/BinaryExpression:CoalesceExpression"] = "null-equality fold over generated operands",
        ["GenerateNullSafeEqualsExpression/ConditionalAccessExpression"] = "null-equality fold over generated operands",
        ["GenerateValueSafeEqualsExpression/BinaryExpression:CoalesceExpression"] = "null-equality fold over generated operands",
        ["GenerateValueSafeEqualsExpression/ConditionalAccessExpression"] = "null-equality fold over generated operands",
        ["GenerateLateBoundPreamble/BinaryExpression:CoalesceExpression"] = "late-bound default fold, no user sub-expression",
        ["GenerateTypeCoercion/ConditionalExpression"] = "coercion of one already-generated operand",

        // --- Statement and member scaffolding. ---
        ["GenerateFor/IfStatement"] = "the for-else completion flag test, not a user expression",
        ["GenerateForEachCoreInner/ForEachStatement"] = "the loop the for statement IS; its body is generated statements",
        ["GenerateYield/ForEachStatement"] = "yield-from delegation over a generated iterable",
        ["GenerateClassMethod/IfStatement"] = "synthesized member body, no user sub-expression",
        ["GenerateDataclassEquals/IfStatement"] = "synthesized __eq__ body, no user sub-expression",

        // --- @test-host scaffolding: xUnit fixtures and assert_raises, no user sub-expression. ---
        ["GenerateAssertThrowsStatements/CatchClause"] = "assert_raises scaffolding over generated statements",
        ["GenerateAssertThrowsStatements/IfStatement"] = "assert_raises flag test, not a user expression",
        ["GenerateAsyncFixtureClass/IfStatement"] = "xUnit fixture scaffolding, no user sub-expression",
        ["GenerateAsyncFixtureClass/ParenthesizedLambdaExpression"] = "xUnit fixture scaffolding, no user sub-expression",
        ["GenerateFixtureClass/ConditionalAccessExpression"] = "xUnit fixture scaffolding, no user sub-expression",
        ["GenerateFixtureClass/ParenthesizedLambdaExpression"] = "xUnit fixture scaffolding, no user sub-expression",
        ["GenerateMemberDataProperty/SimpleLambdaExpression"] = "xUnit MemberData scaffolding, no user sub-expression",
    };

    /// <summary>
    /// Sites that ARE unsunk — a live defect of this class, parked with its issue until fixed. Every
    /// reason must name an issue (<see cref="KnownRedReasons_CiteAnIssue"/>) and the entry is deleted
    /// in the commit that fixes it (drain on fix).
    /// </summary>
    private static readonly Dictionary<string, string> KnownRedSites = new()
    {
        // Empty: #1835 drained when GenerateAssignment gave the `??=` right-hand side its own
        // evaluation sink. Add an entry only with an OPEN issue, and delete it in the commit that
        // fixes the site (drain on fix).
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void ConstructRoster_HasThePinnedSize()
    {
        Assert.Equal(ConditionalConstructCount, ConditionalConstructs.Count);
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void ExemptionReasons_AreNonEmpty()
    {
        foreach (var (site, reason) in ExemptSites)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason),
                $"Exempt site '{site}' has no reason.");
        }
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedReasons_CiteAnIssue()
    {
        foreach (var (site, reason) in KnownRedSites)
        {
            Assert.Contains("#", reason, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(reason),
                $"Known-red site '{site}' has no reason.");
        }
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void ExemptAndKnownRedRosters_DoNotOverlap()
    {
        var overlap = ExemptSites.Keys.Intersect(KnownRedSites.Keys, StringComparer.Ordinal).ToList();
        Assert.True(overlap.Count == 0,
            "A site is either structurally exempt or a parked defect, not both: "
            + string.Join(", ", overlap));
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryConditionalConstructSite_IsInASinkPushingMethodOrExempt()
    {
        var sites = DiscoverConstructSites();

        _output.WriteLine($"{sites.Count} conditional-construct site group(s):");
        var unsunk = new List<string>();
        foreach (var site in sites.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var status = site.Value.PushesSink
                ? "SINK"
                : ExemptSites.ContainsKey(site.Key) ? "EXEMPT"
                : KnownRedSites.ContainsKey(site.Key) ? "KNOWN-RED"
                : "*** UNSUNK ***";
            _output.WriteLine($"  {site.Key,-72} {status}");
            if (!site.Value.PushesSink
                && !ExemptSites.ContainsKey(site.Key)
                && !KnownRedSites.ContainsKey(site.Key))
            {
                unsunk.Add($"{site.Key} ({ConditionalConstructs[site.Value.Kind]}) in {site.Value.File}");
            }
        }

        var staleExemptions = ExemptSites.Keys.Concat(KnownRedSites.Keys)
            .Where(k => !sites.ContainsKey(k) || sites[k].PushesSink)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(unsunk.Count == 0,
            $"Sink totality (plan-0667c5 Design Decision 6): {unsunk.Count} site(s) build a "
            + "conditional/repeated/deferred/scoped C# construct in a method that pushes no hoist "
            + "sink. Give the method a sink, or roster the site in ExemptSites with the reason its "
            + "construct evaluates no user sub-expression conditionally.\n  "
            + string.Join("\n  ", unsunk));

        Assert.True(staleExemptions.Count == 0,
            $"Sink totality: {staleExemptions.Count} ExemptSites entr(ies) no longer describe an "
            + "unsunk site — delete them (drain on fix).\n  "
            + string.Join("\n  ", staleExemptions));
    }

    private readonly record struct SiteInfo(string Kind, string File, bool PushesSink);

    private static Dictionary<string, SiteInfo> DiscoverConstructSites()
    {
        var result = new Dictionary<string, SiteInfo>(StringComparer.Ordinal);

        foreach (var file in FindEmitterSourceFiles())
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var root = tree.GetCompilationUnitRoot();
            var fileName = Path.GetFileName(file);

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                var pushesSink = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(GetInvokedMethodName)
                    .Any(n => n != null && SinkPushingCalls.Contains(n));

                foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    foreach (var kind in ClassifyConstruct(invocation))
                    {
                        var key = $"{methodName}/{kind}";
                        // A method either pushes a sink or does not; the first group wins and the
                        // flag is per-method, so re-keying the same pair is idempotent.
                        result[key] = new SiteInfo(kind, fileName, pushesSink);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the roster key(s) for a <c>SyntaxFactory</c> construct invocation, or nothing when
    /// the invocation builds no conditional construct. <c>BinaryExpression(SyntaxKind.X, …)</c> is
    /// keyed by its kind argument so an arithmetic <c>BinaryExpression</c> is not a hit.
    /// </summary>
    private static IEnumerable<string> ClassifyConstruct(InvocationExpressionSyntax invocation)
    {
        var name = GetInvokedMethodName(invocation);
        if (name == null)
            yield break;

        if (name == "BinaryExpression")
        {
            var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var kindName = firstArg switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            };
            if (kindName != null && ConditionalConstructs.ContainsKey($"BinaryExpression:{kindName}"))
                yield return $"BinaryExpression:{kindName}";
            yield break;
        }

        if (ConditionalConstructs.ContainsKey(name))
            yield return name;
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => null
        };
    }

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
