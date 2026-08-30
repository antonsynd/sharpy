# Nullable Types (.NET Interop)

The `T | None` syntax marks a type as **C# nullable** for .NET interop. This is Sharpy's way of expressing that a value may be `null` at the .NET level.

> **For Sharpy-native optional values**, use `T?` which desugars to `Optional[T]` — a safe tagged union. See [Optional Type](tagged_unions_optional.md).

## Syntax

```python
# C# nullable type annotations (T | None)
raw: str | None = dotnet_api()
result: int | None = get_nullable_int()
optional_list: list[int] | None = None

# All types are non-nullable by default
exists: bool = False           # Cannot be None
count: int = 42                # Cannot be None
numbers: list[int] = [42, 67]  # Cannot be None

# Assigning None requires T | None annotation
value: int | None = None    # OK
other: int = None            # ERROR: Cannot assign None to non-nullable type
```

## `T | None` Is a Nullability Modifier, Not a Union

`T | None` is the **only** valid inline union syntax. It is semantically a nullability modifier (like C# `?`), not a general union constructor. Free unions like `int | str` are not supported.

```python
# ✅ Valid - T | None for nullable
x: int | None = None

# ❌ Invalid - no free unions
x: int | str = 42        # ERROR: free unions not supported
x: int | str | None = 42 # ERROR: free unions not supported
```

**Rationale:** C# 9.0 has no anonymous unions. Named unions via `union Foo:` are more maintainable and .NET-idiomatic. Keeping `| None` special avoids ambiguity.

## When to Use `T | None`

Use `T | None` when interfacing with .NET APIs that return or accept nullable values:

```python
# Calling .NET APIs that may return null
raw: str | None = dotnet_method()
result: int | None = nullable_int_from_csharp()

# Passing nullable values to .NET APIs
def call_dotnet_api(value: int | None) -> None:
    ...

# Working with .NET collections that may contain nulls
items: list[str | None] = dotnet_list_with_nulls()
```

### Declared Nullability of .NET Members

A .NET member's type is read from its **declaration**, not from the runtime `Type`: a member
declared with a nullable reference type (`string? Name`, `Dict<string, string>? Proxies`,
`DirectoryInfo? Parent`, a `string?` return or parameter) is typed `T | None`, and a member declared
non-nullable is typed `T`. `None` may be stored into the former and is refused (SPY0229) for the
latter — the same answer C# gives.

```python
from system import Environment
from system.threading import Thread

def main() -> None:
    home: str | None = Environment.get_environment_variable("HOME")  # declared `string?`
    cwd: str = Environment.current_directory                         # declared `string`
    Thread.current_thread.name = None                                # `string? Name` accepts None
    print(home is not None, len(cwd) > 0)
```

```
True True
```

Only the top-level declaration is read: `List<string?>` is `list[str]`. An assembly compiled without
nullable annotations reports no state, and its members are typed non-nullable.

**Do NOT use `T | None` for Sharpy-native optionals.** Use `T?` (which desugars to `Optional[T]`) instead:

```python
# ❌ Avoid for Sharpy-native code
name: str | None = None

# ✅ Prefer T? for Sharpy-native optionals
name: str? = None()
```

## Converting to Safe Optionals

Use `maybe` to convert a `T | None` value into a safe `T?` (`Optional[T]`):

```python
raw: str | None = dotnet_api()   # C# nullable
safe: str? = maybe raw            # Convert to Optional[str]
```

See [Maybe Expressions](maybe_expressions.md) for details.

*Implementation*
- *✅ Native - Maps to C# nullable reference types with `#nullable enable`.*

## See Also

- [Optional Type](tagged_unions_optional.md) - Safe `T?` / `Optional[T]` for Sharpy-native optionals
- [Maybe Expressions](maybe_expressions.md) - Converting `T | None` to `T?`
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Null Coalescing Assignment](null_coalescing_assignment.md) - The `??=` operator
- [Null Conditional Access](null_conditional_access.md) - The `?.` operator
