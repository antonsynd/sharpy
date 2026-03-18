# Sharpy Language Specification vs Tests Audit Report

**Date:** 2026-03-18
**Scope:** 106 spec files, 1,434 test fixtures, ~350 unit/integration test files
**Method:** 8 parallel domain audits against language specification

---

## Executive Summary

The specification and test suite are **largely aligned** on core semantics. No critical runtime-behavior divergences were found — the compiler does what the tests expect. However, the audit uncovered:

- **3 spec-vs-test divergences** (spec says X, tests say Y)
- **~35 extensions** where tests cover behavior the spec doesn't define
- **~70 gaps** where the spec defines behavior with no test coverage
- **Several outdated spec passages** that don't reflect current implementation

The most impactful findings are organized below by severity.

---

## Part 1: DIVERGENCES (Spec Says X, Tests Say Y)

### D1. Float Leading Decimal Point `.5` — CRITICAL
**Spec** (`float_literals.md`): `.5` is valid (like Python)
**Tests** (`LexerTests.cs:214-220`): `.5` is rejected. Comment says "v0.1 spec requires digit before decimal point"

One of spec or tests must be updated. Either:
- Update spec to require leading digit (matching implementation)
- Update lexer to accept `.5` (matching spec)

### D2. `divmod()` → `div_mod()` Name Mangling — Undocumented
**Spec** (`builtin_functions.md`): Documents function as `divmod(a, b)`
**Tests** (`builtins/divmod_basic.spy`): Calls `div_mod(17, 5)`

The name mangling from `divmod` → `div_mod` is not documented in the spec. Users copying from spec would get compile errors.

**Recommendation:** Add a note in `builtin_functions.md` that `divmod` is spelled `div_mod` due to snake_case conventions, or add it to name_mangling.md's special-case table.

### D3. Exception Hierarchy Incomplete in Spec
**Spec** (`exception_handling.md`): Documents 10 exception aliases
**Implementation** (`Sharpy.Core`): Provides 17+ aliases including `OverflowError`, `AttributeError`, `OSError`, `LookupError`, `FileExistsError`, `PermissionError`, `SystemExit`, etc.

Tests (`exception_aliases.spy`) exercise the extra types. Spec should be updated.

---

## Part 2: OUTDATED SPEC LANGUAGE

### O1. Optional/Result "Planned for Later" — Already Implemented
`tagged_unions_optional.md` and `tagged_unions_result.md` describe struct-based tagged unions as "planned for a later phase." Both are fully implemented with 20+ passing test fixtures exercising `.unwrap()`, `.map()`, `.unwrap_or()`, `Some()`, `None()`, `Ok()`, `Err()`.

### O2. `__getitem__`/`__setitem__` "Not Yet Implemented" — Tests Pass
`operator_overloading.md` has a known-gaps banner saying indexer codegen isn't implemented (#276, #277), but `class_indexer.spy` and `class_readonly_indexer.spy` both pass. Clarify whether the banner is stale.

---

## Part 3: SPEC GAPS — High Priority (Defined Behavior, No Tests)

These are cases where the spec explicitly defines behavior but no test validates it. Regressions here would be invisible.

### G1. Comparison Chaining — NO TESTS
**Spec** (`comparison_chaining.md`): `a < b < c` → `(a < b) and (b < c)` with single evaluation of middle expression.
**Tests:** Zero fixtures or unit tests for chained comparisons.

Missing tests:
- `1 < 2 < 3` (True), `1 < 3 < 2` (False)
- Mixed operators: `1 < 2 >= 2`
- Single evaluation: `a < func() < c` must call `func()` exactly once

### G2. Comprehension Variable Scoping — NO LEAK TEST
**Spec** (`comprehensions.md:122-134`): Variables declared in comprehensions don't leak to enclosing scope.
**Tests:** No test validates that `[x for x in range(10)]` followed by `print(x)` produces a compile error.

This is a fundamental semantic boundary with zero test coverage.

### G3. Walrus Operator in Comprehensions — NOT TESTED
**Spec** (`comprehensions.md:136-148`): Walrus assignments inside comprehensions are comprehension-local (differs from Python 3.8+).
**Tests:** No comprehension walrus tests exist. This Sharpy-specific behavior (different from Python) has zero validation.

### G4. Duplicate Variable Names in Comprehension For Clauses — NOT TESTED
**Spec** (`comprehensions.md:165-186`): `[x for x in range(3) for x in range(3)]` must produce a compile error.
**Tests:** No error fixture validates this.

### G5. Named Tuple Pattern Matching — NOT TESTED
**Spec** (`named_tuples.md:101-125`): Entire section on named tuple pattern matching with `case (x=0.0, y=0.0)` syntax.
**Tests:** Zero pattern matching tests in `named_tuples/` directory.

### G6. Circular Import with Type Annotations — NOT TESTED
**Spec** (`module_system.md`): Circular references are allowed for type annotations (forward references).
**Tests:** Only circular import rejection tests exist. No test validates that circular imports with only type annotations should succeed.

### G7. Type Narrowing Behavioral Integration — NO END-TO-END TEST
**Spec** (`type_narrowing.md`): `is not None` narrows `T?` → `T`, `isinstance(x, Type)` narrows in branch.
**Tests:** `TypeNarrowingContextTests.cs` tests infrastructure, but no integration test shows real code like:
```python
value: str | None = get_optional()
if value is not None:
    print(value.upper())  # Should compile (narrowed to str)
```

### G8. Try/Maybe Expression Precedence with Conditionals — NOT TESTED
**Spec** (`try_expressions.md:61-67`, `maybe_expressions.md:39-52`):
```python
y = try foo() if cond else bar()  # Parsed as: (try foo()) if cond else bar()
```
**Tests:** No test validates this precedence rule for either `try` or `maybe`.

### G9. Loop-Else with `raise` Exception — NOT TESTED
**Spec** (`loop_else.md:30-36`): `else` clause does NOT run when loop exits via exception.
**Tests:** No fixture combines loop-else with `raise`.

### G10. `list.get()` Method — NOT TESTED
**Spec** (`collection_types.md`): `list.get(index) -> T?` returns `Some(value)` or `None()`.
**Tests:** `dict.get()` is well-tested, but zero tests for `list.get()`.

### G11. Struct Parameter Modifiers (`in[]`, `mut[]`, `out[]`) — NOT TESTED
**Spec** (`structs.md`): Detailed sections on `in[T]`, `mut[T]`, `out[T]` parameter modifiers.
**Tests:** Zero test fixtures use these modifiers. (May be deferred — spec should clarify status.)

### G12. `isinstance()` with Generic Type Arguments Rejection — NOT TESTED
**Spec** (`builtin_functions.md`): `isinstance(x, list[int])` should produce a compile error (generic type erasure).
**Tests:** No test validates this error.

### G13. `type()` Function — NOT TESTED
**Spec** (`builtin_functions.md`): Rich behavior documented for `type(x)` returning `System.Type`.
**Tests:** Zero test fixtures.

### G14. `hash()` and Tuple Hashability — NOT TESTED
**Spec** (`builtin_functions.md`): `hash(point)` for tuples, `hash(([1,2], [3,4]))` should error (list not hashable).
**Tests:** Zero test fixtures.

### G15. `next()` Builtin — NOT DOCUMENTED
**Tests** (`builtins/next_default.spy`): Tests `next(iterator, default_value)`.
**Spec:** `next()` is not mentioned in `builtin_functions.md` at all.

### G16. `min()`/`max()` with `default` Parameter — NOT DOCUMENTED
**Tests** (`builtins/min_max_default.spy`): Tests `min(items, default=999)`.
**Spec:** `default` parameter not documented.

---

## Part 4: SPEC GAPS — Medium Priority

### G17. Rest Patterns with Tuples
**Spec** (`tuple_unpacking.md:60-101`): `first, *rest = (1, 2, 3, 4, 5)` — rest patterns work with tuples.
**Tests:** All star-unpack tests use lists, never tuples.

### G18. Tuple Unpacking Count Mismatch Error (SPY0239)
**Spec** (`tuple_unpacking.md:170-176`): SPY0239 for count mismatch.
**Tests:** Multiple `*` tested (SPY0356), but count mismatch not tested.

### G19. Match Expressions in Complex Contexts
**Spec** (`match_statement.md:98-115`): Match as function argument, inside string concat, in ternary.
**Tests:** Only assignment and return tested.

### G20. Partial Type Argument Specification Error
**Spec** (`generics.md:76-80`): `convert[str](...)` with multiple params → must specify all or none.
**Tests:** No test validates this error.

### G21. Function-Scoped Type Aliases
**Spec** (`type_aliases.md:24-28`): `type DataMap = dict[str, list[Result[T, E]]]` inside function body.
**Tests:** Only module-level and class-level aliases tested.

### G22. Mixed Variance Delegates
**Spec** (`generic_variance.md:136-156`): `delegate Transformer[in TIn, out TOut]`.
**Tests:** Covariant and contravariant delegates tested separately, never combined.

### G23. Base Class Precedence Over Interface Defaults
**Spec** (`interface_default_methods.md`): Base class methods take precedence over interface defaults.
**Tests:** Interface-vs-interface conflict tested, but base-class-vs-interface not tested.

### G24. Covariant Return Types in Properties
**Spec** (`properties_inheritance.md`): `Dog.friend -> Dog` overriding `Animal.friend -> Animal`.
**Tests:** No test for covariant property return types.

### G25. Context Manager `__exit__` Exception Parameters
**Spec** (`context_managers.md`): `__exit__(self, exc_type, exc_val, exc_tb) -> bool`.
**Tests:** Tests use `__exit__(self)` with no parameters. Spec's full signature untested.

### G26. Struct Constructor Must Initialize All Fields
**Spec** (`structs.md`): Constructor must initialize all fields (C# requirement).
**Tests:** No test validates error for partial field initialization.

### G27. Async Comprehension Rejection
**Spec** (`async_programming.md`): `async for` inside comprehensions is not supported.
**Tests:** No test validates the compile error.

### G28. `await` in Sync Comprehension Inside Async Function — Rejection
**Spec** (`async_programming.md`): `[await fetch(url) for url in urls]` not supported.
**Tests:** No test validates rejection.

### G29. Async Context Managers (`async with`)
**Spec** (`async_programming.md`): `async with AsyncResource() as resource`.
**Tests:** No test fixtures.

### G30. `global`/`nonlocal` Keyword Rejection
**Spec** (`variable_scoping.md`): `global x` → ERROR.
**Tests:** No error fixture.

### G31. Module-Level Variable Without Type Annotation → Error
**Spec** (`program_entry_point.md`): `x = 5` at module level → ERROR (requires type annotation).
**Tests:** No test validates this error.

---

## Part 5: SPEC GAPS — Low Priority

### G32. Float Trailing Decimal `5.`
Spec says valid, no test exists.

### G33. Comment-Only Lines at Column 0 Inside Indented Blocks
Spec says valid, no test exists.

### G34. `\b` (Backspace) and `\f` (Form Feed) Escape Sequences
Spec documents, no isolated tests.

### G35. Octal String Escapes (`\101`)
Tests exist (`HandlesOctalEscapeInString`) but NOT documented in spec.

### G36. Backtick Identifier Comprehensive Tests
Lexer tests exist, no end-to-end integration fixtures.

### G37. Lambda Loop Closure Capture Gotcha
Spec documents (all capture same variable), no test validates.

### G38. Yield in Nested Functions (Non-Propagating)
Spec documents, no integration test.

### G39. Positional-Only + Keyword-Only + Partial Application Combined
Spec mentions interaction, no combined test.

### G40. Nested Comprehensions (`[[i*j for j in range(3)] for i in range(3)]`)
Spec documents, no standalone test.

### G41. Tuple Spread Literal Error Test
Spec marks as "not yet implemented" but no error test exists.

### G42. `del` Statement Rejection Test
Spec says not yet supported, no test validates error.

### G43. .NET Snake_case Method Access Documentation
Tests show `system.Console.write_line()` works, but `dotnet_interop.md` doesn't document the name mangling.

### G44. Enum Value Mangling Tests
Spec says `CAPS_SNAKE_CASE` → `PascalCase`, no dedicated test.

---

## Part 6: NATURAL EXTENSIONS (Tests Beyond Spec, Compatible)

These are cases where tests exercise behavior the spec doesn't explicitly define, but the behavior is a natural consequence of documented rules. **These should be documented in the spec** to make it authoritative.

| Extension | Test Location | Spec Gap |
|-----------|---------------|----------|
| Yield in try/except/finally restrictions | `yield_in_try_except_error.spy` etc. | `generators.md` should document C# iterator limitations |
| Operator sections with `(_ + _)` (two placeholders) | `operator_sections.spy` | `partial_application.md` shows unary but not binary |
| Spread from tuples into lists/sets | `spread_list_from_tuple.spy` | `spread_operator.md` should mention tuples explicitly |
| Comprehension outer scope variable capture | `comprehension_outer_scope.spy` | `comprehensions.md` implicit but not explicit |
| Multi-for dict/set comprehensions | `multi_for_dict_comprehension.spy` | Only list comprehensions exemplified in spec |
| Loop-else with empty iterables | `ForElse_EmptyIterable_ElseExecutes` unit test | Implicit in boolean flag lowering |
| Nested loop-else independence | `ForElse_NestedLoop_IndependentElse` unit test | Not explicitly documented |
| From-import with overloaded .NET functions | `from_import_overloads.spy` | `import_statements.md` doesn't address overloads |
| From-import shadowing overloads | `from_import_overload_shadow.spy` | Not documented |
| Abstract methods with body-less syntax | `abstract_bodyless_0001.spy` | Only documented for interfaces |
| Keyword-only arguments in constructors | `constructor_kwonly_forward.spy` | Not in `constructors.md` |
| `min()`/`max()` with `default` parameter | `min_max_default.spy` | Not in `builtin_functions.md` |
| `next()` builtin | `next_default.spy` | Not in `builtin_functions.md` |
| `enumerate()` positional `start` argument | `enumerate_basic.spy` | Spec shows keyword-only |
| Bare `raise` outside except → error | `bare_raise_outside_except.error` | Not explicitly prohibited in spec |

---

## Part 7: Recommendations

### Immediate (Spec Accuracy)
1. **Resolve `.5` float literal divergence** — decide and align spec + tests
2. **Document `divmod` → `div_mod` name mangling** in builtin_functions.md
3. **Update exception hierarchy** in spec to match implementation (17+ types)
4. **Remove "planned for later"** from Optional/Result spec pages
5. **Clarify `__getitem__`/`__setitem__` implementation status** — remove stale banner if implemented

### High Priority (Missing Critical Tests)
6. **Comparison chaining** — foundational operator with zero tests
7. **Comprehension variable scoping** (no-leak, duplicate detection, walrus) — 3 fundamental semantic boundaries untested
8. **Named tuple pattern matching** — entire spec section untested
9. **Type narrowing end-to-end** — infrastructure exists but no behavioral test
10. **Try/maybe expression precedence** — spec-defined parsing rules untested
11. **Circular import with type annotations** — spec promises this works, no test

### Medium Priority (Test Coverage)
12. Add loop-else with `raise` and `return` tests
13. Add `list.get()`, `type()`, `hash()`, `isinstance()` generic rejection tests
14. Add tuple rest-pattern, count-mismatch, and function-scoped type alias tests
15. Add mixed variance delegate and base-class-over-interface-default tests
16. Add `__exit__` with exception parameters test

### Spec Enrichment (Document Extensions)
17. Document yield restrictions in try/except/finally in `generators.md`
18. Document overloaded .NET import behavior in `import_statements.md`
19. Document `next()`, `min/max default`, `enumerate start` in `builtin_functions.md`
20. Document abstract body-less syntax in `classes.md` or `inheritance.md`
21. Document .NET snake_case method access in `dotnet_interop.md`

---

## Statistics

| Category | Count |
|----------|-------|
| Spec files audited | 106 |
| Test fixtures examined | 1,434 |
| Unit test files examined | ~350 |
| **Divergences** (spec ≠ tests) | **3** |
| **Outdated spec passages** | **2** |
| **High-priority gaps** (spec defined, no test) | **16** |
| **Medium-priority gaps** | **15** |
| **Low-priority gaps** | **13** |
| **Natural extensions** (test > spec, compatible) | **~35** |
| **Spec enrichment recommendations** | **5** |
