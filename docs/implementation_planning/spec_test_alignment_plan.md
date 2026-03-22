<!-- Verified by /verify-plan on 2026-03-19 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Spec-Test Alignment: Implementation, Testing, and Documentation

## Context

The [spec-vs-test alignment synthesis audit](docs/audits/spec-test-alignment-synthesis-audit.md) identified 8 divergences, 2 outdated spec passages, 47 coverage gaps, 10 ambiguities, and 9 extension enrichments across the Sharpy compiler, stdlib, and language specification. Anton's inline decisions in the audit override the original engineering assessments where they differ.

This plan implements Anton's decisions: fix implementations where specified, add missing tests, update specs, and create GitHub issues for items deferred to later discussion.

**Concurrency note:** The syntax consolidation plan (now archived in `docs/completed_plans/`) is fully complete (Phases 1-4 done). Its Phase 5 (Spec Alignment) was superseded by this plan per Anton's audit decisions.

## Current State

Research verified the following items are **already implemented and tested** (no work needed):
- GAP-1 (comparison chaining), GAP-3 (comprehension scoping), GAP-9 (type narrowing E2E 74+ tests), GAP-11 (try/maybe precedence), GAP-12 (loop-else with return/raise), GAP-14 (type/hash builtins), GAP-23 (mixed variance delegates), GAP-24 (base vs interface precedence), GAP-28 (module-level annotation required), GAP-32 (backtick identifier E2E), GAP-33 (lambda loop closure), GAP-45 (struct init all fields)

Research verified these **partially exist** and need targeted additions:
- GAP-5 (class/struct constraints — parser implemented, tests only for interface constraints)
- AMB-7 (maybe on already-optional — has test), AMB-9 (.NET snake_case — has test), AMB-10 (None vs None() — has error tests but needs "Did you mean?" hints)

## Design Decisions

- **DIV-1 (Decimal m/M):** Implement fully — lexer already handles suffix, semantic type exists. Anton: "It aligns with C# and someone will want it."
- **DIV-3 (Leading decimal .5):** Implement — align with Python and C#. Overrides syntax consolidation plan which said "require leading digit."
- **DIV-4 (divmod naming):** C# method should be `Divmod()` which snake-cases to `divmod`. No exception list — every stdlib function must adhere to naming convention.
- **DIV-7 (Scientific notation):** Output lowercase `e` — Axiom 2 (Python syntax) wins for cosmetic output, not Axiom 1. Overrides syntax consolidation plan which said "just document."
- **GAP-6 (array[T]):** Implement — "we should actually implement this so it works."
- **GAP-8 (Circular imports):** Defer to GitHub issue.
- **GAP-25 (Context manager __exit__):** Defer to GitHub issue — needs scoping for both signatures.
- **GAP-43 (@classmethod):** Should yield error telling user to use `@static` instead. Same for `@staticmethod` if not already.
- **AMB-10 (None vs None()):** Add "Did you mean?" compiler hints.
- **Extension 7 (bodyless methods):** Do NOT document as working — syntax consolidation plan deprecates them.

---

## Implementation

### Phase 1: Quick Implementation Fixes ✅ COMPLETED

**Goal:** Fix small divergences between spec and implementation.

#### Tasks

1. **Implement leading-decimal float literals (.5)** — `src/Sharpy.Compiler/Lexer/Lexer.Literals.cs`
   - Modify `ReadNumber()` to handle tokens starting with `.` followed by a digit
   - Currently the decimal point check at lines 357-358 requires a preceding digit
   - Add new entry point: when current char is `.` and next is a digit, read as float literal
   - Add test fixtures: `float_leading_decimal.spy` + `.expected` (`.5`, `.123`, `.0001`)
   - Add negative test: `.` alone is still member access, not a float
   - Update `docs/language_specification/float_literals.md` to confirm `.5` is valid (spec already says this)
   - Acceptance: `print(.5)` outputs `0.5`, `print(.5 + .5)` outputs `1.0`
   - Commit: `feat(lexer): support leading-decimal float literals (.5, .123)`

2. **Fix scientific notation to output lowercase e** — `src/Sharpy.Core/Str.cs` [CORRECTED: `Partial.Str/` was removed 2026-02-14; `Str.cs` is now at root level in Sharpy.Core]
   - In the `Str()` method for doubles/floats (lines ~125, ~158), post-process `ToString("R", ...)` to replace uppercase `E` with lowercase `e`
   - Update test fixture `float_scientific_notation.expected` from `1E+20` to `1e+20`, etc.
   - Acceptance: `print(1e20)` outputs `1e+20` not `1E+20`
   - Commit: `fix(core): output lowercase e in scientific notation (Axiom 2)`

3. **Fix divmod naming** — `src/Sharpy.Core/DivMod.cs`, test fixtures
   - C# method is currently `DivMod()` (verified) — name mangler produces `div_mod` from PascalCase `DivMod`. Rename all 4 overloads to `Divmod()` so the mangler produces `divmod`
   - Update test fixture `divmod_basic.spy` to call `divmod(17, 5)` instead of `div_mod(17, 5)`
   - Update `docs/language_specification/builtin_functions.md` to say `divmod(a, b)`
   - Acceptance: `print(divmod(17, 5))` outputs `(3, 2)`
   - Commit: `fix(core): rename DivMod to Divmod for correct snake_case mangling`

4. **Add global/nonlocal as reserved keywords with errors** — `src/Sharpy.Compiler/Lexer/Lexer.cs`
   - `global` and `nonlocal` are NOT in the keyword table (verified) — add `TokenType.Global` and `TokenType.Nonlocal` entries to the keywords dictionary in `Lexer.cs` and add the enum values to `TokenType`
   - In the parser, when these keywords are encountered as statements, emit a clear error: "Sharpy does not support 'global' — C# scoping rules apply (Axiom 1)"
   - Add test fixtures: `global_rejected.error`, `nonlocal_rejected.error`
   - Acceptance: `global x` produces SPY error with helpful message
   - Commit: `feat(parser): reject global/nonlocal with helpful Axiom 1 error`

5. **Update @classmethod/@staticmethod rejection messages** — `src/Sharpy.Compiler/Semantic/Validation/DecoratorValidator.cs`
   - `@classmethod` is already rejected (verified, line 38) but message says only "is not supported in Sharpy" — update to suggest `@static`
   - `@staticmethod` is already rejected (verified, lines 36-37) with message "Methods without a 'self' parameter are automatically static" — update to also mention `@static` decorator for fields
   - Test `staticmethod_decorator_unsupported.spy` already exists (verified in `errors/` directory)
   - Add test fixture: `classmethod_decorator_unsupported.error`
   - Acceptance: `@classmethod` and `@staticmethod` both produce errors suggesting `@static`
   - Commit: `feat(semantic): update @classmethod/@staticmethod rejection to suggest @static`

---

### Phase 2: Decimal Type Completion (DIV-1) ✅ COMPLETED

**Goal:** Wire the decimal type through the full pipeline. Lexer and SemanticType already exist.

#### Tasks

6. **Verify decimal type pipeline** — multiple files
   - Lexer already accepts `m`/`M` suffix (verified, `Lexer.Literals.cs` line 446), `SemanticType.Decimal` singleton exists (verified, `SemanticType.cs` line 72), and `PrimitiveCatalog` fully registers decimal with `NumericKind.Decimal` (verified, `Registry/PrimitiveCatalog.cs` line 89)
   - Check parser: does it produce a typed literal node for decimal? If not, add handling
   - Check TypeResolver/TypeChecker: is `decimal` recognized as a type annotation?
   - Check CodeGen: does RoslynEmitter map `decimal` → `decimal` in C#?
   - Check TypeMapper: is decimal in the type mapping table?
   - Add test fixtures: `decimal_basic.spy` + `.expected` (`3.14m` arithmetic, `decimal` type annotation)
   - Add negative test: `decimal_overflow.error` if applicable
   - Update `docs/language_specification/primitive_types.md` to mark decimal as available (not reserved)
   - Acceptance: `x: decimal = 3.14m; print(x + 1.0m)` compiles and runs correctly
   - Commit: `feat: wire decimal type through full compiler pipeline`

---

### Phase 3: Critical Test Coverage (mostly done — 3 items skipped for future)

**Goal:** Add tests for implemented features with zero coverage. Verify each feature works before writing the test; implement/fix if broken.

#### Tasks

7. **UTF-16 string semantics tests (GAP-2)** — test fixtures
   - First verify with `/quick-check`: `print(len("😀"))` — should output `2` (UTF-16 code units)
   - Verify: `print("😀"[0])` — document actual behavior (surrogate half?)
   - If behavior matches spec (UTF-16), add test fixtures:
     - `string_utf16_len.spy` + `.expected`: `len("😀") == 2`
     - `string_utf16_indexing.spy` + `.expected`: indexing into surrogate pairs
   - If behavior doesn't match, fix implementation to use UTF-16 semantics
   - Acceptance: Tests pass reflecting UTF-16 code unit semantics
   - Commit: `test: add UTF-16 string semantics coverage (GAP-2)`

8. **Walrus operator in comprehensions (GAP-4)** — test fixtures
   - Verify with `/quick-check`: `result = [y := x * 2 for x in range(3)]` — walrus var `y` should NOT leak
   - Add test fixture: `walrus_comprehension_local.spy` + `.expected` showing walrus is comprehension-local
   - Add negative test: `walrus_comprehension_leak.error` accessing walrus var outside comprehension
   - If walrus leaks (Python behavior), fix to match Sharpy spec (comprehension-local)
   - Acceptance: Walrus variables in comprehensions are scoped to the comprehension
   - Commit: `test: add walrus-in-comprehension scoping tests (GAP-4)`

9. **class/struct type constraints (GAP-5)** — test fixtures
   - Verify with `/quick-check`: `def foo[T: class](x: T) -> T: return x` works
   - Verify: `def bar[T: struct](x: T) -> T: return x` works
   - Verify: `def baz[T: class & ISomething](x: T) -> T: return x` (compound constraint)
   - Add test fixtures:
     - `generic_class_constraint.spy` + `.expected`
     - `generic_struct_constraint.spy` + `.expected`
     - `generic_compound_constraint.spy` + `.expected`
   - Negative: `generic_class_constraint_violation.error` (pass struct where class required)
   - Acceptance: class/struct constraints enforced at compile time
   - Commit: `test: add class/struct generic constraint coverage (GAP-5)`

10. **Named tuple pattern matching (GAP-7)** — test fixtures
    - Verify with `/quick-check`: match on named tuple fields `case (x=0.0, y=0.0):`
    - If implemented, add test fixture: `match_named_tuple.spy` + `.expected`
    - If not implemented, create GitHub issue and add `.skip` file
    - Acceptance: Named tuple field matching works in match statements
    - Commit: `test: add named tuple pattern matching coverage (GAP-7)`

11. **Comprehension duplicate variable error (GAP-10)** — test fixtures
    - Verify with `/quick-check`: `[x for x in range(3) for x in range(3)]` — should be compile error
    - Add test fixture: `comprehension_duplicate_var.error` with expected error substring
    - If not rejected, implement the check in semantic analysis
    - Acceptance: Duplicate comprehension variables produce compile error
    - Commit: `test: add comprehension duplicate variable error test (GAP-10)`

12. **isinstance() with generic type rejection (GAP-15)** — test fixtures
    - Verify with `/quick-check`: `isinstance(x, list[int])` — should error due to generic erasure
    - Add test fixture: `isinstance_generic_type_error.error`
    - If not rejected, implement the check
    - Acceptance: `isinstance(x, list[int])` produces compile error
    - Commit: `test: add isinstance generic type rejection test (GAP-15)`

13. **type(None) compile error (GAP-17)** — test fixtures
    - Verify with `/quick-check`: `type(None)` — should produce error per spec
    - Add test fixture: `type_none_error.error`
    - If not rejected (research says it returns `typeof(object)`), implement the rejection
    - Acceptance: `type(None)` produces compile error
    - Commit: `test: add type(None) compile error test (GAP-17)`

---

### Phase 4: array[T] Type Implementation (GAP-6) ✅ COMPLETED (negative indexing skipped)

**Goal:** Implement the array[T] type for .NET interop. This is a full-pipeline feature.

**Note:** [CORRECTED: Parser already supports `T[]` suffix syntax (`Parser.Types.cs` lines 96-117) which creates a GenericType with name "array". `TypeMapper` already has `MakeArrayType()` (`TypeMapper.cs` line 763). The main work is wiring these through semantic analysis and ensuring end-to-end compilation works.]

#### Tasks

14. **Lexer/Parser: array type syntax** — `src/Sharpy.Compiler/Parser/Parser.Types.cs`
    - Parser already supports `T[]` suffix syntax (verified, lines 96-117) — creates a type annotation with `Name = "array"` and `TypeArguments` containing the element type
    - Decide whether to also support `array[int]` identifier syntax in addition to `int[]`; if yes, add `array` as a recognized type name
    - Commit: `feat(parser): add array[T] type syntax` (may be mostly done)

15. **Semantic: array type resolution** — `src/Sharpy.Compiler/Semantic/`
    - Register `array` as a builtin generic type (like `list`, `dict`, `set`) so that `TypeResolver` resolves the "array" GenericType created by the parser
    - `int[]` (parsed as `array` with type arg `int`) resolves to `int[]` in C#
    - Type checking: arrays are fixed-size, support indexing, iteration
    - Commit: `feat(semantic): resolve array[T] type to CLR arrays`

16. **CodeGen: array type emission** — `src/Sharpy.Compiler/CodeGen/`
    - `TypeMapper.MakeArrayType()` already exists (verified, `TypeMapper.cs` line 763) — wire the "array" GenericType through to use it
    - Support array creation: `int[](5)` → `new int[5]` or `list.to_array()` / similar
    - Support indexing, length, iteration
    - Commit: `feat(codegen): emit CLR arrays for array[T]`

17. **Sharpy.Core: array helpers** — `src/Sharpy.Core/`
    - Add any needed runtime helpers for array operations
    - Python-style negative indexing on arrays
    - Commit: `feat(core): add array runtime helpers`

18. **Tests and spec** — test fixtures + docs
    - `array_basic.spy` + `.expected`: creation, indexing, length (using `int[]` syntax)
    - `array_from_list.spy` + `.expected`: converting list to array
    - `array_negative_index.spy` + `.expected`: negative indexing
    - `array_type_annotation.spy` + `.expected`: `x: int[]` (using `T[]` syntax already supported by parser)
    - Update `docs/language_specification/` array documentation
    - Commit: `test: add array[T] type test coverage (GAP-6)`

---

### Phase 5: High-Priority Test Coverage (mostly done — 5 items skipped for future)

**Goal:** Add tests for remaining high and medium gaps. Verify first, implement if broken.

#### Tasks

19. **Type alias at class/function scope (GAP-18)** — test fixtures
    - Verify: `type` alias inside a class body and inside a function body
    - Add fixtures: `type_alias_class_scope.spy`, `type_alias_function_scope.spy`
    - If not working, narrow the spec to module-level only
    - Commit: `test: add scoped type alias tests (GAP-18)`

20. **Constraint reordering (GAP-19)** — test fixtures
    - Verify: write constraints in "wrong" C# order (e.g., interface before `class`) and confirm compiler silently reorders
    - Add fixture: `generic_constraint_reorder.spy` + `.expected`
    - Commit: `test: add generic constraint reordering test (GAP-19)`

21. **Pipe operator negative tests (GAP-20)** — test fixtures
    - Add: `pipe_to_constructor.error` (can't pipe to constructor)
    - Add: `pipe_to_operator.error` (can't pipe to operator)
    - Verify these are correctly rejected; implement rejection if not
    - Commit: `test: add pipe operator negative tests (GAP-20)`

22. **Tuple rest-patterns and count mismatch (GAP-21)** — test fixtures
    - Add: `tuple_rest_pattern.spy` + `.expected` (star-unpack with tuples)
    - Add: `tuple_unpack_count_mismatch.error` (SPY0239)
    - Commit: `test: add tuple rest-pattern and mismatch tests (GAP-21)`

23. **Match expressions in complex contexts (GAP-22)** — test fixtures
    - Add: `match_expr_as_argument.spy` + `.expected` (match expression as function argument)
    - Add: `match_expr_in_concatenation.spy` + `.expected` (match in string concat or arithmetic)
    - Commit: `test: add match expression context tests (GAP-22)`

24. **Async feature tests (GAP-26)** — test fixtures + GitHub issues
    - Add negative tests for documented rejections:
      - `async_comprehension_error.error` (async for in comprehension)
      - `await_in_sync_comprehension.error` (await in non-async comprehension)
    - Verify async with works (test exists: `async_with_basic.spy`)
    - Create GitHub issues for any unimplemented async features
    - Commit: `test: add async feature rejection tests (GAP-26)`

25. **list.get() method (GAP-13)** — `src/Sharpy.Core/Partial.List/`
    - Implement `get(index)` returning `Optional<T>` (like dict.get())
    - Implement `get(index, default)` returning `T`
    - Add fixture: `list_get.spy` + `.expected`
    - Commit: `feat(core): implement list.get() with Optional return (GAP-13)`

26. **del statement rejection (GAP-38)** — test fixtures
    - `del` is already a keyword. Verify it produces an error when used
    - Add: `del_rejected.error`
    - Commit: `test: add del statement rejection test (GAP-38)`

27. **Future keywords reserved (GAP-42)** — test fixtures
    - `defer` and `do` are already reserved. Add tests using them as identifiers/statements
    - Add: `reserved_keyword_defer.error`, `reserved_keyword_do.error`
    - Commit: `test: add reserved keyword rejection tests (GAP-42)`

28. **None vs None() helpful errors (AMB-10)** — `src/Sharpy.Compiler/Semantic/`
    - When user writes `None` where `None()` is needed (Optional context), add hint: "Did you mean None()?"
    - When user writes `None()` where `None` is needed (nullable context), add hint: "Did you mean None?"
    - Add test fixtures verifying the hints appear in error messages
    - Commit: `feat(semantic): add 'Did you mean?' hints for None vs None()`

---

### Phase 6: Low-Priority Test Coverage (mostly done — 1 item skipped)

**Goal:** Fill remaining test gaps from GAP-29 through GAP-47 and remaining items.

#### Tasks

29. **Float trailing decimal (GAP-29)** — Verify `5.` behavior, add test
    - Commit: `test: add trailing-decimal float test (GAP-29)`

30. **\b/\f escape sequences (GAP-30)** — Add isolated test for backspace/form-feed escapes
    - Commit: `test: add \\b/\\f escape sequence tests (GAP-30)`

31. **Octal string escapes spec update (GAP-31)** — Update spec to document octal escapes
    - Commit: `docs: add octal escape documentation to spec (GAP-31)`

32. **Yield in nested functions (GAP-34)** — Test inner generator doesn't propagate to outer
    - Commit: `test: add nested function yield isolation test (GAP-34)`

33. **Positional-only + keyword-only + partial (GAP-35)** — Add combinatorial test
    - Commit: `test: add combined parameter modifier test (GAP-35)`

34. **Nested comprehensions (GAP-36)** — Add `[[i*j for j in range(3)] for i in range(3)]` test
    - Commit: `test: add nested comprehension test (GAP-36)`

35. **Tuple spread literal error (GAP-37)** — Add `.error` fixture
    - Commit: `test: add tuple spread literal error test (GAP-37)`

36. **Chained identity operators (GAP-39)** — Verify `a is b is c`, add test
    - Commit: `test: add chained identity operator test (GAP-39)`

37. **Mixed comparison chains (GAP-40)** — Verify `a < b in c`, add test
    - Commit: `test: add mixed comparison chain test (GAP-40)`

38. **Walrus type annotation error (GAP-41)** — `x: int := val` should error, add test
    - Commit: `test: add walrus type annotation error test (GAP-41)`

39. **Enum value mangling (GAP-44)** — Verify CAPS_SNAKE_CASE → PascalCase, add test
    - Commit: `test: add enum value mangling test (GAP-44)`

40. **Partial type argument spec error (GAP-46)** — Must specify all or none, add test
    - Commit: `test: add partial type argument error test (GAP-46)`

41. **Covariant return types in properties (GAP-47)** — Verify + test
    - Commit: `test: add covariant property return type test (GAP-47)`

42. **maybe on already-optional documentation (AMB-7)** — Document no double-wrapping
    - Commit: `docs: document maybe on already-optional stays T? (AMB-7)`

43. **Bare raise outside except (Extension 9)** — Add test if not exists, update spec
    - Commit: `test: add bare raise outside except error test`

44. **Keyword-only arguments in constructors (Extension 8)** — Add test, update spec
    - Commit: `test: add keyword-only constructor argument test`

---

### Phase 7: Spec Updates ✅ COMPLETED

**Goal:** Fix documentation divergences, remove stale banners, enrich spec with undocumented features.

#### Tasks

45. **Fix variadic args spec contradiction (DIV-2)** — `docs/language_specification/function_variadic_arguments.md`
    - Update lines 50-65 to say `*args` can precede keyword-only parameters
    - Cross-reference `flexible_arguments.md`
    - Commit: `docs: fix variadic args position spec contradiction (DIV-2)`

46. **Clarify lambda default params (DIV-5)** — `docs/language_specification/function_types.md`
    - Reword to clarify restriction applies to type annotation syntax `(int, int=0) -> int`, not lambda definitions
    - Add cross-reference to lambda docs
    - Commit: `docs: clarify function type annotation restriction (DIV-5)`

47. **Document integer promotion chain (DIV-6)** — `docs/language_specification/integer_literals.md`
    - Add: literals default to int (32-bit), auto-promote to long (64-bit) if > Int32.MaxValue, error if > Int64.MaxValue
    - Commit: `docs: document int → long → error promotion chain (DIV-6)`

48. **Update exception hierarchy (DIV-8)** — `docs/language_specification/exception_handling.md`
    - Add missing aliases: UnicodeEncodeError, JSONDecodeError, StatisticsError, StopIteration (if not in table)
    - Commit: `docs: update exception hierarchy with full alias table (DIV-8)`

49. **Remove Optional/Result "planned" language (OUT-1)** — tagged_unions_optional.md, tagged_unions_result.md
    - Remove "planned for a later phase" from both files
    - Replace with current implementation status
    - Commit: `docs: remove stale 'planned' language from Optional/Result specs (OUT-1)`

50. **Remove stale operator overloading banner (OUT-2)** — `docs/language_specification/operator_overloading.md`
    - Remove the "Known gaps" banner referencing #276/#277 (both issues are closed and about unrelated dogfood topics; __getitem__/__setitem__ are implemented)
    - Commit: `docs: remove stale __getitem__/__setitem__ gap banner (OUT-2)`

51. **Document comparison chain short-circuit (AMB-3)** — `docs/language_specification/comparison_chaining.md`
    - Explicitly state short-circuit behavior: if first comparison is false, second is not evaluated
    - Commit: `docs: document comparison chain short-circuit behavior (AMB-3)`

52. **Document try expression uncaught types (AMB-6)** — `docs/language_specification/`
    - Document: `try[ValueError] foo()` when TypeError thrown → uncaught exception propagates at runtime
    - Commit: `docs: document try expression uncaught exception propagation (AMB-6)`

53. **Document .NET snake_case access in interop spec (AMB-9)** — `docs/language_specification/dotnet_interop.md`
    - Document that `system.Console.write_line()` maps to `System.Console.WriteLine()`
    - Commit: `docs: document .NET snake_case method access in interop spec (AMB-9)`

54. **Add next() to builtin_functions.md (Extension 1)** — `docs/language_specification/builtin_functions.md`
    - Document `next(iterator)` and `next(iterator, default)` signatures
    - Commit: `docs: add next() to builtin functions spec`

55. **Document min/max default parameter (Extension 2)** — `docs/language_specification/builtin_functions.md`
    - Add `default` parameter to `min()` and `max()` signatures
    - Commit: `docs: add min/max default parameter to spec`

56. **Fix enumerate() signature (Extension 3)** — `docs/language_specification/builtin_functions.md`
    - Update from keyword-only `start` to positional `start` argument
    - Commit: `docs: fix enumerate start parameter to positional`

57. **Document binary operator sections (Extension 4)** — pipe_operator.md or operator docs
    - Document `_ + _` syntax for two-placeholder operator sections
    - Commit: `docs: document binary operator sections (_ + _)`

58. **Document yield restrictions in try/finally (Extension 5)** — spec
    - Add note about C# iterator limitation preventing yield in try/except/finally
    - Commit: `docs: document yield restriction in try/finally blocks`

59. **Document .NET overloaded import behavior (Extension 6)** — dotnet_interop.md
    - Document behavior shown in `from_import_overloads.spy`
    - Commit: `docs: document .NET overloaded import behavior`

---

### Phase 8: GitHub Issues ✅ COMPLETED

**Goal:** Create GitHub issues for items deferred to later discussion.

#### Tasks

60. **Create issue: Lambda type annotations syntax** — from DIV-5
    - Title: "RFC: Lambda parameter type annotations syntax (lambda x: int: x + 1)"
    - Body: Document the double-colon problem, reference property syntax decision, ask for RFC
    - Commit: n/a (GitHub issue only)

61. **Create issue: Circular import for type annotations** — from GAP-8
    - Title: "Support circular imports for type-annotation-only references"
    - Body: Spec says allowed for annotations but implementation rejects all circular imports
    - Commit: n/a

62. **Create issue: Struct parameter modifiers (in/mut/out)** — from GAP-16
    - Title: "Implement struct parameter modifiers (in[], mut[], out[])"
    - Body: Documented in spec, not implemented
    - Commit: n/a

63. **Create issue: Context manager __exit__ with exception parameters** — from GAP-25
    - Title: "RFC: __exit__ signature — support both no-arg and 3-arg forms"
    - Body: User should be able to define either `__exit__(self)` or `__exit__(self, exc_type, exc_val, exc_tb)`
    - Commit: n/a

64. **Create issue: `is` operator with value types** — from AMB-1
    - Title: "Clarify `is` operator semantics with value types (boxing)"
    - Body: Spec says ReferenceEquals but doesn't address boxing; consider compile-time warning
    - Commit: n/a

65. **Create issue: Function type contravariance** — from AMB-4
    - Title: "Clarify function type variance enforcement status"
    - Body: Spec has commented-out examples; clarify if enforced or aspirational
    - Commit: n/a

66. **Create issue: Null-conditional double-wrapping** — from AMB-5
    - Title: "Clarify ?. on method returning nullable — flatten or double-wrap?"
    - Body: Document that return type is T? regardless of nesting depth
    - Commit: n/a

67. **Create issue: Exception type combining syntax in try expressions** — from AMB-6
    - Title: "RFC: Syntax for combining exception types in try expressions"
    - Body: `try[ValueError | TypeError] foo()` or similar — nice syntax for multiple exception types
    - Commit: n/a

68. **Create issue: Membership operator dispatch priority** — from AMB-8
    - Title: "Clarify __contains__ vs .Contains() priority for `in` operator"
    - Body: Consider requiring only one; error if both defined
    - Commit: n/a

69. **Create issue: Async comprehension and context manager gaps** — from GAP-26
    - Title: "Implement async comprehensions and async context manager edge cases"
    - Body: Document which async features are missing from implementation
    - Commit: n/a

---

## Testing Strategy

**New test fixtures by category:**

- **Positive (.spy + .expected):** ~30 new fixtures covering decimal, .5 floats, UTF-16 strings, walrus in comprehensions, class/struct constraints, array[T], named tuple matching, scoped type aliases, constraint reordering, match expressions in context, nested comprehensions, escape sequences, etc.
- **Negative (.spy + .error):** ~20 new error fixtures covering global/nonlocal rejection, @classmethod rejection, isinstance with generics, type(None) error, comprehension duplicate vars, pipe to constructor/operator, tuple mismatch, del rejection, reserved keywords, walrus type annotation, etc.
- **C# snapshots (.expected.cs):** Add for array[T] and decimal type to catch codegen regressions

**Edge cases:**
- `.5` vs `.` (member access) disambiguation
- `divmod` vs `div_mod` in existing code (search for any remaining uses)
- Scientific notation with both positive and negative exponents
- Unicode surrogate pair boundary cases
- array[T] with nullable element types

**Regression prevention:**
- Each implementation fix has at least one positive and one negative test
- Spec changes are paired with test verification

## Issues to Close

- No existing GitHub issues are directly closed by this plan (the referenced #276/#277 are already closed and about unrelated topics)

## Concurrency Safety with Syntax Consolidation Plan

| This Plan Phase | Syntax Consolidation Phase | Conflict? | Resolution |
|-----------------|--------------------------|-----------|------------|
| Phase 1 (lexer fixes) | Phase 2 (AccessValidator) | No | Different files |
| Phase 2 (decimal) | Phase 3 (PropertyValidator) | No | Different files |
| Phase 3 (tests) | Phase 4 (@dataclass) | No | Different files (test fixtures only) |
| Phase 5 (tests) | Phase 4 (@dataclass) | Possible | Both may add DecoratorValidator changes; coordinate diagnostic codes |
| Phase 7 (spec) | Phase 1 (docs) | Possible | Both edit spec files; ensure no conflicting edits |
| Phase 7 (spec) | Phase 5 (spec alignment) | **Yes** | This plan supersedes — Phase 5 decisions overridden by Anton's audit |

**Mitigation:** If running concurrently, Phase 7 of this plan should run AFTER syntax consolidation Phase 5 completes, or syntax consolidation Phase 5 should be skipped entirely in favor of this plan's Phase 7.

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-19
**Plan file:** `~/.claude/plans/plan-ba1574.md`

### Corrections Made

1. **Task 2 — File path**: `src/Sharpy.Core/Partial.Str/Str.cs` → `src/Sharpy.Core/Str.cs`. The `Partial.Str/` directory was removed 2026-02-14; `Str.cs` is now at root level. Also added line references (~125, ~158).

2. **Task 3 — DivMod verification**: Changed from "Verify C# method is named Divmod() (not DivMod())" to stating the verified fact: "C# method is currently DivMod()" with confirmation that all 4 overloads need renaming.

3. **Task 4 — global/nonlocal keywords**: Changed from conditional "If not already in keyword table" to definitive: "are NOT in the keyword table (verified)". Added that `TokenType.Global`/`TokenType.Nonlocal` enum values also need to be added.

4. **Task 5 — @classmethod/@staticmethod**: Rewritten to reflect that both rejections already exist in `DecoratorValidator.cs` but with messages that don't suggest `@static`. Changed from "Add" to "Update".

5. **Task 6 — Decimal pipeline**: Added verified details — `SemanticType.Decimal` singleton at line 72, `PrimitiveCatalog` registration at line 89 with `NumericKind.Decimal`.

6. **Phase 4 — array[T]**: Major correction. Parser already supports `T[]` suffix syntax (`Parser.Types.cs` lines 96-117) creating a GenericType with `Name = "array"`. `TypeMapper.MakeArrayType()` already exists (line 763). Rewrote tasks 14-16 and 18 to reflect existing infrastructure and remaining work.

### Warnings

1. **Task 1 (leading-decimal .5)**: The plan correctly identifies the decimal point check at lines 357-358 in `Lexer.Literals.cs`. However, adding `.` as a float literal entry point requires careful disambiguation from member access (`.`) — the plan acknowledges this but implementation may be trickier than described since the lexer entry point dispatcher needs modification.

2. **Task 5 (@staticmethod suggestion)**: The existing error message for `@staticmethod` ("Methods without a 'self' parameter are automatically static") is technically about methods being auto-static. Suggesting `@static` here may be misleading since `@static` is for class/struct fields, not methods. Consider keeping the current `@staticmethod` message and only updating `@classmethod` to mention both auto-static behavior and `@static` for fields.

3. **Task 25 (list.get())**: The plan says `get(index)` returns `Optional<T>` — verify the correct generic type name in Sharpy (is it `Optional[T]` or the struct-based variant?).

4. **Phase 4 syntax**: The parser uses `T[]` syntax, not `array[T]`. The plan should decide which syntax(es) to support and document the decision clearly. Test fixtures in Task 18 were updated to use `T[]`.

5. **Concurrency table**: Phase 5 mentions "Both may add DecoratorValidator changes" — since Task 5 only updates error message strings (not adding new decorator handling), the conflict risk is minimal.

### Missing Steps Added

1. **Task 4**: Needs to add `TokenType.Global` and `TokenType.Nonlocal` to the `TokenType` enum (not just the keyword dictionary).

2. **Phase 4**: Should verify whether the `BuiltinRegistry` (at `Semantic/Registry/BuiltinRegistry.cs`) needs to register array-related builtins for proper type resolution.

3. **General**: No explicit mention of running `/format` before commits — all commits should ensure `dotnet format whitespace` passes.

### Unchecked Claims

1. **GAP-1, GAP-3, GAP-9, etc. "already implemented"** — Taken at face value from the audit research; not independently re-verified here.

2. **Issues #276/#277 "about unrelated topics"** — Not verified against actual GitHub issues (would require API call). The banner at `operator_overloading.md` line 3 does reference them for `__div__/__mod__` and `__getitem__/__setitem__`.

3. **Python behavior for walrus operator in comprehensions (Task 8)** — The plan notes Sharpy spec says comprehension-local; Python behavior (where walrus leaks in Python 3.8+) was not verified here.

4. **DIV-5, DIV-6, DIV-8, OUT-1, OUT-2, AMB-3, AMB-6, AMB-9 spec changes** — All referenced spec files exist (verified), but the specific lines/content to be changed were not fully audited.

5. **Remaining GAP items (29-47)** — Structural claims in Phase 6 (low-priority test coverage) were not individually verified as they are primarily "verify then add test" tasks.
