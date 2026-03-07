# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-06T21:32:26.932591
**Type:** internal_compiler_error
**Feature Focus:** partial_application
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test partial application with bound methods and lambda-based operator sections
class ValueAdder:
    offset: int

    def __init__(self, offset: int):
        self.offset = offset

    def add(self, value: int) -> int:
        return self.offset + value

def main():
    adder = ValueAdder(100)

    # Partial application: create a function that adds 100 to any value
    # Using lambda since operator sections with _ in boolean expressions have limitations
    add_hundred = lambda x: adder.add(x)

    # Lambda-based operator section: check if value is within range [100, 150]
    in_range = lambda v: 100 <= v and v <= 150

    # Test filtering with partial application-like functions
    values: list[int] = [25, 50, 55, 60]
    accepted: list[int] = []

    for v in values:
        result = add_hundred(v)
        if in_range(result):
            accepted.append(v)

    print(accepted)
    print(add_hundred(42))
    print(in_range(120))
    print(in_range(200))

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'in_range()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp5c6kn8fi/dogfood_test.spy:27:12
    |
 27 |         if in_range(result):
    |            ^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'in_range()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp5c6kn8fi/dogfood_test.spy:33:11
    |
 33 |     print(in_range(200))
    |           ^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'in_range()' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp5c6kn8fi/dogfood_test.spy:32:11
    |
 32 |     print(in_range(120))
    |           ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 257.04s
