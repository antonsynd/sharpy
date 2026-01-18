# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:01:46.900944
**Feature Focus:** class_static_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test static methods in a simple Math utility class
class MathUtils:
    def square(x: int) -> int:
        return x * x

    def add_three(a: int, b: int, c: int) -> int:
        return a + b + c

result1: int = MathUtils.square(7)
print(result1)

result2: int = MathUtils.add_three(2, 5, 8)
print(result2)

# EXPECTED OUTPUT:
# 49
# 15
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_57d829aed0764a71ab093e5df6462ae1.exe

=== Running Program ===

49
15
```

## Timing

- Generation: 3.11s
- Execution: 1.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
