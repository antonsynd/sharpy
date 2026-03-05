# Successful Dogfood Run

**Timestamp:** 2026-03-04T19:38:31.803175
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### math_utils.spy

```python
# Math utilities module

PI: float = 3.14159

def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

```

### string_utils.spy

```python
# String utilities module

GREETING: str = "Hello"

def reverse(s: str) -> str:
    result: str = ""
    i: int = len(s) - 1
    while i >= 0:
        result = result + str(s[i])
        i = i - 1
    return result

def is_palindrome(s: str) -> bool:
    return s == reverse(s)

def count_vowels(s: str) -> int:
    count: int = 0
    for c in s:
        ch: str = str(c).lower()
        if ch == "a" or ch == "e" or ch == "i" or ch == "o" or ch == "u":
            count = count + 1
    return count

```

### data_types.spy

```python
# Data types module

class Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"

    def distance_from_origin(self) -> float:
        return (self.x * self.x + self.y * self.y) ** 0.5

class Rectangle:
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    def area(self) -> float:
        return self.width * self.height

    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

def create_default_point() -> Point:
    return Point(0.0, 0.0)

```

### main.spy

```python
# Main entry point - demonstrates complex module imports

from math_utils import square, cube, factorial, PI
from string_utils import reverse, is_palindrome, count_vowels, GREETING
from data_types import Point, Rectangle, create_default_point

def main():
    # Test math_utils imports
    print("Math tests:")
    print(square(5))
    print(cube(3))
    print(factorial(5))
    print(PI)
    
    # Test string_utils imports
    print("\nString tests:")
    test_str: str = "radar"
    print(reverse(test_str))
    print(is_palindrome(test_str))
    print(is_palindrome("hello"))
    print(count_vowels("Hello World"))
    print(GREETING)
    
    # Test data_types imports
    print("\nData type tests:")
    p1: Point = Point(3.0, 4.0)
    print(p1)
    print(p1.distance_from_origin())
    
    p2: Point = create_default_point()
    print(p2)
    
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.area())
    print(rect.perimeter())

```

## Timing

- Generation: 256.10s
- Execution: 5.03s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
