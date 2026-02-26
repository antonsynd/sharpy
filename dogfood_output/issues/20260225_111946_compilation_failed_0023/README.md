# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T10:54:46.814130
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - integrates types, entities, and data structures
from core_types import Status, Priority, IIdentifiable
from entities import Entity, Person, Task
from data_structs import Point, Config, calculate_score

def process_identifiable(item: IIdentifiable, label: str) -> str:
    # Using method call instead of property access
    return f"{label} #{item.get_id()}"

def main():
    # Create Point struct
    p: Point = Point(3.0, 4.0)
    print(p.distance_from_origin())

    # Create Config struct with default priority
    cfg: Config = Config("test")
    print(cfg.enabled)
    print(cfg.priority)

    # Create Person (inherits Entity, implements IIdentifiable)
    person: Person = Person("Alice", 1001, 30)
    print(person.display())
    print(process_identifiable(person, "Person"))
    print(person.describe())

    # Create Task (inherits Entity, implements IIdentifiable)
    task: Task = Task("Review code", 2001, 5)
    print(task.describe())
    print(task.display())

    # Test status inheritance
    print(task.get_status_text())

    # Test priority calculation
    score: int = calculate_score(10, Priority.HIGH)
    print(score)

# EXPECTED OUTPUT:
# 5.0
# True
# 10
# Person: Alice
# Person #1001
# Person #1001: Alice
# Task #2001: Review code
# Task: Review code (priority 5)
# person is pending
# 100
```

## Error

```
Assembly compilation failed:

error[CS0019]: Operator '*' cannot be applied to operands of type 'int' and 'CoreTypes.Priority'
  --> /tmp/tmpw2e1pi83/data_structs.spy:29:16
    |
 29 |     print(task.display())
    |                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmpw2e1pi83/main.spy:2:24
    |
  2 | from core_types import Status, Priority, IIdentifiable
    |                        ^^^^^^
    |

warning[SPY0452]: Imported name 'Entity' is never used
  --> /tmp/tmpw2e1pi83/main.spy:3:22
    |
  3 | from entities import Entity, Person, Task
    |                      ^^^^^^
    |


```

## Timing

- Generation: 1457.74s
- Execution: 4.35s
