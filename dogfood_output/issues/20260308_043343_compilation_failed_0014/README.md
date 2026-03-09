# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T04:27:41.434969
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests complex cross-module scenarios
from shapes import IShape, IRenderable, ShapeBase, ShapeType, Point
from concrete_shapes import Circle, Rectangle
from shape_utils import calculate_total_area, filter_by_type, ShapeCollection, create_shape_factory

def print_shape_info(shape: ShapeBase) -> None:
    print(shape.describe())
    print(shape.get_type_name())

def main():
    # Create individual shapes
    c1: Circle = Circle(0.0, 0.0, 5.0)
    c2: Circle = Circle(10.0, 10.0, 3.0)
    r1: Rectangle = Rectangle(0.0, 0.0, 4.0, 6.0)
    r2: Rectangle = Rectangle(5.0, 5.0, 8.0, 2.0)
    
    # Test 1: Shape info through inheritance
    print("=== Shape Descriptions ===")
    print_shape_info(c1)
    print_shape_info(r1)
    
    # Test 2: Point distance calculations
    print("=== Point Distances ===")
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist_sq: float = p1.distance_to(p2)
    print(dist_sq)
    
    # Test 3: Total area calculation with interface dispatch
    print("=== Total Area ===")
    all_shapes: list[ShapeBase] = [c1, c2, r1, r2]
    total: float = calculate_total_area(all_shapes)
    print(total)
    
    # Test 4: ShapeCollection and filtering
    print("=== ShapeCollection ===")
    collection: ShapeCollection = ShapeCollection()
    collection.add(c1)
    collection.add(r1)
    collection.add(c2)
    collection.add(r2)
    print(collection.get_count())
    circles: list[ShapeBase] = collection.find_by_type(ShapeType.CIRCLE)
    print(len(circles))
    
    # Test 5: Factory function
    print("=== Factory Created ===")
    factory_circle: ShapeBase = create_shape_factory("circle", 1.0, 2.0)
    factory_rect: ShapeBase = create_shape_factory("rectangle", 3.0, 4.0)
    print(factory_circle.get_type_name())
    print(factory_rect.get_type_name())
    
    # Test 6: Enum member properties
    print("=== ShapeType Enum ===")
    print(ShapeType.CIRCLE.value)
    print(ShapeType.RECTANGLE.name)
    print(ShapeType.TRIANGLE.name)
    
    # Test 7: Interface-based rendering
    print("=== Render Output ===")
    renderable: IRenderable = c1
    print(renderable.render())
    renderable2: IRenderable = r1
    print(renderable2.render())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.ShapeBase' does not contain a definition for 'Type' and no accessible extension method 'Type' accepting a first argument of type 'Shapes.ShapeBase' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpt9ej7wp_/shapes.spy:20:18
    |
 20 |     print_shape_info(r1)
    |                  ^
    |

error[CS1061]: 'Shapes.ShapeBase' does not contain a definition for 'Type' and no accessible extension method 'Type' accepting a first argument of type 'Shapes.ShapeBase' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpt9ej7wp_/shape_utils.spy:20:23
    |
 20 |     print_shape_info(r1)
    |                       ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpt9ej7wp_/shape_utils.spy:1:53
    |
  1 | # Main entry point - tests complex cross-module scenarios
    |                                                     ^^^^^
    |

warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpt9ej7wp_/shape_utils.spy:2:25
    |
  2 | from shapes import IShape, IRenderable, ShapeBase, ShapeType, Point
    |                         ^^^^^
    |

warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpt9ej7wp_/main.spy:2:20
    |
  2 | from shapes import IShape, IRenderable, ShapeBase, ShapeType, Point
    |                    ^^^^^^
    |

warning[SPY0452]: Imported name 'filter_by_type' is never used
  --> /tmp/tmpt9ej7wp_/main.spy:4:47
    |
  4 | from shape_utils import calculate_total_area, filter_by_type, ShapeCollection, create_shape_factory
    |                                               ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 328.84s
- Execution: 5.26s
