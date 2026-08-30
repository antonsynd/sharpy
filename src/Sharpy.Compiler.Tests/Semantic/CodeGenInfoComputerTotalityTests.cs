using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for the six Statement-dispatching switches in
/// <see cref="Sharpy.Compiler.Semantic.CodeGenInfoComputer"/>: ComputeForModule,
/// ProcessModuleLevelDeclarations, ProcessTypeMembers, EnumerateMemberNames,
/// DetectModuleLevelCollisions (two switches), and FindMemberPosition. Each method's
/// switch arms are derived from the production source via SwitchArmScan and
/// cross-checked against the Statement universe (#1694).
/// </summary>
public class CodeGenInfoComputerTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs";

    private readonly ITestOutputHelper _output;

    public CodeGenInfoComputerTotalityTests(ITestOutputHelper output) => _output = output;

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

    // --- ComputeForModule ---

    private static readonly HashSet<string> ComputeForModule_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(FunctionDef),
    };

    private static readonly HashSet<string> ComputeForModule_Skipped = new()
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
    public void ComputeForModule_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ComputeForModule_Handled);
        classified.UnionWith(ComputeForModule_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void ComputeForModule_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ComputeForModule");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ComputeForModule_Handled),
            $"ComputeForModule switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(ComputeForModule_Handled))}\n" +
            $"  Missing from switch: {string.Join(", ", ComputeForModule_Handled.Except(switchArms))}");
    }

    // --- ProcessModuleLevelDeclarations ---

    private static readonly HashSet<string> ProcessModuleLevelDeclarations_Handled = new()
    {
        nameof(VariableDeclaration),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(PropertyDef),
    };

    private static readonly HashSet<string> ProcessModuleLevelDeclarations_Skipped = new()
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
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(TypeAlias),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
    };

    [Fact]
    public void ProcessModuleLevelDeclarations_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ProcessModuleLevelDeclarations_Handled);
        classified.UnionWith(ProcessModuleLevelDeclarations_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void ProcessModuleLevelDeclarations_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ProcessModuleLevelDeclarations");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ProcessModuleLevelDeclarations_Handled),
            $"ProcessModuleLevelDeclarations switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ProcessModuleLevelDeclarations_Handled))}\n" +
            $"  Missing: {string.Join(", ", ProcessModuleLevelDeclarations_Handled.Except(switchArms))}");
    }

    // --- ProcessTypeMembers ---

    private static readonly HashSet<string> ProcessTypeMembers_Handled = new()
    {
        nameof(VariableDeclaration),
        nameof(FunctionDef),
    };

    private static readonly HashSet<string> ProcessTypeMembers_Skipped = new()
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
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
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
    public void ProcessTypeMembers_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ProcessTypeMembers_Handled);
        classified.UnionWith(ProcessTypeMembers_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void ProcessTypeMembers_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ProcessTypeMembers");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ProcessTypeMembers_Handled),
            $"ProcessTypeMembers switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ProcessTypeMembers_Handled))}\n" +
            $"  Missing: {string.Join(", ", ProcessTypeMembers_Handled.Except(switchArms))}");
    }

    // --- EnumerateMemberNames ---

    private static readonly HashSet<string> EnumerateMemberNames_Handled = new()
    {
        nameof(PropertyDef),
        nameof(EventDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(EnumDef),
        nameof(InterfaceDef),
    };

    private static readonly HashSet<string> EnumerateMemberNames_Skipped = new()
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
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
    };

    [Fact]
    public void EnumerateMemberNames_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(EnumerateMemberNames_Handled);
        classified.UnionWith(EnumerateMemberNames_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void EnumerateMemberNames_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "EnumerateMemberNames");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(EnumerateMemberNames_Handled),
            $"EnumerateMemberNames switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(EnumerateMemberNames_Handled))}\n" +
            $"  Missing: {string.Join(", ", EnumerateMemberNames_Handled.Except(switchArms))}");
    }

    // --- DetectModuleLevelCollisions (two switches) ---

    private static readonly HashSet<string> DetectModuleLevelCollisions_Handled = new()
    {
        nameof(FunctionDef),
        nameof(VariableDeclaration),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(DelegateDef),
    };

    private static readonly HashSet<string> DetectModuleLevelCollisions_Skipped = new()
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
        nameof(TypeAlias),
        nameof(PropertyDef),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(EventDef),
    };

    [Fact]
    public void DetectModuleLevelCollisions_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(DetectModuleLevelCollisions_Handled);
        classified.UnionWith(DetectModuleLevelCollisions_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void DetectModuleLevelCollisions_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "DetectModuleLevelCollisions");
        Assert.NotEmpty(switchArms);

        var missing = DetectModuleLevelCollisions_Handled.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"DetectModuleLevelCollisions types missing from switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(DetectModuleLevelCollisions_Handled),
            $"DetectModuleLevelCollisions switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(DetectModuleLevelCollisions_Handled))}\n" +
            $"  Missing: {string.Join(", ", DetectModuleLevelCollisions_Handled.Except(switchArms))}");
    }

    // --- FindMemberPosition ---

    private static readonly HashSet<string> FindMemberPosition_Handled = new()
    {
        nameof(VariableDeclaration),
        nameof(FunctionDef),
        nameof(PropertyDef),
        nameof(EventDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(EnumDef),
        nameof(InterfaceDef),
    };

    private static readonly HashSet<string> FindMemberPosition_Skipped = new()
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
        nameof(TypeAlias),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
    };

    [Fact]
    public void FindMemberPosition_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(FindMemberPosition_Handled);
        classified.UnionWith(FindMemberPosition_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void FindMemberPosition_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "FindMemberPosition");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(FindMemberPosition_Handled),
            $"FindMemberPosition switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(FindMemberPosition_Handled))}\n" +
            $"  Missing: {string.Join(", ", FindMemberPosition_Handled.Except(switchArms))}");
    }

    // --- Cross-method union covers the Statement universe ---

    [Fact]
    public void AllMethods_UnionCoversStatementUniverse()
    {
        var all = GetConcreteStatementNames();

        var union = new HashSet<string>();
        union.UnionWith(ComputeForModule_Handled);
        union.UnionWith(ComputeForModule_Skipped);
        union.UnionWith(ProcessModuleLevelDeclarations_Handled);
        union.UnionWith(ProcessModuleLevelDeclarations_Skipped);
        union.UnionWith(ProcessTypeMembers_Handled);
        union.UnionWith(ProcessTypeMembers_Skipped);
        union.UnionWith(EnumerateMemberNames_Handled);
        union.UnionWith(EnumerateMemberNames_Skipped);
        union.UnionWith(DetectModuleLevelCollisions_Handled);
        union.UnionWith(DetectModuleLevelCollisions_Skipped);
        union.UnionWith(FindMemberPosition_Handled);
        union.UnionWith(FindMemberPosition_Skipped);

        var uncovered = all.Where(n => !union.Contains(n)).ToList();
        Assert.Empty(uncovered);
    }
}
