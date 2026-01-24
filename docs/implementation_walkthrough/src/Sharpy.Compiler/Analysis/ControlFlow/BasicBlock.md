# Walkthrough: BasicBlock.cs

**Source File**: `src/Sharpy.Compiler/Analysis/ControlFlow/BasicBlock.cs`

---

## Overview

`BasicBlock.cs` defines the fundamental building block of **Control Flow Graphs (CFGs)** in the Sharpy compiler. A basic block represents a linear sequence of statements that execute sequentially without internal branching—control enters at the top and exits at the bottom. This file sits in the **Analysis phase** of the compiler pipeline, after parsing but before code generation.

**Role in Pipeline**: After the Parser builds an Abstract Syntax Tree (AST), the semantic analysis phase constructs CFGs to enable:
- Definite assignment analysis (detecting use of uninitialized variables)
- Reachability analysis (detecting unreachable code)
- Return path validation (ensuring all paths return a value)
- Async/await analysis (tracking which blocks contain await expressions)

---

## What is a Basic Block?

In compiler theory, a **basic block** has three defining properties:

1. **Single entry point**: Only the first statement can be branched to from other blocks
2. **Single exit point**: Only the last statement can branch to other blocks
3. **No internal control flow**: Statements execute sequentially with no branches within the block

For example, this Python-like code:
```python
x = 10
y = x + 5
if y > 10:
    print("yes")
else:
    print("no")
```

Would be divided into three basic blocks:
- **Block 1**: `x = 10; y = x + 5` (ends with conditional branch)
- **Block 2**: `print("yes")` (then branch)
- **Block 3**: `print("no")` (else branch)

---

## Class Structure

### BasicBlock (sealed class)

The class is **sealed** (cannot be inherited) and uses a **mutable-during-construction, immutable-after-construction** pattern:

```csharp
public sealed class BasicBlock
{
    public int Id { get; internal set; }
    public string Label { get; }
    public IReadOnlyList<Statement> Statements { get; }
    public IReadOnlyList<BasicBlock> Predecessors { get; }
    public IReadOnlyList<BasicBlock> Successors { get; }
    public BlockTerminator? Terminator { get; internal set; }
    public bool ContainsAwait { get; internal set; }
    public Text.TextSpan? Span { get; }
}
```

**Why not a record?** The class needs **reference identity**—two blocks with identical content are still different blocks in the CFG. Also, predecessor/successor lists are mutated during CFG construction, which would violate record semantics.

---

## Key Properties

### Identity and Metadata

#### `Id` (int)
- **Purpose**: Unique identifier within a CFG (e.g., 0, 1, 2...)
- **Assignment**: Set by `ControlFlowGraph` during construction in sequential order
- **Usage**: Debugging, visualization, and creating stable orderings for analysis algorithms
- **Example**: `BB0:entry`, `BB5:loop_body`

#### `Label` (string)
- **Purpose**: Human-readable name for debugging and diagnostics
- **Examples**: `"entry"`, `"exit"`, `"if_then"`, `"if_else"`, `"loop_body"`, `"loop_condition"`
- **Note**: Empty string for unlabeled blocks; displayed as `BB{Id}:{Label}` via `ToString()`

#### `Span` (TextSpan?)
- **Purpose**: Source location of the first statement (for error reporting)
- **Value**: Null for synthetic blocks (entry/exit) or empty blocks
- **Usage**: Diagnostic messages point to the start of the block

---

### Statements and Control Flow

#### `Statements` (IReadOnlyList&lt;Statement&gt;)
- **Purpose**: The sequential list of AST statements in this block
- **Construction**: Built via `AddStatement()` during CFG construction
- **Empty blocks**: Entry/exit blocks have no statements
- **Ordering**: Execution order (top to bottom)

**Implementation detail**: Exposed as `IReadOnlyList` but backed by private mutable `List<Statement>` to allow construction:

```csharp
public IReadOnlyList<Statement> Statements => _statements;
private readonly List<Statement> _statements;
```

#### `Terminator` (BlockTerminator?)
- **Purpose**: Describes how control leaves this block (branch, return, throw, etc.)
- **Types**: See `BlockTerminator.cs` for 10 terminator types
- **Null case**: Only the synthetic **exit block** has null terminator
- **Set by**: CFG builder when processing control flow statements

**Common terminators**:
- `BranchTerminator`: Unconditional jump (fall-through, goto)
- `ConditionalBranchTerminator`: if/while/for conditions
- `ReturnTerminator`: return statements
- `BreakTerminator` / `ContinueTerminator`: loop control
- `ThrowTerminator`: raise statements

---

### Graph Connections

#### `Predecessors` (IReadOnlyList&lt;BasicBlock&gt;)
- **Purpose**: Blocks that can transfer control **to** this block
- **Example**: In `if x > 0: foo()`, the condition block is a predecessor to both the then-block and the next block
- **Usage**: Backward data flow analysis (reaching definitions, liveness analysis)

#### `Successors` (IReadOnlyList&lt;BasicBlock&gt;)
- **Purpose**: Blocks that control can transfer **to** from this block
- **Count**:
  - 0 successors: exit block, blocks ending in throw/return
  - 1 successor: unconditional branches
  - 2 successors: conditional branches (if/while)
  - 3+ successors: match/switch statements (future)
- **Usage**: Forward data flow analysis (available expressions, constant propagation)

**Deduplication**: `AddPredecessor()` and `AddSuccessor()` check for duplicates to avoid cycles inflating edge counts:

```csharp
internal void AddSuccessor(BasicBlock block)
{
    if (!_successors.Contains(block))
        _successors.Add(block);
}
```

---

### Async Analysis Support

#### `ContainsAwait` (bool)
- **Purpose**: Tracks whether this block has any `await` expressions
- **Set during**: CFG construction by scanning statement trees
- **Use case**:
  - Function-level async inference (if any block has await → function must be async)
  - Async/sync boundary detection
  - Diagnostic: "await used in non-async function"

**Example**: In Python-like syntax:
```python
async def fetch_data():
    x = prepare_request()    # Block 1: ContainsAwait = false
    data = await http.get()  # Block 2: ContainsAwait = true
    return process(data)      # Block 3: ContainsAwait = false
```

---

## Key Methods

### Constructor: `BasicBlock(string label = "")`

```csharp
public BasicBlock(string label = "")
{
    Label = label;
    _statements = new List<Statement>();
}
```

- **Parameters**: Optional label (defaults to empty)
- **Initializes**: Empty statement list, empty predecessor/successor lists
- **Note**: ID is assigned later by `ControlFlowGraph`

**Usage pattern**:
```csharp
var entryBlock = new BasicBlock("entry");
var thenBlock = new BasicBlock("if_then");
var elseBlock = new BasicBlock("if_else");
```

---

### `AddStatement(Statement stmt)` (internal)

```csharp
internal void AddStatement(Statement stmt)
{
    _statements.Add(stmt);
}
```

- **Visibility**: `internal` (only CFG builder can modify)
- **Purpose**: Append a statement during CFG construction
- **No validation**: Caller must ensure statements are added in execution order
- **After construction**: No more statements should be added

---

### `AddPredecessor(BasicBlock block)` (internal)

```csharp
internal void AddPredecessor(BasicBlock block)
{
    if (!_predecessors.Contains(block))
        _predecessors.Add(block);
}
```

- **Purpose**: Record that `block` can transfer control to this block
- **Deduplication**: Prevents duplicate edges (important for loops/diamonds)
- **Bidirectional**: CFG builder must also call `block.AddSuccessor(this)`

**Example**: For `if x: foo()`, the condition block adds both then-block and after-block as successors, and both add condition-block as predecessor.

---

### `AddSuccessor(BasicBlock block)` (internal)

```csharp
internal void AddSuccessor(BasicBlock block)
{
    if (!_successors.Contains(block))
        _successors.Add(block);
}
```

- **Symmetry**: Same pattern as `AddPredecessor`
- **Note**: Using `Contains()` is O(n) but acceptable—successor counts are small (typically ≤ 2)

---

### `ToString()` (override)

```csharp
public override string ToString() =>
    string.IsNullOrEmpty(Label) ? $"BB{Id}" : $"BB{Id}:{Label}";
```

- **Format**: `BB{Id}:{Label}` or just `BB{Id}` if no label
- **Examples**: `"BB0:entry"`, `"BB3:if_then"`, `"BB7"`
- **Usage**: Debugging output, CFG visualization, test assertions

---

## Dependencies

### Internal Dependencies

**`Sharpy.Compiler.Parser.Ast`**
- `Statement`: Base class for all AST statement nodes
- `Expression`: Used in terminators (conditions, return values)
- `Text.TextSpan`: Source location tracking

**`Sharpy.Compiler.Analysis.ControlFlow`** (same namespace)
- `BlockTerminator`: Abstract base for terminators (see below)
- `ControlFlowGraph`: Owns and manages BasicBlock instances

### Related Types

**`BlockTerminator` hierarchy** (see `BlockTerminator.cs`):
- `BranchTerminator`: Unconditional jump
- `ConditionalBranchTerminator`: if/while/for
- `SwitchTerminator`: match/switch (v0.2.x)
- `ReturnTerminator`: return statement
- `ThrowTerminator`: raise statement
- `BreakTerminator` / `ContinueTerminator`: loop control
- `RethrowTerminator`: bare `raise` in except handler
- `UnreachableTerminator`: Marker for unreachable code

**`ControlFlowGraph`** (see `ControlFlowGraph.cs`):
- Contains all blocks for a function
- Provides synthetic `Entry` and `Exit` blocks
- Offers analysis methods: `GetReversePostOrder()`, `FindUnreachableBlocks()`, etc.

---

## Patterns and Design Decisions

### 1. Mutable-During-Construction Pattern

**Problem**: CFGs are cyclic graphs (loops create back edges), so you can't build them immutably top-down.

**Solution**:
- Public API is read-only (`IReadOnlyList` properties)
- Mutation methods (`AddStatement`, `AddSuccessor`) are `internal`
- CFG builder has exclusive access during construction
- After construction, blocks are effectively immutable

**Trade-off**: Violates pure functional style but enables natural graph construction.

---

### 2. Reference Identity over Structural Identity

**Why not `record`?** Two blocks with identical statements are still **different blocks** in the graph:

```csharp
// These are different blocks even with same content
var block1 = new BasicBlock("loop_body");
block1.AddStatement(printStmt);
var block2 = new BasicBlock("loop_body");
block2.AddStatement(printStmt);

// We need: block1 != block2 (reference inequality)
```

Using a `class` ensures reference identity for graph algorithms (visited sets, reachability, etc.).

---

### 3. Synthetic Entry/Exit Blocks

**Pattern**: Every CFG has two synthetic blocks:
- **Entry block**: No statements, single successor (first real block)
- **Exit block**: No statements, no successors, null terminator

**Benefits**:
- Uniform handling: all CFGs have exactly one entry and one exit
- Simplifies analysis: no special-casing for "functions with multiple returns"
- Exit block unifies all return paths (useful for data flow analysis)

**Example**:
```python
def foo(x):
    if x > 0:
        return x
    return -x
```

CFG structure:
```
Entry → Condition → [Then: return x] → Exit
                   ↘ [Else: return -x] ↗
```

Both return paths converge at Exit.

---

### 4. Deduplication in Edge Lists

**Why check `Contains()` before adding?**

Consider a loop:
```python
while x > 0:
    x -= 1
```

CFG has back edge: `loop_body → condition`. Without deduplication, you'd add the edge twice during construction. This inflates degree counts and breaks analysis algorithms.

**Cost**: O(n) lookup, but successor lists are tiny (usually 1-2 elements).

---

## Debugging Tips

### 1. Visualizing CFGs

**Print CFG structure**:
```csharp
foreach (var block in cfg.Blocks)
{
    Console.WriteLine($"{block}:");
    foreach (var stmt in block.Statements)
        Console.WriteLine($"  {stmt}");
    Console.WriteLine($"  → {block.Terminator}");
    Console.WriteLine($"  Successors: {string.Join(", ", block.Successors)}");
}
```

**Output example**:
```
BB0:entry:
  → Branch(BB1)
  Successors: BB1:condition

BB1:condition:
  [while x > 0]
  → ConditionalBranch(x > 0, BB2, BB3)
  Successors: BB2:loop_body, BB3:exit
```

---

### 2. Common Assertion Failures

**"Block has no terminator"** → Builder forgot to set terminator (except for exit block)

**"Block has successors but terminator is Return/Throw"** → Inconsistency between terminator type and successor count

**"Unreachable code detected"** → Use `cfg.FindUnreachableBlocks()` to find dead blocks

**"Missing return statement"** → Use `cfg.FindBlocksNotReachingExit()` to find paths without returns

---

### 3. Async Analysis Issues

**Problem**: Function contains `await` but isn't marked `async`

**Debug**:
```csharp
foreach (var block in cfg.Blocks)
{
    if (block.ContainsAwait)
        Console.WriteLine($"Block {block} contains await at {block.Span}");
}
```

---

### 4. Graph Structural Issues

**Predecessor/successor mismatch**:
```csharp
// Verify bidirectional invariants
foreach (var block in cfg.Blocks)
{
    foreach (var succ in block.Successors)
    {
        Debug.Assert(succ.Predecessors.Contains(block),
            $"{block} → {succ} but {succ} doesn't list {block} as predecessor");
    }
}
```

---

## Contribution Guidelines

### When to Modify BasicBlock

**Add properties for new analysis passes**:
- Example: `ContainsAwait` was added to support async/await analysis
- Pattern: Add `internal set` property, populate during CFG construction

**Add metadata for diagnostics**:
- Example: Adding `Span` property for error reporting
- Keep it read-only from outside

**Don't modify**:
- Core graph structure (predecessors/successors) unless rearchitecting CFG builder
- Method signatures of `AddStatement`, `AddSuccessor`, etc. (breaking changes)

---

### Testing Changes

**Unit tests** (likely in `Sharpy.Compiler.Tests/Analysis/ControlFlow/`):
- Test graph construction for new control flow patterns
- Verify predecessor/successor bidirectionality
- Check edge cases (empty blocks, unreachable blocks)

**Integration tests**:
- `.spy` files with complex control flow (nested loops, try/except, async)
- Verify CFG correctness by inspecting generated C# (via `/project:emit`)

---

### Code Style Conventions

**1. Internal mutators, public readers**:
```csharp
public IReadOnlyList<Statement> Statements => _statements;
private readonly List<Statement> _statements;

internal void AddStatement(Statement stmt) { ... }
```

**2. Null handling**:
- `Terminator` is `BlockTerminator?` (only exit block has null)
- `Span` is `TextSpan?` (synthetic blocks have null)

**3. Deduplication**:
- Always check `Contains()` before adding to predecessor/successor lists

---

## Cross-References

### Related Files in Same Namespace

- **[`BlockTerminator.cs`](./BlockTerminator.md)** — Defines 10 terminator types (Branch, ConditionalBranch, Return, Throw, etc.)
- **[`ControlFlowGraph.cs`](./ControlFlowGraph.md)** — Container for BasicBlocks, provides analysis methods
- **`ControlFlowGraphBuilder.cs`** (likely exists) — Constructs CFGs from AST by creating BasicBlocks

### Related Components

- **Parser (`Sharpy.Compiler.Parser.Ast`)** — Provides `Statement` and `Expression` AST nodes
- **Semantic Analysis** — Uses CFGs for definite assignment, reachability, return validation
- **RoslynEmitter** — May use CFG analysis results to emit warnings/errors

### Documentation

- **Language Specification** (`docs/language_specification/`) — Defines control flow semantics
- **Architecture Overview** (`.github/copilot-instructions.md`) — High-level compiler pipeline

---

## Advanced Topics

### Data Flow Analysis

BasicBlock is designed to support classic compiler optimizations:

**Forward analysis** (e.g., constant propagation):
- Start at entry, traverse successors
- Merge data flow facts at blocks with multiple predecessors

**Backward analysis** (e.g., liveness analysis):
- Start at exit, traverse predecessors
- Compute what variables are "live" at block entry

**Reverse post-order traversal** (`cfg.GetReversePostOrder()`):
- Efficient ordering for iterative data flow algorithms
- Processes blocks before their successors (except back edges)

---

### Exception Handling (Future)

When Sharpy adds try/except support:
- Exception edges will connect throwing blocks to handler blocks
- `BlockTerminator` may need `ExceptHandlerTerminator`
- `Successors` would include both normal flow and exceptional flow

---

### Loop Detection

CFG enables identifying loops via back edges:
- **Back edge**: Edge from block B to ancestor A in DFS tree
- **Natural loop**: Set of blocks dominated by header with back edge to header
- **Use cases**: Loop optimization, continue/break validation

---

## Summary

`BasicBlock.cs` defines the core data structure for control flow analysis in Sharpy:
- **Linear sequences** of statements with single entry/exit
- **Graph connections** via predecessor/successor lists
- **Control flow exit** via `BlockTerminator`
- **Metadata** for async analysis, diagnostics, and debugging

Understanding BasicBlock is essential for working on:
- Semantic analysis (definite assignment, reachability)
- Control flow diagnostics (unreachable code, missing returns)
- Future optimizations (dead code elimination, constant propagation)

The class balances **immutability** (read-only public API) with **practical construction** (internal mutators), reflecting the cyclic nature of control flow graphs.
