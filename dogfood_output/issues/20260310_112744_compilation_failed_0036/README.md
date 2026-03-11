# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T11:19:23.586332
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules
from entities import Entity, Creature
from types_module import Status, Point
from utils import Processor, is_positive, compute_result, apply_predicate

def main():
    # Create entity using cross-module interface implementation
    entity: Entity = Entity(1, "TestEntity")
    print(entity.get_id())
    print(entity.get_name())
    
    # Test enum value access (using value property)
    status_val: int = entity.status.value
    print(status_val)
    
    # Create creature (cross-module inheritance)
    creature: Creature = Creature(2, "Dragon", 100)
    print(creature.get_name())
    
    # Test struct from another module
    point: Point = Point(3.0, 4.0)
    print(point.distance_from_origin())
    
    # Test processor class from another module
    processor: Processor = Processor()
    result: int = processor.process(21)
    print(result)
    
    # Test predicate function type from another module
    # Filter positive values using apply_predicate
    values: list[int] = [-5, 3, 0, 8, -2]
    filtered: list[int] = []
    for v in values:
        if apply_predicate(is_positive, v):
            filtered.append(v)
    
    # Compute final result using cross-module function
    total: int = 0
    for f in filtered:
        total = compute_result(total, f)
    print(total)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'TypesModule.Status' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'TypesModule.Status' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp1r2g5q2y/main.spy:13:39
    |
 13 |     status_val: int = entity.status.value
    |                                       ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmp1r2g5q2y/main.spy:3:26
    |
  3 | from types_module import Status, Point
    |                          ^^^^^^
    |


```

## Timing

- Generation: 454.00s
- Execution: 4.90s
