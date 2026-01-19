# Cross-Module Inheritance & Multi-File Compilation Investigation

## Executive Summary

This document investigates the current support for multi-file/module compilation and inheritance/interface implementation across module boundaries in the Sharpy compiler. It identifies gaps and proposes remediation tasks.

## Current Architecture Overview

### Multi-File Compilation Pipeline

The `ProjectCompiler.cs` implements a 7-phase compilation pipeline:

1. **Phase 1: Parse all source files** - Creates AST modules for each `.spy` file
2. **Phase 2: Initialize shared state** - Creates shared `SymbolTable` and `SemanticInfo`
3. **Phase 3: Collect type declarations** - Two sub-passes:
   - Pass 1: Register type names (shells only)
   - Pass 2: Resolve inheritance relationships
4. **Phase 4: Resolve imports** - Process `import` and `from X import` statements
5. **Phase 5: Semantic analysis** - Type checking across all modules
6. **Phase 6: Code generation** - Generate C# for each module
7. **Phase 7: Assembly compilation** - Compile all C# to single assembly

### Import Resolution

The `ImportResolver.cs` handles:
- `.spy` file imports (both relative and absolute)
- .NET assembly module imports via `ModuleRegistry`
- Re-export tracking for transitive imports
- Circular import detection with detailed chain reporting

### Inheritance Resolution

The `NameResolver.cs` handles inheritance in two passes:
1. **First pass**: Register all type names without resolving inheritance
2. **Second pass**: Resolve base classes and interfaces by looking up symbols

## Identified Gaps and Issues

### Gap 1: Cross-Module Inheritance Symbol Resolution Order

**Problem**: When a class in module A inherits from a class in module B, the inheritance resolution may fail if module B's types haven't been fully collected yet.

**Current Code** (`ProjectCompiler.cs` lines ~160-195):
```csharp
private void CollectTypeDeclarations(ProjectConfig config)
{
    _logger.LogInfo("Phase 2: Collecting type declarations across all files");

    foreach (var (sourceFile, module) in _parsedModules)
    {
        var nameResolver = new NameResolver(_symbolTable, _logger);
        nameResolver.ResolveDeclarations(module);
        // ...
    }

    // Now that all types are declared, resolve inheritance relationships
    _logger.LogInfo("Phase 2b: Resolving inheritance across all files");
    foreach (var (sourceFile, module) in _parsedModules)
    {
        var nameResolver = new NameResolver(_symbolTable, _logger);
        nameResolver.ResolveInheritance();
        // ...
    }
}
```

**Issue**: The `ResolveInheritance()` method creates a new `NameResolver` for each file, but the `_classDefs`, `_structDefs`, and `_interfaceDefs` lists are cleared each time. This means inheritance is only resolved for types within the same file.

**Fix Required**: The inheritance resolution pass should use a single `NameResolver` instance or share the type definition lists across all files.

### Gap 2: Imported Type Not Registered for Inheritance

**Problem**: When resolving imports, the `ImportResolver` adds symbols to the symbol table, but these symbols are not added to the `NameResolver`'s type definition lists (`_classDefs`, etc.), so their inheritance cannot be resolved.

**Example Scenario**:
```python
# base.spy
class Animal:
    name: str

# derived.spy
from base import Animal

class Dog(Animal):  # Animal should be recognized as base class
    breed: str
```

**Current Behavior**: The import is resolved and `Animal` is added to the symbol table, but when `Dog`'s inheritance is resolved, `Animal` might not be found correctly as a `TypeSymbol` with proper `TypeKind`.

### Gap 3: .NET Base Class Inheritance

**Problem**: Inheriting from .NET base classes (like `System.Exception`) requires special handling that may not be fully implemented.

**Current Code** (`ImportResolver.cs`):
```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, ...)
{
    // Creates ModuleInfo with IsNetModule = true
    // But only adds functions, not types
}
```

**Issue**: The .NET module resolution only exports functions, not types. This means you cannot inherit from .NET classes.

### Gap 4: Multi-Level Interface Inheritance Across Modules

**Problem**: When interface A extends interface B, and B is in a different module, the inherited methods from B may not be properly tracked in the implementing class.

**Current Code** (`NameResolver.cs`):
```csharp
private void ResolveInterfaceInheritance(InterfaceDef interfaceDef)
{
    foreach (var baseAnnot in interfaceDef.BaseInterfaces)
    {
        var baseInterfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
        // Adds to typeSymbol.Interfaces list
    }
}
```

**Issue**: The base interface's methods are not copied to the derived interface's `Methods` list, so implementing classes may not know they need to implement those methods.

### Gap 5: Type Resolution in Imported Symbols

**Problem**: When types are imported from another module, their field types and method signatures are resolved with `SemanticType.Unknown` and may not be fully resolved during semantic analysis.

**Current Code** (`ImportResolver.cs`):
```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
{
    case ClassDef classDef:
        var classSymbol = new TypeSymbol
        {
            // Fields and Methods are empty lists!
            // No field/method information is extracted
        };
}
```

**Issue**: Imported class symbols don't have field/method information, which may cause issues when:
1. Accessing inherited fields from a base class in another module
2. Overriding methods from a base class in another module

## Task List: Compiler Fixes

### Task 1: Fix Cross-Module Inheritance Resolution

**File**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

**Changes**:
```csharp
// Create a single NameResolver for all files
private void CollectTypeDeclarations(ProjectConfig config)
{
    var nameResolver = new NameResolver(_symbolTable, _logger);
    
    // First pass: collect all declarations
    foreach (var (sourceFile, module) in _parsedModules)
    {
        nameResolver.ResolveDeclarations(module);
    }
    
    // Second pass: resolve inheritance (single pass across all types)
    nameResolver.ResolveInheritance();
    
    // Collect errors
    if (nameResolver.Errors.Any())
    {
        _errors.AddRange(nameResolver.Errors.Select(e =>
            $"({e.Line},{e.Column}): error: {e.Message}"));
    }
}
```

### Task 2: Extract Full Type Information from Imported Modules

**File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

**Changes**: Enhance `ExtractExportedSymbol` to fully populate class/struct/interface symbols:

```csharp
case ClassDef classDef:
    var classSymbol = new TypeSymbol
    {
        Name = classDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        // ... existing code ...
    };
    
    // Extract fields
    foreach (var stmt in classDef.Body)
    {
        if (stmt is VariableDeclaration varDecl)
        {
            classSymbol.Fields.Add(new VariableSymbol
            {
                Name = varDecl.Name,
                Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                // ...
            });
        }
        else if (stmt is FunctionDef method)
        {
            classSymbol.Methods.Add(new FunctionSymbol
            {
                Name = method.Name,
                // Extract parameters and return type
            });
        }
    }
    
    // Note: Base class resolution happens in NameResolver.ResolveInheritance()
    // after all types are registered in the symbol table
```

### Task 3: Add .NET Type Import Support

**File**: `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs`

**Changes**: Add method to get types from .NET assemblies:

```csharp
public TypeSymbol? GetTypeFromAssembly(string typeName)
{
    foreach (var assembly in _loadedAssemblies)
    {
        var type = assembly.GetType(typeName);
        if (type != null)
        {
            return CreateTypeSymbolFromClrType(type);
        }
    }
    return null;
}
```

**File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

**Changes**: Update `TryResolveNetModule` to include types:

```csharp
// Get types from the module
var types = _moduleRegistry.GetModuleTypes(moduleName);
foreach (var type in types)
{
    moduleInfo.ExportedSymbols[type.Name] = type;
}
```

### Task 4: Propagate Inherited Interface Methods

**File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**Changes**: After resolving interface inheritance, propagate methods:

```csharp
private void ResolveInterfaceInheritance(InterfaceDef interfaceDef)
{
    // ... existing code ...
    
    // After all base interfaces are resolved, propagate methods
    var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
    if (typeSymbol != null)
    {
        PropagateInterfaceMethods(typeSymbol);
    }
}

private void PropagateInterfaceMethods(TypeSymbol interfaceSymbol)
{
    var seenMethods = new HashSet<string>(
        interfaceSymbol.Methods.Select(m => m.Name));
    
    foreach (var baseInterface in interfaceSymbol.Interfaces)
    {
        foreach (var method in baseInterface.Methods)
        {
            if (!seenMethods.Contains(method.Name))
            {
                interfaceSymbol.Methods.Add(method);
                seenMethods.Add(method.Name);
            }
        }
    }
}
```

### Task 5: Validate Interface Implementation Across Modules

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`

**Changes**: Add comprehensive interface implementation validation:

```csharp
private void ValidateInterfaceImplementation(TypeSymbol classSymbol)
{
    foreach (var iface in GetAllImplementedInterfaces(classSymbol))
    {
        foreach (var method in iface.Methods)
        {
            var impl = FindMethodInClass(classSymbol, method.Name);
            if (impl == null && !classSymbol.IsAbstract)
            {
                AddError($"Class '{classSymbol.Name}' does not implement " +
                    $"interface method '{iface.Name}.{method.Name}'");
            }
        }
    }
}

private IEnumerable<TypeSymbol> GetAllImplementedInterfaces(TypeSymbol type)
{
    var visited = new HashSet<string>();
    var queue = new Queue<TypeSymbol>(type.Interfaces);
    
    // Also check base class interfaces
    if (type.BaseType != null)
    {
        foreach (var iface in type.BaseType.Interfaces)
            queue.Enqueue(iface);
    }
    
    while (queue.Count > 0)
    {
        var iface = queue.Dequeue();
        if (visited.Add(iface.Name))
        {
            yield return iface;
            foreach (var baseIface in iface.Interfaces)
                queue.Enqueue(baseIface);
        }
    }
}
```

## Implementation Priority

| Priority | Task | Effort | Impact |
|----------|------|--------|--------|
| 1 | Task 1: Fix Cross-Module Inheritance | Medium | Critical - Core functionality |
| 2 | Task 2: Extract Full Type Information | High | Critical - Enables Task 1 |
| 3 | Task 5: Validate Interface Implementation | Medium | High - Proper error messages |
| 4 | Task 4: Propagate Interface Methods | Low | Medium - Diamond inheritance |
| 5 | Task 3: .NET Type Import | High | Medium - .NET interop |

## Testing Requirements

See the companion document `edge_cases_multifile_inheritance.md` for comprehensive test cases that should all pass after these fixes are implemented.
