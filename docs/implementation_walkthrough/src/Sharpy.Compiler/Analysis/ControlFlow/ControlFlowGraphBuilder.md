# Walkthrough: ControlFlowGraphBuilder.cs

**Source File**: `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs`

---

## Overview

The `ControlFlowGraphBuilder` is responsible for transforming an Abstract Syntax Tree (AST) into a **Control Flow Graph (CFG)**—a directed graph representing all possible execution paths through a function or module. This is a critical component in the Sharpy compiler's semantic analysis phase.

**Key Characteristics:**
- **Input**: Immutable AST nodes (from the Parser phase)
- **Output**: A `ControlFlowGraph` object containing interconnected `BasicBlock` nodes
- **Design**: Builder pattern—stateful during construction, produces immutable output
- **Non-modifying**: Does NOT alter the AST; the CFG references AST nodes but is a separate data structure

**Position in Pipeline:**
```
Parser (AST) → ControlFlowGraphBuilder → ControlFlowGraph → Semantic Validators → Code Generation
```

The CFG is used by downstream validators to detect unreachable code, verify return paths, check break/continue validity, and analyze exception handling.

---

## Class Structure

### Main Fields

```csharp
private readonly List<BasicBlock> _blocks = new();
private BasicBlock _currentBlock = null!;
private BasicBlock _entry = null!;
private BasicBlock _exit = null!;
```

- **`_blocks`**: Accumulates all basic blocks created during construction
- **`_currentBlock`**: The block currently being built (receives new statements)
- **`_entry`**: Synthetic entry block (starting point of the graph)
- **`_exit`**: Synthetic exit block (all return paths converge here)

### Context Stacks

```csharp
private readonly Stack<LoopContext> _loopStack = new();
private readonly Stack<BasicBlock> _handlerStack = new();
```

- **`_loopStack`**: Tracks nested loops to resolve `break`/`continue` targets
- **`_handlerStack`**: Tracks exception handler blocks for validating bare `raise` statements

### LoopContext Record

```csharp
private record LoopContext(
    BasicBlock Header,        // Where continue jumps to
    BasicBlock Exit,          // Where break jumps to
    BasicBlock? ElseBlock     // Optional else block (runs if loop completes without break)
);
```

This record captures the necessary information for correctly wiring up `break` and `continue` statements within nested loops, including Python's loop-else semantics.

---

## Key Methods

### Build Methods

#### `Build(FunctionDef function)` and `Build(IReadOnlyList<Statement> statements)`

**Purpose**: Entry points for building a CFG from either a function definition or a sequence of module-level statements.

**Algorithm:**
1. Reset internal state (clear blocks, stacks)
2. Create synthetic `entry` and `exit` blocks
3. Create a `body_start` block and connect it to entry
4. Process all statements via `BuildStatements()`
5. If the final block doesn't have a terminator (no explicit return), connect it to exit
6. Return a new `ControlFlowGraph` object

**Example Flow:**
```
[entry] → [body_start] → [stmt1] → [stmt2] → ... → [exit]
```

**Key Detail**: If a block is already terminated (e.g., by a `return` statement), subsequent statements in the same sequence are skipped—they're unreachable and won't be added to the CFG.

---

### Statement Builders

#### `BuildStatement(Statement stmt)`

**Purpose**: Dispatcher that routes each statement type to its specialized builder method.

**Control Flow Statements** (create new blocks):
- `ReturnStatement` → `BuildReturn()`
- `IfStatement` → `BuildIf()`
- `WhileStatement` → `BuildWhile()`
- `ForStatement` → `BuildFor()`
- `BreakStatement` → `BuildBreak()`
- `ContinueStatement` → `BuildContinue()`
- `TryStatement` → `BuildTry()`
- `RaiseStatement` → `BuildRaise()`

**Non-Control Flow Statements** (just added to current block):
- Assignments, expressions, function/class definitions, imports

**Design Decision**: Definitions (`FunctionDef`, `ClassDef`, etc.) don't affect control flow—they're declarative. They're excluded from the CFG entirely (lines 163-172).

---

#### `BuildReturn(ReturnStatement stmt)`

**What it does:**
1. Adds the return statement to the current block
2. Connects the current block to the synthetic `exit` block
3. Sets a `ReturnTerminator` (includes the return value expression)

**Why connect to exit?**: All return paths in a CFG converge at a single exit node. This simplifies analysis (e.g., checking if all paths return a value).

```csharp
_currentBlock.Terminator = new ReturnTerminator(stmt.Value)
{
    SourceStatement = stmt
};
```

---

#### `BuildIf(IfStatement stmt)`

**Challenge**: Handle `if`/`elif`/`else` chains with multiple branches merging at a common point.

**Algorithm:**
1. Create a `merge` block where all branches reconverge
2. Collect all branches (if + elifs + optional else) into a list
3. For each branch:
   - If it has a condition: create a `bodyBlock` and a `falseTarget` (either next elif, else, or merge)
   - Set a `ConditionalBranchTerminator(condition, bodyBlock, falseTarget)`
   - Build the body statements
   - Connect body to merge (if not already terminated)
4. Set `_currentBlock` to the merge block

**Diagram:**
```
       [condition1]
        /        \
      true      false
       |          |
   [if_body]  [condition2]
       |       /        \
       |     true      false
       |      |          |
       |  [elif_body]  [else_body]
       |      |          |
       +------+----------+
              |
          [merge]
```

**Key Insight**: The false target of each condition is the *next* condition in the chain (or merge if it's the last and there's no else).

---

#### `BuildWhile(WhileStatement stmt)`

**Challenge**: Model loops with back edges, handle break/continue, and Python's loop-else clause.

**Algorithm:**
1. Create `header`, `body`, and `exit` blocks
2. If there's an else clause, create an `else` block
   - Normal loop exit (condition becomes false) → else block
   - `break` statements → directly to exit block (bypassing else)
3. Connect current block to header
4. Set header terminator: `ConditionalBranchTerminator(condition, body, loopExitTarget)`
5. Push a `LoopContext` onto the stack (for break/continue resolution)
6. Build body statements
7. Connect body back to header (creating the loop)
8. Pop loop context
9. Build else clause if present and connect to exit

**Diagram (with else):**
```
    [header] ──false──> [else] ──> [exit]
       |                              ^
      true                            |
       |                         break jumps
       v                              |
    [body] ────────────────────────────
       |
       └─> (back to header)
```

**Loop-Else Semantics**: The else block runs only if the loop exits normally (condition false), not if it's exited via `break`.

---

#### `BuildFor(ForStatement stmt)`

**Approach**: Similar to `BuildWhile`, but the condition is simplified to the iterator expression.

**Key Simplification** (line 478-479):
```csharp
// Note: We use stmt.Iterator as the condition expression.
// This is a simplification - actual iteration semantics are handled at code gen.
```

The CFG doesn't model the full iteration protocol (`__iter__`, `__next__`, `StopIteration`). It treats the iterator as a placeholder condition. Code generation handles the actual iteration mechanics.

**Why?** The CFG is about control flow structure, not runtime behavior. The important structure is: check → body → check → ... → exit.

---

#### `BuildBreak(BreakStatement stmt)` and `BuildContinue(ContinueStatement stmt)`

**Purpose**: Connect break/continue to the appropriate loop blocks.

**Algorithm:**
1. Check if `_loopStack` is empty
   - If yes: create a terminator with `null` target (error case, detected later by validators)
2. Peek the current `LoopContext`
3. Connect to the appropriate target:
   - `break` → `loop.Exit`
   - `continue` → `loop.Header`
4. Set terminator and end current block

**Error Handling**: Rather than throwing errors during CFG construction, invalid break/continue create terminators with null targets. This allows the CFG to be fully built before validation, enabling better error reporting (e.g., showing multiple errors at once).

---

#### `BuildTry(TryStatement stmt)`

**Most Complex Builder**: Handles try/except/else/finally with multiple control flow paths.

**Structure:**
```
[try_body] ──normal──> [try_else] ──> [finally] ──> [merge]
    |
    └──exception──> [except_handler1] ──> [finally] ──> [merge]
                    [except_handler2] ──> [finally] ──> [merge]
```

**Algorithm:**
1. Create `try_body` block and build try statements
2. Create blocks for each exception handler
   - **Simplified exception edges**: All handlers are connected from the try block
   - Real exception dispatch (which handler catches what) is handled at runtime
3. Build else clause (only runs if try completes without exception)
   - Connected only from try block's normal exit
4. Build finally clause (always runs)
   - Connected from try (or else) normal path AND all handler paths
5. All paths converge at `merge` block

**Simplification Note** (lines 547-549): The CFG doesn't model which specific exceptions each handler catches. This is acceptable because:
- CFG analysis focuses on reachability and control flow structure
- Exception type matching happens at runtime
- This keeps the CFG tractable and focused

---

#### `BuildRaise(RaiseStatement stmt)`

**Two Cases:**

1. **Bare `raise`** (re-raise current exception):
   - Only valid inside an exception handler
   - Creates a `RethrowTerminator`
   - Validation happens later via `_handlerStack`

2. **`raise Exception(...)`**:
   - Creates a `ThrowTerminator` with the exception expression

**Design Note** (lines 217-218): Exception flow to handlers isn't modeled in edges. Throw terminates the block, but doesn't connect to catch blocks. This could be enhanced in future versions for more precise exception flow analysis.

---

### Helper Methods

#### `CreateBlock(string label)`

Creates a new `BasicBlock` with an optional human-readable label (useful for debugging and visualization).

**Labels Convey Intent:**
- `"entry"`, `"exit"` - synthetic blocks
- `"if_then"`, `"if_else"` - conditional branches
- `"while_header"`, `"for_body"` - loop structures
- `"try_body"`, `"except_Exception"` - exception handling

#### `Connect(BasicBlock from, BasicBlock to)`

Bidirectional edge creation:
```csharp
from.AddSuccessor(to);
to.AddPredecessor(from);
```

This maintains both forward (successors) and backward (predecessors) edges, enabling both forward and backward data flow analysis.

---

## Dependencies

### Internal Dependencies

- **`Sharpy.Compiler.Parser.Ast`**: All AST node types (`Statement`, `Expression`, `FunctionDef`, etc.)
- **`BasicBlock`**: Mutable container for statements, successors, predecessors
- **`ControlFlowGraph`**: Immutable output structure
- **`BlockTerminator`** hierarchy: Describes how each block exits

### Downstream Consumers

The produced `ControlFlowGraph` is consumed by:
- **`ControlFlowValidatorV3`**: Validates unreachable code, break/continue placement, return paths
- **Definite assignment analysis**: Checks that variables are assigned before use
- **Async analysis**: Validates `await` usage
- **Code optimization**: Dead code elimination, loop analysis

---

## Patterns and Design Decisions

### 1. Builder Pattern

The class is a classic builder with mutable state during construction, producing an immutable output. This separates the complex construction logic from the final immutable CFG representation.

### 2. Immutable AST Principle

**Critical Design Rule** (lines 9-11):
```csharp
/// It does NOT modify the AST. The resulting CFG references AST nodes
/// but is a separate data structure.
```

The AST is the source of truth. The CFG is a derived analysis artifact. This separation allows multiple analyses to run on the same AST without interference.

### 3. Synthetic Entry/Exit Blocks

Every CFG has exactly one entry and one exit, even if the source code has multiple return statements. Benefits:
- Simplifies analysis algorithms (single starting/ending point)
- Natural place to attach function prologue/epilogue information
- Makes dominance and reachability calculations straightforward

### 4. Null Targets for Errors

Instead of throwing exceptions during construction, invalid break/continue statements create terminators with null targets. This enables:
- Complete CFG construction even with errors
- Better error reporting (collect multiple errors)
- Cleaner separation between construction and validation

### 5. Stack-Based Context Tracking

Using `Stack<LoopContext>` and `Stack<BasicBlock>` to track nesting:
- Natural for recursive descent over nested structures
- Push on entry, pop on exit
- Peek to find the current context

---

## Debugging Tips

### Visualizing the CFG

When debugging, print the CFG structure:
```csharp
foreach (var block in cfg.Blocks)
{
    Console.WriteLine($"{block} -> [{string.Join(", ", block.Successors)}]");
    Console.WriteLine($"  Terminator: {block.Terminator}");
}
```

### Common Issues

**1. Missing Merge Block Connections**

If branches don't reconverge correctly, check that:
- Each branch that doesn't terminate explicitly connects to the merge block
- The terminator is set before moving to the next branch

**2. Loop Context Stack Imbalance**

If break/continue resolve incorrectly:
- Ensure push/pop pairs are balanced (use try/finally if needed)
- Check that break/continue peek before the pop happens

**3. Unreachable Code Detection**

If statements appear to be missing from the CFG:
- Check if a prior statement terminated the block (lines 115-118)
- Once a block is terminated, subsequent statements are skipped

**4. Handler Stack Issues**

For bare `raise` validation:
- Verify `_handlerStack.Push()` happens before building handler body
- Verify `_handlerStack.Pop()` happens after

### Useful Breakpoints

- **Line 121**: Entry to `BuildStatement` (see which statement is being processed)
- **Line 116**: Check if current block is terminated (explains missing statements)
- **Line 225**: Break statement processing (check stack state)
- **Line 361**: Conditional branch creation (verify true/false targets)

---

## Contribution Guidelines

### What Changes Might Be Made

**1. New Control Flow Constructs**

When adding new statement types (e.g., `match` statement, `with` statement):
- Add a case in `BuildStatement` (line 121)
- Implement a dedicated `BuildXXX()` method
- Handle terminators and block connections appropriately
- Update related tests in `ControlFlowTests`

**2. Enhanced Exception Flow Modeling**

Currently, exception edges are simplified (lines 217-218). Enhancements could include:
- Connect throw terminators to reachable exception handlers
- Model exception type matching
- Track exception propagation through call graphs

**3. Loop Optimization Metadata**

Add fields to `LoopContext` or `BasicBlock` to support:
- Loop invariant detection
- Induction variable identification
- Trip count estimation

**4. Async/Await Support**

The `ContainsAwait` flag exists on `BasicBlock` but isn't set during construction. Enhancements:
- Scan statements for `AwaitExpression` during `AddStatement`
- Mark blocks containing await
- Enable async control flow validation

### Testing Strategy

When modifying this file:
1. **Unit tests**: Test individual builder methods in isolation
2. **Integration tests**: Use file-based tests (`.spy` + `.cfg.expected`)
3. **Edge cases**: Empty bodies, nested loops, complex try/except/finally
4. **Error cases**: Break/continue outside loops, bare raise outside handlers

### Code Style Conventions

- **Labels**: Use descriptive, lowercase-with-underscores (e.g., `"while_header"`)
- **Comments**: Explain *why*, not *what* (the code shows what)
- **Error handling**: Prefer graceful degradation (null targets) over exceptions during construction
- **Simplification**: Document where the CFG simplifies actual runtime behavior (e.g., iteration protocol)

---

## Cross-References

### Related Files

- **[BasicBlock.cs](BasicBlock.md)** - Definition of basic blocks (nodes in the CFG)
- **[ControlFlowGraph.cs](ControlFlowGraph.md)** - The output structure with analysis utilities
- **[BlockTerminator.cs](BlockTerminator.md)** - All terminator types (how blocks exit)
- **`ControlFlowValidatorV3.cs`** - Consumes the CFG for semantic validation
- **`Sharpy.Compiler.Parser.Ast` namespace** - All AST node definitions

### External Documentation

- **Language Specification**: `docs/language_specification/control_flow.md` - Defines Python control flow semantics
- **Architecture Guide**: `.github/copilot-instructions.md` - Overall compiler architecture

---

## Summary

The `ControlFlowGraphBuilder` is a critical bridge between parsing and semantic analysis. It transforms the tree-structured AST into a graph-structured CFG, making control flow explicit and analyzable. Key takeaways:

- **Stateful builder** producing **immutable output**
- **Non-modifying** of the input AST
- **Stack-based** context tracking for nested structures
- **Graceful handling** of errors (null targets for validation later)
- **Simplified modeling** of complex runtime semantics (iteration, exceptions)

Understanding this file is essential for working on:
- Control flow validation
- Data flow analysis
- Code optimization
- New language features involving control flow

When in doubt, remember: the CFG doesn't need to model *everything* about runtime execution—just enough to enable the analyses that depend on it.
