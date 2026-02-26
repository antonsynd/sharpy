# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T02:09:04.848989
**Type:** compilation_failed
**Feature Focus:** type_alias
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex type alias test covering chained, generic, and function type aliases
# Simple type aliases (chained)
type Scalar = float
type Magnitude = Scalar

# Function type alias
type BinaryOp = (float, float) -> float
type Predicate = (int) -> bool

# Named tuple type alias
type Coordinate = tuple[x: float, y: float]

# Generic collection aliases
type IntVector = list[int]
type FloatDict = dict[str, float]

# Calculator class using function type aliases
class Calculator:
    operation: BinaryOp

    def __init__(self, op: BinaryOp):
        self.operation = op

    def compute(self, a: float, b: float) -> float:
        return self.operation(a, b)

# Filter utility using predicate type
class NumberFilter:
    data: IntVector

    def __init__(self, values: IntVector):
        self.data = values

    def apply_filter(self, pred: Predicate) -> IntVector:
        result: IntVector = []
        for v in self.data:
            if pred(v):
                result.append(v)
        return result

# Optional coordinate handling using direct Optional[Coordinate]
class PathTracker:
    current: Optional[Coordinate]

    def __init__(self, coord: Optional[Coordinate] = None()):
        self.current = coord

    def get_x_safe(self) -> float:
        if self.current is not None:
            return self.current.x
        return 0.0

    def has_coord(self) -> bool:
        return self.current is not None

def is_even(n: int) -> bool:
    return n % 2 == 0

def main():
    # Test chained type aliases
    mag: Magnitude = 5.5
    print(mag)

    # Test named tuple with type alias
    coord: Coordinate = (x=3.0, y=4.0)
    print(coord.x)
    print(coord.y)

    # Test function type alias in class
    calc = Calculator(lambda a, b: (a + b) * 2.0)
    print(calc.compute(2.0, 3.0))

    # Test collection type aliases
    nums: IntVector = [1, 2, 3, 4, 5, 6]
    flt: NumberFilter = NumberFilter(nums)
    evens: IntVector = flt.apply_filter(is_even)
    print(len(evens))
    print(evens[0])
    print(evens[1])

    # Test float dictionary - construct empty first then assign
    scores: FloatDict = {}
    scores["a"] = 1.5
    scores["b"] = 2.5
    print(scores["a"])

    # Test optional type with named tuple
    tracker: PathTracker = PathTracker(None())
    print(tracker.get_x_safe())

    # Set valid coordinate
    tracker.current = Some(coord)
    print(tracker.get_x_safe())
    print(tracker.has_coord())
```

## Error

```
Assembly compilation failed:

error[CS1736]: Default parameter value for 'coord' must be a compile-time constant
  --> /tmp/tmpclnitj3e/dogfood_test.spy:57:67
    |
 57 |     return n % 2 == 0
    |                      ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpclnitj3e/dogfood_test.cs

```

## Timing

- Generation: 844.89s
- Execution: 4.40s
