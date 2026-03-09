# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T13:04:03.002458
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from shapes import Shape
from rectangles import Rectangle, Square
from utils import calculate_total_area, create_description

def main():
    # Create shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    square: Square = Square(4.0)
    generic: Shape = Shape("Generic")

    # Test individual shapes
    print("Individual shapes:")
    print(rect.describe())
    print(rect.area())
    print(square.describe())
    print(square.area())
    print(generic.describe())
    print(generic.area())

    # Test polymorphism
    shapes: list[Rectangle] = [rect, square]
    total: float = calculate_total_area(shapes)
    print("Total area:")
    print(total)

    # Test descriptions via utility
    print("Descriptions:")
    print(create_description(rect))
    print(create_description(square))
    print(create_description(generic))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Rectangles.Square' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Rectangles.Square' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:29:18
    |
 29 |     print(create_description(rect))
    |                  ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Width' and no accessible extension method '_Width' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:19:25
    |
 19 |     print(generic.area())
    |                         ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Height' and no accessible extension method '_Height' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:19:39
    |
 19 |     print(generic.area())
    |                          ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:23:25
    |
 23 |     total: float = calculate_total_area(shapes)
    |                         ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Width' and no accessible extension method '_Width' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:23:43
    |
 23 |     total: float = calculate_total_area(shapes)
    |                                           ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Height' and no accessible extension method '_Height' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:23:65
    |
 23 |     total: float = calculate_total_area(shapes)
    |                                                ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Width' and no accessible extension method '_Width' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:12:29
    |
 12 |     # Test individual shapes
    |                             ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Height' and no accessible extension method '_Height' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:15:29
    |
 15 |     print(rect.area())
    |                       ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Width' and no accessible extension method '_Width' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:8:18
    |
  8 |     rect: Rectangle = Rectangle(5.0, 3.0)
    |                  ^
    |

error[CS1061]: 'Rectangles.Rectangle' does not contain a definition for '_Height' and no accessible extension method '_Height' accepting a first argument of type 'Rectangles.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/rectangles.spy:9:18
    |
  9 |     square: Square = Square(4.0)
    |                  ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/shapes.spy:14:37
    |
 14 |     print(rect.describe())
    |                           ^
    |

error[CS1061]: 'Shapes.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpqb5wf0sv/shapes.spy:6:18
    |
  6 | def main():
    |            ^
    |


```

## Timing

- Generation: 267.94s
- Execution: 4.71s
