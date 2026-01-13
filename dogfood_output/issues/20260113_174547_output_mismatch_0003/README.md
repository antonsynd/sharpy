# Issue Report: output_mismatch

**Timestamp:** 2026-01-13T17:45:16.549932
**Type:** output_mismatch
**Feature Focus:** comparison_operators
**Complexity:** medium
**Backend:** copilot

## Generated Sharpy Code

```python
# Comparison operators test - testing all comparison operations
def check_equality(a: int, b: int) -> bool:
    if a == b:
        print(1)
        return True
    else:
        print(0)
        return False


def check_relations(x: int, y: int) -> int:
    result: int = 0
    
    if x < y:
        result = result + 1
    
    if x <= y:
        result = result + 10
    
    if x > y:
        result = result + 100
    
    if x >= y:
        result = result + 1000
    
    if x != y:
        result = result + 10000
    
    return result


a: int = 5
b: int = 10

print(a)
print(b)

check_equality(a, b)
check_equality(a, a)

code: int = check_relations(a, b)
print(code)

code2: int = check_relations(b, a)
print(code2)

# EXPECTED OUTPUT:
# 5
# 10
# 0
# 1
# 10011
# 10100
```

## Output Comparison

### Expected
```
5
10
0
1
10011
10100
```

### Actual
```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_6391eddbf0a544339d59625137d2e179.exe

=== Running Program ===

5
10
0
1
10011
11100
```

## Timing

- Generation: 8.88s
- Execution: 1.35s
