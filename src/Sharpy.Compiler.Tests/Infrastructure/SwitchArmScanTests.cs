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
    public void ArmPatternTexts_ReturnsNormalizedArms_ForTuplePatternSwitch()
    {
        var arms = SwitchArmScan.ArmPatternTexts(
            "src/Sharpy.Compiler/Semantic/AugmentedCollectionAssignment.cs",
            "Classify");

        Assert.NotEmpty(arms);
        Assert.Contains("(AssignmentOperator.OrAssign, GenericType { Name: \"dict\" })", arms);
    }
}
