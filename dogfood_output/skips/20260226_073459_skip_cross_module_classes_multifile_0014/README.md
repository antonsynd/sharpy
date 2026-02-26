# Skipped Dogfood Run

**Timestamp:** 2026-02-26T07:23:58.254152
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'ShapeProcessor' has no member 'processed'
  --> /tmp/tmpdw3zdouk/main.spy:67:11
    |
 67 |     print(processor.processed)
    |           ^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Core types module - defines interfaces and base classes used across modules

interface IShape:
    @abstract
    def area(self) -> float: ...

    @abstract
    def perimeter(self) -> float: ...

    @abstract
    def scale(self, factor: float) -> IShape: ...

    @abstract
    def to_string(self) -> str: ...

@abstract
class ShapeBase:
    name: str
    _id: int

    def __init__(self, name: str):
        self.name = name
        self._id = 0

    @virtual
    def get_info(self) -> str:
        return f"Shape: {self.name}"

    @virtual
    def __str__(self) -> str:
        return f"Shape({self.name})"

    @virtual
    def to_string(self) -> str:
        return f"Shape({self.name})"

    def set_id(self, id: int) -> None:
        self._id = id

    def get_id(self) -> int:
        return self._id

interface IMeasurable:
    @abstract
    def get_dimensions(self) -> tuple[float, float]: ...

enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
    YELLOW = 4

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_to_origin(self) -> float:
        return (self.x * self.x + self.y * self.y) ** 0.5

class ScaleFactor:
    DEFAULT: float = 1.0
    DOUBLE: float = 2.0
    HALF: float = 0.5

    @static
    def apply_scale(value: float, factor: float) -> float:
        return value * factor
```

### shapes.spy

```python
# Shapes module - concrete shape implementations

from types import IShape, ShapeBase, Color, Point, ScaleFactor

class Rectangle(ShapeBase, IShape, IMeasurable):
    width: float
    height: float
    fill_color: Color

    def __init__(self, name: str, width: float, height: float, fill_color: Color):
        super().__init__(name)
        self.width = width
        self.height = height
        self.fill_color = fill_color

    def area(self) -> float:
        return self.width * self.height

    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

    def scale(self, factor: float) -> IShape:
        new_width: float = ScaleFactor.apply_scale(self.width, factor)
        new_height: float = ScaleFactor.apply_scale(self.height, factor)
        new_rect: Rectangle = Rectangle(self.name, new_width, new_height, self.fill_color)
        return new_rect

    @override
    def get_info(self) -> str:
        base_info: str = super().get_info()
        return f"{base_info} [Rectangle {self.width} x {self.height}]"

    @override
    def __str__(self) -> str:
        return f"Rectangle({self.name}, {self.width}, {self.height})"

    @override
    def to_string(self) -> str:
        return self.__str__()

    def get_dimensions(self) -> tuple[float, float]:
        return (self.width, self.height)

class Circle(ShapeBase, IShape):
    radius: float
    center: Point

    def __init__(self, name: str, radius: float, center: Point):
        super().__init__(name)
        self.radius = radius
        self.center = center

    def area(self) -> float:
        return ScaleFactor.DEFAULT * 3.14159 * self.radius * self.radius

    def perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius

    def scale(self, factor: float) -> IShape:
        new_radius: float = ScaleFactor.apply_scale(self.radius, factor)
        new_circle: Circle = Circle(self.name, new_radius, self.center)
        return new_circle

    @override
    def get_info(self) -> str:
        return f"Circle: {self.name}, r={self.radius}"

    @override
    def __str__(self) -> str:
        return f"Circle({self.name}, r={self.radius})"

    @override
    def to_string(self) -> str:
        return self.__str__()

class ColoredCircle(Circle):
    border_color: Color

    def __init__(self, name: str, radius: float, center: Point, border_color: Color):
        super().__init__(name, radius, center)
        self.border_color = border_color

    @override
    def get_info(self) -> str:
        base_info: str = super().get_info()
        return f"{base_info}, border={self.border_color}"
```

### utils.spy

```python
# Utils module - helper functions and classes for shape processing

from types import IShape, ShapeBase, Color

class ShapeProcessor:
    processed_count: int = 0

    def __init__(self):
        self.processed_count = 0

    def process_shape(self, shape: IShape) -> str:
        self.processed_count += 1
        return f"Processed: {shape.to_string()}"

    def get_total_area(self, shape_list: list[IShape]) -> float:
        total: float = 0.0
        for shape in shape_list:
            total += shape.area()
        return total

    def get_count(self) -> int:
        return self.processed_count

    property get processed(self) -> int:
        return self.processed_count

def get_color_by_name(name: str) -> Color:
    if name == "red":
        return Color.RED
    elif name == "green":
        return Color.GREEN
    elif name == "blue":
        return Color.BLUE
    else:
        return Color.YELLOW

def filtered_by_area(shape_list: list[IShape], min_area: float) -> list[IShape]:
    result: list[IShape] = []
    for shape in shape_list:
        if shape.area() >= min_area:
            result.append(shape)
    return result
```

### main.spy

```python
# Main entry point - demonstrates cross-module class usage

from types import ShapeBase, Color, Point, IShape, ScaleFactor
from shapes import Rectangle, Circle, ColoredCircle
from utils import ShapeProcessor, get_color_by_name, filtered_by_area

def describe_shape(shape: IShape) -> str:
    return f"{shape.to_string()} - Area: {shape.area()}, Perimeter: {shape.perimeter()}"

def main():
    # Create shapes from different modules
    rect: Rectangle = Rectangle("box1", 5.0, 3.0, Color.BLUE)
    circ: Circle = Circle("wheel", 2.0, Point(0.0, 0.0))
    colored_circ: ColoredCircle = ColoredCircle("target", 1.5, Point(1.0, 1.0), Color.RED)

    # Test 1: Cross-module inheritance
    print("Test 1: Cross-module inheritance")
    base_ref: ShapeBase = rect
    print(base_ref.get_info())
    base_ref = colored_circ
    print(base_ref.get_info())

    # Test 2: Interface usage across modules
    print("Test 2: Interface usage")
    shapes: list[IShape] = []
    shapes.append(rect)
    shapes.append(circ)
    shapes.append(colored_circ)
    for s in shapes:
        print(describe_shape(s))

    # Test 3: Enum usage across modules
    print("Test 3: Enum usage")
    my_color: Color = get_color_by_name("green")
    print(my_color)

    # Test 4: Static methods and factory functions
    print("Test 4: Static helpers")
    result: float = ScaleFactor.apply_scale(10.0, 0.5)
    print(result)

    # Test 5: Utility class across modules
    print("Test 5: Utility class")
    processor: ShapeProcessor = ShapeProcessor()
    processor.process_shape(rect)
    processor.process_shape(circ)
    print(processor.get_count())

    # Test 6: Scaling (returns IShape from different module)
    print("Test 6: Scaling")
    scaled: IShape = rect.scale(2.0)
    print(scaled.to_string())
    print(scaled.area())

    # Test 7: Filtered shapes
    print("Test 7: Filtered collection")
    all_shapes: list[IShape] = []
    all_shapes.append(rect)
    all_shapes.append(circ)
    all_shapes.append(colored_circ)
    large_shapes: list[IShape] = filtered_by_area(all_shapes, 10.0)
    for ls in large_shapes:
        print(ls.area())

    # Test 8: Cross-module property access
    print("Test 8: Property access")
    print(processor.processed)

    # Test 9: Point struct usage
    print("Test 9: Point struct")
    p: Point = Point(3.0, 4.0)
    print(p.distance_to_origin())
```

## Timing

- Generation: 623.60s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
