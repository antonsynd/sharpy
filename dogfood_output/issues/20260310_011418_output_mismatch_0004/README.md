# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T01:08:05.344364
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage, inheritance, and polymorphism
from shapes import Shape, Color, IColorable
from geometry import Rectangle, Circle
from utils import Point, color_name, classify_point

def print_shape_info(shape: Shape) -> None:
    desc = shape.describe()
    area = shape.area()
    print(desc)
    print(area)

def main():
    # Test 1: Create rectangle and get area
    rect = Rectangle("Box", 5.0, 3.0)
    print(rect.area())
    
    # Test 2: Use interface methods
    rect.set_color(Color.BLUE)
    print(color_name(rect.get_color()))
    
    # Test 3: Create circle and get area
    circle = Circle("Disk", 2.0)
    print(circle.area())
    
    # Test 4: Polymorphic method calls
    print_shape_info(rect)
    print_shape_info(circle)
    
    # Test 5: Struct value semantics (copy-on-assign)
    p1 = Point(3.0, 4.0)
    p2 = p1
    p2.x = 10.0
    print(p1.x)
    print(p2.x)
    
    # Test 6: Method access from base class
    print(rect.get_name())
    
    # Test 7: Helper function with struct
    print(classify_point(p1))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
15.0
Blue
12.56636
Rectangle Box (5.0x3.0)
15.0
Circle Disk (r=2.0)
12.56636
3.0
10.0
Box
near

```

### Actual
```
15.0
Blue
12.56636
Rectangle Box (5.0x3.0)
15.0
Circle Disk (r=2.0)
12.56636
3.0
10.0
Box
far
```

## Timing

- Generation: 307.34s
- Execution: 5.34s
