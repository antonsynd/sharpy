# Phase 10.2: `await` Expressions — Implementation Plan

## Overview

Add `await` expression support. `AwaitExpression` AST exists in `Expression.Future.cs`, `TokenType.Await` is lexed, and control flow infrastructure (`BasicBlock.ContainsAwait`, `AsyncStateRegion`, `IdentifyAsyncRegions()`) exists. Wire these across Parser → Semantic → CodeGen → Tests.

## Precedence

In Python, `await` binds at `await_primary ::= 'await' primary | primary` — tighter than unary. `await x + 1` means `(await x) + 1`. Insert `ParseAwaitExpression` between `ParseUnary` and `ParsePower`, calling `ParsePower` for its operand.

```
ParseUnary → ParseAwaitExpression → ParsePower → ParsePostfix → ParsePrimary
```

---

## Commits

### Commit 1: Parser — Add `ParseAwaitExpression()`

**Files:**

1. **`src/Sharpy.Compiler/Parser/Parser.Expressions.cs`**
   - Add `ParseAwaitExpression()` between `ParseUnary` and `ParsePower`
   - Modify `ParseUnary()` to call `ParseAwaitExpression()` instead of `ParsePower()` (line ~660)
   ```csharp
   private Expression ParseAwaitExpression()
   {
       if (Current.Type == TokenType.Await)
       {
           var startLine = Current.Line;
           var startColumn = Current.Column;
           var awaitToken = Current;
           Advance();  // consume 'await'
           var operand = ParseAwaitExpression();  // right-recursive for chained await

           return new AwaitExpression
           {
               Operand = operand,
               LineStart = startLine,
               ColumnStart = startColumn,
               LineEnd = operand.LineEnd,
               ColumnEnd = operand.ColumnEnd,
               Span = CombineSpans(GetSpanFromToken(awaitToken), operand.Span)
           };
       }
       return ParsePower();
   }
   ```

2. **`src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs`**
   - Remove PLACEHOLDER comments from `AwaitExpression` (keep MatchExpression placeholder)

**Verify:** Build succeeds, existing tests pass.

---

### Commit 2: Semantic — Add `CheckAwaitExpression()`

**Files:**

1. **`src/Sharpy.Compiler/Semantic/TypeChecker.cs`**
   - Add field `private bool _currentFunctionIsAsync = false;`

2. **`src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`**
   - In `CheckFunctionDef`, save/set/restore `_currentFunctionIsAsync`:
   ```csharp
   var previousIsAsync = _currentFunctionIsAsync;
   _currentFunctionIsAsync = functionDef.IsAsync;
   // ... check body ...
   _currentFunctionIsAsync = previousIsAsync;
   ```

3. **`src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`**
   - Add `AwaitExpression awaitExpr => CheckAwaitExpression(awaitExpr),` to `CheckExpression` switch
   - Add method:
   ```csharp
   private SemanticType CheckAwaitExpression(AwaitExpression awaitExpr)
   {
       if (!_currentFunctionIsAsync)
       {
           AddError("'await' can only be used inside 'async def' functions",
               awaitExpr.LineStart, awaitExpr.ColumnStart,
               code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: awaitExpr.Span);
           return SemanticType.Unknown;
       }
       var operandType = CheckExpression(awaitExpr.Operand);
       if (operandType is TaskType taskType)
           return taskType.ResultType ?? SemanticType.Void;
       if (operandType is UnknownType)
           return SemanticType.Unknown;
       AddError($"Cannot await non-Task type '{operandType.GetDisplayName()}'",
           awaitExpr.LineStart, awaitExpr.ColumnStart,
           code: DiagnosticCodes.Semantic.InvalidAwaitOperand, span: awaitExpr.Span);
       return SemanticType.Unknown;
   }
   ```

4. **`src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`**
   - Add in Semantic class (SPY0273, SPY0274 — next available after SPY0272):
   ```csharp
   public const string AwaitOutsideAsync = "SPY0273";
   public const string InvalidAwaitOperand = "SPY0274";
   ```

**Verify:** Build succeeds, existing tests pass.

---

### Commit 3: CodeGen — Emit C# `await`

**Files:**

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`**
   - Add `AwaitExpression awaitExpr => GenerateAwaitExpression(awaitExpr),` to `GenerateExpression` switch
   ```csharp
   private ExpressionSyntax GenerateAwaitExpression(AwaitExpression awaitExpr)
   {
       var operand = GenerateExpression(awaitExpr.Operand);
       return AwaitExpression(operand);
   }
   ```

2. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`**
   - Uncomment `// AwaitExpression => true,` in `IsValidCSharpStatementExpression`

**Verify:** Build succeeds, all tests pass.

---

### Commit 4: Update existing async fixtures to use `await`

Update `async_basic.spy`, `async_class_method.spy`, `async_void.spy` to use `await` instead of `.Result`/`.Wait()`. Entry point `main()` becomes `async def main()` where needed (C# supports `async Task Main()`).

Update corresponding `.expected.cs` snapshots.

**Verify:** All file-based integration tests pass.

---

### Commit 5: Add new `await` test fixtures

New files in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/async/`:

| File | Tests |
|------|-------|
| `await_expression.spy` + `.expected` | `await` in arithmetic: `await get_value() + 1` |
| `await_chained.spy` + `.expected` | Nested: `await add(await get_five(), 3)` |
| `await_void_statement.spy` + `.expected` | `await` as standalone statement |
| `await_outside_async_error.spy` + `.error` | SPY0273: await outside async |
| `await_non_task_error.spy` + `.error` | SPY0274: await non-Task type |

**Verify:** All new and existing tests pass.

---

### Commit 6: Update grammar spec and docs

1. **`docs/language_specification/grammar.ebnf.txt`** — Add `await_expr` production
2. **`docs/language_specification/async_programming.md`** — Mark `await` as implemented
3. **`docs/implementation_planning/phases2.md`** — Mark 10.2 complete

---

## Files Summary

| File | Commit | Change |
|------|--------|--------|
| `Parser/Parser.Expressions.cs` | 1 | Add `ParseAwaitExpression()`, modify `ParseUnary()` chain |
| `Parser/Ast/Expression.Future.cs` | 1 | Remove placeholder comments |
| `Semantic/TypeChecker.cs` | 2 | Add `_currentFunctionIsAsync` field |
| `Semantic/TypeChecker.Definitions.cs` | 2 | Save/set/restore async flag |
| `Semantic/TypeChecker.Expressions.cs` | 2 | Add `CheckAwaitExpression()` |
| `Diagnostics/DiagnosticCodes.cs` | 2 | Add SPY0273, SPY0274 |
| `CodeGen/RoslynEmitter.Expressions.cs` | 3 | Add `GenerateAwaitExpression()` |
| `CodeGen/RoslynEmitter.Statements.cs` | 3 | Uncomment `AwaitExpression => true` |
| `TestFixtures/async/*.spy` | 4-5 | Update existing + add new fixtures |
| `grammar.ebnf.txt`, `async_programming.md`, `phases2.md` | 6 | Doc updates |

## Risks

1. **`async def main()` as entry point** — C# supports `async Task Main()` since 7.1. Verify existing async_void tests pass with async main.
2. **`await` in comprehensions** — Spec says NOT supported. C# compiler will reject, providing safety net. Defer explicit validation.
3. **`await` in lambda** — `_currentFunctionIsAsync` will be `false` in lambda bodies, naturally producing SPY0273.
