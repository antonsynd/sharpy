# Walkthrough: RoslynEmitter.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Overview

`RoslynEmitter.cs` is the main entry point for the **Code Generation** phase of the Sharpy compiler. It transforms a typed Abstract Syntax Tree (AST) produced by semantic analysis into valid C# code using Microsoft's Roslyn compiler API. This is the final phase in the Sharpy compilation pipeline.

**Pipeline Position:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# Code
```

**Key Responsibility:** Bridge the gap between Sharpy's Python-like syntax and C#/.NET semantics by:
- Converting snake_case identifiers to appropriate C# naming conventions (PascalCase, camelCase, CONSTANT_CASE)
- Managing variable shadowing and redefinition through name versioning
- Tracking module-level vs. local scope for proper name resolution
- Coordinating with `TypeMapper` for type conversions and `NameMangler` for identifier transformations

## Important: Partial Class Architecture

`RoslynEmitter` is split across **multiple partial class files** for maintainability:

- **`RoslynEmitter.cs`** (this file): Core state, initialization, and name mangling logic
- **`RoslynEmitter.CompilationUnit.cs`**: Top-level compilation unit, namespace, and import generation
- **`RoslynEmitter.Expressions.cs`**: Expression generation (literals, operators, calls, comprehensions)
- **`RoslynEmitter.Statements.cs`**: Statement generation (control flow, assignments, try/catch)
- **`RoslynEmitter.TypeDeclarations.cs`**: Type declarations (classes, structs, enums, interfaces)
- **`RoslynEmitter.ClassMembers.cs`**: Class member generation (methods, properties, fields)
- **`RoslynEmitter.ModuleClass.cs`**: Module-level code wrapping and exports
- **`RoslynEmitter.Operators.cs`**: Operator expression generation

**When reading this file, remember:** You're only seeing the "control center" - the actual code generation logic is distributed across these companion files.

## Class Structure

### Main Class: `RoslynEmitter` (partial)

```csharp
public partial class RoslynEmitter
{
    // Core dependencies
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;

    // Name tracking and versioning
    private readonly HashSet<string> _declaredVariables;
    private readonly Dictionary<string, int> _variableVersions;
    private readonly HashSet<string> _constVariables;
    private readonly HashSet<string> _moduleConstVariables;
    private readonly HashSet<string> _moduleVariables;
    private readonly HashSet<string> _moduleFieldNames;
    private HashSet<string> _variablesWithExecutionOrderIssues;
    private readonly HashSet<string> _classNames;
    private readonly HashSet<string> _structNames;
    private readonly HashSet<string> _stringEnumNames;
    private readonly HashSet<string> _fromImportSymbols;
    private readonly Dictionary<string, string> _importAliasToOriginal;
    private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions;

    // Context flags
    private int _tempVarCounter = 0;
    private TypeAnnotation? _targetTypeContext;
    private bool _isInAbstractClass;
}
```

The class is intentionally **stateful** - it accumulates information during code generation to make context-aware decisions about naming and scoping.

## Key Dependencies

### 1. `CodeGenContext`
Maintains state during code generation:
- **Symbol table** for name resolution
- **Builtin registry** for recognizing built-in types/functions
- **Error collection** for reporting code generation issues
- **Project namespace information** for multi-file compilation

Located at: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs` (src/Sharpy.Compiler/CodeGen/CodeGenContext.cs:9)

### 2. `TypeMapper`
Handles all type conversions from Sharpy to C#:
- Maps Sharpy primitives (`int`, `str`, `bool`) to C# types
- Converts generic types (`list[int]` → `global::Sharpy.Core.List<int>`)
- Handles nullable types, tuples, and function types (`Func<>`, `Action<>`)

Located at: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` (src/Sharpy.Compiler/CodeGen/TypeMapper.cs:12)

### 3. `NameMangler`
Static utility for name transformations:
- `ToPascalCase()`: Functions, methods, types
- `ToCamelCase()`: Variables, parameters
- `ToConstantCase()`: Constants (preserves CAPS_SNAKE_CASE)
- Handles C# keyword escaping (`@base`, `@object`)
- Maps dunder methods (`__str__` → `ToString`)

Located at: `src/Sharpy.Compiler/CodeGen/NameMangler.cs` (src/Sharpy.Compiler/CodeGen/NameMangler.cs:8)

## Core State: Tracking Collections

The `RoslynEmitter` maintains numerous HashSets and Dictionaries to track names across different scopes. Understanding these is crucial for debugging name resolution issues.

### Variable Tracking

```csharp
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
```

**Purpose:** Handle Sharpy's variable shadowing semantics in C# where redeclaration is not allowed.

**Example:**
```python
# Sharpy code
x = 10
x = 20  # Redeclaration allowed in Sharpy
```

Generated C#:
```csharp
var x = 10;
var x_1 = 20;  // Versioned to avoid conflict
```

### Constant Tracking

```csharp
private readonly HashSet<string> _constVariables = new();           // Local constants
private readonly HashSet<string> _moduleConstVariables = new();     // Module-level constants
```

**Purpose:** Distinguish constants from variables to preserve `CONSTANT_CASE` naming.

**Example:**
```python
# Sharpy module-level
MAX_CONNECTIONS = 100

def configure():
    timeout = 30  # variable → camelCase
    return MAX_CONNECTIONS  # constant → CONSTANT_CASE
```

### Module-Level Tracking

```csharp
private readonly HashSet<string> _moduleVariables = new();
private readonly HashSet<string> _moduleFieldNames = new();
private HashSet<string> _variablesWithExecutionOrderIssues = new();
```

**Purpose:** Track module-level variables for PascalCase conversion (they become static fields in the generated module class).

**Why it matters:** Module-level variables in Sharpy are accessible like attributes in Python, but in C# they need to be public static fields with PascalCase names.

The `_variablesWithExecutionOrderIssues` tracks variables that should not become fields due to initialization order problems.

### Type Name Tracking

```csharp
private readonly HashSet<string> _classNames = new();
private readonly HashSet<string> _structNames = new();
private readonly HashSet<string> _stringEnumNames = new();
private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new();
```

**Purpose:** Remember which identifiers are type names vs. variables to apply correct casing rules. The `_interfaceDefinitions` dictionary is used for generating abstract class stubs from interfaces.

### Import Symbol Tracking

```csharp
private readonly HashSet<string> _fromImportSymbols = new();
private readonly Dictionary<string, string> _importAliasToOriginal = new();
```

**Purpose:** Handle `from module import symbol` statements where imported symbols need exact casing preservation.

**Example:**
```python
from config import MAX_RETRIES, get_timeout as timeout_fn
```

The emitter needs to know that `MAX_RETRIES` should stay in CONSTANT_CASE and `timeout_fn` is an alias for `get_timeout`.

### Context Flags

```csharp
private TypeAnnotation? _targetTypeContext;
private bool _isInAbstractClass;
private int _tempVarCounter = 0;
```

- **`_targetTypeContext`**: Used for C# collection literal type inference (e.g., `List<int> items = [1, 2, 3]`)
- **`_isInAbstractClass`**: Tracks whether we're generating methods inside an abstract class (affects how `...` ellipsis bodies are interpreted - they become abstract methods)
- **`_tempVarCounter`**: Generates unique temporary variable names

## Key Method: `GetMangledVariableName()`

This is the **heart of name resolution** in RoslynEmitter. It's called whenever the emitter needs to generate a C# identifier from a Sharpy name.

Located at: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:60`

### Method Signature

```csharp
private string GetMangledVariableName(string name, bool isNewDeclaration)
```

**Parameters:**
- `name`: The original Sharpy identifier (e.g., `my_variable`, `MAX_VALUE`, `MyClass`)
- `isNewDeclaration`: `true` if this is a variable declaration/redefinition, `false` if it's a reference

**Returns:** The C# identifier with appropriate casing and versioning (e.g., `myVariable`, `myVariable_1`, `MAX_VALUE`, `MyClass`)

### Resolution Strategy (Priority Order)

The method implements a **priority-based resolution strategy**. It checks conditions in this exact order:

#### 1. Local Variable Versioning (Highest Priority)

```csharp
if (_variableVersions.ContainsKey(baseName))
{
    if (isNewDeclaration)
    {
        var currentVersion = _variableVersions[baseName];
        var newVersion = currentVersion + 1;
        _variableVersions[baseName] = newVersion;
        return $"{baseName}_{newVersion}";
    }
    else
    {
        var currentVersion = _variableVersions[baseName];
        return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
    }
}
```

**Why first?** Local variables shadow module-level names, so we check local scope before anything else.

**Key insight:** This handles Sharpy's Python-like variable redefinition semantics where you can reassign a variable with a different type.

#### 2. Local Constant Check

```csharp
if (_constVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}
```

**Example:** `MAX_RETRIES` in local scope stays as `MAX_RETRIES`.

#### 3. Module-Level Constant Check

```csharp
if (_moduleConstVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}
```

**Example:** Module-level `API_KEY` stays as `API_KEY`.

#### 4. Module-Level Variable Check

```csharp
if (_moduleVariables.Contains(name))
{
    return NameMangler.ToPascalCase(name);
}
```

**Example:** Module-level `connection_pool` becomes `ConnectionPool` (static field).

#### 5. Type Name Check

```csharp
if (_classNames.Contains(name) || _structNames.Contains(name))
{
    return NameMangler.ToPascalCase(name);
}
```

**Example:** Class `user_profile` becomes `UserProfile`.

#### 6. From-Import Symbol Resolution

```csharp
if (_fromImportSymbols.Contains(name))
{
    // If this is an alias, use the original name for code generation
    var actualName = _importAliasToOriginal.TryGetValue(name, out var originalName)
        ? originalName
        : name;

    // Use the same casing rules as exported module members
    if (IsConstantCaseName(actualName))
        return NameMangler.ToConstantCase(actualName);
    else
        return NameMangler.ToPascalCase(actualName);
}
```

**Complex case:** Handles aliased imports and preserves the exported casing convention.

**Example:**
```python
from config import MAX_RETRIES as max_retries
print(max_retries)  # References MAX_RETRIES
```

The `max_retries` alias maps back to `MAX_RETRIES` (constant case).

#### 7. Module Symbol (Import) Check

```csharp
var symbol = _context.LookupSymbol(name);
if (symbol is ModuleSymbol)
{
    // Use the same sanitization as in GenerateImportUsings
    // Also escape C# keywords like "base" -> "@base"
    return EscapeCSharpKeyword(name.Replace(".", "_"));
}
```

**Example:** `import math_utils` stays as `math_utils` (becomes a `using` alias).

**Note:** Module names preserve their exact casing with dots replaced by underscores and C# keyword escaping applied.

#### 8. Default: New Local Variable

```csharp
if (isNewDeclaration)
{
    // First declaration of this local variable
    _variableVersions[baseName] = 0;
    return baseName;
}
else
{
    // Reference to a variable not yet declared (shouldn't happen in valid code)
    // Fall back to just returning the base name
    return baseName;
}
```

**Fallback case:** For variables that don't match any of the above categories, treat them as new local variables with camelCase naming.

### Common Patterns and Edge Cases

#### Example 1: Variable Shadowing

**Sharpy:**
```python
global_var = "module"

def my_function():
    global_var = "local"  # Shadows module-level variable
    print(global_var)
```

**Generated C#:**
```csharp
public static string GlobalVar = "module";

public static void MyFunction()
{
    var globalVar = "local";  // Different from GlobalVar
    Console.WriteLine(globalVar);
}
```

Notice: `global_var` at module level → `GlobalVar` (PascalCase), but `global_var` in local scope → `globalVar` (camelCase).

#### Example 2: Variable Redefinition with Versioning

**Sharpy:**
```python
x = 10
x = 20
x = x + 1
```

**Generated C#:**
```csharp
var x = 10;
var x_1 = 20;
var x_2 = x_1 + 1;  // References the current version
```

**Key point:** Each redefinition increments the version, and references use the current version.

#### Example 3: From-Import Aliasing

**Sharpy:**
```python
from config import MAX_RETRIES as max_retries, get_settings
print(max_retries)  # Uses alias
get_settings()      # Uses original name
```

**Generated C#:**
```csharp
using static Config.Exports;

Console.WriteLine(MAX_RETRIES);  // Alias resolves to original
GetSettings();                    // Direct use
```

The alias `max_retries` is tracked in `_importAliasToOriginal`, but the actual C# code uses `MAX_RETRIES` (the original exported name).

## Static Data: `UpperCaseAcronyms`

```csharp
private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
{
    "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
    "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
};
```

**Purpose:** Recognize common .NET acronyms that should be fully uppercase in PascalCase (e.g., `HttpClient`, `ApiResponse`, `JsonData`).

**Usage:** Used during namespace and type name generation to ensure .NET naming conventions are followed.

## Patterns and Design Decisions

### 1. Stateful Code Generation

**Decision:** RoslynEmitter accumulates state across the entire module compilation.

**Rationale:**
- Need to track variable versions across the entire function scope
- Need to remember module-level declarations to resolve references correctly
- Simplifies the API for partial class methods (they share state)

**Trade-off:** Not thread-safe, but compiler phases are sequential anyway.

### 2. Priority-Based Name Resolution

**Decision:** Check scopes in a specific order (local → module → imports → types).

**Rationale:** Matches Python's LEGB (Local, Enclosing, Global, Built-in) scoping rules adapted for Sharpy's semantics.

**Implementation:** The `GetMangledVariableName()` method implements this as an explicit sequence of if-checks, making the priority order clear and debuggable.

### 3. Explicit Tracking Collections

**Decision:** Use many small HashSets/Dictionaries instead of a unified symbol resolution system.

**Rationale:**
- Simple and explicit (easy to debug)
- Each collection has a single, clear purpose
- Avoids complex hierarchical scope structures
- Performance is excellent (O(1) lookups)

**Trade-off:** More bookkeeping code, but fewer bugs from scope edge cases.

### 4. Separation via Partial Classes

**Decision:** Split `RoslynEmitter` into 7+ partial class files.

**Rationale:**
- Original single file was too large (thousands of lines)
- Each partial file has a cohesive responsibility (expressions, statements, types, etc.)
- Easier to navigate and maintain
- Enables parallel development on different aspects

**Trade-off:** Must coordinate state across files, but the shared fields make this manageable.

### 5. Version Suffix Strategy

**Decision:** Use numeric suffixes for variable versioning (`x`, `x_1`, `x_2`).

**Rationale:**
- Simple and predictable
- Easy to debug (version number in the name)
- Doesn't conflict with user identifiers (assuming they don't use numeric suffixes)
- Preserves semantic information about redefinition

**Alternative considered:** Mangled names like `x_v1`, `x_shadow1` - rejected for being too verbose.

## Debugging Tips

### Tip 1: Name Resolution Issues

**Symptom:** Variable has wrong casing or wrong version suffix.

**Debug approach:**
1. Set a breakpoint in `GetMangledVariableName()` at line 60
2. Check which branch the code takes (step through the priority checks)
3. Inspect the tracking collections:
   - `_variableVersions` - should contain local variables with their version numbers
   - `_moduleVariables` - should contain module-level variable names
   - `_constVariables` / `_moduleConstVariables` - should contain constants
4. Verify `isNewDeclaration` is set correctly at the call site
5. Check the `baseName` after `ToCamelCase()` transformation

**Common issue:** Local variable shadowing module variable not working - likely the local variable wasn't added to `_variableVersions`.

### Tip 2: Missing Imports

**Symptom:** Generated C# references undefined identifiers from imported modules.

**Debug approach:**
1. Check `_fromImportSymbols` - was the symbol tracked during import processing?
2. Check `_importAliasToOriginal` - is the alias mapping correct?
3. Look at `RoslynEmitter.CompilationUnit.cs` → `GenerateUsingDirectives()` method
4. Verify the symbol table contains the imported module/symbol

### Tip 3: Variable Shadowing Problems

**Symptom:** Local variable incorrectly references module-level variable or vice versa.

**Debug approach:**
1. Verify `isNewDeclaration` is set correctly at call sites (assignment vs. reference)
2. Check that `_variableVersions` is cleared between function scopes (should be in `GenerateFunctionDeclaration()`)
3. Ensure module-level variables are added to `_moduleVariables` during the initial module processing pass
4. Verify the priority order in `GetMangledVariableName()` is being followed

### Tip 4: Constant Casing

**Symptom:** Constants are being converted to camelCase or PascalCase instead of preserving CONSTANT_CASE.

**Debug approach:**
1. Check if the constant was added to `_constVariables` or `_moduleConstVariables`
2. Verify `IsConstantCaseName()` helper function is working correctly (detecting ALL_CAPS names)
3. Ensure constants are recognized during the semantic analysis phase
4. Check that `from module import CONSTANT` populates `_fromImportSymbols` correctly

### Tip 5: Inspect Generated Roslyn Syntax Trees

**Useful technique:**
```csharp
// In any Generate* method
var generatedSyntax = /* ... your generation code ... */;
Console.WriteLine(generatedSyntax.NormalizeWhitespace().ToFullString());
```

The `ToFullString()` method shows the exact C# code that will be emitted, with proper formatting.

### Tip 6: Version Number Mismatches

**Symptom:** Variable reference uses wrong version (e.g., `x` instead of `x_1`).

**Debug approach:**
1. Check that `isNewDeclaration = false` for references
2. Verify `_variableVersions[baseName]` contains the expected version number
3. Ensure the assignment that created the version came before the reference
4. Check that the version counter isn't being reset unexpectedly

## Contribution Guidelines

### When to Modify This File

**Add new tracking collections when:**
- You introduce a new category of symbols that needs special name resolution
- You need to track new semantic information across the compilation unit
- Example: Adding support for async/await might require tracking `async` method contexts

**Add new name resolution rules when:**
- You're implementing new language features that affect scoping
- You need to handle new import patterns
- Example: Supporting `from module import *` might need wildcard import tracking

**Modify `GetMangledVariableName()` when:**
- Changing the priority order of name resolution
- Adding new special cases for naming (e.g., property names)
- Fixing bugs in variable shadowing or versioning

**Don't modify this file when:**
- Adding new expression types (go to `RoslynEmitter.Expressions.cs`)
- Adding new statement types (go to `RoslynEmitter.Statements.cs`)
- Changing type mapping logic (go to `TypeMapper.cs`)
- Changing name mangling rules (go to `NameMangler.cs`)

### Testing Considerations

When modifying name resolution:
1. **Test variable shadowing** (module-level vs. local)
2. **Test variable redefinition** (versioning with multiple assignments)
3. **Test constant preservation** (CONSTANT_CASE vs. PascalCase)
4. **Test from-import aliasing** (alias → original name mapping)
5. **Test edge cases** (keywords, dunder methods, special characters)
6. **Test cross-references** (variable used before/after redefinition)

### Code Style

**Follow existing patterns:**
- Use descriptive collection names (`_moduleConstVariables` not `_modConsts`)
- Add XML doc comments for new public/internal methods
- Keep `GetMangledVariableName()` priority order well-commented
- Use `readonly` for collections that are initialized once
- Initialize collections inline when possible (`new()` syntax)

### Performance Considerations

- **HashSet lookups are O(1)**: Adding more tracking collections doesn't significantly impact performance
- **Avoid LINQ in hot paths**: `GetMangledVariableName()` is called for every identifier - keep it efficient
- **Pre-populate collections**: Do module-level scans before generating code to avoid repeated symbol lookups
- **Use `StringComparer.OrdinalIgnoreCase` judiciously**: Only when case-insensitive comparisons are needed

## Cross-References

This file is part of a larger partial class. For complete understanding, see:

- **[RoslynEmitter.CompilationUnit.md](./RoslynEmitter.CompilationUnit.md)**: Top-level module structure and imports
- **[RoslynEmitter.Expressions.md](./RoslynEmitter.Expressions.md)**: How expressions are converted to Roslyn syntax
- **[RoslynEmitter.Statements.md](./RoslynEmitter.Statements.md)**: Control flow and statement generation
- **[RoslynEmitter.TypeDeclarations.md](./RoslynEmitter.TypeDeclarations.md)**: Class, struct, enum, interface generation
- **[RoslynEmitter.ClassMembers.md](./RoslynEmitter.ClassMembers.md)**: Method and property generation
- **[RoslynEmitter.ModuleClass.md](./RoslynEmitter.ModuleClass.md)**: Module wrapping and exports
- **[RoslynEmitter.Operators.md](./RoslynEmitter.Operators.md)**: Operator expression handling

**Related core files:**
- **[CodeGenContext.md](./CodeGenContext.md)**: State container for code generation
- **[TypeMapper.md](./TypeMapper.md)**: Type conversion Sharpy → C#
- **[NameMangler.md](./NameMangler.md)**: Identifier transformation utilities

**Upstream dependencies:**
- `Sharpy.Compiler.Parser.Ast`: AST node definitions
- `Sharpy.Compiler.Semantic`: Semantic analysis (symbol tables, type checking)

**Specifications:**
- `docs/language_specification/dotnet_interop.md`: .NET interop rules that affect code generation
