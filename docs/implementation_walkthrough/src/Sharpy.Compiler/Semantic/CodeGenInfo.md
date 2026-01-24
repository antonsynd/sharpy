# Walkthrough: CodeGenInfo.cs

**Source File**: `src/Sharpy.Compiler/Semantic/CodeGenInfo.cs`

---

## Overview

`CodeGenInfo` is a semantic metadata container that bridges the gap between semantic analysis and code generation. It stores pre-computed information about how Sharpy symbols should be emitted as C# code, eliminating the need for the `RoslynEmitter` to recompute names and properties during emission.

**Key Responsibilities:**
- Store C# naming conversions (snake_case → PascalCase/camelCase)
- Track variable versioning for redeclarations (`x`, `x_1`, `x_2`)
- Mark module-level vs. local variables
- Identify execution order issues (variables that can't be static fields)
- Store import metadata (aliased imports, from-imports)
- Reserve fields for future features (async, properties, union types)

**Position in Pipeline:**
```
Parser → NameResolver → TypeResolver → TypeChecker → CodeGenInfoComputer → RoslynEmitter
                                                              ↑
                                                         Creates CodeGenInfo
```

**Design Philosophy:**
This is explicitly called out as a "TWO-WAY DOOR" decision in the code comments, meaning it's purely additive and can be removed without breaking other functionality. It's an optimization that moves work from emission time to semantic analysis time.

---

## Class Structure

### `CodeGenInfo` (sealed record)

A C# record with immutable properties storing metadata for a single symbol.

#### Core Name Fields

```csharp
public required string CSharpName { get; init; }
public required string OriginalName { get; init; }
public int Version { get; init; } = 0;
```

**CSharpName**: The transformed name following C# conventions:
- **Variables (local)**: `camelCase` (e.g., `user_name` → `userName`)
- **Variables (module-level)**: `PascalCase` (e.g., `max_count` → `MaxCount`)
- **Constants**: `CONSTANT_CASE` (e.g., `max_value` → `MAX_VALUE`)
- **Types**: `PascalCase` (e.g., `user_profile` → `UserProfile`)
- **Methods**: `PascalCase` (e.g., `get_data` → `GetData`)
- **Interfaces**: `IPascalCase` (e.g., `comparable` → `IComparable`)

**OriginalName**: The Sharpy source name, preserved for diagnostics and debugging.

**Version**: Handles variable redeclarations. Sharpy allows:
```python
x: int = 5      # Version 0 → "x" in C#
x: str = "hi"   # Version 1 → "x_1" in C#
x: bool = True  # Version 2 → "x_2" in C#
```

This enables Sharpy's dynamic-looking syntax while maintaining C#'s static typing.

#### Symbol Classification Flags

```csharp
public bool IsModuleLevel { get; init; }
public bool IsConstant { get; init; }
public bool HasExecutionOrderIssues { get; init; }
```

**IsModuleLevel**: Distinguishes module-level variables (become static fields) from local variables.

**IsConstant**: Variables declared with `const` keyword:
```python
const MAX_SIZE: int = 100  # → public const int MAX_SIZE = 100;
```

**HasExecutionOrderIssues**: Flags variables that cannot safely be static field initializers. Examples:
- Assignment before declaration: `x = 5` before `x: int = 10`
- Multiple declarations: `x: int = 5` then `x: int = 10`
- Runtime-dependent initializers: `y: int = user_input()` (function call at runtime)
- Transitive dependencies: `y = x` where `x` has issues

Variables with execution order issues are emitted as local variables in `Main()` instead of static fields.

#### Enum-Specific Metadata

```csharp
public bool IsStringEnum { get; init; }
```

**IsStringEnum**: C# enums can only be numeric. String enums are detected during semantic analysis:
```python
enum Color:
    RED = "red"      # String value detected → IsStringEnum = true
    GREEN = "green"
```

String enums are emitted as classes with static readonly fields:
```csharp
public sealed class Color {
    public static readonly string RED = "red";
    public static readonly string GREEN = "green";
}
```

#### Import Metadata

```csharp
public ImportKind ImportKind { get; init; } = ImportKind.None;
public string? OriginalImportName { get; init; }
```

**ImportKind**: Tracks how a symbol entered the current scope:
- `None`: Defined locally
- `ModuleImport`: `import math` (accessed as `math.sqrt`)
- `FromImport`: `from math import sqrt` (accessed as `sqrt`)
- `FromImportWithAlias`: `from math import sqrt as square_root` (accessed as `square_root`)

**OriginalImportName**: For aliased imports, stores the original name:
```python
from config import MAX_VALUE as MAX  # OriginalImportName = "MAX_VALUE"
```

#### Future Extensions (Reserved Fields)

```csharp
public int? UnionDiscriminatorValue { get; init; }      // v0.2.x: Tagged unions
public int? AsyncStateId { get; init; }                 // v0.2.x: Async/await
public string? PropertyAccessorName { get; init; }      // Properties
```

These nullable fields are reserved for future features. They won't affect current functionality and allow backward-compatible extension.

---

## Key Methods

### `GetVersionedCSharpName()`

```csharp
public string GetVersionedCSharpName()
{
    if (Version == 0)
        return CSharpName;
    return $"{CSharpName}_{Version}";
}
```

**Purpose**: Returns the C# identifier including version suffix for redeclared variables.

**Examples:**
- Version 0: `"userName"` → `"userName"`
- Version 1: `"userName"` → `"userName_1"`
- Version 2: `"userName"` → `"userName_2"`

**Usage in CodeGen**: The `RoslynEmitter` calls this method when resolving variable names:
```csharp
var csharpName = symbol.CodeGenInfo.GetVersionedCSharpName();
var identifier = Identifier(csharpName);
```

---

## Supporting Types

### `ImportKind` (enum)

```csharp
public enum ImportKind
{
    None,                   // Defined locally
    ModuleImport,           // import module
    FromImport,             // from module import symbol
    FromImportWithAlias     // from module import symbol as alias
}
```

This enum distinguishes the four import scenarios to guide proper name resolution in the emitter.

---

## Dependencies

### Upstream (Creates CodeGenInfo)

**`CodeGenInfoComputer`** (`CodeGenInfoComputer.cs`):
- Runs after type checking is complete
- Traverses the module AST and creates `CodeGenInfo` for all symbols
- Uses `NameMangler` for name transformations
- Uses `ExecutionOrderAnalyzer` to detect variables with execution order issues
- Attaches `CodeGenInfo` to symbols via `symbol.CodeGenInfo = new CodeGenInfo { ... }`

**`NameMangler`** (`CodeGen/NameMangler.cs`):
- Static utility class for name transformations
- `ToCamelCase()`: local variables
- `ToPascalCase()`: module-level variables, types, methods
- `ToConstantCase()`: constants
- `ToInterfaceName()`: ensures "I" prefix for interfaces

**`ExecutionOrderAnalyzer`** (`ExecutionOrderAnalyzer.cs`):
- Multi-pass analyzer detecting variables with initialization dependencies
- Returns a `HashSet<string>` of variable names with execution order issues
- Used by `CodeGenInfoComputer` to set `HasExecutionOrderIssues` flag

### Downstream (Consumes CodeGenInfo)

**`RoslynEmitter`** (`CodeGen/RoslynEmitter.*.cs`):
- Reads `symbol.CodeGenInfo` during code generation
- Uses `GetVersionedCSharpName()` for identifier resolution
- Checks `IsModuleLevel`, `IsConstant`, `HasExecutionOrderIssues` to determine emission strategy
- Falls back to legacy logic if `CodeGenInfo` is null (for backward compatibility)

**`Symbol`** (`Symbol.cs`):
- Base class for all symbols with a `CodeGenInfo?` property
- Allows symbols to carry code generation metadata

**Related Files:**
- `src/Sharpy.Compiler/Semantic/Symbol.cs` - Symbol base class
- `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` - Creator
- `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs` - Execution order analysis
- `src/Sharpy.Compiler/CodeGen/NameMangler.cs` - Name transformations
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Consumer

---

## Patterns and Design Decisions

### 1. Immutable Record Pattern

`CodeGenInfo` is a `sealed record` with `init`-only properties, making it immutable after construction. This aligns with the compiler's philosophy of immutable AST nodes and data structures.

**Why immutable?**
- Thread-safe by default
- Prevents accidental mutations during multi-pass analysis
- Enables structural equality for testing
- Clear data flow: created once, never modified

### 2. Two-Way Door Decision

The code explicitly documents this as a reversible decision:
```csharp
/// This is a TWO-WAY DOOR decision: CodeGenInfo is purely additive and can be
/// removed without affecting other functionality.
```

**Implications:**
- No other compiler components depend on `CodeGenInfo` existing
- `RoslynEmitter` has fallback logic when `CodeGenInfo` is null
- Can be removed if performance benefits aren't realized
- Low risk architectural change

### 3. Separation of Concerns

**CodeGenInfo** stores *what* to generate (the metadata).
**CodeGenInfoComputer** decides *how* to compute it (the algorithm).
**RoslynEmitter** uses it for *emission* (the output).

This separation allows changing computation logic without changing the data structure or emission logic.

### 4. Forward Compatibility (Reserved Fields)

Nullable fields like `UnionDiscriminatorValue?`, `AsyncStateId?`, and `PropertyAccessorName?` are reserved for future language features.

**Benefits:**
- Avoids breaking changes when adding features
- Documents planned extensions
- Allows gradual implementation

**Trade-offs:**
- Slightly larger struct size
- Potential confusion about unused fields

### 5. Migration Strategy

The codebase is migrating from dynamic name computation (at emission time) to pre-computed names (at semantic analysis time). Comments like this appear throughout:

```csharp
// MIGRATION NOTE: In the future, use SemanticBinding.SetCodeGenInfo/GetCodeGenInfo instead.
// The mutable setter is preserved for backward compatibility during migration.
```

The `Symbol.CodeGenInfo` property uses `{ get; set; }` instead of `{ get; init; }` to allow setting after initial symbol creation. Future versions may move to a separate `SemanticBinding` map.

---

## Debugging Tips

### 1. Inspecting CodeGenInfo

Add a breakpoint in `CodeGenInfoComputer.ComputeForModule()` and inspect symbols after computation:
```csharp
// In debugger:
symbol.CodeGenInfo?.CSharpName
symbol.CodeGenInfo?.Version
symbol.CodeGenInfo?.HasExecutionOrderIssues
```

### 2. Verify Name Transformations

Test name mangling in isolation:
```csharp
// Unit test example
var camel = NameMangler.ToCamelCase("user_name");      // → "userName"
var pascal = NameMangler.ToPascalCase("user_name");    // → "UserName"
var constant = NameMangler.ToConstantCase("max_val");  // → "MAX_VAL"
```

### 3. Trace Execution Order Issues

Enable logging in `ExecutionOrderAnalyzer` to see why a variable has execution order issues:
```csharp
// Add debug output:
if (_variablesWithIssues.Contains(varName))
{
    Console.WriteLine($"Variable '{varName}' has execution order issues");
    // Check _variableFirstSeen, _variableFirstDeclared, etc.
}
```

### 4. Emission Time Fallback

If `CodeGenInfo` is null, `RoslynEmitter` falls back to legacy logic. Check the fallback path:
```csharp
// In RoslynEmitter.cs
private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
{
    var symbol = _context.LookupSymbol(sharpyName);
    if (symbol?.CodeGenInfo == null)
        return null;  // ← Breakpoint here to catch missing CodeGenInfo
    // ...
}
```

### 5. Check Import Resolution

For import-related issues, inspect `ImportKind` and `OriginalImportName`:
```python
# Sharpy code
from config import MAX_SIZE as MAX

# Debug: Check symbol for "MAX"
symbol.CodeGenInfo.ImportKind == ImportKind.FromImportWithAlias
symbol.CodeGenInfo.OriginalImportName == "MAX_SIZE"
```

### 6. Version Tracking Issues

If variable versions are incorrect, check `CodeGenInfoComputer.ProcessModuleLevelVariable()`:
- Are all declarations processed in order?
- Is the version counter incrementing correctly?
- Are local redeclarations handled separately from module-level?

---

## Contribution Guidelines

### When to Modify CodeGenInfo

**Add a new field when:**
- Adding a language feature that requires new emission metadata
- The information is expensive to compute and used multiple times
- The information is determined during semantic analysis, not emission

**Don't add a field if:**
- It can be cheaply computed at emission time
- It's only used once
- It's a temporary value during analysis

### Adding a New Field

1. **Add the field to `CodeGenInfo` record:**
   ```csharp
   public bool MyNewFlag { get; init; }
   ```

2. **Update `CodeGenInfoComputer` to populate it:**
   ```csharp
   symbol.CodeGenInfo = new CodeGenInfo
   {
       // ... existing fields ...
       MyNewFlag = ComputeMyFlag(symbol)
   };
   ```

3. **Use it in `RoslynEmitter`:**
   ```csharp
   if (symbol.CodeGenInfo?.MyNewFlag == true)
   {
       // Emit special case
   }
   ```

4. **Add tests:**
   - Unit test for computation logic
   - Integration test for end-to-end emission

### Handling Execution Order Issues

If you're adding logic that creates module-level variables, check if they have execution order issues:

```csharp
var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
var variablesWithIssues = analyzer.Analyze(module.Body);

if (variablesWithIssues.Contains(varName))
{
    // Variable should be local in Main(), not a static field
    codeGenInfo = new CodeGenInfo
    {
        CSharpName = NameMangler.ToCamelCase(varName),
        IsModuleLevel = false,
        HasExecutionOrderIssues = true
    };
}
```

### Import Handling

When adding new import types (e.g., wildcard imports):

1. **Extend `ImportKind` enum if needed:**
   ```csharp
   public enum ImportKind
   {
       // ... existing values ...
       WildcardImport  // from module import *
   }
   ```

2. **Update `CodeGenInfoComputer.ProcessFromImport()` or add new method:**
   ```csharp
   private void ProcessWildcardImport(FromImportStatement fromImport)
   {
       // Populate CodeGenInfo for wildcard imports
   }
   ```

3. **Handle in `RoslynEmitter` import emission**

### Best Practices

**Do:**
- ✅ Keep fields immutable (`init` not `set`)
- ✅ Use descriptive names (`HasExecutionOrderIssues` not `HasIssues`)
- ✅ Document with XML comments
- ✅ Provide examples in comments
- ✅ Test edge cases (redeclarations, imports, execution order)

**Don't:**
- ❌ Store AST nodes (use indices or names instead)
- ❌ Store mutable collections (use immutable or read-only)
- ❌ Compute values at access time (pre-compute during initialization)
- ❌ Assume `CodeGenInfo` is always present (null-check in consumers)

### Testing Checklist

When modifying `CodeGenInfo` or `CodeGenInfoComputer`:

- [ ] Unit test for new field computation
- [ ] Test with variable redeclarations
- [ ] Test with imports (regular, from, aliased)
- [ ] Test with constants vs. variables
- [ ] Test with module-level vs. local variables
- [ ] Test execution order detection
- [ ] Integration test: Sharpy → C# → compile → run
- [ ] Check fallback behavior if `CodeGenInfo` is null

---

## Cross-References

### Related Documentation

If available, see also:
- **`Symbol.md`** - Symbol base class with `CodeGenInfo` property
- **`CodeGenInfoComputer.md`** - How `CodeGenInfo` is computed
- **`ExecutionOrderAnalyzer.md`** - Execution order issue detection
- **`NameMangler.md`** - Name transformation rules
- **`RoslynEmitter.md`** - How `CodeGenInfo` is consumed during emission
- **`SemanticInfo.md`** - Parallel annotation system for types

### Related Source Files

- **Semantic Analysis:**
  - `src/Sharpy.Compiler/Semantic/Symbol.cs` - Symbol hierarchy
  - `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` - Creator
  - `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs` - Execution order analysis
  - `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Calls `CodeGenInfoComputer`

- **Code Generation:**
  - `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Main consumer
  - `src/Sharpy.Compiler/CodeGen/NameMangler.cs` - Name transformations
  - `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` - Variable emission
  - `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` - Module-level emission

- **Testing:**
  - `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` (if exists)
  - `src/Sharpy.Compiler.Tests/Integration/` - End-to-end tests

---

## Example Usage

### From Sharpy to CodeGenInfo to C#

**Input Sharpy:**
```python
# Module-level constant
const MAX_SIZE: int = 100

# Module-level variable (no execution order issues)
user_count: int = 0

# Variable with execution order issues (assignment before declaration)
total = user_count + MAX_SIZE  # Assignment first
total: int                      # Declaration later

# Function
def calculate_total(amount: int) -> int:
    local_var: int = amount * 2
    return local_var
```

**CodeGenInfo Created:**

| Symbol | CSharpName | Version | IsModuleLevel | IsConstant | HasExecutionOrderIssues |
|--------|-----------|---------|---------------|------------|------------------------|
| `MAX_SIZE` | `MAX_SIZE` | 0 | true | true | false |
| `user_count` | `UserCount` | 0 | true | false | false |
| `total` (first) | `total` | 0 | false | false | true |
| `total` (second) | `total` | 1 | false | false | true |
| `calculate_total` | `CalculateTotal` | 0 | true | false | false |
| `local_var` | `localVar` | 0 | false | false | false |

**Output C#:**
```csharp
public static class Program
{
    public const int MAX_SIZE = 100;           // Constant: CONSTANT_CASE
    public static int UserCount = 0;           // Module var: PascalCase

    public static int CalculateTotal(int amount)
    {
        int localVar = amount * 2;             // Local var: camelCase
        return localVar;
    }

    public static void Main()
    {
        // Variables with execution order issues become locals in Main()
        int total = UserCount + MAX_SIZE;      // Version 0
        int total_1 = total;                   // Version 1 (redeclaration)
    }
}
```

---

## Summary

`CodeGenInfo` is the **metadata bridge** between semantic analysis and code generation. It pre-computes emission details during type checking, allowing the `RoslynEmitter` to focus on syntax generation rather than name resolution and symbol classification.

**Key takeaways:**
- Immutable record storing per-symbol emission metadata
- Handles C# naming conventions, versioning, and import tracking
- Detects execution order issues to guide static field vs. local variable emission
- Designed as a reversible optimization ("two-way door")
- Extensible via reserved fields for future language features

For newcomers, understanding `CodeGenInfo` is essential for:
1. **Reading emission code** - Why does the emitter generate certain names?
2. **Adding language features** - Where to store new emission metadata?
3. **Debugging** - Why is a variable emitted as a local vs. static field?

Start by tracing how `CodeGenInfoComputer` creates `CodeGenInfo` for a simple Sharpy program, then follow how `RoslynEmitter` consumes it during emission.
