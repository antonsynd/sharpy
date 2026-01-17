# Issue Report: execution_failed

**Timestamp:** 2026-01-17T09:43:58.649080
**Type:** execution_failed
**Feature Focus:** from_import
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test from_import with complex class hierarchy across modules

# File: shapes.sp
from geometry import Shape, Circle, calculate_area
from colors import ColoredShape, Color

# Main program demonstrating from_import usage

# Create a colored circle
color: Color = Color()
color.r = 255
color.g = 128
color.b = 0

circle: Circle = Circle()
circle.radius = 5

colored: ColoredShape = ColoredShape()
colored.shape = circle
colored.color = color

# Calculate and display
area: int = calculate_area(circle)
print(area)

# Test inheritance through imported classes
shape: Shape = circle
print(shape.get_name())

# Display color info
print(colored.get_description())

# Test with multiple shapes
circle2: Circle = Circle()
circle2.radius = 10
area2: int = calculate_area(circle2)
print(area2)

# Verify polymorphism works with imports
shapes_count: int = 0
i: int = 0
while i < 3:
    if i == 0:
        shapes_count += 1
        print(circle.get_name())
    elif i == 1:
        shapes_count += 1
        print(circle2.get_name())
    else:
        shapes_count += 1
    i += 1

print(shapes_count)

# Final verification
is_valid: bool = area > 0 and area2 > area
print(is_valid)

# EXPECTED OUTPUT:
# 78
# Circle
# ColoredCircle
# 314
# Circle
# Circle
# 3
# True
```

## Error

```
Compilation failed:
  Semantic error at line 10, column 16: Undefined identifier 'Color'
  Semantic error at line 15, column 18: Undefined identifier 'Circle'
  Semantic error at line 18, column 25: Undefined identifier 'ColoredShape'
  Semantic error at line 23, column 13: Undefined identifier 'calculate_area'
  Semantic error at line 34, column 19: Undefined identifier 'Circle'
  Semantic error at line 36, column 14: Undefined identifier 'calculate_area'

```

## Timing

- Generation: 13.83s
- Execution: 0.84s
