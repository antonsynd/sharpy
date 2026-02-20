# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T05:10:11.513403
**Type:** output_mismatch
**Feature Focus:** interface_definition
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Interface definition with multiple implementations in a collection
# Tests polymorphic behavior through interface types

interface IMeasurable:
    def total_length(self) -> int: ...

class Rectangle(IMeasurable):
    width: int
    height: int
    
    def __init__(self, w: int, h: int):
        self.width = w
        self.height = h
    
    @override
    def total_length(self) -> int:
        return 2 * (self.width + self.height)

class Triangle(IMeasurable):
    side: int
    
    def __init__(self, s: int):
        self.side = s
    
    @override
    def total_length(self) -> int:
        return 3 * self.side

def sum_perimeters(items: list[IMeasurable]) -> int:
    total: int = 0
    for shape in items:
        total += shape.total_length()
    return total

def main():
    shapes: list[IMeasurable] = []
    shapes.append(Rectangle(3, 4))
    shapes.append(Triangle(5))
    shapes.append(Rectangle(2, 6))
    
    result: int = sum_perimeters(shapes)
    print(result)
    print(len(shapes))
    
    r: Rectangle = Rectangle(10, 20)
    print(r.total_length())
    
    t: Triangle = Triangle(7)
    print(t.total_length())

# EXPECTED OUTPUT:
# 44
# 3
# 60
# 21
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
44
3
60
21

```

### Actual
```
45
3
60
21
```

## Timing

- Generation: 111.04s
- Execution: 4.47s
