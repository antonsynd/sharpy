# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T06:03:48.980590
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests importing from multiple modules
from shapes import Shape, Rectangle, Circle, create_rectangle, create_circle
from utils import format_number, PI_SQUARED, sum_areas, count_by_name

def process_shape(s: Shape) -> None:
    desc: str = s.describe()
    area: float = s.area()
    formatted: str = format_number(area)
    print(f"{desc}, area={formatted}")

def main():
    # Create shapes using factory functions
    rect: Rectangle = create_rectangle(5.0, 3.0)
    circle: Circle = create_circle(2.5)
    
    # Print individual shape info
    process_shape(rect)
    process_shape(circle)
    
    # Create a list of shapes
    shapes: list[Shape] = [rect, circle, Rectangle(4.0, 4.0), Circle(1.0)]
    
    # Test sum_areas with imported function
    total: float = sum_areas(shapes)
    print(f"Total area: {format_number(total)}")
    
    # Test count_by_name
    rect_count: int = count_by_name(shapes, "Rectangle")
    print(f"Rectangle count: {rect_count}")
    
    # Print constant from utils
    print(f"PI squared: {PI_SQUARED}")
```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp0fsu1zag/shapes.spy:49:25

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp0fsu1zag/shapes.spy:53:32


```

## Timing

- Generation: 369.53s
- Execution: 4.46s
