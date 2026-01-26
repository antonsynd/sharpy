# Compiler Bugs Fix Plan

## Overview

This document outlines the implementation plan for fixing 4 critical compiler bugs (2 P0, 2 P1). Each task includes pinpointed code locations, root cause analysis, and step-by-step implementation instructions that can be executed independently by a junior engineer or AI agent.

**Priority Order**: P0-2 → P0-1 → P1-1 → P1-2

---

## Bug P0-2: Cross-Module Inheritance Type Checking

### Problem
When a class inherits from a base class defined in another module, the compiler fails to recognize the inheritance relationship during type checking.

**Example:**
```python
# data_models.spy
class Content:
    title: str
    
# content_types.spy
from data_models import Content

class Article(Content):  # Inherits from imported Content
    author: str

# main.spy
from content_types import Article
from data_models import Content

def process(item: Content) -> None:
    print(item.title)

article = Article()
process(article)  # ERROR: Cannot pass 'Article' to parameter of type 'Content'
```

### Root Cause

**CRITICAL**: The actual root cause is a **compilation phase ordering problem** in `ProjectCompiler.cs`:

1. **Phase 3** (`CollectTypeDeclarations`): Calls `nameResolver.ResolveDeclarations()` then `nameResolver.ResolveInheritance()`
2. **Phase 4** (`ResolveImports`): Calls `ImportResolver` to resolve imports and populate the symbol table with imported types

The problem is that `ResolveInheritance()` runs **BEFORE** imports are resolved! When `ResolveClassInheritance()` calls `_symbolTable.Lookup(baseAnnot.Name)` for an imported base class, the imported type hasn't been added to the symbol table yet.

**Note**: The compiler already uses a SINGLE NameResolver instance across all files (see `ProjectCompiler.cs:261` comment: "Create a SINGLE NameResolver for ALL files").

### Code Location
**Primary File**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
- `CollectTypeDeclarations()` method (lines 250-300)
- `ResolveImports()` method (lines 300-400)

**Secondary File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `ResolveClassInheritance()` method (line 629)

```csharp
// In NameResolver.cs at line 629
private void ResolveClassInheritance(ClassDef classDef)
{
    // ...
    foreach (var baseAnnot in classDef.BaseClasses)
    {
        // BUG: Imported types aren't in symbol table yet (imports resolved in Phase 4)
        var baseSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
        if (baseSymbol == null)
        {
            AddError($"Base type '{baseAnnot.Name}' not found",
                classDef.LineStart, classDef.ColumnStart);
            continue;
        }
        // ...
    }
}
```

### Fix Strategy

**Option A (Recommended)**: Reorder compilation phases so imports are resolved BEFORE inheritance resolution.

**Option B**: Split inheritance resolution into two passes - one for local types (during Phase 3) and one for imported types (after Phase 4).

**Option C**: Add a mechanism to register imported type symbols before inheritance resolution (workaround).

We recommend **Option A** as it's the cleanest architectural fix.

### Implementation Steps

- [ ] **Step 1**: Analyze phase dependencies in `ProjectCompiler.cs`
  - Verify no circular dependencies between import resolution and type declaration collection
  - Check if `ImportResolver` needs type symbols to be pre-registered

- [ ] **Step 2**: Reorder phases in `ProjectCompiler.CompileProject()`
  - Move import resolution to happen BEFORE inheritance resolution
  - Current order: Parse → CollectTypeDeclarations (includes inheritance) → ResolveImports → TypeCheck
  - New order: Parse → CollectTypeDeclarations (declarations only) → ResolveImports → ResolveInheritance → TypeCheck

- [ ] **Step 3**: Split `CollectTypeDeclarations()` into two methods
  ```csharp
  private void CollectTypeDeclarations(ProjectConfig config)
  {
      // Phase 3a: Collect declarations only (no inheritance)
      var nameResolver = new NameResolver(_symbolTable, _logger);
      foreach (var (_, unit) in _projectModel!.Units)
      {
          nameResolver.SetCurrentFilePath(unit.FilePath);
          nameResolver.ResolveDeclarations(unit.Ast);
      }
      
      // Store nameResolver for later use in inheritance resolution
      _sharedNameResolver = nameResolver;
  }
  
  private void ResolveInheritanceRelationships()
  {
      // Phase 3b: Now that imports are resolved, resolve inheritance
      _sharedNameResolver.ResolveInheritance();
  }
  ```

- [ ] **Step 4**: Update `CompileProject()` call order
  ```csharp
  // Phase 3a: Collect type declarations
  CollectTypeDeclarations(config);
  
  // Phase 4: Resolve imports (NOW imports are in symbol table)
  if (!ResolveImports(config))
      return CreateFailureResult();
  
  // Phase 3b: Resolve inheritance (imports are now available)
  ResolveInheritanceRelationships();
  
  // Phase 5: Type checking
  // ...
  ```

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Semantic/CrossModuleInheritanceTests.cs`
  - Test cases:
    - Class inheriting from imported class
    - Class implementing imported interface
    - Multi-level inheritance across modules
    - Transitive imports (A imports from B which imports from C)

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~CrossModuleInheritance"
```

### Commit Message
```
fix(compiler): reorder phases to resolve imports before inheritance

- Split CollectTypeDeclarations into declaration and inheritance phases
- Move import resolution between declaration and inheritance phases
- Ensures imported base types are in symbol table during inheritance resolution

Fixes dogfood issue #0004
```

---

## Bug P0-1: Cross-Module Namespace Resolution

### Problem
When importing types from another module, the generated C# namespace path is incorrect, causing "type or namespace does not exist" errors.

**Example:**
```python
# models.spy (in project root)
class Product:
    name: str

# main.spy
from models import Product

p = Product()  # Generated C# tries to find Product in wrong namespace
```

**Generated (wrong)**:
```csharp
using TestProject.Main.Models;  // Wrong: prefixes importing module's namespace
```

**Expected**:
```csharp
using TestProject.Models;  // Correct: uses type's DefiningModule
```

### Root Cause

Two potential issues:

1. **ImportResolver not setting DefiningModule**: When `ImportResolver.ExtractFullClassSymbol()` extracts type symbols from imported modules, it may not be setting `TypeSymbol.DefiningModule` correctly.

2. **Code generator not using DefiningModule**: In `RoslynEmitter.Expressions.cs`, `GetFullyQualifiedTypeName()` (line 1261) checks `DefiningFilePath` first, then `DefiningModule`. If `DefiningFilePath` is set incorrectly, it will compute the wrong namespace.

### Code Location
**File 1**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- `ExtractFullClassSymbol()` method - verify DefiningModule is set

**File 2**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`
- `GenerateFromImportUsings()` (line 270)
- `GenerateReExportedTypeNamespaceUsings()` (line 340)

**File 3**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
- `GetFullyQualifiedTypeName()` (line 1261)

### Fix Strategy

1. Ensure `TypeSymbol.DefiningModule` is always set correctly during import resolution
2. Ensure code generation uses `DefiningModule` consistently for namespace computation
3. Generate proper `using` statements for imported types' actual namespaces

### Implementation Steps

- [ ] **Step 1**: Add debug logging to ImportResolver to trace DefiningModule assignment
  ```csharp
  // In ExtractFullClassSymbol() or similar
  _logger.LogDebug($"Setting DefiningModule for {typeSymbol.Name} to '{canonicalModuleName}'");
  ```

- [ ] **Step 2**: Verify DefiningModule is set in ImportResolver extraction methods
  - Check `ExtractFullClassSymbol()`, `ExtractFullStructSymbol()`, `ExtractFullInterfaceSymbol()`
  - Ensure `CanonicalModuleName` or equivalent is computed and assigned to `DefiningModule`

- [ ] **Step 3**: Update `GenerateFromImportUsings()` to always generate namespace using for type imports
  ```csharp
  private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
  {
      // ... existing using static generation ...
      
      // Always generate using for type namespaces (not just re-exports)
      var reExportedSymbols = GetReExportedSymbols(fromImport);
      if (reExportedSymbols != null)
      {
          foreach (var (_, symbol) in reExportedSymbols)
          {
              if (symbol is TypeSymbol typeSymbol && !string.IsNullOrEmpty(typeSymbol.DefiningModule))
              {
                  var definingNamespace = ConvertModuleNameToNamespace(typeSymbol.DefiningModule);
                  string fullNamespace = !string.IsNullOrEmpty(_context.ProjectNamespace)
                      ? $"{_context.ProjectNamespace}.{definingNamespace}"
                      : definingNamespace;
                  
                  yield return UsingDirective(ParseName(fullNamespace));
              }
          }
      }
  }
  ```

- [ ] **Step 4**: Review `GetFullyQualifiedTypeName()` logic in RoslynEmitter.Expressions.cs
  - The method at line 1261 checks `DefiningFilePath` first, then `DefiningModule`
  - Ensure both paths compute the correct namespace
  - Consider prioritizing `DefiningModule` over `DefiningFilePath` for imported types

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/CodeGen/CrossModuleNamespaceTests.cs`
  - Test cases:
    - Simple import: `from models import Product` → `using TestProject.Models;`
    - Nested module: `from lib.math import Calculator` → `using TestProject.Lib.Math;`
    - Re-exported type: `from package import SomeClass` → uses actual defining module's namespace

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~CrossModuleNamespace"
```

### Commit Message
```
fix(codegen): use DefiningModule for cross-module namespace resolution

- Ensure ImportResolver sets DefiningModule correctly
- Update GenerateFromImportUsings to emit using for all imported type namespaces
- Verify GetFullyQualifiedTypeName uses correct namespace for imported types

Fixes dogfood issues #0000, #0001
```

---

## Bug P1-1: Interface Method Signature Parsing

### Problem
Interface methods without an explicit body (`:` followed by `...` or `pass`) cause parser errors.

**Example:**
```python
interface IMaintenance:
    def perform_maintenance(self) -> str  # Parser error: Expected Colon, got Dedent
```

**Expected (should also work):**
```python
interface IMaintenance:
    def perform_maintenance(self) -> str: ...  # Works today
```

### Root Cause
The parser's `ParseFunctionDef()` method at line 189 always calls `Expect(TokenType.Colon)`. For interface methods, the colon and body should be optional since interface methods are implicitly abstract.

### Code Location
**File**: `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`
**Method**: `ParseFunctionDef()` (starts at line 160)

```csharp
private FunctionDef ParseFunctionDef()
{
    // ... parse name, params, return type (lines 160-188) ...
    
    Expect(TokenType.Colon);  // Line 189 - BUG: Always requires colon
    
    // Support inline ellipsis syntax: def foo(): ...
    if (Current.Type == TokenType.Ellipsis)
    {
        // ...
    }
    
    ExpectNewline();
    // ...
}
```

### Fix Strategy

Add a parsing context flag to track if we're parsing inside an interface. When inside an interface, make the colon and body optional for method signatures.

### Implementation Steps

- [ ] **Step 1**: Add interface parsing context flag to Parser.cs
  - Add field: `private bool _parsingInterface = false;`

- [ ] **Step 2**: Set the flag in `ParseInterfaceDef()` (in Parser.Definitions.cs)
  ```csharp
  private InterfaceDef ParseInterfaceDef()
  {
      // ... existing code up to body parsing ...
      
      _parsingInterface = true;
      try
      {
          var body = ParseBlock();
      }
      finally
      {
          _parsingInterface = false;
      }
      
      // ...
  }
  ```

- [ ] **Step 3**: Modify `ParseFunctionDef()` to handle interface methods (around line 189)
  ```csharp
  private FunctionDef ParseFunctionDef()
  {
      // ... parse name, params, return type ...
      
      // For interface methods, colon is optional
      if (_parsingInterface && Current.Type != TokenType.Colon)
      {
          // No body - implicit abstract method
          // Current token should be Newline (end of signature)
          ExpectNewline();
          
          var ellipsisExpr = new EllipsisLiteral
          {
              LineStart = startLine,
              ColumnStart = startColumn,
              LineEnd = startLine,
              ColumnEnd = startColumn
          };
          
          return new FunctionDef
          {
              Name = name,
              TypeParameters = typeParams.ToImmutableArray(),
              Parameters = parameters.ToImmutableArray(),
              ReturnType = returnType,
              Body = ImmutableArray.Create<Statement>(
                  new ExpressionStatement
                  {
                      Expression = ellipsisExpr,
                      LineStart = startLine,
                      ColumnStart = startColumn,
                      LineEnd = startLine,
                      ColumnEnd = startColumn
                  }
              ),
              DocString = null,
              LineStart = startLine,
              ColumnStart = startColumn,
              LineEnd = Current.Line,
              ColumnEnd = Current.Column
          };
      }
      
      Expect(TokenType.Colon);
      // ... rest of existing code ...
  }
  ```

- [ ] **Step 4**: Verify `ValidateInterfaceMethod()` in NameResolver.cs accepts synthesized ellipsis body
  - The method at line 594 already checks for `...` or `pass` bodies
  - The synthesized ellipsis body should pass validation

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Parser/InterfaceMethodParsingTests.cs`
  - Test cases:
    - Interface method without body: `def foo(self) -> str`
    - Interface method with explicit ellipsis: `def foo(self) -> str: ...`
    - Interface method with pass: `def foo(self) -> str: pass`
    - Regular class method still requires body (should fail without colon)
    - Interface method with decorators: `@property def foo(self) -> str`
    - Interface method with type parameters: `def foo[T](self, item: T) -> T`

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~InterfaceMethodParsing"
```

### Commit Message
```
fix(parser): allow interface methods without explicit body

- Add _parsingInterface context flag to Parser
- Make colon and body optional for interface method signatures
- Synthesize ellipsis body for bodyless interface methods

Fixes dogfood issue #0002
```

---

## Bug P1-2: Generic List Type Inference with Subclasses

### Problem
When creating a list with elements that are different subclass types sharing a common base, the type inference produces `List<object>` instead of `List<CommonBaseType>`.

**Example:**
```python
class WorkItem:
    id: int

class Bug(WorkItem):
    severity: str

class Feature(WorkItem):
    priority: int

bug = Bug()
feature = Feature()

# Type inference produces List<object> instead of List<WorkItem>
items: list[WorkItem] = [bug, feature]  # ERROR: cannot convert List<object> to List<WorkItem>
```

### Root Cause
In `TypeChecker.Expressions.cs`, the `CheckListLiteral()` method at line 1156:
1. Takes the first element's type as the candidate "common type"
2. Checks if each subsequent element is assignable to that type
3. If any element is NOT assignable (e.g., Feature is not assignable to Bug), sets commonType to Unknown

The logic doesn't find the Least Common Ancestor (LCA) in the type hierarchy.

### Code Location
**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
**Method**: `CheckListLiteral()` (line 1156)

```csharp
private SemanticType CheckListLiteral(ListLiteral list)
{
    if (list.Elements.Length == 0)
    {
        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Unknown }
        };
    }

    var elementTypes = list.Elements.Select(CheckExpression).ToList();
    var commonType = elementTypes[0];

    // BUG: Doesn't find common base type - just checks assignability to first element
    foreach (var elemType in elementTypes.Skip(1))
    {
        if (!IsAssignable(elemType, commonType))
        {
            commonType = SemanticType.Unknown;  // Gives up, results in object
            break;
        }
    }

    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType> { commonType }
    };
}
```

### Fix Strategy

Implement a Least Common Ancestor (LCA) algorithm that walks up the inheritance chain of all element types to find the most specific common base type.

### Implementation Steps

- [ ] **Step 1**: Add `FindLeastCommonAncestor()` helper method to TypeChecker.Utilities.cs
  ```csharp
  /// <summary>
  /// Finds the least common ancestor (most specific common base type) of a list of types.
  /// Returns SemanticType.Object if no more specific common ancestor exists.
  /// Returns SemanticType.Unknown only if types list is empty.
  /// </summary>
  private SemanticType FindLeastCommonAncestor(List<SemanticType> types)
  {
      if (types.Count == 0)
          return SemanticType.Unknown;
      if (types.Count == 1)
          return types[0];
      
      // Get all ancestors of the first type (including itself)
      var ancestorChain = GetTypeAncestorChain(types[0]);
      if (ancestorChain.Count == 0)
          return SemanticType.Object;
      
      // For each subsequent type, find common ancestors
      foreach (var type in types.Skip(1))
      {
          var typeAncestors = new HashSet<string>(
              GetTypeAncestorChain(type).Select(t => GetTypeKey(t)));
          
          // Filter ancestor chain to only include common ancestors
          ancestorChain = ancestorChain
              .Where(a => typeAncestors.Contains(GetTypeKey(a)))
              .ToList();
          
          if (ancestorChain.Count == 0)
              return SemanticType.Object;
      }
      
      // Return the most specific common ancestor (first in chain)
      return ancestorChain.First();
  }
  
  private string GetTypeKey(SemanticType type)
  {
      return type switch
      {
          UserDefinedType udt => udt.Name,
          PrimitiveType pt => pt.Name,
          _ => type.GetDisplayName()
      };
  }
  ```

- [ ] **Step 2**: Add `GetTypeAncestorChain()` helper method
  ```csharp
  /// <summary>
  /// Gets the inheritance chain for a type, from most specific to least specific.
  /// For UserDefinedType: [Type, BaseType, BaseType.BaseType, ..., object]
  /// For primitives: [PrimitiveType, object]
  /// </summary>
  private List<SemanticType> GetTypeAncestorChain(SemanticType type)
  {
      var chain = new List<SemanticType> { type };
      
      if (type is UserDefinedType udt && udt.Symbol != null)
      {
          var current = udt.Symbol.BaseType;
          while (current != null)
          {
              chain.Add(new UserDefinedType 
              { 
                  Name = current.Name, 
                  Symbol = current 
              });
              current = current.BaseType;
          }
      }
      
      // Add object as ultimate base (if not already there)
      var lastTypeName = chain.Last().GetDisplayName().ToLowerInvariant();
      if (lastTypeName != "object" && lastTypeName != "system.object")
      {
          chain.Add(SemanticType.Object);
      }
      
      return chain;
  }
  ```

- [ ] **Step 3**: Update `CheckListLiteral()` to use LCA
  ```csharp
  private SemanticType CheckListLiteral(ListLiteral list)
  {
      if (list.Elements.Length == 0)
      {
          return new GenericType
          {
              Name = "list",
              TypeArguments = new List<SemanticType> { SemanticType.Unknown }
          };
      }

      var elementTypes = list.Elements.Select(CheckExpression).ToList();
      
      // Find least common ancestor of all element types
      var commonType = FindLeastCommonAncestor(elementTypes);

      return new GenericType
      {
          Name = "list",
          TypeArguments = new List<SemanticType> { commonType }
      };
  }
  ```

- [ ] **Step 4**: Apply same fix to `CheckSetLiteral()` and `CheckDictLiteral()` (for values)
  - `CheckSetLiteral()` - around line 1185
  - `CheckDictLiteral()` - around line 1200 (for value types)

- [ ] **Step 5**: Handle edge cases
  - Nullable types: `[bug, None]` → should infer `list[Bug?]`
  - Interface types: If types share interface but not class, consider interface as LCA
  - Generic types: `[list[int], list[str]]` → `list[list[object]]`

- [ ] **Step 6**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Semantic/ListTypeInferenceTests.cs`
  - Test cases:
    - Homogeneous list: `[bug1, bug2]` → `list[Bug]`
    - Sibling types: `[bug, feature]` → `list[WorkItem]`
    - Mixed with None: `[bug, None]` → `list[Bug?]`
    - Deep hierarchy: grandchild types find common grandparent
    - No common class ancestor: `[1, "str"]` → `list[object]`
    - Empty list: `[]` → `list[Unknown]` (unchanged)
    - Single element: `[bug]` → `list[Bug]`

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~ListTypeInference"
```

### Commit Message
```
fix(semantic): implement LCA for list element type inference

- Add FindLeastCommonAncestor() to find common base type
- Add GetTypeAncestorChain() to traverse inheritance hierarchy
- Update CheckListLiteral() to use LCA instead of first element type
- Apply same fix to CheckSetLiteral() and CheckDictLiteral()

Fixes dogfood issue #0003
```

---

## Testing Strategy

After all fixes are implemented:

1. **Unit Tests**: Each bug has dedicated unit tests (see individual sections)

2. **Integration Tests**: Run the full test suite
   ```bash
   cd /Users/anton/Documents/github/sharpy
   dotnet test tests/Sharpy.Compiler.Tests
   ```

3. **Dogfood Validation**: Compile the dogfood test project
   ```bash
   cd /Users/anton/Documents/github/sharpy
   dotnet run --project src/Sharpy.Compiler.CLI -- compile dogfood/
   ```

4. **Self-Compilation Test**: If the compiler can compile itself, run that test

---

## Commit Strategy

Each bug fix should be a separate commit:

1. `fix(compiler): reorder phases to resolve imports before inheritance` (P0-2)
2. `fix(codegen): use DefiningModule for cross-module namespace resolution` (P0-1)
3. `fix(parser): allow interface methods without explicit body` (P1-1)
4. `fix(semantic): implement LCA for list element type inference` (P1-2)

Branch strategy:
```bash
git checkout -b fix/compiler-bugs-jan-2026
# ... make changes ...
git push origin fix/compiler-bugs-jan-2026
```

---

## Files Modified Summary

| Bug | Files Modified |
|-----|----------------|
| P0-2 | `ProjectCompiler.cs` (primary), possibly `NameResolver.cs` |
| P0-1 | `ImportResolver.cs`, `RoslynEmitter.CompilationUnit.cs`, `RoslynEmitter.Expressions.cs` |
| P1-1 | `Parser.cs`, `Parser.Definitions.cs` |
| P1-2 | `TypeChecker.Expressions.cs`, `TypeChecker.Utilities.cs` |

---

## Questions/Blockers

If you encounter issues during implementation:

1. **P0-2**: The key insight is that compilation phases need reordering. Verify no circular dependencies exist between import resolution and type declaration. The `ImportResolver` may need some type information to resolve correctly - check if it can work with just type names (not full TypeSymbols).

2. **P0-1**: Add debug logging in `ImportResolver.ExtractFullClassSymbol()` to verify `DefiningModule` is being set. If it's not, trace through the extraction logic to find where it should be assigned.

3. **P1-1**: Edge cases to consider:
   - Decorators on interface methods (`@property def foo(self) -> str`)
   - Type parameters on interface methods
   - Interface methods returning `None` (void)

4. **P1-2**: The LCA algorithm needs to handle:
   - Interface inheritance (types sharing only interfaces, not class hierarchy)
   - Generic type parameters
   - Nullable types mixed with non-nullable
   - Performance for deep hierarchies (consider caching ancestor chains)
