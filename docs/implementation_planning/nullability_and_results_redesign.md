## Nullability & Error Handling — Design Decisions

### 1. Reverse the `T?` Semantics

| Syntax | Meaning | Rationale |
|--------|---------|-----------|
| `T?` | `Optional[T]` (safe tagged union) | Safe construct gets ergonomic syntax |
| `T \| None` | C# `T?` (nullable) | Explicit interop marker, reads as what it is |

The safest option should be the default. `T?` is the common case in Sharpy-native code; `T | None` appears only at .NET boundaries.

### 2. `| None` Is a Nullability Modifier, Not a Union

`T | None` is the **only** valid inline union. It's semantically a nullability modifier (like `?`), not a general union constructor. No free unions like `int | str`.

Rationale: C# 9.0 has no anonymous unions, named unions via `union Foo:` are more maintainable and .NET-idiomatic, and keeping `| None` special avoids ambiguity.

### 3. Add `T !E` Syntax for Result Types

```python
def parse(s: str) -> int !ValueError:
    ...

# Desugars to
def parse(s: str) -> Result[int, ValueError]:
    ...
```

**Parsing rules:**
- `!E` only valid in type annotation contexts (no conflict with negation)
- `!E` binds tighter than `| None`: `int !E | None` → `(int !E) | None`
- Recommended: restrict to top-level return types; require `Result[T, E]` for nested/complex cases

### 4. Stdlib Uses Result for Expected Failures

| Category | Style | Example |
|----------|-------|---------|
| Parsing/conversion | `Result` | `int.parse(s: str) -> int !ValueError` |
| File/network open | `Result` | `open(path: str) -> File !IOError` |
| Collection "get" | `Optional` | `dict.get(key: K) -> V?` |
| Collection index | Exception | `list[i]` throws `IndexError` |
| Type casting | `Result` | `obj to Dog` returns `Result` |

**Guiding principle:** Exceptions are for bugs. Results are for expected failures.

### 5. The `maybe` Expression Bridges Worlds

```python
raw: str | None = dotnet_api()  # C# nullable
safe: str? = maybe raw          # Convert to Optional[str]
```

`maybe` is the explicit "crossing from unsafe .NET into safe Sharpy" marker.

### 6. `Optional[T]` Is a Struct

No heap allocation for returning `Optional[T]` — just a bool + value, like `Nullable<T>` but with tagged union semantics.

---

### Quick Reference

```python
# Safe optional (tagged union)
name: str? = user.get_name()           # Optional[str]

# C# nullable (interop)
raw: str | None = dotnet_method()      # C# string?

# Result type (sugar)
def parse(s: str) -> int !ValueError:  # Result[int, ValueError]

# Result type (explicit)
def fetch(url: str) -> Result[Response, HttpError]:

# Conversions
safe_opt: str? = maybe raw_nullable    # T | None → T?
result: int !Exception = try int(s)    # Wrap throwing expr in Result
```
