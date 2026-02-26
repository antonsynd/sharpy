# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T05:19:45.659660
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module class hierarchy
from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
from shapes import Shape, ShapeType, IShape
from shapes_extended import Rectangle, Circle
from utils import Point, ShapeUtils

def main():
    # Create colors from colors module
    red: Color = RED
    green: Color = GREEN

    # Create points from utils module (struct usage)
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(5.0, 5.0)

    # Create shapes from shapes_extended module
    rect: Rectangle = Rectangle(10.0, 5.0, red, p1)
    circle: Circle = Circle(3.0, green, p2)

    # Print 1: Shape descriptions showing @override methods
    print(rect.description())
    print(circle.description())

    # Print 2-3: Calculate areas
    rect_area: float = rect.area()
    circle_area: float = circle.area()
    print(rect_area)
    print(circle_area)

    # Print 4-5: Calculate perimeters
    print(rect.perimeter())
    print(circle.perimeter())

    # Print 6: Test ShapeUtils with cross-module types (color blending)
    blended: Color = ShapeUtils.blend_shape_colors(rect, circle)
    print(format_color(blended))

    # Print 7: Test Circle.contains_point method
    test_point: Point = Point(3.0, 4.0)
    print(circle.contains_point(test_point))

    # Print 8: Test midpoint via static factory method
    mid: Point = ShapeUtils.create_midpoint(p1, p2)
    print(mid)
```

## Error

```
Assembly compilation failed:

error[CS0506]: 'ShapesExtended.Rectangle.Area()': cannot override inherited member 'Shapes.Shape.Area()' because it is not marked virtual, abstract, or override
  --> shapes_extended.cs:24:32
    |
 24 |     # Print 2-3: Calculate areas
    |                                ^
    |

error[CS0506]: 'ShapesExtended.Rectangle.Perimeter()': cannot override inherited member 'Shapes.Shape.Perimeter()' because it is not marked virtual, abstract, or override
  --> /tmp/tmp4qocom_g/shapes_extended.spy:33:32
    |
 33 | 
    | ^
    |

error[CS0506]: 'ShapesExtended.Circle.Area()': cannot override inherited member 'Shapes.Shape.Area()' because it is not marked virtual, abstract, or override
  --> /tmp/tmp4qocom_g/shapes_extended.spy:29:32
    |
 29 | 
    | ^
    |

error[CS0506]: 'ShapesExtended.Circle.Perimeter()': cannot override inherited member 'Shapes.Shape.Perimeter()' because it is not marked virtual, abstract, or override
  --> /tmp/tmp4qocom_g/shapes_extended.spy:62:32

error[CS1061]: 'Utils.Point' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:36:40
    |
 36 |     print(format_color(blended))
    |                                 ^
    |

error[CS1061]: 'Utils.Point' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:36:47
    |
 36 |     print(format_color(blended))
    |                                 ^
    |

error[CS1061]: 'Utils.Point' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:36:62
    |
 36 |     print(format_color(blended))
    |                                 ^
    |

error[CS1061]: 'Utils.Point' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:36:69
    |
 36 |     print(format_color(blended))
    |                                 ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'R' and no accessible extension method 'R' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:78
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'R' and no accessible extension method 'R' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:85
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'G' and no accessible extension method 'G' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:141
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'G' and no accessible extension method 'G' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:148
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'B' and no accessible extension method 'B' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:204
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'B' and no accessible extension method 'B' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:34:211
    |
 34 |     # Print 6: Test ShapeUtils with cross-module types (color blending)
    |                                                                        ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'R' and no accessible extension method 'R' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:38:54
    |
 38 |     # Print 7: Test Circle.contains_point method
    |                                                 ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'G' and no accessible extension method 'G' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:38:62
    |
 38 |     # Print 7: Test Circle.contains_point method
    |                                                 ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'B' and no accessible extension method 'B' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/colors.spy:38:70
    |
 38 |     # Print 7: Test Circle.contains_point method
    |                                                 ^
    |

error[CS1061]: 'Utils.Point' does not contain a definition for 'X' and no accessible extension method 'X' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:24:41
    |
 24 |     # Print 2-3: Calculate areas
    |                                 ^
    |

error[CS1061]: 'Utils.Point' does not contain a definition for 'Y' and no accessible extension method 'Y' accepting a first argument of type 'Utils.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4qocom_g/utils.spy:25:41
    |
 25 |     rect_area: float = rect.area()
    |                                   ^
    |

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmp4qocom_g/shapes_extended.spy:59:20

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmp4qocom_g/shapes_extended.spy:63:27


```

## Compiler Output

```
warning[SPY0452]: Imported name 'RED' is never used
  --> /tmp/tmp4qocom_g/shapes.spy:2:34
    |
  2 | from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
    |                                  ^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmp4qocom_g/utils.spy:2:52
    |
  2 | from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
    |                                                    ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmp4qocom_g/shapes_extended.spy:2:40
    |
  2 | from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
    |                                        ^^^^^^
    |

warning[SPY0452]: Imported name 'BLUE' is never used
  --> /tmp/tmp4qocom_g/main.spy:2:39
    |
  2 | from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
    |                                       ^^^^
    |

warning[SPY0452]: Imported name 'mix_colors' is never used
  --> /tmp/tmp4qocom_g/main.spy:2:45
    |
  2 | from colors import Color, RED, GREEN, BLUE, mix_colors, format_color
    |                                             ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmp4qocom_g/main.spy:3:20
    |
  3 | from shapes import Shape, ShapeType, IShape
    |                    ^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmp4qocom_g/main.spy:3:27
    |
  3 | from shapes import Shape, ShapeType, IShape
    |                           ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmp4qocom_g/main.spy:3:38
    |
  3 | from shapes import Shape, ShapeType, IShape
    |                                      ^^^^^^
    |


```

## Timing

- Generation: 1313.09s
- Execution: 4.26s
