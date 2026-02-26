# Successful Dogfood Run

**Timestamp:** 2026-02-25T02:46:56.751182
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### types.spy

```python
# Types module - enums and abstract base classes
enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

@abstract
class Renderable:
    @abstract
    def render(self) -> str:
        ...
```

### structures.spy

```python
# Structures module - structs and utilities
from types import Priority

struct Vector2:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def magnitude(self) -> float:
        return (self.x * self.x + self.y * self.y) ** 0.5
```

### services.spy

```python
# Services module - component hierarchy
from types import Priority, Renderable
from structures import Vector2

@abstract
class Component(Renderable):
    name: str
    priority: Priority

    def __init__(self, name: str):
        self.name = name
        self.priority = Priority.MEDIUM

    @virtual
    def compute(self) -> int:
        return 0

    @override
    def render(self) -> str:
        return "Component(" + self.name + ")"

class TransformComponent(Component):
    position: Vector2
    scale: float

    def __init__(self, name: str, x: float, y: float):
        super().__init__(name)
        self.position = Vector2(x, y)
        self.scale = 1.0

    @override
    def render(self) -> str:
        return "Transform(" + self.name + ")"

    def transform(self, factor: float) -> None:
        self.scale = self.scale * factor

    @override
    def compute(self) -> int:
        return 1

class SpriteComponent(Component):
    texture: str

    def __init__(self, name: str, texture: str):
        super().__init__(name)
        self.texture = texture
        self.priority = Priority.HIGH

    @override
    def render(self) -> str:
        return "Sprite(" + self.texture + ")"

    @override
    def compute(self) -> int:
        return 2
```

### main.spy

```python
# Main entry point - testing complex cross-module features
from types import Priority, Renderable
from structures import Vector2
from services import Component, TransformComponent, SpriteComponent

def main():
    # Test struct creation and magnitude computation
    v: Vector2 = Vector2(3.0, 4.0)
    print(v.magnitude())

    # Test enum value access
    print(Priority.HIGH)

    # Test TransformComponent (inheritance)
    transform: TransformComponent = TransformComponent("Player", 10.0, 20.0)
    print(transform.name)
    print(transform.render())

    # Test transform method (defined on subclass)
    transform.transform(2.0)
    print(transform.scale)

    # Test SpriteComponent
    sprite: SpriteComponent = SpriteComponent("Enemy", "enemy.png")
    print(sprite.render())
    print(sprite.priority)

    # Calculate distance between two points using Vector2
    start: Vector2 = Vector2(0.0, 0.0)
    end: Vector2 = Vector2(6.0, 8.0)
    distance: float = ((end.x - start.x) ** 2 + (end.y - start.y) ** 2) ** 0.5
    print(distance)

    # Test Renderable abstract base class dispatch
    renderable: Renderable = transform
    print(renderable.render())

# EXPECTED OUTPUT:
# 5.0
# High
# Player
# Transform(Player)
# 2.0
# Sprite(enemy.png)
# High
# 10.0
# Transform(Player)
```

## Timing

- Generation: 890.49s
- Execution: 4.42s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
