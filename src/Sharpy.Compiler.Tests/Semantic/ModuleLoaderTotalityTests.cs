using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>ModuleLoader.ExtractExportedSymbol</c> and
/// <c>ModuleLoader.CreateStubModuleInfo</c>: every concrete <see cref="Statement"/>
/// subtype must be classified as either handled (has a dedicated case arm) or skipped
/// (falls through to the default or is handled elsewhere). The two methods' arm sets
/// are explicitly related — stub exports must be a subset of full exports so they
/// cannot drift (#1694).
/// </summary>
public class ModuleLoaderTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/ModuleLoader.cs";

    private readonly ITestOutputHelper _output;

    public ModuleLoaderTotalityTests(ITestOutputHelper output) => _output = output;

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

    // --- ExtractExportedSymbol ---

    private static readonly HashSet<string> ExtractExportedSymbol_Handled = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(TypeAlias),
    };

    private static readonly HashSet<string> ExtractExportedSymbol_Skipped = new()
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
        nameof(PropertyDef),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
    };

    [Fact]
    public void ExtractExportedSymbol_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ExtractExportedSymbol_Handled);
        classified.UnionWith(ExtractExportedSymbol_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        _output.WriteLine($"Concrete Statement subtypes: {all.Count}");
        foreach (var name in all)
        {
            var group = ExtractExportedSymbol_Handled.Contains(name) ? "HANDLED"
                : ExtractExportedSymbol_Skipped.Contains(name) ? "SKIPPED"
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
    public void ExtractExportedSymbol_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ExtractExportedSymbol");
        Assert.NotEmpty(switchArms);

        var missing = ExtractExportedSymbol_Handled.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"ExtractExportedSymbol_Handled types missing from switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(ExtractExportedSymbol_Handled),
            $"ExtractExportedSymbol switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ExtractExportedSymbol_Handled))}\n" +
            $"  Missing: {string.Join(", ", ExtractExportedSymbol_Handled.Except(switchArms))}");
    }

    // --- CreateStubModuleInfo ---

    private static readonly HashSet<string> CreateStubModuleInfo_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
    };

    private static readonly HashSet<string> CreateStubModuleInfo_Skipped = new()
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
    public void CreateStubModuleInfo_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(CreateStubModuleInfo_Handled);
        classified.UnionWith(CreateStubModuleInfo_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void CreateStubModuleInfo_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "CreateStubModuleInfo");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(CreateStubModuleInfo_Handled),
            $"CreateStubModuleInfo switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(CreateStubModuleInfo_Handled))}\n" +
            $"  Missing: {string.Join(", ", CreateStubModuleInfo_Handled.Except(switchArms))}");
    }

    // --- Relationship between the two methods ---

    [Fact]
    public void StubExports_AreSubsetOf_FullExports()
    {
        var notInFull = CreateStubModuleInfo_Handled
            .Except(ExtractExportedSymbol_Handled).ToList();
        Assert.True(notInFull.Count == 0,
            $"CreateStubModuleInfo handles types not in ExtractExportedSymbol: " +
            $"{string.Join(", ", notInFull)}");
    }

    [Fact]
    public void FullExports_DocumentedSuperset()
    {
        var fullOnly = ExtractExportedSymbol_Handled
            .Except(CreateStubModuleInfo_Handled)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expectedFullOnly = new HashSet<string>
        {
            nameof(FunctionDef),
            nameof(VariableDeclaration),
            nameof(TypeAlias),
        };

        _output.WriteLine($"Full-only exports (not in stub): {string.Join(", ", fullOnly)}");

        Assert.True(new HashSet<string>(fullOnly).SetEquals(expectedFullOnly),
            $"Expected full-only exports: {string.Join(", ", expectedFullOnly)}\n" +
            $"  Actual: {string.Join(", ", fullOnly)}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap1 = ExtractExportedSymbol_Handled.Intersect(ExtractExportedSymbol_Skipped).ToList();
        Assert.Empty(overlap1);

        var overlap2 = CreateStubModuleInfo_Handled.Intersect(CreateStubModuleInfo_Skipped).ToList();
        Assert.Empty(overlap2);
    }
}
