# Sharpy

[![.NET 9 Build](https://github.com/antonsynd/sharpy/actions/workflows/dotnet9.yml/badge.svg)](https://github.com/antonsynd/sharpy/actions/workflows/dotnet9.yml)
[![.NET 10 Build](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml/badge.svg)](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml)
![.NET](https://img.shields.io/badge/.NET-9.0-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)

**A modern, statically-typed Pythonic language for .NET**

Sharpy combines Python's elegant syntax with .NET's type safety and performance. Write Python-like code that compiles to efficient C# and runs on the .NET runtime.

### Key Features

🐍 **Pythonic Syntax** · ⚡ **Static Typing** · 🔗 **.NET Interop** · 🎯 **Null Safety** (`?.`, `??`) · 📦 **Zero Runtime Overhead**

## Quick Example

TODO

## Language Highlights

```python
# Static typing with inference inside functions
def main():
    x: int = 42           # Explicit type
    y = 42                # Inferred as int inside functions
    pi = 3.14159          # Inferred as double

# Module-level requires type annotation
counter: int = 0          # Static field

# Null-safe operations
def process_value(calc: Calculator?, a: int, b: int) -> int:
    result: int? = calc?.add(a, b)  # Null-conditional
    return result ?? 0              # Null coalescing

# Nullable types and type narrowing
def check_value() -> None:
    result: int? = get_optional_value()
    if result is not None:
        print(result + 10)  # Narrowed to int

# Generic collections
def work_with_collections() -> None:
    numbers: list[int] = [1, 2, 3, 4, 5]
    mapping: dict[str, float] = {"pi": 3.14, "e": 2.718}
    unique: set[str] = {"x", "y", "z"}

# C# interop with Pythonic naming
from system.collections.generic import hash_set

def use_dotnet() -> None:
    numbers = hash_set[int]()
    numbers.add(1)
```

## Getting Started

### Prerequisites

- .NET 9.0 or .NET 10.0 SDK ([Download](https://dotnet.microsoft.com/download))

### Build & Test

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln
dotnet test
```

### Using the Compiler

Compile and execute immediately:

```bash
dotnet run --project src/Sharpy.Cli -- run snippets/hello.spy
```

### Your First Program

```python
# hello.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"

# Top-level entry point is main()
def main():
    message = greet("World")
    print(message)
```

## Documentation

- [Language Reference](docs/specs/language_reference.md) — Complete language specification
- [Type System](docs/specs/type_system.md) — Type system details
- [Manual](docs/manual/) — Variables, functions, types, control flow, errors
- [Feature Support](docs/status/feature_support.md) — Implementation status

## Design Philosophy

1. **Sharpy is a .NET language** — Inherits design choices from .NET CLI and C#
2. **Sharpy is Pythonic** — Adopts Python's syntax and conventions where possible
3. **Pragmatic compatibility** — When principles conflict, prefer .NET unless zero-cost abstractions are possible

## Contributing

I welcome contributions! Report bugs, suggest features, improve docs, or submit code.
Check our [issues](https://github.com/antonsynd/sharpy/issues) to get started.

## Project Structure

```
sharpy/
├── src/
│   ├── Sharpy.Core/           # Standard library
│   ├── Sharpy.Compiler/       # Compiler (lexer, parser, semantic, codegen)
│   ├── Sharpy.Cli/            # CLI tool
│   └── *.Tests/               # Test projects
├── docs/                      # Documentation
├── snippets/                  # Example programs
└── lsp/sharpy/               # VS Code extension
```

## License

MIT License — see [LICENSE](LICENSE) for details.

**Links:** [GitHub](https://github.com/antonsynd/sharpy) · [Documentation](docs/) · [Issues](https://github.com/antonsynd/sharpy/issues)
