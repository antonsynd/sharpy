# Sharpy

[![.NET 10 Build](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml/badge.svg)](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)

**A modern, statically-typed Pythonic language for .NET**

Sharpy combines Python's elegant syntax with .NET's type safety and performance. Write Python-like code that compiles to CIL and runs on the .NET runtime.

### Key Features

| | |
|---|---|
| **Pythonic Syntax** | Python-style classes, functions, decorators, comprehensions, f-strings |
| **Static Typing** | Full type inference, generics, type narrowing, |
| **Null Safety** | Nullable types (`T \| None`), null-conditional (`?.`), null-coalescing (`??`) |
| **Tagged Unions** | True optional type (`T?`), result type (`T !Exception`) |
| **.NET Interop** | Import and use .NET types directly; `snake_case` auto-maps to `PascalCase` |
| **Zero Runtime Overhead** | Compiles to idiomatic C# via Roslyn - no interpreter, no reflection |

## Quick Example

```python
# hello.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"

def main():
    message = greet("World")
    print(message)
```

```bash
$ dotnet run --project src/Sharpy.Cli -- run hello.spy
Hello, World!
```

## Language Tour

### Types and Inference

```python
# Type inference inside functions
def main():
    x: int = 42           # Explicit type
    y = 42                # Inferred as int
    pi = 3.14159          # Inferred as float (double)

# Module-level requires type annotations
counter: int = 0
```

### Classes and Inheritance

```python
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def area(self) -> float: ...

class Circle(Shape):
    radius: float

    def __init__(self, radius: float):
        super().__init__("Circle")
        self.radius = radius

    @override
    def area(self) -> float:
        return 3.14 * self.radius * self.radius

def main():
    c = Circle(4.0)
    print(c.name)     # Circle
    print(c.area())   # 50.24
```

### Interfaces

```python
interface IDrawable:
    def draw(self) -> str: ...
    def area(self) -> int: ...

class Circle(IDrawable):
    radius: int

    def __init__(self, r: int):
        self.radius = r

    def draw(self) -> str:
        return "Drawing Circle"

    def area(self) -> int:
        return 3 * self.radius * self.radius
```

### Generics

```python
class Cell[T]:
    value: T

    def __init__(self, initial: T):
        self.value = initial

    def get(self) -> T:
        return self.value

    def set(self, new_value: T):
        self.value = new_value

def identity[T](x: T) -> T:
    return x

def main():
    c = Cell[int](10)
    print(c.get())         # 10
    print(identity("hello"))  # hello
```

### Collections and Comprehensions

```python
def main():
    numbers: list[int] = [1, 2, 3, 4, 5]
    mapping: dict[str, float] = {"pi": 3.14, "e": 2.718}
    unique: set[str] = {"x", "y", "z"}

    # List comprehension
    doubled: list[int] = [x * 2 for x in range(5)]

    # Dict comprehension
    squares: dict[int, int] = {x: x * x for x in range(5)}

    # Set comprehension
    evens: set[int] = {x for x in range(10) if x % 2 == 0}

    # Spread operators
    combined: list[int] = [*numbers, 6, 7, 8]
```

### Optional and Result Types

```python
# Optional type - safe tagged union
def find_user(name: str) -> str?:
    if name == "Alice":
        return Some("alice@example.com")

    return None()

# Result type - Rust-style error handling
def safe_divide(a: int, b: int) -> int !str:
    if b == 0:
        return Err("division by zero")

    return Ok(a / b)

def main():
    email: str? = find_user("Alice")
    print(email.unwrap_or("not found"))  # alice@example.com

    result: int !str = safe_divide(10, 3)
    print(result.unwrap_or(0))           # 3
```

### Null Safety and Type Narrowing

Works with optional types too.

```python
def process_value(calc: Calculator?, a: int, b: int) -> int:
    result: int? = calc?.add(a, b)  # Null-conditional
    return result ?? 0              # Null coalescing

def check_value(x: int?) -> None:
    if x is not None:
        print(x + 10)  # Narrowed to int - no unwrap needed
```

### Pattern Matching

```python
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def describe(value: int):
    match value:
        case 1:
            print("one")
        case 42:
            print("forty-two")
        case x if x > 100:
            print(f"big: {x}")
        case _:
            print("other")
```

### Generators

```python
def fibonacci(n: int) -> int:
    a = 0
    b = 1
    i = 0
    while i < n:
        yield a
        a, b = b, a + b
        i += 1

def main():
    for x in fibonacci(8):
        print(x)  # 0, 1, 1, 2, 3, 5, 8, 13
```

### Structs (Value Types)

```python
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p1 = Point(10, 20)
    p2 = p1              # Copy - value semantics
    p2.x = 99
    print(p1.x)          # 10 - original unchanged
```

### Enums

```python
enum TrafficLight:
    RED = 0
    YELLOW = 1
    GREEN = 2

def main():
    current: TrafficLight = TrafficLight.RED
    print(current == TrafficLight.RED)  # True
```

### Named Tuples

```python
type Point = tuple[x: float, y: float]

def main():
    p: Point = (x=1.0, y=2.0)
    print(p.x)  # 1.0
    print(p.y)  # 2.0
```

### Properties

```python
class Person:
    property name: str
    property age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

def main():
    p = Person("Alice", 30)
    print(p.name)  # Alice
    p.name = "Bob"
    print(p.name)  # Bob
```

### Lambdas and Higher-Order Functions

```python
def main():
    double: (int) -> int = lambda x: x * 2
    is_even: (int) -> bool = lambda x: x % 2 == 0

    numbers: list[int] = [1, 2, 3, 4, 5]
    evens: list[int] = list(filter(is_even, numbers))
    doubled: list[int] = list(map(double, evens))
```

### Exception Handling

```python
def safe_divide(a: int, b: int) -> str:
    try:
        result = a / b
    except Exception:
        return "error"
    else:
        return f"result: {result}"

def main():
    print(safe_divide(10, 2))  # result: 5
```

### .NET Interop

```python
from system.io import StringWriter

def main():
    with StringWriter() as writer:
        writer.write("hello")
        print(writer.to_string())  # hello
```

### Async

```python
from system.threading.tasks import Task

async def compute() -> int:
    return 42

def main():
    t: Task[int] = compute()
    print(t.result)  # 42
```

### Dunder Methods

```python
class Vector:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __str__(self) -> str:
        return f"({self.x}, {self.y})"

    def __eq__(self, other: Vector) -> bool:
        return self.x == other.x and self.y == other.y

    def __len__(self) -> int:
        return 2

def main():
    a = Vector(1, 2)
    b = Vector(3, 4)
    c = a + b
    print(c)        # (4, 6)
    print(len(a))   # 2
```

## Getting Started

### Prerequisites

- .NET 10.0 SDK ([Download](https://dotnet.microsoft.com/download))

### Build & Test

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln
dotnet test
```

### Using the Compiler

```bash
# Compile and execute
dotnet run --project src/Sharpy.Cli -- run hello.spy

# View generated C#
dotnet run --project src/Sharpy.Cli -- emit csharp hello.spy

# View parsed AST
dotnet run --project src/Sharpy.Cli -- emit ast hello.spy

# Multi-file project
dotnet run --project src/Sharpy.Cli -- project path/to/project.spyproj
```

## Design Philosophy

Sharpy follows three axioms in strict priority order:

| Priority | Axiom | Meaning |
|----------|-------|---------|
| 1 | **.NET** | Always compiles to valid C# for the CLR |
| 2 | **Types** | Statically typed, non-nullable by default |
| 3 | **Python** | Syntax and idioms yield to the above when conflicts arise |

## Documentation

- [Language Specification](docs/language_specification/) - Complete language reference
- [Contributing Guide](CONTRIBUTING.md) - How to contribute

## Project Structure

```
sharpy/
├── src/
│   ├── Sharpy.Compiler/         # Compiler (lexer, parser, semantic, codegen)
│   ├── Sharpy.Core/             # Standard library (runtime)
│   ├── Sharpy.Cli/              # CLI tool
│   ├── Sharpy.Compiler.Tests/   # 784 test fixtures + unit tests
│   └── Sharpy.Core.Tests/       # Runtime library tests
├── docs/language_specification/  # Authoritative language specification
└── build_tools/                 # Build automation and dogfooding tools
```

## Editor Support

Sharpy includes a Language Server Protocol (LSP) server for IDE integration.

### VSCode

Install the **Sharpy** extension from the marketplace or build from `editors/vscode/`.

### Other Editors

Any editor supporting LSP can connect to the Sharpy language server:

```bash
sharpyc lsp
```

See [docs/tooling/editor-integration.md](docs/tooling/editor-integration.md) for configuration guides for Neovim, Emacs, Sublime Text, Helix, and Zed.

## License

MIT License - see [LICENSE](LICENSE) for details.

**Links:** [GitHub](https://github.com/antonsynd/sharpy) · [Documentation](docs/) · [Issues](https://github.com/antonsynd/sharpy/issues)
