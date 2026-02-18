# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T20:34:52.302325
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules

from entities import Point, Person, Employee
from interfaces import ConsoleLogger, TimestampLogger
from services import DataProcessor, Task, Status

def main():
    # Test struct import and usage
    p1: Point = Point(3.0, 4.0)
    dist: float = p1.distance_from_origin()
    print(f"Distance: {dist:.1f}")
    
    # Test class inheritance across imports
    emp: Employee = Employee("Alice", 30, 1001, "Engineering")
    print(emp.greet())
    print(emp.describe())
    
    # Test enum import and usage
    task: Task = Task("Implement feature", Point(0.0, 0.0))
    print(task.get_summary())
    task.mark_complete()
    print(task.get_summary())
    
    # Test service class with internal logic
    processor: DataProcessor = DataProcessor()
    processor.add_item(10)
    processor.add_item(20)
    processor.add_item(30)
    avg: float = processor.get_average()
    print(f"Average: {avg:.1f}")
    
    # Test logger implementations
    logger1: ConsoleLogger = ConsoleLogger("APP")
    logger2: TimestampLogger = TimestampLogger("SYSTEM")
    logger1.log("Application started")
    logger2.log("System check complete")

# EXPECTED OUTPUT:
# Distance: 5.0
# Hello, I'm Alice!
# Alice, age 30 (Employee #1001 in Engineering)
# Implement feature [Pending]
# Implement feature [Completed]
# Average: 20.0
# [APP] Application started
# (SYSTEM) System check complete
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'Max' does not exist in the current context
  --> /tmp/tmp1lhim5t_/services.spy:51:20


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Person' is never used
  --> /tmp/tmp1lhim5t_/main.spy:3:29
    |
  3 | from entities import Point, Person, Employee
    |                             ^^^^^^
    |

warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmp1lhim5t_/main.spy:5:43
    |
  5 | from services import DataProcessor, Task, Status
    |                                           ^^^^^^
    |


```

## Timing

- Generation: 184.34s
- Execution: 4.45s
