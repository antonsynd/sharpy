# Walkthrough: CodeGenContext.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

---

## Overview

`CodeGenContext` is a **state management class** that serves as the central hub during the code generation phase of the Sharpy compiler. Think of it as the "context object" or "session state" that gets passed around to various parts of the code generator as it transforms the Sharpy Abstract Syntax Tree (AST) into C# code using Roslyn.

**Primary Purpose**: Maintain and provide access to:
- Symbol table lookups (variables, functions, types)
- Builtin type and function registry
- Indentation state for code formatting
- Project metadata (namespace, root path, source file)

This class doesn't generate code itself—instead, it acts as a **data provider and state container** for classes like `RoslynEmitter` and `TypeMapper` that do the actual code generation work.

---

## Class Structure

### Main Class Definition

```csharp
public class CodeGenContext
{
    // State fields
    private int _indentLevel = 0;
    private const int IndentSize = 4;
    
    // Core dependencies (immutable after construction)
    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }
    
    // Contextual information (mutable during compilation)
    public string? SourceFilePath { get; set; }
    public string? ProjectNamespace { get; set; }
    public string? ProjectRootPath { get; set; }
}
```

### Key Properties

| Property | Type | Purpose |
|----------|------|---------|
| `SymbolTable` | `SymbolTable` | **Symbol resolution**: Contains all declared variables, functions, types, and their scopes |
| `Builtins` | `BuiltinRegistry` | **Builtin identification**: Registry of Sharpy.Core's builtin functions and types (like `print()`, `len()`, `list`, `dict`) |
| `SourceFilePath` | `string?` | **Source tracking**: Path to the `.spy` file being compiled (used for error messages and namespace generation) |
| `ProjectNamespace` | `string?` | **Multi-file compilation**: Root namespace for projects (e.g., `"MyApp"`) |
| `ProjectRootPath` | `string?` | **Relative path computation**: Base directory for computing relative namespaces in multi-file projects |

---

## Key Methods Deep Dive

### 1. Constructor

```csharp
public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
{
    SymbolTable = symbolTable;
    Builtins = builtins;
}
```

**What it does**: Initializes the context with required dependencies from earlier compiler phases.

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

**Important**: The `SymbolTable` passed in is fully populated with all names resolved and types checked. The code generator assumes this information is correct and complete.

---

### 2. Indentation Management

```csharp
public void Indent() => _indentLevel++;
public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);
public string GetIndent() => new string(' ', _indentLevel * IndentSize);
```

**What it does**: Manages whitespace indentation for generated code readability.

**Why it exists**: While Roslyn's `NormalizeWhitespace()` handles most formatting, certain code generation patterns (especially multi-line string building or debugging output) benefit from explicit indentation tracking.

**Usage Pattern**:
```csharp
context.Indent();
// Generate nested code here
context.Dedent();
```

**Safety Feature**: `Dedent()` uses `Math.Max(0, ...)` to prevent negative indentation levels—a defensive programming pattern that prevents crashes from mismatched indent/dedent calls.

**Note**: In the current codebase, `RoslynEmitter` primarily uses Roslyn's built-in formatting rather than manual indentation. These methods are **legacy utilities** kept for backward compatibility and potential future use (e.g., generating intermediate representations or debug output).

---

### 3. Symbol Lookup

```csharp
public Symbol? LookupSymbol(string name)
{
    return SymbolTable.Lookup(name);
}
```

**What it does**: Delegates to the symbol table to find a symbol by name, respecting scope hierarchy.

**Returns**: 
- `Symbol?` - Can be `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, `ModuleSymbol`, or `null` if not found
- `null` indicates the name doesn't exist in any visible scope

**Scope Resolution**: The lookup searches from the current scope outward to parent scopes (including global scope), so local variables shadow outer variables with the same name.

**Example Usage in Code Generation**:
```csharp
// When generating code for: x = 10
var symbol = context.LookupSymbol("x");
if (symbol is VariableSymbol varSymbol)
{
    // Generate assignment with proper type information
    var csharpType = mapper.MapType(varSymbol.Type);
    // ...
}
```

---

### 4. Builtin Function Detection

```csharp
public bool IsBuiltinFunction(string name)
{
    return Builtins.GetFunction(name) != null;
}
```

**What it does**: Checks if a function name refers to a Sharpy.Core builtin (like `print()`, `len()`, `range()`).

**Why this matters**: Builtins require special handling during code generation:
- **Builtin functions** → Map to static calls on `Sharpy.Core.Exports` (e.g., `print(x)` becomes `Exports.Print(x)`)
- **User functions** → Map to local method calls or qualified method calls

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

---

### 5. Builtin Type Detection

```csharp
public bool IsBuiltinType(string name)
{
    return Builtins.GetType(name) != null;
}
```

**What it does**: Checks if a type name is a Sharpy builtin type.

**Builtin Types Include**:
- Primitives: `int`, `float`, `bool`, `str`, `None`
- Collections: `list`, `dict`, `set`
- Special: `object`

**Code Generation Impact**:
```csharp
// Type annotation: def foo(x: list[int]) -> None:

if (context.IsBuiltinType("list"))
{
    // Map to: Sharpy.Core.List<int>
}
else
{
    // Map to user-defined type
}
```

**Edge Case**: Generic types like `list[T]` require additional handling—the base name `"list"` is checked, then type parameters are processed recursively.

---

## Dependencies

### Upstream Dependencies (What CodeGenContext Needs)

1. **`Sharpy.Compiler.Semantic.SymbolTable`**
   - Provides: All declared names, types, scopes
   - Populated by: `NameResolver` and `TypeResolver`
   - Used for: Symbol lookups during code generation

2. **`Sharpy.Compiler.Semantic.BuiltinRegistry`**
   - Provides: Registry of Sharpy.Core builtins
   - Initialized at: Compiler startup
   - Used for: Distinguishing builtins from user code

### Downstream Dependencies (What Uses CodeGenContext)

1. **`RoslynEmitter`** (primary consumer)
   - Uses: All context methods
   - Purpose: Generates C# syntax trees from Sharpy AST nodes

2. **`TypeMapper`**
   - Uses: `SymbolTable`, `Builtins`
   - Purpose: Maps Sharpy types to C# types (e.g., `list[int]` → `List<int>`)

3. **`NameMangler`** (indirectly)
   - May use: Symbol lookups for name conflict resolution
   - Purpose: Converts Python-style names to C#-style names

**Dependency Graph**:
```
        SymbolTable ──┐
                      ├──> CodeGenContext ──┐
    BuiltinRegistry ──┘                     ├──> RoslynEmitter
                                            │
                                            └──> TypeMapper
```

---

## Patterns and Design Decisions

### 1. **Separation of Concerns**

`CodeGenContext` is a **pure data holder**—it doesn't implement code generation logic. This follows the **Context Object pattern**:
- **Benefits**: Clean separation between state management and logic
- **Testability**: Easy to mock or stub for unit tests
- **Maintainability**: Changes to code generation logic don't affect context structure

### 2. **Immutable Core, Mutable Metadata**

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

### 3. **Nullable Reference Types**

Project-related properties are nullable (`string?`) because:
- **Single-file compilation**: `ProjectNamespace` and `ProjectRootPath` are `null`
- **Project compilation**: These are set based on `.spyproj` file configuration

**Code Pattern**:
```csharp
var namespaceName = context.ProjectNamespace ?? "Sharpy.Generated";
```

### 4. **Defensive Programming**

```csharp
public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);
```

The `Math.Max(0, ...)` prevents negative indentation even if `Dedent()` is called more times than `Indent()`. This is a fail-safe against programming errors.

---

## Debugging Tips

### Problem: "Symbol not found during code generation"

**Check**:
```csharp
var symbol = context.LookupSymbol(name);
if (symbol == null)
{
    // Breakpoint here - symbol should exist after semantic analysis
    // If null, either:
    // 1. NameResolver didn't register it
    // 2. Wrong scope is active
    // 3. Name mismatch (Python name vs. mangled name)
}
```

**Common Causes**:
- Variable declared in inner scope but lookup happening in outer scope
- Typo in variable name
- Symbol table not properly populated by semantic analysis

---

### Problem: "Builtin function not recognized"

**Diagnostic**:
```csharp
if (!context.IsBuiltinFunction("print"))
{
    // Breakpoint here
    // Check: Is BuiltinRegistry properly initialized?
    var allBuiltins = context.Builtins.GetAllFunctions();
    // Inspect: Does "print" appear in the list?
}
```

**Possible Issues**:
- `BuiltinRegistry` initialization failed
- Function name mismatch (Python `print` vs. C# `Print`)
- Sharpy.Core assembly not loaded properly

---

### Problem: "Wrong namespace generated"

**Debug Flow**:
```csharp
Console.WriteLine($"ProjectNamespace: {context.ProjectNamespace}");
Console.WriteLine($"ProjectRootPath: {context.ProjectRootPath}");
Console.WriteLine($"SourceFilePath: {context.SourceFilePath}");

// Expected values for project compilation:
// ProjectNamespace: "MyApp"
// ProjectRootPath: "/path/to/project"
// SourceFilePath: "/path/to/project/src/module.spy"
```

**Check**: Ensure `AssemblyCompiler` sets these properties before calling `RoslynEmitter`.

---

### Debugging Indentation Issues

If generated code has indentation problems:

1. **Check Roslyn output**: Run `.NormalizeWhitespace()` on syntax trees
2. **Manual indentation**: If using `GetIndent()`, verify matching `Indent()`/`Dedent()` calls:
   ```csharp
   int indentLevel = 0;
   // Trace indent/dedent calls to ensure balance
   ```

---

## Contribution Guidelines

### When to Modify CodeGenContext

**Add new properties when**:
- ✅ Code generation needs new compiler-wide state
- ✅ Information should be accessible to multiple code generation components
- ✅ Data comes from earlier compiler phases (semantic analysis, project configuration)

**Examples of good additions**:
- `public bool EnableOptimizations { get; set; }` - for conditional optimizations
- `public string? TargetFramework { get; set; }` - for framework-specific code generation

**DON'T add**:
- ❌ Generation logic methods (put in `RoslynEmitter` instead)
- ❌ Per-node temporary state (use local variables in generator methods)
- ❌ AST node references (context should be AST-agnostic)

---

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

---

### Testing Considerations

**Unit Testing Pattern**:
```csharp
[Fact]
public void TestSymbolLookup()
{
    var symbolTable = new SymbolTable(new BuiltinRegistry());
    var builtins = new BuiltinRegistry();
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

**Integration Testing**: Most testing happens through `RoslynEmitter` integration tests:
```csharp
// In Sharpy.Compiler.Tests/CodeGen/RoslynEmitterTests.cs
[Fact]
public void CompileFunctionWithBuiltins()
{
    var source = @"
        def greet(name: str) -> None:
            print(f'Hello, {name}!')
    ";
    
    // Context is created internally
    var result = CompileAndExecute(source);
    Assert.Equal("Hello, World!\n", result.StandardOutput);
}
```

---

### Common Contribution Scenarios

#### Scenario 1: Supporting Debug Symbol Generation

**Goal**: Add debug information to generated C#.

**Changes**:
```csharp
// Add property
public bool EmitDebugInfo { get; set; } = true;

// Usage in RoslynEmitter
if (_context.EmitDebugInfo)
{
    // Add #line directives
    var lineDirective = LineDirectiveTrivia(...);
    // ...
}
```

---

#### Scenario 2: Multi-Target Framework Support

**Goal**: Generate different C# code for .NET 9 vs. .NET 10.

**Changes**:
```csharp
// Add property
public string TargetFramework { get; set; } = "net9.0";

// Usage in TypeMapper
if (_context.TargetFramework.StartsWith("net10"))
{
    // Use .NET 10-specific types
}
```

---

#### Scenario 3: Custom Namespace Strategies

**Goal**: Support different namespace naming conventions.

**Changes**:
```csharp
public enum NamespaceStrategy { FlatStructure, MirrorDirectory, Custom }
public NamespaceStrategy NamespaceMode { get; set; } = NamespaceStrategy.MirrorDirectory;
public Func<string, string>? CustomNamespaceMapper { get; set; }
```

---

## Relationship to Other Components

### CodeGenContext in the Compilation Pipeline

```
┌─────────────────────────────────────────────────────────┐
│                    Compiler.cs                          │
│  (Orchestrates compilation of single .spy file)         │
└───────────────────────┬─────────────────────────────────┘
                        │
        ┌───────────────┴───────────────┐
        │                               │
        ▼                               ▼
┌───────────────┐              ┌────────────────┐
│  Lexer        │              │  Semantic      │
│  → Parser     │─────────────>│  Analysis      │
└───────────────┘              │  (builds       │
                               │  SymbolTable)  │
                               └────────┬───────┘
                                        │
                                        ▼
                               ┌────────────────┐
                               │ CodeGenContext │<── You are here
                               │ (initialized)  │
                               └────────┬───────┘
                                        │
                                        ▼
                               ┌────────────────┐
                               │ RoslynEmitter  │
                               │ (uses context) │
                               └────────┬───────┘
                                        │
                                        ▼
                                  C# Syntax Tree
                                        │
                                        ▼
                                  Roslyn Compiler
                                        │
                                        ▼
                                    .NET IL
```

---

## Advanced Topics

### Thread Safety

**Current State**: `CodeGenContext` is **NOT thread-safe**.

**Implication**: Each compilation should use its own `CodeGenContext` instance. Don't share across threads.

**Future Consideration**: If parallel compilation is needed:
```csharp
// Would need concurrent collections for mutable state
public ConcurrentDictionary<string, string> ThreadSafeMetadata { get; }
```

---

### Extension Points

`CodeGenContext` can be extended through subclassing if needed:

```csharp
public class OptimizingCodeGenContext : CodeGenContext
{
    public OptimizationLevel Optimizations { get; set; }
    
    public OptimizingCodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
        : base(symbolTable, builtins)
    {
    }
}
```

However, **prefer composition over inheritance**—add properties to the base class unless you need polymorphic behavior.

---

## Summary

`CodeGenContext` is the **state management backbone** of Sharpy's code generation phase:

- **Small but critical**: Only ~50 lines, but used throughout code generation
- **Glue layer**: Connects semantic analysis results with code generation logic
- **Query interface**: Provides convenient methods for symbol and builtin lookups
- **Configuration container**: Holds project-level settings and metadata

**Key Takeaway**: This class doesn't generate code—it **enables** code generation by providing necessary context and state. Think of it as the "toolbox" that `RoslynEmitter` reaches into while doing the actual work.

---

## Related Files

| File | Relationship |
|------|--------------|
| `RoslynEmitter.cs` | Primary consumer; uses context for all code generation |
| `TypeMapper.cs` | Uses context for type resolution and mapping |
| `SymbolTable.cs` | Provides symbol lookup functionality |
| `BuiltinRegistry.cs` | Provides builtin identification |
| `Compiler.cs` | Creates and initializes context |
| `AssemblyCompiler.cs` | Sets project-specific context properties |

---

## Next Steps for Learning

1. **Read Next**: `RoslynEmitter.cs` - See how `CodeGenContext` is actually used
2. **Understand**: `SymbolTable.cs` - Learn how symbols are stored and retrieved
3. **Explore**: `BuiltinRegistry.cs` - See how builtins are registered and queried
4. **Practice**: Write a unit test that creates a `CodeGenContext` and performs lookups

---

**Document Version**: 1.0  
**Last Updated**: 2025-12-27  
**Maintainer**: Sharpy Compiler Team
