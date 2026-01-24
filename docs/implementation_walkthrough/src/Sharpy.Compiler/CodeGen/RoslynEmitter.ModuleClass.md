# Walkthrough: RoslynEmitter.ModuleClass.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

---

## Overview

`RoslynEmitter.ModuleClass.cs` is a partial class file that handles the generation of the **module-level structure** in Sharpy's code generation phase. This file is responsible for transforming a Sharpy module (a `.spy` source file) into C# code, specifically:

1. **Module Class Generation**: Creates a static C# class (typically named `Exports` or `Program`) that contains module-level functions, fields, and constants
2. **Namespace-Level Type Placement**: Places user-defined types (classes, structs, interfaces, enums) at the namespace level as siblings to the module class, following C# conventions
3. **Entry Point Handling**: Generates a `Main()` method for entry point files or handles user-defined `main()` functions
4. **Re-Export Support**: Creates delegating members for `from ... import` statements to enable symbol re-export

This file is part of the final phase of the Sharpy compiler pipeline, where the typed AST produced by semantic analysis is converted into executable C# code using Roslyn's `SyntaxFactory` API.

---

## Class/Type Structure

This file extends the `RoslynEmitter` partial class (defined in `RoslynEmitter.cs`). It adds module-specific code generation methods without introducing new types.

### Key Fields Used

From the main `RoslynEmitter` class:

- **`_context: CodeGenContext`** — Provides access to semantic analysis results, symbol tables, error reporting, and compilation settings
- **`_typeMapper: TypeMapper`** — Converts Sharpy semantic types to C# Roslyn type syntax
- **`_moduleFieldNames: HashSet<string>`** — Tracks already-emitted C# field names to prevent duplicates
- **`_forceModuleLevelFields: bool`** — When `true`, forces variables with execution order issues to be static fields (set when user defines `main()`)
- **`_declaredVariables: HashSet<string>`** — Tracks locally declared variables within the current scope
- **`_variableVersions: Dictionary<string, int>`** — Manages variable name versioning for redeclarations (e.g., `x`, `x_1`, `x_2`)
- **`_constVariables: HashSet<string>`** — Tracks const variable names in the current scope
- **`_interfaceDefinitions: Dictionary<string, InterfaceDef>`** — Maps interface names to their AST nodes for abstract class stub generation

---

## Key Functions/Methods

### 1. `GenerateModuleMembers()`

**Signature:**
```csharp
private (ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes)
    GenerateModuleMembers(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
```

**Purpose:** The primary orchestrator for module code generation. Processes all module-level statements and returns both:
- The module class (static class containing functions/fields)
- Namespace-level types (classes, structs, interfaces, enums)

**Key Steps:**

1. **Initialization** (lines 95-130):
   - Clears tracking fields (`_moduleFieldNames`, `_interfaceDefinitions`)
   - Pre-scans for interfaces (needed for abstract class stub generation)
   - Registers enums in the symbol table for member access resolution

2. **Statement Classification** (lines 136-190):
   - **Module declarations**: Functions, fields, constants → go into the module class
   - **Namespace types**: Classes, structs, interfaces, enums → go at namespace level
   - **Executable statements**: Top-level code → wrapped in `Main()` or becomes local variables

3. **Main Method Generation** (lines 192-245):
   - **No user `main()`** + **is entry point**: Generate `Main()` with executable statements
   - **User `main()` exists**: Don't generate `Main()`, but validate no conflicting top-level code
   - **Non-entry point**: Warn and ignore executable statements

4. **Re-Export Handling** (lines 261-267):
   - Generates delegating members for `from .module import symbol` statements

5. **Module Class Construction** (lines 270-279):
   - Determines class name (`Program` for entry points, `Exports` otherwise)
   - Creates static class with all module declarations

**Implementation Details:**

```csharp
// Classification logic (simplified)
foreach (var stmt in statements)
{
    var member = GenerateStatement(stmt);

    if (stmt is ClassDef or StructDef or InterfaceDef or EnumDef)
    {
        namespaceTypes.Add(member);  // Types go at namespace level
    }
    else if (member is MemberDeclarationSyntax)
    {
        moduleDeclarations.Add(member);  // Functions/fields go in module class
    }
    else
    {
        executableStatements.Add(stmt);  // Top-level code goes in Main()
    }
}
```

**Connects to:**
- **Upstream**: Called by `GenerateCompilationUnit()` (in `RoslynEmitter.CompilationUnit.cs`)
- **Downstream**: Calls `GenerateStatement()`, `GenerateFunctionDeclaration()`, type generators

---

### 2. `ConvertModuleNameToNamespace()`

**Signature:**
```csharp
private string ConvertModuleNameToNamespace(string moduleName)
```

**Purpose:** Converts Sharpy module names (Python-style snake_case with dots) to C# namespace naming conventions (PascalCase with dots).

**Examples:**
- `"system.io"` → `"System.IO"`
- `"my_module.sub_module"` → `"MyModule.SubModule"`

**Why Not Use NameMangler?**

The comment on lines 22-24 explains:
> We don't use NameMangler.Transform here because:
> 1. It tracks unique names which causes "system" to become System, System1, System2, etc.
> 2. Namespaces should use simple PascalCase without uniqueness tracking

**Implementation:**
```csharp
var parts = moduleName.Split('.', StringSplitOptions.RemoveEmptyEntries);
var convertedParts = parts.Select(part => SimpleToPascalCase(part));
return string.Join(".", convertedParts);
```

---

### 3. `SimpleToPascalCase()`

**Signature:**
```csharp
private static string SimpleToPascalCase(string name)
```

**Purpose:** Converts a single identifier to PascalCase, handling special cases like acronyms, literal names, and invalid characters.

**Special Handling:**

1. **Literal Names** (lines 37-42): Backtick-escaped names like `` `using` `` are unwrapped
2. **Acronyms** (lines 44-48): Known acronyms (`io`, `ui`, `xml`, etc.) become uppercase (`IO`, `UI`, `XML`)
3. **Invalid Characters** (lines 51-59): Non-identifier characters replaced with underscores
4. **Edge Cases** (lines 73-76): Names starting with digits get `_` prefix

**Algorithm:**
```csharp
// Split by underscores and capitalize each part
var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
var result = string.Join("", parts.Select(p =>
    char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
));
```

---

### 4. `GetModuleClassName()`

**Signature:**
```csharp
private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
```

**Purpose:** Determines the C# class name for the module.

**Naming Rules:**
- **Entry point with `Main()` method**: `"Program"` (standard C# convention)
- **Non-entry point or library module**: `"Exports"` (matches import system expectations)

**Why "Exports"?**

The comment explains:
> This matches the spec and import system expectation:
> `using config = ProjectNamespace.Config.Exports;`

This enables Sharpy's import statement `import config` to resolve to `ProjectNamespace.Config.Exports` in the generated C# code.

---

### 5. `GenerateReExportMembers()`

**Signature:**
```csharp
private IEnumerable<MemberDeclarationSyntax> GenerateReExportMembers(FromImportStatement fromImport)
```

**Purpose:** Creates delegating members for re-exported symbols from `from ... import` statements.

**Example:**
```python
# Sharpy code
from .helpers import utility_func, CONSTANT_VALUE
```

**Generated C# (simplified):**
```csharp
public static class Exports
{
    // Delegating method for utility_func
    public static void UtilityFunc(int x) => Helpers.Exports.UtilityFunc(x);

    // Delegating property for CONSTANT_VALUE
    public static string CONSTANT_VALUE => Helpers.Exports.CONSTANT_VALUE;
}
```

**Handles:**
- **Functions**: Generates delegating methods (`GenerateReExportMethod()`)
- **Variables/Constants**: Generates delegating properties (`GenerateReExportProperty()`)
- **Types**: Skipped (handled by type aliases in the import system)

---

### 6. `GenerateReExportMethod()`

**Signature:**
```csharp
private MemberDeclarationSyntax GenerateReExportMethod(string localName, FunctionSymbol funcSymbol, string sourceClassName)
```

**Purpose:** Creates a public static method that delegates to an imported function.

**Implementation Pattern:**
```csharp
// Input: FunctionSymbol for 'utility_func(x: int, y: str) -> bool'
// Output C# method:
public static bool UtilityFunc(int x, string y)
{
    return Helpers.Exports.UtilityFunc(x, y);
}
```

**Key Steps:**
1. Map parameter names using `NameMangler` (snake_case → camelCase)
2. Map parameter types using `TypeMapper`
3. Map return type
4. Build invocation expression targeting source module's `Exports` class
5. Generate return statement (or expression statement for void)

---

### 7. `GenerateReExportProperty()`

**Signature:**
```csharp
private MemberDeclarationSyntax GenerateReExportProperty(string localName, VariableSymbol varSymbol, string sourceClassName)
```

**Purpose:** Creates a public static property (expression-bodied) that delegates to an imported variable/constant.

**Implementation Pattern:**
```csharp
// Input: VariableSymbol for 'CONSTANT_VALUE' (ALL_CAPS name)
// Output C# property:
public static string CONSTANT_VALUE => Helpers.Exports.CONSTANT_VALUE;
```

**Naming Logic:**
- **ALL_CAPS names** (constants): Preserve constant case using `NameMangler.ToConstantCase()`
- **Regular names**: Convert to PascalCase using `NameMangler.ToPascalCase()`

---

### 8. `GenerateStatement()`

**Signature:**
```csharp
private SyntaxNode? GenerateStatement(Statement stmt)
```

**Purpose:** Dispatches statement generation to the appropriate handler based on AST node type.

**Routing Table:**
```csharp
return stmt switch
{
    FunctionDef     => GenerateFunctionDeclaration(funcDef),
    ClassDef        => GenerateClassDeclaration(classDef),
    StructDef       => GenerateStructDeclaration(structDef),
    InterfaceDef    => GenerateInterfaceDeclaration(interfaceDef),
    EnumDef         => GenerateEnumDeclaration(enumDef),
    VariableDeclaration => GenerateModuleLevelField(varDecl),
    TypeAlias       => null,  // Compile-time only, no C# output
    ReturnStatement => GenerateReturn(ret),
    Assignment      => GenerateAssignment(assign),
    _               => null
};
```

**Note:** This method delegates to specialized generators defined in other partial class files.

---

## Dependencies

### Internal Sharpy Dependencies

1. **`Sharpy.Compiler.Parser.Ast`** — AST node types (`FunctionDef`, `ClassDef`, `Statement`, etc.)
2. **`Sharpy.Compiler.Semantic`** — Semantic analysis types:
   - `CodeGenContext` — Compilation context, symbol tables, error reporting
   - `SemanticType`, `FunctionSymbol`, `VariableSymbol`, `TypeSymbol` — Type system
   - `CodeGenInfo` — Pre-computed code generation metadata

### External Dependencies

1. **`Microsoft.CodeAnalysis.CSharp`** — Roslyn C# compiler API
2. **`Microsoft.CodeAnalysis.CSharp.Syntax`** — Syntax tree node types
3. **`SyntaxFactory`** (static import) — Factory methods for building syntax trees

### Related Partial Class Files

- **`RoslynEmitter.cs`** — Main class definition, fields, helper methods
- **`RoslynEmitter.CompilationUnit.cs`** — Top-level compilation unit generation (calls `GenerateModuleMembers()`)
- **`RoslynEmitter.TypeDeclarations.cs`** — Class, struct, interface, enum generation
- **`RoslynEmitter.Statements.cs`** — Statement generation (function bodies, control flow)
- **`RoslynEmitter.Expressions.cs`** — Expression generation (calls, operators, literals)
- **`RoslynEmitter.ClassMembers.cs`** — Class member generation (methods, properties, fields)

---

## Patterns and Design Decisions

### 1. **Namespace-Level Types (Lines 82-89, 163-167)**

**Design Decision:** User-defined types are placed at the namespace level as **siblings** to the module class, not nested within it.

**Rationale:**
```csharp
// Generated structure:
namespace MyProject.MyModule
{
    public class UserDefinedClass { }  // At namespace level

    public static class Exports         // Module class at namespace level
    {
        public static void MyFunction() { }
    }
}
```

This matches C# conventions where top-level types are namespace siblings, not nested in a container class. It simplifies type references and interop with existing C# code.

---

### 2. **Dual Return Pattern**

The method `GenerateModuleMembers()` returns a tuple:
```csharp
(ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes)
```

This separates concerns:
- **Module class**: Contains executable code (functions, Main)
- **Namespace types**: Pure type definitions (classes, structs)

The caller (`GenerateCompilationUnit()`) places both in the namespace correctly.

---

### 3. **Entry Point Detection and Main Generation (Lines 192-245)**

**Complex Logic for Main():**

| Scenario | Action |
|----------|--------|
| No `main()`, is entry point, has executable code | Generate `Main()` with statements |
| No `main()`, is entry point, no executable code | Generate empty `Main()` |
| User `main()` exists, has executable code | **Error** — can't mix `main()` with top-level code |
| User `main()` exists, no executable code | No `Main()` generated |
| Not entry point, has executable code | **Warning** — ignore statements |

**Why Generate Empty Main()?**

Line 209 comment explains:
> We generate Main even if there are no executable statements, to support entry points that only contain imports or declarations

This ensures every entry point file has a valid C# entry point.

---

### 4. **Scope Tracking Lifecycle (Lines 154-159)**

After generating type or function declarations, the emitter clears local scope tracking:
```csharp
if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef)
{
    _declaredVariables.Clear();
    _variableVersions.Clear();
    _constVariables.Clear();
}
```

**Why?** Parameter names and local variables from class methods must not leak into module-level code. Each declaration starts with a clean scope.

---

### 5. **CodeGenInfo Integration (Lines 97-99)**

The comment highlights a shift in architecture:
> Module variable tracking is now handled by CodeGenInfo during semantic analysis.
> The CodeGenInfoComputer.ComputeForModule method sets CodeGenInfo.IsModuleLevel, etc.

This separates concerns:
- **Semantic analysis** (pre-compute): Determines which variables are module-level, have execution order issues, etc.
- **Code generation** (this file): Uses pre-computed metadata to generate correct C# code

---

### 6. **NameMangler vs. SimpleToPascalCase (Lines 22-24)**

**Design Decision:** Don't use `NameMangler` for namespace conversion.

**Why?** `NameMangler` tracks unique names to avoid collisions. For namespaces, this would cause:
```
system → System
system (second occurrence) → System1
system (third occurrence) → System2
```

Namespaces should have **stable, predictable names** without version suffixes.

---

## Debugging Tips

### 1. **Inspect Generated C# Code**

Use the CLI's `emit` command to see generated C# for a Sharpy file:
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy
```

This shows exactly what `GenerateModuleMembers()` produces.

---

### 2. **Module Class vs. Namespace Types**

If types aren't accessible or imports fail:
- Check if types are in `namespaceTypes` list (should be at namespace level)
- Check if functions/fields are in `moduleClass` (should be in `Exports` class)

Verify with:
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy | grep "class Exports" -A 20
```

---

### 3. **Main Method Generation Issues**

**Symptom:** "Entry point not found" or multiple `Main()` methods.

**Debug:**
1. Check `_context.IsEntryPoint` — is this file marked as entry point?
2. Check `hasMainFunction` — does the file define `main()`?
3. Line 250: Both cases generate a method named `Main` in C#

**Expected behavior:**
- User `main()` → transformed to C# `Main()`
- No user `main()` + entry point → synthesized `Main()`

---

### 4. **Re-Export Resolution**

**Symptom:** Re-exported symbols not found or wrong signatures.

**Check:**
1. Does `GetReExportedSymbols(fromImport)` return the correct symbols?
2. Does `GetResolvedModulePath(fromImport)` resolve the source module correctly?
3. Is the source module's `Exports` class accessible?

**Trace:** Add logging in `GenerateReExportMembers()` to see what's being re-exported.

---

### 5. **Variable Redefinition Handling**

**Symptom:** Duplicate field declarations or missing local variables.

**Lines 174-183:** Variable redefinitions return `null` from `GenerateModuleLevelField()`, then become executable statements for `Main()`.

**Check:**
- Is `_moduleFieldNames` tracking field names correctly?
- Are const redefinitions being skipped? (Line 183)
- Are non-const redefinitions added to `executableStatements`?

---

### 6. **Scope Leakage**

**Symptom:** Variables from class methods appear at module level or cause name collisions.

**Lines 154-159:** Scope tracking is cleared after each type/function declaration.

**Verify:**
- Are `_declaredVariables`, `_variableVersions`, `_constVariables` being cleared at the right times?
- Check around line 200-203 where `Main()` scope is reset.

---

## Contribution Guidelines

### When to Modify This File

You should edit `RoslynEmitter.ModuleClass.cs` when:

1. **Changing Module Structure**:
   - Altering how types are organized (namespace-level vs. nested)
   - Changing module class naming (`Exports` vs. `Program`)
   - Modifying re-export behavior

2. **Entry Point Handling**:
   - Changing `Main()` generation logic
   - Handling new module-level execution scenarios
   - Adjusting entry point detection

3. **Import/Export Semantics**:
   - Adding support for new import patterns
   - Changing how re-exported symbols are delegated
   - Modifying namespace resolution

4. **Namespace Naming**:
   - Adding new acronym mappings
   - Changing snake_case → PascalCase conversion rules

### Critical Rules

From `CLAUDE.md`:

1. **Never modify expected values to make tests pass** — Fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — No string templating or string-based code generation
3. **Immutable AST** — Don't modify AST nodes; annotations go in `SemanticInfo`

### Testing Changes

After modifying this file:

```bash
# Run code generation tests
dotnet test --filter "FullyQualifiedName~CodeGen"

# Run full integration tests (file-based)
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Inspect generated C# for a specific test case
dotnet run --project src/Sharpy.Cli -- emit csharp path/to/test.spy
```

### Common Pitfalls

1. **Forgetting to Clear Scope Tracking**: Always clear `_declaredVariables`, `_variableVersions`, `_constVariables` when entering new scope contexts
2. **Hardcoding Names**: Use `NameMangler` for name transformations, not manual string manipulation
3. **String Building**: Never build C# code as strings — use `SyntaxFactory` methods exclusively
4. **Ignoring CodeGenInfo**: Check if semantic analysis already computed metadata before adding runtime tracking

---

## Cross-References

This file is part of the `RoslynEmitter` partial class. Related files:

### Same Partial Class
- [`RoslynEmitter.md`](./RoslynEmitter.md) — Main class definition, fields, core utilities
- [`RoslynEmitter.CompilationUnit.md`](./RoslynEmitter.CompilationUnit.md) — Top-level compilation unit generation (calls `GenerateModuleMembers`)
- [`RoslynEmitter.TypeDeclarations.md`](./RoslynEmitter.TypeDeclarations.md) — Class, struct, interface, enum generation
- [`RoslynEmitter.Statements.md`](./RoslynEmitter.Statements.md) — Statement generation (control flow, assignments)
- [`RoslynEmitter.Expressions.md`](./RoslynEmitter.Expressions.md) — Expression generation
- [`RoslynEmitter.ClassMembers.md`](./RoslynEmitter.ClassMembers.md) — Class member generation

### Related Components
- **TypeMapper** ([`TypeMapper.md`](./TypeMapper.md)) — Used for converting Sharpy types to C# Roslyn types
- **NameMangler** ([`NameMangler.md`](./NameMangler.md)) — Name transformation utilities (snake_case → camelCase/PascalCase)
- **CodeGenContext** ([`CodeGenContext.md`](./CodeGenContext.md)) — Provides semantic analysis results and compilation settings

### Language Specifications
- [`docs/language_specification/classes.md`](../../../../language_specification/classes.md) — Class semantics
- [`docs/language_specification/class_methods.md`](../../../../language_specification/class_methods.md) — Method semantics
- [`docs/language_specification/constructors.md`](../../../../language_specification/constructors.md) — Constructor handling
- [`docs/language_specification/dotnet_interop.md`](../../../../language_specification/dotnet_interop.md) — .NET interoperability rules
- [`docs/language_specification/inheritance.md`](../../../../language_specification/inheritance.md) — Inheritance semantics

---

## Summary

`RoslynEmitter.ModuleClass.cs` is the architectural cornerstone of Sharpy's module-to-C# transformation. It orchestrates the generation of:

- **Static module classes** (`Exports` or `Program`) containing functions and fields
- **Namespace-level type declarations** (classes, structs, interfaces, enums)
- **Entry point methods** (`Main()`) with proper executable statement handling
- **Re-export delegation** for import system support

The file demonstrates sophisticated separation of concerns, leveraging semantic analysis pre-computation (`CodeGenInfo`) while maintaining runtime tracking for local scopes. Its dual-return pattern elegantly separates module members from namespace types, enabling idiomatic C# output that matches both Sharpy's Python-like syntax and C#'s structural conventions.
