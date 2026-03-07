# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T23:09:03.727453
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point that imports from multiple modules
# Tests complex import patterns and cross-module type usage

from types_module import Priority, Point2D, IIdentifiable
from models import Entity, Measurement, Sensor
from utils import compute_checksum, format_coords, VERSION

def main():
    # Test 1: Create entities and verify ID generation
    entity1 = Entity("Device_A")
    entity2 = Entity("Device_B")
    print(entity1.get_id())
    print(entity2.get_id())

    # Test 2: Use struct Point2D from types_module
    p1 = Point2D(3.0, 4.0)
    print(p1.distance_from_origin())

    # Test 3: Interface implementation across modules
    m = Measurement(42.5, "Celsius")
    print(format_coords(m.get_value(), 0.0))

    # Test 4: Complex class with multiple interfaces
    sensor = Sensor("TempSensor", Point2D(0.0, 0.0), Priority.HIGH)
    print(sensor.get_id())

    # Test 5: Using enum from another module
    print(Priority.MEDIUM)

    # Test 6: Verify function import from utils
    values: list[int] = [10, 20, 30]
    print(compute_checksum(values))

    # Test 7: Verify constant import
    print(VERSION)

    # Test 8: Test distance calculation via subclass method
    sensor2 = Sensor("TestSensor", Point2D(5.0, 0.0), Priority.LOW)
    sensor2.set_reading(99.0)
    origin = Point2D(0.0, 0.0)
    print(sensor2.distance_to(origin))

```

## Error

```
Assembly compilation failed:

error[CS0229]: Ambiguity between 'Utils.Version' and 'Version'
  --> /tmp/tmpg14_ln30/main.spy:35:39
    |
 35 |     print(VERSION)
    |                   ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Models.Entity._NextId'
  --> /tmp/tmpg14_ln30/models.spy:11:26
    |
 11 |     entity2 = Entity("Device_B")
    |                          ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Models.Entity._NextId'
  --> /tmp/tmpg14_ln30/models.spy:12:13
    |
 12 |     print(entity1.get_id())
    |             ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'compute_checksum' is never used
  --> /tmp/tmpg14_ln30/types_module.spy:4:38
    |
  4 | from types_module import Priority, Point2D, IIdentifiable
    |                                      ^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IIdentifiable' is never used
  --> /tmp/tmpg14_ln30/main.spy:4:45
    |
  4 | from types_module import Priority, Point2D, IIdentifiable
    |                                             ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 98.60s
- Execution: 4.68s
