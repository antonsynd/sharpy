# Walkthrough: ControlFlowEdge.cs

**Source File**: `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowEdge.cs`

---

## Overview

`ControlFlowEdge.cs` defines the **edge representation** in the Control Flow Graph (CFG) for the Sharpy compiler. While the CFG uses `BasicBlock` objects as **nodes** to represent sequences of statements, `ControlFlowEdge` instances represent the **directed connections** between these blocks, describing how execution can flow from one block to another.

**Role in the Compiler Pipeline:**
```
Parser (AST) → Semantic Analysis → [CFG Construction] → Validation → Code Generation
                                          ↑
                                   ControlFlowEdge lives here
```

This file sits in the **Semantic Analysis** phase, specifically within the Control Flow Analysis subsystem. Edges are used to:
- Model conditional branches (if/else)
- Track loop back-edges and exits
- Represent exception handling paths
- Enable data flow analysis (reaching definitions, liveness analysis)
- Detect unreachable code and validate return paths

---

## Class/Type Structure

### 1. `EdgeKind` Enum

The `EdgeKind` enumeration categorizes the different types of control flow transitions. This taxonomy is critical for CFG analysis algorithms.

```csharp
public enum EdgeKind
{
    Unconditional,      // Straight-line execution
    ConditionalTrue,    // If condition is true
    ConditionalFalse,   // If condition is false
    SwitchCase,         // Specific case in match/switch
    SwitchDefault,      // Default/else case
    Exception,          // Try → exception handler
    Finally,            // Try/handler → finally block
    LoopBack,           // Loop condition → loop body
    LoopExit            // Break or natural loop exit
}
```

**Design Decision**: The enum provides semantic information beyond just "from/to" connections. For example:
- **Loop detection**: `LoopBack` edges immediately identify cycles
- **Exception analysis**: `Exception` and `Finally` edges enable modeling of exceptional control flow
- **Dominance analysis**: `ConditionalTrue`/`ConditionalFalse` pairs help identify which blocks dominate others

### 2. `ControlFlowEdge` Record

```csharp
public record ControlFlowEdge(
    BasicBlock From,
    BasicBlock To,
    EdgeKind Kind
)
{
    public Parser.Ast.Expression? Condition { get; init; }
    public override string ToString() => $"{From} --{Kind}--> {To}";
}
```

**Key Properties:**
- **`From`**: Source basic block (src/Sharpy.Compiler/Analysis/ControlFlow/BasicBlock.cs:17)
- **`To`**: Destination basic block
- **`Kind`**: Type of transition (from `EdgeKind` enum)
- **`Condition`** (optional): For conditional edges, stores the AST expression being evaluated

**Why a Record?**
- **Structural equality**: Two edges with the same `From`, `To`, and `Kind` are considered equal
- **Immutability**: Once created, edges don't change (CFG is built then frozen)
- **Value semantics**: Edges are descriptions of flow, not stateful objects

---

## Key Methods and Usage

### `ToString()` Method

```csharp
public override string ToString() => $"{From} --{Kind}--> {To}";
```

**Purpose**: Human-readable representation for debugging and visualization.

**Example Output**:
```
BB0:entry --Unconditional--> BB1:loop_condition
BB1:loop_condition --ConditionalTrue--> BB2:loop_body
BB1:loop_condition --ConditionalFalse--> BB3:exit
BB2:loop_body --LoopBack--> BB1:loop_condition
```

This format makes it easy to trace execution paths when debugging CFG construction or analysis passes.

---

## Dependencies and Relationships

### Upstream Dependencies

1. **`BasicBlock.cs`** (src/Sharpy.Compiler/Analysis/ControlFlow/BasicBlock.cs:17)
   - Edges connect `BasicBlock` instances
   - Blocks maintain their own `Predecessors` and `Successors` lists
   - **Important**: Edges are typically not stored separately; instead, the successor/predecessor relationships in `BasicBlock` implicitly define the edge set

2. **`Parser.Ast.Expression`** (src/Sharpy.Compiler/Parser/Ast/*)
   - The `Condition` property references AST nodes
   - Used for conditional edges to track what boolean expression controls the branch

### Downstream Usage

1. **`ControlFlowGraphBuilder.cs`** (src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs)
   - Constructs edges while building the CFG from AST
   - Sets appropriate `EdgeKind` based on statement types (`if`, `while`, `for`, `try`, etc.)

2. **`ControlFlowGraph.cs`** (src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraph.cs:13)
   - Doesn't store edges directly, but algorithms traverse them via `block.Successors`
   - Methods like `GetReversePostOrder()` and `FindUnreachableBlocks()` walk edges implicitly

3. **`BlockTerminator.cs`** (src/Sharpy.Compiler/Analysis/ControlFlow/BlockTerminator.cs:14)
   - Terminators define how a block exits, which determines edge kinds:
     - `BranchTerminator` → `Unconditional` edge
     - `ConditionalBranchTerminator` → `ConditionalTrue` and `ConditionalFalse` edges
     - `SwitchTerminator` → `SwitchCase` and `SwitchDefault` edges
     - `BreakTerminator` → `LoopExit` edge

---

## Patterns and Design Decisions

### Pattern 1: Explicit Edge Kinds vs. Implicit Graph Structure

**Decision**: Use an explicit `EdgeKind` enum rather than inferring edge types from terminators.

**Rationale**:
- **Readability**: `ConditionalTrue` is clearer than checking `if (edge.From.Terminator is ConditionalBranchTerminator t && edge.To == t.TrueTarget)`
- **Efficiency**: Analysis passes can filter edges by kind without casting terminators
- **Extensibility**: New edge kinds (e.g., `YieldReturn` for generators) can be added without changing terminator hierarchy

### Pattern 2: Optional Condition Expression

**Decision**: Store the condition expression on the edge rather than forcing analysis passes to look it up.

**Use Case**: Data flow analysis can directly access the condition to determine constraints:
```csharp
if (edge.Kind == EdgeKind.ConditionalTrue && edge.Condition is BinaryExpression { Op: "==" })
{
    // Constraint: variable equals value on this path
}
```

**Trade-off**: Slight memory overhead vs. convenience (chosen in favor of convenience).

### Pattern 3: Structural Equality via Record

**Why Records Matter**:
- Enables set operations: `HashSet<ControlFlowEdge>` works correctly
- Simplifies testing: `edge1 == edge2` compares values, not references
- Immutability enforced: No risk of modifying edges after CFG construction

---

## Debugging Tips

### Visualizing Edges

When debugging CFG issues, the `ToString()` method is your friend. Example debug code:

```csharp
foreach (var block in cfg.Blocks)
{
    Console.WriteLine($"Block {block}:");
    foreach (var successor in block.Successors)
    {
        var kind = DetermineEdgeKind(block, successor); // You'd need to implement this
        Console.WriteLine($"  → {successor} ({kind})");
    }
}
```

**Pro Tip**: Look for these common issues:
- **Missing `LoopBack` edges**: Infinite loops won't be detected
- **Orphaned blocks**: Blocks with no incoming edges (except entry)
- **Dead `ConditionalFalse` edges**: May indicate unreachable else branches

### Edge Kind Validation

If you suspect incorrect edge kinds, validate against the terminator:

```csharp
void ValidateEdge(ControlFlowEdge edge)
{
    var terminator = edge.From.Terminator;
    switch (edge.Kind)
    {
        case EdgeKind.ConditionalTrue:
        case EdgeKind.ConditionalFalse:
            Debug.Assert(terminator is ConditionalBranchTerminator);
            break;
        case EdgeKind.SwitchCase:
        case EdgeKind.SwitchDefault:
            Debug.Assert(terminator is SwitchTerminator);
            break;
        // ... etc.
    }
}
```

### Condition Expression Gotchas

For conditional edges, the `Condition` property should match the terminator:

```csharp
if (edge.Kind == EdgeKind.ConditionalTrue && edge.From.Terminator is ConditionalBranchTerminator cbt)
{
    Debug.Assert(edge.Condition == cbt.Condition);
}
```

**Warning**: If `Condition` is null on a conditional edge, it's a bug in the CFG builder.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Control Flow Constructs**
   - Example: If Sharpy adds `match` expressions with guards, you might need a new `EdgeKind.MatchGuard`
   - Always coordinate with changes to `BlockTerminator.cs`

2. **Enhanced Analysis Features**
   - Example: Adding a `Probability` field for branch prediction hints
   - Example: Adding `IsCritical` flag for critical path analysis

3. **CFG Visualization Improvements**
   - Enhance `ToString()` to emit DOT format or Mermaid diagrams
   - Add metadata for graphical layout (e.g., "back edge" styling)

### What NOT to Change

- **Don't make edges mutable**: The record structure is intentional
- **Don't remove edge kinds**: Other parts of the codebase may depend on them
- **Don't store complex state**: Edges should be lightweight; put analysis results in separate data structures

### Testing Considerations

When modifying edge logic, ensure these scenarios work:

```csharp
// Test 1: Nested conditionals produce correct edge kinds
if (x > 0)
{
    if (y > 0)
        return 1;
}
// Should have: ConditionalTrue, ConditionalFalse at both levels

// Test 2: Loop edges are correctly classified
while (condition)
{
    if (x)
        break;
    if (y)
        continue;
}
// Should have: LoopBack, LoopExit, ConditionalTrue/False

// Test 3: Exception edges
try {
    riskyCall();
} catch (Exception e) {
    handle();
} finally {
    cleanup();
}
// Should have: Exception edge to handler, Finally edges from try and catch
```

Run integration tests with: `dotnet test --filter "FullyQualifiedName~ControlFlow"`

---

## Cross-References

This file is tightly coupled with other ControlFlow components:

- **[BasicBlock.cs](BasicBlock.md)** - Nodes that edges connect
- **[BlockTerminator.cs](BlockTerminator.md)** - Defines how blocks exit (determines edge kinds)
- **[ControlFlowGraph.cs](ControlFlowGraph.md)** - Container for blocks; traversal algorithms use edges
- **[ControlFlowGraphBuilder.cs](ControlFlowGraphBuilder.md)** - Creates edges during CFG construction

**Note**: While `ControlFlowEdge` is defined as a standalone type, edges are typically not stored in a separate collection. Instead, they're implicitly represented by the `BasicBlock.Successors` and `BasicBlock.Predecessors` lists. The `ControlFlowEdge` record is primarily used during construction and in analysis algorithms that need to associate metadata with specific transitions.

---

## Advanced Topics

### Implicit vs. Explicit Edge Storage

**Current Design**: Edges are implicitly represented via `BasicBlock.Successors/Predecessors`. The `ControlFlowEdge` record exists for:
- Type-safe edge construction in the builder
- Analysis passes that need to tag edges with information
- Algorithms that process edges as first-class entities

**Alternative Design**: Store `List<ControlFlowEdge>` on `ControlFlowGraph`. This would:
- **Pros**: Allow attaching metadata (probabilities, frequencies) to edges
- **Cons**: Duplicate information (edges and successor lists must stay in sync)

The current design favors simplicity and memory efficiency.

### Loop Classification

The `LoopBack` edge kind enables efficient loop detection:

```csharp
IEnumerable<BasicBlock> FindLoopHeaders(ControlFlowGraph cfg)
{
    // A loop header is a block that is the target of a LoopBack edge
    return cfg.Blocks.Where(b => b.Predecessors.Any(pred =>
        GetEdgeKind(pred, b) == EdgeKind.LoopBack));
}
```

**Note**: Natural loops can be identified by finding back-edges (edges to dominators) and computing the loop body as all blocks that can reach the back-edge source without going through the header.

### Exception Edge Semantics

`Exception` and `Finally` edges model complex exception control flow:

```python
try:
    stmt1
    stmt2  # May throw
except ValueError:
    handler
finally:
    cleanup
```

**CFG Structure**:
- `BB_try_body` has normal successors AND exception edges to `BB_except_ValueError`
- Both `BB_try_body` and `BB_except_ValueError` have `Finally` edges to `BB_finally`
- `BB_finally` has unconditional edge to next statement

**Subtlety**: Every statement in a `try` block implicitly has an exception edge. The CFG may model this conservatively (one edge from try entry) or precisely (edges from each statement).

---

## Summary

`ControlFlowEdge.cs` is a small but critical file that provides the **vocabulary** for describing control flow transitions in the Sharpy compiler. Its simple record structure belies its importance:

- **`EdgeKind`** enables semantic classification of transitions
- **`Condition`** property supports constraint-based analysis
- **Immutable record** design ensures CFG integrity

When working with CFG analysis or construction, think of edges as **labeled arrows** that tell you not just "where to go next" but also "why" execution follows that path. This semantic richness is what makes advanced analyses (dead code elimination, constant propagation, null safety checks) possible.
