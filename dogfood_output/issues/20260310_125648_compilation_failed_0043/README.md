# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T12:53:51.139345
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - cross module classes showcase
from shapes import Shape, Rectangle, Circle
from interfaces import IDrawable, IMeasurable
from utils import total_area, total_perimeter, create_square, describe_all

# Module-level constants
DEFAULT_RADIUS: float = 5.0
PI_VALUE: float = 3.14159

def main():
    # Create shapes from imported classes
    rect: Rectangle = Rectangle("box", 10.0, 5.0)
    circle: Circle = Circle("wheel", DEFAULT_RADIUS)
    square: Rectangle = create_square("square", 4.0)
    
    # Test basic method calls on imported types
    print(rect.area())
    print(rect.perimeter())
    
    # Test description method from base and derived
    descriptions: list[str] = describe_all([rect, circle, square])
    for desc in descriptions:
        print(desc)
    
    # Test area calculations
    shapes: list[Shape] = [rect, circle, square]
    area_sum: float = total_area(shapes)
    print(area_sum)
    
    # Test perimeter calculations
    perim_sum: float = total_perimeter(shapes)
    print(perim_sum)
    
    # Access inherited fields from base class
    print(rect.name)
    print(circle.name)
    
    # Test specific shape properties
    print(rect.width)
    print(rect.height)
    print(circle.radius)
    
    # Calculate expected areas manually
    rect_area: float = 10.0 * 5.0
    circle_area: float = PI_VALUE * DEFAULT_RADIUS ** 2.0
    square_area: float = 4.0 * 4.0
    expected_total: float = rect_area + circle_area + square_area
    print(expected_total)
    
    print("done")

```

## Error

```
Assembly compilation failed:

error[CS1950]: The best overloaded Add method 'List<string>.Add(string)' for the collection initializer has some invalid arguments
  --> /tmp/tmp69mtm54m/main.spy:21:84
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Rectangle' to 'string'
  --> /tmp/tmp69mtm54m/main.spy:21:84
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |

error[CS1950]: The best overloaded Add method 'List<string>.Add(string)' for the collection initializer has some invalid arguments
  --> /tmp/tmp69mtm54m/main.spy:21:90
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Circle' to 'string'
  --> /tmp/tmp69mtm54m/main.spy:21:90
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |

error[CS1950]: The best overloaded Add method 'List<string>.Add(string)' for the collection initializer has some invalid arguments
  --> /tmp/tmp69mtm54m/main.spy:21:98
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Rectangle' to 'string'
  --> /tmp/tmp69mtm54m/main.spy:21:98
    |
 21 |     descriptions: list[str] = describe_all([rect, circle, square])
    |                                                                   ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmp69mtm54m/utils.spy:2:28
    |
  2 | from shapes import Shape, Rectangle, Circle
    |                            ^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp69mtm54m/utils.spy:3:14
    |
  3 | from interfaces import IDrawable, IMeasurable
    |              ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IScalable' is never used
  --> /tmp/tmp69mtm54m/utils.spy:3:25
    |
  3 | from interfaces import IDrawable, IMeasurable
    |                         ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp69mtm54m/utils.spy:3:36
    |
  3 | from interfaces import IDrawable, IMeasurable
    |                                    ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmp69mtm54m/main.spy:3:24
    |
  3 | from interfaces import IDrawable, IMeasurable
    |                        ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmp69mtm54m/main.spy:3:35
    |
  3 | from interfaces import IDrawable, IMeasurable
    |                                   ^^^^^^^^^^^
    |


```

## Timing

- Generation: 158.43s
- Execution: 4.85s
