# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T20:22:51.955044
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules
from module_shapes import Color, Rectangle, Circle, Shape, Measurable, Drawable
from module_utils import Dimension, LazyValue, apply_transform, compute_areas

def main():
    # Create shapes with different colors
    r1 = Rectangle(10.0, 5.0, Color.RED)
    r2 = Rectangle(3.0, 4.0, Color.GREEN)
    c1 = Circle(2.0, Color.BLUE)

    # Test 1: Basic shape drawing and color description
    print(r1.draw())
    print(r1.describe_color())

    # Test 2: Dimension virtual method dispatch
    print(c1.dimensions())

    # Test 3: Lazy value with float
    lazy_area: LazyValue[float] = LazyValue[float](lambda: r2.get_measure())
    print(lazy_area.get())

    # Test 4: Struct operations
    dim = Dimension(16.0, 9.0)
    print(dim.aspect_ratio())

    # Test 5: Compute areas for multiple shapes
    # Fixed: declare as list[Measurable] and append individually
    shapes: list[Measurable] = []
    shapes.append(r1)
    shapes.append(r2)
    shapes.append(c1)
    areas = compute_areas(shapes)
    for area in areas:
        print(area)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'ModuleShapes.Shape.Draw()' is abstract but it is contained in non-abstract type 'ModuleShapes.Shape'
  --> /tmp/tmp599jqegm/module_shapes.spy:32:32
    |
 32 |     areas = compute_areas(shapes)
    |                                ^
    |

error[CS0246]: The type or namespace name 'Transform<>' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp599jqegm/module_utils.spy:30:74
    |
 30 |     shapes.append(r2)
    |                      ^
    |

error[CS1061]: 'ModuleShapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'ModuleShapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp599jqegm/module_shapes.spy:25:31
    |
 25 | 
    | ^
    |

error[CS0103]: The name 'Int' does not exist in the current context
  --> /tmp/tmp599jqegm/module_utils.spy:41:31

error[CS0021]: Cannot apply indexing with [] to an expression of type 'method group'
  --> /tmp/tmp599jqegm/module_utils.spy:41:16


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmp599jqegm/main.spy:2:53
    |
  2 | from module_shapes import Color, Rectangle, Circle, Shape, Measurable, Drawable
    |                                                     ^^^^^
    |

warning[SPY0452]: Imported name 'Drawable' is never used
  --> /tmp/tmp599jqegm/main.spy:2:72
    |
  2 | from module_shapes import Color, Rectangle, Circle, Shape, Measurable, Drawable
    |                                                                        ^^^^^^^^
    |

warning[SPY0452]: Imported name 'apply_transform' is never used
  --> /tmp/tmp599jqegm/main.spy:3:48
    |
  3 | from module_utils import Dimension, LazyValue, apply_transform, compute_areas
    |                                                ^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 428.73s
- Execution: 4.61s
