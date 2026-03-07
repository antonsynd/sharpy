# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T18:17:14.378597
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - combines all modules

from colors import Color, ColorPalette
from shapes_extended import Rectangle, Circle
from geometry import PaintCalculator

def main():
    # Create palette and add colors
    palette: ColorPalette = ColorPalette()
    palette.add(Color.RED)
    palette.add(Color.BLUE)
    palette.add(Color.GREEN)
    
    print(f"Palette has {palette.count()} colors")
    
    # Create shapes using cross-module classes
    rect1: Rectangle = Rectangle(10.0, 5.0, Color.RED)
    circle: Circle = Circle(3.0, Color.BLUE)
    rect2: Rectangle = Rectangle(5.0, 6.0, Color.GREEN)
    
    # Test polymorphism - Drawable interface from shapes module
    drawable1: Drawable = rect1
    drawable2: Drawable = circle
    
    drawable1.draw()
    drawable2.draw()
    
    # Calculate paint needed
    calc: PaintCalculator = PaintCalculator(25.0)
    shapes: list[Shape] = [rect1, circle, rect2]
    
    paint: float = calc.paint_needed(shapes)
    print(f"Paint needed: {paint}")
    
    # Get shape descriptions
    for s in shapes:
        print(s.describe())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'string' does not contain a definition for 'Startswith' and no accessible extension method 'Startswith' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpyzv8ljzx/geometry.spy:30:26
    |
 30 |     shapes: list[Shape] = [rect1, circle, rect2]
    |                          ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpyzv8ljzx/colors.spy:24:33
    |
 24 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpyzv8ljzx/shapes_extended.spy:4:15
    |
  4 | from shapes_extended import Rectangle, Circle
    |               ^^^^^
    |

warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpyzv8ljzx/geometry.spy:4:20
    |
  4 | from shapes_extended import Rectangle, Circle
    |                    ^^^^^^^^^
    |


```

## Timing

- Generation: 308.95s
- Execution: 4.47s
