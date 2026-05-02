<p align="center">
  <img src="editors/vscode/icons/sharpy-icon.png" alt="Sharpy" width="128" />
</p>

<h1 align="center">Sharpy</h1>

<p align="center">
  <a href="https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml"><img src="https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml/badge.svg" alt=".NET 10 Build" /></a>
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET" />
</p>

<p align="center"><strong>A statically-typed Pythonic language for .NET</strong></p>

Sharpy starts with Python's syntax and adds static typing, null safety, tagged unions, and seamless .NET interop — then compiles to idiomatic C# via Roslyn with zero runtime overhead.

```python
# hello.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"

def main():
    message = greet("World")
    print(message)
```

```bash
$ sharpyc run hello.spy
Hello, World!
```

## What Sharpy Adds

### Static Typing with Full Inference

Types are checked at compile time. Inside functions, the compiler infers types so you rarely need to write them — but module-level declarations require annotations.

```python
def main():
    x = 42          # Inferred as int
    pi = 3.14159    # Inferred as float (double)
    name = "hello"  # Inferred as str

# Module-level requires annotations
counter: int = 0
```

### Null Safety

Non-nullable by default. Nullable types are explicit, and the compiler tracks nullability through control flow.

```python
def process(calc: Calculator?, a: int, b: int) -> int:
    result: int? = calc?.add(a, b)  # Null-conditional
    return result ?? 0              # Null coalescing

def check(x: int?) -> None:
    if x is not None:
        print(x + 10)  # Narrowed to int — no unwrap needed
```

### Optional and Result Types

Tagged unions for safe error handling — no exceptions required.

```python
def find_user(name: str) -> str?:
    if name == "Alice":
        return Some("alice@example.com")
    return None()

def safe_divide(a: int, b: int) -> int !str:
    if b == 0:
        return Err("division by zero")
    return Ok(a // b)

def main():
    print(find_user("Alice").unwrap_or("not found"))  # alice@example.com
    print(safe_divide(10, 3).unwrap_or(0))            # 3
```

### Interfaces

No duck typing — implement interfaces explicitly.

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

### Structs (Value Types)

True value semantics — copies on assignment, allocated on the stack.

```python
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p1 = Point(10, 20)
    p2 = p1       # Copy — value semantics
    p2.x = 99
    print(p1.x)   # 10 — original unchanged
```

### Generics

Type-safe generics on classes and functions with bracket syntax.

```python
class Cell[T]:
    value: T

    def __init__(self, initial: T):
        self.value = initial

    def get(self) -> T:
        return self.value

def identity[T](x: T) -> T:
    return x

def main():
    c = Cell[int](42)
    print(c.get())          # 42
    print(identity[str]("hi"))  # hi
```

### Properties

First-class property declarations — no `@property` boilerplate.

```python
class Person:
    property name: str
    property age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
```

### Named Tuples

Lightweight named types via type aliases.

```python
type Point = tuple[x: float, y: float]

def main():
    p: Point = (x=1.0, y=2.0)
    print(p.x)  # 1.0
```

### Pattern Matching with Guards

```python
def describe(value: int):
    match value:
        case 1:
            print("one")
        case x if x > 100:
            print(f"big: {x}")
        case _:
            print("other")
```

### .NET Interop

Import .NET types directly. `snake_case` calls auto-map to `PascalCase` .NET methods.

```python
from system import Console

def main():
    Console.write_line("Hello from .NET!")
    Console.write_line(f"2 + 2 = {2 + 2}")
```

### Async

```python
async def fetch_value() -> str:
    return "hello async"

async def main():
    result: str = await fetch_value()
    print(result)  # hello async
```

## Familiar Python

Classes, inheritance, decorators, comprehensions, f-strings, generators, lambdas, dunder methods, `try`/`except`, `match`, `enum` — they all work as you'd expect. Sharpy is designed so that valid Sharpy code *reads* like Python.

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

def fibonacci(n: int) -> int:
    a, b = (0, 1)
    i = 0
    while i < n:
        yield a
        a, b = (b, a + b)
        i += 1

def main():
    c = Circle(4.0)
    print(f"{c.name}: {c.area()}")  # Circle: 50.24

    doubled = [x * 2 for x in range(5)]
    evens = {x for x in range(10) if x % 2 == 0}
    squares = {x: x * x for x in range(5)}
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

- [Documentation Site](https://antonsynd.github.io/sharpy/) - Full documentation (language reference, stdlib API, tooling)
- [Try Sharpy Online](https://antonsynd.github.io/sharpy/playground/) - Browser-based playground
- [Language Specification](docs/language_specification/) - Complete language reference (source)
- [Contributing Guide](CONTRIBUTING.md) - How to contribute

## Project Structure

```
sharpy/
├── src/
│   ├── Sharpy.Compiler/             # Compiler (lexer, parser, semantic, codegen)
│   ├── Sharpy.Core/                 # Standard library (runtime)
│   ├── Sharpy.Cli/                  # CLI tool
│   ├── Sharpy.Lsp/                  # Language Server Protocol server
│   ├── Sharpy.Compiler.Tests/       # 4,914 test fixtures + unit tests
│   ├── Sharpy.Compiler.Benchmarks/  # Performance benchmarks
│   ├── Sharpy.Core.Tests/           # Runtime library tests
│   └── Sharpy.Lsp.Tests/            # LSP server tests
├── editors/vscode/                  # VS Code extension
├── docs/language_specification/     # Authoritative language specification
└── build_tools/                     # Build automation and dogfooding tools
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

**Links:** [GitHub](https://github.com/antonsynd/sharpy) · [Documentation](https://antonsynd.github.io/sharpy/) · [Playground](https://antonsynd.github.io/sharpy/playground/) · [Issues](https://github.com/antonsynd/sharpy/issues)
