# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T05:53:50.687651
**Type:** compilation_failed
**Feature Focus:** struct_definition
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Simple struct definition and usage
# Structs are value types (copied on assignment)

struct Point2D:
    x: float
    y: float
    
    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5

def main():
    p: Point2D = Point2D(3.0, 4.0)
    print(p.x)
    print(p.y)
    print(p.distance_from_origin())
    # EXPECTED OUTPUT:
    # 3.0
    # 4.0
    # 5.0
```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DogfoodTest.Point2D' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpnq4da47d/dogfood_test.spy:12:25
    |
 12 |     p: Point2D = Point2D(3.0, 4.0)
    |                         ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpnq4da47d/dogfood_test.cs

```

## Timing

- Generation: 20.67s
- Execution: 4.47s
