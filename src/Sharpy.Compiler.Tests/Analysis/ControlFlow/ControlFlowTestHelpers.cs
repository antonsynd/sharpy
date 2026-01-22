using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Helper methods for creating CFG test fixtures.
/// </summary>
public static class ControlFlowTestHelpers
{
    /// <summary>
    /// Creates a simple linear CFG: entry -> body -> exit
    /// </summary>
    public static ControlFlowGraph CreateLinearCfg(params Statement[] statements)
    {
        var entry = new BasicBlock("entry");
        var exit = new BasicBlock("exit");
        var body = new BasicBlock("body");

        foreach (var stmt in statements)
            body.AddStatement(stmt);

        ConnectBlocks(entry, body);
        ConnectBlocks(body, exit);
        entry.Terminator = new BranchTerminator(body);
        body.Terminator = new BranchTerminator(exit);

        return new ControlFlowGraph(entry, exit, new List<BasicBlock> { entry, body, exit });
    }

    /// <summary>
    /// Creates a diamond-shaped CFG for if/else:
    /// entry -> condition -> {then, else} -> merge -> exit
    /// </summary>
    public static ControlFlowGraph CreateDiamondCfg(
        Expression condition,
        Statement[] thenStatements,
        Statement[] elseStatements)
    {
        var entry = new BasicBlock("entry");
        var exit = new BasicBlock("exit");
        var condBlock = new BasicBlock("condition");
        var thenBlock = new BasicBlock("then");
        var elseBlock = new BasicBlock("else");
        var mergeBlock = new BasicBlock("merge");

        foreach (var stmt in thenStatements)
            thenBlock.AddStatement(stmt);
        foreach (var stmt in elseStatements)
            elseBlock.AddStatement(stmt);

        ConnectBlocks(entry, condBlock);
        ConnectBlocks(condBlock, thenBlock);
        ConnectBlocks(condBlock, elseBlock);
        ConnectBlocks(thenBlock, mergeBlock);
        ConnectBlocks(elseBlock, mergeBlock);
        ConnectBlocks(mergeBlock, exit);

        entry.Terminator = new BranchTerminator(condBlock);
        condBlock.Terminator = new ConditionalBranchTerminator(condition, thenBlock, elseBlock);
        thenBlock.Terminator = new BranchTerminator(mergeBlock);
        elseBlock.Terminator = new BranchTerminator(mergeBlock);
        mergeBlock.Terminator = new BranchTerminator(exit);

        return new ControlFlowGraph(entry, exit,
            new List<BasicBlock> { entry, condBlock, thenBlock, elseBlock, mergeBlock, exit });
    }

    /// <summary>
    /// Creates a simple loop CFG:
    /// entry -> header -> {body -> header, exit}
    /// </summary>
    public static ControlFlowGraph CreateLoopCfg(
        Expression condition,
        Statement[] bodyStatements)
    {
        var entry = new BasicBlock("entry");
        var exit = new BasicBlock("exit");
        var header = new BasicBlock("loop_header");
        var body = new BasicBlock("loop_body");

        foreach (var stmt in bodyStatements)
            body.AddStatement(stmt);

        ConnectBlocks(entry, header);
        ConnectBlocks(header, body);
        ConnectBlocks(header, exit);
        ConnectBlocks(body, header);

        entry.Terminator = new BranchTerminator(header);
        header.Terminator = new ConditionalBranchTerminator(condition, body, exit);
        body.Terminator = new BranchTerminator(header);

        return new ControlFlowGraph(entry, exit,
            new List<BasicBlock> { entry, header, body, exit });
    }

    /// <summary>
    /// Connects two blocks (adds predecessor/successor relationship).
    /// </summary>
    public static void ConnectBlocks(BasicBlock from, BasicBlock to)
    {
        from.AddSuccessor(to);
        to.AddPredecessor(from);
    }

    /// <summary>
    /// Creates a simple pass statement for testing.
    /// </summary>
    public static PassStatement Pass() => new PassStatement();

    /// <summary>
    /// Creates a simple boolean literal for testing.
    /// </summary>
    public static BooleanLiteral Bool(bool value) => new BooleanLiteral { Value = value };

    /// <summary>
    /// Creates a simple identifier for testing.
    /// </summary>
    public static Identifier Id(string name) => new Identifier { Name = name };

    /// <summary>
    /// Creates a simple integer literal for testing.
    /// </summary>
    public static IntegerLiteral Int(long value) => new IntegerLiteral { Value = value.ToString() };

    /// <summary>
    /// Creates a return statement for testing.
    /// </summary>
    public static ReturnStatement Return(Expression? value = null) =>
        new ReturnStatement { Value = value };
}
