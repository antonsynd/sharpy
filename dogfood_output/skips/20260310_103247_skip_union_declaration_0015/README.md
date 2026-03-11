# Skipped Dogfood Run

**Timestamp:** 2026-03-10T10:25:53.779579
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmprz3kfj9u/dogfood_test.spy:8:26
    |
  8 |         case Shape.Circle(r):
    |                          ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmprz3kfj9u/dogfood_test.spy:10:9
    |
 10 |         case Shape.Rectangle(w, h):
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmprz3kfj9u/dogfood_test.spy:12:9
    |
 12 |         case Shape.Point(x, y):
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmprz3kfj9u/dogfood_test.spy:14:9
    |
 14 |         case _:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmprz3kfj9u/dogfood_test.spy:17:1
    |
 17 | def calculate_area(shape: Shape) -> float:
    | ^
    |


**Feature Focus:** union_declaration
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
union Shape:
    case Circle(radius: float)
    case Rectangle(width: float, height: float)
    case Point(x: float, y: float)

def describe_shape(shape: Shape) -> str:
    match shape:
        case Shape.Circle(r):
            return f"Circle with radius {r}"
        case Shape.Rectangle(w, h):
            return f"Rectangle {w} x {h}"
        case Shape.Point(x, y):
            return f"Point at ({x}, {y})"
        case _:
            return "Unknown shape"

def calculate_area(shape: Shape) -> float:
    match shape:
        case Shape.Circle(r):
            return 3.14159 * r * r
        case Shape.Rectangle(w, h):
            return w * h
        case Shape.Point(x, y):
            return 0.0
        case _:
            return 0.0

def main():
    c: Shape = Shape.Circle(5.0)
    r: Shape = Shape.Rectangle(3.0, 4.0)
    p: Shape = Shape.Point(1.0, 2.0)
    
    print(describe_shape(c))
    print(describe_shape(r))
    print(describe_shape(p))
    
    print(calculate_area(c))
    print(calculate_area(r))
    print(calculate_area(p))

```

## Timing

- Generation: 397.80s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
