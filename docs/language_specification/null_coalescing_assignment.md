# Null-Coalescing Assignment Operator

The null-coalescing assignment operator `??=` assigns a value to a variable only if the variable currently has no value. It works with both `T?` (`Optional[T]`) and `T | None` (C# nullable).

## Syntax

```python
variable ??= value
```

For `T | None` (C# nullable), equivalent to:
```python
if variable is None:
    variable = value
```

For `T?` (Optional), equivalent to:
```python
if variable.is_none:
    variable = Some(value)
```

## Basic Usage

```python
# With T? (Optional)
name: str? = None()
name ??= "Anonymous"  # name is now Some("Anonymous")

# With T | None (C# nullable)
name: str | None = None
name ??= "Anonymous"  # name is now "Anonymous"

# Does nothing if already has value
name = "Alice"
name ??= "Anonymous"  # name is still "Alice"
```

## Lazy Initialization

```python
# Singleton pattern (using T | None for interop-style lazy init)
_instance: MyService | None = None

def get_service() -> MyService:
    _instance ??= MyService()  # Create only if None
    return _instance

# Lazy property
class DataManager:
    _cache: dict[str, Data] | None = None

    def get_cache(self) -> dict[str, Data]:
        self._cache ??= {}  # Initialize on first access
        return self._cache
```

## Dictionary Operations

```python
cache: dict[str, Data] = {}

def get_or_create(key: str) -> Data:
    # Compute only if key is missing or maps to None
    cache[key] ??= compute_expensive_data(key)
    return cache[key]

# Works with dictionary subscript
settings: dict[str, int] = {}
settings["timeout"] ??= 30  # Set default if not present
```

## Return Value

The `??=` operator returns the final value (either existing or newly assigned):

```python
# Can be used in expressions
name: str | None = None
result = (name ??= "Default")  # result is "Default", name is "Default"

# Chaining assignments
a: int | None = None
b: int | None = None
c = (a ??= (b ??= 42))  # c, a, and b are all 42
```

## Type Requirements

The variable must have an optional type (`T?`) or a nullable type (`T | None`):

```python
# ✅ Valid - Optional type (T?)
x: int? = None()
x ??= 10  # OK

# ✅ Valid - C# nullable type (T | None)
x: int | None = None
x ??= 10  # OK

# ❌ Invalid - non-nullable type
y: int = 5
y ??= 10  # ERROR: y is not nullable or optional

# ✅ Valid - dictionary value might be None
cache: dict[str, Data | None] = {}
cache["key"] ??= Data()
```

## Comparison with Other Operators

| Operator | Condition | Effect |
|----------|-----------|--------|
| `??=` | If absent (`None` or `None()`) | Assign new value |
| `=` | Always | Assign new value |
| `??` | N/A | Return non-None value (doesn't assign) |

```python
# ??= checks for absence (None or None())
x: int | None = 0
x ??= 5      # x is still 0 (not None)

# ?? doesn't assign
x: int | None = None
y = x ?? 5   # y is 5, but x is still None
```

## Common Patterns

**Configuration Defaults:**
```python
class Config:
    host: str | None = None
    port: int | None = None

    def ensure_defaults(self) -> None:
        self.host ??= "localhost"
        self.port ??= 8080
```

**Caching:**
```python
class Repository:
    _data_cache: dict[int, Data] = {}

    def get(self, id: int) -> Data:
        self._data_cache[id] ??= fetch_from_db(id)
        return self._data_cache[id]
```

**Lazy Loading:**
```python
class HeavyResource:
    _connection: Connection | None = None

    def get_connection(self) -> Connection:
        self._connection ??= establish_connection()
        return self._connection
```

**Default Arguments in Functions:**
```python
def process(data: list[str] | None, options: Options | None) -> None:
    data ??= []  # Use empty list if None
    options ??= Options.default()
    # ... process with guaranteed non-None values
```

## Short-Circuit Evaluation

The right-hand side is only evaluated if the left-hand side is absent:

```python
x: int | None = 42
x ??= expensive_computation()  # expensive_computation() NOT called

y: int | None = None
y ??= expensive_computation()  # expensive_computation() IS called
```

## Chaining

Can be chained for fallback chains:

```python
# Try multiple sources
value: int | None = None
value ??= get_from_cache()
value ??= get_from_db()
value ??= get_default()
# value is the first non-None result
```

## C# Mapping

Maps directly to C# `??=` operator:

```python
# Sharpy
cache: dict[str, Data] = {}
cache[key] ??= compute_data(key)
```
```csharp
// C# 9.0
var cache = new Dictionary<string, Data>();
cache[key] ??= ComputeData(key);
```

## Atomic Guarantee

Like C#, the assignment is **not** atomic for reference types. For thread-safe initialization, use proper synchronization:

```python
# ❌ Not thread-safe
_instance: MyService | None = None
_instance ??= MyService()  # Race condition possible

# ✅ Thread-safe with lock
_instance: MyService | None = None
_lock: object = object()

def get_instance() -> MyService:
    if _instance is None:
        with lock(_lock):
            _instance ??= MyService()
    return _instance
```

## Precedence

Lower precedence than most operators, higher than compound assignments:

```python
# Arithmetic before ??=
x: int | None = None
x ??= 5 + 3  # Equivalent to: x ??= (5 + 3)

# Lower than null-coalescing operator ??
x: int | None = None
x ??= y ?? 5  # Equivalent to: x ??= (y ?? 5)
```

## Optional (Tagged Union)

The `Optional[T]` tagged union (written as `T?`) works with null-coalescing assignment, with its empty case (`None()`) being treated similarly to bare `None`:

```python
maybe_str: str? = Some("HELLO")
maybe_str ??= Some("hello")  # maybe_str is still Some("HELLO")

maybe_str = None()
maybe_str ??= Some("hello")  # maybe_str is now Some("hello")
```

In this situation, the return type is `T?` where `T` is the expected type of the entire expression if it had evaluated.

## Limitations

- Cannot use with non-nullable types or non-Optional types
- Left side must be assignable (variable, field, property, or indexer)
- Not atomic for concurrent access

```python
# ❌ Cannot use with constants
const DEFAULT: int | None = None
DEFAULT ??= 10  # ERROR: cannot assign to constant

# ❌ Cannot use with function call results
get_value() ??= 10  # ERROR: cannot assign to function result

# ✅ Assign to variable first
value = get_value()
value ??= 10
```

*Implementation*
- *✅ Native - For `T | None` (C# nullable), maps directly to C# `??=` operator.*
- *🔄 Lowered - For `T?` (`Optional[T]`), compiler generates `if`/`else` assignment.*
