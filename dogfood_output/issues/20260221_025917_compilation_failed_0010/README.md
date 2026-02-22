# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T02:53:35.320249
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and complex interactions
from module_utils import Point, Color, format_measurement, SHAPE_RECTANGLE, SHAPE_CIRCLE
from module_shapes import Rectangle, Circle
from module_operations import ShapeCalculator, create_sample_shape

def main():
    # Test 1: Create point and color utilities
    origin: Point = Point(0.0, 0.0)
    corner: Point = Point(10.0, 20.0)
    print(f"Origin distance: {format_measurement(origin.distance_to_origin(), 'units')}")
    print(f"Corner distance: {format_measurement(corner.distance_to_origin(), 'units')}")
    
    # Test 2: Color utilities
    favorite_color: str = Color.from_name("green")
    print(f"Favorite color: {favorite_color}")
    
    # Test 3: Create shapes with specific dimensions
    rect: Rectangle = Rectangle(origin, 5.0, 3.0, "blue")
    circle: Circle = Circle(corner, 4.0, "red")
    print(f"Rectangle area: {format_measurement(rect.area(), 'sq units')}")
    print(f"Circle area: {format_measurement(circle.area(), 'sq units')}")
    
    # Test 4: Use the shape calculator
    calculator: ShapeCalculator = ShapeCalculator()
    calculator.add_shape(rect)
    calculator.add_shape(circle)
    print(f"Total area: {format_measurement(calculator.total_area(), 'sq units')}")
    
    # Test 5: Create shapes using factory function
    sample_rect: Shape = create_sample_shape(SHAPE_RECTANGLE, 0.0, 0.0)
    sample_circle: Shape = create_sample_shape(SHAPE_CIRCLE, 5.0, 5.0)
    calculator.add_shape(sample_rect)
    calculator.add_shape(sample_circle)
    
    # Print final report
    report: str = calculator.get_shape_report()
    print(f"Shape report: {report}")
    print(f"Final total: {format_measurement(calculator.total_area(), 'sq units')}")

# EXPECTED OUTPUT:
# Origin distance: 0.00 units
# Corner distance: 22.36 units
# Favorite color: green
# Rectangle area: 15.00 sq units
# Circle area: 50.27 sq units
# Total area: 65.27 sq units
# Shape report: Rectangle(5.0x3.0): 15.00 sq units, Circle(r=4.0): 50.27 sq units, Rectangle(10.0x5.0): 50.00 sq units, Circle(r=7.0): 153.94 sq units
# Final total: 269.21 sq units
```

## Error

```
Assembly compilation failed:

error[CS0117]: 'ModuleUtils.Color' does not contain a definition for 'Red'
  --> /tmp/tmpztv___ra/module_utils.spy:33:30
    |
 33 |     calculator.add_shape(sample_circle)
    |                              ^
    |

error[CS0117]: 'ModuleUtils.Color' does not contain a definition for 'Green'
  --> /tmp/tmpztv___ra/module_utils.spy:35:30
    |
 35 |     # Print final report
    |                         ^
    |

error[CS0117]: 'ModuleUtils.Color' does not contain a definition for 'Blue'
  --> /tmp/tmpztv___ra/module_utils.spy:37:30
    |
 37 |     print(f"Shape report: {report}")
    |                              ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpztv___ra/module_shapes.spy:2:24
    |
  2 | from module_utils import Point, Color, format_measurement, SHAPE_RECTANGLE, SHAPE_CIRCLE
    |                        ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpztv___ra/module_shapes.spy:2:35
    |
  2 | from module_utils import Point, Color, format_measurement, SHAPE_RECTANGLE, SHAPE_CIRCLE
    |                                   ^^^^^^^^^^^
    |


```

## Timing

- Generation: 309.39s
- Execution: 4.81s
