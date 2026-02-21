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
│   │   ├── Types.cs    # Type annotations
│   │   ├── Pattern.cs  # Pattern matching nodes
│   │   └── ...         # Visitors, validators, future nodes
│   ├── Parser.cs       # Recursive descent parser (6 partial files)
│   └── Parser.*.cs     # .Definitions, .Expressions, .Primaries, .Statements, .Types
├── Diagnostics/        # Unified error handling
│   ├── DiagnosticBag.cs # Structured diagnostic collection
│   └── DiagnosticCodes.cs # SPY error code catalog
├── Semantic/           # Semantic analysis
│   ├── Symbol.cs       # Symbol definitions
│   ├── Scope.cs        # Scope management
│   ├── SymbolTable.cs  # Symbol table
│   ├── BuiltinRegistry.cs # Builtin types/functions registry
│   ├── TypeChecker.cs  # Type checking (8 partial files)
│   ├── TypeChecker.*.cs # .Definitions, .Expressions, .Expressions.Access,
│   │                    # .Expressions.Literals, .Expressions.Operators,
│   │                    # .Statements, .Utilities
│   └── Validation/     # Pluggable validation pipeline (16 validators)
├── CodeGen/            # Code generation
│   ├── RoslynEmitter.cs # Generates C# using Roslyn (11 partial files)
│   ├── RoslynEmitter.*.cs # .Expressions, .Expressions.Access, .Expressions.Literals,
│   │                      # .Expressions.Operators, .Statements, .TypeDeclarations,
│   │                      # .ClassMembers, .CompilationUnit, .ModuleClass, .Operators
│   ├── TypeMapper.cs    # Maps Sharpy types to C# types
│   ├── NameMangler.cs   # Name conversion (snake_case → PascalCase)
│   ├── CodeGenContext.cs # Code generation state
│   └── CodeValidator.cs # Validates generated code compiles
├── Project/            # Multi-file project compilation
│   ├── ProjectCompiler.cs # Orchestrates compilation (7 partial files)
│   ├── SpyProject.cs     # Project file parsing
│   ├── DependencyGraph.cs # Build ordering
│   └── IncrementalCompilationCache.cs # Incremental compilation
├── Services/           # Centralized compiler services layer
├── Discovery/          # CLR type discovery
├── Model/              # Core data model (CompilationUnit, ProjectModel)
├── Analysis/           # Control flow graph infrastructure
├── Shared/             # Constants, keyword escaping, type names
└── Text/               # Source text, spans, locations
```

### Sharpy.Core
The runtime library that compiled Sharpy code depends on.

```
Sharpy.Core/
├── Builtins/           # Always-available types and functions
│   ├── Builtins.cs     # Builtin function dispatch (partial class)
│   └── Exceptions.cs   # Builtin exception types
├── Partial.List/       # List[T] implementation (partial class pattern)
├── Partial.Set/        # Set[T] implementation
├── Partial.Complex/    # Complex number implementation
├── Partial.Iterator/   # Iterator base implementation
├── Partial.ListIterator/ # List iterator
├── Partial.ListReverseIterator/ # Reverse list iterator
├── Partial.SetIterator/ # Set iterator
├── Collections/        # Collection interfaces
├── Dict.cs             # dict[K,V] implementation
├── Range.cs            # range() builtin
├── Enumerate.cs        # enumerate() builtin
├── Print.cs            # print() builtin
├── Len.cs              # len() builtin
├── ISized.cs           # Protocol interface for len()
├── IBoolConvertible.cs # Protocol interface for bool()
└── *.cs                # Other builtins (Zip, Map, Filter, Sorted, etc.)
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
- Generate C# syntax trees via `SyntaxFactory` (no string templating)
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
