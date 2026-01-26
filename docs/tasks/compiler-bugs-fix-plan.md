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
In `NameResolver.ResolveClassInheritance()`, base class lookup only searches the local symbol table via `_symbolTable.Lookup(baseAnnot.Name)`. When the base class is imported from another module, it may not be found because:

1. The imported symbols are registered by `ImportResolver` but the lookup doesn't search imported symbols
2. The base class name needs to be resolved against the current scope which includes imports

### Code Location
**File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Method**: `ResolveClassInheritance()` (lines 380-420)

```csharp
private void ResolveClassInheritance(ClassDef classDef)
{
    // ...
    foreach (var baseAnnot in classDef.BaseClasses)
    {
        // BUG: Only searches local symbol table, not imported types
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

The symbol table already contains imported types (registered during `ResolveFromImport`), but the issue is that `NameResolver` maintains separate instances for each file, so cross-module symbols aren't visible. The fix requires passing imported type symbols to the NameResolver.

### Implementation Steps

- [ ] **Step 1**: Add a method to NameResolver to register imported type symbols
  - Add field: `private readonly Dictionary<string, TypeSymbol> _importedTypes = new();`
  - Add method: `public void RegisterImportedType(string name, TypeSymbol symbol)`
  
- [ ] **Step 2**: Update the compiler pipeline to pass imported types to NameResolver
  - In `Compiler.cs` or `AssemblyCompiler.cs`, after ImportResolver resolves imports:
    - Extract TypeSymbol entries from `FromImportStatement.ReExportedSymbols`
    - Call `nameResolver.RegisterImportedType()` for each type

- [ ] **Step 3**: Modify `ResolveClassInheritance()` to search imported types
  - After `_symbolTable.Lookup()` returns null, check `_importedTypes`
  - Use the imported TypeSymbol if found
  
```csharp
private void ResolveClassInheritance(ClassDef classDef)
{
    // ...
    foreach (var baseAnnot in classDef.BaseClasses)
    {
        var baseSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
        
        // NEW: Check imported types if not found locally
        if (baseSymbol == null)
        {
            _importedTypes.TryGetValue(baseAnnot.Name, out baseSymbol);
        }
        
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

- [ ] **Step 4**: Apply same fix to `ResolveStructInheritance()` and `ResolveInterfaceInheritance()`

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Semantic/CrossModuleInheritanceTests.cs`
  - Test cases:
    - Class inheriting from imported class
    - Class implementing imported interface
    - Multi-level inheritance across modules

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~CrossModuleInheritance"
```

### Commit Message
```
fix(semantic): resolve cross-module inheritance in NameResolver

- Add _importedTypes dictionary to track imported type symbols
- Search imported types when base class not found in local symbol table
- Apply fix to class, struct, and interface inheritance resolution

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
In `RoslynEmitter.CompilationUnit.cs`, the `GenerateFromImportUsings()` method generates namespace paths based on the import module path rather than using the `TypeSymbol.DefiningModule` which tracks where the type is actually defined.

### Code Location
**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`
**Method**: `GenerateFromImportUsings()` (lines 200-280)

The issue is that `GenerateReExportedTypeNamespaceUsings()` is called but only handles the case where `DefiningModule` differs from `importModuleName`. For direct imports (not re-exports), the namespace is generated from the import path without considering DefiningModule.

### Fix Strategy

1. Ensure `TypeSymbol.DefiningModule` is always set correctly (already done in ImportResolver)
2. When generating using statements for imported types, prefer `DefiningModule` over import path

### Implementation Steps

- [ ] **Step 1**: Verify DefiningModule is set correctly in ImportResolver
  - Check `ExtractFullClassSymbol()`, `ExtractFullStructSymbol()`, etc.
  - Ensure `CanonicalModuleName` is computed and used
  - Add debug logging if needed

- [ ] **Step 2**: Update `GenerateFromImportUsings()` to use DefiningModule for type imports
  - When generating namespace for imported types, check if they have DefiningModule set
  - Use DefiningModule to compute the correct namespace path
  
```csharp
private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
{
    // ... existing code for using static ...
    
    // For each imported type, generate namespace using based on DefiningModule
    var reExportedSymbols = GetReExportedSymbols(fromImport);
    if (reExportedSymbols != null)
    {
        foreach (var (name, symbol) in reExportedSymbols)
        {
            if (symbol is TypeSymbol typeSymbol && !string.IsNullOrEmpty(typeSymbol.DefiningModule))
            {
                var definingNamespace = ConvertModuleNameToNamespace(typeSymbol.DefiningModule);
                string fullNamespace = !string.IsNullOrEmpty(_context.ProjectNamespace)
                    ? $"{_context.ProjectNamespace}.{definingNamespace}"
                    : definingNamespace;
                    
                // Add using statement for the type's actual namespace
                yield return UsingDirective(ParseName(fullNamespace));
            }
        }
    }
}
```

- [ ] **Step 3**: Deduplicate using statements (already handled in `GenerateUsingDirectives()`)

- [ ] **Step 4**: Update `GetFullyQualifiedTypeName()` in `RoslynEmitter.Expressions.cs` to use DefiningModule
  - This method already has logic for DefiningModule, verify it's working correctly

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/CodeGen/CrossModuleNamespaceTests.cs`
  - Test cases:
    - Simple import: `from models import Product`
    - Nested module import: `from lib.math import Calculator`
    - Re-exported type: `from package import SomeClass` (where package re-exports from submodule)

### Verification Command
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test --filter "FullyQualifiedName~CrossModuleNamespace"
```

### Commit Message
```
fix(codegen): use DefiningModule for cross-module namespace resolution

- Update GenerateFromImportUsings to use TypeSymbol.DefiningModule
- Ensure imported types resolve to correct C# namespace
- Add debug logging for namespace generation

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
The parser's `ParseFunctionDef()` method requires a colon after the function signature. For interface methods, the colon and body should be optional since interface methods are implicitly abstract.

### Code Location
**File**: `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`
**Method**: `ParseFunctionDef()` (lines 135-200)

```csharp
private FunctionDef ParseFunctionDef()
{
    // ... parse name, params, return type ...
    
    Expect(TokenType.Colon);  // BUG: Always requires colon
    
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

Add a flag or context to track if we're parsing inside an interface. When inside an interface, make the colon and body optional for method signatures.

### Implementation Steps

- [ ] **Step 1**: Add interface parsing context flag to Parser
  - Add field: `private bool _parsingInterface = false;`

- [ ] **Step 2**: Set the flag in `ParseInterfaceDef()` before parsing body
  ```csharp
  private InterfaceDef ParseInterfaceDef()
  {
      // ... existing code ...
      
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

- [ ] **Step 3**: Modify `ParseFunctionDef()` to handle interface methods
  ```csharp
  private FunctionDef ParseFunctionDef()
  {
      // ... parse name, params, return type ...
      
      // For interface methods, colon is optional
      if (_parsingInterface && Current.Type != TokenType.Colon)
      {
          // No body - implicit abstract method
          ExpectNewline();
          
          return new FunctionDef
          {
              Name = name,
              // ... other properties ...
              Body = ImmutableArray.Create<Statement>(
                  new ExpressionStatement
                  {
                      Expression = new EllipsisLiteral { /* position info */ },
                      // ... position info ...
                  }
              ),
              // ...
          };
      }
      
      Expect(TokenType.Colon);
      // ... rest of existing code ...
  }
  ```

- [ ] **Step 4**: Update `ValidateInterfaceMethod()` in NameResolver to accept bodyless methods
  - The method already checks for `...` or `pass` bodies
  - Ensure it also accepts the synthesized ellipsis body from the parser

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Parser/InterfaceMethodParsingTests.cs`
  - Test cases:
    - Interface method without body: `def foo(self) -> str`
    - Interface method with explicit ellipsis: `def foo(self) -> str: ...`
    - Interface method with pass: `def foo(self) -> str: pass`
    - Regular class method still requires body

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
In `TypeChecker.Expressions.cs`, the `CheckListLiteral()` method:
1. Takes the first element's type as the candidate "common type"
2. Checks if each subsequent element is assignable to that type
3. If any element is NOT assignable (e.g., Feature is not assignable to Bug), sets commonType to Unknown

The logic doesn't find the Least Common Ancestor (LCA) in the type hierarchy.

### Code Location
**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
**Method**: `CheckListLiteral()` (lines 1120-1140)

```csharp
private SemanticType CheckListLiteral(ListLiteral list)
{
    if (list.Elements.Length == 0)
    {
        return new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Unknown } };
    }

    var elementTypes = list.Elements.Select(CheckExpression).ToList();
    var commonType = elementTypes[0];

    // BUG: Doesn't find common base type
    foreach (var elemType in elementTypes.Skip(1))
    {
        if (!IsAssignable(elemType, commonType))
        {
            commonType = SemanticType.Unknown;  // Gives up, results in object
            break;
        }
    }

    return new GenericType { Name = "list", TypeArguments = new List<SemanticType> { commonType } };
}
```

### Fix Strategy

Implement a Least Common Ancestor (LCA) algorithm that walks up the inheritance chain of all element types to find the most specific common base type.

### Implementation Steps

- [ ] **Step 1**: Add `FindLeastCommonAncestor()` helper method to TypeChecker.Utilities.cs
  ```csharp
  /// <summary>
  /// Finds the least common ancestor (most specific common base type) of a list of types.
  /// Returns SemanticType.Unknown if no common ancestor exists.
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
          return SemanticType.Unknown;
      
      // For each subsequent type, find common ancestors
      foreach (var type in types.Skip(1))
      {
          var typeAncestors = new HashSet<string>(
              GetTypeAncestorChain(type).Select(t => t.GetDisplayName()));
          
          // Filter ancestor chain to only include common ancestors
          ancestorChain = ancestorChain
              .Where(a => typeAncestors.Contains(a.GetDisplayName()))
              .ToList();
          
          if (ancestorChain.Count == 0)
              return SemanticType.Unknown;
      }
      
      // Return the most specific common ancestor (first in chain)
      return ancestorChain.First();
  }
  ```

- [ ] **Step 2**: Add `GetTypeAncestorChain()` helper method
  ```csharp
  /// <summary>
  /// Gets the inheritance chain for a type, from most specific to least specific.
  /// For UserDefinedType: [Type, BaseType, BaseType.BaseType, ..., object]
  /// </summary>
  private List<SemanticType> GetTypeAncestorChain(SemanticType type)
  {
      var chain = new List<SemanticType> { type };
      
      if (type is UserDefinedType udt && udt.Symbol != null)
      {
          var current = udt.Symbol.BaseType;
          while (current != null)
          {
              chain.Add(new UserDefinedType { Name = current.Name, Symbol = current });
              current = current.BaseType;
          }
      }
      
      // Add object as ultimate base (if not already there)
      if (chain.Last().GetDisplayName() != "object")
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
          return new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Unknown } };
      }

      var elementTypes = list.Elements.Select(CheckExpression).ToList();
      
      // Find least common ancestor of all element types
      var commonType = FindLeastCommonAncestor(elementTypes);

      return new GenericType { Name = "list", TypeArguments = new List<SemanticType> { commonType } };
  }
  ```

- [ ] **Step 4**: Apply same fix to `CheckSetLiteral()` and `CheckDictLiteral()` (for values)

- [ ] **Step 5**: Write unit test
  - Test file: `tests/Sharpy.Compiler.Tests/Semantic/ListTypeInferenceTests.cs`
  - Test cases:
    - Homogeneous list: `[bug1, bug2]` → `list[Bug]`
    - Sibling types: `[bug, feature]` → `list[WorkItem]`
    - Mixed with None: `[bug, None]` → `list[Bug?]`
    - Deep hierarchy: grandchild types find common grandparent
    - No common ancestor: `[1, "str"]` → `list[object]`

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

1. `fix(semantic): resolve cross-module inheritance in NameResolver` (P0-2)
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
| P0-2 | `NameResolver.cs`, `Compiler.cs` or `AssemblyCompiler.cs` |
| P0-1 | `RoslynEmitter.CompilationUnit.cs`, possibly `RoslynEmitter.Expressions.cs` |
| P1-1 | `Parser.Definitions.cs`, possibly `NameResolver.cs` |
| P1-2 | `TypeChecker.Expressions.cs`, `TypeChecker.Utilities.cs` |

---

## Questions/Blockers

If you encounter issues during implementation:

1. **P0-2**: Check how `AssemblyCompiler` orchestrates compilation of multiple files - the symbol table may need to be shared or imported symbols need explicit registration.

2. **P0-1**: Verify that `DefiningModule` is being set correctly by adding debug logging in `ImportResolver.ExtractFullClassSymbol()`.

3. **P1-1**: Consider edge cases like decorators on interface methods, type parameters, etc.

4. **P1-2**: The LCA algorithm may need optimization for deep hierarchies. Also handle interface inheritance (a type implementing multiple interfaces).
