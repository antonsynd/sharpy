# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:42:07.074034
**Feature Focus:** abstract_class
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Abstract class with abstract method
@abstract
class Shape:
    @abstract
    def area(self) -> int:
        ...

# Concrete implementation
class Square(Shape):
    side: int

    def __init__(self, s: int):
        self.side = s

    @override
    def area(self) -> int:
        return self.side * self.side

sq = Square(5)
print(sq.area())

# EXPECTED OUTPUT:
# 25
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_8ac5459b4d6141be9de52eb1c03e3004.exe

=== Running Program ===

25
```

## Timing

- Generation: 4.87s
- Execution: 1.31s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
