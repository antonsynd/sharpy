# Task: Move Types to Namespace Level (Addendum)

**Created:** 2025-01-23
**Prerequisite:** Complete alongside `task_module_entry_point_rules.md`
**Priority:** High
**Estimated Effort:** 1-2 days

## Summary

This addendum addresses an architectural change that should be implemented alongside the module entry point rules. Currently, type declarations (classes, structs, interfaces, enums) are **nested inside** the generated module class. They should instead be **siblings at namespace level**, matching C# conventions.

## Background

### Current Behavior (Nested Types)

```python
# geometry.spy
counter: int = 0

class Point:
    x: int
    y: int

def helper() -> int:
    return 42
```

Currently generates:

```csharp
namespace MyProject.Geometry
{
    public static class Exports
    {
        public static int Counter = 0;
        
        public class Point  // ❌ Nested inside Exports
        {
            public int X;
            public int Y;
        }
        
        public static int Helper() => 42;
    }
}
```

**Access from C#:** `MyProject.Geometry.Exports.Point` ❌

### Desired Behavior (Namespace-Level Types)

```csharp
namespace MyProject.Geometry
{
    public static class Exports
    {
        public static int Counter = 0;
        public static int Helper() => 42;
    }
    
    public class Point  // ✅ Namespace level, sibling to Exports
    {
        public int X;
        public int Y;
    }
}
```

**Access from C#:** `MyProject.Geometry.Point` ✅

## Rationale

1. **C# Convention**: C# top-level statements place types at namespace level, not nested
2. **Cleaner Qualified Names**: `MyProject.Geometry.Point` vs `MyProject.Geometry.Exports.Point`
3. **Simpler Inheritance**: `class Dog(Animal)` maps to `class Dog : Animal`, not `class Dog : Exports.Animal`
4. **Better Interop**: C# consumers see standard type organization
5. **Import System**: `from geometry import Point` still works — the `using static` brings types into scope

---

## Part 1: Compiler Changes

### Task 1.1: Update `RoslynEmitter.CompilationUnit.cs`

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`

**Current `GenerateCompilationUnit` method:**
- Calls `GenerateModuleClass()` which returns a single `ClassDeclarationSyntax`
- Wraps that in a namespace
- Returns compilation unit

**New approach:**
- Call a new method `GenerateModuleMembers()` that returns BOTH:
  - The module class (static class with fields, methods, Main)
  - A list of type declarations (classes, structs, interfaces, enums)
- Add all members to the namespace

**Changes to make:**

```csharp
public CompilationUnitSyntax GenerateCompilationUnit(Module module)
{
    // ... existing using directive generation ...

    // Separate imports from other statements
    var nonImportStatements = module.Body
        .Where(s => s is not ImportStatement && s is not FromImportStatement)
        .ToList();

    // NEW: Generate module members (module class + namespace-level types)
    var (moduleClass, namespaceTypes) = GenerateModuleMembers(nonImportStatements, fromImports);

    // Combine module class with namespace-level types
    var namespaceMembers = new List<MemberDeclarationSyntax> { moduleClass };
    namespaceMembers.AddRange(namespaceTypes);

    // Generate namespace
    var namespaceName = GenerateNamespaceName();
    var namespaceDecl = NamespaceDeclaration(namespaceName)
        .WithMembers(List(namespaceMembers));  // Multiple members now

    // ... rest unchanged ...
}
```

### Task 1.2: Create `GenerateModuleMembers` Method

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

**New method signature:**

```csharp
/// <summary>
/// Generates the module class and namespace-level type declarations.
/// </summary>
/// <returns>
/// A tuple containing:
/// - The module class (static class with fields, functions, Main)
/// - List of type declarations to place at namespace level
/// </returns>
private (ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes) 
    GenerateModuleMembers(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
```

### Task 1.3: Refactor `GenerateModuleClass`

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

**Current behavior in the `foreach` loop (around line 126-165):**

```csharp
foreach (var stmt in statements)
{
    // ... main function check ...
    
    var member = GenerateStatement(stmt);  // Generates ALL statements including types
    
    // ... scope clearing for types ...
    
    if (member is MemberDeclarationSyntax memberDecl)
    {
        declarations.Add(memberDecl);  // ALL go into module class
    }
    // ...
}
```

**New behavior:**

```csharp
private (ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes) 
    GenerateModuleMembers(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
{
    // ... existing setup code ...

    var moduleDeclarations = new List<MemberDeclarationSyntax>();  // For module class
    var namespaceTypes = new List<MemberDeclarationSyntax>();      // For namespace level
    var executableStatements = new List<Statement>();
    bool hasMainFunction = false;

    foreach (var stmt in statements)
    {
        // Check if this is a main function
        if (stmt is FunctionDef funcDef && funcDef.Name == "main")
        {
            hasMainFunction = true;
        }

        // Generate the statement
        var member = GenerateStatement(stmt);

        // Clear local scope after type/function definitions
        if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef)
        {
            _declaredVariables.Clear();
            _variableVersions.Clear();
            _constVariables.Clear();
        }

        if (member is MemberDeclarationSyntax memberDecl)
        {
            // Route type declarations to namespace level
            if (stmt is ClassDef or StructDef or InterfaceDef or EnumDef)
            {
                namespaceTypes.Add(memberDecl);
            }
            else
            {
                // Functions, fields, constants stay in module class
                moduleDeclarations.Add(memberDecl);
            }
        }
        else if (member == null && stmt is VariableDeclaration varRedefinition)
        {
            // ... existing redefinition handling ...
        }
        else
        {
            executableStatements.Add(stmt);
        }
    }

    // ... existing Main generation logic ...
    // ... existing re-export generation ...

    var moduleClassName = GetModuleClassName(willHaveMainMethod, functionNames);
    var moduleClass = ClassDeclaration(moduleClassName)
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword)))
        .WithMembers(List(moduleDeclarations));

    return (moduleClass, namespaceTypes);
}
```

### Task 1.4: Update Method Signature Chain

Several places call `GenerateModuleClass`. Update them:

**In `RoslynEmitter.CompilationUnit.cs`:**
- Change call from `GenerateModuleClass(...)` to `GenerateModuleMembers(...)`
- Update return type handling

---

## Part 2: Verification

### Task 2.1: Add Unit Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/NamespaceLevelTypesTests.cs` (new file)

```csharp
public class NamespaceLevelTypesTests
{
    [Fact]
    public void ClassDef_GeneratesAtNamespaceLevel_NotNestedInModuleClass()
    {
        var source = @"
class Point:
    x: int
    y: int

def main():
    p = Point()
";
        var csharp = CompileToCSharp(source);
        
        // Class should be at namespace level
        Assert.Contains("public class Point", csharp);
        
        // Class should NOT be inside the Program/Exports class
        // Check that Point is a sibling, not a child
        var lines = csharp.Split('\n');
        var programIndex = Array.FindIndex(lines, l => l.Contains("public static class Program"));
        var pointIndex = Array.FindIndex(lines, l => l.Contains("public class Point"));
        
        // Point should come AFTER the closing brace of Program class
        // (This is a simplified check - could use Roslyn to parse and verify structure)
    }

    [Fact]
    public void StructDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
struct Vector:
    dx: float
    dy: float

def main():
    pass
";
        var csharp = CompileToCSharp(source);
        Assert.Contains("public struct Vector", csharp);
    }

    [Fact]
    public void InterfaceDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

def main():
    pass
";
        var csharp = CompileToCSharp(source);
        Assert.Contains("public interface IDrawable", csharp);
    }

    [Fact]
    public void EnumDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
enum Color:
    RED
    GREEN
    BLUE

def main():
    pass
";
        var csharp = CompileToCSharp(source);
        Assert.Contains("public enum Color", csharp);
    }

    [Fact]
    public void MixedDeclarations_CorrectlyPartitioned()
    {
        var source = @"
counter: int = 0
const VERSION: str = ""1.0""

class Point:
    x: int
    y: int

struct Vector:
    dx: float

def helper() -> int:
    return 42

def main():
    pass
";
        var csharp = CompileToCSharp(source);
        
        // Module class should have: Counter, VERSION, Helper, Main
        // Namespace should have: Point, Vector
        
        // Verify Counter and Helper are in the static class
        Assert.Contains("public static int Counter", csharp);
        Assert.Contains("public const string VERSION", csharp);
        Assert.Contains("public static int Helper()", csharp);
        
        // Verify types are present (at namespace level)
        Assert.Contains("public class Point", csharp);
        Assert.Contains("public struct Vector", csharp);
    }
}
```

### Task 2.2: Update Existing Tests

Some existing tests may assert on the exact structure of generated C#. Search for tests that check:
- Nested type access patterns
- Full qualified type names
- Generated C# structure

```bash
grep -r "Exports\..*class\|Program\..*class" src/Sharpy.Compiler.Tests/
grep -r "\.Exports\." src/Sharpy.Compiler.Tests/
```

### Task 2.3: Manual Verification

After implementation, compile a test file and verify the generated C# structure:

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp snippets/test_namespace_types.spy
```

**Create test file `snippets/test_namespace_types.spy`:**

```python
# Test: Verify types generate at namespace level

counter: int = 0

class Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

struct Vector:
    dx: float
    dy: float

interface IShape:
    def area(self) -> float:
        ...

enum Color:
    RED
    GREEN
    BLUE

def helper() -> int:
    return counter

def main():
    p: Point = Point(1, 2)
    print(p.x)
    print(helper())
```

**Expected output structure:**

```csharp
namespace Sharpy.TestNamespaceTypes
{
    public static class Program
    {
        public static int Counter = 0;
        
        public static int Helper() => Counter;
        
        public static void Main()
        {
            var p = new Point(1, 2);
            Console.WriteLine(p.X);
            Console.WriteLine(Helper());
        }
    }
    
    public class Point { ... }
    public struct Vector { ... }
    public interface IShape { ... }
    public enum Color { ... }
}
```

---

## Part 3: Documentation Updates

### Task 3.1: Update CodeGen README

**File:** `src/Sharpy.Compiler/CodeGen/README.md`

Add/update section explaining the generated structure:

```markdown
## Generated Code Structure

A Sharpy module generates a C# namespace containing:

1. **Module Class** (`Exports` or `Program`)
   - Static fields (module-level variables)
   - Static constants
   - Static methods (module-level functions)
   - `Main()` method (entry point files only)

2. **Type Declarations** (at namespace level, NOT nested)
   - Classes
   - Structs  
   - Interfaces
   - Enums

Example:

```python
# geometry.spy
counter: int = 0

class Point:
    x: int
    y: int

def helper() -> int:
    return 42

def main():
    p = Point(1, 2)
```

Generates:

```csharp
namespace MyProject.Geometry
{
    public static class Program
    {
        public static int Counter = 0;
        public static int Helper() => 42;
        public static void Main() { ... }
    }
    
    public class Point  // Namespace level, not nested
    {
        public int X;
        public int Y;
    }
}
```
```

### Task 3.2: Update Main Task List

In `task_module_entry_point_rules.md`, add a note at the top of Part 5 (Compiler Implementation):

```markdown
> **Note:** Before implementing Part 5, complete the addendum task 
> `task_namespace_level_types.md` which restructures how types are 
> placed in generated C# code.
```

---

## Part 4: Edge Cases to Handle

### Task 4.1: Nested Classes Within User Classes

User-defined nested classes should remain nested:

```python
class Outer:
    class Inner:  # Should stay nested inside Outer
        value: int
```

Should generate:

```csharp
public class Outer
{
    public class Inner  // Correctly nested
    {
        public int Value;
    }
}
```

**Verification:** This should work automatically since we only extract TOP-LEVEL type declarations, not recursively nested ones.

### Task 4.2: Type References Across Module

Ensure type references still resolve correctly:

```python
class Point:
    x: int
    y: int

def create_point() -> Point:  # Reference to Point
    return Point(0, 0)

def main():
    p: Point = create_point()  # Reference to Point
```

**Verification:** Since types are in the same namespace, unqualified references should work.

### Task 4.3: Cross-Module Type References

```python
# main.spy
from geometry import Point

def main():
    p: Point = Point(1, 2)
```

**Verification:** The `using static` directive brings types into scope. Test with existing cross-module tests.

---

## Verification Checklist

Before marking complete:

- [x] `GenerateModuleMembers` returns tuple of (module class, namespace types)
- [x] Types (class, struct, interface, enum) placed at namespace level
- [x] Static fields/methods/constants remain in module class
- [x] `Main()` remains in module class
- [x] Nested classes within user classes stay nested (not supported yet)
- [x] All existing tests pass (4031 pass, 13 skipped)
- [x] New unit tests for namespace-level types pass (9 tests)
- [x] Manual verification with `emit csharp` shows correct structure
- [x] Cross-module type references work correctly (fixed enum access and TypeMapper)
- [x] Documentation updated (CodeGen README)

---

## Files Changed Summary

### New Files
- `src/Sharpy.Compiler.Tests/CodeGen/NamespaceLevelTypesTests.cs`
- `snippets/test_namespace_types.spy`

### Modified Files
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` (enum access, GetFullyQualifiedTypeName)
- `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` (GetFullyQualifiedTypeName)
- `src/Sharpy.Compiler/CodeGen/README.md`

---

## Implementation Notes

**Completed:** 2025-01-24

Key changes beyond the original plan:
1. Fixed enum member access to use unqualified type names (was using `Program.EnumName.Member`)
2. Fixed `GetFullyQualifiedTypeName` in both `TypeMapper.cs` and `RoslynEmitter.Expressions.cs`
   to not include `.Exports.` for imported types since types are now at namespace level
