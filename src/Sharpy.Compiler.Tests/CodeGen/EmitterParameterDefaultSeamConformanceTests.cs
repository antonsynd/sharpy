using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Structural guard for the emitter's ONE parameter-default seam (plan-757fbb Phase 4;
/// #1762, #1769). A parameter default is printed by exactly one path,
/// <c>RoslynEmitter.GenerateParameterDefault(Expression, bool isOptionalSlot)</c>, which is the
/// only place that decides between <c>default</c> (an Optional slot whose default is
/// <c>None</c>/<c>None()</c>) and <c>GenerateExpression(defaultValue)</c>. Every
/// <c>ParameterSyntax.WithDefault(...)</c> call in <c>src/Sharpy.Compiler/CodeGen/</c> must take
/// that helper's result — a site that builds its own <c>EqualsValueClause</c> around a user
/// expression is a second default-printing path, and the next default-shape bug (a const that
/// stopped being <c>const</c>, a <c>None</c> spelled for the wrong family) lands there unseen.
///
/// <para>
/// The plan's acceptance was a grep — <c>WithDefault(</c> in <c>CodeGen/</c>: six hits at
/// dff55b2cd, "exactly the helper's one hit after". The helper that landed returns the
/// <c>EqualsValueClauseSyntax</c> rather than the parameter, so each caller still spells
/// <c>.WithDefault(GenerateParameterDefault(...))</c> and the grep never reached one. This scan
/// enforces the substance instead: it parses every CodeGen source file with Roslyn and classifies
/// the argument of every <c>WithDefault(</c> invocation.
/// </para>
///
/// <para>
/// <b>The one non-user default.</b> A late-bound parameter (<c>def f(x =&gt; expr)</c>, PEP 671
/// style) is emitted as a hidden nullable <c>x_lb</c> parameter whose default is the literal
/// <c>null</c>; the user's expression is evaluated in the body, never printed as a default. That
/// site carries no Sharpy <c>Expression</c>, so it cannot go through the helper, and it is admitted
/// by EXACT shape — <c>EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))</c> —
/// not by file or method name. Any other spelling at that site is a violation too, so the
/// exemption cannot widen into a second path.
/// </para>
///
/// <para>
/// <b>Vacuity control.</b> The seam-site and sentinel-site counts are anchored to literals: a scan
/// that matched nothing would fail on the anchors, not pass on an empty violation list. Adding a
/// legitimate default-printing site is a deliberate change — route it through the helper and bump
/// <see cref="SeamSiteCount"/> in the same commit.
/// </para>
/// </summary>
public class EmitterParameterDefaultSeamConformanceTests
{
    private const string SeamHelperName = "GenerateParameterDefault";

    /// <summary>
    /// The late-bound sentinel, matched by normalized text. Whitespace-normalized so a reformat of
    /// the multi-line site does not change the classification.
    /// </summary>
    private const string LateBoundNullSentinel =
        "EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))";

    /// <summary>
    /// `WithDefault(GenerateParameterDefault(...))` sites @ 3709bfd74: constructors (2),
    /// lambda local functions (1), method/function parameters (1), dataclass fields (1).
    /// </summary>
    private const int SeamSiteCount = 5;

    /// <summary>The late-bound `null` sentinel: `RoslynEmitter.TypeDeclarations.cs` only.</summary>
    private const int LateBoundSentinelCount = 1;

    private enum SiteKind { Seam, LateBoundNullSentinel, Violation }

    private sealed record Site(string File, int Line, string ArgumentText, SiteKind Kind);

    [Fact]
    public void EveryWithDefaultInCodeGen_TakesTheSeamHelperOrTheLateBoundNullSentinel()
    {
        var sites = ScanWithDefaultSites();

        var violations = sites.Where(s => s.Kind == SiteKind.Violation).ToList();
        violations.Should().BeEmpty(
            "every parameter default in CodeGen must be printed by " + SeamHelperName +
            "(defaultValue, isOptionalSlot) — the one seam that decides `default` vs the expression. " +
            "Offending sites (route them through the helper):\n" +
            string.Join("\n", violations.Select(v => $"  {v.File}:{v.Line}  WithDefault({v.ArgumentText})")));

        // Vacuity controls: the scan must SEE the sites it claims to police.
        sites.Count(s => s.Kind == SiteKind.Seam).Should().Be(SeamSiteCount,
            "the scan must find every `WithDefault(" + SeamHelperName + "(...))` site; a new " +
            "legitimate site is a deliberate change — bump SeamSiteCount in the same commit. Found:\n" +
            string.Join("\n", sites.Where(s => s.Kind == SiteKind.Seam).Select(s => $"  {s.File}:{s.Line}")));

        sites.Count(s => s.Kind == SiteKind.LateBoundNullSentinel).Should().Be(LateBoundSentinelCount,
            "the late-bound `x_lb = null` parameter is the only default that is not a user expression; " +
            "it is admitted by exact shape, and there is exactly one such site");
    }

    [Fact]
    public void TheSeamHelper_IsDefinedExactlyOnceInCodeGen()
    {
        var definitions = ParseCodeGenSources()
            .SelectMany(t => t.Tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == SeamHelperName)
                .Select(m => $"{t.RelativePath}:{Line(m)}"))
            .ToList();

        definitions.Should().ContainSingle(
            SeamHelperName + " is the one default-printing seam (RoslynEmitter.cs); a second overload " +
            "or a copy in another partial is a second path. Found: " + string.Join(", ", definitions));
    }

    // ── Scan ────────────────────────────────────────────────────────────────────────────────────

    private static List<Site> ScanWithDefaultSites()
    {
        var sites = new List<Site>();
        foreach (var (relativePath, tree) in ParseCodeGenSources())
        {
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    { Name.Identifier.Text: "WithDefault" })
                    continue;

                var argument = invocation.ArgumentList.Arguments.Count == 1
                    ? invocation.ArgumentList.Arguments[0].Expression
                    : null;

                sites.Add(new Site(
                    relativePath,
                    Line(invocation),
                    argument?.NormalizeWhitespace().ToString() ?? invocation.ArgumentList.ToString(),
                    Classify(argument)));
            }
        }
        return sites;
    }

    private static SiteKind Classify(ExpressionSyntax? argument)
    {
        if (argument is InvocationExpressionSyntax { Expression: IdentifierNameSyntax callee }
            && callee.Identifier.Text == SeamHelperName)
            return SiteKind.Seam;

        if (argument is not null
            && argument.NormalizeWhitespace().ToString() == LateBoundNullSentinel)
            return SiteKind.LateBoundNullSentinel;

        return SiteKind.Violation;
    }

    private static IEnumerable<(string RelativePath, SyntaxTree Tree)> ParseCodeGenSources()
    {
        var codeGenDir = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        Directory.Exists(codeGenDir).Should().BeTrue($"CodeGen source directory not found: {codeGenDir}");

        var files = Directory.GetFiles(codeGenDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("the scan must see the emitter sources");

        foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
            yield return (Path.GetRelativePath(codeGenDir, file), tree);
        }
    }

    private static int Line(SyntaxNode node)
        => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
