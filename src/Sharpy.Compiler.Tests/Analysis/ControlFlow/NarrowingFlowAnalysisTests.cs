using System.Collections.Immutable;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Unit tests for <see cref="NarrowingFlowAnalysis"/> — the forward dataflow engine that computes
/// symbolic type-narrowing facts (RemoveNone / IsType) over a control flow graph.
/// </summary>
public class NarrowingFlowAnalysisTests
{
    #region Fact assertion helpers

    private static bool HasRemoveNone(IReadOnlyCollection<NarrowingFact> facts, string key) =>
        facts.Any(f => f.Kind == NarrowingActionKind.RemoveNone && f.Key == key);

    private static bool HasIsType(IReadOnlyCollection<NarrowingFact> facts, string key, string typeKey) =>
        facts.Any(f => f.Kind == NarrowingActionKind.IsType && f.Key == key && f.TypeKey == typeKey);

    #endregion

    #region AST construction helpers

    private static NoneLiteral None() => new NoneLiteral();

    private static BinaryOp IsNotNone(string name) =>
        new BinaryOp { Operator = BinaryOperator.IsNot, Left = Id(name), Right = None() };

    private static BinaryOp IsNone(string name) =>
        new BinaryOp { Operator = BinaryOperator.Is, Left = Id(name), Right = None() };

    private static MemberAccess Member(string objectName, string member) =>
        new MemberAccess { Object = Id(objectName), Member = member };

    private static BinaryOp MemberIsNotNone(string objectName, string member) =>
        new BinaryOp { Operator = BinaryOperator.IsNot, Left = Member(objectName, member), Right = None() };

    private static FunctionCall IsInstance(string name, string typeName) =>
        new FunctionCall
        {
            Function = Id("isinstance"),
            Arguments = ImmutableArray.Create<Expression>(Id(name), Id(typeName))
        };

    private static AssertStatement Assert_(Expression test) => new AssertStatement { Test = test };

    private static Assignment Assign(Expression target) =>
        new Assignment { Target = target, Value = Int(0) };

    #endregion

    [Fact]
    public void NoNarrowingConditions_ProducesEmptyFacts()
    {
        var stmt = Assign(Id("y"));
        var cfg = CreateLinearCfg(stmt);

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        Assert.Empty(result.FactsBefore(stmt));
    }

    [Fact]
    public void StraightLine_AssertGeneratesFactForFollowingStatements()
    {
        var assert = Assert_(IsNotNone("x"));
        var following = Assign(Id("y"));
        var cfg = CreateLinearCfg(assert, following);

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        // The assert itself sees no narrowing yet; the statement after it does.
        Assert.False(HasRemoveNone(result.FactsBefore(assert), "x"));
        Assert.True(HasRemoveNone(result.FactsBefore(following), "x"));
    }

    [Fact]
    public void IfNotNone_NarrowsThenBranchOnly()
    {
        var cfg = CreateDiamondCfg(IsNotNone("x"),
            thenStatements: new Statement[] { Pass() },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        var thenBlock = cfg.Blocks.Single(b => b.Label == "then");
        var elseBlock = cfg.Blocks.Single(b => b.Label == "else");

        Assert.True(HasRemoveNone(result.FactsAtEntry(thenBlock), "x"));
        Assert.False(HasRemoveNone(result.FactsAtEntry(elseBlock), "x"));
    }

    [Fact]
    public void IfIsNone_NarrowsElseBranchOnly()
    {
        var cfg = CreateDiamondCfg(IsNone("x"),
            thenStatements: new Statement[] { Pass() },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        var thenBlock = cfg.Blocks.Single(b => b.Label == "then");
        var elseBlock = cfg.Blocks.Single(b => b.Label == "else");

        Assert.False(HasRemoveNone(result.FactsAtEntry(thenBlock), "x"));
        Assert.True(HasRemoveNone(result.FactsAtEntry(elseBlock), "x"));
    }

    [Fact]
    public void Isinstance_NarrowsThenBranchToType()
    {
        var cfg = CreateDiamondCfg(IsInstance("x", "Cat"),
            thenStatements: new Statement[] { Pass() },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        var thenBlock = cfg.Blocks.Single(b => b.Label == "then");
        var elseBlock = cfg.Blocks.Single(b => b.Label == "else");

        Assert.True(HasIsType(result.FactsAtEntry(thenBlock), "x", "Cat"));
        Assert.False(HasIsType(result.FactsAtEntry(elseBlock), "x", "Cat"));
    }

    [Fact]
    public void Merge_NarrowedInOneBranchOnly_NotNarrowedAfterMerge()
    {
        // Non-narrowing condition; only the then-branch asserts x is not None.
        var cfg = CreateDiamondCfg(Bool(true),
            thenStatements: new Statement[] { Assert_(IsNotNone("x")) },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        var mergeBlock = cfg.Blocks.Single(b => b.Label == "merge");
        Assert.False(HasRemoveNone(result.FactsAtEntry(mergeBlock), "x"));
    }

    [Fact]
    public void Merge_NarrowedInBothBranches_KeptAfterMerge()
    {
        var cfg = CreateDiamondCfg(Bool(true),
            thenStatements: new Statement[] { Assert_(IsNotNone("x")) },
            elseStatements: new Statement[] { Assert_(IsNotNone("x")) });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        var mergeBlock = cfg.Blocks.Single(b => b.Label == "merge");
        Assert.True(HasRemoveNone(result.FactsAtEntry(mergeBlock), "x"));
    }

    [Fact]
    public void Assignment_KillsNarrowingOnTheKey()
    {
        var assert = Assert_(IsNotNone("x"));
        var kill = Assign(Id("x"));
        var after = Assign(Id("y"));
        var cfg = CreateLinearCfg(assert, kill, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        // Narrowed just before the reassignment...
        Assert.True(HasRemoveNone(result.FactsBefore(kill), "x"));
        // ...and un-narrowed after it.
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x"));
    }

    [Fact]
    public void Assignment_ToPrefix_KillsNarrowingOnNestedKey()
    {
        // Narrow both x and x.y, then reassign x — both facts must be killed.
        var assertX = Assert_(IsNotNone("x"));
        var assertXy = Assert_(MemberIsNotNone("x", "y"));
        var killX = Assign(Id("x"));
        var after = Assign(Id("z"));
        var cfg = CreateLinearCfg(assertX, assertXy, killX, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        Assert.True(HasRemoveNone(result.FactsBefore(killX), "x"));
        Assert.True(HasRemoveNone(result.FactsBefore(killX), "x.y"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x.y"));
    }

    [Fact]
    public void Assignment_ToMember_DoesNotKillNarrowingOnObject()
    {
        // Narrow x and x.y, then reassign x.y — only x.y is killed, x survives.
        var assertX = Assert_(IsNotNone("x"));
        var assertXy = Assert_(MemberIsNotNone("x", "y"));
        var killXy = Assign(Member("x", "y"));
        var after = Assign(Id("z"));
        var cfg = CreateLinearCfg(assertX, assertXy, killXy, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        Assert.True(HasRemoveNone(result.FactsBefore(after), "x"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x.y"));
    }

    [Fact]
    public void Loop_ReassignmentInBody_KillsNarrowingAtHeader()
    {
        var kill = Assign(Id("x"));
        var (cfg, header) = BuildPreheaderLoop(
            preheaderStatements: new Statement[] { Assert_(IsNotNone("x")) },
            bodyStatements: new Statement[] { kill });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        // The narrowing established before the loop does not survive the reassignment on the back edge.
        Assert.False(HasRemoveNone(result.FactsAtEntry(header), "x"));
    }

    [Fact]
    public void Loop_NoReassignment_NarrowingPersistsThroughHeader()
    {
        var (cfg, header) = BuildPreheaderLoop(
            preheaderStatements: new Statement[] { Assert_(IsNotNone("x")) },
            bodyStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg);

        Assert.True(HasRemoveNone(result.FactsAtEntry(header), "x"));
    }

    [Fact]
    public void NestedFunctionDef_FactsDoNotLeakAcrossCfgs()
    {
        // outer: assert x is not None; def inner(): x   (inner body is a separate CFG)
        var innerUse = new ExpressionStatement { Expression = Id("x") };
        var inner = new FunctionDef
        {
            Name = "inner",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray.Create<Statement>(innerUse)
        };
        var outer = new FunctionDef
        {
            Name = "outer",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray.Create<Statement>(Assert_(IsNotNone("x")), inner)
        };

        var builder = new ControlFlowGraphBuilder();
        var outerResult = NarrowingFlowAnalysis.Analyze(builder.Build(outer));
        var innerResult = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(inner));

        // The nested def's body is not part of the outer CFG, so the outer analysis never tracks it...
        Assert.Empty(outerResult.FactsBefore(innerUse));
        // ...and analysing the inner CFG on its own sees no narrowing from the enclosing scope.
        Assert.False(HasRemoveNone(innerResult.FactsBefore(innerUse), "x"));
    }

    /// <summary>
    /// Builds a CFG shaped entry → preheader → header → {body → header, exit}, so a narrowing can be
    /// established in the preheader and challenged by the loop body across the back edge. Returns the
    /// header block for entry-fact assertions.
    /// </summary>
    private static (ControlFlowGraph Cfg, BasicBlock Header) BuildPreheaderLoop(
        Statement[] preheaderStatements,
        Statement[] bodyStatements)
    {
        var entry = new BasicBlock("entry");
        var preheader = new BasicBlock("preheader");
        var header = new BasicBlock("loop_header");
        var body = new BasicBlock("loop_body");
        var exit = new BasicBlock("exit");

        foreach (var stmt in preheaderStatements)
            preheader.AddStatement(stmt);
        foreach (var stmt in bodyStatements)
            body.AddStatement(stmt);

        ConnectBlocks(entry, preheader);
        ConnectBlocks(preheader, header);
        ConnectBlocks(header, body);
        ConnectBlocks(header, exit);
        ConnectBlocks(body, header);

        entry.Terminator = new BranchTerminator(preheader);
        preheader.Terminator = new BranchTerminator(header);
        header.Terminator = new ConditionalBranchTerminator(Bool(true), body, exit);
        body.Terminator = new BranchTerminator(header);

        var cfg = new ControlFlowGraph(entry, exit,
            new List<BasicBlock> { entry, preheader, header, body, exit });
        return (cfg, header);
    }
}
