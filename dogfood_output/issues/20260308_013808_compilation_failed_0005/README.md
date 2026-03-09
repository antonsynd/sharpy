# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T01:31:22.981939
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from utils import Status, Box, Empty, factorial, fibonacci, reverse_string, is_palindrome, sum_list, find_max, gcd, lcm

def main():
    # Test enum
    print(Status.OK)
    print(Status.PENDING)
    print(Status.ERROR)

    # Test generic containers
    box: Box[int] = Box[int](42)
    print(box.is_present())
    empty: Empty[str] = Empty[str]()
    print(empty.is_present())

    # Test factorial
    print(factorial(5))
    print(factorial(0))

    # Test fibonacci
    print(fibonacci(10))
    print(fibonacci(5))

    # Test string utilities
    print(reverse_string("hello"))
    print(is_palindrome("radar"))
    print(is_palindrome("hello"))

    # Test list utilities
    nums: list[int] = [5, 2, 8, 1, 9, 3]
    print(sum_list(nums))
    print(find_max(nums))

    # Test math utilities
    print(gcd(48, 18))
    print(lcm(4, 6))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Utils.Box<T>' does not contain a definition for '_Value' and no accessible extension method '_Value' accepting a first argument of type 'Utils.Box<T>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpehops7fr/utils.spy:24:25
    |
 24 |     print(reverse_string("hello"))
    |                         ^
    |


```

## Timing

- Generation: 391.44s
- Execution: 5.06s
