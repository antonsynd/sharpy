# Optional Type

> **`T?` is syntactic sugar for `Optional[T]`.** The `T?` shorthand is the preferred way to express optional values in Sharpy-native code.

The `Optional[T]` type is a special tagged union provided by the Sharpy standard library for representing values that may or may not be present. This is similar to Rust's `Option` type.

`Optional[T]` is a **struct** — no heap allocation for returning optional values, just a bool + value (like `Nullable<T>` but with tagged union semantics).

## Definition

```python
union Optional[T]:
    case Some(value: T)
    case None()
```

The `Optional` type is part of the standard library and provides special syntax and operators for ergonomic optional value handling.

## Creating Optional Values

`Optional[T]` is constructed exclusively through `Some(value)` and `None()`:

```python
value: int? = Some(42)
empty: int? = None()
```

`Some(…)` and `None()` are the only spellings that work. The fully-qualified forms
`Optional.Some(…)` and `Optional.None()` are **not supported today** (#1758): `Optional.None()`
does not compile at all, and `Optional.Some(42)` is typed `Unknown`, so a mistyped destination
leaks a C# error instead of SPY0220. Use the bare constructors.

A bare value or bare `None` is **not** accepted for `T?` — use `Some(…)` or `None()`:

```python
# x: int? = 1       # SPY0604 — use Some(1)
# y: int? = None     # SPY0604 — use None(), or declare y: int | None
```

Bare `None` belongs to `T | None` (C# nullable interop) — see [Nullable Types](nullable_types.md). The `maybe` expression bridges a `T` value into `T?`: `z: int? = maybe x` wraps `x` in `Some` when non-null.

The one place a bare payload value is accepted is **inside a narrowing** of the Optional name: the
store is classified against the payload first, re-wraps, and the narrowing survives (the payload
rule — see [Stores Use the Declared Type](type_narrowing.md#stores-use-the-declared-type)):

```python
def main() -> None:
    x: int? = Some(10)
    if x is not None:
        x += 5          # payload store — x re-wraps as Some(15) and stays narrowed
        n: int = x      # the narrowing survives the store
        print(n)
        x = 7           # payload store — Some(7)
        print(x)
    print(x)
```

```
15
7
7
```

Outside a narrowing (`if True:`, a `while` body, a nested `def`) the declared slot decides and a bare
payload is SPY0604 as above.

## Pattern Matching

Use pattern matching to handle both Some and None cases:

```python
class User:
    name: str

    def __init__(self, name: str):
        self.name = name

def find_user(user_id: int, database: dict[int, User]) -> User?:
    if user_id in database:
        return Some(database[user_id])
    return None()

def main():
    database: dict[int, User] = {123: User("ada")}
    match find_user(123, database):
        case Some(user):
            print(f"Found user: {user.name}")     # Found user: ada
        case None:
            print("User not found")
    match find_user(999, database):
        case Some(user):
            print(f"Found user: {user.name}")
        case None:
            print("User not found")               # User not found
```

Both returns construct the Optional: `Some(user)` for the present case and `None()` for the empty
one. A bare `return None` here is **SPY0604** — `None` belongs to `User | None`, not to `User?`
(see [Creating Optional Values](#creating-optional-values)). `case None:` in the match is a
*pattern*, not the `None` value, and is unaffected.

### Matching the payload by type

A bare **type pattern** naming the payload type is an equivalent spelling of the `Some` case, so a
`T?` can be matched the way Python matches `T | None`:

```python
def describe(x: str?) -> str:
    match x:
        case str():
            return "a string"
        case None:
            return "nothing"
```

`case str():` matches only when the Optional holds a value, and a `None` value falls through to the
next arm — `case None:` here, or a wildcard. The two spellings are interchangeable: `case str():` is
`case Some(str())`, and `case str() as s:` binds `s` at the **payload** type, not at `str?`.

The pattern may name a subtype of the payload, in which case it still discriminates:

```python
def speak(a: Animal?) -> str:
    match a:
        case Dog() as d:
            return d.bark()
        case _:
            return "not a dog"
```

Exhaustiveness counts a payload type pattern as the `Some` case, so `case str():` paired with
`case None:` is exhaustive and needs no wildcard. `case str():` alone reports `None` as the missing
case — a warning for a match statement, an error for a match expression.

The scrutinee is **not** unwrapped to make this work: `unwrap()` throws on an empty Optional, so an
unwrapped subject could never reach a `None` arm. Both spellings destructure the Optional in place.

## Common Methods

The `Optional` type provides several useful methods:

```python
union Optional[T]:
    case Some(value: T)
    case None()

    @property
    def is_some(self) -> bool:
        """Returns True if the optional contains a value"""
        match self:
            case Some():
                return True
            case None:
                return False

    @property
    def is_none(self) -> bool:
        """Returns True if the optional is empty"""
        return not self.is_some

    def unwrap(self) -> T:
        """Returns the value or raises an exception"""
        match self:
            case Some(value):
                return value
            case None:
                raise Exception("Called unwrap on empty Optional")

    def unwrap_or(self, default: T) -> T:
        """Returns the value or the default"""
        match self:
            case Some(value):
                return value
            case None:
                return default

    def unwrap_or_else(self, f: () -> T) -> T:
        """Returns the value or calls f"""
        match self:
            case Some(value):
                return value
            case None:
                return f()

    def map(self, f: (T) -> U) -> U?:
        """Transforms the contained value if present"""
        match self:
            case Some(value):
                return Some(f(value))
            case None:
                return None()
```

## Constructor Shorthand

When the expected type is known, `Some(value)` and `None()` infer the full
`Optional<T>` type from context — no qualification needed:

```python
# With type annotation
x: int? = Some(42)
y: int? = None()

# Function return
def get_value() -> int?:
    return Some(42)

# Default parameter
def foo(x: int? = None()) -> None:
    pass

# Without type context - error (type cannot be inferred)
x = Some(42)   # Error: Cannot infer type for 'Some()'
```

The shorthand is equivalent to calling `Optional<T>.Some(value)` or
`Optional<T>.None()`.

## Protocol Operations and Member Access

`T?` (`Optional[T]`) is **strict**: its safety guarantee is the whole point of the
type, so the underlying value is never reached implicitly. Protocol operations
(`len()`, the `in` membership test, indexing `x[i]`, and iteration
`for v in x`) and direct member/method access on the underlying type are
**compile errors** on a `T?` receiver:

```python
s: str? = get_name()
print(len(s))    # error: Optional type 'str?' does not support len() directly
print(s[0])      # error: Optional type 'str?' does not support indexing directly
print(s.upper()) # error: 'str?' has no member 'upper'
```

Reach the underlying value explicitly — narrow it, use `?.`, pattern-match, or
unwrap it:

```python
# Narrow with `is not None` (refines T? to T in the branch)
if s is not None:
    print(len(s))
    print(s.upper())

# Null-conditional access (?.)
upper: str? = s?.upper()

# Pattern matching (`case str():` is the equivalent bare-payload spelling of `case Some(v):`)
match s:
    case Some(v):
        print(len(v))
    case None:
        print("empty")

# Unwrap (throws on None)
print(len(s.unwrap()))
print(s.unwrap_or("default"))
```

The only members callable directly on a `T?` are `Optional`'s own API
(`unwrap`, `unwrap_or`, `unwrap_or_else`, `map`, `is_some`, `is_none`).

### Passing a `T?` is the same rule

Handing a `T?` to something that expects a `T` is a use of the underlying value,
so it is refused for the same reason `len(s)` is — at argument binding, at an
operator's operand, and at any other position typed `T`. The reverse direction
is free: `Optional[T]` has an implicit conversion **from** `T`, so a plain value
goes into a `T?` parameter unchanged.

```python
def total(xs: list[int]) -> int:
    return len(xs)

ys: list[int]? = get_items()

total(ys)          # error: Cannot pass argument of type 'list[int]?' to parameter
                   # of type 'list[int]' — the argument is Optional[list[int]];
                   # narrow it ('if x is not None:') or unwrap it first
[1, 2] + ys        # error: Type 'list[int]' does not support operator '+' with
                   # operand of type 'list[int]?'

if ys is not None: # narrowed to list[int] in the branch
    total(ys)      # OK
total(ys.unwrap_or([]))  # OK

def describe(v: int?) -> str: ...
describe(7)        # OK — T into T? is the conversion Optional[T] declares
```

A `T?` is not a `T | None` either, even though both spell as `T?` at a use site:
they are different types, and neither converts to the other implicitly. Use
`maybe` to go from `T | None` to `T?`, and narrow or unwrap to go the other way.

### `T | None` is loose

By contrast, `T | None` (the C# nullable interop type — see
[Nullable Types](nullable_types.md)) is **loose**: protocol operations and
member access work directly on the underlying type, and a `None`/`null`
receiver fails at runtime (a `NullReferenceException`, mirroring Python's
`TypeError` / `AttributeError` on `None`). This matches .NET interop semantics,
where nullable references flow into ordinary member access:

```python
s: str | None = dotnet_api()
print(len(s))      # works; throws at runtime if s is None
print(s.upper())   # works; throws at runtime if s is None
```

Choose `T?` when you want the compiler to force you to handle absence; choose
`T | None` at .NET interop boundaries where Python-parity runtime semantics are
acceptable.

## Comparison: `T?` (Optional) vs `T | None` (C# Nullable)

| Feature | `T?` / `Optional[T]` | `T \| None` (C# Nullable) |
|---------|----------------------|---------------------------|
| Meaning | Safe tagged union | C# nullable reference/value |
| Has value | `Some(value)` | `value` |
| No value | `None` or `None()` | `None` |
| Type safety | Works with any `T` | Only reference types and `Nullable<T>` |
| Pattern matching | `case Some(v):` | `if x is not None:` |
| Protocol ops (`len`, `in`, `[i]`, iteration) | Compile error — narrow or `unwrap()` first | Allowed — throws at runtime on `None` |
| Member access (`x.method()`) | Compile error — only the Optional API (`unwrap`, `map`, …) | Allowed — throws at runtime on `None` |
| Heap allocation | **No** (struct) | No |
| Use case | Sharpy-native optionals | .NET interop boundaries |
| Interop | May need conversion | Direct .NET interop |

### When to Use `T?` (Optional)

- You're writing Sharpy-native code
- You want explicit, type-safe optional semantics
- You're working with value types that need to be optional
- You prefer functional programming patterns (map, flatMap, etc.)
- You want to make optionality more explicit in the type system

### When to Use `T | None` (C# Nullable)

- You're interfacing with .NET APIs that use null
- You're at a .NET interop boundary
- You want direct C# interop without conversions

See [Nullable Types](nullable_types.md) for details on `T | None`.

## Examples

### Safe Dictionary Access

```python
def get_config_value(config: dict[str, str], key: str) -> str?:
    if key in config:
        return Some(config[key])
    return None

# Using the result
value = get_config_value(config, "timeout")
match value:
    case Some(v):
        timeout = int(v)
    case None:
        timeout = 30  # default
```

### Chaining Optional Operations

```python
def get_user_city(user_id: int) -> str?:
    user = find_user(user_id)
    if user.is_none:
        return None

    address = user.unwrap().get_address()
    if address.is_none:
        return None

    return Some(address.unwrap().city)
```

### Transforming Optional Values

```python
# Using map to transform the value if present
opt_number: int? = Some(42)
opt_string = opt_number.map(lambda x: f"The answer is {x}")
# Result: Some("The answer is 42")

opt_nothing: int? = None()
opt_result = opt_nothing.map(lambda x: x * 2)
# Result: None
```

## Converting Between Optional and C# Nullable

Use `maybe` to convert from `T | None` (C# nullable) to `T?` (Optional):

```python
# C# nullable to Optional (use maybe)
raw: str | None = dotnet_api()
safe: str? = maybe raw              # Convert to Optional[str]

# Optional to C# nullable
def optional_to_nullable(opt: T?) -> T | None:
    match opt:
        case Some(value):
            return value
        case None:
            return None
```

See [Maybe Expressions](maybe_expressions.md) for details on the `maybe` keyword.

*Implementation*
- *✅ Implemented — `Optional[T]` is a struct-based tagged union in Sharpy.Core. Pattern matching with `Some`/`None`, the [`?` early-return operator](question_mark_operator.md), and `maybe` expressions are all supported.*

## Implementation Details

`Optional[T]` is implemented as a C# `readonly struct` in `Sharpy.Core`:

```csharp
public readonly struct Optional<T>
{
    // Two fields: the value and a hasValue flag
    // Zero heap allocation
}
```

The static helpers `Some(value)` and `None()` are available at module scope
for convenient construction.

## Must-Use Warning (SPY0480)

An `Optional[T]` (`T?`) produced as a bare expression statement and thrown away triggers the **must-use** warning `SPY0480` — discarding an `Optional` usually means an absent value is being silently ignored:

```python
def find(key: str) -> int?:
    ...

def main() -> None:
    find("x")          # ⚠ SPY0480: result of type 'int?' is silently discarded
```

Bind the value, propagate it with `?`, or discard it explicitly with `_ = find("x")`. Note this applies to the strict `Optional[T]` (`T?`) carrier — the loose `T | None` [nullable type](nullable_types.md) used for .NET interop is *not* flagged. The warning is scoped-suppressible with [`@suppress("SPY0480")`](diagnostic_suppression.md).

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Result Type](tagged_unions_result.md) - The Result type for error handling
- [Question-Mark Operator](question_mark_operator.md) - The `?` early-return operator for propagating `None`
- [Maybe Expressions](maybe_expressions.md) - Converting `T | None` to `T?`
- [Nullable Types](nullable_types.md) - `T | None` syntax for .NET interop
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Pattern Matching](match_statement.md) - Pattern matching syntax
