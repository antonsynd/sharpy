# Issue Report: execution_failed

**Timestamp:** 2026-01-18T13:19:12.981253
**Type:** execution_failed
**Feature Focus:** enum_usage
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test enum usage with basic value access and comparison
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

def get_color_value(c: Color) -> int:
    return c

# Create and compare enum values
primary: Color = Color.RED
secondary: Color = Color.BLUE

print(get_color_value(primary))
print(get_color_value(secondary))

if primary == Color.RED:
    print(1)
else:
    print(0)

# EXPECTED OUTPUT:
# 1
# 3
# 1
```

## Error

```
Compilation failed:
  Semantic error at line 8, column 5: Cannot return type 'Color' from function expecting 'int'

```

## Timing

- Generation: 3.49s
- Execution: 0.84s
