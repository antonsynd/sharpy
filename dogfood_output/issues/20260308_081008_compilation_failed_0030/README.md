# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T08:02:13.642315
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - cross-module imports and usage

from shapes import BaseShape, Point2D, Size
from colors import Color, color_to_rgb
from graphics import Circle, Rectangle, ShapeFactory

def print_shape_info(shape: BaseShape) -> None:
    print(shape.describe())

def calculate_total_area(shapes: list[BaseShape]) -> float:
    total: float = 0.0
    for shape in shapes:
        total += shape.area()
    return total

def main():
    # Create shapes using factory and direct construction
    circle1: Circle = ShapeFactory.create_circle(0.0, 0.0, 5.0, Color.RED)
    circle2: Circle = Circle(Point2D(10.0, 10.0), 3.0, Color.BLUE)
    rect1: Rectangle = ShapeFactory.create_rectangle(0.0, 0.0, 4.0, 6.0, Color.GREEN)
    rect2: Rectangle = Rectangle(Point2D(5.0, 5.0), Size(3.0, 3.0), Color.YELLOW)
    
    # Print individual shape info
    print("=== Shape Descriptions ===")
    print_shape_info(circle1)
    print_shape_info(rect1)
    
    # Calculate areas
    print("=== Area Calculations ===")
    print(circle1.area())
    print(rect1.area())
    
    # Calculate total area (polymorphic dispatch)
    shapes: list[BaseShape] = [circle1, circle2, rect1, rect2]
    total: float = calculate_total_area(shapes)
    print(total)
    
    # Test color operations across modules
    print("=== Color Operations ===")
    print(circle2.get_color_name())
    print(rect2.get_color_name())
    
    rgb: tuple[int, int, int] = color_to_rgb(Color.PURPLE)
    print(rgb[0])
    print(rgb[1])
    print(rgb[2])
    
    # Test struct operations
    print("=== Struct Operations ===")
    p1: Point2D = Point2D(0.0, 0.0)
    p2: Point2D = Point2D(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(dist)
    
    size: Size = Size(10.0, 20.0)
    print(size.area)
    
    # Test BaseShape ID generation (access via property)
    print("=== Shape IDs ===")
    print(circle1.id)
    print(circle2.id)
    print(rect1.id)
    print(rect2.id)
    
    # Test inherited fields via property
    print("=== Shape Names ===")
    print(circle1.name)
    print(rect1.name)

```

## Error

```
Assembly compilation failed:

error[CS1721]: Class 'Graphics.Circle' cannot have multiple base classes: 'Shapes.BaseShape' and 'Colors.Colorizable'
  --> graphics.cs:14:45
    |
 14 |     return total
    |                 ^
    |

error[CS1721]: Class 'Graphics.Rectangle' cannot have multiple base classes: 'Shapes.BaseShape' and 'Colors.Colorizable'
  --> /tmp/tmpghruubr6/graphics.spy:18:48
    |
 18 |     circle1: Circle = ShapeFactory.create_circle(0.0, 0.0, 5.0, Color.RED)
    |                                                ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpghruubr6/graphics.spy:44:10
    |
 44 |     print(rgb[0])
    |          ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpghruubr6/graphics.spy:63:10
    |
 63 |     print(rect2.id)
    |          ^
    |

error[CS0428]: Cannot convert method group 'Area' to non-delegate type 'double'. Did you intend to invoke the method?
  --> /tmp/tmpghruubr6/graphics.spy:42:30
    |
 42 |     
    |     ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpghruubr6/colors.spy:21:32
    |
 21 |     rect2: Rectangle = Rectangle(Point2D(5.0, 5.0), Size(3.0, 3.0), Color.YELLOW)
    |                                ^
    |

error[CS1061]: 'Colors.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Colors.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpghruubr6/colors.spy:25:32
    |
 25 |     print_shape_info(circle1)
    |                              ^
    |

error[CS1501]: No overload for method 'Describe' takes 1 arguments
  --> /tmp/tmpghruubr6/graphics.spy:26:38
    |
 26 |     print_shape_info(rect1)
    |                            ^
    |

error[CS1501]: No overload for method 'Describe' takes 1 arguments
  --> /tmp/tmpghruubr6/graphics.spy:50:38
    |
 50 |     p1: Point2D = Point2D(0.0, 0.0)
    |                                    ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'name' of 'Shapes.BaseShape.BaseShape(string)'
  --> /tmp/tmpghruubr6/graphics.spy:31:16
    |
 31 |     print(rect1.area())
    |                ^
    |

error[CS0117]: 'Shapes.BaseShape' does not contain a definition for 'Constructor'
  --> /tmp/tmpghruubr6/graphics.spy:11:23
    |
 11 |     total: float = 0.0
    |                       ^
    |

error[CS0117]: 'Colors.Colorizable' does not contain a definition for 'Constructor'
  --> /tmp/tmpghruubr6/graphics.spy:12:25
    |
 12 |     for shape in shapes:
    |                         ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'name' of 'Shapes.BaseShape.BaseShape(string)'
  --> /tmp/tmpghruubr6/graphics.spy:57:16
    |
 57 |     
    |     ^
    |

error[CS0117]: 'Shapes.BaseShape' does not contain a definition for 'Constructor'
  --> /tmp/tmpghruubr6/graphics.spy:35:23
    |
 35 |     total: float = calculate_total_area(shapes)
    |                       ^
    |

error[CS0117]: 'Colors.Colorizable' does not contain a definition for 'Constructor'
  --> /tmp/tmpghruubr6/graphics.spy:36:25
    |
 36 |     print(total)
    |                 ^
    |

error[CS0030]: Cannot convert type 'Graphics.Circle' to 'Colors.Colorizable'
  --> /tmp/tmpghruubr6/main.spy:40:40
    |
 40 |     print(circle2.get_color_name())
    |                                    ^
    |

error[CS0030]: Cannot convert type 'Graphics.Rectangle' to 'Colors.Colorizable'
  --> /tmp/tmpghruubr6/main.spy:41:40
    |
 41 |     print(rect2.get_color_name())
    |                                  ^
    |


```

## Timing

- Generation: 441.02s
- Execution: 5.51s
