using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guards the store-conversion seam invariant (plan-14853b Phase 2 Task 3, Decisions 1/2/4):
/// a value entering a typed slot is admitted or refused by ONE decision, and a new store position
/// cannot bypass it.
///
/// <para>Three scans, all METHOD-scoped. File scoping was the hole this class was written to close:
/// <c>TypeChecker.Expressions.Access.Calls.cs</c> is 6,000 lines with a dozen argument routes in it,
/// so "this file may call the predicates" exempted every route in the file at once — which is
/// exactly how five binding routes came to decide value shapes on their own. Naming the METHOD makes
/// the exemption as small as the reason for it.</para>
///
/// <list type="number">
///   <item><c>ImplicitConversions.*</c> — the value-shape predicates. Only the seam itself, and the
///   two callers with a documented reason to consult a shape outside a store.</item>
///   <item><c>IsArgumentAssignable</c> — the argument-binding probe. Only the routes that bind an
///   argument to a parameter.</item>
///   <item>data-level <c>.IsAssignableTo(</c> in <c>TypeChecker*.cs</c> — banned outside
///   <c>IsAssignable</c> itself except for rows in <c>assignability-allowlist.txt</c>, each citing
///   the issue that drains it (Decision 4: one assignability authority).</item>
/// </list>
/// </summary>
public class StoreSeamConformanceTests
{
    /// <summary>
    /// <c>file::method</c> keys allowed to name <c>ImplicitConversions</c>. The whole seam file is
    /// one entry because every method in it IS the seam.
    /// </summary>
    private static readonly HashSet<string> ImplicitConversionsAllowedMethods = new(StringComparer.Ordinal)
    {
        "TypeChecker.StoreConversion.cs::*",
        // C# §12.21.4's compound-assignment narrowing asks the same §10.2.11 question the seam
        // asks, about an operand rather than a store (#1666).
        "TypeChecker.Utilities.cs::TryNarrowAugmentedResult",
        // The binary operator's constant pre-step — a promotion question, not a store (plan-299c1b
        // Decision 3). The augmented site calls THIS rather than re-deriving it.
        "TypeChecker.Expressions.Operators.cs::EffectiveOperandTypes",
    };

    /// <summary>
    /// <c>file::method</c> keys allowed to call <c>IsArgumentAssignable</c> — the routes that bind
    /// an argument to a parameter, plus the predicate's own projection recursion.
    /// </summary>
    private static readonly HashSet<string> IsArgumentAssignableAllowedMethods = new(StringComparer.Ordinal)
    {
        "TypeChecker.Utilities.cs::IsArgumentAssignable",
        "TypeChecker.Expressions.Access.Calls.cs::CheckLambdaCall",
        "TypeChecker.Expressions.Access.Calls.cs::ValidateCallArguments",
        "TypeChecker.Expressions.Access.Calls.cs::ValidateKeywordArguments",
        "TypeChecker.Expressions.Access.Calls.cs::ClrParameterAccepts",
        "TypeChecker.Expressions.Access.Calls.Overloads.cs::ResolveOverloadCore",
        "TypeChecker.cs::TypeChecker",
    };

    [Fact]
    public void ImplicitConversions_CallersAreInMethodAllowlist()
    {
        var violations = FindCallSites(
            node => node is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "ImplicitConversions" }
            },
            skipFile: name => name == "ImplicitConversions.cs")
            .Where(site => !IsAllowed(site, ImplicitConversionsAllowedMethods))
            .ToList();

        violations.Should().BeEmpty(
            "the value-shape predicates are the store seam's to consult. A new caller decides "
            + "whether a value fits a slot outside ClassifyStore, which is the defect class this "
            + "seam closes — route it through CheckStore/ClassifyStore, or add a method-scoped "
            + "allowlist entry with the reason it is not a store. Found: "
            + string.Join("; ", violations.Select(v => v.Describe())));
    }

    [Fact]
    public void IsArgumentAssignable_CallersAreInMethodAllowlist()
    {
        var violations = FindCallSites(
            node => node is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "IsArgumentAssignable" }
            },
            skipFile: _ => false)
            .Where(site => !IsAllowed(site, IsArgumentAssignableAllowedMethods))
            .ToList();

        violations.Should().BeEmpty(
            "argument acceptance is one probe used by the argument-binding routes. A new caller "
            + "outside those routes is a route that will not apply the accepted verdict either "
            + "(ApplyArgumentConversion). Found: "
            + string.Join("; ", violations.Select(v => v.Describe())));
    }

    /// <summary>
    /// Decision 4: the checker has ONE assignability authority, <c>IsAssignable</c>. A data-level
    /// <c>SemanticType.IsAssignableTo</c> call anywhere else in the checker skips the variance, CLR
    /// and provenance arms that only the checker can reach — the shape that let a tuple element
    /// slip through to CS0029 (#1701).
    /// </summary>
    [Fact]
    public void DataLevelIsAssignableTo_IsBannedInTheCheckerOutsideIsAssignable()
    {
        var allowlist = ReadAssignabilityAllowlist();

        var sites = FindCallSites(
            node => node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsAssignableTo" },
            skipFile: name => !name.StartsWith("TypeChecker", StringComparison.Ordinal))
            .Where(site => site.Method != "IsAssignable")
            .ToList();

        var keys = sites.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        var violations = sites.Where(s => !allowlist.Keys.Contains(s.Key)).ToList();
        violations.Should().BeEmpty(
            "every data-level IsAssignableTo in the checker skips the variance/CLR/provenance arms "
            + "only IsAssignable reaches (Decision 4, #1701). Call IsAssignable, or add a row to "
            + "src/Sharpy.Compiler.Tests/Conformance/assignability-allowlist.txt citing the issue "
            + "that drains it. Found: " + string.Join("; ", violations.Select(v => v.Describe())));

        var stale = allowlist.Keys.Where(k => !keys.Contains(k)).ToList();
        stale.Should().BeEmpty(
            "an allowlist row that matches nothing has drained — delete it "
            + "(src/Sharpy.Compiler.Tests/Conformance/assignability-allowlist.txt).");

        allowlist.RowsWithoutIssue.Should().BeEmpty(
            "every allowlist row cites the issue that drains it.");
    }

    /// <summary>
    /// Positive control for the scan mechanism itself: the walk must FIND the calls that are
    /// legitimately there. An empty result would make all three assertions pass vacuously — a scan
    /// that resolves nothing reports no violations.
    /// </summary>
    [Fact]
    public void Scan_FindsTheKnownCallSites()
    {
        FindCallSites(
            node => node is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "ImplicitConversions" }
            },
            skipFile: name => name == "ImplicitConversions.cs")
            .Should().NotBeEmpty("the seam itself calls the value-shape predicates");

        var argumentSites = FindCallSites(
            node => node is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "IsArgumentAssignable" }
            },
            skipFile: _ => false).ToList();

        argumentSites.Should().NotBeEmpty("the argument-binding routes call the probe");
        argumentSites.Select(s => s.Method).Should().Contain("ValidateCallArguments");

        FindCallSites(
            node => node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsAssignableTo" },
            skipFile: name => !name.StartsWith("TypeChecker", StringComparison.Ordinal))
            .Should().Contain(s => s.Method == "IsAssignable",
                "IsAssignable is the one place a data-level call belongs");
    }

    private record CallSite(string File, string Method, int Line, string Text)
    {
        public string Key => $"{File}::{Method}";

        public string Describe() => $"{File}:{Line} in {Method}(): {Text}";
    }

    private static bool IsAllowed(CallSite site, IReadOnlySet<string> allowed)
        => allowed.Contains(site.Key) || allowed.Contains($"{site.File}::*");

    private static IReadOnlyList<CallSite> FindCallSites(
        Func<SyntaxNode, bool> matches, Func<string, bool> skipFile)
    {
        var results = new List<CallSite>();

        foreach (var file in Directory.GetFiles(
            FindCompilerSemanticDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (skipFile(fileName))
                continue;

            var text = File.ReadAllText(file);
            var root = CSharpSyntaxTree.ParseText(text).GetRoot();

            foreach (var node in root.DescendantNodes().Where(matches))
            {
                results.Add(new CallSite(
                    fileName,
                    EnclosingMemberName(node),
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    node.ToString().Split('\n')[0].Trim()));
            }
        }

        return results;
    }

    /// <summary>
    /// The name of the member a node sits in — the unit an allowlist entry names. A local function
    /// reports its containing method, because that is the scope a reader checks.
    /// </summary>
    private static string EnclosingMemberName(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;
                case ConstructorDeclarationSyntax constructor:
                    return constructor.Identifier.ValueText;
                case PropertyDeclarationSyntax property:
                    return property.Identifier.ValueText;
                case FieldDeclarationSyntax field:
                    return field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "<field>";
                case LocalFunctionStatementSyntax:
                    continue;
            }
        }

        return "<file>";
    }

    private record Allowlist(IReadOnlySet<string> Keys, IReadOnlyList<string> RowsWithoutIssue);

    private static Allowlist ReadAssignabilityAllowlist()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler.Tests", "Conformance",
            "assignability-allowlist.txt");
        File.Exists(path).Should().BeTrue($"the allowlist file must exist at {path}");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var withoutIssue = new List<string>();

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var hash = line.IndexOf('#');
            var key = (hash >= 0 ? line[..hash] : line).Trim();
            var comment = hash >= 0 ? line[hash..] : string.Empty;

            keys.Add(key);
            if (!comment.Contains("#1", StringComparison.Ordinal)
                && !comment.Contains("#TBD", StringComparison.Ordinal))
                withoutIssue.Add(line);
        }

        return new Allowlist(keys, withoutIssue);
    }

    private static string FindCompilerSemanticDirectory()
        => Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "Semantic");

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "src", "Sharpy.Compiler", "Semantic")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "repository root not found from " + AppContext.BaseDirectory);
    }
}
