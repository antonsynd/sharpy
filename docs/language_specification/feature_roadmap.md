# Feature Roadmap

High-level implementation plan for upcoming features. All features in this document are compatible with netstandard2.1 / C# 9.0 unless noted otherwise. Features requiring newer .NET versions are tracked in [deferred_features.md](deferred_features.md).

For rejected features, see [rejected_proposals.md](rejected_proposals.md).

---

## Tier 1 — Low Complexity, High Value

### 1.1 Try Expression Multi-Exception Types ✅ Implemented

**Issue:** [#424](https://github.com/antonsynd/sharpy/issues/424)
**Spec:** [try_expressions.md](try_expressions.md)

Allow try expressions to catch multiple exception types using union syntax, consistent with `except` clause syntax in try statements.

**Syntax:**
```python
x = try[ValueError | TypeError] int(input_str)
# Result type: Result[int, Exception]
# Catches ValueError OR TypeError; others propagate uncaught
```

**Design decisions:**
- Union syntax `try[A | B]` over tuple syntax `try[A, B]` — consistent with existing `except (A, B):` semantics and avoids ambiguity with generic type arguments.
- The Result error type is the nearest common base class of the listed exceptions (often `Exception`). If all listed types share a more specific base (e.g., both are `ValueError` subclasses), use that.
- Parser change: extend `try[...]` to accept `|`-separated type list.

**Implementation outline:**
1. **Parser** — Extend `ParseTryExpression` to accept `TypeExpression | TypeExpression | ...` inside the brackets. New AST: change the single `ExceptionType` field to `ExceptionTypes: IReadOnlyList<TypeExpression>`.
2. **Semantic** — TypeChecker validates each exception type is a subclass of `Exception`. Compute the common base for the Result error type.
3. **CodeGen** — Emit chained catch clauses, one per exception type, all funneling into the same `Err` construction.
4. **Tests** — Single-type (regression), two-type, three-type, unrelated types, common-base types, invalid non-exception type.

---

### 1.2 Keyword Argument Partial Application (`_`) ✅ Implemented

**Spec:** [partial_application.md](partial_application.md)

Extend the existing `_` placeholder partial application to support keyword arguments.

**Syntax:**
```python
def fetch_url(url: str, timeout: int, retry: bool) -> str:
    ...

# Today: positional only
quick_fetch = fetch_url(_, 5, False)

# Proposed: keyword arguments
robust_fetch = fetch_url(_, timeout=30, retry=_)
# Lowers to: (url, retry) => fetch_url(url, timeout=30, retry=retry)

robust_fetch("https://api.example.com", True)
```

**Design decisions:**
- Positional `_` params keep the existing `__placeholder_N` naming in the generated lambda.
- Keyword `_` params use the keyword name itself as the lambda parameter name (e.g., `retry=_` produces a parameter named `retry`). This is more natural and produces readable generated code.
- Mixed positional + keyword placeholders are supported. Parameter order in the lambda: positional placeholders first (left-to-right), then keyword placeholders (in order of appearance).
- Operator sections (`(_ * 2)`) are unaffected — they don't involve keyword arguments.

**Implementation outline:**
1. **Parser** (~30 lines in `LowerPartialApplicationCall`) — Remove the SPY0130 error that rejects `_` in keyword args. Extend the `hasPlaceholder` scan to include `KeywordArguments`. In the lowering loop, when `kwarg.Value is Identifier { Name: "_" }`, create a parameter named after the keyword and replace the value with a reference to it.
2. **Semantic** — No changes expected. The output is a standard `LambdaExpression` with a `FunctionCall` body; TypeChecker already handles keyword args in calls.
3. **CodeGen** — No changes. Lambda emission and keyword arg emission are already independent.
4. **Spec** — Update `partial_application.md` to remove the keyword limitation and add examples.
5. **Tests** — `f(x=_)`, `f(_, y=_)`, `f(x=_, y=_)`, type inference through keyword placeholders, keyword placeholder with default values on other params.

---

### 1.3 `@final` Field Decorator ✅ Implemented

**Spec reference:** [statements.md](statements.md) line 218

Decorator for per-instance immutable fields. Maps directly to C# `readonly` field modifier.

**Syntax:**
```python
class Config:
    @final
    name: str

    def __init__(self, name: str):
        self.name = name  # OK: assignment in constructor

    def rename(self, new_name: str):
        self.name = new_name  # ERROR: cannot assign to @final field outside constructor
```

**Design decisions:**
- `@final` on a field means it can only be assigned in `__init__` (constructor). This mirrors C# `readonly`.
- `@final` on a class means no inheritance (already supported via `@final class`). `@final` on a method means no override (already supported). This extends the decorator to fields.
- Applies to instance fields only. Class-level constants use `const`.
- For structs, `@final` fields can be assigned in any constructor overload.

**Implementation outline:**
1. **Parser** — Already parses `@final` as a decorator. Extend field declaration to accept it.
2. **Semantic** — In TypeChecker, when encountering an assignment to a `@final` field, verify the assignment is inside a constructor of the declaring class. Emit `SPY0xxx` if violated.
3. **Validation** — Add check in `StructRulesValidator` or a new `FinalFieldValidator`.
4. **CodeGen** — Emit `readonly` modifier on the C# field declaration.
5. **Tests** — Valid constructor assignment, invalid method assignment, struct constructors, inheritance (derived class cannot reassign base `@final` field).

---

### 1.4 Tuple Spreading ✅ Implemented

**Spec:** [spread_operator.md](spread_operator.md)

Spread into tuple literals. Builds on existing list/set/dict spreading infrastructure.

**Syntax:**
```python
first = (1, 2)
second = (3, 4)
combined = (*first, *second)  # (1, 2, 3, 4)

coords = (*point_2d, z_value)  # Convert 2D to 3D
```

**Design decisions:**
- Resulting tuple type is the concatenation of all element types. `tuple[int, int]` + `tuple[str, str]` = `tuple[int, int, str, str]`.
- Only tuples with known arity can be spread (compile-time type must be a concrete `TupleType`). Spreading a generic `tuple` or runtime-typed value is an error.
- Mixed spread + literal elements: `(*pair, 5)` where `pair: tuple[int, int]` yields `tuple[int, int, int]`.

**Implementation outline:**
1. **Parser** — Already parses `*expr` in tuple positions. Verify the AST `SpreadExpression` is accepted in tuple literal context.
2. **Semantic** — TypeChecker resolves each spread element's `TupleType` and concatenates element types. Error if spread target is not a tuple type with known arity.
3. **CodeGen** — Lower to `ValueTuple.Create(first.Item1, first.Item2, second.Item1, second.Item2)` or nested `ValueTuple` constructors for >7 elements.
4. **Tests** — Two tuples, mixed spread + literal, type inference, type mismatch errors, large tuples (>7 elements), nested spreading.

---

## Tier 2 — Medium Complexity

### 2.1 `functools.partial` (Compatibility Shim) ✅ Implemented

**Issue:** [#396](https://github.com/antonsynd/sharpy/issues/396)

> **Note:** With keyword `_` partial application (1.2), Sharpy's native syntax covers nearly all `functools.partial` use cases more ergonomically. This item is a compatibility shim for Python programmers who reach for `partial` out of habit.

**Syntax (Sharpy user-facing):**
```python
from functools import partial

def power(base: int, exp: int) -> int:
    return base ** exp

square = partial(power, exp=2)
print(square(5))  # 25

# Idiomatic Sharpy equivalent (preferred):
square = power(_, exp=2)
```

**Design decisions:**
- `partial` is a thin wrapper that the compiler lowers to the same lambda as `_` placeholder syntax. No runtime `Partial<T>` wrapper class needed.
- A linter hint (or compiler info diagnostic) should suggest the `_` placeholder form as the idiomatic alternative.
- `.func`, `.args`, `.keywords` introspection attributes are **not** supported — the lowered lambda has no such metadata. If introspection is needed, users should use the function directly.
- Depends on keyword `_` support (1.2) being implemented first.

**Implementation outline:**
1. **Semantic** — Recognize `functools.partial(func, ...)` as a special form in TypeChecker. Desugar to the equivalent `_` placeholder call.
2. **CodeGen** — No special handling needed — the desugared form is a standard lambda.
3. **Diagnostics** — Emit an info-level hint suggesting the `_` placeholder equivalent.
4. **Tests** — Positional fixing, named fixing, equivalence with `_` syntax, chained partials.

---

### 2.2 Context Manager `__exit__` with Exception Args ✅ Implemented

**Spec:** [context_managers.md](context_managers.md) RFC section

Support the 3-arg `__exit__` signature to enable exception-aware context managers.

**Syntax:**
```python
class SuppressErrors:
    def __enter__(self) -> SuppressErrors:
        return self

    def __exit__(self, exc_type: type?, exc_val: Exception?, exc_tb: object?) -> bool:
        if exc_val is not None:
            print(f"Suppressed: {exc_val}")
            return True   # suppress the exception
        return False       # propagate
```

**Design decisions:**
- **Option C (codegen wrapping)** is the chosen approach — the emitter generates try/catch/finally that captures exception info and passes it to `Exit()`. No new Sharpy.Core interface needed.
- The `exc_tb` parameter is always `None` (no CLR traceback equivalent). Type it as `object?` and document this clearly.
- `__exit__` returning `True` suppresses the exception, matching Python semantics exactly.
- Both the no-arg form (current, maps to `IDisposable.Dispose()`) and the 3-arg form are supported. The emitter detects which signature is present.
- `__aexit__` supports the same 3-arg variant with identical semantics.

**Implementation outline:**
1. **Semantic** — Update `ProtocolRegistry` to accept `ExpectedParamCount: 1 or 4` for `__exit__` and `__aexit__`. TypeChecker validates parameter types.
2. **CodeGen** — When 3-arg `__exit__` is detected, emit the try/catch/finally wrapper pattern (see RFC in context_managers.md). When 1-arg, emit current `IDisposable` pattern unchanged.
3. **Tests** — No-arg (regression), 3-arg with no exception, 3-arg with suppressed exception, 3-arg with propagated exception, async variant, interaction with `IDisposable` types.

---

### 2.3 `functools.lru_cache` / `cache` ✅ Implemented

**Issue:** [#396](https://github.com/antonsynd/sharpy/issues/396)

Memoization decorators. Depends on decorator argument support in the compiler.

**Syntax:**
```python
from functools import lru_cache, cache

@lru_cache(maxsize=128)
def fibonacci(n: int) -> int:
    if n < 2:
        return n
    return fibonacci(n - 1) + fibonacci(n - 2)

@cache  # Equivalent to @lru_cache(maxsize=None)
def expensive(x: int) -> int:
    return x * x
```

**Design decisions:**
- **Compiler support for decorator arguments** is the prerequisite. The compiler must parse `@decorator(args)` and pass args to the decorator's codegen logic.
- `@lru_cache(maxsize=N)` generates a wrapper that checks a `ConcurrentDictionary<TKey, TResult>` before calling the original function. When `maxsize` is set, eviction uses insertion order (approximating LRU with bounded size).
- `@cache` is syntactic sugar for `@lru_cache(maxsize=None)` (unbounded cache).
- Cache is per-function-instance (module-level functions get a static cache; instance methods get a per-instance cache).
- `.cache_info()` and `.cache_clear()` methods are available on the wrapper, matching Python's API.

**Implementation outline:**
1. **Parser** — Extend decorator parsing to support `@name(args)` in addition to bare `@name`. New AST field: `DecoratorExpression.Arguments`.
2. **Semantic** — Validate decorator arguments. For `lru_cache`, validate `maxsize` is `int?`.
3. **CodeGen** — Generate wrapper method with cache dictionary. For `@lru_cache(maxsize=N)`, emit bounded `OrderedDictionary`-style cache. For `@cache`/`@lru_cache(maxsize=None)`, emit `ConcurrentDictionary`.
4. **Sharpy.Core** — `LruCache<TKey, TResult>` helper class with `Get`, `CacheInfo()`, `CacheClear()`.
5. **Tests** — Fibonacci memoization, cache hits/misses, `cache_info()`, `cache_clear()`, maxsize eviction, thread safety, per-instance vs static.

---

## Tier 3 — High Complexity

### 3.1 Circular Imports (Type-Annotation-Only)

**Spec:** [module_system.md](module_system.md) RFC section

Allow circular imports when imported symbols are used only in type annotation positions.

**Design decisions:** See the full RFC in module_system.md. Key points:
- Two-pass import system: stub collection in Pass 1, full resolution in Pass 2.
- Base class cycles remain forbidden.
- Runtime usages of circular-imported symbols still emit SPY0302.
- No new syntax required — the compiler detects annotation-only usage statically.

**Implementation outline:**
1. **ModuleLoader** — When circular import detected, defer rejection. Create stub `ModuleInfo` with type declarations only.
2. **ImportResolver** — Track deferred-cycle imports. Register stubs in symbol table.
3. **New analysis pass** — Walk AST to classify each use of deferred-cycle symbols as type-annotation-only vs. runtime.
4. **Error emission** — Emit SPY0302 only when a cycle symbol has runtime usage. Error message identifies the specific symbol and usage site.
5. **IncrementalCache** — Bidirectional dependency edges for circular imports (both files recompile when either changes).
6. **Tests** — Mutual type annotations, base class cycle (still rejected), runtime usage in cycle (rejected), mixed annotation+runtime, multi-file cycles, incremental rebuild.

---

## Tier 4 — Standard Library

### 4.1 Grapheme Cluster Module ✅ Implemented

**Spec reference:** [string_type.md](string_type.md) line 110

Module for working with user-perceived characters (grapheme clusters), wrapping .NET's `StringInfo` and `TextElementEnumerator`.

**API sketch:**
```python
import grapheme

# Iterate grapheme clusters
for g in grapheme.graphemes("é"):  # é as e + combining accent
    print(g)  # Prints: é

# Count user-perceived characters
grapheme.length("👨‍👩‍👧‍👦")  # 1 (family emoji is one grapheme cluster)

# Slice by grapheme index
grapheme.slice("Héllo", 0, 3)  # "Hél"
```

**Implementation outline:**
1. **Sharpy.Core** — New `Grapheme/` module directory with `Grapheme.cs`.
2. Use `System.Globalization.StringInfo.GetTextElementEnumerator()` for grapheme iteration.
3. Implement `graphemes()`, `length()`, `slice()`, `at()`.
4. **Tests** — ASCII, combining characters, emoji, ZWJ sequences, edge cases (empty string, out of range).

---

## Proposals (Not Committed)

See [docs/design/](../design/) for standalone proposal documents:
- [Property Observers (`willset`/`didset`)](../design/property-observers-proposal.md) — [#416](https://github.com/antonsynd/sharpy/issues/416)

---

## Notes

### `str.format()` and `str.format_map()` (Issue #108)

These methods are **already fully implemented** in `src/Sharpy.Core/StringExtensions.Format.cs`. Issue [#108](https://github.com/antonsynd/sharpy/issues/108) is stale and should be closed.

### Decorator Arguments (Cross-Cutting Concern)

Decorator argument support (`@decorator(args)`) is a prerequisite for `lru_cache` (2.3) and potentially future decorators. It should be prioritized alongside or before `lru_cache`. The parser already handles bare decorators; extending to argument forms is the primary work.
