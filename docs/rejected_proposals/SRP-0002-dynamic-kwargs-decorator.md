# SRP-0002: `@dynamic_kwargs` Decorator

| Field | Value |
|-------|-------|
| **Status** | Rejected |
| **Date** | 2026-03-01 |
| **Phase** | 11.4 (v0.2.5) |
| **Author** | — |
| **Rejection reason** | Conflicts with Axiom 3 (type safety); syntax is context-dependent |

## Summary

A compiler-understood decorator enabling Python's `**kwargs` pattern via dictionary-based dynamic keyword arguments with explicit opt-in.

## Proposed Syntax

```python
@dynamic_kwargs
def forward_request(endpoint: str, **kwargs: dict[str, object]) -> Response:
    return http_post(endpoint, kwargs)

# Call-site keyword arguments lowered to dictionary entries
forward_request("/api", name="Alice", age=30)
```

## Proposed C# Emission

```csharp
public static Response ForwardRequest(string endpoint, IDictionary<string, object?>? kwargs = null)
{
    kwargs ??= new Dictionary<string, object?>();
    return HttpPost(endpoint, kwargs);
}
```

## Motivation

Some scenarios involve truly dynamic keyword arguments where keys are unknown at compile time:
- Forwarding arguments to external APIs with unknown schemas
- Building generic wrappers or middleware
- Interfacing with dynamic .NET APIs (e.g., `ExpandoObject`)

The proposal attempted to provide syntactic sugar for these cases while requiring an explicit decorator opt-in.

## Rejection Rationale

### 1. Directly conflicts with Axiom 3 (type safety)

The feature explicitly trades type safety for flexibility. Inside the function body, values require runtime casts:

```python
name = kwargs.get("name") to str? ?? "default"
count = kwargs.get("count") to int? ?? 0
```

Key validation is runtime-only (`KeyError`), value types are `object`, IDE autocomplete is limited. This is the opposite of Sharpy's type-first philosophy.

The axiom precedence is clear: **.NET > Type Safety > Python Syntax**. A feature that sacrifices type safety to replicate Python syntax fails the prioritization test.

### 2. Context-dependent syntax

The `**kwargs` parameter syntax would only be valid when the `@dynamic_kwargs` decorator is present. Without the decorator, `**kwargs` doesn't parse. A syntax form that is legal or illegal based on a decorator above it is surprising and hard to teach.

### 3. Action at a distance in call-site transformation

At a call site like `forward_request("/api", name="Alice", age=30)`, the compiler must distinguish "these are dictionary keys" from "these are parameter names." This determination depends on the decorator attached to the callee — action at a distance. If the decorator is removed, the call site silently changes meaning or breaks.

### 4. The alternative is trivial and explicit

```python
def forward_request(endpoint: str, kwargs: dict[str, object] = {}) -> Response:
    return http_post(endpoint, kwargs)

forward_request("/api", {"name": "Alice", "age": 30})
```

Less pretty, but the types are visible, the dictionary construction is explicit, and there's no compiler magic. The small ergonomic cost is justified by the clarity gain.

### 5. "Add X because Python has it"

Python's `**kwargs` exists because Python is dynamically typed — all values are `object` already. In a statically typed language, recreating this pattern requires either abandoning type safety (this proposal) or generating typed structs (SRP-0001). Neither earns its complexity when `dict[str, T]` is already available as a parameter type.

## Alternative

Pass a dictionary explicitly:

```python
def forward_request(endpoint: str, kwargs: dict[str, object] = {}) -> Response:
    return http_post(endpoint, kwargs)

forward_request("/api", {"name": "Alice", "age": 30})
```

For scenarios with known keys, use a typed struct instead of a dictionary.

## See Also

- [SRP-0001: `@kwargs` decorator](SRP-0001-kwargs-decorator.md) — the typed variant, also rejected
- [Flexible Arguments spec](../language_specification/flexible_arguments.md) — positional-only and keyword-only markers (implemented)
