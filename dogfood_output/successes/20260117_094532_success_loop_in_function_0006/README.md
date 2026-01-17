# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:45:16.472517
**Feature Focus:** loop_in_function
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: loop inside a function
def sum_range(n: int) -> int:
    total: int = 0
    for i in range(n):
        total += i
    return total

result = sum_range(5)
print(result)

result2 = sum_range(10)
print(result2)

# EXPECTED OUTPUT:
# 10
# 45
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_e7051752c0b747a0953caf80a0dc9397.exe

=== Running Program ===

10
45
```

## Timing

- Generation: 4.60s
- Execution: 1.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
