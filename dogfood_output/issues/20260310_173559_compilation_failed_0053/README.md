# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T17:22:06.540978
**Type:** compilation_failed
**Feature Focus:** property_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Property inheritance with virtual/abstract properties and interface implementation
# Tests that properties correctly follow virtual/override semantics through inheritance chains

interface IMeasurable:
    pass

@abstract
class Shape(IMeasurable):
    _area: float
    _shape_id: int

    def __init__(self):
        self._area = 0.0
        self._shape_id = 0

    @virtual
    property get scale_factor() -> float:
        return 1.0

    property get measurement() -> float:
        return self._area

class Rectangle(Shape):
    width: float
    height: float
    _rect_id: int

    def __init__(self, sid: int, w: float, h: float):
        super().__init__()
        self._rect_id = sid
        self.width = w
        self.height = h
        self._area = self.width * self.height
        self._shape_id = sid

    @override
    property get scale_factor() -> float:
        return 1.5

    property get shape_id() -> int:
        return self._rect_id

class Square(Rectangle):
    side_length: float

    def __init__(self, sid: int, s: float):
        super().__init__(sid, s, s)
        self.side_length = s
        self._area = self.side_length * self.side_length

    @override
    property get scale_factor() -> float:
        return 2.0

    property get perimeter() -> float:
        return 4.0 * self.side_length

def main():
    r: Rectangle = Rectangle(10, 2.0, 3.0)
    sq: Square = Square(20, 4.0)

    # Test rectangle properties
    print(r.shape_id)
    print(r.scale_factor)
    print(r.measurement)

    # Test square properties
    print(sq.shape_id)
    print(sq.scale_factor)
    print(sq.perimeter)
    print(sq.measurement)

    # Test polymorphic access through base reference
    shapes: list[Shape] = [Rectangle(30, 5.0, 6.0), Square(40, 2.0)]
    idx: int = 0
    while idx < len(shapes):
        s: Shape = shapes[idx]
        print(s.scale_factor)
        print(s.measurement)
        idx += 1

    total_area: float = 0.0
    idx = 0
    while idx < len(shapes):
        s: Shape = shapes[idx]
        total_area += s.measurement
        idx += 1
    print(total_area)

```

## Error

```
Assembly compilation failed:

error[CS0112]: A static member cannot be marked as 'virtual'
  --> dogfood_test.cs:19:38
    |
 19 | 
    | ^
    |

error[CS0112]: A static member cannot be marked as 'override'
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:23:39
    |
 23 | class Rectangle(Shape):
    |                        ^
    |

error[CS0112]: A static member cannot be marked as 'override'
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:41:39
    |
 41 |         return self._rect_id
    |                             ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:21:24
    |
 21 |         return self._area
    |                        ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:41:24
    |
 41 |         return self._rect_id
    |                        ^
    |

error[CS0026]: Keyword 'this' is not valid in a static property, static method, or static field initializer
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:56:31
    |
 56 |         return 4.0 * self.side_length
    |                               ^
    |

error[CS0176]: Member 'DogfoodTest.Rectangle.ShapeId' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:63:39
    |
 63 |     print(r.shape_id)
    |                      ^
    |

error[CS0176]: Member 'DogfoodTest.Rectangle.ScaleFactor' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:64:39
    |
 64 |     print(r.scale_factor)
    |                          ^
    |

error[CS0176]: Member 'DogfoodTest.Shape.Measurement' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:65:39
    |
 65 |     print(r.measurement)
    |                         ^
    |

error[CS0176]: Member 'DogfoodTest.Rectangle.ShapeId' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:68:39
    |
 68 |     print(sq.shape_id)
    |                       ^
    |

error[CS0176]: Member 'DogfoodTest.Square.ScaleFactor' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:69:39
    |
 69 |     print(sq.scale_factor)
    |                           ^
    |

error[CS0176]: Member 'DogfoodTest.Square.Perimeter' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:70:39
    |
 70 |     print(sq.perimeter)
    |                        ^
    |

error[CS0176]: Member 'DogfoodTest.Shape.Measurement' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:71:39
    |
 71 |     print(sq.measurement)
    |                          ^
    |

error[CS0176]: Member 'DogfoodTest.Shape.ScaleFactor' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:78:43
    |
 78 |         print(s.scale_factor)
    |                              ^
    |

error[CS0176]: Member 'DogfoodTest.Shape.Measurement' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:79:43
    |
 79 |         print(s.measurement)
    |                             ^
    |

error[CS0176]: Member 'DogfoodTest.Shape.Measurement' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmp2dnlytq3/dogfood_test.spy:86:37
    |
 86 |         total_area += s.measurement
    |                                    ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp2dnlytq3/dogfood_test.cs

```

## Timing

- Generation: 813.03s
- Execution: 4.89s
