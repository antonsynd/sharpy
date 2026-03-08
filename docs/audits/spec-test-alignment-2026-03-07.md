# Spec–Test Alignment Audit

**Date**: 2026-03-07
**Scope**: `docs/language_specification/` (110 spec files) vs `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` (1,263 `.spy` fixtures)
**Method**: 6 parallel domain analysis agents, synthesized here

---

## Executive Summary

| Domain | Spec Features | Covered | Missing | Misalignments |
|--------|--------------|---------|---------|---------------|
| Operators & Literals | 89 | 72 (81%) | 17 | 2 |
| Control Flow & Statements | 22 | 21 (95%) | 1 | 0 |
| Functions, Lambdas & Generators | 65 | 48 (74%) | 17 | 3 |
| Classes, OOP & Advanced Type Features | 65 | 56 (86%) | 9 | 3 |
| Type System | 32 | 25 (78%) | 5 | 2 |
| Builtins, Collections, Modules & Exceptions | 95 | 45 (47%) | 18 | 3 |
| **TOTAL** | **368** | **267 (73%)** | **67** | **13** |

**Overall alignment**: 73% of documented spec features have test coverage. The most under-tested domain is Builtins/Collections/Modules (47%). No critical correctness breaks were found, but 13 misalignments were identified that warrant investigation.

---

## Domain 1: Operators & Literals

### Well-Covered
Arithmetic (`+`, `-`, `*`, `//`, `%`, `**`), all augmented assignments, bitwise operators, comparison operators (including chaining), logical operators with short-circuit, identity operators (`is`, `is not None`), membership operators, string repetition, all literal types (integer bases, float suffixes `f`/`d`/`m`, scientific notation, triple-quoted strings, raw strings, escape sequences), null coalescing (`??`), null coalescing assignment (`??=`), null conditional access (`?.`), operator precedence.

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| HIGH | Numeric type promotion rules (int32+int64→int64, float32+float64→float64) | `arithmetic_operators.md` | No explicit mixed-type arithmetic tests |
| HIGH | `/` operator return type rules (integer-only → float64) | `arithmetic_operators.md` | Return type contract not verified |
| HIGH | Forbidden `float64 + decimal` (compile error) | `arithmetic_operators.md` | No error test for mixing float64 and decimal |
| HIGH | String type safety: `"str" + int` is a compile error | `string_operators.md` | No error test for implicit string + non-string |
| MED | Floor division with negative operands (`-7 // 3 = -3`) | `arithmetic_operators.md` | Negative operand semantics not tested |
| MED | Ellipsis in concrete method → throws `NotImplementedException` | `ellipsis_literal.md` | Only abstract-method ellipsis tested |
| MED | `__pow__` operator overloading | `operator_overloading.md` | No test for custom `__pow__` |
| MED | `__neg__` / `__pos__` unary operator overloading | `operator_overloading.md` | No unary dunder tests |
| MED | `__contains__` operator overloading | `operator_overloading.md` | No custom `__contains__` test |
| MED | `__hash__` / `__eq__` contract enforcement | `operator_overloading.md` | No test: `__eq__` without `__hash__` should error |
| MED | `__truediv__` vs `__div__` canonical name | `operator_overloading.md` | Tests use `__div__`; spec says `__truediv__` |
| LOW | `__floordiv__` operator overloading | `operator_overloading.md` | No test for custom `__floordiv__` |
| LOW | Operator overloading: comparison with polymorphic types | `operator_overloading.md` | `__eq__` accepting `object` not explicitly tested |
| LOW | User-defined `__iadd__` etc. explicitly rejected | `assignment_operators.md` | No test confirming these are not supported |
| LOW | Optional type `??=` comprehensive coverage | `null_coalescing_assignment.md` | Only T\|None tested; Optional variant minimal |
| DEFERRED | Conversion operators (`@implicit`, `@explicit`) | `conversion_operators.md` | Deferred post-v0.2.x; no tests expected |
| DEFERRED | `__getitem__` / `__setitem__` / `__delitem__` | `operator_overloading.md` | Codegen gaps #276, #277; no tests expected |

### Misalignments

1. **`__div__` vs `__truediv__` naming** — `operator_overloading.md` specifies `__truediv__` as the Python-canonical name for `/`, but `dunder_div_mod.spy` uses `__div__`. This may be intentional dual support, but the spec needs to be explicit about which name is canonical.
   _Files_: `operator_overloading.md` ↔ `basics/dunder_div_mod.spy`

2. **Decimal literal suffix documentation** — `float_literals.md` documents the `m`/`M` suffix in the type table but not in the "Suffix notation" subsection, creating a documentation inconsistency (no code bug).

---

## Domain 2: Control Flow & Statements

### Well-Covered
`if`/`elif`/`else`, `for` over collections and `range()` (single, start/end, step), `while`, `break`/`continue` (including outside-loop errors), loop `else`, block scoping (C# semantics), variable shadowing/versioning, walrus operator (`:=`), `assert` with/without message, `pass`, `return`, indentation validation (tabs forbidden), match statements (literal, wildcard, binding, tuple, guard, type, property, or-pattern), match exhaustiveness checking.

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| MED | `enumerate()` in `for` loops | `for_statement.md` | Spec explicitly shows `for index, name in enumerate(names)`; no dedicated test |

### Misalignments
None found. All tested features align with specification.

---

## Domain 3: Functions, Lambdas & Generators

### Well-Covered
Basic function definitions, default parameters, named arguments, variadic `*args`, positional-only parameters (`/`), keyword-only parameters (`*`), mixed parameter markers, function type annotations, lambdas (single/multi-arg, closures, defaults, higher-order), generators (`yield`, early `return`, `yield from`, `__iter__`, `__reversed__`), async functions and `await`, async generators, async `for`/`with`, partial application, pipe operator (`|>`), spread operator in calls.

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| HIGH | Function type variance: covariant return types, contravariant parameters | `function_types.md` | No tests for `() -> Dog` assignable to `() -> Animal` |
| MED | Parameter names in function types are irrelevant to compatibility | `function_types.md` | `(x: int) -> int` and `(y: int) -> int` not tested as equivalent |
| MED | Ellipsis in non-abstract method → `throw new NotImplementedException()` | `function_definition.md` | Only abstract-method usage tested |
| MED | Docstring-only function body compiles as empty | `function_definition.md` | No test for docstring-only function |
| MED | Walrus operator inside lambda body | `lambdas.md` | `lambda x: (y := x * 2, y + 1)[-1]` not tested |
| MED | Nested function with `yield` doesn't make enclosing function a generator | `generators.md` | No explicit test for this scoping rule |
| MED | `yield from` inside `__next__` is an error | `generators.md` | Edge case of SPY0268 (base case tested, `yield from` variant not) |
| MED | Mixed direct + spread arguments: `sum_all(1, 2, *[3, 4], 5)` | `function_variadic_arguments.md` | No test for mixed positional + spread in one call |
| MED | Async `yield from` delegating to async iterable | `async_programming.md` | Only sync→async delegation tested; async→async not tested |
| MED | `asyncio.gather` with multiple concurrent tasks + error handling | `async_programming.md` | Basic gather tested; multiple types + errors not covered |
| MED | Pipe operator error: cannot pipe to constructor | `pipe_operator.md` | No compile error test for `data \|> MyClass()` |
| LOW | Lambda type inference: generic function infers from other args | `lambdas.md` | `transform([1,2,3], lambda x: str(x))` pattern not tested |
| DEFERRED | `ref[T]`, `out[T]`, `in[T]` parameter modifiers | `parameter_modifiers.md` | Deferred post-v0.2.x; no tests expected |
| DEFERRED | Spread into tuple literals: `(*first, *second)` | `spread_operator.md` | Not yet implemented; no tests expected |
| DEFERRED | Object copy with overrides: `obj.copy(**{"age": 31})` | `spread_operator.md` | Not yet implemented; no tests expected |
| DEFERRED | `**kwargs` dict spreading at call site | `spread_operator.md` | Not supported; no tests expected |
| DEFERRED | `async` comprehensions | `async_programming.md` | Not supported; no tests expected |

### Misalignments

1. **Lambda parameter type annotation syntax** — `lambdas.md` states types are "inferred from context" with no inline annotation syntax, but `functions/lambda_basic_0001.spy` uses `lambda x: int: x + 1` (explicit type annotation). The spec should clarify whether inline annotations are supported or document this syntax.
   _Files_: `lambdas.md` ↔ `functions/lambda_basic_0001.spy`

2. **Async generator `yield from` with async iterables** — `async_programming.md` documents auto-selection between `foreach` and `await foreach` for both sync and async sources, but tests only cover the sync source case.
   _Files_: `async_programming.md` ↔ `async/async_generator_yield_from.spy`

3. **Async constructor rejection** — `async_programming.md` states `async def __init__` is rejected with SPY0358. Test `async_init_error.spy` correctly verifies this. (No contradiction — listed for completeness.)

---

## Domain 4: Classes, OOP & Advanced Type Features

### Well-Covered
Class definitions, constructors, abstract/virtual/override/final methods, single inheritance with `super()`, multiple interface implementation, interface default methods, interface inheritance, auto-properties (read-write, read-only `property get`, init-only `property init`), function-style properties, abstract/virtual property overrides, explicit interface implementation, structs (value semantics, zero-initialization), integer and string enums, events (auto and function-style, static, inheritance), delegates with variance, dunder methods (`__init__`, `__eq__`, `__hash__`, `__lt__`, `__getitem__`, `__setitem__`, `__str__`, `__repr__`, `__len__`, `__contains__`, `__iter__`), access modifiers (`@public`, `@protected`, `@private`, `@internal`), method overloading, decorators, static methods.

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| HIGH | Multiple interface conflict: class must resolve duplicate default methods | `interface_default_methods.md` | No test for conflict resolution error when class doesn't implement |
| MED | Cross-dunder synthesis inside dunders (`__le__` calling `__lt__` + `__eq__`) | `dunder_invocation_rules.md` | No dedicated test for cross-dunder synthesis pattern |
| MED | Dunder capture prevention: `func = self.__eq__` should be an error | `dunder_invocation_rules.md` | No compile error test for dunder reference capture |
| MED | Covariant return types on property overrides | `properties_inheritance.md` | No test for `property get animal(self) -> Dog` overriding `-> Animal` |
| MED | Interface auto-properties with default values are optional to implement | `properties_inheritance.md` | No test for optional interface property semantics |
| MED | `property init` without default must be set in every constructor | `properties.md` | No error test for constructor omitting required `init` property |
| MED | Protocol interface synthesis (SPY1001 diagnostic) | `dunder_methods.md` | No test verifying SPY1001 is emitted when `ISized`, `IBoolConvertible`, etc. are synthesized |
| LOW | String enum lowering to static class with constants | `enums.md` | No explicit test for string enum → C# static class compilation |
| LOW | Struct parameter modifiers (`in[T]`, `mut[T]`, `out[T]`) to avoid copies | `structs.md` | No tests for reference parameters on structs |

### Misalignments

1. **`@override` requirement for dunder methods** — `dunder_invocation_rules.md` specifies when `@override` is optional vs. required for dunders, but test coverage does not comprehensively verify all cases. Some dunder tests use `@override` where it should be optional; the enforcement boundary is unclear from tests alone.
   _Files_: `dunder_invocation_rules.md` ↔ `classes/dunder_str_*.spy`

2. **Operator overloading return type "compatibility" definition** — `operator_overloading.md` requires return type to be "same type as `self` or compatible" but does not define "compatible" precisely. Tests use exact return types only; no test validates the variance boundary.
   _Files_: `operator_overloading.md` ↔ `classes/dunder_arithmetic.spy`

3. **`property init` enforcement** — `properties.md` states init properties without defaults must be set in every constructor, but no error test verifies that omitting assignment in a constructor is caught at compile time.
   _Files_: `properties.md` ↔ `properties/auto_property_init.spy`

---

## Domain 5: Type System

### Well-Covered
Type annotations (basic + inference), type annotation shorthand (list/dict/set/tuple), type aliases (generic and non-generic), generic classes and functions with constraints, generic variance (covariance `out`, contravariance `in`, nesting, error cases), Optional type (`T?`/`Optional[T]`) with narrowing, Result type (`T!E`) with Ok/Err/unwrap, named tuples (field access, positional indexing, pattern matching), tuple unpacking (simple, nested, rest, loops, comprehensions), type narrowing (`is not None`, `isinstance()`), tagged unions (user-defined with pattern matching), try expressions, maybe expressions, nullable types (`T | None`), null coalescing.

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| HIGH | Array type `array[T]` and postfix `T[]`, `int[][]` | `primitive_types.md`, `type_annotation_shorthand.md` | No tests for array creation, indexing, or postfix syntax |
| HIGH | Statically impossible cast detection: `x: int = 42; x to str` → compile error | `type_casting.md` | Only unrelated-class casts tested; invalid primitive casts not tested |
| HIGH | .NET interop: `from system.io import File`, `.NET` property access, extension methods | `dotnet_interop.md` | No tests for .NET type imports or interop patterns |
| MED | Safe cast edge cases: `Dog? to Cat?`, always-safe cast warnings | `type_casting.md` | No tests for nullable→nullable cast or always-safe cast compile warnings |
| MED | Union case name shorthand in returns: `return Ok(42)` instead of `return Result.Ok(42)` | `tagged_unions.md` | Tests use only explicit form; shorthand syntax untested |
| LOW | Type annotation shorthand error: empty `{}` is ambiguous | `type_annotation_shorthand.md` | No error test for ambiguous empty braces |
| LOW | Type annotation shorthand error: mixed named/unnamed tuple fields | `type_annotation_shorthand.md` | No test for partial tuple field naming error |

### Misalignments

1. **Safe cast operator: `as` vs `to`** — `type_casting.md` establishes `value to T` (throwing) and `value to T?` (safe) as the canonical cast syntax, with `as` appearing only in `try` expressions. However, `expressions/safe_cast.spy` uses `x as int` (C# style) directly outside a `try` context. The spec and tests are inconsistent on which keyword is canonical.
   _Files_: `type_casting.md` ↔ `expressions/safe_cast.spy`

2. **Union case shorthand in returns** — Spec documents shorthand `return Err("msg")` as equivalent to `return Result.Err("msg")`, but all tests use the explicit form. The shorthand is untested, leaving uncertainty about whether it actually works.
   _Files_: `tagged_unions.md` ↔ `optional_result/result_type_0004.spy`, `unions/union_basic.spy`

---

## Domain 6: Builtins, Collections, Modules & Exceptions

### Well-Covered
`str()`, `int()`, `float()`, `bool()`, `len()`, `min()`, `max()`, `sorted()`, `reversed()`, `abs()`, `pow()`, `id()`, `isinstance()`, list/dict/set literals and comprehensions, slicing, collection methods, tuple unpacking, comprehension variable scoping, `import`/`from...import` (basic and aliased), cross-module inheritance, `try`/`except`/`finally`, multiple `except` clauses, `except E as e`, bare `raise`, exception aliases, try/else scoping, `with` statement (custom context managers, multiple resources, exception propagation), f-strings (basic, nested, builtin calls in expressions).

### Missing Test Coverage

| Priority | Feature | Spec File | Gap |
|----------|---------|-----------|-----|
| HIGH | `enumerate()` builtin | `builtin_functions.md` | Only manual implementation found; no direct builtin call test |
| HIGH | `zip()` builtin | `builtin_functions.md` | Only manual implementation found; no direct builtin call test |
| HIGH | `filter()` builtin | `builtin_functions.md` | Not used in any test fixture |
| HIGH | `map()` builtin | `builtin_functions.md` | Not used in any test fixture (Result.map is different) |
| HIGH | `all()` / `any()` builtins | `builtin_functions.md` | Only manual implementations found |
| HIGH | `sum()` builtin | `builtin_functions.md` | Not called as builtin in any test fixture |
| MED | `repr()` builtin | `builtin_functions.md` | Not tested anywhere |
| MED | `round()` builtin | `builtin_functions.md` | No direct tests |
| MED | `divmod()` builtin | `builtin_functions.md` | Not tested |
| MED | `type()` builtin | `builtin_functions.md` | No dedicated test |
| MED | `float.parse()` returning Result | `builtin_functions.md` | Only `int.parse()` tested in optional_result tests |
| MED | F-string dictionary literals require parentheses: `{(dict_lit)}` | `fstrings.md` | No test for required parenthesis syntax |
| MED | F-string dynamic format specifiers: `f"{pi:.{precision}f}"` | `fstrings.md` | Not tested |
| MED | String UTF-16 semantics with emoji/surrogate pairs | `string_type.md` | No tests for code unit behavior with non-BMP characters |
| MED | String methods: `find`, `rfind`, `replace`, `count`, `isdigit`, `isalpha`, `isalnum`, `isspace`, `lstrip`, `rstrip`, `startswith`, `endswith` | `string_type.md` | Only `upper`, `lower`, `strip`, `split`, `join` tested |
| MED | Circular imports allowed in type annotations | `module_system.md` | No test demonstrating circular forward references in annotations |
| MED | Walrus operator scoping in comprehensions (comprehension-local, unlike Python 3.8+) | `comprehensions.md` | Variable-leakage check not tested |
| LOW | `input()` builtin | `builtin_functions.md` | No test (I/O function; may be intentionally excluded) |

### Misalignments

1. **`raise ... from ...` exception chaining** — `exception_handling.md` explicitly states `raise ... from ...` is not supported and should produce a compile error. `exception_handling/raise_from_0001.spy` exists with a corresponding `.error` file. This is aligned (test verifies the error), but the test content should be audited to ensure the error message is informative.
   _Files_: `exception_handling.md` ↔ `exception_handling/raise_from_0001.spy`

2. **Comprehension variable scoping** — `comprehensions.md` states variables do not leak to outer scope (unlike Python). `collections/comprehension_outer_scope.spy` tests outer variable *capture* but does not explicitly verify that new variables defined in the comprehension don't leak out. The scoping boundary test is incomplete.
   _Files_: `comprehensions.md` ↔ `collections/comprehension_outer_scope.spy`

3. **F-string brace escaping** — `fstrings.md` documents `{{` → `{` escape syntax but notes known gap #287 ("brace escaping generates invalid C#"). Test `fstrings/fstring_brace_escape.spy` exercises this known-broken feature. This is a deferred bug, not a spec contradiction, but a test for a broken feature may produce false positives.
   _Files_: `fstrings.md` ↔ `fstrings/fstring_brace_escape.spy`

### Deferred Features with Tests

| Feature | Test | Notes |
|---------|------|-------|
| `raise ... from ...` | `exception_handling/raise_from_0001.spy` | Correctly tests compile error for unsupported syntax |
| F-string brace escaping | `fstrings/fstring_brace_escape.spy` | Tests known-broken feature (issue #287) |

---

## Cross-Domain Themes

### Theme 1: Ellipsis Body in Non-Abstract Methods
Three domains identified this gap independently (Operators, Functions, Classes). The spec states that `...` in a non-abstract concrete method should lower to `throw new NotImplementedException()`. No test exercises this runtime path.
**Action**: Add `basics/ellipsis_concrete_method.spy` that calls a concrete method with `...` body and verifies `NotImplementedException` is thrown.

### Theme 2: Operator Dunder Method Completeness
Operators domain found 18+ missing dunder overloading tests. Classes domain found cross-dunder synthesis gap. These form a coherent test gap: operator overloading is partially tested at the surface level but the full dunder contract (return types, hash/eq contract, unary operators, capture prevention) is under-tested.

### Theme 3: Deferred Features Appearing in Spec
Several spec files document features marked "Deferred post-v0.2.x" or "Not yet implemented". In most cases there are no tests (correct). Exceptions:
- `raise ... from ...` — has a compile-error test ✓ (correctly verifies rejection)
- F-string brace escaping — has a test for a known-broken feature (issue #287), which may be confusing

### Theme 4: Missing Builtin Function Tests
The `builtin_functions.md` spec documents ~19 builtin functions. Only about 7 have dedicated test fixtures. Functions like `enumerate`, `zip`, `filter`, `map`, `all`, `any`, `sum` are exercised indirectly (or via manual implementations) but have no standalone tests that confirm the builtin works correctly.

### Theme 5: .NET Interop is Untested
`dotnet_interop.md` documents a complete interop model (type imports, properties, extension methods, `IDisposable`/`with`). No integration tests cover any of this. Given that Sharpy runs on .NET, interop is a primary use case.

---

## Prioritized Action List

### P0 — Correctness Risk (missing error tests may mask bugs)
1. Add error test: `float64 + decimal` → compile error (`arithmetic_operators.md`)
2. Add error test: `"str" + int` → type error (`string_operators.md`)
3. Add error test: `__eq__` defined without `__hash__` → compile error (`operator_overloading.md`)
4. Add error test: statically impossible cast `x: int = 42; x to str` → compile error (`type_casting.md`)
5. Add error test: multiple interface default method conflict → compile error (`interface_default_methods.md`)
6. Investigate `as` vs `to` operator inconsistency in `expressions/safe_cast.spy` vs `type_casting.md`
7. Investigate `__div__` vs `__truediv__` naming: clarify canonical dunder name in spec or compiler

### P1 — Spec Features Completely Untested
8. Add tests for builtins: `enumerate()`, `zip()`, `filter()`, `map()`, `all()`, `any()`, `sum()`, `repr()`, `round()`, `divmod()` (`builtin_functions.md`)
9. Add tests for `array[T]` type and postfix `T[]` syntax (`primitive_types.md`)
10. Add tests for .NET interop: import system types, access .NET properties, use `IDisposable` with `with` (`dotnet_interop.md`)
11. Add tests for string methods: `find`, `rfind`, `replace`, `count`, `isdigit`, `startswith`, `endswith`, `lstrip`, `rstrip` (`string_type.md`)
12. Add test: `enumerate()` in `for` loop (`for_statement.md`)
13. Add test: union case shorthand `return Ok(42)` without union type prefix (`tagged_unions.md`)

### P2 — Spec Features Partially Tested
14. Add tests for numeric type promotion matrix (`arithmetic_operators.md`)
15. Add tests for function type variance (covariant return, contravariant params) (`function_types.md`)
16. Add tests for `yield from` with async iterables in async generators (`async_programming.md`)
17. Add tests for walrus operator in lambda body (`lambdas.md`)
18. Add tests for mixed positional + spread arguments in one call (`function_variadic_arguments.md`)
19. Add tests for comprehension variable scoping: verify leak prevention (`comprehensions.md`)
20. Add tests for covariant property return type overrides (`properties_inheritance.md`)
21. Add test: `property init` without default must be set in constructor or error (`properties.md`)
22. Add test: protocol interface synthesis emits SPY1001 diagnostic (`dunder_methods.md`)

### P3 — Documentation Alignment
23. Update `operator_overloading.md` to clarify canonical dunder names (`__div__` vs `__truediv__`)
24. Update `lambdas.md` to document or forbid inline type annotation syntax `lambda x: int:`
25. Update `float_literals.md` to include `m`/`M` suffix in the "Suffix notation" subsection
26. Clarify `type_casting.md`: explicitly state `as` keyword is also valid in direct safe-cast position, or update tests to use `to T?`

---

## Appendix: Test Fixtures Without Any Spec Coverage

The following test behaviors were identified without a corresponding spec document:

| Test | Behavior | Recommendation |
|------|----------|----------------|
| `classes/enum_method_name_collision.spy` | Enum method name conflicts with value accessor | Document in `enums.md` |
| `properties/error_field_name_conflict.spy` | Field and property name collision within class | Document in `classes.md` or `properties.md` |
| `type_system/clr_generic_*.spy` | CLR generics like `IComparable[T]` as constraints | Document in `generics.md` or `dotnet_interop.md` |
| `functions/identity_functions.spy` | Identity/passthrough function patterns | Low priority; general language feature |
| `functions/comparison_functions.spy` | Functions returning bool | Low priority; general language feature |

---

_Generated by spec-test alignment audit team — 6 domain analysis agents, 2026-03-07_
