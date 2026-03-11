# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T08:56:30.473612
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from geometry import Point, Color, Shape, make_point

class Circle(Shape):
    radius: float

    def __init__(self, pos: Point, r: float, c: Color):
        super().__init__(pos, c)
        self.radius = r

    @override
    def get_name(self) -> str:
        return "Circle"

    @override
    def calculate_area(self) -> float:
        return 3.14159 * self.radius * self.radius

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, pos: Point, w: float, h: float, c: Color):
        super().__init__(pos, c)
        self.width = w
        self.height = h

    @override
    def get_name(self) -> str:
        return "Rectangle"

    @override
    def calculate_area(self) -> float:
        return self.width * self.height

def main():
    p: Point = make_point(1.0, 2.0)
    c: Circle = Circle(p, 2.0, Color.RED)
    r: Rectangle = Rectangle(p, 3.0, 4.0, Color.BLUE)

    print(c.get_name())
    print(r.get_name())
    print(c.calculate_area())
    print(r.calculate_area())
    print(c.color.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Geometry.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Geometry.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpb6a5abxx/main.spy:44:47
    |
 44 |     print(c.color.name)
    |                        ^
    |


```

## Timing

- Generation: 436.50s
- Execution: 5.04s
