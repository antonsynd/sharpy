# Flexible Arguments

This document specifies Sharpy's extended argument handling features, which provide Python-style parameter flexibility while maintaining static type safety.

## Overview

Sharpy provides three tiers of argument flexibility:

| Tier | Feature | Cost | Decorator Required |
|------|---------|------|-------------------|
| 0 | Positional-only (`/`) and keyword-only (`*`) markers | Zero (compile-time only) | No |
| 1 | Typed kwargs via generated struct | Small (struct + overload) | `@kwargs` |
| 2 | Dynamic kwargs dictionary | Runtime (dictionary operations) | `@dynamic_kwargs` |

Each tier is opt-in and the costs are explicit. The vanilla C# calling convention is always preserved alongside any extended overloads.

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

## Tier 1: Typed Kwargs (`@kwargs`)

The `@kwargs` decorator enables Python-style keyword argument bundling while maintaining full static type safety. It generates a companion struct and an additional overload.

### Syntax

```python
@kwargs
def configure(host: str, /, *, port: int = 8080, timeout: float = 30.0, retries: int = 3) -> Config:
    return Config(host, port, timeout, retries)
```

**Requirements:**
- The `@kwargs` decorator must be applied to the function
- At least one keyword-only parameter (after `*`) must be present
- Positional-only parameters (before `/`) remain positional in all overloads

### Generated Code

For the example above, the compiler generates:

```csharp
// Primary overload (vanilla C# signature)
public static Config Configure(string host, int port = 8080, float timeout = 30.0f, int retries = 3)
    => new Config(host, port, timeout, retries);

// Kwargs overload
public static Config Configure(string host, ConfigureKwargs kwargs)
    => Configure(host, kwargs.Port ?? 8080, kwargs.Timeout ?? 30.0f, kwargs.Retries ?? 3);

// Auto-generated kwargs struct
public readonly struct ConfigureKwargs
{
    public int? Port { get; init; }
    public float? Timeout { get; init; }
    public int? Retries { get; init; }
}
```

### Calling Conventions

```python
# Standard call (uses primary overload)
configure("localhost", port=9000, timeout=60.0)

# Create reusable options object
prod_opts = ConfigureKwargs(port=443, timeout=120.0, retries=5)
dev_opts = ConfigureKwargs(port=8080, timeout=5.0, retries=1)

# Pass options object (uses kwargs overload)
configure("prod.example.com", prod_opts)
configure("localhost", dev_opts)

# Spread syntax to unpack kwargs struct
configure("localhost", **prod_opts)
```

### Struct Initialization

The kwargs struct supports multiple initialization styles:

```python
# Named arguments (most common)
opts = ConfigureKwargs(port=9000, timeout=60.0)

# Partial initialization (unset fields remain None internally)
opts = ConfigureKwargs(port=9000)  # timeout and retries use function defaults

# Modification via 'with' expression
new_opts = opts with timeout=120.0
```

### Combining with Positional-Only

When `/` is used, positional-only parameters are excluded from the kwargs struct:

```python
@kwargs
def process(data: bytes, /, *, encoding: str = "utf-8", validate: bool = True) -> str:
    pass
```

```csharp
// 'data' stays positional, only keyword-only params go in struct
public readonly struct ProcessKwargs
{
    public string? Encoding { get; init; }
    public bool? Validate { get; init; }
}

public static string Process(byte[] data, string encoding = "utf-8", bool validate = true) => ...;
public static string Process(byte[] data, ProcessKwargs kwargs) => ...;
```

### Struct Naming Convention

The generated struct name follows the pattern `{FunctionName}Kwargs`:

| Function | Struct Name |
|----------|-------------|
| `configure` | `ConfigureKwargs` |
| `http_request` | `HttpRequestKwargs` |
| `MyClass.process` | `MyClass.ProcessKwargs` (nested) |

For methods, the struct is nested within the containing class to avoid name collisions.

### Inheritance and Kwargs

When overriding a method decorated with `@kwargs`, the derived class can:

1. **Inherit the same kwargs struct** (default behavior)
2. **Extend with additional kwargs** (generates a new struct that includes parent fields)

```python
class BaseClient:
    @kwargs
    @virtual
    def request(self, url: str, /, *, timeout: float = 30.0) -> Response:
        pass

class ExtendedClient(BaseClient):
    @kwargs
    @override
    def request(self, url: str, /, *, timeout: float = 30.0, retry_count: int = 3) -> Response:
        pass
```

### Performance Characteristics

| Aspect | Cost |
|--------|------|
| Struct allocation | Stack-allocated (zero heap allocation for `readonly struct`) |
| Nullable wrappers | Minimal overhead for value types |
| Overload dispatch | Resolved at compile time |
| Default value handling | Single null-coalescing check per field |

The kwargs struct is a `readonly struct`, which .NET can often optimize to pass by reference or inline entirely.

### Limitations

- Kwargs struct fields are always nullable (to detect "not provided")
- Cannot spread (`**`) a dictionary into a kwargs struct (use Tier 2 for dynamic kwargs)
- Kwargs struct cannot contain `ref` or `out` parameters

*Implementation: 🔄 Lowered - Generates struct definition and additional method overload.*

---

## Tier 2: Dynamic Kwargs (`@dynamic_kwargs`)

For rare cases requiring truly dynamic keyword arguments (unknown keys at compile time), Sharpy provides `@dynamic_kwargs`. This trades type safety for flexibility.

### Syntax

```python
@dynamic_kwargs
def forward_request(endpoint: str, **kwargs: dict[str, object]) -> Response:
    return http_post(endpoint, kwargs)
```

**Requirements:**
- The `@dynamic_kwargs` decorator must be applied
- The function must have a `**kwargs` parameter with explicit type annotation
- The annotation must be `dict[str, T]` where `T` is the value type

### Generated Code

```csharp
public static Response ForwardRequest(string endpoint, IDictionary<string, object?>? kwargs = null)
{
    kwargs ??= new Dictionary<string, object?>();
    return HttpPost(endpoint, kwargs);
}
```

### Calling Conventions

```python
# Pass keyword arguments dynamically
forward_request("/api/users", name="Alice", age=30, active=True)

# Pass a dictionary directly
params = {"name": "Bob", "role": "admin"}
forward_request("/api/users", **params)

# Mix positional and dynamic kwargs
forward_request("/api/users", **params, extra_field="value")
```

### Type Safety Trade-offs

| Aspect | Tier 1 (`@kwargs`) | Tier 2 (`@dynamic_kwargs`) |
|--------|-------------------|---------------------------|
| Key validation | Compile-time | Runtime (KeyError) |
| Value types | Strongly typed per field | Single type or `object` |
| IDE autocomplete | Full support | Limited to known keys |
| Refactoring safety | High | Low |

### Accessing Dynamic Kwargs

Inside the function, kwargs is a standard dictionary:

```python
@dynamic_kwargs
def process(**kwargs: dict[str, object]) -> None:
    # Type-safe access requires explicit casting
    name = kwargs.get("name") to str? ?? "default"
    count = kwargs.get("count") to int? ?? 0

    # Iteration
    for key, value in kwargs.items():
        print(f"{key} = {value}")

    # Check for presence
    if "optional_flag" in kwargs:
        handle_flag(kwargs["optional_flag"] to bool)
```

### Combining with Typed Parameters

Dynamic kwargs can coexist with typed positional and keyword parameters:

```python
@dynamic_kwargs
def api_call(
    method: str,           # Required positional
    url: str,              # Required positional
    /,                     # Positional-only marker
    *,                     # Keyword-only marker
    timeout: float = 30.0, # Typed keyword-only
    **headers: dict[str, str]  # Dynamic kwargs for HTTP headers
) -> Response:
    pass

# Usage
api_call("GET", "/users", timeout=60.0, Authorization="Bearer token", Accept="application/json")
```

### When to Use Tier 2

Use `@dynamic_kwargs` only when:
- Forwarding arguments to external APIs with unknown schemas
- Building generic wrappers or decorators
- Interfacing with dynamic .NET APIs (e.g., `ExpandoObject`)

For most cases, prefer Tier 1 (`@kwargs`) for its compile-time safety.

*Implementation: 🔄 Lowered - Generates method with dictionary parameter and call-site transformation.*

---

## Interaction with Other Features

### Pipe Operator

All tiers work with the pipe operator:

```python
@kwargs
def transform(data: list[int], /, *, scale: float = 1.0) -> list[float]:
    return [x * scale for x in data]

# Pipe with kwargs
result = [1, 2, 3] |> transform(scale=2.0)

# Pipe with kwargs struct
opts = TransformKwargs(scale=2.0)
result = [1, 2, 3] |> transform(opts)
```

### Partial Application

Partial application works with Tier 0 validation:

```python
def example(a: int, /, b: int, *, c: int) -> int:
    return a + b + c

partial_fn = example(1, _, c=3)  # ✅ 'a' positional, 'c' keyword-only
result = partial_fn(2)          # Returns 6
```

### Overloading

Functions with `@kwargs` can still be overloaded by parameter types:

```python
@kwargs
def process(data: str, /, *, encoding: str = "utf-8") -> bytes:
    pass

@kwargs
def process(data: bytes, /, *, validate: bool = True) -> str:
    pass

# Each overload gets its own kwargs struct
# ProcessKwargs_str (for str overload)
# ProcessKwargs_bytes (for bytes overload)
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
dynamic_kwargs    = "**" IDENTIFIER ":" "dict" "[" type "," type "]" ;

(* Kwargs spread at call site *)
argument          = expression | IDENTIFIER "=" expression | "**" expression ;
```

---

## Migration Guide

### From Python

| Python | Sharpy |
|--------|--------|
| `def f(x, /, y, *, z): ...` | `def f(x: T, /, y: T, *, z: T) -> R: ...` (same syntax, needs types) |
| `def f(**kwargs): ...` | `@dynamic_kwargs def f(**kwargs: dict[str, object]) -> R: ...` |
| Untyped kwargs dict | Use `@kwargs` with typed struct instead |

### From C#

| C# | Sharpy |
|-----|--------|
| Named arguments | Same: `f(name: value)` |
| Optional parameters | Same: `def f(x: int = 0)` |
| `params` array | `*args: T` syntax |
| No equivalent | `/` and `*` markers (Sharpy-only validation) |

---

## .NET Interop and Metadata

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
