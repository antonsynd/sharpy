# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T05:55:55.349164
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from shapes import Shape, Circle, Rectangle, IDrawable
from drawing import Triangle, Canvas, describe_shapes

def main():
    c = Circle(10, 5, 3.0)
    r = Rectangle(0, 0, 4.0, 5.0)
    t = Triangle(2, 3, 6.0, 4.0)
    
    print(c.area())
    
    canvas = Canvas()
    canvas.add(c)
    canvas.add(r)
    canvas.add(t)
    print(canvas.total_area())
    
    descriptions = describe_shapes(canvas.shapes)
    for desc in descriptions:
        print(desc)
    
    drawings = canvas.draw_all()
    for d in drawings:
        print(d)
    
    # Test interface implementation across modules
    if isinstance(c, IDrawable):
        drawable: IDrawable = c
        result: str = drawable.draw()
        print(result)

# EXPECTED OUTPUT:
# 28.27431
# 68.27431
# Circle radius=3.0 at (10, 5)
# Rectangle 4.0x5.0 at (0, 0)
# Triangle base=6.0 height=4.0
# Drawing circle with radius 3.0
# Drawing rectangle 4.0x5.0
# Drawing triangle with base 6.0
# Drawing circle with radius 3.0
```

## Error

```
Assembly compilation failed:

error[CS1073]: Unexpected token 'base'
  --> /tmp/tmpk8o_vzqt/drawing.spy:19:65
    |
 19 |     for desc in descriptions:
    |                              ^
    |

error[CS1001]: Identifier expected
  --> /tmp/tmpk8o_vzqt/drawing.spy:19:70
    |
 19 |     for desc in descriptions:
    |                              ^
    |

error[CS1073]: Unexpected token 'base'
  --> /tmp/tmpk8o_vzqt/drawing.spy:22:78
    |
 22 |     drawings = canvas.draw_all()
    |                                 ^
    |

error[CS1001]: Identifier expected
  --> /tmp/tmpk8o_vzqt/drawing.spy:22:83
    |
 22 |     drawings = canvas.draw_all()
    |                                 ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/drawing.spy:19:65
    |
 19 |     for desc in descriptions:
    |                              ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:66:61

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:66:74

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:66:92

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:66:102

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/drawing.spy:19:84
    |
 19 |     for desc in descriptions:
    |                              ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/drawing.spy:22:78
    |
 22 |     drawings = canvas.draw_all()
    |                                 ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:72:69

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:72:82

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:28:57
    |
 28 |         drawable: IDrawable = c
    |                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:28:73
    |
 28 |         drawable: IDrawable = c
    |                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:28:83
    |
 28 |         drawable: IDrawable = c
    |                                ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:43:65

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:43:83

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:43:93

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmpk8o_vzqt/shapes.spy:49:78


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpk8o_vzqt/drawing.spy:2:20
    |
  2 | from shapes import Shape, Circle, Rectangle, IDrawable
    |                    ^^^^^^
    |

warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpk8o_vzqt/drawing.spy:2:28
    |
  2 | from shapes import Shape, Circle, Rectangle, IDrawable
    |                            ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpk8o_vzqt/drawing.spy:2:50
    |
  2 | from shapes import Shape, Circle, Rectangle, IDrawable
    |                                                  ^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpk8o_vzqt/main.spy:2:20
    |
  2 | from shapes import Shape, Circle, Rectangle, IDrawable
    |                    ^^^^^
    |


```

## Timing

- Generation: 143.27s
- Execution: 4.21s
