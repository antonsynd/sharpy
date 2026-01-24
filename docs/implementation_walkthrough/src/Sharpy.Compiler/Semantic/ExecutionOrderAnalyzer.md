# Walkthrough: ExecutionOrderAnalyzer.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs`

---

## Overview

The `ExecutionOrderAnalyzer` is a specialized semantic analysis component that detects module-level variables with **execution order issues**—variables that cannot safely be compiled to C# static field initializers.

### Why This Matters

In C#, static field initialization order is undefined within a class. If variable `b` depends on variable `a`, but they're both static fields, C# doesn't guarantee `a` initializes before `b`. This can cause subtle runtime bugs.

Sharpy's solution: Variables with execution order issues are **hoisted into a `Main()` method** as local variables, where execution order is well-defined and sequential.

### Role in the Pipeline

```
Parser (AST) → Semantic Analysis → ExecutionOrderAnalyzer → CodeGenInfoComputer → RoslynEmitter
```

- **Input**: List of module-level AST statements + SymbolTable
- **Output**: HashSet of variable names that have execution order issues
- **Downstream consumer**: `CodeGenInfoComputer` uses this to tag variables appropriately, which `RoslynEmitter` later uses to decide between static fields vs. local variables

This analyzer was **extracted from RoslynEmitter** to separate concerns and enable earlier detection during semantic analysis.

---

## Class Structure

### Main Class: `ExecutionOrderAnalyzer`

```csharp
public class ExecutionOrderAnalyzer
{
    private readonly SymbolTable _symbolTable;
    
    // Tracking dictionaries and sets...
    
    public ExecutionOrderAnalyzer(SymbolTable symbolTable) { }
    public HashSet<string> Analyze(IReadOnlyList<Statement> statements) { }
}
```

**Design Pattern**: Single-responsibility analyzer with internal state that resets between analyses.

---

## State Tracking Fields

The analyzer maintains several data structures to categorize and track variables:

### Position Tracking
```csharp
private readonly Dictionary<string, int> _variableFirstSeen;      // Assignment position
private readonly Dictionary<string, int> _variableFirstDeclared;  // Declaration position
```

These track the **statement index** where each variable first appears. Used to detect "assigned before declared" issues.

### Variable Categorization
```csharp
private readonly HashSet<string> _constVariables;           // Const or CONSTANT_CASE
private readonly HashSet<string> _assignmentVariables;      // Created by Assignment only
private readonly HashSet<string> _typeAndFunctionNames;     // Types/functions (safe to reference)
```

**Why categorize?**
- **Const variables**: Safe to reference—they're compile-time constants
- **Assignment variables**: Variables with `x = 5` but no `x: int = 5` declaration → always local
- **Types/functions**: Safe to reference—they're not initialized at runtime

### Result
```csharp
private readonly HashSet<string> _variablesWithIssues;
```

The final output: variables that need special handling.

---

## Key Methods

### 1. `Analyze()` — Main Entry Point

```csharp
public HashSet<string> Analyze(IReadOnlyList<Statement> statements)
{
    // Clear state from previous analysis
    ClearAllState();
    
    // Pass 1: Collect type/function names and const variables
    CollectDeclarationNames(statements);
    
    // Pass 2: Track variable positions and detect basic issues
    DetectBasicIssues(statements);
    
    // Pass 3: Detect initializer dependencies (transitive closure)
    DetectInitializerDependencies(statements);
    
    return new HashSet<string>(_variablesWithIssues);
}
```

**Three-pass algorithm** for correctness and clarity:

#### **Pass 1**: Build safe reference lists
- Identify all types, functions, enums, interfaces
- Identify const variables (explicit `const` or `CONSTANT_CASE`)

#### **Pass 2**: Detect ordering issues
- Track first assignment vs. first declaration
- Flag multiple declarations
- Flag assignment-only variables (no type annotation)

#### **Pass 3**: Detect dependency chains
- If `b` initializer references `a`, and `a` has issues → `b` has issues
- Uses **fixed-point iteration** to find transitive closure

---

### 2. `CollectDeclarationNames()` — Pass 1

```csharp
private void CollectDeclarationNames(IReadOnlyList<Statement> statements)
{
    foreach (var stmt in statements)
    {
        switch (stmt)
        {
            case ClassDef classDef:
                _typeAndFunctionNames.Add(classDef.Name);
                break;
            case FunctionDef funcDef:
                _typeAndFunctionNames.Add(funcDef.Name);
                break;
            case VariableDeclaration varDecl when varDecl.IsConst || IsConstantCaseName(varDecl.Name):
                _constVariables.Add(varDecl.Name);
                break;
            // ... other cases
        }
    }
}
```

**Purpose**: Build whitelists of "safe" names that can be referenced without causing issues.

**Convention Check**: `IsConstantCaseName()` treats `ALL_CAPS_WITH_UNDERSCORES` as constants, matching Python convention.

---

### 3. `DetectBasicIssues()` — Pass 2

```csharp
private void DetectBasicIssues(IReadOnlyList<Statement> statements)
{
    for (int i = 0; i < statements.Count; i++)
    {
        var stmt = statements[i];
        
        if (stmt is VariableDeclaration varDecl && !_constVariables.Contains(varDecl.Name))
        {
            var varName = varDecl.Name;
            
            if (_variableFirstDeclared.ContainsKey(varName))
            {
                // Multiple declarations → issue
                _variablesWithIssues.Add(varName);
            }
            else
            {
                _variableFirstDeclared[varName] = i;
                
                // Check if assigned before declared
                if (_variableFirstSeen.TryGetValue(varName, out var firstSeen) && firstSeen < i)
                {
                    _variablesWithIssues.Add(varName);
                }
            }
        }
        else if (stmt is Assignment assign && assign.Target is Identifier targetId)
        {
            var varName = targetId.Name;
            
            if (!_variableFirstSeen.ContainsKey(varName))
            {
                _variableFirstSeen[varName] = i;
            }
            
            // Assignment-only variable (no declaration)
            if (!_variableFirstDeclared.ContainsKey(varName) && !_constVariables.Contains(varName))
            {
                _assignmentVariables.Add(varName);
                _variablesWithIssues.Add(varName);
            }
        }
    }
}
```

**Detection Logic**:

1. **Multiple declarations**: 
   ```python
   x: int = 5
   x: int = 10  # Error! Two declarations
   ```

2. **Assigned before declared**:
   ```python
   x = 5       # Assignment at position 0
   x: int = 10 # Declaration at position 1 → issue!
   ```

3. **Assignment-only variables** (Python's dynamic typing):
   ```python
   x = 5  # No type annotation → must be local in Main()
   ```

---

### 4. `DetectInitializerDependencies()` — Pass 3

```csharp
private void DetectInitializerDependencies(IReadOnlyList<Statement> statements)
{
    // Build map of variable → initializer
    var variableDeclarations = new Dictionary<string, VariableDeclaration>();
    foreach (var stmt in statements)
    {
        if (stmt is VariableDeclaration varDecl &&
            !_constVariables.Contains(varDecl.Name) &&
            varDecl.InitialValue != null)
        {
            variableDeclarations[varDecl.Name] = varDecl;
        }
    }
    
    // Iterate until no new issues found (fixed-point)
    bool changed = true;
    while (changed)
    {
        changed = false;
        
        foreach (var (varName, varDecl) in variableDeclarations)
        {
            if (_variablesWithIssues.Contains(varName))
                continue;
            
            var referencedIds = new HashSet<string>();
            CollectReferencedIdentifiers(varDecl.InitialValue!, referencedIds);
            
            foreach (var refId in referencedIds)
            {
                // Skip safe references
                if (_typeAndFunctionNames.Contains(refId) || _constVariables.Contains(refId))
                    continue;
                
                var symbol = _symbolTable.Lookup(refId);
                if (symbol is FunctionSymbol or TypeSymbol)
                    continue;
                
                // Transitive issue detection
                if (_variablesWithIssues.Contains(refId) ||
                    _assignmentVariables.Contains(refId) ||
                    variableDeclarations.ContainsKey(refId))
                {
                    _variablesWithIssues.Add(varName);
                    changed = true;
                    break;
                }
            }
        }
    }
}
```

**Fixed-Point Algorithm**: Keeps iterating until no new issues are found.

**Example**:
```python
a: int = 1 + 2        # OK - literal values
b: int = a * 3        # Issue! References non-const module variable
c: int = b + 5        # Issue! Transitively depends on 'b'
```

**Iteration 1**: `b` flagged (references `a`)  
**Iteration 2**: `c` flagged (references `b`, which has issues)  
**Iteration 3**: No changes → done

---

### 5. `CollectReferencedIdentifiers()` — AST Traversal

```csharp
private void CollectReferencedIdentifiers(Expression expr, HashSet<string> identifiers)
{
    switch (expr)
    {
        case Identifier id:
            identifiers.Add(id.Name);
            break;
        case BinaryOp binOp:
            CollectReferencedIdentifiers(binOp.Left, identifiers);
            CollectReferencedIdentifiers(binOp.Right, identifiers);
            break;
        case FunctionCall call:
            if (call.Function is Identifier funcId)
                identifiers.Add(funcId.Name);
            // ... traverse arguments
            break;
        // ... 20+ expression types handled
    }
}
```

**Purpose**: Recursively walk the AST to find all identifiers referenced in an expression.

**Completeness**: Handles all expression types including:
- Binary/unary operations
- Function calls (with keyword arguments)
- Member access, indexing, slicing
- Literals (lists, dicts, sets, tuples)
- Comprehensions (list/set/dict)
- Lambda expressions
- F-strings
- Comparison chains

**Pattern**: Classic recursive visitor without the Visitor pattern boilerplate.

---

### 6. `IsConstantCaseName()` — Convention Check

```csharp
private static bool IsConstantCaseName(string name)
{
    return name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c))
           && name.Any(char.IsUpper);
}
```

**Python Convention**: Variables like `MAX_SIZE` or `DEFAULT_TIMEOUT` are treated as constants even without explicit `const` keyword.

**Requirements**:
- All characters uppercase, underscore, or digits
- At least one uppercase letter (to exclude `_` alone)

**Examples**:
- ✅ `MAX_SIZE`, `PI`, `HTTP_200_OK`
- ❌ `max_size`, `_`, `123`

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Parser.Ast`**: All AST node types
   - `Statement` hierarchy: `VariableDeclaration`, `Assignment`, `ClassDef`, `FunctionDef`, etc.
   - `Expression` hierarchy: `Identifier`, `BinaryOp`, `FunctionCall`, comprehensions, etc.

2. **`SymbolTable`** (from Semantic namespace):
   - Used to distinguish builtins (`FunctionSymbol`, `TypeSymbol`) from user variables
   - Helps avoid false positives (e.g., `len(x)` shouldn't flag `len`)

### Downstream Consumers

- **`CodeGenInfoComputer`**: Uses the result to populate `CodeGenInfo`
- **`RoslynEmitter`**: Reads `CodeGenInfo` to decide variable placement (static field vs. local variable)

---

## Patterns and Design Decisions

### 1. **Three-Pass Algorithm**

**Why not single pass?**
- Pass 1 needs complete type/function catalog before Pass 2 decisions
- Pass 3 requires complete basic issue detection from Pass 2

**Tradeoff**: Slightly more overhead for cleaner logic and correctness.

### 2. **Fixed-Point Iteration**

The transitive dependency detection uses a **fixed-point algorithm**:

```csharp
bool changed = true;
while (changed) {
    changed = false;
    // ... if any new issue found, set changed = true
}
```

**Why?** Dependency chains can be arbitrarily deep:
```python
a: int = some_func()  # Has issues
b: int = a            # Depends on a → has issues
c: int = b            # Depends on b → has issues
d: int = c            # ... and so on
```

**Convergence**: Guaranteed to terminate because:
- `_variablesWithIssues` only grows (monotonic)
- Finite set of variables
- Eventually, all transitive issues are found

### 3. **Stateful but Reusable**

The class maintains state but **clears it at the start of each analysis**:

```csharp
public HashSet<string> Analyze(...)
{
    _variableFirstSeen.Clear();
    _variableFirstDeclared.Clear();
    // ... clear all state
}
```

**Pattern**: Object pooling—create once, use multiple times with different modules.

### 4. **Whitelisting over Blacklisting**

Instead of trying to detect all "safe" patterns, the analyzer:
1. Whitelists known-safe references (consts, types, functions)
2. Assumes everything else is potentially unsafe
3. **Conservative approach**: Might flag false positives, but never misses real issues

### 5. **Separation of Concerns**

Originally in `RoslynEmitter`, this logic:
- Mixed code generation with semantic analysis
- Prevented CodeGen preparation during semantic phase
- Made testing difficult

**Extracted** into standalone analyzer:
- ✅ Single responsibility
- ✅ Testable in isolation
- ✅ Used earlier in pipeline

---

## Debugging Tips

### 1. **Unexpected Variable in Issues Set**

Check these common causes:

**Cause 1**: Variable references another module variable
```python
x: int = 10
y: int = x  # y flagged because references module var x
```

**Debug**: Add logging in `DetectInitializerDependencies()`:
```csharp
Console.WriteLine($"Variable {varName} references {refId}");
```

**Cause 2**: Variable name matches a type/function name
```python
list: int = 5  # Shadows builtin 'list'
```

**Debug**: Check if name is in `_typeAndFunctionNames` but also in `_variableFirstDeclared`.

### 2. **Variable Missing from Issues Set**

This is more serious—means unsafe code might compile as static field.

**Check**:
1. Is it detected as const? (`_constVariables.Contains(name)`)
2. Is it detected as a type/function? (`_typeAndFunctionNames.Contains(name)`)
3. Does it have an initializer? (Pass 3 only checks variables with `InitialValue != null`)

**Debug**: Add breakpoints in all three passes for the specific variable name.

### 3. **Fixed-Point Loop Doesn't Terminate**

**Symptom**: Analyzer hangs in `DetectInitializerDependencies()`.

**Likely cause**: Bug in `CollectReferencedIdentifiers()` causing infinite recursion or circular reference detection.

**Debug**:
```csharp
int iterationCount = 0;
while (changed) {
    if (++iterationCount > 1000) {
        throw new Exception($"Fixed-point didn't converge after {iterationCount} iterations");
    }
    // ... rest of loop
}
```

### 4. **Inspecting AST Traversal**

To see what identifiers are collected from an expression:

```csharp
var ids = new HashSet<string>();
CollectReferencedIdentifiers(someExpression, ids);
Console.WriteLine($"Found identifiers: {string.Join(", ", ids)}");
```

### 5. **Statement Position Debugging**

Print the statement positions being tracked:

```csharp
Console.WriteLine($"Variable {varName}: FirstSeen={_variableFirstSeen[varName]}, FirstDeclared={_variableFirstDeclared[varName]}");
```

---

## Contribution Guidelines

### When to Modify This File

1. **New execution order issue pattern discovered**
   - Add detection logic to appropriate pass
   - Add test case showing the issue

2. **New AST expression type added**
   - Update `CollectReferencedIdentifiers()` switch statement
   - Ensure new expression types are traversed correctly

3. **Performance optimization**
   - Current algorithm is O(n²) in worst case (fixed-point iteration)
   - Could optimize with dependency graph and topological sort
   - Only optimize if profiling shows it's a bottleneck

4. **False positive reduction**
   - Refine whitelist logic (e.g., more sophisticated const detection)
   - Update `DetectInitializerDependencies()` filtering

### Making Changes Safely

**Step 1**: Add a failing test
```csharp
[Fact]
public void AnalyzerDetectsNewPattern()
{
    var source = @"
x = 5
y: int = x  # Should be flagged
";
    var analyzer = new ExecutionOrderAnalyzer(symbolTable);
    var issues = analyzer.Analyze(statements);
    
    Assert.Contains("y", issues);
}
```

**Step 2**: Implement the fix

**Step 3**: Verify existing tests still pass
```bash
dotnet test --filter "FullyQualifiedName~ExecutionOrderAnalyzer"
```

**Step 4**: Test with file-based integration tests
```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

### Code Style Guidelines

1. **Keep pass structure clear**: Don't merge passes unless there's a compelling reason
2. **Document new detection patterns**: Update class XML comment with new bullet points
3. **Maintain immutability**: Don't modify AST nodes; only read them
4. **Clear variable names**: `_variablesWithIssues` is better than `_flagged`

### Common Pitfalls

❌ **Don't modify AST during analysis**
```csharp
// WRONG
varDecl.HasIssues = true;
```

✅ **Use external tracking**
```csharp
// CORRECT
_variablesWithIssues.Add(varDecl.Name);
```

❌ **Don't assume expression types**
```csharp
// WRONG - might crash on null or wrong type
var funcId = (Identifier)call.Function;
```

✅ **Use pattern matching**
```csharp
// CORRECT
if (call.Function is Identifier funcId)
    identifiers.Add(funcId.Name);
```

---

## Cross-References

### Related Files in Semantic Analysis

- **`SymbolTable.cs`**: Provides symbol lookup for distinguishing builtins
- **`TypeChecker.cs`**: Main semantic analyzer that calls ExecutionOrderAnalyzer
- **`SemanticInfo.cs`**: Stores type/symbol information about AST nodes
- **`CodeGenInfoComputer.cs`**: Consumes ExecutionOrderAnalyzer results

### Related Files in CodeGen

- **`RoslynEmitter.cs`**: Uses CodeGenInfo to decide variable placement
  - Variables with issues → local variables in `Main()`
  - Variables without issues → static fields

### Related Documentation

- **Language Spec**: `docs/language_specification/` (module-level variable semantics)
- **Compiler Pipeline**: Root `CLAUDE.md` for overall architecture
- **Testing Guide**: `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`

---

## Example Walkthrough

Let's trace through a concrete example:

```python
# Module-level statements
PI: float = 3.14159      # Line 0
x = 5                    # Line 1
y: int = x + 10          # Line 2
z: int = PI * 2          # Line 3
```

### Pass 1: `CollectDeclarationNames()`
- `_constVariables`: `{}`
- `_typeAndFunctionNames`: `{}`
- No types/functions/consts detected

### Pass 2: `DetectBasicIssues()`

**Statement 0**: `PI: float = 3.14159`
- Not a const (not marked with `const`, not `CONSTANT_CASE`)
- `_variableFirstDeclared["PI"] = 0`

**Statement 1**: `x = 5`
- Assignment without declaration
- `_variableFirstSeen["x"] = 1`
- `_assignmentVariables.Add("x")`
- ⚠️ `_variablesWithIssues.Add("x")` (assignment-only variable)

**Statement 2**: `y: int = x + 10`
- `_variableFirstDeclared["y"] = 2`

**Statement 3**: `z: int = PI * 2`
- `_variableFirstDeclared["z"] = 3`

**After Pass 2**: `_variablesWithIssues = {"x"}`

### Pass 3: `DetectInitializerDependencies()`

**Build initializer map**:
```
variableDeclarations = {
    "PI": VariableDeclaration(InitialValue: 3.14159),
    "y": VariableDeclaration(InitialValue: x + 10),
    "z": VariableDeclaration(InitialValue: PI * 2)
}
```

**Iteration 1**:

- **Check PI**: References only literal `3.14159` → no new issues
- **Check y**: 
  - Collects identifiers: `{"x"}`
  - `x` is in `_assignmentVariables` 
  - ⚠️ `_variablesWithIssues.Add("y")`
  - `changed = true`
- **Check z**: 
  - Collects identifiers: `{"PI"}`
  - `PI` is in `variableDeclarations` (non-const module var)
  - ⚠️ `_variablesWithIssues.Add("z")`
  - `changed = true`

**Iteration 2**:
- All variables already in `_variablesWithIssues` or have no new references
- `changed = false` → exit loop

**Final Result**: `{"x", "y", "z"}`

Only `PI` can be a static field; `x`, `y`, `z` become local variables in `Main()`.

---

## Summary

The `ExecutionOrderAnalyzer` is a critical component that ensures Sharpy's module-level variables compile correctly to C#. By detecting execution order issues through a three-pass algorithm, it enables the code generator to make informed decisions about variable placement—preventing subtle initialization order bugs that would otherwise be difficult to diagnose.

**Key Takeaways**:
- Three passes: declaration collection → basic issues → transitive dependencies
- Conservative approach: prefers false positives over false negatives
- Fixed-point iteration for transitive closure
- Extracted from RoslynEmitter for better separation of concerns
- Directly impacts code generation strategy (static fields vs. local variables)
