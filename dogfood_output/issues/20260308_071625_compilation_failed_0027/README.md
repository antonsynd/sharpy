# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T07:02:18.900324
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from utils import Calculator, Rectangle, Circle, Shape, process_shapes, factorial, fibonacci

def main() -> None:
    # Test Calculator
    calc: Calculator = Calculator()
    Calculator.reset()
    
    sum_result: int = calc.add(10, 20)
    prod_result: int = calc.multiply(5, 6)
    print(sum_result)
    print(prod_result)
    
    # Test factorial
    fact5: int = factorial(5)
    print(fact5)
    
    # Test fibonacci
    fib10: int = fibonacci(10)
    print(fib10)
    
    # Test shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.5)
    
    print(rect.area())
    print(rect.perimeter())
    print(circle.area())
    print(circle.perimeter())
    
    # Test polymorphism
    shapes: list[Shape] = [rect, circle]
    i: int = 0
    while i < len(shapes):
        s: Shape = shapes[i]
        print(s.area())
        i = i + 1
    
    # Test process_shapes
    result: tuple[float, float] = process_shapes(shapes)
    print(result[0])
    print(result[1])

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Utils.Shape.Area()' is abstract but it is contained in non-abstract type 'Utils.Shape'
  --> /tmp/tmpzdx6x_5r/utils.spy:28:32
    |
 28 |     print(circle.perimeter())
    |                              ^
    |

error[CS0513]: 'Utils.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Utils.Shape'
  --> /tmp/tmpzdx6x_5r/utils.spy:29:32
    |
 29 |     
    |     ^
    |

error[CS1061]: 'Utils.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:25:63
    |
 25 |     print(rect.area())
    |                       ^
    |

error[CS1061]: 'Utils.Polygon' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Polygon' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:40:57
    |
 40 |     print(result[0])
    |                     ^
    |

error[CS1061]: 'Utils.Polygon' does not contain a definition for 'Sides' and no accessible extension method 'Sides' accepting a first argument of type 'Utils.Polygon' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:40:76
    |
 40 |     print(result[0])
    |                     ^
    |

error[CS1061]: 'Utils.Shape' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:22:18
    |
 22 |     rect: Rectangle = Rectangle(5.0, 3.0)
    |                  ^
    |

error[CS1061]: 'Utils.Polygon' does not contain a definition for 'Sides' and no accessible extension method 'Sides' accepting a first argument of type 'Utils.Polygon' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:37:18
    |
 37 |     
    |     ^
    |

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:55:25

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:55:38

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:58:33

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:58:46

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:61:67

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:61:83

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmpzdx6x_5r/utils.spy:73:20

error[CS1061]: 'Utils.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'Utils.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:73:35

error[CS1061]: 'Utils.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'Utils.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:73:49

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmpzdx6x_5r/utils.spy:77:27

error[CS1061]: 'Utils.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'Utils.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:77:42

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Width' and no accessible extension method 'Width' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:45:18

error[CS1061]: 'Utils.Rectangle' does not contain a definition for 'Height' and no accessible extension method 'Height' accepting a first argument of type 'Utils.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:46:18

error[CS1061]: 'Utils.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'Utils.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:80:64

error[CS1061]: 'Utils.Circle' does not contain a definition for 'Radius' and no accessible extension method 'Radius' accepting a first argument of type 'Utils.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpzdx6x_5r/utils.spy:66:18


```

## Timing

- Generation: 825.53s
- Execution: 5.56s
