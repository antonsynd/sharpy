# Successful Dogfood Run

**Timestamp:** 2026-02-25T02:15:04.793192
**Feature Focus:** f_string_basic
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: f_string_basic with class methods and arithmetic expressions

class Product:
    name: str
    price: float
    quantity: int
    
    def __init__(self, name: str, price: float, quantity: int):
        self.name = name
        self.price = price
        self.quantity = quantity
    
    def total_value(self) -> float:
        return self.price * self.quantity
    
    def discount_price(self, percent_off: int) -> float:
        discount = self.price * percent_off / 100.0
        return self.price - discount

def main():
    item = Product("Widget", 19.99, 5)
    
    print(f"Product: {item.name}")
    
    print(f"Stock value: ${item.total_value()}")
    
    discounted = item.discount_price(10)
    print(f"After 10% off: ${discounted}")
    
    in_stock = item.quantity > 0
    print(f"In stock: {in_stock}")
    
    if item.quantity < 10:
        print(f"Low stock alert: only {item.quantity} remaining!")
    else:
        print(f"Well stocked: {item.quantity} units")

# EXPECTED OUTPUT:
# Product: Widget
# After 10% off: $17.991
```

## Output

```
Product: Widget
Stock value: $99.94999999999999
After 10% off: $17.991
In stock: True
Low stock alert: only 5 remaining!
```

## Timing

- Generation: 208.96s
- Execution: 4.39s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
