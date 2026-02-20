# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T08:18:56.533700
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex module imports

from shapes import Circle, Rectangle, Color, Point
from shapes import IDrawable, IMeasurable
from containers import Box, ShapeContainer
from utils import format_number, clamp, Status, Dimension, calculate_statistics

def main():
    # Test 1: Create shapes from shapes module
    circle = Circle(5.0)
    rectangle = Rectangle(4.0, 6.0)
    
    print(circle.draw())
    print(rectangle.draw())
    
    # Test 2: Use geometric shapes with measurements
    print(f"Circle area: {{format_number(circle.area(), 2)}}")
    print(f"Rectangle area: {{format_number(rectangle.area(), 2)}}")
    
    # Test 3: Use ShapeContainer from containers module
    container = ShapeContainer()
    container.add_shape(circle)
    container.add_shape(rectangle)
    print(f"Total area: {{format_number(container.total_area(), 2)}}")
    
    # Test 4: Use Point struct
    p = Point(3.0, 4.0)
    print(f"Point distance: {{format_number(p.distance_from_origin(), 2)}}")
    
    # Test 5: Use Color enum
    c = Color.GREEN
    print(f"Selected color: {{c}}")
    
    # Test 6: Use Status enum from utils
    status = Status.OK
    print(f"Status: {{status}}")
    
    # Test 7: Use Dimension struct
    dim = Dimension(16.0, 9.0)
    print(f"Aspect ratio: {{format_number(dim.aspect_ratio(), 2)}}")
    
    # Test 8: Use generic Box
    int_box = Box[int]()
    int_box.add(10)
    int_box.add(20)
    print(f"Box count: {{int_box.count()}}")
    
    # Test 9: Calculate statistics
    values: list[float] = [10.5, 20.3, 15.7, 8.2]
    stats = calculate_statistics(values)
    print(f"Sum: {{format_number(stats[0], 2)}}")
    print(f"Average: {{format_number(stats[1], 2)}}")

# EXPECTED OUTPUT:
# Drawing circle with radius 5.0
# Drawing rectangle 4.0x6.0
# Circle area: 78.54
# Rectangle area: 24.0
# Total area: 102.54
# Point distance: 5.0
# Selected color: Green
# Status: Ok
# Aspect ratio: 1.78
# Box count: 2
# Sum: 54.7
# Average: 13.67
```

## Error

```
Assembly compilation failed:

error[CS0534]: 'Shapes.Circle' does not implement inherited abstract member 'Shapes.Shape.CalculateArea()'
  --> /tmp/tmp7x28cyz2/shapes.spy:18:18
    |
 18 |     print(f"Rectangle area: {{format_number(rectangle.area(), 2)}}")
    |                  ^
    |

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Shapes.Shape.CalculateArea()'
  --> /tmp/tmp7x28cyz2/shapes.spy:28:18
    |
 28 |     print(f"Point distance: {{format_number(p.distance_from_origin(), 2)}}")
    |                  ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:17:83
    |
 17 |     print(f"Circle area: {{format_number(circle.area(), 2)}}")
    |                                                               ^
    |

error[CS1061]: 'Shapes.Circle' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'Shapes.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7x28cyz2/main.spy:17:104
    |
 17 |     print(f"Circle area: {{format_number(circle.area(), 2)}}")
    |                                                               ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:18:86
    |
 18 |     print(f"Rectangle area: {{format_number(rectangle.area(), 2)}}")
    |                                                                     ^
    |

error[CS1061]: 'Shapes.Rectangle' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'Shapes.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7x28cyz2/main.spy:18:110
    |
 18 |     print(f"Rectangle area: {{format_number(rectangle.area(), 2)}}")
    |                                                                     ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:24:82
    |
 24 |     print(f"Total area: {{format_number(container.total_area(), 2)}}")
    |                                                                       ^
    |

error[CS1061]: 'Containers.ShapeContainer' does not contain a definition for 'total_area' and no accessible extension method 'total_area' accepting a first argument of type 'Containers.ShapeContainer' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7x28cyz2/main.spy:24:106
    |
 24 |     print(f"Total area: {{format_number(container.total_area(), 2)}}")
    |                                                                       ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:28:86
    |
 28 |     print(f"Point distance: {{format_number(p.distance_from_origin(), 2)}}")
    |                                                                             ^
    |

error[CS1061]: 'Shapes.Point' does not contain a definition for 'distance_from_origin' and no accessible extension method 'distance_from_origin' accepting a first argument of type 'Shapes.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7x28cyz2/main.spy:28:102
    |
 28 |     print(f"Point distance: {{format_number(p.distance_from_origin(), 2)}}")
    |                                                                             ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:40:84
    |
 40 |     print(f"Aspect ratio: {{format_number(dim.aspect_ratio(), 2)}}")
    |                                                                     ^
    |

error[CS1061]: 'Utils.Dimension' does not contain a definition for 'aspect_ratio' and no accessible extension method 'aspect_ratio' accepting a first argument of type 'Utils.Dimension' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7x28cyz2/main.spy:40:102
    |
 40 |     print(f"Aspect ratio: {{format_number(dim.aspect_ratio(), 2)}}")
    |                                                                     ^
    |

error[CS0103]: The name 'int_box' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:46:81
    |
 46 |     print(f"Box count: {{int_box.count()}}")
    |                                             ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:51:75
    |
 51 |     print(f"Sum: {{format_number(stats[0], 2)}}")
    |                                                  ^
    |

error[CS0021]: Cannot apply indexing with [] to an expression of type '(double, double)'
  --> /tmp/tmp7x28cyz2/main.spy:51:89
    |
 51 |     print(f"Sum: {{format_number(stats[0], 2)}}")
    |                                                  ^
    |

error[CS0103]: The name 'format_number' does not exist in the current context
  --> /tmp/tmp7x28cyz2/main.spy:52:79
    |
 52 |     print(f"Average: {{format_number(stats[1], 2)}}")
    |                                                      ^
    |

error[CS0021]: Cannot apply indexing with [] to an expression of type '(double, double)'
  --> /tmp/tmp7x28cyz2/main.spy:52:93
    |
 52 |     print(f"Average: {{format_number(stats[1], 2)}}")
    |                                                      ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp7x28cyz2/containers.spy:3:24
    |
  3 | from shapes import Circle, Rectangle, Color, Point
    |                        ^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'rounded' is assigned but never used
  --> /tmp/tmp7x28cyz2/utils.spy:6:3
    |
  6 | from utils import format_number, clamp, Status, Dimension, calculate_statistics
    |   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'p' is assigned but never used
  --> /tmp/tmp7x28cyz2/main.spy:27:5
    |
 27 |     p = Point(3.0, 4.0)
    |     ^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'c' is assigned but never used
  --> /tmp/tmp7x28cyz2/main.spy:31:5
    |
 31 |     c = Color.GREEN
    |     ^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'status' is assigned but never used
  --> /tmp/tmp7x28cyz2/main.spy:35:5
    |
 35 |     status = Status.OK
    |     ^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'dim' is assigned but never used
  --> /tmp/tmp7x28cyz2/main.spy:39:5
    |
 39 |     dim = Dimension(16.0, 9.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'stats' is assigned but never used
  --> /tmp/tmp7x28cyz2/main.spy:50:5
    |
 50 |     stats = calculate_statistics(values)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp7x28cyz2/main.spy:4:20
    |
  4 | from shapes import IDrawable, IMeasurable
    |                    ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp7x28cyz2/main.spy:4:31
    |
  4 | from shapes import IDrawable, IMeasurable
    |                               ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'format_number' is never used
  --> /tmp/tmp7x28cyz2/main.spy:6:19
    |
  6 | from utils import format_number, clamp, Status, Dimension, calculate_stat
```

## Timing

- Generation: 136.65s
- Execution: 4.38s
