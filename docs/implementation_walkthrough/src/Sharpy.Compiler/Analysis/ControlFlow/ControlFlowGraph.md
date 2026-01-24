# Walkthrough: ControlFlowGraph.cs

**Source File**: `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraph.cs`

---

## 1. Overview

The `ControlFlowGraph` class is the core data structure representing the control flow of a Sharpy function or method. It sits in the **Semantic Analysis** phase of the compiler pipeline:

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#
                                             ↑
                                    ControlFlowGraph lives here
```

### Purpose

This class represents a function's control flow as a graph of basic blocks, enabling the compiler to:

- **Verify return paths**: Ensure all execution paths return a value (or None for void functions)
- **Detect unreachable code**: Find statements that can never execute
- **Validate control flow**: Ensure break/continue are only in loops, returns are valid, etc.
- **Future analyses**: Support async/await state machine generation, definite assignment, and data flow analysis

### Key Insight

Once constructed, a `ControlFlowGraph` is **effectively immutable**. The builder creates the blocks and connections, then the graph is frozen and used for analysis. This immutability makes it safe to cache and reuse across multiple analysis passes.

---

## 2. Class/Type Structure

### Core Properties

```csharp
public sealed class ControlFlowGraph
{
    // Synthetic blocks - every CFG has these
    public BasicBlock Entry { get; }  // No statements, connects to first real block
    public BasicBlock Exit { get; }   // All return paths converge here

    // All blocks in the graph
    public IReadOnlyList<BasicBlock> Blocks { get; }

    // Source context (for diagnostics)
    public FunctionDef? SourceFunction { get; }
    public string? SourceFile { get; init; }
}
```

### Design Decisions

1. **Sealed class**: Cannot be inherited - this is a concrete data structure, not an extensibility point
2. **Entry/Exit blocks**: These are synthetic (no statements) and simplify graph algorithms:
   - **Entry**: Single starting point for forward traversals
   - **Exit**: Single endpoint for backward reachability analysis
3. **No particular block order**: Blocks are stored in construction order, not execution order (use `GetReversePostOrder()` for that)

---

## 3. Key Methods

### 3.1 Constructor (Internal)

```csharp
internal ControlFlowGraph(
    BasicBlock entry,
    BasicBlock exit,
    List<BasicBlock> blocks,
    FunctionDef? sourceFunction = null)
```

**Who calls this?** Only `ControlFlowGraphBuilder` (see `ControlFlowGraphBuilder.cs:XXX`)

**What it does:**
1. Stores the entry, exit, and all blocks
2. Assigns sequential IDs to all blocks (0, 1, 2, ...) for debugging and visualization
3. Sets the source function reference for error reporting

**Why internal?** CFG construction is complex and error-prone. By making this internal, we force all creation to go through the builder, which handles the intricate block wiring and terminator setup correctly.

---

### 3.2 GetReversePostOrder()

```csharp
public IReadOnlyList<BasicBlock> GetReversePostOrder()
```

**Purpose**: Returns blocks in an order optimal for **forward data flow analysis**.

**Algorithm**:
1. Performs depth-first search from Entry
2. Adds each block to a list when all its children are visited (post-order)
3. Reverses the list (hence "reverse post-order")

**Why this order matters:**

In reverse post-order, **dominators come before dominated blocks**. This means:
- Loop headers come before loop bodies
- If blocks come before then/else blocks
- You can process blocks in a single pass for many analyses

**Example:**

```python
# Sharpy code
def foo(x: int) -> int:
    if x > 0:
        return x
    else:
        return -x
```

Reverse post-order might be: `[Entry, BB_condition, BB_then, BB_else, Exit]`

This ordering is **essential** for optimization passes like constant propagation, where you want to propagate values from definitions to uses in a single forward sweep.

---

### 3.3 FindUnreachableBlocks()

```csharp
public ImmutableHashSet<BasicBlock> FindUnreachableBlocks()
```

**Purpose**: Find blocks that can never be reached from Entry (dead code detection).

**Algorithm**:
1. **Forward reachability** using a worklist (breadth-first search)
2. Start from Entry, mark it reachable
3. Add all successors to worklist, mark them reachable
4. Return all blocks NOT marked reachable

**Example use case:**

```python
def example() -> int:
    return 42
    print("This is unreachable!")  # Warning: unreachable code
```

The CFG will have a block after the return statement, but it won't be reachable from Entry.

**Performance**: O(V + E) where V = blocks, E = edges (typical graph traversal)

---

### 3.4 FindBlocksNotReachingExit()

```csharp
public ImmutableHashSet<BasicBlock> FindBlocksNotReachingExit()
```

**Purpose**: Find blocks that **don't reach the Exit block** - used to detect missing return statements.

**Algorithm**:
1. **Backward reachability** using fixed-point iteration
2. Start with Exit in the "reaches-exit" set
3. Repeatedly add blocks whose successors are already in the set
4. Continue until no more blocks are added (fixed point)
5. Return blocks NOT in the set

**Why fixed-point iteration?**

Unlike forward reachability, we can't just traverse predecessors (they're not always wired up). Instead, we iterate until convergence:

```
Iteration 1: {Exit}
Iteration 2: {Exit, Return1, Return2}
Iteration 3: {Exit, Return1, Return2, IfThen, IfElse, ConditionBlock}
...converged
```

**When blocks don't reach Exit:**

1. **Infinite loops**: `while True: pass` never reaches Exit
2. **All paths throw**: Every branch throws an exception
3. **Missing returns**: Some path falls off the end without returning

**Example:**

```python
def missing_return(x: int) -> int:
    if x > 0:
        return x
    # Missing return here! FalseTarget block doesn't reach Exit
```

The `FalseTarget` block after the if statement won't reach Exit because it falls through without a return.

---

### 3.5 FindThrowingBlocks()

```csharp
public ImmutableHashSet<BasicBlock> FindThrowingBlocks()
```

**Purpose**: Identify blocks that explicitly throw exceptions (for exception flow analysis).

**Algorithm**: Simple filter - find blocks whose terminator is `ThrowTerminator` or `RethrowTerminator`.

**Use cases:**

1. **Exception safety analysis**: Does this function guarantee no exceptions?
2. **Try-catch validation**: Are all throws caught by handlers?
3. **Control flow verification**: Exception blocks shouldn't require returns

**Example:**

```python
def validate(x: int) -> int:
    if x < 0:
        raise ValueError("x must be positive")  # ThrowTerminator block
    return x
```

---

## 4. Dependencies

### Internal Dependencies

- **`BasicBlock`** (`BasicBlock.cs`): The nodes of the graph
  - Contains statements, predecessors/successors, and a terminator
  - Mutable during construction, effectively immutable after

- **`BlockTerminator`** (`BlockTerminator.cs`): Describes how control leaves a block
  - `BranchTerminator`: Unconditional jump
  - `ConditionalBranchTerminator`: If/while conditions
  - `ReturnTerminator`: Function returns
  - `ThrowTerminator`/`RethrowTerminator`: Exception throwing
  - `BreakTerminator`/`ContinueTerminator`: Loop control
  - `SwitchTerminator`: Match/switch statements (future)
  - `UnreachableTerminator`: Should never appear in valid CFGs

### External Dependencies

- **`Sharpy.Compiler.Parser.Ast`**: The `FunctionDef` AST node
  - Used for source context and diagnostics
  - CFG is built FROM the AST but doesn't modify it (immutable AST principle)

### Upstream Components

- **`ControlFlowGraphBuilder`** (`ControlFlowGraphBuilder.cs`): Constructs CFGs from AST nodes
- **`ControlFlowAnalysis`** (`ControlFlowAnalysis.cs`): Uses CFG for semantic checks

### Downstream Components

- **Semantic validation**: Uses CFG to validate return paths, unreachable code
- **Code generation** (future): Async state machine generation will split CFG at await points
- **Optimization** (future): Dead code elimination, constant propagation

---

## 5. Patterns and Design Decisions

### 5.1 Immutability After Construction

**Pattern**: "Freeze after construction"

The CFG and its blocks are mutable during building (internal methods like `AddStatement`, `AddSuccessor`), but external code treats them as immutable.

**Why?**
- **Safety**: Prevents accidental corruption during analysis
- **Caching**: Can safely reuse CFGs across multiple analysis passes
- **Concurrency-ready**: Multiple analyses can read the same CFG

### 5.2 Synthetic Entry/Exit Blocks

**Pattern**: "Canonical start/end points"

Every CFG has Entry and Exit blocks, even if the function is a single `return 42`.

**Benefits:**
1. **Simplifies algorithms**: No special-casing "what if there's no first block?"
2. **Multiple returns**: All returns connect to Exit, giving you a single convergence point
3. **Empty functions**: Even `def foo(): pass` has Entry → Exit

**Example:**

```python
def multi_return(x: int) -> int:
    if x > 0:
        return x
    else:
        return -x
```

CFG structure:
```
Entry → ConditionBlock → ThenBlock → Exit
                      ↘ ElseBlock ↗
```

Both return paths converge at Exit.

### 5.3 Separation of Concerns

**Why is this a separate class from the builder?**

1. **Single Responsibility**: CFG is data, Builder is construction logic
2. **Testability**: Can create CFGs directly in tests without going through full AST parsing
3. **Future flexibility**: Could have multiple builders (optimized CFG, async-aware CFG, etc.)

---

## 6. Debugging Tips

### 6.1 Visualizing the CFG

**ToString() pattern:**

```csharp
// BasicBlock.ToString() produces: "BB0:entry", "BB1:if_then", etc.
foreach (var block in cfg.Blocks)
{
    Console.WriteLine($"{block} → {string.Join(", ", block.Successors)}");
}
```

**Output:**
```
BB0:entry → BB1
BB1:condition → BB2, BB3
BB2:then → BB4
BB3:else → BB4
BB4:exit
```

### 6.2 Common Issues

**Issue 1: "Missing return path" false positives**

If `FindBlocksNotReachingExit()` returns blocks that YOU think should reach Exit:
1. Check if the block has a terminator (null terminator means construction error)
2. Verify the terminator's target is wired correctly
3. Ensure all return statements create `ReturnTerminator` (not just `BranchTerminator` to Exit)

**Issue 2: "Unreachable code" false positives**

If `FindUnreachableBlocks()` returns blocks that ARE reachable:
1. Check if predecessors are wired correctly (use `block.Predecessors`)
2. Verify Entry block connects to the first real block
3. Look for broken edges in the builder logic

**Issue 3: Fixed-point iteration doesn't converge**

If `FindBlocksNotReachingExit()` hangs:
1. Check for cycles in successor edges (infinite loop detection is separate)
2. Verify the algorithm's fixed-point condition (`changed = false` when no new blocks added)

### 6.3 Instrumentation Points

Add logging to see graph traversals:

```csharp
var rpo = cfg.GetReversePostOrder();
Console.WriteLine($"RPO: {string.Join(" → ", rpo.Select(b => b.ToString()))}");

var unreachable = cfg.FindUnreachableBlocks();
Console.WriteLine($"Unreachable: {string.Join(", ", unreachable)}");
```

---

## 7. Contribution Guidelines

### When You'd Modify This File

1. **New traversal algorithms**: Adding a new graph traversal method (e.g., `FindLoops()`, `ComputeDominators()`)
2. **New analysis queries**: Adding helpers for specific analyses (e.g., `FindBlocksWithAwait()`)
3. **Performance optimization**: Caching computed results (e.g., memoize reverse post-order)
4. **Bug fixes**: Fixing issues in existing traversal algorithms

### When You'd NOT Modify This File

1. **Changing block contents**: Modify `BasicBlock.cs` instead
2. **Adding terminator types**: Modify `BlockTerminator.cs` instead
3. **Changing construction logic**: Modify `ControlFlowGraphBuilder.cs` instead
4. **Adding new analyses**: Create methods in `ControlFlowAnalysis.cs` instead (keep this class focused on the data structure)

### Design Constraints

1. **Keep immutable after construction**: Don't add public mutators
2. **Maintain Entry/Exit invariants**: Every CFG has them, they're never null
3. **No AST modification**: CFG is read-only view of AST, never mutates it
4. **Performance-conscious**: Graph algorithms run on every function; O(n²) is usually too slow

### Testing Considerations

When adding new methods:
1. **Test empty functions**: `def foo(): pass`
2. **Test infinite loops**: `while True: pass`
3. **Test multiple returns**: `if cond: return a else: return b`
4. **Test exceptions**: `raise Exception()` paths
5. **Test unreachable code**: Code after `return` or `raise`

---

## 8. Cross-References

### Related Files in ControlFlow Module

- **[BasicBlock.cs](BasicBlock.md)**: The nodes of the graph (if walkthrough exists)
- **[BlockTerminator.cs](BlockTerminator.md)**: How control leaves blocks (if walkthrough exists)
- **[ControlFlowGraphBuilder.cs](ControlFlowGraphBuilder.md)**: How CFGs are constructed from AST (if walkthrough exists)
- **[ControlFlowAnalysis.cs](ControlFlowAnalysis.md)**: High-level analyses using CFGs (if walkthrough exists)
- **[README.md](README.md)**: Overview of the entire ControlFlow module

### Upstream Dependencies

- `Sharpy.Compiler.Parser.Ast.FunctionDef`: The AST node representing a function definition

### Downstream Consumers

- **Semantic validation pipeline**: Uses CFG for return path and reachability checks
- **Future: Async/await lowering**: Will split CFG at await boundaries
- **Future: Optimization passes**: Dead code elimination, constant propagation

### Related Documentation

- `docs/language_specification/`: Authoritative spec for control flow semantics
- `.github/agents/semantic-expert.md`: Agent specializing in semantic analysis

---

## Quick Reference Card

| Method | Purpose | Algorithm | Time Complexity |
|--------|---------|-----------|-----------------|
| `GetReversePostOrder()` | Get blocks in forward-analysis order | DFS + reverse | O(V + E) |
| `FindUnreachableBlocks()` | Find dead code | Forward reachability (BFS) | O(V + E) |
| `FindBlocksNotReachingExit()` | Find missing returns | Backward reachability (fixed-point) | O(V × E) worst case |
| `FindThrowingBlocks()` | Find exception-throwing blocks | Filter by terminator type | O(V) |

**Legend**: V = number of blocks, E = number of edges

---

## Example: Complete CFG for a Simple Function

```python
# Sharpy code
def abs_value(x: int) -> int:
    if x >= 0:
        return x
    else:
        return -x
```

**Resulting CFG:**

```
Entry (BB0)
  ↓
Condition (BB1): [if x >= 0]
  ↓ true          ↓ false
ThenBlock (BB2)  ElseBlock (BB3)
  [return x]       [return -x]
  ↓                ↓
  └─────→ Exit (BB4) ←─────┘
```

**Block Details:**

- **BB0 (Entry)**: Statements: [], Terminator: `Branch(BB1)`
- **BB1 (Condition)**: Statements: [], Terminator: `ConditionalBranch(x >= 0, BB2, BB3)`
- **BB2 (ThenBlock)**: Statements: [], Terminator: `Return(x)`
- **BB3 (ElseBlock)**: Statements: [], Terminator: `Return(-x)`
- **BB4 (Exit)**: Statements: [], Terminator: null

**Analysis Results:**

- `GetReversePostOrder()`: `[BB0, BB1, BB2, BB3, BB4]`
- `FindUnreachableBlocks()`: `[]` (no unreachable code)
- `FindBlocksNotReachingExit()`: `[]` (all paths return)
- `FindThrowingBlocks()`: `[]` (no exceptions)

---

## Final Notes for Newcomers

1. **Start with the README**: `src/Sharpy.Compiler/Analysis/ControlFlow/README.md` gives the big picture
2. **Read ControlFlowGraphBuilder next**: Understanding how CFGs are built helps you understand their structure
3. **This class is simple**: Most complexity is in the builder and analysis - this is just the data structure
4. **Graph algorithms are standard**: If you know BFS/DFS, you'll understand this code
5. **The real power**: This simple data structure enables sophisticated compiler analyses (return checking, async/await, optimization, etc.)

**Welcome to the Sharpy compiler team! 🎉**
