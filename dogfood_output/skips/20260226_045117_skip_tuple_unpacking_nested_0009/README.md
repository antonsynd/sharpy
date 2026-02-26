# Skipped Dogfood Run

**Timestamp:** 2026-02-26T04:43:02.706576
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpw9am9kao/dogfood_test.spy:51:5
    |
 51 |     total_area = 0.0
    |     ^^^^^^^^^^
    |


**Feature Focus:** tuple_unpacking_nested
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex nested tuple unpacking with class hierarchy and geometric calculations
# Using type aliases for named tuples
type Point2D = tuple[x: float, y: float]
type BoundingBox = tuple[top_left: Point2D, bottom_right: Point2D]

class Rectangle:
    _width: float
    _height: float
    
    def __init__(self, w: float, h: float):
        self._width = w
        self._height = h
    
    def get_bounds(self) -> BoundingBox:
        p1: Point2D = (0.0, 0.0)
        p2: Point2D = (self._width, self._height)
        return (p1, p2)

class Shape:
    _bbox: BoundingBox?
    
    def __init__(self):
        self._bbox = None()
    
    def calculate_bounds(self) -> BoundingBox:
        p: Point2D = (0.0, 0.0)
        return (p, p)
    
    def get_bounds(self) -> BoundingBox:
        if self._bbox is not None:
            return self._bbox
        self._bbox = self.calculate_bounds()
        return self._bbox.unwrap()

def analyze_geometry(shape: Shape) -> None:
    bounds = shape.get_bounds()
    ((x1, y1), (x2, y2)) = bounds
    width = x2 - x1
    height = y2 - y1
    area = width * height
    print(width)
    print(height)
    print(area)

def process_multiple_boxes() -> None:
    boxes: list[BoundingBox] = [
        ((0.0, 0.0), (3.0, 4.0)),
        ((1.0, 2.0), (5.0, 7.0)),
        ((0.0, 0.0), (10.0, 10.0))
    ]
    total_area = 0.0
    for (p1, p2) in boxes:
        (a, b) = p1
        (c, d) = p2
        area = (c - a) * (d - b)
        total_area = total_area + area
    print(total_area)

def deep_unpack_demo() -> None:
    nested: tuple[int, tuple[int, tuple[int, int]], int] = (1, (2, (3, 4)), 5)
    (first, (second, (third, fourth)), fifth) = nested
    print(first)
    print(second)
    print(third)
    print(fourth)
    print(fifth)

def main():
    rect = Rectangle(5.0, 3.0)
    analyze_geometry(rect)
    process_multiple_boxes()
    deep_unpack_demo()
```

## Timing

- Generation: 479.94s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
