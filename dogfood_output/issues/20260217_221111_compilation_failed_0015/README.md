# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T22:07:03.136033
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module inheritance and imports

from shapes import Rectangle, Circle, Polygon, IShape, IMeasurable, total_area
from geometry_utils import Color, Point, Dimension, midpoint, distance, LineStyle, StyleConfig, color_to_str
from shape_collection import ShapeManager, ShapeEntry

def main():
    # Create various shapes from shapes module
    rect1: Rectangle = Rectangle(5.0, 3.0)
    rect2: Rectangle = Rectangle(4.0, 4.0)
    circle: Circle = Circle(2.5)
    
    # Test polymorphic area calculation
    shapes: list[IShape] = [rect1, rect2, circle]
    
    print("=== Shape Areas ===")
    area1: float = rect1.area()
    print(area1)
    area2: float = rect2.area()
    print(area2)
    area3: float = circle.area()
    print(area3)
    
    total: float = total_area(shapes)
    print(total)
    
    print("=== Shape Descriptions ===")
    desc1: str = rect1.describe()
    print(desc1)
    desc2: str = circle.describe()
    print(desc2)
    
    print("=== Geometry Utils ===")
    # Test struct usage
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = distance(p1, p2)
    print(dist)
    
    mid: Point = midpoint(p1, p2)
    print(mid.x)
    
    # Test enum usage
    color: Color = Color.BLUE
    color_str: str = color_to_str(color)
    print(color_str)
    
    # Test shape collection manager
    print("=== Shape Collection ===")
    manager: ShapeManager = ShapeManager()
    manager.add(rect1, Color.RED)
    manager.add(circle, Color.GREEN)
    
    manager_count: int = manager.count()
    print(manager_count)
    
    print("=== Style Config ===")
    style: StyleConfig = StyleConfig(Color.YELLOW, LineStyle.DASHED, 2)
    summary: str = style.get_summary()
    print(summary)

# EXPECTED OUTPUT:
# === Shape Areas ===
# 15.0
# 16.0
# 19.6349375
# 50.6349375
# === Shape Descriptions ===
# Rectangle 5.0x3.0
# Circle with radius 2.5
# === Geometry Utils ===
# 5.0
# 1.5
# Blue
# === Shape Collection ===
# 2
# === Style Config ===
# Yellow 2px
```

## Error

```
Assembly compilation failed:

error[CS0533]: 'Shapes.Polygon.Describe()' hides inherited abstract member 'Shapes.Shape.Describe()'
  --> /tmp/tmphs28wlus/shapes.spy:24:31
    |
 24 |     total: float = total_area(shapes)
    |                               ^
    |

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Describe()'
  --> /tmp/tmphs28wlus/shapes.spy:29:18
    |
 29 |     print(desc1)
    |                 ^
    |

error[CS1729]: 'GeometryUtils.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmphs28wlus/geometry_utils.spy:23:20
    |
 23 |     
    |     ^
    |

error[CS1729]: 'GeometryUtils.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmphs28wlus/main.spy:35:38
    |
 35 |     p1: Point = Point(0.0, 0.0)
    |                                ^
    |

error[CS1729]: 'GeometryUtils.Point' does not contain a constructor that takes 2 arguments
  --> /tmp/tmphs28wlus/main.spy:36:38
    |
 36 |     p2: Point = Point(3.0, 4.0)
    |                                ^
    |

error[CS1729]: 'GeometryUtils.Dimension' does not contain a constructor that takes 2 arguments
  --> /tmp/tmphs28wlus/geometry_utils.spy:41:20
    |
 41 |     print(mid.x)
    |                 ^
    |

error[CS1061]: 'Shapes.IShape' does not contain a definition for 'Describe' and no accessible extension method 'Describe' accepting a first argument of type 'Shapes.IShape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmphs28wlus/shape_collection.spy:43:49
    |
 43 |     # Test enum usage
    |                      ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'StyleConfig' is never used
  --> /tmp/tmphs28wlus/shape_collection.spy:3:53
    |
  3 | from shapes import Rectangle, Circle, Polygon, IShape, IMeasurable, total_area
    |                                                     ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Polygon' is never used
  --> /tmp/tmphs28wlus/main.spy:3:39
    |
  3 | from shapes import Rectangle, Circle, Polygon, IShape, IMeasurable, total_area
    |                                       ^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmphs28wlus/main.spy:3:56
    |
  3 | from shapes import Rectangle, Circle, Polygon, IShape, IMeasurable, total_area
    |                                                        ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Dimension' is never used
  --> /tmp/tmphs28wlus/main.spy:4:42
    |
  4 | from geometry_utils import Color, Point, Dimension, midpoint, distance, LineStyle, StyleConfig, color_to_str
    |                                          ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeEntry' is never used
  --> /tmp/tmphs28wlus/main.spy:5:44
    |
  5 | from shape_collection import ShapeManager, ShapeEntry
    |                                            ^^^^^^^^^^
    |


```

## Timing

- Generation: 230.95s
- Execution: 4.40s
