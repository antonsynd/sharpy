# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T07:21:10.701074
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Entry point - imports from multiple modules
from models import Status, Product
from handlers import PricedInventoryItem, StatusHandler
from utils import calculate_discount, format_product_info, create_product

def main():
    # Test enum import and usage
    print(Status.COMPLETED.name)
    
    # Test struct import and factory function from utils
    widget: Product = create_product("Widget", 10.0, 5)
    print(format_product_info(widget))
    
    # Test utility function
    discounted: float = calculate_discount(100.0, 15)
    print(discounted)
    
    # Test class with interface implementation
    item: PricedInventoryItem = PricedInventoryItem("ABC-123", 49.99, "Test product item")
    
    # Test inherited method
    print(item.get_display_label())
    
    # Test interface method
    print(item.get_name())
    
    # Test status handler from handlers module
    handler: StatusHandler = StatusHandler()
    result: str = handler.process_item(item)
    print(result)
    
    # Test status count
    print(handler.items_processed)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Models.Status' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Models.Status' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbd6g23km/handlers.spy:44:89


```

## Timing

- Generation: 157.00s
- Execution: 5.05s
