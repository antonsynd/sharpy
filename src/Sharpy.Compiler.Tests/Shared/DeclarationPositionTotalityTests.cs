using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

/// <summary>
/// <see cref="DeclarationPosition.Of"/> points a diagnostic at a declaration's NAME token (#2032).
/// Its switch is closed over every AST node kind that records one — every <see cref="Node"/> subtype
/// with a <c>NameLineStart</c>/<c>NameColumnStart</c> pair, discovered by reflection. A kind the
/// switch misses falls into the default arm and points at the statement's first token (the
/// <c>def</c>/<c>class</c> keyword) instead, which is the miss this guard reports.
/// </summary>
public class DeclarationPositionTotalityTests
{
    public static IEnumerable<object[]> NamedNodeKinds()
        => typeof(Node).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Node).IsAssignableFrom(t)
                && t.GetProperty("NameLineStart") != null && t.GetProperty("NameColumnStart") != null)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new object[] { t.Name });

    [Theory]
    [MemberData(nameof(NamedNodeKinds))]
    public void Of_PointsAtTheNameToken(string kind)
    {
        var type = typeof(Node).Assembly.GetTypes().Single(t => t.Name == kind && typeof(Node).IsAssignableFrom(t));
        var node = (Node)RuntimeHelpers.GetUninitializedObject(type);
        Set(node, "LineStart", 1);
        Set(node, "ColumnStart", 1);
        Set(node, "NameLineStart", 7);
        Set(node, "NameColumnStart", 9);

        Assert.Equal(new DeclarationPosition(7, 9), DeclarationPosition.Of(node));
    }

    [Fact]
    public void Of_FallsBackToTheStatement_WhenNoNamePositionIsTracked()
    {
        var node = new FunctionDef { Name = "f", LineStart = 3, ColumnStart = 1 };
        Assert.Equal(new DeclarationPosition(3, 1), DeclarationPosition.Of(node));
    }

    /// <summary>
    /// The switch's arms, read from the source, are exactly the named kinds: a missing arm is the
    /// miss above, and an arm for a kind that no longer records a name token is dead.
    /// </summary>
    [Fact]
    public void OfSwitchArms_AreExactlyTheNamedNodeKinds()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "Shared", "DeclarationPosition.cs");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
        var of = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Of");
        var arms = of.DescendantNodes().OfType<SwitchExpressionArmSyntax>()
            .Select(a => a.Pattern).OfType<DeclarationPatternSyntax>()
            .Select(p => p.Type.ToString())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var kinds = NamedNodeKinds().Select(r => (string)r[0]).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(kinds, arms);
    }

    /// <summary>The discovery is not vacuous: the kinds the emitter reports against are all found.</summary>
    [Fact]
    public void NamedNodeKinds_IncludeTheDeclarationsTheEmitterReportsAgainst()
    {
        var kinds = NamedNodeKinds().Select(r => (string)r[0]).ToHashSet();
        foreach (var expected in new[] { "FunctionDef", "ClassDef", "StructDef", "InterfaceDef", "EnumDef", "UnionDef", "DelegateDef" })
            Assert.Contains(expected, kinds);
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Could not find the repository root (no .git above the test binaries).");
    }

    private static void Set(Node node, string property, int value)
        => node.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(node, value);
}
