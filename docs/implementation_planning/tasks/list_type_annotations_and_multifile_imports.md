# Supporting `list[T]` Type Annotations and Multi-File Imports

**Date:** 2026-01-24  
**Status:** Analysis Complete  
**Related Tests:** `skip_module_imports_multifile_0003`, `skip_module_imports_multifile_0006`

## Executive Summary

Two dogfood test cases are failing:

1. **`list[T]` Type Annotations** (test 0006)
   - Parser: ✅ Already supports `list[T]` syntax
   - Type System: ✅ Infrastructure exists for generic type resolution  
   - BuiltinRegistry: ❌ Maps to `Sharpy.Core.List<>` instead of `System.Collections.Generic.List<>`
   - TypeMapper: ❌ Maps to `global::Sharpy.Core.List` instead of `System.Collections.Generic.List`
   - Dogfood Validator: ❌ Explicitly blocks `list[T]` with regex pattern

2. **Multi-File Circular Dependencies** (test 0003)
   - Not actually circular - imports form a DAG: geometry → shapes → analyzer
   - Dogfood validator marked as "invalid per spec" but likely due to infrastructure limitation
   - May need investigation of CLI multi-file handling

---

## Part 1: `list[T]`, `dict[K,V]`, `set[T]` Type Annotations

### Current State

The parser correctly handles `list[T]` syntax in `Parser.Types.cs`:
- `list[int]` is parsed as `TypeAnnotation { Name = "list", TypeArguments = [int] }`
- The shorthand `[T]` is also supported and parsed identically

However, the type system maps to **Sharpy.Core** wrapper types instead of **.NET** types:

| Location | Current Mapping | Required Mapping (per phases.md) |
|----------|-----------------|----------------------------------|
| `BuiltinRegistry.cs` | `Sharpy.Core.List<>` | `System.Collections.Generic.List<>` |
| `TypeMapper.cs` | `global::Sharpy.Core.List` | `System.Collections.Generic.List` |

### Required Changes

#### 1. `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

**Current code (lines 42-44):**
```csharp
// Collections (generic) - not in PrimitiveCatalog
// NOTE: These use Sharpy.Core types, not System.Collections.Generic!
RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
```

**Change to:**
```csharp
// Collections (generic) - v0.1.x uses .NET types directly per phases.md
// Sharpy.Core wrapper types will be introduced in v0.2.x+
RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
```

#### 2. `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

**Current code (static constructor, lines 23-26):**
```csharp
// Add non-primitive type mappings (collections, etc.)
// These are Sharpy runtime types (use global:: to avoid conflicts when output namespace contains "Sharpy")
_builtinTypeMap["list"] = "global::Sharpy.Core.List";
_builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
_builtinTypeMap["set"] = "global::Sharpy.Core.Set";
```

**Change to:**
```csharp
// Add non-primitive type mappings (collections, etc.)
// v0.1.x uses .NET types directly per phases.md (Sharpy.Core wrappers in v0.2.x+)
_builtinTypeMap["list"] = "System.Collections.Generic.List";
_builtinTypeMap["dict"] = "System.Collections.Generic.Dictionary";
_builtinTypeMap["set"] = "System.Collections.Generic.HashSet";
```

#### 3. `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` - `CreateDictType` method

**Current code (line 206):**
```csharp
public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
{
    return GenericName("global::Sharpy.Core.Dict")
        .WithTypeArgumentList(
            TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
}
```

**Change to:**
```csharp
public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
{
    return GenericName("System.Collections.Generic.Dictionary")
        .WithTypeArgumentList(
            TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
}
```

### Method Name Mapping Impact

Per phases.md:
> **Method Naming:** In v0.1.x, .NET method names are used directly (via standard name mangling: `ContainsKey` → `contains_key`). Python-style aliases like `append`, `get`, `pop` will be provided by Sharpy.Core wrappers in v0.2.x+.

This means the test code needs to use .NET method names:
- `list.add(x)` (maps to `List<T>.Add(x)`)
- `dict.contains_key(k)` (maps to `Dictionary<K,V>.ContainsKey(k)`)
- `set.add(x)` (maps to `HashSet<T>.Add(x)`)

#### 4. `build_tools/sharpy_dogfood/orchestrator.py` - Remove v0.1.11 feature blocks

**Current code (lines 1231-1237 in `_quick_prevalidate`):**
```python
forbidden_checks = [
    # String features not yet supported
    (r'f"[^"]*\{', "f-string interpolation (v0.1.11)"),
    (r"f'[^']*\{", "f-string interpolation (v0.1.11)"),
    # Collections (v0.1.11)
    (r":\s*list\[", "list type annotation (v0.1.11)"),
    (r":\s*dict\[", "dict type annotation (v0.1.11)"),
    (r":\s*set\[", "set type annotation (v0.1.11)"),
```

**Change to (remove or comment out the collection type blocks):**
```python
forbidden_checks = [
    # String features not yet supported
    (r'f"[^"]*\{', "f-string interpolation (v0.1.11)"),
    (r"f'[^']*\{", "f-string interpolation (v0.1.11)"),
    # Collections (v0.1.11) - NOW SUPPORTED
    # (r":\s*list\[", "list type annotation (v0.1.11)"),
    # (r":\s*dict\[", "dict type annotation (v0.1.11)"),
    # (r":\s*set\[", "set type annotation (v0.1.11)"),
```

Also remove/update these related patterns:
- `(r"\[\s*\]", "empty list literal (v0.1.11)")` - Keep this if empty literals aren't supported
- `(r"\{\s*\}", "empty dict/set literal (v0.1.11)")` - Keep this if empty literals aren't supported

### Verification

After making these changes, run:
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test src/Sharpy.Compiler.Tests
```

Create a new test case to verify:
```python
# test_list_type_annotation.spy
def calculate_total_area(shapes: list[float]) -> float:
    total: float = 0.0
    for area in shapes:
        total += area
    return total

def main():
    areas: list[float] = [3.14, 6.28, 9.42]
    total = calculate_total_area(areas)
    print(total)
```

---

## Part 2: Multi-File Imports with Cross-Module Type References

### Current State

Test case `skip_module_imports_multifile_0003` has this structure:

```
main.spy
├── imports from: shapes, geometry, analyzer
shapes.spy
├── imports from: geometry (Shape, IMeasurable, Point)
├── defines: Rectangle(Shape, IMeasurable), Circle(Shape, IMeasurable)
geometry.spy
├── imports: none
├── defines: IMeasurable, Shape, Point
analyzer.spy
├── imports from: shapes (Rectangle, Circle), geometry (Point)
├── defines: ShapeAnalyzer
```

The issue is **not** circular imports (there are none here). The issue is:

1. When `shapes.spy` imports `Shape` from `geometry.spy`, the type symbol needs to be fully resolved with inheritance info
2. When `analyzer.spy` then imports `Rectangle` from `shapes.spy`, it needs to know that `Rectangle` extends `Shape`
3. The current `ImportResolver` extracts symbols but doesn't fully resolve cross-module inheritance

### Root Cause Analysis

In `ImportResolver.cs`, the `ExtractFullClassSymbol` method:
```csharp
private TypeSymbol ExtractFullClassSymbol(ClassDef classDef, string definingModulePath)
{
    // ...
    // Note: Base class resolution happens later in NameResolver.ResolveInheritance()
    // after all types are registered in the symbol table.
    // ...
}
```

The problem is that when types are re-exported through modules, the base class resolution may not have happened yet, leading to incomplete type information.

### Required Changes

#### 1. Ensure Dependency Graph Builder Is Used

In `Compiler.cs` or wherever multi-file compilation is orchestrated, verify that:
1. All files are lexed and parsed first
2. All type symbols are registered in the symbol table
3. Inheritance is resolved across all modules BEFORE type checking
4. Only then is code generation performed

#### 2. Fix Type Resolution Order in `NameResolver.cs`

Check `NameResolver.ResolveInheritance()` to ensure it handles types from imported modules:

```csharp
// In NameResolver.cs, ensure inheritance resolution works across modules
public void ResolveInheritance(ClassDef classDef, TypeSymbol classSymbol)
{
    foreach (var baseTypeName in classDef.BaseClasses)
    {
        // Must look up in symbol table, which includes imported types
        var baseTypeSymbol = _symbolTable.LookupType(baseTypeName.Name);
        if (baseTypeSymbol == null)
        {
            // This could happen if import wasn't processed yet
            AddError($"Base type '{baseTypeName.Name}' not found. Ensure module is imported.");
            continue;
        }
        
        // ... resolve inheritance ...
    }
}
```

#### 3. Multi-File CLI Support

Verify that the CLI properly handles multi-file compilation with module paths:

```bash
# The CLI should support:
sharpy compile main.spy -m ./  # -m specifies module search path
```

Check `Sharpy.Cli` to ensure it:
1. Discovers all `.spy` files in the module path
2. Builds a dependency graph
3. Compiles in dependency order

### Verification Steps

1. Create a simplified multi-file test without the problematic `list[Shape]`:

```python
# geometry.spy
@abstract
class Shape:
    name: str
    def __init__(self, shape_name: str):
        self.name = shape_name

# shapes.spy
from geometry import Shape

class Rectangle(Shape):
    width: float
    def __init__(self, w: float):
        super().__init__("Rectangle")
        self.width = w

# main.spy
from shapes import Rectangle

def main():
    rect = Rectangle(5.0)
    print(rect.name)
```

2. Run compilation and verify the inheritance chain is resolved correctly.

---

## Summary of Changes

### Files to Modify for `list[T]` Support

| # | File | Change Description | Priority |
|---|------|-------------------|----------|
| 1 | `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Map `list`/`dict`/`set` to .NET types | **HIGH** |
| 2 | `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Map collection names to .NET type strings | **HIGH** |
| 3 | `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Update `CreateDictType` method | **HIGH** |
| 4 | `build_tools/sharpy_dogfood/orchestrator.py` | Remove/comment v0.1.11 feature blocks | **MEDIUM** |

### Files to Investigate for Multi-File Fix

| # | File | Investigation Needed | Priority |
|---|------|---------------------|----------|
| 1 | `src/Sharpy.Cli/` | Verify module path handling with `-m` flag | **HIGH** |
| 2 | `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Verify cross-module inheritance resolution | **MEDIUM** |
| 3 | `src/Sharpy.Compiler/Compiler.cs` | Verify multi-file compilation order | **MEDIUM** |

### Test Cases to Add

1. `collections/list_type_parameter.spy` - Basic `list[T]` usage
2. `collections/dict_type_parameter.spy` - Basic `dict[K,V]` usage
3. `modules/cross_module_inheritance.spy` - Type extending imported type

---

## Appendix: Current Test Failures

### Test 0006: `list[Shape]` Type Annotation
```python
def calculate_total_area(shapes: list[Shape]) -> float:
    # Currently fails because:
    # 1. `list` maps to Sharpy.Core.List (wrong for v0.1.x)
    # 2. Shape needs to be resolved from the imported module
```

### Test 0003: Multi-File Imports
```
analyzer.spy imports Rectangle from shapes.spy
shapes.spy imports Shape from geometry.spy
Rectangle extends Shape

Issue: When analyzer uses Rectangle, the Shape base class
may not be fully resolved in the type symbol.
```
