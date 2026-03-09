# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-08T18:21:49.928349
**Type:** internal_compiler_error
**Feature Focus:** lambda_multiarg
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex multi-argument lambda test with higher-order functions,
# class methods, and currying patterns
# Uses type inference instead of explicit generic type arguments

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __str__(self) -> str:
        return f"({self.x}, {self.y})"

class GeometryUtils:
    @static
    def sort_points(points: list[Point], compare: (Point, Point) -> int) -> list[Point]:
        result: list[Point] = []
        for p in points:
            result.append(p)

        # Simple bubble sort using the comparator lambda
        i = 0
        while i < len(result):
            j = i + 1
            while j < len(result):
                if compare(result[j], result[i]) < 0:
                    temp = result[i]
                    result[i] = result[j]
                    result[j] = temp
                j += 1
            i += 1
        return result

    @static
    def fold_int(items: list[int], initial: int, combine: (int, int) -> int) -> int:
        accumulator = initial
        for item in items:
            accumulator = combine(accumulator, item)
        return accumulator

    @static
    def fold_float(items: list[float], initial: float, combine: (float, float) -> float) -> float:
        accumulator = initial
        for item in items:
            accumulator = combine(accumulator, item)
        return accumulator

    @static
    def curry_add() -> (float) -> (float) -> float:
        # Returns a lambda that takes x, returns another lambda taking y
        return lambda x: lambda y: x + y

class Rectangle:
    width: float
    height: float

    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h

    def get_area(self) -> float:
        return self.width * self.height

    def get_perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

    @virtual
    def compare_with(self, other: Rectangle) -> int:
        # Multi-arg lambda inside method for local calculation
        diff_calc = lambda a, b: (a - b) * 100.0
        diff = diff_calc(self.get_area(), other.get_area())
        if diff > 0.0:
            return 1
        elif diff < 0.0:
            return -1
        return 0

def make_multiplier(factor: float) -> (float, float) -> float:
    # Returns a 2-arg lambda capturing factor from outer scope
    return lambda x, y: (x * y) * factor

def main():
    # Test 1: Sort points by distance from origin using 2-arg comparison lambda
    points: list[Point] = [Point(3.0, 4.0), Point(0.0, 1.0), Point(2.0, 2.0), Point(5.0, 0.0)]

    # Compare by sum of coordinates (Manhattan-ish sort)
    sorted_points = GeometryUtils.sort_points(points, lambda p1, p2: int((p1.x + p1.y) - (p2.x + p2.y)))
    for p in sorted_points:
        print(p)

    # Test 2: Fold (reduce) with 2-arg accumulator lambda using concrete methods
    nums: list[int] = [1, 2, 3, 4, 5]
    sum_result = GeometryUtils.fold_int(nums, 0, lambda acc, n: acc + n)
    print(sum_result)
    product_result = GeometryUtils.fold_int(nums, 1, lambda acc, n: acc * n)
    print(product_result)

    # Test 3: Float fold
    floats: list[float] = [1.0, 2.0, 3.0, 4.0]
    float_sum = GeometryUtils.fold_float(floats, 0.0, lambda acc, n: acc + n)
    print(float_sum)

    # Test 4: Currying - create a partial application factory
    add_five_factory = GeometryUtils.curry_add()
    add_five = add_five_factory(5.0)
    print(add_five(10.0))

    # Test 5: Lambda capturing outer scope
    triple_mult = make_multiplier(3.0)
    print(triple_mult(4.0, 5.0))

    # Test 6: Rectangle comparison using internal lambda
    r1 = Rectangle(4.0, 3.0)
    r2 = Rectangle(2.0, 2.0)
    r3 = Rectangle(5.0, 5.0)
    print(r1.compare_with(r2))
    print(r2.compare_with(r3))

    # Test 7: Nested multi-arg lambdas with closure
    outer_capture = 100
    complex_calc = lambda a, b: outer_capture + (lambda x, y: x * y)(a, b)
    print(complex_calc(3, 4))

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'diff_calc()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpi14qiu56/dogfood_test.spy:73:16
    |
 73 |         diff = diff_calc(self.get_area(), other.get_area())
    |                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'complex_calc()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpi14qiu56/dogfood_test.spy:124:11
     |
 124 |     print(complex_calc(3, 4))
     |           ^^^^^^^^^^^^^^^^^^
     |


```

## Timing

- Generation: 336.79s
