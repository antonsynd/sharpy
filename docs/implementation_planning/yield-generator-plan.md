<!-- Re-verified by /project:verify-plan on 2026-02-22 -->
<!-- Verification result: PASS WITH CORRECTIONS (confirmed) -->

# Plan: Yield/Generator Support + Complete `__reversed__` Implementation

## Context

Sharpy's `__reversed__` dunder is partially implemented — the compiler synthesizes `IReverseEnumerable<T>` and generates a `GetReverseEnumerator()` method, but the method body can't actually produce an `IEnumerator<T>` because Sharpy lacks `yield` support. A SPY0457 warning tells users it's "not fully supported."

The root cause is that `__reversed__` (and optionally `__iter__`) are naturally expressed as **generator functions** using `yield`, but Sharpy has no generator support. This plan adds `yield`/`yield from` as a language feature, which unblocks both `__reversed__` and generator-based `__iter__`.

### Design decisions (established in prior discussion)

1. **`yield` in function body is the sole signal** for generator detection — no annotations, no special flags on the AST
2. **Two iteration patterns coexist**: generator (yield) vs explicit iterator (`__next__`), distinguished by yield presence
3. **The compiler never inspects what `__iter__` returns** — no `return self` tracking
4. **Guard rails**: generator `__iter__` + `__next__` = error; `yield` in `__next__` = error; `yield` + `return <value>` = error
5. **Codegen delegates to C#**: `yield x` → `yield return x`, letting Roslyn build the state machine

---

## Commit 1: AST — Add `YieldStatement` node

Add the AST record for yield statements. No functional changes.

### Files to modify

**`src/Sharpy.Compiler/Parser/Ast/Statement.cs`** — Add after `ReturnStatement` (line 164):

```csharp
public record YieldStatement : Statement
{
    /// <summary>The expression to yield. Required (bare yield not supported).</summary>
    public Expression Value { get; init; } = null!;

    /// <summary>True for "yield from iterable" (delegation).</summary>
    public bool IsFrom { get; init; }

    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "YieldStatement.Value cannot be null");
    }

    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Value;
    }
}
```

Follows the `ReturnStatement` pattern with one addition: `ValidateInvariants()` override to assert `Value` is non-null (since `Value` is non-nullable here, unlike `ReturnStatement.Value` which is nullable). [CORRECTED: ReturnStatement does not have ValidateInvariants; the addition is intentional but the "exact" claim was inaccurate.] `Value` is non-nullable (no bare `yield` / send-protocol in v0.1 — note: the grammar spec allows bare `yield` per `yield_expr ::= 'yield' [ expression ]`; this is a deliberate v0.1 restriction). `IsFrom` distinguishes `yield expr` from `yield from expr`.

### Tests
- Build compiles successfully

---

## Commit 2: Parser — Parse `yield` and `yield from`

### Files to modify

**`src/Sharpy.Compiler/Parser/Parser.cs`** (line ~404, in `ParseStatement()` switch, near `TokenType.Return`) — Add:
```csharp
TokenType.Yield => ParseYieldStatement(),
```

**`src/Sharpy.Compiler/Parser/Parser.Statements.cs`** — Add `ParseYieldStatement()` after `ParseReturnStatement()` (after line 511). Follows the same structure: consume `yield` (`TokenType.Yield`), optionally consume `from` (check for `TokenType.From`), parse expression, expect statement end. `yield from` is unambiguous here because `from` as an import keyword only appears at module-level, and inside `ParseYieldStatement()` we've already consumed `yield`.

### Tests
- Parser unit test: `yield 42` → `YieldStatement { Value: IntegerLiteral(42), IsFrom: false }`
- Parser unit test: `yield from items` → `YieldStatement { Value: Identifier("items"), IsFrom: true }`
- Parser error test: bare `yield` without expression → parse error

---

## Commit 3: Semantic — Generator detection and type checking

### Files to modify

**`src/Sharpy.Compiler/Semantic/Symbol.cs`** (line 107, in `FunctionSymbol`) — Add property:
```csharp
public bool IsGenerator { get; internal set; }
```

**`src/Sharpy.Compiler/Semantic/SemanticInfo.cs`** (after line 168) — Add generator tracking:
```csharp
private readonly HashSet<FunctionDef> _generatorFunctions = new(ReferenceEqualityComparer.Instance);
public void MarkAsGenerator(FunctionDef funcDef) => _generatorFunctions.Add(funcDef);
public bool IsGenerator(FunctionDef funcDef) => _generatorFunctions.Contains(funcDef);
```

**`src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`** (line 140, after `ContinueOutsideLoop`) — Add new codes in SPY0265-SPY0269 range:
```csharp
public const string YieldOutsideFunction = "SPY0265";
public const string YieldWithReturn = "SPY0267";
public const string YieldInNext = "SPY0268";
public const string GeneratorIterConflict = "SPY0269";
```
Note: SPY0266 is already taken (`NotAllPathsReturn`).

**`src/Sharpy.Compiler/Semantic/TypeChecker.cs`** (line ~328, in `CheckStatement()`) — Add case:
```csharp
case YieldStatement yieldStmt:
    CheckYield(yieldStmt);
    break;
```

**`src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`** — Add `CheckYield()` after `CheckReturn()` (after line 359):
- For `yield expr`: type-check `Value`, verify yielded type matches function return type annotation (which describes the element type)
- For `yield from expr`: type-check `Value`, call `_typeInference.InferIterableElementType()` to get element type, verify it matches function return type annotation
- If not inside a function (`_currentFunctionReturnType == null`), emit SPY0265

**`src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`** (after line 367, after body checking loop, before restoring context at line 372) — Add generator detection:
```csharp
if (ContainsYield(functionDef.Body))
{
    _semanticInfo.MarkAsGenerator(functionDef);
    // Set IsGenerator on the FunctionSymbol via updatedSymbol pattern
    // (same sync pattern used for ReturnType updates at lines 330-358)
}
```

Add `ContainsYield()` helper: recursively scan statement list for `YieldStatement` nodes, but **do not descend into nested `FunctionDef` nodes** (yield in a nested function makes the nested function a generator, not the outer one). Must handle `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement`, `WithStatement`, `MatchStatement` bodies.

### Tests
- Function with `yield` is marked as generator
- Nested function with `yield` does NOT mark outer function as generator
- `yield` outside function → SPY0265
- `yield "hello"` in function with `-> int` → type mismatch
- `yield from [1, 2, 3]` in function with `-> int` → OK
- `yield from 42` → error (non-iterable)

---

## Commit 4: Validation — Guard rails and CFG updates

### Files to create

**`src/Sharpy.Compiler/Semantic/Validation/GeneratorValidator.cs`** — New validator at Order 155 (after SignatureValidator at 150):
- **Guard 1**: Class has generator `__iter__` (yield in body) AND `__next__` → SPY0269 error
- **Guard 2**: `yield` in `__next__` body → SPY0268 error
- **Guard 3**: `yield` + `return <value>` in same generator function → SPY0267 error (bare `return` OK)
- Uses `context.SemanticInfo.IsGenerator()` to check generator status

### Files to modify

**`src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`** (line 22) — Register:
```csharp
.AddValidator(new GeneratorValidator())  // Order: 155
```

**`src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidator.cs`** (line 90-92) — Skip "missing return" check for generators:
```csharp
if (returnType != SemanticType.Void && !_context.SemanticInfo.IsGenerator(func))
```
Generators produce values via yield; falling off the end is normal.

**`src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs`** (line 200, `default` case) — `YieldStatement` falls through to `AddStatement(stmt)` already (it's a simple statement in CFG terms — it doesn't terminate the block). No explicit case needed, but add one for clarity and to future-proof against the `default` branch changing.

**`src/Sharpy.Compiler/Semantic/Validation/SignatureValidator.cs`** (lines 108-118) — **Remove** the SPY0457 warning block for `__reversed__`. No longer "unsupported."

### Tests
- Generator `__iter__` + `__next__` on same class → SPY0269
- `yield` in `__next__` → SPY0268
- `yield x` + `return 42` in same function → SPY0267
- `yield x` + bare `return` → no error
- Generator without explicit `return` → no "not all paths return" error
- `__reversed__` no longer produces SPY0457

---

## Commit 5: Code generation — `yield return`, `yield break`, generator methods

### Files to modify

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`** (line 54, in the switch) — Add:
```csharp
YieldStatement yieldStmt => GenerateYield(yieldStmt),
```

Add `GenerateYield()`:
- `yield expr` → `SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, GenerateExpression(value))`
- `yield from expr` → `ForEachStatement(var, __yieldItem, expr, Block(YieldStatement(YieldReturnStatement, __yieldItem)))`

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`** (line 208, `GenerateReturn()`) — Modify to handle generators:
```csharp
if (ret.Value == null && _isCurrentMethodGenerator)
    return YieldStatement(SyntaxKind.YieldBreakStatement);
```
Change return type from `ReturnStatementSyntax` to `StatementSyntax`.

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`** — Add tracking field:
```csharp
private bool _isCurrentMethodGenerator;
```

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`** (lines 119-145) — Update dunder dispatch:

For `__iter__` (line 120-139): Add new branch when no `__next__` but function is a generator (check via `_context.SemanticInfo.IsGenerator(funcDef)` or the FunctionSymbol on the owning TypeSymbol). Generate `GetEnumerator()` returning `IEnumerator<T>` with the user's body (which contains yield → yield return). Also generate the non-generic `IEnumerable.GetEnumerator()` bridge.

For `__reversed__` (line 142-144): Set `_isCurrentMethodGenerator = true` before calling existing `GenerateReverseEnumeratorMethod()`, reset after. The existing method already generates the right signature (`IEnumerator<T> GetReverseEnumerator()`); now the body's yield statements will correctly emit as `yield return`.

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` and/or `.ModuleClass.cs`** — For standalone generator functions (not dunders): when generating a module-level or class method that is a generator, wrap return type in `IEnumerable<T>` (standalone generators return `IEnumerable<T>`, not `IEnumerator<T>`). The exact file depends on where module-level function generation dispatches; trace from `GenerateCompilationUnit` or `GenerateModuleClass` to find the method return type emission point.

### Key distinction
| Context | C# return type |
|---------|---------------|
| Standalone generator function | `IEnumerable<T>` |
| Generator `__iter__` | `IEnumerator<T>` (from `GetEnumerator()`) |
| Generator `__reversed__` | `IEnumerator<T>` (from `GetReverseEnumerator()`) |

### How codegen accesses SemanticInfo
The emitter's `_context` is a `CodeGenContext` which has `SemanticInfo? SemanticInfo` property (line 90 of `CodeGen/CodeGenContext.cs`). Access via `_context.SemanticInfo?.IsGenerator(funcDef)`. However, IsGenerator on FunctionDef requires the AST node. In codegen, dunder methods are dispatched from FunctionDef AST nodes, so this is directly available. For the FunctionSymbol-based path, use `FunctionSymbol.IsGenerator`.

### Tests
- Integration: standalone generator function with `yield` → produces correct output
- Integration: `yield from` delegation → produces correct output
- Integration: generator `__iter__` in class → class usable in `for` loop
- Integration: generator `__reversed__` in class → `GetReverseEnumerator()` works

---

## Commit 6: Synthesis — `IEnumerable<T>` for generator `__iter__`

Currently `IEnumerable<T>` is only synthesized when `__next__` is present (SynthesisAnalyzer Phase 2b, line 100). Generator `__iter__` (no `__next__`) needs to also trigger `IEnumerable<T>` synthesis.

### Files to modify

**`src/Sharpy.Compiler/Semantic/SynthesisAnalyzer.cs`** (after line 109, after Phase 2b) — Add Phase 2c:
```csharp
// Phase 2c: IEnumerable<T> from generator __iter__ (without __next__)
if (typeSymbol.ProtocolMethods.TryGetValue(DunderNames.Iter, out var iterOverloads2)
    && !typeSymbol.ProtocolMethods.ContainsKey(DunderNames.Next))
{
    var iterFunc = iterOverloads2.FirstOrDefault();
    if (iterFunc is { IsGenerator: true })
    {
        var elementType = iterFunc.ReturnType is not (UnknownType or VoidType)
            ? iterFunc.ReturnType
            : new UserDefinedType { Name = "object" };
        result.Add(new SynthesizedInterfaceInfo(
            "IEnumerable", "System.Collections.Generic",
            new[] { elementType }, DunderNames.Iter));
    }
}
```

This uses `FunctionSymbol.IsGenerator` (added in Commit 3), which is set during type checking before synthesis runs.

### Tests
- Class with generator `__iter__` → SPY1001 info diagnostic for `IEnumerable<T>`
- Class with generator `__iter__` + `__reversed__` → both interfaces synthesized

---

## Commit 7: Incremental compilation — Persist `IsGenerator` flag

The `IsGenerator` property on `FunctionSymbol` must survive incremental compilation cache round-trips.

### Files to modify

**`src/Sharpy.Compiler/Project/SymbolCache.cs`** (after line 122, after `IsOverride`) — Add to cached symbol record:
```csharp
public bool IsGenerator { get; init; }
```

**`src/Sharpy.Compiler/Project/SymbolSerializer.cs`** (line ~146, in `SerializeFunctionSymbol`, after `IsOverride`) — Add: [CORRECTED: line was 143, actual `IsOverride` serialization is at line 146]
```csharp
IsGenerator = fs.IsGenerator,
```

**`src/Sharpy.Compiler/Project/SymbolSerializer.cs`** (line 385, in `DeserializeFunctionSymbol`, after `IsOverride`) — Add:
```csharp
IsGenerator = cached.IsGenerator,
```

**`src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs`** (line 41) — Bump schema version:
```csharp
internal const int CurrentSchemaVersion = 4;  // was 3
```

### Tests
- Incremental compilation test: generator function survives cache round-trip with IsGenerator preserved

---

## Commit 8: Integration tests and cleanup

Comprehensive file-based integration tests. Create `generators/` subdirectory in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`.

### Test fixtures to create

| File | Tests | Expected |
|------|-------|----------|
| `generators/yield_basic.spy` + `.expected` | Standalone generator with `yield` in a loop | `0\n1\n2\n3\n4\n` |
| `generators/yield_from.spy` + `.expected` | `yield from` delegation between generators | `0\n1\n2\n3\n` |
| `generators/generator_iter.spy` + `.expected` | Class with generator `__iter__` used in `for` | `3\n2\n1\n` |
| `generators/generator_reversed.spy` + `.expected` | Class with both generator `__iter__` and `__reversed__` | Forward iteration output |
| `generators/yield_early_return.spy` + `.expected` | Bare `return` in generator (early exit → `yield break`) | First N items |
| `generators/yield_return_value_error.spy` + `.error` | `yield` + `return 42` in same function | `cannot use 'return' with a value` |
| `generators/yield_in_next_error.spy` + `.error` | `yield` inside `__next__` | `cannot contain 'yield'` |
| `generators/generator_iter_conflict.spy` + `.error` | Generator `__iter__` + `__next__` on same class | `generator '__iter__'` |
| `generators/reversed_generator.spy` + `.expected` + empty `.warning` | `__reversed__` with yield — no SPY0457 | Reversed output |

### Existing test to update

**`src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/reversed_unsupported_warning.*`** — SPY0457 is removed. This fixture has 3 files (`.spy`, `.error`, `.warning`). The `.warning` file checks for "not fully supported" and the `.error` file expects a C# compilation error (`Cannot implicitly convert type 'int' to 'IEnumerator<int>'`). Delete or rework all three files. [CORRECTED: specified all 3 files]

---

## Known constraint: C# `yield` in `try`/`catch`

C# does not allow `yield return` inside a `try` block that has a `catch` clause. If a Sharpy user writes `yield` inside `try`/`except`, the generated C# will fail. This should be addressed as a follow-up validation rule in `GeneratorValidator` (emit a clear error rather than letting the C# compilation fail with an opaque message). This can be done in the same Commit 4 or as a fast follow-up.

---

## Verification

```bash
# Build
dotnet build sharpy.sln

# Run all tests
dotnet test

# Run generator-specific tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Manual verification: standalone generator
dotnet run --project src/Sharpy.Cli -- run snippets/generator_test.spy

# Manual verification: generator __iter__
dotnet run --project src/Sharpy.Cli -- emit csharp snippets/generator_iter_test.spy

# Verify no regressions in existing iteration tests
dotnet test --filter "DisplayName~iter"
dotnet test --filter "DisplayName~reversed"
```

---

## File index

| File | Commits | Change type |
|------|---------|-------------|
| `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | 1 | Add `YieldStatement` record |
| `src/Sharpy.Compiler/Parser/Parser.cs` | 2 | Add `TokenType.Yield` dispatch |
| `src/Sharpy.Compiler/Parser/Parser.Statements.cs` | 2 | Add `ParseYieldStatement()` |
| `src/Sharpy.Compiler/Semantic/Symbol.cs` | 3 | Add `IsGenerator` to `FunctionSymbol` |
| `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` | 3 | Add generator tracking |
| `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` | 3 | Add SPY0265/0267/0268/0269 |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | 3 | Add `YieldStatement` case |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` | 3 | Add `CheckYield()` |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs` | 3 | Generator detection + `ContainsYield()` |
| `src/Sharpy.Compiler/Semantic/Validation/GeneratorValidator.cs` | 4 | **New file** — guard rails |
| `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs` | 4 | Register `GeneratorValidator` |
| `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidator.cs` | 4 | Skip missing-return for generators |
| `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs` | 4 | Add `YieldStatement` case |
| `src/Sharpy.Compiler/Semantic/Validation/SignatureValidator.cs` | 4 | Remove SPY0457 warning |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | 5 | Add `_isCurrentMethodGenerator` field |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` | 5 | Add `GenerateYield()`, modify `GenerateReturn()` |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | 5 | Generator dispatch for `__iter__`/`__reversed__` |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` | 5 | Standalone generator return type wrapping |
| `src/Sharpy.Compiler/Semantic/SynthesisAnalyzer.cs` | 6 | Phase 2c: `IEnumerable<T>` for generator `__iter__` |
| `src/Sharpy.Compiler/Project/SymbolCache.cs` | 7 | Add `IsGenerator` to cached symbol |
| `src/Sharpy.Compiler/Project/SymbolSerializer.cs` | 7 | Serialize/deserialize `IsGenerator` |
| `src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs` | 7 | Bump schema version to 4 |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/generators/` | 8 | **New directory** — 9 test fixtures |

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS (confirmed on re-verification)
**Verified on:** 2026-02-22 (re-verified 2026-02-22)
**Plan file:** `~/.claude/plans/playful-tumbling-quokka.md`

### Corrections Made

1. **Commit 1: "exact ReturnStatement pattern" claim** — `ReturnStatement` does NOT have `ValidateInvariants()`, but the proposed `YieldStatement` includes it. Corrected the claim to note this is an intentional addition, not an exact copy. Also noted that the grammar spec allows bare `yield` (`'yield' [ expression ]`), making the v0.1 restriction a deliberate design choice.

2. **Commit 7: `SymbolSerializer.cs` line reference** — Plan said "line 143" but `IsOverride` serialization (the natural insertion point) is at line 146. Corrected to "line ~146".

3. **Commit 2: `from` token type** — Added explicit mention that `from` is `TokenType.From` (a keyword token) in `ParseYieldStatement()`.

4. **Commit 8: `reversed_unsupported_warning` fixture** — Plan said `.*` without listing files. Corrected to specify all 3 files (`.spy`, `.error`, `.warning`) and their contents.

5. **Commit 5: Standalone generator codegen location** — Plan said "`.CompilationUnit.cs` or `.ModuleClass.cs`" ambiguously. Added guidance to trace from entry points.

### Warnings

1. **Lexer step not explicitly acknowledged** — The feature implementation order says Lexer→Parser→...→Tests. The plan starts at AST/Parser because `TokenType.Yield` already exists (verified: `Lexer/Token.cs:69` and `Lexer/Lexer.cs:118`). This is correct but should be noted for traceability.

2. **`yield` in comprehensions/lambdas not discussed** — Python forbids `yield` in lambdas but allows generator expressions. The plan's `ContainsYield()` helper (Commit 3) only scans statement lists, which naturally excludes lambda bodies (since they're expressions, not statement-bodied in Sharpy). However, if comprehensions can contain `yield`, this needs a guard. Consider adding a validation rule or noting this as out-of-scope for v0.1.

3. **No return type annotation on generator** — The plan says yielded type should match the "function return type annotation (which describes the element type)" but doesn't specify what happens when there's no type annotation. Should default to `object` (consistent with SynthesisAnalyzer's fallback) or infer from yield expressions.

4. **`_isCurrentMethodGenerator` naming** — Existing emitter fields use patterns like `_isInAbstractClass`, `_forceModuleLevelFields`. The proposed `_isCurrentMethodGenerator` is reasonable but `_isInGeneratorMethod` might be more consistent. Non-blocking.

5. **C# `yield` in `try`/`catch` constraint** — Plan mentions this in "Known constraint" section. Good. This should be implemented as part of Commit 4's `GeneratorValidator` to avoid opaque C# compilation errors reaching users.

### Missing Steps Added

1. **Grammar spec update** — Consider updating `docs/language_specification/grammar.ebnf.txt` if the v0.1 restriction (no bare `yield`) differs from the existing grammar. Currently the grammar allows `'yield' [ expression ]` — either update the grammar to match v0.1 or add a note.

2. **Language spec cross-reference** — The `dunder_methods_recommendations.md` already shows `yield` examples in `__iter__`. No spec updates needed for that.

### Unchecked Claims

1. **`_typeInference.InferIterableElementType()` return behavior for non-iterables** — Verified the method exists (`TypeInferenceService.cs:597`), but did not trace its implementation to confirm it returns `null` for non-iterable types (e.g., `int`). Assumed correct based on usage patterns in `TypeChecker.Statements.cs:475`.

### Verified Claims (all confirmed)

- All 22 file paths exist in the codebase
- `TokenType.Yield` already exists (`Lexer/Token.cs:69`, `Lexer/Lexer.cs:118`)
- SPY0265 is available (gap between SPY0264 and SPY0266, noted in audit)
- SPY0266 (`NotAllPathsReturn`) exists, confirming SPY0266 is taken
- `FunctionSymbol` line 107 is `IsOverride` — correct insertion point
- `SymbolCache.cs` line 122 is `IsOverride` — correct insertion point
- `IncrementalCompilationCache.cs` line 41 is `CurrentSchemaVersion = 3` — correct
- `SynthesisAnalyzer.cs` Phase 2b ends at line 109 — correct insertion point
- `ControlFlowValidator` has `_context.SemanticInfo` access via `SemanticContext`
- `CodeGenContext.SemanticInfo` is nullable (`SemanticInfo?`) — plan's `?.` usage is correct
- `GenerateReverseEnumeratorMethod()` exists at `RoslynEmitter.ClassMembers.cs:791`
- `ValidationPipelineFactory.cs` registration pattern confirmed (line 17-34)
- All diagnostic code references verified against `DiagnosticCodes.cs`
