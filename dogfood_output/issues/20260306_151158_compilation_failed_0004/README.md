# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T15:04:10.284976
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and complex usage
from shapes import Rectangle, Circle, IShape
from types import Color, Point, Status, Dimension
from utils import calculate_average, find_max, Counter, Task

def process_shape(shape: IShape):
    print(f"Area: {shape.area()}")
    print(f"Perimeter: {shape.perimeter()}")

def main():
    # Test Counter (static field across instances)
    c1 = Counter()
    c2 = Counter()
    c3 = Counter()
    print(f"Counter total: {Counter.total_count()}")

    # Test Rectangle from shapes module
    rect = Rectangle(5.0, 3.0, Color.RED)
    print(f"Rectangle: {rect.description()}")
    print(f"Area: {rect.area()}")

    # Test Circle from shapes module
    circle = Circle(2.5, Color.BLUE)
    print(f"Circle: {circle.description()}")

    # Test Point struct from types
    p = Point(3.0, 4.0)
    print(f"Point distance: {p.distance_from_origin()}")

    # Test utils functions
    values: list[float] = [4.0, 8.0, 2.0, 10.0, 6.0]
    avg = calculate_average(values)
    mx = find_max(values)
    print(f"Average: {avg}")
    print(f"Max: {mx}")

    # Test Task with enum Status
    task = Task("Compile project")
    info_before = task.get_info()
    task.complete()
    info_after = task.get_info()
    print(f"Before: {info_before}")
    print(f"After: {info_after}")

    # Test processor function with interface
    print("Processing Rectangle:")
    process_shape(rect)

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmp__jmf4pw/shapes.spy:26:18
    |
 26 |     # Test Point struct from types
    |                  ^
    |

error[CS0534]: 'Shapes.Circle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmp__jmf4pw/shapes.spy:34:18
    |
 34 |     print(f"Average: {avg}")
    |                  ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp__jmf4pw/utils.spy:29:10
    |
 29 | 
    | ^
    |

error[CS1061]: 'Types.Status' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Types.Status' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp__jmf4pw/utils.spy:46:79
    |
 46 |     print("Processing Rectangle:")
    |                                   ^
    |

error[CS1061]: 'Types.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Types.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp__jmf4pw/shapes.spy:21:85
    |
 21 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmp__jmf4pw/utils.spy:1:65
    |
  1 | # Main entry point - demonstrates cross-module imports and complex usage
    |                                                                 ^^^^^^^^
    |

warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmp__jmf4pw/utils.spy:2:20
    |
  2 | from shapes import Rectangle, Circle, IShape
    |                    ^^^^^
    |

warning[SPY0452]: Imported name 'Dimension' is never used
  --> /tmp/tmp__jmf4pw/utils.spy:2:27
    |
  2 | from shapes import Rectangle, Circle, IShape
    |                           ^^^^^^^^^
    |

warning[SPY0451]: Local variable 'c1' is assigned but never used
  --> /tmp/tmp__jmf4pw/main.spy:12:5
    |
 12 |     c1 = Counter()
    |     ^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'c2' is assigned but never used
  --> /tmp/tmp__jmf4pw/main.spy:13:5
    |
 13 |     c2 = Counter()
    |     ^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'c3' is assigned but never used
  --> /tmp/tmp__jmf4pw/main.spy:14:5
    |
 14 |     c3 = Counter()
    |     ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmp__jmf4pw/main.spy:3:33
    |
  3 | from types import Color, Point, Status, Dimension
    |                                 ^^^^^^
    |

warning[SPY0452]: Imported name 'Dimension' is never used
  --> /tmp/tmp__jmf4pw/main.spy:3:41
    |
  3 | from types import Color, Point, Status, Dimension
    |                                         ^^^^^^^^^
    |


```

## Timing

- Generation: 425.69s
- Execution: 4.42s
