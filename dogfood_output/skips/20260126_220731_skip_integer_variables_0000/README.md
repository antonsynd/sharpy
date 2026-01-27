# Skipped Dogfood Run

**Timestamp:** 2026-01-26T22:07:06.772091
**Skip Reason:** Invalid expected output after 3 attempts (Python says: )
**Feature Focus:** integer_variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Integer variables with inventory tracking system
# Demonstrates: int arithmetic, multiple variables, method calculations

class Inventory:
    laptops: int
    mice: int
    keyboards: int
    
    def __init__(self):
        self.laptops = 50
        self.mice = 120
        self.keyboards = 80
    
    def sell_bundle(self, quantity: int) -> None:
        self.laptops = self.laptops - quantity
        self.mice = self.mice - quantity
        self.keyboards = self.keyboards - quantity
    
    def restock(self, item_count: int) -> None:
        self.laptops = self.laptops + item_count
        self.mice = self.mice + (item_count * 2)
        self.keyboards = self.keyboards + item_count
    
    def total_items(self) -> int:
        return self.laptops + self.mice + self.keyboards
    
    def get_laptops(self) -> int:
        return self.laptops

def main():
    store: Inventory = Inventory()
    print(store.total_items())
    
    store.sell_bundle(15)
    print(store.get_laptops())
    print(store.total_items())
    
    store.restock(20)
    print(store.get_laptops())
    print(store.total_items())

# EXPECTED OUTPUT:
# 250
# 35
# 205
# 55
# 285
```

## Timing

- Generation: 24.60s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
