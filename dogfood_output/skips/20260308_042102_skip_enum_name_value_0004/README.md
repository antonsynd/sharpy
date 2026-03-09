# Skipped Dogfood Run

**Timestamp:** 2026-03-08T04:16:59.193142
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpl28i1ell/dogfood_test.spy:72:5
    |
 72 |     total_value = 0
    |     ^^^^^^^^^^^
    |


**Feature Focus:** enum_name_value
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Enum name/value properties in a task scheduling system
# Combines: enums, inheritance, virtual/override, type aliases
type TaskId = int

enum Priority:
    LOW = 1
    MEDIUM = 5
    HIGH = 10
    CRITICAL = 20

@abstract
class Task:
    id: TaskId
    name: str

    def __init__(self, id: TaskId, name: str):
        self.id = id
        self.name = name

    @virtual
    def compute_priority(self) -> Priority:
        return Priority.MEDIUM

    @virtual
    def describe(self) -> str:
        return f"Task({self.id})"

@abstract
class ScheduledTask(Task):
    scheduled_hour: int

    def __init__(self, id: TaskId, name: str, hour: int):
        super().__init__(id, name)
        self.scheduled_hour = hour

    @override
    def compute_priority(self) -> Priority:
        if self.scheduled_hour < 12:
            return Priority.HIGH
        return Priority.MEDIUM

    @override
    def describe(self) -> str:
        return f"Scheduled({self.id} at {self.scheduled_hour}:00)"

class AutomatedTask(ScheduledTask):
    system_name: str

    def __init__(self, id: TaskId, name: str, hour: int, system: str):
        super().__init__(id, name, hour)
        self.system_name = system

    @override
    def compute_priority(self) -> Priority:
        base = super().compute_priority()
        if self.system_name == "CORE":
            return Priority.CRITICAL
        return base

def format_task_info(task: Task) -> str:
    p = task.compute_priority()
    return f"{task.describe()}: {task.name} [{p.name}={p.value}]"

def main():
    tasks: list[Task] = [
        Task(TaskId(1), "backup"),
        ScheduledTask(TaskId(2), "report", 9),
        AutomatedTask(TaskId(3), "cleanup", 8, "CORE"),
        AutomatedTask(TaskId(4), "sync", 14, "AUX")
    ]

    total_value = 0
    high_priority_count = 0

    for t in tasks:
        info = format_task_info(t)
        print(info)
        p = t.compute_priority()
        total_value = total_value + p.value
        if p.value >= 10:
            high_priority_count = high_priority_count + 1

    print(f"Total priority value: {total_value}")
    print(f"High priority tasks: {high_priority_count}")

    all_priorities: list[str] = []
    for p in Priority:
        all_priorities.append(p.name)
    print(f"Priority levels: {len(all_priorities)}")

```

## Timing

- Generation: 226.82s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
