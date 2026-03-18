# Sharpy Language Specification Audit Report

**Date:** 2026-03-17
**Scope:** All 95+ spec files in `docs/language_specification/` audited against ~1,434 test fixtures and string-based unit/integration tests
**Methodology:** 6 parallel domain-specific audits covering Types & Literals, Expressions & Operators, Statements & Control Flow, Functions & Callables, Classes & OOP, and Modules & Infrastructure

---

## Executive Summary

| Category | Count | Severity |
|----------|-------|----------|
| **Divergences** (spec contradicts implementation) | 5 | 2 HIGH, 3 MEDIUM |
| **Spec Gaps** (defined but untested) | 28 | 3 CRITICAL, 10 HIGH, 15 LOW |
| **Spec Ambiguities** (unclear, tests assume) | 21 | 5 HIGH, 16 LOW |
| **Extensions** (tests extrapolate naturally) | 32+ | All compatible |

Overall the spec and tests are **well-aligned** — no catastrophic divergences exist. The most actionable findings are (1) a handful of spec statements that contradict the actual implementation, (2) several documented features with zero test coverage, and (3) spec sections that need clarification.

---

## Part 1: DIVERGENCES

These are cases where the specification explicitly states one thing but the compiler/tests demonstrate different behavior. These require either fixing the spec or fixing the implementation.

### D1. [HIGH] Decimal Literal Suffix (`m`/`M`) — Spec Says Supported, Compiler Rejects

- **Spec:** `float_literals.md` line 23 — states `m` or `M` suffix creates `decimal` type: `3.14m`
- **Test:** `basics/float_literal_suffix_m.spy` + `.error` — compiler rejects with "Unsupported float literal suffix 'm'"
- **Action:** Either implement `m`/`M` suffix or remove from spec. Since `decimal` type is listed in `primitive_types.md` but has zero positive test coverage, this may be a deferred feature that was prematurely documented.

### D2. [HIGH] Variadic Arguments Position — Two Spec Files Contradict Each Other

- **Spec A:** `function_variadic_arguments.md` lines 50-65 — "The `*args` parameter must be the last parameter in the function signature"
- **Spec B:** `flexible_arguments.md` line 41 — "`*args` (variadic) implicitly acts as the `*` marker for keyword-only separation"
- **Tests:** `functions/variadic_with_keyword_only.spy`, `functions/variadic_keyword_only_mixed.spy` — keyword-only parameters after `*args` work correctly
- **Action:** Update `function_variadic_arguments.md` to state that `*args` can precede keyword-only parameters (matching `flexible_arguments.md` and actual behavior).

### D3. [MEDIUM] Lambda Default Parameters vs Function Type Spec

- **Spec:** `function_types.md` lines 84-109 — "Function types cannot specify optional parameters"
- **Test:** `functions/lambda_default_args.spy` — lambdas successfully use default parameters
- **Action:** Clarify that the restriction applies to *function type annotations* (`(int, int) -> int`), not lambda *expressions*. These are distinct concepts conflated in the spec.

### D4. [MEDIUM] Integer Literal Overflow Range

- **Spec:** `integer_literals.md` lines 15-17 — "Integer literals are inferred as `int` (32-bit)"
- **Test:** `basics/integer_literal_overflow.error` — error message says "too large for a 64-bit integer"
- **Action:** Clarify the inference and range-checking rules. The compiler appears to accept literals up to int64 range even when the default inference type is int32.

### D5. [MEDIUM] Scientific Notation Output Format (Undocumented Axiom 1 Trade-off)

- **Spec:** `extended_numeric_literals.md` — no mention of uppercase vs lowercase `E` in output
- **Test:** `basics/float_scientific_notation.spy` — outputs `1E+20` (uppercase), with comment noting ".NET uses uppercase 'E', Python uses lowercase 'e'"
- **Action:** Document this Axiom 1 trade-off in the spec.

---

## Part 2: SPEC GAPS (Documented but Untested)

Features the spec describes but that have zero or near-zero test coverage.

### Critical Gaps

#### G1. [CRITICAL] UTF-16 String Semantics — Zero Test Coverage

- **Spec:** `string_type.md` lines 9-24 — defines that `len("😀")` returns `2` (UTF-16 code units), indexing returns surrogate halves, slicing can produce invalid strings
- **Tests:** No integration tests verify any of this behavior
- **Impact:** This is a key Axiom 1 divergence from Python (`len("😀")` = 1 in Python). Developers migrating from Python will hit this.
- **Needed tests:** `string_emoji_length.spy`, `string_emoji_indexing.spy`, `string_emoji_slicing.spy`

#### G2. [CRITICAL] `class`/`struct` Type Constraints — Zero Test Coverage

- **Spec:** `generics.md` lines 101-106 — documents `T: class`, `T: struct`, `T: class & IFoo`, `T: struct & IFoo`
- **Tests:** Only interface constraints tested (e.g., `T: IComparable[T]`). Zero tests for `class`/`struct` constraints.
- **Needed tests:** Positive and negative tests for reference-type and value-type constraints

#### G3. [CRITICAL] Array Type (`array[T]`) — Zero Test Coverage

- **Spec:** `primitive_types.md` lines 30-58, `type_annotation_shorthand.md` lines 130-141 — documents `array[T]` syntax, `int[]` postfix, `Array[int](lst)` constructor, `list(arr)` conversion
- **Tests:** No integration tests use array types at all. All collection tests use `list[T]`, `dict[K,V]`, `set[T]`.
- **Note:** If arrays are not yet implemented, they should be moved to `deferred_features.md`.

### High-Priority Gaps

#### G4. Multiple Type Constraints with `&` Syntax
- **Spec:** `generics.md` lines 110-162 — `T: IFoo & IBar`, `T: class & IFoo & IBar`
- **Tests:** Only single-interface constraint tests exist. Zero tests use `&` syntax.

#### G5. Loop-Else with Return in Loop Body
- **Spec:** `loop_else.md` lines 17-40 — else clause does NOT run if loop exits via `return`
- **Tests:** Tests only verify `break` skipping else. No test has `return` inside the loop body.

#### G6. Loop-Else with Exception
- **Spec:** `loop_else.md` lines 32-36 — else clause does NOT run if loop exits via exception
- **Tests:** No test combines loop-else with raise/exception.

#### G7. Walrus Operator Scoping in Comprehensions
- **Spec:** `variable_scoping.md` lines 46-61 — walrus variables in comprehensions are scoped to comprehension block (differs from Python 3.8+)
- **Tests:** No test verifies this Sharpy-specific behavior.

#### G8. Comprehension Duplicate Variable Error
- **Spec:** `comprehensions.md` lines 177-186 — `[x for x in range(3) for x in range(3)]` should be a compile-time error
- **Tests:** No negative test for this rule.

#### G9. Pipe Operator Limitation Tests
- **Spec:** `pipe_operator.md` lines 161-171 — cannot pipe to constructors or operators
- **Tests:** No negative tests for these specific limitations.

#### G10. Circular Imports — Type-Only vs Base Class
- **Spec:** `module_system.md` line 67 — "Circular references are allowed for type annotations" but "not allowed for base classes"
- **Tests:** Implementation rejects ALL circular imports unconditionally. No test verifies type-only circular imports work.

#### G11. `type(None)` Compile-Time Error
- **Spec:** `builtin_functions.md` line 89 — `type(None)` should be a compile-time error
- **Tests:** No test for this error case.

#### G12. Type Alias Scope (Class-Level, Function-Level)
- **Spec:** `type_aliases.md` lines 15-28 — type aliases at module, class, and function level
- **Tests:** All tested aliases are module-level only.

#### G13. Constraint Reordering
- **Spec:** `generics.md` lines 146-158 — compiler reorders constraints to match C# requirements
- **Tests:** All tests manually write constraints in correct order. No test verifies automatic reordering.

### Low-Priority Gaps

| ID | Feature | Spec Location | Notes |
|----|---------|---------------|-------|
| G14 | Expression blocks (`do:`) | `expression_blocks.md` | Documented as deferred; no tests by design |
| G15 | `@classmethod` rejection | `class_methods.md` | No negative test |
| G16 | Parameter modifiers (`ref`/`out`/`in`) | `parameter_modifiers.md` | Documented as deferred |
| G17 | Conversion operators | `conversion_operators.md` | Documented as deferred |
| G18 | Future keywords reserved | `keywords.md` | `defer`, `do` not tested as reserved |
| G19 | `auto` keyword | `keywords.md` | Not explicitly tested |
| G20 | Chained identity operators | `operator_precedence.md` | `a is b is c` not tested |
| G21 | Mixed comparison chains | `operator_precedence.md` | `a < b in c` not tested |
| G22 | Walrus type annotation error | `walrus_operator.md` | `x: int := val` should error; not tested |
| G23 | Parameter names in function types | `function_types.md` | Ignored names not explicitly tested |
| G24 | Delegate variance in function_types.md | `function_types.md` | Only in delegates.md |
| G25 | Indentation edge cases | `indentation.md` | Tab rejection, blank line handling not tested |
| G26 | Lexer lookahead behavior | `lexer_implementation.md` | Implementation exists but not tested |
| G27 | `await` operator precedence | `operator_precedence.md` | Tested contextually but not precedence-specifically |
| G28 | Variable-length tuple unpacking | `tuple_unpacking.md` | `tuple[T, ...]` not tested |

---

## Part 3: SPEC AMBIGUITIES

Places where the specification is unclear and tests make assumptions about the intended behavior.

### High-Priority Ambiguities

#### A1. Context Manager `__exit__` Exception Handling
- **Spec:** `context_managers.md` lines 38-40 — `__exit__(self)` with no parameters
- **Issue:** Doesn't clarify if `__exit__` receives exception info (Python protocol) or can suppress exceptions
- **Actual:** Implementation follows C# `using` semantics (no exception parameters)
- **Action:** Explicitly document that Sharpy's `__exit__` does NOT receive exception parameters (unlike Python)

#### A2. Identity Operator (`is`) — Value vs Reference Semantics
- **Spec:** `identity_operators.md` lines 5-6 — maps to `object.ReferenceEquals()`
- **Issue:** Doesn't clarify behavior with small integers, strings, or other interned objects
- **Tests:** Only test with collection references
- **Action:** Document that all `is` comparisons use reference equality, even for primitives (which may box)

#### A3. Pipe Operator Partial Application Syntax (`_` placeholder)
- **Spec:** `pipe_operator.md` — mentions "partial application" but doesn't detail `_` syntax
- **Tests:** `pipe_with_partial.spy` uses `5 |> multiply(_, 3)` with `_` placeholder
- **Action:** Document the `_` placeholder syntax for partial application in pipe expressions

#### A4. Comparison Chaining Short-Circuit Behavior
- **Spec:** `comparison_chaining.md` lines 24-31 — "Each intermediate expression evaluated only once"
- **Issue:** Doesn't explicitly state short-circuit behavior
- **Tests:** `comparison_chain_side_effects.spy` proves short-circuiting works
- **Action:** Explicitly document that chained comparisons short-circuit (stop evaluating once a comparison is false)

#### A5. Function Type Contravariance Enforcement
- **Spec:** `function_types.md` lines 111-132 — shows contravariance example with commented-out code
- **Issue:** Unclear if this is aspirational or enforced. Commented-out examples suggest it might not work.
- **Tests:** No direct test for function type contravariance
- **Action:** Clarify whether function type variance is enforced, and add tests

### Lower-Priority Ambiguities

| ID | Area | Issue |
|----|------|-------|
| A6 | Membership operator dispatch | Priority when type has both `__contains__` and `.Contains()` |
| A7 | Null-conditional double-wrapping | What if `?.` method returns nullable? |
| A8 | Try expression uncaught types | `try[ValueError] foo()` when foo throws TypeError — compile or runtime? |
| A9 | Maybe expression type constraints | What if `maybe opt` where `opt` is already `str?`? |
| A10 | Numeric promotion error timing | `float64 + decimal` → compile-time or runtime error? |
| A11 | Operator overloading precedence | Do overloaded operators follow built-in precedence? |
| A12 | Generator return type wrapping | Edge cases in `IEnumerable<T>` wrapping |
| A13 | Async constructor rejection rationale | SPY0358 documented but rationale missing |
| A14 | Single-element tuple type syntax | `(T)` vs `(T,)` in type annotations |
| A15 | Optional vs Nullable method calls | Conversion semantics when mixing `T?` and `T | None` |
| A16 | None vs None() confusion | No test for using `None` where `None()` is required |
| A17 | Module-level variable rejection | Error message doesn't distinguish "assignment" from "missing type annotation" |
| A18 | .NET method name aliasing | Whether snake_case aliases exist for .NET PascalCase methods |
| A19 | String concatenation associativity | Assumed left-to-right but not documented |
| A20 | Module name acronym uppercasing | `io` → `Io` behavior documented but not tested |
| A21 | Ellipsis in concrete methods | "No-op" vs "throw" semantics could be clearer |

---

## Part 4: EXTENSIONS (Tests Beyond Spec — Compatible)

These are places where tests exercise behavior that is a natural, compatible extrapolation of what the spec defines. These indicate areas where the spec could be expanded to be more thorough.

| Area | Extension | Status |
|------|-----------|--------|
| Type narrowing | `T | None` with `is None` check narrows type in control flow | Natural from nullable semantics |
| Result type | Works with enum error values + `map_err()` transformation | Natural from Result[T,E] definition |
| Maybe + null conditional | `maybe` composed with `?.` and `??` in chains | Natural from individual feature specs |
| Variance + constraints | `interface IProducer[out T: Animal]` combining variance with constraints | Natural from both features |
| Comparison chaining side effects | Proves single-evaluation guarantee with Counter class | Concrete proof of spec guarantee |
| Walrus in nested conditionals | Multiple walrus operators in `and` chains | Natural from walrus + short-circuit |
| Spread + comprehensions | `[*filtered, 0, *doubled]` with comprehension results | Natural from spread and comprehension specs |
| Null coalescing + null conditional | `cfg?.get_label() ?? "default"` chains | Natural composition |
| Async yield from | `yield from sync_items()` inside `async def` | Documented Sharpy extension |
| Nested loop variable shadowing | Same-named loop variables in nested loops properly shadow | Natural from block scoping |
| Const type inference at module level | `const MAX_SIZE = 100` infers type without annotation | Consistent with spec examples |
| Loop-else independence | Each nested loop's else clause is independent | Natural from block scoping |
| Constructor chaining restrictions | `self.__init__()` only valid as first statement | Matches spec exactly |
| Dunder invocation restrictions | `self.__dunder__()` allowed inside dunders, `other.__dunder__()` rejected | Matches spec exactly |
| Interface dunder restrictions | User interfaces cannot declare dunders | Matches spec exactly |

---

## Part 5: RECOMMENDATIONS

### Immediate (Fix Spec/Implementation Contradictions)

1. **Fix `function_variadic_arguments.md`** — Remove "must be the last parameter" claim; align with `flexible_arguments.md` which correctly describes keyword-only separation
2. **Fix `float_literals.md`** — Either remove `m`/`M` suffix documentation or move to deferred features
3. **Clarify `function_types.md`** — Distinguish function type annotations from lambda expressions re: default parameters
4. **Clarify `integer_literals.md`** — Document the actual overflow range check behavior (int64 vs int32)
5. **Document `extended_numeric_literals.md`** — Add note about uppercase `E` in scientific notation output (Axiom 1)

### High Priority (Add Test Coverage)

1. **UTF-16 string behavior** — `len("😀")`, emoji indexing, surrogate slicing
2. **`class`/`struct` constraints** — or move to deferred if not implemented
3. **Array types** — or move to deferred if not implemented
4. **Multi-constraints with `&`** — or move to deferred if not implemented
5. **Loop-else with return/exception** — verify spec-defined behavior
6. **Walrus scoping in comprehensions** — verify Sharpy differs from Python
7. **Circular imports** — verify type-only circular imports work (or update spec)

### Medium Priority (Spec Clarifications)

1. Document `_` placeholder syntax for pipe operator partial application
2. Explicitly state comparison chaining short-circuits
3. Clarify `__exit__` doesn't receive exception parameters (unlike Python)
4. Document `is` behavior with value types (boxing/reference semantics)
5. Clarify function type contravariance — aspirational or enforced?

### Low Priority (Edge Case Tests)

1. Comprehension duplicate variable error test
2. Pipe to constructor/operator negative tests
3. Chained identity operators (`a is b is c`)
4. Walrus type annotation error test
5. `type(None)` compile error test
6. Future keywords (`defer`, `do`) reserved test
7. Type alias at class/function scope
