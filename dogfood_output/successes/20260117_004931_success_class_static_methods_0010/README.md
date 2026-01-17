# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:49:13.307754
**Feature Focus:** class_static_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test static methods in a class (methods without self parameter)
class MathUtils:
    # Static method - no self parameter
    def square(n: int) -> int:
        return n * n
    
    # Another static method
    def add_ten(n: int) -> int:
        return n + 10

# Call static methods on the class
result1 = MathUtils.square(5)
print(result1)

result2 = MathUtils.add_ten(7)
print(result2)

result3 = MathUtils.square(3)
print(result3)

# EXPECTED OUTPUT:
# 25
# 17
# 9
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_c48a4254e4ef4930b1ccb9d07c5bdd1c.exe

=== Running Program ===

25
17
9
```

## Timing

- Generation: 5.16s
- Execution: 1.32s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
