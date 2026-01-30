# Issue Report: execution_failed

**Timestamp:** 2026-01-29T21:14:45.767117
**Type:** execution_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module utilities
from geometry import Circle, Rectangle, IShape
from validators import ShapeValidator, validate_number, ValidationResult
from formatters import ShapeFormatter, get_shape_type_name

def main():
    # Create shapes
    circle: Circle = Circle(5.0)
    rectangle: Rectangle = Rectangle(4.0, 6.0)
    
    # Test formatter
    formatter: ShapeFormatter = ShapeFormatter(2)
    print(formatter.format_area(circle))
    print(formatter.format_area(rectangle))
    
    # Test shape type detection
    circle_type: str = get_shape_type_name(circle)
    rectangle_type: str = get_shape_type_name(rectangle)
    print(circle_type)
    print(rectangle_type)
    
    # Test validator
    validator: ShapeValidator = ShapeValidator(20.0)
    circle_result: ValidationResult = validator.validate_shape(circle)
    rectangle_result: ValidationResult = validator.validate_shape(rectangle)
    
    print(circle_result.message)
    print(rectangle_result.message)
    
    # Test number validator
    is_valid: bool = validate_number(50, 1, 100)
    print(is_valid)

# EXPECTED OUTPUT:
# Area: 78.53975, Perimeter: 31.4159
# Area: 24.0, Perimeter: 20.0
# Circle
# Rectangle
# Shape meets minimum area requirement
# Shape meets minimum area requirement
# True
```

## Error

```
Compilation failed:
  Semantic error at line 2, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpzfe74489/geometry.spy': Parser error at line 3, column 2: Expected decorator name (in main.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpzfe74489/geometry.spy': Parser error at line 3, column 2: Expected decorator name (in validators.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpzfe74489/geometry.spy': Parser error at line 3, column 2: Expected decorator name (in validators.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpzfe74489/geometry.spy': Parser error at line 3, column 2: Expected decorator name (in formatters.spy)
  Semantic error at line 3, column 1: Error loading module '/var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpzfe74489/geometry.spy': Parser error at line 3, column 2: Expected decorator name (in formatters.spy)

```

## Timing

- Generation: 20.34s
- Execution: 0.94s
