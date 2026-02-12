# Skipped Dogfood Run

**Timestamp:** 2026-02-11T23:42:47.853473
**Skip Reason:** Sharpy compiler error in data_models.spy: Compilation errors:

error[SPY0102]: Expected newline, got Dedent
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp3lb3grd_/dogfood_test.spy:46:18
    |
 46 |     DELIVERED = 4
    |                  ^
    |

error[SPY0104]: Expected Dedent, got Eof
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp3lb3grd_/dogfood_test.spy:46:18
    |
 46 |     DELIVERED = 4
    |                  ^
    |


**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** claude
**Test Type:** Multi-file (3 files)

## Source Files

### data_models.spy

```python
# Data models for a multi-layered architecture system
# Provides base interfaces and data structures

interface ISerializable:
    def to_string(self) -> str:
        ...

    def get_type_name(self) -> str:
        ...

class DataEntity(ISerializable):
    id: int
    name: str

    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name

    def to_string(self) -> str:
        return f"Entity({self.id}, {self.name})"

    def get_type_name(self) -> str:
        return "DataEntity"

class Product(DataEntity):
    price: float
    category: str

    def __init__(self, id: int, name: str, price: float, category: str):
        super().__init__(id, name)
        self.price = price
        self.category = category

    @override
    def to_string(self) -> str:
        return f"Product[{self.id}]: {self.name} - ${self.price} ({self.category})"

    @override
    def get_type_name(self) -> str:
        return "Product"

enum OrderStatus:
    PENDING = 1
    PROCESSING = 2
    SHIPPED = 3
    DELIVERED = 4
```

### business_logic.spy

```python
# Business logic layer - processes and validates entities
from data_models import Product, OrderStatus, ISerializable

class Order(ISerializable):
    order_id: int
    products: list[Product]
    status: OrderStatus
    total: float

    def __init__(self, order_id: int):
        self.order_id = order_id
        self.products = []
        self.status = OrderStatus.PENDING
        self.total = 0.0

    def add_product(self, product: Product) -> None:
        self.products.append(product)
        self.total += product.price

    def process_order(self) -> None:
        self.status = OrderStatus.PROCESSING

    def ship_order(self) -> None:
        self.status = OrderStatus.SHIPPED

    def to_string(self) -> str:
        return f"Order #{self.order_id}: {len(self.products)} items, Total: ${self.total}, Status: {self.status}"

    def get_type_name(self) -> str:
        return "Order"

class DiscountCalculator:
    @staticmethod
    def apply_bulk_discount(order: Order, threshold: int, discount_pct: float) -> float:
        if len(order.products) >= threshold:
            discount: float = order.total * discount_pct
            return order.total - discount
        return order.total
```

### main.spy

```python
# Main application - demonstrates cross-module usage
from data_models import Product, OrderStatus
from business_logic import Order, DiscountCalculator

def main():
    # Create products using the Product class from data_models
    laptop = Product(101, "Laptop", 999.99, "Electronics")
    mouse = Product(102, "Mouse", 25.50, "Accessories")
    keyboard = Product(103, "Keyboard", 75.00, "Accessories")
    
    print(laptop.to_string())
    
    # Create order using Order class from business_logic
    order = Order(5001)
    order.add_product(laptop)
    order.add_product(mouse)
    order.add_product(keyboard)
    
    print(order.to_string())
    
    # Process the order
    order.process_order()
    print(f"Order status: {order.status}")
    
    # Apply discount using static method from DiscountCalculator
    discounted_total: float = DiscountCalculator.apply_bulk_discount(order, 3, 0.10)
    print(f"Discounted total: ${discounted_total}")
    
    # Ship the order
    order.ship_order()
    print(f"Final status: {order.status}")
    
    # Demonstrate interface usage
    serializable_name: str = laptop.get_type_name()
    print(f"Type name: {serializable_name}")
    
    order_type: str = order.get_type_name()
    print(f"Order type: {order_type}")

# EXPECTED OUTPUT:
# Product[101]: Laptop - $999.99 (Electronics)
# Order #5001: 3 items, Total: $1100.49, Status: OrderStatus.PENDING
# Order status: OrderStatus.PROCESSING
# Discounted total: $990.441
# Final status: OrderStatus.SHIPPED
# Type name: Product
# Order type: Order
```

## Timing

- Generation: 17.24s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
