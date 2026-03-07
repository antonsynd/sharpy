# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T17:36:04.285159
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from shapes import Shape
from shapes_extended import Rectangle, Circle
from utils import calculate_total_area, describe_shape, create_shapes

def main():
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.area())
    
    circle: Circle = Circle(2.0)
    print(circle.area())
    
    shape: Shape = rect
    print(shape.area())
    
    description: str = describe_shape(circle)
    print(description)
    
    shapes: list[Shape] = create_shapes()
    total: float = calculate_total_area(shapes)
    print(total)
    
    shapes.append(circle)
    print(len(shapes))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'ShapesExtended.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'ShapesExtended.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:22:36
    |
 22 |     shapes.append(circle)
    |                          ^
    |

error[CS1061]: 'ShapesExtended.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'ShapesExtended.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:22:50
    |
 22 |     shapes.append(circle)
    |                          ^
    |

error[CS1061]: 'ShapesExtended.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'ShapesExtended.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:26:64

error[CS1061]: 'ShapesExtended.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'ShapesExtended.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:18:18
    |
 18 |     shapes: list[Shape] = create_shapes()
    |                  ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:10:25
    |
 10 |     print(circle.area())
    |                         ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:10:38
    |
 10 |     print(circle.area())
    |                         ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:14:67
    |
 14 |     
    |     ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:14:83
    |
 14 |     
    |     ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:5:18
    |
  5 | def main():
    |            ^
    |

error[CS1061]: 'ShapesExtended.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'ShapesExtended.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbdq5o56g/shapes_extended.spy:6:18
    |
  6 |     rect: Rectangle = Rectangle(5.0, 3.0)
    |                  ^
    |


```

## Compiler Output

```
warning[SPY0458]: @virtual is redundant on '__str__' in 'Shape' — it always overrides Object.ToString(). The @virtual decorator will be ignored.
  --> /tmp/tmpbdq5o56g/shapes.spy:3:9
    |
  3 | from utils import calculate_total_area, describe_shape, create_shapes
    |         ^^^^^^^^
    |

warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpbdq5o56g/utils.spy:2:40
    |
  2 | from shapes_extended import Rectangle, Circle
    |                                        ^^^^^^
    |


```

## Timing

- Generation: 162.99s
- Execution: 4.31s
