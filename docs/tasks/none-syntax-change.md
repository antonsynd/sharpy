# Task: Change `Nothing` to `None()` for Empty Optional

## Summary

This task changes Sharpy's syntax for constructing empty `Optional<T>` values from `Nothing` to `None()`, while keeping bare `None` as the C# null literal.

### Rationale

1. **Symmetry with `Some()`**: Tagged union cases use constructor syntax. `Some(42)` and `None()` form a natural pair, making the language more consistent.

2. **Pythonic familiarity**: `None` remains the keyword Python developers expect, just with context-dependent semantics.

3. **Clear distinction**: 
   - `None()` = "I'm constructing a Sharpy Optional" (safe, explicit)
   - `None` = "C# null" (interop, raw)
   - The parentheses signal "construction is happening"

4. **Rust/Haskell alignment**: `Some`/`None` mirrors Rust's `Option` type naming.

### Syntax Summary

```python
# BEFORE (old syntax)
x: int? = Nothing           # Empty Optional
y: int | None = None        # C# null

# AFTER (new syntax)  
x: int? = None()            # Empty Optional - constructor call
y: int | None = None        # C# null - unchanged

# Pattern matching (unchanged - patterns don't use parens for unit cases)
match opt:
    case Some(v): print(v)
    case None: print("empty")  # No parens in pattern

# User-defined tagged unions follow same rule
union LoadState:
    case NotStarted
    case Loaded(data: str)

state = LoadState.NotStarted()  # Construction: parens required
match state:
    case NotStarted: ...        # Pattern: no parens
```

### Key Design Decisions

1. **Construction vs Pattern distinction**:
   - **Construction** (creating values): Always use parens, even for unit cases → `None()`, `LoadState.NotStarted()`
   - **Patterns** (matching shapes): No parens for unit cases → `case None:`, `case NotStarted:`

2. **Enums vs Tagged Unions**:
   - **Enums** (int-backed): No parens, accessing a value → `Color.Red`
   - **Tagged Unions**: Parens required, constructing an instance → `LoadState.NotStarted()`

3. **Error handling**: `x: int? = None` (bare, no parens) should produce a helpful error message suggesting `None()`.

---

## Phase 1: Documentation Updates

Update all language specification documents in `docs/language_specification/`.

### Task 1.1: Update `tagged_unions_optional.md`

**File**: `docs/language_specification/tagged_unions_optional.md`

**Changes**:
- Replace all `Nothing` with `None()` in construction contexts
- Keep `None` in pattern matching contexts (no parens)
- Update the definition section
- Update comparison table
- Update all code examples

**Key sections to update**:
```python
# Definition - change from:
union Optional[T]:
    case Some(value: T)
    case Nothing()  # Or simply: case Nothing

# To:
union Optional[T]:
    case Some(value: T)
    case None  # Unit case for empty optional
```

```python
# Creating values - change from:
value: int? = Some(42)
empty: int? = Nothing

# To:
value: int? = Some(42)
empty: int? = None()  # Constructor syntax mirrors Some()
```

```python
# Pattern matching stays the same (no parens in patterns):
match opt:
    case Some(v): print(v)
    case None: print("empty")  # No parens in pattern
```

**Verification**: Read through entire file, ensure no `Nothing` remains except in historical/rationale notes if any.

---

### Task 1.2: Update `tagged_unions.md`

**File**: `docs/language_specification/tagged_unions.md`

**Changes**:
- Update Optional definition example
- Add/update section on unit case construction syntax (parens required)
- Update any `Nothing` references to `None()`

**Specific updates**:
```python
# Unit case construction - emphasize parens required
state = LoadState.NotStarted()   # Parens required for construction
opt = None()                      # Parens required for unit case
```

---

### Task 1.3: Update `nullable_types.md`

**File**: `docs/language_specification/nullable_types.md`

**Changes**:
- Update the comparison showing `T?` vs `T | None`
- Change any `Nothing` examples to `None()`

```python
# Change from:
name: str? = Nothing

# To:
name: str? = None()
```

---

### Task 1.4: Update `none_literal.md`

**File**: `docs/language_specification/none_literal.md`

**Changes**:
- Clarify that bare `None` = C# null
- Add note about `None()` for Optional construction
- Ensure the distinction is clear

**Updated content**:
```markdown
# None Literal

`None` represents the absence of a value in nullable contexts (C# `null`):

```python
value: str | None = None   # C# null
```

For constructing empty `Optional[T]` values, use `None()` with parentheses:

```python
value: str? = None()       # Optional<str>.None - empty optional
```

The distinction:
- `None` (bare) → C# `null` for `T | None` types
- `None()` (with parens) → Empty `Optional<T>` for `T?` types
```

---

### Task 1.5: Update `null_coalescing_operator.md`

**File**: `docs/language_specification/null_coalescing_operator.md`

**Changes**:
- Update any `Nothing` examples to `None()`

---

### Task 1.6: Update `null_coalescing_assignment.md`

**File**: `docs/language_specification/null_coalescing_assignment.md`

**Changes**:
- Update any `Nothing` examples to `None()`

---

### Task 1.7: Update `null_conditional_access.md`

**File**: `docs/language_specification/null_conditional_access.md`

**Changes**:
- Update any `Nothing` examples to `None()`

---

### Task 1.8: Update `maybe_expressions.md`

**File**: `docs/language_specification/maybe_expressions.md`

**Changes**:
- Update conversion examples
- Change `Nothing` to `None()` where appropriate

---

### Task 1.9: Update `collection_types.md`

**File**: `docs/language_specification/collection_types.md`

**Changes**:
- Update safe access examples that return `Optional`
- Change any `Nothing` to `None()`

---

### Task 1.10: Update `type_casting.md`

**File**: `docs/language_specification/type_casting.md`

**Changes**:
- Update any `Nothing` references to `None()`

---

### Task 1.11: Update `keywords.md`

**File**: `docs/language_specification/keywords.md`

**Changes**:
- Remove `Nothing` if it was listed as a keyword/identifier
- Ensure `None` is documented correctly

---

### Task 1.12: Update `match_statement.md` (if exists)

**File**: `docs/language_specification/match_statement.md`

**Changes**:
- Update Optional pattern matching examples
- Clarify that patterns use bare `None` (no parens)

---

### Task 1.13: Update `function_default_parameters.md`

**File**: `docs/language_specification/function_default_parameters.md`

**Changes**:
- Update any default parameter examples using Optional
- Change `Nothing` to `None()`

```python
# Change from:
def foo(x: int? = Nothing) -> None:

# To:
def foo(x: int? = None()) -> None:
```

---

### Task 1.14: Update other affected docs

Search for and update any remaining files containing `Nothing`:
- `comprehensions.md`
- `indentation.md` 
- `tagged_unions_result.md`
- Any other files found via: `grep -r "Nothing" docs/language_specification/`

---

## Phase 2: Compiler Implementation

### Task 2.1: Update Parser to Handle `None()` Call

**File**: `src/Sharpy.Compiler/Parser/Parser.Expressions.cs` (or similar)

**Changes**:
- When parsing a function call where the function is `Identifier("None")` with zero arguments, this should be recognized as valid
- The AST will represent this as `FunctionCall(Function: Identifier("None"), Arguments: [])`
- No special AST node needed - it's just a zero-arg function call

**Note**: The parser likely already handles this correctly since `None()` is syntactically valid as a function call. Verify this works.

---

### Task 2.2: Update Semantic Analysis / Type Checker

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

**Current behavior** (handling `Nothing` identifier):
```csharp
// Handle Nothing identifier in Optional context
if (id.Name == "Nothing" && _expectedType is OptionalType)
{
    return _expectedType;
}
```

**New behavior** (handle `None()` function call):
```csharp
// In CheckFunctionCall or similar:
// When call is None() with zero args and expected type is OptionalType,
// return the OptionalType
if (call.Function is Identifier funcId && 
    funcId.Name == "None" && 
    call.Arguments.Length == 0 &&
    _expectedType is OptionalType optType)
{
    return optType;
}
```

**Also update error reporting**:
- When bare `None` is used where `OptionalType` is expected, emit helpful error:
  ```
  Error: Cannot assign 'None' to 'int?'. 'None' is the C# null literal.
  Hint: Did you mean 'None()' to construct an empty Optional?
  ```

---

### Task 2.3: Update Code Generation

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

**Current code** (around line 43-45):
```csharp
// Handle Nothing -> null (for T? codegen compatibility)
Identifier name when name.Name == "Nothing" && GetExpressionSemanticType(name) is OptionalType
    => LiteralExpression(SyntaxKind.NullLiteralExpression),
```

**New code**:
```csharp
// Handle None() -> null (for T? Optional codegen)
// None() is parsed as FunctionCall with zero args
FunctionCall call when IsNoneConstructorCall(call) && GetExpressionSemanticType(call) is OptionalType
    => LiteralExpression(SyntaxKind.NullLiteralExpression),
```

Add helper method:
```csharp
private bool IsNoneConstructorCall(FunctionCall call)
{
    return call.Function is Identifier id && 
           id.Name == "None" && 
           call.Arguments.Length == 0 &&
           call.KeywordArguments.Length == 0;
}
```

**Remove** the old `Nothing` handling code.

---

### Task 2.4: Update DefaultParameterValidator

**File**: `src/Sharpy.Compiler/Semantic/Validation/DefaultParameterValidator.cs`

**Current code** (checks for `Nothing` as compile-time constant):
```csharp
// Nothing is a compile-time constant for Optional types
Identifier { Name: "Nothing" } => true,
```

**New code**:
```csharp
// None() is a compile-time constant for Optional types
FunctionCall call when IsNoneConstructorCall(call) => true,
```

Also update error messages that mention `Nothing`.

---

### Task 2.5: Update any other semantic validators

Search for `"Nothing"` in the compiler source and update:
- `TypeChecker.cs`
- `TypeChecker.Definitions.cs`
- `TypeChecker.Expressions.cs`
- `SemanticType.cs` (update XML doc comments)
- Any other files with references

**Command to find all occurrences**:
```bash
grep -rn '"Nothing"' src/Sharpy.Compiler/
grep -rn 'Nothing' src/Sharpy.Compiler/Semantic/
```

---

## Phase 3: Test Updates

### Task 3.1: Update `ConstructorInferenceTests.cs`

**File**: `src/Sharpy.Compiler.Tests/Semantic/ConstructorInferenceTests.cs`

**Changes**:
- Update test that uses `Nothing` to use `None()`
- Add new test for `None()` being recognized correctly
- Add negative test for bare `None` with Optional type (should error)

```csharp
[Fact]
public void None_Call_InOptionalContext_TypesCorrectly()
{
    var source = @"
x: int? = None()
";
    // ... verify no errors and correct type inference
}

[Fact]
public void None_Bare_InOptionalContext_ProducesError()
{
    var source = @"
x: int? = None
";
    // ... verify error with helpful message
}
```

---

### Task 3.2: Add/Update CodeGen Tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/OptionalResultCodeGenTests.cs`

**Changes**:
- Update any tests using `Nothing` to use `None()`
- Add test verifying `None()` generates `null` literal

---

### Task 3.3: Update Integration Tests

**Files**: Various in `src/Sharpy.Compiler.Tests/Integration/`

Search for and update any tests using `Nothing`:
```bash
grep -rn 'Nothing' src/Sharpy.Compiler.Tests/
```

---

### Task 3.4: Add New Tests for None()/None Distinction

Create or add to existing test file:

```csharp
[Fact]
public void None_WithParens_IsOptionalEmpty()
{
    var source = @"
def foo() -> int?:
    return None()
";
    // Should compile successfully
}

[Fact]
public void None_WithoutParens_IsNullLiteral()
{
    var source = @"
def foo() -> int | None:
    return None
";
    // Should compile successfully
}

[Fact]
public void None_WithoutParens_CannotAssignToOptional()
{
    var source = @"
x: int? = None
";
    // Should produce error
}
```

---

## Phase 4: Snippets and Examples

### Task 4.1: Update Snippets

**Directory**: `snippets/`

Search and update any `.spy` files that might use `Nothing`:
```bash
grep -rn 'Nothing' snippets/
```

If none found (as search indicated), no changes needed.

---

### Task 4.2: Update Dogfood Examples

**Directory**: `dogfood_output/`

Check if any examples use `Nothing` and update accordingly.

---

## Phase 5: Verification

### Task 5.1: Run All Tests

```bash
cd src/Sharpy.Compiler.Tests
dotnet test
```

All tests should pass.

---

### Task 5.2: Verify Documentation Consistency

```bash
# Should return no results (except possibly in rationale/history sections)
grep -rn 'Nothing' docs/language_specification/ | grep -v '<!--'
```

---

### Task 5.3: Manual Smoke Test

Create a test file and compile:

```python
# test_none_syntax.spy

# Optional with None()
x: int? = Some(42)
y: int? = None()

# Nullable with bare None
a: int | None = 42
b: int | None = None

# Function returning Optional
def maybe_value(flag: bool) -> int?:
    if flag:
        return Some(100)
    return None()

# Default parameter
def greet(name: str? = None()) -> str:
    return name ?? "World"
```

Compile and verify C# output is correct.

---

## Commit Strategy

1. **Commit 1**: Documentation updates (Phase 1)
   - Message: "docs: change Nothing to None() for empty Optional construction"

2. **Commit 2**: Compiler implementation (Phase 2)
   - Message: "feat: implement None() syntax for empty Optional, bare None remains C# null"

3. **Commit 3**: Test updates (Phase 3)
   - Message: "test: update tests for None()/None syntax change"

4. **Commit 4**: Examples and final verification (Phase 4-5)
   - Message: "chore: update examples and verify None() syntax"

---

## Rollback Plan

If issues are discovered:
1. Revert commits in reverse order
2. The old `Nothing` syntax can be temporarily re-enabled by adding it back to the identifier check in `RoslynEmitter.Expressions.cs`

---

## Future Considerations

This change establishes the pattern that **tagged union unit case construction requires parentheses**. When implementing user-defined tagged unions (e.g., `union LoadState`), ensure the same rule applies:

```python
state = LoadState.NotStarted()   # Parens required
state = LoadState.Loading()      # Parens required  
state = LoadState.Loaded("x")    # Parens required (data case)
```

Pattern matching remains without parens for unit cases:
```python
case LoadState.NotStarted: ...   # No parens
case LoadState.Loaded(d): ...    # Parens for binding
```
