# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T23:46:19.007300
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Entry point - demonstrates complex cross-module interactions

from types import OrderStatus
from products import DigitalProduct, PhysicalProduct, Product, StatusTracker
from orders import Order, OrderItem

def main():
    # Create products
    ebook: DigitalProduct = DigitalProduct("Programming Guide", 50.0, "ABC-123")
    laptop: PhysicalProduct = PhysicalProduct("Laptop", 1000.0, 2.5)
    mouse: Product = Product("Mouse", 25.0)
    
    # Test polymorphic pricing
    print(ebook.get_price())
    print(laptop.get_price())
    print(mouse.get_price())
    
    # Create order with items
    order: Order = Order(1001)
    order.add_item(OrderItem(ebook, 2))
    order.add_item(OrderItem(laptop, 1))
    order.add_item(OrderItem(mouse, 2))
    
    # Print order total
    print(order.total())
    
    # Test status progression
    print(order.get_status().value)
    order.advance_status()
    print(order.get_status().value)
    
    # Test interface method
    print(order.log_entry())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Types.OrderStatus' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Types.OrderStatus' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpvkwbohkp/main.spy:28:57
    |
 28 |     print(order.get_status().value)
    |                                    ^
    |

error[CS1061]: 'Types.OrderStatus' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Types.OrderStatus' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpvkwbohkp/main.spy:30:57
    |
 30 |     print(order.get_status().value)
    |                                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'DigitalProduct' is never used
  --> /tmp/tmpvkwbohkp/orders.spy:4:36
    |
  4 | from products import DigitalProduct, PhysicalProduct, Product, StatusTracker
    |                                    ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'PhysicalProduct' is never used
  --> /tmp/tmpvkwbohkp/orders.spy:4:52
    |
  4 | from products import DigitalProduct, PhysicalProduct, Product, StatusTracker
    |                                                    ^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'OrderStatus' is never used
  --> /tmp/tmpvkwbohkp/main.spy:3:19
    |
  3 | from types import OrderStatus
    |                   ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'StatusTracker' is never used
  --> /tmp/tmpvkwbohkp/main.spy:4:64
    |
  4 | from products import DigitalProduct, PhysicalProduct, Product, StatusTracker
    |                                                                ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 51.06s
- Execution: 4.65s
