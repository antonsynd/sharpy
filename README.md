# Sharpy

[![.NET 9 Build](https://github.com/antonsynd/sharpy/actions/workflows/dotnet9.yml/badge.svg)](https://github.com/antonsynd/sharpy/actions/workflows/dotnet9.yml)
[![.NET 10 Build](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml/badge.svg)](https://github.com/antonsynd/sharpy/actions/workflows/dotnet10.yml)
![.NET](https://img.shields.io/badge/.NET-9.0-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)

**A modern, statically-typed Pythonic language for .NET**

Sharpy brings Python's elegant syntax to the .NET ecosystem with full static typing, seamless C# interoperability, and zero-cost abstractions. Write Python-like code that compiles to performant C# and runs on the .NET runtime.

## What is Sharpy?

Sharpy is a statically-typed language that combines Python's clean, readable syntax with .NET's type safety and performance. While not compatible with Python code directly, Sharpy will feel instantly familiar to Python developers while providing the benefits of compile-time type checking and native .NET integration.

### Key Features

- **🐍 Pythonic Syntax** - Indentation-based blocks, familiar operators, and readable code
- **⚡ Static Typing** - Catch errors at compile time with full type inference
- **🔗 .NET Interoperability** - Seamless integration with C# libraries and the entire .NET ecosystem
- **🎯 Modern Features** - Null-conditional operators (`?.`), null coalescing (`??`), and type narrowing
- **📦 Zero Runtime Overhead** - Compiles to efficient C# code with no runtime interpretation
- **🛠️ Rich Type System** - Generics, nullable types, interfaces, structs, and more

## Quick Example

Here's what Sharpy looks like:

```python
# Constants with type inference
const PI: float = 3.14159

# Classes with auto type inference
class Calculator:
    name: str
    history = ["foo"]  # Type inferre as list[str]

    def __init__(self, name: str):
        self.name = name

    def add(self, a: int, b: int) -> int:
        result = a + b
        self.history.append(f"{a} + {b} = {result}")
        return result

    def get_history(self) -> list[str]:
        return self.history

# Null-safe operations
def process_value(value: str?) -> str:
    # Null-conditional operator
    lower: str? = value?.lower()
    # Null coalescing operator
    return value ?? "default"

# Exception handling
def safe_divide(a: float, b: float) -> float?:
    try:
        return a / b
    except ZeroDivisionError:
        print("Cannot divide by zero")
        return None

# Create and use instances
calc = Calculator("MyCalc")
result = calc.add(5, 3)
print(f"Result: {result}")  # F-strings supported!
```

This compiles to efficient C# code that runs on .NET!

## Language Highlights

### Static Typing with Inference

```python
# Explicit types
x: int = 42
name: str = "Alice"

# Type inference
y = 42              # Inferred as int
pi = 3.14159        # Inferred as double

# Auto keyword for complex types
data: auto = {"name": "Alice", "age": 30}  # Inferred as dict[str, object]
```

### Null Safety

```python
# Nullable types
result: int? = get_optional_value()

# Null-conditional operator (short-circuits on None)
length: int? = text?.length

# Null coalescing operator (not based on truthiness)
value: str = input ?? "default"

# Type narrowing
if result is not None:
    # result is narrowed to int (non-nullable) here
    print(result + 10)
```

### Modern Control Flow

```python
# For loops
for item in collection:
    process(item)

# While loops with else clause
while condition:
    work()
else:
    cleanup()  # Executes if loop completes normally

# Ternary expressions
status = "even" if n % 2 == 0 else "odd"
```

### Generics and Collections

```python
# Generic collections with type safety
numbers: list[int] = [1, 2, 3, 4, 5]
mapping: dict[str, double] = {"pi": 3.14, "e": 2.718}
unique: set[str] = {"x", "y", "z"}

# Nested generics
matrix: list[list[double]] = [[1.0, 2.0], [3.0, 4.0]]
```

### Seamless C# Interop

```python
# Import .NET namespaces
from system.collections.generic import hash_set
from system.linq import enumerable

# Use C# libraries directly with automatic Pythonic snake case
numbers = hash_set[int]()
numbers.add(1)
numbers.add(2)

# Call C# LINQ methods
result = enumerable.range(1, 10).where(lambda x: x % 2 == 0).to_list()
```

## Getting Started

### Prerequisites

- .NET 9.0 or .NET 10.0 SDK

### Building the Compiler

1. Clone the repository:
```bash
git clone https://github.com/antonsynd/sharpy.git
cd sharpy
```

2. Build the compiler and CLI:
```bash
dotnet build
```

### Compiling Your First Sharpy Script

1. Create a file named `hello.spy`:

```python
def greet(name: str) -> None:
    message: str = f"Hello, {name}! Welcome to Sharpy!"
    print(message)

greet("World")
```

2. Compile and view tokens (current functionality):
```bash
dotnet run --project src/Sharpy.Cli -- hello.spy --emit-tokens
```

3. *(Full compilation coming soon)*
```bash
# Future: Compile to executable
sharpyc hello.spy -t exe -o hello.exe

# Future: Run the compiled program
./hello.exe
```

### Try the Examples

Check out the `snippets/` directory for more examples:

```bash
# View a comprehensive example
cat snippets/example_v05.spy

# Tokenize the example
dotnet run --project src/Sharpy.Cli -- snippets/example_v05.spy --emit-tokens
```

## Documentation

- **[Language Reference](docs/language_reference.md)** - Complete language specification
- **[Type System](docs/type_system.md)** - Deep dive into Sharpy's type system
- **[Manual](docs/manual/)** - Guides on specific language features
  - [Variables](docs/manual/variables.md)
  - [Functions](docs/manual/functions.md)
  - [Types](docs/manual/types.md)
  - [Control Flow](docs/manual/control_flow.md)
  - [Error Handling](docs/manual/errors.md)
- **[Feature Support](docs/feature_support.md)** - Current implementation status

## Project Status

Sharpy is actively under development. Current version: **v0.5**

### ✅ Implemented (v0.5)

- **Lexer & Parser** - Full lexical analysis and parsing
- **Type System** - Static typing with inference, generics, nullable types
- **Core Features** - Classes, functions, control flow, exception handling
- **Syntax** - Indentation-based blocks, f-strings, operators
- **CLI Tool** - Token emission for debugging

### 🚧 In Progress

- **Code Generation** - C# code emission from AST
- **Semantic Analysis** - Type checking and validation
- **Standard Library** - Core Sharpy library implementations

### 🔮 Planned

- **v1.0** - Properties, comprehensions, pattern matching, more operators
- **v1.5+** - Async/await, generators, advanced features

See [v0.5 Feature List](docs/v0.5-feature-list.md) for complete details.

## Design Philosophy

Sharpy follows three core principles in order of priority:

1. **Sharpy is a .NET language** - Inherits design choices from .NET CLI and C#
2. **Sharpy is Pythonic** - Adopts Python's syntax, semantics, and conventions where possible
3. **Pragmatic compatibility** - When principles conflict, prefer .NET unless zero-cost abstractions are possible

This means Sharpy feels like Python but thinks like C#. You get Python's readable syntax with .NET's performance and type safety.

## Contributing

We welcome contributions! Whether you're:

- 🐛 Reporting bugs
- 💡 Suggesting features
- 📖 Improving documentation
- 🔧 Submitting code

Please check out our issues and feel free to open pull requests.

## Project Structure

```
sharpy/
├── src/
│   ├── Sharpy.Core/           # Standard library
│   ├── Sharpy.Compiler/       # Compiler implementation
│   ├── Sharpy.Cli/            # CLI tool (sharpyc)
│   ├── Sharpy.Core.Tests/     # Standard library tests
│   └── Sharpy.Compiler.Tests/ # Compiler tests
├── docs/                      # Documentation
├── snippets/                  # Example Sharpy programs
└── lsp/sharpy/               # VS Code extension (syntax highlighting)
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Links

- **GitHub**: [github.com/antonsynd/sharpy](https://github.com/antonsynd/sharpy)
- **Documentation**: [docs/](docs/)
- **Issues**: [github.com/antonsynd/sharpy/issues](https://github.com/antonsynd/sharpy/issues)
