# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T06:38:25.158298
**Type:** compilation_failed
**Feature Focus:** generator_iter_class
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex generator iteration with inheritance and property views

class SequenceView:
    _source: list[int]
    _offset: int
    _end: int

    def __init__(self, source: list[int], offset: int):
        self._source = source
        self._offset = offset
        self._end = len(source)

    property get offset(self) -> int:
        return self._offset

    def __iter__(self) -> int:
        i = self._offset
        while i < self._end:
            yield self._source[i]
            i += 1

    def __reversed__(self) -> int:
        i = self._end - 1
        while i >= self._offset:
            yield self._source[i]
            i -= 1

class Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

class ShapeCollection:
    _points: list[Point]
    _name: str

    def __init__(self, name: str):
        self._name = name
        self._points = []

    property get count(self) -> int:
        return len(self._points)

    property get name(self) -> str:
        return self._name

    def add_point(self, x: float, y: float) -> None:
        pt: Point = Point(x, y)
        self._points.append(pt)

    def __iter__(self) -> Point:
        i = 0
        while i < len(self._points):
            yield self._points[i]
            i += 1

    def __reversed__(self) -> Point:
        i = len(self._points) - 1
        while i >= 0:
            yield self._points[i]
            i -= 1

class Polygon(ShapeCollection):
    property get perimeter(self) -> float:
        total = 0.0
        count = len(self._points)
        if count < 2:
            return total
        i = 0
        while i < count:
            p1 = self._points[i]
            next_idx = (i + 1) % count
            p2 = self._points[next_idx]
            dx = p2.x - p1.x
            dy = p2.y - p1.y
            dist = (dx * dx + dy * dy) ** 0.5
            total = total + dist
            i += 1
        return total

    def __iter__(self) -> Point:
        return super().__iter__()

    def __reversed__(self) -> Point:
        return super().__reversed__()

def sum_view(view: SequenceView) -> int:
    total = 0
    for val in view:
        total = total + val
    return total

def main():
    # Test SequenceView with generators
    nums: list[int] = [10, 20, 30, 40, 50]
    view = SequenceView(nums, 1)
    print(view.offset)

    # Forward iteration
    for v in view:
        print(v)

    # Reverse iteration
    for v in reversed(view):
        print(v)

    # Test sum_view helper using generators
    print(sum_view(view))

    # Test Polygon with inherited generators
    poly = Polygon("triangle")
    poly.add_point(0.0, 0.0)
    poly.add_point(3.0, 0.0)
    poly.add_point(0.0, 4.0)
    poly.add_point(0.0, 0.0)

    print(poly.count)

    # Forward iteration via generator
    for pt in poly:
        print(pt.x)

    # Reverse iteration
    count = 0
    for pt in reversed(poly):
        count = count + 1
    print(count)

    # Generator in polygon subclass
    print(poly.perimeter)

```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'System.Collections.Generic.IEnumerator<DogfoodTest.Point>' to 'DogfoodTest.Point'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmpe8cri4je/dogfood_test.spy:85:20
    |
 85 |         return super().__iter__()
    |                    ^
    |

error[CS0117]: 'DogfoodTest.Point' does not contain a definition for 'Current'
  --> /tmp/tmpe8cri4je/dogfood_test.spy:123:37
     |
 123 |     for pt in poly:
     |                    ^
     |

error[CS0202]: foreach requires that the return type 'DogfoodTest.Point' of 'DogfoodTest.Polygon.GetEnumerator()' must have a suitable public 'MoveNext' method and public 'Current' property
  --> /tmp/tmpe8cri4je/dogfood_test.spy:123:37
     |
 123 |     for pt in poly:
     |                    ^
     |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpe8cri4je/dogfood_test.cs

```

## Timing

- Generation: 195.25s
- Execution: 4.87s
