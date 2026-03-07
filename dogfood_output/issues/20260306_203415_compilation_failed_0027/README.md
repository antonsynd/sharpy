# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T20:30:27.893402
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from all modules to test cross-module features
from models import Color, Dimensions
from shapes import Shape
from geometry import Circle

def main():
    # Create a concrete circle instance
    c = Circle("MyCircle", 5.0, Color.BLUE)
    
    # Test overridden method from cross-module inheritance
    print(c.get_info())
    
    # Test struct returned from cross-module method
    dims = c.get_dimensions()
    print(dims.width)
    print(dims.height)
    
    # Test method defined in subclass
    area = c.get_area()
    print(area)
    
    # Test enum value access
    print(c.color.value)
    
    # Test polymorphism with base class reference
    s: Shape = Circle("PolyCircle", 3.0, Color.GREEN)
    print(s.get_info())
    
    # Test enum name
    print(Color.RED.name)
    
    # Test enum name through base class reference
    print(s.color.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Models.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Models.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpxq4mu2hj/main.spy:23:47
    |
 23 |     print(c.color.value)
    |                         ^
    |

error[CS1061]: 'Models.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Models.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpxq4mu2hj/main.spy:33:47
    |
 33 |     print(s.color.name)
    |                        ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Dimensions' is never used
  --> /tmp/tmpxq4mu2hj/main.spy:2:27
    |
  2 | from models import Color, Dimensions
    |                           ^^^^^^^^^^
    |


```

## Timing

- Generation: 206.56s
- Execution: 5.45s
