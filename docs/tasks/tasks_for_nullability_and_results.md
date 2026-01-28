## Task: Update Language Specification for Nullability & Result Redesign

### Overview

This task updates the Sharpy language specification to reflect the new nullability and error handling design:

- `T?` now means `Optional[T]` (safe tagged union)
- `T | None` now means C# nullable (interop)
- `T !E` is new syntax sugar for `Result[T, E]`
- `maybe` converts `T | None` → `T?`

**Scope:** Language spec markdown files and code snippets only. Do NOT modify C# source code, test files, or test data.

**Working directory:** Assume you're in a worktree; no branch creation needed.

---

### Phase 1: Core Type Documentation

#### 1.1 Update `nullable_types.md`

- [x] Change the title/framing to indicate this is for **.NET interop**
- [x] Replace all `T?` syntax with `T | None` syntax
- [x] Update the description to explain this represents C# `T?` for interop scenarios
- [x] Add a note that `T | None` is the **only** valid inline union (no free unions like `int | str`)
- [x] Add a section "When to Use `T | None`" emphasizing interop with .NET APIs
- [x] Add cross-reference to `tagged_unions_optional.md` for safe Sharpy-native optionals
- [x] Update all code examples to use `T | None` syntax

```
git add docs/language_specification/nullable_types.md
git commit -m "spec: update nullable_types.md - T | None for C# interop"
```

#### 1.2 Update `tagged_unions_optional.md`

- [x] Add prominent note at top: `T?` is syntactic sugar for `Optional[T]`
- [x] Update "Creating Optional Values" section to show `T?` shorthand:
  ```python
  # Shorthand (preferred)
  value: int? = Some(42)
  empty: int? = Nothing

  # Explicit (equivalent)
  value: Optional[int] = Optional.Some(42)
  ```
- [x] Update the comparison table to compare `T?` (Optional) vs `T | None` (C# nullable)
- [x] Update "When to Use" sections:
  - `T?` → Sharpy-native code, safe optional semantics
  - `T | None` → .NET interop boundaries
- [x] Add note that `Optional[T]` is a **struct** (no heap allocation)
- [x] Update all examples to use `T?` shorthand where appropriate

```
git add docs/language_specification/tagged_unions_optional.md
git commit -m "spec: update Optional docs - T? is now sugar for Optional[T]"
```

#### 1.3 Update `tagged_unions_result.md`

- [x] Add new section "Shorthand Syntax" after "Definition":
  ```python
  # Shorthand (in return type annotations)
  def parse(s: str) -> int !ValueError:
      ...

  # Equivalent explicit form
  def parse(s: str) -> Result[int, ValueError]:
      ...
  ```
- [x] Document that `!E` syntax is recommended for **top-level return types only**
- [x] Document precedence: `!E` binds tighter than `| None`
  ```python
  int !ValueError | None  →  (int !ValueError) | None  →  Result[int, ValueError] | None
  ```
- [x] Add note that `Result[T, E]` should be a **struct** (no heap allocation)
- [x] Update examples throughout to show both shorthand and explicit forms
- [x] Add section on stdlib conventions (parsing returns Result, indexing throws, etc.)

```
git add docs/language_specification/tagged_unions_result.md
git commit -m "spec: add T !E shorthand syntax for Result types"
```

---

### Phase 2: Operator Documentation

#### 2.1 Update `null_coalescing_operator.md`

- [x] Update intro to clarify it works with **both** `T?` (Optional) and `T | None` (C# nullable)
- [x] Update examples to show both forms:
  ```python
  # With Optional (T?)
  name: str? = get_name()
  display = name ?? "Anonymous"

  # With C# nullable (T | None)
  raw: str | None = dotnet_api()
  display = raw ?? "Anonymous"
  ```
- [x] Update the `Optional` section examples to use `T?` shorthand

```
git add docs/language_specification/null_coalescing_operator.md
git commit -m "spec: update null coalescing for T? and T | None"
```

#### 2.2 Update `null_coalescing_assignment.md`

- [x] Same pattern as above: clarify works with both `T?` and `T | None`
- [x] Update all examples to use new syntax

```
git add docs/language_specification/null_coalescing_assignment.md
git commit -m "spec: update null coalescing assignment for T? and T | None"
```

#### 2.3 Update `null_conditional_access.md`

- [x] Same pattern: clarify works with both `T?` and `T | None`
- [x] Update examples to use new syntax
- [x] Clarify return types:
  - `x?.foo` where `x: T?` → returns `T?` (Optional)
  - `x?.foo` where `x: T | None` → returns `U | None` (C# nullable)

```
git add docs/language_specification/null_conditional_access.md
git commit -m "spec: update null conditional access for T? and T | None"
```

---

### Phase 3: Expression Documentation

#### 3.1 Update `maybe_expressions.md`

- [x] Update description: `maybe` converts `T | None` (C# nullable) to `T?` (Optional)
- [x] Reframe as the **bridge from .NET interop to safe Sharpy code**
- [x] Update type constraint: operand must be `T | None`, not `T?`
  ```python
  # ✅ Valid - converts C# nullable to Optional
  raw: str | None = dotnet_api()
  safe: str? = maybe raw

  # ❌ Invalid - already an Optional
  opt: str? = Some("hello")
  x = maybe opt  # ERROR: opt is str?, not str | None
  ```
- [x] Update all examples to use new syntax

```
git add docs/language_specification/maybe_expressions.md
git commit -m "spec: update maybe expressions - bridges T | None to T?"
```

#### 3.2 Update `try_expressions.md`

- [x] Add note showing shorthand return type:
  ```python
  x: int !Exception = try int("42")

  # Equivalent to:
  x: Result[int, Exception] = try int("42")
  ```
- [x] Update examples to use `T !E` shorthand in type annotations where appropriate

```
git add docs/language_specification/try_expressions.md
git commit -m "spec: update try expressions with T !E shorthand examples"
```

---

### Phase 4: Supporting Documentation

#### 4.1 Update `tagged_unions.md`

- [x] Update the `Optional` overview to mention `T?` shorthand
- [x] Update the `Result` overview to mention `T !E` shorthand
- [x] Update any examples that use nullable or optional types

```
git add docs/language_specification/tagged_unions.md
git commit -m "spec: update tagged unions overview with new syntax"
```

#### 4.2 Update `type_annotations.md` (if exists) or create section in README

- [x] Add comprehensive type syntax reference:
  ```python
  T           # Non-nullable type
  T?          # Optional[T] - safe tagged union
  T | None    # C# nullable - .NET interop only
  T !E        # Result[T, E] - for return types
  ```
- [x] Document that `| None` is the only valid inline union form

```
git add docs/language_specification/type_annotations.md  # or README.md
git commit -m "spec: add comprehensive type syntax reference"
```

#### 4.3 Update `operator_precedence.md`

- [x] Add entry for `!E` in type annotations (binds tighter than `| None`)
- [x] Clarify this is type-level precedence, not expression-level

```
git add docs/language_specification/operator_precedence.md
git commit -m "spec: add T !E precedence in type annotations"
```

#### 4.4 Update `README.md` (spec index)

- [x] Update any summary descriptions that mention nullable types
- [x] Ensure cross-references are accurate

```
git add docs/language_specification/README.md
git commit -m "spec: update README index for nullability changes"
```

---

### Phase 5: Stdlib API Documentation

#### 5.1 Update `builtin_functions.md`

- [x] Update parsing functions to show Result-returning variants:
  ```python
  # Throwing version (Python-compatible)
  n = int("42")  # Raises ValueError

  # Result-returning version (recommended for user input)
  def int.parse(s: str) -> int !ValueError: ...
  ```
- [x] Update type conversion examples to use new syntax

```
git add docs/language_specification/builtin_functions.md
git commit -m "spec: update builtin functions with Result-returning variants"
```

#### 5.2 Update collection type documentation (if exists)

- [x] Ensure `dict.get()` returns `V?` (Optional), not `V | None`
- [x] Ensure indexing (`dict[key]`, `list[i]`) throws exceptions
- [x] Add `list.get(index: int) -> T?` if not present

```
git add docs/language_specification/builtin_types.md  # or equivalent
git commit -m "spec: update collection APIs with T? return types"
```

---

### Phase 6: Search and Sweep

#### 6.1 Global search for old patterns

- [x] Search all `.md` files in `docs/language_specification/` for `T?` used as C# nullable and update to `T | None`
- [x] Search for `Optional[T]` in examples where `T?` shorthand would be cleaner
- [x] Search for `Result[T, E]` in return type examples where `T !E` would be cleaner
- [x] Verify no examples show invalid free unions like `int | str`

```
git add docs/language_specification/
git commit -m "spec: sweep remaining files for syntax consistency"
```

#### 6.2 Final review

- [x] Read through each modified file for consistency
- [x] Verify all cross-references still work
- [x] Check that comparison tables are accurate and consistent across files

```
git add docs/language_specification/
git commit -m "spec: final consistency review and fixes"
```

---

### Verification Checklist

Before marking complete, verify:

- [x] `T?` consistently means `Optional[T]` everywhere
- [x] `T | None` consistently means C# nullable everywhere
- [x] `T !E` is documented as return-type sugar for `Result[T, E]`
- [x] `maybe` is documented as converting `T | None` → `T?`
- [x] No examples show invalid free unions
- [x] Comparison tables are updated and consistent
- [x] Cross-references between docs are accurate
