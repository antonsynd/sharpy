# Walkthrough: CodeGenContext.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

---

## Overview

`CodeGenContext` is the **state container** for the code generation phase of the Sharpy compiler. Think of it as the "control panel" that the `RoslynEmitter` consults while transforming a typed AST (Abstract Syntax Tree) into C# code using Roslyn's `SyntaxFactory`.

**Role in the Pipeline:**
```
Semantic Analysis → CodeGenContext → RoslynEmitter → C# Source Code
     (typed AST)    (state & config)  (transformation)
```

This class doesn't perform any transformation itself—it's a **passive data holder** that provides:
- Access to semantic information (symbol tables, type info)
- Configuration flags (entry point detection, project structure)
- Error accumulation during code generation
- Utility methods for indentation and symbol lookups

---

## Class Structure

### Core Responsibilities

`CodeGenContext` serves three primary roles:

1. **Semantic Data Access** — Bridges code generation to the semantic analysis phase
2. **Configuration Management** — Stores project-level settings (namespaces, entry points)
3. **Error Tracking** — Collects errors that occur during C# code emission

### Class Diagram

```csharp
public class CodeGenContext
{
    // Semantic Data (read-only references from upstream)
    SymbolTable SymbolTable
    BuiltinRegistry Builtins
    SemanticBinding? SemanticBinding

    // Configuration
    string? SourceFilePath
    string? ProjectNamespace
    string? ProjectRootPath
    bool IsEntryPoint
    ICompilerLogger Logger

    // State Management
    int _indentLevel
    List<string> _errors
}
```

---

## Key Properties

### 1. Semantic Data Properties

#### `SymbolTable` (required)
**Type**: `SymbolTable` (from `Sharpy.Compiler.Semantic`)
**Purpose**: Resolves user-defined symbols (variables, functions, classes) to their semantic information.

```csharp
public SymbolTable SymbolTable { get; }
```

**When to use**: When the emitter needs to know "What type does variable `x` have?" or "What function does `calculate_total` refer to?"

**Example usage in RoslynEmitter**:
```csharp
var symbol = _context.LookupSymbol("user_input");
if (symbol?.Type is FunctionType funcType) {
    // Generate C# method call with correct signature
}
```

#### `Builtins` (required)
**Type**: `BuiltinRegistry` (from `Sharpy.Compiler.Semantic`)
**Purpose**: Identifies Sharpy's built-in functions (e.g., `print`, `len`, `range`) and types (e.g., `int`, `str`, `list`).

```csharp
public BuiltinRegistry Builtins { get; }
```

**Why it matters**: Built-in functions map to `Sharpy.Core.Exports` static methods, not user-defined code. The emitter needs to distinguish:
- `len(my_list)` → `Sharpy.Core.Exports.Len(myList)`
- `my_function(arg)` → `MyFunction(arg)` (user-defined)

#### `SemanticBinding` (optional)
**Type**: `SemanticBinding?` (from `Sharpy.Compiler.Semantic`)
**Purpose**: Stores immutable semantic data outside the AST (following the "Immutable AST" rule from CLAUDE.md).

```csharp
public SemanticBinding? SemanticBinding { get; set; }
```

**Design rationale**: Originally, semantic data was stored directly on AST nodes as mutable properties. This led to bugs when AST nodes were reused or cached. The `SemanticBinding` class separates semantic data into a parallel structure indexed by AST node references.

**Usage example**: Resolving imports without mutating the AST:
```csharp
var importData = _context.SemanticBinding?.GetImportInfo(importNode);
```

---

### 2. Configuration Properties

#### `SourceFilePath` (optional)
**Type**: `string?`
**Purpose**: The absolute path to the `.spy` file being compiled. Used for generating source location metadata and computing relative namespaces.

#### `ProjectNamespace` (optional)
**Type**: `string?`
**Purpose**: The root namespace for multi-file projects (e.g., `"MySharpyApp"`). Single-file scripts don't set this.

**Example**: If compiling `src/utils/helpers.spy` in a project with `ProjectNamespace = "MyApp"`, the generated C# might use namespace `MyApp.Utils`.

#### `ProjectRootPath` (optional)
**Type**: `string?`
**Purpose**: The root directory of a multi-file project. Used with `SourceFilePath` to compute relative namespaces.

**Calculation**:
```
SourceFilePath:   /home/user/project/src/utils/helpers.spy
ProjectRootPath:  /home/user/project
Relative path:    src/utils/helpers.spy
Namespace:        MyApp.Src.Utils
```

#### `IsEntryPoint` (configuration flag)
**Type**: `bool` (default: `false`)
**Purpose**: Signals whether this file should generate a `static void Main(string[] args)` method.

**Important**: Only the entry point file sets this to `true`. Libraries and imported modules must set this to `false` to avoid "multiple Main methods" compiler errors.

**Usage in RoslynEmitter**:
```csharp
if (_context.IsEntryPoint) {
    // Emit: public static void Main(string[] args) { ... }
}
```

#### `Logger` (logging infrastructure)
**Type**: `ICompilerLogger` (from `Sharpy.Compiler.Logging`)
**Default**: `NullLogger.Instance` (silent logger)
**Purpose**: Emits warnings and debug messages during code generation.

**Example logging scenarios**:
- Warn when mangling a Python name conflicts with a C# keyword
- Log when falling back to `dynamic` type for unresolved expressions

---

### 3. Error Tracking Properties

#### `Errors` (read-only collection)
**Type**: `IReadOnlyList<string>`
**Purpose**: Exposes accumulated errors for the caller to inspect.

```csharp
public IReadOnlyList<string> Errors => _errors;
```

**Backing field**: Private `List<string> _errors`

#### `HasErrors` (convenience property)
**Type**: `bool`
**Purpose**: Quick check for error state without examining the collection.

```csharp
public bool HasErrors => _errors.Count > 0;
```

**Usage pattern**:
```csharp
var emitter = new RoslynEmitter(context);
var csharpCode = emitter.Emit(ast);

if (context.HasErrors) {
    foreach (var error in context.Errors) {
        Console.Error.WriteLine(error);
    }
    return ExitCode.CodeGenError;
}
```

---

## Key Methods

### Constructor

```csharp
public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
{
    SymbolTable = symbolTable;
    Builtins = builtins;
}
```

**Required parameters**:
- `symbolTable`: The symbol table built during semantic analysis
- `builtins`: The builtin registry (usually shared across all compilation units)

**Optional configuration** (set after construction):
```csharp
var context = new CodeGenContext(symbolTable, builtins);
context.SourceFilePath = "/path/to/script.spy";
context.IsEntryPoint = true;
context.Logger = new ConsoleLogger();
```

---

### `AddError(string message)`

```csharp
public void AddError(string message) => _errors.Add(message);
```

**Purpose**: Records a code generation error (e.g., unsupported AST node, invalid type mapping).

**Important**: Errors here are **non-fatal** during emission. The emitter continues processing and accumulates all errors. The caller decides whether to abort based on `HasErrors`.

**Example usage in RoslynEmitter**:
```csharp
if (typeAnnotation is UnknownType) {
    _context.AddError($"Cannot map Sharpy type to C# type: {typeAnnotation}");
    return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)); // fallback
}
```

---

### Indentation Management

#### `Indent()` / `Dedent()` / `GetIndent()`

```csharp
public void Indent() => _indentLevel++;
public void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);
public string GetIndent() => new string(' ', _indentLevel * IndentSize);
```

**Purpose**: Tracks indentation level for pretty-printing generated code.

**Design note**: The `Dedent()` implementation uses `Math.Max(0, _indentLevel - 1)` to prevent negative indentation from mismatched `Indent()`/`Dedent()` calls.

**Usage pattern** (hypothetical string-based code generation):
```csharp
_context.Indent();
sb.AppendLine(_context.GetIndent() + "if (condition) {");
_context.Indent();
sb.AppendLine(_context.GetIndent() + "DoSomething();");
_context.Dedent();
sb.AppendLine(_context.GetIndent() + "}");
_context.Dedent();
```

**Important caveat**: Sharpy's `RoslynEmitter` uses `SyntaxFactory` exclusively (per CLAUDE.md Critical Rule #2), so these indentation methods are **legacy/unused** in the current implementation. They're preserved for:
1. Debugging output (when dumping raw C# code)
2. Potential future string-based emitters
3. Testing utilities

---

### Symbol Lookup Utilities

#### `LookupSymbol(string name)`

```csharp
public Symbol? LookupSymbol(string name)
{
    return SymbolTable.Lookup(name);
}
```

**Purpose**: Convenience wrapper around `SymbolTable.Lookup()`.

**Returns**: `Symbol?` (nullable) — `null` if the symbol doesn't exist.

**Usage in RoslynEmitter**:
```csharp
var symbol = _context.LookupSymbol("my_variable");
if (symbol is VariableSymbol varSymbol) {
    var csharpType = MapType(varSymbol.Type);
    // ...
}
```

#### `IsBuiltinFunction(string name)` / `IsBuiltinType(string name)`

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

**Purpose**: Quick checks to distinguish built-ins from user-defined symbols.

**Example decision tree**:
```csharp
if (_context.IsBuiltinFunction("print")) {
    // Emit: Sharpy.Core.Exports.Print(...)
} else if (_context.LookupSymbol("print") is FunctionSymbol userFunc) {
    // Emit: Print(...) — user defined a function named 'print'
} else {
    _context.AddError("Unknown function: print");
}
```

---

## Dependencies

### Internal Dependencies (Sharpy.Compiler)

| Namespace | Types Used | Purpose |
|-----------|------------|---------|
| `Sharpy.Compiler.Semantic` | `SymbolTable`, `BuiltinRegistry`, `SemanticBinding`, `Symbol` | Access to semantic analysis results |
| `Sharpy.Compiler.Logging` | `ICompilerLogger`, `NullLogger` | Compiler logging infrastructure |

### Downstream Consumers

| Component | File | How It Uses CodeGenContext |
|-----------|------|----------------------------|
| `RoslynEmitter` | `RoslynEmitter.*.cs` | Passes context to all `Emit*()` methods; queries symbol table, builtins, and config |
| `TypeMapper` | `TypeMapper.cs` | May receive context for logging type mapping warnings |
| `NameMangler` | `NameMangler.cs` | May use context for logging name mangling conflicts |

**Key insight**: `CodeGenContext` is **read-mostly**. The emitter queries it frequently but modifies only the `_errors` list and (rarely) indentation state.

---

## Patterns and Design Decisions

### 1. **Separation of Concerns**
- **Context** = State container (this file)
- **Emitter** = Transformation logic (`RoslynEmitter.*.cs`)
- **Mappers** = Specialized conversions (`TypeMapper.cs`, `NameMangler.cs`)

This separation allows testing the emitter with mock contexts and swapping context implementations (e.g., adding caching layers).

### 2. **Immutable AST Compliance**
The `SemanticBinding` property enforces the "Immutable AST" rule (CLAUDE.md Critical Rule #3). Instead of:
```csharp
// ❌ BAD: Mutating AST nodes
astNode.ResolvedType = inferredType;
```

Do:
```csharp
// ✅ GOOD: Store in parallel SemanticBinding
context.SemanticBinding.SetNodeType(astNode, inferredType);
```

### 3. **Error Accumulation Over Exceptions**
Code generation errors are collected, not thrown. This design:
- **Allows batch error reporting** (show all errors at once)
- **Enables partial code generation** (useful for IDE tooling)
- **Simplifies emitter logic** (no try-catch boilerplate)

**Tradeoff**: Caller must check `HasErrors` after emission.

### 4. **Nullable Reference Types**
Optional properties (`SourceFilePath`, `ProjectNamespace`, etc.) use `string?` to signal "this is not required for single-file scripts."

**Design guideline**: If a property is `null`, the emitter should use sensible defaults:
- No `ProjectNamespace` → Use root namespace `"SharpyGenerated"`
- No `SourceFilePath` → Skip `#line` directives

---

## Debugging Tips

### 1. **"Symbol not found" Errors**
If `LookupSymbol(name)` returns `null` unexpectedly:

1. **Check symbol table construction**: Is the symbol being added during semantic analysis?
   ```bash
   # Add breakpoint in SemanticAnalyzer where symbols are added
   ```

2. **Check name mangling**: Python `snake_case` → C# `PascalCase`
   ```csharp
   // When looking up "my_var", the symbol table stores "MyVar"
   var symbol = _context.LookupSymbol(NameMangler.Mangle("my_var"));
   ```

3. **Check scope**: Symbol tables are hierarchical. Ensure you're searching the correct scope.

### 2. **"Multiple Main methods" Error**
If you see:
```
error CS0017: Program has more than one entry point defined
```

**Cause**: Multiple files have `IsEntryPoint = true`.

**Fix**: Ensure only the entry script sets this flag:
```csharp
var mainFileContext = new CodeGenContext(symbolTable, builtins);
mainFileContext.IsEntryPoint = true; // Only for main.spy

var libraryContext = new CodeGenContext(symbolTable, builtins);
libraryContext.IsEntryPoint = false; // For imported modules
```

### 3. **Missing Semantic Data**
If `SemanticBinding` is `null` or missing data:

1. **Verify semantic analysis ran**: Check that `SemanticAnalyzer.Analyze()` completed successfully.
2. **Check binding population**: Ensure `SemanticBinding` is passed through the pipeline:
   ```csharp
   var analyzer = new SemanticAnalyzer(ast);
   var binding = analyzer.Analyze();

   var context = new CodeGenContext(analyzer.SymbolTable, builtins);
   context.SemanticBinding = binding; // ← Don't forget this!
   ```

### 4. **Inspecting Context State**
Add this debugging helper to your emitter:
```csharp
private void DumpContext()
{
    Console.WriteLine($"[CodeGenContext]");
    Console.WriteLine($"  Source: {_context.SourceFilePath ?? "(none)"}");
    Console.WriteLine($"  Entry Point: {_context.IsEntryPoint}");
    Console.WriteLine($"  Errors: {_context.Errors.Count}");
    Console.WriteLine($"  Symbols: {_context.SymbolTable.Count}");
}
```

Call before/after emission to track state changes.

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made to This File

#### ✅ **Likely Changes**

1. **Adding Configuration Properties**
   - Example: Add `public bool EnableNullableContext { get; set; }` to control C# nullable reference types
   - Pattern: Add property → Update constructor/docs → Use in `RoslynEmitter`

2. **Enhancing Error Reporting**
   - Example: Replace `List<string>` with `List<CompilerError>` (with source location, severity)
   - Impact: Update `AddError()` signature and `Errors` property type

3. **Adding Caching/Memoization**
   - Example: Cache `LookupSymbol()` results in a dictionary
   - Rationale: Reduce symbol table lookups during large compilations

4. **Multi-File Compilation Features**
   - Example: Add `public Dictionary<string, ModuleInfo> ImportedModules { get; }`
   - Rationale: Track cross-file dependencies for incremental compilation

#### ❌ **Unlikely Changes**

1. **Adding Transformation Logic**
   - ❌ Don't add methods like `EmitFunction()` here — those belong in `RoslynEmitter`
   - Rule: Context is **passive state**, not **active logic**

2. **Breaking Immutability**
   - ❌ Don't allow external mutation of `SymbolTable` or `Builtins`
   - Rule: These are set once in the constructor and should remain read-only

### Code Style Guidelines

When modifying this file:

1. **Use XML Doc Comments** for all public members:
   ```csharp
   /// <summary>
   /// Brief description (one sentence).
   /// </summary>
   public string PropertyName { get; set; }
   ```

2. **Prefer Auto-Properties** for simple getters/setters:
   ```csharp
   public bool IsEntryPoint { get; set; } = false; // ✅ Clear default value
   ```

3. **Validate Invariants** in setters when needed:
   ```csharp
   private int _indentLevel;
   public int IndentLevel
   {
       get => _indentLevel;
       set => _indentLevel = Math.Max(0, value); // Prevent negative indentation
   }
   ```

4. **Follow Null Safety Conventions**:
   - Use `?` suffix for optional properties (`string? SourceFilePath`)
   - Provide non-null defaults where possible (`Logger = NullLogger.Instance`)

---

## Cross-References

### Related CodeGen Files
- **[RoslynEmitter.md](RoslynEmitter.md)** — Main consumer of `CodeGenContext`; orchestrates C# code generation
- **[TypeMapper.md](TypeMapper.md)** — Maps Sharpy types to C# types; may log warnings via context
- **[NameMangler.md](NameMangler.md)** — Converts Python naming conventions to C# (e.g., `snake_case` → `PascalCase`)

### Semantic Analysis Dependencies
- **SymbolTable** (`src/Sharpy.Compiler/Semantic/SymbolTable.cs`) — Stores resolved symbols (variables, functions, classes)
- **BuiltinRegistry** (`src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`) — Defines Sharpy's built-in functions and types
- **SemanticBinding** (`src/Sharpy.Compiler/Semantic/SemanticBinding.cs`) — Immutable storage for semantic data

### Logging Infrastructure
- **ICompilerLogger** (`src/Sharpy.Compiler/Logging/ICompilerLogger.cs`) — Interface for compiler logging
- **NullLogger** (`src/Sharpy.Compiler/Logging/NullLogger.cs`) — Silent logger (default)

### Partial Class Notice
**This is NOT a partial class.** It's a standalone class. However, its main consumer (`RoslynEmitter`) is split across multiple partial class files:
- `RoslynEmitter.cs` (main file)
- `RoslynEmitter.Expressions.cs`
- `RoslynEmitter.Statements.cs`
- `RoslynEmitter.ClassMembers.cs`
- `RoslynEmitter.CompilationUnit.cs`
- `RoslynEmitter.ModuleClass.cs`
- `RoslynEmitter.TypeDeclarations.cs`
- `RoslynEmitter.Operators.cs`

Refer to these files to see how `CodeGenContext` is used in practice.

---

## Quick Reference

### Typical Initialization Pattern

```csharp
// After semantic analysis completes...
var context = new CodeGenContext(semanticAnalyzer.SymbolTable, builtinRegistry);
context.SourceFilePath = inputFile;
context.IsEntryPoint = (inputFile == entryPointFile);
context.Logger = compilerOptions.Verbose ? new ConsoleLogger() : NullLogger.Instance;
context.SemanticBinding = semanticAnalyzer.Binding;

// Generate C# code
var emitter = new RoslynEmitter(context);
var csharpSyntax = emitter.EmitCompilationUnit(ast);

// Check for errors
if (context.HasErrors) {
    foreach (var error in context.Errors) {
        Console.Error.WriteLine($"Code generation error: {error}");
    }
    Environment.Exit(1);
}

// Proceed to Roslyn compilation...
```

### Common Queries

| Task | Code |
|------|------|
| Check if name is built-in | `_context.IsBuiltinFunction("print")` |
| Look up user symbol | `_context.LookupSymbol("my_var")` |
| Record error | `_context.AddError("Type mismatch")` |
| Check for errors | `if (_context.HasErrors) { ... }` |
| Access semantic data | `_context.SemanticBinding?.GetNodeType(expr)` |

---

**Last Updated**: 2026-01-23
**Maintainer**: Sharpy Compiler Team
**Related Documentation**: See [CodeGen/README.md](../README.md) for pipeline overview
