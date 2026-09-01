using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <see cref="Sharpy.Compiler.Semantic.TypeChecker"/>'s
/// CheckDeferBodyControlFlow switch over Statement kinds. Every concrete Statement
/// subtype must be classified. A new subtype fails this test, forcing classification.
/// </summary>
public class DeferBodyControlFlowTotalityTests
{
    private readonly ITestOutputHelper _output;
    public DeferBodyControlFlowTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> Escapes = new()
    {
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(BreakStatement),
        nameof(ContinueStatement),
    };

    private static readonly HashSet<string> Recurses = new()
    {
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(IfStatement),
        nameof(WithStatement),
        nameof(TryStatement),
        nameof(DeferStatement),
        nameof(MatchStatement),
        nameof(DecoratedStatement),
    };

    private static readonly HashSet<string> ScopeBoundaryOrLeaf = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(Assignment),
        nameof(VariableDeclaration),
        nameof(ExpressionStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(PassStatement),
        nameof(RaiseStatement),
        nameof(AssertStatement),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(EventDef),
        nameof(BreakWithFlagStatement),
    };

    [Fact]
    public void AllConcreteStatementSubtypes_AreClassified()
    {
        var statementType = typeof(Statement);
        var concreteStatements = statementType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementType) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var allClassified = new HashSet<string>(Escapes);
        allClassified.UnionWith(Recurses);
        allClassified.UnionWith(ScopeBoundaryOrLeaf);

        var unclassified = concreteStatements.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteStatements.Contains(n)).ToList();

        foreach (var name in concreteStatements)
        {
            var group = Escapes.Contains(name) ? "ESCAPE"
                : Recurses.Contains(name) ? "RECURSE"
                : ScopeBoundaryOrLeaf.Contains(name) ? "SCOPE-BOUNDARY/LEAF"
                : "*** UNCLASSIFIED ***";
            _output.WriteLine($"  {name,-30} {group}");
        }

        if (unclassified.Count > 0)
            _output.WriteLine($"\nUnclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom (listed but not found): {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void SwitchArms_MatchEscapesAndRecurses()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs",
            "CheckDeferBodyControlFlow");

        var expected = new HashSet<string>(Escapes);
        expected.UnionWith(Recurses);

        Assert.True(switchArms.SetEquals(expected),
            $"CheckDeferBodyControlFlow switch arms differ from Escapes + Recurses.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(expected))}\n" +
            $"  Missing from switch: {string.Join(", ", expected.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        Assert.Empty(Escapes.Intersect(Recurses));
        Assert.Empty(Escapes.Intersect(ScopeBoundaryOrLeaf));
        Assert.Empty(Recurses.Intersect(ScopeBoundaryOrLeaf));
    }

    [Fact]
    public void ArmPatternTexts_PinBreakContinueGuards()
    {
        var armTexts = SwitchArmScan.ArmPatternTexts(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs",
            "CheckDeferBodyControlFlow");

        Assert.Contains(armTexts, t => t.Contains("BreakStatement"));
        Assert.Contains(armTexts, t => t.Contains("ContinueStatement"));

        // Roslyn's Pattern node excludes the WhenClause, so verify the guards via source text.
        var repoRoot = AppContext.BaseDirectory;
        while (repoRoot != null
               && !Directory.Exists(Path.Combine(repoRoot, ".git"))
               && !File.Exists(Path.Combine(repoRoot, ".git")))
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        var source = File.ReadAllText(Path.Combine(repoRoot!,
            "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs"));
        Assert.Contains("BreakStatement brk when !insideLoop", source);
        Assert.Contains("ContinueStatement cont when !insideLoop", source);
    }
}
