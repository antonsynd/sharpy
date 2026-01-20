# Sharpy.Compiler.Model Namespace

This namespace contains the core data model classes for the Sharpy compiler.

## Key Classes

### CompilationUnit

Represents a single Sharpy source file and all its compilation artifacts:
- Source text and content hash (for incremental compilation)
- Tokens (for LSP hover/completion)
- AST (Module)
- Semantic artifacts (declared types, functions, scope)
- Dependencies (direct imports)
- Generated C# code
- Diagnostics (errors/warnings specific to this file)

### ProjectModel

Represents a complete Sharpy project being compiled:
- Collection of CompilationUnits
- Global symbol table
- Dependency graph
- Build ordering

### CompilationUnitFactory

Factory methods for creating and processing CompilationUnits:
- `CreateFromFile()` - Load source from file
- `Lex()` / `Parse()` / `LexAndParse()` - Process compilation phases

## Usage

```csharp
// Create a project model
var model = new ProjectModel(config);

// Add compilation units
var unit = model.CreateUnit(filePath, modulePath, sourceText);

// Process the unit
CompilationUnitFactory.LexAndParse(unit, logger);

// Access results
var ast = unit.Ast;
var tokens = unit.Tokens;
var errors = unit.Diagnostics.GetAll();
```

## Design Principles

1. **Immutability where practical**: Use `init` properties, but allow
   internal setters for compilation pipeline flexibility.

2. **Thread-safety**: DiagnosticBag is thread-safe for future parallel
   compilation support.

3. **Incremental compilation ready**: Content hashing and dependency
   tracking support future incremental compilation.

4. **LSP ready**: Token storage and source spans enable future IDE
   integration.
