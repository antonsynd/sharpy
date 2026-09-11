using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Totality guard for readers of a <c>with</c> item (#1710, #1697, plan-0667c5).
///
/// <para><b>The class contract.</b> A <c>WithItem</c> has two halves — the context expression and
/// the <c>as</c> target — and the target is a STORE that also READS everything under it
/// (<c>with CM() as p.x</c> writes <c>p.x</c> and therefore reads <c>p</c>). Every pass that walks
/// with-items must see both halves. The failure mode is a reader that enumerates
/// <c>withStmt.Items</c>, visits <c>item.ContextExpression</c>, and silently drops
/// <c>item.Target</c>: the CFG recorded no write for the target (so <c>with CM() as p.x</c> on a
/// bare-declared <c>p</c> was reported at a later read instead of at the target) and the
/// unused-variable validator counted <c>p</c> as never used (SPY0451 on a name the statement does
/// use — fixtures <c>with_statement/with_as_member_target_1710</c> and
/// <c>with_statement/as_member_no_unused_1710</c>).</para>
///
/// <para><b>Why the scan is keyed on the item, not on <c>.ContextExpression</c>.</b> A scan keyed
/// on <c>.ContextExpression</c> occurrences rosters only the readers that already touch that half,
/// so the readers most likely to carry the defect — and the two LSP resolvers, which reach the
/// bound name through <c>GetWithItemSymbol</c> and never mention <c>.ContextExpression</c> — fall
/// outside the universe. The exemption would be the subject (verification-contract §2). The key
/// here is the WITH ITEM: a method that enumerates a with statement's <c>Items</c>, indexes them,
/// or takes a <c>WithItem</c>-typed parameter. Every such site must then be shown to reach the
/// target, by reading <c>.Target</c>, by resolving the bound symbol
/// (<c>GetWithItemSymbol</c>/<c>ResolveWithItemSymbol</c>), or by carrying the justification
/// comment <c>// WithItem: identifier-guarded (#1710)</c> on a site that legitimately needs only
/// the context expression.</para>
///
/// <para><b>Mutation (recorded in the commit body).</b> A new reader that enumerates
/// <c>withStmt.Items</c>, touches only <c>ContextExpression</c> and carries no justification
/// comment must make <see cref="EveryWithItemReader_ReachesTheTarget"/> red.</para>
/// </summary>
public class WithItemReaderConformanceTests
{
    private readonly ITestOutputHelper _output;

    public WithItemReaderConformanceTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The marker a site carries when it legitimately never reaches the <c>as</c> target, followed
    /// by that site's own reason. Spelled exactly, so it is greppable and cannot be written by
    /// accident; the reason is prose because the three honest cases differ — a FORM check that only
    /// asks what the context expression names, a MAP accessor keyed by the item that walks nothing,
    /// and a DISPATCHER that hands each item to the per-shape generator which does bind the target.
    /// </summary>
    private const string Justification = "WithItem justified (#1710):";

    /// <summary>
    /// Reader routes that reach the <c>as</c> target. <c>Target</c> is the direct read;
    /// the symbol resolvers reach the bound name through the checker's recorded
    /// <c>WithItem → VariableSymbol</c> map instead of the syntax.
    /// </summary>
    private static readonly string[] TargetReachingNames =
    {
        "Target",
        "GetWithItemSymbol",
        "ResolveWithItemSymbol",
    };

    /// <summary>The projects a with-item reader can live in.</summary>
    private static readonly string[] ScannedProjects = { "Sharpy.Compiler", "Sharpy.Lsp" };

    private readonly record struct ReaderSite(
        string Key, string File, bool ReachesTarget, bool IsJustified);

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryWithItemReader_ReachesTheTarget()
    {
        var sites = DiscoverReaderSites();

        _output.WriteLine($"{sites.Count} with-item reader site(s):");
        var offenders = new List<string>();
        foreach (var site in sites.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var status = site.ReachesTarget ? "TARGET"
                : site.IsJustified ? "JUSTIFIED"
                : "*** CONTEXT-EXPRESSION ONLY ***";
            _output.WriteLine($"  {site.Key,-70} {status}  ({site.File})");
            if (!site.ReachesTarget && !site.IsJustified)
                offenders.Add($"{site.Key} in {site.File}");
        }

        Assert.True(offenders.Count == 0,
            $"WithItem reader totality (#1710): {offenders.Count} site(s) read a `with` item without "
            + "ever reaching its `as` target. Read `item.Target` (a target is a store AND a read of "
            + $"everything under it), resolve its symbol, or add a `// {Justification} <reason>` "
            + "comment stating why this site never needs the target.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The scan finds sites at all. Without this the guard above would pass vacuously if the
    /// discovery predicate stopped matching (a renamed property, a moved directory) — the classic
    /// "instrument that passes over nothing" (verification-contract §2).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Scan_FindsTheKnownReaderSites()
    {
        var sites = DiscoverReaderSites();
        var keys = sites.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        // A literal floor, anchored to sites named in the #1710 investigation. Anchoring to the
        // scan's own output would make the assertion a tautology (§2).
        string[] mustBeRostered =
        {
            "ControlFlowGraphBuilder.BuildWith",
            "UnusedVariableValidator.CollectFromStatement",
            "LoweringPass.LowerWithStatement",
            "AssertRaisesForm.IsRewritten",
            "UnparseVisitor.VisitWithStatement",
            "AstNormalizer.VisitWithStatement",
            "AstFingerprint.WithItemsEqual",
            "DeclarationCursorResolver.ResolveWithItemSymbol",
        };

        var missing = mustBeRostered.Where(m => !keys.Contains(m)).ToList();
        Assert.True(missing.Count == 0,
            "The with-item reader scan no longer rosters known reader sites — the discovery "
            + "predicate has gone stale and the guard would pass over nothing:\n  "
            + string.Join("\n  ", missing)
            + "\nRostered: " + string.Join(", ", keys.OrderBy(k => k, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Discovers every method that reads a <c>with</c> item: one that takes a <c>WithItem</c>-typed
    /// parameter or local, or that reads the <c>Items</c> member of a with statement.
    /// </summary>
    private static List<ReaderSite> DiscoverReaderSites()
    {
        var result = new List<ReaderSite>();

        foreach (var file in FindScannedSourceFiles())
        {
            var text = File.ReadAllText(file);
            // Cheap pre-filter: a file with no mention of the with-item types cannot host a reader.
            if (!text.Contains("WithItem", StringComparison.Ordinal)
                && !text.Contains("WithStatement", StringComparison.Ordinal))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
            var fileName = Path.GetFileName(file);

            foreach (var member in root.DescendantNodes()
                         .Where(n => n is MethodDeclarationSyntax or AccessorDeclarationSyntax
                                       or LocalFunctionStatementSyntax))
            {
                if (!ReadsAWithItem(member, out var memberName))
                    continue;

                var typeName = EnclosingTypeName(member);
                var body = member.ToFullString();

                result.Add(new ReaderSite(
                    Key: $"{typeName}.{memberName}",
                    File: fileName,
                    ReachesTarget: ReachesTarget(member),
                    IsJustified: body.Contains(Justification, StringComparison.Ordinal)));
            }
        }

        return result;
    }

    /// <summary>
    /// True when the member reads a with item: a <c>WithItem</c>-typed parameter or local (the
    /// per-item helpers), or an <c>Items</c> member read inside a member that also names
    /// <c>WithStatement</c>/<c>WithItem</c> or lives on the <c>WithStatement</c> record itself
    /// (the enumerating readers). The type qualification is what keeps an unrelated
    /// <c>.Items</c> — a dictionary's, a list's — out of the universe.
    /// </summary>
    private static bool ReadsAWithItem(SyntaxNode member, out string memberName)
    {
        memberName = member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            LocalFunctionStatementSyntax f => f.Identifier.Text,
            AccessorDeclarationSyntax a => AccessorName(a),
            _ => "?"
        };

        var mentionsWithItemType =
            member.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Any(id => id.Identifier.Text is "WithItem" or "WithStatement")
            || EnclosingTypeName(member) is "WithStatement";

        if (!mentionsWithItemType)
            return false;

        var readsItems = member.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Any(ma => ma.Name.Identifier.Text == "Items");

        var hasWithItemTypedDeclaration = member.DescendantNodes()
            .Any(n => (n is ParameterSyntax p && MentionsWithItem(p.Type))
                   || (n is VariableDeclarationSyntax v && MentionsWithItem(v.Type)));

        return readsItems || hasWithItemTypedDeclaration;
    }

    private static bool MentionsWithItem(TypeSyntax? type)
        => type != null && type.ToString().Contains("WithItem", StringComparison.Ordinal);

    /// <summary>
    /// True when the member reaches the <c>as</c> target: it reads the <c>Target</c> member, names
    /// it as a parameter/argument, or routes through a with-item symbol resolver.
    /// </summary>
    private static bool ReachesTarget(SyntaxNode member)
    {
        foreach (var node in member.DescendantNodes())
        {
            var name = node switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            };
            if (name != null && TargetReachingNames.Contains(name, StringComparer.Ordinal))
                return true;
        }
        return false;
    }

    private static string AccessorName(AccessorDeclarationSyntax accessor)
    {
        var owner = accessor.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        return owner == null ? "accessor" : $"{owner.Identifier.Text}.{accessor.Keyword.Text}";
    }

    private static string EnclosingTypeName(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text
           ?? "<file>";

    private static IReadOnlyList<string> FindScannedSourceFiles()
    {
        var repoRoot = FindRepoRoot();
        var files = new List<string>();
        foreach (var project in ScannedProjects)
        {
            var dir = Path.Combine(repoRoot, "src", project);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException($"Scanned project not found at '{dir}'.");

            files.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                StringComparison.Ordinal)));
        }
        files.Sort(StringComparer.Ordinal);
        return files;
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
