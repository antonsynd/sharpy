using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Tests.CodeGen;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guard: no <c>GetDisplayName()</c> invocation in <c>Semantic/</c> or <c>CodeGen/</c> is used
/// as a comparison operand or a collection key (#1718). The ratchet starts EMPTY — every
/// display-keyed identity site was migrated to <see cref="Sharpy.Compiler.Semantic.SemanticType.CanonicalKey"/>.
///
/// <para>
/// The persisted-format sites (<c>TypeSignature.Name</c> in <c>Discovery/</c> and
/// <c>BuiltinRegistry</c>) are object-initializer assignments outside this predicate by
/// construction, and <c>OverloadIndexBuilder.cs</c> is outside <c>Semantic/</c>+<c>CodeGen/</c>.
/// </para>
/// </summary>
public class SemanticDisplayKeyScanTests
{
    private readonly ITestOutputHelper _output;

    public SemanticDisplayKeyScanTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void NoDisplayKeyedSites_InSemanticOrCodeGen()
    {
        var semanticDir = FindSemanticSourceDirectory();
        var codeGenDir = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();

        var findings = new List<string>();
        ScanDirectory(semanticDir, findings);
        ScanDirectory(codeGenDir, findings);

        foreach (var f in findings)
            _output.WriteLine(f);

        Assert.Empty(findings);
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

    private static void ScanDirectory(string directory, List<string> findings)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(
                Path.GetFullPath(Path.Combine(directory, "..", "..")),
                file);
            findings.AddRange(ScanSource(source, relativePath));
        }
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

            if (IsInFlaggedContext(invocation))
            {
                var location = invocation.GetLocation();
                var line = location.GetLineSpan().StartLinePosition.Line + 1;
                findings.Add($"{filePath}:{line} — GetDisplayName() used as identity key/comparison");
            }
        }

        return findings;
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

    private static bool IsInFlaggedContext(InvocationExpressionSyntax invocation)
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

    internal static string FindSemanticSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var semanticPath = Path.Combine(current, "src", "Sharpy.Compiler", "Semantic");
            if (Directory.Exists(semanticPath))
                return semanticPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "Semantic"));
    }
}
