# Skipped Dogfood Run

**Timestamp:** 2026-03-06T20:48:33.841423
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0102]: Expected newline, got Case
  --> /tmp/tmp79sxwo11/dogfood_test.spy:112:33
     |
 112 |     result: str = match status: case TaskStatus.PENDING: "wait" case TaskStatus.RUNNING: "go" case _: "done"
     |                                 ^^^^
     |


**Feature Focus:** match_with_enum
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Advanced enum pattern matching using if/elif/else chains
# Tests: Enum comparisons, exhaustiveness via if chains, combining enums with control flow

enum TaskStatus:
    PENDING = 0
    RUNNING = 1
    COMPLETED = 2
    FAILED = 3

enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

class Task:
    name: str
    status: TaskStatus
    priority: Priority

    def __init__(self, name: str, status: TaskStatus, priority: Priority):
        self.name = name
        self.status = status
        self.priority = priority

def get_action(status: TaskStatus) -> str:
    if status == TaskStatus.PENDING:
        return "queue"
    elif status == TaskStatus.RUNNING:
        return "monitor"
    elif status == TaskStatus.COMPLETED:
        return "archive"
    else:
        return "recover"

def priority_label(p: Priority) -> str:
    if p == Priority.LOW:
        return "low"
    elif p == Priority.MEDIUM:
        return "medium"
    else:
        return "high"

def is_active(s: TaskStatus) -> bool:
    if s == TaskStatus.PENDING or s == TaskStatus.RUNNING:
        return True
    else:
        return False

def status_from_int(n: int) -> TaskStatus:
    if n == 0:
        return TaskStatus.PENDING
    elif n == 1:
        return TaskStatus.RUNNING
    elif n == 2:
        return TaskStatus.COMPLETED
    else:
        return TaskStatus.FAILED

def priority_from_int(n: int) -> Priority:
    if n == 1:
        return Priority.LOW
    elif n == 2:
        return Priority.MEDIUM
    else:
        return Priority.HIGH

def status_name(s: TaskStatus) -> str:
    if s == TaskStatus.PENDING:
        return "pending"
    elif s == TaskStatus.RUNNING:
        return "running"
    elif s == TaskStatus.COMPLETED:
        return "completed"
    else:
        return "failed"

def priority_name(p: Priority) -> str:
    if p == Priority.LOW:
        return "low"
    elif p == Priority.MEDIUM:
        return "medium"
    else:
        return "high"

def main():
    # Build list incrementally
    tasks: list[Task] = []
    tasks.append(Task("Backup", TaskStatus.PENDING, Priority.HIGH))
    tasks.append(Task("Tests", TaskStatus.RUNNING, Priority.MEDIUM))
    tasks.append(Task("Review", TaskStatus.COMPLETED, Priority.LOW))
    tasks.append(Task("Scan", TaskStatus.FAILED, Priority.HIGH))

    active_count: int = 0
    for task in tasks:
        action: str = get_action(task.status)
        label: str = priority_label(task.priority)
        if is_active(task.status):
            active_count += 1
        print(f"{task.name}:{action}:{label}")

    print(active_count)

    # Test enum iteration
    for s in TaskStatus:
        print(s.name)

    for p in Priority:
        print(p.value)

    # Test single-line match expression for simple case
    status: TaskStatus = TaskStatus.PENDING
    result: str = match status: case TaskStatus.PENDING: "wait" case TaskStatus.RUNNING: "go" case _: "done"
    print(result)

    # Test status_from_int function
    s1: TaskStatus = status_from_int(0)
    print(status_name(s1))

    # Test priority_label with all values
    print(priority_label(Priority.LOW))
    print(priority_label(Priority.MEDIUM))
    print(priority_label(Priority.HIGH))

```

## Timing

- Generation: 888.82s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
