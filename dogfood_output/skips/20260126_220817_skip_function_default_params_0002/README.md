# Skipped Dogfood Run

**Timestamp:** 2026-01-26T22:07:51.198978
**Skip Reason:** Invalid expected output after 3 attempts (Python says: )
**Feature Focus:** function_default_params
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Function default parameters with order system
# Tests: default params, multiple defaults, optional parameters, function calls

class OrderItem:
    product: str
    quantity: int
    price: float
    
    def __init__(self, product: str, quantity: int, price: float):
        self.product = product
        self.quantity = quantity
        self.price = price
    
    def total(self) -> float:
        return self.quantity * self.price

def create_order(product: str, quantity: int = 1, price: float = 9.99, discount: float = 0.0) -> float:
    item: OrderItem = OrderItem(product, quantity, price)
    subtotal: float = item.total()
    final_price: float = subtotal - discount
    return final_price

def main():
    order1: float = create_order("Widget")
    print(f"{order1}")
    
    order2: float = create_order("Gadget", 3)
    print(f"{order2}")
    
    order3: float = create_order("Tool", 2, 15.50)
    print(f"{order3}")
    
    order4: float = create_order("Premium", 5, 20.00, 10.00)
    print(f"{order4}")

# EXPECTED OUTPUT:
# 9.99
# 29.97
# 31.0
# 90.0
```

## Timing

- Generation: 26.36s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
