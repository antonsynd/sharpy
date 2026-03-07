# Skipped Dogfood Run

**Timestamp:** 2026-03-07T01:07:19.847111
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmp2mpo10u6/dogfood_test.spy:9:26
    |
  9 |         case Shape.Circle(r):
    |                          ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp2mpo10u6/dogfood_test.spy:11:9
    |
 11 |         case Shape.Rectangle(w, h):
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp2mpo10u6/dogfood_test.spy:13:9
    |
 13 |         case Shape.Triangle(b, h):
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmp2mpo10u6/dogfood_test.spy:16:1
    |
 16 | def describe(shape: Shape) -> str:
    | ^
    |


**Feature Focus:** union_declaration
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Tagged union declaration with pattern matching for shapes
union Shape:
    case Circle(radius: float)
    case Rectangle(width: float, height: float)
    case Triangle(base: float, height: float)

def area(shape: Shape) -> float:
    return match shape:
        case Shape.Circle(r):
            3.14159 * r * r
        case Shape.Rectangle(w, h):
            w * h
        case Shape.Triangle(b, h):
            0.5 * b * h

def describe(shape: Shape) -> str:
    return match shape:
        case Shape.Circle(r):
            f"Circle with radius {r}"
        case Shape.Rectangle(w, h):
            f"Rectangle {w} x {h}"
        case Shape.Triangle(b, h):
            f"Triangle base={b} height={h}"

def is_large(shape: Shape) -> bool:
    return match shape:
        case Shape.Circle(r):
            r > 10.0
        case Shape.Rectangle(w, h):
            w * h > 100.0
        case Shape.Triangle(b, h):
            0.5 * b * h > 50.0

def main():
    # Create various shapes using union cases
    circle: Shape = Shape.Circle(5.0)
    rect: Shape = Shape.Rectangle(4.0, 6.0)
    triangle: Shape = Shape.Triangle(3.0, 4.0)

    # Print descriptions and areas
    print(describe(circle))
    print(area(circle))
    print(describe(rect))
    print(area(rect))
    print(describe(triangle))
    print(area(triangle))

    # Large shape check
    large_circle: Shape = Shape.Circle(15.0)
    print(is_large(large_circle))
    print(is_large(triangle))

    # Scale test with different shapes in a list
    shapes: list[Shape] = [
        Shape.Circle(2.0),
        Shape.Rectangle(3.0, 4.0),
        Shape.Triangle(5.0, 6.0)
    ]

    print(len(shapes))

```

## Timing

- Generation: 265.96s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
