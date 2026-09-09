<p align="center">
  <img src="editors/vscode/icons/sharpy-icon.png" alt="Sharpy" width="128" />
</p>

<h1 align="center">Sharpy</h1>

<p align="center">
  <a href="https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml"><img src="https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml/badge.svg" alt=".NET 10 Build" /></a>
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET" />
</p>
<p align="center">
  <a href="https://www.nuget.org/packages/sharpyc"><img src="https://img.shields.io/nuget/v/sharpyc?label=sharpyc" alt="sharpyc NuGet" /></a>
  <a href="https://www.nuget.org/packages/SharpyLang.Core"><img src="https://img.shields.io/nuget/v/SharpyLang.Core?label=SharpyLang.Core" alt="SharpyLang.Core NuGet" /></a>
  <a href="https://www.nuget.org/packages/SharpyLang.Stdlib"><img src="https://img.shields.io/nuget/v/SharpyLang.Stdlib?label=SharpyLang.Stdlib" alt="SharpyLang.Stdlib NuGet" /></a>
  <a href="https://www.nuget.org/packages/SharpyLang.Compiler"><img src="https://img.shields.io/nuget/v/SharpyLang.Compiler?label=SharpyLang.Compiler" alt="SharpyLang.Compiler NuGet" /></a>
</p>

<p align="center"><strong>A statically-typed Pythonic language for .NET</strong></p>

```python
# hello.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"

def main():
    print(greet("World"))
```

```bash
$ sharpyc run hello.spy
Hello, World!
```

## Getting Started

Requires [.NET 10.0 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet tool install -g sharpyc
```

### From Source

```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
dotnet build sharpy.sln
dotnet run --project src/Sharpy.Cli -- run hello.spy
```

## Documentation

- [Documentation Site](https://antonsynd.github.io/sharpy/) — language reference, stdlib API, tooling
- [Playground](https://antonsynd.github.io/sharpy/playground/) — try Sharpy in the browser
- [Language Specification](docs/language_specification/)
- [VS Code Extension](editors/vscode/) — syntax highlighting, LSP integration
- [Editor Integration](docs/tooling/editor-integration.md) — Neovim, Emacs, Sublime Text, Helix, Zed
- [Contributing](CONTRIBUTING.md)

## Community

- [GitHub Issues](https://github.com/antonsynd/sharpy/issues) — bug reports, feature requests
- [GitHub Discussions](https://github.com/antonsynd/sharpy/discussions)

## License

Licensed under either of [Apache License, Version 2.0](LICENSE-APACHE) or [MIT License](LICENSE-MIT) at your option.

Unless you explicitly state otherwise, any contribution intentionally submitted
for inclusion in this project shall be dual licensed as above, without any
additional terms or conditions.
