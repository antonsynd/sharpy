# Issue Report: output_mismatch

**Timestamp:** 2026-02-21T04:34:40.990543
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module features
from module_shapes import Shape
from module_shapes_extended import Rectangle, Circle
from module_geometry import ShapeType, Point

def main():
    print("=== Shape System Demo ===")
    
    # Create shapes directly
    rect = Rectangle(10.0, 5.0)
    circle = Circle(5.0)
    
    # Test method access
    print(rect.describe())
    print(f"Rectangle area: {rect.get_area()}")
    print(circle.describe())
    print(f"Circle area: {circle.get_area()}")
    
    # Test struct and Point functionality
    p1 = Point(0.0, 0.0)
    p2 = Point(3.0, 4.0)
    print(f"Distance: {p1.distance_to(p2)}")
    
    # Test interface-like methods through concrete class
    print(rect.draw())
    
    # Test enum
    rect_type = ShapeType.RECTANGLE
    circle_type = ShapeType.CIRCLE
    
    print("=== Demo Complete ===")

# EXPECTED OUTPUT:
# === Shape System Demo ===
# Rectangle (10.0x5.0)
# Rectangle area: 50.0
# Circle (r=5.0)
# Circle area: 78.53975
# Distance: 5.0
# Drawing rectangle 10.0x5.0
# === Demo Complete ===
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
=== Shape System Demo ===
Rectangle (10.0x5.0)
Rectangle area: 50.0
Circle (r=5.0)
Circle area: 78.53975
Distance: 5.0
Drawing rectangle 10.0x5.0
=== Demo Complete ===

```

### Actual
```
=== Shape System Demo ===
Rectangle (10x5)
Rectangle area: 50
Circle (r=5)
Circle area: 78.53975
Distance: 5
Drawing rectangle 10x5
=== Demo Complete ===
```

## Timing

- Generation: 751.80s
- Execution: 4.95s
