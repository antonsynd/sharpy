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

    private static BinaryOp NotEqNone(string name) =>
        new BinaryOp { Operator = BinaryOperator.NotEqual, Left = Id(name), Right = None() };

    private static BinaryOp EqNone(string name) =>
        new BinaryOp { Operator = BinaryOperator.Equal, Left = Id(name), Right = None() };

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

    private static FunctionCall QualifiedIsInstance(string name, string typeName) =>
        new FunctionCall
        {
            Function = Member("builtins", "isinstance"),
            Arguments = ImmutableArray.Create<Expression>(Id(name), Id(typeName))
        };

    private static AssertStatement Assert_(Expression test) => new AssertStatement { Test = test };

    /// <summary>A store of a literal — definitely not None, so a RemoveNone fact on the key survives (R-T, plan-757fbb Decision 4).</summary>
    private static Assignment Assign(Expression target) =>
        new Assignment { Target = target, Value = Int(0) };

    /// <summary>A store of a call result — possibly None (`g() -> int?`), so every fact on the key dies.</summary>
    private static Assignment AssignCall(Expression target) =>
        new Assignment
        {
            Target = target,
            Value = new FunctionCall { Function = Id("g"), Arguments = ImmutableArray<Expression>.Empty }
        };

    #endregion

    #region builtins-module predicate (#1381)

    /// <summary>
    /// The answer for every CFG in this file that has no qualified spelling in it: these graphs are
    /// built from hand-written AST with no symbol table, so no receiver denotes the <c>builtins</c>
    /// module. Named rather than inlined at each call site so that a test which DOES want the
    /// qualified arm has to opt in visibly — see
    /// <see cref="QualifiedIsinstance_NarrowsOnlyWhenTheReceiverDenotesBuiltins"/>.
    /// </summary>
    private static readonly Func<Expression, bool> NoBuiltinsModule = _ => false;

    /// <summary>
    /// The opt-in counterpart, standing in for the compiler's symbol-table lookup: here the
    /// identifier <c>builtins</c> denotes the module.
    /// </summary>
    private static readonly Func<Expression, bool> BuiltinsIsTheModule =
        receiver => receiver is Identifier { Name: "builtins" };

    #endregion

    [Fact]
    public void NoNarrowingConditions_ProducesEmptyFacts()
    {
        var stmt = Assign(Id("y"));
        var cfg = CreateLinearCfg(stmt);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.Empty(result.FactsBefore(stmt));
    }

    [Fact]
    public void QualifiedIsinstance_NarrowsOnlyWhenTheReceiverDenotesBuiltins()
    {
        // #1381: `builtins.isinstance(x, Cat)` must narrow exactly as the bare spelling does — the
        // qualified escape from a shadowed `isinstance` is still the builtin (the #1322 agreement
        // contract). Leaving it unrecognised would give the escape a type test that compiles
        // without narrowing.
        //
        // Both arms run the SAME graph through the SAME engine and differ only in the predicate, so
        // this is also the positive control for the `denotesBuiltinsModule` parameter itself: the
        // other 21 call sites in this file all pass NoBuiltinsModule, and every one of them would
        // still pass against an engine that ignored the argument entirely. Without the first
        // assertion below, nothing here distinguishes "consulted and answered false" from
        // "never consulted."
        var assertQualified = Assert_(QualifiedIsInstance("x", "Cat"));
        var after = Assign(Id("z"));

        var recognised = NarrowingFlowAnalysis.Analyze(
            CreateLinearCfg(assertQualified, after), BuiltinsIsTheModule);
        Assert.True(HasIsType(recognised.FactsBefore(after), "x", "Cat"));

        // Same source shape, but the receiver does not denote the module — a local named `builtins`,
        // or a user's own builtins.spy. It must NOT narrow.
        var notRecognised = NarrowingFlowAnalysis.Analyze(
            CreateLinearCfg(assertQualified, after), NoBuiltinsModule);
        Assert.False(HasIsType(notRecognised.FactsBefore(after), "x", "Cat"));
    }

    [Fact]
    public void StraightLine_AssertGeneratesFactForFollowingStatements()
    {
        var assert = Assert_(IsNotNone("x"));
        var following = Assign(Id("y"));
        var cfg = CreateLinearCfg(assert, following);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

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

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

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

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

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

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

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

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        var mergeBlock = cfg.Blocks.Single(b => b.Label == "merge");
        Assert.False(HasRemoveNone(result.FactsAtEntry(mergeBlock), "x"));
    }

    [Fact]
    public void Merge_NarrowedInBothBranches_KeptAfterMerge()
    {
        var cfg = CreateDiamondCfg(Bool(true),
            thenStatements: new Statement[] { Assert_(IsNotNone("x")) },
            elseStatements: new Statement[] { Assert_(IsNotNone("x")) });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        var mergeBlock = cfg.Blocks.Single(b => b.Label == "merge");
        Assert.True(HasRemoveNone(result.FactsAtEntry(mergeBlock), "x"));
    }

    [Fact]
    public void Assignment_OfDefinitelyNonNoneValue_KeepsNarrowingOnTheKey()
    {
        // R-T (plan-757fbb Decision 4): a store whose value cannot be None keeps the RemoveNone
        // fact — the slot still holds a non-None value, so `d = 5` under `if d is not None:`
        // re-wraps and the narrowing survives. Before that ruling every store killed the fact.
        var assert = Assert_(IsNotNone("x"));
        var store = Assign(Id("x"));
        var after = Assign(Id("y"));
        var cfg = CreateLinearCfg(assert, store, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsBefore(store), "x"));
        Assert.True(HasRemoveNone(result.FactsBefore(after), "x"));
    }

    [Fact]
    public void Assignment_OfCall_KillsNarrowingOnTheKey()
    {
        // The kill control: a call may return None, so the fact dies (the checker would otherwise
        // `.Unwrap()` a None at the next read).
        var assert = Assert_(IsNotNone("x"));
        var kill = AssignCall(Id("x"));
        var after = Assign(Id("y"));
        var cfg = CreateLinearCfg(assert, kill, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsBefore(kill), "x"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x"));
    }

    [Fact]
    public void Assignment_ToPrefix_KillsNarrowingOnNestedKey_EvenWhenTheValueIsNonNone()
    {
        // Narrow both x and x.y, then reassign x to a definitely-non-None value: x itself stays
        // narrowed (it is still not None) but x.y MUST die — the new object's members are unknown.
        var assertX = Assert_(IsNotNone("x"));
        var assertXy = Assert_(MemberIsNotNone("x", "y"));
        var storeX = Assign(Id("x"));
        var after = Assign(Id("z"));
        var cfg = CreateLinearCfg(assertX, assertXy, storeX, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsBefore(storeX), "x"));
        Assert.True(HasRemoveNone(result.FactsBefore(storeX), "x.y"));
        Assert.True(HasRemoveNone(result.FactsBefore(after), "x"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x.y"));
    }

    [Fact]
    public void Assignment_OfCallToPrefix_KillsNarrowingOnKeyAndNestedKey()
    {
        var assertX = Assert_(IsNotNone("x"));
        var assertXy = Assert_(MemberIsNotNone("x", "y"));
        var killX = AssignCall(Id("x"));
        var after = Assign(Id("z"));
        var cfg = CreateLinearCfg(assertX, assertXy, killX, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.False(HasRemoveNone(result.FactsBefore(after), "x"));
        Assert.False(HasRemoveNone(result.FactsBefore(after), "x.y"));
    }

    [Fact]
    public void Assignment_ToMember_DoesNotKillNarrowingOnObject()
    {
        // Narrow x and x.y, then store into x.y: x survives (the object is untouched); x.y
        // survives too when the value is definitely non-None (payload store, R-T) ...
        var assertX = Assert_(IsNotNone("x"));
        var assertXy = Assert_(MemberIsNotNone("x", "y"));
        var storeXy = Assign(Member("x", "y"));
        var after = Assign(Id("z"));
        var cfg = CreateLinearCfg(assertX, assertXy, storeXy, after);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsBefore(after), "x"));
        Assert.True(HasRemoveNone(result.FactsBefore(after), "x.y"));

        // ... and dies when the value is a call (possibly None) — x still survives.
        var killXy = AssignCall(Member("x", "y"));
        var after2 = Assign(Id("z"));
        var cfg2 = CreateLinearCfg(Assert_(IsNotNone("x")), Assert_(MemberIsNotNone("x", "y")), killXy, after2);

        var result2 = NarrowingFlowAnalysis.Analyze(cfg2, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result2.FactsBefore(after2), "x"));
        Assert.False(HasRemoveNone(result2.FactsBefore(after2), "x.y"));
    }

    [Fact]
    public void Loop_PayloadReassignmentInBody_KeepsNarrowingAtHeader()
    {
        // The plan-757fbb block-kind rule: `d = 5` in a for/while body under `if d is not None:`
        // keeps d narrowed at the loop head, as it does in a try/with body.
        var store = Assign(Id("x"));
        var (cfg, header) = BuildPreheaderLoop(
            preheaderStatements: new Statement[] { Assert_(IsNotNone("x")) },
            bodyStatements: new Statement[] { store });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsAtEntry(header), "x"));
    }

    [Fact]
    public void Loop_CallReassignmentInBody_KillsNarrowingAtHeader()
    {
        var kill = AssignCall(Id("x"));
        var (cfg, header) = BuildPreheaderLoop(
            preheaderStatements: new Statement[] { Assert_(IsNotNone("x")) },
            bodyStatements: new Statement[] { kill });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        // The narrowing established before the loop does not survive a possibly-None store on the back edge.
        Assert.False(HasRemoveNone(result.FactsAtEntry(header), "x"));
    }

    [Fact]
    public void Loop_NoReassignment_NarrowingPersistsThroughHeader()
    {
        var (cfg, header) = BuildPreheaderLoop(
            preheaderStatements: new Statement[] { Assert_(IsNotNone("x")) },
            bodyStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsAtEntry(header), "x"));
    }

    [Fact]
    public void FactsBeforeBranch_ReturnsFactsAtTheConditionOfANestedBranch()
    {
        // Outer if narrows x; the then-branch contains an inner if whose condition must see it.
        var innerIf = new IfStatement
        {
            Test = IsNotNone("y"),
            ThenBody = ImmutableArray.Create<Statement>(Pass())
        };
        var outer = new FunctionDef
        {
            Name = "f",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray.Create<Statement>(new IfStatement
            {
                Test = IsNotNone("x"),
                ThenBody = ImmutableArray.Create<Statement>(Assert_(IsNotNone("x")), innerIf)
            })
        };

        var result = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(outer), NoBuiltinsModule);

        // The inner branch's condition sees the narrowing established by the enclosing then-branch.
        Assert.True(HasRemoveNone(result.FactsBeforeBranch(innerIf.Test), "x"));
    }

    [Fact]
    public void IsTracked_TrueForBlockStatements_FalseForForeign()
    {
        var pass = Pass();
        // An assert makes the fact universe non-empty, so the analysis tracks every block statement.
        var cfg = CreateLinearCfg(Assert_(IsNotNone("x")), pass);

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(result.IsTracked(pass));
        Assert.False(result.IsTracked(Pass())); // a different node the analysis never saw
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
        var outerResult = NarrowingFlowAnalysis.Analyze(builder.Build(outer), NoBuiltinsModule);
        var innerResult = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(inner), NoBuiltinsModule);

        // The nested def's body is not part of the outer CFG, so the outer analysis never tracks it...
        Assert.Empty(outerResult.FactsBefore(innerUse));
        // ...and analysing the inner CFG on its own sees no narrowing from the enclosing scope.
        Assert.False(HasRemoveNone(innerResult.FactsBefore(innerUse), "x"));
    }

    [Fact]
    public void NotEqualsNone_NarrowsThenBranchOnly()
    {
        var cfg = CreateDiamondCfg(NotEqNone("x"),
            thenStatements: new Statement[] { Pass() },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsAtEntry(cfg.Blocks.Single(b => b.Label == "then")), "x"));
        Assert.False(HasRemoveNone(result.FactsAtEntry(cfg.Blocks.Single(b => b.Label == "else")), "x"));
    }

    [Fact]
    public void EqualsNone_NarrowsElseBranchOnly()
    {
        var cfg = CreateDiamondCfg(EqNone("x"),
            thenStatements: new Statement[] { Pass() },
            elseStatements: new Statement[] { Pass() });

        var result = NarrowingFlowAnalysis.Analyze(cfg, NoBuiltinsModule);

        Assert.False(HasRemoveNone(result.FactsAtEntry(cfg.Blocks.Single(b => b.Label == "then")), "x"));
        Assert.True(HasRemoveNone(result.FactsAtEntry(cfg.Blocks.Single(b => b.Label == "else")), "x"));
    }

    [Fact]
    public void ForLoopTarget_KillsNarrowingInBody()
    {
        // x narrowed before the loop, then rebound as the loop target — narrowing must not survive.
        var useInBody = Assign(Id("z"));
        var func = FunctionWithBody(
            Assert_(IsNotNone("x")),
            new ForStatement
            {
                Target = Id("x"),
                Iterator = Id("items"),
                Body = ImmutableArray.Create<Statement>(useInBody)
            });

        var result = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(func), NoBuiltinsModule);

        Assert.False(HasRemoveNone(result.FactsBefore(useInBody), "x"));
    }

    [Fact]
    public void ForLoopTarget_DifferentVariable_PreservesNarrowing()
    {
        // The loop rebinds y, not x — x's narrowing survives into the body.
        var useInBody = Assign(Id("z"));
        var func = FunctionWithBody(
            Assert_(IsNotNone("x")),
            new ForStatement
            {
                Target = Id("y"),
                Iterator = Id("items"),
                Body = ImmutableArray.Create<Statement>(useInBody)
            });

        var result = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(func), NoBuiltinsModule);

        Assert.True(HasRemoveNone(result.FactsBefore(useInBody), "x"));
    }

    [Fact]
    public void WithAsBinding_KillsNarrowingInBody()
    {
        var useInBody = Assign(Id("z"));
        var func = FunctionWithBody(
            Assert_(IsNotNone("x")),
            new WithStatement
            {
                Items = ImmutableArray.Create(new WithItem { ContextExpression = Id("cm"), Target = Id("x") }),
                Body = ImmutableArray.Create<Statement>(useInBody)
            });

        var result = NarrowingFlowAnalysis.Analyze(new ControlFlowGraphBuilder().Build(func), NoBuiltinsModule);

        Assert.False(HasRemoveNone(result.FactsBefore(useInBody), "x"));
    }

    private static FunctionDef FunctionWithBody(params Statement[] body) => new FunctionDef
    {
        Name = "f",
        Parameters = ImmutableArray<Parameter>.Empty,
        Body = ImmutableArray.Create(body)
    };

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
