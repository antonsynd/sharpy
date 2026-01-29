# Issue Report: execution_failed

**Timestamp:** 2026-01-29T00:04:41.657655
**Type:** execution_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - geometry processing with validation
from geometry import Shape, Rectangle, Circle
from validators import ValidationResult, validate_positive, validate_shape_dimensions, RangeValidator

def main():
    print("=== Module Import Test: Geometry & Validation ===")
    
    # Create shapes using imported classes
    rect: Rectangle = Rectangle(5.0, 3.0)
    circ: Circle = Circle(2.0)
    
    # Test cross-module inheritance (Shape from geometry, used in validators)
    print(rect.describe())
    print(circ.describe())
    
    # Test validation functions
    validation: ValidationResult = validate_positive(rect.width)
    print(validation.message)
    
    # Test validator that uses imported Shape type
    rect_valid: bool = validate_shape_dimensions(rect)
    circ_valid: bool = validate_shape_dimensions(circ)
    print(f"Rectangle valid: {rect_valid}")
    print(f"Circle valid: {circ_valid}")
    
    # Test imported class with methods
    range_check: RangeValidator = RangeValidator(0.0, 100.0)
    area_in_range: bool = range_check.is_in_range(rect.area())
    print(f"Rectangle area in range: {area_in_range}")
    
    print("=== Complete ===")

# EXPECTED OUTPUT:
# === Module Import Test: Geometry & Validation ===
# Rectangle: area=15, perimeter=16
# Circle: area=12.56636, perimeter=12.56636
# Value is positive
# Rectangle valid: True
# Circle valid: True
# Rectangle area in range: True
# === Complete ===
```

## Error

```
Compilation failed:
  Semantic error at line 13, column 11: Type 'Rectangle' has no member 'describe'
  Semantic error at line 14, column 11: Type 'Circle' has no member 'describe'
  Semantic error at line 17, column 5: Cannot assign type 'ValidationResult' to variable of type 'ValidationResult'
  Semantic error at line 21, column 50: Cannot pass argument of type 'Rectangle' to parameter of type 'Shape'
  Semantic error at line 22, column 50: Cannot pass argument of type 'Circle' to parameter of type 'Shape'

```

## Timing

- Generation: 19.22s
- Execution: 1.03s
