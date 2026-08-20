# SharpyLang.Compiler

Compiler library for the [Sharpy](https://github.com/antonsynd/sharpy) programming language. Embed the Sharpy compiler in your .NET application to compile `.spy` source to C# and .NET assemblies programmatically.

## Usage

```bash
dotnet add package SharpyLang.Compiler
```

```csharp
using Sharpy.Compiler;

var api = new CompilerApi();
var result = api.Compile("""
    def main():
        print("Hello from embedded Sharpy!")
    """);

if (result.Success)
{
    Console.WriteLine(result.GeneratedCSharp);
}
```

## Features

- Full Sharpy compilation pipeline (lexer, parser, semantic analysis, Roslyn code generation)
- Single-file and multi-file (`.spyproj`) compilation
- AST inspection and diagnostic reporting
- Roslyn-based C# emission

## Links

- [Documentation](https://antonsynd.github.io/sharpy/)
- [GitHub](https://github.com/antonsynd/sharpy)
