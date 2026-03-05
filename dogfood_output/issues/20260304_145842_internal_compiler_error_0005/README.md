# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-04T14:47:22.553728
**Type:** internal_compiler_error
**Feature Focus:** method_overloading
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex method overloading with type aliases and enum dispatch
# Verifies overload resolution works with different parameter counts and types

enum ShapeType:
    CIRCLE = 1
    RECTANGLE = 2
    TRIANGLE = 3

type Dimension2D = tuple[float, float]

class GeometryCalculator:
    def area(self, radius: float) -> float:
        return 3.14159 * radius * radius

    def area(self, width: float, height: float) -> float:
        return width * height

    def area(self, dims: Dimension2D) -> float:
        w: float = dims[0]
        h: float = dims[1]
        return w * h

    def area(self, a: float, b: float, is_triangle: bool) -> float:
        if is_triangle:
            return 0.5 * a * b
        return a * b

    def describe(self, kind: ShapeType) -> str:
        if kind == ShapeType.CIRCLE:
            return "Circle"
        elif kind == ShapeType.RECTANGLE:
            return "Rectangle"
        elif kind == ShapeType.TRIANGLE:
            return "Triangle"
        else:
            return "Unknown"

class AdvancedCalculator(GeometryCalculator):
    def perimeter(self, radius: float) -> float:
        return 2.0 * 3.14159 * radius

    def perimeter(self, width: float, height: float) -> float:
        return 2.0 * (width + height)

    def perimeter(self, dims: Dimension2D) -> float:
        w: float = dims[0]
        h: float = dims[1]
        return 2.0 * (w + h)

def main():
    calc = GeometryCalculator()
    adv_calc = AdvancedCalculator()

    # Test overloaded area methods on base class
    circle_area: float = calc.area(5.0)
    print(circle_area)

    rect_area: float = calc.area(4.0, 3.0)
    print(rect_area)

    dim: Dimension2D = (3.0, 4.0)
    tuple_area: float = calc.area(dim)
    print(tuple_area)

    tri_area: float = calc.area(10.0, 8.0, True)
    print(tri_area)

    # Test overloaded area methods on subclass
    circle_area2: float = adv_calc.area(5.0)
    print(circle_area2)

    # Test overloaded perimeter methods on subclass
    circle_perim: float = adv_calc.perimeter(5.0)
    print(circle_perim)

    rect_perim: float = adv_calc.perimeter(4.0, 3.0)
    print(rect_perim)

    tuple_perim: float = adv_calc.perimeter(dim)
    print(tuple_perim)

    # Test enum dispatch
    desc1: str = calc.describe(ShapeType.CIRCLE)
    print(desc1)

    desc2: str = calc.describe(ShapeType.RECTANGLE)
    print(desc2)

    desc3: str = calc.describe(ShapeType.TRIANGLE)
    print(desc3)

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:55:26
    |
 55 |     circle_area: float = calc.area(5.0)
    |                          ^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:58:24
    |
 58 |     rect_area: float = calc.area(4.0, 3.0)
    |                        ^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:62:25
    |
 62 |     tuple_area: float = calc.area(dim)
    |                         ^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:65:23
    |
 65 |     tri_area: float = calc.area(10.0, 8.0, True)
    |                       ^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:69:27
    |
 69 |     circle_area2: float = adv_calc.area(5.0)
    |                           ^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:73:27
    |
 73 |     circle_perim: float = adv_calc.perimeter(5.0)
    |                           ^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:76:25
    |
 76 |     rect_perim: float = adv_calc.perimeter(4.0, 3.0)
    |                         ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp98rhf1s6/dogfood_test.spy:79:26
    |
 79 |     tuple_perim: float = adv_calc.perimeter(dim)
    |                          ^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 662.94s
