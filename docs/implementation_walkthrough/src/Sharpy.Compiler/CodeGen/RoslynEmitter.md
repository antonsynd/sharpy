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
- Using pre-computed `CodeGenInfo` from semantic analysis for module-level symbols
- Runtime tracking of local variable redeclarations during emission

## Important: Partial Class Architecture

`RoslynEmitter` is split across **multiple partial class files** for maintainability:

- **`RoslynEmitter.cs`** (this file): Core state, initialization, and name resolution logic
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

    // Local scope tracking (mutable during emission)
    private readonly HashSet<string> _declaredVariables;
    private readonly Dictionary<string, int> _variableVersions;
    private readonly HashSet<string> _constVariables;
    private readonly HashSet<string> _moduleFieldNames;

    // Type and interface tracking
    private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions;

    // Context flags
    private int _tempVarCounter = 0;
    private TypeAnnotation? _targetTypeContext;
    private bool _isInAbstractClass;
}
```

The class is intentionally **stateful** - it accumulates information during code generation to make context-aware decisions about naming and scoping.

## Architectural Shift: CodeGenInfo vs. Runtime Tracking

**Key Design Principle (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:14-20):**

The emitter uses a **hybrid approach** for name resolution:

1. **Module-level symbols** (variables, constants, functions, types, imports):
   - Use `Symbol.CodeGenInfo` which is pre-computed during semantic analysis
   - Provides: C# names, versioning, constant flags, import metadata
   - Read-only during emission (no mutation needed)

2. **Local variables**:
   - Use runtime tracking (`_declaredVariables`, `_variableVersions`)
   - Why? Local variable redeclarations happen during emission, not semantic analysis
   - Mutable state that evolves as we generate method bodies

This separation clarifies responsibility: **semantic analysis computes module-level metadata, emitter tracks local scope mutations**.

### Why This Matters

**Old approach (removed):** Many HashSets tracking module constants, variables, class names, etc.

**New approach:** CodeGenInfo consolidates that metadata in the symbol table, reducing duplication and making the emitter simpler.

**What remains in emitter:** Only truly runtime concerns like local variable redeclarations within function bodies.

## Key Dependencies

### 1. `CodeGenContext`

Maintains state during code generation (src/Sharpy.Compiler/CodeGen/CodeGenContext.cs:9):
- **Symbol table** for name resolution
- **Builtin registry** for recognizing built-in types/functions
- **Error collection** for reporting code generation issues
- **Project namespace information** for multi-file compilation
- **SemanticBinding** for accessing semantic data separate from AST nodes

Located at: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

### 2. `TypeMapper`

Handles all type conversions from Sharpy to C#:
- Maps Sharpy primitives (`int`, `str`, `bool`) to C# types
- Converts generic types (`list[int]` → `global::Sharpy.Core.List<int>`)
- Handles nullable types, tuples, and function types (`Func<>`, `Action<>`)

Located at: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

### 3. `NameMangler`

Static utility for name transformations (src/Sharpy.Compiler/CodeGen/NameMangler.cs:8):
- `ToPascalCase()`: Functions, methods, types
- `ToCamelCase()`: Variables, parameters
- `ToConstantCase()`: Constants (preserves CAPS_SNAKE_CASE)
- Handles C# keyword escaping (`@base`, `@object`)
- Maps dunder methods (`__str__` → `ToString`)

Located at: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

## Core State: Tracking Collections

The `RoslynEmitter` maintains a **minimal set** of tracking collections for local scope concerns. Most module-level tracking has been moved to `CodeGenInfo`.

### Local Scope Tracking (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:28-60)

```csharp
// Track which variables have been declared (for shadowing detection)
private readonly HashSet<string> _declaredVariables = new();

// Track variable version numbers for handling redeclarations
// E.g., x = 1; x = "hello" produces x then x_1
private readonly Dictionary<string, int> _variableVersions = new();

// Track local const variables (original Sharpy names)
private readonly HashSet<string> _constVariables = new();

// Track module-level field names (C# names) to prevent duplicate declarations
private readonly HashSet<string> _moduleFieldNames = new();
```

**Purpose:** Handle Sharpy's variable shadowing and redefinition semantics in C# where redeclaration is not allowed.

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

**Key Insight:** Local variable tracking happens at emission time because redeclarations occur as we generate the function body. We don't know ahead of time how many versions we'll need.

### Type Detection (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:62-65)

**Removed tracking sets:** `_classNames`, `_structNames`, `_stringEnumNames`

**New approach:**
- **Class/struct detection**: Use `SymbolTable.Lookup()` and check `TypeSymbol.TypeKind`
- **String enum detection**: Use `CodeGenInfo.IsStringEnum` flag

**Rationale:** Type information is already in the symbol table from semantic analysis. No need to duplicate it.

### Interface Tracking

```csharp
private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new();
```

**Purpose:** Track interface definitions for generating abstract class stubs. Used when an abstract class implements an interface with default methods.

### Context Flags (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:68-76)

```csharp
private int _tempVarCounter = 0;                   // Generates unique temp variable names
private TypeAnnotation? _targetTypeContext;        // For collection literal type inference
private bool _isInAbstractClass;                   // Tracks if in abstract class (for ellipsis handling)
```

- **`_targetTypeContext`**: Used for C# collection literal type inference (e.g., `List<int> items = [1, 2, 3]`)
- **`_isInAbstractClass`**: Tracks whether we're generating methods inside an abstract class (affects how `...` ellipsis bodies are interpreted - they become abstract methods)
- **`_tempVarCounter`**: Generates unique temporary variable names

### Static Data: Upper Case Acronyms (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:78-83)

```csharp
private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
{
    "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
    "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
};
```

**Purpose:** Recognize common .NET acronyms that should be fully uppercase in PascalCase (e.g., `HttpClient`, `ApiResponse`, `JsonData`).

**Usage:** Used during namespace and type name generation to ensure .NET naming conventions are followed.

## Constructor

```csharp
public RoslynEmitter(CodeGenContext context)
{
    _context = context;
    _typeMapper = new TypeMapper(context);
}
```

Simple initialization - the emitter delegates most of the complex state to `CodeGenContext`.

## CodeGenInfo Helper Methods (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:204-276)

These methods provide a **clean interface** for accessing `Symbol.CodeGenInfo` data. They encapsulate the logic for reading pre-computed semantic information.

### `GetCSharpNameForSymbol()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:215-234)

```csharp
private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
{
    if (symbol.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.GetVersionedCSharpName();
    }

    // Fallback for local variables during emission
    return symbol.Kind switch
    {
        SymbolKind.Variable => GetMangledVariableName(symbol.Name, isNewDeclaration),
        SymbolKind.Function => NameMangler.ToPascalCase(symbol.Name),
        SymbolKind.Type => NameMangler.ToPascalCase(symbol.Name),
        SymbolKind.Module => EscapeCSharpKeyword(symbol.Name.Replace(".", "_")),
        SymbolKind.Parameter => NameMangler.ToCamelCase(symbol.Name),
        _ => symbol.Name
    };
}
```

**Purpose:** Get the final C# name for any symbol, preferring CodeGenInfo when available.

**Fallback:** For local variables (which don't have CodeGenInfo), delegates to `GetMangledVariableName()`.

### `IsModuleLevelConstant()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:239-242)

```csharp
private bool IsModuleLevelConstant(Symbol symbol)
{
    return symbol.CodeGenInfo?.IsModuleLevel == true && symbol.CodeGenInfo.IsConstant;
}
```

**Purpose:** Check if a symbol is a module-level constant using pre-computed flags.

**Old approach:** Check if name is in `_moduleConstVariables` HashSet.

**New approach:** Read flag from CodeGenInfo.

### `IsModuleLevelVariable()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:247-250)

Similar to above, but checks for non-constant module-level variables.

### `HasExecutionOrderIssues()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:255-258)

```csharp
private bool HasExecutionOrderIssues(Symbol symbol)
{
    return symbol.CodeGenInfo?.HasExecutionOrderIssues == true;
}
```

**Purpose:** Identify variables that shouldn't become static fields due to initialization order dependencies.

**Example:** A module variable that references another module variable in its initializer.

### `IsFromImportSymbol()` / `GetOriginalImportName()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:263-275)

```csharp
private bool IsFromImportSymbol(Symbol symbol)
{
    return symbol.CodeGenInfo?.ImportKind == ImportKind.FromImport ||
           symbol.CodeGenInfo?.ImportKind == ImportKind.FromImportWithAlias;
}

private string? GetOriginalImportName(Symbol symbol)
{
    return symbol.CodeGenInfo?.OriginalImportName;
}
```

**Purpose:** Handle `from module import symbol as alias` statements.

**Example:**
```python
from config import MAX_RETRIES as max_retries
```

`CodeGenInfo` stores:
- `ImportKind = FromImportWithAlias`
- `OriginalImportName = "MAX_RETRIES"`

## SemanticBinding Helper Methods (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:277-316)

These methods read from `SemanticBinding` when available, falling back to direct AST properties for backward compatibility.

### `GetResolvedModulePath()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:287-294)

```csharp
private string? GetResolvedModulePath(FromImportStatement fromImport)
{
    if (_context.SemanticBinding != null)
    {
        return _context.SemanticBinding.GetResolvedModulePath(fromImport);
    }
    return fromImport.ResolvedModulePath;
}
```

**Purpose:** Get the resolved module path for a `from ... import` statement.

**Why SemanticBinding?** Avoids storing mutable semantic data directly in AST nodes, keeping AST immutable.

### `GetReExportedSymbols()` / `HasReExportedSymbols()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:299-315)

Similar pattern for accessing re-exported symbols (symbols imported from one module and re-exported from another).

## Key Method: `TryGetCSharpNameFromCodeGenInfo()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:95-120)

This method attempts to resolve a C# name using `CodeGenInfo` before falling back to runtime tracking.

```csharp
private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
{
    var symbol = _context.LookupSymbol(sharpyName);
    if (symbol?.CodeGenInfo == null)
        return null;

    var info = symbol.CodeGenInfo;

    // For new declarations, check if this is a local redeclaration
    // Local variable redeclarations still need runtime tracking via _variableVersions
    if (isNewDeclaration && !info.IsModuleLevel)
    {
        return null; // Let GetMangledVariableName handle local redeclarations
    }

    var csharpName = info.GetVersionedCSharpName();

    // Module imports need C# keyword escaping (e.g., "base" -> "@base")
    if (symbol is ModuleSymbol)
    {
        return EscapeCSharpKeyword(csharpName);
    }

    return csharpName;
}
```

**Key Logic:**
1. Lookup symbol in symbol table
2. If no CodeGenInfo, return null (fall back to runtime tracking)
3. If this is a local redeclaration, return null (CodeGenInfo doesn't track local versions)
4. Otherwise, use the pre-computed C# name from CodeGenInfo
5. Apply C# keyword escaping for module imports

**Why return null?** Signals to the caller that runtime tracking should handle this name.

## Key Method: `GetMangledVariableName()` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:128-202)

This is the **heart of name resolution** in RoslynEmitter. It's called whenever the emitter needs to generate a C# identifier from a Sharpy name.

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

#### 1. Local Variable Versioning (Highest Priority) (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:132-152)

```csharp
if (_variableVersions.ContainsKey(baseName))
{
    // There's a local variable with this name - use local resolution
    if (isNewDeclaration)
    {
        // This is a redefinition of an existing local variable
        var currentVersion = _variableVersions[baseName];
        var newVersion = currentVersion + 1;
        _variableVersions[baseName] = newVersion;
        return $"{baseName}_{newVersion}";
    }
    else
    {
        // This is a reference to the local variable
        var currentVersion = _variableVersions[baseName];
        return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
    }
}
```

**Why first?** Local variables shadow module-level names, so we check local scope before anything else. This matches Python's LEGB scoping.

**Key insight:** This handles Sharpy's Python-like variable redefinition semantics where you can reassign a variable with a different type.

**Example:**
```python
x = 10
x = "hello"  # Redeclaration with different type
```

Generated C#:
```csharp
var x = 10;
var x_1 = "hello";
```

#### 2. Local Constant Check (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:154-159)

```csharp
if (_constVariables.Contains(name))
{
    return NameMangler.ToConstantCase(name);
}
```

**Example:** `MAX_RETRIES` in local scope stays as `MAX_RETRIES`.

#### 3. Type Name Check (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:161-171)

```csharp
var symbol = _context.LookupSymbol(name);

// Uses symbol table lookup instead of legacy tracking sets
if (symbol is TypeSymbol typeSymbol &&
    (typeSymbol.TypeKind == TypeKind.Class ||
     typeSymbol.TypeKind == TypeKind.Struct))
{
    return NameMangler.ToPascalCase(name);
}
```

**New approach:** Query symbol table for type information instead of maintaining `_classNames` / `_structNames` sets.

**Example:** Class `user_profile` becomes `UserProfile`.

#### 4. Module Symbol Check (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:173-180)

```csharp
if (symbol is ModuleSymbol)
{
    // Use the same sanitization as in GenerateImportUsings
    // Also escape C# keywords like "base" -> "@base"
    return EscapeCSharpKeyword(name.Replace(".", "_"));
}
```

**Example:** `import math.utils` becomes `math_utils` or `@base` if keyword.

#### 5. CodeGenInfo-Based Resolution (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:182-187)

```csharp
var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
if (codeGenName != null)
    return codeGenName;
```

**Purpose:** Use pre-computed module-level symbol names from semantic analysis.

**Handles:**
- Module-level variables and constants
- From-import symbols (with aliases)
- Functions and types

**Why after local checks?** Parameters should shadow globals correctly (parameter `x` shadows global `x`).

#### 6. Default: New Local Variable (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:189-202)

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

#### Example 1: Parameter Shadowing Module Variable

**Sharpy:**
```python
global_var = "module"

def my_function(global_var):  # Parameter shadows module variable
    print(global_var)
```

**Generated C#:**
```csharp
public static string GlobalVar = "module";

public static void MyFunction(string globalVar)  // Different from GlobalVar
{
    Console.WriteLine(globalVar);
}
```

**Resolution path:**
1. `global_var` lookup in local scope → found in `_variableVersions` (added as parameter)
2. Returns `globalVar` (camelCase)

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

**Resolution path:**
1. First `x = 10`: New declaration → `_variableVersions[x] = 0` → returns `x`
2. Second `x = 20`: Redeclaration → increments version → `_variableVersions[x] = 1` → returns `x_1`
3. Third `x = x + 1`: Reference to `x` → returns `x_1`, then redeclaration → returns `x_2`

#### Example 3: From-Import with Alias (via CodeGenInfo)

**Sharpy:**
```python
from config import MAX_RETRIES as max_retries, get_settings
print(max_retries)  # Uses alias
get_settings()      # Uses original name
```

**Generated C#:**
```csharp
using static Config.Exports;

Console.WriteLine(MAX_RETRIES);  // Alias resolves to original via CodeGenInfo
GetSettings();                    // Direct use
```

**Resolution path:**
1. Lookup `max_retries` in symbol table
2. Find `CodeGenInfo` with `ImportKind = FromImportWithAlias`
3. `CodeGenInfo.OriginalImportName = "MAX_RETRIES"`
4. `CodeGenInfo.GetVersionedCSharpName()` returns `MAX_RETRIES`

## Patterns and Design Decisions

### 1. Hybrid Name Resolution: CodeGenInfo + Runtime Tracking

**Decision:** Use CodeGenInfo for module-level symbols, runtime tracking for local variables.

**Rationale:**
- **Module-level symbols**: Known at semantic analysis time, can pre-compute C# names and metadata
- **Local variables**: Redeclarations happen during emission (can't know version count ahead of time)
- Separation of concerns: semantic analysis computes static info, emitter handles dynamic scope

**Trade-off:** More complex than pure runtime tracking, but enables better semantic analysis and clearer boundaries.

### 2. Priority-Based Name Resolution

**Decision:** Check scopes in a specific order (local → types → modules → CodeGenInfo → default).

**Rationale:** Matches Python's LEGB (Local, Enclosing, Global, Built-in) scoping rules adapted for Sharpy's semantics.

**Implementation:** The `GetMangledVariableName()` method implements this as an explicit sequence of if-checks, making the priority order clear and debuggable.

### 3. Minimal Tracking Collections

**Decision:** Remove most tracking sets in favor of symbol table queries and CodeGenInfo.

**Rationale:**
- **Eliminates duplication**: Type information already exists in symbol table
- **Single source of truth**: CodeGenInfo is the authoritative metadata source
- **Simpler emitter**: Fewer HashSets to maintain and synchronize
- **Better encapsulation**: Semantic analysis owns the logic for computing C# names

**What remains:** Only truly runtime concerns (local variable versions, module field names to prevent duplicates).

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

### 6. Immutable AST with SemanticBinding

**Decision:** Store semantic data (import resolution, re-exported symbols) in `SemanticBinding` instead of AST nodes.

**Rationale:**
- **Immutable AST**: AST nodes remain pure parse-time structures
- **Clean separation**: Parsing vs. semantic analysis responsibilities
- **Reusable AST**: Same AST can be analyzed with different semantic contexts

**Implementation:** Helper methods like `GetResolvedModulePath()` abstract away the choice between SemanticBinding and AST properties.

## Debugging Tips

### Tip 1: Name Resolution Issues

**Symptom:** Variable has wrong casing or wrong version suffix.

**Debug approach:**
1. Set a breakpoint in `GetMangledVariableName()` at line 128
2. Check which branch the code takes (step through the priority checks)
3. Inspect the tracking collections:
   - `_variableVersions` - should contain local variables with their version numbers
   - `_constVariables` - should contain local constants
4. Check if symbol has `CodeGenInfo`:
   ```csharp
   var symbol = _context.LookupSymbol(name);
   var info = symbol?.CodeGenInfo;
   // Inspect info properties
   ```
5. Verify `isNewDeclaration` is set correctly at the call site
6. Check the `baseName` after `ToCamelCase()` transformation

**Common issue:** Local variable shadowing module variable not working - likely the local variable wasn't added to `_variableVersions`.

### Tip 2: CodeGenInfo Missing or Incorrect

**Symptom:** Module-level symbol not using CodeGenInfo, falling back to runtime resolution.

**Debug approach:**
1. Check if semantic analysis was run with `computeCodeGenInfo: true`
2. Verify the symbol exists in the symbol table: `_context.LookupSymbol(name)`
3. Check if `symbol.CodeGenInfo` is non-null
4. Inspect CodeGenInfo properties:
   - `IsModuleLevel` - should be true for module symbols
   - `IsConstant` - for constants
   - `ImportKind` - for from-imports
   - `OriginalImportName` - for aliased imports
5. Look at `CodeGenInfoComputer` in semantic analysis to see how it's populated

### Tip 3: Variable Shadowing Problems

**Symptom:** Local variable incorrectly references module-level variable or vice versa.

**Debug approach:**
1. Verify `isNewDeclaration` is set correctly at call sites (assignment vs. reference)
2. Check that `_variableVersions` is cleared between function scopes (should be in `GenerateFunctionDeclaration()`)
3. Ensure priority order in `GetMangledVariableName()` is being followed
4. Check if parameter was added to `_variableVersions` during parameter processing

### Tip 4: Import Resolution Issues

**Symptom:** From-import symbols have wrong names or aliases not working.

**Debug approach:**
1. Check `symbol.CodeGenInfo.ImportKind` - should be `FromImport` or `FromImportWithAlias`
2. Check `symbol.CodeGenInfo.OriginalImportName` for aliased imports
3. Verify `GetResolvedModulePath()` returns correct module path
4. Look at `RoslynEmitter.CompilationUnit.cs` → `GenerateUsingDirectives()` method
5. Check if SemanticBinding has correct import resolution data

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
- You need to track new runtime state during emission (not pre-computable)
- Example: Tracking async/await contexts for method generation

**Modify `GetMangledVariableName()` when:**
- Changing the priority order of name resolution
- Adding new special cases for naming (e.g., property names)
- Fixing bugs in variable shadowing or versioning

**Add CodeGenInfo helper methods when:**
- You need to access new CodeGenInfo fields from multiple places
- You want to encapsulate CodeGenInfo logic (keep it DRY)

**Don't modify this file when:**
- Adding new expression types (go to `RoslynEmitter.Expressions.cs`)
- Adding new statement types (go to `RoslynEmitter.Statements.cs`)
- Changing type mapping logic (go to `TypeMapper.cs`)
- Changing name mangling rules (go to `NameMangler.cs`)
- Adding new CodeGenInfo fields (go to `Sharpy.Compiler.Semantic.CodeGenInfo`)

### Testing Considerations

When modifying name resolution:
1. **Test variable shadowing** (module-level vs. local, parameter shadowing)
2. **Test variable redefinition** (versioning with multiple assignments)
3. **Test constant preservation** (CONSTANT_CASE vs. PascalCase)
4. **Test from-import aliasing** (alias → original name mapping)
5. **Test CodeGenInfo integration** (module-level symbols use pre-computed names)
6. **Test edge cases** (keywords, dunder methods, special characters)
7. **Test cross-references** (variable used before/after redefinition)

### Code Style

**Follow existing patterns:**
- Use descriptive collection names (`_moduleFieldNames` not `_modFields`)
- Add XML doc comments for new public/internal methods
- Keep `GetMangledVariableName()` priority order well-commented
- Use `readonly` for collections that are initialized once
- Initialize collections inline when possible (`new()` syntax)
- Prefer CodeGenInfo queries over new tracking collections

### Performance Considerations

- **HashSet lookups are O(1)**: Adding more tracking collections doesn't significantly impact performance
- **Avoid LINQ in hot paths**: `GetMangledVariableName()` is called for every identifier - keep it efficient
- **Symbol table lookups are fast**: Use `_context.LookupSymbol()` freely, it's optimized
- **CodeGenInfo is pre-computed**: No performance penalty for using it (already paid during semantic analysis)

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
- `Sharpy.Compiler.Semantic`: Semantic analysis (symbol tables, type checking, CodeGenInfo)

**Key semantic analysis integration:**
- `Symbol.CodeGenInfo`: Pre-computed code generation metadata (C# names, import info, constant flags)
- `CodeGenInfoComputer`: Populates CodeGenInfo during semantic analysis
- `SemanticBinding`: Stores semantic data separate from AST nodes

**Specifications:**
- `docs/language_specification/dotnet_interop.md`: .NET interop rules that affect code generation
