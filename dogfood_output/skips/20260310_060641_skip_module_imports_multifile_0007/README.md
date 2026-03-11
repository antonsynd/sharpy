# Skipped Dogfood Run

**Timestamp:** 2026-03-10T05:55:03.285747
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'Circle' to parameter of type 'Shape'
  --> /tmp/tmpaz__jljy/main.spy:14:19
    |
 14 |     process_shape(c)
    |                   ^
    |


**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Types module - shared types
# Demonstrates: enums, structs

enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

struct Point:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

```

### shapes.spy

```python
# Shapes module - inheritance demonstration
# Demonstrates: inheritance, virtual/override methods

from types import Color, Point

class Shape:
    _color: Color
    _position: Point
    
    def __init__(self, color: Color, pos: Point):
        self._color = color
        self._position = pos
    
    def get_color(self) -> Color:
        return self._color
    
    def get_position(self) -> Point:
        return self._position
    
    def move(self, dx: float, dy: float) -> None:
        self._position.x = self._position.x + dx
        self._position.y = self._position.y + dy
    
    def draw(self) -> str:
        return "Shape"

class Circle(Shape):
    _radius: float
    
    def __init__(self, color: Color, pos: Point, radius: float):
        super().__init__(color, pos)
        self._radius = radius
    
    def draw(self) -> str:
        return "Circle(r=" + str(self._radius) + ")"
    
    def get_radius(self) -> float:
        return self._radius

class Rectangle(Shape):
    _width: float
    _height: float
    
    def __init__(self, color: Color, pos: Point, width: float, height: float):
        super().__init__(color, pos)
        self._width = width
        self._height = height
    
    def draw(self) -> str:
        return "Rectangle(w=" + str(self._width) + ",h=" + str(self._height) + ")"
    
    def get_width(self) -> float:
        return self._width
    
    def get_height(self) -> float:
        return self._height

```

### utils.spy

```python
# Utils module - helper functions
# Demonstrates: module imports, utility functions

from types import Color, Point
from shapes import Circle

def create_circle_at_origin(color: Color, radius: float) -> Circle:
    origin = Point(0.0, 0.0)
    return Circle(color, origin, radius)

def get_color_name(color: Color) -> str:
    if color == Color.RED:
        return "red"
    elif color == Color.GREEN:
        return "green"
    else:
        return "blue"

class ShapeUtils:
    @static
    SHAPE_COUNT: int = 0
    
    @static
    def increment_count() -> None:
        ShapeUtils.SHAPE_COUNT = ShapeUtils.SHAPE_COUNT + 1
    
    @static
    def get_count() -> int:
        return ShapeUtils.SHAPE_COUNT
    
    @static
    def reset_count() -> None:
        ShapeUtils.SHAPE_COUNT = 0

```

### main.spy

```python
# Main entry point - demonstrates complex cross-module imports
from types import Color, Point
from shapes import Circle, Rectangle, Shape
from utils import create_circle_at_origin, get_color_name, ShapeUtils

def process_shape(s: Shape) -> None:
    ShapeUtils.increment_count()
    print(s.draw())
    print(get_color_name(s.get_color()))

def main():
    # Test 1: Create circle via factory and process it
    c = create_circle_at_origin(Color.RED, 5.0)
    process_shape(c)
    
    # Test 2: Move the circle and verify position
    c.move(3.0, 4.0)
    pos = c.get_position()
    print(str(pos.x))
    print(str(pos.y))
    
    # Test 3: Create rectangle directly and process
    r = Rectangle(Color.GREEN, Point(1.0, 2.0), 10.0, 20.0)
    process_shape(r)
    
    # Test 4: Get rectangle dimensions
    print(str(r.get_width()))
    print(str(r.get_height()))
    
    # Test 5: Verify static counter
    print(str(ShapeUtils.get_count()))
    
    # Test 6: Reset and verify
    ShapeUtils.reset_count()
    print(str(ShapeUtils.get_count()))

```

## Timing

- Generation: 656.23s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
