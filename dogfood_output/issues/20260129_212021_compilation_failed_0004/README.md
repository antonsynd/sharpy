# Issue Report: compilation_failed

**Timestamp:** 2026-01-29T21:19:29.359435
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from shapes import Circle, Shape
from containers import ShapeContainer

def main():
    print("=== Cross-Module Classes Demo ===")
    
    # Create shapes from imported module
    circle1: Circle = Circle(5.0)
    circle2: Circle = Circle(10.0)
    
    print(circle1.describe())
    print(f"Area: {circle1.area()}")
    
    # Use container from another module
    container: ShapeContainer = ShapeContainer()
    container.add_shape(circle1)
    container.add_shape(circle2)
    
    print(f"Total shapes: {container.count}")
    print(f"Average area: {container.get_average_area()}")

# EXPECTED OUTPUT:
# === Cross-Module Classes Demo ===
# Circle with radius 5.0
# Area: 78.53975
# Total shapes: 2
# Average area: 196.3493625
```

## Error

```
Assembly compilation failed:
  containers.cs(32,51): error CS0246: The type or namespace name 'Float' could not be found (are you missing a using directive or an assembly reference?)

```

## Timing

- Generation: 15.83s
- Execution: 1.33s
