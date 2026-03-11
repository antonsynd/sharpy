# Skipped Dogfood Run

**Timestamp:** 2026-03-10T16:50:02.703105
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Less
  --> /tmp/tmp31uoyzxq/dogfood_test.spy:39:1
    |
 39 | </expected>
    | ^
    |


**Feature Focus:** spread_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def sum_three(a: int, b: int, c: int) -> int:
    return a + b + c

def sum_variadic(*args: int) -> int:
    total: int = 0
    for x in args:
        total = total + x
    return total

def main():
    nums: list[int] = [1, 2, 3]

    # Call fixed-params function directly with individual args
    result1: int = sum_three(nums[0], nums[1], nums[2])
    print(result1)

    # Spread list into variadic function
    result2: int = sum_variadic(*nums)
    print(result2)

    # Tuple spread into variadic
    coords: tuple[int, int, int] = (10, 20, 30)
    result3: int = sum_variadic(*coords)
    print(result3)

    # Multiple spreads in one call
    first: list[int] = [1, 2]
    second: list[int] = [3, 4]
    result4: int = sum_variadic(*first, *second)
    print(result4)

    # Mixed spread and individual args
    result5: int = sum_variadic(0, *first, 5)
    print(result5)

    # Spread from list literal (variadic)
    result6: int = sum_variadic(*[10, 20, 30])
    print(result6)
</expected>

6
6
60
10
8
60

```

## Timing

- Generation: 277.84s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
