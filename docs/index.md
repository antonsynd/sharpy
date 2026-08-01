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
- [Source Generators Guide](guides/source_generators.md) -- Practical compile-time code generation examples
- [Playground](https://antonsynd.github.io/sharpy/playground/) -- Try Sharpy in your browser

## Getting Started

```bash
# Install the Sharpy compiler
dotnet tool install -g sharpyc

# Compile and run
sharpyc run hello.spy

# Pass arguments to your program: everything after `--` goes to it
sharpyc run hello.spy -- alpha beta

# Inspect generated C#
sharpyc emit csharp hello.spy
```

!!! warning "CLI change: options take one value per occurrence"

    `--reference`, `--project-reference`, `--module-path` and `--args` each take **one value per
    occurrence** and repeat to collect:

    ```bash
    sharpyc build app.spy --reference a.dll --reference b.dll
    ```

    The multi-value spellings `--reference a.dll b.dll` and `--args a b c` **no longer work**. A
    single occurrence used to keep consuming the bare tokens that followed it, which meant it also
    swallowed the file you were compiling — `sharpyc run --module-path src file.spy` bound
    `file.spy` to `--module-path` and then failed with "Required argument missing".

    Program arguments now have a conventional spelling: everything after a bare `--` is passed to
    the program being run, as with `dotnet run --` and `cargo run --`. Tokens after `--` are never
    read as compiler options, so `sharpyc run app.spy -- --verbose` passes `--verbose` to your
    program. `--args` is **deprecated** in favour of it; `--args a --args b` still works, but
    `--args a b c` does not.

## Features

| Feature | Description |
|---------|-------------|
| Null safety | `T?` opt-in, `??` coalescing, `?.` conditional access |
| Error propagation | `?` operator for ergonomic Result/Optional early-return |
| Pattern matching | `match` with destructuring, guards, and exhaustiveness checking |
| Tagged unions | `Result[T, E]` and `Optional[T]` built in |
| Generics | With variance (`in`/`out`) and type constraints |
| Properties | Auto-properties and function-style with validation |
| .NET interop | Import and use any .NET library directly |
| Comprehensions | List, dict, and set comprehensions |
| Generators | `yield` and `yield from` with lazy evaluation |
