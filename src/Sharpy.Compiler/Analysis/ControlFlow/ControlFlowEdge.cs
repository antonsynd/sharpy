namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Describes the kind of edge in the CFG.
/// </summary>
internal enum EdgeKind
{
    /// <summary>Unconditional transfer of control.</summary>
    Unconditional,

    /// <summary>Taken when condition is true.</summary>
    ConditionalTrue,

    /// <summary>Taken when condition is false.</summary>
    ConditionalFalse,

    /// <summary>A specific case in a switch/match.</summary>
    SwitchCase,

    /// <summary>Default case in a switch/match.</summary>
    SwitchDefault,

    /// <summary>Edge from try block to exception handler.</summary>
    Exception,

    /// <summary>Edge from try/handler to finally block.</summary>
    Finally,

    /// <summary>Back edge in a loop.</summary>
    LoopBack,

    /// <summary>Exit from a loop (break or natural exit).</summary>
    LoopExit
}

/// <summary>
/// Represents an edge in the control flow graph.
/// </summary>
internal record ControlFlowEdge(
    BasicBlock From,
    BasicBlock To,
    EdgeKind Kind
)
{
    /// <summary>
    /// For conditional edges, the condition expression.
    /// </summary>
    public Parser.Ast.Expression? Condition { get; init; }

    public override string ToString() => $"{From} --{Kind}--> {To}";
}
