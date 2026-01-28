# Optional Type

> **`T?` is syntactic sugar for `Optional[T]`.** The `T?` shorthand is the preferred way to express optional values in Sharpy-native code.

The `Optional[T]` type is a special tagged union provided by the Sharpy standard library for representing values that may or may not be present. This is similar to Rust's `Option` type.

`Optional[T]` is a **struct** — no heap allocation for returning optional values, just a bool + value (like `Nullable<T>` but with tagged union semantics).

## Definition

```python
union Optional[T]:
    case Some(value: T)
    case Nothing()  # Or simply: case Nothing
```

The `Optional` type is part of the standard library and provides special syntax and operators for ergonomic optional value handling.

## Creating Optional Values

```python
# Shorthand (preferred)
value: int? = Some(42)
empty: int? = Nothing

# Explicit (equivalent)
value: Optional[int] = Optional.Some(42)
empty: Optional[int] = Optional.Nothing
```

## Pattern Matching

Use pattern matching to handle both Some and Nothing cases:

```python
def find_user(id: int) -> User?:
    user = database.find(id)
    if user is not None:
        return Some(user)
    return Nothing

result = find_user(123)
match result:
    case Some(user):
        print(f"Found user: {user.name}")
    case Nothing:
        print("User not found")
```

## Common Methods

The `Optional` type provides several useful methods:

```python
union Optional[T]:
    case Some(value: T)
    case Nothing

    def is_some(self) -> bool:
        """Returns True if the optional contains a value"""
        match self:
            case Some():
                return True
            case Nothing:
                return False

    def is_nothing(self) -> bool:
        """Returns True if the optional is empty"""
        return not self.is_some()

    def unwrap(self) -> T:
        """Returns the value or raises an exception"""
        match self:
            case Some(value):
                return value
            case Nothing:
                raise Exception("Called unwrap on Nothing")

    def unwrap_or(self, default: T) -> T:
        """Returns the value or the default"""
        match self:
            case Some(value):
                return value
            case Nothing:
                return default

    def unwrap_or_else(self, f: () -> T) -> T:
        """Returns the value or calls f"""
        match self:
            case Some(value):
                return value
            case Nothing:
                return f()

    def map(self, f: (T) -> U) -> U?:
        """Transforms the contained value if present"""
        match self:
            case Some(value):
                return Some(f(value))
            case Nothing:
                return Nothing
```

## Comparison: `T?` (Optional) vs `T | None` (C# Nullable)

| Feature | `T?` / `Optional[T]` | `T \| None` (C# Nullable) |
|---------|----------------------|---------------------------|
| Meaning | Safe tagged union | C# nullable reference/value |
| Has value | `Some(value)` | `value` |
| No value | `Nothing` | `None` |
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
    return Nothing

# Using the result
value = get_config_value(config, "timeout")
match value:
    case Some(v):
        timeout = int(v)
    case Nothing:
        timeout = 30  # default
```

### Chaining Optional Operations

```python
def get_user_city(user_id: int) -> str?:
    user = find_user(user_id)
    if user.is_nothing():
        return Nothing

    address = user.unwrap().get_address()
    if address.is_nothing():
        return Nothing

    return Some(address.unwrap().city)
```

### Transforming Optional Values

```python
# Using map to transform the value if present
opt_number: int? = Some(42)
opt_string = opt_number.map(lambda x: f"The answer is {x}")
# Result: Some("The answer is 42")

opt_nothing: int? = Nothing
opt_result = opt_nothing.map(lambda x: x * 2)
# Result: Nothing
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
        case Nothing:
            return None
```

See [Maybe Expressions](maybe_expressions.md) for details on the `maybe` keyword.

*Implementation*
- *🔄 Lowered - Struct-based tagged union (no heap allocation). See [Tagged Unions](tagged_unions.md) for implementation details.*

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Result Type](tagged_unions_result.md) - The Result type for error handling
- [Maybe Expressions](maybe_expressions.md) - Converting `T | None` to `T?`
- [Nullable Types](nullable_types.md) - `T | None` syntax for .NET interop
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Pattern Matching](match_statement.md) - Pattern matching syntax
