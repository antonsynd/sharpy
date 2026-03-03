# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T10:38:51.352910
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Cross-module class test with geometry helpers
# Demonstrates multi-file projects with class inheritance and module imports

from geometry import calculate_perimeter_rectangle, calculate_diagonal_rectangle, format_dimensions, scale_dimension, pi_constant, describe_circle_area

class Shape:
    _shape_name: str
    
    def __init__(self, name: str):
        self._shape_name = name
    
    @virtual
    def area(self) -> float:
        return 0.0
    
    @virtual
    def description(self) -> str:
        return f"Shape({self._shape_name})"
    
    property get name() -> str:
        return self._shape_name

class Circle(Shape):
    _radius: float
    
    def __init__(self, radius: float):
        super().__init__("circle")
        self._radius = radius
    
    @override
    def area(self) -> float:
        return pi_constant() * self._radius * self._radius
    
    @override
    def description(self) -> str:
        return f"Circle(r={self._radius})"
    
    property get radius() -> float:
        return self._radius

class Rectangle(Shape):
    _width: float
    _height: float
    
    def __init__(self, width: float, height: float):
        super().__init__("rectangle")
        self._width = width
        self._height = height
    
    @override
    def area(self) -> float:
        return self._width * self._height
    
    @override
    def description(self) -> str:
        dim_str: str = format_dimensions(self._width, self._height)
        return f"Rectangle({dim_str})"
    
    def scale(self, factor: float) -> Rectangle:
        new_width: float = scale_dimension(self._width, factor)
        new_height: float = scale_dimension(self._height, factor)
        return Rectangle(new_width, new_height)
    
    def perimeter(self) -> float:
        return calculate_perimeter_rectangle(self._width, self._height)
    
    def diagonal(self) -> float:
        return calculate_diagonal_rectangle(self._width, self._height)

def process_shape(s: Shape) -> None:
    print(s.description())
    print(s.area())

def main():
    # Create shapes
    c = Circle(3.0)
    r = Rectangle(4.0, 5.0)
    
    # Test polymorphism
    process_shape(c)
    process_shape(r)
    
    # Test geometry module functions
    print(c.name)
    print(r.name)
    
    # Test rectangle-specific methods using imported functions
    p = r.perimeter()
    print(p)
    
    d = r.diagonal()
    print(d)
    
    # Test scaling
    scaled = r.scale(2.0)
    print(scaled.area())
    
    # Test circle area
    print(c.area())
    
    # Test describe_circle_area from geometry module
    desc = describe_circle_area(2.0)
    print(desc)

```

## Error

```
Assembly compilation failed:

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmphy2ckh63/main.spy:39:24
    |
 39 |         return self._radius
    |                        ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmphy2ckh63/main.spy:21:24
    |
 21 |         return self._shape_name
    |                        ^
    |

error[CS0176]: Member 'Program.Shape.Name' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmphy2ckh63/main.spy:84:39
    |
 84 |     print(c.name)
    |                  ^
    |

error[CS0176]: Member 'Program.Shape.Name' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmphy2ckh63/main.spy:85:39
    |
 85 |     print(r.name)
    |                  ^
    |


```

## Timing

- Generation: 965.07s
- Execution: 4.73s
