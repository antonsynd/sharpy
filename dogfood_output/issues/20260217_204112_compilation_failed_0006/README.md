# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T20:38:13.948681
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex cross-module imports
from types import Color, Point
from shapes import IShape, Rectangle, Circle, ShapeBase, IDrawable
from utils import format_shape_info, create_default_point, ShapeFactory

def main():
    # Create shapes using factory from utils
    factory: ShapeFactory = ShapeFactory()
    
    rect: Rectangle = factory.create_red_rectangle(10.0, 5.0)
    circle: Circle = factory.create_blue_circle(3.0)
    
    # Print factory count
    print(factory.get_count())
    
    # Test struct operations
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    print(p2.distance_to(p1))
    
    # Print shape descriptions (cross-module virtual calls)
    print(rect.describe())
    print(circle.describe())
    
    # Print formatted shape info using interface types
    print(format_shape_info(rect))
    print(format_shape_info(circle))
    
    # Test interface methods
    drawable_rect: IDrawable = rect
    print(drawable_rect.draw())
    
    # Test enum access
    print(Color.GREEN)
    
    # Test inheritance chain
    base: ShapeBase = rect
    print(base.describe())

# EXPECTED OUTPUT:
# 2
# 5.0
# Rectangle with color 1
# Circle with color 3
# Area: 50.00, Perimeter: 30.00
# Area: 28.27, Perimeter: 18.85
# Drawing Rectangle at (0.0, 0.0)
# 2
# Rectangle with color 1
```

## Error

```
Assembly compilation failed:

error[CS0535]: 'Shapes.ShapeBase' does not implement interface member 'Types.IShape.Area()'
  --> shapes.cs:12:39
    |
 12 |     
    |     ^
    |

error[CS0115]: 'Shapes.Rectangle.Area()': no suitable method found to override
  --> /tmp/tmp0wulu7es/shapes.spy:20:32
    |
 20 |     
    |     ^
    |

error[CS0115]: 'Shapes.Rectangle.Perimeter()': no suitable method found to override
  --> /tmp/tmp0wulu7es/shapes.spy:36:32
    |
 36 |     # Test inheritance chain
    |                             ^
    |

error[CS0115]: 'Shapes.Circle.Area()': no suitable method found to override
  --> /tmp/tmp0wulu7es/shapes.spy:37:32
    |
 37 |     base: ShapeBase = rect
    |                           ^
    |

error[CS0115]: 'Shapes.Circle.Perimeter()': no suitable method found to override
  --> /tmp/tmp0wulu7es/shapes.spy:57:32


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmp0wulu7es/main.spy:3:20
    |
  3 | from shapes import IShape, Rectangle, Circle, ShapeBase, IDrawable
    |                    ^^^^^^
    |

warning[SPY0452]: Imported name 'create_default_point' is never used
  --> /tmp/tmp0wulu7es/main.spy:4:38
    |
  4 | from utils import format_shape_info, create_default_point, ShapeFactory
    |                                      ^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 161.42s
- Execution: 4.23s
