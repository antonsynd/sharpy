# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:00:44.161876
**Feature Focus:** class_with_init
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test class initialization with multiple fields and basic operations
class Rectangle:
    width: int
    height: int
    
    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h
    
    def area(self) -> int:
        return self.width * self.height

r = Rectangle(5, 3)
print(r.width)
print(r.height)
print(r.area())

# EXPECTED OUTPUT:
# 5
# 3
# 15
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_3513be54f4124c33b9e76c6a21eda1fd.exe

=== Running Program ===

5
3
15
```

## Timing

- Generation: 2.89s
- Execution: 1.28s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
