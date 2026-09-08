using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Tests.CodeGen;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guard: no <c>GetDisplayName()</c> invocation in <c>Semantic/</c>, <c>CodeGen/</c>,
/// <c>Discovery/</c> or <c>Project/</c> is used as a type-identity decision (#1718): an operand
/// of <c>==</c>/<c>!=</c>, an argument of <c>.Add</c>/<c>.Contains</c>/<c>.TryGetValue</c>/
/// <c>.Equals</c>/<c>.Remove</c>/<c>.ContainsKey</c>, a dictionary indexer, or a string built from
/// it (an interpolation or <c>string.Join</c>, through a lambda) that flows into one of those in
/// the same expression — the <c>visited.Add($"{...GetDisplayName()}")</c> shape the walker's
/// <c>MakeKey</c> once had. The ratchet starts EMPTY — every display-keyed identity site was
/// migrated to <see cref="Sharpy.Compiler.Semantic.SemanticType.CanonicalKey"/>.
///
/// <para>
/// Persisted-format roster (documented, NOT exempted — nothing here matches the predicate, so an
/// entry would fail drain-on-fix on its first run): <c>TypeSignature.Name</c> is assigned from
/// <c>GetDisplayName()</c> in object initializers at <c>Discovery/Caching/OverloadIndexBuilder.cs</c>
/// and <c>Semantic/Registry/BuiltinRegistry.cs</c>, and round-tripped by
/// <c>CachedModuleDiscovery.ConvertTypeSignature</c>'s string switch. That is a serialization
/// FORMAT with its own sentinels (the <c>NullableSentinel</c>), not an identity decision — the
/// #1718 comment records it as inert — and an object-initializer assignment is outside this
/// predicate by construction (<see cref="AntiVacuity_ObjectInitializerIsNotFlagged"/>).
/// </para>
/// </summary>
public class SemanticDisplayKeyScanTests
{
    private readonly ITestOutputHelper _output;

    public SemanticDisplayKeyScanTests(ITestOutputHelper output) => _output = output;

    /// <summary>The compiler directories the scan ranges over; a literal so a new directory is a decision.</summary>
    private static readonly string[] ScannedDirectories = { "Semantic", "CodeGen", "Discovery", "Project" };

    [Fact]
    public void NoDisplayKeyedSites_InSemanticCodeGenDiscoveryOrProject()
    {
        var findings = new List<string>();
        var scannedFiles = 0;
        foreach (var dir in ScannedDirectories)
        {
            var path = FindCompilerSourceDirectory(dir);
            Assert.True(Directory.Exists(path), $"scanned directory must exist: {path}");
            scannedFiles += ScanDirectory(path, findings);
        }

        foreach (var f in findings)
            _output.WriteLine(f);

        // An empty scan cannot pass: every directory must contribute files.
        Assert.True(scannedFiles > ScannedDirectories.Length * 2, $"scanned only {scannedFiles} files");
        Assert.Empty(findings);
    }

    [Fact]
    public void AntiVacuity_InterpolatedKeyIntoAddIsFlagged()
    {
        var source = @"
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        var visited = new System.Collections.Generic.HashSet<string>();
        visited.Add($""k:{t.GetDisplayName()}"");
    }
}";
        var findings = ScanSource(source, "synthetic_interpolated_key.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_JoinedKeyThroughLambdaIntoContainsIsFlagged()
    {
        var source = @"
class Test {
    void M(System.Collections.Generic.List<Sharpy.Compiler.Semantic.SemanticType> ts) {
        var seen = new System.Collections.Generic.HashSet<string>();
        if (seen.Contains(string.Join("","", ts.Select(x => x.GetDisplayName())))) { }
    }
}";
        var findings = ScanSource(source, "synthetic_joined_key.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_DisplayNameInsideKeyBuilderIsFlagged()
    {
        var source = @"
class Test {
    static string MakeKey(Sharpy.Compiler.Semantic.SemanticType t) {
        var args = string.Join("","", new[] { t.GetDisplayName() });
        return $""k|{args}"";
    }
}";
        var findings = ScanSource(source, "synthetic_key_builder.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_VoidCheckKeyMethodFormattingAMessageIsNotFlagged()
    {
        var source = @"
class Test {
    void CheckDictKey(Sharpy.Compiler.Semantic.SemanticType keyType) {
        var message = $""Dict key must be '{keyType.GetDisplayName()}'"";
        Report(message);
    }
    void Report(string m) { }
}";
        var findings = ScanSource(source, "synthetic_void_check_key.cs");
        Assert.Empty(findings);
    }

    [Fact]
    public void AntiVacuity_DisplayNameInsideMessageFormatterIsNotFlagged()
    {
        var source = @"
class Test {
    static string FormatMessage(Sharpy.Compiler.Semantic.SemanticType t) {
        return $""expected {t.GetDisplayName()}"";
    }
}";
        var findings = ScanSource(source, "synthetic_message_formatter.cs");
        Assert.Empty(findings);
    }

    [Fact]
    public void AntiVacuity_InterpolatedMessageIntoListAdd_IsFlaggedByShape()
    {
        var source = @"
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        var errors = new System.Collections.Generic.List<string>();
        errors.Add($""bad type {t.GetDisplayName()}"");
    }
}";
        // A diagnostics list is not an identity set: the predicate keys on the method name, so this
        // IS flagged by shape — the guard's price. Keep the message-only sites on `AddError`/logging
        // APIs (not `List<string>.Add`), which is what the compiler does today.
        var findings = ScanSource(source, "synthetic_message_add.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_ComparisonIsFlagged()
    {
        var source = @"
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        if (t.GetDisplayName() == ""int"") { }
    }
}";
        var findings = ScanSource(source, "synthetic_comparison.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_CollectionAddIsFlagged()
    {
        var source = @"
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        var set = new System.Collections.Generic.HashSet<string>();
        set.Add(t.GetDisplayName());
    }
}";
        var findings = ScanSource(source, "synthetic_add.cs");
        Assert.NotEmpty(findings);
    }

    [Fact]
    public void AntiVacuity_InterpolationIsNotFlagged()
    {
        var source = @"
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        var msg = $""got {t.GetDisplayName()}"";
    }
}";
        var findings = ScanSource(source, "synthetic_interpolation.cs");
        Assert.Empty(findings);
    }

    [Fact]
    public void AntiVacuity_ObjectInitializerIsNotFlagged()
    {
        var source = @"
class X { public string Name { get; set; } }
class Test {
    void M(Sharpy.Compiler.Semantic.SemanticType t) {
        var x = new X { Name = t.GetDisplayName() };
    }
}";
        var findings = ScanSource(source, "synthetic_init.cs");
        Assert.Empty(findings);
    }

    private static int ScanDirectory(string directory, List<string> findings)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            count++;
            var source = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(
                Path.GetFullPath(Path.Combine(directory, "..", "..")),
                file);
            findings.AddRange(ScanSource(source, relativePath));
        }
        return count;
    }

    private static List<string> ScanSource(string source, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root = tree.GetCompilationUnitRoot();
        var findings = new List<string>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsGetDisplayNameCall(invocation))
                continue;

            if (IsInsideGetDisplayNameImpl(invocation))
                continue;

            if (FlowsIntoFlaggedContext(invocation) || IsInsideKeyBuilder(invocation))
            {
                var location = invocation.GetLocation();
                var line = location.GetLineSpan().StartLinePosition.Line + 1;
                findings.Add($"{filePath}:{line} — GetDisplayName() used as identity key/comparison");
            }
        }

        return findings;
    }

    /// <summary>
    /// A <c>GetDisplayName()</c> inside a method whose name ends in <c>Key</c> (<c>MakeKey</c>,
    /// <c>GetTypeKey</c>, <c>BuildQualifiedTypeKey</c>, …) that RETURNS a string is an identity
    /// decision by convention: the string such a method returns is what a visited set or a
    /// dictionary is keyed on one call later, where the flow arm cannot see it. A void
    /// <c>Check…Key</c> that formats a refusal message is not a key builder. This is the arm the
    /// plan's named mutation — "revert <c>MakeKey</c> to display names" — trips.
    /// </summary>
    private static bool IsInsideKeyBuilder(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return method != null
            && method.Identifier.Text.EndsWith("Key", StringComparison.Ordinal)
            && method.ReturnType is PredefinedTypeSyntax { Keyword.Text: "string" };
    }

    private static bool IsInsideGetDisplayNameImpl(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null)
            return false;
        return method.Identifier.Text == "GetDisplayName"
            || method.Identifier.Text == "FormatTypeArgs";
    }

    private static bool IsGetDisplayNameCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Name.Identifier.Text == "GetDisplayName";
        return false;
    }

    /// <summary>
    /// The invocation itself, or a string built from it (an interpolation or a <c>string.Join</c>
    /// call, reached through lambdas and argument lists), sits in a flagged context.
    /// </summary>
    private static bool FlowsIntoFlaggedContext(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax node = invocation;
        while (true)
        {
            if (IsInFlaggedContext(node))
                return true;

            var carrier = node.Ancestors()
                .TakeWhile(a => a is not StatementSyntax)
                .OfType<ExpressionSyntax>()
                .FirstOrDefault(a => a is InterpolatedStringExpressionSyntax
                    || (a is InvocationExpressionSyntax join && IsStringJoinCall(join)));
            if (carrier == null)
                return false;
            node = carrier;
        }
    }

    private static bool IsStringJoinCall(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Join" } access
           && access.Expression is PredefinedTypeSyntax or IdentifierNameSyntax { Identifier.Text: "String" };

    private static bool IsInFlaggedContext(ExpressionSyntax invocation)
    {
        var parent = invocation.Parent;
        while (parent is ParenthesizedExpressionSyntax)
            parent = parent.Parent;

        if (parent is BinaryExpressionSyntax binary)
        {
            var kind = binary.Kind();
            if (kind == SyntaxKind.EqualsExpression || kind == SyntaxKind.NotEqualsExpression)
                return true;
        }

        if (parent is ArgumentSyntax arg && arg.Parent is ArgumentListSyntax argList
            && argList.Parent is InvocationExpressionSyntax outerCall
            && outerCall.Expression is MemberAccessExpressionSyntax outerAccess)
        {
            var methodName = outerAccess.Name.Identifier.Text;
            if (methodName is "Add" or "Contains" or "TryGetValue" or "Equals" or "Remove"
                or "ContainsKey")
                return true;
        }

        if (parent is ArgumentSyntax indexArg && indexArg.Parent is BracketedArgumentListSyntax)
            return true;

        return false;
    }

    internal static string FindSemanticSourceDirectory() => FindCompilerSourceDirectory("Semantic");

    internal static string FindCompilerSourceDirectory(string subdirectory)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler", subdirectory);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", subdirectory));
    }
}
