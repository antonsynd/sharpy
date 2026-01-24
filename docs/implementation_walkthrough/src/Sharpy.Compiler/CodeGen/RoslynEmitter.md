# Walkthrough: RoslynEmitter.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Overview

`RoslynEmitter.cs` is the **core infrastructure file** for the code generation phase of the Sharpy compiler. This file defines the `RoslynEmitter` partial class, which orchestrates the transformation of validated Sharpy AST nodes into C# Roslyn syntax trees.

**Role in Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# Code → .NET IL
                                                                  ↑
                                                          You are here
```

This file specifically handles:
- **Class initialization** and dependency injection (CodeGenContext, TypeMapper)
- **Name resolution** for variables, functions, types, and modules
- **State tracking** for local variable redeclarations and module-level symbols
- **Helper methods** for accessing semantic metadata (CodeGenInfo, SemanticBinding)

**What this file does NOT do:**
The actual AST-to-C# transformations are split across other partial class files:
- `RoslynEmitter.CompilationUnit.cs` - Top-level module structure
- `RoslynEmitter.Statements.cs` - Statement emission (if, while, for, etc.)
- `RoslynEmitter.Expressions.cs` - Expression emission (literals, operators, calls)
- `RoslynEmitter.TypeDeclarations.cs` - Type definitions (class, struct, enum, interface)
- `RoslynEmitter.ClassMembers.cs` - Class member generation (methods, fields, properties)
- `RoslynEmitter.ModuleClass.cs` - Module-level code organization
- `RoslynEmitter.Operators.cs` - Operator overloading

---

## Architecture: The Partial Class Strategy

`RoslynEmitter` uses C#'s **partial class** feature to split a large emitter (~8000+ lines) into focused, maintainable files. Think of this main file as the "spine" that holds:
- Constructor and dependencies
- Shared state (fields)
- Core naming resolution logic
- Helper methods used across all partial files

```csharp
public partial class RoslynEmitter
{
    // Dependencies (injected)
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;

    // Mutable state (tracked during emission)
    private readonly HashSet<string> _declaredVariables;
    private readonly Dictionary<string, int> _variableVersions;
    // ... more state fields
}
```

---

## Class Structure

### Dependencies

#### `CodeGenContext _context`
The central state container for code generation. Provides:
- **SymbolTable**: Symbol lookup for module-level and imported symbols
- **BuiltinRegistry**: Builtin functions and types
- **SemanticBinding**: Semantic metadata (import resolutions, etc.)
- **Logger**: Diagnostic output
- **Project configuration**: Namespace, root path, entry point status

See `CodeGenContext.cs` (src/Sharpy.Compiler/CodeGen/CodeGenContext.cs:9) for implementation details.

#### `TypeMapper _typeMapper`
Handles all Sharpy-to-C# type mappings:
- Primitives: `int` → `int`, `str` → `string`, `float` → `double`
- Collections: `list` → `global::Sharpy.Core.List`, `dict` → `global::Sharpy.Core.Dict`
- Generics: `list[int]` → `global::Sharpy.Core.List<int>`
- Function types: `(int, str) -> bool` → `System.Func<int, string, bool>`
- User-defined types: `UserProfile` → `UserProfile` (with namespace qualification)

See `TypeMapper.cs` for implementation details.

---

### State Tracking Fields

The emitter maintains **mutable state** during code generation. Understanding these fields is crucial for debugging emission issues.

#### Local Variable Tracking (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:26-49)

```csharp
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
```

**Why track locally?** Local variable redeclarations happen **during emission**, not during semantic analysis. Sharpy allows:

```python
x: int = 5      # → var x = 5;
x: str = "hi"   # → var x_1 = "hi";
x: bool = True  # → var x_2 = true;
```

The emitter assigns version suffixes incrementally:
- First `x` → version 0 → no suffix → `x`
- Second `x` → version 1 → `x_1`
- Third `x` → version 2 → `x_2`

**`_declaredVariables`**: Tracks which variables (base names) have been declared in the current scope.

**`_variableVersions`**: Maps variable base names (camelCase) to their current version number (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:39-43).

**`_constVariables`**: Tracks local const variable names (original Sharpy names) so they can be emitted in `CONSTANT_CASE` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:45-49).

#### Module-Level Symbol Tracking (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:51-63)

```csharp
private readonly HashSet<string> _moduleFieldNames = new();
private bool _forceModuleLevelFields;
```

**`_moduleFieldNames`**: Tracks which C# field names have already been emitted to prevent duplicate static field declarations. Even though `CodeGenInfo` pre-computes names, we still need to track what's been emitted.

**`_forceModuleLevelFields`**: A flag that changes emission behavior when the user defines a `main()` function. When `true`:
- Variables with execution order issues are **forced to be static fields** instead of local variables in `Main()`
- The user takes responsibility for initialization order
- The emitter switches from camelCase (local var) to PascalCase (static field) for these variables

#### Interface and Type Tracking

```csharp
private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new();
```

**`_interfaceDefinitions`**: Tracks interface AST nodes so that abstract classes can generate stub implementations. Used for abstract class generation (ellipsis body `...` in abstract class = abstract method) (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:74).

#### Context and Temporary Tracking (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:75-83)

```csharp
private int _tempVarCounter = 0;
private TypeAnnotation? _targetTypeContext;
private bool _isInAbstractClass;
```

**`_tempVarCounter`**: Generates unique temporary variable names for complex expressions (e.g., `__temp0`, `__temp1`).

**`_targetTypeContext`**: Set before generating expressions that need target type information (e.g., collection literal type inference).

**`_isInAbstractClass`**: Tracks whether we're currently emitting methods for an abstract class (affects implicit abstract method detection).

#### Common .NET Acronyms (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:86-90)

```csharp
private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
{
    "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
    "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
};
```

Used for namespace naming: `io_utils` → `IOUtils` (not `IoUtils`).

---

## Key Design Principle: Hybrid Name Resolution (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:14-22)

The emitter uses a **hybrid approach** for name resolution:

### Module-Level Symbols (Pre-computed)
Use `Symbol.CodeGenInfo` computed during semantic analysis:
- Variables, constants, functions, types, imports
- C# names, versioning, constant flags, import metadata
- Read-only during emission (no mutation needed)

### Local Variables (Runtime Tracking)
Use `_declaredVariables`, `_variableVersions`:
- Why? Local variable redeclarations happen during emission
- Mutable state that evolves as we generate method bodies

**Rationale:** This separation clarifies responsibility:
- **Semantic analysis** computes module-level metadata
- **Emitter** tracks local scope mutations

This is explicitly a **"TWO-WAY DOOR" decision** (see CodeGenInfo.cs comments) — purely additive and can be removed if needed.

---

## Key Methods

### Constructor (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:92-96)

```csharp
public RoslynEmitter(CodeGenContext context)
{
    _context = context;
    _typeMapper = new TypeMapper(context);
}
```

Simple dependency injection. The `CodeGenContext` contains everything needed for emission.

---

### Name Resolution: The Core Logic

The emitter needs to resolve **Sharpy names** to **C# names** following different conventions. This is the most complex part of the infrastructure.

#### `TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:98-138)

**Purpose:** Resolve module-level symbols using pre-computed `CodeGenInfo` from semantic analysis.

**What is CodeGenInfo?**
`CodeGenInfo` is metadata attached to symbols during semantic analysis. It pre-computes:
- C# name (with proper casing)
- Version number for redeclarations
- Module-level vs. local status
- Constant vs. variable status
- Execution order issues
- Import metadata (aliases, from-imports)

**Algorithm:**
1. Look up symbol in symbol table
2. If no `CodeGenInfo`, return `null` (use fallback logic)
3. For **new local declarations**: return `null` unless `_forceModuleLevelFields` is set
   - Local redeclarations need runtime tracking via `_variableVersions`
4. **Special case:** When `_forceModuleLevelFields = true` and variable has execution order issues:
   - Override the camelCase name with PascalCase (for static fields)
5. Get versioned C# name from `CodeGenInfo`
6. **Module imports**: Escape C# keywords (e.g., `base` → `@base`)

**Why return null for local redeclarations?**
Local variable redeclarations happen **during emission**, so `CodeGenInfo` can't pre-compute them. The emitter uses `_variableVersions` to track versions dynamically.

---

#### `GetMangledVariableName(string name, bool isNewDeclaration)` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:140-220)

This is the **heart of name resolution** in RoslynEmitter. It's called whenever the emitter needs to generate a C# identifier from a Sharpy name.

**Parameters:**
- `name`: The original Sharpy identifier (e.g., `my_variable`, `MAX_VALUE`, `MyClass`)
- `isNewDeclaration`: `true` if this is a variable declaration/redefinition, `false` if it's a reference

**Returns:** The C# identifier with appropriate casing and versioning (e.g., `myVariable`, `myVariable_1`, `MAX_VALUE`, `MyClass`)

**Resolution Strategy (Priority Order):**

1. **Local Variable Versioning** (Highest Priority)
   ```csharp
   if (_variableVersions.ContainsKey(baseName))
   {
       if (isNewDeclaration)
       {
           var newVersion = _variableVersions[baseName] + 1;
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
   **Why first?** Local variables shadow module-level names (matches Python's LEGB rule).

2. **Local Const Variables**
   ```csharp
   if (_constVariables.Contains(name))
       return NameMangler.ToConstantCase(name);
   ```

3. **Type Symbols** (Classes/Structs)
   ```csharp
   if (symbol is TypeSymbol typeSymbol &&
       (typeSymbol.TypeKind == TypeKind.Class || typeSymbol.TypeKind == TypeKind.Struct))
       return NameMangler.ToPascalCase(name);
   ```
   **New approach:** Query symbol table instead of maintaining `_classNames` / `_structNames` sets.

4. **Module Symbols**
   ```csharp
   if (symbol is ModuleSymbol)
       return EscapeCSharpKeyword(name.Replace(".", "_"));
   ```

5. **CodeGenInfo-Based Resolution**
   ```csharp
   var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
   if (codeGenName != null)
       return codeGenName;
   ```
   **Handles:** Module-level variables, constants, from-imports (with aliases).

6. **Default: New Local Variable**
   ```csharp
   if (isNewDeclaration)
   {
       _variableVersions[baseName] = 0;
       return baseName;
   }
   ```

**Example:**
```python
# Sharpy code
user_name: str = "Alice"     # First declaration
print(user_name)             # Reference
user_name: int = 42          # Redeclaration
```

```csharp
// Generated C#
var userName = "Alice";      // version 0
Console.WriteLine(userName); // reference to version 0
var userName_1 = 42;         // version 1
```

---

#### `GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)` (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:230-252)

**Purpose:** High-level helper to get the C# name for any symbol.

**Algorithm:**
1. If `CodeGenInfo` exists: use `GetVersionedCSharpName()`
2. Otherwise, fallback based on symbol kind:
   - Variable → `GetMangledVariableName()`
   - Function → `PascalCase`
   - Type → `PascalCase`
   - Module → Sanitized name with keyword escaping
   - Parameter → `camelCase`

**When is CodeGenInfo unavailable?**
- Local variables during emission (tracked via `_variableVersions`)
- Symbols not processed by `CodeGenInfoComputer` (rare edge cases)

---

### CodeGenInfo Helper Methods (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:222-293)

These methods provide a **clean interface** for accessing `Symbol.CodeGenInfo` data. They encapsulate null-safety and abstraction.

```csharp
private bool IsModuleLevelConstant(Symbol symbol)
    → symbol.CodeGenInfo?.IsModuleLevel == true && symbol.CodeGenInfo.IsConstant

private bool IsModuleLevelVariable(Symbol symbol)
    → symbol.CodeGenInfo?.IsModuleLevel == true && !symbol.CodeGenInfo.IsConstant

private bool HasExecutionOrderIssues(Symbol symbol)
    → symbol.CodeGenInfo?.HasExecutionOrderIssues == true

private bool IsFromImportSymbol(Symbol symbol)
    → symbol.CodeGenInfo?.ImportKind is FromImport or FromImportWithAlias

private string? GetOriginalImportName(Symbol symbol)
    → symbol.CodeGenInfo?.OriginalImportName
```

**Why use helper methods?**
- **Null safety**: Graceful handling when `CodeGenInfo` is missing
- **Abstraction**: Hides the internal structure of `CodeGenInfo`
- **Readability**: `IsModuleLevelConstant(symbol)` is clearer than `symbol.CodeGenInfo?.IsModuleLevel == true && symbol.CodeGenInfo.IsConstant`

---

### SemanticBinding Helper Methods (src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:295-333)

These methods read from `SemanticBinding` when available, falling back to direct AST properties for backward compatibility.

```csharp
private string? GetResolvedModulePath(FromImportStatement fromImport)
private Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement fromImport)
private bool HasReExportedSymbols(FromImportStatement fromImport)
```

**Why use SemanticBinding?**
The compiler is transitioning from **mutable AST** (where semantic data is stored in AST node properties) to **immutable AST + SemanticBinding** (where semantic data is stored separately). These helpers support both approaches during the transition.

**Example:**
```csharp
// Old approach (mutable AST)
var modulePath = fromImport.ResolvedModulePath;

// New approach (immutable AST + SemanticBinding)
var modulePath = _context.SemanticBinding?.GetResolvedModulePath(fromImport);

// Helper (supports both)
var modulePath = GetResolvedModulePath(fromImport);
```

---

## Name Resolution Strategy: Priority Order

Understanding the **priority order** of name resolution is critical for debugging. When resolving a Sharpy name, the emitter checks in this order:

1. **Local variables** (`_variableVersions`)
   - Includes parameters (they shadow globals)
   - Highest priority (local shadows global)

2. **Local const variables** (`_constVariables`)
   - Use `CONSTANT_CASE`

3. **Type symbols** (classes/structs)
   - Use `PascalCase`
   - Checked via `SymbolTable.LookupType()`

4. **Module symbols**
   - Preserve sanitized name
   - Escape C# keywords

5. **CodeGenInfo** (module-level symbols, from-imports)
   - Pre-computed names from semantic analysis
   - Handles aliases, versioning, etc.

6. **Fallback: new local variable**
   - Add to `_variableVersions` with version 0
   - Use `camelCase`

**Why does order matter?**

```python
# Sharpy code
MAX_SIZE = 100  # Module-level constant

def foo(MAX_SIZE: int):  # Parameter shadows global
    print(MAX_SIZE)      # Refers to parameter, not global
```

```csharp
// Generated C#
public static readonly int MAX_SIZE = 100;

public static void Foo(int maxSize)  // Parameter uses camelCase
{
    Console.WriteLine(maxSize);      // Refers to local parameter
}
```

The parameter `MAX_SIZE` is added to `_variableVersions` during parameter emission, so when the print statement references `MAX_SIZE`, the emitter finds it in local variables first (camelCase → `maxSize`) before checking module-level constants.

---

## Debugging Tips

### Name Resolution Issues

**Symptom:** Generated C# uses wrong name or casing.

**Debug steps:**
1. Check symbol table: Is the symbol registered?
2. Check `CodeGenInfo`: What's the pre-computed name?
3. Check `_variableVersions`: Is it tracked as a local?
4. Add logging in `GetMangledVariableName()` to see the resolution path

**Example issue:** Module-level variable emitted as `camelCase` instead of `PascalCase`.
- **Likely cause:** `CodeGenInfo` not populated (semantic analysis skipped)
- **Fix:** Ensure `TypeChecker.CheckModule()` is called with `computeCodeGenInfo: true`

### Variable Version Issues

**Symptom:** Variable redeclaration doesn't get version suffix (or gets wrong version).

**Debug steps:**
1. Check `_variableVersions` dictionary state
2. Ensure `isNewDeclaration = true` for new declarations
3. Verify that `_variableVersions` isn't being cleared between declarations

**Example issue:** Second `x` declaration generates `x` instead of `x_1`.
- **Likely cause:** `isNewDeclaration` passed as `false`
- **Fix:** Check call sites in `RoslynEmitter.Statements.cs` (variable declaration emission)

### Execution Order Issues

**Symptom:** Module-level variables generate compilation errors about initialization order.

**Debug steps:**
1. Check `HasExecutionOrderIssues()` for the symbol
2. Verify `_forceModuleLevelFields` flag state
3. Check if user-defined `main()` exists

**Example issue:** Variable with execution order issues emitted as static field instead of local in `Main()`.
- **Likely cause:** `_forceModuleLevelFields = true` but no user-defined `main()`
- **Fix:** Check `Main()` detection logic in `RoslynEmitter.CompilationUnit.cs`

### Import Resolution Issues

**Symptom:** Imported symbols not found or have wrong names.

**Debug steps:**
1. Check `SemanticBinding.GetResolvedModulePath()`
2. Verify `CodeGenInfo.ImportKind` (FromImport vs. ModuleImport)
3. Check `CodeGenInfo.OriginalImportName` for aliases

**Example issue:** `from math import sqrt as square_root` generates reference to `sqrt` instead of `squareRoot`.
- **Likely cause:** `CodeGenInfo.CSharpName` not set correctly
- **Fix:** Check `CodeGenInfoComputer` from-import handling

---

## Patterns and Design Decisions

### 1. **Partial Classes for Maintainability**

**Why?** A single 8000+ line file is unmaintainable. Partial classes allow logical separation:
- **This file**: Infrastructure (state, naming, helpers)
- **CompilationUnit**: Top-level structure
- **Statements**: Control flow emission
- **Expressions**: Value emission
- **TypeDeclarations**: Type definitions
- **ClassMembers**: Member definitions
- **ModuleClass**: Module organization
- **Operators**: Operator overloading

**Trade-off:** Context switching between files, but better than scrolling thousands of lines.

### 2. **CodeGenInfo: Pre-computed vs. Runtime Resolution**

**Decision:** Move name resolution from emission time to semantic analysis time.

**Pros:**
- Single source of truth for names
- Faster emission (no recomputation)
- Easier debugging (names computed once)

**Cons:**
- Can't handle local redeclarations (still need runtime tracking)
- Adds complexity to semantic analysis phase

**Result:** Hybrid approach:
- **Module-level symbols**: Use `CodeGenInfo` (pre-computed)
- **Local variables**: Use `_variableVersions` (runtime tracking)

### 3. **Immutable AST + SemanticBinding**

**Decision:** Transition from mutable AST (semantic data in AST nodes) to immutable AST (semantic data in separate binding).

**Why?**
- **Parallelization**: Immutable AST can be shared across threads
- **Caching**: AST can be cached without invalidation concerns
- **Clarity**: Separation of parsing and semantic concerns

**Current state:** Transitional (supports both approaches via helper methods).

### 4. **Priority-Based Name Resolution**

**Decision:** Local variables shadow globals, types have priority over variables, etc.

**Why?** Matches Python's LEGB rule (Local, Enclosing, Global, Builtin) for name resolution.

**Implementation:** Explicit priority ordering in `GetMangledVariableName()`.

### 5. **Keyword Escaping**

C# keywords need `@` prefix when used as identifiers. The emitter handles this transparently:
- Module import `base` → `@base`
- Variable `class` → `@class`

**Why not just forbid keywords?** Sharpy aims for Python compatibility. Python allows `class` as a variable name (though discouraged).

---

## Contribution Guidelines

### When to Modify This File

**Add state tracking fields when:**
- Introducing new symbol kinds that need emission-time tracking
- Adding new context flags (like `_isInAbstractClass`)

**Add helper methods when:**
- Accessing `CodeGenInfo` or `SemanticBinding` properties
- Performing common name resolution tasks used across partial files

**Modify name resolution when:**
- Changing naming conventions (e.g., different casing rules)
- Adding new symbol types (e.g., traits, protocols)
- Fixing shadowing or priority bugs

### When NOT to Modify This File

**Don't add emission logic here.** This file is **infrastructure only**. Emission logic belongs in:
- `RoslynEmitter.Statements.cs` - Statement emission
- `RoslynEmitter.Expressions.cs` - Expression emission
- `RoslynEmitter.TypeDeclarations.cs` - Type emission
- etc.

**Don't add type mapping logic here.** That belongs in `TypeMapper.cs`.

**Don't add name mangling logic here.** That belongs in `NameMangler.cs`.

### Testing Changes

When modifying name resolution or state tracking:

1. **Unit tests**: Add tests in `RoslynEmitterTests.cs`
2. **Integration tests**: Add file-based tests in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
3. **Manual testing**: Use `dotnet run --project src/Sharpy.Cli -- emit csharp file.spy` to inspect generated C#

**Example test case for variable versioning:**
```python
# file: test_variable_redeclaration.spy
x: int = 5
x: str = "hello"
x: bool = True
```

Expected C#:
```csharp
var x = 5;
var x_1 = "hello";
var x_2 = true;
```

### Code Review Checklist

When reviewing changes to this file:

- [ ] Are new state fields documented with XML comments?
- [ ] Are helper methods named clearly (verb + object)?
- [ ] Does name resolution preserve priority order?
- [ ] Are C# keywords escaped?
- [ ] Are edge cases handled (null symbols, missing CodeGenInfo)?
- [ ] Are tests added for new behavior?

---

## Cross-References

### Related Partial Class Files

This is the **main file** in a partial class split. See related files for emission logic:

- **[RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md)** - Top-level module structure, using statements, namespace generation
- **[RoslynEmitter.ModuleClass.md](RoslynEmitter.ModuleClass.md)** - Module class generation, entry point detection, static field emission
- **[RoslynEmitter.Statements.md](RoslynEmitter.Statements.md)** - Statement emission (if, while, for, return, etc.)
- **[RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md)** - Expression emission (literals, operators, calls, etc.)
- **[RoslynEmitter.TypeDeclarations.md](RoslynEmitter.TypeDeclarations.md)** - Type definitions (class, struct, enum, interface)
- **[RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md)** - Class member generation (methods, fields, properties)
- **[RoslynEmitter.Operators.md](RoslynEmitter.Operators.md)** - Operator overloading

### Related Infrastructure Files

- **`CodeGenContext.cs`** - State container for code generation (src/Sharpy.Compiler/CodeGen/CodeGenContext.cs:9)
- **`TypeMapper.cs`** - Sharpy-to-C# type mapping (src/Sharpy.Compiler/CodeGen/TypeMapper.cs:14)
- **`NameMangler.cs`** - Naming convention transformations (src/Sharpy.Compiler/CodeGen/NameMangler.cs:8)
- **`CodeGenInfo.cs`** - Semantic metadata for name resolution (see [CodeGenInfo.md](../Semantic/CodeGenInfo.md))
- **`CodeGenInfoComputer.cs`** - Computes CodeGenInfo during semantic analysis (see [CodeGenInfoComputer.md](../Semantic/CodeGenInfoComputer.md))
- **`SemanticBinding.cs`** - Immutable semantic data storage (see [SemanticBinding.md](../Semantic/SemanticBinding.md))

### Upstream Dependencies

- **`Parser.Ast`** - AST node definitions (statements, expressions, types)
- **`Semantic.Symbol`** - Symbol definitions (VariableSymbol, FunctionSymbol, TypeSymbol, etc.)
- **`Semantic.SymbolTable`** - Symbol lookup and scoping

### Downstream Usage

- **`Sharpy.Cli`** - Command-line interface uses `RoslynEmitter.Emit()` to generate C# from AST
- **`Sharpy.Compiler.Tests`** - Integration tests verify emitted C# compiles and executes correctly

---

## Summary

`RoslynEmitter.cs` is the **spine** of the code generation phase. It:
- Initializes dependencies (`CodeGenContext`, `TypeMapper`)
- Tracks mutable state during emission (variable versions, declared symbols)
- Resolves Sharpy names to C# names with proper casing and versioning
- Provides helper methods for accessing semantic metadata
- Coordinates with other partial class files for actual AST-to-C# emission

**Key takeaway:** This file is **infrastructure, not implementation**. When debugging emission issues, start here to understand name resolution, then follow the trail to the appropriate partial file (Statements, Expressions, etc.) for the actual emission logic.
