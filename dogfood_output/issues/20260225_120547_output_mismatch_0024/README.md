# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T12:00:52.967651
**Type:** output_mismatch
**Feature Focus:** spread_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Function call spreading with variadic and fixed parameters
# Tests: Spread operator (*) in function calls with tuples allowed, list spread forbidden

def sum_three(a: int, b: int, c: int) -> int:
    return a + b + c

def product(a: int, b: int) -> int:
    return a * b

def compute(a: int, b: int, c: int, d: int) -> int:
    return (a + b) * (c + d)

def main():
    triple: tuple[int, int, int] = (1, 2, 3)
    result1 = sum_three(*triple)
    print(result1)

    pair: tuple[int, int] = (4, 5)
    result2 = sum_three(10, *pair)
    print(result2)

    values: list[int] = [2, 3]
    result3 = product(values[0], values[1])
    print(result3)

    left: tuple[int, int] = (1, 2)
    right: tuple[int, int] = (3, 4)
    result4 = compute(*left, *right)
    print(result4)

    nums: list[int] = [5, 6]
    # product(*nums) is forbidden - use element access instead
    intermediate: tuple[int, int, int] = (sum_three(*triple), product(nums[0], nums[1]), 7)
    result5 = sum_three(*intermediate)
    print(result5)

# EXPECTED OUTPUT:
# 6
# 19
# 6
# 12
# 43
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
6
19
6
12
43

```

### Actual
```
6
19
6
21
43
```

## Timing

- Generation: 200.71s
- Execution: 4.51s
