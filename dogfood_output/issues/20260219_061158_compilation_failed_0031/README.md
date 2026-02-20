# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T06:05:36.522336
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports and usage
# Import hierarchy: main -> widgets -> core (clean, no cycles)
# main -> helpers (standalone)

from core import Person, Entity, IIdentifiable
from helpers import Point, format_value, check_status, Status
from widgets import Widget, Button, Drawable

def process_entity(e: Entity) -> None:
    print(e.get_type())
    print(e.describe())

def main():
    # Test classes with inheritance from core module
    person: Person = Person("Alice", 30)
    
    # Test interface implementation
    identifiable: IIdentifiable = person
    print(identifiable.get_id())
    
    # Test method overriding
    process_entity(person)
    
    # Test enum from helpers
    print(check_status(Status.ACTIVE))
    print(check_status(Status.PENDING))
    
    # Test struct from helpers
    p: Point = Point(3.0, 4.0)
    print(format_value(p.distance_from_origin()))
    
    # Test interface implementation from widgets
    widget: Widget = Widget("Panel", 100, 50)
    drawable: Drawable = widget
    print(drawable.draw())
    
    # Test inheritance chain in widgets
    button: Button = Button("SubmitBtn", 80, 30, "Click Me")
    print(button.draw())
    
    # Test multiple interface implementations
    print(button.get_type())

# EXPECTED OUTPUT:
# 1
# Person
# Entity: Alice, Age: 30
# active
# pending
# 5.00
# Drawing Panel (100x50)
# Drawing SubmitBtn (80x30) [Button: Click Me]
# Widget
```

## Error

```
Assembly compilation failed:

error[CS0506]: 'Widgets.Button.Draw()': cannot override inherited member 'Widgets.Widget.Draw()' because it is not marked virtual, abstract, or override
  --> /tmp/tmpubrpg_ab/widgets.spy:23:32
    |
 23 |     
    |     ^
    |

error[CS1061]: 'Core.Person' does not contain a definition for '_Id' and no accessible extension method '_Id' accepting a first argument of type 'Core.Person' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpubrpg_ab/core.spy:42:25
    |
 42 |     print(button.get_type())
    |                         ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Core.Person.IdCounter'
  --> /tmp/tmpubrpg_ab/core.spy:29:13
    |
 29 |     p: Point = Point(3.0, 4.0)
    |             ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'Core.Person.IdCounter'
  --> /tmp/tmpubrpg_ab/core.spy:29:32
    |
 29 |     p: Point = Point(3.0, 4.0)
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IIdentifiable' is never used
  --> /tmp/tmpubrpg_ab/widgets.spy:3:5
    |
  3 | # main -> helpers (standalone)
    |     ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 352.42s
- Execution: 4.35s
