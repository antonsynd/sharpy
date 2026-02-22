# Issue Report: output_mismatch

**Timestamp:** 2026-02-21T03:48:53.392726
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point: Tests cross-module class usage with inheritance

from geometry import Point, Shape, origin
from shapes import Rectangle, Circle, create_unit_square

def print_shape_info(shape: Shape):
    desc: str = shape.describe()
    area: float = shape.area()
    print(desc)
    print(f"Area: {area}")

def main():
    # Test 1: Create and use Point from geometry module
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(f"Distance: {dist}")

    # Test 2: Rectangle from shapes module (inherits from geometry.Shape)
    rect: Rectangle = Rectangle(p1, 5.0, 3.0)
    print_shape_info(rect)

    # Test 3: Circle from shapes module
    circle: Circle = Circle(p2, 2.0)
    print_shape_info(circle)

    # Test 4: Use factory function from shapes module
    unit: Rectangle = create_unit_square()
    print(f"Square area: {unit.area()}")

# EXPECTED OUTPUT:
# Distance: 5.0
# Shape: Rectangle [5.0x3.0]
# Area: 15.0
# Shape: Circle
# Area: 12.56636
# Square area: 1.0
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Distance: 5.0
Shape: Rectangle [5.0x3.0]
Area: 15.0
Shape: Circle
Area: 12.56636
Square area: 1.0

```

### Actual
```
Distance: 5
Shape: Rectangle [5x3]
Area: 15
Shape: Circle
Area: 12.56636
Square area: 1
```

## Timing

- Generation: 517.70s
- Execution: 5.00s
