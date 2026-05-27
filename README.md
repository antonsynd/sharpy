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

## Features at a Glance

### Tagged Unions & Pattern Matching

```python
union Shape:
    case Circle(radius: float)
    case Rectangle(width: float, height: float)

def area(shape: Shape) -> float:
    match shape:
        case Circle(r):
            return 3.14159 * r * r
        case Rectangle(w, h):
            return w * h

def main():
    print(area(Shape.Circle(5.0)))
    print(area(Shape.Rectangle(3.0, 4.0)))
```

### Result Types

Error handling without exceptions — `T !E` returns either `Ok(value)` or `Err(error)`:

```python
def validate_age(age: int) -> int !str:
    if age < 0:
        return Err("Age cannot be negative")
    return Ok(age)

def main():
    match validate_age(25):
        case Ok(v):
            print(f"Valid: {v}")
        case Err(e):
            print(f"Error: {e}")
```

### Null Safety & Optionals

Non-nullable by default. `T?` tracks nullability, `?.` and `??` navigate safely:

```python
def find(items: list[str], target: str) -> str?:
    for item in items:
        if item == target:
            return Some(item)
    return None()

def main():
    result = find(["apple", "banana"], "banana")
    print(result ?? "not found")
```

### Pipe Operator

Chain transformations left-to-right:

```python
def double(x: int) -> int:
    return x * 2

def add_one(x: int) -> int:
    return x + 1

def main():
    result = 5 |> double() |> add_one()
    print(result)  # 11
```

### Classes & Properties

Python syntax with access modifiers and first-class properties:

```python
class Temperature:
    __celsius: float

    def __init__(self, celsius: float):
        self.__celsius = celsius

    property get fahrenheit(self) -> float:
        return self.__celsius * 9.0 / 5.0 + 32.0

    def __str__(self) -> str:
        return f"{self.__celsius}C ({self.fahrenheit}F)"

def main():
    temp = Temperature(100.0)
    print(temp)
```

**Also:** full type inference, interfaces, structs, generics with variance, async/await, decorators, comprehensions, generators, lambdas, operator overloading, partial application, and seamless .NET interop (`snake_case` auto-maps to `PascalCase`).

[**Try all 13 examples in the playground →**](https://antonsynd.github.io/sharpy/playground/)

## Getting Started

Requires [.NET 10.0 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln
dotnet test

# Compile and run
dotnet run --project src/Sharpy.Cli -- run hello.spy

# View generated C#
dotnet run --project src/Sharpy.Cli -- emit csharp hello.spy
```

## Design Philosophy

Sharpy follows three axioms in strict priority order:

| Priority | Axiom | Meaning |
|----------|-------|---------|
| 1 | **.NET** | Always compiles to valid C# for the CLR |
| 2 | **Types** | Statically typed, non-nullable by default |
| 3 | **Python** | Syntax and idioms yield to the above when conflicts arise |

## Documentation

- [Documentation Site](https://antonsynd.github.io/sharpy/) — language reference, stdlib API, tooling
- [Try Sharpy Online](https://antonsynd.github.io/sharpy/playground/) — browser-based playground
- [Language Specification](docs/language_specification/) — complete language reference (source)
- [VS Code Extension](editors/vscode/) — syntax highlighting, LSP integration
- [Editor Integration](docs/tooling/editor-integration.md) — Neovim, Emacs, Sublime Text, Helix, Zed
- [Contributing](CONTRIBUTING.md)

## License

Licensed under either of [Apache License, Version 2.0](LICENSE-APACHE) or [MIT License](LICENSE-MIT) at your option.

Unless you explicitly state otherwise, any contribution intentionally submitted
for inclusion in this project shall be dual licensed as above, without any
additional terms or conditions.
