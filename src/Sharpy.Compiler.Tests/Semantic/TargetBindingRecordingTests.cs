using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1560 D1 §2: every binding node carries a <see cref="TargetBinding"/> — <c>Rebinds</c> iff the
/// checker linked a predecessor, <c>Declares</c> otherwise — and the emitter reads it instead of
/// deciding. Block kind × cell for the assignment target, plus the construct binders.
/// Mutation (recorded in the commit body): dropping the cross-scope predecessor link in
/// <c>CheckAssignment</c> turns the outer-reassign cells <c>Declares</c> and this file red.
/// </summary>
public class TargetBindingRecordingTests
{
    public static TheoryData<string> BodyKinds()
    {
        var data = new TheoryData<string>();
        foreach (var kind in BlockKinds.BodyKinds)
            data.Add(kind);
        return data;
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void SiblingRedeclare_SecondBlockDeclares(string kind)
    {
        var source = BlockKinds.Program(
            BlockKinds.Wrap(kind, 1, "x = 1\nprint(x)") + BlockKinds.Wrap(kind, 2, "x = 2\nprint(x)"));
        var a = LocalBindingTestHarness.Analyze(source);
        BlockKinds.Cell(kind, "SiblingRedeclareRecorded", () =>
        {
            a.BindingOf(a.AssignmentTarget("x", 0)).Should().Be(TargetBindingKind.Declares, kind);
            a.BindingOf(a.AssignmentTarget("x", 1)).Should().Be(TargetBindingKind.Declares, kind);
        });
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void OuterDeclaredReassignInside_Rebinds(string kind)
    {
        var source = BlockKinds.Program("    x = 10\n" + BlockKinds.Wrap(kind, 1, "x = 20") + "    print(x)\n");
        var a = LocalBindingTestHarness.Analyze(source);
        BlockKinds.Cell(kind, "OuterReassignRecorded", () =>
        {
            a.BindingOf(a.AssignmentTarget("x", 0)).Should().Be(TargetBindingKind.Declares, kind);
            a.BindingOf(a.AssignmentTarget("x", 1)).Should().Be(TargetBindingKind.Rebinds, kind);
        });
    }

    [Fact]
    public void NestedDefAssignmentToOuter_Rebinds()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def main() -> None:\n    x = 10\n    def inner() -> None:\n        x = 20\n    inner()\n    print(x)\n");
        a.BindingOf(a.AssignmentTarget("x", 1)).Should().Be(TargetBindingKind.Rebinds);
        a.Info.GetBindingChain(a.Info.GetIdentifierSymbol(a.AssignmentTarget("x", 1))!).Should().HaveCount(2);
    }

    [Fact]
    public void ModuleVariableAssignedInFunction_Rebinds()
    {
        var a = LocalBindingTestHarness.Analyze("counter: int = 0\n\ndef bump() -> None:\n    counter = 1\n");
        a.BindingOf(a.AssignmentTarget("counter", 0)).Should().Be(TargetBindingKind.Rebinds);
    }

    [Fact]
    public void ForTarget_Declares()
    {
        var a = LocalBindingTestHarness.Analyze("def main() -> None:\n    for i in range(3):\n        pass\n");
        var target = LocalBindingTestHarness.Descendants(a.Module).OfType<ForStatement>().Single().Target;
        a.BindingOf(target).Should().Be(TargetBindingKind.Declares);
    }

    [Fact]
    public void ForTupleElements_Declare()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def main() -> None:\n    d: dict[str, int] = {\"a\": 1}\n    for k, v in d.items():\n        pass\n");
        var tuple = (TupleLiteral)LocalBindingTestHarness.Descendants(a.Module).OfType<ForStatement>().Single().Target;
        foreach (var element in tuple.Elements)
            a.BindingOf(element).Should().Be(TargetBindingKind.Declares);
    }

    [Fact]
    public void ComprehensionTarget_Declares()
    {
        var a = LocalBindingTestHarness.Analyze("def main() -> None:\n    ys: list[int] = [i for i in range(3)]\n");
        var target = LocalBindingTestHarness.Descendants(a.Module).OfType<ForClause>().Single().Target;
        a.BindingOf(target).Should().Be(TargetBindingKind.Declares);
    }

    [Fact]
    public void MatchCapture_Declares()
    {
        var a = LocalBindingTestHarness.Analyze("def main() -> None:\n    match 7:\n        case n:\n            pass\n");
        var pattern = LocalBindingTestHarness.Descendants(a.Module).OfType<BindingPattern>().Single();
        a.BindingOf(pattern).Should().Be(TargetBindingKind.Declares);
    }

    [Fact]
    public void WalrusFirstBinding_Declares_AndRebindRebinds()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def main() -> None:\n    if (n := 1) > 0:\n        pass\n    if (n := 2) > 0:\n        pass\n");
        var walruses = LocalBindingTestHarness.Descendants(a.Module).OfType<WalrusExpression>().ToList();
        a.BindingOf(walruses[0]).Should().Be(TargetBindingKind.Declares);
        a.BindingOf(walruses[1]).Should().Be(TargetBindingKind.Rebinds);
        var second = a.Info.GetWalrusSymbol(walruses[1])!;
        a.Info.GetBindingChain(second).Should().HaveCount(2);
        a.Info.GetBindingChain(second)[0].Should().BeSameAs(a.Info.GetWalrusSymbol(walruses[0]));
    }

    [Fact]
    public void WalrusOverOuterLocal_Rebinds()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def main() -> None:\n    x = 10\n    if (x := 30) > 0:\n        pass\n");
        var walrus = LocalBindingTestHarness.Descendants(a.Module).OfType<WalrusExpression>().Single();
        a.BindingOf(walrus).Should().Be(TargetBindingKind.Rebinds);
        a.Info.GetBindingChain(a.Info.GetWalrusSymbol(walrus)!)[0]
            .Should().BeSameAs(a.Info.GetIdentifierSymbol(a.AssignmentTarget("x", 0)));
    }

    [Fact]
    public void InlineOutFirstBinding_Declares_AndRebindRebinds()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def tp(s: str, r: out int) -> bool:\n    r = 1\n    return True\n\n"
            + "def main() -> None:\n    if tp(\"1\", out v: int):\n        pass\n    if tp(\"2\", out v: int):\n        pass\n");
        var outs = LocalBindingTestHarness.Descendants(a.Module).OfType<ModifiedArgument>()
            .Where(m => m.InlineName != null).ToList();
        a.BindingOf(outs[0]).Should().Be(TargetBindingKind.Declares);
        a.BindingOf(outs[1]).Should().Be(TargetBindingKind.Rebinds);
        a.Info.GetBindingChain(a.Info.GetInlineOutSymbol(outs[1])!)[0]
            .Should().BeSameAs(a.Info.GetInlineOutSymbol(outs[0]));
    }
}
