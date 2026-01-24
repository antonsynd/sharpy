# Walkthrough: ControlFlowAnalysis.cs

**Source File**: `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs`

---

## Overview

`ControlFlowAnalysis.cs` provides **high-level analysis utilities** that operate on Control Flow Graphs (CFGs) to detect common control flow issues and extract semantic information. While `ControlFlowGraph.cs` and `ControlFlowGraphBuilder.cs` handle the construction and representation of CFGs, this file provides the **semantic validation layer** that ensures code is well-formed.

**Role in the Compiler Pipeline:**
```
Parser (AST) → CFG Builder → CFG → [ControlFlowAnalysis] → Semantic Validation → Code Generation
                                           ↑ YOU ARE HERE
```

This file sits in the **Semantic Analysis** phase, using the CFG data structure to detect issues like:
- Missing return statements
- Unreachable code
- Invalid use of `break`/`continue` outside loops
- Async/await state machine regions (future feature)

Think of it as a "linter" that runs on the CFG rather than the AST, enabling flow-sensitive analysis.

---

## Class/Type Structure

### Main Class: `ControlFlowAnalysis`

A **static utility class** containing analysis methods. It has no state—all methods take a `ControlFlowGraph` as input and return analysis results.

**Why static?** These are pure algorithmic functions that don't need instance state. They're designed to be called by the semantic analysis phase after CFG construction.

### Supporting Records (Data Types)

Three immutable record types represent analysis results:

#### 1. `UnreachableCodeInfo`
```csharp
public record UnreachableCodeInfo(
    BasicBlock Block,
    Statement FirstUnreachableStatement
);
```
Represents code that can never execute (e.g., statements after `return`).

#### 2. `ControlFlowError`
```csharp
public record ControlFlowError(
    string Message,
    Statement Statement
);
```
Represents a control flow violation (e.g., `break` outside a loop).

#### 3. `AsyncStateRegion`
```csharp
public record AsyncStateRegion(
    int StateId,
    ImmutableArray<BasicBlock> Blocks,
    Expression? AwaitExpression
);
```
Represents a region of code for async state machine generation (future feature for v0.2.x).

---

## Key Functions/Methods

### 1. `FindMissingReturnPaths`

**Purpose:** Detect functions that don't return a value on all code paths.

```csharp
public static ImmutableArray<BasicBlock> FindMissingReturnPaths(ControlFlowGraph cfg)
```

**How It Works:**

1. **Reachability Pass:** Uses breadth-first search (BFS) from the entry block to find all reachable blocks:
   ```csharp
   var worklist = new Queue<BasicBlock>();
   worklist.Enqueue(cfg.Entry);
   while (worklist.Count > 0) {
       var block = worklist.Dequeue();
       if (!reachable.Add(block)) continue;
       foreach (var succ in block.Successors)
           worklist.Enqueue(succ);
   }
   ```

2. **Missing Return Detection:** For each reachable block (excluding entry/exit):
   - Check if it branches directly to the exit block
   - Check if it lacks a `ReturnStatement` in its statements list
   - If both conditions are true → missing return path

**Key Parameters:**
- `cfg`: The control flow graph to analyze

**Return Value:**
- `ImmutableArray<BasicBlock>`: Blocks that reach exit without returning

**Upstream Connection:** Called by semantic analysis after CFG construction.

**Downstream Connection:** Results are converted to compiler diagnostics/errors.

**Example Scenario:**
```python
def foo(x: int) -> int:
    if x > 0:
        return 1
    # Missing else branch - this block falls through to exit!
```
This method would identify the block containing the implicit fall-through path.

---

### 2. `FindUnreachableCode`

**Purpose:** Detect code that can never be executed.

```csharp
public static ImmutableArray<UnreachableCodeInfo> FindUnreachableCode(ControlFlowGraph cfg)
```

**How It Works:**

1. Delegates to `cfg.FindUnreachableBlocks()` (in `ControlFlowGraph.cs`) to get all blocks not reachable from entry
2. For each unreachable block with statements, creates an `UnreachableCodeInfo` record pointing to the first unreachable statement

**Key Insight:**
The method focuses on **non-empty** unreachable blocks. Empty synthetic blocks (like loop exits) are ignored because they're internal CFG machinery.

**Example Scenario:**
```python
def bar():
    return 42
    print("This is unreachable!")  # ← Detected here
```

**Why CFG-based?** Unreachability is a flow-sensitive property. You can't detect it from AST alone—you need to trace execution paths.

---

### 3. `ValidateLoopControlFlow`

**Purpose:** Ensure `break` and `continue` statements are only used inside loops.

```csharp
public static ImmutableArray<ControlFlowError> ValidateLoopControlFlow(ControlFlowGraph cfg)
```

**How It Works:**

1. **Iterate all blocks** in the CFG
2. For blocks with `BreakTerminator`:
   - Check if the target block is a loop exit (via label inspection)
   - If not, record an error
3. For blocks with `ContinueTerminator`:
   - Check if the target block is a loop header (via label inspection)
   - If not, record an error

**Label-Based Detection:**
```csharp
private static bool IsLoopExitBlock(BasicBlock? block, ControlFlowGraph cfg)
{
    return block != null &&
           (block.Label.Contains("while_exit") ||
            block.Label.Contains("for_exit"));
}
```

**Why Labels?** During CFG construction (`ControlFlowGraphBuilder.cs`), loop-related blocks are labeled with semantic names like `"while_header"`, `"for_exit_0"`. This provides a simple metadata channel without needing extra data structures.

**Upstream Connection:** The CFG builder sets terminators with `null` targets when `break`/`continue` appears outside a loop.

**Downstream Connection:** Errors are reported as semantic diagnostics.

---

### 4. `IdentifyAsyncRegions`

**Purpose:** Partition a function into state machine regions for async/await transformation.

```csharp
public static ImmutableArray<AsyncStateRegion> IdentifyAsyncRegions(ControlFlowGraph cfg)
```

**Current Status:** **Placeholder implementation** for future v0.2.x async/await support.

**Design Intent:**

1. Find all blocks containing `await` expressions (via `BasicBlock.ContainsAwait` flag)
2. Partition the CFG into regions between await points
3. Each region becomes a state in the generated async state machine

**Current Implementation:**
- If no awaits: returns a single region containing all blocks
- If awaits exist: each await block becomes its own state (naive placeholder)

**Future Work:**
The TODO comment indicates this needs full implementation. The correct algorithm should:
- Trace forward/backward from await points to determine region boundaries
- Extract await expressions for state machine variable lifting
- Handle control flow merges (multiple paths reaching same await)

**Upstream Connection:** `BasicBlock.ContainsAwait` is set during CFG construction by scanning for `AwaitExpression` AST nodes.

**Downstream Connection:** Will feed into async state machine code generation in `RoslynEmitter`.

---

## Dependencies

### Internal Dependencies

| File | Purpose |
|------|---------|
| `ControlFlowGraph.cs` | The main CFG data structure; provides `FindUnreachableBlocks()` method |
| `BasicBlock.cs` | Represents individual blocks; provides `Statements`, `Successors`, `Terminator` |
| `BlockTerminator.cs` | Defines terminator types (`BreakTerminator`, `ContinueTerminator`, etc.) |
| `ControlFlowGraphBuilder.cs` | Constructs CFGs from AST; sets block labels and terminator targets |

### External Dependencies

| Namespace | Purpose |
|-----------|---------|
| `Sharpy.Compiler.Parser.Ast` | AST node types (`Statement`, `Expression`, `ReturnStatement`) |
| `System.Collections.Immutable` | Immutable collections for thread-safe results |

### Dependency Graph
```
ControlFlowAnalysis.cs
    ↓ uses
ControlFlowGraph.cs (FindUnreachableBlocks, Blocks, Entry, Exit)
    ↓ contains
BasicBlock.cs (Label, Terminator, Statements, Successors)
    ↓ uses
BlockTerminator.cs (BreakTerminator, ContinueTerminator, etc.)
```

---

## Patterns and Design Decisions

### 1. **Static Utility Class Pattern**

All methods are static because they're pure functions with no mutable state. This is idiomatic C# for algorithmic utilities.

**Alternative:** Could have been instance methods on `ControlFlowGraph`, but that would bloat the core data structure.

### 2. **Immutable Results via Records**

Result types (`UnreachableCodeInfo`, `ControlFlowError`, `AsyncStateRegion`) are immutable records. This ensures analysis results can't be accidentally modified and enables structural equality.

### 3. **Label-Based Metadata**

Loop validation relies on string labels (`"while_exit"`, `"for_header"`) embedded in block names.

**Pros:**
- Simple to implement
- No additional data structures needed
- Easy to debug (blocks have human-readable names)

**Cons:**
- Fragile to label name changes
- Uses string matching (`.Contains()`) which could have false positives

**Alternative:** Could use a `BasicBlock.LoopRole` enum, but labels serve double duty for debugging.

### 4. **Reachability Analysis Pattern**

Both `FindMissingReturnPaths` and `FindUnreachableCode` use **worklist algorithms**:
- Maintain a `Queue<BasicBlock>` of blocks to process
- Maintain a `HashSet<BasicBlock>` of visited blocks
- Process successors until worklist is empty

This is the standard BFS algorithm for graph traversal.

### 5. **Separation of Concerns**

Notice that `FindUnreachableCode` delegates reachability computation to `ControlFlowGraph.FindUnreachableBlocks()`. This follows Single Responsibility Principle:
- `ControlFlowGraph`: Owns graph traversal algorithms
- `ControlFlowAnalysis`: Owns semantic interpretation of results

---

## Debugging Tips

### 1. **Visualize the CFG**

If analysis results seem wrong, dump the CFG to see what the graph actually looks like:
```csharp
foreach (var block in cfg.Blocks)
{
    Console.WriteLine($"{block} (Terminator: {block.Terminator})");
    foreach (var stmt in block.Statements)
        Console.WriteLine($"  {stmt}");
}
```

### 2. **Check Block Labels**

Loop validation relies on labels. If `break`/`continue` errors are incorrect, inspect block labels:
```csharp
Console.WriteLine($"Break target: {breakTerminator.Target?.Label}");
```

Look for expected patterns: `while_header`, `for_exit`, etc.

### 3. **Inspect Entry/Exit Connections**

Missing return paths are about entry-to-exit connectivity. Check:
```csharp
Console.WriteLine($"Entry successors: {string.Join(", ", cfg.Entry.Successors)}");
Console.WriteLine($"Exit predecessors: {string.Join(", ", cfg.Exit.Predecessors)}");
```

### 4. **Use Reverse Post-Order**

When debugging data flow, iterate blocks in RPO:
```csharp
foreach (var block in cfg.GetReversePostOrder())
{
    // Process blocks in execution order
}
```

### 5. **Test with Minimal Cases**

Reproduce issues with minimal functions:
```python
# Minimal missing return case
def test() -> int:
    pass

# Minimal unreachable code case
def test2():
    return
    x = 1
```

### 6. **Check CFG Builder Output**

If analysis is wrong, the issue might be in CFG construction. Review `ControlFlowGraphBuilder.cs` to ensure it's creating correct blocks and terminators.

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made?

#### 1. **New Analysis Methods** (Common)
Add methods for new semantic checks:
```csharp
public static ImmutableArray<TailCallInfo> FindTailCalls(ControlFlowGraph cfg)
{
    // Analyze blocks for tail call optimization opportunities
}
```

**Pattern to Follow:**
- Static method taking `ControlFlowGraph`
- Return immutable result type
- Use worklist algorithms for graph traversal
- Avoid modifying the CFG itself

#### 2. **Improve Loop Detection** (Medium Priority)
Current implementation uses string label matching, which is fragile. Consider:
- Add `LoopContext` metadata to `BasicBlock`
- Replace `IsLoopExitBlock` with metadata checks
- Coordinate with `ControlFlowGraphBuilder.cs` changes

#### 3. **Complete Async Region Analysis** (Future Work)
The `IdentifyAsyncRegions` method is a placeholder. Full implementation needs:
- Dominator tree analysis to find region boundaries
- Await expression extraction from `BasicBlock.Statements`
- Proper handling of control flow merges

**Resources Needed:**
- Study async state machine transformation in Roslyn
- Review "Static Single Assignment" algorithms for region analysis

#### 4. **Add More Granular Unreachable Code Detection**
Currently reports the first unreachable statement in a block. Could improve:
- Detect unreachable code **within** a block (after `return` mid-block)
- Distinguish between different causes (after `return` vs. `raise` vs. in dead branch)

#### 5. **Performance Optimization** (Low Priority)
For large CFGs, optimize:
- Reuse reachability sets across multiple analyses
- Cache results in `ControlFlowGraph` if analyses are called multiple times

### Code Style Requirements

- **Immutable results:** Always use `ImmutableArray<T>` or records
- **Null safety:** Check `block != null` before dereferencing (especially for terminator targets)
- **Comments:** Add XML doc comments (`///`) for public methods
- **Error messages:** Keep them concise and user-friendly
- **Testing:** Add test cases in `Sharpy.Compiler.Tests` for any new analysis

### Testing Strategy

When adding new analysis methods:

1. **Unit tests** in `Sharpy.Compiler.Tests/Analysis/ControlFlow/`
2. **Integration tests** using `.spy` + `.error` file pairs in `TestFixtures/`
3. **Edge cases** to test:
   - Empty functions
   - Functions with no exit paths (infinite loops)
   - Nested loops/conditionals
   - Exception handling (try/except)

---

## Cross-References

### Related Documentation Files

- **[ControlFlowGraph.md](ControlFlowGraph.md)**: Core CFG data structure and graph algorithms
- **[BasicBlock.md](BasicBlock.md)**: Individual block representation
- **[BlockTerminator.md](BlockTerminator.md)**: Control flow terminator types
- **[ControlFlowGraphBuilder.md](ControlFlowGraphBuilder.md)**: How CFGs are constructed from AST

### Related Source Files

| File | Relationship |
|------|--------------|
| `ControlFlowGraph.cs` | Provides the CFG data structure analyzed by this file |
| `BasicBlock.cs` | Block representation used in analysis results |
| `BlockTerminator.cs` | Terminator types checked in validation |
| `ControlFlowGraphBuilder.cs` | Constructs CFGs; sets labels used by loop validation |
| `src/Sharpy.Compiler/Semantic/SemanticAnalysis.cs` | Calls these analysis methods to generate diagnostics |

### Upstream/Downstream Context

**Upstream (What Creates the Input):**
- `ControlFlowGraphBuilder.BuildGraph(FunctionDef)` creates the CFG passed to these methods

**Downstream (What Consumes the Output):**
- Semantic analysis converts `ControlFlowError` to compiler diagnostics
- `UnreachableCodeInfo` generates "unreachable code" warnings
- Missing return paths generate "not all paths return a value" errors

---

## Key Takeaways for Newcomers

1. **This file is the "semantic validator" for control flow** - it detects logic errors in CFGs.

2. **All methods are stateless algorithms** - they don't modify the CFG, just analyze it.

3. **Reachability is the core primitive** - most analyses boil down to "can we reach block X from block Y?"

4. **Label-based metadata is a pragmatic choice** - it keeps the core data structures simple while enabling semantic checks.

5. **Future async support is planned** - the `IdentifyAsyncRegions` method is a scaffold for v0.2.x work.

6. **When in doubt, trace execution paths** - control flow bugs are best understood by manually walking through the CFG.

---

**Next Steps:**
- Read [ControlFlowGraph.md](ControlFlowGraph.md) to understand the underlying data structure
- Read [ControlFlowGraphBuilder.md](ControlFlowGraphBuilder.md) to see how CFGs are constructed
- Try running the `/project:emit` command on a `.spy` file to visualize the CFG
