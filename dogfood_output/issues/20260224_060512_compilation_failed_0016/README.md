# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T05:56:49.953970
**Type:** compilation_failed
**Feature Focus:** named_tuple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type Bounds = tuple[min: float, max: float]
type Config[T, U] = tuple[key: T, value: U, count: int]

@abstract
class DataAnalyzer[T]:
    @abstract
    def analyze(self, data: list[T]) -> Bounds: ...

class NumericAnalyzer(DataAnalyzer[int]):
    offset: float
    
    def __init__(self, offset: float):
        self.offset = offset
    
    @override
    def analyze(self, data: list[int]) -> Bounds:
        if len(data) == 0:
            return (min=0.0 + self.offset, max=0.0 + self.offset)
        min_val: float = float(data[0])
        max_val: float = float(data[0])
        for item in data:
            f: float = float(item)
            if f < min_val:
                min_val = f
            if f > max_val:
                max_val = f
        return (min=min_val + self.offset, max=max_val + self.offset)

type Point = tuple[x: int, y: int, z: int]

class PointCluster:
    points: list[Point]
    
    def __init__(self, points: list[Point]):
        self.points = points
    
    def bounding_box(self) -> tuple[left: Point, right: Point]:
        if len(self.points) == 0:
            return (left=(x=0, y=0, z=0), right=(x=0, y=0, z=0))
        p: Point = self.points[0]
        min_x: int = p.x
        max_x: int = p.x
        for point in self.points:
            if point.x < min_x:
                min_x = point.x
            if point.x > max_x:
                max_x = point.x
        return (left=(x=min_x, y=p.y, z=p.z), right=(x=max_x, y=p.y, z=p.z))

def main():
    analyzer: NumericAnalyzer = NumericAnalyzer(10.0)
    data: list[int] = [5, 2, 8, 1, 9]
    bounds: Bounds = analyzer.analyze(data)
    print(bounds.min)
    print(bounds.max)
    
    config: Config[str, int] = (key="items", value=42, count=3)
    if config.count > 0:
        print(config.value)
    
    items: list[int] = [10, 20, 30]
    configs: list[Config[str, int]] = []
    i: int = 0
    for val in items:
        configs.append((key=f"item_{i}", value=val, count=i))
        i += 1
    
    for cfg in configs:
        updated: Config[str, int] = (key=cfg.key, value=cfg.value + 1, count=cfg.count)
        print(updated.value)
    
    cluster: PointCluster = PointCluster([(x=1, y=2, z=3), (x=5, y=2, z=3), (x=3, y=2, z=3)])
    box: tuple[left: Point, right: Point] = cluster.bounding_box()
    print(box.left.x)
    print(box.right.x)

# EXPECTED OUTPUT:
# 11.0
# 19.0
# 42
# 11
# 21
# 31
# 1
# 5
```

## Error

```
Assembly compilation failed:

error[CS1061]: '(string, int, int)' does not contain a definition for 'Count' and no accessible extension method 'Count' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:58:20
    |
 58 |     if config.count > 0:
    |                    ^
    |

error[CS1061]: '(string, int, int)' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:59:50
    |
 59 |         print(config.value)
    |                            ^
    |

error[CS1061]: '(string, int, int)' does not contain a definition for 'Key' and no accessible extension method 'Key' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:69:69
    |
 69 |         updated: Config[str, int] = (key=cfg.key, value=cfg.value + 1, count=cfg.count)
    |                                                                     ^
    |

error[CS1061]: '(string, int, int)' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:69:85
    |
 69 |         updated: Config[str, int] = (key=cfg.key, value=cfg.value + 1, count=cfg.count)
    |                                                                                     ^
    |

error[CS1061]: '(string, int, int)' does not contain a definition for 'Count' and no accessible extension method 'Count' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:69:107
    |
 69 |         updated: Config[str, int] = (key=cfg.key, value=cfg.value + 1, count=cfg.count)
    |                                                                                        ^
    |

error[CS1061]: '(string, int, int)' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type '(string, int, int)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp39yucdjb/dogfood_test.spy:70:51
    |
 70 |         print(updated.value)
    |                             ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp39yucdjb/dogfood_test.cs

```

## Timing

- Generation: 478.56s
- Execution: 4.59s
