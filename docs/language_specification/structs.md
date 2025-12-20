# Structs

Structs are value types that do not support inheritance but can implement interfaces.

```python
struct Vector2:
    """A 2D vector value type."""

    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def magnitude(self) -> double:
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
    x: double
    y: double

    def __init__(self, x: double, y: double):
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

**When to Use Structs:**
- Small data structures (typically < 16 bytes)
- Immutable value types (Vector2, Point, Color)
- Types that benefit from value semantics

*Implementation: ✅ Native - Direct mapping to C# `struct`.*
