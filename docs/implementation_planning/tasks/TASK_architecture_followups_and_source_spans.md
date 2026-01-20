# Architecture Follow-ups and Source Span Migration

**Status:** Ready for Implementation  
**Assignee:** Junior Engineer / Claude Sonnet  
**Estimated Effort:** 3-5 days  
**Prerequisites:** All existing tests passing  

---

## Overview

This task list covers:
1. **CodeGenInfo Emitter Migration** - Wire up the pre-computed CodeGenInfo in RoslynEmitter
2. **Validation Pipeline Cleanup** - Remove legacy dual-path code
3. **Source Span Migration** (Rec #10) - Complete TextSpan population across AST nodes

### Guiding Principles

- **Test-driven:** Run tests after each checkpoint; all existing passing tests must continue to pass
- **Two-way door preferred:** Changes should be reversible where possible
- **Incremental commits:** Commit at each major step for easy revert/bisection
- **No breaking changes:** The compiler should produce identical output before and after (except for better error locations)

### Long-term Considerations

These changes enable future features:
- **LSP (v0.2.x+):** Requires precise source locations for hover, go-to-definition, error squiggles
- **Async/Await (v0.2.x+):** CFG construction benefits from accurate spans
- **Tagged Unions (v0.2.x+):** Exhaustive pattern matching errors need precise locations
- **Debugger Support:** PDB generation requires accurate source mapping

---

## Pre-flight Checklist

Before starting, ensure your environment is ready:

- [ ] Clone latest `main` branch
- [ ] Run `dotnet build sharpy.sln` - should succeed
- [ ] Run `dotnet test` - record baseline: `_____ tests passed, _____ failed, _____ skipped`
- [ ] Note any pre-existing failing tests (do not fix these as part of this task)

```bash
# Save baseline test results
dotnet test --logger "console;verbosity=normal" 2>&1 | tee baseline_tests.log
grep -E "(Passed|Failed|Skipped)" baseline_tests.log | tail -5
```

---

## Part 1: CodeGenInfo Emitter Migration

**Goal:** Migrate RoslynEmitter to use pre-computed CodeGenInfo instead of runtime tracking sets.

> **Note:** The infrastructure (CodeGenInfo, CodeGenInfoComputer, helper methods) already exists. This task wires up the actual emission code to use it.

### 1.1 Audit Current Helper Methods

- [ ] Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- [ ] Locate existing helper methods (search for `GetCSharpNameForSymbol`):
  - `GetCSharpNameForSymbol(Symbol symbol)` 
  - `IsModuleLevelConstant(string name)`
  - `IsModuleLevelVariable(string name)`
  - `HasExecutionOrderIssues(string name)`
  - `IsFromImportSymbol(string name)`
  - `GetOriginalImportName(string name)`
- [ ] Verify each helper has fallback logic for when `CodeGenInfo` is null
- [ ] If any helpers are missing, they may have been implemented already - check `RoslynEmitter*.cs` files

### 1.2 Create Migration Utility Method

Add a utility method that allows gradual migration with runtime comparison:

```csharp
// Add to RoslynEmitter.cs (temporary, for migration validation)
#if DEBUG
private void ValidateCodeGenInfoMatch(string symbolName, string legacyResult, string codeGenResult, string context)
{
    if (legacyResult != codeGenResult)
    {
        _context.Logger?.LogWarning(
            $"CodeGenInfo mismatch for '{symbolName}' in {context}: " +
            $"legacy='{legacyResult}', codeGen='{codeGenResult}'");
    }
}
#endif
```

- [ ] Add the validation method above (only in DEBUG builds)
- [ ] Run `dotnet build` - should succeed
- [ ] Commit: `git commit -m "feat(codegen): Add CodeGenInfo migration validation helper"`

### 1.3 Migrate Variable Name Resolution

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (or `RoslynEmitter.Expressions.cs`)

Find usages of `GetMangledVariableName()` and related variable name logic:

- [ ] Search for `_declaredVariables` usage
- [ ] Search for `_variableVersions` usage  
- [ ] Search for `GetMangledVariableName` calls

For each usage site, update to try CodeGenInfo first:

```csharp
// BEFORE (example):
var csharpName = GetMangledVariableName(varName);

// AFTER (with validation):
var symbol = _context.LookupSymbol(varName);
var csharpNameFromCodeGen = symbol != null ? GetCSharpNameForSymbol(symbol) : null;
var csharpNameLegacy = GetMangledVariableName(varName);

#if DEBUG
if (csharpNameFromCodeGen != null)
    ValidateCodeGenInfoMatch(varName, csharpNameLegacy, csharpNameFromCodeGen, "variable name");
#endif

var csharpName = csharpNameFromCodeGen ?? csharpNameLegacy;
```

- [ ] Update 2-3 usage sites as a pilot
- [ ] Run `dotnet test --filter "FullyQualifiedName~CodeGen"` - should pass
- [ ] Run full test suite: `dotnet test` - should have same pass/fail count as baseline
- [ ] Commit: `git commit -m "feat(codegen): Migrate variable name resolution to CodeGenInfo (pilot)"`

### 1.4 Migrate Module-Level Detection

Update checks for module-level variables/constants:

- [ ] Find usages of `_moduleVariables.Contains(...)` 
- [ ] Find usages of `_moduleConstVariables.Contains(...)`
- [ ] Replace with `IsModuleLevelVariable()` and `IsModuleLevelConstant()` helpers (which check CodeGenInfo)

```csharp
// BEFORE:
if (_moduleConstVariables.Contains(name))

// AFTER:
if (IsModuleLevelConstant(name))
```

- [ ] Update all module-level detection sites
- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(codegen): Migrate module-level detection to CodeGenInfo"`

### 1.5 Migrate Import Symbol Handling

Update from-import symbol detection:

- [ ] Find usages of `_fromImportSymbols.Contains(...)`
- [ ] Find usages of `_importAliasToOriginal[...]`
- [ ] Replace with `IsFromImportSymbol()` and `GetOriginalImportName()` helpers

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(codegen): Migrate import symbol handling to CodeGenInfo"`

### 1.6 Migrate Execution Order Detection

- [ ] Find usages of `_variablesWithExecutionOrderIssues.Contains(...)`
- [ ] Replace with `HasExecutionOrderIssues()` helper

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(codegen): Migrate execution order detection to CodeGenInfo"`

### 1.7 Add Deprecation Markers to Legacy Fields

Once migration is validated, mark legacy fields as obsolete:

```csharp
[Obsolete("Use CodeGenInfo via GetCSharpNameForSymbol(). Will be removed in v0.2.")]
private readonly HashSet<string> _declaredVariables = new();

[Obsolete("Use CodeGenInfo.Version. Will be removed in v0.2.")]
private readonly Dictionary<string, int> _variableVersions = new();

// ... etc for other fields
```

- [ ] Add `[Obsolete]` attributes to all legacy tracking fields
- [ ] Run `dotnet build` - note warnings (expected)
- [ ] Run tests: `dotnet test` - should still pass
- [ ] Commit: `git commit -m "chore(codegen): Mark legacy tracking fields as obsolete"`

### 1.8 Remove DEBUG Validation Code (Optional)

Once confident in the migration:

- [ ] Remove `ValidateCodeGenInfoMatch` calls
- [ ] Remove the method itself
- [ ] Commit: `git commit -m "chore(codegen): Remove migration validation code"`

**Checkpoint:** All tests passing. CodeGenInfo is now the primary source for code generation metadata.

---

## Part 2: Validation Pipeline Cleanup

**Goal:** Remove dual-path code now that the validation pipeline is the default.

### 2.1 Verify Pipeline Is Default

- [ ] Open `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- [ ] Find the constructor that accepts `ValidationPipeline?`
- [ ] Verify that when `null` is passed, a default pipeline is created:

```csharp
// Should look like:
_validationPipeline = validationPipeline ?? ValidationPipelineFactory.CreateDefault(logger);
```

- [ ] If not, update the constructor to create default pipeline when none provided
- [ ] Run tests: `dotnet test`
- [ ] Commit if changes made: `git commit -m "feat(semantic): Make validation pipeline the default"`

### 2.2 Remove `_usePipeline` Field

- [ ] Search for `_usePipeline` in TypeChecker.cs
- [ ] If it exists and is always `true`, remove the field
- [ ] Remove all `if (_usePipeline)` / `if (!_usePipeline)` conditionals
- [ ] Keep only the pipeline code path

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "refactor(semantic): Remove _usePipeline dual-path code"`

### 2.3 Clean Up Error Aggregation

- [ ] Find the `Errors` property getter in TypeChecker.cs
- [ ] Remove any code that aggregates from legacy validators when not using pipeline
- [ ] The getter should only return errors from `_errors` and `_typeResolver.Errors`
  - V2 validator errors flow through the pipeline's DiagnosticBag

```csharp
// Target state:
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);
        // Pipeline errors are merged in CheckModule via DiagnosticBag
        return allErrors;
    }
}
```

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "refactor(semantic): Simplify TypeChecker.Errors aggregation"`

### 2.4 Update SemanticContext Legacy Properties

- [ ] Open `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`
- [ ] Verify legacy state properties have `[Obsolete]` attributes:
  - `CurrentClass`
  - `CurrentFunction`
  - `InLoop`
  - `LoopDepth`
- [ ] If not marked obsolete, add attributes pointing to `Traversal.*` equivalents
- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "chore(semantic): Mark SemanticContext legacy properties as obsolete"`

**Checkpoint:** Validation pipeline is the only code path. No dual-mode complexity.

---

## Part 3: Source Span Migration (Recommendation #10)

**Goal:** Populate `TextSpan` on all AST nodes for precise source location tracking.

### Architecture Decision: Span Population Strategy

**Decision:** Populate spans in the Parser during AST construction (not retroactively).

**Rationale:**
- The Lexer already tracks token positions via `Token.Position`
- Parser has access to start and end tokens for each construct
- This is a **one-way door** (changing later would require parser rewrite), but:
  - It's the same approach used by Roslyn, TypeScript, and other production compilers
  - Enables future incremental parsing
  - Required for LSP anyway

### 3.1 Audit Current Span Infrastructure

- [ ] Verify `TextSpan` exists: `src/Sharpy.Compiler/Text/TextSpan.cs`
- [ ] Verify `ILocatable` exists: `src/Sharpy.Compiler/Text/ILocatable.cs`
- [ ] Verify `Node` base class has `Span` property: `src/Sharpy.Compiler/Parser/Ast/Node.cs`
- [ ] Verify `Token` has position tracking: check for `Position` property and `GetSpan()` method
- [ ] Check `source_span_migration_status.md` for current state

### 3.2 Create Parser Helper Methods (If Not Present)

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

Check if these helpers exist; if not, add them:

```csharp
/// <summary>
/// Create a TextSpan from a single token.
/// </summary>
private TextSpan? GetSpanFromToken(Token token)
{
    if (token.Position < 0) return null;
    return new TextSpan(token.Position, token.Value?.Length ?? 0);
}

/// <summary>
/// Create a TextSpan covering multiple tokens (start to end inclusive).
/// </summary>
private TextSpan? GetSpanFromTokens(Token startToken, Token endToken)
{
    if (startToken.Position < 0 || endToken.Position < 0) return null;
    var endLength = endToken.Value?.Length ?? 0;
    return TextSpan.FromBounds(startToken.Position, endToken.Position + endLength);
}

/// <summary>
/// Create a TextSpan covering from a start token to an AST node's end.
/// Useful when the end is another node, not a token.
/// </summary>
private TextSpan? GetSpanFromTokenToNode(Token startToken, Node endNode)
{
    if (startToken.Position < 0 || endNode.Span == null) return null;
    return TextSpan.FromBounds(startToken.Position, endNode.Span.Value.End);
}

/// <summary>
/// Create a TextSpan covering multiple nodes.
/// </summary>
private TextSpan? GetSpanFromNodes(Node startNode, Node endNode)
{
    if (startNode.Span == null || endNode.Span == null) return null;
    return startNode.Span.Value.Union(endNode.Span.Value);
}
```

- [ ] Add missing helper methods
- [ ] Run `dotnet build`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan helper methods for AST construction"`

### 3.3 Phase A: High-Priority Expressions (Error-Frequent)

These nodes appear most often in error messages. Prioritize them.

#### 3.3.1 Literals

**Files:** `src/Sharpy.Compiler/Parser/Parser.cs` (look for `Parse*Literal` methods)

- [ ] `IntegerLiteral` - set `Span` from the integer token
- [ ] `FloatLiteral` - set `Span` from the float token
- [ ] `StringLiteral` - set `Span` from the string token
- [ ] `BooleanLiteral` - set `Span` from `True`/`False` token
- [ ] `NoneLiteral` - set `Span` from `None` token

Example pattern:
```csharp
// In ParsePrimaryExpression or similar:
case TokenType.Integer:
    var intToken = Advance();
    return new IntegerLiteral 
    { 
        Value = ParseIntValue(intToken),
        LineStart = intToken.Line,
        ColumnStart = intToken.Column,
        Span = GetSpanFromToken(intToken)  // ADD THIS
    };
```

- [ ] Run tests: `dotnet test --filter "FullyQualifiedName~Parser"`
- [ ] Run full tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to literal expressions"`

#### 3.3.2 Call Expressions

- [ ] Find `FunctionCall` parsing (likely `ParseCallExpression` or `ParsePostfixExpression`)
- [ ] Set `Span` from function name/expression start to closing parenthesis

```csharp
return new FunctionCall
{
    Function = callee,
    Arguments = args,
    // ... existing properties ...
    Span = GetSpanFromTokens(callStartToken, closingParenToken)
};
```

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to FunctionCall"`

#### 3.3.3 Member Access

- [ ] Find `MemberAccess` parsing
- [ ] Set `Span` from object start to member name end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to MemberAccess"`

#### 3.3.4 Index Access

- [ ] Find `IndexAccess` parsing  
- [ ] Set `Span` from object start to closing bracket

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to IndexAccess"`

#### 3.3.5 Binary and Unary Operators

- [ ] Find `BinaryOp` parsing (likely in expression parsing with precedence)
- [ ] Set `Span` from left operand start to right operand end:
  ```csharp
  Span = GetSpanFromNodes(left, right)
  ```

- [ ] Find `UnaryOp` parsing
- [ ] Set `Span` from operator token to operand end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to BinaryOp and UnaryOp"`

**Checkpoint:** High-priority expressions have spans. Run full test suite.

### 3.4 Phase B: Statements

#### 3.4.1 Simple Statements

- [ ] `ReturnStatement` - from `return` keyword to value end (or just keyword if no value)
- [ ] `BreakStatement` - from `break` keyword
- [ ] `ContinueStatement` - from `continue` keyword
- [ ] `PassStatement` - from `pass` keyword
- [ ] `AssertStatement` - from `assert` keyword to expression end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to simple statements"`

#### 3.4.2 Variable Declaration and Assignment

- [ ] `VariableDeclaration` - from name to initializer end (or type annotation end if no init)
- [ ] `Assignment` - from target start to value end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to VariableDeclaration and Assignment"`

#### 3.4.3 Control Flow Statements

- [ ] `IfStatement` - from `if` keyword to last body statement/else end
- [ ] `WhileStatement` - from `while` keyword to body end
- [ ] `ForStatement` - from `for` keyword to body end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to control flow statements"`

#### 3.4.4 Exception Handling

- [ ] `TryStatement` - from `try` keyword to finally/last handler end
- [ ] `RaiseStatement` - from `raise` keyword to exception expression end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to exception statements"`

**Checkpoint:** All statements have spans. Run full test suite.

### 3.5 Phase C: Definitions

#### 3.5.1 Function Definition

- [ ] `FunctionDef` - from decorators (if any) or `def` keyword to body end

Consider: Functions can have decorators that precede them:
```python
@decorator
def foo():
    pass
```

The span should start at the first decorator if present.

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to FunctionDef"`

#### 3.5.2 Class, Struct, Interface, Enum Definitions

- [ ] `ClassDef` - from decorators/`class` keyword to body end
- [ ] `StructDef` - from `struct` keyword to body end
- [ ] `InterfaceDef` - from `interface` keyword to body end
- [ ] `EnumDef` - from `enum` keyword to body end

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to type definitions"`

### 3.6 Phase D: Remaining Expressions

#### 3.6.1 Collection Literals

- [ ] `ListLiteral` - from `[` to `]`
- [ ] `DictLiteral` - from `{` to `}`
- [ ] `SetLiteral` - from `{` to `}`
- [ ] `TupleLiteral` - from `(` to `)` or first to last element if no parens

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to collection literals"`

#### 3.6.2 Comprehensions

- [ ] `ListComprehension` - from `[` to `]`
- [ ] `DictComprehension` - from `{` to `}`
- [ ] `SetComprehension` - from `{` to `}`

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to comprehensions"`

#### 3.6.3 Other Expressions

- [ ] `ConditionalExpression` (ternary) - from true value to false value
- [ ] `LambdaExpression` - from `lambda` keyword to body end
- [ ] `Parenthesized` - from `(` to `)`
- [ ] `FStringLiteral` - from `f"` to closing quote
- [ ] `SliceAccess` - from object to `]`

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to remaining expressions"`

### 3.7 Phase E: Import Statements and Type Annotations

#### 3.7.1 Import Statements

- [ ] `ImportStatement` - from `import` keyword to last module name
- [ ] `FromImportStatement` - from `from` keyword to last imported name

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to import statements"`

#### 3.7.2 Type Annotations

Type annotations are parsed in various contexts. Check for `TypeAnnotation` subclasses:

- [ ] `SimpleTypeAnnotation` - from type name token
- [ ] `GenericTypeAnnotation` - from name to closing `]`
- [ ] `NullableTypeAnnotation` - from inner type to `?`
- [ ] `FunctionTypeAnnotation` - from `(` to return type end
- [ ] `TupleTypeAnnotation` - from `(` to `)`

- [ ] Run tests: `dotnet test`
- [ ] Commit: `git commit -m "feat(parser): Add TextSpan to type annotations"`

**Checkpoint:** All AST nodes have spans populated. Run full test suite.

### 3.8 Update Migration Status Document

- [ ] Open `docs/implementation_planning/source_span_migration_status.md`
- [ ] Update all checkboxes to reflect completed work
- [ ] Add any new node types discovered during implementation
- [ ] Commit: `git commit -m "docs: Update source span migration status"`

### 3.9 Add Span Tests

Create tests verifying spans are populated correctly:

**File:** `src/Sharpy.Compiler.Tests/Parser/SpanTests.cs`

```csharp
public class SpanTests
{
    [Fact]
    public void IntegerLiteral_HasCorrectSpan()
    {
        var source = "42";
        var module = Parse(source);
        var expr = GetFirstExpression(module);
        
        Assert.NotNull(expr.Span);
        Assert.Equal(0, expr.Span.Value.Start);
        Assert.Equal(2, expr.Span.Value.Length);
    }

    [Fact]
    public void BinaryOp_SpanCoversOperands()
    {
        var source = "1 + 2";
        var module = Parse(source);
        var binOp = GetFirstExpression(module) as BinaryOp;
        
        Assert.NotNull(binOp?.Span);
        Assert.Equal(0, binOp.Span.Value.Start);
        Assert.Equal(5, binOp.Span.Value.Length);
    }

    [Fact]
    public void FunctionCall_SpanIncludesParens()
    {
        var source = "foo(1, 2)";
        var module = Parse(source);
        var call = GetFirstExpression(module) as FunctionCall;
        
        Assert.NotNull(call?.Span);
        Assert.Equal(0, call.Span.Value.Start);
        Assert.Equal(9, call.Span.Value.Length);
    }
    
    // ... more tests for each node type
}
```

- [ ] Create comprehensive span tests (at least one per category)
- [ ] Run tests: `dotnet test --filter "FullyQualifiedName~SpanTests"`
- [ ] Commit: `git commit -m "test(parser): Add comprehensive TextSpan tests"`

---

## Part 4: Final Verification

### 4.1 Full Test Suite Verification

- [ ] Run complete test suite: `dotnet test`
- [ ] Compare with baseline recorded at start
- [ ] All previously passing tests should still pass
- [ ] New tests (span tests) should pass

```bash
dotnet test --logger "console;verbosity=normal" 2>&1 | tee final_tests.log
diff <(grep -E "(Passed|Failed)" baseline_tests.log | sort) \
     <(grep -E "(Passed|Failed)" final_tests.log | sort)
```

### 4.2 Build Verification

- [ ] Clean and rebuild: `dotnet clean && dotnet build`
- [ ] No new warnings (except expected obsolete warnings from Part 1)

### 4.3 Integration Test

Compile a sample project and verify error messages have better locations:

```bash
# Create a test file with an intentional error
echo 'x = undefined_var + 1' > /tmp/test.spy
dotnet run --project src/Sharpy.Cli -- compile /tmp/test.spy 2>&1
# Error message should show precise location
```

- [ ] Error messages reference correct line/column
- [ ] (Future: Once semantic layer uses spans, error positions will be more precise)

### 4.4 Final Commit

- [ ] Review all changes: `git diff main...HEAD --stat`
- [ ] Ensure no unintended changes
- [ ] Final commit message summarizing the work:

```bash
git commit --allow-empty -m "feat: Complete architecture follow-ups and source span migration

- Migrated RoslynEmitter to use pre-computed CodeGenInfo
- Removed validation pipeline dual-path code  
- Populated TextSpan on all AST nodes for precise source locations

This enables future LSP support, better error messages, and debugger integration.

Relates to: Architecture Recommendations #3, #4, #5, #10"
```

---

## Appendix A: Troubleshooting

### Tests Fail After Span Changes

**Symptom:** Tests that check AST structure fail because `Span` is now populated.

**Fix:** Tests should not break due to new `Span` values. If they do:
1. Check if tests are using strict equality on AST nodes
2. `Span` is optional (nullable), so existing comparisons should work
3. Update tests only if they explicitly expected `Span = null`

### Lexer Position Not Tracked

**Symptom:** `GetSpanFromToken()` returns `null` because `Token.Position` is `-1`.

**Fix:** The Lexer must set `Token.Position`. Check:
1. `src/Sharpy.Compiler/Lexer/Lexer.cs` 
2. Ensure `_position` is tracked and assigned to tokens
3. See existing implementation in `Token.cs`

### Overlapping/Invalid Spans

**Symptom:** Spans have negative length or don't cover expected range.

**Fix:** 
1. Verify start/end token order
2. Use `TextSpan.FromBounds()` which validates bounds
3. Log suspicious spans during development

---

## Appendix B: Files Modified Summary

| File | Part | Changes |
|------|------|---------|
| `RoslynEmitter.cs` | 1 | Use CodeGenInfo helpers, deprecate tracking fields |
| `TypeChecker.cs` | 2 | Remove dual-path, simplify Errors getter |
| `SemanticContext.cs` | 2 | Mark legacy properties obsolete |
| `Parser.cs` | 3 | Add span helpers, populate spans on all nodes |
| `Ast/Node.cs` | 3 | (Already has Span property) |
| `Ast/Expression.cs` | 3 | (Inherits Span from Node) |
| `Ast/Statement.cs` | 3 | (Inherits Span from Node) |
| `source_span_migration_status.md` | 3 | Update completion status |
| `SpanTests.cs` | 3 | New test file |

---

## Appendix C: Decision Log

| Decision | Type | Rationale |
|----------|------|-----------|
| Populate spans in Parser | One-way door | Industry standard; enables incremental parsing; required for LSP |
| Keep legacy tracking fields (deprecated) | Two-way door | Can be removed in v0.2; allows gradual migration |
| Use `TextSpan.FromBounds()` for validation | Two-way door | Catches errors early; can remove if performance issue |
| Span is nullable | Two-way door | Backward compatible; allows opt-in during migration |
