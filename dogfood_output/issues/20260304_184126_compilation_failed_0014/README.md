# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T18:38:49.102969
**Type:** compilation_failed
**Feature Focus:** class_instance_methods
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Class instance methods with method chaining and internal method calls
# Tests various instance method patterns: void returns, calculations,
# and methods calling other methods on the same instance.

class ShoppingCart:
    items: list[str]
    prices: dict[str, double]

    def __init__(self):
        self.items = []
        self.prices = {"apple": 1.50, "banana": 0.75, "cherry": 0.25}

    def add_item(self, item: str) -> None:
        self.items.append(item)

    def get_item_count(self) -> int:
        return len(self.items)

    def calculate_total(self) -> double:
        total: double = 0.0
        for item in self.items:
            price: double = self.prices.get(item, 0.0)
            total = total + price
        return total

    def apply_discount(self, percent: int) -> double:
        total: double = self.calculate_total()
        discount: double = total * percent / 100.0
        return total - discount

    def remove_item(self, item: str) -> bool:
        idx: int = self.items.index(item)
        if idx >= 0:
            self.items.pop(idx)
            return True
        return False

def main():
    cart = ShoppingCart()
    
    # Add items
    cart.add_item("apple")
    cart.add_item("banana")
    cart.add_item("cherry")
    cart.add_item("cherry")
    
    print(cart.get_item_count())
    
    total: double = cart.calculate_total()
    print(total)
    
    discounted: double = cart.apply_discount(10)
    print(discounted)
    
    cart.remove_item("cherry")
    print(cart.get_item_count())
    
    final: double = cart.calculate_total()
    print(final)

```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'uint' to 'int'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmp2jb9bz4k/dogfood_test.spy:32:23
    |
 32 |         idx: int = self.items.index(item)
    |                       ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp2jb9bz4k/dogfood_test.cs

```

## Timing

- Generation: 137.80s
- Execution: 4.73s
