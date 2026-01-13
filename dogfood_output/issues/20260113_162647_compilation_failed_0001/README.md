# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:26:34.626852
**Type:** compilation_failed
**Feature Focus:** if_else_simple
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Simple if-else branching
x: int = 15
y: int = 10

if x > y:
    print("x is greater")
else:
    print("y is greater or equal")

result: int = 0
if x == 15:
    result = 100
else:
    result = 200

print(result)

# EXPECTED OUTPUT:
# x is greater
# 100
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 4.29s
- Execution: 1.17s
