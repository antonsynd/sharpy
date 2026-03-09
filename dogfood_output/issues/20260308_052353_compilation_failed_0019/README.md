# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T05:19:20.897057
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Entry point - tests polymorphism and cross-module imports
from utils import BaseCounter, double_value, Countable
from advanced import AdvancedCounter

def main():
    # Test 1: Interface-typed reference to base class
    c: Countable = BaseCounter(5)
    print(c.get_count())
    
    # Test 2: Base class virtual method (increments by 1)
    base = BaseCounter(10)
    print(base.increment())
    
    # Test 3: Derived class override (increments by step=3)
    adv = AdvancedCounter(20, 3)
    print(adv.increment())
    
    # Test 4: Polymorphism - BaseCounter type, AdvancedCounter instance
    poly: BaseCounter = AdvancedCounter(100, 50)
    print(poly.increment())
    
    # Test 5: Static field usage via utility function
    print(double_value(3))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'multiplier' does not exist in the current context
  --> /tmp/tmpgfvsmq4b/utils.spy:24:20
    |
 24 | 
    | ^
    |


```

## Timing

- Generation: 256.36s
- Execution: 4.54s
