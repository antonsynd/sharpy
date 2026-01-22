# Control Flow Graph (CFG) Infrastructure Implementation Task List

**Recommendation:** #9 from architecture review addendum
**Target Version:** v0.2.x preparation (architectural foundation)
**Priority:** HIGH
**Effort:** Large (estimated 8-12 development sessions)

---

## Overview

This task list implements the Control Flow Graph infrastructure needed for:
- **Async/await:** State machine generation requires splitting code at await boundaries
- **Pattern matching:** Exhaustiveness checking requires analyzing all possible execution paths
- **Dead code detection:** Identifying unreachable code
- **Definite assignment:** Ensuring variables are assigned before use (future)
- **Return path analysis:** More accurate "must return a value" checking

The implementation is designed to be **additive** - all existing tests should continue to pass throughout. The CFG infrastructure is built alongside (not replacing) existing code until integration is complete.

---

## Key Design Decisions

### AST vs Bound Nodes

**Decision:** Build CFG from **raw AST nodes** (`Statement`, `Expression`), not from bound nodes.

**Rationale:**
- Sharpy's AST is already immutable (C# records with `init` properties)
- No separate "bound tree" layer currently exists in the compiler
- Semantic information is stored externally via `SemanticInfo` class (reference-equality keyed)
- CFG can access semantic info via the `SemanticContext` when needed
- Aligns with current architecture where analysis happens on AST with SemanticInfo annotations

**Future Consideration:** If a bound tree layer is added later, CFG can be refactored to work on bound nodes instead.

### Statement Types Affecting Control Flow

The following AST statement types directly affect control flow:
- `ReturnStatement` - Function exit
- `RaiseStatement` - Exception throw
- `BreakStatement` - Loop exit
- `BreakWithFlagStatement` - Loop exit with flag (internal, for loop-else)
- `ContinueStatement` - Loop iteration skip
- `IfStatement` with `ElifClause` - Conditional branching
- `WhileStatement` - Pre-test loop with optional else clause
- `ForStatement` - Iterator loop with optional else clause
- `TryStatement` with `ExceptHandler` - Exception handling

**Python-style loop else:** Both `while` and `for` support an else clause that executes if the loop completes without `break`. This requires special CFG modeling.

---

## Prerequisites

Before starting, ensure:
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`
- [ ] You understand the existing AST node hierarchy (`Statement.cs`, `Expression.cs`)
- [ ] You understand that AST nodes are **immutable records** with `init` properties
- [ ] You've reviewed `ControlFlowValidatorV2.cs` (current implementation to eventually replace)
- [ ] You've read the architecture review addendum section on CFG (Recommendation #9)

---

## Phase 1: Core CFG Data Structures (Foundation)

**Goal:** Create the fundamental data structures for representing control flow. These are pure data classes with no dependencies on existing compiler code.

### Task 1.1: Create BasicBlock Data Structure

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/BasicBlock.cs`

Create the BasicBlock class that represents a sequence of statements with no internal branching:

- [ ] Create directory: `src/Sharpy.Compiler/Analysis/ControlFlow/`
- [ ] Create `BasicBlock.cs` with the following:

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// A basic block is a sequence of statements with:
/// - Single entry point (only first statement can be branched to)
/// - Single exit point (only last statement can branch out)
/// - No internal control flow (no branches within the block)
/// </summary>
/// <remarks>
/// BasicBlock is a mutable class during CFG construction, then becomes
/// effectively immutable once the CFG is built. It is NOT a record because
/// we need reference identity (two blocks with same content are different blocks)
/// and mutable predecessor/successor lists during construction.
/// </remarks>
public sealed class BasicBlock
{
    /// <summary>
    /// Unique identifier for this block within a CFG.
    /// Assigned by the ControlFlowGraph that owns this block.
    /// </summary>
    public int Id { get; internal set; }

    /// <summary>
    /// Human-readable label for debugging (e.g., "entry", "exit", "if_then", "loop_body").
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The statements in this block, in execution order.
    /// Empty for synthetic blocks (entry/exit).
    /// </summary>
    public IReadOnlyList<Statement> Statements => _statements;
    private readonly List<Statement> _statements;

    /// <summary>
    /// Predecessor blocks - blocks that can transfer control TO this block.
    /// </summary>
    public IReadOnlyList<BasicBlock> Predecessors => _predecessors;
    private readonly List<BasicBlock> _predecessors = new();

    /// <summary>
    /// Successor blocks - blocks that control can transfer TO from this block.
    /// </summary>
    public IReadOnlyList<BasicBlock> Successors => _successors;
    private readonly List<BasicBlock> _successors = new();

    /// <summary>
    /// The terminator instruction that ends this block.
    /// Null only for the exit block.
    /// </summary>
    public BlockTerminator? Terminator { get; internal set; }

    /// <summary>
    /// For async analysis: true if any statement in this block contains an await expression.
    /// Set during CFG construction by scanning for AwaitExpression nodes.
    /// </summary>
    public bool ContainsAwait { get; internal set; }

    /// <summary>
    /// The source span of the first statement in this block (for diagnostics).
    /// </summary>
    public Text.TextSpan? Span => _statements.Count > 0 ? _statements[0].Span : null;

    public BasicBlock(string label = "")
    {
        Label = label;
        _statements = new List<Statement>();
    }

    /// <summary>
    /// Add a statement to this block. Only valid during CFG construction.
    /// </summary>
    internal void AddStatement(Statement stmt)
    {
        _statements.Add(stmt);
    }

    internal void AddPredecessor(BasicBlock block)
    {
        if (!_predecessors.Contains(block))
            _predecessors.Add(block);
    }

    internal void AddSuccessor(BasicBlock block)
    {
        if (!_successors.Contains(block))
            _successors.Add(block);
    }

    public override string ToString() =>
        string.IsNullOrEmpty(Label) ? $"BB{Id}" : $"BB{Id}:{Label}";
}
```

**Design Notes:**
- `BasicBlock` is a **sealed class**, not a record, because:
  1. We need reference identity (two blocks with same statements are different blocks)
  2. We need mutable lists during construction (predecessors, successors, statements)
  3. Record copy semantics would be incorrect for graph nodes
- IDs are assigned by the owning `ControlFlowGraph`, not via static counter (avoids thread-safety issues)
- Statements list is mutable during construction, exposed as `IReadOnlyList<T>` after

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests (should still pass): `dotnet test src/Sharpy.Compiler.Tests`

### Task 1.2: Create Block Terminator Hierarchy

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/BlockTerminator.cs`

Create the terminator types that describe how a block exits:

- [ ] Create `BlockTerminator.cs`:

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Describes how control flow leaves a basic block.
/// Every basic block (except exit) has exactly one terminator.
/// </summary>
/// <remarks>
/// Terminators are immutable records - they represent the computed control flow
/// and don't change after CFG construction.
/// </remarks>
public abstract record BlockTerminator
{
    /// <summary>
    /// The statement that caused this terminator (for diagnostics).
    /// </summary>
    public Statement? SourceStatement { get; init; }
}

/// <summary>
/// Unconditional branch to a single target block.
/// Generated by: fall-through, unconditional goto
/// </summary>
public sealed record BranchTerminator(BasicBlock Target) : BlockTerminator;

/// <summary>
/// Conditional branch based on a boolean expression.
/// Generated by: if statements, while conditions, for conditions
/// </summary>
public sealed record ConditionalBranchTerminator(
    Expression Condition,
    BasicBlock TrueTarget,
    BasicBlock FalseTarget
) : BlockTerminator;

/// <summary>
/// Multi-way branch based on a value.
/// Generated by: match/switch statements (v0.2.x)
/// </summary>
public sealed record SwitchTerminator(
    Expression Value,
    ImmutableArray<SwitchCase> Cases,
    BasicBlock? DefaultTarget
) : BlockTerminator;

/// <summary>
/// A single case in a switch terminator.
/// </summary>
public sealed record SwitchCase(
    Pattern? Pattern,  // null for default case
    Expression? Guard,
    BasicBlock Target
);

/// <summary>
/// Return from the function.
/// Generated by: return statements
/// </summary>
public sealed record ReturnTerminator(Expression? Value) : BlockTerminator;

/// <summary>
/// Throw an exception.
/// Generated by: raise statements (re-raise has null Exception)
/// </summary>
public sealed record ThrowTerminator(Expression? Exception) : BlockTerminator;

/// <summary>
/// Break out of a loop to a specific target.
/// Generated by: break statements and BreakWithFlagStatement (internal)
/// </summary>
/// <remarks>
/// For BreakWithFlagStatement (used for loop-else), the flag assignment
/// is added as a statement before this terminator is reached.
/// </remarks>
public sealed record BreakTerminator(BasicBlock Target) : BlockTerminator;

/// <summary>
/// Continue to the next iteration of a loop.
/// Generated by: continue statements
/// </summary>
public sealed record ContinueTerminator(BasicBlock Target) : BlockTerminator;

/// <summary>
/// Re-raise the current exception (bare 'raise' with no expression).
/// Can only appear inside an except handler.
/// </summary>
public sealed record RethrowTerminator() : BlockTerminator;

/// <summary>
/// Marker terminator for blocks that don't naturally terminate (unreachable).
/// This should not appear in a well-formed CFG.
/// </summary>
public sealed record UnreachableTerminator() : BlockTerminator;
```

**Design Notes:**
- `ThrowTerminator` now accepts `Expression?` to handle bare `raise` (re-raise current exception)
- Added `RethrowTerminator` as a distinct type for bare `raise` inside except handlers
- `BreakTerminator` comment clarifies handling of `BreakWithFlagStatement`

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 1.3: Create ControlFlowGraph Container

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraph.cs`

Create the main CFG container class:

- [ ] Create `ControlFlowGraph.cs`:

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Represents the control flow graph for a function or method body.
/// </summary>
/// <remarks>
/// Once constructed, a ControlFlowGraph is effectively immutable.
/// The blocks list and block connections don't change after construction.
/// </remarks>
public sealed class ControlFlowGraph
{
    private int _nextBlockId;

    /// <summary>
    /// The synthetic entry block - has no statements, just connects to the first real block.
    /// </summary>
    public BasicBlock Entry { get; }

    /// <summary>
    /// The synthetic exit block - all return paths lead here.
    /// </summary>
    public BasicBlock Exit { get; }

    /// <summary>
    /// All blocks in the CFG, including entry and exit.
    /// Blocks are in no particular order.
    /// </summary>
    public IReadOnlyList<BasicBlock> Blocks => _blocks;
    private readonly List<BasicBlock> _blocks;

    /// <summary>
    /// The function or method this CFG was built from (for diagnostics).
    /// Null for module-level CFGs.
    /// </summary>
    public FunctionDef? SourceFunction { get; }

    /// <summary>
    /// The source file path (for diagnostics).
    /// </summary>
    public string? SourceFile { get; init; }

    internal ControlFlowGraph(BasicBlock entry, BasicBlock exit, List<BasicBlock> blocks, FunctionDef? sourceFunction = null)
    {
        Entry = entry;
        Exit = exit;
        _blocks = blocks;
        SourceFunction = sourceFunction;

        // Assign sequential IDs to all blocks
        foreach (var block in blocks)
        {
            block.Id = _nextBlockId++;
        }
    }

    /// <summary>
    /// Get all blocks in reverse post-order (useful for data flow analysis).
    /// Entry block comes first, exit block typically last.
    /// </summary>
    public IReadOnlyList<BasicBlock> GetReversePostOrder()
    {
        var visited = new HashSet<BasicBlock>();
        var postOrder = new List<BasicBlock>();

        void Visit(BasicBlock block)
        {
            if (!visited.Add(block)) return;
            foreach (var succ in block.Successors)
                Visit(succ);
            postOrder.Add(block);
        }

        Visit(Entry);
        postOrder.Reverse();
        return postOrder;
    }

    /// <summary>
    /// Find all blocks that are unreachable from the entry block.
    /// </summary>
    public ImmutableHashSet<BasicBlock> FindUnreachableBlocks()
    {
        var reachable = new HashSet<BasicBlock>();
        var worklist = new Queue<BasicBlock>();
        worklist.Enqueue(Entry);

        while (worklist.Count > 0)
        {
            var block = worklist.Dequeue();
            if (!reachable.Add(block)) continue;
            foreach (var succ in block.Successors)
                worklist.Enqueue(succ);
        }

        return _blocks.Where(b => !reachable.Contains(b)).ToImmutableHashSet();
    }

    /// <summary>
    /// Check if all paths through the CFG reach the exit block.
    /// Returns the blocks that don't reach exit (missing return).
    /// </summary>
    /// <remarks>
    /// Uses backward reachability from Exit. Blocks that can't reach Exit
    /// either throw exceptions, loop infinitely, or have missing returns.
    /// </remarks>
    public ImmutableHashSet<BasicBlock> FindBlocksNotReachingExit()
    {
        var reachesExit = new HashSet<BasicBlock> { Exit };
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var block in _blocks)
            {
                if (reachesExit.Contains(block)) continue;
                if (block.Successors.Any(reachesExit.Contains))
                {
                    reachesExit.Add(block);
                    changed = true;
                }
            }
        }

        return _blocks.Where(b => !reachesExit.Contains(b)).ToImmutableHashSet();
    }

    /// <summary>
    /// Find all blocks that contain exception-throwing terminators.
    /// Useful for exception flow analysis.
    /// </summary>
    public ImmutableHashSet<BasicBlock> FindThrowingBlocks()
    {
        return _blocks
            .Where(b => b.Terminator is ThrowTerminator or RethrowTerminator)
            .ToImmutableHashSet();
    }
}
```

**Design Notes:**
- Block IDs are assigned by the owning CFG, ensuring uniqueness within the graph
- Added `SourceFile` property for better diagnostics
- Added `FindThrowingBlocks()` utility for exception flow analysis
- CFG is a `sealed class` because it doesn't need extension

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 1.4: Create ControlFlowEdge for Explicit Edge Tracking

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowEdge.cs`

- [ ] Create `ControlFlowEdge.cs`:

```csharp
namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Describes the kind of edge in the CFG.
/// </summary>
public enum EdgeKind
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
public record ControlFlowEdge(
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
```

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 1

```bash
git add -A
git commit -m "feat(analysis): Add CFG core data structures (BasicBlock, Terminator, ControlFlowGraph, Edge)

- BasicBlock: sequence of statements with single entry/exit
- BlockTerminator hierarchy: Branch, Conditional, Switch, Return, Throw, Break, Continue
- ControlFlowGraph: container with entry/exit and utility methods
- ControlFlowEdge: typed edge representation

This is foundational infrastructure for async/await and pattern matching exhaustiveness.
No behavioral changes - all existing tests pass."
```

---

## Implementation Considerations

### Exception Flow Modeling

The initial implementation uses a **simplified exception model**:
- Exception-throwing statements (`raise`) terminate their block
- Try/except handlers are all conceptually reachable from the try body
- We don't model which specific exception types go to which handlers

**Why simplified:**
1. Accurate exception flow requires tracking exception types through the CFG
2. Python allows catching arbitrary exception types, making exhaustive analysis complex
3. The simplified model is sufficient for return path analysis and unreachable code detection

**Future enhancement:** Add `ExceptionFlowGraph` as a separate layer that refines the basic CFG with exception type information.

### Short-Circuit Boolean Evaluation

Expressions like `a and b` and `a or b` have implicit control flow:
- `a and b`: if `a` is false, `b` is not evaluated
- `a or b`: if `a` is true, `b` is not evaluated

**Current approach:** Treat as single expression in CFG. The sub-expressions are not split into separate blocks.

**Impact:** This is correct for most analyses. For very precise definite assignment analysis, this may need refinement.

### Await Expression Detection

For async/await support (v0.2.x), the CFG builder needs to detect `AwaitExpression` nodes within statements. This requires:
1. Walking all expressions in a statement to find await
2. Setting `BasicBlock.ContainsAwait = true` when found

**Implementation note:** Add an expression visitor that scans for AwaitExpression during statement processing.

### Python-Style Loop Else

Both `while` and `for` loops support an else clause that runs if the loop completes normally (without `break`). The CFG models this as:
- Normal loop exit (condition false) → else block → exit
- `break` statement → exit (bypassing else)

This is implemented via the `LoopContext` which tracks both the loop exit and else block separately.

---

## Phase 2: Unit Tests for CFG Data Structures

**Goal:** Create tests for the data structures before building the CFG builder. This ensures the data structures work correctly in isolation.

### Task 2.1: Create CFG Test Infrastructure

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/ControlFlowTestHelpers.cs`

- [ ] Create directory: `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/`
- [ ] Create `ControlFlowTestHelpers.cs`:

```csharp
using System.Collections.Immutable;
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
    public static IntegerLiteral Int(long value) => new IntegerLiteral { Value = value };

    /// <summary>
    /// Creates a return statement for testing.
    /// </summary>
    public static ReturnStatement Return(Expression? value = null) =>
        new ReturnStatement { Value = value };
}
```

**Design Notes:**
- Uses the new BasicBlock constructor that takes label as parameter
- Uses `AddStatement()` method instead of init-only `Statements` property
- Removed `EdgeKind` parameter from `ConnectBlocks` since edges are implicit in successor/predecessor lists
- Added helper methods for common test constructs

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler.Tests`

### Task 2.2: Create BasicBlock Tests

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/BasicBlockTests.cs`

- [ ] Create `BasicBlockTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class BasicBlockTests
{
    [Fact]
    public void BasicBlock_DefaultsToEmptyStatements()
    {
        var block = new BasicBlock();

        Assert.Empty(block.Statements);
    }

    [Fact]
    public void BasicBlock_CanAddStatements()
    {
        var block = new BasicBlock();
        block.AddStatement(Pass());
        block.AddStatement(Pass());

        Assert.Equal(2, block.Statements.Count);
    }

    [Fact]
    public void BasicBlock_TracksSuccessors()
    {
        var block1 = new BasicBlock("block1");
        var block2 = new BasicBlock("block2");

        ConnectBlocks(block1, block2);

        Assert.Contains(block2, block1.Successors);
    }

    [Fact]
    public void BasicBlock_TracksPredecessors()
    {
        var block1 = new BasicBlock("block1");
        var block2 = new BasicBlock("block2");

        ConnectBlocks(block1, block2);

        Assert.Contains(block1, block2.Predecessors);
    }

    [Fact]
    public void BasicBlock_DoesNotDuplicateConnections()
    {
        var block1 = new BasicBlock();
        var block2 = new BasicBlock();

        ConnectBlocks(block1, block2);
        ConnectBlocks(block1, block2);

        Assert.Single(block1.Successors);
        Assert.Single(block2.Predecessors);
    }

    [Fact]
    public void BasicBlock_ToStringIncludesLabel()
    {
        var block = new BasicBlock("test_block");

        Assert.Contains("test_block", block.ToString());
    }

    [Fact]
    public void BasicBlock_SpanFromFirstStatement()
    {
        var block = new BasicBlock();
        var stmt = new Parser.Ast.PassStatement
        {
            Span = new Text.TextSpan(10, 5)
        };
        block.AddStatement(stmt);

        Assert.NotNull(block.Span);
        Assert.Equal(10, block.Span!.Value.Start);
    }

    [Fact]
    public void BasicBlock_EmptyBlockHasNoSpan()
    {
        var block = new BasicBlock();

        Assert.Null(block.Span);
    }
}
```

**Design Notes:**
- Tests updated to use new BasicBlock constructor and `AddStatement()` method
- Removed test for unique IDs since IDs are now assigned by ControlFlowGraph
- Added tests for Span property behavior

### Task 2.3: Create ControlFlowGraph Tests

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/ControlFlowGraphTests.cs`

- [ ] Create `ControlFlowGraphTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowGraphTests
{
    [Fact]
    public void CreateLinearCfg_HasEntryAndExit()
    {
        var cfg = CreateLinearCfg(Pass());

        Assert.NotNull(cfg.Entry);
        Assert.NotNull(cfg.Exit);
        Assert.Equal("entry", cfg.Entry.Label);
        Assert.Equal("exit", cfg.Exit.Label);
    }

    [Fact]
    public void CreateLinearCfg_EntryConnectsToBody()
    {
        var cfg = CreateLinearCfg(Pass());

        Assert.Single(cfg.Entry.Successors);
    }

    [Fact]
    public void CreateLinearCfg_AssignsBlockIds()
    {
        var cfg = CreateLinearCfg(Pass());

        // All blocks should have unique IDs assigned
        var ids = cfg.Blocks.Select(b => b.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void CreateDiamondCfg_HasCorrectStructure()
    {
        var cfg = CreateDiamondCfg(
            Bool(true),
            new[] { Pass() },
            new[] { Pass() }
        );

        Assert.Equal(6, cfg.Blocks.Count); // entry, cond, then, else, merge, exit
    }

    [Fact]
    public void CreateLoopCfg_HasBackEdge()
    {
        var cfg = CreateLoopCfg(Bool(true), new[] { Pass() });

        var body = cfg.Blocks.First(b => b.Label == "loop_body");
        var header = cfg.Blocks.First(b => b.Label == "loop_header");

        Assert.Contains(header, body.Successors); // back edge
    }

    [Fact]
    public void FindUnreachableBlocks_EmptyForConnectedGraph()
    {
        var cfg = CreateLinearCfg(Pass());

        var unreachable = cfg.FindUnreachableBlocks();

        Assert.Empty(unreachable);
    }

    [Fact]
    public void FindUnreachableBlocks_FindsDisconnectedBlocks()
    {
        var cfg = CreateLinearCfg(Pass());
        var orphan = new BasicBlock("orphan");

        // Add orphan to blocks list without connecting it
        var blocks = cfg.Blocks.ToList();
        blocks.Add(orphan);
        var newCfg = new ControlFlowGraph(cfg.Entry, cfg.Exit, blocks);

        var unreachable = newCfg.FindUnreachableBlocks();

        Assert.Contains(orphan, unreachable);
    }

    [Fact]
    public void GetReversePostOrder_EntryComesFirst()
    {
        var cfg = CreateLinearCfg(Pass());

        var rpo = cfg.GetReversePostOrder();

        Assert.Equal(cfg.Entry, rpo.First());
    }

    [Fact]
    public void GetReversePostOrder_ExitComesLast()
    {
        var cfg = CreateLinearCfg(Pass());

        var rpo = cfg.GetReversePostOrder();

        Assert.Equal(cfg.Exit, rpo.Last());
    }

    [Fact]
    public void FindBlocksNotReachingExit_AllReachInLinearCfg()
    {
        var cfg = CreateLinearCfg(Pass());

        var notReaching = cfg.FindBlocksNotReachingExit();

        Assert.Empty(notReaching);
    }

    [Fact]
    public void FindThrowingBlocks_FindsThrowTerminators()
    {
        var entry = new BasicBlock("entry");
        var throwing = new BasicBlock("throwing");
        var exit = new BasicBlock("exit");

        ConnectBlocks(entry, throwing);
        entry.Terminator = new BranchTerminator(throwing);
        throwing.Terminator = new ThrowTerminator(Id("error"));

        var cfg = new ControlFlowGraph(entry, exit, new List<BasicBlock> { entry, throwing, exit });

        var throwingBlocks = cfg.FindThrowingBlocks();

        Assert.Single(throwingBlocks);
        Assert.Contains(throwing, throwingBlocks);
    }
}
```

**Design Notes:**
- Added test for block ID assignment
- Added test for `FindBlocksNotReachingExit`
- Added test for `FindThrowingBlocks`
- Updated BasicBlock instantiation to use constructor

### Task 2.4: Create BlockTerminator Tests

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/BlockTerminatorTests.cs`

- [ ] Create `BlockTerminatorTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class BlockTerminatorTests
{
    [Fact]
    public void BranchTerminator_HasSingleTarget()
    {
        var target = new BasicBlock { Label = "target" };
        var terminator = new BranchTerminator(target);
        
        Assert.Equal(target, terminator.Target);
    }
    
    [Fact]
    public void ConditionalBranchTerminator_HasBothTargets()
    {
        var trueTarget = new BasicBlock { Label = "true" };
        var falseTarget = new BasicBlock { Label = "false" };
        var terminator = new ConditionalBranchTerminator(
            Bool(true), trueTarget, falseTarget);
        
        Assert.Equal(trueTarget, terminator.TrueTarget);
        Assert.Equal(falseTarget, terminator.FalseTarget);
    }
    
    [Fact]
    public void ReturnTerminator_CanHaveValue()
    {
        var terminator = new ReturnTerminator(Id("x"));
        
        Assert.NotNull(terminator.Value);
    }
    
    [Fact]
    public void ReturnTerminator_CanBeVoid()
    {
        var terminator = new ReturnTerminator(null);
        
        Assert.Null(terminator.Value);
    }
    
    [Fact]
    public void ThrowTerminator_HasException()
    {
        var terminator = new ThrowTerminator(Id("error"));
        
        Assert.NotNull(terminator.Exception);
    }
}
```

**Verification:**
- [ ] All new tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~ControlFlow"`
- [ ] All existing tests still pass: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 2

```bash
git add -A
git commit -m "test(analysis): Add unit tests for CFG data structures

- ControlFlowTestHelpers: factory methods for test CFGs
- BasicBlockTests: id uniqueness, connections, labels
- ControlFlowGraphTests: structure, reachability, reverse post-order
- BlockTerminatorTests: all terminator types

All tests pass. No behavioral changes to existing code."
```

---

## Phase 3: CFG Builder Implementation

**Goal:** Create the builder that constructs CFGs from AST nodes. This is the core logic of the CFG infrastructure.

### Task 3.1: Create CFG Builder Skeleton

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs`

- [ ] Create `ControlFlowGraphBuilder.cs` with initial structure:

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Builds a control flow graph from a function body.
/// </summary>
/// <remarks>
/// The builder operates on the immutable AST nodes (Statement, Expression).
/// It does NOT modify the AST. The resulting CFG references AST nodes
/// but is a separate data structure.
/// </remarks>
public class ControlFlowGraphBuilder
{
    private readonly List<BasicBlock> _blocks = new();
    private BasicBlock _currentBlock = null!;
    private BasicBlock _entry = null!;
    private BasicBlock _exit = null!;

    // Loop tracking for break/continue
    private readonly Stack<LoopContext> _loopStack = new();

    // Exception handler tracking for re-raise
    private readonly Stack<BasicBlock> _handlerStack = new();

    /// <summary>
    /// Context for loop constructs, tracking where break/continue should go.
    /// </summary>
    private record LoopContext(
        BasicBlock Header,        // Where continue jumps to
        BasicBlock Exit,          // Where break jumps to
        BasicBlock? ElseBlock     // Optional else block (runs if loop completes without break)
    );

    /// <summary>
    /// Build a CFG from a function definition.
    /// </summary>
    public ControlFlowGraph Build(FunctionDef function)
    {
        Reset();

        _entry = CreateBlock("entry");
        _exit = CreateBlock("exit");

        var bodyStart = CreateBlock("body_start");
        Connect(_entry, bodyStart);
        _currentBlock = bodyStart;

        BuildStatements(function.Body);

        // If we didn't explicitly return, connect to exit
        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, _exit);
            _currentBlock.Terminator = new BranchTerminator(_exit);
        }

        return new ControlFlowGraph(_entry, _exit, _blocks, function);
    }

    /// <summary>
    /// Build a CFG from a list of top-level statements (module body).
    /// </summary>
    public ControlFlowGraph Build(IReadOnlyList<Statement> statements)
    {
        Reset();

        _entry = CreateBlock("entry");
        _exit = CreateBlock("exit");

        var bodyStart = CreateBlock("body_start");
        Connect(_entry, bodyStart);
        _currentBlock = bodyStart;

        BuildStatements(statements);

        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, _exit);
            _currentBlock.Terminator = new BranchTerminator(_exit);
        }

        return new ControlFlowGraph(_entry, _exit, _blocks);
    }

    private void Reset()
    {
        _blocks.Clear();
        _loopStack.Clear();
        _handlerStack.Clear();
        _currentBlock = null!;
    }

    private BasicBlock CreateBlock(string label = "")
    {
        var block = new BasicBlock(label);
        _blocks.Add(block);
        return block;
    }

    private void Connect(BasicBlock from, BasicBlock to)
    {
        from.AddSuccessor(to);
        to.AddPredecessor(from);
    }

    private void BuildStatements(IReadOnlyList<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            BuildStatement(stmt);

            // If current block is terminated, remaining statements are unreachable
            if (_currentBlock.Terminator != null)
                break;
        }
    }

    private void BuildStatement(Statement stmt)
    {
        switch (stmt)
        {
            case ReturnStatement ret:
                BuildReturn(ret);
                break;

            case IfStatement ifStmt:
                BuildIf(ifStmt);
                break;

            case WhileStatement whileStmt:
                BuildWhile(whileStmt);
                break;

            case ForStatement forStmt:
                BuildFor(forStmt);
                break;

            case BreakStatement breakStmt:
                BuildBreak(breakStmt);
                break;

            case BreakWithFlagStatement breakWithFlag:
                // BreakWithFlagStatement is an internal statement for loop-else support.
                // It sets a flag variable then breaks. We treat it as a break for CFG purposes.
                BuildBreakWithFlag(breakWithFlag);
                break;

            case ContinueStatement contStmt:
                BuildContinue(contStmt);
                break;

            case TryStatement tryStmt:
                BuildTry(tryStmt);
                break;

            case RaiseStatement raiseStmt:
                BuildRaise(raiseStmt);
                break;

            case FunctionDef:
            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
            case TypeAlias:
            case ImportStatement:
            case FromImportStatement:
                // Type/function definitions and imports don't affect control flow
                break;

            default:
                // Simple statements - add to current block
                AddStatement(stmt);
                break;
        }
    }

    private void AddStatement(Statement stmt)
    {
        _currentBlock.AddStatement(stmt);
    }

    // Individual statement builders - implemented in subsequent tasks
    private void BuildReturn(ReturnStatement stmt) => throw new NotImplementedException();
    private void BuildIf(IfStatement stmt) => throw new NotImplementedException();
    private void BuildWhile(WhileStatement stmt) => throw new NotImplementedException();
    private void BuildFor(ForStatement stmt) => throw new NotImplementedException();
    private void BuildBreak(BreakStatement stmt) => throw new NotImplementedException();
    private void BuildBreakWithFlag(BreakWithFlagStatement stmt) => throw new NotImplementedException();
    private void BuildContinue(ContinueStatement stmt) => throw new NotImplementedException();
    private void BuildTry(TryStatement stmt) => throw new NotImplementedException();
    private void BuildRaise(RaiseStatement stmt) => throw new NotImplementedException();
}
```

**Design Notes:**
- Added `_handlerStack` for tracking current exception handler (needed for bare `raise`)
- Added `BreakWithFlagStatement` handling (internal statement for loop-else semantics)
- `LoopContext` now includes optional `ElseBlock` for Python-style loop else
- Added `ImportStatement` and `FromImportStatement` to ignored statements
- Simplified `AddStatement` since BasicBlock is now a mutable class during construction

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 3.2: Implement Simple Statement Handling

**File:** Update `ControlFlowGraphBuilder.cs`

- [ ] Implement `BuildReturn`:

```csharp
private void BuildReturn(ReturnStatement stmt)
{
    AddStatement(stmt);
    Connect(_currentBlock, _exit);
    _currentBlock.Terminator = new ReturnTerminator(stmt.Value)
    {
        SourceStatement = stmt
    };
}
```

- [ ] Implement `BuildRaise`:

```csharp
private void BuildRaise(RaiseStatement stmt)
{
    AddStatement(stmt);

    if (stmt.Exception == null)
    {
        // Bare 'raise' - re-raises current exception
        // Only valid inside an except handler (validated by ControlFlowValidatorV2)
        _currentBlock.Terminator = new RethrowTerminator
        {
            SourceStatement = stmt
        };
    }
    else
    {
        // raise Exception() or raise Exception() from cause
        _currentBlock.Terminator = new ThrowTerminator(stmt.Exception)
        {
            SourceStatement = stmt
        };
    }
    // Note: Exception flow modeling is simplified. Throw terminates the block
    // but doesn't connect to handlers. Full exception flow can be added later.
}
```

- [ ] Implement `BuildBreak`:

```csharp
private void BuildBreak(BreakStatement stmt)
{
    if (_loopStack.Count == 0)
    {
        // Error: break outside loop (caught by ControlFlowValidatorV2)
        // Add statement but don't terminate - let analysis continue
        AddStatement(stmt);
        _currentBlock.Terminator = new UnreachableTerminator { SourceStatement = stmt };
        return;
    }

    AddStatement(stmt);
    var loop = _loopStack.Peek();
    Connect(_currentBlock, loop.Exit);
    _currentBlock.Terminator = new BreakTerminator(loop.Exit)
    {
        SourceStatement = stmt
    };
}
```

- [ ] Implement `BuildBreakWithFlag`:

```csharp
private void BuildBreakWithFlag(BreakWithFlagStatement stmt)
{
    // BreakWithFlagStatement is generated internally for loop-else support.
    // It sets a flag to false before breaking, so the else clause knows not to run.
    // For CFG purposes, we treat it the same as a regular break.

    if (_loopStack.Count == 0)
    {
        // Shouldn't happen with internally generated statements
        AddStatement(stmt);
        _currentBlock.Terminator = new UnreachableTerminator { SourceStatement = stmt };
        return;
    }

    AddStatement(stmt);
    var loop = _loopStack.Peek();
    Connect(_currentBlock, loop.Exit);
    _currentBlock.Terminator = new BreakTerminator(loop.Exit)
    {
        SourceStatement = stmt
    };
}
```

- [ ] Implement `BuildContinue`:

```csharp
private void BuildContinue(ContinueStatement stmt)
{
    if (_loopStack.Count == 0)
    {
        // Error: continue outside loop (caught by ControlFlowValidatorV2)
        AddStatement(stmt);
        _currentBlock.Terminator = new UnreachableTerminator { SourceStatement = stmt };
        return;
    }

    AddStatement(stmt);
    var loop = _loopStack.Peek();
    Connect(_currentBlock, loop.Header);
    _currentBlock.Terminator = new ContinueTerminator(loop.Header)
    {
        SourceStatement = stmt
    };
}
```

**Design Notes:**
- Error cases (break/continue outside loop) now set `UnreachableTerminator` to properly terminate the block
- Added `BuildBreakWithFlag` for internal loop-else support statement
- `RethrowTerminator` is used for bare `raise` (re-raise current exception)

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 3.3: Implement If Statement Handling

**File:** Update `ControlFlowGraphBuilder.cs`

- [ ] Implement `BuildIf`:

```csharp
private void BuildIf(IfStatement stmt)
{
    var mergeBlock = CreateBlock("if_merge");

    // Collect all branches: if + elifs + else
    var branches = new List<(Expression? condition, IReadOnlyList<Statement> body, string label)>
    {
        (stmt.Test, stmt.ThenBody, "if_then")
    };

    for (int i = 0; i < stmt.ElifClauses.Length; i++)
    {
        var elif = stmt.ElifClauses[i];
        branches.Add((elif.Test, elif.Body, $"elif_{i}"));
    }

    if (stmt.ElseBody.Length > 0)
    {
        branches.Add((null, stmt.ElseBody, "if_else")); // null condition = unconditional else
    }

    // Process each branch
    var currentCondBlock = _currentBlock;

    for (int i = 0; i < branches.Count; i++)
    {
        var (condition, body, label) = branches[i];
        var isLast = i == branches.Count - 1;
        var hasElse = stmt.ElseBody.Length > 0;

        if (condition == null)
        {
            // This is the else branch - just build the body
            _currentBlock = currentCondBlock;
            BuildStatements(body);

            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, mergeBlock);
                _currentBlock.Terminator = new BranchTerminator(mergeBlock);
            }
        }
        else
        {
            // Create block for the body
            var bodyBlock = CreateBlock(label);

            // Determine false target
            BasicBlock falseTarget;
            if (isLast && !hasElse)
            {
                // Last condition with no else - false goes to merge
                falseTarget = mergeBlock;
            }
            else if (isLast && hasElse)
            {
                // Last condition before else - false goes to else block
                falseTarget = CreateBlock("if_else_entry");
            }
            else
            {
                // More conditions follow - false goes to next condition block
                falseTarget = CreateBlock($"elif_{i}_cond");
            }

            // Set up conditional branch
            Connect(currentCondBlock, bodyBlock);
            Connect(currentCondBlock, falseTarget);
            currentCondBlock.Terminator = new ConditionalBranchTerminator(condition, bodyBlock, falseTarget);

            // Build body
            _currentBlock = bodyBlock;
            BuildStatements(body);

            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, mergeBlock);
                _currentBlock.Terminator = new BranchTerminator(mergeBlock);
            }

            // Move to next condition block if there are more branches
            currentCondBlock = falseTarget;
        }
    }

    // If no else clause, the last false target was merge, which is correct
    // If there was an else clause, we've already processed it above

    _currentBlock = mergeBlock;
}
```

**Design Notes:**
- Simplified the algorithm by treating else as a branch with null condition
- Properly handles the case where all branches return/throw (merge block may be unreachable)
- Each branch connects to merge if it doesn't have its own terminator

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 3.4: Implement Loop Handling

**File:** Update `ControlFlowGraphBuilder.cs`

**Important:** Python-style loop else semantics are complex. The else clause runs ONLY if the loop completes normally (condition becomes false), NOT if a `break` is executed. This is handled at code generation via flag variables, but for CFG analysis we model it as:
- Loop condition false → else block (if present) → exit
- Loop break → exit (bypassing else)

- [ ] Implement `BuildWhile`:

```csharp
private void BuildWhile(WhileStatement stmt)
{
    var headerBlock = CreateBlock("while_header");
    var bodyBlock = CreateBlock("while_body");
    var exitBlock = CreateBlock("while_exit");

    // Connect current block to header
    Connect(_currentBlock, headerBlock);
    _currentBlock.Terminator = new BranchTerminator(headerBlock);

    BasicBlock loopExitTarget;
    BasicBlock? elseBlock = null;

    if (stmt.ElseBody.Length > 0)
    {
        // With else clause: normal exit goes to else block, break goes to exit
        elseBlock = CreateBlock("while_else");
        loopExitTarget = elseBlock;
    }
    else
    {
        // No else clause: normal exit goes directly to exit
        loopExitTarget = exitBlock;
    }

    // Header: condition check
    // True → body, False → else (if present) or exit
    Connect(headerBlock, bodyBlock);
    Connect(headerBlock, loopExitTarget);
    headerBlock.Terminator = new ConditionalBranchTerminator(stmt.Test, bodyBlock, loopExitTarget);

    // Push loop context for break/continue
    // break always goes to exitBlock (bypassing else), continue goes to header
    _loopStack.Push(new LoopContext(headerBlock, exitBlock, elseBlock));

    // Build body
    _currentBlock = bodyBlock;
    BuildStatements(stmt.Body);

    // Connect body back to header (if not terminated by break/return/etc.)
    if (_currentBlock.Terminator == null)
    {
        Connect(_currentBlock, headerBlock);
        _currentBlock.Terminator = new BranchTerminator(headerBlock);
    }

    _loopStack.Pop();

    // Build else clause if present
    if (elseBlock != null)
    {
        _currentBlock = elseBlock;
        BuildStatements(stmt.ElseBody);

        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, exitBlock);
            _currentBlock.Terminator = new BranchTerminator(exitBlock);
        }
    }

    _currentBlock = exitBlock;
}
```

- [ ] Implement `BuildFor`:

```csharp
private void BuildFor(ForStatement stmt)
{
    // For loops iterate over a collection: for x in items: body
    // The CFG models this as: header (has next?) → body → back to header
    // We use the Iterator expression as a placeholder for the "has next" condition

    var headerBlock = CreateBlock("for_header");
    var bodyBlock = CreateBlock("for_body");
    var exitBlock = CreateBlock("for_exit");

    // Connect current block to header
    Connect(_currentBlock, headerBlock);
    _currentBlock.Terminator = new BranchTerminator(headerBlock);

    BasicBlock loopExitTarget;
    BasicBlock? elseBlock = null;

    if (stmt.ElseBody.Length > 0)
    {
        // With else clause: normal exit goes to else block, break goes to exit
        elseBlock = CreateBlock("for_else");
        loopExitTarget = elseBlock;
    }
    else
    {
        // No else clause: normal exit goes directly to exit
        loopExitTarget = exitBlock;
    }

    // Header: "iterator has more items?" check
    // Note: We use stmt.Iterator as the condition expression.
    // This is a simplification - actual iteration semantics are handled at code gen.
    Connect(headerBlock, bodyBlock);
    Connect(headerBlock, loopExitTarget);
    headerBlock.Terminator = new ConditionalBranchTerminator(stmt.Iterator, bodyBlock, loopExitTarget);

    // Push loop context for break/continue
    _loopStack.Push(new LoopContext(headerBlock, exitBlock, elseBlock));

    // Build body
    _currentBlock = bodyBlock;
    BuildStatements(stmt.Body);

    // Connect body back to header
    if (_currentBlock.Terminator == null)
    {
        Connect(_currentBlock, headerBlock);
        _currentBlock.Terminator = new BranchTerminator(headerBlock);
    }

    _loopStack.Pop();

    // Build else clause if present
    if (elseBlock != null)
    {
        _currentBlock = elseBlock;
        BuildStatements(stmt.ElseBody);

        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, exitBlock);
            _currentBlock.Terminator = new BranchTerminator(exitBlock);
        }
    }

    _currentBlock = exitBlock;
}
```

**Design Notes:**
- The `LoopContext.Exit` field is where `break` jumps to (always bypasses else)
- The `LoopContext.ElseBlock` field tracks whether the loop has an else clause
- Normal loop exit (condition false, iterator exhausted) goes to else if present
- This correctly models Python semantics where break bypasses else

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 3.5: Implement Try/Except Handling (Simplified Model)

**File:** Update `ControlFlowGraphBuilder.cs`

**Design Decision:** Exception flow is complex to model precisely. For the initial implementation, we use a simplified model that:
1. Models control flow for return path analysis (all paths must return)
2. Allows bare `raise` (re-raise) only in handlers by tracking handler context
3. Does NOT model precise exception type routing (which handler catches what)

Full exception flow modeling can be added later for more precise analysis.

- [ ] Implement `BuildTry`:

```csharp
private void BuildTry(TryStatement stmt)
{
    // Structure:
    // - try body: executes normally, may throw
    // - handlers: catch exceptions (simplified - we don't model which catches what)
    // - else: runs if try completes without exception
    // - finally: always runs before exit

    var tryBlock = CreateBlock("try_body");
    var mergeBlock = CreateBlock("try_merge");

    // Connect to try block
    Connect(_currentBlock, tryBlock);
    _currentBlock.Terminator = new BranchTerminator(tryBlock);

    // Build try body
    _currentBlock = tryBlock;
    BuildStatements(stmt.Body);
    var tryExitBlock = _currentBlock;

    // Build handlers
    var handlerExitBlocks = new List<BasicBlock>();
    foreach (var handler in stmt.Handlers)
    {
        var typeName = handler.ExceptionType switch
        {
            SimpleType st => st.Name,
            _ => "all"
        };
        var handlerBlock = CreateBlock($"except_{typeName}");

        // Simplified: all handlers are reachable from try body (exception edges)
        // We don't model which specific exceptions go to which handlers
        Connect(tryBlock, handlerBlock);

        // Push handler context for bare raise
        _handlerStack.Push(handlerBlock);

        _currentBlock = handlerBlock;
        BuildStatements(handler.Body);
        handlerExitBlocks.Add(_currentBlock);

        _handlerStack.Pop();
    }

    // Build else body (runs if try completes without exception)
    BasicBlock? elseExitBlock = null;
    if (stmt.ElseBody.Length > 0)
    {
        var elseBlock = CreateBlock("try_else");

        // else runs only if try completed normally (no exception)
        if (tryExitBlock.Terminator == null)
        {
            Connect(tryExitBlock, elseBlock);
            tryExitBlock.Terminator = new BranchTerminator(elseBlock);
        }

        _currentBlock = elseBlock;
        BuildStatements(stmt.ElseBody);
        elseExitBlock = _currentBlock;
    }

    // Build finally body (always runs)
    if (stmt.FinallyBody.Length > 0)
    {
        var finallyBlock = CreateBlock("finally");

        // Normal path: try (or else) → finally
        var normalExit = elseExitBlock ?? tryExitBlock;
        if (normalExit.Terminator == null)
        {
            Connect(normalExit, finallyBlock);
            normalExit.Terminator = new BranchTerminator(finallyBlock);
        }

        // Handler paths: each handler → finally
        foreach (var handlerExit in handlerExitBlocks)
        {
            if (handlerExit.Terminator == null)
            {
                Connect(handlerExit, finallyBlock);
                handlerExit.Terminator = new BranchTerminator(finallyBlock);
            }
        }

        _currentBlock = finallyBlock;
        BuildStatements(stmt.FinallyBody);

        // finally → merge
        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, mergeBlock);
            _currentBlock.Terminator = new BranchTerminator(mergeBlock);
        }
    }
    else
    {
        // No finally: connect normal path to merge
        var normalExit = elseExitBlock ?? tryExitBlock;
        if (normalExit.Terminator == null)
        {
            Connect(normalExit, mergeBlock);
            normalExit.Terminator = new BranchTerminator(mergeBlock);
        }

        // Connect handler paths to merge
        foreach (var handlerExit in handlerExitBlocks)
        {
            if (handlerExit.Terminator == null)
            {
                Connect(handlerExit, mergeBlock);
                handlerExit.Terminator = new BranchTerminator(mergeBlock);
            }
        }
    }

    _currentBlock = mergeBlock;
}
```

**Design Notes:**
- `_handlerStack` tracks whether we're inside an except handler (for bare `raise` validation)
- Exception edges are simplified: all handlers are conceptually reachable from try body
- The `else` clause only executes if try completes normally (no exception thrown)
- Finally block is reached from both normal and exception paths
- Return statements in try/handler/finally are already handled by `BuildReturn`

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 3

```bash
git add -A
git commit -m "feat(analysis): Implement CFG builder for all statement types

- ControlFlowGraphBuilder: constructs CFG from FunctionDef or statement list
- Handles: return, raise, break, continue, if/elif/else, while, for, try/except/finally
- Loop tracking for break/continue targets
- Basic exception flow modeling

All existing tests pass. CFG builder is ready for integration testing."
```

---

## Phase 4: CFG Builder Tests

**Goal:** Comprehensive tests for the CFG builder.

### Task 4.1: Create CFG Builder Tests

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/ControlFlowGraphBuilderTests.cs`

- [ ] Create comprehensive tests:

```csharp
using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowGraphBuilderTests
{
    private readonly ControlFlowGraphBuilder _builder = new();
    
    #region Simple Functions
    
    [Fact]
    public void Build_EmptyFunction_HasEntryAndExit()
    {
        var func = CreateFunction("empty", ImmutableArray<Statement>.Empty);
        
        var cfg = _builder.Build(func);
        
        Assert.NotNull(cfg.Entry);
        Assert.NotNull(cfg.Exit);
    }
    
    [Fact]
    public void Build_PassStatement_SingleBlock()
    {
        var func = CreateFunction("pass_only", ImmutableArray.Create<Statement>(Pass()));
        
        var cfg = _builder.Build(func);
        
        // entry -> body -> exit
        Assert.True(cfg.Blocks.Count >= 3);
    }
    
    [Fact]
    public void Build_ReturnStatement_ConnectsToExit()
    {
        var func = CreateFunction("returns", ImmutableArray.Create<Statement>(
            new ReturnStatement { Value = Id("x") }
        ));
        
        var cfg = _builder.Build(func);
        
        var returnBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is ReturnTerminator);
        
        Assert.NotNull(returnBlock);
        Assert.Contains(cfg.Exit, returnBlock.Successors);
    }
    
    #endregion
    
    #region If Statements
    
    [Fact]
    public void Build_SimpleIf_CreatesDiamond()
    {
        var func = CreateFunction("simple_if", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var condBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is ConditionalBranchTerminator);
        
        Assert.NotNull(condBlock);
    }
    
    [Fact]
    public void Build_IfElse_HasBothBranches()
    {
        var func = CreateFunction("if_else", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var condBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is ConditionalBranchTerminator cond);
        
        Assert.NotNull(condBlock);
        var cond = (ConditionalBranchTerminator)condBlock!.Terminator!;
        Assert.NotEqual(cond.TrueTarget, cond.FalseTarget);
    }
    
    [Fact]
    public void Build_IfWithReturn_DoesNotReachMerge()
    {
        var func = CreateFunction("if_return", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = Id("x") }
                )
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var thenBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is ReturnTerminator);
        
        Assert.NotNull(thenBlock);
    }
    
    #endregion
    
    #region Loops
    
    [Fact]
    public void Build_WhileLoop_HasBackEdge()
    {
        var func = CreateFunction("while_loop", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var headerBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("header"));
        Assert.NotNull(headerBlock);
        
        // Header should have a predecessor that is the body (back edge)
        Assert.True(headerBlock.Predecessors.Count >= 2); // entry + body
    }
    
    [Fact]
    public void Build_WhileWithBreak_ExitsLoop()
    {
        var func = CreateFunction("while_break", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new BreakStatement()
                )
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var breakBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is BreakTerminator);
        
        Assert.NotNull(breakBlock);
    }
    
    [Fact]
    public void Build_WhileWithContinue_JumpsToHeader()
    {
        var func = CreateFunction("while_continue", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new ContinueStatement()
                )
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var continueBlock = cfg.Blocks.FirstOrDefault(b => 
            b.Terminator is ContinueTerminator);
        
        Assert.NotNull(continueBlock);
        var cont = (ContinueTerminator)continueBlock!.Terminator!;
        Assert.Contains("header", cont.Target.Label);
    }
    
    [Fact]
    public void Build_ForLoop_HasBackEdge()
    {
        var func = CreateFunction("for_loop", ImmutableArray.Create<Statement>(
            new ForStatement
            {
                Target = Id("i"),
                Iterator = Id("items"),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var headerBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("header"));
        Assert.NotNull(headerBlock);
    }
    
    #endregion
    
    #region Try/Except
    
    [Fact]
    public void Build_TryExcept_HasExceptionEdge()
    {
        var func = CreateFunction("try_except", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                Handlers = ImmutableArray.Create(new ExceptHandler
                {
                    ExceptionType = new SimpleType { Name = "Exception" },
                    Body = ImmutableArray.Create<Statement>(Pass())
                })
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var tryBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("try"));
        var exceptBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("except"));
        
        Assert.NotNull(tryBlock);
        Assert.NotNull(exceptBlock);
    }
    
    [Fact]
    public void Build_TryFinally_FinallyAlwaysRuns()
    {
        var func = CreateFunction("try_finally", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                FinallyBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        
        var finallyBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("finally"));
        Assert.NotNull(finallyBlock);
    }
    
    #endregion
    
    #region Reachability
    
    [Fact]
    public void Build_AllBlocksReachable()
    {
        var func = CreateFunction("reachable", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));
        
        var cfg = _builder.Build(func);
        var unreachable = cfg.FindUnreachableBlocks();
        
        Assert.Empty(unreachable);
    }
    
    [Fact]
    public void Build_CodeAfterReturn_IsUnreachable()
    {
        var func = CreateFunction("unreachable", ImmutableArray.Create<Statement>(
            new ReturnStatement { Value = Id("x") },
            Pass() // This should be unreachable
        ));
        
        var cfg = _builder.Build(func);
        
        // The pass statement shouldn't be in any block's statements
        // because we stop processing after return
        var hasPassAfterReturn = cfg.Blocks.Any(b => 
            b.Statements.Length > 1 && 
            b.Statements[0] is ReturnStatement);
        
        Assert.False(hasPassAfterReturn);
    }
    
    #endregion
    
    #region Helpers
    
    private static FunctionDef CreateFunction(string name, ImmutableArray<Statement> body)
    {
        return new FunctionDef
        {
            Name = name,
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = body
        };
    }
    
    #endregion
}
```

**Verification:**
- [ ] All new tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~ControlFlowGraphBuilder"`
- [ ] All existing tests still pass: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 4

```bash
git add -A
git commit -m "test(analysis): Add comprehensive CFG builder tests

- Tests for simple functions, return statements
- Tests for if/elif/else branching
- Tests for while/for loops with break/continue
- Tests for try/except/finally
- Tests for reachability analysis

All tests pass."
```

---

## Phase 5: CFG Analysis Utilities

**Goal:** Add analysis methods that use the CFG for semantic checks.

### Task 5.1: Create CFG Analysis Module

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs`

- [ ] Create analysis utilities:

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Provides control flow analysis utilities using the CFG.
/// </summary>
public static class ControlFlowAnalysis
{
    /// <summary>
    /// Check if all paths through the function return a value.
    /// Returns blocks that don't reach the exit via a return.
    /// </summary>
    public static ImmutableArray<BasicBlock> FindMissingReturnPaths(ControlFlowGraph cfg)
    {
        // Find all blocks that:
        // 1. Have no terminator (fall-through to exit) - missing return
        // 2. Are not the entry or exit block
        // 3. Have successors that include exit
        
        var missingReturn = new List<BasicBlock>();
        
        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry || block == cfg.Exit)
                continue;
                
            // If this block reaches exit but doesn't return
            if (block.Terminator is BranchTerminator branch && 
                branch.Target == cfg.Exit &&
                !block.Statements.Any(s => s is ReturnStatement))
            {
                missingReturn.Add(block);
            }
        }
        
        return missingReturn.ToImmutableArray();
    }
    
    /// <summary>
    /// Find blocks containing unreachable code (after return/raise/break/continue).
    /// </summary>
    public static ImmutableArray<UnreachableCodeInfo> FindUnreachableCode(ControlFlowGraph cfg)
    {
        var result = new List<UnreachableCodeInfo>();
        var unreachableBlocks = cfg.FindUnreachableBlocks();
        
        foreach (var block in unreachableBlocks)
        {
            if (block.Statements.Length > 0)
            {
                result.Add(new UnreachableCodeInfo(
                    block,
                    block.Statements[0]
                ));
            }
        }
        
        return result.ToImmutableArray();
    }
    
    /// <summary>
    /// Check if break/continue are only used inside loops.
    /// </summary>
    public static ImmutableArray<ControlFlowError> ValidateLoopControlFlow(ControlFlowGraph cfg)
    {
        var errors = new List<ControlFlowError>();
        
        foreach (var block in cfg.Blocks)
        {
            if (block.Terminator is BreakTerminator breakTerm)
            {
                // If target is null or not a loop exit, it's an error
                // (The builder sets target to null if not in a loop)
                if (breakTerm.SourceStatement != null && 
                    !IsLoopExitBlock(breakTerm.Target, cfg))
                {
                    errors.Add(new ControlFlowError(
                        "'break' outside loop",
                        breakTerm.SourceStatement
                    ));
                }
            }
            
            if (block.Terminator is ContinueTerminator contTerm)
            {
                if (contTerm.SourceStatement != null &&
                    !IsLoopHeaderBlock(contTerm.Target, cfg))
                {
                    errors.Add(new ControlFlowError(
                        "'continue' outside loop",
                        contTerm.SourceStatement
                    ));
                }
            }
        }
        
        return errors.ToImmutableArray();
    }
    
    /// <summary>
    /// For async/await: identify regions between await points.
    /// Each region becomes a state in the state machine.
    /// </summary>
    public static ImmutableArray<AsyncStateRegion> IdentifyAsyncRegions(ControlFlowGraph cfg)
    {
        // Find all blocks containing await
        var awaitBlocks = cfg.Blocks
            .Where(b => b.ContainsAwait)
            .ToList();
        
        if (awaitBlocks.Count == 0)
        {
            // No awaits - single region
            return ImmutableArray.Create(new AsyncStateRegion(
                0,
                cfg.Blocks.ToImmutableArray(),
                null
            ));
        }
        
        // TODO: Full implementation for async state machine
        // This is a placeholder for v0.2.x async/await support
        
        var regions = new List<AsyncStateRegion>();
        int stateId = 0;
        
        // For now, each await block is its own state
        foreach (var block in awaitBlocks)
        {
            regions.Add(new AsyncStateRegion(
                stateId++,
                ImmutableArray.Create(block),
                null // TODO: extract await expression
            ));
        }
        
        return regions.ToImmutableArray();
    }
    
    private static bool IsLoopExitBlock(BasicBlock? block, ControlFlowGraph cfg)
    {
        return block != null && 
               (block.Label.Contains("exit") || 
                block.Label.Contains("while_") || 
                block.Label.Contains("for_"));
    }
    
    private static bool IsLoopHeaderBlock(BasicBlock? block, ControlFlowGraph cfg)
    {
        return block != null && block.Label.Contains("header");
    }
}

/// <summary>
/// Information about unreachable code.
/// </summary>
public record UnreachableCodeInfo(
    BasicBlock Block,
    Statement FirstUnreachableStatement
);

/// <summary>
/// A control flow validation error.
/// </summary>
public record ControlFlowError(
    string Message,
    Statement Statement
);

/// <summary>
/// A region of code between await points for async state machine generation.
/// </summary>
public record AsyncStateRegion(
    int StateId,
    ImmutableArray<BasicBlock> Blocks,
    Expression? AwaitExpression
);
```

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 5.2: Create Analysis Tests

**File:** `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/ControlFlowAnalysisTests.cs`

- [ ] Create analysis tests:

```csharp
using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowAnalysisTests
{
    private readonly ControlFlowGraphBuilder _builder = new();
    
    [Fact]
    public void FindMissingReturnPaths_AllPathsReturn_Empty()
    {
        var func = new FunctionDef
        {
            Name = "all_return",
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("x") }
                    ),
                    ElseBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("y") }
                    )
                }
            )
        };
        
        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
        
        Assert.Empty(missing);
    }
    
    [Fact]
    public void FindMissingReturnPaths_MissingElseReturn_NotEmpty()
    {
        var func = new FunctionDef
        {
            Name = "missing_return",
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("x") }
                    )
                    // No else - missing return path
                }
            )
        };
        
        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
        
        // There should be a path that doesn't return
        // (The merge block after the if)
        Assert.NotEmpty(missing);
    }
    
    [Fact]
    public void FindUnreachableCode_NoUnreachable_Empty()
    {
        var func = new FunctionDef
        {
            Name = "reachable",
            Body = ImmutableArray.Create<Statement>(
                Pass(),
                new ReturnStatement { Value = Id("x") }
            )
        };
        
        var cfg = _builder.Build(func);
        var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
        
        Assert.Empty(unreachable);
    }
}
```

**Verification:**
- [ ] All new tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~ControlFlowAnalysis"`
- [ ] All existing tests still pass: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 5

```bash
git add -A
git commit -m "feat(analysis): Add CFG analysis utilities

- FindMissingReturnPaths: detect functions missing return statements
- FindUnreachableCode: detect code after return/raise/break/continue
- ValidateLoopControlFlow: verify break/continue inside loops
- IdentifyAsyncRegions: placeholder for async state machine (v0.2.x)

Analysis utilities are ready for integration into validation pipeline."
```

---

## Phase 6: Integration with Validation Pipeline (Optional Enhancement)

**Goal:** Create a new validator that uses CFG for improved control flow checking. This replaces the deprecated ControlFlowValidator.

> **Note:** This phase is optional for initial implementation. The CFG infrastructure can be used independently. Integration with the validation pipeline can be done incrementally.

### Task 6.1: Create CFG-Based Control Flow Validator

**File:** `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidatorV3.cs`

- [ ] Create CFG-based validator:

```csharp
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Control flow validator using CFG analysis.
/// Provides more accurate analysis than the AST-walking V2 validator.
/// </summary>
/// <remarks>
/// This validator is OPTIONAL and behind a feature flag. It provides more
/// accurate analysis but has higher overhead than V2. Use for:
/// - Development/CI where accuracy matters more than speed
/// - Debugging complex control flow issues
/// </remarks>
public class ControlFlowValidatorV3 : SemanticValidatorBase
{
    public override string Name => "ControlFlowV3";
    public override int Order => 400; // Same slot as V2 - use one or the other

    private readonly ControlFlowGraphBuilder _cfgBuilder = new();
    private SemanticContext _context = null!;
    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting CFG-based control flow validation");

        foreach (var stmt in module.Body)
        {
            ValidateTopLevelStatement(stmt);
        }
    }

    private void ValidateTopLevelStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef func:
                ValidateFunction(func);
                break;

            case ClassDef cls:
                foreach (var member in cls.Body)
                    if (member is FunctionDef method)
                        ValidateFunction(method);
                break;

            case StructDef str:
                foreach (var member in str.Body)
                    if (member is FunctionDef method)
                        ValidateFunction(method);
                break;
        }
    }

    private void ValidateFunction(FunctionDef func)
    {
        _logger.LogDebug($"Building CFG for function: {func.Name}");

        // Skip abstract/interface methods
        if (func.Decorators.Any(d => d.Name == "abstract"))
            return;

        // Skip stub bodies (ellipsis only)
        if (func.Body.Length == 1 && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral })
            return;

        // Build CFG
        var cfg = _cfgBuilder.Build(func);

        // 1. Check for unreachable code
        var unreachableBlocks = cfg.FindUnreachableBlocks();
        foreach (var block in unreachableBlocks)
        {
            if (block.Statements.Count > 0)
            {
                var firstStmt = block.Statements[0];
                AddWarning(_context, "Unreachable code detected",
                    firstStmt.LineStart, firstStmt.ColumnStart);
            }
        }

        // 2. Check return paths (if function has return type)
        var returnType = GetFunctionReturnType(func);
        if (returnType != SemanticType.Void)
        {
            var missingReturnBlocks = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
            if (missingReturnBlocks.Length > 0)
            {
                AddError(_context,
                    $"Function '{func.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
                    func.LineStart, func.ColumnStart);
            }
        }

        // 3. Validate loop control flow (break/continue outside loops)
        var loopErrors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);
        foreach (var error in loopErrors)
        {
            AddError(_context, error.Message,
                error.Statement.LineStart, error.Statement.ColumnStart);
        }
    }

    private SemanticType GetFunctionReturnType(FunctionDef func)
    {
        if (func.ReturnType == null)
            return SemanticType.Void;

        // Try cached type first
        var cachedType = _context.SemanticInfo.GetTypeAnnotation(func.ReturnType);
        if (cachedType != null)
            return cachedType;

        // Fall back to resolving
        return _context.TypeResolver.ResolveTypeAnnotation(func.ReturnType);
    }
}
```

**Design Notes:**
- Inherits from `SemanticValidatorBase` like other V2 validators (provides `AddError`, `AddWarning` helpers)
- Uses same `Order` (400) as V2 - should NOT run both validators on same module
- Uses existing `SemanticInfo.GetTypeAnnotation` and `TypeResolver.ResolveTypeAnnotation` APIs
- Logs CFG building for debugging

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`

### Task 6.2: Add V3 Validator to Pipeline Factory (Behind Feature Flag)

**File:** Update `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`

- [ ] Add option to use V3 validator:

```csharp
// Add to ValidationPipelineFactory.cs

/// <summary>
/// Creates a validation pipeline with CFG-based control flow analysis.
/// </summary>
public static ValidationPipeline CreateWithCfgAnalysis()
{
    var pipeline = new ValidationPipeline();
    
    // ... existing validators ...
    
    // Use CFG-based control flow validator instead of V2
    pipeline.AddValidator(new ControlFlowValidatorV3());
    
    return pipeline;
}

/// <summary>
/// Configuration options for the validation pipeline.
/// </summary>
public class ValidationPipelineOptions
{
    /// <summary>
    /// Use CFG-based control flow analysis (more accurate but slower).
    /// Default: false (use V2 AST-walking validator)
    /// </summary>
    public bool UseCfgAnalysis { get; set; } = false;
}
```

**Verification:**
- [ ] File compiles: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests still pass: `dotnet test src/Sharpy.Compiler.Tests`

### 🔖 COMMIT POINT 6

```bash
git add -A
git commit -m "feat(validation): Add CFG-based control flow validator V3

- ControlFlowValidatorV3: uses CFG for more accurate analysis
- Detects unreachable code, missing return paths, invalid break/continue
- Added behind feature flag (UseCfgAnalysis) for gradual rollout

Existing V2 validator unchanged. All tests pass."
```

---

## Phase 7: Documentation and Cleanup

### Task 7.1: Create CFG Infrastructure Documentation

**File:** `src/Sharpy.Compiler/Analysis/ControlFlow/README.md`

- [ ] Create documentation:

```markdown
# Control Flow Graph (CFG) Infrastructure

## Overview

The CFG infrastructure provides control flow analysis capabilities for the Sharpy compiler. It is used for:

- **Return path analysis**: Ensuring functions return values in all code paths
- **Unreachable code detection**: Identifying code after return/raise/break/continue
- **Loop control validation**: Ensuring break/continue are only inside loops
- **Future: Async state machine generation**: Splitting code at await boundaries
- **Future: Pattern matching exhaustiveness**: Analyzing all possible execution paths

## Architecture

```
┌─────────────────────┐
│   FunctionDef AST   │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ ControlFlowGraph    │
│      Builder        │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  ControlFlowGraph   │
│  - Entry/Exit       │
│  - BasicBlocks      │
│  - Terminators      │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ ControlFlowAnalysis │
│  - FindMissing      │
│  - Reachability     │
│  - LoopValidation   │
└─────────────────────┘
```

## Key Classes

- **BasicBlock**: A sequence of statements with single entry/exit
- **BlockTerminator**: Describes how control leaves a block (branch, return, throw, etc.)
- **ControlFlowGraph**: The complete CFG with entry/exit blocks
- **ControlFlowGraphBuilder**: Constructs CFG from AST
- **ControlFlowAnalysis**: Analysis utilities

## Usage Example

```csharp
var builder = new ControlFlowGraphBuilder();
var cfg = builder.Build(functionDef);

// Check return paths
var missingReturns = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
if (missingReturns.Length > 0)
{
    // Report error: function doesn't return in all paths
}

// Check for unreachable code
var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
foreach (var info in unreachable)
{
    // Report warning: unreachable code
}
```

## Future Enhancements

1. **Async/Await Support**: Identify await boundaries for state machine generation
2. **Pattern Matching**: Exhaustiveness checking for match statements
3. **Definite Assignment**: Ensure variables are assigned before use
4. **Data Flow Analysis**: Track variable values through the CFG
```

### Task 7.2: Update Architecture Status Document

**File:** Update `docs/implementation/architecture_status.md` (or create if doesn't exist)

- [ ] Document CFG implementation status:

```markdown
## Recommendation #9: Control Flow Graph Infrastructure

**Status:** ✅ Implemented (Foundation)
**Date:** [Current Date]

### What's Done

- Core data structures: BasicBlock, BlockTerminator, ControlFlowGraph, ControlFlowEdge
- CFG builder for all statement types (if/while/for/try/break/continue/return/raise)
- Analysis utilities: FindMissingReturnPaths, FindUnreachableCode, ValidateLoopControlFlow
- ControlFlowValidatorV3: Optional CFG-based validator (behind feature flag)
- Comprehensive unit tests

### What's Remaining (Future)

- Full async/await state machine region identification
- Pattern matching exhaustiveness checking (when match statement implemented)
- Definite assignment analysis
- Integration with LSP for real-time analysis

### Decision Log

| Decision | Type | Rationale |
|----------|------|-----------|
| Separate data structures from AST | Two-way door | CFG is a separate concern; can change without affecting parser |
| Mutable BasicBlock during construction | Two-way door | Simpler builder logic; sealed after construction |
| Optional V3 validator via feature flag | Two-way door | Allows gradual rollout without breaking existing behavior |
| Basic exception flow modeling | Two-way door | Full exception flow can be added later without breaking API |
```

### Task 7.3: Verify All Tests Pass

- [ ] Run full test suite: `dotnet test src/Sharpy.Compiler.Tests`
- [ ] Verify no test regressions from baseline
- [ ] Run any integration tests if available

### 🔖 FINAL COMMIT

```bash
git add -A
git commit -m "docs(analysis): Add CFG infrastructure documentation

- README.md explaining CFG architecture and usage
- Updated architecture status document

CFG infrastructure is complete and ready for use.
All existing tests pass."
```

---

## Summary

### Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `Analysis/ControlFlow/BasicBlock.cs` | Created | Basic block data structure |
| `Analysis/ControlFlow/BlockTerminator.cs` | Created | Terminator type hierarchy |
| `Analysis/ControlFlow/ControlFlowGraph.cs` | Created | CFG container |
| `Analysis/ControlFlow/ControlFlowEdge.cs` | Created | Edge type |
| `Analysis/ControlFlow/ControlFlowGraphBuilder.cs` | Created | CFG builder |
| `Analysis/ControlFlow/ControlFlowAnalysis.cs` | Created | Analysis utilities |
| `Analysis/ControlFlow/README.md` | Created | Documentation |
| `Semantic/Validation/ControlFlowValidatorV3.cs` | Created | CFG-based validator |
| `Semantic/Validation/ValidationPipelineFactory.cs` | Modified | Add V3 option |

### Tests Created

| File | Description |
|------|-------------|
| `ControlFlowTestHelpers.cs` | Test helper methods |
| `BasicBlockTests.cs` | BasicBlock unit tests |
| `ControlFlowGraphTests.cs` | CFG unit tests |
| `BlockTerminatorTests.cs` | Terminator tests |
| `ControlFlowGraphBuilderTests.cs` | Builder integration tests |
| `ControlFlowAnalysisTests.cs` | Analysis tests |

### Commit Points

1. Core data structures (BasicBlock, Terminator, CFG, Edge)
2. Data structure unit tests
3. CFG builder implementation
4. CFG builder tests
5. Analysis utilities
6. V3 validator (optional)
7. Documentation

### Future Work

This implementation provides the foundation for:
- **v0.2.x async/await**: State machine generation using `IdentifyAsyncRegions`
- **v0.2.x pattern matching**: Exhaustiveness checking
- **LSP integration**: Real-time control flow analysis
- **Advanced diagnostics**: Definite assignment, data flow analysis

The design decisions are two-way doors where possible, allowing future enhancements without breaking changes.
