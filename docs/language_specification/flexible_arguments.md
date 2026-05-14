# Flexible Arguments

> **Implementation status:** Tier 0 (positional-only `/` and keyword-only `*`) is fully implemented. Tiers 1 and 2 (`@kwargs` and `@dynamic_kwargs`) have been dropped from the roadmap.

This document specifies Sharpy's extended argument handling features, which provide Python-style parameter flexibility while maintaining static type safety.

## Overview

Sharpy provides compile-time argument flexibility via positional-only and keyword-only parameter markers:

| Feature | Cost | Decorator Required |
|---------|------|-------------------|
| Positional-only (`/`) and keyword-only (`*`) markers | Zero (compile-time only) | No |

The vanilla C# calling convention is always preserved. For bundling optional keyword arguments, use a user-defined options struct — no special decorator needed.

> **Design note:** `@kwargs` (compiler-generated structs) and `@dynamic_kwargs` (dictionary-based `**kwargs`) were evaluated and dropped. Compiler-understood transforming decorators violate the "no magic" principle, and dynamic kwargs conflicts with Axiom 3 (type safety). Named arguments with default values and explicit option structs achieve the same goals without invisible code generation.

---

## Tier 0: Positional-Only and Keyword-Only Parameters

Sharpy supports Python 3.8+ style parameter markers that enforce how arguments must be passed at call sites. This is **purely compile-time validation** with zero runtime cost.

### Syntax

```python
def example(pos_only: int, /, normal: int, *, kw_only: int) -> int:
    return pos_only + normal + kw_only
```

- `/` — Parameters **before** this marker are positional-only
- `*` — Parameters **after** this marker are keyword-only
- Parameters between `/` and `*` can be passed either way

### Rules

1. `/` must appear before `*` if both are present
2. `/` and `*` are not parameters themselves; they are markers
3. Default values work normally with both marker types
4. `*args` (variadic) implicitly acts as the `*` marker for keyword-only separation

### Examples

```python
# Positional-only parameters (before /)
def set_position(x: int, y: int, /) -> None:
    """x and y must be passed positionally."""
    pass

set_position(10, 20)        # ✅ Valid
set_position(x=10, y=20)    # ❌ ERROR: 'x' is positional-only
set_position(10, y=20)      # ❌ ERROR: 'y' is positional-only

# Keyword-only parameters (after *)
def configure(*, host: str, port: int = 8080) -> None:
    """host and port must be passed by name."""
    pass

configure(host="localhost")           # ✅ Valid
configure(host="localhost", port=9000) # ✅ Valid
configure("localhost")                # ❌ ERROR: 'host' is keyword-only
configure("localhost", 9000)          # ❌ ERROR: positional args not allowed

# Combined: positional-only, normal, and keyword-only
def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]:
    """
    query: must be positional
    limit: can be positional or keyword
    case_sensitive: must be keyword
    """
    pass

search("hello")                                    # ✅ Valid
search("hello", 20)                                # ✅ Valid
search("hello", limit=20)                          # ✅ Valid
search("hello", 20, case_sensitive=True)           # ✅ Valid
search(query="hello")                              # ❌ ERROR: 'query' is positional-only
search("hello", 20, True)                          # ❌ ERROR: 'case_sensitive' is keyword-only
```

### Use Cases

**Positional-only (`/`) is useful when:**
- Parameter names are implementation details that shouldn't be part of the API
- You want freedom to rename parameters without breaking callers
- The parameter name would be confusing as a keyword (e.g., `len(obj)` not `len(obj=x)`)

**Keyword-only (`*`) is useful when:**
- Parameters are optional flags that benefit from explicit naming
- Argument order would be confusing or error-prone
- You want to force callers to be explicit about their intent

### C# Mapping

The generated C# signature is unchanged—markers are purely compile-time Sharpy semantics:

```python
# Sharpy
def example(a: int, /, b: int, *, c: int = 3) -> int:
    return a + b + c
```

```csharp
// C# 9.0 - No runtime difference
public static int Example(int a, int b, int c = 3) => a + b + c;
```

The Sharpy compiler tracks parameter categories and validates call sites accordingly.

*Implementation: 🔄 Lowered - Compile-time validation only, no runtime cost.*

---

## ~~Tier 1: Typed Kwargs (`@kwargs`)~~ — Dropped

> **Dropped from roadmap.** See [SRP-0001](../rejected_proposals/SRP-0001-kwargs-decorator.md) for full rationale. Compiler-understood transforming decorators (generating invisible structs and overloads) violate the "no magic" principle. Named arguments with default values provide equivalent ergonomics. For reusable option bundles, define an explicit struct:
>
> ```python
> struct ConfigOptions:
>     port: int = 8080
>     timeout: float = 30.0
>
> def configure(host: str, opts: ConfigOptions = ConfigOptions()) -> Config:
>     return Config(host, opts.port, opts.timeout)
> ```

---

## ~~Tier 2: Dynamic Kwargs (`@dynamic_kwargs`)~~ — Dropped

> **Dropped from roadmap.** See [SRP-0002](../rejected_proposals/SRP-0002-dynamic-kwargs-decorator.md) for full rationale. Dynamic kwargs conflicts with Axiom 3 (type safety) and introduces a `**kwargs` parameter syntax that only works with a specific decorator. For dynamic argument forwarding, pass a `dict[str, T]` explicitly:
>
> ```python
> def forward_request(endpoint: str, kwargs: dict[str, object] = {}) -> Response:
>     return http_post(endpoint, kwargs)
>
> forward_request("/api", {"name": "Alice", "age": 30})
> ```

---

## Interaction with Other Features

### Pipe Operator

Positional-only and keyword-only markers work with the pipe operator:

```python
def transform(data: list[int], /, *, scale: float = 1.0) -> list[float]:
    return [x * scale for x in data]

result = [1, 2, 3] |> transform(scale=2.0)
```

### Partial Application

Partial application works with positional-only and keyword-only validation:

```python
def example(a: int, /, b: int, *, c: int) -> int:
    return a + b + c

partial_fn = example(1, _, c=3)  # ✅ 'a' positional, 'c' keyword-only
result = partial_fn(2)          # Returns 6
```

---

## Grammar Extensions

```ebnf
(* Parameter markers *)
parameter_list    = [positional_only_params] [regular_params] [keyword_only_params] [variadic_params] ;
positional_only_params = param_def {"," param_def} "," "/" ;
regular_params    = param_def {"," param_def} ;
keyword_only_params = "*" "," param_def {"," param_def} ;
variadic_params   = "*" IDENTIFIER [":" type_annotation] ;
```

---

## Migration Guide

### From Python

| Python | Sharpy |
|--------|--------|
| `def f(x, /, y, *, z): ...` | `def f(x: T, /, y: T, *, z: T) -> R: ...` (same syntax, needs types) |
| `def f(**kwargs): ...` | Not supported — pass `dict[str, T]` explicitly |
| Untyped kwargs dict | Use named arguments with defaults, or a user-defined options struct |

### From C#

| C# | Sharpy |
|-----|--------|
| Named arguments | Same: `f(name: value)` |
| Optional parameters | Same: `def f(x: int = 0)` |
| `params` array | `*args: T` syntax |
| No equivalent | `/` and `*` markers (Sharpy-only validation) |

---

## .NET Interop and Metadata

> **Status: Not Yet Implemented** — The attribute-based metadata described in this section is planned but not yet implemented. Currently, flexible argument constraints (`/` and `*` markers) are enforced only within Sharpy source compilation and are not preserved in compiled assemblies. The design below describes the intended future behavior.

Flexible argument constraints are preserved in compiled assemblies via .NET custom attributes. This enables Sharpy code to enforce positional-only and keyword-only rules when importing functions from compiled Sharpy libraries.

### Attribute Schema

The compiler emits a `[FlexibleArgs]` attribute on methods with `/` or `*` markers:

```csharp
[FlexibleArgs(positionalOnlyBoundary: 0, keywordOnlyBoundary: 2)]
public static List<string> Search(string query, int limit = 10, bool caseSensitive = false)
```

The boundary indices indicate:
- `positionalOnlyBoundary`: Parameters at indices 0 through this value (inclusive) are positional-only. `-1` means no positional-only parameters.
- `keywordOnlyBoundary`: Parameters at this index and above are keyword-only. `-1` means no keyword-only parameters.

### Cross-Library Enforcement

When Sharpy code imports a function from a compiled `.dll`:

```python
# library.dll was compiled from Sharpy with:
# def search(query: str, /, limit: int = 10, *, case_sensitive: bool = False) -> list[str]

from library import search

search("hello", case_sensitive=True)  # ✅ Valid
search(query="hello")                  # ❌ ERROR: 'query' is positional-only
```

The Sharpy compiler reads the `[FlexibleArgs]` attribute during assembly discovery and enforces constraints at compile time.

### C# Callers

C# code calling Sharpy libraries will **not** automatically enforce these constraints—C# doesn't understand `[FlexibleArgs]`. The constraints are Sharpy-specific compile-time checks.

C# developers can:
1. Observe the attribute in IDE tooltips/documentation
2. Use a Sharpy Roslyn analyzer (if provided) for enforcement
3. Simply follow the documented API contract

### Optional Per-Parameter Attributes

For enhanced IDE support, the compiler can optionally emit per-parameter attributes:

```csharp
[FlexibleArgs(positionalOnlyBoundary: 0, keywordOnlyBoundary: 2)]
public static List<string> Search(
    [PositionalOnly] string query,
    int limit = 10,
    [KeywordOnly] bool caseSensitive = false)
```

This is controlled by a compiler flag and is not emitted by default. The method-level `[FlexibleArgs]` attribute is the canonical source of truth.

*Implementation: The `FlexibleArgsAttribute`, `PositionalOnlyAttribute`, and `KeywordOnlyAttribute` types are defined in `Sharpy.Attributes` namespace.*

---

## See Also

- [Function Parameters](function_parameters.md) — Base parameter handling
- [Function Default Parameters](function_default_parameters.md) — Default value rules
- [Function Variadic Arguments](function_variadic_arguments.md) — `*args` handling
- [Decorators](decorators.md) — Decorator syntax and built-in decorators
- [Spread Operator](spread_operator.md) — `*` and `**` unpacking syntax
