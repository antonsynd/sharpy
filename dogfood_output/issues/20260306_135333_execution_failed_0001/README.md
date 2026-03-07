# Issue Report: execution_failed

**Timestamp:** 2026-03-06T13:48:04.090422
**Type:** execution_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point

from shapes import Rectangle, Circle
from utils import describe_shape, is_square, ShapeCollection, create_metrics

def main():
    # Create shapes from shapes module
    rect: Rectangle = Rectangle("R1", 5.0, 3.0)
    square: Rectangle = Rectangle("SQ", 4.0, 4.0)
    circle: Circle = Circle("C1", 3.0)

    # Test polymorphic __str__ (virtual dispatch)
    print(describe_shape(rect))
    print(describe_shape(circle))

    # Test ShapeMetrics from utils module working with shapes
    metrics = create_metrics(rect)
    print(str(metrics))

    # Test ShapeCollection from utils managing shapes from shapes module
    collection: ShapeCollection = ShapeCollection()
    collection.add(rect)
    collection.add(square)
    collection.add(circle)
    print(len(collection))
    print(collection.total_area())
    print(collection.total_perimeter())

    # Test utility function from utils that works with Rectangle
    print(is_square(square))

```

## Error

```
Unhandled exception. Sharpy.TypeError: object of type 'ShapeCollection' has no len()
   at Sharpy.Builtins.Len(Object obj)
   at Program.Main() in /tmp/tmpncm30dih/main.spy:line 25

```

## Compiler Output

```
Rectangle(R1: 5.0x3.0)
Circle(C1: r=3.0)
Metrics(area=15.0, p=16.0)

```

## Timing

- Generation: 300.48s
- Execution: 5.94s
