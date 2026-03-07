# Successful Dogfood Run

**Timestamp:** 2026-03-06T17:54:12.861052
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### models.spy

```python
# Module providing base models, interfaces, and enums
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

interface IIdentifiable:
    def get_id(self) -> str: ...

@abstract
class BaseEntity:
    _name: str

    def __init__(self, name: str):
        self._name = name

    @virtual
    def get_display_name(self) -> str:
        return self._name

    @abstract
    def validate(self) -> bool: ...

def get_module_info() -> str:
    return "Models module loaded"

```

### types.spy

```python
# Module providing data structures and type utilities
struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

class Calculator:
    @static
    def scale(value: float, factor: float) -> float:
        return value * factor

def format_id(prefix: str, value: int) -> str:
    return f"{prefix}-{value}"

```

### services.spy

```python
# Services module demonstrating cross-module inheritance and interface implementation
from models import BaseEntity, Status, IIdentifiable
from types import Point

class UserService(BaseEntity, IIdentifiable):
    _user_id: int
    _status: Status

    def __init__(self, name: str, user_id: int):
        super().__init__(name)
        self._user_id = user_id
        self._status = Status.PENDING

    def get_id(self) -> str:
        return f"{self._name}-{self._user_id}"

    @override
    def validate(self) -> bool:
        return self._user_id > 0

    def get_status(self) -> Status:
        return self._status

class ProductService(BaseEntity, IIdentifiable):
    _product_id: int

    def __init__(self, name: str, product_id: int):
        super().__init__(name)
        self._product_id = product_id

    def get_id(self) -> str:
        return f"{self._name}-{self._product_id}"

    @override
    def validate(self) -> bool:
        return self._product_id > 100

```

### main.spy

```python
# Main entry point demonstrating complex cross-module imports and polymorphism
from models import BaseEntity, Status, IIdentifiable, get_module_info
from types import Point, Calculator, format_id
from services import UserService, ProductService

def main():
    # Test 1: Module function import
    info: str = get_module_info()
    print(info)

    # Test 2: Cross-module inheritance + interface implementation
    user: UserService = UserService("User", 100)
    print(user.get_id())

    # Test 3: Method access for status (using getter method instead of property)
    current_status: Status = user.get_status()
    print(current_status)

    # Test 4: Another service class
    product: ProductService = ProductService("Widget", 500)
    print(product.get_id())

    # Test 5: Struct creation and field access
    point: Point = Point(10.0, 20.0)
    print(point.x)

    # Test 6: Static method from another module
    scaled: float = Calculator.scale(5.0, 2.0)
    print(scaled)

    # Test 7-9: Polymorphism through base class references
    entities: list[BaseEntity] = [user, product]
    entity: BaseEntity
    for entity in entities:
        result: bool = entity.validate()
        print(result)

```

## Timing

- Generation: 658.06s
- Execution: 4.56s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
