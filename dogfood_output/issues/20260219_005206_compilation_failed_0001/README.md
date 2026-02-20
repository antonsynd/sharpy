# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T00:50:23.869241
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex cross-module interactions
# Tests: inheritance, interfaces, imports, enums, structs, generics

from core_types import Entity, IIdentifiable
from data_shapes import Rectangle, Circle, Point, Color, Shape
from utils import ShapeAnalyzer, create_colored_rectangle, identify_entity

def main():
    # Create objects from different modules and show cross-module inheritance
    rect: Rectangle = Rectangle("MyRect", 5.0, 3.0)
    circle: Circle = Circle("MyCircle", 4.0)
    
    # Test interface implementation across modules
    print(rect.describe())
    print(circle.describe())
    
    # Test interface-based function from utils
    print(identify_entity(rect))
    print(identify_entity(circle))
    
    # Test static helper from utils
    print(ShapeAnalyzer.analyze(rect))
    print(ShapeAnalyzer.analyze(circle))
    
    # Test enum and struct together
    p1: Point = Point(3.0, 4.0)
    color: Color = Color.BLUE
    
    result: str = "distance"
    print(f"Point distance: {{p1.distance_from_origin():.2f}}")
    
    # Test factory function from utils with enum
    colored_rect: Rectangle = create_colored_rectangle(Color.GREEN, 10.0, 5.0)
    print(f"Created {{colored_rect.name}} with color {{colored_rect.color}}")
    
    # Test type checking across module boundaries
    shapes: list[Shape] = [rect, circle]
    print(f"Total shapes: {{len(shapes)}}")

# EXPECTED OUTPUT:
# Rectangle MyRect (5.0 x 3.0)
# Circle MyCircle (r=4.0)
# ID: 1000, Name: MyRect
# ID: 2000, Name: MyCircle
# Shape MyRect has area 15.00
# Shape MyCircle has area 50.27
# Point distance: 5.00
# Created ColoredRect with color Green
# Total shapes: 2
```

## Error

```
Assembly compilation failed:

error[CS0115]: 'DataShapes.Rectangle.Area()': no suitable method found to override
  --> data_shapes.cs:22:32
    |
 22 |     print(ShapeAnalyzer.analyze(rect))
    |                                ^
    |

error[CS0115]: 'DataShapes.Rectangle.Perimeter()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:33:32
    |
 33 |     colored_rect: Rectangle = create_colored_rectangle(Color.GREEN, 10.0, 5.0)
    |                                ^
    |

error[CS0115]: 'DataShapes.Rectangle.GetId()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:37:29
    |
 37 |     shapes: list[Shape] = [rect, circle]
    |                             ^
    |

error[CS0115]: 'DataShapes.Rectangle.GetName()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:41:32
    |
 41 | # Rectangle MyRect (5.0 x 3.0)
    |                               ^
    |

error[CS0115]: 'DataShapes.Circle.Area()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:34:32
    |
 34 |     print(f"Created {{colored_rect.name}} with color {{colored_rect.color}}")
    |                                ^
    |

error[CS0115]: 'DataShapes.Circle.Perimeter()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:63:32

error[CS0115]: 'DataShapes.Circle.GetId()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:67:29

error[CS0115]: 'DataShapes.Circle.GetName()': no suitable method found to override
  --> /tmp/tmp7d28xsld/data_shapes.spy:71:32

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/core_types.spy:16:59
    |
 16 |     
    |     ^
    |

error[CS1503]: Argument 1: cannot convert from 'DataShapes.Color' to 'int'
  --> /tmp/tmp7d28xsld/utils.spy:19:33
    |
 19 |     print(identify_entity(circle))
    |                                 ^
    |

error[CS1061]: 'DataShapes.Point' does not contain a definition for 'distance_from_origin' and no accessible extension method 'distance_from_origin' accepting a first argument of type 'DataShapes.Point' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7d28xsld/main.spy:30:89
    |
 30 |     print(f"Point distance: {{p1.distance_from_origin():.2f}}")
    |                                                                ^
    |

error[CS0103]: The name 'colored_rect' does not exist in the current context
  --> /tmp/tmp7d28xsld/main.spy:34:78
    |
 34 |     print(f"Created {{colored_rect.name}} with color {{colored_rect.color}}")
    |                                                                              ^
    |

error[CS0103]: The name 'colored_rect' does not exist in the current context
  --> /tmp/tmp7d28xsld/main.spy:34:109
    |
 34 |     print(f"Created {{colored_rect.name}} with color {{colored_rect.color}}")
    |                                                                              ^
    |

error[CS0103]: The name 'len' does not exist in the current context
  --> /tmp/tmp7d28xsld/main.spy:38:84
    |
 38 |     print(f"Total shapes: {{len(shapes)}}")
    |                                            ^
    |

error[CS1061]: 'DataShapes.Shape' does not contain a definition for 'name' and no accessible extension method 'name' accepting a first argument of type 'DataShapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7d28xsld/utils.spy:10:63
    |
 10 |     rect: Rectangle = Rectangle("MyRect", 5.0, 3.0)
    |                                                    ^
    |

error[CS1061]: 'DataShapes.Shape' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'DataShapes.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7d28xsld/utils.spy:10:85
    |
 10 |     rect: Rectangle = Rectangle("MyRect", 5.0, 3.0)
    |                                                    ^
    |

error[CS1061]: 'CoreTypes.IIdentifiable' does not contain a definition for 'get_id' and no accessible extension method 'get_id' accepting a first argument of type 'CoreTypes.IIdentifiable' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7d28xsld/utils.spy:23:58
    |
 23 |     print(ShapeAnalyzer.analyze(circle))
    |                                         ^
    |

error[CS1061]: 'CoreTypes.IIdentifiable' does not contain a definition for 'get_name' and no accessible extension method 'get_name' accepting a first argument of type 'CoreTypes.IIdentifiable' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7d28xsld/utils.spy:23:83
    |
 23 |     print(ShapeAnalyzer.analyze(circle))
    |                                         ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'name' of 'CoreTypes.Entity.Entity(string)'
  --> data_shapes.cs:12:27
    |
 12 |     
    |     ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/data_shapes.spy:46:61
    |
 46 | # Shape MyCircle has area 50.27
    |                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/data_shapes.spy:46:74
    |
 46 | # Shape MyCircle has area 50.27
    |                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/data_shapes.spy:46:89
    |
 46 | # Shape MyCircle has area 50.27
    |                                ^
    |

error[CS1729]: 'DataShapes.Shape' does not contain a constructor that takes 1 arguments
  --> /tmp/tmp7d28xsld/data_shapes.spy:49:70
    |
 49 | # Total shapes: 2
    |                  ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/data_shapes.spy:76:58

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp7d28xsld/data_shapes.spy:76:73

error[CS1729]: 'DataShapes.Shape' does not contain a constructor that takes 1 arguments
  --> /tmp/tmp7d28xsld/data_shapes.spy:79:53


```

## Compiler Output

```
warning[SPY0451]: Local variable 'p1' is assigned but never used
  --> /tmp/tmp7d28xsld/main.spy:26:5
    |
 26 |     p1: Point = Point(3.0, 4.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'color' is assigned but never used
  --> /tmp/tmp7d28xsld/main.spy:27:5
    |
 27 |     color: Color = Color.BLUE
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'result' is assigned but never used
  --> /tmp/tmp7d28xsld/main.spy:29:5
    |
 29 |     result: str = "distance"
    |     ^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'colored_rect' is assigned but never used
  --> /tmp/tmp7d28xsld/main.spy:33:5
    |
 33 |     colored_rect: Rectangle = create_colored_rectangle(Color.GREEN, 10.0, 5.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'shapes' is assigned but never used
  --> /tmp/tmp7d28xsld/main.spy:37:5
    |
 37 |     shapes: list[Shape] = [rect, circle]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Entity' is never used
  --> /tmp/tmp7d28xsld/main.spy:4:24
    |
  4 | from core_types import Entity, IIdentifiable
    |                        ^^^^^^
    |

warning[SPY0452]: Imported name 'IIdentifiable' is never used
  --> /tmp/tmp7d28xsld/main.spy:4:32
    |
  4 | from core_types import Entity, IIdentifiable
    |                                ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 85.18s
- Execution: 4.40s
