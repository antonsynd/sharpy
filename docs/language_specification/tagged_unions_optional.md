# Optional Type

The `Optional[T]` type is a special tagged union provided by the Sharpy standard library for representing values that may or may not be present. This is similar to Rust's `Option` type and provides an alternative to nullable types (`T?`).

## Definition

```python
union Optional[T]:
    case Some(value: T)
    case Nothing()  # Or simply: case Nothing
```

The `Optional` type is part of the standard library and provides special syntax and operators for ergonomic optional value handling.

## Creating Optional Values

```python
# Some case (value is present)
some_value: Optional[int] = Optional.Some(42)

# Nothing case (no value)
no_value: Optional[int] = Optional.Nothing
```

## Pattern Matching

Use pattern matching to handle both Some and Nothing cases:

```python
def find_user(id: int) -> Optional[User]:
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

    def map(self, f: (T) -> U) -> Optional[U]:
        """Transforms the contained value if present"""
        match self:
            case Some(value):
                return Some(f(value))
            case Nothing:
                return Nothing
```

## Comparison with Nullable Types

| Feature | `Optional[T]` | `T?` (Nullable) |
|---------|---------------|-----------------|
| Has value | `Optional.Some(value)` | `value` |
| No value | `Optional.Nothing` | `None` |
| Type safety | Works with any `T` | Only reference types and `Nullable<T>` |
| Pattern matching | `case Optional.Some(v):` | `if x is not None:` |
| Use case | Explicit optional semantics | Standard .NET nullable pattern |
| Interop | May need conversion | Direct .NET interop |

### When to Use Optional vs Nullable

**Use `Optional[T]` when:**
- You want explicit, type-safe optional semantics
- You're working with value types that need to be optional
- You prefer functional programming patterns (map, flatMap, etc.)
- You want to make optionality more explicit in the type system

**Use `T?` (nullable) when:**
- You're interfacing with .NET APIs that use null
- You want simpler syntax with `??` and `?.` operators
- You're following .NET conventions
- You want direct C# interop without conversions

## Examples

### Safe Dictionary Access

```python
def get_config_value(config: dict[str, str], key: str) -> Optional[str]:
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
def get_user_city(user_id: int) -> Optional[str]:
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
opt_number: Optional[int] = Some(42)
opt_string = opt_number.map(lambda x: f"The answer is {x}")
# Result: Optional.Some("The answer is 42")

opt_nothing: Optional[int] = Nothing
opt_result = opt_nothing.map(lambda x: x * 2)
# Result: Optional.Nothing
```

## Converting Between Optional and Nullable

```python
# Nullable to Optional
def nullable_to_optional(value: T?) -> Optional[T]:
    if value is not None:
        return Some(value)
    return Nothing

# Optional to Nullable
def optional_to_nullable(opt: Optional[T]) -> T?:
    match opt:
        case Some(value):
            return value
        case Nothing:
            return None
```

*Implementation*
- *🔄 Lowered - Abstract base class + sealed nested case classes (see [Tagged Unions](tagged_unions.md) for implementation details)*

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Result Type](tagged_unions_result.md) - The Result type for error handling
- [Maybe Expressions](maybe_expressions.md) - Special syntax for Optional types
- [Nullable Types](nullable_types.md) - Standard nullable type syntax with `?`
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator for nullable types
- [Pattern Matching](match_statement.md) - Pattern matching syntax
