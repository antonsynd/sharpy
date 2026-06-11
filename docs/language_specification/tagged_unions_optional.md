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

```python
# Bare None (preferred)
value: int? = Some(42)
empty: int? = None

# None() (explicit alternative)
empty: int? = None()

# Fully qualified (equivalent)
value: Optional[int] = Optional.Some(42)
empty: Optional[int] = Optional.None()
```

Bare `None` and `None()` are interchangeable for `T?`. Bare `None` is the more ergonomic form. For `T | None` (C# nullable interop), only bare `None` is valid — see [Nullable Types](nullable_types.md).

## Pattern Matching

Use pattern matching to handle both Some and None cases:

```python
def find_user(id: int) -> User?:
    user = database.find(id)
    if user is not None:
        return Some(user)
    return None

result = find_user(123)
match result:
    case Some(user):
        print(f"Found user: {user.name}")
    case None:
        print("User not found")
```

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
                return None
```

## Constructor Shorthand

When the expected type is known, you can use `Some(value)` and `None`
without qualifying with the type name:

```python
# With type annotation - shorthand works
x: int? = Some(42)
y: int? = None

# Function return - shorthand works
def get_value() -> int?:
    return Some(42)

# Default parameter - shorthand works
def foo(x: int? = None) -> None:
    pass

# Without type context - error (type cannot be inferred)
x = Some(42)   # Error: Cannot infer type for 'Some()'
```

The compiler infers the full type from context. The shorthand is equivalent to
calling `Optional<T>.Some(value)` or using `Optional<T>.None()`.

## Protocol Operations on `T?`

An optional value supports the same protocol operations as its underlying type
`T` — `len()`, the `in` membership test, indexing (`x[i]`), iteration
(`for item in x`), and direct method/attribute access all work on a `T?`
receiver and implicitly unwrap it:

```python
s: str? = get_name()
print(len(s))        # length of the underlying string
print("a" in s)      # membership against the underlying string
print(s[0])          # first character
print(s.upper())     # method call on the underlying string

xs: list[int]? = get_items()
total = 0
for v in xs:         # iterates the underlying list
    total += v
```

The result type is inferred from the underlying type `T` (e.g. `s[0]` is `str`,
iterating `list[int]?` yields `int`), exactly as if the value were a plain `T`.

**Runtime semantics — throw on `None`.** Implicit unwrapping mirrors Python's
behavior on `None`: if the optional is empty, the operation raises at runtime
(`Optional.unwrap()` throws; a `T | None` C# nullable raises a
`NullReferenceException`), analogous to Python's
`TypeError: object of type 'NoneType' has no len()`. This is the "throw for
bugs" policy — reach for an empty optional and the program fails fast.

To avoid the throw, narrow first with `is not None` (which refines `T?` to `T`
in the branch) or handle both cases with pattern matching:

```python
if s is not None:
    print(len(s))    # narrowed to str — no implicit unwrap, no throw risk
```

The same implicit-unwrap rule applies to `T | None` (C# nullable) receivers.

## Comparison: `T?` (Optional) vs `T | None` (C# Nullable)

| Feature | `T?` / `Optional[T]` | `T \| None` (C# Nullable) |
|---------|----------------------|---------------------------|
| Meaning | Safe tagged union | C# nullable reference/value |
| Has value | `Some(value)` | `value` |
| No value | `None` or `None()` | `None` |
| Type safety | Works with any `T` | Only reference types and `Nullable<T>` |
| Pattern matching | `case Some(v):` | `if x is not None:` |
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

opt_nothing: int? = None
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
- *✅ Implemented — `Optional[T]` is a struct-based tagged union in Sharpy.Core. Pattern matching with `Some`/`None`, the `?` operator, and `maybe` expressions are all supported.*

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

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Result Type](tagged_unions_result.md) - The Result type for error handling
- [Maybe Expressions](maybe_expressions.md) - Converting `T | None` to `T?`
- [Nullable Types](nullable_types.md) - `T | None` syntax for .NET interop
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Pattern Matching](match_statement.md) - Pattern matching syntax
