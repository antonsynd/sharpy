# SRP-0007: `Sharpy.Str` Wrapper Type

| Field | Value |
|-------|-------|
| **Status** | Rejected (removing) |
| **Date** | 2026-04-08 |
| **Phase** | Post-v0.1 |
| **Author** | — |
| **Rejection reason** | Anti-pattern: wrapper type for Pythonic API; Axiom 1 (.NET interop) friction |

## Summary

Sharpy's `str` type was originally implemented as `Sharpy.Str`, a `readonly struct` wrapping `System.String`. This gave Sharpy strings a Python-style method surface (`s.upper()`, `s.find()`, `s.split()`) without polluting `System.String`.

After real-world usage, the wrapper creates more problems than it solves. This document records the decision to remove it and the alternatives considered.

## What `Sharpy.Str` Did

```csharp
public readonly partial struct Str
{
    public readonly string Value;
    public static implicit operator Str(string s) => new Str(s);
    public static implicit operator string(Str s) => s.Value;

    public Str Upper() => new Str(Value.ToUpperInvariant());
    // ~2,350 lines of Python string methods
}
```

The compiler mapped `str` → `Sharpy.Str` and emitted casts at string literal boundaries:

```csharp
// Sharpy: x = "hello"
var x = ((Sharpy.Str)"hello");
```

## Problems

### 1. Default parameter workaround (~60 lines across 7 codegen files)

C# requires default parameter values to be compile-time constants. `Sharpy.Str` is a value type, so `(Sharpy.Str)"default"` is not a constant expression. The compiler worked around this with a `_pendingStrDefaults` mechanism: emit `default` as the C# default, then prepend `if (param == default) param = (Sharpy.Str)"actual";` in the method body. This workaround spanned 10 callsites across 7 files.

### 2. Pattern matching casts

`switch` in C# requires constant patterns to match the scrutinee type. Since `Sharpy.Str` is a value type and string literals are `System.String`, every `match` statement on a string required an explicit cast-down of the scrutinee to `string`:

```csharp
// Emitted for: match s: case "hello": ...
switch ((string)s) { case "hello": ... }
```

### 3. Const field ineligibility

String fields declared `const` in Sharpy had to be emitted as `static readonly` because `Sharpy.Str` cannot participate in C# `const` declarations (only primitives and `string` can).

### 4. Interop friction ("colored function" problem)

Every .NET API returns `string`. Every Sharpy API takes `Sharpy.Str`. The implicit conversion operators papered over this, but:
- Boxing occurred at boundaries
- Overload resolution sometimes picked the wrong overload
- Generic code (`List<string>` vs `List<Sharpy.Str>`) was unresolvable without explicit casts

### 5. F-string and enum `.name` wrapping

F-string results and enum `.name` access both required `(Sharpy.Str)` cast wrapping in the emitted code.

## Alternatives Considered

### A. Keep `Sharpy.Str` and improve workarounds

**Rejected.** The workarounds are load-bearing — each one exists because `Str` is a value type and C# semantics don't bend. The number of workarounds will only grow as more features are added (e.g., interpolated string handlers, string switch expressions, generic constraints).

### B. Make `Sharpy.Str` a class instead of a struct

**Rejected.** This would fix const defaults (nullable reference types can use `null` as sentinel), but:
- Heap allocation for every string operation
- Breaking change for all existing Sharpy programs
- Still has the colored function problem
- Equality semantics change (reference vs value)

### C. Use a C# type alias (`using Str = string`)

**Rejected.** C# `using` aliases are file-scoped (even with C# 12 global usings). You cannot add extension methods that only appear when you "are" the alias — extension methods must target the real type (`string`). This means `s.upper()` would be available on all strings in the project, which is actually what we want (see chosen approach).

### D. Source generators to auto-wrap `string`

**Rejected.** Adds build complexity (Roslyn analyzers, generator registration), doesn't solve the const default problem, and still requires runtime wrapping at boundaries.

### E. Compiler-level implicit wrapping (never materialize `Str` in IL)

**Rejected.** This would mean the compiler emits `string` everywhere but pretends it's `Str` during type checking. Essentially what the chosen approach does, but without the clean separation. Would require a phantom type in the semantic model that doesn't exist at runtime — more confusing than just mapping to `string`.

## Chosen Approach: Map `str` → `System.String` + Extension Methods

Follow the Kotlin model: Kotlin's `String` is `java.lang.String` with extension functions in the stdlib (`kotlin.text`).

- `str` maps directly to `string` (C# keyword) in codegen
- Python string methods (`upper()`, `find()`, `split()`, etc.) become extension methods on `string` in `Sharpy.StringExtensions`
- Operations that can't be extension methods (`s * 3`, `s[-1]`, `for c in s`) use `Sharpy.StringHelpers` static methods, called by compiler-generated code
- String literals emit as raw `"hello"` — no casts
- String defaults emit as literal defaults — no sentinel workaround
- Pattern matching works natively — no cast-down
- `const` string fields work natively

### Why this aligns with the axioms

- **Axiom 1 (.NET):** `string` is the native .NET type. Zero interop friction.
- **Axiom 2 (Python):** Extension methods provide `s.upper()`, `s.find()`, etc. — same surface as Python.
- **Axiom 3 (Type Safety):** No implicit conversions, no boxing, no overload ambiguity.

### Precedent

This is explicitly called out in the project's Design Anti-Patterns table:

> | Wrapper types for Pythonic API | Use extension methods instead |

The `Sharpy.Str` wrapper was the last remaining instance of this anti-pattern.

## Impact

- **Compiler:** Remove ~60 lines of workaround code across 7 codegen files
- **Runtime:** Replace ~2,350 lines of struct methods with extension methods (method bodies largely unchanged)
- **Tests:** Regenerate ~174 C# snapshots; update ~10 Core test files
- **Users:** No Sharpy source changes needed (`str` type and methods work identically)
