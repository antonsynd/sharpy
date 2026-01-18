# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:18:08.474149
**Feature Focus:** struct_definition
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test struct definition with value semantics (copy on assignment)

struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def move(self, dx: int, dy: int) -> None:
        self.x += dx
        self.y += dy

p1 = Point(10, 20)
p2 = p1
p2.move(5, 5)

print(p1.x)
print(p1.y)
print(p2.x)
print(p2.y)

# EXPECTED OUTPUT:
# 10
# 20
# 15
# 25
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_02ac3f50d45049a08766d939da5520cb.exe

=== Running Program ===

10
20
15
25
```

## Timing

- Generation: 4.17s
- Execution: 1.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
