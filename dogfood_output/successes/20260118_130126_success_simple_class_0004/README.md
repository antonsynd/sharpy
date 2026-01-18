# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:01:14.184008
**Feature Focus:** simple_class
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Simple class with fields and methods
class Rectangle:
    width: int
    height: int

    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

rect = Rectangle(5, 3)
print(rect.area())
print(rect.perimeter())

# EXPECTED OUTPUT:
# 15
# 16
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_01b10f32e3894bd4968a636e79da87b6.exe

=== Running Program ===

15
16
```

## Timing

- Generation: 2.66s
- Execution: 1.29s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
