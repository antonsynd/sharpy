# Successful Dogfood Run

**Timestamp:** 2026-01-26T23:53:27.512837
**Feature Focus:** function_calling_function
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Function calling function with discount calculation system
# Tests: function composition, multiple return values, conditional logic

def calculate_subtotal(price: int, quantity: int) -> int:
    return price * quantity

def apply_discount(amount: int, is_vip: bool) -> int:
    if is_vip:
        discount: int = amount * 20 // 100
        return amount - discount
    else:
        discount: int = amount * 10 // 100
        return amount - discount

def calculate_total(price: int, quantity: int, is_vip: bool) -> int:
    subtotal: int = calculate_subtotal(price, quantity)
    print(f"Subtotal: {subtotal}")
    
    total: int = apply_discount(subtotal, is_vip)
    return total

def main():
    print("Order 1: Regular customer")
    regular_total: int = calculate_total(100, 3, False)
    print(f"Final total: {regular_total}")
    
    print("Order 2: VIP customer")
    vip_total: int = calculate_total(100, 3, True)
    print(f"Final total: {vip_total}")

# EXPECTED OUTPUT:
# Order 1: Regular customer
# Subtotal: 300
# Final total: 270
# Order 2: VIP customer
# Subtotal: 300
# Final total: 240
```

## Output

```
Order 1: Regular customer
Subtotal: 300
Final total: 270
Order 2: VIP customer
Subtotal: 300
Final total: 240
```

## Timing

- Generation: 6.78s
- Execution: 1.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
