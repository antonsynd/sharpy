# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T07:59:45.259920
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from shapes import Shape, Circle, Rectangle, Square
from utils import IdGenerator, NamedShape, MetricsCollector, IIdentifiable

def create_shapes() -> list[Shape]:
    """Factory function using imported classes"""
    shapes: list[Shape] = []
    shapes.append(Circle(3.0))
    shapes.append(Rectangle(4.0, 5.0))
    shapes.append(Square(2.0))
    return shapes

def describe_shape(s: Shape):
    """Uses virtual method dispatch across module boundaries"""
    print(s.describe())
    print(s.area())

def main():
    # Create shapes and demonstrate polymorphism
    shapes = create_shapes()

    # Print descriptions and areas
    for s in shapes:
        describe_shape(s)

    # Use static utility class from utils module
    print(IdGenerator.next_id())
    print(IdGenerator.next_id())

    # Use NamedShape from utils module - using getter methods
    config = NamedShape("TestShape")
    print(config.get_name())
    config.set_enabled(False)
    print(config.get_status())

    # Use metrics collector across modules
    collector = MetricsCollector()
    for s in shapes:
        collector.add_area(s.area())
    print(collector.average())

```

## Error

```
Assembly compilation failed:

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> utils.cs:26:10
    |
 26 |     # Use static utility class from utils module
    |          ^
    |

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmph_9r4524/shapes.spy:22:20
    |
 22 |     # Print descriptions and areas
    |                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IIdentifiable' is never used
  --> /tmp/tmph_9r4524/main.spy:3:62
    |
  3 | from utils import IdGenerator, NamedShape, MetricsCollector, IIdentifiable
    |                                                              ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 664.18s
- Execution: 4.75s
