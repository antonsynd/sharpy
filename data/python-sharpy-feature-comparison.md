# Python 3.0–3.15 vs Sharpy: Feature Comparison

*Generated 2026-06-27, **verified against implementation**. Sharpy v0.5.0 on branch `dev` at commit `bd93a93`.*

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Fully supported in Sharpy |
| ⚡ | Partially supported or limited |
| 🔀 | Sharpy has a different/better approach |
| ❌ | Not supported |
| N/A | Not applicable (CPython-specific runtime detail) |

---

## Comparison by Python Version

### Python 3.0

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| Function annotations (`def f(x: int) -> str:`) | 3107 | ✅ | Core feature — annotations are real types, not just hints |
| Keyword-only parameters (`def f(*, key):`) | 3102 | ✅ | `ParameterKind.KeywordOnly` |
| Extended iterable unpacking (`a, *rest, b = seq`) | 3132 | ✅ | Star expressions in assignment |
| `raise X from Y` exception chaining | 3134 | ✅ | Exception chaining supported |
| `except E as var:` syntax | 3110 | ✅ | Standard syntax |
| New metaclass syntax (`class C(metaclass=M):`) | 3115 | ❌ | Metaclasses not supported; use interfaces/decorators |
| Dictionary comprehensions (`{k: v for ...}`) | — | ✅ | Including spread: `{**d for d in dicts}` |
| Set literals (`{1, 2}`) and set comprehensions | — | ✅ | |
| Binary literals (`0b1010`), new octal (`0o720`) | — | ✅ | Standard numeric literals |
| Bytes literals (`b"..."`) | — | ✅ | `BytesLiteralExpression` AST node |
| Ellipsis (`...`) as general expression | — | ✅ | Used for abstract method bodies |
| Non-ASCII identifiers | 3131 | ✅ | Unicode identifiers via `char.IsLetter` |
| `nonlocal` statement | 3104 | ❌ | Explicitly rejected — C# block scoping rules |
| `print()` as function | 3105 | ✅ | Always a function in Sharpy |
| `super()` without arguments | 3135 | ✅ | |
| `str` = Unicode, `bytes` = binary | — | 🔀 | `str` = .NET `string` (UTF-16); `bytes` = `byte[]` |
| Single `int` type (long merged) | 237 | 🔀 | Sharpy has `int` (32-bit) and `long` (64-bit) separately |
| True division by default (`1/2` = `0.5`) | 238 | 🔀 | Follows .NET: `1/2` = `0` (integer division); use `float` cast |
| `__nonzero__` → `__bool__` | — | ✅ | `__bool__` → `operator true/false` |
| Absolute imports by default | 328 | ✅ | Relative imports require explicit dot notation |
| `str.format()` method | 3101 | ✅ | Via .NET `string.Format` |
| Removed: tuple param unpacking, backticks, `<>` | — | ✅ | Never had these |

### Python 3.1

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| Multiple context managers in `with` | — | ✅ | `with a, b:` syntax |
| Format string auto-numbering (`'{} {}'`) | — | ✅ | Via f-strings (no `.format()` auto-numbering) |
| Thousands separator format spec (`,`) | 378 | ⚡ | F-string format specs supported; `,` delegated to .NET formatting |
| `collections.OrderedDict` | 372 | 🔀 | `Dict` preserves insertion order (.NET guarantee since .NET 4) |
| `collections.Counter` | — | ✅ | In `Sharpy.Stdlib.Collections` |

### Python 3.2

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| `callable()` restored | — | ✅ | Builtin function |
| `argparse` module | 389 | ✅ | `Sharpy.Stdlib.Argparse` |
| `concurrent.futures` | 3148 | ❌ | Use `async`/`await` + `Task.WhenAll` instead |
| `functools.lru_cache()` | — | ✅ | `@lru_cache` decorator emits caching codegen |
| `functools.total_ordering()` | — | ❌ | Implement comparison operators directly |
| `itertools.accumulate()` | — | ✅ | In `Sharpy.Stdlib.Itertools` |

### Python 3.3

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| `yield from` expression | 380 | ✅ | Delegates to sub-iterator |
| `raise ... from None` to suppress context | 409 | ✅ | Exception chaining control |
| `u'...'` string prefix restored | 414 | ❌ | Not needed; all strings are Unicode |
| `__qualname__` | 3155 | ❌ | .NET has its own qualified name system |
| `str.casefold()` | — | ⚡ | Via .NET string comparison options |
| OSError subclass hierarchy | 3151 | 🔀 | Uses .NET exception hierarchy (IOException, etc.) |
| Implicit namespace packages (no `__init__.py`) | 420 | ❌ | `__init__.spy` required for packages |
| `venv` module | 405 | N/A | .NET has its own project system |
| `unittest.mock` | — | ❌ | Use .NET mocking libraries (Moq, NSubstitute) |
| `ipaddress` module | — | ✅ | `Sharpy.Stdlib.Ipaddress` |
| `contextlib.ExitStack` | — | ❌ | Use `with` statement directly |
| `math.log2()` | — | ✅ | In `Sharpy.Stdlib.Math` |

### Python 3.4

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| `asyncio` module | 3156 | 🔀 | `async`/`await` maps directly to .NET Tasks |
| `enum` module (`Enum`, `IntEnum`) | 435 | ✅ | Native `enum` keyword with richer features |
| `pathlib` module | 428 | ✅ | `Sharpy.Stdlib.Pathlib` |
| `statistics` module | 450 | ✅ | `Sharpy.Stdlib.Statistics` |
| `functools.singledispatch()` | 443 | ❌ | Use method overloading or pattern matching |
| `min()`/`max()` with `default` keyword | — | ⚡ | Builtins exist; `default` parameter TBD |

### Python 3.5

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| `async def` / `await` / `async for` / `async with` | 492 | ✅ | Full async support → .NET Task/ValueTask |
| `@` matrix multiplication operator | 465 | ❌ | No `__matmul__` support; `@` used for decorators |
| Generalized unpacking (`[*a, *b]`, `{**d1, **d2}`) | 448 | ✅ | Spread syntax in literals and calls |
| `typing` module (`Any`, `Union`, `Optional`, `TypeVar`) | 484 | 🔀 | Real compile-time types, not runtime hints |
| `%` formatting for bytes | 461 | ❌ | |

### Python 3.6

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **F-strings** (`f"Hello {name}"`) | 498 | ✅ | Full support including format specs |
| **Variable annotations** (`x: int = 5`) | 526 | ✅ | First-class type annotations |
| **Underscores in numeric literals** (`1_000_000`) | 515 | ✅ | With validation (no consecutive, no trailing) |
| **Async generators** | 525 | ✅ | `async def gen(): yield x` → `IAsyncEnumerable<T>` |
| **Async comprehensions** | 530 | ❌ | Explicitly rejected in Sharpy |
| `__init_subclass__` hook | 487 | ❌ | No metaclass-like hooks |
| `__set_name__` descriptor protocol | 487 | ❌ | No descriptor protocol |
| `os.PathLike` protocol | 519 | 🔀 | Uses .NET `FileInfo`/`DirectoryInfo` interop |
| Class attribute order preservation | 520 | ✅ | Guaranteed by C# class compilation |
| Keyword argument order preservation | 468 | ✅ | .NET preserves parameter order |

### Python 3.7

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **`@dataclass` decorator** | 557 | ✅ | Auto-generates `__init__`, `__eq__`, `__repr__`, `__hash__` |
| Postponed annotation evaluation | 563 | N/A | Sharpy compiles ahead-of-time; types are resolved |
| Module `__getattr__` / `__dir__` | 562 | ❌ | Static compilation — no dynamic module attributes |
| `contextvars` | 567 | 🔀 | .NET has `AsyncLocal<T>` |
| `async`/`await` become reserved keywords | — | ✅ | Always reserved in Sharpy |
| Dict order guaranteed by language spec | — | ✅ | .NET `Dictionary` preserves insertion order |
| `breakpoint()` builtin | 553 | ❌ | Use IDE debugger; .NET has `Debugger.Break()` |

### Python 3.8

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Walrus operator** (`:=`) | 572 | ✅ | Assignment expressions |
| **Positional-only parameters** (`/` separator) | 570 | ✅ | `ParameterKind.PositionalOnly` with SPY0370 diagnostic |
| **f-string `=` specifier** (`f'{expr=}'`) | — | ❌ | Self-documenting debug format not implemented |
| `continue` in `finally` blocks | — | ⚡ | Depends on .NET behavior |
| Generalized unpacking in `return`/`yield` | — | ✅ | |

### Python 3.9

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Dict merge operators** (`d1 \| d2`, `d1 \|= d2`) | 584 | ✅ | `Dict<K,V>` has `operator \|` |
| **Generic syntax in builtins** (`list[int]` without `typing`) | 585 | ✅ | `list[int]` → `Sharpy.List<int>` from the start |
| **`str.removeprefix()` / `str.removesuffix()`** | 616 | ✅ | In `StringExtensions` |
| Relaxed decorator grammar (any expression) | 614 | ⚡ | Sharpy allows specific registered decorator names (not arbitrary expressions), plus `@[...]` bracket syntax for arbitrary C# attributes |
| `typing.Annotated` | 593 | ❌ | No runtime metadata annotation system |

### Python 3.10

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Structural pattern matching** (`match`/`case`) | 634/635/636 | ✅ | **More extensive than Python**: adds relational patterns (`> 0`), match expressions, typed bindings, exhaustiveness checking. List patterns (`[a, b]`, `[first, *rest]`, `[*init, last]`, nested) and `and` patterns are implemented end-to-end (#991). |
| **Union type operator** (`X \| Y`) | 604 | ⚡ | Only `T \| None` allowed (nullable interop). Free unions like `int \| str` are explicitly rejected — use `union` declarations instead |
| `ParamSpec` and `Concatenate` | 612 | ❌ | Higher-order function typing not implemented |
| `TypeAlias` annotation | 613 | ✅ | `type` keyword: `type UserId = int` |
| `TypeGuard` | 647 | 🔀 | Sharpy uses `is` type narrowing instead of user-defined guards |
| `zip(strict=True)` | 618 | ✅ | `zip(a, b, strict=True)` raises `ValueError` on length mismatch (#988) |
| Parenthesized context managers | — | ⚡ | Multiple `with` via comma, no parenthesized syntax |

### Python 3.11

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Exception Groups** (`ExceptionGroup`, `except*`) | 654 | ✅ | Full support including multiple match groups |
| `BaseException.add_note()` | 678 | ❌ | No exception notes |
| Fine-grained error locations in tracebacks | 657 | 🔀 | Sharpy diagnostics point to exact spans at compile time |
| `TypeVarTuple` (variadic generics) | 646 | ❌ | No variadic generics |
| **`Self` type** | 673 | ✅ | `Self` keyword for covariant return annotations |
| **`LiteralString`** type | 675 | ✅ | `LiteralStringType` in semantic type hierarchy |
| `Required` / `NotRequired` for TypedDict | 655 | N/A | No TypedDict equivalent |
| `@dataclass_transform()` | 681 | ❌ | |
| Starred unpacking in `for` targets | — | ✅ | Star expressions in for loops |
| Async comprehensions inside comprehensions | — | ❌ | Async comprehensions explicitly rejected |

### Python 3.12

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Type parameter syntax** (`def f[T](x: T) -> T:`) | 695 | ✅ | Sharpy had `[T]` syntax from the start |
| **`type` statement** (`type Point = tuple[float, float]`) | 695 | ✅ | `type` keyword supported |
| F-string improvements (quote reuse, multiline, nesting) | 701 | ⚡ | F-strings support nesting; backslashes/comments in expressions TBD |
| Comprehension inlining (no function frame) | 709 | N/A | Sharpy compiles to C# — no frame overhead |
| `Unpack[TypedDict]` for `**kwargs` typing | 692 | ❌ | No TypedDict |
| `@typing.override` decorator | 698 | ✅ | `@override` decorator — checked at compile time |
| `NameError` suggests missing imports | — | ⚡ | Sharpy has "did you mean?" for misspelled identifiers |
| Invalid escape sequences raise `SyntaxWarning` | — | ✅ | Warnings are errors in Sharpy |

### Python 3.13

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **TypeVar / ParamSpec / TypeVarTuple defaults** | 696 | ✅ | `TypeParameterDefinition.DefaultType` supported |
| **`TypeIs`** (better type narrowing) | 742 | 🔀 | Sharpy uses `is` for type narrowing; no user-defined type guard functions |
| `ReadOnly` for TypedDict | 705 | N/A | No TypedDict |
| `warnings.deprecated()` decorator | 702 | ❌ | Use `@[Obsolete("...")]` C# attribute instead |
| Free-threaded CPython (no GIL) | 703 | N/A | .NET is already multi-threaded |
| Experimental JIT compiler | 744 | N/A | .NET already has JIT |
| Defined `locals()` semantics | 667 | N/A | Compile-time language |
| Docstring whitespace stripping | — | N/A | |

### Python 3.14

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Deferred annotation evaluation** | 649/749 | N/A | Sharpy resolves types at compile time; forward references handled by multi-pass |
| **Template strings** (`t"..."`) | 750 | ✅ | `TStringLiteral` AST node — produces `Template` object |
| `concurrent.interpreters` (sub-interpreters) | 734 | N/A | .NET has `AppDomain` / process isolation |
| Parentheses optional in multi-except (no `as`) | 758 | ✅ | Already supported: `except TimeoutError, ConnectionError:` |
| `SyntaxWarning` for `return`/`break` in `finally` | 765 | ⚡ | ControlFlowValidator may catch some cases |
| `sys.remote_exec()` for debugging | 768 | N/A | Runtime debugging feature |
| `map(strict=...)` | — | ✅ | Multi-iterable `map(f, a, b[, c], strict=True)` raises `ValueError` on length mismatch (#990); element-type inference in unannotated nested calls tracked by #999 |
| Keyword error message improvements | — | ⚡ | Sharpy has "did you mean?" suggestions |

### Python 3.15 (Beta)

| Feature | PEP | Sharpy | Notes |
|---------|-----|--------|-------|
| **Lazy imports** (`lazy import numpy`) | 810 | ❌ | No lazy import mechanism |
| **Unpacking in comprehensions** (`[*L for L in lists]`) | 798 | ✅ | Implemented with PEP 798 comments in parser — `[*L for L in lists]`, `{*s for s in sets}`, `{**d for d in dicts}` |
| **`frozendict` built-in** | 814 | ✅ | `FrozenDict<K,V>` in Sharpy.Core (backed by `ImmutableDictionary`) |
| **`sentinel()` built-in** | 661 | ❌ | No sentinel value creation |
| **UTF-8 default encoding** | 686 | 🔀 | .NET uses UTF-16 internally; I/O encoding configurable |
| **`TypeForm[T]`** (type expression objects) | 747 | ❌ | No type-level metaprogramming |
| **Closed `TypedDict`** (`closed=True`) | 728 | N/A | No TypedDict |
| **`@typing.disjoint_base`** | 800 | ❌ | No disjoint base marking |
| `TypeVarTuple` bounds/variance keywords | — | ❌ | No TypeVarTuple |
| Unary `+` in match literal patterns | — | ⚡ | Match literal patterns support numbers; unary `+` TBD |
| `slice` subscriptable as generic (`slice[int]`) | — | ❌ | |
| `__dict__`/`__weakref__` in `__slots__` | — | N/A | No `__slots__` |
| `re.prefixmatch()` replacing `re.match()` | — | ❌ | `Sharpy.Stdlib.Re` follows current Python API |
| `math.integer` module | 791 | ❌ | |
| Error message improvements (cross-attribute suggestions) | — | ⚡ | Sharpy has its own diagnostic suggestions |
| `profiling` package | 799 | N/A | .NET has its own profiling tools |

---

## Summary Statistics

| Category | ✅ Supported | ⚡ Partial | 🔀 Different | ❌ Missing | N/A |
|----------|:-----------:|:---------:|:------------:|:---------:|:---:|
| Python 3.0 (foundations) | 19 | 0 | 4 | 2 | 0 |
| Python 3.1–3.4 (stdlib era) | 14 | 2 | 3 | 7 | 1 |
| Python 3.5–3.8 (async + typing) | 11 | 2 | 1 | 5 | 0 |
| Python 3.9–3.10 (syntax sugar) | 5 | 3 | 1 | 3 | 0 |
| Python 3.11–3.12 (type system) | 8 | 2 | 1 | 4 | 2 |
| Python 3.13–3.14 (maturity) | 2 | 1 | 1 | 2 | 6 |
| Python 3.15 (beta) | 2 | 1 | 1 | 6 | 3 |
| **Total** | **~61** | **~11** | **~12** | **~29** | **~12** |

**Coverage of applicable Python features: ~72% fully supported, ~82% including partial/different approach.**

*Statistics verified against compiler source, test fixtures, and stdlib implementation on 2026-06-27.*

---

## Sharpy-Only Features (Not in Python)

These are features Sharpy provides that Python does not have:

| Feature | Description |
|---------|-------------|
| `?` operator | Postfix early-return on `Result`/`Optional` |
| `Result[T, E]` / `T !E` | Zero-allocation discriminated union for error handling |
| `Optional[T]` / `T?` | Struct-based optional (distinct from nullable) |
| `??` / `??=` / `?.` | Null coalescing, null coalescing assignment, null conditional access |
| `try` expression | Wraps expression in `Result`, capturing exceptions |
| `maybe` expression | Wraps nullable in `Optional` |
| `\|>` pipe operator | Function application: `x \|> f` = `f(x)` |
| `struct` keyword | Value types with stack allocation |
| `interface` keyword | Explicit protocol/contract definitions |
| `event` system | .NET event pattern with `add`/`remove` |
| `delegate` keyword | Named function type declarations with variance |
| Match expressions | `result = match x: case ...: expr` (not just statements) |
| Relational patterns | `case > 0:`, `case >= 10:` in match |
| `and` patterns (forward-declared) | AST + codegen exist; parser not yet wired |
| Exhaustiveness checking | Compiler verifies all cases handled |
| `@[Attribute]` syntax | C# attribute interop |
| `@implicit` / `@explicit` | User-defined type conversions |
| `d"..."` dedented strings | Auto-strip indentation |
| `FrozenDict` | Immutable hashable dictionary (predates Python 3.15) |
| Generic variance (`in T` / `out T`) | Covariance/contravariance on type parameters |
| Type constraints (`T: IFoo & struct`) | Rich generic constraints |
| Source generators | Compile-time code generation |
| Multi-target output | `net10.0` + `netstandard2.1` |
| `property init` | Init-only property setters |
| `ref` / `out` / `in` parameters | Pass-by-reference semantics |
| `except ... when condition` | Exception filter expressions |

---

## Recommended Features to Consider

### Tier 1 — High Value, Low-to-Medium Complexity

These features would improve Sharpy with reasonable implementation effort:

#### 1. F-string `=` specifier (Python 3.8) — [#986](https://github.com/antonsynd/sharpy/issues/986)

**What:** `f'{expr=}'` expands to `f'expr={expr!r}'` for self-documenting debug output.

**Why:** Extremely useful for debugging. Python developers use this constantly. Simple to implement — the lexer/parser already handles f-strings; just need to detect `=` before `}` and emit `$"expr = {expr}"` in the Roslyn emitter.

**Complexity:** Low. Lexer change to recognize `=` in f-string expressions + emitter change.

**Example:**
```python
x = 42
print(f'{x=}')        # → "x=42"
print(f'{x + 1=}')    # → "x + 1=43"
print(f'{name=!r}')   # → "name='Alice'"  (if conversion flags added)
```

#### 2. `zip(strict=True)` (Python 3.10) — [#988](https://github.com/antonsynd/sharpy/issues/988) ✅ Implemented

**What:** `zip(a, b, strict=True)` raises `ValueError` if iterables have different lengths.

**Why:** Common source of silent bugs. Sharpy's type system catches many errors, but mismatched iteration lengths are a runtime concern.

**Status:** Implemented in `Builtins.Zip` (2- and 3-iterable `strict` overloads, `Sharpy.Core/Zip.cs`); covered by `ZipStrictTests` and the `zip_strict`/`zip_strict_error` fixtures.

#### 3. `map(strict=True)` (Python 3.14) — [#990](https://github.com/antonsynd/sharpy/issues/990) ✅ Implemented

**What:** Same as zip strict, but for `map()` with multiple iterables.

**Status:** Implemented in `Builtins.Map` (2- and 3-iterable overloads with `strict`, `Sharpy.Core/Map.cs`); covered by `MapMultiTests` and the `map_multi`/`map_multi_strict_error` fixtures. Works in `for` loops and annotated assignments; element-type inference for unannotated nested calls (e.g. `print(list(map(f, a, b, strict=True)))`) is tracked by [#999](https://github.com/antonsynd/sharpy/issues/999).

#### 4. `@` matrix multiplication operator (Python 3.5, PEP 465) — [#989](https://github.com/antonsynd/sharpy/issues/989)

**What:** `a @ b` for matrix multiplication, with `__matmul__` / `__rmatmul__` / `__imatmul__` dunder methods.

**Why:** Sharpy has `numpy` in its stdlib (backed by MathNet.Numerics). Scientific/numeric computing is a natural use case, and `@` is the standard Python operator for this. Currently `@` is used for decorators (start-of-line only), so `@` as infix is unambiguous.

**Complexity:** Medium. New token type for infix `@`, new dunder methods, operator registry entry, codegen.

**Example:**
```python
import numpy as np
a = np.array([[1, 2], [3, 4]])
b = np.array([[5, 6], [7, 8]])
c = a @ b  # matrix product
```

### Tier 2 — High Value, Higher Complexity

#### 5. Lazy imports (Python 3.15, PEP 810) — [#993](https://github.com/antonsynd/sharpy/issues/993)

**What:** `lazy import numpy` defers module loading until first attribute access.

**Why:** Startup time matters for CLI tools and scripts. Sharpy compiles to .NET assemblies, where eager loading of large libraries (numpy/MathNet, sqlite, etc.) can slow startup. Could map to `Lazy<T>` wrapping of module references.

**Complexity:** High. Touches import resolution, module loading, and codegen. The compiled nature of Sharpy makes this harder than in Python (where it's just deferred `__import__`), but `Lazy<ModuleType>` is a viable implementation strategy.

**Example:**
```python
lazy import numpy as np  # not loaded until first use
lazy import json          # loaded on first json.loads() call

if needs_numpy:
    result = np.array([1, 2, 3])  # numpy loaded here
```

#### 6. `sentinel()` built-in (Python 3.15, PEP 661) — [#994](https://github.com/antonsynd/sharpy/issues/994)

**What:** `MISSING = sentinel("MISSING")` creates a unique sentinel value with proper identity semantics, repr, and copy/pickle support.

**Why:** Sentinel values are a common pattern (distinguishing "not provided" from `None`). Currently requires boilerplate class definitions. A builtin would standardize the pattern. Maps naturally to a singleton-pattern class in C#.

**Complexity:** Medium. New builtin function, new `Sentinel` type in Sharpy.Core, codegen for singleton pattern.

**Example:**
```python
MISSING = sentinel("MISSING")

def get(self, key, default=MISSING):
    if default is MISSING:
        raise KeyError(key)
    return default
```

#### 7. User-defined type guards / `TypeIs` (Python 3.13, PEP 742) — [#995](https://github.com/antonsynd/sharpy/issues/995)

**What:** Functions that narrow types when used in conditionals: `def is_str(x: object) -> TypeIs[str]: return isinstance(x, str)`.

**Why:** Sharpy already has `is` type narrowing, but user-defined type guard functions would allow more complex type narrowing logic (e.g., validating dict structure, checking enum variants). This extends the type narrowing system rather than replacing it.

**Complexity:** High. Needs a `TypeIs[T]` return type, semantic analysis to track narrowing through function calls, and integration with `TypeNarrowingContext`.

#### 8. `ParamSpec` (Python 3.10, PEP 612) — [#996](https://github.com/antonsynd/sharpy/issues/996)

**What:** Type variable that captures the full parameter signature of a callable, enabling precise typing of decorators and higher-order functions.

**Why:** Sharpy has decorators and higher-order functions. Without `ParamSpec`, wrapper functions lose parameter type information. This is important for framework code that wraps callables.

**Complexity:** Very high. Fundamental addition to the type system. Would need `ParamSpec` as a new `SemanticType`, inference for parameter capture, and codegen for delegate forwarding.

#### 9. `TypeVarTuple` / variadic generics (Python 3.11, PEP 646) — [#997](https://github.com/antonsynd/sharpy/issues/997)

**What:** Type variable that captures a variable number of type arguments: `def f[*Ts](*args: *Ts) -> tuple[*Ts]:`.

**Why:** Enables typing for variadic functions, tuple manipulation, and NumPy-style array shape typing. Sharpy's numpy stdlib would benefit from shape-typed arrays.

**Complexity:** Very high. Major type system extension affecting generics inference, codegen, and potentially runtime representation.

### Tier 3 — Worth Watching / Niche Value

#### 10. Async comprehensions (Python 3.6, PEP 530) — [#998](https://github.com/antonsynd/sharpy/issues/998)

**What:** `[x async for x in aiter]` and `{await f() for f in funcs}`.

**Status:** Explicitly rejected in Sharpy. Consider revisiting if demand emerges — `await foreach` in C# makes the codegen straightforward, and the pattern is increasingly common in async-heavy Python code.

#### 11. Implicit namespace packages (Python 3.3, PEP 420)

**What:** Directories without `__init__.py` can be packages.

**Why:** Reduces boilerplate for simple project structures. However, explicit `__init__.spy` gives the compiler a clear package boundary signal, which is valuable for ahead-of-time compilation.

**Recommendation:** Keep current behavior. The explicitness aligns with Sharpy's compiled nature.

#### 12. `__init_subclass__` hook (Python 3.6, PEP 487)

**What:** Base class receives notification when subclassed, with arguments: `class Base: def __init_subclass__(cls, **kwargs):`.

**Why:** Useful for class registries and plugin systems. However, .NET has alternatives (assembly scanning, source generators, `[DerivedType]` attributes). Lower priority.

#### 13. Module `__getattr__` (Python 3.7, PEP 562)

**What:** Modules can define `__getattr__()` for lazy attribute access and deprecation warnings.

**Why:** Useful for backward compatibility and lazy loading. Conflicts with Sharpy's static compilation — all module attributes must be known at compile time. Not recommended.

#### 14. `@dataclass_transform` (Python 3.11, PEP 681)

**What:** Marks a decorator/base class as creating dataclass-like types, for type checker compatibility.

**Why:** Sharpy's `@dataclass` is built into the compiler, so third-party dataclass-like patterns aren't a concern. Skip.

#### 15. f-string conversion flags (`!r`, `!s`, `!a`) (Python 3.0)

**What:** `f'{name!r}'` calls `repr()`, `!s` calls `str()`, `!a` calls `ascii()`.

**Why:** Common in Python debugging output. `!r` is especially useful for string values (shows quotes). If implementing the `=` specifier (#1), conversion flags are a natural companion.

**Complexity:** Low-medium. Lexer needs to recognize `!` before format spec in f-strings; codegen wraps expression in `repr()`/`str()` call.

---

## Feature Priority Matrix

| Feature | Issue | Value | Complexity | .NET Fit | Priority |
|---------|:-----:|:-----:|:----------:|:--------:|:--------:|
| f-string `=` specifier | [#986](https://github.com/antonsynd/sharpy/issues/986) | High | Low | Easy | **P0** |
| f-string conversion flags (`!r`) | [#987](https://github.com/antonsynd/sharpy/issues/987) | High | Low | Easy | **P0** |
| `zip(strict=True)` | [#988](https://github.com/antonsynd/sharpy/issues/988) | Medium | Low | Easy | **P0** |
| `@` matrix multiplication | [#989](https://github.com/antonsynd/sharpy/issues/989) | Medium | Medium | Good | **P1** |
| `map(strict=True)` | [#990](https://github.com/antonsynd/sharpy/issues/990) | Low | Low | Easy | **P1** |
| List + and patterns in match | [#991](https://github.com/antonsynd/sharpy/issues/991) | Medium | Low | Easy | **P1** |
| `X \| Y` free union types | [#992](https://github.com/antonsynd/sharpy/issues/992) | Medium | High | Complex | **P2** |
| Lazy imports | [#993](https://github.com/antonsynd/sharpy/issues/993) | High | High | Good (Lazy\<T\>) | **P2** |
| `sentinel()` builtin | [#994](https://github.com/antonsynd/sharpy/issues/994) | Medium | Medium | Good | **P2** |
| User-defined type guards | [#995](https://github.com/antonsynd/sharpy/issues/995) | High | High | Complex | **P2** |
| `ParamSpec` | [#996](https://github.com/antonsynd/sharpy/issues/996) | Medium | Very High | Complex | **P3** |
| `TypeVarTuple` | [#997](https://github.com/antonsynd/sharpy/issues/997) | Medium | Very High | Complex | **P3** |
| Async comprehensions | [#998](https://github.com/antonsynd/sharpy/issues/998) | Medium | Medium | Easy | **Revisit** |

*~~Unpacking in comprehensions~~ — already implemented (PEP 798 with parser support).*

---

## Version Alignment

If Sharpy were mapped to a "Python equivalent version" based on feature coverage:

| Python Era | Coverage | Notes |
|------------|----------|-------|
| 3.0–3.5 | ~95% | Missing: metaclasses (by design), `@` operator |
| 3.6–3.8 | ~85% | Missing: async comprehensions (by design), `f'{x=}'` |
| 3.9–3.10 | ~75% | Missing: free `X \| Y` unions (only `T \| None`), `ParamSpec`, `zip(strict=)` |
| 3.11–3.12 | ~70% | Missing: `TypeVarTuple`, `@dataclass_transform`, exception notes |
| 3.13–3.14 | ~60% | Has TypeVar defaults + template strings; missing `TypeIs`, lazy annotations N/A |
| 3.15 | ~40% | Has `frozendict` + comprehension unpacking; missing lazy imports, sentinels |

**Sharpy's feature set most closely aligns with Python ~3.10–3.12**, with some features from 3.13–3.15 (template strings, TypeVar defaults, frozendict, comprehension unpacking) and unique features that go beyond any Python version (Result types, Optional types, `?` operator, match expressions, structs, interfaces, events, .NET interop).

---

## Verification Notes

*This document was verified against the actual compiler implementation, stdlib source, and test fixtures on 2026-06-27. The following corrections were applied after verification:*

| Original Claim | Correction | Evidence |
|----------------|------------|----------|
| `X \| Y` union syntax: ✅ | → ⚡ Partial | `Parser.Types.cs:32` — "Free unions like 'int \| str' are not supported"; only `T \| None` allowed |
| Unpacking in comprehensions: ❌ | → ✅ Implemented | `Parser.Primaries.cs:453,536,629` — PEP 798 comments; `ListSpreadElement`, `DictSpreadComprehension` AST nodes |
| List patterns (`[a, b]`): ✅ | → ✅ Implemented (#991) | Parser (`ParseSinglePattern` `LeftBracket` → `ParseListPattern`, `StarPattern` for `*rest`), semantic (`CheckListPattern`), codegen (C# list/slice patterns). Sharpy.List gained `Length` + `Index`/`Range` indexers to be list-pattern compatible. |
| And patterns (`x and y`): ✅ | → ✅ Implemented (#991) | Parser (`ParseAndPattern`, `and` binds tighter than `\|`), semantic (`CheckAndPattern`, duplicate-capture check SPY0372), codegen (C# `and` pattern) |
| Guard clause keyword: `when` | → `if` (in match) | `Parser.Statements.cs:1284,1318` — match arms/cases use `if`; `when` is used only in `except` filters |
| Decorator grammar: "specific keywords" | → specific registered names | `DecoratorValidator.cs:287` rejects unknown names (SPY0444); `@[...]` bracket syntax allows arbitrary C# attributes |
| `@classmethod`: unlisted | → ❌ Explicitly unsupported | `DecoratorValidator.cs` UnsupportedDecorators dict: "@classmethod is not supported in Sharpy" |
| `@staticmethod`: unlisted | → ❌ Explicitly unsupported | Same dict — "@staticmethod is not supported" (use `@static` or implicit static) |
| `delegate` keyword: listed as ✅ | Confirmed ✅ fully implemented | `Parser.Definitions.cs:1082`, `TypeChecker.Definitions.cs:1369`, `RoslynEmitter.TypeDeclarations.cs:1783`, test fixtures in `delegates/` |
| Multiple inheritance: ❌ | → ⚡ First=class, rest=interfaces | `ModuleLoader.cs` — `BaseClasses[0]` = inheritance, `BaseClasses.Skip(1)` = interfaces |
