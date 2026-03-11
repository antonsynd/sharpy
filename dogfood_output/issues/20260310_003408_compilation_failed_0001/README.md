# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T00:24:05.714978
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from shapes import Shape
from utils import MathHelper

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, name: str, width: float, height: float):
        super().__init__(name)
        self.width = width
        self.height = height

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

    @override
    def describe(self) -> str:
        base = super().describe()
        return f"{base}, Rectangle {self.width}x{self.height}"

class Circle(Shape):
    radius: float

    def __init__(self, name: str, radius: float):
        super().__init__(name)
        self.radius = radius

    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

    @override
    def perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius

def main():
    rect = Rectangle("Rect1", 5.0, 3.0)
    circle = Circle("Circle1", 4.0)

    areas: list[float] = [rect.area(), circle.area()]
    perims: list[float] = [rect.perimeter(), circle.perimeter()]

    print(rect.describe())
    print(circle.describe())
    total_area = MathHelper.sum(areas)
    avg_area = MathHelper.average(areas)
    print(f"Total area: {total_area}")
    print(f"Average area: {avg_area}")
    print(f"Total perimeter: {MathHelper.sum(perims)}")

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpdhz8capk/shapes.spy:16:64
    |
 16 | 
    | ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpdhz8capk/shapes.spy:4:18
    |
  4 | class Rectangle(Shape):
    |                  ^
    |


```

## Timing

- Generation: 584.89s
- Execution: 5.08s
