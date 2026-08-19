# sharpyc

The Sharpy compiler — a statically-typed Pythonic language for .NET. Write Python-like code that compiles to C# and runs on the CLR.

## Install

```bash
dotnet tool install -g sharpyc
```

## Quick Start

```bash
# Create hello.spy
echo 'print("Hello from Sharpy!")' > hello.spy

# Compile and run
sharpyc run hello.spy

# View generated C#
sharpyc emit csharp hello.spy

# Build a multi-file project
sharpyc project myapp.spyproj
```

## Features

- Python syntax with static typing
- Compiles to idiomatic C# via Roslyn
- Full .NET interop — use any NuGet package
- Built-in LSP server for editor support
- Pythonic standard library (json, os, re, math, and 60+ modules)

## Links

- [Documentation](https://antonsynd.github.io/sharpy/)
- [GitHub](https://github.com/antonsynd/sharpy)
- [Playground](https://antonsynd.github.io/sharpy/playground/)
