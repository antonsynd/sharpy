# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T16:21:56.169217
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from shapes import Shape, Rectangle, Circle, ShapeFactory, ShapeCollection
from shape_comparators import ShapeComparer, ShapeAnalyzer
from geo_utils import Point, Color, ShapeType

def create_test_shapes() -> list[Shape]:
    shapes: list[Shape] = []
    
    # Create using factory
    rect: Rectangle = ShapeFactory.create_rectangle(10.0, 5.0)
    circle: Circle = ShapeFactory.create_circle(3.0)
    
    # Create directly with custom centers
    p1: Point = Point(1.0, 2.0)
    p2: Point = Point(5.0, 5.0)
    rect2: Rectangle = Rectangle(4.0, 4.0, Color.RED, p1)
    circle2: Circle = Circle(2.0, Color.BLUE, p2)
    
    shapes.append(rect)
    shapes.append(circle)
    shapes.append(rect2)
    shapes.append(circle2)
    return shapes

def demonstrate_interface_polymorphism(shapes: list[Shape]) -> None:
    analyzer: ShapeAnalyzer = ShapeAnalyzer()
    for shape in shapes:
        result: str = analyzer.analyze_shape(shape)
        print(result)

def demonstrate_scaling(rect: Rectangle, circle: Circle) -> None:
    # Scale the rectangle
    original_area: float = rect.get_area()
    print(original_area)
    new_area: float = rect.scale(2.0)
    print(new_area)
    
    # Scale the circle
    original_c_area: float = circle.get_area()
    print(original_c_area)
    new_c_area: float = circle.scale(2.0)
    print(new_c_area)

def demonstrate_comparisons(shapes: list[Shape]) -> None:
    comparer: ShapeComparer = ShapeComparer()
    if len(shapes) >= 2:
        result: str = comparer.compare_by_area(shapes[0], shapes[1])
        print(result)

def demonstrate_generic_collection(rects: list[Rectangle]) -> None:
    collection: ShapeCollection[Rectangle] = ShapeCollection[Rectangle]()
    for shape in rects:
        collection.add(shape)
    count: int = collection.get_count()
    print(count)
    total: float = collection.get_total_area()
    print(total)

def demonstrate_find_largest(shapes: list[Shape]) -> None:
    comparer: ShapeComparer = ShapeComparer()
    largest: Shape | None = comparer.find_largest(shapes)
    if largest is not None:
        print(largest.get_area())

def main():
    # Test 1: Create shapes and show polymorphic describe
    shapes: list[Shape] = create_test_shapes()
    print("Shapes created")
    
    # Test 2: Interface polymorphism - ShapeAnalyzer uses Shape methods
    demonstrate_interface_polymorphism(shapes)
    
    # Test 3: Cross-module comparison
    demonstrate_comparisons(shapes)
    
    # Test 4: Find largest shape
    demonstrate_find_largest(shapes)
    
    # Test 5: Scaling (mutates shapes) - use specific Rectangle instances
    rect_for_scaling: Rectangle = ShapeFactory.create_rectangle(5.0, 3.0)
    circle_for_scaling: Circle = ShapeFactory.create_circle(2.0)
    print("Before scaling")
    demonstrate_scaling(rect_for_scaling, circle_for_scaling)
    print("After scale area")
    
    # Test 6: Color and ShapeType enums
    col: Color = Color.RED
    st: ShapeType = ShapeType.CIRCLE
    print("Enum values work")
    
    # Test 7: Generic collection with rectangles
    rects: list[Rectangle] = []
    rects.append(ShapeFactory.create_rectangle(2.0, 3.0))
    rects.append(ShapeFactory.create_rectangle(4.0, 5.0))
    demonstrate_generic_collection(rects)

```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'T' to 'GeoUtils.IMeasurable'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmpx3edzryh/shapes.spy:115:51


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpx3edzryh/shape_comparators.spy:2:42
    |
  2 | from shapes import Shape, Rectangle, Circle, ShapeFactory, ShapeCollection
    |                                          ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpx3edzryh/shape_comparators.spy:2:53
    |
  2 | from shapes import Shape, Rectangle, Circle, ShapeFactory, ShapeCollection
    |                                                     ^^^^^^
    |

warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpx3edzryh/shape_comparators.spy:3:7
    |
  3 | from shape_comparators import ShapeComparer, ShapeAnalyzer
    |       ^^^^^
    |

warning[SPY0452]: Imported name 'Color' is never used
  --> /tmp/tmpx3edzryh/shape_comparators.spy:3:14
    |
  3 | from shape_comparators import ShapeComparer, ShapeAnalyzer
    |              ^^^^^
    |

warning[SPY0451]: Local variable 'col' is assigned but never used
  --> /tmp/tmpx3edzryh/main.spy:87:5
    |
 87 |     col: Color = Color.RED
    |     ^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'st' is assigned but never used
  --> /tmp/tmpx3edzryh/main.spy:88:5
    |
 88 |     st: ShapeType = ShapeType.CIRCLE
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 368.80s
- Execution: 4.98s
