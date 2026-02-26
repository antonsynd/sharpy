# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T06:18:39.800736
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates module imports
from geometry import Point, Rectangle, Circle, calculate_diagonal, is_square, create_unit_square

def main():
    # Create a point at origin
    origin: Point = Point(0.0, 0.0)
    
    # Create another point
    p2: Point = Point(3.0, 4.0)
    
    # Calculate distance between points
    dist: float = origin.distance_to(p2)
    print(dist)
    
    # Create rectangles and test square detection
    rect: Rectangle = Rectangle(4.0, 3.0)
    square: Rectangle = Rectangle(5.0, 5.0)
    
    # Print rectangle area and perimeter
    print(rect.area())
    print(rect.perimeter())
    
    # Check if it's a square
    print(is_square(rect))
    print(is_square(square))
    
    # Calculate diagonal using imported function
    diag: float = calculate_diagonal(rect)
    print(diag)
    
    # Create and test circle
    circle: Circle = Circle(2.0)
    print(circle.area())
    
    # Create unit square using factory function
    unit: Rectangle = create_unit_square()
    print(unit.area())
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmpf_qcbwuz/geometry.spy:62:20

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmpf_qcbwuz/geometry.spy:65:27


```

## Timing

- Generation: 385.57s
- Execution: 4.37s
