# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T09:39:24.837022
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
##! Main entry point - demonstrates cross-module polymorphism

from colors_module import Color, color_priority
from geometry_module import Rectangle, Circle, IDrawable
from pos_module import Position

def main():
    # Create shape instances with different colors and dimensions
    r: Rectangle = Rectangle(Color.RED, 4.0, 3.0)
    c: Circle = Circle(Color.BLUE, 2.0)
    
    # Polymorphic method dispatch - area calculations
    print(r.area())
    print(c.area())
    print(r.describe())
    
    # Interface-typed parameter passing
    process_drawable(r)
    process_drawable(c)
    
    # Cross-module function with enum parameter
    print(color_priority(Color.GREEN))
    
    # Struct value type operations
    p1: Position = Position(0.0, 0.0)
    p2: Position = Position(3.0, 4.0)
    print(p1.distance_to(p2))
    
    # Enum value access through instance
    print(r.color.value)

def process_drawable(d: IDrawable) -> None:
    # Demonstrates interface dispatch across modules
    print(d.draw())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'ColorsModule.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ColorsModule.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpr03dse72/main.spy:30:47
    |
 30 |     print(r.color.value)
    |                         ^
    |


```

## Timing

- Generation: 605.42s
- Execution: 5.15s
