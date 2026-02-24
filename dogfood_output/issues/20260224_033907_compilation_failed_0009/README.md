# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T03:36:50.210210
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests module imports and cross-module usage

from shapes import Rectangle, Circle
from math_utils import add, factorial, describe_shape, circle_stats, GOLDEN_RATIO

def process_rectangles(rects: list[Rectangle]) -> float:
    total: float = 0.0
    for rect in rects:
        total += rect.area()
    return total

def main():
    # Test 1: Basic function import from math_utils
    sum_result: float = add(10.0, 25.0)
    print(f"Sum: {sum_result}")

    # Test 2: Constant import
    print(f"Golden ratio: {GOLDEN_RATIO}")

    # Test 3: Factorial function
    fact7: int = factorial(7)
    print(f"7! = {fact7}")

    # Test 4: Class import and usage
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.5)

    desc: str = describe_shape(rect)
    print(f"Shape description: {desc}")

    # Test 5: Multiple shapes and total area calculation
    shapes_list: list[Rectangle] = [Rectangle(2.0, 4.0), Rectangle(3.0, 6.0)]
    total_area: float = process_rectangles(shapes_list)
    print(f"Total rectangle area: {total_area}")

    # EXPECTED OUTPUT:
    # Sum: 35.0
    # Golden ratio: 1.61803
    # 7! = 5040
    # Shape description: Rectangle 5.0x3.0: area=15.00, perimeter=16.00
    # Total rectangle area: 30.0
```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.Shape.Area()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:14:32
    |
 14 |     sum_result: float = add(10.0, 25.0)
    |                                ^
    |

error[CS0513]: 'Shapes.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:15:32
    |
 15 |     print(f"Sum: {sum_result}")
    |                                ^
    |

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpy7216mgp/shapes.spy:35:25
    |
 35 | 
    | ^
    |

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpy7216mgp/shapes.spy:39:32
    |
 39 |     # 7! = 5040
    |                ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'circle' is assigned but never used
  --> /tmp/tmpy7216mgp/main.spy:26:5
    |
 26 |     circle: Circle = Circle(2.5)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'circle_stats' is never used
  --> /tmp/tmpy7216mgp/main.spy:4:56
    |
  4 | from math_utils import add, factorial, describe_shape, circle_stats, GOLDEN_RATIO
    |                                                        ^^^^^^^^^^^^
    |


```

## Timing

- Generation: 121.68s
- Execution: 4.43s
