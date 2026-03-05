# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T18:05:37.861313
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module functionality

from models import Status, IIdentifiable, INameable
from utils import Point, Dimension, calculate_center, status_to_string
from services import User, Product, create_user

def process_entity(entity: IIdentifiable) -> None:
    print(entity.get_id())

def display_name(named: INameable) -> None:
    print(named.get_name())

def main():
    # Create a user using factory function
    user: User = create_user(1001, "alice", 10.0, 20.0)

    # Create a product directly
    prod_pos: Point = Point(50.0, 50.0)
    prod_size: Dimension = Dimension(100.0, 200.0)
    product: Product = Product(2001, "Widget", prod_pos, prod_size)

    # Test interface polymorphism - IIdentifiable
    print("User ID:")
    process_entity(user)

    # Test interface polymorphism - INameable
    print("User name:")
    display_name(user)

    # Test status before activation
    print("User status before:")
    print(status_to_string(user.get_status()))

    # Activate the user
    user.activate()

    # Test status after activation
    print("User status after:")
    print(status_to_string(user.get_status()))

    # Test product properties (cross-module access)
    print("Product name:")
    display_name(product)

    # Calculate center of product area
    center: Point = calculate_center(product.get_size())
    print("Product center X:")
    print(center.x)

```

## Error

```
Assembly compilation failed:

error[CS0106]: The modifier 'virtual' is not valid for this item
  --> utils.cs:17:31
    |
 17 |     # Create a product directly
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IIdentifiable' is never used
  --> /tmp/tmpmgd_4zu0/services.spy:3:15
    |
  3 | from models import Status, IIdentifiable, INameable
    |               ^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmpmgd_4zu0/main.spy:3:20
    |
  3 | from models import Status, IIdentifiable, INameable
    |                    ^^^^^^
    |


```

## Timing

- Generation: 483.78s
- Execution: 4.56s
