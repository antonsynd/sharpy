# Successful Dogfood Run

**Timestamp:** 2026-03-03T08:49:59.560014
**Feature Focus:** inheritance_with_override
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Inheritance with override - simple discount pricing
# Tests @virtual and @override decorators with basic arithmetic

class Item:
    base_price: float
    
    def __init__(self, price: float):
        self.base_price = price
    
    @virtual
    def get_price(self) -> float:
        return self.base_price

class DiscountedItem(Item):
    discount_percent: int
    
    def __init__(self, price: float, discount: int):
        super().__init__(price)
        self.discount_percent = discount
    
    @override
    def get_price(self) -> float:
        discount_amount: float = self.base_price * self.discount_percent / 100.0
        return self.base_price - discount_amount

def main():
    regular: Item = Item(100.0)
    on_sale: Item = DiscountedItem(100.0, 25)
    
    print(regular.get_price())
    print(on_sale.get_price())

```

## Output

```
100.0
75.0
```

## Timing

- Generation: 290.83s
- Execution: 4.73s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
