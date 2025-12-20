# Named Tuples

Named tuples provide field names for tuple elements, improving code readability while maintaining tuple performance and immutability.

## Type Declaration

```python
# Define a named tuple type alias
type Point = tuple[x: double, y: double]
type Coordinate = tuple[x: double, y: double, z: double]
type Bounds = tuple[min: int, max: int]
```

## Creating Named Tuples

```python
# Using named fields
pos: Point = (x=1.0, y=2.0)
bounds: Bounds = (min=0, max=100)

# Can mix named and positional (positional must come first)
coord: Coordinate = (1.0, 2.0, z=3.0)

# All positional (when type is known)
p: Point = (1.0, 2.0)  # Fields are named based on type
```

## Accessing Fields

```python
type Point = tuple[x: double, y: double]
pos: Point = (x=1.0, y=2.0)

# Access by name
print(pos.x)  # 1.0
print(pos.y)  # 2.0

# Access by position (0-indexed)
print(pos[0])  # 1.0
print(pos[1])  # 2.0
```

## Unpacking

Named tuples support standard tuple unpacking:

```python
type Point = tuple[x: double, y: double]
pos: Point = (x=3.0, y=4.0)

# Positional unpacking
x, y = pos
print(x, y)  # 3.0 4.0

# With wildcards
first, _ = pos  # Ignore second element
```

## Function Return Types

```python
# Function returning named tuple
def get_bounds() -> tuple[min: int, max: int]:
    return (min=0, max=100)

# Usage
bounds = get_bounds()
print(bounds.min)   # 0
print(bounds.max)   # 100

# Unpacking return value
min_val, max_val = get_bounds()
```

## Function Parameters

```python
type Point = tuple[x: double, y: double]

def distance(p1: Point, p2: Point) -> double:
    dx = p2.x - p1.x
    dy = p2.y - p1.y
    return (dx * dx + dy * dy) ** 0.5

result = distance((x=0.0, y=0.0), (x=3.0, y=4.0))  # 5.0
```

## Anonymous Named Tuples

Named tuple types can be used inline without a type alias:

```python
# Inline named tuple type
def parse_config() -> tuple[host: str, port: int, ssl: bool]:
    return (host="localhost", port=8080, ssl=True)

config = parse_config()
print(f"{config.host}:{config.port}")  # localhost:8080
```

## Pattern Matching

Named tuples work with pattern matching:

```python
type Point = tuple[x: double, y: double]

def describe(point: Point) -> str:
    match point:
        case (x=0.0, y=0.0):
            return "Origin"
        case (x=0.0, y=_):
            return "On Y-axis"
        case (x=_, y=0.0):
            return "On X-axis"
        case (x=x, y=y):
            return f"Point at ({x}, {y})"

# Positional patterns also work
match point:
    case (0.0, 0.0):
        print("Origin")
    case (x, y):
        print(f"Point at ({x}, {y})")
```

## Comparison and Equality

Named tuples compare by value, element by element:

```python
type Point = tuple[x: double, y: double]

p1: Point = (x=1.0, y=2.0)
p2: Point = (x=1.0, y=2.0)
p3: Point = (1.0, 2.0)  # Positional construction

print(p1 == p2)  # True
print(p1 == p3)  # True - same values
print(p1 is p2)  # False - different instances
```

## Immutability

Like regular tuples, named tuples are immutable:

```python
type Point = tuple[x: double, y: double]
pos: Point = (x=1.0, y=2.0)

# ❌ Cannot modify fields
pos.x = 3.0  # ERROR: cannot assign to immutable field

# ✅ Create new tuple with modified values
new_pos: Point = (x=3.0, y=pos.y)
```

## C# Mapping

Named tuples map directly to C# `ValueTuple` with named elements:

```python
# Sharpy
type Point = tuple[x: double, y: double]
pos: Point = (x=1.0, y=2.0)
print(pos.x)
```
```csharp
// C# 9.0
(double x, double y) pos = (x: 1.0, y: 2.0);
Console.WriteLine(pos.x);

// Or with type alias
using Point = (double x, double y);
Point pos = (1.0, 2.0);
```

## Type Compatibility

Named tuples with the same field names and types in the same order are compatible:

```python
type Point2D = tuple[x: double, y: double]
type Coordinate2D = tuple[x: double, y: double]

p: Point2D = (x=1.0, y=2.0)
c: Coordinate2D = p  # OK - same structure

# Different names or order - not compatible
type Position = tuple[x: double, y: double]
type Size = tuple[width: double, height: double]

pos: Position = (x=1.0, y=2.0)
size: Size = pos  # ERROR - different field names
```

## Nested Named Tuples

```python
type Point = tuple[x: double, y: double]
type Line = tuple[start: Point, end: Point]

line: Line = (
    start=(x=0.0, y=0.0),
    end=(x=3.0, y=4.0)
)

print(line.start.x)  # 0.0
print(line.end.y)    # 4.0
```

## Common Patterns

**Configuration Objects:**
```python
type Config = tuple[host: str, port: int, timeout: int]

def load_config() -> Config:
    return (host="localhost", port=8080, timeout=30)
```

**Multiple Return Values:**
```python
def parse_header() -> tuple[status: int, message: str, timestamp: long]:
    return (status=200, message="OK", timestamp=get_timestamp())
```

**Coordinate Systems:**
```python
type Point2D = tuple[x: double, y: double]
type Point3D = tuple[x: double, y: double, z: double]
type RGB = tuple[r: byte, g: byte, b: byte]
```

## Limitations

- All fields must be named, or none (cannot partially name)
- Field names must be valid identifiers
- Cannot have default values (use structs for that)
- Cannot add methods (use classes/structs for that)

```python
# ❌ Cannot partially name fields
type Mixed = tuple[x: int, int]  # ERROR

# ❌ Cannot have default values
type Point = tuple[x: double = 0.0, y: double = 0.0]  # ERROR

# ✅ Use struct instead for defaults
struct Point:
    x: double = 0.0
    y: double = 0.0
```

*Implementation: ✅ Native - Maps to C# `ValueTuple` with named elements (`(Type1 Name1, Type2 Name2)`).*

---
