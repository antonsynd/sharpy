# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T10:26:13.685744
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy: Entry point demonstrating complex module imports
from shapes import Shape, IArea, IPerimeter, ShapeType, Color
from geometry import Circle, Rectangle, Triangle, Point
from utils import calculate_total_area, calculate_total_perimeter, get_shape_category, format_color_code

def main():
    # Create a Point for shape positioning
    origin: Point = Point(0.0, 0.0)
    offset: Point = Point(10.0, 10.0)

    # Create shapes with different colors
    circle: Circle = Circle("Sun", Color.RED, 5.0, origin)
    rect: Rectangle = Rectangle("Door", Color.GREEN, 4.0, 6.0, offset)
    tri: Triangle = Triangle("Pyramid", Color.BLUE, 3.0, 4.0, 5.0, origin)

    # Print shape descriptions and colors
    print(circle.get_description())
    print(circle.get_color_name())
    print(rect.get_description())
    print(rect.get_color_name())
    print(tri.get_description())
    print(tri.get_color_name())

    # Calculate and print individual areas
    print(circle.get_area())
    print(rect.get_area())
    print(tri.get_area())

    # Calculate and print individual perimeters
    print(circle.get_perimeter())
    print(rect.get_perimeter())
    print(tri.get_perimeter())

    # Test category matching
    print(get_shape_category(circle))

    # Test color formatting - use .value to get the int value from enum
    print(format_color_code(Color.GREEN.value))

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'Geometry.Circle' does not implement inherited abstract member 'Shapes.Shape.GetColorName()'
  --> /tmp/tmpjhkltfaf/geometry.spy:15:18
    |
 15 | 
    | ^
    |

error[CS0534]: 'Geometry.Rectangle' does not implement inherited abstract member 'Shapes.Shape.GetColorName()'
  --> /tmp/tmpjhkltfaf/geometry.spy:33:18
    |
 33 | 
    | ^
    |

error[CS0534]: 'Geometry.Triangle' does not implement inherited abstract member 'Shapes.Shape.GetColorName()'
  --> /tmp/tmpjhkltfaf/geometry.spy:65:18

error[CS0161]: 'Utils.GetShapeCategory(Shapes.Shape)': not all code paths return a value
  --> /tmp/tmpjhkltfaf/utils.spy:31:26
    |
 31 |     print(rect.get_perimeter())
    |                          ^
    |

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpjhkltfaf/geometry.spy:112:31

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpjhkltfaf/geometry.spy:77:31

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpjhkltfaf/geometry.spy:47:31


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpjhkltfaf/utils.spy:3:3
    |
  3 | from geometry import Circle, Rectangle, Triangle, Point
    |   ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Triangle' is never used
  --> /tmp/tmpjhkltfaf/utils.spy:3:14
    |
  3 | from geometry import Circle, Rectangle, Triangle, Point
    |              ^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpjhkltfaf/main.spy:2:20
    |
  2 | from shapes import Shape, IArea, IPerimeter, ShapeType, Color
    |                    ^^^^^
    |

warning[SPY0452]: Imported name 'IArea' is never used
  --> /tmp/tmpjhkltfaf/main.spy:2:27
    |
  2 | from shapes import Shape, IArea, IPerimeter, ShapeType, Color
    |                           ^^^^^
    |

warning[SPY0452]: Imported name 'IPerimeter' is never used
  --> /tmp/tmpjhkltfaf/main.spy:2:34
    |
  2 | from shapes import Shape, IArea, IPerimeter, ShapeType, Color
    |                                  ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpjhkltfaf/main.spy:2:46
    |
  2 | from shapes import Shape, IArea, IPerimeter, ShapeType, Color
    |                                              ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'calculate_total_area' is never used
  --> /tmp/tmpjhkltfaf/main.spy:4:19
    |
  4 | from utils import calculate_total_area, calculate_total_perimeter, get_shape_category, format_color_code
    |                   ^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'calculate_total_perimeter' is never used
  --> /tmp/tmpjhkltfaf/main.spy:4:41
    |
  4 | from utils import calculate_total_area, calculate_total_perimeter, get_shape_category, format_color_code
    |                                         ^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 379.80s
- Execution: 5.09s
