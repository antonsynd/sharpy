# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T18:33:30.820934
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point using cross-module classes
from shapes import Rectangle, Square
from utils import describe_shape, sum_areas

def main():
    r = Rectangle("rect", 10.0, 5.0)
    s = Square("square", 4.0)
    
    print(describe_shape(r))
    print(describe_shape(s))
    
    shapes = [r, s]
    total = sum_areas(shapes)
    print(total)

```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'Sharpy.List<Shapes.Rectangle>' to 'Sharpy.List<Shapes.Shape>'
  --> /tmp/tmp9njfsl1z/main.spy:13:30
    |
 13 |     total = sum_areas(shapes)
    |                              ^
    |


```

## Timing

- Generation: 274.31s
- Execution: 4.26s
