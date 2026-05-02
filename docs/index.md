<p align="center">
  <img src="assets/sharpy-icon.png" alt="Sharpy" width="128" />
</p>

# Sharpy

**A statically-typed Pythonic language for .NET**

Sharpy combines Python's clean syntax with .NET's type system and runtime. Write code that looks like Python, runs on .NET, and catches bugs at compile time.

```python
class Greeter:
    name: str

    def __init__(self, name: str):
        self.name = name

    def greet(self) -> str:
        return f"Hello, {self.name}!"

greeter = Greeter("World")
print(greeter.greet())  # Hello, World!
```

## Core Principles

**Python Syntax** -- Indentation-based blocks, comprehensions, decorators, and dunders. If you know Python, you can read Sharpy.

**Static Typing** -- Every type is known at compile time. `None` requires explicit opt-in via `T?`. No runtime surprises.

**.NET Runtime** -- Compiles to C# and runs on the .NET CLR. Full interop with any .NET library.

## Quick Links

- [Language Reference](language_specification/README.md) -- Complete language specification
- [Standard Library](stdlib/index.md) -- Built-in functions, types, and modules
- [Tooling](tooling/editor-integration.md) -- Editor support and LSP
- [Playground](https://antonsynd.github.io/sharpy/playground/) -- Try Sharpy in your browser

## Getting Started

```bash
# Install the Sharpy compiler
dotnet tool install -g sharpyc

# Compile and run
sharpyc run hello.spy

# Inspect generated C#
sharpyc emit csharp hello.spy
```

## Features

| Feature | Description |
|---------|-------------|
| Null safety | `T?` opt-in, `??` coalescing, `?.` conditional access |
| Pattern matching | `match` with destructuring, guards, and exhaustiveness checking |
| Tagged unions | `Result[T, E]` and `Optional[T]` built in |
| Generics | With variance (`in`/`out`) and type constraints |
| Properties | Auto-properties and function-style with validation |
| .NET interop | Import and use any .NET library directly |
| Comprehensions | List, dict, and set comprehensions |
| Generators | `yield` and `yield from` with lazy evaluation |
