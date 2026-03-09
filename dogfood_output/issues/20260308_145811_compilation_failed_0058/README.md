# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T14:40:34.161638
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module features

from shapes import Shape, Rectangle, Circle, Color, calculate_total_area, IPerimeter
from utils import Point, Dimension, format_dimension, get_color_name, sum_perimeters
from collections import Box, DrawableRectangle, create_bounded_shape

def main():
    # Create shapes from shapes module
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.5)

    # Create geometry from utils module
    origin: Point = Point(0.0, 0.0)
    corner: Point = Point(5.0, 3.0)

    # Test struct methods
    dist: float = origin.distance(corner)
    print(dist)

    # Create list[IPerimeter] directly to avoid generic invariance issue
    perimeters: list[IPerimeter] = []
    perimeters.append(rect)
    perimeters.append(circle)

    # Create list[Shape] for area calculation
    shapes: list[Shape] = []
    shapes.append(rect)
    shapes.append(circle)

    # Calculate total area using shapes module function
    total_area: float = calculate_total_area(shapes)
    print(total_area)

    # Test perimeter sum using utils function
    # perimeters is list[IPerimeter] which matches the parameter type
    total_perim: float = sum_perimeters(perimeters)
    print(total_perim)

    # Test Point and Dimension
    dim: Dimension = Dimension(10.0, 5.0)
    print(format_dimension(dim))
    print(dim.aspect_ratio())

    # Test enum and color name
    col: Color = Color.BLUE
    print(get_color_name(col))

    # Test Box from collections (multiple inheritance via mixins)
    box: Box = Box(4.0, 3.0, 2.0)
    box.add(rect)
    box.add(circle)
    print(box.describe())
    print(box.volume())

    # Test DrawableRectangle
    draw_rect: DrawableRectangle = DrawableRectangle(6.0, 4.0, Color.GREEN, Point(10.0, 20.0))
    print(draw_rect.describe())

    # Test create_bounded_shape factory
    bounded: DrawableRectangle = create_bounded_shape(Dimension(8.0, 6.0), Color.RED)
    print(bounded.describe())

    # Final counts
    print(len(shapes))

    # Verify box capacity matches contents
    print(box.capacity())

```

## Error

```
Assembly compilation failed:

error[CS1721]: Class 'Collections.Box' cannot have multiple base classes: 'Shapes.Rectangle' and 'Collections.ShapeContainer'
  --> /tmp/tmpf_uux8ls/collections.spy:18:42
    |
 18 |     print(dist)
    |                ^
    |

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Describe()'
  --> /tmp/tmpf_uux8ls/shapes.spy:22:18
    |
 22 |     perimeters.append(rect)
    |                  ^
    |

error[CS0534]: 'Shapes.Circle' does not implement inherited abstract member 'Shapes.Shape.Describe()'
  --> /tmp/tmpf_uux8ls/shapes.spy:34:18
    |
 34 |     # Test perimeter sum using utils function
    |                  ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Area' and no accessible extension method 'Area' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpf_uux8ls/shapes.spy:66:31
    |
 66 |     # Verify box capacity matches contents
    |                               ^
    |

error[CS1729]: 'Utils.Dimension' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpf_uux8ls/main.spy:40:35
    |
 40 |     dim: Dimension = Dimension(10.0, 5.0)
    |                                   ^
    |

error[CS0030]: Cannot convert type 'Collections.Box' to 'Collections.ShapeContainer'
  --> /tmp/tmpf_uux8ls/main.spy:50:10
    |
 50 |     box.add(rect)
    |          ^
    |

error[CS0030]: Cannot convert type 'Collections.Box' to 'Collections.ShapeContainer'
  --> /tmp/tmpf_uux8ls/main.spy:51:10
    |
 51 |     box.add(circle)
    |          ^
    |

error[CS1729]: 'Utils.Dimension' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpf_uux8ls/main.spy:60:72
    |
 60 |     bounded: DrawableRectangle = create_bounded_shape(Dimension(8.0, 6.0), Color.RED)
    |                                                                        ^
    |

error[CS1061]: 'Collections.Box' does not contain a definition for 'Contents' and no accessible extension method 'Contents' accepting a first argument of type 'Collections.Box' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpf_uux8ls/collections.spy:29:53
    |
 29 | 
    | ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'width' of 'Shapes.Rectangle.Rectangle(double, double)'
  --> /tmp/tmpf_uux8ls/collections.spy:38:16
    |
 38 | 
    | ^
    |

error[CS0117]: 'Shapes.Rectangle' does not contain a definition for 'Constructor'
  --> /tmp/tmpf_uux8ls/collections.spy:24:23
    |
 24 | 
    | ^
    |

error[CS0117]: 'Collections.ShapeContainer' does not contain a definition for 'Constructor'
  --> /tmp/tmpf_uux8ls/collections.spy:25:28
    |
 25 |     # Create list[Shape] for area calculation
    |                            ^
    |

error[CS0117]: 'Shapes.Shape' does not contain a definition for 'NextId'
  --> /tmp/tmpf_uux8ls/shapes.spy:17:34
    |
 17 |     dist: float = origin.distance(corner)
    |                                  ^
    |

error[CS0117]: 'Shapes.Shape' does not contain a definition for 'NextId'
  --> /tmp/tmpf_uux8ls/shapes.spy:18:19
    |
 18 |     print(dist)
    |                ^
    |

error[CS0117]: 'Shapes.Shape' does not contain a definition for 'NextId'
  --> /tmp/tmpf_uux8ls/shapes.spy:18:34
    |
 18 |     print(dist)
    |                ^
    |


```

## Timing

- Generation: 1023.74s
- Execution: 5.31s
