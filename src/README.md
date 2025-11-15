# Sharpy Compiler & Runtime

C#-based compiler toolchain for the Sharpy programming language.

## Project Structure

### Sharpy.Compiler
The compiler library that transforms Sharpy source code into executable .NET assemblies.

```
Sharpy.Compiler/
├── Lexer/              # Tokenization
│   ├── Token.cs        # Token types and data structures
│   ├── Lexer.cs        # Lexical analyzer
│   └── LexerError.cs   # Lexer error handling
├── Parser/             # Parsing
│   ├── Ast/            # Abstract Syntax Tree definitions
│   │   ├── Node.cs     # Base AST node types
│   │   ├── Statement.cs # Statement nodes
│   │   ├── Expression.cs # Expression nodes
│   │   └── Types.cs    # Type annotations
│   └── Parser.cs       # (To be implemented)
├── Semantic/           # Semantic analysis
│   ├── Symbol.cs       # Symbol definitions
│   ├── Scope.cs        # Scope management
│   ├── SymbolTable.cs  # Symbol table
│   ├── BuiltinRegistry.cs # Builtin types/functions registry
│   └── SemanticError.cs # Error handling
└── CodeGen/            # Code generation
    ├── RoslynEmitter.cs # Generates C# using Roslyn
    ├── NameMangler.cs   # Name conversion (snake_case → PascalCase)
    └── CodeGenContext.cs # Code generation state
```

### Sharpy.Core
The runtime library that compiled Sharpy code depends on.

```
Sharpy.Core/
├── Builtins/           # Always-available types and functions
│   ├── Exports.cs      # Global functions (print, len, etc.)
│   └── Exceptions.cs   # Exception types
└── Modules/            # Importable standard library modules
    └── (Future: sys, collections, json, etc.)
```

## Key Design Decisions

### 1. Runtime Reference in Compiler
The compiler project references the runtime project, allowing direct reflection over stdlib types and methods during compilation:

```csharp
// Compiler can inspect runtime types at compile-time
var strType = typeof(Sharpy.Str);
var methods = strType.GetMethods(); // Get all Sharpy string methods
```

### 2. Roslyn Code Generation
Uses Microsoft.CodeAnalysis (Roslyn) to:
- Generate C# syntax trees
- Emit optimized IL directly
- Produce PDB debug symbols
- Leverage existing .NET optimizations

### 3. Name Mangling
Automatic conversion between naming conventions:
- Sharpy: `my_function` → C#: `MyFunction` (methods)
- Sharpy: `my_variable` → C#: `myVariable` (locals)

### 4. Builtin Registry
At compiler initialization, the `BuiltinRegistry` uses reflection to discover:
- All builtin types (int, str, list, dict, etc.)
- Their methods and properties
- Global functions (print, len, range, etc.)

This enables the semantic analyzer to resolve symbols and validate code without hardcoding.

## Building

```bash
# Build both projects
dotnet build

# Build just the compiler
dotnet build src/Sharpy.Compiler

# Build just the runtime
dotnet build src/Sharpy.Core
```

## Usage (Future)

```csharp
using Sharpy.Compiler;

var compiler = new Compiler();
var result = compiler.Compile("def main(): print('Hello')", "output.dll");

if (result.Success)
{
    // Run the compiled assembly
    Assembly.LoadFile("output.dll");
}
```

## Next Steps

1. Port lexer from Rust implementation
2. Implement parser
3. Implement semantic analyzer with builtin registry
4. Complete code generator
5. Add CLI tool (Sharpy.Cli)
6. Add test projects
