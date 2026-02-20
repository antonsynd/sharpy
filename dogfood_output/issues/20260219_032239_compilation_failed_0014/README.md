# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T03:20:12.853857
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module class usage

from shapes import Shape, Rectangle, Circle
from utils import ShapeAnalyzer

def main():
    # Create various shapes
    rect1 = Rectangle(5.0, 3.0)
    rect2 = Rectangle(4.0, 4.0)
    circle = Circle(2.5)
    
    # Print individual shape descriptions
    print(rect1.describe())
    print(circle.describe())
    
    # Use the analyzer from utils module
    analyzer = ShapeAnalyzer()
    shapes: list[Shape] = [rect1, rect2, circle]
    
    # Calculate total area
    total: float = analyzer.total_area(shapes)
    print(total)
    
    # Count rectangles
    rect_count: int = analyzer.count_rectangles(shapes)
    print(rect_count)
    
    # Find largest shape
    largest: Shape = analyzer.largest_shape(shapes)
    print(largest.describe())

# EXPECTED OUTPUT:
# Rectangle 5.0x3.0
# Circle radius=2.5
# 43.7267375
# 2
# Rectangle 4.0x4.0
```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpcjdjjau7/shapes.spy:47:25


```

## Timing

- Generation: 131.61s
- Execution: 4.33s
