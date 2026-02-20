# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T06:49:29.643382
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing cross-module classes
from geometry import Rectangle, Circle, Point, create_unit_square
from shapes3d import Cube, Sphere, Shape3D, origin

def main():
    print("=== Cross-Module Classes Test ===")
    
    # Test imported classes from geometry module
    rect: Rectangle = Rectangle(5.0, 3.0)
    print(rect.draw())
    
    circle: Circle = Circle(2.5)
    print(circle.draw())
    
    # Calculate total area directly using helper function
    # (Avoid list[IShape] which has type inference issues)
    total_area: float = rect.area() + circle.area()
    print(f"Total area: {{total_area}}")
    
    # Test imported factory function
    unit: Rectangle = create_unit_square()
    print(f"Unit rect: {{unit.area()}}")
    
    # Test cross-module inheritance (3D shapes)
    print("=== 3D Shapes ===")
    center: Point = origin()
    cube: Cube = Cube(center, 3.0)
    sphere: Sphere = Sphere(center, 2.0)
    print(f"Cube surface area: {{cube.area()}}")
    print(f"Cube volume: {{cube.volume()}}")
    print(f"Sphere surface area: {{sphere.area()}}")
    print(f"Sphere volume: {{sphere.volume()}}")
    print("=== Complete ===")

# EXPECTED OUTPUT:
# === Cross-Module Classes Test ===
# Rectangle(5.0 x 3.0)
# Circle(r=2.5)
# Total area: 34.567375
# Unit rect: 1.0
# === 3D Shapes ===
# Cube surface area: 54.0
# Cube volume: 27.0
# Sphere surface area: 50.2655
# Sphere volume: 33.5102
# === Complete ===
```

## Error

```
Assembly compilation failed:

error[CS0506]: 'Shapes3d.Cube.Volume()': cannot override inherited member 'Shapes3d.Shape3D.Volume()' because it is not marked virtual, abstract, or override
  --> /tmp/tmpheitcg0h/shapes3d.spy:25:32
    |
 25 |     print("=== 3D Shapes ===")
    |                               ^
    |

error[CS0535]: 'Shapes3d.Cube' does not implement interface member 'Geometry.IShape.Perimeter()'
  --> /tmp/tmpheitcg0h/shapes3d.spy:12:43
    |
 12 |     circle: Circle = Circle(2.5)
    |                                 ^
    |

error[CS0506]: 'Shapes3d.Sphere.Volume()': cannot override inherited member 'Shapes3d.Shape3D.Volume()' because it is not marked virtual, abstract, or override
  --> /tmp/tmpheitcg0h/shapes3d.spy:40:32
    |
 40 | # Unit rect: 1.0
    |                 ^
    |

error[CS0535]: 'Shapes3d.Sphere' does not implement interface member 'Geometry.IShape.Perimeter()'
  --> /tmp/tmpheitcg0h/shapes3d.spy:22:45
    |
 22 |     print(f"Unit rect: {{unit.area()}}")
    |                                         ^
    |

error[CS1061]: 'Geometry.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Geometry.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/geometry.spy:55:25

error[CS1061]: 'Geometry.Circle' does not contain a definition for 'Pi' and no accessible extension method 'Pi' accepting a first argument of type 'Geometry.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/geometry.spy:59:29

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpheitcg0h/geometry.spy:63:60

error[CS0103]: The name 'total_area' does not exist in the current context
  --> /tmp/tmpheitcg0h/main.spy:18:82
    |
 18 |     print(f"Total area: {{total_area}}")
    |                                         ^
    |

error[CS1061]: 'Geometry.Rectangle' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'Geometry.Rectangle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/main.spy:22:86
    |
 22 |     print(f"Unit rect: {{unit.area()}}")
    |                                         ^
    |

error[CS1061]: 'Shapes3d.Cube' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'Shapes3d.Cube' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/main.spy:29:94
    |
 29 |     print(f"Cube surface area: {{cube.area()}}")
    |                                                 ^
    |

error[CS1061]: 'Shapes3d.Cube' does not contain a definition for 'volume' and no accessible extension method 'volume' accepting a first argument of type 'Shapes3d.Cube' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/main.spy:30:88
    |
 30 |     print(f"Cube volume: {{cube.volume()}}")
    |                                             ^
    |

error[CS1061]: 'Shapes3d.Sphere' does not contain a definition for 'area' and no accessible extension method 'area' accepting a first argument of type 'Shapes3d.Sphere' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/main.spy:31:98
    |
 31 |     print(f"Sphere surface area: {{sphere.area()}}")
    |                                                     ^
    |

error[CS1061]: 'Shapes3d.Sphere' does not contain a definition for 'volume' and no accessible extension method 'volume' accepting a first argument of type 'Shapes3d.Sphere' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpheitcg0h/main.spy:32:92
    |
 32 |     print(f"Sphere volume: {{sphere.volume()}}")
    |                                                 ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpheitcg0h/geometry.spy:24:57
    |
 24 |     # Test cross-module inheritance (3D shapes)
    |                                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpheitcg0h/geometry.spy:24:67
    |
 24 |     # Test cross-module inheritance (3D shapes)
    |                                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpheitcg0h/geometry.spy:44:61
    |
 44 | # Sphere surface area: 50.2655
    |                               ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpheitcg0h/geometry.spy:44:76
    |
 44 | # Sphere surface area: 50.2655
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'total_area' is assigned but never used
  --> /tmp/tmpheitcg0h/main.spy:17:5
    |
 17 |     total_area: float = rect.area() + circle.area()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'unit' is assigned but never used
  --> /tmp/tmpheitcg0h/main.spy:21:5
    |
 21 |     unit: Rectangle = create_unit_square()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'cube' is assigned but never used
  --> /tmp/tmpheitcg0h/main.spy:27:5
    |
 27 |     cube: Cube = Cube(center, 3.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'sphere' is assigned but never used
  --> /tmp/tmpheitcg0h/main.spy:28:5
    |
 28 |     sphere: Sphere = Sphere(center, 2.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape3D' is never used
  --> /tmp/tmpheitcg0h/main.spy:3:36
    |
  3 | from shapes3d import Cube, Sphere, Shape3D, origin
    |                                    ^^^^^^^
    |


```

## Timing

- Generation: 481.11s
- Execution: 4.23s
