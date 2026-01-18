# Dogfood Follow-up Tasks: Implementation Guide

This document provides step-by-step implementation guidance for the remaining compiler issues discovered during dogfood investigation. Each task is broken down into atomic, testable steps that can be completed independently.

---

## Overview

| Issue | Priority | Complexity | Files Affected |
|-------|----------|------------|----------------|
| 1. Abstract class interface implementation | Medium | Medium | `RoslynEmitter.cs`, potentially `NameResolver.cs` |
| 2. ProjectCompiler cross-file imports | **High** | High | `RoslynEmitter.cs`, `ProjectCompiler.cs` |
| 3. Interface method return type inference | Medium | Low-Medium | `TypeChecker.cs` |

**Recommended Order:** Issue 3 → Issue 1 → Issue 2

- Issue 3 is the simplest fix and unblocks polymorphic interface usage
- Issue 1 is a self-contained code generation fix
- Issue 2 is the most complex and may require coordinated changes across multiple files

---

## Issue 1: Abstract Class Interface Implementation

### Problem Summary

When an abstract class declares it implements an interface but doesn't provide all interface methods (expecting subclasses to implement them), the generated C# fails to compile.

**Example failing code:**
```sharpy
interface IDisplayable:
    def display(self) -> None: ...

@abstract
class Shape(IDisplayable):
    # Missing display() method - expects subclass to implement
    def area(self) -> int: ...
```

**Error:** `'Shape' does not implement interface member 'IDisplayable.Display()'`

### Root Cause

The `RoslynEmitter.GenerateClassDeclaration()` method doesn't check for missing interface methods when generating an abstract class. In C#, even abstract classes must either implement interface methods OR declare them as abstract.

### Solution Design

When generating an abstract class that implements interfaces:
1. Collect all interface methods the class declares it implements
2. Check which methods are NOT defined in the class body
3. For each missing method, generate an abstract method stub

### Implementation Tasks

#### Task 1.1: Create Interface Method Collection Helper

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** Add a new private method near the `GenerateClassDeclaration` method

**Implementation:**
```csharp
/// <summary>
/// Collects all method signatures from interfaces that a class implements.
/// Returns a dictionary mapping method name to (return type, parameters) tuple.
/// </summary>
private Dictionary<string, (TypeAnnotation ReturnType, List<ParameterDef> Parameters)> 
    CollectInterfaceMethodSignatures(List<TypeAnnotation> baseTypes)
{
    var result = new Dictionary<string, (TypeAnnotation, List<ParameterDef>)>();
    
    foreach (var baseType in baseTypes)
    {
        // Get the type name (handling generic types if needed)
        var typeName = baseType switch
        {
            SimpleType st => st.Name,
            GenericType gt => gt.Name,
            _ => null
        };
        
        if (typeName == null) continue;
        
        // Look up the interface symbol
        var symbol = _context.SymbolTable.Lookup(typeName);
        if (symbol is not TypeSymbol typeSymbol) continue;
        if (typeSymbol.TypeKind != TypeKind.Interface) continue;
        
        // Collect all methods from this interface
        foreach (var method in typeSymbol.Methods)
        {
            // Skip if already collected (from another interface or override)
            if (result.ContainsKey(method.Name)) continue;
            
            result[method.Name] = (method.ReturnType, method.Parameters);
        }
    }
    
    return result;
}
```

**Test:** Unit test that verifies interface methods are correctly collected from simple and generic interfaces.

---

#### Task 1.2: Create Method Existence Checker

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** Add near the helper method from Task 1.1

**Implementation:**
```csharp
/// <summary>
/// Returns the set of method names that are defined in the class body.
/// Includes methods from __init__ and regular methods.
/// </summary>
private HashSet<string> GetDefinedMethodNames(List<Statement> classBody)
{
    var defined = new HashSet<string>();
    
    foreach (var stmt in classBody)
    {
        if (stmt is FunctionDef func)
        {
            defined.Add(func.Name);
        }
    }
    
    return defined;
}
```

**Test:** Unit test that verifies method names are correctly extracted from class body.

---

#### Task 1.3: Create Abstract Stub Generator

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** Add near the helper methods

**Implementation:**
```csharp
/// <summary>
/// Generates an abstract method stub for an interface method that is not implemented.
/// </summary>
private MethodDeclarationSyntax GenerateAbstractMethodStub(
    string methodName, 
    TypeAnnotation returnType, 
    List<ParameterDef> parameters)
{
    // Map return type
    var returnTypeSyntax = returnType == null 
        ? PredefinedType(Token(SyntaxKind.VoidKeyword))
        : _typeMapper.MapType(returnType);
    
    // Transform method name to PascalCase
    var csharpMethodName = NameMangler.Transform(methodName, NameContext.Method);
    
    // Generate parameters (skip 'self' if present)
    var csharpParams = parameters
        .Where(p => p.Name != "self")
        .Select(p => Parameter(Identifier(NameMangler.Transform(p.Name, NameContext.Parameter)))
            .WithType(_typeMapper.MapType(p.Type)))
        .ToArray();
    
    // Create abstract method declaration
    return MethodDeclaration(returnTypeSyntax, csharpMethodName)
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.AbstractKeyword)))
        .WithParameterList(ParameterList(SeparatedList(csharpParams)))
        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
}
```

**Test:** Unit test that verifies abstract method stubs are correctly generated with proper C# syntax.

---

#### Task 1.4: Integrate into GenerateClassDeclaration

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** Modify the `GenerateClassDeclaration` method, after generating class members

**Implementation:**

Find this section in `GenerateClassDeclaration`:
```csharp
// Generate class members from body
var members = GenerateClassMembers(classDef.Body, className);
classDecl = classDecl.WithMembers(List(members));
```

Modify to:
```csharp
// Generate class members from body
var members = GenerateClassMembers(classDef.Body, className);

// For abstract classes implementing interfaces, generate stubs for missing methods
if (_isInAbstractClass && classDef.BaseClasses.Count > 0)
{
    var interfaceMethods = CollectInterfaceMethodSignatures(classDef.BaseClasses);
    var definedMethods = GetDefinedMethodNames(classDef.Body);
    
    var stubMembers = new List<MemberDeclarationSyntax>();
    
    foreach (var (methodName, (returnType, parameters)) in interfaceMethods)
    {
        // Skip if method is already defined in the class
        if (definedMethods.Contains(methodName)) continue;
        
        // Generate abstract stub
        var stub = GenerateAbstractMethodStub(methodName, returnType, parameters);
        stubMembers.Add(stub);
    }
    
    // Add stubs to members list
    if (stubMembers.Count > 0)
    {
        members = members.Concat(stubMembers).ToList();
    }
}

classDecl = classDecl.WithMembers(List(members));
```

**Test:** Integration test using the dogfood example from Issue 0001.

---

#### Task 1.5: Handle Inherited Interface Methods

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** Enhance `CollectInterfaceMethodSignatures` to handle interface inheritance

**Implementation:** Modify `CollectInterfaceMethodSignatures` to recursively collect methods from base interfaces:

```csharp
private Dictionary<string, (TypeAnnotation ReturnType, List<ParameterDef> Parameters)> 
    CollectInterfaceMethodSignatures(List<TypeAnnotation> baseTypes)
{
    var result = new Dictionary<string, (TypeAnnotation, List<ParameterDef>)>();
    var visited = new HashSet<string>();
    
    void CollectFromInterface(TypeSymbol interfaceSymbol)
    {
        if (visited.Contains(interfaceSymbol.Name)) return;
        visited.Add(interfaceSymbol.Name);
        
        // Collect methods from this interface
        foreach (var method in interfaceSymbol.Methods)
        {
            if (!result.ContainsKey(method.Name))
            {
                result[method.Name] = (method.ReturnType, method.Parameters);
            }
        }
        
        // Recursively collect from base interfaces
        foreach (var baseInterface in interfaceSymbol.BaseInterfaces)
        {
            var baseSymbol = _context.SymbolTable.Lookup(baseInterface);
            if (baseSymbol is TypeSymbol baseTypeSymbol && 
                baseTypeSymbol.TypeKind == TypeKind.Interface)
            {
                CollectFromInterface(baseTypeSymbol);
            }
        }
    }
    
    foreach (var baseType in baseTypes)
    {
        var typeName = baseType switch
        {
            SimpleType st => st.Name,
            GenericType gt => gt.Name,
            _ => null
        };
        
        if (typeName == null) continue;
        
        var symbol = _context.SymbolTable.Lookup(typeName);
        if (symbol is TypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Interface)
        {
            CollectFromInterface(typeSymbol);
        }
    }
    
    return result;
}
```

**Test:** Test with interface inheritance scenario (IJSONSerializable extends ISerializable).

---

#### Task 1.6: Write Comprehensive Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/AbstractClassInterfaceTests.cs` (new file)

**Test Cases:**
1. Abstract class with single interface, missing one method
2. Abstract class with single interface, missing multiple methods
3. Abstract class with multiple interfaces, missing methods from each
4. Abstract class implementing interface that extends another interface
5. Abstract class with some methods implemented, some missing
6. Non-abstract class (should not generate stubs - should error)

---

### Long-Term Considerations for Issue 1

**Future-Proofing:**
- This implementation assumes interface methods are stored in `TypeSymbol.Methods`. Verify this is populated by `NameResolver`.
- If interface default methods are added later (C# 8.0+), we should NOT generate stubs for methods with default implementations.
- Consider adding a diagnostic warning when generating stubs (optional, for clarity).

**Potential One-Way Door:**
- None identified. This is a pure code generation fix with no semantic implications.

---

## Issue 2: ProjectCompiler Cross-File Import Code Generation

### Problem Summary

The `ProjectCompiler` correctly parses and analyzes multi-file projects with `from X import Y`, but the generated C# code doesn't properly connect the imports across files.

**Example:**
```sharpy
# main.spy
from math_utils import square, multiply_by_two
print(square(5))

# math_utils.spy
def square(n: int) -> int:
    return n * n
```

**Error:** `The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Test'`

### Root Cause Analysis

The issue is in how namespaces and `using` directives are generated:

1. **`math_utils.spy`** generates a class in namespace `Sharpy.Test.MathUtils` with class named `Exports`
2. **`main.spy`** generates `using static Sharpy.Test.MathUtils.Exports;` 

But the actual namespace hierarchy might not match what's expected. The issue could be:
- The module namespace isn't being calculated consistently between the importing file and the imported file
- The project namespace prefix isn't being applied correctly in both places

### Solution Design

1. Ensure consistent namespace calculation across all files in a project
2. Verify the `using static` directive matches the actual generated namespace
3. Add debug logging to trace namespace resolution

### Implementation Tasks

#### Task 2.1: Add Debug Logging for Namespace Generation

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Purpose:** Add logging to understand the current namespace generation

**Implementation:**

In `GenerateNamespaceName()`, add logging:
```csharp
private NameSyntax GenerateNamespaceName()
{
    // ... existing code ...
    
    var namespaceName = /* result */;
    
    _context.Logger?.LogDebug($"[Namespace] File: {_context.SourceFilePath}");
    _context.Logger?.LogDebug($"[Namespace] Project: {_context.ProjectNamespace}");
    _context.Logger?.LogDebug($"[Namespace] Generated: {namespaceName}");
    
    return namespaceName;
}
```

In `GenerateFromImportUsings()`, add logging:
```csharp
private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
{
    // ... existing code ...
    
    _context.Logger?.LogDebug($"[Import] Module: {fromImport.Module}");
    _context.Logger?.LogDebug($"[Import] Resolved: {fromImport.ResolvedModulePath}");
    _context.Logger?.LogDebug($"[Import] Using: {fullModuleClass}");
    
    // ... rest of code ...
}
```

**Test:** Run simple_import_test with verbose logging enabled.

---

#### Task 2.2: Analyze Namespace Mismatch

**Action:** Run the simple_import_test with debug logging and document the actual vs expected namespaces.

**Expected Output Analysis:**
- What namespace is `math_utils.spy` generating?
- What `using` directive is `main.spy` generating?
- Do they match?

**Document findings** in the task output before proceeding.

---

#### Task 2.3: Fix Namespace Calculation for Module Files

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** `GenerateNamespaceName()` or related methods

**Hypothesis:** The issue is likely that:
- The source file path isn't being calculated relative to the project root consistently
- Or the module name in `from X import Y` isn't being resolved to the same path

**Implementation:** Based on findings from Task 2.2, implement the fix. Likely fixes:

Option A: Ensure source file path is always relative to project root:
```csharp
// In GenerateNamespaceName
var relativePath = _context.ProjectRootPath != null
    ? Path.GetRelativePath(_context.ProjectRootPath, _context.SourceFilePath)
    : _context.SourceFilePath;
```

Option B: Use the same path normalization in import resolution:
```csharp
// In GenerateFromImportUsings
var normalizedModule = NormalizeModulePath(fromImport.Module, _context.SourceFilePath);
```

---

#### Task 2.4: Ensure Consistent Project Namespace Prefix

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Issue:** Both the generated namespace AND the using directive must include the project namespace prefix.

**Verification:**
1. Check that `GenerateNamespaceName()` includes `_context.ProjectNamespace`
2. Check that `GenerateFromImportUsings()` includes `_context.ProjectNamespace`
3. Ensure both use the same string format

**Implementation:** Add a helper method to ensure consistency:
```csharp
private string GetFullNamespace(string modulePath)
{
    var moduleNamespace = ConvertModuleNameToNamespace(modulePath);
    
    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
    {
        return $"{_context.ProjectNamespace}.{moduleNamespace}";
    }
    return moduleNamespace;
}
```

Use this helper in both namespace generation and import resolution.

---

#### Task 2.5: Handle Relative Imports

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Issue:** Relative imports (starting with `.`) need special handling.

**Implementation:**
```csharp
private string ResolveRelativeImport(string modulePath, string currentFilePath)
{
    if (!modulePath.StartsWith("."))
        return modulePath;
    
    // Get the directory of the current file
    var currentDir = Path.GetDirectoryName(currentFilePath);
    
    // Handle leading dots
    var parts = modulePath.Split('.');
    var upLevels = parts.TakeWhile(p => p == "").Count();
    
    // Go up directories
    for (int i = 1; i < upLevels; i++)
    {
        currentDir = Path.GetDirectoryName(currentDir);
    }
    
    // Combine with remaining path
    var remainingPath = string.Join("/", parts.Skip(upLevels));
    return Path.Combine(currentDir ?? "", remainingPath).Replace(Path.DirectorySeparatorChar, '.');
}
```

---

#### Task 2.6: Update Import Resolution in ProjectCompiler

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

**Issue:** The `FromImportStatement.ResolvedModulePath` might not be set correctly for intra-project imports.

**Implementation:** In `ResolveImports()`, ensure `ResolvedModulePath` is set:
```csharp
// After resolving the module, set the resolved path
if (moduleInfo != null && fromImport.ResolvedModulePath == null)
{
    // Calculate the module path relative to project root
    var relativePath = Path.GetRelativePath(
        config.ProjectDirectory, 
        moduleInfo.Path);
    
    // Convert to dot notation
    fromImport.ResolvedModulePath = Path.ChangeExtension(relativePath, null)
        .Replace(Path.DirectorySeparatorChar, '.')
        .Replace(Path.AltDirectorySeparatorChar, '.');
}
```

**Note:** This may require making `ResolvedModulePath` mutable or using a different approach.

---

#### Task 2.7: Write Integration Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/MultiFileImportTests.cs` (new or existing)

**Test Cases:**
1. Simple `from X import Y` between two files in same directory
2. Import from subdirectory (`from subdir.module import func`)
3. Relative import (`from .sibling import func`)
4. Import with alias (`from module import func as f`)
5. Star import (`from module import *`)
6. Circular type imports (should work for type annotations)

---

### Long-Term Considerations for Issue 2

**Future-Proofing:**
- The module system design supports packages with `__init__.spy`. Ensure the fix doesn't break this.
- Consider how this interacts with .NET assembly references in the future.

**Potential One-Way Door:**
- Namespace structure is somewhat visible to users who look at generated C# code.
- Choose a consistent, predictable namespace scheme.
- Current scheme: `{ProjectNamespace}.{ModulePath}.Exports`
- This is reasonable and should be maintained.

---

## Issue 3: Interface Method Return Type Inference

### Problem Summary

When calling a method through an interface type, the return type isn't properly inferred for augmented assignment.

**Example:**
```sharpy
interface ICalculator:
    def calculate(self, x: int) -> int: ...

def run_calculator(calc: ICalculator, value: int) -> int:
    return calc.calculate(value)

total = 0
total += run_calculator(proc, 1)  # Error: Type '<?>' does not support '+='
```

**Error:** `Type 'int' does not support augmented assignment operator '+=' with right operand of type '<?>'`

### Root Cause

The `TypeChecker` isn't properly resolving the return type when:
1. A method is called through an interface type
2. The interface method's return type needs to be looked up

The `<?>` indicates the return type is being resolved as `Unknown`.

### Solution Design

When type-checking a method call on an interface type:
1. Look up the interface in the symbol table
2. Find the method signature in the interface
3. Return the declared return type from the interface method

### Implementation Tasks

#### Task 3.1: Identify the Method Call Type Resolution Code

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Location:** Find the `CheckFunctionCall` method or `CheckMethodCall` / member access handling

**Action:** Add a breakpoint or logging to understand the current flow for:
```sharpy
calc.calculate(value)  # where calc: ICalculator
```

---

#### Task 3.2: Fix Interface Method Return Type Resolution

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Location:** In the method call type checking logic

**Current Issue:** When the callee is a member access on an interface type, the return type isn't being resolved from the interface definition.

**Implementation:**

Find the member access handling (likely in `CheckExpression` switch for `MemberAccess` or `FunctionCall`):

```csharp
// When checking a method call like: obj.method(args)
private SemanticType CheckMethodCall(FunctionCall call)
{
    if (call.Function is MemberAccess memberAccess)
    {
        // Get the type of the object
        var objectType = CheckExpression(memberAccess.Object);
        
        // If it's an interface type, look up the method in the interface
        if (objectType is InterfaceType interfaceType)
        {
            var interfaceSymbol = _symbolTable.Lookup(interfaceType.Name);
            if (interfaceSymbol is TypeSymbol typeSymbol)
            {
                // Find the method in the interface
                var method = typeSymbol.Methods.FirstOrDefault(
                    m => m.Name == memberAccess.Member);
                
                if (method != null)
                {
                    // Return the declared return type
                    return ResolveType(method.ReturnType);
                }
            }
        }
        
        // ... existing handling for class methods, etc.
    }
    
    // ... existing code
}
```

**Alternative Location:** The issue might be in how `MemberAccess` is typed when the object is an interface:

```csharp
private SemanticType CheckMemberAccess(MemberAccess memberAccess)
{
    var objectType = CheckExpression(memberAccess.Object);
    
    // Handle interface member access
    if (objectType is InterfaceType || 
        (objectType is UserDefinedType udt && udt.Symbol.TypeKind == TypeKind.Interface))
    {
        var interfaceName = objectType switch
        {
            InterfaceType it => it.Name,
            UserDefinedType udt => udt.Symbol.Name,
            _ => null
        };
        
        if (interfaceName != null)
        {
            var symbol = _symbolTable.Lookup(interfaceName);
            if (symbol is TypeSymbol typeSymbol)
            {
                // Look up method
                var method = typeSymbol.Methods.FirstOrDefault(m => m.Name == memberAccess.Member);
                if (method != null)
                {
                    // Return a function type that represents the method
                    return new FunctionType
                    {
                        ParameterTypes = method.Parameters.Select(p => ResolveType(p.Type)).ToList(),
                        ReturnType = ResolveType(method.ReturnType)
                    };
                }
            }
        }
    }
    
    // ... existing code for class members
}
```

---

#### Task 3.3: Add Test for Interface Method Return Types

**File:** `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerInterfaceTests.cs` (new or existing)

**Test Cases:**
```csharp
[Fact]
public void InterfaceMethodCall_ReturnsCorrectType()
{
    var source = @"
interface ICalculator:
    def calculate(self, x: int) -> int: ...

def test(calc: ICalculator) -> int:
    return calc.calculate(5)
";
    
    var result = CompileAndGetType("test", source);
    Assert.Equal(SemanticType.Int, result);
}

[Fact]
public void InterfaceMethodCall_WorksWithAugmentedAssignment()
{
    var source = @"
interface ICalculator:
    def calculate(self, x: int) -> int: ...

def test(calc: ICalculator) -> int:
    total: int = 0
    total += calc.calculate(5)
    return total
";
    
    var errors = CompileAndGetErrors(source);
    Assert.Empty(errors);
}
```

---

#### Task 3.4: Handle Generic Interface Methods

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Issue:** If the interface is generic, the return type might contain type parameters that need substitution.

**Example:**
```sharpy
interface IContainer[T]:
    def get(self, index: int) -> T: ...

def test(container: IContainer[int]) -> int:
    return container.get(0)  # Should resolve to int
```

**Implementation:**
```csharp
// When resolving return type for generic interface
if (objectType is GenericType genericType && 
    genericType.TypeArguments.Count > 0)
{
    // Build type argument mapping
    var typeArgMapping = new Dictionary<string, SemanticType>();
    for (int i = 0; i < typeSymbol.TypeParameters.Count && 
                    i < genericType.TypeArguments.Count; i++)
    {
        typeArgMapping[typeSymbol.TypeParameters[i].Name] = 
            genericType.TypeArguments[i];
    }
    
    // Substitute type parameters in return type
    return SubstituteTypeParameters(method.ReturnType, typeArgMapping);
}
```

---

### Long-Term Considerations for Issue 3

**Future-Proofing:**
- This fix should handle both simple interfaces and generic interfaces
- Consider interaction with variance annotations (`out T`, `in T`) when those are implemented
- Interface default methods (C# 8.0+) should also work with this fix

**Potential One-Way Door:**
- None identified. This is a semantic analysis fix with no visible impact on generated code structure.

---

## Test Infrastructure Notes

After fixing Issue 2, update the test infrastructure:

1. **Update `simple_import_test/main.error`** → `main.expected` with actual expected output:
   ```
   25
   10
   ```

2. **Add more multi-file test cases** for:
   - `import module` (whole module import)
   - `from module import *` (star import)
   - Circular imports (should error at runtime, not compile time for type-only references)
   - Missing module imports (should error)
   - Re-exports via `__init__.spy`

---

## Summary Checklist

### Issue 1: Abstract Class Interface Implementation
- [ ] Task 1.1: Create `CollectInterfaceMethodSignatures` helper
- [ ] Task 1.2: Create `GetDefinedMethodNames` helper
- [ ] Task 1.3: Create `GenerateAbstractMethodStub` helper
- [ ] Task 1.4: Integrate into `GenerateClassDeclaration`
- [ ] Task 1.5: Handle inherited interface methods
- [ ] Task 1.6: Write comprehensive tests

### Issue 2: ProjectCompiler Cross-File Imports
- [ ] Task 2.1: Add debug logging for namespace generation
- [ ] Task 2.2: Analyze namespace mismatch (document findings)
- [ ] Task 2.3: Fix namespace calculation for module files
- [ ] Task 2.4: Ensure consistent project namespace prefix
- [ ] Task 2.5: Handle relative imports
- [ ] Task 2.6: Update import resolution in ProjectCompiler
- [ ] Task 2.7: Write integration tests

### Issue 3: Interface Method Return Type Inference
- [ ] Task 3.1: Identify method call type resolution code
- [ ] Task 3.2: Fix interface method return type resolution
- [ ] Task 3.3: Add test for interface method return types
- [ ] Task 3.4: Handle generic interface methods

---

## Appendix: Reference Files

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | C# code generation |
| `src/Sharpy.Compiler/Project/ProjectCompiler.cs` | Multi-file compilation orchestration |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Type checking and inference |
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Symbol table population |
| `src/Sharpy.Compiler/Semantic/Symbol.cs` | Symbol definitions including `TypeSymbol` |
| `docs/language_specification/interfaces.md` | Interface language spec |
| `docs/language_specification/inheritance.md` | Inheritance language spec |
| `docs/language_specification/import_statements.md` | Import language spec |
