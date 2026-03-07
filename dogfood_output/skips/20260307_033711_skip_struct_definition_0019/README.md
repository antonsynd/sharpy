# Skipped Dogfood Run

**Timestamp:** 2026-03-07T03:29:00.320719
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0220]: Cannot assign type 'object' to variable of type 'Dimension'
  --> /tmp/tmpfvaot4nh/dogfood_test.spy:21:9
    |
 21 |         d: Dimension = other
    |         ^^^^^^^^^^^^^^^^^^^^
    |

Validation errors:
error[SPY0322]: Decorators cannot be applied to module-level variable declarations. '@static' on 'widget_counter' is only valid inside a class or struct body.
  --> /tmp/tmpfvaot4nh/dogfood_test.spy:66:1
    |
 66 | @static
    | ^^^^^^^
    |

error[SPY0455]: Class 'Dimension' defines '__eq__(self, other: object)' but not '__hash__'. The .NET equality contract requires both. Define '__hash__(self) -> int'.
  --> /tmp/tmpfvaot4nh/dogfood_test.spy:18:5
    |
 18 |     def __eq__(self, other: object) -> bool:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** struct_definition
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test complex struct patterns: composition and dunder methods
# Using if-elif instead of match guards (guards not implemented)

struct Dimension:
    width: float
    height: float

    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h

    def area(self) -> float:
        return self.width * self.height

    def __str__(self) -> str:
        return f"Dimension({self.width}, {self.height})"

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Dimension):
            return False
        d: Dimension = other
        return self.width == d.width and self.height == d.height

struct Position:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> float:
        return (self.x ** 2.0 + self.y ** 2.0) ** 0.5

    def __str__(self) -> str:
        return f"Position({self.x}, {self.y})"

struct Widget:
    pos: Position
    size: Dimension
    name: str
    visible: bool

    def __init__(self, pos: Position, size: Dimension, name: str):
        self.pos = pos
        self.size = size
        self.name = name
        self.visible = True

    def hide(self) -> None:
        self.visible = False

    def show(self) -> None:
        self.visible = True

    def center(self) -> Position:
        cx = self.pos.x + self.size.width / 2.0
        cy = self.pos.y + self.size.height / 2.0
        return Position(cx, cy)

    def __str__(self) -> str:
        status = "visible" if self.visible else "hidden"
        return f"Widget({self.name}, {self.pos}, {self.size}, {status})"

# Static counter using @static decorator
@static
widget_counter: int = 0

def classify_size(dim: Dimension) -> str:
    # No guard clauses in match - using if-elif instead
    if dim.width < 10.0 and dim.height < 10.0:
        return "small"
    if dim.width > 100.0 or dim.height > 100.0:
        return "large"
    return "medium"

def optional_widget_check(w: Widget?) -> None:
    if w is not None:
        print(f"Widget present: {w.name}")
    else:
        print("No widget found")

def create_widget_at(x: float, y: float, w: float, h: float, name: str) -> Widget:
    # Access and modify the static counter
    widget_counter += 1
    return Widget(Position(x, y), Dimension(w, h), f"{name}_{widget_counter}")

def main():
    # Test struct creation and methods
    dim = Dimension(5.0, 3.0)
    print(dim.area())

    # Test nested struct operations
    pos = Position(10.0, 20.0)
    print(pos.distance_from_origin())

    # Test struct composition in Widget
    widget = Widget(pos, dim, "button")
    center = widget.center()
    print(center.x)
    print(center.y)

    # Test struct stored in optionals
    widget_opt: Widget? = widget
    optional_widget_check(widget_opt)
    widget_opt = None()
    optional_widget_check(widget_opt)

    # Test global counter in struct factory
    w1 = create_widget_at(0.0, 0.0, 50.0, 30.0, "panel")
    w2 = create_widget_at(100.0, 100.0, 200.0, 150.0, "window")
    print(w1.name)
    print(w2.name)

    # Test struct visibility state
    print(widget.visible)
    widget.hide()
    print(widget.visible)
    widget.show()
    print(widget.visible)

    # Test struct equality
    dim2 = Dimension(5.0, 3.0)
    dim3 = Dimension(8.0, 9.0)
    print(dim == dim2)
    print(dim == dim3)

    # Test classification (using if-elif since guards not supported)
    print(classify_size(dim))
    print(classify_size(Dimension(150.0, 50.0)))
    print(classify_size(Dimension(50.0, 50.0)))

```

## Timing

- Generation: 475.86s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
