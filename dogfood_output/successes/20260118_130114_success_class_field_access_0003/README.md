# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:00:58.390699
**Feature Focus:** class_field_access
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test class field access with multiple fields and computed properties

class Rectangle:
    width: int
    height: int
    label: str

    def __init__(self, w: int, h: int, name: str):
        self.width = w
        self.height = h
        self.label = name

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * self.width + 2 * self.height

    def scale(self, factor: int) -> None:
        self.width *= factor
        self.height *= factor

class Point:
    x: int
    y: int

    def __init__(self, px: int, py: int):
        self.x = px
        self.y = py

    def distance_from_origin(self) -> int:
        # Simplified: using abs for Manhattan distance
        return self.x + self.y if self.x >= 0 and self.y >= 0 else 0

rect = Rectangle(5, 3, "MyRect")
print(rect.width)
print(rect.height)
print(rect.area())
print(rect.perimeter())

rect.scale(2)
print(rect.width)
print(rect.height)
print(rect.area())

point = Point(4, 6)
print(point.x)
print(point.y)
print(point.distance_from_origin())

# EXPECTED OUTPUT:
# 5
# 3
# 15
# 16
# 10
# 6
# 60
# 4
# 6
# 10
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_37ff892917ff447d98b6948b70718bca.exe

=== Running Program ===

5
3
15
16
10
6
60
4
6
10
```

## Timing

- Generation: 5.13s
- Execution: 1.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
