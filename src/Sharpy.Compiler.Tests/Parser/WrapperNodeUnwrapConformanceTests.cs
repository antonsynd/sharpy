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
/// Conformance test enforcing that hand-rolled type dispatch on the import statement kinds unwraps
/// the transparent <c>DecoratedStatement</c> wrapper (the parallel-site completeness contract of
/// #1145; Class H of the defect taxonomy — member history #1124/#1125).
///
/// <para>
/// A statement-scoped <c>@suppress</c> (and any decorator) wraps its target in a
/// <c>DecoratedStatement</c>. Imports uniquely became wrappable this way: a <c>module.Body</c> scan
/// that type-tests <c>is ImportStatement</c> / <c>case FromImportStatement</c> without first calling
/// <see cref="Sharpy.Compiler.Parser.Ast.StatementExtensions.UnwrapDecorated"/> silently stops
/// matching a decorated import — which is exactly how #1124 (import resolution / <c>@suppress</c>
/// scope) and #1125 (LSP document links + hover) failed. The #1124 sweep fixed the resolver scans;
/// #1125 was the LSP handlers the sweep did not reach. Nothing mechanically stops a new hand-rolled
/// import scan from reintroducing the raw <c>is</c> test — this test does.
/// </para>
///
/// <para>
/// <b>Scan:</b> parse every <c>Sharpy.Compiler</c> and <c>Sharpy.Lsp</c> source file with Roslyn
/// (syntax only) and find each method that type-dispatches on <c>ImportStatement</c> or
/// <c>FromImportStatement</c> — an <c>is</c>/<c>is not</c> pattern, an <c>is</c> expression, a
/// <c>switch</c> case pattern, or <c>OfType&lt;T&gt;()</c>. Such a method is required to be
/// wrapper-aware: its body must reference <c>UnwrapDecorated</c> (it unwraps before/at the dispatch)
/// or <c>DecoratedStatement</c> (its switch/handler has a wrapper case that recurses). A method that
/// dispatches on an import kind while referencing neither is a raw scan that a decorated import slips
/// past.
/// </para>
///
/// <para>
/// <b>Scope (deliberate):</b> the target set is the two import kinds, the proven #1124/#1125 failure
/// mode. Definition kinds (<c>ClassDef</c>/<c>FunctionDef</c>/…) are decorated on the normal path and
/// their collection sites were wrapper-aware from the start, so including them would flood the
/// allowlist with hundreds of structurally-safe visitor arms and drown the signal. The mechanism
/// generalizes — extend <see cref="TargetStatementTypes"/> to widen coverage — but v1 pins the class
/// that actually recurred. The method-scope heuristic can miss a method that unwraps one value but
/// raw-dispatches a second; that residual is accepted for v1 and not observed today.
/// </para>
///
/// <para>
/// Complements the behavioral <c>DecoratedImportConformanceTests</c> (which feeds a decorated import
/// through the real consumers) with a mechanical source guard, and mirrors the source-scan style of
/// <c>SemanticInfoMergeConformanceTests</c> / <c>EmitterBannedTokenScanTests</c>.
/// </para>
/// </summary>
public class WrapperNodeUnwrapConformanceTests
{
    /// <summary>Statement kinds whose type-dispatch must be wrapper-aware (the #1124/#1125 class).</summary>
    private static readonly HashSet<string> TargetStatementTypes = new(StringComparer.Ordinal)
    {
        "ImportStatement",
        "FromImportStatement",
    };

    private const string UnwrapHelper = "UnwrapDecorated";
    private const string WrapperType = "DecoratedStatement";

    /// <summary>
    /// Methods that type-dispatch on an import kind but are wrapper-safe for a reason the method body
    /// itself does not show (their caller has already unwrapped / filtered out decorated imports).
    /// Key = "<c>{fileName} :: {methodName}</c>".
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // OrganizeImportsProvider.ExecuteAsync collects import-like statements, then skips every
        // DecoratedStatement wrapper (`if (stmt is DecoratedStatement) continue;`) BEFORE populating
        // the stdlib/project lists these helpers classify, sort, and render. Decorated imports are
        // pinned and rendered by RenderDecoratedImportVerbatim instead, so these helpers only ever
        // receive plain, unwrapped ImportStatement/FromImportStatement values.
        ["OrganizeImportsProvider.cs :: IsStdlibImport"] =
            "Receives only plain imports; ExecuteAsync skips DecoratedStatement wrappers before calling it.",
        ["OrganizeImportsProvider.cs :: CompareImports"] =
            "Sorts pre-filtered plain imports; decorated wrappers are excluded from the sorted groups.",
        ["OrganizeImportsProvider.cs :: GetSortKey"] =
            "Keys pre-filtered plain imports; decorated wrappers never reach the sort.",
        ["OrganizeImportsProvider.cs :: RenderImportStatement"] =
            "Renders pre-filtered plain imports; decorated imports go to RenderDecoratedImportVerbatim.",

        // BENIGN — verified unreachable. The module-level statement emitter has no DecoratedStatement
        // arm, but a decorated statement cannot reach it: decorated imports are filtered out by
        // GenerateModuleMembers' caller; decorated defs/VariableDeclarations carry their decorators on
        // the node (the parser never wraps those); and every other decorated statement at module level
        // is an executable statement, which semantic analysis rejects with SPY0340 BEFORE codegen
        // (confirmed via `emit diagnostics` on `@suppress(...) side()` / `@suppress(...) x = 2`).
        ["RoslynEmitter.ModuleClass.cs :: GenerateStatement"] =
            "BENIGN: module-level executable statements are rejected by SPY0340 before codegen; no decorated statement reaches the default arm.",

        // #1150 (real latent bug — LSP incremental re-analysis). AstFingerprint.Classify iterates
        // module.Body raw; for a (DecoratedStatement, DecoratedStatement) pair SignatureEquals falls to
        // `_ => a.GetType() == b.GetType()` (always true), so editing ONLY a @suppress-decorated import's
        // target (e.g. `from foo import bar` -> `baz`) is misclassified as NoChange. The LSP
        // (LanguageService.cs:377, SharpyWorkspace.cs:151) skips re-analysis on NoChange, so it serves
        // stale diagnostics/hover for the edited decorated import. Same failure mode as #1125. Drain this
        // entry when #1150 is fixed (SignatureEquals will then unwrap and this method becomes wrapper-aware).
        ["AstFingerprint.cs :: SignatureEquals"] =
            "#1150: decorated-import edits classified as NoChange -> LSP skips re-analysis (stale). Drain on fix.",

    };

    [Fact]
    public void ImportDispatchSites_AreWrapperAware()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateSyntaxTrees())
        {
            foreach (var (member, memberName) in DispatchingMembers(root))
            {
                var body = MemberBody(member);
                if (body is null)
                    continue;

                if (ReferencesIdentifier(body, UnwrapHelper) || ReferencesIdentifier(body, WrapperType))
                    continue; // wrapper-aware: unwraps at/near the dispatch, or has a DecoratedStatement arm

                var key = $"{fileName} :: {memberName}";
                if (Allowlist.ContainsKey(key))
                    continue;

                var line = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(
                    $"{key}  [{fileName}:{line}] — type-dispatches on an import kind but references neither "
                    + $"{UnwrapHelper} nor {WrapperType}.");
            }
        }

        violations.Should().BeEmpty(
            "every method that type-tests ImportStatement/FromImportStatement must first unwrap the "
            + "DecoratedStatement wrapper (call UnwrapDecorated) or handle it in a DecoratedStatement "
            + "switch arm, or a decorated import silently slips past the scan (Class H / #1124/#1125). "
            + "Make the dispatch wrapper-aware, or add the printed key to the Allowlist with a written "
            + "reason the method only ever sees unwrapped imports.\nViolations:\n"
            + string.Join("\n", violations));
    }

    // --- Dispatch detection -----------------------------------------------------------------

    /// <summary>
    /// Yields each method-like member that type-dispatches on a target statement kind, paired with a
    /// display name. A member is yielded at most once even if it dispatches several times.
    /// </summary>
    private static IEnumerable<(MemberDeclarationSyntax Member, string Name)> DispatchingMembers(
        CompilationUnitSyntax root)
    {
        var seen = new HashSet<MemberDeclarationSyntax>();
        foreach (var node in root.DescendantNodes())
        {
            if (!IsImportTypeDispatch(node))
                continue;

            var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>(m =>
                m is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax);
            if (member is null || !seen.Add(member))
                continue;

            yield return (member, MemberDisplayName(node, member));
        }
    }

    /// <summary>True if the node is a type-test on a target statement kind.</summary>
    private static bool IsImportTypeDispatch(SyntaxNode node)
    {
        switch (node)
        {
            // x is Import / x is Import binding / x is not Import / x is A or B — any pattern subtree.
            case IsPatternExpressionSyntax isPat:
                return PatternNamesTarget(isPat.Pattern);

            // Legacy `x is ImportStatement` (type test without a pattern) parses as an IsExpression.
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.IsExpression):
                return NamesTarget(bin.Right);

            // case ImportStatement: / case FromImportStatement fromImport:
            case CasePatternSwitchLabelSyntax label:
                return PatternNamesTarget(label.Pattern);

            // switch-expression arm: ImportStatement imp => ...
            case SwitchExpressionArmSyntax arm:
                return PatternNamesTarget(arm.Pattern);

            // seq.OfType<FromImportStatement>()
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax g } }
                when g.Identifier.Text == "OfType":
                return g.TypeArgumentList.Arguments.Any(NamesTarget);

            default:
                return false;
        }
    }

    /// <summary>True if any type reference inside a pattern subtree names a target kind.</summary>
    private static bool PatternNamesTarget(PatternSyntax pattern)
        => pattern.DescendantNodesAndSelf().OfType<TypeSyntax>().Any(NamesTarget)
           // A bare TypePattern's type is itself a TypeSyntax already covered above; guard against a
           // pattern whose only type token is the pattern's immediate child.
           || (pattern is DeclarationPatternSyntax d && NamesTarget(d.Type))
           || (pattern is TypePatternSyntax t && NamesTarget(t.Type))
           || (pattern is RecursivePatternSyntax r && r.Type is { } rt && NamesTarget(rt));

    private static bool NamesTarget(SyntaxNode? node)
        => node is IdentifierNameSyntax id && TargetStatementTypes.Contains(id.Identifier.Text);

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
                // Cheap pre-filter: only files naming an import kind can contain a dispatch on one.
                if (!TargetStatementTypes.Any(t => text.Contains(t, StringComparison.Ordinal)))
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
