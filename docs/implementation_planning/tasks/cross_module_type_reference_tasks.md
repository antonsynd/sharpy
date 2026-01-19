## Task List for Junior Engineer/Agent

### Prerequisites
- Familiarize yourself with the project structure:
  - `src/Sharpy.Compiler/Semantic/` - Symbol table, type resolution, imports
  - `src/Sharpy.Compiler/CodeGen/` - C# code generation
  - `src/Sharpy.Compiler.Tests/Integration/` - Integration tests

---

## Part A: Fix Cross-Module Type References (Issues 1 & 2)

### Task A1: Add `DefiningModule` Property to TypeSymbol

**File**: `src/Sharpy.Compiler/Semantic/Symbol.cs`

**What to do**:
Add a new property to track which module defined a type.

**Location**: Inside the `TypeSymbol` record class (around line 65-90)

**Add this property**:
```csharp
/// <summary>
/// The module path that defines this type (e.g., "animal" for a type imported from animal.spy).
/// Null for types defined in the current module.
/// </summary>
public string? DefiningModule { get; init; }
```

**Verification**:
- Build the project: `dotnet build src/Sharpy.Compiler/`
- Should compile without errors

---

### Task A2: Track DefiningModule When Importing Types

**File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

**What to do**:
When creating symbols for imported types, set the `DefiningModule` property to track where the type came from.

**Location 1**: `ExtractFullClassSymbol` method (around line 430-460)

**Modify**: Add `DefiningModule` to the TypeSymbol creation. The method currently creates a `TypeSymbol` without `DefiningModule`. The tricky part is that this method extracts symbols from the *source* module, not the importing module. The `DefiningModule` should be set when the symbol is *re-exported* or when it's used in another module.

**Better approach**: Modify `CreateReExportSymbol` method (around line 370-410) to ensure `DefiningModule` is properly set:

**Current code** (around line 387-398):
```csharp
TypeSymbol type => new TypeSymbol
{
    Name = newName ?? type.Name,
    Kind = type.Kind,
    TypeKind = type.TypeKind,
    AccessLevel = type.AccessLevel,
    DeclarationLine = fromImport.LineStart,
    DeclarationColumn = fromImport.ColumnStart,
    IsReExport = true,
    OriginalModule = fromImport.Module
},
```

**Change to**:
```csharp
TypeSymbol type => new TypeSymbol
{
    Name = newName ?? type.Name,
    Kind = type.Kind,
    TypeKind = type.TypeKind,
    AccessLevel = type.AccessLevel,
    TypeParameters = type.TypeParameters,
    Fields = type.Fields,
    Methods = type.Methods,
    Properties = type.Properties,
    Constructors = type.Constructors,
    BaseType = type.BaseType,
    Interfaces = type.Interfaces,
    DeclarationLine = fromImport.LineStart,
    DeclarationColumn = fromImport.ColumnStart,
    IsReExport = true,
    OriginalModule = fromImport.Module,
    DefiningModule = fromImport.ResolvedModulePath ?? fromImport.Module
},
```

**Verification**:
- Build succeeds
- Run existing passing tests to ensure no regressions

---

### Task A3: Update NameResolver to Preserve DefiningModule for Imported Types

**File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**What to do**:
When types are imported and registered in the symbol table, ensure the `DefiningModule` is preserved.

**Location**: The `ResolveFromImport` method needs to be updated (or created if import resolution happens elsewhere).

**Note**: Currently, the `NameResolver` has stub implementations for `ResolveImport` and `ResolveFromImport`. The actual symbol registration happens through the compilation pipeline. You'll need to trace how imported symbols flow into the symbol table.

**Key insight**: When the `TypeChecker` or another component resolves type references in base class declarations, it needs to check if the referenced type has a `DefiningModule` set.

---

### Task A4: Modify TypeMapper to Emit Fully Qualified Names for Cross-Module Types

**File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

**What to do**:
Update `GetMappedTypeName` to check if a type is from another module and emit the fully qualified name.

**Location**: `GetMappedTypeName` method (lines 218-236)

**Current code**:
```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    // Check if it's a built-in type
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
    {
        return csharpType;
    }

    // Check if it's a known builtin from the registry
    if (_context.IsBuiltinType(sharpyTypeName))
    {
        // Builtin types from registry should be in Sharpy namespace
        return $"Sharpy.{sharpyTypeName}";
    }

    // User-defined types keep their original name
    // Name mangling will be applied separately if needed
    return sharpyTypeName;
}
```

**Change to**:
```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    // Check if it's a built-in type
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
    {
        return csharpType;
    }

    // Check if it's a known builtin from the registry
    if (_context.IsBuiltinType(sharpyTypeName))
    {
        // Builtin types from registry should be in Sharpy namespace
        return $"Sharpy.{sharpyTypeName}";
    }

    // Check if it's a user-defined type from another module
    var typeSymbol = _context.SymbolTable.LookupType(sharpyTypeName);
    if (typeSymbol != null && !string.IsNullOrEmpty(typeSymbol.DefiningModule))
    {
        // Type from another module - use fully qualified name
        // Format: {ProjectNamespace}.{ModuleNamespace}.Exports.{TypeName}
        var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);

        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.Exports.{NameMangler.ToPascalCase(sharpyTypeName)}";
        }
        return $"{moduleNamespace}.Exports.{NameMangler.ToPascalCase(sharpyTypeName)}";
    }

    // User-defined types in current module keep their PascalCase name
    return NameMangler.ToPascalCase(sharpyTypeName);
}

/// <summary>
/// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
/// </summary>
private static string ConvertModuleToNamespace(string modulePath)
{
    var parts = modulePath.Split('.');
    return string.Join(".", parts.Select(p => SimpleToPascalCase(p)));
}

private static string SimpleToPascalCase(string name)
{
    if (string.IsNullOrEmpty(name)) return name;
    return char.ToUpperInvariant(name[0]) + name.Substring(1);
}
```

**Important**: You may need to add a reference to `NameMangler` at the top of the class if not already imported.

---

### Task A5: Update MapSemanticType for UserDefinedType

**File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

**What to do**:
Update the `MapSemanticType` method to use the `GetMappedTypeName` logic for `UserDefinedType`.

**Location**: Inside `MapSemanticType` method (around line 69)

**Current code**:
```csharp
// Handle user-defined types
UserDefinedType udt => ParseTypeName(GetMappedTypeName(udt.Name)),
```

**Consideration**: The `UserDefinedType` has a `Symbol` property that might contain the `TypeSymbol` with `DefiningModule`. If `GetMappedTypeName` already does the lookup, this should work. But you may want to add a direct path:

```csharp
// Handle user-defined types
UserDefinedType udt => ParseTypeName(GetMappedTypeNameFromSymbol(udt)),
```

And add a new helper method:
```csharp
private string GetMappedTypeNameFromSymbol(UserDefinedType udt)
{
    // If we have a symbol with DefiningModule, use it directly
    if (udt.Symbol != null && !string.IsNullOrEmpty(udt.Symbol.DefiningModule))
    {
        var moduleNamespace = ConvertModuleToNamespace(udt.Symbol.DefiningModule);
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.Exports.{NameMangler.ToPascalCase(udt.Name)}";
        }
        return $"{moduleNamespace}.Exports.{NameMangler.ToPascalCase(udt.Name)}";
    }

    // Fall back to name-based lookup
    return GetMappedTypeName(udt.Name);
}
```

---

### Task A6: Verification - Enable and Run the Tests

**Files to modify**:
1. Delete: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/mixed_inheritance_interface/main.skip`
2. Delete: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.skip`

**Run tests**:
```bash
cd src/Sharpy.Compiler.Tests
dotnet test --filter "Category=cross_module_inheritance"
# Or run all integration tests:
dotnet test --filter "FullyQualifiedName~Integration"
```

**Expected outcome**:
- Both `mixed_inheritance_interface` and `three_level_class_inheritance` tests should pass
- Generated C# code should show fully qualified type names like `Animal.Exports.Animal` instead of just `Animal`

---

## Part B: Fix Package Namespace Isolation (Issue 3)

**Note**: This is a larger architectural change. It may be deferred to a separate PR.

### Task B1: Understand the Current Symbol Table Architecture

**Files to study**:
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs`
- `src/Sharpy.Compiler/Semantic/Scope.cs`
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**Current behavior**:
- All symbols from all modules are registered in a shared global scope
- When importing `package_a.helper.func` and `package_b.helper.func`, both `func` symbols try to register with the same name

### Task B2: Design Decision - Package-Qualified Symbols

**Option A: Store symbols with full package path**
- Change symbol storage to use keys like `package_a.helper.func` instead of just `func`
- Modify symbol lookup to resolve based on import context

**Option B: Separate symbol tables per package**
- Create a hierarchy of symbol tables, one per package
- More complex but better isolation

### Task B3: Implement Package-Qualified Symbol Storage

**File**: `src/Sharpy.Compiler/Semantic/SymbolTable.cs` and `src/Sharpy.Compiler/Semantic/Scope.cs`

**What to do**:
Modify the `Define` and `Lookup` methods to support package-qualified names.

**This requires**:
1. Tracking the current package context during compilation
2. Prefixing symbol names with package path when defining
3. Resolving the correct symbol based on import context when looking up

### Task B4: Enable and Run the Test

**File**: `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`

**Location**: Line 866

**Current code**:
```csharp
[Fact(Skip = "Requires package namespace isolation - symbols from different packages currently share the same global scope")]
public void EdgeCase_ImportSameName_FromDifferentPackages_Works()
```

**Change to**:
```csharp
[Fact]
public void EdgeCase_ImportSameName_FromDifferentPackages_Works()
```

**Run test**:
```bash
dotnet test --filter "EdgeCase_ImportSameName_FromDifferentPackages_Works"
```

---

## Summary of Changes by File

| File | Changes |
|------|---------|
| `Symbol.cs` | Add `DefiningModule` property to `TypeSymbol` |
| `ImportResolver.cs` | Set `DefiningModule` in `CreateReExportSymbol` for TypeSymbol |
| `TypeMapper.cs` | Update `GetMappedTypeName` to emit fully qualified names for cross-module types |
| `SymbolTable.cs` | (Part B) Support package-qualified symbol storage |
| `Scope.cs` | (Part B) Support package-qualified keys |
| Test files | Delete `.skip` files and remove `Skip` attribute |

---

## Testing Checklist

- [ ] Build succeeds with no errors
- [ ] All existing tests pass (no regressions)
- [ ] `three_level_class_inheritance` test passes after enabling
- [ ] `mixed_inheritance_interface` test passes after enabling
- [ ] (Part B) `EdgeCase_ImportSameName_FromDifferentPackages_Works` test passes after enabling
- [ ] Generated C# code shows correct fully qualified names (manual inspection recommended)
