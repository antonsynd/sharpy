# Sharpy Compiler & Runtime

C#-based compiler toolchain for the Sharpy programming language.

## Project Structure

### Sharpy.Compiler
The compiler library that transforms Sharpy source code into executable .NET assemblies.

```
Sharpy.Compiler/
├── Lexer/              # Tokenization
│   ├── Token.cs        # Token types and data structures
│   └── Lexer.cs        # Lexical analyzer
├── Parser/             # Parsing
│   ├── Ast/            # Abstract Syntax Tree definitions
│   │   ├── Node.cs     # Base AST node types
│   │   ├── Statement.cs # Statement nodes
│   │   ├── Expression.cs # Expression nodes
│   │   └── Types.cs    # Type annotations
│   └── Parser.cs       # Recursive descent parser
├── Diagnostics/        # Unified error handling
│   ├── DiagnosticBag.cs # Structured diagnostic collection
│   └── DiagnosticCodes.cs # SPY error code catalog
├── Semantic/           # Semantic analysis
│   ├── Symbol.cs       # Symbol definitions
│   ├── Scope.cs        # Scope management
│   ├── SymbolTable.cs  # Symbol table
│   └── BuiltinRegistry.cs # Builtin types/functions registry
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
│   └── Exports.cs      # Global functions (print, len, etc.)
├── Partial.List/       # List[T] implementation (partial class pattern)
├── Partial.Set/        # Set[T] implementation
├── Partial.*/          # Other type implementations split by interface
├── Collections/        # Collection interfaces
├── Dict.cs             # dict[K,V] implementation
├── Range.cs            # range() builtin
├── Enumerate.cs        # enumerate() builtin
└── *.cs                # Other builtins (Zip, Map, Filter, etc.)
```

## Key Design Decisions

### 1. Runtime Reference in Compiler
The compiler project references the runtime project, allowing direct reflection over stdlib types and methods during compilation:

```csharp
// Compiler can inspect runtime types at compile-time
var listType = typeof(Sharpy.List<>);
var methods = listType.GetMethods(); // Get all Sharpy list methods
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

## Usage

```bash
# Compile and run a Sharpy file
dotnet run --project src/Sharpy.Cli -- run myfile.spy

# Build to DLL
dotnet run --project src/Sharpy.Cli -- build myfile.spy -o output.dll

# Inspect generated C# (for debugging)
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy
```
