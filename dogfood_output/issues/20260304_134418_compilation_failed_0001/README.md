# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T13:39:47.451287
**Type:** compilation_failed
**Feature Focus:** generator_reversed_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Generator-based __reversed__ with priority queue pattern
# A TaskQueue that yields tasks by priority (high to low) normally,
# and in reverse priority order (low to high) when reversed

struct Task:
    name: str
    priority: int

class TaskQueue:
    _tasks: list[Task]

    def __init__(self):
        self._tasks = []

    def add(self, name: str, priority: int) -> None:
        self._tasks.append(Task(name, priority))

    def __iter__(self) -> Task:
        # Yield tasks sorted by priority (highest first)
        p: int = 10
        while p >= 1:
            for task in self._tasks:
                if task.priority == p:
                    yield task
            p -= 1

    def __reversed__(self) -> Task:
        # Yield tasks sorted by priority (lowest first)
        p: int = 1
        while p <= 10:
            for task in self._tasks:
                if task.priority == p:
                    yield task
            p += 1

def main():
    queue = TaskQueue()
    queue.add("critical_bug", 10)
    queue.add("feature_request", 3)
    queue.add("documentation", 2)
    queue.add("performance", 8)
    queue.add("cleanup", 1)

    print("=== HIGH to LOW priority ===")
    for t in queue:
        print(t.name)

    print("=== LOW to HIGH priority ===")
    for t in reversed(queue):
        print(t.name)

```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DogfoodTest.Task' does not contain a constructor that takes 2 arguments
  --> /tmp/tmplw7hmek1/dogfood_test.spy:16:36
    |
 16 |         self._tasks.append(Task(name, priority))
    |                                    ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmplw7hmek1/dogfood_test.cs

```

## Timing

- Generation: 257.08s
- Execution: 4.66s
