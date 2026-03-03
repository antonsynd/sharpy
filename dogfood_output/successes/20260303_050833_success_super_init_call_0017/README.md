# Successful Dogfood Run

**Timestamp:** 2026-03-03T05:07:03.145775
**Feature Focus:** super_init_call
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: super().__init__() with conditional updates in subclass
class Tool:
    _name: str
    _status: str

    def __init__(self, name: str, status: str = "ready"):
        self._name = name
        self._status = status

class Equipment(Tool):
    _location: str
    _maintenance_due: bool

    def __init__(self, name: str, status: str, location: str, hours_used: int = 0):
        super().__init__(name, status)
        self._location = location
        # Conditional based on usage hours
        self._maintenance_due = hours_used > 100
        if self._maintenance_due:
            self._status = "maintenance_required"

    def report(self) -> str:
        if self._maintenance_due:
            return f"{self._name} at {self._location} needs service"
        return f"{self._name} at {self._location} is {self._status}"

def main():
    # Equipment with low usage - status from parent constructor preserved
    drill = Equipment("Drill", "operational", "Workshop-A", 45)
    print(drill.report())

    # Equipment with high usage - status overridden by child logic
    lathe = Equipment("Lathe", "operational", "Workshop-B", 150)
    print(lathe.report())

    # Equipment using default status
    saw = Equipment("Saw", "standby", "Workshop-C")
    print(saw.report())

```

## Output

```
Drill at Workshop-A is operational
Lathe at Workshop-B needs service
Saw at Workshop-C is standby
```

## Timing

- Generation: 79.29s
- Execution: 4.77s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
