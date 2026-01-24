# Walkthrough: SemanticBinding.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticBinding.cs`

---

## Overview

`SemanticBinding` is a **semantic data storage layer** introduced to the Sharpy compiler as part of an architectural evolution toward **immutable AST design**. While `SemanticInfo` stores mappings from AST nodes (expressions, identifiers) to their semantic properties, `SemanticBinding` stores metadata about **symbols themselves**—particularly information computed *after* initial symbol creation but *before* code generation.

**Key Insight**: In the Sharpy compiler, symbols are created early (during `NameResolver`) but their complete semantic information isn't known until later phases (during `TypeChecker`, `TypeResolver`, and `CodeGenInfoComputer`). Originally, this data was stored directly on `Symbol` objects via mutable properties. `SemanticBinding` represents a migration toward **separating mutable semantic data from symbol definitions**.

**Role in the Compilation Pipeline**:
```
Source → Lexer → Parser (AST) → NameResolver (creates symbols) → TypeResolver → TypeChecker → CodeGenInfoComputer
                                          ↓                            ↓            ↓              ↓
                                    Symbol objects              SemanticBinding (stores computed semantic data)
                                    (immutable)                 (separate from symbols)
```

Think of `SemanticBinding` as a **companion database** that travels alongside the symbol table, storing information that:
1. **Isn't known at symbol creation time** (e.g., resolved base types after inheritance resolution)
2. **Is computed during semantic analysis** (e.g., CodeGenInfo for name mangling)
3. **Should be kept separate from AST/Symbol definitions** (for future LSP/incremental compilation support)

---

## Class Structure

### Main Class: `SemanticBinding`

```csharp
public class SemanticBinding
```

A thread-safe storage container that maintains four concurrent dictionaries mapping symbols/AST nodes to their computed semantic properties.

### Design Philosophy: Why ConcurrentDictionary?

All internal storage uses `ConcurrentDictionary<TKey, TValue>`:

```csharp
private readonly ConcurrentDictionary<Symbol, CodeGenInfo> _codeGenInfo = new();
private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes = new();
```

**Rationale**:
- **Thread-safe parallel compilation**: Multiple `.spy` files can be compiled simultaneously
- **Future-proof for LSP**: Language servers need concurrent access during incremental edits
- **Safe for multiple bindings**: The same AST could theoretically have multiple semantic bindings (e.g., different versions with different type resolutions)

**Trade-off**: Slightly higher memory overhead than regular `Dictionary`, but worth it for parallelization benefits.

---

## Internal State: Four Core Dictionaries

### 1. CodeGenInfo Map

```csharp
private readonly ConcurrentDictionary<Symbol, CodeGenInfo> _codeGenInfo = new();
```

**Purpose**: Stores code generation metadata for symbols—C# names, version numbers, import information, etc.

**Key Type**: Any `Symbol` (VariableSymbol, FunctionSymbol, TypeSymbol, etc.)

**Value Type**: `CodeGenInfo` - Contains:
- `CSharpName`: The mangled C# identifier (e.g., `snake_case` → `SnakeCase`)
- `OriginalName`: The original Sharpy name (for diagnostics)
- `Version`: For variable redeclaration (0 for first, 1+ for subsequent)
- `IsModuleLevel`: Whether this becomes a static field
- `IsConstant`: Whether to emit as `const` in C#
- `ImportKind`: How the symbol was imported
- And more...

**When Populated**: During `CodeGenInfoComputer` pass (after type checking)

**Example Usage**:
```csharp
// During semantic analysis
var symbol = new VariableSymbol { Name = "user_count", ... };
var codeGenInfo = new CodeGenInfo 
{ 
    CSharpName = "UserCount",
    OriginalName = "user_count",
    IsModuleLevel = true
};
semanticBinding.SetCodeGenInfo(symbol, codeGenInfo);

// During code generation
var info = semanticBinding.GetCodeGenInfo(symbol);
var csharpName = info?.CSharpName ?? symbol.Name; // "UserCount"
```

**Migration Note**: Originally stored as `Symbol.CodeGenInfo` (mutable property). Gradually migrating to use `SemanticBinding` instead for cleaner separation.

---

### 2. Variable Types Map

```csharp
private readonly ConcurrentDictionary<VariableSymbol, SemanticType> _variableTypes = new();
```

**Purpose**: Stores the resolved types for variable symbols.

**Key Type**: `VariableSymbol` only (not generic `Symbol`)

**Value Type**: `SemanticType` - Could be:
- `BuiltinType` (int, str, float)
- `UserDefinedType` (custom classes)
- `GenericType` (list[int], dict[str, int])
- `OptionalType` (int?, str?)
- `UnknownType` (for error recovery)

**When Populated**: During `TypeChecker` and `TypeResolver` phases

**Example Usage**:
```python
# Sharpy source
x: int = 42
y = "hello"  # Type inference
```

```csharp
// During type checking
var xSymbol = new VariableSymbol { Name = "x", ... };
semanticBinding.SetVariableType(xSymbol, SemanticType.Int);

var ySymbol = new VariableSymbol { Name = "y", ... };
var inferredType = InferType("hello");  // Returns SemanticType.Str
semanticBinding.SetVariableType(ySymbol, inferredType);

// Later retrieval
var xType = semanticBinding.GetVariableType(xSymbol);  // SemanticType.Int
```

**Default Behavior**: `GetVariableType()` returns `SemanticType.Unknown` if not set (safe fallback for error recovery).

**Migration Note**: Originally stored as `VariableSymbol.Type` (mutable property). Use `SemanticBinding` for new code.

---

### 3. Base Types Map

```csharp
private readonly ConcurrentDictionary<TypeSymbol, TypeSymbol> _baseTypes = new();
```

**Purpose**: Stores inheritance relationships—which class inherits from which.

**Key Type**: `TypeSymbol` (the derived class)

**Value Type**: `TypeSymbol` (the base/parent class)

**When Populated**: During `NameResolver.ResolveInheritance()` (Pass 2 of semantic analysis)

**Example Usage**:
```python
# Sharpy source
class Animal:
    pass

class Dog(Animal):
    pass

class Husky(Dog):
    pass
```

```csharp
// During inheritance resolution
var animalSymbol = new TypeSymbol { Name = "Animal", ... };
var dogSymbol = new TypeSymbol { Name = "Dog", ... };
var huskySymbol = new TypeSymbol { Name = "Husky", ... };

semanticBinding.SetBaseType(dogSymbol, animalSymbol);    // Dog → Animal
semanticBinding.SetBaseType(huskySymbol, dogSymbol);     // Husky → Dog

// Later queries
var dogBase = semanticBinding.GetBaseType(dogSymbol);     // animalSymbol
var huskyBase = semanticBinding.GetBaseType(huskySymbol); // dogSymbol
var animalBase = semanticBinding.GetBaseType(animalSymbol); // null (no base)
```

**Use Cases**:
- **Method resolution**: Find inherited methods
- **Type compatibility**: Check if type A is assignable to type B
- **Virtual method dispatch**: Determine if method is overriding base method

**Migration Note**: Originally stored as `TypeSymbol.BaseType` (mutable property).

---

### 4. Module Resolution Maps

Two dictionaries for import statement data:

#### 4a. Resolved Module Paths

```csharp
private readonly ConcurrentDictionary<FromImportStatement, string> _resolvedModulePaths = new();
```

**Purpose**: Maps `from X import Y` statements to the actual file path or .NET assembly name of module `X`.

**Key Type**: `FromImportStatement` (AST node)

**Value Type**: `string` - Either:
- File path: `/path/to/project/utils.spy`
- .NET assembly: `System.Collections.Generic`

**When Populated**: During `ImportResolver` phase (early in semantic analysis)

**Example**:
```python
# Sharpy source
from utils import helper
from collections import list
```

```csharp
// During import resolution
var utilsImport = /* FromImportStatement for "from utils import helper" */;
var resolvedPath = "/Users/anton/project/utils.spy";
semanticBinding.SetResolvedModulePath(utilsImport, resolvedPath);

var collectionsImport = /* FromImportStatement for "from collections import list" */;
semanticBinding.SetResolvedModulePath(collectionsImport, "Sharpy.Core.Collections");

// Later retrieval (e.g., in code generator)
var path = semanticBinding.GetResolvedModulePath(utilsImport);  // "/Users/.../utils.spy"
```

#### 4b. Re-Exported Symbols

```csharp
private readonly ConcurrentDictionary<FromImportStatement, Dictionary<string, Symbol>> _reExportedSymbols = new();
```

**Purpose**: When a module re-exports symbols from another module (e.g., `from .submodule import func`), tracks which symbols are being imported.

**Key Type**: `FromImportStatement` (AST node)

**Value Type**: `Dictionary<string, Symbol>` - Maps imported names to their symbols

**When Populated**: During `ImportResolver` when processing imports

**Example**:
```python
# utils/__init__.spy
from .helpers import format_text, parse_date

# main.spy
from utils import format_text  # Re-exported symbol
```

```csharp
// In utils/__init__.spy processing
var helpersImport = /* FromImportStatement for "from .helpers import ..." */;
var reExportedSymbols = new Dictionary<string, Symbol>
{
    ["format_text"] = formatTextSymbol,  // FunctionSymbol from helpers.spy
    ["parse_date"] = parseDateSymbol     // FunctionSymbol from helpers.spy
};
semanticBinding.SetReExportedSymbols(helpersImport, reExportedSymbols);

// When main.spy imports from utils
var symbols = semanticBinding.GetReExportedSymbols(helpersImport);
var formatTextSymbol = symbols["format_text"];  // Get the original symbol
```

**Use Case**: Enables proper symbol tracking across multi-file imports and package re-exports.

---

## Key Methods

All methods follow a simple **Set/Get pattern** with null-safe returns.

### CodeGenInfo Methods

#### `SetCodeGenInfo(Symbol symbol, CodeGenInfo info)`

```csharp
public void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
    => _codeGenInfo[symbol] = info;
```

**Purpose**: Associate code generation metadata with a symbol.

**Thread-Safe**: Yes (uses ConcurrentDictionary)

**Typical Call Site**: `CodeGenInfoComputer.cs` after type checking

**Example**:
```csharp
// In CodeGenInfoComputer.cs
foreach (var symbol in moduleSymbols)
{
    var info = ComputeCodeGenInfo(symbol);
    semanticBinding.SetCodeGenInfo(symbol, info);
}
```

#### `GetCodeGenInfo(Symbol symbol)`

```csharp
public CodeGenInfo? GetCodeGenInfo(Symbol symbol)
    => _codeGenInfo.TryGetValue(symbol, out var info) ? info : null;
```

**Returns**: `CodeGenInfo` if set, `null` otherwise

**Typical Call Site**: `RoslynEmitter.cs` during C# code generation

**Example**:
```csharp
// In RoslynEmitter.cs
private string GetCSharpName(Symbol symbol)
{
    var info = _semanticBinding.GetCodeGenInfo(symbol);
    if (info != null)
        return info.GetVersionedCSharpName();  // Includes version suffix
    
    // Fallback to default name mangling
    return NameMangler.Mangle(symbol.Name);
}
```

#### `HasCodeGenInfo(Symbol symbol)`

```csharp
public bool HasCodeGenInfo(Symbol symbol)
    => _codeGenInfo.ContainsKey(symbol);
```

**Returns**: `true` if CodeGenInfo has been set for this symbol

**Use Case**: Quick check to see if a symbol has been processed by `CodeGenInfoComputer`

---

### Variable Type Methods

#### `SetVariableType(VariableSymbol symbol, SemanticType type)`

```csharp
public void SetVariableType(VariableSymbol symbol, SemanticType type)
    => _variableTypes[symbol] = type;
```

**Purpose**: Store the resolved type for a variable.

**Typical Call Site**: `TypeChecker.cs` after inferring or resolving variable types

**Example**:
```csharp
// In TypeChecker.cs - handling variable declaration
var varSymbol = new VariableSymbol { Name = assignment.Target.Name };

// Infer type from initializer
var initType = CheckExpression(assignment.Value);
semanticBinding.SetVariableType(varSymbol, initType);
```

#### `GetVariableType(VariableSymbol symbol)`

```csharp
public SemanticType GetVariableType(VariableSymbol symbol)
    => _variableTypes.TryGetValue(symbol, out var type) ? type : SemanticType.Unknown;
```

**Returns**: The variable's type, or `SemanticType.Unknown` if not set

**Safe Default**: Always returns a valid `SemanticType` (never null), using `Unknown` for error recovery

**Typical Call Site**: Type checking, code generation

**Example**:
```csharp
// In TypeChecker.cs - checking assignment compatibility
var varSymbol = _symbolTable.Lookup(target.Name) as VariableSymbol;
var varType = _semanticBinding.GetVariableType(varSymbol);
var valueType = CheckExpression(assignment.Value);

if (!IsAssignableFrom(varType, valueType))
{
    Error($"Cannot assign {valueType} to variable of type {varType}");
}
```

---

### Base Type Methods

#### `SetBaseType(TypeSymbol symbol, TypeSymbol baseType)`

```csharp
public void SetBaseType(TypeSymbol symbol, TypeSymbol baseType)
    => _baseTypes[symbol] = baseType;
```

**Purpose**: Record inheritance relationship (symbol inherits from baseType).

**Typical Call Site**: `NameResolver.ResolveInheritance()` (Pass 2)

**Example**:
```csharp
// In NameResolver.cs - processing class inheritance
var classDef = /* ClassDef AST node with base class */;
var classSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
var baseSymbol = ResolveBaseClass(classDef.BaseClass);

if (baseSymbol != null)
{
    _semanticBinding.SetBaseType(classSymbol, baseSymbol);
}
```

#### `GetBaseType(TypeSymbol symbol)`

```csharp
public TypeSymbol? GetBaseType(TypeSymbol symbol)
    => _baseTypes.TryGetValue(symbol, out var bt) ? bt : null;
```

**Returns**: The base class symbol, or `null` if the type has no base class

**Use Cases**:
- Method resolution order (MRO) traversal
- Checking type hierarchy for casting
- Virtual method resolution

**Example**:
```csharp
// In TypeChecker.cs - building method resolution order
private List<TypeSymbol> GetMRO(TypeSymbol type)
{
    var mro = new List<TypeSymbol> { type };
    var current = type;
    
    while (true)
    {
        var baseType = _semanticBinding.GetBaseType(current);
        if (baseType == null)
            break;
            
        mro.Add(baseType);
        current = baseType;
    }
    
    return mro;
}
```

---

### Module Resolution Methods

#### `SetResolvedModulePath(FromImportStatement stmt, string path)`

```csharp
public void SetResolvedModulePath(FromImportStatement stmt, string path)
    => _resolvedModulePaths[stmt] = path;
```

**Purpose**: Store the resolved file path or assembly name for an import statement.

**Typical Call Site**: `ImportResolver.cs` during import resolution

**Example**:
```csharp
// In ImportResolver.cs
private void ResolveImport(FromImportStatement importStmt)
{
    var moduleName = importStmt.Module;
    var resolvedPath = _moduleResolver.ResolvePath(moduleName);
    
    if (resolvedPath != null)
    {
        _semanticBinding.SetResolvedModulePath(importStmt, resolvedPath);
    }
}
```

#### `GetResolvedModulePath(FromImportStatement stmt)`

```csharp
public string? GetResolvedModulePath(FromImportStatement stmt)
    => _resolvedModulePaths.TryGetValue(stmt, out var path) ? path : null;
```

**Returns**: The resolved module path, or `null` if import failed to resolve

**Use Case**: Code generation needs to know where symbols came from to generate correct `using` directives

---

#### `SetReExportedSymbols(FromImportStatement stmt, Dictionary<string, Symbol> symbols)`

```csharp
public void SetReExportedSymbols(FromImportStatement stmt, Dictionary<string, Symbol> symbols)
    => _reExportedSymbols[stmt] = symbols;
```

**Purpose**: Store symbols that are re-exported through this import.

**Use Case**: Package initialization files (`__init__.spy`) that re-export submodule members

#### `GetReExportedSymbols(FromImportStatement stmt)`

```csharp
public Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement stmt)
    => _reExportedSymbols.TryGetValue(stmt, out var symbols) ? symbols : null;
```

**Returns**: Dictionary of re-exported symbols, or `null` if none

---

## Dependencies

### Direct Dependencies

1. **`System.Collections.Concurrent`** - For `ConcurrentDictionary`
   - Thread-safe parallel compilation
   - Future LSP support

2. **`Sharpy.Compiler.Parser.Ast`** - For AST node types
   - `FromImportStatement` - Used as dictionary key

3. **`Sharpy.Compiler.Semantic` namespace** (implicit)
   - `Symbol`, `VariableSymbol`, `TypeSymbol` - Symbol types
   - `SemanticType` - Type system
   - `CodeGenInfo` - Code generation metadata

### Consumers (Who Uses SemanticBinding)

1. **`ProjectCompiler`** - Creates the binding
   ```csharp
   var semanticBinding = new SemanticBinding();
   _importResolver.SetSemanticBinding(semanticBinding);
   ```

2. **`ImportResolver`** - Stores module resolution data
   - Sets resolved module paths
   - Sets re-exported symbols

3. **`CodeGenInfoComputer`** - Populates code generation info
   - Computes C# names
   - Sets CodeGenInfo for all symbols

4. **`RoslynEmitter` (CodeGen)** - Reads semantic data
   - Gets CodeGenInfo for name mangling
   - Gets variable types for C# type emission

5. **Symbol classes** - Transitional dual storage
   - Currently store data in both places during migration
   - Eventually will only use SemanticBinding

---

## Design Patterns and Decisions

### Pattern 1: Separation of AST and Semantic Data

**Design**: Semantic information is stored externally from AST nodes and symbols.

**Benefits**:
- **Immutability**: Symbols can be made immutable records in the future
- **Multiple bindings**: The same AST/symbols could have different semantic bindings (useful for LSP)
- **Clear phases**: Semantic analysis clearly happens after symbol creation

**Contrast with SemanticInfo**:
| SemanticInfo | SemanticBinding |
|--------------|-----------------|
| Maps AST nodes to properties | Maps symbols to properties |
| Expression types, identifier bindings | Symbol metadata, inheritance, codegen info |
| Populated during type checking | Populated across multiple passes |

**Why both exist**: 
- `SemanticInfo`: "What type is this expression?" (AST-centric)
- `SemanticBinding`: "What are this symbol's properties?" (Symbol-centric)

---

### Pattern 2: Thread-Safe Concurrent Storage

**Design**: Use `ConcurrentDictionary` for all storage.

**Benefits**:
- Parallel file compilation
- Safe for future LSP incremental compilation
- No locking overhead in hot paths (TryGetValue is lock-free)

**Trade-offs**:
- Slightly higher memory usage (~72 bytes overhead per dictionary)
- Worthwhile for parallelization benefits

**When it matters**:
```csharp
// Multiple files can be compiled in parallel
Parallel.ForEach(spyFiles, file => 
{
    var semanticBinding = new SemanticBinding();  // One per file
    // ... compile file, populate binding
    // Thread-safe access to shared symbol registries
});
```

---

### Pattern 3: Migration Strategy (Dual Storage)

**Current State**: Data is stored in **both** places during migration:
- Old location: Mutable properties on `Symbol` classes
- New location: `SemanticBinding` dictionaries

**Evidence in Symbol.cs**:
```csharp
// Symbol.cs - Migration notes
public CodeGenInfo? CodeGenInfo { get; set; }  // OLD WAY
// MIGRATION NOTE: In the future, use SemanticBinding.SetCodeGenInfo/GetCodeGenInfo instead.

public SemanticType Type { get; set; }  // OLD WAY
// MIGRATION NOTE: In the future, use SemanticBinding.SetVariableType/GetVariableType instead.

public TypeSymbol? BaseType { get; set; }  // OLD WAY
// MIGRATION NOTE: In the future, use SemanticBinding.SetBaseType/GetBaseType instead.
```

**Migration Path**:
1. **Phase 1** (Current): Both approaches coexist
2. **Phase 2**: New code uses `SemanticBinding` exclusively
3. **Phase 3**: Remove mutable properties from `Symbol` classes
4. **Phase 4**: Make `Symbol` immutable records

**Why gradual**: Allows testing new approach without breaking existing code.

---

### Pattern 4: Null-Safe Returns

**Design**: All `Get*` methods return nullable types and handle missing data gracefully.

**Exception**: `GetVariableType()` returns `SemanticType.Unknown` instead of `null`

**Rationale**:
```csharp
// GetCodeGenInfo, GetBaseType, etc. - can reasonably be null
var info = binding.GetCodeGenInfo(symbol);
if (info == null)
{
    // Symbol hasn't been processed yet - use default behavior
}

// GetVariableType - never null (always has a default)
var type = binding.GetVariableType(varSymbol);
// type is never null, but might be SemanticType.Unknown
if (type == SemanticType.Unknown)
{
    // Type inference failed or not yet run
}
```

**Best Practice**: Always check for null/Unknown before using returned values.

---

### Pattern 5: AST Node Keys for Import Data

**Design**: Use `FromImportStatement` (AST node) as dictionary key for import resolution data.

**Why AST nodes instead of strings**:
- Same module can be imported multiple times with different aliases
- Each import statement needs its own resolution data
- Preserves source location information for error reporting

**Example showing why this matters**:
```python
# Multiple imports of same module
from utils import helper as h1  # Import 1
from utils import helper as h2  # Import 2
from utils import format_text   # Import 3
```

Each `FromImportStatement` gets its own entry in `_resolvedModulePaths`, even though they refer to the same module.

---

## Detailed Documentation: XML Comments

The source file includes **excellent XML documentation** with `<summary>` and `<remarks>` tags. Let's highlight the key design insights from the documentation:

### Class-Level Documentation

```csharp
/// <summary>
/// Stores semantic information that is computed after AST creation.
/// This separates mutable semantic data from immutable syntax.
/// </summary>
```

**Key phrase**: "computed **after** AST creation" - This is a post-parsing phase.

```csharp
/// <remarks>
/// <para>
/// The AST represents pure syntax - it's computed during parsing and should be immutable.
/// However, semantic analysis needs to attach additional information to AST nodes and symbols:
/// </para>
```

**Philosophy**: AST = syntax, SemanticBinding = semantics. They're separate concerns.

```csharp
/// <para>
/// By storing this information in SemanticBinding instead of on the AST/Symbol directly,
/// we enable:
/// - Multiple bindings per AST (useful for LSP with incremental edits)
/// - Thread-safe parallel compilation (ConcurrentDictionary)
/// - Clear separation between parsing and semantic analysis
/// </para>
```

**Three benefits** clearly stated:
1. **Multiple bindings**: Future-proofs for LSP where same AST might have different type resolutions as user edits
2. **Thread-safe**: Critical for performance on multi-file projects
3. **Separation**: Clean architecture

---

## Common Usage Scenarios

### Scenario 1: Complete Compilation Flow

Let's trace a simple class through the compiler:

```python
# Sharpy source
class Animal:
    pass

class Dog(Animal):
    def __init__(self, name: str):
        self.dog_name = name
```

**Step 1: NameResolver creates symbols (Pass 1 - Declarations)**
```csharp
var animalSymbol = new TypeSymbol { Name = "Animal", TypeKind = TypeKind.Class };
_symbolTable.Define(animalSymbol);

var dogSymbol = new TypeSymbol { Name = "Dog", TypeKind = TypeKind.Class };
_symbolTable.Define(dogSymbol);

// Note: BaseType is NOT set yet - we don't know what "Animal" refers to
```

**Step 2: NameResolver resolves inheritance (Pass 2 - Inheritance)**
```csharp
// Process Dog's base class
var baseClass = LookupTypeInScope("Animal");  // Finds animalSymbol

// Store in SemanticBinding (NEW WAY)
_semanticBinding.SetBaseType(dogSymbol, animalSymbol);

// Also store on symbol for backward compatibility (OLD WAY - temporary)
dogSymbol.BaseType = animalSymbol;
```

**Step 3: TypeResolver resolves type annotations**
```csharp
// Process "name: str" parameter
var strType = _primitiveCatalog.GetType("str");

var nameParam = new ParameterSymbol { Name = "name", Type = strType };
initMethod.Parameters.Add(nameParam);
```

**Step 4: TypeChecker infers field types**
```csharp
// Process "self.dog_name = name"
var dogNameSymbol = new VariableSymbol { Name = "dog_name" };

// Infer type from assignment
var nameType = GetParameterType(nameParam);  // SemanticType.Str

// Store in SemanticBinding
_semanticBinding.SetVariableType(dogNameSymbol, nameType);
```

**Step 5: CodeGenInfoComputer computes C# names**
```csharp
// Process class name
var dogInfo = new CodeGenInfo
{
    CSharpName = "Dog",  // Already PascalCase
    OriginalName = "Dog",
    IsModuleLevel = true
};
_semanticBinding.SetCodeGenInfo(dogSymbol, dogInfo);

// Process field name (snake_case → PascalCase)
var fieldInfo = new CodeGenInfo
{
    CSharpName = "DogName",  // Mangled to PascalCase
    OriginalName = "dog_name",
    IsModuleLevel = false
};
_semanticBinding.SetCodeGenInfo(dogNameSymbol, fieldInfo);
```

**Step 6: RoslynEmitter generates C#**
```csharp
// Generate class declaration
var classInfo = _semanticBinding.GetCodeGenInfo(dogSymbol);
var className = classInfo?.CSharpName ?? "Dog";

// Determine base class
var baseType = _semanticBinding.GetBaseType(dogSymbol);
var baseTypeSyntax = baseType != null 
    ? SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseType.Name))
    : null;

// Generate field
var fieldInfo = _semanticBinding.GetCodeGenInfo(dogNameSymbol);
var fieldName = fieldInfo?.CSharpName ?? "dog_name";
var fieldType = _semanticBinding.GetVariableType(dogNameSymbol);
var fieldTypeSyntax = TypeMapper.MapType(fieldType);  // "string"
```

**Generated C#**:
```csharp
public class Dog : Animal
{
    public string DogName;
    
    public Dog(string name)
    {
        this.DogName = name;
    }
}
```

---

### Scenario 2: Module Import with Re-exports

```python
# utils/__init__.spy
from .helpers import format_text, parse_date

# main.spy
from utils import format_text
```

**Step 1: ImportResolver processes utils/__init__.spy**
```csharp
// Parse: from .helpers import format_text, parse_date
var importStmt = /* FromImportStatement node */;

// Resolve module path
var helpersPath = ResolveRelativePath(".helpers");  // → "utils/helpers.spy"
_semanticBinding.SetResolvedModulePath(importStmt, helpersPath);

// Load helpers.spy and get symbols
var formatTextSymbol = LoadSymbol(helpersPath, "format_text");
var parseDateSymbol = LoadSymbol(helpersPath, "parse_date");

// Store re-exported symbols
var reExports = new Dictionary<string, Symbol>
{
    ["format_text"] = formatTextSymbol,
    ["parse_date"] = parseDateSymbol
};
_semanticBinding.SetReExportedSymbols(importStmt, reExports);
```

**Step 2: ImportResolver processes main.spy**
```csharp
// Parse: from utils import format_text
var mainImport = /* FromImportStatement node */;

// Resolve "utils" → "utils/__init__.spy"
var utilsPath = ResolveModulePath("utils");  // → "utils/__init__.spy"
_semanticBinding.SetResolvedModulePath(mainImport, utilsPath);

// Load utils's exports - check for re-exported symbols
var utilsModule = LoadModule(utilsPath);
var reExportedSymbols = _semanticBinding.GetReExportedSymbols(utilsImportStmt);

// Find format_text in re-exports
if (reExportedSymbols != null && reExportedSymbols.ContainsKey("format_text"))
{
    var symbol = reExportedSymbols["format_text"];
    _symbolTable.Define(symbol);  // Make available in main.spy
}
```

**Result**: `main.spy` can use `format_text` even though it's actually defined in `helpers.spy`.

---

## Debugging Tips

### Tip 1: Visualize the SemanticBinding State

Add a debug method to dump all stored information:

```csharp
// Add to SemanticBinding.cs (in debug builds)
#if DEBUG
public void DebugDump(TextWriter writer)
{
    writer.WriteLine("=== CodeGenInfo ===");
    foreach (var (symbol, info) in _codeGenInfo)
    {
        writer.WriteLine($"{symbol.GetType().Name} '{symbol.Name}' → CSharpName='{info.CSharpName}', Version={info.Version}");
    }
    
    writer.WriteLine("\n=== Variable Types ===");
    foreach (var (varSymbol, type) in _variableTypes)
    {
        writer.WriteLine($"Variable '{varSymbol.Name}' → {type.GetDisplayName()}");
    }
    
    writer.WriteLine("\n=== Base Types ===");
    foreach (var (typeSymbol, baseSymbol) in _baseTypes)
    {
        writer.WriteLine($"Type '{typeSymbol.Name}' : '{baseSymbol.Name}'");
    }
    
    writer.WriteLine("\n=== Resolved Module Paths ===");
    foreach (var (stmt, path) in _resolvedModulePaths)
    {
        writer.WriteLine($"Import at line {stmt.LineStart} → '{path}'");
    }
}
#endif
```

**Usage**:
```csharp
// In your test or debug session
semanticBinding.DebugDump(Console.Out);
```

---

### Tip 2: Check for Missing Semantic Data

If code generation fails with strange errors, often a semantic binding is missing:

```csharp
// In RoslynEmitter.cs - add defensive checks
var info = _semanticBinding.GetCodeGenInfo(symbol);
if (info == null)
{
    throw new InvalidOperationException(
        $"Symbol '{symbol.Name}' at {symbol.DeclarationLine}:{symbol.DeclarationColumn} " +
        $"has no CodeGenInfo. CodeGenInfoComputer may have failed or skipped this symbol.");
}
```

---

### Tip 3: Trace Migration Inconsistencies

During the migration phase, data might be in the old location, new location, or both:

```csharp
// Add validation method
public void ValidateConsistency(Symbol symbol)
{
    // Check CodeGenInfo consistency
    var newInfo = GetCodeGenInfo(symbol);
    var oldInfo = symbol.CodeGenInfo;
    
    if (newInfo != null && oldInfo != null && newInfo != oldInfo)
    {
        _logger.LogWarning(
            $"Symbol '{symbol.Name}' has inconsistent CodeGenInfo:\n" +
            $"  SemanticBinding: {newInfo.CSharpName}\n" +
            $"  Symbol.CodeGenInfo: {oldInfo.CSharpName}");
    }
}
```

---

### Tip 4: Watch for Concurrent Access Issues

If you see rare, non-deterministic failures in parallel compilation:

```csharp
// Test thread safety
[Fact]
public void TestThreadSafety()
{
    var binding = new SemanticBinding();
    var symbols = Enumerable.Range(0, 1000)
        .Select(i => new VariableSymbol { Name = $"var{i}" })
        .ToList();
    
    // Write from multiple threads
    Parallel.ForEach(symbols, symbol =>
    {
        var info = new CodeGenInfo { CSharpName = symbol.Name, OriginalName = symbol.Name };
        binding.SetCodeGenInfo(symbol, info);
    });
    
    // Read from multiple threads
    Parallel.ForEach(symbols, symbol =>
    {
        var info = binding.GetCodeGenInfo(symbol);
        Assert.NotNull(info);
    });
}
```

---

### Tip 5: Trace Import Resolution

When debugging import issues:

```csharp
// In ImportResolver.cs
private void LogImportResolution(FromImportStatement stmt, string? resolvedPath)
{
    _logger.LogDebug(
        $"Import '{stmt.Module}' at line {stmt.LineStart} resolved to: " +
        (resolvedPath ?? "<FAILED>"));
    
    if (resolvedPath != null && _semanticBinding != null)
    {
        _semanticBinding.SetResolvedModulePath(stmt, resolvedPath);
        
        // Double-check it was stored
        var retrieved = _semanticBinding.GetResolvedModulePath(stmt);
        if (retrieved != resolvedPath)
        {
            _logger.LogError($"Failed to store module path in SemanticBinding!");
        }
    }
}
```

---

## Contribution Guidelines

### When to Add New Mappings

Add new mappings to `SemanticBinding` when:

1. **New semantic property computed after symbol creation**
   - Example: Capture analysis for closures, effect tracking for async functions

2. **Multi-pass analysis produces intermediate results**
   - Example: Definite assignment analysis, nullability flow analysis

3. **Code generation needs pre-computed metadata**
   - Example: Lambda lifting information, inline hints

**Template for adding new mapping**:
```csharp
// 1. Add dictionary field
private readonly ConcurrentDictionary<Symbol, YourDataType> _yourData = new();

// 2. Add region with Set/Get/Has methods
#region Your Data

/// <summary>
/// Sets the your-data for a symbol.
/// </summary>
public void SetYourData(Symbol symbol, YourDataType data)
    => _yourData[symbol] = data;

/// <summary>
/// Gets the your-data for a symbol, or null if not set.
/// </summary>
public YourDataType? GetYourData(Symbol symbol)
    => _yourData.TryGetValue(symbol, out var data) ? data : null;

/// <summary>
/// Checks if a symbol has your-data.
/// </summary>
public bool HasYourData(Symbol symbol)
    => _yourData.ContainsKey(symbol);

#endregion
```

---

### When NOT to Add to SemanticBinding

**Don't add** to `SemanticBinding` if:

1. **The data belongs on AST nodes** → Use `SemanticInfo` instead
   - Expression types
   - Identifier-to-symbol bindings
   - Resolved function call targets

2. **The data is part of symbol definition** → Keep on `Symbol` classes (for now)
   - Symbol name, kind, access level
   - Function parameters, return type
   - Class fields, methods

3. **The data is transient analysis state** → Use local variables in analyzer
   - Control flow graph nodes
   - Type constraint solving state
   - Temporary name resolution scopes

---

### Migration Guidelines

When migrating from mutable Symbol properties to SemanticBinding:

**Step 1**: Add accessors to SemanticBinding (already done for CodeGenInfo, VariableType, BaseType)

**Step 2**: Update writers to use SemanticBinding:
```csharp
// OLD WAY
symbol.CodeGenInfo = computedInfo;

// NEW WAY
_semanticBinding.SetCodeGenInfo(symbol, computedInfo);

// TRANSITION: Do both temporarily
symbol.CodeGenInfo = computedInfo;
_semanticBinding.SetCodeGenInfo(symbol, computedInfo);
```

**Step 3**: Update readers to prefer SemanticBinding:
```csharp
// OLD WAY
var info = symbol.CodeGenInfo;

// NEW WAY (with fallback)
var info = _semanticBinding.GetCodeGenInfo(symbol) ?? symbol.CodeGenInfo;
```

**Step 4**: Once all readers/writers use SemanticBinding, remove Symbol property:
```csharp
// Remove from Symbol.cs
public CodeGenInfo? CodeGenInfo { get; set; }  // DELETE THIS
```

**Step 5**: Make Symbol immutable:
```csharp
// Change from mutable to init-only
public record TypeSymbol : Symbol
{
    // Before: public TypeSymbol? BaseType { get; set; }
    // After:  public TypeSymbol? BaseType { get; init; }
}
```

---

### Testing Guidelines

Test both storage and retrieval:

```csharp
[Fact]
public void TestCodeGenInfo_SetAndGet()
{
    var binding = new SemanticBinding();
    var symbol = new VariableSymbol { Name = "test_var" };
    var info = new CodeGenInfo 
    { 
        CSharpName = "TestVar",
        OriginalName = "test_var"
    };
    
    // Should return null before setting
    Assert.Null(binding.GetCodeGenInfo(symbol));
    Assert.False(binding.HasCodeGenInfo(symbol));
    
    // Set the info
    binding.SetCodeGenInfo(symbol, info);
    
    // Should return the set info
    Assert.NotNull(binding.GetCodeGenInfo(symbol));
    Assert.Equal("TestVar", binding.GetCodeGenInfo(symbol)!.CSharpName);
    Assert.True(binding.HasCodeGenInfo(symbol));
}
```

Test thread safety for concurrent access:

```csharp
[Fact]
public void TestThreadSafety_CodeGenInfo()
{
    var binding = new SemanticBinding();
    var symbols = Enumerable.Range(0, 100)
        .Select(i => new VariableSymbol { Name = $"var{i}" })
        .ToList();
    
    // Multiple threads writing
    Parallel.ForEach(symbols, symbol =>
    {
        var info = new CodeGenInfo 
        { 
            CSharpName = symbol.Name.ToUpperInvariant(),
            OriginalName = symbol.Name
        };
        binding.SetCodeGenInfo(symbol, info);
    });
    
    // Multiple threads reading
    Parallel.ForEach(symbols, symbol =>
    {
        var info = binding.GetCodeGenInfo(symbol);
        Assert.NotNull(info);
        Assert.Equal(symbol.Name.ToUpperInvariant(), info.CSharpName);
    });
}
```

---

## Integration Points

### With NameResolver

`NameResolver` creates symbols but doesn't set their complete semantic info:

```csharp
// NameResolver.cs
public void ResolveInheritance(Module module)
{
    foreach (var classDef in module.Classes)
    {
        var classSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        
        if (classDef.BaseClass != null)
        {
            var baseSymbol = ResolveType(classDef.BaseClass);
            
            // NEW: Store in SemanticBinding
            _semanticBinding?.SetBaseType(classSymbol, baseSymbol);
            
            // OLD: Also store on symbol (temporary during migration)
            classSymbol.BaseType = baseSymbol;
        }
    }
}
```

---

### With TypeChecker

`TypeChecker` resolves and stores variable types:

```csharp
// TypeChecker.cs
private void CheckVariableDeclaration(VarDecl decl)
{
    var symbol = _symbolTable.Lookup(decl.Target.Name) as VariableSymbol;
    
    // Infer or check type
    SemanticType type;
    if (decl.Annotation != null)
    {
        type = _typeResolver.ResolveType(decl.Annotation);
    }
    else
    {
        type = InferType(decl.Value);
    }
    
    // Store in SemanticBinding
    _semanticBinding.SetVariableType(symbol, type);
}
```

---

### With CodeGenInfoComputer

`CodeGenInfoComputer` is the primary writer of CodeGenInfo:

```csharp
// CodeGenInfoComputer.cs
public void ComputeForModule(Module module, SymbolTable symbolTable)
{
    foreach (var symbol in symbolTable.GetAllSymbols())
    {
        var info = ComputeInfo(symbol);
        _semanticBinding.SetCodeGenInfo(symbol, info);
    }
}

private CodeGenInfo ComputeInfo(Symbol symbol)
{
    var csharpName = NameMangler.Mangle(symbol.Name);
    
    return new CodeGenInfo
    {
        CSharpName = csharpName,
        OriginalName = symbol.Name,
        IsModuleLevel = symbol.DeclarationLine == null || IsModuleLevelScope(symbol)
    };
}
```

---

### With RoslynEmitter

`RoslynEmitter` is the primary reader of all semantic binding data:

```csharp
// RoslynEmitter.cs
private MemberDeclarationSyntax EmitClassDeclaration(ClassDef classDef)
{
    var symbol = _semanticInfo.GetTypeSymbol(classDef);
    
    // Get C# name from CodeGenInfo
    var info = _semanticBinding.GetCodeGenInfo(symbol);
    var className = info?.CSharpName ?? symbol.Name;
    
    // Get base type for inheritance
    var baseType = _semanticBinding.GetBaseType(symbol);
    var baseList = baseType != null
        ? SyntaxFactory.BaseList(
            SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseType.Name))))
        : null;
    
    return SyntaxFactory.ClassDeclaration(className)
        .WithBaseList(baseList)
        // ... more code generation
        ;
}
```

---

### With ImportResolver

`ImportResolver` uses `SemanticBinding` to store import resolution results:

```csharp
// ImportResolver.cs
public void SetSemanticBinding(SemanticBinding binding)
{
    _semanticBinding = binding;
}

private void ResolveFromImport(FromImportStatement stmt)
{
    var modulePath = _moduleResolver.ResolvePath(stmt.Module);
    
    if (modulePath != null && _semanticBinding != null)
    {
        _semanticBinding.SetResolvedModulePath(stmt, modulePath);
        
        // If this is a re-exporting import, store the symbols
        var symbols = LoadSymbolsFromModule(modulePath);
        _semanticBinding.SetReExportedSymbols(stmt, symbols);
    }
}
```

---

## Cross-References

This file works closely with:

- **[`SemanticInfo.md`](SemanticInfo.md)** - AST node annotations (expression types, identifier bindings)
- **[`Symbol.md`](Symbol.md)** - Symbol definitions and properties (shows migration notes)
- **[`CodeGenInfo.md`](CodeGenInfo.md)** - Detailed documentation of code generation metadata
- **[`SemanticType.md`](SemanticType.md)** - Type system used in variable type mappings
- **[`NameResolver.md`](NameResolver.md)** - Creates symbols and resolves inheritance (populates base types)
- **[`TypeChecker.md`](TypeChecker.md)** - Resolves types and populates variable types
- **[`CodeGenInfoComputer.md`](CodeGenInfoComputer.md)** - Computes and stores CodeGenInfo
- **[`ImportResolver.md`](ImportResolver.md)** - Resolves imports and stores module paths

**Key Distinction**:
- **SemanticInfo**: "What properties does this AST node have?" (expression → type)
- **SemanticBinding**: "What properties does this symbol have?" (symbol → CodeGenInfo, type, base class)

---

## Summary

`SemanticBinding` is a **critical architectural component** in the Sharpy compiler's evolution toward immutable AST design:

**What it does**: Stores semantic metadata about symbols and imports, separate from their definitions

**Why it exists**: Enables immutable symbols, thread-safe parallel compilation, and future LSP support

**How it works**: Four concurrent dictionaries with simple Set/Get APIs

**When it's used**: 
- Written during semantic analysis (NameResolver, TypeChecker, CodeGenInfoComputer, ImportResolver)
- Read during code generation (RoslynEmitter)

**Key Design Principles**:
1. **Separation of Concerns**: Syntax (AST) vs. Semantics (Binding)
2. **Thread Safety**: ConcurrentDictionary for parallel compilation
3. **Gradual Migration**: Coexists with old mutable Symbol properties
4. **Null Safety**: All getters return nullable types or safe defaults

**For New Contributors**:
1. Start by reading `SemanticInfo.md` to understand the AST annotation layer
2. Compare `SemanticInfo` (AST-centric) vs `SemanticBinding` (Symbol-centric)
3. Look at migration notes in `Symbol.cs` to see the transition in progress
4. Search for `SetCodeGenInfo`, `GetVariableType`, etc. in the codebase to see usage patterns
5. When adding new semantic properties, consider whether they belong in `SemanticInfo` or `SemanticBinding`

**Future Direction**: Eventually, all mutable Symbol properties will migrate to `SemanticBinding`, enabling fully immutable AST and symbol tables. This will unlock powerful features like incremental compilation, parallel type checking, and LSP support with minimal overhead.
