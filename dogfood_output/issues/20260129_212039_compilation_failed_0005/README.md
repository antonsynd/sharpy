# Issue Report: compilation_failed

**Timestamp:** 2026-01-29T21:20:21.188147
**Type:** compilation_failed
**Feature Focus:** dotnet_import
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Basic .NET interop - import and use System.Math
from system import Math

def main():
    x = Math.Abs(-42)
    print(x)
    
    y = Math.Max(10, 25)
    print(y)
    
    z = Math.Min(7, 3)
    print(z)

# EXPECTED OUTPUT:
# 42
# 25
# 3
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(14,21): error CS0234: The type or namespace name 'Abs' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(16,21): error CS0234: The type or namespace name 'Max' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(18,21): error CS0234: The type or namespace name 'Min' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)

```

## Timing

- Generation: 6.78s
- Execution: 1.36s
