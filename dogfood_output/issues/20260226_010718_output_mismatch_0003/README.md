# Issue Report: output_mismatch

**Timestamp:** 2026-02-26T01:03:43.871741
**Type:** output_mismatch
**Feature Focus:** star_unpacking
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test star unpacking patterns in various contexts
# Uses first/*rest, *prefix/last, and head/*middle/tail patterns

class TaskSplitter:
    _priorities: list[int]
    _backlog: list[int]
    
    def __init__(self, tasks: list[int]):
        # Split: first is priority, rest is backlog
        priority, *backlog_items = tasks
        self._priorities = [priority]
        self._backlog = backlog_items
    
    def distribute(self, extra: list[int]) -> list[int]:
        # Split extra into head, middle, tail
        head, *middle, tail = extra
        # Combine using spread and build result
        result: list[int] = [*self._priorities, head]
        for x in middle:
            result = result + [x]
        return result + [tail, *self._backlog]

def main():
    tasks: list[int] = [100, 20, 30, 40, 50]
    splitter = TaskSplitter(tasks)
    
    # Test middle-star pattern with distribute
    combined: list[int] = splitter.distribute([5, 6, 7, 8])
    
    # Various star unpacking patterns
    first, *rest = combined
    *prefix, last = combined
    
    print(first)
    print(last)
    print(len(rest))
    print(len(prefix))
    print(len(combined))
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
100
50
5
5
7

```

### Actual
```
100
50
8
8
9
```

## Timing

- Generation: 124.67s
- Execution: 4.49s
