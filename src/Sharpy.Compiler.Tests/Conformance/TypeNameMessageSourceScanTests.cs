using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Every runtime message names the python type (#2035, Decision 26): a Core or Stdlib site that
/// spells a type into text reads the ONE authority, <c>Sharpy.PyFormat</c> — <c>PyTypeName</c> (the
/// tp_name a C-level message spells) or <c>PyDunderName</c> (<c>__name__</c>, which python-level
/// modules such as json spell) — never the CLR name (<c>String</c>, <c>Timedelta</c>, <c>List`1</c>,
/// <c>MyThing</c>). The 25-site roster the plan
/// measured (<c>obj.GetType().Name</c> in a <c>throw</c>, <c>typeof(T).Name</c> in a comparison
/// refusal, <c>return type.Name</c> in a helper, …) is routed; this scan keeps it that way.
///
/// <para><b>What it scans.</b> A Roslyn syntax walk over every <c>src/Sharpy.Core</c> and
/// <c>src/Sharpy.Stdlib</c> source file (bin/obj excluded). A <i>candidate</i> is
/// (a) a <c>.Name</c>/<c>.FullName</c> read whose receiver is <c>X.GetType()</c>, <c>typeof(…)</c> or
/// a name ending in <c>Type</c>/<c>type</c>, or (b) a bare <c>X.GetType()</c> used as a string-concat
/// operand or an interpolation hole (its implicit <c>ToString()</c>). Each candidate is classified:
/// <list type="bullet">
/// <item><b>Authority</b> — inside <c>PyFormat</c>'s own name projections.</item>
/// <item><b>Identity</b> — compared, never shown: the receiver of <c>StartsWith</c>/<c>EndsWith</c>/
/// <c>Equals</c>/<c>Contains</c>, or an operand of <c>==</c>/<c>!=</c>.</item>
/// <item><b>Bug-throw</b> — inside a <c>throw</c> of a <c>System.*</c> exception (ArgumentException
/// family, InvalidOperationException, NotSupportedException, NotImplementedException) whose text
/// begins <c>unreachable</c>: a compiler/runtime bug, not a python message.</item>
/// <item><b>Exempt</b> — a row of <see cref="Exemptions"/>, each with its reason and issue; a row
/// that matches no candidate is stale and fails.</item>
/// <item><b>Violation</b> — everything else (a throw, a return, an assignment, an append).</item>
/// </list>
/// The non-violation candidates are pinned to a LITERAL count, so a scan whose candidate walk stops
/// matching (and would pass vacuously) goes red; the synthetic positive controls run the same
/// classifier.</para>
/// </summary>
public class TypeNameMessageSourceScanTests
{
    /// <summary>
    /// Authority + identity + bug-throw + exempt candidates at landing (measured). Re-measure —
    /// never adjust to pass — when a sanctioned site is added or removed, and say why in the commit.
    /// </summary>
    private const int ExpectedSanctionedCandidates = 8;

    /// <summary>
    /// Sites that spell a CLR type name into text on purpose: (repository-relative path, the
    /// candidate's code, why + issue). Empty at landing.
    /// </summary>
    private static readonly (string Path, string Code, string Reason)[] Exemptions = [];

    private enum Verdict { Authority, Identity, BugThrow, Exempt, Violation }

    private sealed record Candidate(string Path, int Line, string Code, Verdict Verdict);

    [Fact]
    public void EveryTypeNameInCoreAndStdlibText_GoesThroughPyTypeName()
    {
        var candidates = ScanSources().ToList();
        var violations = candidates.Where(c => c.Verdict == Verdict.Violation).ToList();

        Assert.True(violations.Count == 0,
            "CLR type names spelled into text outside PyFormat.PyTypeName (#2035):\n" +
            string.Join("\n", violations.Select(v => $"  {v.Path}:{v.Line}: {v.Code}")));

        var stale = Exemptions.Where(e => !candidates.Any(c => c.Path == e.Path && c.Code == e.Code)).ToList();
        Assert.True(stale.Count == 0,
            "stale exemptions (no such candidate; delete the row):\n" +
            string.Join("\n", stale.Select(e => $"  {e.Path}: {e.Code}")));

        var sanctioned = candidates.Where(c => c.Verdict != Verdict.Violation).ToList();
        Assert.True(sanctioned.Count == ExpectedSanctionedCandidates,
            $"expected {ExpectedSanctionedCandidates} sanctioned candidates (authority/identity/bug-throw/exempt), found {sanctioned.Count}:\n" +
            string.Join("\n", sanctioned.Select(c => $"  {c.Verdict} {c.Path}:{c.Line}: {c.Code}")));
    }

    [Theory]
    [InlineData("class C { void M(object v) { throw new TypeError(\"x \" + v.GetType().Name); } }", 1, 0)]
    [InlineData("class C { string M<T>() { return $\"'{typeof(T).Name}'\"; } }", 1, 0)]
    [InlineData("class C { string M(System.Type type) { string n = type.Name; return n; } }", 1, 0)]
    [InlineData("class C { string M(object v) { return \"a\" + v.GetType(); } }", 1, 0)]
    [InlineData("class C { string M(object v) { return PyFormat.PyTypeName(v.GetType()); } }", 0, 0)]
    [InlineData("class C { bool M(System.Type type) { return type.FullName.StartsWith(\"System.ValueTuple`\"); } }", 0, 1)]
    [InlineData("class C { void M(object v) { throw new System.InvalidOperationException(\"unreachable: \" + v.GetType()); } }", 0, 1)]
    public void Classifier_PositiveControls(string source, int expectedViolations, int expectedSanctioned)
    {
        var candidates = Classify("Synthetic.cs", CSharpSyntaxTree.ParseText(source).GetRoot()).ToList();
        Assert.Equal(expectedViolations, candidates.Count(c => c.Verdict == Verdict.Violation));
        Assert.Equal(expectedSanctioned, candidates.Count(c => c.Verdict != Verdict.Violation));
    }

    private static IEnumerable<Candidate> ScanSources()
    {
        foreach (var project in new[] { "Sharpy.Core", "Sharpy.Stdlib" })
        {
            var root = Path.Combine(FixtureRoots.RepositoryRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/');
                if (rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal))
                    continue;
                foreach (var candidate in Classify(rel, CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot()))
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<Candidate> Classify(string path, SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            ExpressionSyntax? site = node switch
            {
                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Name" or "FullName" } access
                    when IsTypeReceiver(access.Expression) => access,
                InvocationExpressionSyntax invocation when IsGetTypeCall(invocation) && IsImplicitlyStringified(invocation) => invocation,
                _ => null,
            };
            if (site == null)
                continue;

            var line = site.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var code = site.ToString();
            var verdict = Exemptions.Any(e => e.Path == path && e.Code == code) ? Verdict.Exempt : Judge(site);
            yield return new Candidate(path, line, code, verdict);
        }
    }

    private static bool IsTypeReceiver(ExpressionSyntax receiver) => receiver switch
    {
        TypeOfExpressionSyntax => true,
        InvocationExpressionSyntax invocation => IsGetTypeCall(invocation),
        IdentifierNameSyntax id => EndsWithType(id.Identifier.ValueText),
        MemberAccessExpressionSyntax member => EndsWithType(member.Name.Identifier.ValueText),
        _ => false,
    };

    private static bool EndsWithType(string name) =>
        name.EndsWith("Type", StringComparison.Ordinal) || name.EndsWith("type", StringComparison.Ordinal);

    private static bool IsGetTypeCall(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0
        && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetType" };

    private static bool IsImplicitlyStringified(ExpressionSyntax expression) =>
        expression.Parent is InterpolationSyntax
        || (expression.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } binary
            && (IsStringish(binary.Left) || IsStringish(binary.Right)));

    private static bool IsStringish(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            or InterpolatedStringExpressionSyntax
        || (expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } nested
            && (IsStringish(nested.Left) || IsStringish(nested.Right)));

    private static Verdict Judge(ExpressionSyntax site)
    {
        if (site.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } method
            && method.Identifier.ValueText is "PyTypeName" or "PythonSimpleName" or "PyQualifiedName" or "PyClassRepr"
            && method.Ancestors().OfType<ClassDeclarationSyntax>().Any(c => c.Identifier.ValueText == "PyFormat"))
            return Verdict.Authority;

        if (site.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "StartsWith" or "EndsWith" or "Equals" or "Contains" } compared
            && compared.Expression == site)
            return Verdict.Identity;
        if (site.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression })
            return Verdict.Identity;

        var thrown = site.Ancestors().Select(a => a switch
        {
            ThrowStatementSyntax statement => statement.Expression,
            ThrowExpressionSyntax expression => expression.Expression,
            _ => null,
        }).FirstOrDefault(e => e != null);
        if (thrown is ObjectCreationExpressionSyntax creation
            && creation.Type.ToString().Replace("System.", "", StringComparison.Ordinal) is
                "ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException"
                or "InvalidOperationException" or "NotSupportedException" or "NotImplementedException"
            && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString()
                .TrimStart('$', '@', '"').StartsWith("unreachable", StringComparison.Ordinal) == true)
            return Verdict.BugThrow;

        return Verdict.Violation;
    }
}
