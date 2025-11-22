# Walkthrough: NameResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

---

## 1. Overview

The `NameResolver` is the **first pass** of Sharpy's semantic analysis phase. Its primary responsibility is to traverse the Abstract Syntax Tree (AST) and build comprehensive **symbol tables** that map names to their declarations.

**What it does:**
- Discovers all type declarations (classes, structs, interfaces, enums)
- Registers top-level functions and constants
- Resolves class/struct/interface members (methods, fields, properties)
- Resolves inheritance relationships between types
- Detects name collision errors (redefinitions)
- Determines access levels based on Python naming conventions

**What it doesn't do:**
- Type checking (handled by `TypeChecker` in a later pass)
- Type inference (deferred to `TypeChecker`)
- Expression resolution (deferred to later passes)
- Import module loading (partially implemented, marked as TODO)

**Position in compilation pipeline:**
```
Parser (AST) → NameResolver (Symbols) → TypeChecker (Types) → CodeGenerator (C#)
```

The NameResolver operates in **two phases**:
1. **Declaration Pass**: Registers all type and function names
2. **Inheritance Pass**: Resolves base classes and interfaces

This two-phase approach allows forward references (using a type before it's fully defined) and circular dependencies.

---

## 2. Class/Type Structure

### Main Class: `NameResolver`

```csharp
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly List<ClassDef> _classDefs = new();
    private readonly List<StructDef> _structDefs = new();
    private readonly List<InterfaceDef> _interfaceDefs = new();
    
    public IReadOnlyList<SemanticError> Errors => _errors;
}
```

**Key Fields:**

- **`_symbolTable`**: The central symbol table that stores all symbols across all scopes. This is passed in from the semantic analyzer and shared with other passes.

- **`_logger`**: Optional logger for debugging and diagnostics. Uses `NullLogger.Instance` if not provided.

- **`_errors`**: Accumulates semantic errors (e.g., redefinitions, missing types). Non-fatal errors are collected here rather than throwing exceptions immediately.

- **`_classDefs`, `_structDefs`, `_interfaceDefs`**: Temporary storage for type definitions during the first pass. These are processed in the second pass to resolve inheritance relationships.

---

## 3. Key Methods/Functions

### 3.1 Entry Points: Two-Phase Resolution

#### `ResolveDeclarations(Module module)`

**Purpose:** First pass - registers all type and function declarations.

```csharp
public void ResolveDeclarations(Module module)
{
    _logger.LogInfo("Name resolution pass 1: Declarations in module");
    
    foreach (var statement in module.Body)
    {
        ResolveDeclaration(statement);
    }
}
```

**How it works:**
- Iterates through top-level statements in the module
- Delegates to `ResolveDeclaration()` which dispatches based on statement type
- Only processes declarations (classes, functions, enums, constants, imports)
- Ignores regular statements (assignments, if/while, etc.) - these are handled in later passes

**Why this matters:** By separating declaration from type checking, we can use types before they're fully analyzed (forward references).

---

#### `ResolveInheritance()`

**Purpose:** Second pass - resolves base classes and interfaces after all types are declared.

```csharp
public void ResolveInheritance()
{
    _logger.LogInfo("Name resolution pass 2: Inheritance relationships");
    
    foreach (var classDef in _classDefs)
        ResolveClassInheritance(classDef);
    
    foreach (var structDef in _structDefs)
        ResolveStructInheritance(structDef);
        
    foreach (var interfaceDef in _interfaceDefs)
        ResolveInterfaceInheritance(interfaceDef);
}
```

**How it works:**
- Processes all classes, structs, and interfaces collected in the first pass
- Looks up base types in the symbol table (which now contains all declared types)
- Links type symbols to their base types and interfaces
- Validates inheritance rules (e.g., structs can't inherit from classes)

**Why this matters:** Inheritance must be resolved after all types are declared to support circular dependencies (e.g., `class A(B)` and `class B(A)` - though this would fail validation, we need both types declared first).

---

### 3.2 Declaration Resolution

#### `ResolveDeclaration(Statement statement)`

**Purpose:** Dispatcher that routes statements to appropriate handlers.

```csharp
private void ResolveDeclaration(Statement statement)
{
    switch (statement)
    {
        case ClassDef classDef:
            ResolveClassDeclaration(classDef);
            break;
        case StructDef structDef:
            ResolveStructDeclaration(structDef);
            break;
        case InterfaceDef interfaceDef:
            ResolveInterfaceDeclaration(interfaceDef);
            break;
        case EnumDef enumDef:
            ResolveEnumDeclaration(enumDef);
            break;
        case FunctionDef functionDef:
            ResolveFunctionDeclaration(functionDef);
            break;
        case VariableDeclaration varDecl when varDecl.IsConst:
            ResolveConstantDeclaration(varDecl);
            break;
        case ImportStatement import:
            ResolveImport(import);
            break;
        case FromImportStatement fromImport:
            ResolveFromImport(fromImport);
            break;
        // Other statements ignored in this pass
    }
}
```

**Key design decision:** Pattern matching on AST node types. This is clean, type-safe, and easily extensible (add a new case for new statement types).

**Note:** Only `const` variable declarations are handled here. Regular variables are resolved when type-checking function bodies.

---

### 3.3 Type Declarations

#### `ResolveClassDeclaration(ClassDef classDef)`

**Purpose:** Registers a class type and its members (methods, fields).

```csharp
private void ResolveClassDeclaration(ClassDef classDef)
{
    _logger.LogDebug($"Resolving class declaration: {classDef.Name}");
    
    // Check for redefinition
    if (_symbolTable.Lookup(classDef.Name, searchParents: false) != null)
    {
        AddError($"Class '{classDef.Name}' is already defined", ...);
        return;
    }
    
    // Create type symbol
    var typeSymbol = new TypeSymbol
    {
        Name = classDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        AccessLevel = AccessLevel.Public,
        TypeParameters = classDef.TypeParameters,
        ...
    };
    
    _symbolTable.Define(typeSymbol);
    _classDefs.Add(classDef);  // Save for inheritance pass
    
    // Enter class scope to resolve members
    _symbolTable.EnterScope($"class:{classDef.Name}");
    
    foreach (var statement in classDef.Body)
    {
        if (statement is FunctionDef method)
            ResolveMethodDeclaration(method, typeSymbol);
        else if (statement is VariableDeclaration field)
            ResolveFieldDeclaration(field, typeSymbol);
    }
    
    _symbolTable.ExitScope();
}
```

**Key steps:**
1. **Redefinition check**: Ensures no duplicate names in current scope (`searchParents: false`)
2. **Create TypeSymbol**: Captures metadata (name, type parameters, location)
3. **Define in symbol table**: Makes the type available for lookups
4. **Save for second pass**: Stores in `_classDefs` for inheritance resolution
5. **Enter class scope**: Creates nested scope for class members
6. **Resolve members**: Processes methods and fields
7. **Exit scope**: Returns to module/parent scope

**Important detail:** The class name is added to the *parent* scope (module level), but members are added to the *class* scope. This allows name shadowing and prevents member names from polluting the global namespace.

**Naming convention:** Scope names use `"class:ClassName"` format for debugging/logging.

---

#### `ResolveStructDeclaration(StructDef structDef)`

**Purpose:** Nearly identical to `ResolveClassDeclaration`, but creates a `TypeKind.Struct` instead.

**Key difference:** Structs are value types in .NET, so this distinction is important for code generation later. The logic is otherwise the same as classes.

---

#### `ResolveInterfaceDeclaration(InterfaceDef interfaceDef)`

**Purpose:** Registers an interface type and its method signatures.

**Key difference from classes:**
- Only processes methods (interfaces can't have fields in traditional OOP)
- No instance fields allowed
- All methods are implicitly abstract

---

#### `ResolveEnumDeclaration(EnumDef enumDef)`

**Purpose:** Registers an enum type (simplest type declaration).

```csharp
private void ResolveEnumDeclaration(EnumDef enumDef)
{
    var typeSymbol = new TypeSymbol
    {
        Name = enumDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Enum,
        AccessLevel = AccessLevel.Public,
        ...
    };
    _symbolTable.Define(typeSymbol);
}
```

**Why it's simpler:** Enums don't have members to resolve (enum values are handled separately), and they can't inherit from other types.

---

### 3.4 Function and Member Declarations

#### `ResolveFunctionDeclaration(FunctionDef functionDef)`

**Purpose:** Registers a module-level (top-level) function.

```csharp
private void ResolveFunctionDeclaration(FunctionDef functionDef)
{
    if (_symbolTable.Lookup(functionDef.Name, searchParents: false) != null)
    {
        AddError($"Function '{functionDef.Name}' is already defined", ...);
        return;
    }
    
    var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
    {
        Name = p.Name,
        Type = SemanticType.Unknown,  // Resolved later by TypeChecker
        HasDefault = p.DefaultValue != null,
        DefaultValue = p.DefaultValue
    }).ToList();
    
    var funcSymbol = new FunctionSymbol
    {
        Name = functionDef.Name,
        Kind = SymbolKind.Function,
        Parameters = parameters,
        ...
    };
    
    _symbolTable.Define(funcSymbol);
}
```

**Critical design decision:** Types are set to `SemanticType.Unknown` initially. The `TypeChecker` will resolve parameter and return types in a later pass. This allows the NameResolver to stay focused on *what exists*, not *what types things have*.

**Parameter handling:** Captures parameter names and default values, but doesn't validate types yet.

---

#### `ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)`

**Purpose:** Registers a method within a class/struct/interface.

```csharp
private void ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)
{
    var accessLevel = DetermineAccessLevel(method.Name);
    
    // Check decorators
    bool isStatic = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");
    bool isAbstract = method.Decorators.Any(d => d.Name == "abstract" || d.Name == "abstractmethod");
    bool isVirtual = method.Decorators.Any(d => d.Name == "virtual");
    bool isOverride = method.Decorators.Any(d => d.Name == "override");
    
    var funcSymbol = new FunctionSymbol
    {
        Name = method.Name,
        AccessLevel = accessLevel,
        IsStatic = isStatic,
        IsAbstract = isAbstract,
        IsVirtual = isVirtual,
        IsOverride = isOverride,
        ...
    };
    
    owningType.Methods.Add(funcSymbol);
    _symbolTable.Define(funcSymbol);
}
```

**Key features:**
- **Access level inference**: Uses `DetermineAccessLevel()` based on Python naming conventions
- **Decorator parsing**: Extracts method modifiers from Python decorators (`@static`, `@abstract`, etc.)
- **Dual registration**: Adds method to both the type's method list AND the current scope's symbol table

**Why dual registration?** 
- `owningType.Methods.Add()`: Allows type-based member lookup (e.g., "what methods does this class have?")
- `_symbolTable.Define()`: Allows name-based lookup within the class scope

---

#### `ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)`

**Purpose:** Registers a field (instance variable) within a class/struct.

**Similar to method resolution:** Determines access level, creates a `VariableSymbol`, and adds to both the type and the symbol table.

**Key difference from local variables:** Fields are registered immediately during name resolution, while local variables in function bodies are handled during type checking.

---

### 3.5 Access Level Determination

#### `DetermineAccessLevel(string name)`

**Purpose:** Infers C# access modifiers from Python naming conventions.

```csharp
private AccessLevel DetermineAccessLevel(string name)
{
    // Python conventions:
    // __name__ (dunder methods) = public (special methods)
    // __name (but not __name__) = private (name mangling)
    // _name = protected
    // name = public
    
    if (name.StartsWith("__") && name.EndsWith("__"))
        return AccessLevel.Public;    // __init__, __str__, etc.
    if (name.StartsWith("__") && !name.EndsWith("__"))
        return AccessLevel.Private;   // __internal_helper
    if (name.StartsWith("_"))
        return AccessLevel.Protected; // _protected_method
    return AccessLevel.Public;        // public_method
}
```

**Python → C# mapping:**
| Python Pattern | Access Level | Example |
|---------------|--------------|---------|
| `name` | `public` | `calculate()` |
| `_name` | `protected` | `_validate()` |
| `__name` | `private` | `__helper()` |
| `__name__` | `public` | `__init__()`, `__str__()` |

**Why dunder methods are public:** Special methods like `__init__`, `__add__`, `__str__` are part of Python's protocol system and need to be accessible by the runtime.

**Implementation note:** This is a convention-based heuristic, not enforced by Python. Sharpy makes it explicit in C#.

---

### 3.6 Inheritance Resolution

#### `ResolveClassInheritance(ClassDef classDef)`

**Purpose:** Links a class to its base class and implemented interfaces.

```csharp
private void ResolveClassInheritance(ClassDef classDef)
{
    if (classDef.BaseClasses.Count == 0)
        return;
    
    var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
    
    // First base class = base type (C# single inheritance)
    var baseClassAnnot = classDef.BaseClasses[0];
    var baseSymbol = _symbolTable.Lookup(baseClassAnnot.Name) as TypeSymbol;
    
    if (baseSymbol == null)
    {
        AddError($"Base class '{baseClassAnnot.Name}' not found", ...);
        return;
    }
    
    if (baseSymbol.TypeKind != TypeKind.Class && baseSymbol.TypeKind != TypeKind.Interface)
    {
        AddError($"'{baseClassAnnot.Name}' is not a class or interface", ...);
        return;
    }
    
    typeSymbol.BaseType = baseSymbol;
    
    // Remaining base classes = interfaces
    for (int i = 1; i < classDef.BaseClasses.Count; i++)
    {
        var interfaceSymbol = _symbolTable.Lookup(classDef.BaseClasses[i].Name) as TypeSymbol;
        // ... validation ...
        typeSymbol.Interfaces.Add(interfaceSymbol);
    }
}
```

**C# Inheritance Model:**
- **First base class**: Treated as the base class (single inheritance)
- **Subsequent base classes**: Treated as interfaces (multiple interface implementation)

**Python vs C# difference:** Python supports multiple inheritance, but C# only supports single class inheritance + multiple interfaces. Sharpy follows the C# model.

**Example:**
```python
class MyClass(BaseClass, IFoo, IBar):  # BaseClass is parent, IFoo/IBar are interfaces
    pass
```

**Error handling:** Reports errors for missing types or invalid inheritance (e.g., inheriting from an enum).

---

#### `ResolveStructInheritance(StructDef structDef)`

**Purpose:** Links a struct to its implemented interfaces.

**Key constraint:** Structs can ONLY implement interfaces, they cannot inherit from classes (C# limitation).

```csharp
if (interfaceSymbol.TypeKind != TypeKind.Interface)
{
    AddError($"Structs can only implement interfaces, '{baseAnnot.Name}' is not an interface", ...);
    continue;
}
```

**Why this matters:** In .NET, structs are value types and can't participate in class inheritance. This validation ensures generated C# code will compile.

---

#### `ResolveInterfaceInheritance(InterfaceDef interfaceDef)`

**Purpose:** Links an interface to other interfaces it extends.

**Key constraint:** Interfaces can only extend other interfaces (standard OOP rule).

**Multiple interface inheritance:** Interfaces CAN extend multiple interfaces (unlike classes).

---

### 3.7 Import Resolution (Partially Implemented)

#### `ResolveImport(ImportStatement import)`

**Purpose:** Placeholder for module import resolution.

```csharp
private void ResolveImport(ImportStatement import)
{
    _logger.LogDebug($"Resolving import: {string.Join(", ", import.Names.Select(n => n.Name))}");
    // TODO: Implement module loading and resolution
}
```

**Current status:** Just logs the import, doesn't actually load modules.

**Future implementation:** Would need to:
1. Locate the module file (`.spy` or `.dll`)
2. Parse/load the module
3. Extract exported symbols
4. Add to current scope

**Related file:** `ImportResolver.cs` likely handles the actual implementation.

---

### 3.8 Error Handling

#### `AddError(string message, int? line, int? column)`

**Purpose:** Records a semantic error without stopping compilation.

```csharp
private void AddError(string message, int? line = null, int? column = null)
{
    var error = new SemanticError(message, line, column);
    _errors.Add(error);
    _logger.LogError(error.Message, line ?? 0, column ?? 0);
}
```

**Design philosophy:** Collect errors and continue processing rather than throwing exceptions. This allows the compiler to report multiple errors in a single run (better developer experience).

**Error exposure:** The `Errors` property exposes collected errors as read-only, so the semantic analyzer can check if resolution failed.

---

## 4. Dependencies

### Internal Dependencies (Sharpy.Compiler)

**Parser.Ast namespace:**
- `Module`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`, `FunctionDef`
- `VariableDeclaration`, `ImportStatement`, `FromImportStatement`
- `Statement`, `Expression` (base types)

**Semantic namespace:**
- `SymbolTable`: Central symbol registry (passed in constructor)
- `Symbol`, `TypeSymbol`, `FunctionSymbol`, `VariableSymbol`, `ParameterSymbol`: Symbol types
- `SemanticType`: Represents types in the type system (e.g., `SemanticType.Unknown`)
- `SemanticError`: Error representation

**Logging namespace:**
- `ICompilerLogger`: Interface for logging
- `NullLogger`: Default no-op logger

### External Dependencies

**System namespaces:**
- `System.Collections.Generic` (List, Stack, HashSet)
- `System.Linq` (LINQ queries for parameter mapping)

---

## 5. Patterns and Design Decisions

### 5.1 Two-Pass Architecture

**Problem:** Forward references and circular dependencies.

**Solution:** Separate declaration registration from type resolution.

**Benefits:**
- Types can reference each other (mutual recursion)
- Methods can return their own class type
- Inheritance chains can be resolved in correct order

**Example that works because of two-pass:**
```python
class Node:
    next: Optional[Node]  # Forward reference to Node
    
    def get_next(self) -> Optional[Node]:
        return self.next
```

---

### 5.2 Scope Management

**Pattern:** Stack-based scope hierarchy managed by `SymbolTable`.

**How it works:**
```csharp
_symbolTable.EnterScope($"class:{classDef.Name}");
// ... resolve members ...
_symbolTable.ExitScope();
```

**Benefits:**
- Clean separation between module-level and class-level names
- Supports nested scopes (classes inside classes, functions inside classes)
- Automatic parent scope lookup for name resolution

**Naming convention:** Scopes have descriptive names (`"class:Foo"`, `"struct:Bar"`) for debugging.

---

### 5.3 Visitor-Like Pattern (but not quite)

**Pattern:** Switch-based dispatch instead of full Visitor pattern.

```csharp
switch (statement)
{
    case ClassDef classDef:
        ResolveClassDeclaration(classDef);
        break;
    // ...
}
```

**Why not Visitor?** 
- Visitor requires all AST nodes to accept visitors
- This approach is simpler and more C#-idiomatic
- Pattern matching is concise and type-safe

**Trade-off:** Less extensible than Visitor (can't add new operations without modifying this class), but more readable.

---

### 5.4 Error Collection vs Exception Throwing

**Design decision:** Accumulate errors in `_errors` list rather than throwing.

**Benefits:**
- Reports multiple errors per compilation
- Better IDE integration (show all errors at once)
- Compiler can continue to later passes to find more issues

**When exceptions ARE thrown:** Fatal errors that prevent further processing (e.g., malformed AST) would still throw in practice.

---

### 5.5 Deferred Type Resolution

**Design decision:** Set types to `SemanticType.Unknown` and resolve later.

**Rationale:**
- Name resolution is about *what exists*, not *what types things have*
- Type checking requires all declarations to be known first
- Separation of concerns: each pass has a clear responsibility

**Example:**
```csharp
Type = SemanticType.Unknown,  // Will be resolved during type checking
```

This appears in parameter symbols, return types, and variable types during name resolution.

---

### 5.6 Python Naming Convention Mapping

**Design decision:** Automatically infer access levels from naming.

**Alternative approaches:**
- Require explicit access modifiers (more verbose)
- Default everything to public (less safe)

**Sharpy's approach:** Honor Python conventions but enforce them in generated C#.

---

## 6. Debugging Tips

### 6.1 Enable Detailed Logging

Pass a logger to the constructor:
```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var resolver = new NameResolver(symbolTable, logger);
```

Look for output like:
```
[DEBUG] Resolving class declaration: MyClass
[DEBUG] Resolving method declaration: MyClass.my_method
```

---

### 6.2 Check Symbol Table State

After `ResolveDeclarations()`, inspect the symbol table:
```csharp
var symbol = symbolTable.Lookup("MyClass");
Console.WriteLine($"Found: {symbol?.Name}, Kind: {symbol?.Kind}");
```

Or examine specific scopes:
```csharp
Console.WriteLine($"Current scope: {symbolTable.CurrentScope.Name}");
Console.WriteLine($"Symbols in scope: {symbolTable.CurrentScope.Symbols.Count}");
```

---

### 6.3 Inspect Collected Errors

```csharp
resolver.ResolveDeclarations(module);
foreach (var error in resolver.Errors)
{
    Console.WriteLine($"[{error.Line}:{error.Column}] {error.Message}");
}
```

**Common errors to watch for:**
- "Class 'X' is already defined" → Duplicate declaration
- "Base class 'X' not found" → Typo or missing import
- "Structs can only implement interfaces" → Invalid inheritance

---

### 6.4 Verify Two-Pass Execution

Set breakpoints or add logging:
1. After `ResolveDeclarations()` - check that `_classDefs` is populated
2. After `ResolveInheritance()` - check that `TypeSymbol.BaseType` is set

**Common issue:** Forgetting to call `ResolveInheritance()` after `ResolveDeclarations()`, leading to unresolved base types.

---

### 6.5 Check Scope Stack Balance

**Problem:** Mismatched `EnterScope()`/`ExitScope()` calls can corrupt the symbol table.

**Debug check:**
```csharp
var depthBefore = symbolTable.ScopeDepth;
resolver.ResolveDeclarations(module);
var depthAfter = symbolTable.ScopeDepth;

if (depthBefore != depthAfter)
    Console.WriteLine($"WARNING: Scope leak! Before: {depthBefore}, After: {depthAfter}");
```

**Expected:** Depth should be the same before and after (usually 1 for global scope).

---

### 6.6 Trace Method Resolution

Add logging to `ResolveMethodDeclaration()`:
```csharp
Console.WriteLine($"Method: {method.Name}");
Console.WriteLine($"  Access: {accessLevel}");
Console.WriteLine($"  Static: {isStatic}, Abstract: {isAbstract}");
Console.WriteLine($"  Decorators: {string.Join(", ", method.Decorators.Select(d => d.Name))}");
```

This helps debug decorator parsing issues.

---

## 7. Contribution Guidelines

### 7.1 What to Contribute

**High-impact additions:**

1. **Complete import resolution** (`ResolveImport`, `ResolveFromImport`)
   - Load external modules
   - Resolve cross-module references
   - Handle circular imports

2. **Generic type constraints**
   - Parse `where T : SomeType` constraints
   - Store in `TypeSymbol.TypeParameters`
   - Validate during inheritance resolution

3. **Property declarations**
   - Add `ResolvePropertyDeclaration()` method
   - Support getter/setter access levels
   - Handle Python's `@property` decorator

4. **Nested class support**
   - Allow classes inside classes
   - Properly handle scope nesting
   - Generate correct C# nested types

5. **Better error messages**
   - Add suggestions for typos ("Did you mean 'MyClass'?")
   - Show declaration location for duplicates
   - Hint at correct syntax for common mistakes

---

### 7.2 Testing Your Changes

**When adding features:**

1. **Write tests in `Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`:**
   ```csharp
   [Fact]
   public void TestResolveGenericClass()
   {
       var source = "class MyClass[T]: pass";
       var resolver = CreateResolver();
       var module = Parse(source);
       
       resolver.ResolveDeclarations(module);
       
       var symbol = symbolTable.Lookup("MyClass") as TypeSymbol;
       Assert.NotNull(symbol);
       Assert.Single(symbol.TypeParameters);
       Assert.Equal("T", symbol.TypeParameters[0]);
   }
   ```

2. **Test error cases:**
   ```csharp
   [Fact]
   public void TestRedefinitionError()
   {
       var source = """
           class Foo: pass
           class Foo: pass  # Error: duplicate
           """;
       
       resolver.ResolveDeclarations(Parse(source));
       
       Assert.Single(resolver.Errors);
       Assert.Contains("already defined", resolver.Errors[0].Message);
   }
   ```

3. **Test inheritance resolution:**
   ```csharp
   [Fact]
   public void TestClassInheritance()
   {
       var source = """
           class Base: pass
           class Derived(Base): pass
           """;
       
       resolver.ResolveDeclarations(Parse(source));
       resolver.ResolveInheritance();
       
       var derived = symbolTable.Lookup("Derived") as TypeSymbol;
       Assert.NotNull(derived.BaseType);
       Assert.Equal("Base", derived.BaseType.Name);
   }
   ```

---

### 7.3 Code Style Guidelines

**Follow existing patterns:**

1. **Use pattern matching for AST traversal:**
   ```csharp
   case NewNodeType newNode:
       ResolveNewNode(newNode);
       break;
   ```

2. **Check for redefinitions first:**
   ```csharp
   if (_symbolTable.Lookup(name, searchParents: false) != null)
   {
       AddError($"'{name}' is already defined", ...);
       return;
   }
   ```

3. **Use descriptive scope names:**
   ```csharp
   _symbolTable.EnterScope($"function:{functionDef.Name}");
   ```

4. **Always balance Enter/ExitScope:**
   ```csharp
   _symbolTable.EnterScope(name);
   try
   {
       // ... resolve members ...
   }
   finally
   {
       _symbolTable.ExitScope();
   }
   ```

5. **Log important actions:**
   ```csharp
   _logger.LogDebug($"Resolving {kindName} declaration: {name}");
   ```

---

### 7.4 Common Pitfalls to Avoid

**❌ Don't resolve types during name resolution:**
```csharp
// WRONG:
var paramType = ResolveType(param.TypeAnnotation);  // Too early!

// CORRECT:
Type = SemanticType.Unknown,  // Defer to TypeChecker
```

**❌ Don't forget to save for second pass:**
```csharp
// WRONG:
_symbolTable.Define(typeSymbol);  // Inheritance won't be resolved

// CORRECT:
_symbolTable.Define(typeSymbol);
_classDefs.Add(classDef);  // Save for ResolveInheritance()
```

**❌ Don't search parent scopes for redefinition checks:**
```csharp
// WRONG:
if (_symbolTable.Lookup(name) != null)  // Finds parent scopes too

// CORRECT:
if (_symbolTable.Lookup(name, searchParents: false) != null)
```

**❌ Don't throw exceptions for recoverable errors:**
```csharp
// WRONG:
throw new SemanticException("Type not found");

// CORRECT:
AddError("Type not found", line, column);
return;  // Continue processing
```

---

### 7.5 Integration with Other Passes

**When modifying NameResolver, consider impact on:**

- **`TypeChecker`**: Expects all symbols to be registered with `Unknown` types
- **`CodeGenerator`**: Relies on complete symbol tables with resolved inheritance
- **`ImportResolver`**: May need coordination for module-level imports
- **`SemanticAnalyzer`**: Orchestrates the passes - check call order

**Communication contract:**
- NameResolver populates `SymbolTable` with all declarations
- Sets types to `SemanticType.Unknown` for later resolution
- Resolves inheritance relationships completely
- Collects errors without throwing exceptions

---

## Summary

The `NameResolver` is the foundation of Sharpy's semantic analysis. It builds the symbol tables that all subsequent passes depend on. By separating name resolution from type checking, it enables forward references and keeps concerns cleanly separated.

**Key takeaways:**
- **Two-phase design**: Declarations first, inheritance second
- **Scope management**: Hierarchical scopes for proper name shadowing
- **Deferred type resolution**: Names now, types later
- **Error tolerance**: Collect errors, don't throw
- **Python conventions**: Automatic access level inference

When debugging name resolution issues, start by checking the symbol table state and error list. When contributing, focus on completing imports, adding properties, or improving error messages. Always test both success and error cases, and maintain the two-pass architecture.
