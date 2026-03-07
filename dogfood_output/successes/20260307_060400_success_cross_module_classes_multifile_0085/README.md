# Successful Dogfood Run

**Timestamp:** 2026-03-07T06:01:22.962514
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### models.spy

```python
@virtual
class Person:
    first_name: str
    last_name: str

    def __init__(self, first: str, last: str):
        self.first_name = first
        self.last_name = last

    @virtual
    def describe(self) -> str:
        return f"Person: {self.first_name} {self.last_name}"


class Employee(Person):
    employee_id: int

    def __init__(self, first: str, last: str, emp_id: int):
        super().__init__(first, last)
        self.employee_id = emp_id

    @override
    def describe(self) -> str:
        return f"Employee: {self.first_name} {self.last_name} (ID: {self.employee_id})"


struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y


class Product:
    _name: str
    _price: float

    def __init__(self, name: str, price: float):
        self._name = name
        self._price = price

    property get name(self) -> str:
        return self._name

    property get price(self) -> str:
        return str(self._price) + " USD"

```

### utils.spy

```python
from models import Person, Employee, Point, Product


class Company:
    _employee: Employee

    def __init__(self, emp: Employee):
        self._employee = emp

    def get_employee_info(self) -> str:
        return self._employee.describe()


class GeometryUtil:
    @static
    def distance_from_origin(p: Point) -> float:
        return (p.x ** 2.0 + p.y ** 2.0) ** 0.5


class Store:
    _inventory: list[Product]

    def __init__(self):
        self._inventory = []

    def add_product(self, p: Product):
        self._inventory.append(p)

    def get_product_list(self) -> str:
        result: str = ""
        for i in range(len(self._inventory)):
            p: Product = self._inventory[i]
            result = result + p.name + " - " + p.price
            if i < len(self._inventory) - 1:
                result = result + ", "
        return result

```

### main.spy

```python
from models import Person, Employee, Point, Product
from utils import Company, GeometryUtil, Store


def main():
    emp: Employee = Employee("Alice", "Smith", 12345)
    comp: Company = Company(emp)
    print(comp.get_employee_info())

    pt: Point = Point(3.0, 4.0)
    dist: float = GeometryUtil.distance_from_origin(pt)
    print(f"Distance: {dist}")

    prod1: Product = Product("Laptop", 999.99)
    prod2: Product = Product("Mouse", 29.99)
    store: Store = Store()
    store.add_product(prod1)
    store.add_product(prod2)
    print(store.get_product_list())

```

## Timing

- Generation: 141.32s
- Execution: 4.93s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
