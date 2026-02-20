# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T01:41:11.195413
**Type:** output_mismatch
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage

from shapes import Shape, ShapeType, Point, IResizable, IDrawable, IMeasurable
from primitives import Rectangle, Circle, Square
from renderer import Canvas, BoundingBoxCalculator

def main():
    # Test cross-module enum usage
    st: ShapeType = ShapeType.RECTANGLE
    print(st)

    # Create cross-module class instances
    rect = Rectangle("box1", 10.0, 5.0, 0.0, 0.0)
    circ = Circle("wheel", 50.0, 50.0, 10.0)
    square = Square("tile", 8.0, 20.0, 20.0)

    # Test inherited methods across modules
    rect_desc: str = rect.describe()
    print(rect_desc)
    circ_desc: str = circ.describe()
    print(circ_desc)
    square_desc: str = square.describe()
    print(square_desc)

    # Test interface implementation (IDrawable)
    d1: IDrawable = rect
    d2: IDrawable = circ
    draw1: str = d1.draw()
    draw2: str = d2.draw()
    print(draw1)
    print(draw2)

    # Test interface implementation (IMeasurable)
    m1: IMeasurable = rect
    m2: IMeasurable = circ
    area1: float = m1.area()
    area2: float = m2.area()
    print(area1)
    print(area2)

    # Test interface method (IResizable)
    resizable: IResizable = rect
    resizable.resize(2.0)
    print(rect.width)
    print(rect.height)

    # Test Canvas (cross-module composition)
    canvas = Canvas("main_canvas")
    canvas.add_shape(rect)
    canvas.add_shape(circ)
    canvas.add_shape(square)

    # Test Canvas.total_area
    total: float = canvas.total_area()
    print(total)

    # Test Canvas.render_all
    rendered: list[str] = canvas.render_all()
    for line in rendered:
        print(line)

    # Test BoundingBoxCalculator (static cross-module usage)
    bb: tuple[float, float, float, float] = BoundingBoxCalculator.from_rectangle(rect)
    print(bb[0])
    print(bb[1])
    print(bb[2])
    print(bb[3])

    # Test struct from another module
    p1 = Point(3.0, 4.0)
    p2 = Point(0.0, 0.0)
    dist: float = p1.distance_to(p2)
    print(dist)

# EXPECTED OUTPUT:
# Rectangle
# Shape: box1
# Shape: wheel
# Square 'tile' with side 10.0
# Rect[box1]: 20.0x10.0@(0.0,0.0)
# Circle[wheel]: r=10.0@(50.0,50.0)
# 200.0
# 314.1592653589793
# 20.0
# 10.0
# 564.1592653589793
# Rect[box1]: 20.0x10.0@(0.0,0.0)
# Circle[wheel]: r=10.0@(50.0,50.0)
# Square 'tile' with side 10.0
# 0.0
# 0.0
# 20.0
# 10.0
# 5.0
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Rectangle
Shape: box1
Shape: wheel
Square 'tile' with side 10.0
Rect[box1]: 20.0x10.0@(0.0,0.0)
Circle[wheel]: r=10.0@(50.0,50.0)
200.0
314.1592653589793
20.0
10.0
564.1592653589793
Rect[box1]: 20.0x10.0@(0.0,0.0)
Circle[wheel]: r=10.0@(50.0,50.0)
Square 'tile' with side 10.0
0.0
0.0
20.0
10.0
5.0

```

### Actual
```
Rectangle
Shape: box1
Shape: wheel
Square 'tile' with side 8.0
Rect[box1]: 10x5@(0.0,0.0)
Circle[wheel]: r=10.0@(50.0,50.0)
50.0
314.1592653589793
20.0
10.0
578.1592653589794
Rect[box1]: 20x10@(0.0,0.0)
Circle[wheel]: r=10.0@(50.0,50.0)
Rect[tile]: 8x8@(20.0,20.0)
0.0
0.0
20.0
10.0
5.0
```

## Timing

- Generation: 389.88s
- Execution: 4.73s
