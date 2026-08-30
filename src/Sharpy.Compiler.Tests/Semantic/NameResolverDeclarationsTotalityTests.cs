using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>NameResolver.ResolveDeclaration</c> and
/// <c>NameResolver.ResolveNestedTypeDeclaration</c>: every concrete
/// <see cref="Statement"/> subtype must be classified as either handled (has a dedicated
/// case arm) or skipped (falls through, handled in later passes). A new Statement subtype
/// that is not listed here will fail this test (#1694).
/// </summary>
public class NameResolverDeclarationsTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/NameResolver.Declarations.cs";

    private readonly ITestOutputHelper _output;

    public NameResolverDeclarationsTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcreteStatementNames()
    {
        var statementBaseType = typeof(Statement);
        return statementBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    // --- ResolveDeclaration ---

    private static readonly HashSet<string> ResolveDeclaration_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(FunctionDef),
        nameof(VariableDeclaration),
        nameof(TypeAlias),
        nameof(PropertyDef),
    };

    private static readonly HashSet<string> ResolveDeclaration_Skipped = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(BreakWithFlagStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(EventDef),
    };

    [Fact]
    public void ResolveDeclaration_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ResolveDeclaration_Handled);
        classified.UnionWith(ResolveDeclaration_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        _output.WriteLine($"Concrete Statement subtypes: {all.Count}");
        foreach (var name in all)
        {
            var group = ResolveDeclaration_Handled.Contains(name) ? "HANDLED"
                : ResolveDeclaration_Skipped.Contains(name) ? "SKIPPED"
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
    public void ResolveDeclaration_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ResolveDeclaration");
        Assert.NotEmpty(switchArms);

        var missing = ResolveDeclaration_Handled.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"ResolveDeclaration_Handled types missing from switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(ResolveDeclaration_Handled),
            $"ResolveDeclaration switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ResolveDeclaration_Handled))}\n" +
            $"  Missing: {string.Join(", ", ResolveDeclaration_Handled.Except(switchArms))}");
    }

    // --- ResolveNestedTypeDeclaration ---

    private static readonly HashSet<string> ResolveNestedTypeDeclaration_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
    };

    private static readonly HashSet<string> ResolveNestedTypeDeclaration_Skipped = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(VariableDeclaration),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(BreakWithFlagStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(FunctionDef),
        nameof(TypeAlias),
        nameof(PropertyDef),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
    };

    [Fact]
    public void ResolveNestedTypeDeclaration_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ResolveNestedTypeDeclaration_Handled);
        classified.UnionWith(ResolveNestedTypeDeclaration_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void ResolveNestedTypeDeclaration_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ResolveNestedTypeDeclaration");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ResolveNestedTypeDeclaration_Handled),
            $"ResolveNestedTypeDeclaration switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ResolveNestedTypeDeclaration_Handled))}\n" +
            $"  Missing: {string.Join(", ", ResolveNestedTypeDeclaration_Handled.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap1 = ResolveDeclaration_Handled.Intersect(ResolveDeclaration_Skipped).ToList();
        Assert.Empty(overlap1);

        var overlap2 = ResolveNestedTypeDeclaration_Handled
            .Intersect(ResolveNestedTypeDeclaration_Skipped).ToList();
        Assert.Empty(overlap2);
    }

    [Fact]
    public void ResolveNestedTypeDeclaration_IsSubsetOf_ResolveDeclaration()
    {
        var notInResolve = ResolveNestedTypeDeclaration_Handled
            .Except(ResolveDeclaration_Handled).ToList();
        Assert.True(notInResolve.Count == 0,
            $"ResolveNestedTypeDeclaration handles types not in ResolveDeclaration: " +
            $"{string.Join(", ", notInResolve)}");
    }
}
