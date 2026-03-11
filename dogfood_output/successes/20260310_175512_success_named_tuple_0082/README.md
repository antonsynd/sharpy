# Successful Dogfood Run

**Timestamp:** 2026-03-10T17:54:25.193923
**Feature Focus:** named_tuple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test named tuples with mixed types and unpacking in iteration
# Uses a Product type with name (str), price (float), and in_stock (bool)

type Product = tuple[name: str, price: float, in_stock: bool]

def main():
    # Named construction with keyword arguments
    apple: Product = (name="Apple", price=0.50, in_stock=True)
    banana: Product = (name="Banana", price=0.30, in_stock=False)
    cherry: Product = (name="Cherry", price=2.50, in_stock=True)
    
    # Collect into list
    products: list[Product] = [apple, banana, cherry]
    
    total: float = 0.0
    count: int = 0
    
    # Iterate with tuple unpacking
    for item_name, item_price, available in products:
        if available:
            # Access through variable (unpacked)
            total += item_price
            count += 1
            print(item_name)
    
    print(count)
    print(total)
    
    # Access by field name on original
    print(apple.name)
    print(apple.price)

```

## Output

```
Apple
Cherry
2
3.0
Apple
0.5
```

## Timing

- Generation: 35.65s
- Execution: 5.02s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
