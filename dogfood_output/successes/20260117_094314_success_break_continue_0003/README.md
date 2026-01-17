# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:42:49.766487
**Feature Focus:** break_continue
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Testing break and continue in loops

def find_first_multiple(target: int, limit: int) -> int:
    result: int = -1
    i: int = 1
    while i <= limit:
        if i % target == 0:
            result = i
            break
        i += 1
    return result

def sum_odd_numbers(max_val: int) -> int:
    total: int = 0
    for i in range(1, max_val + 1):
        if i % 2 == 0:
            continue
        total += i
    return total

# Test break: find first multiple of 7 up to 50
first_mult: int = find_first_multiple(7, 50)
print(first_mult)

# Test continue: sum odd numbers from 1 to 10
odd_sum: int = sum_odd_numbers(10)
print(odd_sum)

# Nested loop with break
outer: int = 0
while outer < 5:
    inner: int = 0
    while inner < 5:
        if inner == 3:
            break
        inner += 1
    print(inner)
    outer += 1

# EXPECTED OUTPUT:
# 7
# 25
# 3
# 3
# 3
# 3
# 3
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_4cf6be5e7ca6498f9055c0cd6fdd0409.exe

=== Running Program ===

7
25
3
3
3
3
3
```

## Timing

- Generation: 7.17s
- Execution: 1.38s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
