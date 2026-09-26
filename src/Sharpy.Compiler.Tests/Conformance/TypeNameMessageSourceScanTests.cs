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
/// <para><b>What it scans.</b> A Roslyn walk over every <c>src/Sharpy.Core</c> and
/// <c>src/Sharpy.Stdlib</c> source file (bin/obj excluded), parsed once per target framework's
/// preprocessor symbols (<see cref="TargetSymbolSets"/>: <c>net10.0</c> and <c>netstandard2.1</c>),
/// so an <c>#if NET10_0_OR_GREATER</c> region — disabled trivia to a symbol-less parse — is scanned
/// like any other line; each project's files are bound together in one compilation so a receiver's
/// type is known. A <i>Type receiver</i> is an expression whose bound type is <c>System.Type</c> (or a
/// subclass), or — syntactically, for a site the binder cannot resolve — <c>X.GetType()</c>,
/// <c>typeof(…)</c> or a name ending in <c>Type</c>/<c>type</c>. A <i>candidate</i> is
/// (a) a <c>.Name</c>/<c>.FullName</c>/<c>.AssemblyQualifiedName</c> read on a Type receiver,
/// (b) a <c>.ToString()</c> call on a Type receiver, or (c) a Type receiver used as a string-concat
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
    /// 8 → 10 when the scan learned <c>.ToString()</c> on a Type receiver and the per-TFM parse:
    /// PyFormat.PyClassRepr's own <c>type.ToString()</c> (authority) and the TypedLoadContract row
    /// of <see cref="Exemptions"/> (pending; 9 once that row is dropped).
    /// </summary>
    private const int ExpectedSanctionedCandidates = 10;

    /// <summary>
    /// Sites that spell a CLR type name into text on purpose: (repository-relative path, the
    /// candidate's code, why + issue). Empty at landing.
    /// </summary>
    private static readonly (string Path, string Code, string Reason)[] Exemptions =
    [
        // routed by the stdlib lane (refs #2099)
        ("src/Sharpy.Stdlib/TypedLoadContract.cs", "target.Name", "routed by the stdlib lane (refs #2099)"),
    ];

    /// <summary>
    /// The preprocessor symbols each target framework compiles Core and Stdlib under — the
    /// conditional-compilation symbols their sources branch on (<c>#if NET10_0_OR_GREATER</c>,
    /// <c>#if !NET10_0_OR_GREATER</c>, <c>#if NETSTANDARD2_1</c>). Every region is live under one set.
    /// </summary>
    private static readonly string[][] TargetSymbolSets = [["NET10_0_OR_GREATER"], ["NETSTANDARD2_1"]];

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
    // ToString() on a Type receiver is the implicit stringification spelled out.
    [InlineData("class C { void M(object v) { throw new TypeError(\"x \" + v.GetType().ToString()); } }", 1, 0)]
    [InlineData("class C { string M(System.Type t) { return t.ToString(); } }", 1, 0)]
    // A Type-typed variable whose name does not end in Type is a Type receiver by its bound type.
    [InlineData("class C { string M(object v) { System.Type t = v.GetType(); return \"x \" + t.FullName; } }", 1, 0)]
    [InlineData("class C { string M(System.Type t) { return $\"x {t}\"; } }", 1, 0)]
    // A region only one target framework compiles is scanned under that framework's symbols.
    [InlineData("class C {\n#if NET10_0_OR_GREATER\n string M(object v) { return \"x\" + v.GetType().Name; }\n#endif\n}", 1, 0)]
    [InlineData("class C {\n#if NETSTANDARD2_1\n string M(object v) { return \"x\" + v.GetType().Name; }\n#endif\n}", 1, 0)]
    public void Classifier_PositiveControls(string source, int expectedViolations, int expectedSanctioned)
    {
        var candidates = ScanProject([("Synthetic.cs", source)]).ToList();
        Assert.Equal(expectedViolations, candidates.Count(c => c.Verdict == Verdict.Violation));
        Assert.Equal(expectedSanctioned, candidates.Count(c => c.Verdict != Verdict.Violation));
    }

    private static IEnumerable<Candidate> ScanSources()
    {
        foreach (var project in new[] { "Sharpy.Core", "Sharpy.Stdlib" })
        {
            var root = Path.Combine(FixtureRoots.RepositoryRoot, "src", project);
            var files = new List<(string Path, string Text)>();
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/');
                if (rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal))
                    continue;
                files.Add((rel, File.ReadAllText(file)));
            }
            foreach (var candidate in ScanProject(files))
                yield return candidate;
        }
    }

    /// <summary>
    /// One project's candidates: its files parsed under each <see cref="TargetSymbolSets"/> entry and
    /// bound together, deduplicated across the sets (a line outside any <c>#if</c> is seen by both).
    /// </summary>
    private static IEnumerable<Candidate> ScanProject(IReadOnlyList<(string Path, string Text)> files)
    {
        var seen = new HashSet<(string, int, string)>();
        foreach (var symbols in TargetSymbolSets)
        {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest).WithPreprocessorSymbols(symbols);
            var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Text, options, path: f.Path)).ToList();
            var compilation = CSharpCompilation.Create("TypeNameScan", trees, IntegrationTestBase.GetSharedReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var systemType = compilation.GetTypeByMetadataName("System.Type")
                ?? throw new InvalidOperationException("System.Type does not resolve: the binder would see no Type receiver");
            foreach (var tree in trees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var candidate in Classify(tree.FilePath, tree.GetRoot(), model, systemType))
                {
                    if (seen.Add((candidate.Path, candidate.Line, candidate.Code)))
                        yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<Candidate> Classify(string path, SyntaxNode root, SemanticModel model, INamedTypeSymbol systemType)
    {
        foreach (var node in root.DescendantNodes())
        {
            ExpressionSyntax? site = node switch
            {
                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Name" or "FullName" or "AssemblyQualifiedName" } access
                    when IsTypeReceiver(access.Expression, model, systemType) => access,
                InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0, Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } toString } invocation
                    when IsTypeReceiver(toString.Expression, model, systemType) => invocation,
                ExpressionSyntax expression
                    when IsImplicitlyStringified(expression, model) && IsTypeValue(expression, model, systemType) => expression,
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

    private static bool IsTypeReceiver(ExpressionSyntax receiver, SemanticModel model, INamedTypeSymbol systemType) =>
        IsSyntacticTypeReceiver(receiver) || IsBoundToType(model.GetTypeInfo(receiver).Type, systemType);

    /// <summary>
    /// A stringified operand is a Type when it is <c>X.GetType()</c>/<c>typeof(…)</c> or bound to
    /// <c>System.Type</c> — not by the <c>*Type</c> name heuristic, which a string such as a MIME
    /// <c>subtype</c> also matches.
    /// </summary>
    private static bool IsTypeValue(ExpressionSyntax expression, SemanticModel model, INamedTypeSymbol systemType) =>
        expression is TypeOfExpressionSyntax
        || (expression is InvocationExpressionSyntax invocation && IsGetTypeCall(invocation))
        || IsBoundToType(model.GetTypeInfo(expression).Type, systemType);

    private static bool IsBoundToType(ITypeSymbol? type, INamedTypeSymbol systemType)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, systemType))
                return true;
        }
        return false;
    }

    private static bool IsSyntacticTypeReceiver(ExpressionSyntax receiver) => receiver switch
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

    private static bool IsImplicitlyStringified(ExpressionSyntax expression, SemanticModel model) =>
        expression.Parent is InterpolationSyntax
        || (expression.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } binary
            && (IsStringish(binary.Left) || IsStringish(binary.Right)
                || model.GetTypeInfo(binary).Type?.SpecialType == SpecialType.System_String));

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
