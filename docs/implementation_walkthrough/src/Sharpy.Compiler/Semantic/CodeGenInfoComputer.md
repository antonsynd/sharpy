# Walkthrough: CodeGenInfoComputer.cs

**Source File**: `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

---

## Overview

`CodeGenInfoComputer` is a semantic analysis component that bridges the gap between type checking and code generation. It runs **after** type checking is complete and **before** code emission begins. Its sole purpose is to compute and attach `CodeGenInfo` metadata to symbols in the symbol table.

**What it does:**
- Determines the C# names for all Sharpy symbols (applying naming conventions like `snake_case` → `camelCase/PascalCase`)
- Decides which module-level variables should become static fields vs local variables in `Main()`
- Detects execution order issues in module-level variable initializers
- Tags symbols with import metadata for proper code generation

**Why it exists:**
Previously, these decisions were made **during code emission** by `RoslynEmitter`. This meant:
- Recomputation of the same information multiple times
- Scattered logic that was hard to maintain
- Tight coupling between semantic analysis and code generation

By moving this computation to semantic analysis, we achieve:
- **Single source of truth**: Compute once, use everywhere
- **Better separation of concerns**: Semantic phase knows about naming rules, codegen just uses the names
- **Easier debugging**: Inspect computed names before emission starts

**Pipeline Position:**
```
Parser (AST) → NameResolver → TypeResolver → TypeChecker → CodeGenInfoComputer → RoslynEmitter → C#
                                                            ↑ YOU ARE HERE
```

---

## Class/Type Structure

### Main Class: `CodeGenInfoComputer`

```csharp
public class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();
}
```

**Fields:**
- `_symbolTable`: Reference to the compiler's symbol table containing all symbols from semantic analysis
- `_processedModuleLevelVars`: Tracks which module-level variables have been processed (prevents duplicate processing)
- `_variablesWithExecutionOrderIssues`: Result from `ExecutionOrderAnalyzer` - variables that can't be static fields

### Related Types

**`CodeGenInfo` (in `CodeGenInfo.cs`)**
The data structure that gets attached to each symbol:

```csharp
public sealed record CodeGenInfo
{
    public required string CSharpName { get; init; }           // "myVariable" or "MyClass"
    public required string OriginalName { get; init; }         // "my_variable" or "MyClass"
    public int Version { get; init; } = 0;                     // For redeclared variables
    public bool IsModuleLevel { get; init; }                   // Static field vs local variable
    public bool IsConstant { get; init; }                      // const field
    public bool HasExecutionOrderIssues { get; init; }         // Can't be static field initializer
    public bool IsStringEnum { get; init; }                    // String enum (class) vs int enum
    public ImportKind ImportKind { get; init; }                // How symbol was imported
    public string? OriginalImportName { get; init; }           // For aliased imports
}
```

**`ExecutionOrderAnalyzer` (in `ExecutionOrderAnalyzer.cs`)**
Analyzes module-level statements to detect variables with initialization order problems (covered in detail later).

---

## Key Functions/Methods

### 1. `ComputeForModule(Module module)` - Entry Point

**Purpose:** Main entry point that orchestrates the computation of `CodeGenInfo` for all symbols in a module.

**Algorithm:**
```csharp
public void ComputeForModule(Module module)
{
    // Step 1: Analyze execution order issues
    var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
    _variablesWithExecutionOrderIssues = analyzer.Analyze(module.Body);

    // Step 2: Process module-level declarations (variables, imports)
    ProcessModuleLevelDeclarations(module);

    // Step 3: Process type declarations (classes, structs, interfaces, enums, functions)
    foreach (var stmt in module.Body)
    {
        switch (stmt)
        {
            case ClassDef classDef: ProcessClassDef(classDef); break;
            case StructDef structDef: ProcessStructDef(structDef); break;
            case InterfaceDef interfaceDef: ProcessInterfaceDef(interfaceDef); break;
            case EnumDef enumDef: ProcessEnumDef(enumDef); break;
            case FunctionDef funcDef: ProcessFunctionDef(funcDef, isModuleLevel: true); break;
        }
    }
}
```

**Two-Pass Approach:**
1. **First pass** (`ProcessModuleLevelDeclarations`): Handles variables, constants, and imports
2. **Second pass**: Handles type definitions and functions

**Why two passes?** Module-level variables and imports need special handling for execution order analysis before processing types.

---

### 2. `ProcessModuleLevelVariable(VariableDeclaration varDecl)` - Core Variable Logic

**Purpose:** Determines how a module-level variable should be emitted in C#.

**The Critical Decision:**
```csharp
var hasIssues = _variablesWithExecutionOrderIssues.Contains(varDecl.Name);

// Variables with execution order issues → locals in Main() (camelCase)
// Module-level fields → static fields (PascalCase)
var csharpName = hasIssues
    ? NameMangler.ToCamelCase(varDecl.Name)
    : NameMangler.ToPascalCase(varDecl.Name);
```

**Example:**

```python
# Sharpy source
X = 10              # No issues → static field "X"
y = X + 5           # References X (another var) → local "y" in Main()
PI = 3.14159        # Const → static const "PI"
```

**Generated C#:**
```csharp
public static class Module
{
    private static int X = 10;        // Module-level field (PascalCase)
    private const double PI = 3.14159; // Constant (CONSTANT_CASE preserved)

    private static void Main()
    {
        var y = X + 5;                 // Local variable (camelCase)
    }
}
```

**CodeGenInfo created:**
```csharp
varSymbol.CodeGenInfo = new CodeGenInfo
{
    CSharpName = csharpName,                    // "y" or "X"
    OriginalName = varDecl.Name,                // Original Sharpy name
    Version = 0,                                // No redeclaration
    IsModuleLevel = !hasIssues,                 // false for "y", true for "X"
    IsConstant = false,                         // Not a const
    HasExecutionOrderIssues = hasIssues         // true for "y"
};
```

---

### 3. `ProcessModuleLevelConstant(VariableDeclaration constDecl)` - Constants

**Purpose:** Handle constant declarations (marked with `const` keyword).

**Key Points:**
- Constants always become static const fields (never locals)
- Use `CONSTANT_CASE` naming via `NameMangler.ToConstantCase()`
- No execution order issues (constants are compile-time)

```csharp
varSymbol.CodeGenInfo = new CodeGenInfo
{
    CSharpName = NameMangler.ToConstantCase(constDecl.Name),
    OriginalName = constDecl.Name,
    Version = 0,
    IsModuleLevel = true,      // Always module-level
    IsConstant = true,         // Mark as constant
    HasExecutionOrderIssues = false  // Constants don't have execution order issues
};
```

---

### 4. `ProcessImport` and `ProcessFromImport` - Import Metadata

**Purpose:** Tag imported symbols with import metadata for proper code generation.

#### `ProcessImport(ImportStatement import)`

Handles `import module` and `import module as alias`:

```python
import sys
import numpy as np
```

```csharp
foreach (var alias in import.Names)
{
    var effectiveName = alias.AsName ?? alias.Name;  // "np" or "sys"
    var symbol = _symbolTable.Lookup(effectiveName);
    if (symbol is ModuleSymbol moduleSymbol)
    {
        moduleSymbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = effectiveName.Replace(".", "_"),  // "numpy" → "numpy", "os.path" → "os_path"
            OriginalName = effectiveName,
            ImportKind = alias.AsName != null
                ? ImportKind.FromImportWithAlias
                : ImportKind.ModuleImport
        };
    }
}
```

#### `ProcessFromImport(FromImportStatement fromImport)`

Handles `from module import symbol [as alias]`:

```python
from math import pi, sqrt
from config import MAX_SIZE as MAX
```

**Naming logic:**
```csharp
private string DetermineCSharpNameForFromImport(string name, Symbol symbol)
{
    // CONSTANT_CASE names stay as CONSTANT_CASE
    if (IsConstantCaseName(name))  // All uppercase with underscores
        return NameMangler.ToConstantCase(name);

    // Other names become PascalCase
    return NameMangler.ToPascalCase(name);
}
```

**Why this matters:**
- `from math import PI` → `PI` (keep as constant)
- `from utils import helper_function` → `HelperFunction` (PascalCase)

---

### 5. Type Processing Methods

#### `ProcessClassDef(ClassDef classDef)`

**Purpose:** Process class definitions and their members.

```csharp
private void ProcessClassDef(ClassDef classDef)
{
    var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
    if (typeSymbol != null)
    {
        // Class name → PascalCase
        typeSymbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = NameMangler.ToPascalCase(classDef.Name),
            OriginalName = classDef.Name
        };

        // Process class members (fields, methods)
        ProcessTypeMembers(typeSymbol, classDef.Body);
    }
}
```

**Handled similarly:**
- `ProcessStructDef` - Structs (PascalCase)
- `ProcessInterfaceDef` - Interfaces (uses `ToInterfaceName` to preserve/add `I` prefix)

#### `ProcessEnumDef(EnumDef enumDef)`

**Special handling for string enums:**

```csharp
private void ProcessEnumDef(EnumDef enumDef)
{
    var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
    if (typeSymbol != null)
    {
        // Check if any member has a string value
        var isStringEnum = enumDef.Members.Any(m => m.Value is StringLiteral);

        typeSymbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = NameMangler.ToPascalCase(enumDef.Name),
            OriginalName = enumDef.Name,
            IsStringEnum = isStringEnum  // ← Important for code generation
        };
    }
}
```

**Why string enum detection matters:**
- **Int enums** → C# `enum` type
- **String enums** → C# `class` with `static readonly` fields (C# enums can't have string values)

**Example:**
```python
# Integer enum
enum Status:
    PENDING = 0
    APPROVED = 1
    REJECTED = 2

# String enum
enum Color:
    RED = "red"
    GREEN = "green"
    BLUE = "blue"
```

**Generated C#:**
```csharp
// Int enum
public enum Status { PENDING = 0, APPROVED = 1, REJECTED = 2 }

// String enum (class)
public class Color
{
    public static readonly Color RED = new Color("red");
    public static readonly Color GREEN = new Color("green");
    public static readonly Color BLUE = new Color("blue");

    private readonly string _value;
    private Color(string value) => _value = value;
}
```

---

### 6. `ProcessTypeMembers` - Class/Struct Members

**Purpose:** Process fields and methods inside type definitions.

```csharp
private void ProcessTypeMembers(TypeSymbol typeSymbol, IEnumerable<Statement> body)
{
    foreach (var stmt in body)
    {
        switch (stmt)
        {
            case VariableDeclaration fieldDecl:
                ProcessField(typeSymbol, fieldDecl);
                break;
            case FunctionDef funcDef:
                ProcessMethodDef(typeSymbol, funcDef);
                break;
        }
    }
}
```

#### Field Processing

```csharp
private void ProcessField(TypeSymbol typeSymbol, VariableDeclaration fieldDecl)
{
    var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
    if (fieldSymbol != null)
    {
        fieldSymbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = NameMangler.ToCamelCase(fieldDecl.Name),  // camelCase for fields
            OriginalName = fieldDecl.Name,
            IsModuleLevel = false,  // Class fields aren't module-level
            IsConstant = fieldDecl.IsConst
        };
    }
}
```

**Naming convention:**
```python
class Person:
    first_name: str    # → private string firstName;
    last_name: str     # → private string lastName;
```

#### Method Processing

```csharp
private void ProcessMethodDef(TypeSymbol typeSymbol, FunctionDef funcDef)
{
    var methodSymbol = typeSymbol.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
    if (methodSymbol != null)
    {
        methodSymbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = NameMangler.ToPascalCase(funcDef.Name),  // PascalCase for methods
            OriginalName = funcDef.Name,
            IsModuleLevel = false
        };
    }
}
```

**Naming convention:**
```python
class Calculator:
    def add_numbers(self, a: int, b: int) -> int:  # → public int AddNumbers(...)
        return a + b
```

---

## Dependencies

### Internal Dependencies

**`NameMangler` (in `CodeGen/NameMangler.cs`)**
- Converts Sharpy naming conventions to C# conventions
- Key methods:
  - `ToPascalCase(name)` - Classes, methods, module-level variables
  - `ToCamelCase(name)` - Local variables, fields, parameters
  - `ToConstantCase(name)` - Constants (preserves CAPS_SNAKE_CASE)
  - `ToInterfaceName(name)` - Interfaces (preserves/adds `I` prefix)

**`ExecutionOrderAnalyzer` (in `Semantic/ExecutionOrderAnalyzer.cs`)**
- Detects variables with execution order issues
- Multi-pass analysis:
  1. Collect type/function/const names
  2. Detect assignment-before-declaration
  3. Detect transitive dependencies (variable depends on another variable)

**Execution order issues include:**
- Assignment before declaration: `x = 5` before `x: int = 10`
- Multiple declarations: `x: int = 1` then `x: int = 2`
- Initializer references assignment variable: `y = 10; x = y + 5`
- Initializer references another module variable: `X = 10; Y = X + 5` (C# static field init order undefined)

**`SymbolTable` (in `Semantic/SymbolTable.cs`)**
- Contains all symbols discovered during semantic analysis
- Provides `Lookup(name)` to retrieve symbols by name

**`CodeGenInfo` (in `Semantic/CodeGenInfo.cs`)**
- Data structure attached to symbols
- Immutable record with required and optional fields

### Downstream Dependencies

**`RoslynEmitter` (in `CodeGen/RoslynEmitter*.cs`)**
- Consumes `CodeGenInfo` from symbols
- Uses `symbol.CodeGenInfo.CSharpName` instead of computing names on-the-fly
- Checks `HasExecutionOrderIssues` to decide field vs local variable placement

---

## Patterns and Design Decisions

### 1. Two-Pass Processing

**Why not single pass?**
- Module-level variables need execution order analysis first
- Types and functions can reference module-level variables
- Separation makes the logic clearer and more maintainable

### 2. Execution Order Analysis as Separate Class

**Design rationale:**
- `ExecutionOrderAnalyzer` is complex (300+ lines) and deserves its own file
- Can be tested independently
- Single Responsibility Principle: `CodeGenInfoComputer` orchestrates, `ExecutionOrderAnalyzer` analyzes

### 3. Immutable `CodeGenInfo` Record

**Why immutable?**
- Prevents accidental modification after computation
- Makes debugging easier (inspect once, trust it doesn't change)
- Record syntax provides value equality for testing

### 4. Symbol Table as Dependency Injection

```csharp
public CodeGenInfoComputer(SymbolTable symbolTable)
{
    _symbolTable = symbolTable;
}
```

**Benefits:**
- Testable: Can inject mock symbol table
- Clear dependency: Makes it explicit what data we need
- No global state

### 5. Attached Metadata Pattern

**Instead of:**
```csharp
Dictionary<Symbol, CodeGenInfo> _metadata;  // External mapping
```

**We use:**
```csharp
public class Symbol
{
    public CodeGenInfo? CodeGenInfo { get; set; }  // Attached to symbol
}
```

**Why?**
- Co-located: Symbol and its metadata travel together
- Simpler: No need to maintain separate dictionary
- Natural: Symbol "owns" its code generation metadata

**Trade-off:** Slight coupling between semantic model and codegen, but acceptable since `CodeGenInfo` is in `Semantic` namespace.

### 6. Module-Level Variable Decision Logic

**The fundamental question:** Should this variable be a static field or a local in `Main()`?

**Sharpy semantics:**
```python
# Module-level statements execute top-to-bottom
X = 10        # First
Y = X + 5     # Second (depends on X)
print(Y)      # Third
```

**C# has two options:**

**Option A: Static field initializers (undefined order)**
```csharp
private static int X = 10;
private static int Y = X + 5;  // ⚠️ Might run before X! Undefined!
```

**Option B: Initialization in Main() (defined order)**
```csharp
private static int X = 10;

private static void Main()
{
    var y = X + 5;  // ✅ Runs after X is initialized
    Console.WriteLine(y);
}
```

**Decision:** Use execution order analysis to detect issues → place problematic variables in `Main()`.

---

## Debugging Tips

### 1. Inspect CodeGenInfo After Semantic Analysis

**Add breakpoint after `ComputeForModule`:**
```csharp
var computer = new CodeGenInfoComputer(symbolTable);
computer.ComputeForModule(module);  // ← Breakpoint here

// Inspect symbols:
foreach (var symbol in symbolTable.GetAllSymbols())
{
    if (symbol.CodeGenInfo != null)
    {
        Console.WriteLine($"{symbol.Name} → {symbol.CodeGenInfo.CSharpName}");
    }
}
```

### 2. Check Execution Order Issues

**If a variable isn't being emitted as expected:**
```csharp
// In ComputeForModule, after analyzer runs:
foreach (var varName in _variablesWithExecutionOrderIssues)
{
    Console.WriteLine($"Variable '{varName}' has execution order issues");
}
```

### 3. Verify NameMangler Behavior

**Test naming transformations:**
```csharp
Console.WriteLine(NameMangler.ToPascalCase("my_function"));     // MyFunction
Console.WriteLine(NameMangler.ToCamelCase("my_variable"));      // myVariable
Console.WriteLine(NameMangler.ToConstantCase("MAX_SIZE"));      // MAX_SIZE
Console.WriteLine(NameMangler.ToPascalCase("__init__"));        // Constructor
```

### 4. Common Issues

**Issue: Variable not found in symbol table**
```csharp
var symbol = _symbolTable.Lookup(varDecl.Name);
if (symbol == null)
{
    // Problem: NameResolver didn't add this symbol
    // Check NameResolver.cs for bugs
}
```

**Issue: CodeGenInfo is null during emission**
```csharp
// In RoslynEmitter:
if (symbol.CodeGenInfo == null)
{
    // Problem: CodeGenInfoComputer wasn't run OR
    //          symbol type not handled in ComputeForModule
}
```

**Issue: Wrong C# name generated**
- Check `NameMangler` logic
- Verify correct `NameMangler` method is being called
- Check if name should be treated as constant (ALL_CAPS check)

### 5. Trace Execution Order Analysis

**See what analyzer detected:**
```csharp
var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
analyzer.Analyze(module.Body);

// Add diagnostic output in ExecutionOrderAnalyzer.Analyze():
Console.WriteLine($"Assignment variables: {string.Join(", ", _assignmentVariables)}");
Console.WriteLine($"Variables with issues: {string.Join(", ", _variablesWithIssues)}");
```

---

## Contribution Guidelines

### When to Modify This File

**Add handling for new declaration types:**
```csharp
// Example: Adding support for type aliases
case TypeAliasDef aliasDef:
    ProcessTypeAlias(aliasDef);
    break;
```

**Change naming conventions:**
- Modify `NameMangler` (don't change this file)
- This file delegates to `NameMangler`

**Fix execution order detection bugs:**
- Modify `ExecutionOrderAnalyzer` (separate file)
- This file consumes its result

**Add new metadata to CodeGenInfo:**
1. Add field to `CodeGenInfo` record
2. Populate it in appropriate `Process*` method

### What NOT to Do

**❌ Don't compute types here**
- Type information comes from `TypeChecker`
- This class only attaches names and metadata

**❌ Don't emit C# code here**
- This is semantic analysis, not code generation
- `RoslynEmitter` consumes the metadata we produce

**❌ Don't modify the AST**
- AST is immutable after parsing
- We only read from it and write to symbols

**❌ Don't add business logic to symbol lookup**
- Keep it simple: lookup, check type, attach metadata
- Complex logic belongs in helper methods or separate classes

### Testing Strategy

**Unit tests should cover:**
- Each `Process*` method independently
- Various naming scenarios (snake_case, PascalCase, CONSTANT_CASE)
- Import handling (aliased, non-aliased, from-imports)
- Enum detection (string vs int)

**Integration tests should cover:**
- Full module processing
- Interaction with `ExecutionOrderAnalyzer`
- Correct metadata attached for complex scenarios

**Test example structure:**
```csharp
[Fact]
public void ProcessModuleLevelVariable_WithExecutionOrderIssues_UsesCamelCase()
{
    // Arrange: Create symbol table with variable that references another variable
    var symbolTable = new SymbolTable();
    var module = ParseModule("X = 10\nY = X + 5");

    // Act
    var computer = new CodeGenInfoComputer(symbolTable);
    computer.ComputeForModule(module);

    // Assert
    var ySymbol = symbolTable.Lookup("Y");
    Assert.NotNull(ySymbol.CodeGenInfo);
    Assert.Equal("y", ySymbol.CodeGenInfo.CSharpName);  // camelCase
    Assert.True(ySymbol.CodeGenInfo.HasExecutionOrderIssues);
    Assert.False(ySymbol.CodeGenInfo.IsModuleLevel);
}
```

### Code Style

**Follow existing patterns:**
```csharp
// ✅ Good: Consistent with file
private void ProcessNewDeclaration(NewDef newDef)
{
    var symbol = _symbolTable.Lookup(newDef.Name) as NewSymbol;
    if (symbol != null)
    {
        symbol.CodeGenInfo = new CodeGenInfo
        {
            CSharpName = NameMangler.ToPascalCase(newDef.Name),
            OriginalName = newDef.Name
        };
    }
}

// ❌ Bad: Different style
private void processNewDeclaration(NewDef decl) {
    if (decl == null) return;
    var sym = _symbolTable.Lookup(decl.Name);
    // ... different error handling ...
}
```

### Performance Considerations

**This runs once per module** - performance is not critical, but:
- ✅ Single pass over AST nodes
- ✅ Dictionary lookups are O(1)
- ✅ No nested loops over all symbols
- ⚠️ `ExecutionOrderAnalyzer` has transitive closure loop (can be O(n²) in worst case, but rare)

---

## Cross-References

### Related Semantic Analysis Files

- **[`ExecutionOrderAnalyzer.cs`](./ExecutionOrderAnalyzer.md)** - Detects variables with initialization order issues (if walkthrough exists)
- **[`CodeGenInfo.cs`](./CodeGenInfo.md)** - The metadata structure this class populates (if walkthrough exists)
- **[`SymbolTable.cs`](./SymbolTable.md)** - Symbol storage and lookup (if walkthrough exists)
- **[`TypeChecker.cs`](./TypeChecker.md)** - Runs before this class, provides type information (if walkthrough exists)

### Related CodeGen Files

- **[`NameMangler.cs`](../CodeGen/NameMangler.md)** - Naming convention transformations (if walkthrough exists)
- **[`RoslynEmitter.cs`](../CodeGen/RoslynEmitter.md)** - Consumes CodeGenInfo to emit C# (if walkthrough exists)

### Documentation

- **[Semantic Analysis Guide](../../../../.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md)** - High-level semantic phase documentation
- **[Language Specification](../../../../docs/language_specification/)** - Authoritative reference for Sharpy semantics

---

## Summary

`CodeGenInfoComputer` is the **bridge between semantic analysis and code generation**. It:

1. **Runs once** after type checking completes
2. **Computes C# names** for all symbols using `NameMangler`
3. **Detects execution order issues** using `ExecutionOrderAnalyzer`
4. **Attaches metadata** to symbols via `CodeGenInfo`
5. **Enables codegen** to be a simple "read and emit" process

**Key insight:** By computing this information upfront during semantic analysis, we make code generation deterministic, debuggable, and maintainable. The alternative (computing names on-the-fly during emission) leads to scattered logic and potential inconsistencies.

**For newcomers:** This class is a great example of the **Single Responsibility Principle** - it does one thing (compute codegen metadata) and does it well. When debugging name-related issues, this is your starting point.
