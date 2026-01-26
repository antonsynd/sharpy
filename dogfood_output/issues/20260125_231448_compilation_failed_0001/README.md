# Issue Report: compilation_failed

**Timestamp:** 2026-01-25T23:14:08.300088
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module class interactions
from models import Product, Customer
from cart import ShoppingCart

def main():
    laptop = Product("Laptop", 999.99, 5)
    mouse = Product("Mouse", 25.50, 10)
    
    print(laptop.get_info())
    print(mouse.get_info())
    
    alice = Customer("Alice", 1500.0)
    print(alice.balance)
    
    cart = ShoppingCart()
    cart.add_item(laptop)
    cart.add_item(mouse)
    
    print(cart.get_total())
    
    success: bool = cart.checkout(alice)
    print(alice.balance)

# EXPECTED OUTPUT:
# Laptop - $999.99
# Mouse - $25.5
# 1500.0
# 1025.49
# 474.51
```

## Error

```
Assembly compilation failed:
  main.cs(7,26): error CS0234: The type or namespace name 'Models' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(8,26): error CS0234: The type or namespace name 'Cart' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(16,42): error CS0234: The type or namespace name 'Models' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(17,41): error CS0234: The type or namespace name 'Models' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(20,41): error CS0234: The type or namespace name 'Models' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)
  main.cs(22,40): error CS0234: The type or namespace name 'Cart' does not exist in the namespace 'Sharpy.Main' (are you missing an assembly reference?)

```

## Timing

- Generation: 9.30s
- Execution: 1.22s
