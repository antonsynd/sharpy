# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T01:30:00.163033
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point: orchestrates cross-module types
from types import Status, Point, ADMIN_ID
from entities import User, Task, Worker
from utils import status_to_string, format_point, distance_between, check_status_message

def process_entity(e: Entity) -> str:
    return e.describe()

def check_status(t: Task) -> str:
    status: Status = t.get_status()
    return status_to_string(status)

def get_display_info(u: User) -> str:
    return f"ID={u.get_id()}, name={u.get_display_name()}"

def main():
    # Create instances
    alice: Worker = Worker(101, "alice", "alice@example.com")
    bob: User = User(102, "bob", "bob@example.com")
    task: Task = Task(1, "Deploy", Point(10.0, 20.0))

    # Test polymorphic describe via Entity base class
    print(process_entity(alice))
    print(process_entity(bob))
    print(process_entity(task))

    # Test display info
    print(get_display_info(alice))
    print(get_display_info(bob))

    # Test status tracking
    print(check_status(task))
    print(check_status_message(alice.get_status_enum()))

    # Test Point formatting and distance
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    print(format_point(p1))
    print(format_point(p2))
    dist: float = distance_between(p1, p2)
    print(dist)

    # Simulate workflow
    alice.assign_task(task)
    print(check_status(task))
    print(check_status_message(alice.get_status_enum()))

    alice.complete_task()
    print(check_status_message(alice.get_status_enum()))
    print(alice.get_completed_count())

    # Test constants
    print(ADMIN_ID)
```

## Error

```
Assembly compilation failed:

error[CS1929]: 'Entities.Task' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpzod8lewu/entities.spy:76:38

error[CS0131]: The left-hand side of an assignment must be a variable, property or indexer
  --> /tmp/tmpzod8lewu/entities.spy:79:17

error[CS0149]: Method name expected
  --> /tmp/tmpzod8lewu/entities.spy:79:46


```

## Timing

- Generation: 788.38s
- Execution: 4.40s
