# @dataclass

The `@dataclass` decorator automatically generates boilerplate methods for data-holding classes: constructor (`__init__`), equality (`__eq__`, `__hash__`), and string representation (`__repr__`).

## Basic Usage

```python
@dataclass
class Point:
    x: float
    y: float
```

This generates a class with:
- Auto-properties for each field
- A constructor accepting all fields as parameters
- `__eq__` (value equality via `Equals` and `operator ==`/`!=`)
- `__hash__` (via `HashCode.Combine`)
- `__repr__` (via `ToString()`)

**C# output:**

```csharp
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point(double x, double y)
    {
        this.X = x;
        this.Y = y;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Point other)
            return false;
        return Equals(X, other.X) && Equals(Y, other.Y);
    }

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Point? left, Point? right) => Equals(left, right);
    public static bool operator !=(Point? left, Point? right) => !Equals(left, right);

    public override string ToString() => $"Point(x={X}, y={Y})";
}
```

## Field Declarations

All fields must have explicit type annotations:

```python
@dataclass
class Config:
    name: str
    debug: bool = False
    retries: int = 3
```

**Rules:**
- Fields without default values must precede fields with default values (same as Python)
- Fields without type annotations are not recognized as dataclass fields
- Static fields (marked with `@static`) are excluded from dataclass synthesis

## Field Defaults

A field may declare a default value. A **compile-time-constant** default (a number, string,
`True`/`False`, an enum member, or a `const` whose own initializer is constant) is emitted as the
default value of the corresponding constructor parameter.

A **mutable-collection** default -- a `list`, `dict`, or `set` literal; a `list()`/`dict()`/`set()`
call; or a comprehension -- is initialized **per instance**. Each object gets its own fresh
collection, so mutating one instance's field never affects another, and an explicit argument still
wins:

```python
@dataclass
class Bag:
    label: str
    items: list[int] = [1]

def main() -> None:
    a = Bag("a")
    b = Bag("b")
    a.items.append(2)
    print(a.items)               # [1, 2]
    print(b.items)               # [1]  -- b is unaffected
    print(Bag("c", [9]).items)   # [9]  -- an explicit argument wins
```

This differs from Python, where a bare mutable default raises `ValueError` at class definition and
the idiom is `field(default_factory=...)`. Sharpy accepts the bare literal and gives it
`default_factory` semantics automatically.

**Generated .NET shape.** The synthesized constructor takes a nullable sentinel parameter and
builds a fresh collection when the caller omits the argument:

```csharp
public Bag(string label, Sharpy.List<int>? items = null)
{
    this.Label = label;
    this.Items = items ?? new Sharpy.List<int>() { 1 };
}
```

The per-instance value is assigned **before** `__post_init__` runs, so the hook observes the
default (matching Python's `default_factory`-then-`__post_init__` ordering):

```python
@dataclass
class Box:
    items: list[int] = [1]
    count: int = 0

    def __post_init__(self) -> None:
        self.count = len(self.items)

def main() -> None:
    print(Box().count)           # 1
```

**Nullable-typed fields keep the refusal.** A mutable-collection default on a field whose type is
itself nullable is refused with `SPY0400`: the nullable sentinel the per-instance lowering relies
on would be indistinguishable from a genuine `None`. Use `None` as the default and build the
collection in `__post_init__`:

<!-- spec-sweep: error SPY0400 -->
```python
@dataclass
class Bad:
    xs: list[int] | None = [1]   # error[SPY0400]: Mutable default value is not allowed
                                 # for field 'xs' in dataclass 'Bad'.
```

## Parameters

The `@dataclass` decorator accepts keyword arguments:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `frozen`  | `False` | If `True`, generates `{ get; init; }` properties instead of `{ get; set; }` |
| `eq`      | `True`  | If `True`, generates `Equals`, `GetHashCode`, `operator ==`, `operator !=` |
| `repr`    | `True`  | If `True`, generates `ToString()` |

```python
@dataclass(frozen=True)
class Coord:
    x: int
    y: int
```

With `frozen=True`, properties use `init` accessors:

```csharp
public int X { get; init; }
public int Y { get; init; }
```

## `__post_init__` Hook

Define `__post_init__` to run custom logic after the generated constructor:

```python
@dataclass
class Square:
    side: float
    area: float = 0.0

    def __post_init__(self):
        self.area = self.side * self.side
```

The generated constructor calls `PostInit()` after assigning all fields:

```csharp
public Square(double side, double area = 0.0d)
{
    this.Side = side;
    this.Area = area;
    PostInit();  // calls __post_init__
}
```

## Inheritance

Dataclass inheritance collects fields from parent classes (parent fields first):

```python
@dataclass
class Base:
    x: int

@dataclass
class Child(Base):
    y: int
```

The child constructor accepts all fields and delegates inherited fields via `base()`:

```csharp
public Child(int x, int y) : base(x)
{
    this.Y = y;
}
```

The generated `Equals`, `GetHashCode`, and `ToString` include all fields (inherited + own).

## Explicit Method Override

When a class has an explicit `__init__`, the dataclass skips constructor generation but still generates `__eq__`, `__hash__`, and `__repr__` (unless disabled via parameters):

```python
@dataclass
class Named:
    first: str
    last: str

    def __init__(self, full_name: str):
        parts = full_name.split(" ")
        self.first = parts[0]
        self.last = parts[1]
```

Similarly, explicitly defining `__eq__`, `__hash__`, or `__repr__` prevents the dataclass from generating those methods.

## Disabling Features

Use `eq=False` to prevent equality synthesis:

```python
@dataclass(eq=False)
class Item:
    name: str
    value: int = 0
```

This class gets a constructor and `ToString` but no `Equals`, `GetHashCode`, or `operator ==`/`!=`.

## Restrictions

- `@dataclass` can only be applied to classes (not structs or interfaces) -- error SPY0380
- Fields without defaults cannot follow fields with defaults -- error SPY0381
- All dataclass fields must have type annotations -- error SPY0382
- Decorator arguments must be boolean literals (`True`/`False`) for recognized options (`frozen`, `eq`, `repr`) -- error SPY0383
- A mutable-collection default on a **nullable-typed** field is refused -- error SPY0400 (see [Field Defaults](#field-defaults))
- A non-constant, non-mutable-collection default (a tuple literal, or an arbitrary function call) is refused -- error SPY0401
