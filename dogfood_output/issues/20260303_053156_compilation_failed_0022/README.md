# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T05:28:14.813210
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage

from shapes import Point, Rectangle, Circle
from containers import Box, Grid, ShapeContainer
from utils import Calculator, Validator, format_number, sum_points_x

def main():
    # Create and test Point
    p1 = Point(3.0, 4.0)
    p2 = Point(6.0, 8.0)
    
    print("Points:")
    print(str(p1))
    print(str(p2))
    
    # Test distance calculation
    calc = Calculator()
    dist = p1.distance_from_origin()
    print(f"p1 distance: {dist}")
    
    # Test Calculator methods
    hyp = calc.hypotenuse(3.0, 4.0)
    print(f"hypotenuse: {hyp}")
    
    mid = calc.midpoint(p1, p2)
    print(f"midpoint: {mid}")
    
    # Test Rectangle
    print("")
    print("Shapes:")
    rect = Rectangle(5.0, 3.0)
    print(rect.describe())
    
    # Test Circle
    circ = Circle(2.5)
    print(circ.describe())
    
    # Test Validator
    print("")
    print("Validation:")
    validator = Validator()
    print(validator.is_positive(5.0))
    print(validator.is_positive(-2.0))
    print(validator.in_range(7.5, 0.0, 10.0))
    
    # Test Box container
    print("")
    print("Box container:")
    int_box = Box[int]()
    int_box.add(10)
    int_box.add(20)
    int_box.add(30)
    print(int_box.count())
    
    # Test Grid
    print("")
    print("Grid:")
    grid = Grid()
    grid.add_point(Point(1.0, 2.0))
    grid.add_point(Point(5.0, 8.0))
    grid.add_point(Point(3.0, 3.0))
    
    bounds = grid.get_bounds()
    print(f"min_x: {bounds.min_x}")
    print(f"max_x: {bounds.max_x}")
    print(f"min_y: {bounds.min_y}")
    print(f"max_y: {bounds.max_y}")
    
    # Test ShapeContainer
    print("")
    print("Shape container:")
    container = ShapeContainer()
    container.add(Rectangle(4.0, 5.0))
    container.add(Rectangle(2.0, 3.0))
    print(container.total_area())
    
    # Test utils
    print("")
    print("Utils:")
    points = [Point(1.0, 2.0), Point(3.0, 4.0), Point(5.0, 6.0)]
    x_sum = sum_points_x(points)
    print(x_sum)
    
    formatted = format_number(3.14159265, 2)
    print(formatted)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.Shape.Area()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> /tmp/tmp43llm_31/shapes.spy:16:32
    |
 16 |     # Test distance calculation
    |                                ^
    |

error[CS0513]: 'Shapes.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> /tmp/tmp43llm_31/shapes.spy:17:32
    |
 17 |     calc = Calculator()
    |                        ^
    |

error[CS0117]: 'Shapes.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmp43llm_31/shapes.spy:62:27
    |
 62 |     
    |     ^
    |

error[CS1061]: '(double min_x, double min_y, double max_x, double max_y)' does not contain a definition for 'MinX' and no accessible extension method 'MinX' accepting a first argument of type '(double min_x, double min_y, double max_x, double max_y)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp43llm_31/main.spy:64:85
    |
 64 |     print(f"min_x: {bounds.min_x}")
    |                                    ^
    |

error[CS1061]: '(double min_x, double min_y, double max_x, double max_y)' does not contain a definition for 'MaxX' and no accessible extension method 'MaxX' accepting a first argument of type '(double min_x, double min_y, double max_x, double max_y)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp43llm_31/main.spy:65:85
    |
 65 |     print(f"max_x: {bounds.max_x}")
    |                                    ^
    |

error[CS1061]: '(double min_x, double min_y, double max_x, double max_y)' does not contain a definition for 'MinY' and no accessible extension method 'MinY' accepting a first argument of type '(double min_x, double min_y, double max_x, double max_y)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp43llm_31/main.spy:66:85
    |
 66 |     print(f"min_y: {bounds.min_y}")
    |                                    ^
    |

error[CS1061]: '(double min_x, double min_y, double max_x, double max_y)' does not contain a definition for 'MaxY' and no accessible extension method 'MaxY' accepting a first argument of type '(double min_x, double min_y, double max_x, double max_y)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp43llm_31/main.spy:67:85
    |
 67 |     print(f"max_y: {bounds.max_y}")
    |                                    ^
    |

error[CS0117]: 'Shapes.Circle' does not contain a definition for 'Pi'
  --> /tmp/tmp43llm_31/shapes.spy:66:34
    |
 66 |     print(f"min_y: {bounds.min_y}")
    |                                  ^
    |


```

## Timing

- Generation: 203.36s
- Execution: 4.75s
