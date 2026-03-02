# SRP-0001: `@kwargs` Decorator

| Field | Value |
|-------|-------|
| **Status** | Rejected |
| **Date** | 2026-03-01 |
| **Phase** | 11.3 (v0.2.5) |
| **Author** | — |
| **Rejection reason** | Violates "no magic" principle; existing features suffice |

## Summary

A compiler-understood transforming decorator that generates a companion struct and method overload for bundling keyword-only arguments into a reusable object.

## Proposed Syntax

```python
@kwargs
def configure(host: str, /, *, port: int = 8080, timeout: float = 30.0) -> Config:
    return Config(host, port, timeout)

# Generated struct usable at call sites
opts = ConfigureKwargs(port=9000, timeout=60.0)
configure("localhost", opts)
```

## Proposed C# Emission

```csharp
// Primary overload (unchanged)
public static Config Configure(string host, int port = 8080, double timeout = 30.0)
    => new Config(host, port, timeout);

// Generated kwargs overload
public static Config Configure(string host, ConfigureKwargs kwargs)
    => Configure(host, kwargs.Port ?? 8080, kwargs.Timeout ?? 30.0);

// Generated struct
public readonly struct ConfigureKwargs
{
    public int? Port { get; init; }
    public double? Timeout { get; init; }
}
```

## Motivation

Python's `**kwargs` pattern allows callers to bundle keyword arguments into dictionaries and pass them to functions. This is commonly used for:
- Reusable configuration objects
- Forwarding options through layers of function calls
- APIs with many optional parameters

The `@kwargs` proposal attempted to bring this pattern to Sharpy with full static type safety by generating a struct at compile time.

## Rejection Rationale

### 1. Violates "no magic" — the strongest objection

Every existing Sharpy decorator (`@virtual`, `@final`, `@abstract`, `@static`, etc.) is a **metadata flag** — it annotates existing code. `@kwargs` would be a fundamentally new kind of decorator: a **code generator** that synthesizes types and methods that don't appear in the source. This is invisible, surprising, and hard to debug.

The principle "magic behavior — unpredictable; prefer explicit" exists precisely to prevent this class of feature.

### 2. Creates a second calling convention

Sharpy already supports named arguments with default values:

```python
configure("localhost", port=9000, timeout=60.0)
```

Adding a kwargs struct creates a second way to do the same thing. The anti-pattern "multiple ways to do same thing — consistency issue" applies directly.

### 3. Complexity cascades

The full proposal required handling:
- **Overload disambiguation**: type-suffixed struct names (`ProcessKwargs_str`, `ProcessKwargs_bytes`) — fragile naming
- **Inheritance extension**: derived classes generating new structs that include parent fields
- **`with` expressions on structs**: requires C# 10 record structs, not available in C# 9.0 generated code

Each of these is solvable, but the aggregate complexity is disproportionate to the value delivered.

### 4. "Add X because Python has it"

Python needs `**kwargs` because it lacks named arguments with default values as a first-class feature at the language level. C# (and Sharpy) already have this. The feature earns its complexity in Python but not in Sharpy.

## Alternative

Define an explicit options struct — no compiler magic, works today:

```python
struct ConfigOptions:
    port: int = 8080
    timeout: float = 30.0

def configure(host: str, opts: ConfigOptions = ConfigOptions()) -> Config:
    return Config(host, opts.port, opts.timeout)
```

This is more boilerplate but fully explicit, debuggable, and consistent with the rest of the language.

## See Also

- [SRP-0002: `@dynamic_kwargs` decorator](SRP-0002-dynamic-kwargs-decorator.md) — the dynamic variant, also rejected
- [Flexible Arguments spec](../language_specification/flexible_arguments.md) — positional-only and keyword-only markers (implemented)
