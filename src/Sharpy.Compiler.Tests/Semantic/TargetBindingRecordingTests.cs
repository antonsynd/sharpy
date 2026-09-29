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

    // ================================================================
    // The binding law (#1974, P21a): store kind × spelling × predecessor
    // ================================================================

    /// <summary>The binding-law cells that compile: a refused cell (SPY0225) records nothing the contract names.</summary>
    public static TheoryData<string> BindingLawRecordedCellIds()
    {
        var data = new TheoryData<string>();
        foreach (var cell in BlockKinds.BindingLawCells())
            if (cell.ExpectedCode == null)
                data.Add($"{cell.Kind}/{cell.Cell}");
        return data;
    }

    /// <summary>
    /// Every name the store binds records <c>Rebinds</c> with a predecessor link iff the spelling's
    /// law writes through (<see cref="BlockKinds.StoreWritesThrough"/>), and <c>Declares</c> with NO
    /// predecessor link otherwise — so every <c>let</c> target, at every kind and over every
    /// predecessor, is a fresh chain root. The store node itself carries the spelling's
    /// <c>IsLet</c>, so a cell cannot pass on a program the parser read as another spelling.
    /// Mutation (commit body): with the checker ignoring <c>IsLet</c>, the <c>let</c>,
    /// <c>let-tuple</c> and <c>let-star</c> cells over an existing non-const predecessor record
    /// <c>Rebinds</c> and are red.
    /// </summary>
    [Theory]
    [MemberData(nameof(BindingLawRecordedCellIds))]
    public void BindingLaw_StoreRecordsTheSpellingsBinding(string id)
    {
        var cell = BlockKinds.BindingLawCellById(id);
        var a = LocalBindingTestHarness.Analyze(cell.Source);
        var writesThrough = BlockKinds.StoreWritesThrough(cell.Spelling, cell.Predecessor);

        BlockKinds.Cell(cell.Kind, "BindingLawRecorded/" + cell.Cell, () =>
        {
            var bound = StoreBindings(a, cell.Spelling);
            bound.Should().NotBeEmpty(id);
            foreach (var (name, node, symbol) in bound)
            {
                // Only the stored name `x` can have a predecessor; `y` / `rest` never exist before.
                var rebinds = writesThrough && name == "x";
                a.BindingOf(node).Should().Be(rebinds ? TargetBindingKind.Rebinds : TargetBindingKind.Declares, $"{id}: {name}");
                symbol.Should().NotBeNull($"{id}: {name} has a symbol");
                var hasPredecessor = !ReferenceEquals(a.Info.GetRootBinding(symbol!), symbol);
                hasPredecessor.Should().Be(rebinds, $"{id}: {name} predecessor link");
            }
        });
    }

    /// <summary>The names the cell's store binds, each with the node its binding is recorded on and its symbol.</summary>
    private static System.Collections.Generic.List<(string Name, Node Node, VariableSymbol? Symbol)> StoreBindings(
        LocalBindingTestHarness.Analysis a, BlockKinds.Spelling spelling)
    {
        var result = new System.Collections.Generic.List<(string, Node, VariableSymbol?)>();
        var nodes = LocalBindingTestHarness.Descendants(a.Module).ToList();
        switch (spelling.Id)
        {
            case "annotated" or "let-annotated":
                {
                    var decl = nodes.OfType<VariableDeclaration>().Last(d => d.Name == "x");
                    decl.IsLet.Should().Be(spelling.IsLet, spelling.Id);
                    result.Add(("x", decl, a.Info.GetDeclarationSymbol(decl) as VariableSymbol));
                    break;
                }
            case "bare" or "let":
                {
                    var store = nodes.OfType<Assignment>().Last(s => s.Target is Identifier { Name: "x" });
                    store.IsLet.Should().Be(spelling.IsLet, spelling.Id);
                    var id = (Identifier)store.Target;
                    result.Add(("x", id, a.Info.GetIdentifierSymbol(id) as VariableSymbol));
                    break;
                }
            default:
                {
                    var store = nodes.OfType<Assignment>().Last(s => s.Target is TupleLiteral);
                    store.IsLet.Should().Be(spelling.IsLet, spelling.Id);
                    foreach (var element in ((TupleLiteral)store.Target).Elements)
                    {
                        var id = element switch
                        {
                            Identifier i => i,
                            StarExpression { Operand: Identifier i } => i,
                            _ => throw new System.InvalidOperationException($"unexpected tuple target element {element.GetType().Name}"),
                        };
                        result.Add((id.Name, id, a.Info.GetIdentifierSymbol(id) as VariableSymbol));
                    }

                    break;
                }
        }

        return result;
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
