# Issue Report: compilation_failed

**Timestamp:** 2026-01-26T23:52:57.039413
**Type:** compilation_failed
**Feature Focus:** collection_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Collection methods - list operations with add, remove, contains
class TodoList:
    items: list[str]

    def __init__(self):
        self.items = []

    def add_task(self, task: str) -> None:
        self.items.append(task)

    def task_count(self) -> int:
        return len(self.items)

def main():
    todos = TodoList()
    todos.add_task("Write code")
    todos.add_task("Run tests")
    print(todos.task_count())
    print(todos.items[0])

# EXPECTED OUTPUT:
# 2
# Write code
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(37,26): error CS0029: Cannot implicitly convert type 'System.Collections.Generic.List<object>' to 'System.Collections.Generic.List<string>'

```

## Timing

- Generation: 7.08s
- Execution: 1.37s
