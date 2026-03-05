# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T16:54:50.126333
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module imports and polymorphism
from types import Dimensions, Status
from entities import Entity, Product, Service
from utils import format_status, calculate_total_value, count_by_status, get_entity_summary

def main():
    # Create dimensions using struct factory method
    dims1: Dimensions = Dimensions.square(5.0)
    dims2: Dimensions = Dimensions(10.0, 20.0)

    # Create entities
    laptop: Product = Product(1, "Laptop", dims1, 999.99)
    monitor: Product = Product(2, "Monitor", dims2, 299.50)
    repair: Service = Service(3, "Tech Support", 75.0)

    # Activate some entities
    laptop.activate()
    repair.activate()

    # Create list for processing
    items: list[Entity] = [laptop, monitor, repair]

    # Print 1: Display entity info with polymorphic dispatch
    print(laptop.get_display_name())

    # Print 2: Service display name
    print(repair.get_display_name())

    # Print 3: Status formatting (using get_status method)
    print(format_status(monitor.get_status()))

    # Print 4: Calculate area (interface implementation)
    print(laptop.get_area())

    # Print 5: Service cost estimation
    print(repair.estimate_cost(4))

    # Print 6: Total value calculation
    print(calculate_total_value(items))

    # Print 7: Count active entities
    active_count: int = count_by_status(items, Status.ACTIVE)
    print(active_count)

    # Print 8: Monitor area
    print(monitor.get_area())

    # Print 9: Entity summaries
    print(get_entity_summary(laptop))
    print(get_entity_summary(repair))

```

## Error

```
Assembly compilation failed:

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> types.cs:34:10
    |
 34 | 
    | ^
    |


```

## Timing

- Generation: 326.69s
- Execution: 4.72s
