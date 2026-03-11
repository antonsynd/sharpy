# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T13:49:47.651026
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module inheritance, interfaces, structs, enums

from module_shapes import Rectangle, Circle, Color, Shape, IMeasurable
from module_utils import Point, Dimension, ShapeFactory, format_measurement, calculate_diagonal

def main():
    # Test enum from module_shapes
    rect_color: Color = Color.BLUE
    
    # Test classes with inheritance from module_shapes
    rect: Rectangle = Rectangle(5.0, 3.0, rect_color)
    circle: Circle = Circle(2.5, Color.RED)
    
    # Print 1: Rectangle area
    rect_area: float = rect.area()
    print(format_measurement(rect_area, " sq units"))
    
    # Print 2: Rectangle color name
    print(rect.get_color_name())
    
    # Print 3: Circle area
    circle_area: float = circle.area()
    print(format_measurement(circle_area, " sq units"))
    
    # Print 4: Compare areas
    larger: str = compare_areas(rect, circle)
    print(larger)
    
    # Print 5: Circle draw result
    print(circle.draw())
    
    # Test struct from module_utils
    dim: Dimension = Dimension()
    dim.width = 10.0
    dim.height = 20.0
    
    # Print 6: Diagonal calculation via struct
    diag: float = calculate_diagonal(dim)
    print(format_measurement(diag, " units"))
    
    # Print 7: Point distance from origin
    point: Point = Point(3.0, 4.0)
    dist: float = point.distance_from_origin()
    print(format_measurement(dist, " units"))
    
    # Test static class from module_utils
    factory_result: tuple[Dimension, Point] = ShapeFactory.create_square(6.0)
    square_dim: Dimension = factory_result[0]
    square_center: Point = factory_result[1]
    
    # Print 8: ShapeFactory counter
    count: int = ShapeFactory.increment_counter()
    print(count)

def compare_areas(s1: Shape, s2: Shape) -> str:
    a1: float = s1.area()
    a2: float = s2.area()
    if a1 > a2:
        return "first"
    elif a2 > a1:
        return "second"
    else:
        return "equal"

```

## Error

```
Assembly compilation failed:

error[CS0708]: 'ModuleUtils.ShapeFactory._Counter': cannot declare instance members in a static class
  --> /tmp/tmpspfr0b1m/module_utils.spy:21:20
    |
 21 |     # Print 3: Circle area
    |                    ^
    |

error[CS0534]: 'ModuleShapes.Rectangle' does not implement inherited abstract member 'ModuleShapes.Shape.Draw()'
  --> /tmp/tmpspfr0b1m/module_shapes.spy:28:18
    |
 28 |     
    |     ^
    |

error[CS0534]: 'ModuleShapes.Circle' does not implement inherited abstract member 'ModuleShapes.Shape.Draw()'
  --> /tmp/tmpspfr0b1m/module_shapes.spy:36:18
    |
 36 |     
    |     ^
    |

error[CS1061]: 'ModuleShapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'ModuleShapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpspfr0b1m/module_shapes.spy:23:31
    |
 23 |     print(format_measurement(circle_area, " sq units"))
    |                               ^
    |

error[CS1061]: 'ModuleShapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'ModuleShapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpspfr0b1m/module_shapes.spy:52:25
    |
 52 |     count: int = ShapeFactory.increment_counter()
    |                         ^
    |

error[CS1061]: 'ModuleShapes.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'ModuleShapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpspfr0b1m/module_shapes.spy:55:32
    |
 55 | def compare_areas(s1: Shape, s2: Shape) -> str:
    |                                ^
    |

error[CS0161]: 'ModuleUtils.ShapeFactory.CreateSquare(double)': not all code paths return a value
  --> /tmp/tmpspfr0b1m/module_utils.spy:22:83
    |
 22 |     circle_area: float = circle.area()
    |                                       ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'ModuleUtils.ShapeFactory._Counter'
  --> /tmp/tmpspfr0b1m/module_utils.spy:32:13
    |
 32 |     # Test struct from module_utils
    |             ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'ModuleUtils.ShapeFactory._Counter'
  --> /tmp/tmpspfr0b1m/module_utils.spy:32:37
    |
 32 |     # Test struct from module_utils
    |                                    ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'ModuleUtils.ShapeFactory._Counter'
  --> /tmp/tmpspfr0b1m/module_utils.spy:33:20
    |
 33 |     dim: Dimension = Dimension()
    |                    ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'center' is assigned but never used
  --> /tmp/tmpspfr0b1m/module_utils.spy:15:27
    |
 15 |     rect_area: float = rect.area()
    |                           ^^^^^^^^
    |

warning[SPY0451]: Local variable 'square_dim' is assigned but never used
  --> /tmp/tmpspfr0b1m/main.spy:48:5
    |
 48 |     square_dim: Dimension = factory_result[0]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'square_center' is assigned but never used
  --> /tmp/tmpspfr0b1m/main.spy:49:5
    |
 49 |     square_center: Point = factory_result[1]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpspfr0b1m/main.spy:3:60
    |
  3 | from module_shapes import Rectangle, Circle, Color, Shape, IMeasurable
    |                                                            ^^^^^^^^^^^
    |


```

## Timing

- Generation: 91.01s
- Execution: 5.22s
