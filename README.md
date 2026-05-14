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

## Features

- **Static typing with full inference** — types checked at compile time, rarely written inside functions
- **Null safety** — non-nullable by default, nullable types explicit (`T?`), compiler-tracked narrowing
- **Optional and Result types** — tagged unions for safe error handling without exceptions
- **Interfaces** — explicit implementation, no duck typing
- **Structs** — true value semantics, stack-allocated
- **Generics** — type-safe with bracket syntax, variance support (`out`/`in`)
- **Properties** — first-class declarations, no `@property` boilerplate
- **Pattern matching** — `match` with guards, type patterns, destructuring
- **Async/await** — first-class async support
- **.NET interop** — import .NET types directly, `snake_case` auto-maps to `PascalCase`
- **Familiar Python** — classes, inheritance, decorators, comprehensions, f-strings, generators, lambdas, dunder methods

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

[MIT](LICENSE)
