# Skipped Dogfood Run

**Timestamp:** 2026-02-24T04:44:22.142391
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmpmpma71uk/dogfood_test.spy:65:20
    |
 65 |         case Circle() as c:
    |                    ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpmpma71uk/dogfood_test.spy:70:9
    |
 70 |         case Rectangle() as r:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpmpma71uk/dogfood_test.spy:75:9
    |
 75 |         case _:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpmpma71uk/dogfood_test.spy:78:1
    |
 78 | def check_container(obj: Container) -> str:
    | ^
    |


**Feature Focus:** match_type_binding
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Tests: pattern matching with binding, guard clauses, and classification
class Value:
    data: float

    def __init__(self, data: float):
        self.data = data

class Container:
    items: list[int]

    def __init__(self, items: list[int]):
        self.items = items

class Text:
    content: str

    def __init__(self, content: str):
        self.content = content

class Shape:
    pass

class Circle(Shape):
    radius: float

    def __init__(self, radius: float):
        self.radius = radius

class Rectangle(Shape):
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

def classify_value(value: float) -> str:
    match value:
        case n if n == 0.0:
            return "zero"
        case n if n > 0.0 and n < 10.0:
            return "small_positive"
        case n if n >= 10.0 and n < 100.0:
            return "medium_positive"
        case n if n >= 100.0:
            return "large_positive"
        case n if n < 0.0:
            return "negative"
        case _:
            return "unknown"

def analyze_number(value: float, target: float) -> str:
    match value:
        case n if abs(n - target) < 0.001:
            return "exact_match"
        case n if n > target:
            return "above_target"
        case n if n < target:
            return "below_target"
        case _:
            return "unknown"

def classify_shape(shape: Shape) -> str:
    match shape:
        case Circle() as c:
            r: float = c.radius
            if r > 10.0:
                return "large_circle"
            return "small_circle"
        case Rectangle() as r:
            area: float = r.width * r.height
            if area > 100.0:
                return "large_rectangle"
            return "small_rectangle"
        case _:
            return "unknown_shape"

def check_container(obj: Container) -> str:
    count: int = len(obj.items)
    match count:
        case n if n == 0:
            return "empty"
        case n if n == 1:
            return "single"
        case n if n > 1 and n <= 5:
            return "few"
        case n if n > 5:
            return "many"
        case _:
            return "unknown"

def process_value(val: Value) -> str:
    d: float = val.data
    match d:
        case n if n > 0.0:
            result: str = classify_value(n)
            return "positive_{result}"
        case n if n < 0.0:
            return "negative_value"
        case n if n == 0.0:
            return "zero_value"
        case _:
            return "invalid"

def analyze_text(text: Text) -> str:
    content: str = text.content
    length: int = len(content)
    match length:
        case n if n == 0:
            return "empty_string"
        case n if n > 0 and n <= 5:
            return "short"
        case n if n > 5 and n <= 20:
            return "medium"
        case n if n > 20:
            return "long"
        case _:
            return "unknown"

def compare_values(a: float, b: float) -> str:
    match a:
        case x if x > b:
            return "first_larger"
        case x if x < b:
            return "second_larger"
        case x if x == b:
            return "equal"
        case _:
            return "incomparable"

def main():
    # Test 1: Basic value classification
    print(classify_value(0.0))
    print(classify_value(5.0))
    print(classify_value(50.0))
    print(classify_value(200.0))
    print(classify_value(-3.0))

    # Test 2: Number analysis with guards
    print(analyze_number(42.0, 42.0))
    print(analyze_number(50.0, 40.0))
    print(analyze_number(30.0, 40.0))

    # Test 3: Shape classification
    c1: Circle = Circle(5.0)
    c2: Circle = Circle(20.0)
    r1: Rectangle = Rectangle(2.0, 3.0)
    r2: Rectangle = Rectangle(10.0, 20.0)
    print(classify_shape(c1))
    print(classify_shape(c2))
    print(classify_shape(r1))
    print(classify_shape(r2))

    # Test 4: Container checks
    empty_items: list[int] = []
    empty: Container = Container(empty_items)
    single: Container = Container([42])
    few: Container = Container([1, 2, 3])
    many: Container = Container([1, 2, 3, 4, 5, 6, 7, 8])
    print(check_container(empty))
    print(check_container(single))
    print(check_container(few))
    print(check_container(many))

    # Test 5: Value processing
    v1: Value = Value(7.0)
    v2: Value = Value(-5.0)
    v3: Value = Value(0.0)
    print(process_value(v1))
    print(process_value(v2))
    print(process_value(v3))

    # Test 6: Text analysis
    t1: Text = Text("")
    t2: Text = Text("hello")
    t3: Text = Text("hello world test")
    t4: Text = Text("this is a very long string for testing")
    print(analyze_text(t1))
    print(analyze_text(t2))
    print(analyze_text(t3))
    print(analyze_text(t4))

    # Test 7: Value comparison
    print(compare_values(10.0, 5.0))
    print(compare_values(3.0, 8.0))
    print(compare_values(7.0, 7.0))

# EXPECTED OUTPUT:
# zero
# small_positive
# medium_positive
# large_positive
# negative
# exact_match
# above_target
# below_target
# small_circle
# large_circle
# small_rectangle
# large_rectangle
# empty
# single
# few
# many
# positive_small_positive
# negative_value
# zero_value
# empty_string
# short
# medium
# long
# first_larger
# second_larger
# equal
```

## Timing

- Generation: 1372.65s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
