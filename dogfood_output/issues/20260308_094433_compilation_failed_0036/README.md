# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T09:41:29.542947
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from shapes and utils modules
from shapes import Rectangle, Circle, Square, IDrawable, IShape
from utils import calculate_total_area, calculate_total_perimeter, create_rectangles, scale_factor

def main():
    # Test 1: Create shapes from shapes module
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.0)
    square: Square = Square(4.0)

    # Test 2: Use IDrawable interface from shapes module
    items: list[IDrawable] = [rect, circle, square]
    for item in items:
        print(item.draw())

    # Test 3: Calculate areas using utils functions
    # FIX: Explicitly typed as list[IShape] to avoid type inference issues
    shapes: list[IShape] = [rect, circle, square]
    total_area: float = calculate_total_area(shapes)
    total_perim: float = calculate_total_perimeter(shapes)
    print(f"Total area: {total_area:.2f}")
    print(f"Total perimeter: {total_perim:.2f}")

    # Test 4: Use create_rectangles from utils (creates shapes via module import)
    rectangles: list[Rectangle] = create_rectangles(3)
    for r in rectangles:
        print(f"Rect: {r.area():.1f}")

    # Test 5: Use scale_factor function
    scaled: float = scale_factor(5.0)
    print(f"Scaled: {scaled:.1f}")

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpu4kcnjx8/shapes.spy:42:25

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpu4kcnjx8/shapes.spy:46:32


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpu4kcnjx8/utils.spy:2:32
    |
  2 | from shapes import Rectangle, Circle, Square, IDrawable, IShape
    |                                ^^^^^^
    |


```

## Timing

- Generation: 155.88s
- Execution: 5.10s
