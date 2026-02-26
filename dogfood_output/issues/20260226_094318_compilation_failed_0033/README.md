# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T09:35:44.304602
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests multi-file imports and polymorphism

from shapes import Rectangle, Circle, IMeasurable
from utils import total_area, is_large_rect, scale_dimensions

def main():
    rect1: Rectangle = Rectangle(6.0, 5.0)
    rect2: Rectangle = Rectangle(4.0, 4.0)
    circle: Circle = Circle(2.0)
    
    shapes: list[IMeasurable] = [rect1, rect2, circle]
    
    total: float = total_area(shapes)
    print(total)
    
    is_large: bool = is_large_rect(rect1)
    print(is_large)
    
    w: float
    h: float
    w, h = scale_dimensions(2.0, 3.0, 1.5)
    print(w)
    print(h)
```

## Error

```
Assembly compilation failed:

error[CS0535]: 'Shapes.Rectangle' does not implement interface member 'Shapes.IMeasurable.Measure()'
  --> shapes.cs:17:30
    |
 17 |     print(is_large)
    |                    ^
    |

error[CS0535]: 'Shapes.Circle' does not implement interface member 'Shapes.IMeasurable.Measure()'
  --> /tmp/tmpxhddbc87/shapes.spy:16:27
    |
 16 |     is_large: bool = is_large_rect(rect1)
    |                           ^
    |

error[CS1061]: 'Shapes.Rectangle' does not contain a definition for 'Measure' and no accessible extension method 'Measure' accepting a first argument of type 'Shapes.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpxhddbc87/utils.spy:12:18
    |
 12 |     
    |     ^
    |


```

## Timing

- Generation: 440.03s
- Execution: 4.24s
