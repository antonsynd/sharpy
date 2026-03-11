# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T17:52:51.423219
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both modules
from math_utils import PI, E, factorial, is_prime
from geometry import Circle, Rectangle

def main():
    # Test 1: Constants from math_utils
    print(PI)
    print(E)
    
    # Test 2: Function from math_utils
    fact5: int = factorial(5)
    print(fact5)
    
    # Test 3: is_prime function
    print(is_prime(2))
    print(is_prime(17))
    print(is_prime(20))
    
    # Test 4: Circle class from geometry (uses PI internally)
    circle: Circle = Circle(5.0)
    print(circle.area())
    print(circle.perimeter())
    
    # Test 5: Rectangle class from geometry  
    rect: Rectangle = Rectangle(4.0, 6.0)
    print(rect.area())
    print(rect.perimeter())

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Geometry.Shape.Area()' is abstract but it is contained in non-abstract type 'Geometry.Shape'
  --> geometry.cs:15:32
    |
 15 |     print(is_prime(2))
    |                       ^
    |

error[CS0513]: 'Geometry.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Geometry.Shape'
  --> geometry.cs:16:32
    |
 16 |     print(is_prime(17))
    |                        ^
    |


```

## Timing

- Generation: 77.19s
- Execution: 4.86s
