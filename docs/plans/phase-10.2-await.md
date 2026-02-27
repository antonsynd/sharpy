<!-- Verified by /verify-plan on 2026-02-27 (re-verified x2) -->
<!-- Verification result: PASS WITH CORRECTIONS -->

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
           var operand = ParsePower();  // operand is power (matches Python: 'await' primary)

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
   - In `CheckFunction`, save/set/restore `_currentFunctionIsAsync`: [CORRECTED: method is `CheckFunction`, not `CheckFunctionDef`]
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

No `.expected.cs` snapshots exist for these fixtures — only `.expected` (runtime output) files need updating. [CORRECTED: async fixtures have no `.expected.cs` snapshot files]

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
3. **`await` in lambda** — `CheckLambda()` does NOT save/restore `_currentFunctionIsAsync`, so it inherits the enclosing function's value. `await` inside a lambda within an `async def` would be silently accepted, NOT rejected. To match Python semantics (where `await` in lambda is always a SyntaxError), either (a) save/restore `_currentFunctionIsAsync = false` in `CheckLambda`, or (b) add an explicit check in `CheckAwaitExpression`. Consider adding an `await_in_lambda_error` test fixture. [CORRECTED: `_currentFunctionIsAsync` is inherited, not reset to `false` in lambdas]

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-02-27 (re-verified x2)
**Plan file:** `docs/plans/phase-10.2-await.md`

### Corrections Made

1. **Commit 4 — `.expected.cs` snapshots**: Plan originally said "Update corresponding `.expected.cs` snapshots" but no async fixtures have `.expected.cs` files. Corrected to note only `.expected` runtime output files need updating. (Note: `async_basic.cs` and `async_class_method.cs` exist as compiled C# files in the directory — these are NOT `.expected.cs` snapshot files and appear to be leftover from an older convention.)

2. **Risk #3 — `await` in lambda**: Plan claimed `_currentFunctionIsAsync` would be `false` in lambda bodies, naturally rejecting `await`. This is incorrect — `CheckLambda()` (TypeChecker.Expressions.Access.cs:1628) does NOT save/restore ANY context flags (`_currentFunctionIsGenerator`, `_currentMethodIsOverride`, `_currentMethodIsDunder`, etc.), so the async flag would be inherited from the enclosing function. Corrected with mitigation options. Python `compile()` confirms: `await` in lambda produces `SyntaxError: 'await' outside async function`.

3. **Commit 1 — `ParseAwaitExpression` operand**: Plan originally used `ParseAwaitExpression()` (right-recursive) for the operand, which would allow `await await x`. Python rejects this as a syntax error (`await primary`, not `await await_primary`). [CORRECTED: changed operand to `ParsePower()` to match Python semantics. If C#-style `await await task_of_task` is needed later, it can be added with an explicit parenthesized form `await (await x)`.]

4. **Commit 2 — method name**: Plan referenced `CheckFunctionDef` but the actual method is `CheckFunction` (TypeChecker.Definitions.cs:15). Corrected inline.

### Warnings

1. **Commit 2 — save/restore placement**: `CheckFunction` (TypeChecker.Definitions.cs:15-472) [CORRECTED: method is `CheckFunction`, not `CheckFunctionDef`] already has significant async validation: async generator rejection (line 373), async constructor rejection (line 383), and async return type wrapping (lines 438-457). The `_currentFunctionIsAsync` save/set must be placed alongside `_currentFunctionIsGenerator` at lines 368-370 (BEFORE the body-checking loop at line 393) and the restore must go in the existing restore block at lines 463-468 where `_currentFunctionIsGenerator` and 5 other context fields are already restored.

2. **Diagnostic code range**: SPY0273/SPY0274 fall within the "Return and control flow (SPY0260-SPY0279)" sub-range. `AwaitOutsideAsync` fits well there, but `InvalidAwaitOperand` is more of a type checking concern (SPY0220-SPY0259). Keeping both await codes together is reasonable but worth noting.

### Missing Steps Added

1. **Lambda await rejection**: Add save/restore of `_currentFunctionIsAsync = false` in `CheckLambda()` (TypeChecker.Expressions.Access.cs:1628), or add explicit lambda context check in `CheckAwaitExpression()`. Add test fixture `await_in_lambda_error.spy` + `.error`.

### Unchecked Claims

- None. All file paths, method names, diagnostic codes, AST nodes, Roslyn API usage, spec references, and Python semantics claims were verified against the codebase (including Python `await` behavior via `python3`).
