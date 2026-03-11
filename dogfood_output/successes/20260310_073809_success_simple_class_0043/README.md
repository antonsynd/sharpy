# Successful Dogfood Run

**Timestamp:** 2026-03-10T07:37:04.282359
**Feature Focus:** simple_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Simple class with computed property discount logic and __str__
class OrderItem:
    name: str
    unit_price: float
    quantity: int
    
    def __init__(self, name: str, price: float):
        self.name = name
        self.unit_price = price
        self.quantity = 0
    
    property get total(self) -> float:
        subtotal: float = self.unit_price * self.quantity
        if self.quantity >= 10:
            return round(subtotal * 0.85, 2)
        elif self.quantity >= 5:
            return round(subtotal * 0.9, 2)
        else:
            return subtotal
    
    def __str__(self) -> str:
        return f"{self.name} x{self.quantity}"

def main():
    item: OrderItem = OrderItem("Widget", 20.0)
    print(item)
    item.quantity = 4
    print(item.total)
    item.quantity = 6
    print(item.total)
    print(item.name)

```

## Output

```
Widget x0
80.0
108.0
Widget
```

## Timing

- Generation: 53.75s
- Execution: 5.31s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
