# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T06:40:10.474521
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex cross-module imports
from data_types import Status, Point, create_point, Shape, IDisplayable, IMeasurable
from entities import Entity, Product
from utils import calculate_distance, process_entity, status_to_int, DEFAULT_LIMIT, UtilityHelper, clamp_value

def main():
    # Test 1: Enum from data_types
    s1: Status = Status.ACTIVE
    s2: Status = Status.PENDING
    print(s1.value)
    print(s2.name)

    # Test 2: Struct from data_types
    p1: Point = create_point(3.0, 4.0)
    p2: Point = Point(0.0, 0.0)
    print(p1.distance_from_origin())

    # Test 3: Function from utils using struct from data_types
    dist: float = calculate_distance(p1, p2)
    print(dist)

    # Test 4: Class from entities, method from utils
    prod: Product = Product(1, "Widget", 29.99)
    print(process_entity(prod))

    # Test 5: Class from data_types implementing interfaces
    shape: Shape = Shape("Circle")

    # Cast to interface and call method
    disp: IDisplayable = shape
    print(disp.display())

    # Test 6: Utils constant and enum conversion
    print(DEFAULT_LIMIT)
    print(status_to_int(Status.INACTIVE))

    # Test 7: Static method from utils
    print(UtilityHelper.format_status(Status.PENDING))

    # Test 8: Utils function
    print(clamp_value(150, 0, 100))

```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DataTypes.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpugo9olrv/data_types.spy:43:20

error[CS1729]: 'DataTypes.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpugo9olrv/main.spy:15:34
    |
 15 |     p2: Point = Point(0.0, 0.0)
    |                                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Entity' is never used
  --> /tmp/tmpugo9olrv/data_types.spy:2:24
    |
  2 | from data_types import Status, Point, create_point, Shape, IDisplayable, IMeasurable
    |                        ^^^^^^
    |

warning[SPY0452]: Imported name 'Product' is never used
  --> /tmp/tmpugo9olrv/utils.spy:2:57
    |
  2 | from data_types import Status, Point, create_point, Shape, IDisplayable, IMeasurable
    |                                                         ^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpugo9olrv/main.spy:2:74
    |
  2 | from data_types import Status, Point, create_point, Shape, IDisplayable, IMeasurable
    |                                                                          ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Entity' is never used
  --> /tmp/tmpugo9olrv/main.spy:3:22
    |
  3 | from entities import Entity, Product
    |                      ^^^^^^
    |


```

## Timing

- Generation: 165.63s
- Execution: 4.71s
