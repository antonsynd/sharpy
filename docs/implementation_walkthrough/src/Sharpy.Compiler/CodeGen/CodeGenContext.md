# Walkthrough: CodeGenContext.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

---

## 1. Overview

`CodeGenContext` is a lightweight state container that serves as the shared context for the code generation phase of the Sharpy compiler. It acts as a bridge between semantic analysis and code generation, carrying forward the symbol information and builtin registry that the `RoslynEmitter` and `TypeMapper` need to produce valid C# code.

**Position in the Pipeline:**
```
Source (.spy) → Lexer → Parser → Semantic Analysis → [CodeGenContext] → RoslynEmitter → C#
                                        ↓                    ↓
                                   SymbolTable          Uses context
                                   BuiltinRegistry      for lookups
```

The context is instantiated *after* semantic analysis completes and is passed to `RoslynEmitter`, which then uses it to:
- Look up symbols to determine their types and properties
- Check if identifiers refer to builtin functions or types
- Track source file information for namespace generation
- Manage indentation state (though this is largely unused in the Roslyn-based emitter)

---

## 2. Class/Type Structure

### `CodeGenContext`

A simple, mutable state container class with no inheritance or interface implementation.

```csharp
public class CodeGenContext
{
    // Private state
    private int _indentLevel = 0;
    private const int IndentSize = 4;

    // Core dependencies (immutable after construction)
    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }

    // Configuration properties (mutable)
    public string? SourceFilePath { get; set; }
    public string? ProjectNamespace { get; set; }
    public string? ProjectRootPath { get; set; }
    public bool IsEntryPoint { get; set; } = true;
}
```

### Property Categories

| Category | Properties | Purpose |
|----------|------------|---------|
| **Core Dependencies** | `SymbolTable`, `Builtins` | Read-only references from semantic analysis |
| **File Context** | `SourceFilePath` | Used to derive namespace names from file paths |
| **Project Context** | `ProjectNamespace`, `ProjectRootPath` | Multi-file compilation support |
| **Entry Point** | `IsEntryPoint` | Controls whether `Main()` is generated |
| **Indentation** | `_indentLevel`, `IndentSize` | Legacy text-based emission support |

---

## 3. Key Functions/Methods

### Constructor

```csharp
public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
{
    SymbolTable = symbolTable;
    Builtins = builtins;
}
```

**Purpose**: Establishes the connection to semantic analysis results.

**Parameters**:
- `symbolTable`: Contains all declared symbols (variables, functions, types) discovered during semantic analysis
- `builtins`: Registry of built-in types (`int`, `str`, `list`, etc.) and functions (`print`, `len`, etc.)

**When it's called**: During the compiler pipeline, after semantic analysis completes:
```
Lexer → Parser → NameResolver → TypeResolver → TypeChecker
                                                    ↓
                                              [SymbolTable ready]
                                                    ↓
                                         new CodeGenContext(symbolTable, builtins)
                                                    ↓
                                              RoslynEmitter
```

**Design Note**: Both dependencies are passed explicitly rather than retrieved from a service locator, making the class easy to test and understand.

---

### Symbol Lookup

```csharp
public Symbol? LookupSymbol(string name)
{
    return SymbolTable.Lookup(name);
}
```

**Purpose**: Provides a convenient passthrough to the symbol table for looking up any symbol by name.

**Returns**: The `Symbol` if found (can be `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`), or `null` if the name is not declared.

**Scope Resolution**: The lookup searches from the current scope outward to parent scopes (including global scope), so local variables shadow outer variables with the same name.

**Usage in Pipeline**: The `RoslynEmitter` uses this to:
- Determine the type of a variable reference
- Check if a function call refers to a user-defined or builtin function
- Resolve type names in annotations

**Example Usage**:
```csharp
// When generating code for: x = 10
var symbol = context.LookupSymbol("x");
if (symbol is VariableSymbol varSymbol)
{
    var csharpType = mapper.MapType(varSymbol.Type);
    // Generate assignment with proper type information
}
```

---

### Builtin Checks

```csharp
public bool IsBuiltinFunction(string name)
{
    return Builtins.GetFunction(name) != null;
}

public bool IsBuiltinType(string name)
{
    return Builtins.GetType(name) != null;
}
```

**Purpose**: Quick boolean checks for whether a name refers to a builtin.

**Why These Exist**: During code generation, builtin functions and types are handled differently from user-defined ones:
- **Builtin functions** → Map to static calls on `Sharpy.Core.Exports` (e.g., `print(x)` becomes `Exports.Print(x)`)
- **User functions** → Map to local method calls or qualified method calls
- **Builtin types** → Map to specific C# types (e.g., `list[int]` → `Sharpy.Core.List<int>`)

**Example Decision Tree**:
```csharp
if (context.IsBuiltinFunction(functionName))
{
    // Generate: Sharpy.Core.Exports.Print(...)
    return GenerateBuiltinCall(functionName, args);
}
else
{
    // Generate: MyFunction(...) or obj.Method(...)
    return GenerateUserFunctionCall(functionName, args);
}
```

**Builtin Types Include**:
- Primitives: `int`, `float`, `bool`, `str`, `None`
- Collections: `list`, `dict`, `set`
- Special: `object`

---

### Indentation Management

```csharp
public void Indent() => _indentLevel++;
public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);
public string GetIndent() => new string(' ', _indentLevel * IndentSize);
```

**Purpose**: Track and produce indentation for formatted code output.

**Historical Context**: These methods are vestiges of an earlier string-based code generation approach. The current `RoslynEmitter` uses Roslyn's `SyntaxFactory` and `NormalizeWhitespace()` for formatting, making these methods largely unused. They remain for potential debugging or alternative emission strategies.

**Usage Pattern**:
```csharp
context.Indent();
// Generate nested code here
context.Dedent();
```

**Safety Feature**: `Dedent()` guards against negative indentation with `Math.Max(0, ...)`, preventing crashes from mismatched indent/dedent calls.

---

## 4. Dependencies

### Internal Dependencies

| Dependency | Namespace | Role |
|------------|-----------|------|
| `SymbolTable` | `Sharpy.Compiler.Semantic` | Scope-based symbol storage from semantic analysis |
| `BuiltinRegistry` | `Sharpy.Compiler.Semantic` | Registry of Sharpy's built-in types and functions |
| `Symbol` | `Sharpy.Compiler.Semantic` | Base class for all symbol types |

### Relationship Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Semantic Analysis Phase                   │
├─────────────────────────────────────────────────────────────┤
│  BuiltinRegistry ◄──── SymbolTable                          │
│       │                     │                               │
│       │                     │  (populated during analysis)  │
│       ▼                     ▼                               │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ passed to constructor
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      CodeGenContext                          │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────────┐  │
│  │ SymbolTable  │  │ BuiltinRegistry│  │ Config Properties│  │
│  └──────────────┘  └───────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ injected into
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Code Generation Phase                     │
├─────────────────────────────────────────────────────────────┤
│  RoslynEmitter ────────► TypeMapper                         │
│       │                      │                              │
│       │ (uses context)       │ (uses context)               │
│       ▼                      ▼                              │
│  CompilationUnitSyntax   TypeSyntax nodes                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Patterns and Design Decisions

### Dependency Injection Over Service Location

The context receives its dependencies via the constructor rather than looking them up from a global registry. This pattern:
- Makes testing straightforward (just construct with mock dependencies)
- Makes the dependency graph explicit and visible
- Avoids hidden coupling to global state

### Mutable Configuration Properties

The "init-style" pattern is used for configuration:

```csharp
var codeGenContext = new CodeGenContext(symbolTable, builtins)
{
    SourceFilePath = sourceFile,
    ProjectNamespace = projectConfig.RootNamespace,
    ProjectRootPath = projectRoot,
    IsEntryPoint = fileName.Equals("main", StringComparison.OrdinalIgnoreCase)
};
```

This approach allows the core dependencies to be set via constructor (required) while optional configuration can be set via object initializer syntax.

### Single-File vs Multi-File Compilation

The context supports both compilation modes:

| Property | Single-File | Multi-File |
|----------|-------------|------------|
| `IsEntryPoint` | `true` (default) | `true` only for `main.spy` |
| `ProjectNamespace` | `null` | Set from project config |
| `ProjectRootPath` | `null` | Points to `src/` directory |

### Minimal Abstraction

The class is deliberately simple—it's essentially a struct with some convenience methods. This reflects the philosophy that code generation context is "just data" that flows through the system, not a complex abstraction with behavior.

### Immutable Core, Mutable Metadata

```csharp
// Immutable (set once in constructor)
public SymbolTable SymbolTable { get; }
public BuiltinRegistry Builtins { get; }

// Mutable (set during compilation)
public string? SourceFilePath { get; set; }
public string? ProjectNamespace { get; set; }
```

**Design Rationale**:
- Core dependencies (`SymbolTable`, `Builtins`) are immutable to prevent accidental modification
- Metadata properties are mutable to support single-file vs. multi-file compilation modes

---

## 6. Debugging Tips

### Inspecting Symbol Resolution

If code generation produces incorrect output for a variable or function, check what the context sees:

```csharp
// Add temporary debugging in RoslynEmitter
var symbol = _context.LookupSymbol("myVariable");
Console.WriteLine($"Symbol: {symbol?.Name}, Kind: {symbol?.Kind}, Type: {(symbol as VariableSymbol)?.Type}");
```

**Common Causes of "Symbol not found"**:
- Variable declared in inner scope but lookup happening in outer scope
- Typo in variable name
- Symbol table not properly populated by semantic analysis

### Tracing Builtin Detection

If a builtin function is being emitted incorrectly:

```csharp
// Check if it's recognized as a builtin
Console.WriteLine($"Is 'print' builtin function? {_context.IsBuiltinFunction("print")}");
Console.WriteLine($"Is 'str' builtin type? {_context.IsBuiltinType("str")}");

// If not recognized, inspect the registry
var allBuiltins = _context.Builtins.GetAllFunctions();
// Does "print" appear in the list?
```

**Possible Issues**:
- `BuiltinRegistry` initialization failed
- Function name mismatch (Python `print` vs. C# `Print`)
- Sharpy.Core assembly not loaded properly

### Namespace Generation Issues

If generated namespaces are wrong, check the context configuration:

```csharp
Console.WriteLine($"SourceFilePath: {context.SourceFilePath}");
Console.WriteLine($"ProjectNamespace: {context.ProjectNamespace}");
Console.WriteLine($"ProjectRootPath: {context.ProjectRootPath}");
```

### Common Issues Quick Reference

| Symptom | Likely Cause | Check |
|---------|--------------|-------|
| "Symbol not found" at codegen | Symbol not registered during semantic analysis | Verify semantic analysis completed successfully |
| Wrong C# type emitted | Builtin type not recognized | Check `IsBuiltinType()` returns expected value |
| Missing `Main()` method | `IsEntryPoint` is false | Verify entry point detection logic |
| Wrong namespace in output | `ProjectRootPath` mismatch | Check path computation logic |

---

## 7. Contribution Guidelines

### Types of Changes

**Likely Changes:**
- Adding new configuration properties for new compilation modes
- Adding new lookup convenience methods (following the pattern of `IsBuiltinFunction`/`IsBuiltinType`)
- Adding properties to support new code generation features

**Unlikely Changes:**
- Changing the constructor signature (would require updating all call sites)
- Adding complex behavior (this class should remain a simple container)
- Removing the indentation methods (kept for backward compatibility)

### Adding a New Property

When adding a configuration property:

1. Add the property with a sensible default:
```csharp
/// <summary>
/// Description of what this controls
/// </summary>
public bool NewFeatureEnabled { get; set; } = false;
```

2. Update all `new CodeGenContext` call sites if the property needs explicit initialization
3. Document how `RoslynEmitter` or `TypeMapper` uses the new property

### Adding New Lookup Methods

If you need specialized lookups:

```csharp
// Example: Look up only variables in current scope (no parent search)
public VariableSymbol? LookupLocalVariable(string name)
{
    var symbol = SymbolTable.Lookup(name, searchParents: false);
    return symbol as VariableSymbol;
}
```

**Guidelines**:
- Keep methods simple delegations to `SymbolTable` or `Builtins`
- Add XML documentation comments
- Consider if the logic belongs in `SymbolTable` instead

### Coding Conventions

- Use XML doc comments for all public members
- Keep methods small and focused (single responsibility)
- Prefer explicit null checks over null-conditional operators for clarity
- Follow the existing naming conventions (`_privateField`, `PublicProperty`)

### Testing

The context itself has minimal logic to test, but changes should be validated through:
- `RoslynEmitterIntegrationTests` - End-to-end code generation
- `TypeMapperTests` - Type mapping with various context configurations
- Manual inspection of generated C# code for edge cases

**Unit Testing Pattern**:
```csharp
[Fact]
public void TestSymbolLookup()
{
    var builtins = new BuiltinRegistry();
    var symbolTable = new SymbolTable(builtins);
    var context = new CodeGenContext(symbolTable, builtins);

    // Register a test symbol
    symbolTable.Define(new VariableSymbol
    {
        Name = "testVar",
        Type = SemanticType.Int
    });

    // Test lookup
    var result = context.LookupSymbol("testVar");
    Assert.NotNull(result);
    Assert.IsType<VariableSymbol>(result);
}
```

---

## Related Files

| File | Relationship |
|------|--------------|
| `RoslynEmitter.cs` | Primary consumer; uses context for all code generation |
| `TypeMapper.cs` | Uses context for type resolution and mapping |
| `SymbolTable.cs` | Provides symbol lookup functionality |
| `BuiltinRegistry.cs` | Provides builtin identification |
| `Compiler.cs` | Creates and initializes context |

---

## Summary

`CodeGenContext` is the **state management backbone** of Sharpy's code generation phase:

- **Small but critical**: Only ~58 lines, but used throughout code generation
- **Glue layer**: Connects semantic analysis results with code generation logic
- **Query interface**: Provides convenient methods for symbol and builtin lookups
- **Configuration container**: Holds project-level settings and metadata

**Key Takeaway**: This class doesn't generate code—it **enables** code generation by providing necessary context and state. Think of it as the "toolbox" that `RoslynEmitter` reaches into while doing the actual work.
