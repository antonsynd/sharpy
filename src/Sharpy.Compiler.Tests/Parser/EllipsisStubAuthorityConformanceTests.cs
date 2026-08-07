using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Conformance test enforcing that stub detection goes through the single authority in
/// <c>AstHelper</c> rather than through a hand-rolled <c>is EllipsisLiteral</c> test (#1214; the
/// parallel-site completeness contract of #1145, and the #1146 "un-lowerable accepted shape"
/// contract).
///
/// <para>
/// Grouping is transparent: <c>(...)</c> denotes the same stub as <c>...</c>. #1197 established
/// that at the expression-statement seam, but twenty-one other sites — class-member emission,
/// declaration type-checking, cross-module symbol extraction, and four validators — still
/// pattern-matched the raw expression. The result was a checker/emitter disagreement: a class body
/// containing <c>(...)</c> produced SPY0510 + SPY0908, and an interface stub written <c>(...)</c>
/// drew a wrong SPY0266, while both compiled with a bare <c>...</c>. Nothing mechanically stops a
/// new hand-rolled stub test from reintroducing that divergence — this test does.
/// </para>
///
/// <para>
/// <b>Scan:</b> parse every <c>Sharpy.Compiler</c> and <c>Sharpy.Lsp</c> source file with Roslyn
/// (syntax only) and find each method-like member that type-dispatches on <c>EllipsisLiteral</c> —
/// an <c>is</c>/<c>is not</c> pattern, an <c>is</c> expression, a <c>switch</c> case pattern, a
/// switch-expression arm, or <c>OfType&lt;T&gt;()</c>. Such a member is compliant if it references
/// one of the authority members (<c>TryGetEllipsisStub</c>, <c>IsEllipsisStubBody</c>,
/// <c>IsAbstractStubBody</c>) or <c>UnwrapParenthesized</c> — the latter because a member that
/// strips grouping before its own type test is transparent by construction, which is exactly the
/// shape of the already-correct <c>GenerateExpressionStatement</c> seam from #1197.
/// </para>
///
/// <para>
/// <b>Scope (deliberate):</b> only type <em>dispatch</em> is flagged. Constructing an
/// <c>EllipsisLiteral</c> (the parser), declaring the record, and naming it in a
/// <c>VisitEllipsisLiteral</c> / <c>GenerateEllipsisLiteral</c> signature are not decisions about
/// whether a body is a stub, so they are out of scope rather than allowlist noise. The
/// member-scope heuristic shares the residual of its sibling
/// <see cref="WrapperNodeUnwrapConformanceTests"/>: a member that legitimately unwraps one value
/// but raw-dispatches a second would pass. That residual is accepted and not observed today.
/// </para>
///
/// <para>
/// Complements the behavioral <c>(...)</c> fixtures (class body, interface/abstract method stub,
/// property stub, imported module) with a mechanical source guard, and mirrors the source-scan
/// style of <c>SemanticInfoMergeConformanceTests</c> / <c>EmitterPurityConformanceTests</c>.
/// </para>
/// </summary>
public class EllipsisStubAuthorityConformanceTests
{
    private const string TargetType = "EllipsisLiteral";

    /// <summary>The file that owns the authority; its own dispatch is the implementation.</summary>
    private const string AuthorityFile = "AstHelper.cs";

    /// <summary>
    /// Members whose presence makes a dispatch stub-authority-aware: the three authority entry
    /// points, plus the paren strip they are built on (a member that unwraps grouping first cannot
    /// diverge between <c>...</c> and <c>(...)</c>).
    /// </summary>
    private static readonly string[] AuthorityMembers =
    {
        "TryGetEllipsisStub",
        "IsEllipsisStubBody",
        "IsAbstractStubBody",
        "UnwrapParenthesized",
    };

    /// <summary>
    /// Members that type-dispatch on <c>EllipsisLiteral</c> for a reason that is not stub detection.
    /// Key = "<c>{fileName} :: {memberName}</c>"; every entry states why the site does not decide
    /// whether a declaration body is a stub.
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // Renamed from GenerateExpression when the sequence-materialization applier took that name as
        // a wrapper (#1251); the dispatch switch itself is unchanged and so is this reason.
        ["RoslynEmitter.Expressions.cs :: GenerateExpressionCore"] =
            "Expression-kind dispatch: routes the literal to GenerateEllipsisLiteral. Grouping is handled by the sibling Parenthesized arm, not here.",
        ["RoslynEmitter.Operators.cs :: CollectReferencedIdentifiers"] =
            "Identifier-collection walk: the arm is the leaf 'this literal names nothing' case, not a body classification.",
        ["AstVisitor.cs :: Visit"] =
            "Visitor dispatch (both the void and generic Visit overloads share this key): routes any EllipsisLiteral node to VisitEllipsisLiteral regardless of position.",
        ["StructuralEqualityComparer.cs :: Equals"] =
            "Structural comparison: two ellipsis literals are equal because they carry no payload.",
        ["TypeChecker.Expressions.cs :: CheckExpression"] =
            "Expression-type dispatch: an ellipsis expression is typed Void wherever it appears; the Parenthesized arm normalizes grouping.",
        ["ExtractVariableProvider.cs :: IsTrivialExpression"] =
            "LSP refactoring: excludes trivial expressions from extract-variable. Not a declaration-body classification.",
    };

    [Fact]
    public void EllipsisDispatchSites_UseTheStubAuthority()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateSyntaxTrees())
        {
            if (fileName == AuthorityFile)
                continue; // the authority itself

            foreach (var (member, memberName) in DispatchingMembers(root))
            {
                var body = MemberBody(member);
                if (body is null)
                    continue;

                if (AuthorityMembers.Any(m => ReferencesIdentifier(body, m)))
                    continue; // routed through the authority, or unwraps grouping before dispatching

                var key = $"{fileName} :: {memberName}";
                if (Allowlist.ContainsKey(key))
                    continue;

                var line = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(
                    $"{key}  [{fileName}:{line}] — type-dispatches on {TargetType} without referencing "
                    + $"any of: {string.Join(", ", AuthorityMembers)}.");
            }
        }

        violations.Should().BeEmpty(
            "stub detection has one authority: AstHelper.TryGetEllipsisStub / IsEllipsisStubBody / "
            + "IsAbstractStubBody, which unwrap grouping so `(...)` is the same stub as `...`. A raw "
            + "`is EllipsisLiteral` test reintroduces the checker/emitter divergence #1214 fixed. Route "
            + "the dispatch through the authority (or unwrap with AstHelper.UnwrapParenthesized first), "
            + "or add the printed key to the Allowlist with a written reason the site is not stub "
            + "detection.\nViolations:\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// The allowlist must not rot: every entry has to name a member that still dispatches on
    /// <see cref="TargetType"/>. A stale entry silently widens the guard's blind spot.
    /// </summary>
    [Fact]
    public void Allowlist_HasNoStaleEntries()
    {
        var live = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (fileName, root) in EnumerateSyntaxTrees())
        {
            if (fileName == AuthorityFile)
                continue;

            foreach (var (_, memberName) in DispatchingMembers(root))
                live.Add($"{fileName} :: {memberName}");
        }

        var stale = Allowlist.Keys.Where(k => !live.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "each allowlist entry must correspond to a member that still type-dispatches on "
            + $"{TargetType}; delete entries whose site is gone or has been routed through the "
            + "authority.\nStale:\n" + string.Join("\n", stale));
    }

    // --- Dispatch detection -----------------------------------------------------------------

    /// <summary>
    /// Yields each method-like member that type-dispatches on <see cref="TargetType"/>, paired with
    /// a display name. A member is yielded at most once even if it dispatches several times.
    /// </summary>
    private static IEnumerable<(MemberDeclarationSyntax Member, string Name)> DispatchingMembers(
        CompilationUnitSyntax root)
    {
        var seen = new HashSet<MemberDeclarationSyntax>();
        foreach (var node in root.DescendantNodes())
        {
            if (!IsEllipsisTypeDispatch(node))
                continue;

            var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>(m =>
                m is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax);
            if (member is null || !seen.Add(member))
                continue;

            yield return (member, MemberDisplayName(node, member));
        }
    }

    /// <summary>True if the node is a type-test on <see cref="TargetType"/>.</summary>
    private static bool IsEllipsisTypeDispatch(SyntaxNode node)
    {
        switch (node)
        {
            // x is EllipsisLiteral / ... e / is not / property patterns like { Expression: EllipsisLiteral }
            case IsPatternExpressionSyntax isPat:
                return PatternNamesTarget(isPat.Pattern);

            // Legacy `x is EllipsisLiteral` type test without a pattern.
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.IsExpression):
                return NamesTarget(bin.Right);

            // case EllipsisLiteral n: (a designation makes it a pattern label)
            case CasePatternSwitchLabelSyntax label:
                return PatternNamesTarget(label.Pattern);

            // case EllipsisLiteral: — a bare type name with no designation parses as a constant
            // case label, not a pattern label, so it needs its own arm.
            case CaseSwitchLabelSyntax constLabel:
                return NamesTarget(constLabel.Value);

            // switch-expression arm: EllipsisLiteral => ...
            case SwitchExpressionArmSyntax arm:
                return PatternNamesTarget(arm.Pattern);

            // seq.OfType<EllipsisLiteral>()
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax g } }
                when g.Identifier.Text == "OfType":
                return g.TypeArgumentList.Arguments.Any(NamesTarget);

            default:
                return false;
        }
    }

    /// <summary>True if any type reference inside a pattern subtree names the target kind.</summary>
    private static bool PatternNamesTarget(PatternSyntax pattern)
        => pattern.DescendantNodesAndSelf().OfType<TypeSyntax>().Any(NamesTarget)
           || (pattern is DeclarationPatternSyntax d && NamesTarget(d.Type))
           || (pattern is TypePatternSyntax t && NamesTarget(t.Type))
           || (pattern is RecursivePatternSyntax r && r.Type is { } rt && NamesTarget(rt));

    private static bool NamesTarget(SyntaxNode? node)
        => node is IdentifierNameSyntax id && id.Identifier.Text == TargetType;

    // --- Member helpers ---------------------------------------------------------------------

    private static SyntaxNode? MemberBody(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
            ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
            PropertyDeclarationSyntax p => (SyntaxNode?)p.ExpressionBody ?? p.AccessorList,
            _ => null,
        };

    /// <summary>
    /// Display name for the enclosing member. When the dispatch is inside a local function, prefer
    /// the local function's name so the allowlist key is stable and specific.
    /// </summary>
    private static string MemberDisplayName(SyntaxNode dispatch, MemberDeclarationSyntax member)
    {
        var localFn = dispatch.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        if (localFn != null && localFn.Ancestors().Contains(member))
            return localFn.Identifier.Text;

        return member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            _ => member.GetType().Name,
        };
    }

    private static bool ReferencesIdentifier(SyntaxNode body, string identifier)
        => body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == identifier);

    // --- File discovery ---------------------------------------------------------------------

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> EnumerateSyntaxTrees()
    {
        foreach (var dir in new[] { FindSourceDir("Sharpy.Compiler"), FindSourceDir("Sharpy.Lsp") })
        {
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var text = File.ReadAllText(file);
                // Cheap pre-filter: only files naming the literal can dispatch on it.
                if (!text.Contains(TargetType, StringComparison.Ordinal))
                    continue;

                var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
                yield return (Path.GetFileName(file), root);
            }
        }
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}
