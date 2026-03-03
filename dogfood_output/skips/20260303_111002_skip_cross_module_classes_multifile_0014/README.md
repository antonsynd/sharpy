# Skipped Dogfood Run

**Timestamp:** 2026-03-03T10:57:47.912641
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'Inventory' has no member 'location'
  --> /tmp/tmp0z0ji3iw/main.spy:22:5
    |
 22 |     inv.location = Point(10.0, 20.0)
    |     ^^^^^^^^^^^^
    |


**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### math_utils.spy

```python
# Module-level state (shared across the module)
instance_count: int = 0

class Calculator:
    def __init__(self):
        global instance_count
        instance_count += 1
        self._value: int = 0
    
    def get_value(self) -> int:
        return self._value
    
    def set_value(self, v: int) -> None:
        self._value = v
    
    def add(self, a: int, b: int) -> int:
        return a + b
    
    def multiply(self, a: int, b: int) -> int:
        return a * b

```

### item_types.spy

```python
class Point:
    def __init__(self, x: float, y: float):
        self.x: float = x
        self.y: float = y
    
    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"

class Item:
    def __init__(self, name: str, value: int):
        self.name: str = name
        self.value: int = value
    
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Item):
            return False
        o_item: Item = other as Item
        return self.name == o_item.name and self.value == o_item.value
    
    def __hash__(self) -> int:
        return hash(self.name) + self.value
    
    def __str__(self) -> str:
        return f"Item({self.name}, {self.value})"

```

### inventory.spy

```python
from item_types import Point
from item_types import Item

class Inventory:
    def __init__(self):
        self.items: list[Item] = []
        self.location: Point = Point(0.0, 0.0)
    
    def add_item(self, item: Item) -> None:
        self.items.append(item)
    
    def get_item_count(self) -> int:
        return len(self.items)
    
    def total_value(self) -> int:
        total: int = 0
        for item in self.items:
            total += item.value
        return total
    
    def find_item(self, name: str) -> Item?:
        for item in self.items:
            if item.name == name:
                return Some(item)
        return None()
    
    def get_location_str(self) -> str:
        return str(self.location)

```

### main.spy

```python
from math_utils import Calculator
from math_utils import instance_count
from item_types import Point
from item_types import Item
from inventory import Inventory

def main():
    calc: Calculator = Calculator()
    calc.set_value(10)
    print(f"Calculator value: {calc.get_value()}")
    print(f"Instances: {instance_count}")
    
    sum_result: int = calc.add(5, 3)
    mult_result: int = calc.multiply(4, 7)
    print(f"Sum: {sum_result}")
    print(f"Product: {mult_result}")
    
    p1: Point = Point(3.0, 4.0)
    print(f"Point: {p1}")
    
    inv: Inventory = Inventory()
    inv.location = Point(10.0, 20.0)
    
    item1: Item = Item("Sword", 100)
    item2: Item = Item("Shield", 50)
    item3: Item = Item("Potion", 25)
    
    inv.add_item(item1)
    inv.add_item(item2)
    inv.add_item(item3)
    
    print(f"Item count: {inv.get_item_count()}")
    print(f"Total value: {inv.total_value()}")
    print(f"Location: {inv.get_location_str()}")
    
    found: Item? = inv.find_item("Shield")
    if found is not None:
        print(f"Found: {found}")
    else:
        print("Not found")
    
    missing: Item? = inv.find_item("Armor")
    if missing is None:
        print("Armor not in inventory")
    
    calc2: Calculator = Calculator()
    print(f"Total instances: {instance_count}")

```

## Timing

- Generation: 704.81s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
