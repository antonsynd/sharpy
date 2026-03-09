# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T05:02:33.017168
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module features
# Complex imports from multiple modules

from shapes import IShape, Circle, Rectangle, Square
from utils import ShapeCalculator, for_each_shape, create_test_shapes
from config import ShapeType, Color, Point

def print_shape_info(shape: IShape) -> None:
    print(shape.get_name())
    print(shape.area())
    print(shape.perimeter())

def main():
    # Test enum from config module
    print(Color.RED)
    print(Color.GREEN.value)

    # Test struct from config module
    pt: Point = Point(3.0, 4.0)
    print(pt.x)
    print(pt.y)
    print(pt.distance_from_origin())

    # Test ShapeCalculator from utils (uses shapes internally)
    calc: ShapeCalculator = ShapeCalculator()
    shapes: list[IShape] = create_test_shapes()
    for shape in shapes:
        calc.add_shape(shape)

    # Print totals
    print(calc.total_area())
    print(calc.total_perimeter())

    # Test inheritance (Square extends Rectangle extends ShapeBase)
    sq: Square = Square(2.0)
    print(sq.area())
    print(sq.perimeter())
    print(sq.get_name())

    # Test shape type values
    print(ShapeType.CIRCLE.value)
    print(ShapeType.RECTANGLE)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
1
2
3.0
4.0
5.0
101.0617
43.41628
4.0
8.0
Square
1
Rectangle

```

### Actual
```
Red
2
3.0
4.0
5.0
103.10611
57.98226
4.0
8.0
Square
1
Rectangle
```

## Timing

- Generation: 138.83s
- Execution: 5.41s
