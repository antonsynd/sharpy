# Walkthrough: RoslynEmitter.ModuleClass.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

---

## Overview

This partial class file handles **module-level code generation** for the Sharpy compiler. It transforms a Sharpy module (a `.spy` file) into a C# static class containing all top-level declarations and executable code.

**Key Responsibilities:**
- Converting module names to C# namespaces (`system.io` → `System.IO`)
- Deciding which module-level variables become static fields vs. local variables in `Main()`
- Generating the module's `Exports` class (or `Program` class for entry points)
- Creating delegating methods/properties for re-exported symbols (`from .helpers import utility_func`)
- Generating a `Main()` entry point when needed

**Role in the Pipeline:**
- **Input**: List of AST statements from a parsed Sharpy module
- **Output**: A `ClassDeclarationSyntax` representing the module as a C# static class
- **Upstream**: Called by `GenerateCompilationUnit()` (likely in `RoslynEmitter.CompilationUnit.cs`)
- **Downstream**: The class is integrated into a full compilation unit and emitted as C# source

---

## Class/Type Structure

This file extends the `RoslynEmitter` partial class with module-specific generation logic.

### Partial Class Context

This is one of **seven** partial class files for `RoslynEmitter`:
- `RoslynEmitter.cs` - Main class and core infrastructure
- `RoslynEmitter.CompilationUnit.cs` - Top-level compilation unit generation
- **`RoslynEmitter.ModuleClass.cs`** (this file) - Module class generation
- `RoslynEmitter.ClassMembers.cs` - Class/struct member generation
- `RoslynEmitter.TypeDeclarations.cs` - Type definition generation
- `RoslynEmitter.Statements.cs` - Statement generation
- `RoslynEmitter.Expressions.cs` - Expression generation
- `RoslynEmitter.Operators.cs` - Operator generation

### State Used from Main Class

This file relies on several fields from the main `RoslynEmitter` class:
- `_moduleConstVariables` - Tracks const/ALL_CAPS variables (become static readonly fields)
- `_moduleVariables` - Tracks regular module-level variables (become static fields)
- `_moduleFieldNames` - Used during field name generation
- `_variablesWithExecutionOrderIssues` - Variables that must become locals in `Main()`
- `_interfaceDefinitions` - Cached interface definitions for abstract class stub generation
- `_context` - The `CodeGenContext` (error reporting, symbol table, etc.)
- `_typeMapper` - The `TypeMapper` for converting semantic types to Roslyn types
- `_declaredVariables`, `_variableVersions`, `_constVariables` - For tracking variable scope

---

## Key Functions/Methods

### 1. `GenerateModuleClass()` - The Main Orchestrator

```csharp
private ClassDeclarationSyntax GenerateModuleClass(
    List<Statement> statements,
    List<FromImportStatement>? reExportImports = null)
```

**Purpose**: The entry point for generating a module's static class. This is the most complex method in the file.

**Algorithm Overview:**

1. **Pre-scanning Phase** (lines 84-306):
   - Clears tracking collections (`_moduleConstVariables`, `_moduleVariables`, etc.)
   - Collects all type and function names (classes, structs, enums, functions)
   - Caches interface definitions for abstract class stub generation
   - **Two-pass execution order analysis**:
     - First pass: Identifies const variables, multiple declarations, assignments before declarations
     - Second pass: Transitive closure to find variables whose initializers reference other local variables

2. **Variable Classification**:
   - **Const variables**: `IsConst` or `ALL_CAPS_NAME` → static readonly fields
   - **Module variables**: Regular variables with no execution order issues → static fields
   - **Local variables**: Variables with execution order issues → local variables in `Main()`

3. **Statement Separation** (lines 308-342):
   - Separates declarations (classes, functions, fields) from executable statements
   - Detects if there's a user-defined `main` function
   - Handles variable redefinitions (null return from `GenerateModuleLevelField`)

4. **Main Method Generation** (lines 346-385):
   - Generates `Main()` if:
     - No user-defined `main` function exists **AND**
     - This is an entry point file (`_context.IsEntryPoint`)
   - Error if both `main()` function and module-level statements exist
   - Warning if non-entry-point file has executable statements

5. **Re-export Generation** (lines 399-408):
   - Generates delegating methods/properties for `from .helpers import utility_func` patterns

6. **Class Assembly** (lines 410-418):
   - Determines class name: `"Program"` for entry points with `Main()`, else `"Exports"`
   - Returns `ClassDeclarationSyntax` with `public static` modifiers

**Key Design Decision**: The execution order analysis ensures that Sharpy's Python-like execution semantics work correctly. Variables initialized with other module variables must be evaluated at runtime (in `Main()`), not as static field initializers (which have undefined ordering in C#).

**Example**:
```python
# Sharpy code
x = 10
y = x + 5  # y references x - execution order issue!
```

Without the analysis, `y` might become a static field initialized before `x`, causing undefined behavior. The algorithm detects this and makes `y` a local variable in `Main()`.

---

### 2. `ConvertModuleNameToNamespace()` - Module Name Transformation

```csharp
private string ConvertModuleNameToNamespace(string moduleName)
```

**Purpose**: Converts Python-style module names to C# namespace naming conventions.

**Examples**:
- `"system.io"` → `"System.IO"`
- `"my_module.sub_module"` → `"MyModule.SubModule"`

**Implementation Details**:
- Splits on `.` separator
- Uses `SimpleToPascalCase()` for each part (not `NameMangler.Transform()`)
- **Why not NameMangler?** The name mangler tracks unique names to avoid collisions, which would cause `"system"` to become `System`, `System1`, `System2`, etc. Namespaces should use simple PascalCase without uniqueness tracking.

---

### 3. `SimpleToPascalCase()` - Basic Name Conversion

```csharp
private static string SimpleToPascalCase(string name)
```

**Purpose**: Converts snake_case names to PascalCase for namespace generation.

**Algorithm**:
1. Handle literal names (backtick-escaped): `` `namespace` `` → `namespace` (preserve as-is)
2. Check for known acronyms (e.g., `"io"` → `"IO"`, not `"Io"`)
3. Sanitize invalid identifier characters to underscores
4. Split on underscores and capitalize each part
5. Prefix with `_` if result starts with a digit

**Examples**:
- `"my_module"` → `"MyModule"`
- `"io"` → `"IO"` (acronym)
- `"123invalid"` → `"_123invalid"` (digit prefix)
- `"___"` → `"_"` (edge case: only underscores)

**Difference from NameMangler**: This is stateless and doesn't track uniqueness. Used specifically for namespaces.

---

### 4. `GetModuleClassName()` - Class Naming Logic

```csharp
private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
```

**Purpose**: Determines the name of the module's static class.

**Logic**:
- If generating a `Main()` method → `"Program"` (standard C# entry point class)
- Otherwise → `"Exports"` (consistent with import system: `using config = ProjectNamespace.Config.Exports;`)

**Why "Exports"?** This matches the Sharpy import specification. When you import a module, you're accessing its `Exports` class.

---

### 5. Re-export Generation Methods

#### `GenerateReExportMembers()`

```csharp
private IEnumerable<MemberDeclarationSyntax> GenerateReExportMembers(FromImportStatement fromImport)
```

**Purpose**: Generates delegating members for re-exported symbols from `from .helpers import utility_func` statements.

**Process**:
1. Converts resolved module path to namespace: `"mypackage.helpers"` → `"Mypackage.Helpers.Exports"`
2. Iterates through `ReExportedSymbols` (populated during semantic analysis)
3. Generates delegating methods for functions, delegating properties for variables
4. Type symbols (classes, structs, enums) use type aliases (handled elsewhere)

**Why Delegation?** Re-exports create public API surface in the importing module that forwards to the original module. This enables hierarchical module organization.

---

#### `GenerateReExportMethod()`

```csharp
private MemberDeclarationSyntax GenerateReExportMethod(string localName, FunctionSymbol funcSymbol, string sourceClassName)
```

**Purpose**: Creates a delegating method that forwards calls to the original module.

**Generated Code Pattern**:
```csharp
// from .helpers import utility_func
// Becomes:
public static ReturnType UtilityFunc(ParamType1 param1, ParamType2 param2)
{
    return Mypackage.Helpers.Exports.UtilityFunc(param1, param2);
}
```

**Implementation**:
- Maps parameter types using `_typeMapper.MapSemanticType()`
- Handles void return types (expression statement vs. return statement)
- Uses `NameMangler` for consistent naming

---

#### `GenerateReExportProperty()`

```csharp
private MemberDeclarationSyntax GenerateReExportProperty(string localName, VariableSymbol varSymbol, string sourceClassName)
```

**Purpose**: Creates a delegating property for re-exported constants/variables.

**Generated Code Pattern**:
```csharp
// from .config import MAX_CONNECTIONS
// Becomes:
public static int MAX_CONNECTIONS => Mypackage.Config.Exports.MAX_CONNECTIONS;
```

**Special Handling**:
- Preserves `ALL_CAPS` naming for constants using `NameMangler.ToConstantCase()`
- Uses expression-bodied property syntax (arrow function)
- Read-only (no setter)

---

### 6. `GenerateStatement()` - Statement Dispatcher

```csharp
private SyntaxNode? GenerateStatement(Statement stmt)
```

**Purpose**: Routes AST statements to appropriate generation methods.

**Returns**:
- `MemberDeclarationSyntax` for declarations (classes, functions, fields)
- `null` for compile-time constructs (type aliases) or executable statements

**Note**: This is a simple dispatcher. The actual implementation delegates to methods in other partial class files:
- `GenerateFunctionDeclaration()` - likely in `RoslynEmitter.ClassMembers.cs`
- `GenerateClassDeclaration()` - likely in `RoslynEmitter.TypeDeclarations.cs`
- `GenerateReturn()`, `GenerateAssignment()` - likely in `RoslynEmitter.Statements.cs`

---

## Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (`_context`):
   - `_context.IsEntryPoint` - Determines if this module is the program entry point
   - `_context.AddError()` - Reports semantic errors
   - `_context.Logger.LogWarning()` - Reports warnings
   - `_context.LookupSymbol()` - Queries the symbol table
   - `_context.SymbolTable.Define()` - Registers enum symbols

2. **`TypeMapper`** (`_typeMapper`):
   - `_typeMapper.MapSemanticType()` - Converts Sharpy semantic types to Roslyn types

3. **`NameMangler`** (static utility):
   - `NameMangler.Transform()` - Converts names with context (Method, Parameter, etc.)
   - `NameMangler.ToPascalCase()` - Converts to PascalCase
   - `NameMangler.ToConstantCase()` - Preserves ALL_CAPS naming

4. **AST Types** (from `Sharpy.Compiler.Parser.Ast`):
   - `Statement`, `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `VariableDeclaration`, `Assignment`, `FromImportStatement`

5. **Semantic Types** (from `Sharpy.Compiler.Semantic`):
   - `FunctionSymbol`, `VariableSymbol`, `TypeSymbol`
   - `SemanticType`, `VoidType`

### External Dependencies

1. **Roslyn** (`Microsoft.CodeAnalysis.CSharp`):
   - `SyntaxFactory` methods (aliased as static imports)
   - `ClassDeclarationSyntax`, `MemberDeclarationSyntax`, `StatementSyntax`
   - `SyntaxKind` enums

---

## Patterns and Design Decisions

### 1. **Execution Order Analysis (The "Big Algorithm")**

**Problem**: Python/Sharpy executes module-level code top-to-bottom. C# static field initializers have **undefined execution order**.

**Solution**: A two-pass algorithm (lines 121-267) that detects "execution order issues":

**Pass 1**: Detect obvious issues:
- Variables declared multiple times
- Variables assigned before declaration
- Const variables (no issue - can be static)

**Pass 2**: Transitive closure to find indirect dependencies:
- Variables whose initializers reference other local variables
- Variables whose initializers reference assignment-only variables
- Variables with mutual dependencies

**Result**: Variables with issues → local variables in `Main()`. Clean variables → static fields.

**Example of Transitive Dependency**:
```python
x = get_value()  # Assignment without type annotation → goes to Main()
y: int = x + 1   # y references x, so y must also go to Main()
z: int = y * 2   # z references y, so z must also go to Main()
```

---

### 2. **Module Class Naming Convention**

**Design**: All modules use `"Exports"` class name, except entry points which use `"Program"`.

**Rationale**:
- **Consistency**: Imports always access `ModuleName.Exports.Function()`
- **Entry Points**: C# convention uses `Program.Main()` as the entry point
- **Spec Compliance**: Matches the language specification for imports

---

### 3. **Re-export Delegation Pattern**

**Design**: `from .helpers import utility_func` generates a delegating method in the current module.

**Alternatives Considered**:
- Type aliases (used for type symbols)
- Public re-exports via `using` (doesn't work for methods/variables)

**Benefits**:
- Preserves type information
- Allows IDEs to show documentation from the original module
- Supports hierarchical module organization (e.g., `from .internals import _helper` in `__init__.py`)

---

### 4. **Const Variable Detection**

**Design**: Variables are const if:
- Explicitly marked `is_const` in AST, **OR**
- Name is `ALL_CAPS` (Python convention)

**Implementation**: `IsConstantCaseName()` helper (defined elsewhere, likely in main `RoslynEmitter.cs`)

**C# Generation**:
```python
MAX_SIZE = 100        # → public static readonly int MAX_SIZE = 100;
max_size: int = 100   # → private static int MaxSize = 100;
```

---

### 5. **Partial Class Organization**

**Design**: Split `RoslynEmitter` across multiple files by responsibility:
- Module generation (this file)
- Class member generation (separate file)
- Expression generation (separate file)
- etc.

**Benefits**:
- Smaller, more focused files (~500-1000 lines each)
- Easier to navigate and maintain
- Clear separation of concerns

---

## Debugging Tips

### 1. **Variable Placement Issues**

**Symptom**: Variables appear as static fields when they should be in `Main()`, or vice versa.

**Debug Steps**:
1. Check `_variablesWithExecutionOrderIssues` - should contain the variable name
2. Add logging in the two-pass algorithm (lines 121-267) to trace the analysis
3. Verify `CollectReferencedIdentifiers()` finds all dependencies in the initializer
4. Check if the variable is in `_moduleVariables` or `_moduleConstVariables`

**Common Causes**:
- Initializer references another variable declared later
- Circular dependency between variables
- Variable assigned via `Assignment` statement (no type annotation)

---

### 2. **Module Name Conversion Errors**

**Symptom**: Generated namespace doesn't match expected C# naming.

**Debug Steps**:
1. Trace through `ConvertModuleNameToNamespace()` with the input module name
2. Check if name parts are in `UpperCaseAcronyms` (e.g., `"io"` → `"IO"`)
3. Verify `SimpleToPascalCase()` handles special characters correctly

**Common Issues**:
- Acronyms not recognized (add to `UpperCaseAcronyms`)
- Invalid characters in module name (e.g., hyphens)

---

### 3. **Re-export Not Working**

**Symptom**: `from .helpers import utility_func` doesn't generate delegating method.

**Debug Steps**:
1. Verify `fromImport.ReExportedSymbols` is populated (set during semantic analysis)
2. Check `fromImport.ResolvedModulePath` is not null
3. Trace through `GenerateReExportMembers()` to see if symbol is skipped
4. Verify the symbol type (Function vs. Variable vs. Type)

**Common Causes**:
- Semantic analysis didn't populate `ReExportedSymbols`
- Module path resolution failed
- Symbol is a type (uses type alias instead of delegation)

---

### 4. **Main Method Generation Confusion**

**Symptom**: `Main()` generated when it shouldn't be, or missing when expected.

**Debug Steps**:
1. Check `hasMainFunction` flag (is there a user-defined `main()`?)
2. Verify `_context.IsEntryPoint` (is this the entry point module?)
3. Review the three conditions on lines 346-385

**Decision Tree**:
- User has `main()` function → No generated `Main()`, their `main` becomes `Main()`
- No `main()` + entry point → Generate `Main()` with executable statements
- No `main()` + not entry point → No `Main()`, executable statements ignored (with warning)
- User has `main()` + executable statements → Error (invalid)

---

### 5. **Tracing Variable Classification**

**Add Temporary Logging**:
```csharp
// After line 286 (end of variable classification)
foreach (var varName in _moduleConstVariables)
    Console.WriteLine($"CONST: {varName}");
foreach (var varName in _moduleVariables)
    Console.WriteLine($"FIELD: {varName}");
foreach (var varName in _variablesWithExecutionOrderIssues)
    Console.WriteLine($"LOCAL: {varName}");
```

This shows exactly how each variable was classified.

---

## Contribution Guidelines

### When to Modify This File

1. **Changing module class naming conventions**
   - Edit `GetModuleClassName()`
   - Update import system if changing from `"Exports"`

2. **Improving execution order analysis**
   - Modify the two-pass algorithm (lines 121-267)
   - Add test cases for new edge cases
   - Document new heuristics

3. **Adding new re-export patterns**
   - Extend `GenerateReExportMembers()` to handle new symbol types
   - Add delegating patterns for new constructs

4. **Supporting new module-level statement types**
   - Extend `GenerateStatement()` switch expression
   - Add corresponding generation methods (in other partial files)

5. **Changing Main method generation logic**
   - Modify conditions on lines 346-390
   - Update error messages and warnings

---

### Testing Considerations

**Key Test Scenarios**:

1. **Execution Order Analysis**:
   - Variables with circular dependencies
   - Variables referencing forward-declared variables
   - Variables assigned before declaration
   - Const variables (should always be static fields)

2. **Module Naming**:
   - Snake_case module names
   - Dotted module paths (`package.subpackage.module`)
   - Acronyms in module names (`io`, `http`, etc.)
   - Special characters in module names

3. **Re-exports**:
   - `from .module import function`
   - `from .module import variable`
   - `from .module import Class` (should use type alias)
   - Multiple re-exports from same module

4. **Entry Points**:
   - Entry point with user-defined `main()`
   - Entry point without `main()` (auto-generated `Main()`)
   - Entry point with both `main()` and executable statements (error)
   - Non-entry-point with executable statements (warning)

---

### Code Style and Conventions

**Follow Existing Patterns**:
- Use `SyntaxFactory` methods with static imports
- Prefer expression-bodied methods where appropriate
- Use pattern matching in switch expressions
- Document complex algorithms with inline comments
- Separate concerns into helper methods (keep methods < 50 lines when possible)

**Naming**:
- Private methods: `PascalCase` (e.g., `GenerateModuleClass`)
- Local variables: `camelCase` (e.g., `moduleClassName`)
- Fields: `_camelCase` (e.g., `_context`)

**Error Handling**:
- Use `_context.AddError()` for semantic errors (e.g., conflicting declarations)
- Use `_context.Logger.LogWarning()` for non-fatal issues (e.g., ignored code)
- Return `null` for unsupported constructs (graceful degradation)

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class. For complete understanding, reference:

1. **[RoslynEmitter.md](RoslynEmitter.md)** - Main class overview and core infrastructure
2. **[RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md)** - Class/struct member generation (functions, properties, fields)
3. Other partial files (not yet documented):
   - `RoslynEmitter.CompilationUnit.cs` - Top-level compilation unit generation
   - `RoslynEmitter.TypeDeclarations.cs` - Class/struct/interface/enum definitions
   - `RoslynEmitter.Statements.cs` - Statement generation (return, assignment, etc.)
   - `RoslynEmitter.Expressions.cs` - Expression generation
   - `RoslynEmitter.Operators.cs` - Operator generation

### Related Components

1. **[NameMangler.md](NameMangler.md)** - Name transformation (snake_case → PascalCase)
2. **[TypeMapper.md](TypeMapper.md)** - Semantic type → Roslyn type conversion
3. **[CodeGenContext.md](CodeGenContext.md)** - Code generation context (symbol table, error reporting)

### Language Specifications

- `docs/language_specification/classes.md` - Class semantics
- `docs/language_specification/dotnet_interop.md` - .NET interop and naming conventions

---

## Summary

`RoslynEmitter.ModuleClass.cs` is the **module-to-class transformer** in the Sharpy compiler. It solves the challenging problem of mapping Python's top-down execution model to C#'s static class model, using sophisticated execution order analysis to decide which variables become static fields vs. local variables.

**Key Takeaways for Newcomers**:
- The two-pass algorithm is the most complex part - understand why execution order matters
- Module class naming follows a convention: `"Exports"` for libraries, `"Program"` for entry points
- Re-exports use delegation to create hierarchical module organization
- This file orchestrates, but delegates actual code generation to other partial class files
- When debugging, trace variable classification and check `_context` state

**Next Steps**: Read `RoslynEmitter.ClassMembers.md` to understand how functions and class members are generated within the module class.
