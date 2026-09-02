using Xunit;

namespace Sharpy.Compiler.Tests.Infrastructure;

public class SwitchArmScanTests
{
    [Fact]
    public void CaseTypeNames_ReturnsNonEmptySet_ForBuildStatement()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "BuildStatement");

        Assert.NotEmpty(arms);
        Assert.Contains("ReturnStatement", arms);
    }

    [Fact]
    public void CaseTypeNames_ThrowsOnMissingMethod()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SwitchArmScan.CaseTypeNames(
                "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
                "NonExistentMethod"));
    }

    [Fact]
    public void CaseTypeNames_WorksOnSwitchExpressions()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Shared/ExhaustivenessHelper.cs",
            "IsIrrefutable");

        Assert.NotEmpty(arms);
        Assert.Contains("WildcardPattern", arms);
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_AstVisitorVoid_Returns93Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor");

        Assert.Equal(93, arms.Count);
        Assert.Contains("FStringLiteral", arms);
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_AstVisitorGeneric_Returns93Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor`1");

        Assert.Equal(93, arms.Count);
        Assert.Contains("FStringLiteral", arms);
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_WrongTypeName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SwitchArmScan.CaseTypeNames(
                "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
                "Visit",
                "NonExistentType"));
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_FilterDiscriminatesByDispatchForm()
    {
        // The two Visit overloads' rosters coincide (93 == 93), so roster assertions cannot
        // detect a filter regression that silently merges the overloads (the plan-e31e76
        // verify-round warning). The overloads differ in dispatch FORM: the void overload is
        // one switch STATEMENT, the generic overload one switch EXPRESSION. A merged
        // (filter-ignoring) scan reports both forms for both filters.
        var voidForms = SwitchArmScan.DispatchFormCounts(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs", "Visit", "AstVisitor");
        var genericForms = SwitchArmScan.DispatchFormCounts(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs", "Visit", "AstVisitor`1");

        Assert.Equal((1, 0), voidForms);
        Assert.Equal((0, 1), genericForms);
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_OverloadsAreSeparate()
    {
        var voidArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor");
        var genericArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor`1");

        Assert.True(voidArms.SetEquals(genericArms),
            "Both overloads should match the same 93 types today");
    }

    [Fact]
    public void ArmPatternTexts_ReturnsNormalizedArms_ForTuplePatternSwitch()
    {
        var arms = SwitchArmScan.ArmPatternTexts(
            "src/Sharpy.Compiler/Semantic/AugmentedCollectionAssignment.cs",
            "Classify");

        Assert.NotEmpty(arms);
        Assert.Contains("(AssignmentOperator.OrAssign, GenericType { Name: \"dict\" })", arms);
    }
}
