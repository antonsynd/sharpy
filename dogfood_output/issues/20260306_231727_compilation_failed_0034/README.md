# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T23:11:00.448985
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module imports
from shapes import IShape, Shape, IDrawable
from shapes_impl import Circle, Rectangle
from utils import Point, Color, ShapeValidator, MetricsCollector

def main():
    # Create shapes
    circle: Circle = Circle("Sun", 5.0)
    rect: Rectangle = Rectangle("Door", 3.0, 2.0)

    # Test interface methods
    print(circle.area())
    print(rect.perimeter())

    # Test describe and summary
    print(circle.describe())
    print(rect.summary())

    # Use struct from utils
    p: Point = Point(3.0, 4.0)
    print(p.distance_from_origin())

    # Test enum
    print(Color.RED)

    # Cross-module validation
    validator: ShapeValidator = ShapeValidator()
    print(validator.is_valid(circle))

    # Test metrics collection
    metrics: MetricsCollector = MetricsCollector()
    total: float = 0.0
    shapes: list[IShape] = [circle, rect]
    for shape in shapes:
        total += metrics.record_shape(shape)
    print(total)
    print(metrics.get_count())

```

## Error

```
Assembly compilation failed:

error[CS0501]: 'Shapes.Shape.Describe()' must declare a body because it is not marked abstract, extern, or partial
  --> shapes.cs:25:30
    |
 25 | 
    | ^
    |

error[CS0534]: 'ShapesImpl.Circle' does not implement inherited abstract member 'Shapes.Shape.Area()'
  --> shapes_impl.cs:13:18
    |
 13 |     print(rect.perimeter())
    |                  ^
    |

error[CS0736]: 'ShapesImpl.Circle' does not implement instance interface member 'Shapes.IDrawable.Draw()'. 'ShapesImpl.Circle.Draw()' cannot implement the interface member because it is static.
  --> shapes_impl.cs:13:41
    |
 13 |     print(rect.perimeter())
    |                            ^
    |

error[CS0534]: 'ShapesImpl.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Area()'
  --> /tmp/tmpx009aaqa/shapes_impl.spy:14:18
    |
 14 | 
    | ^
    |

error[CS0736]: 'ShapesImpl.Rectangle' does not implement instance interface member 'Shapes.IDrawable.Draw()'. 'ShapesImpl.Rectangle.Draw()' cannot implement the interface member because it is static.
  --> /tmp/tmpx009aaqa/shapes_impl.spy:14:44
    |
 14 | 
    | ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:40:20

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:40:33

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:44:28

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:44:41

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:14:31
    |
 14 | 
    | ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:14:45
    |
 14 | 
    | ^
    |

error[CS1061]: 'object' does not contain a definition for 'Area' and no accessible extension method 'Area' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpx009aaqa/utils.spy:30:26
    |
 30 |     # Test metrics collection
    |                          ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/utils.spy:11:37
    |
 11 |     # Test interface methods
    |                             ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/utils.spy:11:46
    |
 11 |     # Test interface methods
    |                             ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/utils.spy:11:55
    |
 11 |     # Test interface methods
    |                             ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/utils.spy:11:64
    |
 11 |     # Test interface methods
    |                             ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:48:63

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:48:78

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:48:93

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:52:71

error[CS1061]: 'object' does not contain a definition for 'Area' and no accessible extension method 'Area' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpx009aaqa/utils.spy:20:26
    |
 20 |     p: Point = Point(3.0, 4.0)
    |                          ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:18:38
    |
 18 | 
    | ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:22:60
    |
 22 | 
    | ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:22:87
    |
 22 | 
    | ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmpx009aaqa/shapes_impl.spy:26:68
    |
 26 |     # Cross-module validation
    |                              ^
    |

error[CS0176]: Member 'ShapesImpl.Circle.Area()' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpx009aaqa/main.spy:12:39
    |
 12 |     print(circle.area())
    |                         ^
    |

error[CS0176]: Member 'ShapesImpl.Rectangle.Perimeter()' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpx009aaqa/main.spy:13:39
    |
 13 |     print(rect.perimeter())
    |                            ^
    |

error[CS0176]: Member 'ShapesImpl.Circle.Describe()' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpx009aaqa/main.spy:16:39
    |
 16 |     print(circle.describe())
    |                             ^
    |

error[CS0176]: Member 'Shapes.Shape.Summary()' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpx009aaqa/main.spy:17:39
    |
 17 |     print(rect.summary())
    |                          ^
    |

error[CS0176]: Member 'Utils.Point.DistanceFromOrigin()' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpx009aaqa/main.spy:21:39
    |
 21 |     print(p.distance_from_origin())
    |                                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpx009aaqa/shapes_impl.spy:2:11
    |
  2 | from shapes import IShape, Shape, IDrawable
    |           ^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpx009aaqa/main.spy:2:28
    |
  2 | from shapes import IShape, Shape, IDrawable
    |                            ^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpx009aaqa/main.spy:2:35
    |
  2 | from shapes import IShape, Shape, IDrawable
    |                                   ^^^^^^^^^
    |


```

## Timing

- Generation: 343.84s
- Execution: 4.98s
