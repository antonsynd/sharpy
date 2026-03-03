# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T02:08:53.516939
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex imports and cross-module usage

from shapes import Shape, Rectangle, Circle, Dimensions
from colors import Color, ColorType, get_color_type
from utils import calculate_total_area, count_by_type, create_report

def main():
    red: Color = Color("red", 255, 0, 0)
    
    rect: Rectangle = Rectangle(10.0, 5.0, red)
    circ: Circle = Circle(0.0)
    
    shapes: list[Shape] = [rect, circ]
    
    print(circ.area())
    print(rect.get_color())
    
    total: float = calculate_total_area(shapes)
    print(total)
    
    blue_type: ColorType = get_color_type("blue")
    print(blue_type.name)
    
    dim: Dimensions = Dimensions(4.0, 3.0)
    print(dim.area())
    
    rect_count: int = count_by_type(shapes, "rectangle")
    print(rect_count)
    
    report: str = create_report(rect, circ)
    print(report)

```

## Error

```
Assembly compilation failed:

error[CS1729]: 'Shapes.Dimensions' does not contain a constructor that takes 2 arguments
  --> /tmp/tmp2dx104c4/main.spy:24:37
    |
 24 |     dim: Dimensions = Dimensions(4.0, 3.0)
    |                                     ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp2dx104c4/shapes.spy:9:18
    |
  9 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ColorType' is never used
  --> /tmp/tmp2dx104c4/shapes.spy:4:20
    |
  4 | from colors import Color, ColorType, get_color_type
    |                    ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'get_color_type' is never used
  --> /tmp/tmp2dx104c4/shapes.spy:4:31
    |
  4 | from colors import Color, ColorType, get_color_type
    |                               ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ColorType' is never used
  --> /tmp/tmp2dx104c4/utils.spy:4:19
    |
  4 | from colors import Color, ColorType, get_color_type
    |                   ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'get_color_type' is never used
  --> /tmp/tmp2dx104c4/utils.spy:4:30
    |
  4 | from colors import Color, ColorType, get_color_type
    |                              ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 310.63s
- Execution: 4.76s
