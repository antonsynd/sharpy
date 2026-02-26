# Skipped Dogfood Run

**Timestamp:** 2026-02-26T00:06:11.661959
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0287]: Cannot access parent fields via super(); only methods are allowed
  --> /tmp/tmpnzuoyxhf/dogfood_test.spy:45:26
    |
 45 |         parent_id: int = super().entity_id
    |                          ^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** class_field_access
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex class field access test covering:
# - Field shadowing in inheritance hierarchy
# - Protected property access across class boundaries
# - Static instance tracking
# - Virtual method accessing shadowed fields
# - Field access through super() and self
# - Property shadowing base class field

@abstract
class BaseEntity:
    entity_id: int
    _counter: int

    def __init__(self, id: int):
        self.entity_id = id
        self._counter = id * 10

    @virtual
    def describe(self) -> str:
        return f"Base:{self.entity_id}"

    @protected
    def get_counter(self) -> int:
        return self._counter

class DataContainer:
    value: int
    metadata: str

    def __init__(self, val: int, meta: str):
        self.value = val
        self.metadata = meta

class DerivedEntity(BaseEntity):
    entity_id: str
    extra_data: DataContainer

    def __init__(self, id: int, label: str):
        super().__init__(id)
        self.entity_id = label
        self.extra_data = DataContainer(id * 100, label)

    @override
    def describe(self) -> str:
        parent_id: int = super().entity_id
        return f"Derived:{self.entity_id}({parent_id})"

# Static tracker class to avoid global keyword
class StatusTracker:
    _status: str

    def __init__(self):
        self._status = "inactive"

    def get_status(self) -> str:
        return self._status

    def set_status(self, status: str) -> None:
        self._status = status

def main():
    print("Creating entities...")

    # Create derived entity
    derived = DerivedEntity(42, "TestLabel")

    # Test shadowed field access
    print(f"Derived entity_id (str): {derived.entity_id}")

    # Get base entity's entity_id via describe
    desc: str = derived.describe()
    print(f"Description: {desc}")

    # Test protected method access
    counter: int = derived.get_counter()
    print(f"Protected counter: {counter}")

    # Test extra_data field access
    print(f"Extra data value: {derived.extra_data.value}")
    print(f"Extra data metadata: {derived.extra_data.metadata}")

    # Test static tracker class instead of global
    tracker: StatusTracker = StatusTracker()
    print(f"Initial module status: {tracker.get_status()}")
    tracker.set_status("active")
    print(f"Updated module status: {tracker.get_status()}")

    # Create another instance to verify instance isolation
    derived2 = DerivedEntity(100, "Second")
    print(f"First derived counter: {derived.get_counter()}")
    print(f"Second derived counter: {derived2.get_counter()}")
    print(f"Second description: {derived2.describe()}")

    # Test direct field shadowing resolution
    print(f"Describe on instance 1: {derived.describe()}")
    print(f"Describe on instance 2: {derived2.describe()}")
```

## Timing

- Generation: 517.46s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
