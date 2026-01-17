# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:47:38.184002
**Feature Focus:** arithmetic_operators
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test arithmetic operators with int variables
a: int = 15
b: int = 4

# Basic arithmetic
sum_result: int = a + b
diff_result: int = a - b
prod_result: int = a * b
div_result: int = a // b
mod_result: int = a % b

print(sum_result)
print(diff_result)
print(prod_result)
print(div_result)
print(mod_result)

# EXPECTED OUTPUT:
# 19
# 11
# 60
# 3
# 3
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_2ba24ad0707f4483b716b4ff18c7ba9c.exe

=== Running Program ===

19
11
60
3
3
```

## Timing

- Generation: 4.43s
- Execution: 1.38s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
