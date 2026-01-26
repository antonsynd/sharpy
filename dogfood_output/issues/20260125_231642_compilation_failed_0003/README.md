# Issue Report: compilation_failed

**Timestamp:** 2026-01-25T23:16:19.464039
**Type:** compilation_failed
**Feature Focus:** enum_usage
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Enum-based state machine with polymorphic behavior
# Tests: enum definition, enum comparison, enum as class fields, virtual/override with enum switching

enum TaskStatus:
    PENDING = 0
    IN_PROGRESS = 1
    COMPLETED = 2
    FAILED = 3

enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3
    CRITICAL = 4

@abstract
class WorkItem:
    status: TaskStatus
    priority: Priority
    name: str

    def __init__(self, name: str, priority: Priority):
        self.name = name
        self.priority = priority
        self.status = TaskStatus.PENDING

    @virtual
    def start(self) -> None:
        self.status = TaskStatus.IN_PROGRESS

    @virtual
    def complete(self) -> None:
        self.status = TaskStatus.COMPLETED

    @abstract
    def get_type(self) -> str:
        ...

class Bug(WorkItem):
    severity: int

    def __init__(self, name: str, priority: Priority, severity: int):
        super().__init__(name, priority)
        self.severity = severity

    @override
    def get_type(self) -> str:
        return "Bug"

class Feature(WorkItem):
    estimated_hours: int

    def __init__(self, name: str, priority: Priority, hours: int):
        super().__init__(name, priority)
        self.estimated_hours = hours

    @override
    def get_type(self) -> str:
        return "Feature"

def process_items(items: list[WorkItem]) -> None:
    for item in items:
        print(item.get_type())
        print(item.status)
        
        if item.priority == Priority.CRITICAL:
            item.start()
            item.complete()
        elif item.priority == Priority.HIGH:
            item.start()
        
        print(item.status)

def main():
    items: list[WorkItem] = []
    
    bug: Bug = Bug("Crash on login", Priority.CRITICAL, 5)
    feature: Feature = Feature("Dark mode", Priority.HIGH, 20)
    task: Bug = Bug("Typo in docs", Priority.LOW, 1)
    
    print(bug.status)
    bug.start()
    print(bug.status)
    bug.complete()
    print(bug.status)
    
    process_items([bug, feature])

# EXPECTED OUTPUT:
# Pending
# InProgress
# Completed
# Bug
# Completed
# Completed
# Feature
# Pending
# InProgress
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(46,26): error CS1503: Argument 1: cannot convert from 'System.Collections.Generic.List<object>' to 'System.Collections.Generic.List<Sharpy.DogfoodTest.DogfoodTest.WorkItem>'

```

## Timing

- Generation: 11.11s
- Execution: 1.28s
