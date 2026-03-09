# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T08:34:29.559915
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class features
from shapes_base import Shape, ShapeCategory, IDrawable, IMeasurable
from shapes_concrete import Circle, Rectangle, Triangle
from shape_utils import Point, calculate_total_area, find_largest_shape, create_unit_circle, create_square, filter_shapes, is_large_shape
from shape_controllers import ShapeRenderer, CircleFactory, RandomShapeGenerator, ShapeAnalyzer

def main():
    # Test 1: Create shapes from different modules
    circle: Circle = Circle(5.0)
    rectangle: Rectangle = Rectangle(4.0, 6.0)
    triangle: Triangle = Triangle(3.0, 4.0, 5.0)
    print(circle.name)
    print(rectangle.category.name)
    print(triangle.category.value)

    # Test 2: Test interface implementation across modules
    renderer: ShapeRenderer = ShapeRenderer()
    print(renderer.render(circle))

    # Test 3: Test abstract class inheritance
    analyzer: ShapeAnalyzer = ShapeAnalyzer()
    print(analyzer.analyze(rectangle))

    # Test 4: Test factory pattern
    factory: CircleFactory = CircleFactory(3.0)
    new_circle: Shape = factory.create_shape()
    print(str(new_circle))

    # Test 5: Test utility functions with cross-module types
    shapes: list[Shape] = [circle, rectangle, triangle]
    total: float = calculate_total_area(shapes)
    print(f"{total:.2f}")
    largest: Shape = find_largest_shape(shapes)
    print(largest.name)

    # Test 6: Test struct from another module
    origin: Point = Point(0.0, 0.0)
    print(str(origin))

    # Test 7: Test filtering with higher-order functions (using raw function type)
    large_shapes: list[Shape] = filter_shapes(shapes, is_large_shape)
    print(len(large_shapes))

    # Test 8: Test static methods and enum iteration
    generator: RandomShapeGenerator = RandomShapeGenerator()
    generated: list[Shape] = generator.generate_shapes()
    count: int = analyzer.count_by_category(generated, ShapeCategory.GEOMETRIC)
    print(count)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'ShapesBase.ShapeCategory' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'ShapesBase.ShapeCategory' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmplzwnp4db/main.spy:13:58
    |
 13 |     print(rectangle.category.name)
    |                                   ^
    |

error[CS1061]: 'ShapesBase.ShapeCategory' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'ShapesBase.ShapeCategory' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmplzwnp4db/main.spy:14:57
    |
 14 |     print(triangle.category.value)
    |                                   ^
    |

error[CS1061]: 'ShapesBase.ShapeCategory' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'ShapesBase.ShapeCategory' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmplzwnp4db/shapes_base.spy:36:34
    |
 36 |     # Test 6: Test struct from another module
    |                                  ^
    |


```

## Compiler Output

```
warning[SPY0458]: @virtual is redundant on '__str__' in 'Shape' — it always overrides Object.ToString(). The @virtual decorator will be ignored.
  --> /tmp/tmplzwnp4db/shapes_base.spy:10:10
    |
 10 |     rectangle: Rectangle = Rectangle(4.0, 6.0)
    |          ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmplzwnp4db/shape_utils.spy:2:23
    |
  2 | from shapes_base import Shape, ShapeCategory, IDrawable, IMeasurable
    |                       ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmplzwnp4db/shape_controllers.spy:2:55
    |
  2 | from shapes_base import Shape, ShapeCategory, IDrawable, IMeasurable
    |                                                       ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmplzwnp4db/shape_controllers.spy:4:22
    |
  4 | from shape_utils import Point, calculate_total_area, find_largest_shape, create_unit_circle, create_square, filter_shapes, is_large_shape
    |                      ^^^^^
    |

warning[SPY0452]: Imported name 'calculate_total_area' is never used
  --> /tmp/tmplzwnp4db/shape_controllers.spy:4:29
    |
  4 | from shape_utils import Point, calculate_total_area, find_largest_shape, create_unit_circle, create_square, filter_shapes, is_large_shape
    |                             ^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'filter_shapes' is never used
  --> /tmp/tmplzwnp4db/shape_controllers.spy:4:51
    |
  4 | from shape_utils import Point, calculate_total_area, find_largest_shape, create_unit_circle, create_square, filter_shapes, is_large_shape
    |                                                   ^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'is_large_shape' is never used
  --> /tmp/tmplzwnp4db/shape_controllers.spy:4:66
    |
  4 | from shape_utils import Point, calculate_total_area, find_largest_shape, create_unit_circle, create_square, filter_shapes, is_large_s
```

## Timing

- Generation: 509.60s
- Execution: 5.02s
