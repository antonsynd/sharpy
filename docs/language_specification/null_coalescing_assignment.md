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

The dictionary value type must be nullable or Optional (see [Type Requirements](#type-requirements)).
A subscript `??=` on a *missing* key raises `KeyError` — the dictionary subscript operator reads the
current value first, and a missing key is an error, not an absent value:

```python
cache: dict[str, Data | None] = {}
cache["timeout"] = None
cache["timeout"] ??= compute_default()  # OK — key exists, value is None

try:
    cache["missing"] ??= compute_default()  # KeyError — the key does not exist
except KeyError:
    print("missing key")
```

To set a default for a missing key, use `dict.setdefault()` or guard with `in`:

```python
settings: dict[str, int | None] = {}
if "timeout" not in settings:
    settings["timeout"] = 30
```

## Statement Only

`??=` is a statement — it cannot appear inside a parenthesized expression or any other
expression context. Using it as an expression produces `SPY0146` with a steer toward `:=`
(walrus operator) for inline assignment:

```python
# ✅ Valid — statement form
name: str | None = None
name ??= "Default"

# ❌ Invalid — augmented assignments are not expressions (SPY0146)
# result = (name ??= "Default")
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

**Caching (key known to exist):**
```python
class Repository:
    _data_cache: dict[int, Data | None] = {}

    def get(self, id: int) -> Data:
        # Key must already exist; ??= fills a None value, not a missing key
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

The right-hand side is only evaluated if the left-hand side is absent.
When the value is already present, the store (setter, indexer write) is skipped entirely:

```python
x: int | None = 42
x ??= expensive_computation()  # expensive_computation() NOT called, setter NOT called

y: int | None = None
y ??= expensive_computation()  # expensive_computation() IS called, setter IS called
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

The `Optional[T]` tagged union (written as `T?`) works with null-coalescing assignment. The RHS
is a **store into the left slot**: both a bare payload value and an `Optional` value are accepted,
and a constant converts to the payload width:

```python
x: int? = None()
x ??= 42          # accepted — wraps as Some(42)
x ??= Some(42)    # also accepted — already an Optional

y: int8? = None()
y ??= 7            # accepted — the constant 7 converts to int8
```

Cross-family stores are refused with a steer:

```python
a: int? = None()
b: int | None = None
# a ??= b    # SPY0220 — use `maybe b` to wrap
# b ??= a    # SPY0220 — unwrap with `.unwrap()` or `match`
```

`None` and `None()` as the RHS keep today's refusals (SPY0222 / SPY0244): assigning emptiness
to an already-empty slot is a no-op and likely a mistake.

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
- *✅ Native — For `T | None` (C# nullable): `target ??= value;` (C# native, setter skipped when not null).*
- *✅ Lowered — For `T?` (`Optional[T]`): `if (!target.IsSome) target = value;` (setter skipped when present).*
