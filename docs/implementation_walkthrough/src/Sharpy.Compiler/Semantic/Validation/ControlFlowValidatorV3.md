# Walkthrough: ControlFlowValidatorV3.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidatorV3.cs`

---

## 1. Overview

`ControlFlowValidatorV3` is a semantic validator that uses **Control Flow Graph (CFG)** analysis to perform sophisticated control flow validation. It represents a more accurate and robust approach compared to the older V2 validator, which relied on simple AST traversal.

### Role in the Compiler Pipeline

```
Parser → AST → Semantic Analysis → [ControlFlowValidatorV3] → CodeGen
                      ↓
              ValidationPipeline
         (runs at Order=400, after type checking)
```

This validator runs as part of the **ValidationPipeline** during semantic analysis, after:
- Name resolution (Order ~100)
- Type resolution (Order ~200)
- Type checking (Order ~300)

### What It Does

The validator performs three critical checks:

1. **Unreachable Code Detection**: Finds code that can never be executed (e.g., statements after `return`)
2. **Missing Return Path Detection**: Ensures functions with return types return values on all code paths
3. **Loop Control Flow Validation**: Verifies `break`/`continue` statements only appear inside loops

### Why CFG-Based Analysis?

Traditional AST-walking validators struggle with complex control flow like:
```python
def complex_flow(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0
    # AST walker might not realize this is unreachable
    print("This is unreachable!")
```

CFG-based analysis explicitly models all possible execution paths, making it mathematically precise.

---

## 2. Class/Type Structure

### Class Declaration

```csharp
public class ControlFlowValidatorV3 : SemanticValidatorBase
```

**Inheritance**: Inherits from `SemanticValidatorBase`, which provides:
- Common error/warning reporting methods (`AddError`, `AddWarning`)
- Interface compliance with `ISemanticValidator`

### Key Properties

```csharp
public override string Name => "ControlFlowValidatorV3";
public override int Order => 400;
```

- **Name**: Identifier for logging and debugging
- **Order**: Execution order in the validation pipeline (400 = after type checking at 300)
  - **Note**: Same order as V2 validator—projects use one or the other, not both

### Instance Fields

```csharp
private readonly ControlFlowGraphBuilder _cfgBuilder = new();
private SemanticContext _context = null!;
private ICompilerLogger _logger = NullLogger.Instance;
```

- **`_cfgBuilder`**: Reusable builder for constructing CFGs (stateless, can be shared)
- **`_context`**: Current semantic analysis context (set per validation run)
- **`_logger`**: Logging interface (initialized to NullLogger for safety)

---

## 3. Key Functions/Methods

### 3.1 `Validate(Module, SemanticContext)` - Entry Point

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by the `ValidationPipeline`.

**Flow**:
1. Stores the `context` and `logger` for use by other methods
2. Iterates through all top-level statements in the module
3. Dispatches to `ValidateTopLevelStatement` for each

**Key Detail**: Only validates top-level statements—nested functions are not validated separately in this pass (they're validated when their containing function is processed).

---

### 3.2 `ValidateTopLevelStatement(Statement)` - Dispatcher

```csharp
private void ValidateTopLevelStatement(Statement stmt)
```

**Purpose**: Routes different statement types to appropriate validation handlers.

**Handles**:
- **`FunctionDef`**: Directly validates the function
- **`ClassDef`**: Validates all method members within the class
- **`StructDef`**: Validates all method members within the struct
- **Other statements**: Ignored (no control flow validation needed)

**Design Pattern**: This is a **visitor-style dispatch** using pattern matching (`switch` on statement types).

**Why Classes/Structs Are Special**: Methods inside classes/structs have their own control flow that needs validation, so we recurse into them.

---

### 3.3 `ValidateFunction(FunctionDef)` - Core Validation Logic

```csharp
private void ValidateFunction(FunctionDef func)
```

**Purpose**: The workhorse method that performs all control flow checks on a single function.

#### Step-by-Step Breakdown

**Step 1: Skip Special Cases**

```csharp
// Skip abstract methods
if (func.Decorators.Any(d => d.Name == "abstract"))
    return;

// Skip stub bodies (ellipsis only)
if (func.Body.Length == 1 && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral })
    return;
```

**Why Skip?**
- **Abstract methods**: Have no implementation, so no control flow to validate
- **Stub methods** (`def foo(): ...`): Used for type hints/interfaces, not real implementations

**Pattern**: This uses C# 9.0 pattern matching with property patterns for concise type checking.

---

**Step 2: Build the Control Flow Graph**

```csharp
var cfg = _cfgBuilder.Build(func);
```

**What Happens Here**: The `ControlFlowGraphBuilder` analyzes the function's AST and constructs a graph where:
- **Nodes (BasicBlocks)**: Sequences of statements that execute together
- **Edges**: Possible control flow transitions (e.g., if-then-else branches)

**Example Visualization**:
```python
def example(x: int) -> int:
    if x > 0:
        return 1
    return -1
```

Creates CFG:
```
[Entry] → [if condition] → [then: return 1] → [Exit]
              ↓
         [else: return -1] → [Exit]
```

---

**Step 3: Detect Unreachable Code**

```csharp
var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
foreach (var info in unreachable)
{
    AddError(_context, "Unreachable code detected",
        info.FirstUnreachableStatement.LineStart,
        info.FirstUnreachableStatement.ColumnStart);
}
```

**What It Catches**:
```python
def bad_function():
    return 42
    print("Never runs!")  # ← Unreachable code error
```

**How It Works**: The `FindUnreachableCode` method performs graph reachability analysis:
1. Start from the entry block
2. Mark all blocks reachable via edges
3. Any unmarked blocks are unreachable

**Location Info**: Reports the **first** unreachable statement in each unreachable block for better error messages.

---

**Step 4: Check Return Paths**

```csharp
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
```

**What It Catches**:
```python
def bad_return(x: int) -> int:
    if x > 0:
        return 1
    # Missing return for x <= 0 case!
```

**Why Skip Void?**: Functions without return types (or `-> None`) don't need to return values.

**Algorithm**: The `FindMissingReturnPaths` method:
1. Finds all blocks that reach the exit block
2. Checks if they contain a `return` statement
3. Reports blocks that reach exit without returning

**Error Location**: Reports at the function definition (not specific line) because the error is about the overall function structure.

---

**Step 5: Validate Loop Control Flow**

```csharp
var loopErrors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);
foreach (var error in loopErrors)
{
    AddError(_context, error.Message,
        error.Statement.LineStart, error.Statement.ColumnStart);
}
```

**What It Catches**:
```python
def bad_break():
    break  # ← Error: break outside loop

def bad_continue():
    if True:
        continue  # ← Error: continue outside loop
```

**How It Works**: During CFG construction, the builder tracks loop contexts. When it encounters `break`/`continue`:
- **Inside loop**: Terminators point to appropriate loop blocks (exit for break, header for continue)
- **Outside loop**: Terminators have null or invalid targets

The validator checks these targets to detect misuse.

---

### 3.4 `GetFunctionReturnType(FunctionDef)` - Type Resolution

```csharp
private SemanticType GetFunctionReturnType(FunctionDef func)
```

**Purpose**: Retrieves the function's return type for validation.

**Strategy** (Two-Level Lookup):

1. **Fast Path**: Check cached type annotation
   ```csharp
   var cachedType = _context.SemanticInfo.GetTypeAnnotation(func.ReturnType);
   if (cachedType != null)
       return cachedType;
   ```

2. **Slow Path**: Resolve type if not cached
   ```csharp
   return _context.TypeResolver.ResolveTypeAnnotation(func.ReturnType);
   ```

**Why Cache?**: Type resolution can be expensive, especially for complex generic types. The `SemanticInfo` cache avoids redundant work.

**Null Handling**: If `func.ReturnType == null`, returns `SemanticType.Void` (function doesn't declare a return type).

---

## 4. Dependencies

### Internal Sharpy Components

| Dependency | Purpose |
|-----------|---------|
| `Sharpy.Compiler.Analysis.ControlFlow.ControlFlowGraphBuilder` | Constructs CFGs from ASTs |
| `Sharpy.Compiler.Analysis.ControlFlow.ControlFlowAnalysis` | Static analysis algorithms (reachability, return paths, loop validation) |
| `Sharpy.Compiler.Parser.Ast.*` | AST node types (`FunctionDef`, `ClassDef`, `Statement`, etc.) |
| `Sharpy.Compiler.Logging.ICompilerLogger` | Diagnostic logging |
| `SemanticValidatorBase` | Base class providing error reporting |

### Key Data Structures

- **`ControlFlowGraph`**: Represents function control flow as a directed graph
- **`BasicBlock`**: A sequence of statements executed together (node in CFG)
- **`SemanticContext`**: Shared analysis context (symbol tables, type info, diagnostics)
- **`SemanticType`**: Type system representation

---

## 5. Patterns and Design Decisions

### 5.1 Validator Pattern

The validator implements the **Validator Pattern** from the pipeline architecture:
- Each validator has a specific responsibility (single concern)
- Validators are stateless between invocations (can be reused)
- Order is controlled via the `Order` property
- Errors accumulate in `context.Diagnostics` (don't throw exceptions)

### 5.2 Graph-Based Analysis vs. AST Walking

**Why CFG Instead of AST?**

| Approach | Pros | Cons |
|----------|------|------|
| **AST Walking** (V2) | Simple, direct | Imprecise for complex control flow |
| **CFG Analysis** (V3) | Mathematically precise, handles all cases | More complex, higher upfront cost |

**Trade-off**: V3 is preferred for production because correctness matters more than simplicity.

### 5.3 Separation of Concerns

Notice the clean separation:
- **Builder** (`ControlFlowGraphBuilder`): Constructs CFGs
- **Analysis** (`ControlFlowAnalysis`): Pure algorithms on CFGs
- **Validator** (`ControlFlowValidatorV3`): Orchestrates and reports errors

**Benefits**:
- Testable in isolation
- Reusable (e.g., CFG analysis could be used for optimizations)
- Maintainable (changes to algorithms don't affect validation logic)

### 5.4 Immutable AST Pattern

The validator **never modifies the AST**. It:
1. Reads AST nodes
2. Builds a separate CFG data structure
3. Reports errors via the diagnostics system

**Why?**: Immutable ASTs are thread-safe, cacheable, and prevent accidental corruption.

### 5.5 Early Exit Optimization

```csharp
if (func.Decorators.Any(d => d.Name == "abstract"))
    return;
```

By filtering out special cases early, the validator avoids expensive CFG construction for functions that don't need it.

---

## 6. Debugging Tips

### 6.1 Enable Debug Logging

Set logging level to Debug to see CFG construction:
```csharp
_logger.LogDebug($"Building CFG for function: {func.Name}");
```

This will show which functions are being analyzed.

### 6.2 Visualize the CFG

Add temporary code to dump the CFG:
```csharp
var cfg = _cfgBuilder.Build(func);
Console.WriteLine(cfg.ToDotFormat()); // If implemented
```

Use Graphviz to visualize the control flow.

### 6.3 Inspect Unreachable Blocks

When debugging unreachable code false positives:
```csharp
var unreachable = cfg.FindUnreachableBlocks();
foreach (var block in unreachable)
{
    Console.WriteLine($"Block {block.Label}: {block.Statements.Count} statements");
}
```

### 6.4 Check Terminator Targets

For loop control flow bugs:
```csharp
foreach (var block in cfg.Blocks)
{
    if (block.Terminator is BreakTerminator bt)
        Console.WriteLine($"Break in {block.Label} → {bt.Target?.Label ?? "NULL"}");
}
```

A `null` target usually indicates a bug in the CFG builder's loop tracking.

### 6.5 Common Issues

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| False positive unreachable code | CFG builder incorrectly marking block unreachable | Check edge construction in builder |
| Missing return not detected | Block reaches exit without `ReturnStatement` check | Verify terminator types in `FindMissingReturnPaths` |
| Break/continue false positive | Loop context tracking broken | Debug `_loopStack` in builder |

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add New Control Flow Checks**:
```csharp
// Add new validation in ValidateFunction:
var infiniteLoops = ControlFlowAnalysis.FindInfiniteLoops(cfg);
foreach (var loop in infiniteLoops)
{
    AddWarning(_context, "Infinite loop detected", ...);
}
```

**Support New Language Features**:
- If Sharpy adds `try`/`finally`, update this validator to check exception flow
- If Sharpy adds `async`/`await`, integrate async region validation

### 7.2 What NOT to Change

**Don't Modify**:
- The CFG builder (that's in `ControlFlowGraphBuilder.cs`)
- The analysis algorithms (that's in `ControlFlowAnalysis.cs`)
- The AST nodes themselves (immutable by design)

**Do Modify**:
- Error messages for clarity
- Which checks to perform
- How to report diagnostics

### 7.3 Adding a New Validation

**Template**:
```csharp
// In ValidateFunction, after existing checks:

// 4. Check for <your concern>
var issues = ControlFlowAnalysis.Your NewCheck(cfg);
foreach (var issue in issues)
{
    AddError(_context, issue.Message,
        issue.Location.LineStart, issue.Location.ColumnStart);
}
```

**Steps**:
1. Implement the analysis algorithm in `ControlFlowAnalysis.cs`
2. Call it from `ValidateFunction`
3. Add tests in `Sharpy.Compiler.Tests/Semantic/`
4. Update language specification if it's a new rule

### 7.4 Testing Strategy

**Unit Tests**: Test the validator with hand-crafted ASTs
```csharp
[Fact]
public void UnreachableCodeAfterReturn_ReportsError()
{
    var module = CreateModule(@"
        def foo():
            return 1
            print('unreachable')
    ");
    var validator = new ControlFlowValidatorV3();
    validator.Validate(module, context);
    Assert.Contains("Unreachable code", context.Diagnostics.Errors);
}
```

**Integration Tests**: Use file-based tests
```
TestFixtures/control_flow/
├── unreachable_after_return.spy
└── unreachable_after_return.error
```

### 7.5 Performance Considerations

- CFG construction is O(n) where n = statements in function
- Reachability analysis is O(V + E) where V = blocks, E = edges
- **Optimization**: The validator reuses the same `_cfgBuilder` instance (no allocations between functions)

---

## 8. Cross-References

### Related Files

| File | Relationship |
|------|--------------|
| `ControlFlowValidatorV2.cs` (removed) | **Predecessor**: AST-based validator, replaced by V3 |
| `ControlFlowGraphBuilder.cs` | **Dependency**: Constructs the CFG this validator analyzes |
| `ControlFlowAnalysis.cs` | **Dependency**: Provides analysis algorithms used by this validator |
| `ISemanticValidator.cs` | **Interface**: Defines validator contract and base class |
| `ValidationPipeline.cs` | **Orchestrator**: Runs this validator in the correct order |
| `SemanticContext.cs` | **Context**: Shared state across validators |

### Related Documentation

- **ValidationPipeline**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.md` *(if exists)*
- **CFG Builder**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.md` *(if exists)*
- **Language Spec**: `docs/language_specification/` (for control flow semantics)

### Related Tests

- `src/Sharpy.Compiler.Tests/Semantic/ControlFlowValidatorV3Tests.cs` *(if exists)*
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/control_flow/` (file-based integration tests)

---

## Appendix: Example Scenarios

### Scenario 1: Unreachable Code Detection

**Input**:
```python
def example() -> int:
    return 42
    print("Never runs")  # Unreachable
    x = 10              # Also unreachable
```

**CFG**:
```
[Entry] → [return 42] → [Exit]
          ↓
    [print(...)]  ← Unreachable block
          ↓
    [x = 10]      ← Also unreachable
```

**Output**: Error at line 3: "Unreachable code detected"

---

### Scenario 2: Missing Return Path

**Input**:
```python
def example(x: int) -> int:
    if x > 0:
        return 1
    # Missing return for x <= 0
```

**CFG**:
```
[Entry] → [if x > 0] → [return 1] → [Exit]
              ↓
         [else branch] → [Exit]  ← No return!
```

**Output**: Error at line 1: "Function 'example' must return a value of type 'int' in all code paths"

---

### Scenario 3: Break Outside Loop

**Input**:
```python
def bad():
    if True:
        break  # Error: not in a loop
```

**CFG**:
```
[Entry] → [if True] → [break] → [NULL target!]
```

**Output**: Error at line 3: "'break' statement outside loop"

---

## Summary

`ControlFlowValidatorV3` is a critical component of Sharpy's semantic analysis that ensures code correctness through graph-based control flow analysis. By building a CFG and applying standard compiler algorithms, it provides accurate, comprehensive validation of:

- Unreachable code
- Return path completeness  
- Loop control flow correctness

As a newcomer, focus on:
1. Understanding the CFG abstraction (nodes = blocks, edges = control flow)
2. How the validator orchestrates analysis vs. performing it
3. The separation between AST (input) and CFG (analysis structure)
4. How errors are reported through the diagnostics system

For most contributions, you'll add new checks in `ValidateFunction` that call analysis methods from `ControlFlowAnalysis`, following the established pattern.
