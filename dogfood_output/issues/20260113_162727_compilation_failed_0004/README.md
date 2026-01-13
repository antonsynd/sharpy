# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:27:15.771599
**Type:** compilation_failed
**Feature Focus:** arithmetic_operators
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test basic arithmetic operators
a: int = 15
b: int = 4

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

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 4.74s
- Execution: 1.17s
