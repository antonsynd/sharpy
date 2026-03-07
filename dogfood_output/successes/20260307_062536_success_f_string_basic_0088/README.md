# Successful Dogfood Run

**Timestamp:** 2026-03-07T06:25:01.214547
**Feature Focus:** f_string_basic
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Basic f-string interpolation with format specifiers
# Demonstrates: variable interpolation, arithmetic expressions, float/int formatting

def main():
    # Variables for the receipt
    item_name: str = "Widget"
    unit_price: float = 12.50
    quantity: int = 3
    tax_rate: float = 0.08
    
    # Calculate values
    subtotal: float = unit_price * quantity
    tax_amount: float = subtotal * tax_rate
    total: float = subtotal + tax_amount
    
    # Basic variable interpolation
    print(f"Item: {item_name}")
    
    # Expression interpolation with arithmetic
    print(f"Quantity: {quantity} @ ${unit_price} each")
    
    # Float formatting with two decimal places
    print(f"Subtotal: ${subtotal:.2f}")
    print(f"Tax (8%): ${tax_amount:.2f}")
    
    # Integer formatting with padding for alignment
    invoice_id: int = 42
    print(f"Receipt #{invoice_id:04d}")
    
    # Combined multiple values with formatting
    print(f"Total: ${total:.2f}")

```

## Output

```
Item: Widget
Quantity: 3 @ $12.5 each
Subtotal: $37.50
Tax (8%): $3.00
Receipt #42
Total: $40.50
```

## Timing

- Generation: 25.20s
- Execution: 4.50s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
