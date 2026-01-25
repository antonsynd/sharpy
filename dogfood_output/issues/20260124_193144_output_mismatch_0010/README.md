# Issue Report: output_mismatch

**Timestamp:** 2026-01-24T18:38:53.637971
**Type:** output_mismatch
**Feature Focus:** enum_usage
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test enum usage with status codes and conditionals

enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2
    FAILED = 3

class Task:
    status: Status

    def __init__(self, initial_status: Status):
        self.status = initial_status

    def get_status(self) -> Status:
        return self.status

def main():
    task = Task(Status.PENDING)
    print(task.get_status())
    
    task.status = Status.ACTIVE
    print(task.get_status())
    
    task.status = Status.COMPLETED
    print(task.get_status())

# EXPECTED OUTPUT:
# Status.PENDING
# Status.ACTIVE
# Status.COMPLETED
```

## Output Comparison

### Expected
```
Status.PENDING
Status.ACTIVE
Status.COMPLETED

```

### Actual
```
Pending
Active
Completed
```

## Timing

- Generation: 11.27s
- Execution: 3.81s
