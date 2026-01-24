# Walkthrough: RoslynEmitter.ModuleClass.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

---

## Overview

This partial class file handles **module-level code generation** for the Sharpy compiler. It transforms a Sharpy module (a `.spy` file) into a C# static class containing all top-level declarations and executable code.

**Key Responsibilities:**
- Converting module names to C# namespaces (`system.io` → `System.IO`)
- Generating the module's `Exports` class (or `Program` class for entry points)
- Creating delegating methods/properties for re-exported symbols (`from .helpers import utility_func`)
- Generating a `Main()` entry point when needed
- Managing the separation between declarations (classes, functions, fields) and executable statements
- Pre-registering enum types in the symbol table for correct member access

**Role in the Pipeline:**
- **Input**: List of AST statements from a parsed Sharpy module
- **Output**: A `ClassDeclarationSyntax` representing the module as a C# static class
- **Upstream**: Called by `GenerateCompilationUnit()` (in `RoslynEmitter.CompilationUnit.cs`)
- **Downstream**: The class is integrated into a full compilation unit and emitted as C# source

**Important Context**: The semantic analysis phase (specifically `CodeGenInfoComputer.ComputeForModule`) handles complex module-level variable analysis. This file focuses on code generation, relying on `CodeGenInfo` annotations attached to symbols during semantic analysis.

---

## Class/Type Structure

This file extends the `RoslynEmitter` partial class with module-specific generation logic.

### Partial Class Context

This is one of **eight** partial class files for `RoslynEmitter`:
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

**Fields Defined in RoslynEmitter.cs:**
- `_context` - The `CodeGenContext` (error reporting, symbol table, semantic binding)
- `_typeMapper` - The `TypeMapper` for converting semantic types to Roslyn types
- `_declaredVariables` - Tracks declared variables in current scope
- `_variableVersions` - Tracks version numbers for variable redeclarations (e.g., `x`, `x_1`, `x_2`)
- `_constVariables` - Tracks const variable names in local scopes
- `_moduleFieldNames` - Tracks C# field names to prevent duplicates
- `_interfaceDefinitions` - Cached interface definitions for abstract class stub generation
- `UpperCaseAcronyms` - Common acronyms that should be all uppercase (e.g., `IO`, `API`, `HTTP`)

**Important Design Note**: Module variable tracking is handled by `CodeGenInfo` during semantic analysis, not during emission. The `CodeGenInfoComputer.ComputeForModule` method sets:
- `CodeGenInfo.IsModuleLevel` - Whether a variable is module-level
- `CodeGenInfo.HasExecutionOrderIssues` - Whether it should be a local in `Main()`
- `CodeGenInfo.GetVersionedCSharpName()` - The final C# name with version suffix

---

## Key Functions/Methods

### 1. `GenerateModuleClass()` - The Main Orchestrator

```csharp
private ClassDeclarationSyntax GenerateModuleClass(
    List<Statement> statements,
    List<FromImportStatement>? reExportImports = null)
```

**Purpose**: The entry point for generating a module's static class.

**Algorithm Overview:**

1. **Initialization Phase** (lines 84-99):
   - Clears `_moduleFieldNames` (needed to prevent duplicate field declarations)
   - Clears `_interfaceDefinitions`
   - Note: Module variable tracking is now in `CodeGenInfo`, not local tracking sets

2. **Interface Collection** (lines 91-99):
   - Collects all interface definitions for abstract class stub generation
   - Stores in `_interfaceDefinitions` dictionary keyed by interface name
   - Used when generating abstract classes that implement interfaces

3. **Enum Pre-registration** (lines 101-119):
   - Pre-scans statements for enum declarations
   - Registers them in the symbol table as `TypeSymbol` with `TypeKind.Enum`
   - Ensures enum member access (e.g., `Color.RED`) works correctly during code generation
   - Only adds if not already present (avoids duplicates)

4. **Statement Separation** (lines 122-165):
   - Separates declarations (classes, functions, fields) from executable statements
   - Detects if there's a user-defined `main` function
   - For each statement:
     - Generates the member via `GenerateStatement()`
     - Clears scope tracking after class/struct/function/interface/enum declarations
     - Handles variable redefinitions (when `GenerateModuleLevelField` returns `null`)
   - Variable redefinition handling:
     - Const variables: Skip entirely (consts can't be redeclared)
     - Regular variables: Add to executable statements (becomes local in `Main()`)

5. **Main Method Generation** (lines 167-208):
   - Generates `Main()` if no user-defined `main` function exists AND this is the entry point
   - Generated even if there are no executable statements (to support declaration-only entry points)
   - Clears scope tracking before generating `Main()` body
   - Error conditions:
     - User has `main()` + module-level executable statements → Error (ambiguous execution)
     - Non-entry-point with executable statements → Warning (statements ignored)

6. **Re-export Generation** (lines 224-231):
   - Generates delegating methods/properties for `from .helpers import utility_func` patterns
   - Only if `reExportImports` is provided

7. **Class Assembly** (lines 234-240):
   - Collects function names to check for collisions with class name
   - Determines class name: `"Program"` for entry points with `Main()`, else `"Exports"`
   - Returns `ClassDeclarationSyntax` with `public static` modifiers

**Key Design Decisions**:

1. **Scope Clearing**: After generating class/struct/function declarations, the code clears `_declaredVariables`, `_variableVersions`, and `_constVariables`. This prevents parameter names from methods leaking into module-level code.

2. **Main Generation**: `Main()` is generated for ALL entry point files, even empty ones (line 172-184). This ensures a valid C# entry point exists.

3. **Entry Point Detection**: The `willHaveMainMethod` flag tracks whether a `Main` method will exist (either user-defined or auto-generated), which affects class naming.

**Example Flow**:
```python
# Sharpy module (entry point)
class User:
    pass

def greet(name: str):
    print(f"Hello, {name}")

x = 10
greet("World")
```

Becomes:
```csharp
public static class Program
{
    public class User { }

    public static void Greet(string name) { ... }

    public static void Main()
    {
        var x = 10;
        Greet("World");
    }
}
```

---

### 2. `ConvertModuleNameToNamespace()` - Module Name Transformation

```csharp
private string ConvertModuleNameToNamespace(string moduleName)
```

**Purpose**: Converts Python-style module names to C# namespace naming conventions.

**Examples**:
- `"system.io"` → `"System.IO"`
- `"my_module.sub_module"` → `"MyModule.SubModule"`
- `"http.client"` → `"HTTP.Client"` (acronym handling)

**Implementation Details**:
- Splits on `.` separator
- Uses `SimpleToPascalCase()` for each part
- **Why not NameMangler?** The `NameMangler` tracks unique names to avoid collisions (causing `"system"` to become `System`, `System1`, `System2`, etc.). Namespaces should use simple PascalCase without uniqueness tracking.

**Comment Insight** (lines 22-24):
> We don't use NameMangler.Transform here because:
> 1. It tracks unique names which causes "system" to become System, System1, System2, etc.
> 2. Namespaces should use simple PascalCase without uniqueness tracking

---

### 3. `SimpleToPascalCase()` - Basic Name Conversion

```csharp
private static string SimpleToPascalCase(string name)
```

**Purpose**: Converts snake_case names to PascalCase for namespace generation.

**Algorithm**:
1. **Literal Names** (lines 37-42): Handle backtick-escaped names: `` `namespace` `` → `namespace` (strip backticks, preserve as-is)
2. **Acronym Detection** (lines 44-48): Check if name is a known acronym (e.g., `"io"` → `"IO"`)
3. **Character Sanitization** (lines 51-59): Replace invalid identifier characters with underscores
4. **PascalCase Conversion** (lines 62-70):
   - Split on underscores
   - Capitalize first letter of each part
   - Join without separators
5. **Digit Prefix Handling** (lines 73-76): Prefix with `_` if result starts with a digit
6. **Edge Cases** (lines 65-66): Handle empty result (only underscores) → `"_"`

**Examples**:
- `"my_module"` → `"MyModule"`
- `"io"` → `"IO"` (acronym)
- `"http_client"` → `"HTTP_Client"` (first part is acronym)
- `"123invalid"` → `"_123invalid"` (digit prefix)
- `"___"` → `"_"` (edge case)
- `` `class` `` → `"class"` (literal name)

**Difference from NameMangler**: This is stateless and doesn't track uniqueness. Used specifically for namespaces where consistency matters more than collision avoidance.

---

### 4. `GetModuleClassName()` - Class Naming Logic

```csharp
private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
```

**Purpose**: Determines the name of the module's static class.

**Logic**:
- If generating a `Main()` method → `"Program"` (standard C# entry point class)
- Otherwise → `"Exports"` (consistent with import system)

**Why "Exports"?** (lines 246-247):
> This matches the spec and import system expectation:
> "using config = ProjectNamespace.Config.Exports;"

When you import a Sharpy module, you're accessing its `Exports` class. This convention provides consistency across the entire import system.

**Note**: The `functionNames` parameter is accepted but not currently used. This may be a placeholder for future collision detection logic.

---

### 5. Re-export Generation Methods

#### `GenerateReExportMembers()`

```csharp
private IEnumerable<MemberDeclarationSyntax> GenerateReExportMembers(FromImportStatement fromImport)
```

**Purpose**: Generates delegating members for re-exported symbols from `from .helpers import utility_func` statements.

**Process**:
1. Calls `GetReExportedSymbols(fromImport)` and `GetResolvedModulePath(fromImport)`
   - These methods are defined elsewhere (likely in another partial file)
   - Populated during semantic analysis
2. Converts resolved module path to namespace: `"mypackage.helpers"` → `"Mypackage.Helpers.Exports"`
3. Iterates through re-exported symbols
4. Generates delegating methods for `FunctionSymbol`, delegating properties for `VariableSymbol`
5. Type symbols (classes, structs, enums) are skipped - they use type aliases handled elsewhere

**Why Delegation?** Re-exports create a public API surface in the importing module that forwards to the original module. This enables hierarchical module organization (e.g., `__init__.py` pattern in Python).

**Return Type**: `IEnumerable<MemberDeclarationSyntax>` - yields multiple members
**Yields**: Nothing if symbols or path is null (early exit via `yield break`)

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

**Implementation Steps**:
1. **Name Mangling** (lines 297-298): Transform names using `NameMangler`
2. **Parameter Generation** (lines 301-308):
   - Map parameter types using `_typeMapper.MapSemanticType()`
   - Create Roslyn `Parameter` nodes
3. **Argument Generation** (lines 311-313): Create arguments for the delegate call
4. **Return Type Mapping** (line 316): Map function return type
5. **Delegate Call** (lines 319-324): Build `SourceModule.Exports.Method(args)` expression
6. **Void Handling** (lines 327-335):
   - Void functions → expression statement
   - Non-void functions → return statement
7. **Method Assembly** (lines 337-342): Combine all parts into method declaration

**Special Handling**: The void return type check uses two conditions:
```csharp
if (funcSymbol.ReturnType is VoidType || funcSymbol.ReturnType == SemanticType.Void)
```
This handles both the type pattern (`VoidType`) and singleton comparison (`SemanticType.Void`).

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

**Implementation**:
1. **Name Handling** (lines 351-357):
   - `ALL_CAPS` names → `NameMangler.ToConstantCase()` (preserves case)
   - Regular names → `NameMangler.ToPascalCase()`
   - Applied to both local and source property names
2. **Type Mapping** (line 360): Map variable type to Roslyn type
3. **Delegate Access** (lines 363-366): Build `SourceModule.Exports.Property` expression
4. **Property Assembly** (lines 369-374):
   - Expression-bodied property (arrow syntax)
   - `public static` modifiers
   - Read-only (no setter)
   - Semicolon token required for expression-bodied syntax

**Design Note**: Uses `IsConstantCaseName()` helper (defined elsewhere) to detect `ALL_CAPS` naming convention.

---

### 6. `GenerateStatement()` - Statement Dispatcher

```csharp
private SyntaxNode? GenerateStatement(Statement stmt)
```

**Purpose**: Routes AST statements to appropriate generation methods using pattern matching.

**Returns**:
- `MemberDeclarationSyntax` for declarations (classes, functions, fields)
- `null` for:
  - Compile-time constructs (type aliases)
  - Executable statements (assignments, returns)
  - Variable redefinitions (when `GenerateModuleLevelField` returns null)

**Switch Expression Mapping** (lines 379-392):
```csharp
FunctionDef      → GenerateFunctionDeclaration()
ClassDef         → GenerateClassDeclaration()
StructDef        → GenerateStructDeclaration()
InterfaceDef     → GenerateInterfaceDeclaration()
EnumDef          → GenerateEnumDeclaration()
VariableDeclaration → GenerateModuleLevelField()
TypeAlias        → null (compile-time only)
ReturnStatement  → GenerateReturn()
Assignment       → GenerateAssignment()
_                → null (unknown/unsupported)
```

**Note**: Most generation methods are defined in other partial class files:
- `GenerateFunctionDeclaration()` - in `RoslynEmitter.ClassMembers.cs`
- `GenerateClassDeclaration()`, `GenerateStructDeclaration()` - in `RoslynEmitter.TypeDeclarations.cs`
- `GenerateReturn()`, `GenerateAssignment()` - in `RoslynEmitter.Statements.cs`

---

## Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (`_context`):
   - `IsEntryPoint` - Determines if this module is the program entry point
   - `AddError()` - Reports semantic errors
   - `Logger.LogWarning()` - Reports warnings
   - `LookupSymbol()` - Queries the symbol table
   - `SymbolTable.Define()` - Registers enum symbols
   - `SemanticBinding` - Provides semantic analysis results (import resolution, etc.)

2. **`TypeMapper`** (`_typeMapper`):
   - `MapSemanticType()` - Converts Sharpy semantic types to Roslyn type syntax

3. **`NameMangler`** (static utility):
   - `Transform()` - Converts names with context (Method, Parameter, etc.)
   - `ToPascalCase()` - Converts to PascalCase
   - `ToConstantCase()` - Preserves ALL_CAPS naming

4. **AST Types** (from `Sharpy.Compiler.Parser.Ast`):
   - `Statement`, `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `VariableDeclaration`, `Assignment`, `FromImportStatement`

5. **Semantic Types** (from `Sharpy.Compiler.Semantic`):
   - `FunctionSymbol`, `VariableSymbol`, `TypeSymbol`, `ModuleSymbol`
   - `SemanticType`, `VoidType`
   - `Symbol.CodeGenInfo` - Provides code generation metadata computed during semantic analysis

### External Dependencies

1. **Roslyn** (`Microsoft.CodeAnalysis.CSharp`):
   - `SyntaxFactory` methods (aliased as static imports via `using static`)
   - `ClassDeclarationSyntax`, `MemberDeclarationSyntax`, `StatementSyntax`
   - `SyntaxKind` enums
   - `TokenList`, `SeparatedList`, etc.

2. **System Libraries**:
   - `System.IO` - Path operations
   - `System.Linq` - Collection operations

---

## Patterns and Design Decisions

### 1. **Module-Level Variable Classification (Moved to Semantic Analysis)**

**Previous Design**: This file used to contain a complex two-pass execution order analysis algorithm.

**Current Design**: Variable classification is now handled by `CodeGenInfoComputer.ComputeForModule()` during semantic analysis. The results are stored in `Symbol.CodeGenInfo`.

**Benefits**:
- Semantic analysis and code generation are properly separated
- Variable classification logic is testable independently
- Code generation phase is simpler and more predictable
- `CodeGenInfo` provides a clean interface: `IsModuleLevel`, `HasExecutionOrderIssues`, `GetVersionedCSharpName()`

**Remaining Local Tracking**: This file still tracks local variables at runtime (`_declaredVariables`, `_variableVersions`) because local variable redeclarations happen during emission, not during semantic analysis.

---

### 2. **Scope Clearing After Type Declarations**

**Design** (lines 138-143):
```csharp
if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef)
{
    _declaredVariables.Clear();
    _variableVersions.Clear();
    _constVariables.Clear();
}
```

**Problem**: Without this, parameter names from methods would leak into module-level code:
```python
def foo(x: int):  # x is a parameter
    pass

y = x  # Error: x is not defined at module level
```

**Solution**: After generating each type/function declaration, clear the local scope tracking. This ensures parameter names don't leak.

**Why It's Safe**: Each class/function has its own independent scope. Clearing tracking between declarations doesn't affect the generated code inside those declarations.

---

### 3. **Module Class Naming Convention**

**Design**: All modules use `"Exports"` class name, except entry points which use `"Program"`.

**Rationale**:
- **Consistency**: Imports always access `ModuleName.Exports.Function()`
- **Entry Points**: C# convention uses `Program.Main()` as the entry point
- **Spec Compliance**: Matches the language specification for imports

**Import Example**:
```python
# Sharpy: from .config import MAX_SIZE
# C#: using static Mypackage.Config.Exports;
# Access: var size = MAX_SIZE;
```

---

### 4. **Re-export Delegation Pattern**

**Design**: `from .helpers import utility_func` generates a delegating method in the current module.

**Alternatives Considered**:
- Type aliases (used for type symbols)
- Public re-exports via `using` (doesn't work for methods/variables in C#)
- Direct inline code (loses separation of concerns)

**Benefits**:
- Preserves type information
- Allows IDEs to show documentation from the original module
- Supports hierarchical module organization (e.g., `from .internals import _helper` in `__init__.py`)
- Maintains single source of truth (original module)

**Trade-off**: Adds small runtime overhead (one extra method call), but this is negligible and often inlined by JIT.

---

### 5. **Enum Pre-registration**

**Design** (lines 101-119): Pre-scan for enum declarations and register them in the symbol table before generating any code.

**Problem Without Pre-registration**:
```python
class MyClass:
    color = Color.RED  # Error: Color not in symbol table yet

enum Color:
    RED
    GREEN
```

**Solution**: Register all enums in the symbol table before generating class members. This ensures enum member access works regardless of declaration order.

**Implementation**:
- Creates `TypeSymbol` with `TypeKind.Enum`
- Only adds if not already present (avoids duplicates from semantic analysis)
- Doesn't set `ClrType` (that's determined later)

---

### 6. **Main Method Generation Logic**

**Decision Tree** (lines 167-208):

| Condition | Result |
|-----------|--------|
| User has `main()` function | No generated `Main()`, their `main` becomes `Main()` |
| No `main()` + entry point | Generate `Main()` with executable statements |
| No `main()` + not entry point | No `Main()`, executable statements ignored (warning) |
| User has `main()` + executable statements | Error (ambiguous execution) |

**Design Rationale**:
- **User `main()` takes precedence**: If you define `main()`, it's the entry point
- **Auto-generate when needed**: Entry points need a `Main()` for C# to compile
- **Error on ambiguity**: Having both `main()` and top-level statements is unclear - which runs first?
- **Warn on ignored code**: Non-entry-point modules shouldn't have executable statements

**Empty Entry Points**: Generated even without executable statements (line 184) to support declaration-only libraries that are still entry points.

---

### 7. **Partial Class Organization**

**Design**: Split `RoslynEmitter` across eight files by responsibility:
- Module generation (this file)
- Compilation unit (top-level structure)
- Class member generation
- Type declarations
- Statements
- Expressions
- Operators
- Core infrastructure

**Benefits**:
- Smaller, more focused files (~400-500 lines each)
- Easier to navigate and maintain
- Clear separation of concerns
- Reduces merge conflicts in team development

**Convention**: Each partial file has a clear single responsibility and minimal cross-file dependencies.

---

## Debugging Tips

### 1. **Module Name Conversion Errors**

**Symptom**: Generated namespace doesn't match expected C# naming.

**Debug Steps**:
1. Trace through `ConvertModuleNameToNamespace()` with the input module name
2. Check if name parts are in `UpperCaseAcronyms` (e.g., `"io"` → `"IO"`)
3. Verify `SimpleToPascalCase()` handles special characters correctly

**Common Issues**:
- Acronyms not recognized (add to `UpperCaseAcronyms` in `RoslynEmitter.cs`)
- Invalid characters in module name (e.g., hyphens become underscores)
- Backtick-escaped names not handled (check lines 37-42)

**Example Debugging**:
```csharp
// Input: "http.client"
// Split: ["http", "client"]
// SimpleToPascalCase("http"): "HTTP" (acronym)
// SimpleToPascalCase("client"): "Client"
// Output: "HTTP.Client"
```

---

### 2. **Re-export Not Working**

**Symptom**: `from .helpers import utility_func` doesn't generate delegating method.

**Debug Steps**:
1. Verify `GetReExportedSymbols(fromImport)` returns non-null
   - Check if semantic analysis populated `fromImport.ReExportedSymbols`
   - This is set during the semantic analysis phase
2. Check `GetResolvedModulePath(fromImport)` returns non-null
   - Verify module path resolution succeeded
   - Check `fromImport.ResolvedModulePath`
3. Trace through `GenerateReExportMembers()` to see if symbol is skipped
4. Verify the symbol type (Function vs. Variable vs. Type)
   - Types are skipped (use type aliases)

**Common Causes**:
- Semantic analysis didn't populate `ReExportedSymbols` (fix in semantic phase)
- Module path resolution failed (check import resolver)
- Symbol is a type (expected - uses type alias instead)

**Breakpoint Locations**:
- Line 266: Entry to `GenerateReExportMembers()`
- Line 274: After retrieving symbols/path
- Line 279: Function symbol handling
- Line 282: Variable symbol handling

---

### 3. **Main Method Generation Confusion**

**Symptom**: `Main()` generated when it shouldn't be, or missing when expected.

**Debug Steps**:
1. Check `hasMainFunction` flag (line 124) - is there a user-defined `main()`?
2. Verify `_context.IsEntryPoint` - is this the entry point module?
3. Review the three conditions on lines 172-208
4. Check for error messages (line 201) or warnings (line 207)

**Decision Tree Verification**:
```csharp
// Line 129: Detect user main
if (stmt is FunctionDef funcDef && funcDef.Name == "main")
    hasMainFunction = true;

// Line 172: Auto-generate Main
if (!hasMainFunction && _context.IsEntryPoint)
    // Generate Main()

// Line 196: Error condition
if (hasMainFunction && executableStatements.Count > 0)
    // Error: can't have both

// Line 203: Warning condition
if (!_context.IsEntryPoint && executableStatements.Count > 0)
    // Warning: statements ignored
```

**Example Scenarios**:

| `hasMainFunction` | `IsEntryPoint` | `executableStatements` | Result |
|-------------------|----------------|------------------------|--------|
| true | true | 0 | User `main()` becomes `Main()` |
| false | true | 5 | Auto-generated `Main()` with statements |
| true | true | 3 | **ERROR** |
| false | false | 2 | **WARNING**, no `Main()` |
| false | true | 0 | Empty auto-generated `Main()` |

---

### 4. **Enum Member Access Fails**

**Symptom**: Code like `Color.RED` generates error during code generation.

**Debug Steps**:
1. Verify enum pre-registration (lines 103-119) executed
2. Check if enum was added to symbol table
3. Verify `_context.LookupSymbol("Color")` returns non-null `TypeSymbol`
4. Check `TypeSymbol.TypeKind == TypeKind.Enum`

**Common Issues**:
- Enum declared after it's used in a class (should still work due to pre-registration)
- Enum name collision with existing symbol
- Symbol table not shared across compiler phases

**Add Debug Logging**:
```csharp
// After line 117
Console.WriteLine($"Registered enum: {enumDef.Name}");
var registered = _context.LookupSymbol(enumDef.Name);
Console.WriteLine($"  Lookup result: {registered?.GetType().Name}");
```

---

### 5. **Variable Redefinition Not Working**

**Symptom**: Variable redefinition generates duplicate field or missing local variable.

**Debug Steps**:
1. Check if `GenerateModuleLevelField()` returns `null` (line 149)
2. Verify condition on line 154: `!varRedefinition.IsConst && !IsConstantCaseName()`
3. Check if variable was added to `executableStatements` (line 156)
4. Trace code generation in `Main()` method

**Expected Behavior**:
```python
# First declaration
x: int = 10  # Becomes static field

# Redefinition (different type)
x = "hello"  # GenerateModuleLevelField returns null
             # Added to executableStatements
             # Becomes local in Main()
```

**Const Variable Behavior**:
```python
MAX_SIZE = 100    # First declaration - static readonly field
MAX_SIZE = 200    # Redefinition - SKIPPED entirely (line 158)
                  # Consts can't be redeclared
```

---

### 6. **Scope Leakage Issues**

**Symptom**: Parameter names from functions appearing in module-level code.

**Root Cause**: Scope tracking not cleared after type declarations.

**Verify Fix** (lines 138-143):
```csharp
// This should execute after each class/function/struct/interface/enum
_declaredVariables.Clear();
_variableVersions.Clear();
_constVariables.Clear();
```

**Test Case**:
```python
def foo(helper_var: int):
    pass

helper_var = 10  # Should NOT conflict with parameter
```

Without clearing, `helper_var` might incorrectly reference the parameter scope.

---

## Contribution Guidelines

### When to Modify This File

1. **Changing module class naming conventions**
   - Edit `GetModuleClassName()` (lines 243-255)
   - Update import system if changing from `"Exports"`
   - Ensure consistency with language specification

2. **Adding new re-export patterns**
   - Extend `GenerateReExportMembers()` to handle new symbol types
   - Add delegating patterns for new constructs
   - Update semantic analysis to populate `ReExportedSymbols`

3. **Supporting new module-level statement types**
   - Extend `GenerateStatement()` switch expression (lines 379-392)
   - Add corresponding generation methods in appropriate partial files
   - Update documentation

4. **Changing Main method generation logic**
   - Modify conditions on lines 167-208
   - Update error messages and warnings
   - Consider backward compatibility

5. **Modifying namespace generation**
   - Edit `ConvertModuleNameToNamespace()` or `SimpleToPascalCase()`
   - Add new acronyms to `UpperCaseAcronyms` in `RoslynEmitter.cs`
   - Test with edge cases (special characters, numbers, etc.)

6. **Changing enum handling**
   - Modify pre-registration logic (lines 101-119)
   - Coordinate with semantic analysis phase
   - Update symbol table registration

---

### Testing Considerations

**Key Test Scenarios**:

1. **Module Naming**:
   - Snake_case module names: `my_module` → `MyModule`
   - Dotted module paths: `package.subpackage.module`
   - Acronyms: `io`, `http`, `api` → `IO`, `HTTP`, `API`
   - Special characters: `http-client` → `HttpClient`
   - Numbers: `lib2to3` → `Lib2to3`
   - Backtick escapes: `` `class` ``

2. **Re-exports**:
   - `from .module import function` → delegating method
   - `from .module import variable` → delegating property
   - `from .module import CONSTANT` → preserves ALL_CAPS
   - `from .module import Class` → type alias (not delegation)
   - Multiple re-exports from same module
   - Re-exports with type annotations

3. **Entry Points**:
   - Entry point with user-defined `main()` → no generated `Main()`
   - Entry point without `main()` → auto-generated `Main()`
   - Entry point with both `main()` and executable statements → error
   - Non-entry-point with executable statements → warning
   - Empty entry point (declarations only) → empty `Main()`

4. **Enum Handling**:
   - Enum declared before use
   - Enum declared after use (tests pre-registration)
   - Enum member access in class initializers
   - Multiple enums in same module

5. **Variable Redefinitions**:
   - Regular variable redefinition → local in `Main()`
   - Const variable redefinition → skipped
   - ALL_CAPS variable redefinition → skipped
   - Mixed type redefinition: `x: int = 10` then `x = "hello"`

6. **Scope Clearing**:
   - Parameter names not leaking to module level
   - Local variables in functions not affecting module scope
   - Multiple functions with same parameter names

---

### Code Style and Conventions

**Follow Existing Patterns**:
- Use `SyntaxFactory` methods with static imports (`using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory`)
- Prefer expression-bodied methods where appropriate
- Use pattern matching in switch expressions
- Document complex algorithms with inline comments
- Separate concerns into helper methods (keep methods < 100 lines when possible)

**Naming**:
- Private methods: `PascalCase` (e.g., `GenerateModuleClass`)
- Local variables: `camelCase` (e.g., `moduleClassName`)
- Fields: `_camelCase` (e.g., `_context`)
- Constants: `PascalCase` (e.g., `UpperCaseAcronyms`)

**Error Handling**:
- Use `_context.AddError()` for semantic errors (e.g., conflicting declarations)
- Use `_context.Logger.LogWarning()` for non-fatal issues (e.g., ignored code)
- Return `null` for unsupported constructs (graceful degradation)
- Include descriptive error messages with context

**Roslyn Usage**:
- **Never use string concatenation or templating for code generation**
- Always use `SyntaxFactory` methods
- Use `TokenList`, `SeparatedList` for collections
- Build syntax trees bottom-up (expressions → statements → members → classes)

**Example Good Practice**:
```csharp
// GOOD: Using SyntaxFactory
var method = MethodDeclaration(returnType, methodName)
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithBody(Block(statements));

// BAD: String concatenation (NEVER DO THIS)
var code = $"public {returnType} {methodName}() {{ {body} }}";
```

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class. For complete understanding, reference:

1. **[RoslynEmitter.md](RoslynEmitter.md)** - Main class overview and core infrastructure
2. **[RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md)** - Top-level compilation unit generation (calls `GenerateModuleClass`)
3. **[RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md)** - Class/struct member generation (`GenerateFunctionDeclaration`, etc.)
4. **[RoslynEmitter.TypeDeclarations.md](RoslynEmitter.TypeDeclarations.md)** - Type definitions (`GenerateClassDeclaration`, `GenerateEnumDeclaration`, etc.)
5. **[RoslynEmitter.Statements.md](RoslynEmitter.Statements.md)** - Statement generation (`GenerateReturn`, `GenerateAssignment`, etc.)
6. **[RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md)** - Expression generation
7. **[RoslynEmitter.Operators.md](RoslynEmitter.Operators.md)** - Operator generation

### Related Components

1. **[NameMangler.md](NameMangler.md)** - Name transformation (snake_case → PascalCase, dunder methods, etc.)
2. **[TypeMapper.md](TypeMapper.md)** - Semantic type → Roslyn type conversion
3. **[CodeGenContext.md](CodeGenContext.md)** - Code generation context (symbol table, error reporting)

### Semantic Analysis Components

1. **`CodeGenInfoComputer`** (in `Sharpy.Compiler.Semantic`) - Computes `CodeGenInfo` for symbols during semantic analysis
   - Sets `IsModuleLevel`, `HasExecutionOrderIssues`, versioned names
   - Called before code generation phase

### Language Specifications

- `docs/language_specification/modules.md` - Module semantics and import system
- `docs/language_specification/classes.md` - Class semantics
- `docs/language_specification/dotnet_interop.md` - .NET interop and naming conventions

---

## Summary

`RoslynEmitter.ModuleClass.cs` is the **module-to-class transformer** in the Sharpy compiler. It generates the top-level static class that represents a Sharpy module, handling the critical task of mapping Python-like module semantics to C# static class semantics.

**Key Takeaways for Newcomers**:

1. **Simplified Architecture**: Module-level variable analysis is handled by `CodeGenInfo` during semantic analysis, not during code generation. This file focuses on generating the Roslyn syntax tree.

2. **Module Class Naming**: Consistent convention - `"Exports"` for libraries, `"Program"` for entry points with `Main()`.

3. **Re-exports Use Delegation**: `from .helpers import func` generates a delegating method that forwards to `Helpers.Exports.Func()`.

4. **Scope Clearing Is Critical**: After generating type/function declarations, scope tracking must be cleared to prevent parameter name leakage.

5. **Enum Pre-registration**: Enums must be registered in the symbol table before generating any other code to support forward references.

6. **Main Generation Logic**: Entry points always get a `Main()` method (even if empty), unless the user defines `main()`.

7. **This Is An Orchestrator**: Most actual code generation is delegated to other partial files. This file orchestrates the overall module structure.

**Next Steps**:
- Read `RoslynEmitter.CompilationUnit.md` to understand how this fits into the top-level compilation unit
- Read `RoslynEmitter.ClassMembers.md` to see how functions are generated within the module class
- Read `CodeGenContext.md` to understand the shared context used across all code generation
