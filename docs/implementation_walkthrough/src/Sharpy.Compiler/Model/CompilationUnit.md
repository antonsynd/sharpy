# Walkthrough: CompilationUnit.cs

**Source File**: `src/Sharpy.Compiler/Model/CompilationUnit.cs`

---

## Overview

`CompilationUnit` is the **fundamental container** that represents a single Sharpy source file (`.spy`) as it flows through the entire compilation pipeline. Think of it as the "passport" that carries everything we learn about a source file from tokenization all the way to C# code generation.

**Role in Pipeline:**
```
Source (.spy) → Lexer → Parser → Semantic Analysis → CodeGen
     ↓            ↓        ↓            ↓               ↓
   Created      Lexed   Parsed   NamesResolved/    CodeGenerated
                                 TypeChecked
```

Every stage of compilation writes its results into the same `CompilationUnit` instance, creating a complete audit trail of what happened to the file.

---

## Why This Design?

The class documentation explicitly states four design goals:

1. **Incremental Compilation**: Track content hashes so we can skip recompiling unchanged files
2. **Parallel Compilation**: Immutable after construction (except for `GeneratedCSharp` setter)
3. **LSP Support**: Store tokens/diagnostics for IDE features (hover, completion, error squiggles)
4. **Debugging**: Preserve source mapping from Sharpy → C# → IL

This makes `CompilationUnit` a **data container**, not a behavior class. It holds state; other classes (Lexer, Parser, TypeChecker, etc.) operate on it.

---

## Class Structure

### Constructor

```csharp
public CompilationUnit(string filePath, string modulePath, string sourceText)
```

**Parameters:**
- `filePath`: Absolute path like `/project/src/mypackage/helpers.spy`
- `modulePath`: Dotted path like `mypackage.helpers` (derived from file location)
- `sourceText`: Raw source code content

**What it does:**
- Validates non-null inputs (throws `ArgumentNullException` if null)
- Computes SHA-256 hash of source for change detection
- Initializes an empty `DiagnosticBag` for collecting errors/warnings
- Sets initial `Phase = CompilationPhase.Created`

**Why SHA-256?** Strong cryptographic hash ensures reliable change detection for caching. Even tiny edits produce completely different hashes.

---

## Property Groups (Regions)

The file organizes properties into logical regions. Let's walk through each:

### 1. Source Information (`#region Source Information`)

```csharp
public string FilePath { get; }
public string ModulePath { get; }
public string SourceText { get; }
public string ContentHash { get; }
```

**Immutable** (init-only or get-only). These never change after construction.

- `FilePath`: Physical location on disk (used for error messages, debugging)
- `ModulePath`: Logical name in import system (`mypackage.helpers` → `using MyPackage.Helpers;` in C#)
- `SourceText`: Original source (needed for error reporting with line/column context)
- `ContentHash`: SHA-256 hex string (used by `IsStale()` for incremental builds)

### 2. Parsing Artifacts (`#region Parsing Artifacts`)

```csharp
public IReadOnlyList<Token>? Tokens { get; internal set; }
public Module? Ast { get; internal set; }
```

**Nullable** until respective compilation phases complete.

- `Tokens`: Output from Lexer (`TokenType.Identifier`, `TokenType.Plus`, etc.)
  - **Why store them?** Future LSP work needs tokens for syntax highlighting, hover info
  - Set during `CompilationPhase.Lexed`
  
- `Ast`: Root AST node (`Module` contains list of `Statement`s)
  - **Why nullable?** If lexing fails, we never get to parsing
  - Set during `CompilationPhase.Parsed`

**Key insight:** These are **internal set** because only compiler infrastructure (Lexer, Parser) should mutate them.

### 3. Semantic Artifacts (`#region Semantic Artifacts`)

```csharp
public Scope? ModuleScope { get; internal set; }
public IReadOnlyList<TypeSymbol> DeclaredTypes { get; internal set; }
public IReadOnlyList<FunctionSymbol> DeclaredFunctions { get; internal set; }
public IReadOnlyList<ImportStatement> Imports { get; internal set; }
public IReadOnlyList<FromImportStatement> FromImports { get; internal set; }
```

**Set during semantic analysis** (NameResolver, TypeChecker):

- `ModuleScope`: Symbol table for this file's top-level declarations
  - Contains all classes, functions, variables defined at module level
  - Nested scopes (function bodies, class bodies) hang off this

- `DeclaredTypes`: Classes/structs/enums/interfaces defined in this file
  - Used to generate C# type declarations
  - Empty list (not null) by default for simpler code elsewhere

- `DeclaredFunctions`: Top-level functions (not methods inside classes)
  - Converted to static methods in generated C#

- `Imports` / `FromImports`: AST nodes representing import statements
  - `import mypackage.helpers` → `ImportStatement`
  - `from mypackage import helper_func` → `FromImportStatement`
  - Extracted during parsing, used during import resolution

**Why separate lists?** Different code generation paths. Types become `class`/`struct` declarations, functions become static methods.

### 4. Dependencies (`#region Dependencies`)

```csharp
public ImmutableHashSet<string> DirectDependencies { get; internal set; }
```

**File paths** (not module paths) that this unit imports:
- Built during import resolution
- Used to determine compilation order (topological sort)
- **Immutable collection** prevents accidental mutation during parallel compilation

**Example:**
```python
# mypackage/main.spy
import mypackage.helpers
from mypackage.utils import format_string
```
→ `DirectDependencies = { "/project/src/mypackage/helpers.spy", "/project/src/mypackage/utils.spy" }`

### 5. Code Generation (`#region Code Generation`)

```csharp
public string? GeneratedCSharp { get; set; }
```

**Only public setter** in the entire class!

- Output from `RoslynEmitter`
- C# source code as a string
- Nullable until `CompilationPhase.CodeGenerated`

**Why public setter?** Code generation happens outside this class, and the emitter needs write access. Everything else is `internal set`.

### 6. Diagnostics (`#region Diagnostics`)

```csharp
public DiagnosticBag Diagnostics { get; }
public bool HasErrors => Diagnostics.HasErrors;
```

- `DiagnosticBag`: Thread-safe collection of errors/warnings
  - Each diagnostic includes message, location (line/column), severity
  - Added throughout compilation (lexer errors, parser errors, type errors, etc.)

- `HasErrors`: Convenience property (checks `Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)`)

**Pattern:** Every compilation stage appends to this bag. If `HasErrors` is true after a stage, later stages may be skipped.

### 7. Metrics (`#region Metrics`)

```csharp
public CompilationMetrics? Metrics { get; internal set; }
```

**Performance tracking** (timing, memory usage per compilation phase):
- Used for profiling and optimization
- Null until compilation starts
- Not critical for correctness, purely observability

### 8. Compilation State (`#region Compilation State`)

```csharp
public CompilationPhase Phase { get; internal set; }
```

**State machine tracker**. See `CompilationPhase` enum below.

---

## Key Methods

### `IsStale(string? cachedHash)`

```csharp
public bool IsStale(string? cachedHash)
{
    if (string.IsNullOrEmpty(cachedHash))
        return true;
    return !string.Equals(ContentHash, cachedHash, StringComparison.Ordinal);
}
```

**Purpose:** Incremental compilation. Determines if a file needs recompilation.

**Logic:**
1. If no cached hash exists → file is stale (needs compilation)
2. Otherwise, compare hashes with **ordinal** (byte-by-byte) comparison
3. Different hash → source changed → stale

**Usage pattern:**
```csharp
var cachedHash = compilationCache.GetHash(filePath);
if (unit.IsStale(cachedHash))
{
    // Recompile this file
}
else
{
    // Use cached output
}
```

### `ComputeHash(string content)` (private static)

```csharp
private static string ComputeHash(string content)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    var hashBytes = sha256.ComputeHash(bytes);
    return Convert.ToHexString(hashBytes);
}
```

**Why private static?** Implementation detail. Callers don't need to know we use SHA-256.

**Why UTF-8?** Sharpy source files are text. UTF-8 handles all Unicode correctly.

**Why `using`?** `SHA256` implements `IDisposable` (native resources). Using ensures cleanup.

**Output:** Hex string like `"A3B2C1..."` (64 characters for SHA-256)

---

## The CompilationPhase Enum

```csharp
public enum CompilationPhase
{
    Created,         // Constructor finished
    Lexed,           // Tokens available
    Parsed,          // AST available
    NamesResolved,   // Symbols declared, ModuleScope populated
    TypeChecked,     // Semantic analysis complete
    CodeGenerated,   // C# output available
    Failed           // Error occurred, compilation halted
}
```

**State transitions:**
```
Created → Lexed → Parsed → NamesResolved → TypeChecked → CodeGenerated
   ↓         ↓       ↓            ↓             ↓             ↓
                                Failed (if HasErrors)
```

**Why track phase?**
- **Debugging:** Know exactly where compilation stopped
- **Error recovery:** Can partially compile even if later stages fail
- **Testing:** Can test individual stages in isolation
- **Parallel compilation:** Know which units are ready for next stage

**Example usage:**
```csharp
if (unit.Phase < CompilationPhase.Parsed)
{
    throw new InvalidOperationException("Cannot type-check before parsing");
}
```

---

## Dependencies on Other Components

### Imported Namespaces

| Namespace | Used For |
|-----------|----------|
| `Sharpy.Compiler.Diagnostics` | `DiagnosticBag` (error/warning collection) |
| `Sharpy.Compiler.Lexer` | `Token` type (lexer output) |
| `Sharpy.Compiler.Parser.Ast` | `Module`, `ImportStatement`, `FromImportStatement` |
| `Sharpy.Compiler.Semantic` | `Scope`, `TypeSymbol`, `FunctionSymbol` |

### Upstream (What creates CompilationUnit?)

- `Compiler.cs`: Single-file compilation
- `AssemblyCompiler.cs`: Multi-file project compilation
- Test harness: `IntegrationTestBase.CompileAndExecute()`

### Downstream (What consumes CompilationUnit?)

- `Lexer.cs`: Reads `SourceText`, writes `Tokens`
- `Parser.cs`: Reads `Tokens`, writes `Ast` and `Imports`/`FromImports`
- `NameResolver.cs`: Reads `Ast`, writes `ModuleScope`, `DeclaredTypes`, `DeclaredFunctions`
- `TypeChecker.cs`: Reads symbols, validates types
- `RoslynEmitter*.cs`: Reads `Ast` + semantic info, writes `GeneratedCSharp`

---

## Patterns and Design Decisions

### 1. **Immutability (Mostly)**

All properties are `internal set` except `GeneratedCSharp`. This enforces single-direction data flow:
```
Compiler → Lexer → Parser → Semantic → CodeGen
         (writes)  (writes)  (writes)    (writes)
```

No component can corrupt earlier stages' output.

### 2. **Nullable Properties for Optional Data**

```csharp
public Module? Ast { get; internal set; }
```

**Alternative designs:**
- ❌ Throw exception if accessed before set → fragile, hard to test
- ❌ Non-nullable with dummy default → confusing, hides initialization bugs
- ✅ Nullable → explicitly models "not yet available"

Pattern: Check `if (unit.Ast != null)` before using.

### 3. **Empty Collections Instead of Null**

```csharp
public IReadOnlyList<TypeSymbol> DeclaredTypes { get; internal set; } = Array.Empty<TypeSymbol>();
```

**Why?** Avoids null checks in foreach loops:
```csharp
// Safe without null check:
foreach (var type in unit.DeclaredTypes)
{
    // ...
}
```

### 4. **Region Organization**

Regions group related properties:
- Makes large class navigable
- Each region represents a compilation stage
- Easy to find "what does semantic analysis produce?" → look in `#region Semantic Artifacts`

### 5. **Internal Setters Enforce Encapsulation**

Only code in `Sharpy.Compiler` assembly can mutate. External consumers (tests, CLI, LSP) get read-only view.

---

## Debugging Tips

### Inspecting a CompilationUnit

**In debugger, check these in order:**

1. **Phase** → Where did compilation stop?
   - `Failed` → Check `Diagnostics`
   - `Parsed` but not `NamesResolved` → Parser succeeded, semantic analysis failed

2. **HasErrors** → Any errors?
   - `true` → Inspect `Diagnostics` collection
   - `false` + `Failed` phase → Logic bug (should have diagnostics)

3. **FilePath/ModulePath** → Is the compiler looking at the right file?
   - Common bug: wrong module path → imports fail

4. **Tokens/Ast/Symbols** → Is expected data present?
   - `Tokens == null` but `Phase == Parsed` → Logic bug
   - `Ast.Statements.Count == 0` → Empty file or parser bug

### Common Issues

**Problem:** "File not found during import resolution"
- **Check:** `ModulePath` matches expected import path
- **Fix:** Ensure module path derivation logic matches import resolution

**Problem:** "Type not found during code generation"
- **Check:** `DeclaredTypes` contains expected types
- **Check:** `ModuleScope` has symbols registered
- **Fix:** Name resolution might have failed silently

**Problem:** "Generated C# doesn't compile"
- **Check:** `GeneratedCSharp` property
- **Debug:** Use `dotnet run --project src/Sharpy.Cli -- emit csharp file.spy` to see output
- **Fix:** Bug in `RoslynEmitter`

### Logging Strategy

```csharp
logger.LogInformation(
    "Compiled {FilePath} → Phase: {Phase}, Errors: {ErrorCount}",
    unit.FilePath,
    unit.Phase,
    unit.Diagnostics.Count(d => d.IsError)
);
```

Logs the "resume" of what happened to this file.

---

## Contribution Guidelines

### When to Modify CompilationUnit

**Add properties when:**
- ✅ New compilation stage needs to persist data (e.g., optimization passes)
- ✅ New LSP feature needs cached data (e.g., symbol usage counts)
- ✅ New incremental compilation strategy needs metadata

**Don't modify when:**
- ❌ Adding behavior → Create separate classes that operate on CompilationUnit
- ❌ Temporary computation → Use local variables in compiler stages
- ❌ Stage-specific logic → Belongs in Lexer/Parser/Semantic classes

### Adding a New Property

**Pattern to follow:**

1. **Choose correct region** (or add new region)
2. **Make it nullable or default-initialized** if not always available
3. **Use `internal set`** unless external mutation required (rare)
4. **Use immutable collections** (`ImmutableHashSet`, `IReadOnlyList`)
5. **Document when it's populated** (which compilation phase)

**Example:**
```csharp
#region Optimization Artifacts

/// <summary>
/// Constant folding results.
/// Null until optimization pass completes.
/// </summary>
public IReadOnlyDictionary<Expression, object>? ConstantValues { get; internal set; }

#endregion
```

### Adding a New Compilation Phase

1. **Add to `CompilationPhase` enum:**
   ```csharp
   public enum CompilationPhase
   {
       // ... existing phases ...
       Optimized,  // New phase
   }
   ```

2. **Add corresponding property region:**
   ```csharp
   #region Optimization Artifacts
   // Properties set during this phase
   #endregion
   ```

3. **Update state machine in compiler:**
   ```csharp
   unit.Phase = CompilationPhase.TypeChecked;
   RunOptimizationPass(unit);
   unit.Phase = CompilationPhase.Optimized;
   ```

### Testing Changes

**Unit test pattern:**
```csharp
[Fact]
public void CompilationUnit_CreatedPhase_InitializesCorrectly()
{
    var unit = new CompilationUnit("/test.spy", "test", "print(42)");
    
    Assert.Equal(CompilationPhase.Created, unit.Phase);
    Assert.NotNull(unit.ContentHash);
    Assert.Empty(unit.Diagnostics);
    Assert.Null(unit.Ast);
}
```

**Integration test pattern:**
```csharp
[Fact]
public void CompiledFile_HasExpectedPhase()
{
    var result = CompileAndExecute("print(42)");
    
    // Access internal unit if needed:
    var unit = GetCompilationUnit(result);
    Assert.Equal(CompilationPhase.CodeGenerated, unit.Phase);
    Assert.False(unit.HasErrors);
}
```

---

## Cross-References

### Related Documentation

This file is a **standalone data model** (not partial), but heavily interacts with:

- **Lexer:** See [`Lexer/Lexer.md`](../Lexer/Lexer.md) - Populates `Tokens` property
- **Parser:** See [`Parser/Parser.md`](../Parser/Parser.md) - Populates `Ast`, `Imports`, `FromImports`
- **Semantic:** See [`Semantic/NameResolver.md`](../Semantic/NameResolver.md) - Populates `ModuleScope`, `DeclaredTypes`, `DeclaredFunctions`
- **CodeGen:** See [`CodeGen/RoslynEmitter.md`](../CodeGen/RoslynEmitter.md) - Populates `GeneratedCSharp`
- **Diagnostics:** See [`Diagnostics/DiagnosticBag.md`](../Diagnostics/DiagnosticBag.md) - Used by `Diagnostics` property

### Files That Import CompilationUnit

```bash
# Find all files that use CompilationUnit:
grep -r "CompilationUnit" src/Sharpy.Compiler/ --include="*.cs" -l
```

Key consumers:
- `src/Sharpy.Compiler/Compiler.cs` - Single-file compilation orchestration
- `src/Sharpy.Compiler/AssemblyCompiler.cs` - Multi-file project compilation
- `src/Sharpy.Compiler/Discovery/*.cs` - Import resolution
- Test files in `src/Sharpy.Compiler.Tests/`

---

## Summary

`CompilationUnit` is the **central data structure** that unifies all compiler stages. Think of it as:

- **A container** that holds everything we know about one `.spy` file
- **A progress tracker** via the `Phase` property
- **An error aggregator** via the `Diagnostics` bag
- **A cache key** via the `ContentHash` property

It's intentionally **data-focused** (not behavior-focused) to keep the compiler pipeline modular. Each stage reads from earlier properties and writes to later properties, maintaining clean separation of concerns.

**Key takeaway for newcomers:** If you're adding a feature, ask "What data does this stage need to persist?" If the answer isn't temporary, it probably belongs in `CompilationUnit`.
