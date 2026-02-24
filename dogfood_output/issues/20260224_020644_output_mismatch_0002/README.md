# Issue Report: output_mismatch

**Timestamp:** 2026-02-24T01:59:28.591026
**Type:** output_mismatch
**Feature Focus:** dunder_eq_hash
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
class Point2D:
    x_pos: float
    y_pos: float

    def __init__(self, x: float, y: float):
        self.x_pos = x
        self.y_pos = y

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Point2D):
            return False
        p: Point2D = other as Point2D
        return self.x_pos == p.x_pos and self.y_pos == p.y_pos

    def __hash__(self) -> int:
        return int(self.x_pos * 1000) + int(self.y_pos)

class LabeledPoint(Point2D):
    tag: str

    def __init__(self, x: float, y: float, label: str):
        super().__init__(x, y)
        self.tag = label

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, LabeledPoint):
            return False
        lp: LabeledPoint = other as LabeledPoint
        return (super().__eq__(lp) and self.tag == lp.tag)

    @override
    def __hash__(self) -> int:
        base: int = super().__hash__()
        extra: int = 0
        idx: int = 0
        for c in self.tag:
            extra += (ord(c) * (idx + 1))
            idx += 1
        return base + extra

class HashAnalyzer:
    def __init__(self):
        pass

    def check_collision(self, p1: Point2D, p2: Point2D) -> bool:
        return hash(p1) == hash(p2) and not (p1 == p2)

def main():
    p1: Point2D = Point2D(3.0, 4.0)
    p2: Point2D = Point2D(3.0, 4.0)
    p3: Point2D = Point2D(4.0, 3.0)
    print(p1 == p2)
    print(p1 == p3)
    print(hash(p1))
    print(hash(p2))

    lp1: LabeledPoint = LabeledPoint(1.0, 2.0, "alpha")
    lp2: LabeledPoint = LabeledPoint(1.0, 2.0, "beta")
    print(lp1 == lp2)
    print(hash(lp1) == hash(lp2))
    points: list[Point2D] = [p1, p3, lp1, lp2]
    hashes: list[int] = []
    for pt in points:
        hashes.append(hash(pt))
        print(hash(pt))

    analyzer: HashAnalyzer = HashAnalyzer()
    print(analyzer.check_collision(p1, p2))
    print(analyzer.check_collision(lp1, lp2))

# EXPECTED OUTPUT:
# True
# False
# 3004
# 3004
# False
# False
# 3004
# 4003
# 1097
# 1126
# False
# False
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
True
False
3004
3004
False
False
3004
4003
1097
1126
False
False

```

### Actual
```
True
False
3004
3004
False
False
3004
4003
2552
2038
False
False
```

## Timing

- Generation: 287.20s
- Execution: 4.71s
