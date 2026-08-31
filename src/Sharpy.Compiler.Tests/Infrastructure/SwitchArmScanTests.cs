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
    public void CaseTypeNames_PerContainingType_AstVisitorVoid_Returns94Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor");

        Assert.Equal(94, arms.Count);
        Assert.Contains("FStringLiteral", arms);
    }

    [Fact]
    public void CaseTypeNames_PerContainingType_AstVisitorGeneric_Returns94Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs",
            "Visit",
            "AstVisitor`1");

        Assert.Equal(94, arms.Count);
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
            "Both overloads should match the same 94 types today");
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
