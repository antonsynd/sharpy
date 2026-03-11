# Successful Dogfood Run

**Timestamp:** 2026-03-10T15:33:55.047379
**Feature Focus:** match_expression
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Match expression testing discount pricing tiers with relational patterns

def calculate_price(quantity: int, unit_price: float) -> float:
    # Match expression returns discount multiplier
    discount = match quantity:
        case >= 100: 0.85  # 15% off
        case >= 50: 0.90  # 10% off
        case >= 20: 0.95  # 5% off
        case _: 1.00       # no discount
    
    return quantity * unit_price * discount

def main():
    unit_price: float = 10.0
    
    qty1: int = 5
    qty2: int = 25
    qty3: int = 60
    qty4: int = 150
    
    price1 = calculate_price(qty1, unit_price)
    price2 = calculate_price(qty2, unit_price)
    price3 = calculate_price(qty3, unit_price)
    price4 = calculate_price(qty4, unit_price)
    
    print(price1)
    print(price2)
    print(price3)
    print(price4)

```

## Output

```
50.0
237.5
540.0
1275.0
```

## Timing

- Generation: 150.84s
- Execution: 5.17s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
