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
        // min/max's constant-argument pre-step — the same promotion question over an argument LIST,
        // asked before overload selection so `max(u64, 1)` unifies on uint64 (plan-299c1b, #1700).
        // Not a store: nothing is admitted into a slot here, and no fact is recorded.
        "TypeChecker.Expressions.Operators.cs::EffectiveMinMaxArgumentTypes",
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
        // The selection half of ResolveOverloadCore (plan-499995, #1721): the probe runs here,
        // and the binding half applies the verdict once the winner is known.
        "TypeChecker.Expressions.Access.Calls.Overloads.cs::SelectOverload",
        "TypeChecker.cs::TypeChecker",
        // R-U (#1750): the needle of `in`/`not in` is an ARGUMENT into the container's element slot,
        // so the membership arm calls IsArgumentAssignable exactly as a call site does (plan-757fbb).
        "TypeChecker.Expressions.Operators.cs::ClassifyMembership",
        // #1775: the same-argument overload rule probes each candidate's parameter at the failing
        // index — it is a read-only assignability check, not a binding decision, and no conversion
        // is applied; the only consumer is the SPY0220/SPY0354 decision in ReportOverloadError.
        "TypeChecker.Expressions.Access.Calls.cs::TrySameArgumentOverloadRefusal",
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

    /// <summary>
    /// Every <c>_expectedType =</c> AND <c>_parameterTypedArgument =</c> write in the checker must go
    /// through <c>EnterStore</c>, <c>EnterArgumentContext</c> or <c>ClearExpectation</c>
    /// (plan-ebd58b Decision 1, which names both fields in one sentence). A raw write bypasses the
    /// save/restore seam and the <c>StoreContext</c> record the diagnostics read for context.
    /// </summary>
    [Fact]
    public void ExpectationIsPushedOnlyThroughEnterStore()
    {
        var semanticDir = FindCompilerSemanticDirectory();
        var files = Directory.GetFiles(semanticDir, "TypeChecker*.cs", SearchOption.TopDirectoryOnly);

        files.Should().NotBeEmpty("positive control: the scan must find TypeChecker files");

        var violations = new List<string>();
        var enterStoreCount = 0;
        var clearExpectationCount = 0;
        var scannedFileCount = 0;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            // The seam itself is the ONE file allowed to write _expectedType
            if (fileName == "TypeChecker.StoreConversion.cs")
                continue;

            scannedFileCount++;
            var text = File.ReadAllText(file);
            var lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                // Skip comments
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///"))
                    continue;

                // Field declarations are allowed: `private SemanticType? _expectedType = null;`
                if (trimmed.Contains("private") && trimmed.Contains("SemanticType?") && trimmed.Contains("_expectedType"))
                    continue;
                if (trimmed.Contains("private") && trimmed.Contains("Expression?") && trimmed.Contains("_parameterTypedArgument"))
                    continue;

                // Check for raw `_expectedType =` / `_parameterTypedArgument =` writes
                // (assignment, not comparison)
                foreach (var field in SeamOwnedFields)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, field + @"\s*=[^=]"))
                        violations.Add($"{fileName}:{i + 1}: {trimmed.TrimEnd()}");

                    // `ref _expectedType` (ScopedValue.Push)
                    if (line.Contains("ref " + field))
                        violations.Add($"{fileName}:{i + 1}: {trimmed.TrimEnd()}");
                }

                // Count the seam's push forms — on CODE lines only. The comment skip above is what
                // makes this a call-site count rather than a text-occurrence count that drifts every
                // time a comment names the method.
                enterStoreCount += CountCalls(line, "EnterStore") + CountCalls(line, "EnterArgumentContext");
                clearExpectationCount += CountCalls(line, "ClearExpectation");
            }
        }

        // Also count EnterStore/ClearExpectation in StoreConversion.cs (only the definition,
        // not call sites — but the definition includes the method name)
        foreach (var line in File.ReadAllLines(Path.Combine(semanticDir, "TypeChecker.StoreConversion.cs")))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///"))
                continue;
            enterStoreCount += CountCalls(line, "EnterStore") + CountCalls(line, "EnterArgumentContext");
            clearExpectationCount += CountCalls(line, "ClearExpectation");
        }

        // Subtract the three DEFINITIONS, which the same scan counts
        enterStoreCount -= 2;
        clearExpectationCount -= 1;

        violations.Should().BeEmpty(
            "every _expectedType write must go through EnterStore or ClearExpectation — "
            + "a raw write bypasses the save/restore seam and the StoreContext record. Found: "
            + string.Join("; ", violations));

        scannedFileCount.Should().BeGreaterThan(0,
            "positive control: the scan must examine at least one TypeChecker file");

        var totalCallSites = enterStoreCount + clearExpectationCount;
        totalCallSites.Should().Be(ExpectedSeamCallSiteCount,
            "the literal anchor for EnterStore/EnterArgumentContext + ClearExpectation call sites "
            + $"(got {enterStoreCount} pushes + {clearExpectationCount} ClearExpectation = {totalCallSites})");
    }

    /// <summary>
    /// The fields the seam owns outright. Both are named by plan-ebd58b Decision 1 in one sentence:
    /// "<c>EnterStore</c> becomes the ONLY writer of <c>_expectedType</c> (and of
    /// <c>_parameterTypedArgument</c>)". The second one was the half that shipped unscanned, and
    /// five raw writes survived in the call-argument loops because nothing looked for the name.
    /// </summary>
    private static readonly string[] SeamOwnedFields = { "_expectedType", "_parameterTypedArgument" };

    private static int CountCalls(string line, string method)
        => System.Text.RegularExpressions.Regex.Matches(line, method + @"\(").Count;

    /// <summary>
    /// Measured at the implementer's sha, over CODE lines only. An empty scan cannot pass it, and a
    /// comment that happens to name <c>EnterStore()</c> no longer moves it (the anchor it replaced
    /// was bumped 45 -> 47 for two comment mentions). 47 -> 48 @ 80d759f8c: the starred-unpacking
    /// arm pushes each non-star target's declared slot through <c>EnterStore(TupleElement, …)</c>
    /// (38 pushes + 10 clears, measured by this scan at 9785f8a91). 48 -> 51 (plan-499995,
    /// #1797): the <c>Some</c>/<c>Ok</c>/<c>Err</c> constructor arms clear the expectation to type a
    /// payload freely when the formal's slot is OPEN (a shape hint the inference binds through),
    /// three <c>ClearExpectation</c> sites; the post-inference re-check pushes the CLOSED formal
    /// through <c>EnterStore(ArgumentPositional, …)</c> and the argument loops' two channels
    /// collapsed into the one <c>FormalSlots</c> push, so the push count stays 38
    /// (38 pushes + 13 clears, measured by this scan). 51 -> 53 (plan-499995, #1719/#1807): the
    /// operand seam pushes the selected dunder overload's slot through
    /// <c>EnterStore(OperatorOperand, …)</c> for the right operand of a user operator, and the
    /// dict index READ pushes the key type the same way (40 pushes + 13 clears, measured by this
    /// scan). 53 -> 55 (plan-499995, #1721): a slot-typed construction (`None()`, `Some`, `Ok`,
    /// `Err`) at an overload-set callee is PROBED under an open slot
    /// (<c>ProbeSlotTypedConstruction</c>) and, once a candidate wins, bound to that candidate's
    /// slot (<c>BindArgumentsToSelectedOverload</c>) — two pushes, one per half of the seam
    /// (42 pushes + 13 clears, measured by this scan). 55 -> 56 (plan-499995, #1797): the
    /// post-inference re-check (<c>RecheckOpenArguments</c>) pushes the closed slot for a KEYWORD
    /// argument recorded open, the twin of the positional push it already had
    /// (43 pushes + 13 clears, measured by this scan). 56 -> 60 (plan-c068ff, #1743, R-W):
    /// FStringHole (f-string + t-string holes), TruthinessTest (CheckTruthinessTest helper),
    /// and the conditional's own TruthinessTest push (47 pushes + 13 clears).
    /// </summary>
    private const int ExpectedSeamCallSiteCount = 61;

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
