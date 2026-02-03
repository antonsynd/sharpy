# Remaining Compiler Hardening Concerns

> **Date:** 2026-02-03
> **Status:** Analysis complete, ready for implementation
> **Context:** Follow-up to `compiler-hardening-assessment.md` after reviewing completed work

---

## Executive Summary

The compiler is in excellent shape after the hardening work. Four concerns remain:

| # | Concern | Type | Impact | Effort |
|---|---------|------|--------|--------|
| 1 | Comparison chain re-evaluation | Correctness | HIGH | Medium |
| 2 | `IsFloatExpression` heuristic | Correctness | Medium | Low |
| 3 | Lexer error recovery | UX | Medium | Medium |
| 4 | TypeChecker size | Maintainability | Low | High |

**Recommendation:** Address #1 and #2 immediately (correctness bugs). Address #3 for UX improvement. Defer #4 unless actively adding type system features.

---

## Concern 1: Comparison Chain Re-evaluation (Issue #101)

### Analysis

**What:** For chained comparisons like `a < f() < c`, the generated C# evaluates `f()` twice.

**Current behavior:**
```python
# Sharpy source
result = a < get_value() < c
```
```csharp
// Generated C# (WRONG)
result = a < get_value() && get_value() < c;  // f() called twice
```

**Python behavior:**
```python
# Python guarantees single evaluation
a < f() < c  # f() is called exactly once
```

### Rationale

This is a **spec violation** with observable side effects:
- Functions with side effects (incrementing counters, I/O, state mutation) behave incorrectly
- The bug is silent — no error, just wrong behavior
- Users familiar with Python will expect single evaluation

**Risk assessment:** Medium-high. Any code using comparison chains with non-pure expressions is affected.

### Plan

**Approach:** Introduce temp variables for non-trivial intermediate expressions.

**Files to modify:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` — comparison chain generation (~line 1093)

**Implementation:**

1. **Identify non-trivial expressions** — anything that isn't:
   - `Identifier`
   - `IntLiteral`, `FloatLiteral`, `StringLiteral`, `BoolLiteral`
   - Simple member access on identifier (e.g., `obj.field`)

2. **Generate temp variables** using existing `_variableVersions` pattern:
   ```csharp
   // Before comparison chain
   var __cmp_0 = get_value();
   // In comparison
   result = a < __cmp_0 && __cmp_0 < c;
   ```

3. **Edge cases:**
   - Nested function calls: `a < f(g()) < c` — only outer call needs temp
   - Multiple intermediates: `a < f() < g() < c` — temp for both f() and g()
   - Short chains: `a < b` — no temp needed (not a chain)

**Tests to add:**
- `type_system/comparison_chain_side_effects.spy` — function incrementing counter
- `type_system/comparison_chain_multiple_calls.spy` — multiple function calls
- `type_system/comparison_chain_simple.spy` — literals/identifiers (no temp needed)

**Verification:**
```bash
python3 -c "
counter = 0
def f():
    global counter
    counter += 1
    return counter
result = 0 < f() < 10
print(f'result={result}, counter={counter}')  # Should print counter=1
"
```

**Estimated effort:** 2-4 hours

---

## Concern 2: `IsFloatExpression` Heuristic

### Analysis

**What:** `RoslynEmitter.Operators.cs:406` — For floor division (`//`), `IsFloatExpression` guesses the operand type from AST shape instead of consulting `SemanticInfo`.

**Current behavior:**
```csharp
private bool IsFloatExpression(Expression expr)
{
    return expr switch
    {
        FloatLiteral => true,
        // ... other literal checks
        _ => false  // Variables, function calls default to int!
    };
}
```

**Problem:** `x // get_float()` generates integer division behavior when `get_float()` returns a float.

### Rationale

- **Correctness bug** — produces wrong numerical results
- **Silent failure** — no error, just wrong answer
- **Easy fix** — `SemanticInfo` already has the type information

**Risk assessment:** Medium. Affects any floor division with non-literal float operands.

### Plan

**Approach:** Consult `SemanticInfo` for resolved types instead of guessing.

**Files to modify:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs` — `IsFloatExpression` method

**Implementation:**

```csharp
private bool IsFloatExpression(Expression expr)
{
    // First, try to get the resolved type from SemanticInfo
    if (_context.SemanticInfo.TryGetExpressionType(expr, out var type))
    {
        return type == BuiltinType.Float || type == BuiltinType.Double || type == BuiltinType.Float32;
    }

    // Fallback to heuristic for edge cases (shouldn't happen for well-typed code)
    _context.Logger?.LogDebug($"IsFloatExpression falling back to heuristic for {expr.GetType().Name}");
    return expr switch
    {
        FloatLiteral => true,
        // ... existing heuristics
        _ => false
    };
}
```

**Tests to add:**
- `operators/floor_division_float_function.spy` — `def get_float() -> float: return 7.5; print(get_float() // 2)`
- `operators/floor_division_float_variable.spy` — `x: float = 7.5; print(x // 2)`

**Verification:**
```bash
python3 -c "print(7.5 // 2.0)"  # Should print 3.0
python3 -c "def f(): return 7.5
print(f() // 2)"  # Should print 3.0
```

**Estimated effort:** 1-2 hours

---

## Concern 3: Lexer Error Recovery

### Analysis

**What:** The lexer aborts on the first error (`Lexer.cs:183-187`). An unterminated string on line 1 prevents reporting any parser errors on subsequent lines.

**Current behavior:**
```python
# Line 1: unterminated string
x = "hello
# Line 2: valid code
y = 42
# Line 3: type error
z: int = "world"
```
**Output:** Only the unterminated string error. User can't see the type error on line 3.

### Rationale

- **UX impact** — Users fix one error at a time in a loop. Blocking all downstream errors slows the edit-compile cycle.
- **Consistency** — Parser and TypeChecker already have `MaxErrors` and recovery. Lexer is the outlier.
- **Common scenario** — Unterminated strings are one of the most common typos.

**Risk assessment:** Low (no correctness impact), but high UX value for real-world usage.

### Plan

**Approach:** Skip to next newline on lexer error, then resume tokenization.

**Files to modify:**
- `src/Sharpy.Compiler/Lexer/Lexer.cs`

**Implementation:**

1. **Add recovery method:**
   ```csharp
   private void RecoverFromError()
   {
       // Skip to next newline
       while (_position < _source.Length && _source[_position] != '\n')
           _position++;

       // Skip the newline itself
       if (_position < _source.Length)
       {
           _position++;
           _line++;
           _column = 1;
       }

       // Reset indent tracking to base level (conservative)
       while (_indentStack.Count > 1)
           _indentStack.Pop();
   }
   ```

2. **Modify `TokenizeAll()` loop:**
   ```csharp
   while (!IsAtEnd())
   {
       try
       {
           var token = NextToken();
           if (token != null)
               tokens.Add(token);
       }
       catch (LexerAbortException)
       {
           if (_diagnostics.Errors.Count() >= MaxErrors)
           {
               // Already reported truncation notice
               break;
           }
           RecoverFromError();
           // Continue tokenizing
       }
   }
   ```

3. **Edge cases:**
   - Multi-line strings: Recovery should handle `"""` strings that span lines
   - Indentation corruption: Reset indent stack to prevent cascading INDENT/DEDENT errors
   - Error at EOF: Don't infinite loop

**Tests to add:**
- `errors/lexer_recovery_unterminated_string.spy` + `.error` — verify both lexer and parser errors reported
- `errors/lexer_recovery_multiple_errors.spy` + `.error` — verify multiple lexer errors reported
- `errors/lexer_recovery_valid_after_error.spy` + `.expected` — verify code after error still works

**Estimated effort:** 3-5 hours (including edge case handling)

---

## Concern 4: TypeChecker Size (~4,600 lines)

### Analysis

**What:** `TypeChecker` is split across 5 partial files but is still a single class with many responsibilities:
- Type checking
- Type inference (via `_expectedType` context)
- Type narrowing (via `_narrowedTypes` dictionary)
- CodeGenInfo computation
- Validation orchestration
- Error reporting

### Rationale

**Why this matters less now:**
- Correctness bugs are fixed (SHP0255/SHP0256 for unrecognized nodes)
- Error propagation works (Unknown type handled correctly)
- No immediate plans to add major type system features

**Why it would matter for future work:**
- Adding `ErrorType` sentinel (distinct from `UnknownType`) requires touching narrowing logic
- Adding generic constraints requires modifying inference
- LSP "hover" and "go to definition" need to extract logic from TypeChecker

**Current state:** The 5 partial files are well-organized by concern:
- `TypeChecker.cs` — orchestration, statement dispatch
- `TypeChecker.Expressions.cs` — expression type checking
- `TypeChecker.Statements.cs` — statement type checking
- `TypeChecker.Definitions.cs` — class/function definitions
- `TypeChecker.Utilities.cs` — error helpers, narrowing utilities

### Plan (Deferred)

**Recommendation:** Defer this refactoring unless one of these triggers occurs:
1. Adding `ErrorType` sentinel for better cascading error handling
2. Adding generic type constraints
3. Starting LSP implementation

**When triggered, extract in this order:**

1. **TypeNarrower** (~200 lines)
   - `_narrowedTypes` dictionary
   - `ExtractNarrowedTypes()`, `ExtractNarrowingKey()`
   - Narrowing logic in `CheckIfStatement()`

2. **ExpressionTypeInference** (~500 lines)
   - Core inference from `TypeChecker.Expressions.cs`
   - `_expectedType` context management
   - Binary/unary operator inference

3. **CodeGenInfoComputer** (move to post-TypeChecker pass)
   - Currently interleaved with type checking
   - Should run after all types are resolved

**Estimated effort:** 8-16 hours (significant refactoring with test updates)

---

## Implementation Order

| Priority | Item | Rationale |
|----------|------|-----------|
| **P0** | 1.3 Comparison chains | Correctness bug with spec violation |
| **P0** | 1.4 IsFloatExpression | Correctness bug, easy fix |
| **P1** | 2.4 Lexer recovery | High UX impact, medium effort |
| **P2** | TypeChecker refactor | Deferred until triggered |

**Total estimated effort for P0+P1:** 6-11 hours

---

## Verification Checklist

After implementing P0 items:
- [ ] All existing tests pass
- [ ] New test fixtures added for comparison chains
- [ ] New test fixtures added for floor division with floats
- [ ] Python behavior verified for each test case

After implementing P1:
- [ ] Lexer recovery tests pass
- [ ] Fuzz tests still pass (lexer never crashes)
- [ ] Multi-error scenarios show all errors

---

## Appendix: Related Files

| Concern | Primary Files |
|---------|---------------|
| Comparison chains | `CodeGen/RoslynEmitter.Expressions.cs:~1093` |
| IsFloatExpression | `CodeGen/RoslynEmitter.Operators.cs:406` |
| Lexer recovery | `Lexer/Lexer.cs:183-187` |
| TypeChecker | `Semantic/TypeChecker*.cs` (5 files) |
