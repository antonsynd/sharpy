# Issue Report: output_mismatch

**Timestamp:** 2026-03-06T18:55:58.027894
**Type:** output_mismatch
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex module imports and usage

from types_module import Status, Priority, Point, Dimension, get_status_name, get_priority_value
from interfaces_module import ITrackable, IMeasurable, create_default_point
from base_classes import WorkItem, Shape

class Task(WorkItem):
    _priority: Priority

    def __init__(self, id: int, name: str, priority: Priority):
        super().__init__(id, name)
        self._priority = priority

    @override
    def get_name(self) -> str:
        return f"Task: {self._name}"

    @override
    def execute(self) -> bool:
        self.set_status(Status.RUNNING)
        return True

class Rectangle(Shape):
    _label: str

    def __init__(self, width: int, height: int, label: str):
        super().__init__(Dimension(width, height))
        self._label = label

    @override
    def get_name(self) -> str:
        return f"Rectangle[{self._label}]"

def process_trackable(t: ITrackable) -> None:
    print(t.get_id())
    print(get_status_name(t.get_status()))

def main():
    # Test enum imports and usage
    print(Status.PENDING.value)
    print(get_priority_value(Priority.HIGH))

    # Test struct imports and usage
    p = Point(3.0, 4.0)
    print(p.distance_from_origin())

    default_point = create_default_point()
    print(default_point.x)
    print(default_point.y)

    # Test Dimension struct
    dim = Dimension(10, 20)
    print(dim.area())

    # Test cross-module inheritance with WorkItem
    task = Task(101, "TestTask", Priority.MEDIUM)
    print(task.get_name())
    process_trackable(task)

    # Test cross-module inheritance with Shape
    rect = Rectangle(5, 8, "Box")
    print(rect.get_name())
    print(rect.get_area())

    # Test interface polymorphism
    measurable: IMeasurable = rect
    print(measurable.get_area())

    # Test method execution
    print(task.execute())
    print(get_status_name(task.get_status()))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
0
10
5.0
0.0
0.0
200
Task: TestTask
101
Running
Rectangle[Box]
40
40
True
Running

```

### Actual
```
0
10
5.0
0.0
0.0
200
Task: TestTask
101
Pending
Rectangle[Box]
40
40
True
Running
```

## Timing

- Generation: 149.39s
- Execution: 4.64s
