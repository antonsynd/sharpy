# Implementation Plan: Task R-0.1.4.2 - Implement Try `else` Clause

## Overview

The Python `try-else` clause executes code when the try block completes **without raising an exception**. This is distinct from `finally` which always runs.

```python
try:
    result = risky_operation()
except ValueError:
    print("Error occurred")
else:
    print(f"Success: {result}")  # Only runs if no exception
finally:
    cleanup()  # Always runs
```

## Current State

The codebase already supports `try/except/finally` but lacks the `else` clause:
- `TryStatement` AST node has `Body`, `Handlers`, `FinallyBody` — missing `ElseBody`
- Parser handles `try:`, `except:`, `finally:` — missing `else:` parsing
- Code generator maps to C# `try/catch/finally` — missing else handling
- The spec (`exception_handling.md`) documents this as "Boolean flag pattern"

---

## Step-by-Step Implementation

### Step 1: Add `ElseBody` Property to AST Node

**File:** `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Change:** Add `ElseBody` property to `TryStatement` record.

```csharp
public record TryStatement : Statement
{
    public List<Statement> Body { get; init; } = new();
    public List<ExceptHandler> Handlers { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();      // <-- ADD THIS
    public List<Statement> FinallyBody { get; init; } = new();
}
```

**Location:** Lines 157-165

---

### Step 2: Update Parser to Handle `else:` Clause

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

**Change:** In `ParseTryStatement()`, after parsing all `except` handlers, check for `else:` before `finally:`.

**Per Python semantics:** The `else` clause must appear after all `except` handlers but before `finally`.

**Current parser flow (lines 943-1019):**
1. Parse `try:` + body
2. Loop through `except` handlers
3. Parse optional `finally:`
4. Return

**New flow:**
1. Parse `try:` + body
2. Loop through `except` handlers
3. **Parse optional `else:` clause** ← NEW
4. Parse optional `finally:`
5. Return

**Implementation snippet to add after the except loop:**

```csharp
// Parse else clause (must come after all except handlers, before finally)
var elseBody = new List<Statement>();
if (Current.Type == TokenType.Else)
{
    Advance();
    Expect(TokenType.Colon);
    ExpectNewline();
    Expect(TokenType.Indent);
    elseBody = ParseBlock();
    Expect(TokenType.Dedent);
}
```

**Note:** The `else` keyword token already exists (`TokenType.Else`) since it's used in if-else statements.

**Update the return statement to include `ElseBody`:**

```csharp
return new TryStatement
{
    Body = body,
    Handlers = handlers,
    ElseBody = elseBody,        // <-- ADD THIS
    FinallyBody = finallyBody,
    LineStart = startLine,
    // ...
};
```

---

### Step 3: Update Code Generation

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Change:** Implement the **boolean flag pattern** in `GenerateTry()` method (lines 1793-1835).

**Pattern (matches loop else implementation):**

```csharp
// Without else clause:
//   try { ... } catch { ... } finally { ... }
//
// With else clause:
//   bool _noException = true;
//   try {
//       ... try body ...
//   }
//   catch (...) {
//       _noException = false;
//       ... handler body ...
//   }
//   finally {
//       ... finally body ...
//   }
//   if (_noException) {
//       ... else body ...
//   }
```

**Key considerations:**
- Only apply the flag pattern when `ElseBody` is non-empty
- Each catch clause must set the flag to `false` before its body
- The flag check happens **after** the try/catch/finally (not inside finally)
- The flag variable name should use `GenerateTempVarName("noException")`

**Implementation approach:**

```csharp
private StatementSyntax GenerateTry(TryStatement tryStmt)
{
    // If no else clause, use simple generation (existing code)
    if (tryStmt.ElseBody.Count == 0)
    {
        return GenerateTrySimple(tryStmt);  // Extract current code here
    }

    // With else clause: use boolean flag pattern
    var flagName = GenerateTempVarName("noException");
    var statements = new List<StatementSyntax>();

    // 1. Declare flag: bool _noException = true;
    statements.Add(LocalDeclarationStatement(
        VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(flagName))
                    .WithInitializer(EqualsValueClause(
                        LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

    // 2. Generate try block (unchanged)
    var tryBlock = Block(tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

    // 3. Generate catch clauses with flag = false prepended
    var catchClauses = tryStmt.Handlers.Select(handler =>
    {
        var handlerStatements = new List<StatementSyntax>();

        // Add: _noException = false;
        handlerStatements.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(flagName),
                LiteralExpression(SyntaxKind.FalseLiteralExpression))));

        // Add handler body
        handlerStatements.AddRange(handler.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        var catchBlock = Block(handlerStatements);

        // ... existing catch declaration logic ...
    }).ToList();

    // 4. Generate finally clause (unchanged)
    FinallyClauseSyntax? finallyClause = null;
    if (tryStmt.FinallyBody.Count > 0)
    {
        var finallyBlock = Block(tryStmt.FinallyBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        finallyClause = FinallyClause(finallyBlock);
    }

    // 5. Add the try statement
    statements.Add(TryStatement(tryBlock, List(catchClauses), finallyClause));

    // 6. Add else check: if (_noException) { ... else body ... }
    var elseBodyBlock = Block(tryStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
    statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

    return Block(statements);
}
```

---

### Step 4: Update Semantic Analysis

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Change:** In `CheckTry()` method (lines 880-907), add scope handling for else block.

```csharp
// Add after the except handlers loop, before the finally block handling:
if (tryStmt.ElseBody != null && tryStmt.ElseBody.Count > 0)
{
    _symbolTable.EnterScope("else");
    foreach (var stmt in tryStmt.ElseBody)
        CheckStatement(stmt);
    _symbolTable.ExitScope();
}
```

---

### Step 5: Update Control Flow Validation

**File:** `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

**Change:** In `ValidateTry()` method (lines 190-214), include else clause in return analysis.

**Python semantics:**
- If no exception occurs: try block runs, then else block runs, then finally runs
- If exception occurs: try block runs (partial), handler runs, finally runs (no else)

**Updated logic:**
- All paths return if: `finally returns` OR (`(try returns AND else returns)` AND `all handlers return`)
- The try and else form a sequential path; both must return for that path to return

```csharp
private (bool, bool) ValidateTry(TryStatement tryStmt)
{
    var (tryReturns, _) = ValidateBlock(tryStmt.Body);

    // Check else block
    bool elseReturns = false;
    if (tryStmt.ElseBody != null && tryStmt.ElseBody.Count > 0)
    {
        var (elseRet, _) = ValidateBlock(tryStmt.ElseBody);
        elseReturns = elseRet;
    }

    bool allHandlersReturn = true;
    foreach (var handler in tryStmt.Handlers)
    {
        var (handlerReturns, _) = ValidateBlock(handler.Body);
        allHandlersReturn = allHandlersReturn && handlerReturns;
    }

    bool finallyReturns = false;
    if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
    {
        var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody);
        finallyReturns = finReturns;
    }

    // For the no-exception path: try runs, then else runs
    // Both must return for that path to return (unless there's no else, then just try)
    bool noExceptionPathReturns = tryStmt.ElseBody.Count > 0
        ? (tryReturns && elseReturns)
        : tryReturns;

    // All paths return if:
    // - Finally returns (overrides everything), OR
    // - No-exception path returns AND all exception handlers return
    bool allPathsReturn = finallyReturns || (noExceptionPathReturns && allHandlersReturn);

    return (allPathsReturn, allPathsReturn);
}
```

---

## Files to Modify

| File | Change |
|------|--------|
| `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | Add `ElseBody` property to `TryStatement` |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Parse `else:` clause in `ParseTryStatement()` |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Implement boolean flag pattern in `GenerateTry()` |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Add else scope in `CheckTry()` |
| `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs` | Update return analysis in `ValidateTry()` |

---

## Tests to Add

### 1. Parser Tests (`src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`)

```csharp
[Fact]
public void ParseTryExceptElse()
{
    var source = @"
try:
    risky()
except Exception:
    handle()
else:
    success()
";
    var module = Parse(source);
    var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
    tryStmt.Body.Should().HaveCount(1);
    tryStmt.Handlers.Should().HaveCount(1);
    tryStmt.ElseBody.Should().HaveCount(1);
    tryStmt.FinallyBody.Should().BeEmpty();
}

[Fact]
public void ParseTryExceptElseFinally()
{
    var source = @"
try:
    risky()
except:
    handle()
else:
    success()
finally:
    cleanup()
";
    var module = Parse(source);
    var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
    tryStmt.Handlers.Should().HaveCount(1);
    tryStmt.ElseBody.Should().HaveCount(1);
    tryStmt.FinallyBody.Should().HaveCount(1);
}
```

### 2. Code Generation Tests (`src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterStatementTests.cs`)

```csharp
[Fact]
public void TryExceptElseGeneratesCorrectCode()
{
    var source = @"
def test():
    try:
        x = 1
    except Exception:
        x = 2
    else:
        x = 3
    return x
";
    var code = GenerateCode(source);
    code.Should().Contain("bool ");  // Flag variable
    code.Should().Contain("= true");  // Flag initialization
    code.Should().Contain("= false"); // Flag set in catch
    code.Should().Contain("if (");    // Else check
}
```

### 3. Integration/E2E Tests

```python
# test_try_else_no_exception.spy
def test():
    result = ""
    try:
        result += "try,"
    except Exception:
        result += "except,"
    else:
        result += "else,"
    return result

assert test() == "try,else,"
```

```python
# test_try_else_with_exception.spy
def test():
    result = ""
    try:
        result += "try,"
        raise ValueError("test")
    except ValueError:
        result += "except,"
    else:
        result += "else,"
    return result

assert test() == "try,except,"
```

```python
# test_try_else_finally.spy
def test():
    result = ""
    try:
        result += "try,"
    except:
        result += "except,"
    else:
        result += "else,"
    finally:
        result += "finally"
    return result

assert test() == "try,else,finally"
```

---

## Potential Risks and Questions

### 1. **Order of `else` and `finally`**
**Question:** Does the `else` block execute before or after `finally`?

**Answer:** In Python, execution order is: `try` → `else` → `finally`. The else runs **before** finally. Our boolean flag pattern naturally handles this since the flag check comes after the try/catch/finally statement but the flag is only checked (not the finally content).

**Verification needed:** Ensure the generated code order matches Python semantics.

### 2. **Exception in `else` block**
**Question:** What if the else block itself raises an exception?

**Answer:** In Python, if the else block raises an exception:
- The finally block still runs
- The exception propagates normally

**Risk:** Our current pattern places the else check **outside** the try/finally, so an exception in else won't trigger the finally.

**Solution:** Wrap the else check inside the finally's scope:
```csharp
try { ... }
catch { _flag = false; ... }
finally {
    ... existing finally body ...
    // Note: This doesn't work - can't conditionally run code in finally
}
```

**Better solution:** The else body should be inside an outer try-finally:
```csharp
bool _noException = true;
try {
    try { ... }
    catch { _noException = false; ... }
    if (_noException) { ... else body ... }
} finally {
    ... finally body ...
}
```

This ensures finally runs even if else throws. **This needs to be addressed in the implementation.**

### 3. **`else` without `except`**
**Question:** Is `try-else` without any `except` clause valid?

**Answer:** In Python, NO. You must have at least one `except` clause to use `else`. The parser should enforce this.

**Implementation:** Add validation in parser or semantic analysis:
```csharp
if (elseBody.Count > 0 && handlers.Count == 0)
{
    throw new ParseException("'else' clause requires at least one 'except' clause");
}
```

### 4. **Variable scope**
**Question:** Can the else block access variables defined in the try block?

**Answer:** Yes, in Python they share scope. Variables defined in try are accessible in else.

**Verification:** Our current scope handling should work since we use separate scopes for semantic analysis but the generated C# code uses the same lexical scope.

### 5. **Break/Continue/Return in try block**
**Question:** If the try block has `return`/`break`/`continue`, should else still run?

**Answer:** No. In Python:
- `return` in try → else does NOT run (finally still runs)
- `break`/`continue` in try (inside loop) → else does NOT run

**Risk:** Our boolean flag pattern doesn't account for this. The flag remains `true` even if try exits early via return.

**Solution:** This may require control flow analysis to detect early exits and set the flag to false. For now, we can document this as a known limitation or defer to a follow-up task.

---

## Implementation Order

1. **AST change** (Step 1) - No dependencies
2. **Parser change** (Step 2) - Depends on Step 1
3. **Semantic analysis** (Steps 4 & 5) - Depends on Step 1
4. **Code generation** (Step 3) - Depends on Step 1
5. **Tests** - Depends on all above

Recommend implementing in order 1 → 2 → 4 → 5 → 3 → Tests, as code generation is the most complex and should be tackled after the foundation is solid.

---

## Definition of Done

- [ ] `TryStatement` AST node has `ElseBody` property
- [ ] Parser correctly parses `try-except-else` and `try-except-else-finally`
- [ ] Parser rejects `try-else` without `except` clause
- [ ] Type checker handles else scope
- [ ] Control flow validator includes else in return analysis
- [ ] Code generator produces correct boolean flag pattern
- [ ] All existing try/except tests still pass
- [ ] New tests for try-else scenarios pass
- [ ] E2E tests confirm correct runtime behavior
