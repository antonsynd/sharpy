# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T09:47:25.500438
**Type:** compilation_failed
**Feature Focus:** enum_definition
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Enum definition with explicit values for order status tracking
enum OrderStatus:
    PENDING = 0
    PROCESSING = 1
    SHIPPED = 2
    DELIVERED = 3
    CANCELLED = 4

def get_status_description(status: OrderStatus) -> str:
    if status == OrderStatus.PENDING:
        return "Order is pending"
    elif status == OrderStatus.PROCESSING:
        return "Order is being processed"
    elif status == OrderStatus.SHIPPED:
        return "Order has been shipped"
    elif status == OrderStatus.DELIVERED:
        return "Order delivered successfully"
    else:
        return "Order was cancelled"

def can_cancel(status: OrderStatus) -> bool:
    if status == OrderStatus.PENDING:
        return True
    elif status == OrderStatus.PROCESSING:
        return True
    else:
        return False

# Test enum values and functions
current_status: OrderStatus = OrderStatus.PENDING
print(get_status_description(current_status))
print(can_cancel(current_status))

current_status = OrderStatus.PROCESSING
print(get_status_description(current_status))
print(can_cancel(current_status))

current_status = OrderStatus.SHIPPED
print(get_status_description(current_status))
print(can_cancel(current_status))

current_status = OrderStatus.DELIVERED
print(get_status_description(current_status))

# EXPECTED OUTPUT:
# Order is pending
# True
# Order is being processed
# True
# Order has been shipped
# False
# Order delivered successfully
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(27,22): error CS0019: Operator '==' cannot be applied to operands of type 'Program.OrderStatus' and 'int'
  dogfood_test.cs(31,22): error CS0019: Operator '==' cannot be applied to operands of type 'Program.OrderStatus' and 'int'
  dogfood_test.cs(35,22): error CS0019: Operator '==' cannot be applied to operands of type 'Program.OrderStatus' and 'int'
  dogfood_test.cs(51,22): error CS0019: Operator '==' cannot be applied to operands of type 'Program.OrderStatus' and 'int'
  dogfood_test.cs(66,29): error CS0266: Cannot implicitly convert type 'int' to 'Sharpy.DogfoodTest.DogfoodTest.Program.OrderStatus'. An explicit conversion exists (are you missing a cast?)
  dogfood_test.cs(69,29): error CS0266: Cannot implicitly convert type 'int' to 'Sharpy.DogfoodTest.DogfoodTest.Program.OrderStatus'. An explicit conversion exists (are you missing a cast?)
  dogfood_test.cs(72,29): error CS0266: Cannot implicitly convert type 'int' to 'Sharpy.DogfoodTest.DogfoodTest.Program.OrderStatus'. An explicit conversion exists (are you missing a cast?)

```

## Timing

- Generation: 6.98s
- Execution: 1.27s
