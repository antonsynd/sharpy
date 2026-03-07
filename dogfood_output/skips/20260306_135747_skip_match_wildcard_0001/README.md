# Skipped Dogfood Run

**Timestamp:** 2026-03-06T13:53:33.759867
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Newline
  --> /tmp/tmpfa19df7a/dogfood_test.spy:48:31
    |
 48 |         case ShapeType.CIRCLE:
    |                               ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpfa19df7a/dogfood_test.spy:50:9
    |
 50 |         case ShapeType.RECTANGLE:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpfa19df7a/dogfood_test.spy:52:9
    |
 52 |         case _:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpfa19df7a/dogfood_test.spy:55:1
    |
 55 | def bonus_multiplier(shape_type: ShapeType) -> float:
    | ^
    |


**Feature Focus:** match_wildcard
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Match wildcard pattern with inheritance hierarchy and match expressions
# Tests `_` as default/catch-all in statements and expressions

enum ShapeType:
    CIRCLE = 1
    RECTANGLE = 2
    TRIANGLE = 3
    POLYGON = 4

class Shape:
    @virtual
    def get_type(self) -> ShapeType:
        return ShapeType.POLYGON
    
    def area(self) -> float:
        return 0.0

class Circle(Shape):
    radius: float
    
    def __init__(self, r: float):
        self.radius = r
    
    @override
    def get_type(self) -> ShapeType:
        return ShapeType.CIRCLE
    
    def circle_area(self) -> float:
        return 3.14159 * self.radius ** 2

class Rectangle(Shape):
    width: float
    height: float
    
    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h
    
    @override
    def get_type(self) -> ShapeType:
        return ShapeType.RECTANGLE
    
    def rect_area(self) -> float:
        return self.width * self.height

def classify_shape(shape: Shape) -> str:
    return match shape.get_type():
        case ShapeType.CIRCLE:
            "round"
        case ShapeType.RECTANGLE:
            "angular"
        case _:
            "unknown"

def bonus_multiplier(shape_type: ShapeType) -> float:
    return match shape_type:
        case ShapeType.CIRCLE:
            1.5
        case ShapeType.RECTANGLE:
            1.25
        case ShapeType.TRIANGLE:
            1.1
        case _:
            1.0

def process_shapes(shapes: list[Shape]) -> None:
    total: float = 0.0
    
    for shape in shapes:
        base_score: float = match shape.get_type():
            case ShapeType.CIRCLE:
                shape.circle_area()
            case ShapeType.RECTANGLE:
                shape.rect_area()
            case _:
                shape.area()
        
        multiplier: float = bonus_multiplier(shape.get_type())
        final: float = base_score * multiplier
        total += final
        print(final)
    
    match len(shapes):
        case 0:
            print("empty")
        case 1:
            print("single")
        case 2:
            print("pair")
        case _:
            print("many")
    
    print(total)

def main():
    shapes: list[Shape] = [Circle(2.0), Rectangle(3.0, 4.0), Shape()]
    process_shapes(shapes)
    
    # Test match expression with wildcard on literals
    x: int = 42
    result: str = match x:
        case 0:
            "zero"
        case 1 | 2 | 3:
            "small"
        case _:
            "large"
    print(result)
    
    # Test match with direct value comparison
    y: int = 5
    msg: str = match y:
        case 1:
            "one"
        case 2:
            "two"
        case _:
            "other"
    print(msg)

```

## Timing

- Generation: 243.60s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
