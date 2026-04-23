# Structs

Structs are value types that do not support inheritance but can implement interfaces.

```python
struct Vector2:
    """A 2D vector value type."""

    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5

    def __add__(self, other: Vector2) -> Vector2:
        return Vector2(self.x + other.x, self.y + other.y)
```

**Struct Rules:**
- All fields must be declared at struct level
- If no constructor is defined, fields are zero-initialized (matching C# 9.0 struct semantics)
- Users can define additional constructors that initialize all or some fields
- When a constructor is defined, it must initialize all fields (C# requirement)
- Cannot inherit from other structs or classes
- Can implement interfaces (including interfaces with default methods)
- Value semantics: copied when assigned or passed

**Default Initialization:**

C# structs always have an implicit parameterless constructor that zero-initializes all fields. Sharpy structs inherit this behavior:

```python
struct Point:
    x: int
    y: int

# Using implicit parameterless constructor (zero-initialized)
p1 = Point()           # x = 0, y = 0

# Using explicit constructor
struct Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

**Structs and Interface Default Methods:**

Structs can implement interfaces that have default method implementations. However, be aware of boxing implications:

```python
interface IDescribable:
    def describe(self) -> str:
        return "An object"  # Default implementation

struct Point(IDescribable):
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    # Can override default, or use it as-is
    def describe(self) -> str:
        return f"Point({self.x}, {self.y})"

# Direct call - no boxing
p = Point(10, 20)
print(p.describe())  # "Point(10, 20)" - efficient

# Interface call - requires boxing (allocates)
d: IDescribable = p  # Boxing occurs here
print(d.describe())  # "Point(10, 20)" - works but allocates
```

**Performance Note:** When a struct is assigned to an interface variable or passed as an interface parameter, the struct is boxed (copied to the heap). For performance-critical code, prefer calling struct methods directly rather than through interface references.

## Default Field Values

Struct fields can have default values, enabling partial construction:

```python
struct Point:
    x: int
    y: int = 0

p1 = Point(1, 2)   # x = 1, y = 2
p2 = Point(3)      # x = 3, y = 0 (default)
```

All fields can have defaults:

```python
struct Config:
    width: int = 800
    height: int = 600
    fullscreen: bool = False

c1 = Config()                    # 800, 600, False
c2 = Config(1024, 768, True)     # 1024, 768, True
c3 = Config(1920)                # 1920, 600, False
```

**Ordering rule:** Once a field has a default value, all subsequent fields must also have defaults (same as function parameters):

```python
# OK: defaults at the end
struct Good:
    x: int
    y: int = 0

# ERROR: non-default field after field with default value
struct Bad:
    x: int = 0
    y: int         # Error: cannot follow a field with a default value
```

The compiler auto-generates a constructor with optional parameters for fields that have defaults.

**When to Use Structs:**
- Small data structures (typically < 16 bytes)
- Immutable value types (Vector2, Point, Color)
- Types that benefit from value semantics

## Value Semantics

Structs in Sharpy are **value types**, meaning they have fundamentally different behavior from classes (reference types):

### Copy-on-Assignment

Structs are **copied** when assigned to a new variable:

```python
struct Point:
    x: int
    y: int

p1 = Point(10, 20)
p2 = p1              # p2 is a COPY of p1

p2.x = 99            # Only p2.x changes
print(p1.x)          # Prints: 10 (p1 is unchanged)
print(p2.x)          # Prints: 99
```

This is different from classes, where assignment creates a new reference to the same object:

```python
class PointClass:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p1 = PointClass(10, 20)
p2 = p1              # p2 references the SAME object as p1

p2.x = 99            # Changes the shared object
print(p1.x)          # Prints: 99 (p1.x also changed!)
print(p2.x)          # Prints: 99
```

### Pass-by-Value

Structs are **copied** when passed to functions by default:

```python
struct Counter:
    count: int

def increment(c: Counter) -> None:
    c.count += 1

counter = Counter(10)
increment(counter)
print(counter.count)  # Prints: 10 (unchanged - function modified a copy)
```

### Inline Storage

Structs are stored **inline** wherever they are declared:

- In local variables → stored on the stack
- In class/struct fields → stored inline in the containing object (no separate heap allocation)
- In arrays → stored contiguously in memory (no indirection)

This provides excellent cache locality and performance, but can be inefficient for large structs.

### Avoiding Copies with Parameter Modifiers

For large structs or performance-critical code, use parameter modifiers to avoid expensive copies:

#### `in T` - Read-Only Reference

Pass a struct by reference without allowing modifications:

```python
struct LargeData:
    buffer: list[int]  # Assume this is large

    def process(self) -> int:
        return sum(self.buffer)

def analyze(data: in LargeData) -> int:
    # 'data' is passed by reference (no copy)
    # 'data' cannot be modified (read-only)
    return data.process()

large = LargeData([1, 2, 3, 4, 5])
result = analyze(large)  # No copy! Efficient.
```

**Use `in T` when:**
- You need to read the struct but not modify it
- The struct is large (> 16 bytes)
- Performance is critical

#### `ref T` — Mutable Reference

Pass a struct by reference and allow modifications:

```python
struct Counter:
    count: int

def increment(c: ref Counter) -> None:
    # 'c' is passed by reference
    # Changes to 'c' affect the original struct
    c.count += 1

counter = Counter(10)
increment(counter)
print(counter.count)  # Prints: 11 (modified!)
```

**Use `ref T` when:**
- You need to modify the caller's struct
- You want to avoid copies for large structs

#### `out T` - Output Parameter

Initialize a struct and return it via parameter:

```python
struct Point:
    x: int
    y: int

def try_parse_point(text: str, result: out Point) -> bool:
    # 'result' must be assigned before returning
    parts = text.split(',')
    if len(parts) != 2:
        result = Point(0, 0)
        return False

    result = Point(int(parts[0]), int(parts[1]))
    return True

point: Point
if try_parse_point("10,20", point):
    print(f"Parsed: ({point.x}, {point.y})")
```

**Use `out T` when:**
- Implementing try-parse patterns
- Returning multiple values (one via return, others via `out`)

### Performance Guidelines

| Struct Size | Assignment/Parameter Passing | Recommendation |
|-------------|------------------------------|----------------|
| ≤ 16 bytes  | Cheap to copy | Pass by value (default) is fine |
| > 16 bytes  | Expensive to copy | Use `in T` for read-only, `ref T` for mutation |
| Very large  | Very expensive | Consider using a class instead |

### Immutability Best Practice

For best performance and safety, prefer **immutable structs**:

```python
struct Vector2:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    # Return new instances instead of modifying
    def __add__(self, other: Vector2) -> Vector2:
        return Vector2(self.x + other.x, self.y + other.y)

    def scale(self, factor: float) -> Vector2:
        return Vector2(self.x * factor, self.y * factor)

# Immutable usage pattern
v1 = Vector2(1.0, 2.0)
v2 = Vector2(3.0, 4.0)
v3 = v1 + v2           # Creates new Vector2
v4 = v3.scale(2.0)     # Creates new Vector2
```

**Benefits of immutable structs:**
- Thread-safe by default (no shared mutable state)
- Easier to reason about (no hidden state changes)
- Can be safely passed by value without defensive copies

*Implementation*
- *✅ Native - Direct mapping to C# `struct`.*
- *✅ Native - `in T` maps to C# `in T` parameter modifier (space-separated syntax)*
- *✅ Native - `ref T` maps to C# `ref T` parameter modifier (space-separated syntax)*
- *✅ Native - `out T` maps to C# `out T` parameter modifier (space-separated syntax)*
