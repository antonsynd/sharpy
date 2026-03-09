# Skipped Dogfood Run

**Timestamp:** 2026-03-08T18:43:24.963901
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'Product' has no member 'display_name'
  --> /tmp/tmpml9_jwn3/main.spy:16:11
    |
 16 |     print(prod.display_name)
    |           ^^^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'Product' has no member 'id'
  --> /tmp/tmpml9_jwn3/main.spy:17:11
    |
 17 |     print(prod.id)
    |           ^^^^^^^
    |

error[SPY0203]: Type 'Product' has no member 'status'. Did you mean '_status'?
  --> /tmp/tmpml9_jwn3/main.spy:24:25
    |
 24 |     print(format_status(prod.status))
    |                         ^^^^^^^^^^^
    |

error[SPY0203]: Type 'Product' has no member 'status'. Did you mean '_status'?
  --> /tmp/tmpml9_jwn3/main.spy:25:21
    |
 25 |     print(is_active(prod.status))
    |                     ^^^^^^^^^^^
    |

error[SPY0203]: Type 'Service' has no member 'status'. Did you mean '_status'?
  --> /tmp/tmpml9_jwn3/main.spy:33:25
    |
 33 |     print(format_status(svc.status))
    |                         ^^^^^^^^^^
    |


**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Module defining shared types
# Interfaces, enums, and structs for entity system

interface Identifiable:
    property get id() -> int
    property get display_name() -> str

enum Status:
    ACTIVE = 1
    INACTIVE = 2
    PENDING = 3

struct Dimensions:
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    def area(self) -> float:
        return self.width * self.height

```

### models.spy

```python
# Entity models with inheritance and interface implementation

from types import Identifiable, Status, Dimensions

@abstract
class Entity(Identifiable):
    _id: int
    _name: str
    _status: Status

    def __init__(self, id: int, name: str):
        self._id = id
        self._name = name
        self._status = Status.PENDING

    @virtual
    def get_description(self) -> str:
        return f"Entity({self._id})"

    property get id() -> int:
        return self._id

    property get display_name() -> str:
        return self._name

    property get status() -> Status:
        return self._status


class Product(Entity):
    _dims: Dimensions
    _price: float

    def __init__(self, id: int, name: str, width: float, height: float, price: float):
        super().__init__(id, name)
        self._dims = Dimensions(width, height)
        self._price = price
        self._status = Status.ACTIVE

    @override
    def get_description(self) -> str:
        return f"Product({self._name})"


class Service(Entity):
    _duration: int

    def __init__(self, id: int, name: str, duration: int):
        super().__init__(id, name)
        self._duration = duration

    @override
    def get_description(self) -> str:
        return f"Service({self._name}, {self._duration}min)"

```

### utils.spy

```python
# Utility functions for entity processing

from types import Status, Dimensions

def format_status(s: Status) -> str:
    return f"[{s.name}]"

def is_active(status: Status) -> bool:
    return status == Status.ACTIVE

def scale_dimensions(dim: Dimensions, factor: float) -> Dimensions:
    return Dimensions(dim.width * factor, dim.height * factor)

```

### main.spy

```python
# Main entry point
# Demonstrates cross-module imports and usage

from types import Status, Dimensions
from models import Entity, Product, Service
from utils import format_status, is_active, scale_dimensions

def main():
    # Create product
    prod: Product = Product(1, "Widget", 10.0, 5.0, 99.99)

    # Create service
    svc: Service = Service(2, "Consultation", 60)

    # Test interface implementation
    print(prod.display_name)
    print(prod.id)

    # Test polymorphic method dispatch
    print(prod.get_description())
    print(svc.get_description())

    # Test status enum across modules
    print(format_status(prod.status))
    print(is_active(prod.status))

    # Test struct operations
    small: Dimensions = Dimensions(4.0, 3.0)
    large: Dimensions = scale_dimensions(small, 2.0)
    print(large.area())

    # Test service status
    print(format_status(svc.status))

    # Test name property on enum
    status: Status = Status.ACTIVE
    print(status.name)

```

## Timing

- Generation: 346.63s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
