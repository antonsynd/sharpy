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
- Collect errors that occur during code generation
- Log warnings and debug information
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
    private readonly List<string> _errors = new();

    // Core dependencies (immutable after construction)
    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }

    // Configuration properties (mutable)
    public string? SourceFilePath { get; set; }
    public string? ProjectNamespace { get; set; }
    public string? ProjectRootPath { get; set; }
    public bool IsEntryPoint { get; set; } = false;

    // Error tracking
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;
    public void AddError(string message) => _errors.Add(message);

    // Logging
    public ICompilerLogger Logger { get; set; } = NullLogger.Instance;
}
```

### Property Categories

| Category | Properties | Purpose |
|----------|------------|---------|
| **Core Dependencies** | `SymbolTable`, `Builtins` | Read-only references from semantic analysis |
| **File Context** | `SourceFilePath` | Used to derive namespace names from file paths |
| **Project Context** | `ProjectNamespace`, `ProjectRootPath` | Multi-file compilation support |
| **Entry Point** | `IsEntryPoint` | Controls whether `Main()` is generated (default: `false`) |
| **Error Tracking** | `Errors`, `HasErrors`, `AddError()` | Collects errors during code generation |
| **Logging** | `Logger` | Logs warnings and debug info (uses Null Object pattern by default) |
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

### Error Tracking

```csharp
public IReadOnlyList<string> Errors => _errors;
public bool HasErrors => _errors.Count > 0;
public void AddError(string message) => _errors.Add(message);
```

**Purpose**: Collects errors that occur during the code generation phase.

**Why This Exists**: While most errors are caught during semantic analysis, some issues can only be detected during code generation:
- Type mapping failures for complex or unsupported types
- Code generation patterns that can't be expressed in C#
- Edge cases in name mangling or namespace generation

**Usage Pattern**:
```csharp
// In RoslynEmitter when encountering an issue
if (!CanGenerateCodeFor(node))
{
    _context.AddError($"Cannot generate code for {node.GetType().Name} at line {node.Line}");
    return null; // Or generate placeholder code
}

// Later, check if code generation succeeded
if (context.HasErrors)
{
    foreach (var error in context.Errors)
        Console.WriteLine($"CodeGen Error: {error}");
}
```

**Design Note**: The error list is exposed as `IReadOnlyList<string>` to prevent external code from modifying it directly, while still allowing iteration and inspection.

---

### Logging

```csharp
public ICompilerLogger Logger { get; set; } = NullLogger.Instance;
```

**Purpose**: Provides a logging interface for warnings, debug info, and trace-level messages during code generation.

**Default Value**: `NullLogger.Instance` — a Null Object pattern implementation that silently discards all log messages. This means logging has zero overhead when not configured.

**When to Use**:
- Warnings for deprecated patterns or potential issues
- Debug information for tracing code generation decisions
- Trace-level logging for detailed step-by-step emission

**Example Usage**:
```csharp
// Log a warning about a deprecated pattern
context.Logger.LogWarning("Using deprecated syntax for operator overload", node.Line, node.Column);

// Log debug info about type mapping decisions
if (context.Logger.IsEnabled(CompilerLogLevel.Debug))
{
    context.Logger.LogDebug($"Mapping {sharpyType} to {csharpType}");
}
```

**Enabling Logging**: Set a concrete logger (like `ConsoleCompilerLogger`) when configuring the context:
```csharp
var context = new CodeGenContext(symbolTable, builtins)
{
    Logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug)
};
```

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
| `ICompilerLogger` | `Sharpy.Compiler.Logging` | Interface for compiler logging |
| `NullLogger` | `Sharpy.Compiler.Logging` | Null Object pattern implementation for logging |

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
│  ┌──────────────┐  ┌───────────────┐                        │
│  │ Error List   │  │    Logger     │                        │
│  └──────────────┘  └───────────────┘                        │
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
| `IsEntryPoint` | Explicitly set to `true` | `true` only for `main.spy` |
| `ProjectNamespace` | `null` | Set from project config |
| `ProjectRootPath` | `null` | Points to `src/` directory |

**Note**: `IsEntryPoint` defaults to `false`, so it must be explicitly set when compiling a file that should have a `Main()` method.

### Null Object Pattern for Logging

The `Logger` property defaults to `NullLogger.Instance`, which implements `ICompilerLogger` with empty method bodies. This pattern:
- Eliminates null checks throughout the codebase
- Provides zero-overhead logging when disabled (methods are aggressively inlined)
- Makes it safe to call logging methods unconditionally

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

### Checking Code Generation Errors

Always check if errors occurred after code generation:

```csharp
var emitter = new RoslynEmitter(context);
var result = emitter.GenerateCompilationUnit(module);

if (context.HasErrors)
{
    Console.WriteLine("Code generation failed with errors:");
    foreach (var error in context.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### Enabling Debug Logging

For detailed tracing of code generation decisions:

```csharp
var context = new CodeGenContext(symbolTable, builtins)
{
    Logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug)
};
```

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
| Silent failures | Errors collected but not checked | Inspect `context.Errors` after generation |

---

## 7. Contribution Guidelines

### Types of Changes

**Likely Changes:**
- Adding new configuration properties for new compilation modes
- Adding new lookup convenience methods (following the pattern of `IsBuiltinFunction`/`IsBuiltinType`)
- Adding properties to support new code generation features
- Extending error tracking with more structured error types

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

[Fact]
public void TestErrorTracking()
{
    var builtins = new BuiltinRegistry();
    var symbolTable = new SymbolTable(builtins);
    var context = new CodeGenContext(symbolTable, builtins);

    Assert.False(context.HasErrors);
    Assert.Empty(context.Errors);

    context.AddError("Test error message");

    Assert.True(context.HasErrors);
    Assert.Single(context.Errors);
    Assert.Equal("Test error message", context.Errors[0]);
}
```

---

## 8. Cross-References

### Related Walkthrough Documents

| File | Relationship |
|------|--------------|
| [`RoslynEmitter.md`](./RoslynEmitter.md) | Primary consumer of CodeGenContext; uses it for all code generation decisions |
| [`TypeMapper.md`](./TypeMapper.md) | Uses context for type resolution and mapping Sharpy types to C# |
| [`SymbolTable.md`](../Semantic/SymbolTable.md) | Provides the symbol lookup functionality exposed through CodeGenContext |
| [`BuiltinRegistry.md`](../Semantic/BuiltinRegistry.md) | Provides builtin identification used by `IsBuiltinFunction` and `IsBuiltinType` |
| [`ICompilerLogger.md`](../Logging/ICompilerLogger.md) | Logging interface used by the `Logger` property |
| [`NullLogger.md`](../Logging/NullLogger.md) | Default logger implementation (Null Object pattern) |

### Source Files

| File | Relationship |
|------|--------------|
| `RoslynEmitter.cs` | Primary consumer; uses context for all code generation |
| `TypeMapper.cs` | Uses context for type resolution and mapping |
| `SymbolTable.cs` | Provides symbol lookup functionality |
| `BuiltinRegistry.cs` | Provides builtin identification |
| `Compiler.cs` | Creates and initializes context |

### RoslynEmitter Partial Class Files

The `RoslynEmitter` class is split across multiple partial class files for maintainability:

| File | Purpose |
|------|---------|
| `RoslynEmitter.cs` | Core class definition and variable name mangling |
| `RoslynEmitter.CompilationUnit.cs` | Top-level file generation (usings, namespace, class wrapper) |
| `RoslynEmitter.ModuleClass.cs` | Module-level code (fields, static constructor, Main) |
| `RoslynEmitter.ClassMembers.cs` | Class/struct member generation |
| `RoslynEmitter.TypeDeclarations.cs` | Type declarations (classes, structs, enums, interfaces) |
| `RoslynEmitter.Statements.cs` | Statement-level code generation |
| `RoslynEmitter.Expressions.cs` | Expression-level code generation |
| `RoslynEmitter.Operators.cs` | Operator overload generation |

All of these partial class files use `CodeGenContext` through the `_context` field.

---

## Summary

`CodeGenContext` is the **state management backbone** of Sharpy's code generation phase:

- **Small but critical**: Only ~80 lines, but used throughout code generation
- **Glue layer**: Connects semantic analysis results with code generation logic
- **Query interface**: Provides convenient methods for symbol and builtin lookups
- **Error collection**: Accumulates errors that occur during code generation
- **Logging support**: Enables debug and trace logging with zero overhead when disabled
- **Configuration container**: Holds project-level settings and metadata

**Key Takeaway**: This class doesn't generate code—it **enables** code generation by providing necessary context and state. Think of it as the "toolbox" that `RoslynEmitter` reaches into while doing the actual work.
