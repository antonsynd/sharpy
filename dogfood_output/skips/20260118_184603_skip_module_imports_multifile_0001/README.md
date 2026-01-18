# Skipped Dogfood Run

**Timestamp:** 2026-01-18T18:45:47.887746
**Skip Reason:** geometry.spy invalid per spec
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** claude
**Test Type:** Multi-file (3 files)

## Source Files

### geometry.spy

```python
# Geometry module providing shapes and interfaces

@interface
class IMeasurable:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...

@abstract
class Shape:
    name: str

    def __init__(self, shape_name: str):
        self.name = shape_name

    def describe(self) -> str:
        return self.name

    @abstract
    def area(self) -> float: ...

class Rectangle(Shape, IMeasurable):
    width: float
    height: float

    def __init__(self, w: float, h: float):
        super().__init__("Rectangle")
        self.width = w
        self.height = h

    @override
    def area(self) -> float:
        return self.width * self.height

    @override
    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

enum ShapeType:
    RECTANGLE = 1
    CIRCLE = 2
    POLYGON = 3
```

### calculator.spy

```python
# Calculator module with computation utilities
from geometry import IMeasurable, ShapeType

struct Dimensions:
    length: float
    width: float
    shape_type: ShapeType

class ShapeCalculator:
    precision: int

    def __init__(self, prec: int):
        self.precision = prec

    def compute_area(self, measurable: IMeasurable) -> float:
        return measurable.area()

    def compute_perimeter(self, measurable: IMeasurable) -> float:
        return measurable.perimeter()

    def round_value(self, value: float) -> int:
        return value to int

def create_dimensions(length: float, width: float, st: ShapeType) -> Dimensions:
    d: Dimensions = Dimensions()
    d.length = length
    d.width = width
    d.shape_type = st
    return d
```

### main.spy

```python
# Main entry point demonstrating cross-module imports and inheritance
from geometry import Rectangle, Shape, IMeasurable, ShapeType
from calculator import ShapeCalculator, create_dimensions, Dimensions

def test_shape_hierarchy():
    rect: Rectangle = Rectangle(5.0, 3.0)
    
    # Test inheritance - Rectangle inherits from Shape
    shape_name: str = rect.describe()
    print(shape_name)
    
    # Test interface implementation
    calc: ShapeCalculator = ShapeCalculator(2)
    area: float = calc.compute_area(rect)
    perimeter: float = calc.compute_perimeter(rect)
    
    print(calc.round_value(area))
    print(calc.round_value(perimeter))

def test_enum_and_struct():
    # Test enum from geometry module
    rect_type: ShapeType = ShapeType.RECTANGLE
    print(rect_type to int)
    
    # Test struct from calculator module
    dims: Dimensions = create_dimensions(10.0, 8.0, ShapeType.RECTANGLE)
    print(dims.length to int)
    print(dims.width to int)
    print(dims.shape_type to int)

def test_polymorphism():
    rect: Rectangle = Rectangle(4.0, 6.0)
    measurable: IMeasurable = rect
    
    area_result: float = measurable.area()
    print(area_result to int)

print(999)
test_shape_hierarchy()
test_enum_and_struct()
test_polymorphism()

# EXPECTED OUTPUT:
# 999
# Rectangle
# 15
# 16
# 1
# 10
# 8
# 1
# 24
```

## Validation Output

```
```
INVALID
Reason: @interface decorator syntax not in phases 0.1.0-0.1.10
Line: 3
Found: @interface
```

**Explanation:**

According to the specification for phase 0.1.7 (Inheritance & Interfaces), interfaces are defined using the `interface` keyword, not an `@interface` decorator:

**Correct syntax (v0.1.7):**
```python
interface IMeasurable:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
```

**Found in code (incorrect):**
```python
@interface
class IMeasurable:
    def area(self) -> float: ...
```

The code attempts to use `@interface` as a decorator on a class, which is NOT part of the allowed syntax. The specification explicitly states interfaces should be declared with `interface IName:` syntax, not as a decorator.

```

## Timing

- Generation: 11.51s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
