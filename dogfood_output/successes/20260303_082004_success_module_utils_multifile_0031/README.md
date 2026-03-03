# Successful Dogfood Run

**Timestamp:** 2026-03-03T08:15:34.403325
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Types module: interfaces, enums, struct, and abstract base class
# These are fundamental types used across other modules

interface IIdentifiable:
    property get id: int

interface IValidatable:
    def is_valid(self) -> bool: ...

enum Status:
    ACTIVE = 1
    INACTIVE = 0
    PENDING = 2

struct Point:
    x: float
    y: float

    def __init__(self, x_coord: float, y_coord: float):
        self.x = x_coord
        self.y = y_coord

    def distance_from_origin(self) -> float:
        return (self.x ** 2.0 + self.y ** 2.0) ** 0.5

@abstract
class Entity(IIdentifiable):
    _id: int

    def __init__(self, entity_id: int):
        self._id = entity_id

    @virtual
    property get id(self) -> int:
        return self._id

    @abstract
    def describe(self) -> str: ...

```

### utils.spy

```python
# Utils module: concrete classes using types from types module
from types import IIdentifiable, IValidatable, Status, Point, Entity

class User(Entity, IValidatable):
    name: str
    user_status: Status

    def __init__(self, user_id: int, user_name: str, status: Status):
        super().__init__(user_id)
        self.name = user_name
        self.user_status = status

    @override
    def describe(self) -> str:
        return f"User({self.id}, {self.name})"

    def is_valid(self) -> bool:
        return self.name != "" and self.user_status != Status.INACTIVE

class Product(Entity):
    price: float
    location: Point

    def __init__(self, prod_id: int, prod_price: float, loc: Point):
        super().__init__(prod_id)
        self.price = prod_price
        self.location = loc

    @override
    def describe(self) -> str:
        return f"Product({self.id}, {self.price})"

    @override
    property get id(self) -> int:
        return self._id + 1000

class StaticUtils:
    @static
    property get version() -> str:
        return "1.0"

    @static
    def build_point(x: float, y: float) -> Point:
        return Point(x, y)

    @static
    def format_status(s: Status) -> str:
        if s == Status.ACTIVE:
            return "Active"
        elif s == Status.INACTIVE:
            return "Inactive"
        else:
            return "Pending"

```

### helpers.spy

```python
# Helpers module: utility functions
from types import Status, Point, IValidatable, IIdentifiable
from utils import StaticUtils

def calculate_distance(p1: Point, p2: Point) -> float:
    dx: float = p1.x - p2.x
    dy: float = p1.y - p2.y
    return (dx ** 2.0 + dy ** 2.0) ** 0.5

def check_entities(items: list[IValidatable]) -> int:
    valid_count: int = 0
    for item in items:
        if item.is_valid():
            valid_count += 1
    return valid_count

def get_ids(items: list[IIdentifiable]) -> list[int]:
    results: list[int] = []
    for item in items:
        results.append(item.id)
    return results

def create_default_user(user_id: int) -> "utils.User":
    # Forward reference to User type
    import utils
    return utils.User(user_id, "Default", Status.ACTIVE)

```

### main.spy

```python
# Main entry point: imports and uses all modules
from types import Status, Point, Entity, IValidatable, IIdentifiable
from utils import StaticUtils, User, Product
from helpers import calculate_distance, check_entities, get_ids

def main():
    # Create points using static utility
    p1: Point = StaticUtils.build_point(3.0, 4.0)
    p2: Point = Point(0.0, 0.0)

    # Test struct method
    dist: float = p1.distance_from_origin()
    print(dist)

    # Create users with different statuses
    user1: User = User(1, "Alice", Status.ACTIVE)
    user2: User = User(2, "Bob", Status.INACTIVE)
    user3: User = User(3, "", Status.PENDING)

    # Test polymorphic describe
    print(user1.describe())

    # Test IValidatable interface collection
    validatables: list[IValidatable] = [user1, user2, user3]
    valid_count: int = check_entities(validatables)
    print(valid_count)

    # Test static utility formatting
    print(StaticUtils.format_status(Status.ACTIVE))
    print(StaticUtils.version)

    # Test Products with custom id override
    prod1: Product = Product(101, 49.99, p1)
    print(prod1.describe())

    # Test IIdentifiable interface collection
    entities: list[IIdentifiable] = [user1, prod1]
    ids: list[int] = get_ids(entities)
    for id_val in ids:
        print(id_val)

    # Test distance calculation
    p3: Point = Point(6.0, 8.0)
    total_dist: float = calculate_distance(p1, p3)
    print(total_dist)

```

## Timing

- Generation: 237.53s
- Execution: 5.06s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
