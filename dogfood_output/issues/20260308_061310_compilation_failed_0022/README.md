# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T06:10:34.538285
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - cross-module class testing

from shapes_base import Shape, IDrawable, Color
from shapes_derived import Rectangle, Circle
from shape_utils import Point, Dimensions, DEFAULT_ORIGIN

def process_shape(s: Shape) -> None:
    # Test virtual dispatch across modules
    print(s.describe())
    print(s.area())

def render_drawable(d: IDrawable) -> None:
    # Test interface dispatch
    print(d.draw())

def create_test_shapes() -> list[Shape]:
    shapes: list[Shape] = []
    
    # Create rectangle
    rect_origin: Point = Point(10.0, 20.0)
    rect_dims: Dimensions = Dimensions(5.0, 3.0)
    rect = Rectangle("MyRect", Color.RED, rect_origin, rect_dims)
    shapes.append(rect)
    
    # Create circle
    circle_center: Point = Point(50.0, 50.0)
    circle = Circle("MyCircle", Color.BLUE, circle_center, 10.0)
    shapes.append(circle)
    
    return shapes

def main():
    # Get shapes from derived module
    shapes: list[Shape] = create_test_shapes()
    
    # Test enum access across modules
    rect: Rectangle = Rectangle("TestRect", Color.GREEN, DEFAULT_ORIGIN, Dimensions(2.0, 4.0))
    print(rect.get_color_name())
    
    # Process each shape (tests virtual method dispatch)
    for s in shapes:
        process_shape(s)
    
    # Test interface-based polymorphism
    circle: Circle = Circle("BigCircle", Color.RED, Point(0.0, 0.0), 5.0)
    render_drawable(circle)
    
    # Test struct value semantics
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point(0.0, 0.0)
    dist: float = p1.distance_to(p2)
    print(dist)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'ShapesBase.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'ShapesBase.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmphkyufdoe/shapes_base.spy:29:31
    |
 29 |     
    |     ^
    |


```

## Timing

- Generation: 136.72s
- Execution: 4.91s
