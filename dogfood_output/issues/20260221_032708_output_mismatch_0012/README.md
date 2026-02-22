# Issue Report: output_mismatch

**Timestamp:** 2026-02-21T03:25:38.592391
**Type:** output_mismatch
**Feature Focus:** class_with_init
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Property Initialization in __init__

class Rectangle:
    width: float
    height: float
    property area: float
    
    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h
        self.area = w * h
    
    def scale(self, factor: float) -> None:
        self.width *= factor
        self.height *= factor
        self.area = self.width * self.height

def main():
    r = Rectangle(3.0, 4.0)
    print(r.width)
    print(r.height)
    print(r.area)
    r.scale(2.0)
    print(r.area)

# EXPECTED OUTPUT:
# 3.0
# 4.0
# 12.0
# 24.0
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
3.0
4.0
12.0
24.0

```

### Actual
```
3.0
4.0
12.0
48.0
```

## Timing

- Generation: 63.93s
- Execution: 4.78s
