# Issue Report: internal_compiler_error

**Timestamp:** 2026-02-24T03:30:15.670163
**Type:** internal_compiler_error
**Feature Focus:** generator_reversed_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class ScoreTracker:
    scores: list[int]

    def __init__(self):
        self.scores = []

    def record(self, points: int) -> None:
        self.scores.append(points)

    def __iter__(self) -> int:
        for i in range(len(self.scores)):
            yield self.scores[i]

    def __reversed__(self) -> int:
        for i in range(len(self.scores) - 1, -1, -1):
            yield self.scores[i]

def main():
    tracker = ScoreTracker()
    tracker.record(100)
    tracker.record(200)
    tracker.record(300)
    print("oldest_first")
    for score in tracker:
        print(score)
    print("newest_first")
    for score in reversed[int](tracker):
        print(score)
# EXPECTED OUTPUT:
# oldest_first
# 100
# 200
# 300
# newest_first
# 300
# 200
# 100
```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmphq4yth1b/dogfood_test.spy:27:18
    |
 27 |     for score in reversed[int](tracker):
    |                  ^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 383.51s
