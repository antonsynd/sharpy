# Successful Dogfood Run

**Timestamp:** 2026-03-08T20:27:28.590700
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### shapes.spy

```python
interface IMeasurable:
    def measure(self) -> float:
        ...

interface IDrawable:
    def draw(self) -> str:
        ...

struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance_to(self, other: Point) -> float:
        dx: float = other.x - self.x
        dy: float = other.y - self.y
        return (dx * dx + dy * dy) ** 0.5

class Rectangle(IMeasurable, IDrawable):
    width: float
    height: float

    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    @virtual
    def area(self) -> float:
        return self.width * self.height

    def measure(self) -> float:
        return self.area()

    def draw(self) -> str:
        return f"Rectangle({self.width} x {self.height})"

class Cuboid(Rectangle):
    depth: float

    def __init__(self, width: float, height: float, depth: float):
        super().__init__(width, height)
        self.depth = depth

    @override
    def area(self) -> float:
        w: float = self.width
        h: float = self.height
        d: float = self.depth
        return 2.0 * (w * h + h * d + w * d)

```

### animals.spy

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return "..."

    @virtual
    def describe(self) -> str:
        return f"Animal({self.name})"

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def speak(self) -> str:
        return "Woof!"

    @override
    def describe(self) -> str:
        return f"Dog({self.name}, {self.breed})"

class Cat(Animal):
    color: str

    def __init__(self, name: str, color: str):
        super().__init__(name)
        self.color = color

    @override
    def speak(self) -> str:
        return "Meow!"

    @override
    def describe(self) -> str:
        return f"Cat({self.name}, {self.color})"

```

### utils.spy

```python
enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2

class ColorTriple:
    r: int
    g: int
    b: int

    def __init__(self, r: int, g: int, b: int):
        self.r = r
        self.g = g
        self.b = b

class Config:
    @static
    priority_threshold: int = 2

    _name: str
    _value: int

    def __init__(self, name: str, value: int):
        self._name = name
        self._value = value

    def get_display(self) -> str:
        return f"{self._name}={self._value}"

def calculate_priority_score(priority: Priority, multiplier: int) -> int:
    p_val: int = priority.value
    return p_val * multiplier

def create_color(red: int, green: int, blue: int) -> ColorTriple:
    return ColorTriple(r=red, g=green, b=blue)

```

### main.spy

```python
from shapes import Rectangle, Cuboid, Point, IMeasurable, IDrawable
from animals import Animal, Dog, Cat
from utils import Priority, Status, Config, calculate_priority_score, create_color, ColorTriple

def test_shapes():
    rect: Rectangle = Rectangle(4.0, 5.0)
    print(rect.draw())
    print(rect.measure())
    cube: Cuboid = Cuboid(2.0, 3.0, 4.0)
    print(cube.draw())
    print(cube.measure())
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    print(p1.distance_to(p2))

def test_animals():
    dog: Dog = Dog("Rex", "German Shepherd")
    cat: Cat = Cat("Whiskers", "Tabby")
    print(dog.describe())
    print(dog.speak())
    print(cat.describe())
    print(cat.speak())

def test_utils():
    p: Priority = Priority.HIGH
    print(p.value)
    s: Status = Status.ACTIVE
    print(s.name)
    print(Config.priority_threshold)
    cfg: Config = Config("timeout", 30)
    print(cfg.get_display())
    score: int = calculate_priority_score(Priority.MEDIUM, 5)
    print(score)
    rgb: ColorTriple = create_color(255, 128, 64)
    print(rgb.r)
    print(rgb.g)

def main():
    test_shapes()
    test_animals()
    test_utils()

```

## Timing

- Generation: 420.03s
- Execution: 5.12s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
