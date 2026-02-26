# Skipped Dogfood Run

**Timestamp:** 2026-02-26T01:43:54.600855
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0230]: 'Some' must be called as a function, e.g. 'Some(value)'
  --> /tmp/tmpg8nstyfq/dogfood_test.spy:16:35
    |
 16 |                 self.best_score = Some(score)
    |                                   ^^^^
    |


**Feature Focus:** optional_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class ScoreTracker:
    best_score: int?
    attempts: int
    
    def __init__(self):
        self.best_score = None()
        self.attempts = 0
    
    def record(self, score: int) -> None:
        self.attempts += 1
        if self.best_score is None:
            self.best_score = Some(score)
        else:
            current: int = self.best_score.unwrap()
            if score > current:
                self.best_score = Some(score)
    
    def get_best(self) -> int:
        if self.best_score is not None:
            return self.best_score.unwrap()
        return 0

def main():
    tracker = ScoreTracker()
    result: int? = tracker.best_score
    if result is not None:
        print(result.unwrap())
    else:
        print(-1)
    tracker.record(15)
    print(tracker.get_best())
    print(tracker.attempts)
    tracker.record(25)
    tracker.record(10)
    print(tracker.get_best())
    print(tracker.attempts)
```

## Timing

- Generation: 232.98s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
