# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T07:56:52.567631
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module complex features
from types_module import Status, Priority, IIdentifiable, ITrackable
from data_module import Task, FinalizedTask, Point2D, create_task, create_point
from utils_module import apply_n_times, format_status_message, parse_priority, get_even_numbers, safe_get_id, MAX_RETRY_COUNT

def main():
    # Test 1: Enum usage across modules
    print("=== Test 1: Enum Values ===")
    my_status: Status = Status.ACTIVE
    print(format_status_message(my_status))
    print(format_status_message(Status.COMPLETED, "Current"))

    # Test 2: Result type handling
    print("")
    print("=== Test 2: Result Type ===")
    high_result: Priority !str = parse_priority("HIGH")
    if high_result.is_ok():
        print("Parsed HIGH")
    invalid_result: Priority !str = parse_priority("INVALID")
    if invalid_result.is_err():
        print("Invalid parse detected")

    # Test 3: Higher-order function with lambda
    print("")
    print("=== Test 3: Higher-Order Functions ===")
    base: int = 5
    double: (int) -> int = lambda x: x * 2
    squared: int = apply_n_times(base, double, 2)
    print(f"Double applied twice: {squared}")

    # Test 4: Struct creation and usage
    print("")
    print("=== Test 4: Struct Operations ===")
    origin: Point2D = create_point(3.0, 4.0)
    mag: int = int(origin.magnitude())
    print(f"Point magnitude: {mag}")

    # Test 5: Class with interface implementation
    print("")
    print("=== Test 5: Interface Implementation ===")
    task1: Task = create_task(1, "Sample Task")
    ident: IIdentifiable = task1
    trackable: ITrackable = task1
    trackable.set_status(Status.ACTIVE)
    task_id: int = ident.get_id()
    print(f"ID: {task_id}")
    print(f"Name: {ident.get_name()}")

    # Test 6: Inheritance and virtual methods
    print("")
    print("=== Test 6: Inheritance Chain ===")
    # Create Task directly
    task2: Task = Task(2, "Inheritance Test")
    print(task2.describe())

    # FinalizedTask extends Task
    final: FinalizedTask = FinalizedTask(3, "Completed Work", "2024-01-15")
    print(final.describe())

    # Test 7: Nullable types and safe access
    print("")
    print("=== Test 7: Nullable Types ===")
    maybe_task: Task? = Some(task1)
    safe_id: int = safe_get_id(maybe_task)
    print(f"Safe ID: {safe_id}")
    null_task: Task? = None()
    null_id: int = safe_get_id(null_task)
    print(f"Null safe ID: {null_id}")

    # Test 8: List comprehension via utility
    print("")
    print("=== Test 8: List Operations ===")
    evens: list[int] = get_even_numbers(10)
    i: int = 0
    while i < len(evens):
        if i < 3:
            print(f"Even: {evens[i]}")
        i += 1

    # Test 9: Constants access
    print("")
    print("=== Test 9: Module Constants ===")
    print(f"Retry count: {MAX_RETRY_COUNT}")

    print("")
    print("=== All Tests Complete ===")

# EXPECTED OUTPUT:
# === Test 1: Enum Values ===
# Status: Active
# Current: Completed
# 
# === Test 2: Result Type ===
# Parsed HIGH
# Invalid parse detected
# 
# === Test 3: Higher-Order Functions ===
# Double applied twice: 20
# 
# === Test 4: Struct Operations ===
# Point magnitude: 5
# 
# === Test 5: Interface Implementation ===
# ID: 1
# Name: Sample Task
# 
# === Test 6: Inheritance Chain ===
# Task: Inheritance Test
# Completed Task: Completed Work
# 
# === Test 7: Nullable Types ===
# Safe ID: 1
# Null safe ID: -1
# 
# === Test 8: List Operations ===
# Even: 0
# Even: 2
# Even: 4
# 
# === Test 9: Module Constants ===
# Retry count: 5
# 
# === All Tests Complete ===
```

## Error

```
Assembly compilation failed:

error[CS0037]: Cannot convert null to 'Optional<int>' because it is a non-nullable value type
  --> /tmp/tmp1t1rm5af/data_module.spy:29:29
    |
 29 |     print(f"Double applied twice: {squared}")
    |                             ^
    |

error[CS1503]: Argument 1: cannot convert from 'Sharpy.Optional<DataModule.Task>' to 'Sharpy.Optional<TypesModule.IIdentifiable>'
  --> /tmp/tmp1t1rm5af/main.spy:64:32
    |
 64 |     safe_id: int = safe_get_id(maybe_task)
    |                                ^
    |

error[CS1503]: Argument 1: cannot convert from 'Sharpy.Optional<DataModule.Task>' to 'Sharpy.Optional<TypesModule.IIdentifiable>'
  --> /tmp/tmp1t1rm5af/main.spy:67:32
    |
 67 |     null_id: int = safe_get_id(null_task)
    |                                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point2D' is never used
  --> /tmp/tmp1t1rm5af/utils_module.spy:2:1
    |
  2 | from types_module import Status, Priority, IIdentifiable, ITrackable
    | ^^^^^^^
    |


```

## Timing

- Generation: 510.71s
- Execution: 4.57s
