# Walkthrough: CodeGenContext.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

---

## Overview

`CodeGenContext` is a **state management class** used during the C# code generation phase of the Sharpy compiler. Think of it as the "memory" or "context bag" that the code generator carries around while transforming Sharpy AST nodes into C# code via Roslyn.

**Key Responsibilities:**
- Track indentation levels for pretty-printing generated C# code
- Provide access to the symbol table (for variable/function lookups)
- Provide access to builtin registry (to identify Sharpy builtin functions and types)
- Store project-level metadata (namespace, root path) for multi-file compilation
- Act as a bridge between the semantic analysis phase and code generation phase

**Role in the Compiler Pipeline:**
```
Sharpy Source → Lexer → Parser → Semantic Analyzer → Code Generator → C# Code
                                      ↓                      ↑
                                 SymbolTable          CodeGenContext
                                 BuiltinRegistry      (uses both)
```

---

## Class Structure

### Class Definition

```csharp
public class CodeGenContext
{
    // Private fields
    private int _indentLevel = 0;
    private const int IndentSize = 4;
    
    // Public properties
    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }
    public string? SourceFilePath { get; set; }
    public string? ProjectNamespace { get; set; }
    public string? ProjectRootPath { get; set; }
}
```

### Properties

#### `SymbolTable SymbolTable` (Read-only)
- **Type**: `SymbolTable` from the Semantic namespace
- **Purpose**: Stores all symbols (variables, functions, classes) discovered during semantic analysis
- **Initialized**: Via constructor parameter (injected dependency)
- **Usage**: Code generator uses this to look up type information when generating C# code

#### `BuiltinRegistry Builtins` (Read-only)
- **Type**: `BuiltinRegistry` from the Semantic namespace
- **Purpose**: Registry of all Sharpy builtin functions (`print`, `len`, `range`) and types (`int`, `str`, `list`)
- **Initialized**: Via constructor parameter (injected dependency)
- **Usage**: Code generator checks if a function/type is builtin vs user-defined to generate appropriate C# code

#### `SourceFilePath` (Nullable, Read-write)
- **Type**: `string?`
- **Purpose**: Path to the current Sharpy source file being compiled
- **Usage**: Used for generating appropriate namespaces and for error reporting with file context

#### `ProjectNamespace` (Nullable, Read-write)
- **Type**: `string?`
- **Purpose**: Root namespace for multi-file projects (e.g., "MyApp")
- **Set by**: `AssemblyCompiler` when compiling .spyproj files
- **Usage**: All generated C# files in a project will be under this namespace

#### `ProjectRootPath` (Nullable, Read-write)
- **Type**: `string?`
- **Purpose**: Root directory of the project (where .spyproj lives)
- **Usage**: Used to compute relative namespaces. For example:
  - Project root: `/home/user/myapp/`
  - Source file: `/home/user/myapp/src/utils/helpers.spy`
  - Resulting namespace: `MyApp.Src.Utils`

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

**Parameters:**
- `symbolTable`: The symbol table built during semantic analysis containing all discovered symbols
- `builtins`: Registry of Sharpy builtin functions and types

**Design Note**: Constructor follows **dependency injection** pattern. The code generator doesn't create its own symbol table or builtin registry—it receives them from the semantic analyzer, ensuring a single source of truth.

**Typical Usage:**
```csharp
// In RoslynEmitter or AssemblyCompiler
var symbolTable = semanticAnalyzer.SymbolTable;
var builtins = semanticAnalyzer.Builtins;
var context = new CodeGenContext(symbolTable, builtins);
```

---

### Indentation Management

#### `Indent()`
```csharp
public void Indent() => _indentLevel++;
```

**Purpose**: Increase indentation level by 1

**When to use**: Before generating code inside a block (class body, function body, if statement body, etc.)

**Example**:
```csharp
// Generating a class
sb.Append("class MyClass\n");
sb.Append("{\n");
context.Indent();  // Increase indentation for class members
sb.Append(context.GetIndent() + "public void MyMethod()\n");
sb.Append(context.GetIndent() + "{\n");
context.Indent();  // Increase indentation for method body
sb.Append(context.GetIndent() + "Console.WriteLine(\"Hello\");\n");
context.Dedent();  // Back to class-level indentation
sb.Append(context.GetIndent() + "}\n");
context.Dedent();  // Back to top-level indentation
sb.Append("}\n");
```

#### `Dedent()`
```csharp
public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);
```

**Purpose**: Decrease indentation level by 1

**Safety Feature**: Uses `Math.Max(0, ...)` to prevent negative indentation (defensive programming)

**When to use**: After closing a block (end of class, function, if statement, etc.)

#### `GetIndent()`
```csharp
public string GetIndent() => new string(' ', _indentLevel * IndentSize);
```

**Purpose**: Generate the actual indentation string (spaces) for the current level

**How it works**:
- `_indentLevel`: Current nesting depth (0, 1, 2, 3, ...)
- `IndentSize`: Constant = 4 (matches C# convention)
- Result: String with `_indentLevel * 4` spaces

**Examples**:
- `_indentLevel = 0` → `""` (no spaces)
- `_indentLevel = 1` → `"    "` (4 spaces)
- `_indentLevel = 2` → `"        "` (8 spaces)
- `_indentLevel = 3` → `"            "` (12 spaces)

**Note**: The Sharpy compiler currently uses **Roslyn** for code generation, which has built-in formatting via `.NormalizeWhitespace()`. These indentation methods are **legacy/utility methods** that may not be actively used in the current Roslyn-based implementation but remain available for manual code generation scenarios.

---

### Symbol Lookup

#### `LookupSymbol(string name)`
```csharp
public Symbol? LookupSymbol(string name)
{
    return SymbolTable.Lookup(name);
}
```

**Purpose**: Look up a symbol (variable, function, class) by name

**Returns**: `Symbol?` (nullable) - the symbol if found, or `null` if not found

**Delegates to**: `SymbolTable.Lookup()`, which searches scopes from innermost to outermost

**Typical Usage**:
```csharp
// When generating code for a variable reference
var symbol = context.LookupSymbol("myVariable");
if (symbol is VariableSymbol varSymbol)
{
    // Generate code using the variable's type information
    var csType = typeMapper.MapType(varSymbol.Type);
    // ...
}
```

**Symbol Types** you might get back:
- `VariableSymbol` - for variables and function parameters
- `FunctionSymbol` - for functions and methods
- `TypeSymbol` - for classes, structs, interfaces, enums

---

### Builtin Checks

#### `IsBuiltinFunction(string name)`
```csharp
public bool IsBuiltinFunction(string name)
{
    return Builtins.GetFunction(name) != null;
}
```

**Purpose**: Check if a function name refers to a Sharpy builtin function

**Returns**: `true` if builtin (e.g., `print`, `len`, `range`), `false` otherwise

**Typical Usage**:
```csharp
// When generating a function call
if (context.IsBuiltinFunction(functionName))
{
    // Generate call to Sharpy.Core builtin
    // e.g., Print.print(...) or Range.range(...)
}
else
{
    // Generate call to user-defined function
    // e.g., myFunction(...)
}
```

**Examples of builtin functions**:
- I/O: `print`, `input`
- Collections: `len`, `range`, `enumerate`, `zip`
- Type conversion: `int`, `str`, `bool`, `float`
- Iteration: `iter`, `next`, `sorted`, `reversed`
- Math: `sum`, `min`, `max`, `abs`, `round`

#### `IsBuiltinType(string name)`
```csharp
public bool IsBuiltinType(string name)
{
    return Builtins.GetType(name) != null;
}
```

**Purpose**: Check if a type name refers to a Sharpy builtin type

**Returns**: `true` if builtin (e.g., `int`, `str`, `list`), `false` otherwise

**Typical Usage**:
```csharp
// When generating type annotations
if (context.IsBuiltinType(typeName))
{
    // Map to C# builtin or Sharpy.Core type
    // e.g., "int" → int, "str" → string, "list[int]" → List<int>
}
else
{
    // Use user-defined type name
    // e.g., "MyClass" → MyClass
}
```

**Examples of builtin types**:
- Primitives: `int`, `long`, `float`, `double`, `bool`, `str`
- Collections: `list`, `dict`, `set`
- Special: `None` (void), `object`

---

## Dependencies

### Direct Dependencies

1. **`Sharpy.Compiler.Semantic.SymbolTable`**
   - Manages scopes and symbols during semantic analysis
   - Provides `Lookup(name)` for symbol resolution
   - Built during semantic analysis phase, consumed during code generation

2. **`Sharpy.Compiler.Semantic.BuiltinRegistry`**
   - Registry of Sharpy builtin functions and types
   - Populated via reflection from `Sharpy.Core` assembly
   - Used to distinguish builtins from user-defined symbols

3. **`Sharpy.Compiler.Semantic.Symbol`** (indirect via SymbolTable)
   - Base class for all symbols
   - Subtypes: `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`

### Usage by Other Components

**`RoslynEmitter`** (main code generator):
```csharp
public class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    
    public RoslynEmitter(CodeGenContext context)
    {
        _context = context;
        _typeMapper = new TypeMapper(context);
    }
    // Uses _context throughout code generation
}
```

**`TypeMapper`** (type mapping from Sharpy to C#):
```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    
    public TypeMapper(CodeGenContext context)
    {
        _context = context;
    }
    // Uses _context.Builtins to map types
}
```

**`AssemblyCompiler`** (multi-file compilation):
- Sets `ProjectNamespace` and `ProjectRootPath`
- Creates one context per compilation unit
- Shares SymbolTable and BuiltinRegistry across files

---

## Patterns and Design Decisions

### 1. **Context Object Pattern**

`CodeGenContext` is a classic **Context Object** pattern implementation:
- Encapsulates state needed across multiple operations (code generation)
- Passed as parameter to avoid global state
- Allows different contexts for different compilation units (thread-safe)

**Benefits**:
- No global variables
- Easy to test (inject mock contexts)
- Supports parallel compilation (each file gets its own context)

### 2. **Dependency Injection**

Constructor receives dependencies (SymbolTable, BuiltinRegistry) rather than creating them:
```csharp
// Good: Dependency Injection
var context = new CodeGenContext(symbolTable, builtins);

// Bad: Tight coupling (not used)
// var context = new CodeGenContext();
// context.InitializeSymbolTable();  // Creates own symbol table
```

**Benefits**:
- Single source of truth for symbols
- Easier to test (inject test data)
- Loose coupling between phases

### 3. **Immutable Core Properties, Mutable Project Properties**

**Immutable** (read-only, set via constructor):
- `SymbolTable`
- `Builtins`

**Mutable** (settable):
- `SourceFilePath`
- `ProjectNamespace`
- `ProjectRootPath`

**Rationale**: Core dependencies don't change during code generation, but project metadata may be set after construction by the AssemblyCompiler.

### 4. **Defensive Programming**

```csharp
public void Dedent() => _indentLevel = System.Math.Max(0, _indentLevel - 1);
```

Prevents `_indentLevel` from going negative even if `Dedent()` is called too many times. This prevents crashes from mismatched Indent/Dedent calls.

### 5. **Facade Pattern for SymbolTable and Builtins**

`CodeGenContext` provides simple wrapper methods:
- `LookupSymbol()` → `SymbolTable.Lookup()`
- `IsBuiltinFunction()` → `Builtins.GetFunction() != null`
- `IsBuiltinType()` → `Builtins.GetType() != null`

**Benefits**:
- Simpler API for code generators
- Can add logging/instrumentation in one place
- Hides implementation details of SymbolTable and BuiltinRegistry

---

## Debugging Tips

### 1. **Inspecting Symbol Table State**

When debugging code generation issues, inspect the symbol table:
```csharp
// In debugger or with logging
var symbol = context.LookupSymbol("myVariable");
if (symbol == null)
{
    Console.WriteLine("ERROR: Symbol 'myVariable' not found in symbol table!");
    // Check if semantic analysis failed or symbol name is different
}
else
{
    Console.WriteLine($"Found symbol: {symbol.Name}, Kind: {symbol.Kind}, Type: {symbol.Type}");
}
```

### 2. **Tracking Indentation Issues**

If generated C# code has incorrect indentation:
```csharp
// Add logging to Indent/Dedent
public void Indent()
{
    _indentLevel++;
    Console.WriteLine($"[DEBUG] Indent: level now {_indentLevel}");
}

public void Dedent()
{
    var oldLevel = _indentLevel;
    _indentLevel = Math.Max(0, _indentLevel - 1);
    Console.WriteLine($"[DEBUG] Dedent: level {oldLevel} → {_indentLevel}");
}
```

Look for:
- Mismatched Indent/Dedent calls
- Missing Dedent calls (indentation keeps increasing)
- Extra Dedent calls (triggers Math.Max safety net)

### 3. **Project Path Issues**

If namespaces are incorrect in multi-file projects:
```csharp
Console.WriteLine($"ProjectNamespace: {context.ProjectNamespace}");
Console.WriteLine($"ProjectRootPath: {context.ProjectRootPath}");
Console.WriteLine($"SourceFilePath: {context.SourceFilePath}");

// Verify these are set correctly by AssemblyCompiler
```

### 4. **Builtin Detection Issues**

If builtin functions/types aren't recognized:
```csharp
var isBuiltin = context.IsBuiltinFunction("print");
Console.WriteLine($"Is 'print' a builtin? {isBuiltin}");

// If false, check BuiltinRegistry initialization
var builtinFunc = context.Builtins.GetFunction("print");
Console.WriteLine($"Builtin 'print': {builtinFunc}");
```

### 5. **Common Pitfalls**

**Problem**: Symbol not found during code generation
- **Cause**: Semantic analysis didn't run or failed
- **Fix**: Ensure semantic analysis completes successfully before code generation

**Problem**: Builtin function not recognized
- **Cause**: `Sharpy.Core` assembly not loaded or BuiltinRegistry not initialized
- **Fix**: Check that BuiltinRegistry constructor ran and discovered Sharpy.Core types/functions

**Problem**: Wrong namespace in generated code
- **Cause**: `ProjectNamespace` or `ProjectRootPath` not set
- **Fix**: AssemblyCompiler should set these for .spyproj compilation

---

## Contribution Guidelines

### When to Modify This File

**Add new properties when**:
- Adding support for new project-level settings (e.g., target framework, optimization level)
- Tracking additional state needed during code generation (e.g., current class being generated)

**Add new methods when**:
- Adding new lookup patterns (e.g., `LookupMethod()`, `LookupField()`)
- Adding new checks (e.g., `IsGenericType()`, `IsNullableType()`)

**Don't modify when**:
- Adding new code generation logic → Use `RoslynEmitter` instead
- Adding new type mappings → Use `TypeMapper` instead
- Adding new builtin types/functions → Modify `BuiltinRegistry` instead

### Example Contributions

#### Adding a New Property for Target Framework

```csharp
/// <summary>
/// Target .NET framework version (e.g., "net9.0", "net10.0")
/// </summary>
public string? TargetFramework { get; set; }
```

**Usage**: AssemblyCompiler would set this from .spyproj, and RoslynEmitter could use it to conditionally generate code.

#### Adding a Method to Check if Type is Generic

```csharp
public bool IsGenericType(string typeName)
{
    var typeSymbol = Builtins.GetType(typeName);
    return typeSymbol?.IsGeneric ?? false;
}
```

**Usage**: Code generator could use this to decide how to map generic types to C#.

#### Adding Current Class Context

```csharp
/// <summary>
/// Stack of currently generating classes (for nested classes)
/// </summary>
public Stack<string> ClassStack { get; } = new Stack<string>();

public void EnterClass(string className) => ClassStack.Push(className);
public void ExitClass() => ClassStack.Pop();
public string? CurrentClass => ClassStack.Count > 0 ? ClassStack.Peek() : null;
```

**Usage**: Track nesting level and current class for name mangling and access control.

### Testing Guidelines

Since `CodeGenContext` is a simple state-holding class, testing is straightforward:

```csharp
[Fact]
public void TestIndentation()
{
    var context = new CodeGenContext(new SymbolTable(new BuiltinRegistry()), new BuiltinRegistry());
    
    Assert.Equal("", context.GetIndent());  // Level 0
    
    context.Indent();
    Assert.Equal("    ", context.GetIndent());  // Level 1 (4 spaces)
    
    context.Indent();
    Assert.Equal("        ", context.GetIndent());  // Level 2 (8 spaces)
    
    context.Dedent();
    Assert.Equal("    ", context.GetIndent());  // Back to level 1
    
    context.Dedent();
    Assert.Equal("", context.GetIndent());  // Back to level 0
    
    context.Dedent();  // Extra dedent
    Assert.Equal("", context.GetIndent());  // Still level 0 (safety)
}

[Fact]
public void TestSymbolLookup()
{
    var builtins = new BuiltinRegistry();
    var symbolTable = new SymbolTable(builtins);
    var context = new CodeGenContext(symbolTable, builtins);
    
    // Add a variable to symbol table
    symbolTable.EnterScope("test");
    symbolTable.Define(new VariableSymbol 
    { 
        Name = "myVar", 
        Type = SemanticType.Int 
    });
    
    // Lookup via context
    var symbol = context.LookupSymbol("myVar");
    Assert.NotNull(symbol);
    Assert.Equal("myVar", symbol.Name);
}

[Fact]
public void TestBuiltinDetection()
{
    var builtins = new BuiltinRegistry();
    var context = new CodeGenContext(new SymbolTable(builtins), builtins);
    
    Assert.True(context.IsBuiltinFunction("print"));
    Assert.True(context.IsBuiltinFunction("len"));
    Assert.False(context.IsBuiltinFunction("myCustomFunction"));
    
    Assert.True(context.IsBuiltinType("int"));
    Assert.True(context.IsBuiltinType("str"));
    Assert.False(context.IsBuiltinType("MyCustomClass"));
}
```

### Code Review Checklist

When reviewing changes to `CodeGenContext`:

- [ ] New properties have XML doc comments explaining their purpose
- [ ] New methods have XML doc comments with param/return descriptions
- [ ] Defensive programming maintained (e.g., Math.Max for Dedent)
- [ ] No breaking changes to public API (unless major version bump)
- [ ] Thread-safety considered (if adding mutable shared state)
- [ ] Tests added for new functionality
- [ ] Related documentation updated (this walkthrough, architecture docs)

---

## Related Files

### Direct Collaborators

- **`RoslynEmitter.cs`** - Main consumer of CodeGenContext
- **`TypeMapper.cs`** - Uses CodeGenContext for type mapping
- **`Sharpy.Compiler/Semantic/SymbolTable.cs`** - Provides symbol lookup
- **`Sharpy.Compiler/Semantic/BuiltinRegistry.cs`** - Provides builtin registry

### Related Documentation

- **Compiler Architecture**: `docs/architecture/compiler_architecture.md`
- **Code Generation Guide**: `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
- **Symbol Table Design**: `docs/architecture/symbol_table.md` (if exists)

### Test Files

- **`src/Sharpy.Compiler.Tests/CodeGen/CodeGenContextTests.cs`** (if exists)
- **`src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterTests.cs`** - Tests using CodeGenContext

---

## Quick Reference

### Creating a CodeGenContext

```csharp
// Typical setup
var builtins = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtins);
// ... run semantic analysis to populate symbol table ...
var context = new CodeGenContext(symbolTable, builtins);

// For multi-file projects
context.ProjectNamespace = "MyApp";
context.ProjectRootPath = "/path/to/myapp";
context.SourceFilePath = "/path/to/myapp/src/main.spy";
```

### Using Indentation

```csharp
sb.Append(context.GetIndent() + "public class MyClass\n");
sb.Append(context.GetIndent() + "{\n");
context.Indent();

sb.Append(context.GetIndent() + "public void MyMethod()\n");
sb.Append(context.GetIndent() + "{\n");
context.Indent();

sb.Append(context.GetIndent() + "Console.WriteLine(\"Hello\");\n");

context.Dedent();
sb.Append(context.GetIndent() + "}\n");

context.Dedent();
sb.Append(context.GetIndent() + "}\n");
```

### Checking Builtins

```csharp
if (context.IsBuiltinFunction("print"))
{
    // Generate: Print.print(...)
}

if (context.IsBuiltinType("list"))
{
    // Map to: List<T>
}
```

### Symbol Lookup

```csharp
var symbol = context.LookupSymbol(variableName);
if (symbol is VariableSymbol varSym)
{
    var type = varSym.Type;
    // Generate code with type information
}
```

---

## Conclusion

`CodeGenContext` is a **simple but essential** piece of the Sharpy compiler. It acts as the "glue" between semantic analysis and code generation, providing:

- **State management** (indentation)
- **Data access** (symbols, builtins)
- **Project metadata** (namespaces, paths)

While it's not a complex class, understanding how it fits into the compilation pipeline is crucial for working on the code generator. Think of it as the "toolbelt" that RoslynEmitter carries around—it doesn't do much on its own, but provides everything needed to transform Sharpy AST into C# code.

**Next Steps for Learning:**
1. Read `RoslynEmitter.cs` to see how CodeGenContext is used in practice
2. Read `SymbolTable.cs` to understand symbol resolution
3. Read `BuiltinRegistry.cs` to understand builtin discovery
4. Trace a simple Sharpy program through the entire compilation pipeline
