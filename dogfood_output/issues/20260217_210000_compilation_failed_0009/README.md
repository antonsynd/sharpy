# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T20:56:30.861822
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex cross-module imports

from shapes import Shape, IMeasurable, IRender
from geometry_types import Color, Dimension, ShapeCategory, get_default_color
from shapes_impl import Rectangle, Circle, ShapeFactory

def analyze_shape(measurable: IMeasurable) -> None:
    area: float = measurable.area()
    perimeter: float = measurable.perimeter()
    # Note: print only takes single argument
    print(f"Area: {{area:.2f}}")
    print(f"Perimeter: {{perimeter:.2f}}")

def main():
    print("=== Cross-Module Shape System Demo ===")
    
    # Test ShapeFactory from shapes_impl
    factory = ShapeFactory()
    
    # Test colors from geometry_types
    default_color: Color = get_default_color()
    print(f"Default color: {{default_color}}")
    
    # Create shapes using factory
    rect: Rectangle = factory.create_rectangle("Rect001", 10.0, 5.0)
    circle: Circle = factory.create_circle("Circle001", 7.0)
    
    # Test rectangle - inherits from Shape, implements IMeasurable/IRender
    print("--- Rectangle ---")
    print(rect.info())
    print(rect.describe())
    analyze_shape(rect)
    print(rect.render())
    
    # Test circle - inherits from Shape, implements IMeasurable
    print("--- Circle ---")
    print(circle.info())
    print(circle.describe())
    analyze_shape(circle)
    print(circle.render())
    
    # Test Dimension struct from geometry_types
    print("--- Dimensions ---")
    dim: Dimension = Dimension(20.0, 15.0)
    print(f"Original: {{dim}}")
    scaled: Dimension = dim.scale(2.0)
    print(f"Scaled: {{scaled}}")
    
    # Test ShapeCategory enum
    print("--- Categories ---")
    print(f"Circle category: {{circle.category}}")
    
    print("=== Demo Complete ===")

# EXPECTED OUTPUT:
# === Cross-Module Shape System Demo ===
# Default color: 2
# --- Rectangle ---
# Shape Rect001 (ID: 1)
# Rectangle Rect001 with dimensions 10.0x5.0
# Area: 50.00
# Perimeter: 30.00
# [RECT Rect001 2]
# --- Circle ---
# Shape Circle001 (ID: 2)
# Circle Circle001 with radius 7.0
# Area: 153.94
# Perimeter: 43.98
# [CIRC Circle001 r=7.0]
# --- Dimensions ---
# Original: 20.0x15.0
# Scaled: 40.0x30.0
# --- Categories ---
# Circle category: Circle
# === Demo Complete ===
```

## Error

```
Assembly compilation failed:

error[CS0246]: The type or namespace name 'IRenderable' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:17:61
    |
 17 |     # Test ShapeFactory from shapes_impl
    |                                         ^
    |

error[CS0115]: 'ShapesImpl.Circle.Render()': no suitable method found to override
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:55:32
    |
 55 | # EXPECTED OUTPUT:
    |                   ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes.spy:24:57
    |
 24 |     # Create shapes using factory
    |                                  ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes.spy:24:74
    |
 24 |     # Create shapes using factory
    |                                  ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/geometry_types.spy:26:51
    |
 26 |     circle: Circle = factory.create_circle("Circle001", 7.0)
    |                                                   ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/geometry_types.spy:26:64
    |
 26 |     circle: Circle = factory.create_circle("Circle001", 7.0)
    |                                                             ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:17:61
    |
 17 |     # Test ShapeFactory from shapes_impl
    |                                         ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:17:89
    |
 17 |     # Test ShapeFactory from shapes_impl
    |                                         ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:42:58
    |
 42 |     # Test Dimension struct from geometry_types
    |                                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:42:82
    |
 42 |     # Test Dimension struct from geometry_types
    |                                                ^
    |

error[CS0103]: The name 'default_color' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/main.spy:22:85
    |
 22 |     print(f"Default color: {{default_color}}")
    |                                               ^
    |

error[CS1061]: 'ShapesImpl.Circle' does not contain a definition for 'category' and no accessible extension method 'category' accepting a first argument of type 'ShapesImpl.Circle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpo_gxhrfy/main.spy:51:94
    |
 51 |     print(f"Circle category: {{circle.category}}")
    |                                                   ^
    |

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:47:20
    |
 47 |     print(f"Scaled: {{scaled}}")
    |                    ^
    |

error[CS0103]: The name 'math' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:52:26
    |
 52 |     
    |     ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:56:57
    |
 56 | # === Cross-Module Shape System Demo ===
    |                                         ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:56:71
    |
 56 | # === Cross-Module Shape System Demo ===
    |                                         ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:29:57
    |
 29 |     print("--- Rectangle ---")
    |                               ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpo_gxhrfy/shapes_impl.spy:29:69
    |
 29 |     print("--- Rectangle ---")
    |                               ^
    |


```

## Compiler Output

```
Name resolution warnings:
warning[SPY0904]: Internal invariant violation: type 'Circle' has 2 unresolved interface names but only 1 resolved interfaces after inheritance resolution. This is a compiler bug — please report it.
  --> /tmp/tmpo_gxhrfy/main.spy

Validation warnings:
warning[SPY0451]: Local variable 'area' is assigned but never used
  --> /tmp/tmpo_gxhrfy/main.spy:8:5
    |
  8 |     area: float = measurable.area()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'perimeter' is assigned but never used
  --> /tmp/tmpo_gxhrfy/main.spy:9:5
    |
  9 |     perimeter: float = measurable.perimeter()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'default_color' is assigned but never used
  --> /tmp/tmpo_gxhrfy/main.spy:21:5
    |
 21 |     default_color: Color = get_default_color()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'scaled' is assigned but never used
  --> /tmp/tmpo_gxhrfy/main.spy:46:5
    |
 46 |     scaled: Dimension = dim.scale(2.0)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpo_gxhrfy/main.spy:3:20
    |
  3 | from shapes import Shape, IMeasurable, IRender
    |                    ^^^^^
    |

warning[SPY0452]: Imported name 'IRender' is never used
  --> /tmp/tmpo_gxhrfy/main.spy:3:40
    |
  3 | from shapes import Shape, IMeasurable, IRender
    |                                        ^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeCategory' is never used
  --> /tmp/tmpo_gxhrfy/main.spy:4:46
    |
  4 | from geometry_types import Color, Dimension, ShapeCategory, get_default_color
    |                                              ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 192.96s
- Execution: 4.22s
