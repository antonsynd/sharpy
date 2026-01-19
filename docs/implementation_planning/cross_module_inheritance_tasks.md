# Cross-Module Inheritance & Multi-File Compilation: Gap Analysis and Task List

## Executive Summary

This document provides a comprehensive investigation of the current support for multi-file/module compilation and inheritance/interface implementation across modules in the Sharpy compiler. It identifies **5 critical gaps** and provides detailed remediation tasks.

**Status**: Investigation Complete  
**Priority**: P0 (Critical for v0.2.0)  
**Estimated Total Effort**: 4-5 days (for experienced engineer with compiler knowledge)

---

## Table of Contents

1. [Current Architecture Analysis](#current-architecture-analysis)
2. [Identified Gaps](#identified-gaps)
3. [Detailed Task List](#detailed-task-list)
4. [Test Cases](#test-cases)
5. [Implementation Order](#implementation-order)

---

## Current Architecture Analysis

### Multi-File Compilation Pipeline

The `ProjectCompiler.cs` implements a 7-phase compilation pipeline:

```
Phase 1: Parse all source files → Creates AST modules
Phase 2: Initialize shared state → Creates shared SymbolTable and SemanticInfo
Phase 3: Collect type declarations:
    - Pass 3a: Register type names (shells only) per file
    - Pass 3b: Resolve inheritance relationships per file ← BUG HERE
Phase 4: Resolve imports → Process import statements
Phase 5: Semantic analysis → Type checking
Phase 6: Code generation → Generate C# via Roslyn
Phase 7: Assembly compilation → Emit assembly
```

### Current Inheritance Resolution Flow

```
NameResolver.ResolveDeclarations(module)
    → Iterates statements
    → ResolveClassDeclaration(classDef)
        → Creates TypeSymbol shell
        → Adds to _classDefs list for later inheritance resolution
    → Similar for structs/interfaces

NameResolver.ResolveInheritance()
    → Iterates _classDefs, _structDefs, _interfaceDefs
    → Resolves base classes/interfaces via SymbolTable.Lookup()
```

---

## Identified Gaps

### Gap 1: NameResolver Instance Isolation Bug (CRITICAL)

**Location**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs` lines 165-183

**Problem**: Phase 3b creates a **new** `NameResolver` instance for each file, but the `_classDefs`, `_structDefs`, and `_interfaceDefs` lists are instance fields. These lists only get populated during `ResolveDeclarations()` (Phase 3a), so when `ResolveInheritance()` is called on a fresh instance, these lists are empty.

**Current Code**:
```csharp
// Phase 3: Collect type declarations
foreach (var (sourceFile, module) in _parsedModules)
{
    var nameResolver = new NameResolver(_symbolTable, _logger);
    nameResolver.ResolveDeclarations(module);
    // _classDefs populated here
}

// Phase 3b: Resolve inheritance
foreach (var (sourceFile, module) in _parsedModules)
{
    var nameResolver = new NameResolver(_symbolTable, _logger);  // NEW INSTANCE!
    nameResolver.ResolveInheritance();  // _classDefs is EMPTY!
}
```

**Impact**: Cross-module inheritance silently fails. Types from imported modules won't have their BaseType/Interfaces resolved.

---

### Gap 2: Incomplete Type Extraction for Imported Modules (CRITICAL)

**Location**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs` method `ExtractExportedSymbol()`

**Problem**: When types are imported from another `.spy` file, only a bare `TypeSymbol` shell is created. The following properties are NOT populated:
- `Fields` - List stays empty
- `Methods` - List stays empty  
- `Constructors` - List stays empty
- `BaseType` - Not set (null)
- `Interfaces` - List stays empty

**Current Code**:
```csharp
case ClassDef classDef:
    var classSymbol = new TypeSymbol
    {
        Name = classDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        AccessLevel = classAccessLevel,
        DeclarationLine = classDef.LineStart,
        DeclarationColumn = classDef.ColumnStart
        // NO Fields, Methods, BaseType, Interfaces!
    };
    moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
```

**Impact**: 
- Cannot access inherited fields (`dog.name` from `Animal` fails)
- Cannot check for missing interface implementations
- Cannot resolve `super()` calls to correct parent methods
- Virtual method dispatch may be incorrect

---

### Gap 3: No .NET Type Import Support (MEDIUM)

**Location**: `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs` and `ImportResolver.cs`

**Problem**: `ModuleRegistry.GetModuleFunctions()` only returns functions. There is no `GetModuleTypes()` method. The `TryResolveNetModule()` method in `ImportResolver` only adds functions to `ExportedSymbols`.

**Current Code** (ImportResolver.cs):
```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, ...)
{
    // Get functions from the module
    var functions = _moduleRegistry.GetModuleFunctions(moduleName);
    // ... only functions are added, no types!
}
```

**Impact**: Cannot inherit from .NET base classes or implement .NET interfaces like:
- `System.Exception` 
- `System.IComparable<T>`
- `System.IDisposable`
- Unity classes like `MonoBehaviour`

---

### Gap 4: No Interface Method Propagation (MEDIUM)

**Location**: `src/Sharpy.Compiler/Semantic/NameResolver.cs` method `ResolveInterfaceInheritance()`

**Problem**: When interface A extends interface B, the methods from B are NOT copied to A's `Methods` list. The `Interfaces` list is populated correctly, but implementations don't know they need to implement inherited methods.

**Current Code**:
```csharp
private void ResolveInterfaceInheritance(InterfaceDef interfaceDef)
{
    foreach (var baseAnnot in interfaceDef.BaseInterfaces)
    {
        var baseInterfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
        // Only adds to Interfaces list, doesn't copy methods!
        typeSymbol.Interfaces.Add(baseInterfaceSymbol);
    }
}
```

**Impact**: Classes implementing `IExtended(IBase)` don't see `IBase.method()` in their required methods list.

---

### Gap 5: No Cross-Module Interface Implementation Validation (MEDIUM)

**Location**: `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`

**Problem**: TypeChecker validates interface implementations but doesn't recursively collect methods from:
1. Base interfaces
2. Interfaces on base classes
3. Imported interfaces with incomplete method information

**Impact**: Missing interface method implementations are not detected at compile time, leading to runtime errors.

---

## Detailed Task List

### Task 1: Fix NameResolver Instance Isolation Bug

**Priority**: P0 (CRITICAL - Blocks all cross-module inheritance)  
**Effort**: 2-3 hours  
**Files to Modify**:
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

**Detailed Steps**:

1. **Modify `CollectTypeDeclarations` method** (lines ~147-183):

```csharp
/// <summary>
/// Phase 3: Collect type declarations from all files
/// FIXED: Use single NameResolver instance to preserve type definition lists
/// across files for correct inheritance resolution.
/// </summary>
private void CollectTypeDeclarations(ProjectConfig config)
{
    _logger.LogInfo("Phase 3: Collecting type declarations across all files");

    // CHANGE: Create ONE NameResolver for ALL files
    var nameResolver = new NameResolver(_symbolTable, _logger);

    // Phase 3a: Collect all type declarations (shells only)
    foreach (var (sourceFile, module) in _parsedModules)
    {
        nameResolver.ResolveDeclarations(module);
    }

    // Phase 3b: Resolve inheritance (using the SAME NameResolver instance)
    _logger.LogInfo("Phase 3b: Resolving inheritance across all files");
    nameResolver.ResolveInheritance();

    // Collect any errors
    if (nameResolver.Errors.Any())
    {
        _errors.AddRange(nameResolver.Errors.Select(e =>
            $"({e.Line},{e.Column}): error: {e.Message}"));
    }
}
```

**Verification**:
- Run test: `cross_module_inheritance/three_level_class_inheritance`
- Dog should correctly inherit from Mammal which inherits from Animal
- `dog.name` should resolve to Animal's field

---

### Task 2: Extract Full Type Information from Imported Modules

**Priority**: P0 (CRITICAL - Required for cross-module member access)  
**Effort**: 4-6 hours  
**Files to Modify**:
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

**Detailed Steps**:

1. **Create helper method to extract full class information**:

```csharp
/// <summary>
/// Extract full type information from a class definition including
/// fields, methods, constructors, and base class information.
/// </summary>
private TypeSymbol ExtractFullClassSymbol(ClassDef classDef)
{
    var accessLevel = GetAccessLevel(classDef.Name);
    bool isAbstract = classDef.Decorators.Any(d => d.Name == "abstract");

    var classSymbol = new TypeSymbol
    {
        Name = classDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        AccessLevel = accessLevel,
        IsAbstract = isAbstract,
        TypeParameters = classDef.TypeParameters,
        DeclarationLine = classDef.LineStart,
        DeclarationColumn = classDef.ColumnStart
    };

    // Extract fields
    foreach (var stmt in classDef.Body)
    {
        if (stmt is VariableDeclaration varDecl)
        {
            var fieldSymbol = new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                Type = ConvertTypeAnnotationToSemanticType(varDecl.Type),
                IsConstant = varDecl.IsConst,
                AccessLevel = GetAccessLevel(varDecl.Name),
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart
            };
            classSymbol.Fields.Add(fieldSymbol);
        }
    }

    // Extract methods
    foreach (var stmt in classDef.Body)
    {
        if (stmt is FunctionDef method)
        {
            var methodSymbol = ExtractMethodSymbol(method);
            classSymbol.Methods.Add(methodSymbol);
            
            // Track constructors separately
            if (method.Name == "__init__")
            {
                classSymbol.Constructors.Add(methodSymbol);
            }
        }
    }

    // Store base class names for later resolution
    // (Actual TypeSymbol resolution happens in NameResolver.ResolveInheritance)
    // We need to store these for the imported symbol to be resolvable
    classSymbol.UnresolvedBaseClasses = classDef.BaseClasses; // Add this property

    return classSymbol;
}

/// <summary>
/// Extract method symbol with parameter and return type information.
/// </summary>
private FunctionSymbol ExtractMethodSymbol(FunctionDef method)
{
    var accessLevel = GetAccessLevel(method.Name);
    
    bool hasSelfParameter = method.Parameters.Any(p => 
        string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));
    bool hasStaticDecorator = method.Decorators.Any(d => 
        d.Name == "static" || d.Name == "staticmethod");
    bool isStatic = hasStaticDecorator || !hasSelfParameter;

    var parameters = method.Parameters.Select(p => new ParameterSymbol
    {
        Name = p.Name,
        Type = ConvertTypeAnnotationToSemanticType(p.Type),
        HasDefault = p.DefaultValue != null,
        DefaultValue = p.DefaultValue,
        IsVariadic = p.IsVariadic
    }).ToList();

    return new FunctionSymbol
    {
        Name = method.Name,
        Kind = SymbolKind.Function,
        Parameters = parameters,
        ReturnType = ConvertTypeAnnotationToSemanticType(method.ReturnType),
        IsStatic = isStatic,
        IsAbstract = method.Decorators.Any(d => d.Name == "abstract"),
        IsVirtual = method.Decorators.Any(d => d.Name == "virtual"),
        IsOverride = method.Decorators.Any(d => d.Name == "override"),
        TypeParameters = method.TypeParameters,
        AccessLevel = accessLevel,
        DeclarationLine = method.LineStart,
        DeclarationColumn = method.ColumnStart
    };
}
```

2. **Modify `ExtractExportedSymbol` to use new helpers**:

```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
{
    switch (statement)
    {
        case ClassDef classDef:
            var classSymbol = ExtractFullClassSymbol(classDef);
            moduleInfo.ExportedSymbols[classDef.Name] = classSymbol;
            break;

        case StructDef structDef:
            var structSymbol = ExtractFullStructSymbol(structDef);
            moduleInfo.ExportedSymbols[structDef.Name] = structSymbol;
            break;

        case InterfaceDef interfaceDef:
            var interfaceSymbol = ExtractFullInterfaceSymbol(interfaceDef);
            moduleInfo.ExportedSymbols[interfaceDef.Name] = interfaceSymbol;
            break;

        // ... rest unchanged
    }
}
```

3. **Add similar methods for struct and interface extraction** (similar pattern to class)

4. **Add `UnresolvedBaseClasses` property to TypeSymbol**:

In `Symbol.cs`:
```csharp
public record TypeSymbol : Symbol
{
    // ... existing properties ...
    
    /// <summary>
    /// Unresolved base class type annotations (used during import resolution).
    /// These are resolved to actual TypeSymbols during NameResolver.ResolveInheritance().
    /// </summary>
    public List<TypeAnnotation> UnresolvedBaseClasses { get; set; } = new();
}
```

**Verification**:
- Import a class from another module
- Access its fields and methods
- Verify inheritance chain is resolved correctly

---

### Task 3: Add .NET Type Import Support

**Priority**: P1 (Important for .NET interop)  
**Effort**: 6-8 hours  
**Files to Modify**:
- `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs`
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs` (may need extension)

**Detailed Steps**:

1. **Add type discovery to ModuleRegistry**:

```csharp
// In ModuleRegistry.cs

/// <summary>
/// Get all public types exported by a specific module/namespace.
/// </summary>
public List<TypeSymbol> GetModuleTypes(string moduleName)
{
    var types = new List<TypeSymbol>();
    
    foreach (var assembly in _loadedAssemblies.Values)
    {
        // Get types in the specified namespace
        var matchingTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && 
                       (t.Namespace == moduleName || 
                        t.Namespace?.StartsWith(moduleName + ".") == true));
        
        foreach (var clrType in matchingTypes)
        {
            var typeSymbol = CreateTypeSymbolFromClrType(clrType);
            if (typeSymbol != null)
            {
                types.Add(typeSymbol);
            }
        }
    }
    
    return types;
}

/// <summary>
/// Get a specific type by full name (e.g., "System.Exception").
/// </summary>
public TypeSymbol? GetType(string fullTypeName)
{
    foreach (var assembly in _loadedAssemblies.Values)
    {
        var clrType = assembly.GetType(fullTypeName);
        if (clrType != null)
        {
            return CreateTypeSymbolFromClrType(clrType);
        }
    }
    
    // Also check mscorlib/System.Runtime
    var systemType = Type.GetType(fullTypeName);
    if (systemType != null)
    {
        return CreateTypeSymbolFromClrType(systemType);
    }
    
    return null;
}

/// <summary>
/// Convert a CLR Type to a Sharpy TypeSymbol.
/// </summary>
private TypeSymbol CreateTypeSymbolFromClrType(Type clrType)
{
    var typeKind = clrType.IsInterface ? TypeKind.Interface :
                   clrType.IsEnum ? TypeKind.Enum :
                   clrType.IsValueType ? TypeKind.Struct :
                   TypeKind.Class;
    
    var symbol = new TypeSymbol
    {
        Name = clrType.Name,
        Kind = SymbolKind.Type,
        TypeKind = typeKind,
        ClrType = clrType,
        IsAbstract = clrType.IsAbstract && !clrType.IsInterface,
        AccessLevel = clrType.IsPublic ? AccessLevel.Public : AccessLevel.Protected
    };
    
    // Add methods
    foreach (var method in clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
    {
        if (method.IsSpecialName) continue; // Skip property accessors, etc.
        
        var methodSymbol = CreateFunctionSymbolFromMethodInfo(method);
        symbol.Methods.Add(methodSymbol);
    }
    
    // Add properties as fields (simplified)
    foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        var fieldSymbol = new VariableSymbol
        {
            Name = prop.Name,
            Kind = SymbolKind.Variable,
            Type = MapClrTypeToSemanticType(prop.PropertyType)
        };
        symbol.Fields.Add(fieldSymbol);
    }
    
    // Resolve base type
    if (clrType.BaseType != null && clrType.BaseType != typeof(object))
    {
        symbol.BaseType = CreateTypeSymbolFromClrType(clrType.BaseType);
    }
    
    // Resolve interfaces
    foreach (var iface in clrType.GetInterfaces())
    {
        var ifaceSymbol = CreateTypeSymbolFromClrType(iface);
        symbol.Interfaces.Add(ifaceSymbol);
    }
    
    return symbol;
}
```

2. **Modify `TryResolveNetModule` in ImportResolver**:

```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
{
    if (_moduleRegistry == null)
        return null;

    // Check cache first
    var cacheKey = $".net:{moduleName}";
    if (_moduleCache.TryGetValue(cacheKey, out var cached))
        return cached;

    _logger.LogDebug($"Resolving .NET module: {moduleName}");

    // Create ModuleInfo for the .NET module
    var moduleInfo = new ModuleInfo
    {
        Path = $".net:{moduleName}",
        Module = null!,
        ExportedSymbols = new Dictionary<string, Symbol>(),
        IsNetModule = true
    };

    // Get functions from the module
    var functions = _moduleRegistry.GetModuleFunctions(moduleName);
    foreach (var function in functions)
    {
        moduleInfo.ExportedSymbols[function.Name] = function;
    }

    // NEW: Get types from the module
    var types = _moduleRegistry.GetModuleTypes(moduleName);
    foreach (var type in types)
    {
        moduleInfo.ExportedSymbols[type.Name] = type;
    }

    // Also check for well-known system types
    var wellKnownTypes = new[] { "Exception", "IComparable", "IDisposable", "IEnumerable" };
    foreach (var typeName in wellKnownTypes)
    {
        var fullName = $"{moduleName}.{typeName}";
        var type = _moduleRegistry.GetType(fullName);
        if (type != null && !moduleInfo.ExportedSymbols.ContainsKey(typeName))
        {
            moduleInfo.ExportedSymbols[typeName] = type;
        }
    }

    if (moduleInfo.ExportedSymbols.Count == 0)
    {
        _logger.LogWarning($".NET module '{moduleName}' has no exported symbols", 
            lineStart ?? 0, columnStart ?? 0);
        return null;
    }

    _moduleCache[cacheKey] = moduleInfo;
    _loadedModules.Add(cacheKey);

    _logger.LogInfo($"Loaded .NET module '{moduleName}' with {functions.Count} functions and {types.Count} types");

    return moduleInfo;
}
```

**Verification**:
- Import `System.Exception` and inherit from it
- Implement `System.IComparable<T>` interface
- Access inherited .NET members

---

### Task 4: Propagate Inherited Interface Methods

**Priority**: P1 (Important for interface inheritance)  
**Effort**: 2-3 hours  
**Files to Modify**:
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**Detailed Steps**:

1. **Add method propagation after interface inheritance resolution**:

```csharp
private void ResolveInterfaceInheritance(InterfaceDef interfaceDef)
{
    if (interfaceDef.BaseInterfaces.Count == 0)
        return;

    var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
    if (typeSymbol == null)
        return;

    // Resolve base interfaces
    foreach (var baseAnnot in interfaceDef.BaseInterfaces)
    {
        var baseInterfaceSymbol = _symbolTable.Lookup(baseAnnot.Name) as TypeSymbol;
        if (baseInterfaceSymbol == null)
        {
            AddError($"Interface '{baseAnnot.Name}' not found",
                interfaceDef.LineStart, interfaceDef.ColumnStart);
            continue;
        }

        if (baseInterfaceSymbol.TypeKind != TypeKind.Interface)
        {
            AddError($"'{baseAnnot.Name}' is not an interface",
                interfaceDef.LineStart, interfaceDef.ColumnStart);
            continue;
        }

        typeSymbol.Interfaces.Add(baseInterfaceSymbol);
    }

    // NEW: Propagate inherited methods from base interfaces
    PropagateInterfaceMethods(typeSymbol);
}

/// <summary>
/// Propagate methods from base interfaces to the derived interface.
/// Uses BFS to handle multi-level interface inheritance.
/// </summary>
private void PropagateInterfaceMethods(TypeSymbol interfaceSymbol)
{
    var seenMethods = new HashSet<string>(
        interfaceSymbol.Methods.Select(m => GetMethodSignature(m)));
    
    var visited = new HashSet<string> { interfaceSymbol.Name };
    var queue = new Queue<TypeSymbol>(interfaceSymbol.Interfaces);
    
    while (queue.Count > 0)
    {
        var baseInterface = queue.Dequeue();
        if (!visited.Add(baseInterface.Name))
            continue;
        
        // Copy methods from base interface
        foreach (var method in baseInterface.Methods)
        {
            var signature = GetMethodSignature(method);
            if (seenMethods.Add(signature))
            {
                // Clone the method with a note that it's inherited
                var inheritedMethod = method with 
                { 
                    DeclarationLine = null,  // Mark as inherited
                    DeclarationColumn = null 
                };
                interfaceSymbol.Methods.Add(inheritedMethod);
            }
        }
        
        // Add base interface's bases to the queue
        foreach (var grandBase in baseInterface.Interfaces)
        {
            queue.Enqueue(grandBase);
        }
    }
}

/// <summary>
/// Get a unique signature string for method deduplication.
/// </summary>
private string GetMethodSignature(FunctionSymbol method)
{
    var paramTypes = string.Join(",", method.Parameters.Select(p => p.Type.GetDisplayName()));
    return $"{method.Name}({paramTypes})";
}
```

**Verification**:
- Create interface chain: `IBase -> IExtended -> IFull`
- Implement `IFull` and verify all methods from entire chain are required

---

### Task 5: Add Comprehensive Interface Implementation Validation

**Priority**: P1 (Important for proper error messages)  
**Effort**: 4-5 hours  
**Files to Modify**:
- `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`
- New file: `src/Sharpy.Compiler/Semantic/InterfaceValidator.cs`

**Detailed Steps**:

1. **Create `InterfaceValidator.cs`**:

```csharp
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates that classes correctly implement all required interface methods.
/// </summary>
public class InterfaceValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public InterfaceValidator(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Validate that a class implements all required interface methods.
    /// </summary>
    public void ValidateImplementation(TypeSymbol classSymbol)
    {
        if (classSymbol.IsAbstract)
        {
            // Abstract classes don't need to implement all interface methods
            return;
        }

        var allInterfaces = CollectAllInterfaces(classSymbol);
        var implementedMethods = CollectImplementedMethods(classSymbol);

        foreach (var iface in allInterfaces)
        {
            foreach (var method in iface.Methods)
            {
                // Skip 'self' parameter when checking
                var signature = GetMethodSignature(method);
                
                if (!implementedMethods.Contains(signature))
                {
                    AddError(
                        $"Class '{classSymbol.Name}' does not implement interface method " +
                        $"'{iface.Name}.{method.Name}'",
                        classSymbol.DeclarationLine,
                        classSymbol.DeclarationColumn);
                }
            }
        }
    }

    /// <summary>
    /// Collect all interfaces a type implements, including:
    /// - Directly implemented interfaces
    /// - Base interfaces (interface inheritance)
    /// - Interfaces implemented by base classes
    /// </summary>
    private HashSet<TypeSymbol> CollectAllInterfaces(TypeSymbol type)
    {
        var result = new HashSet<TypeSymbol>();
        var visited = new HashSet<string>();
        var queue = new Queue<TypeSymbol>();

        // Add directly implemented interfaces
        foreach (var iface in type.Interfaces)
        {
            queue.Enqueue(iface);
        }

        // Add interfaces from base class hierarchy
        var baseType = type.BaseType;
        while (baseType != null)
        {
            foreach (var iface in baseType.Interfaces)
            {
                queue.Enqueue(iface);
            }
            baseType = baseType.BaseType;
        }

        // BFS through interface inheritance
        while (queue.Count > 0)
        {
            var iface = queue.Dequeue();
            if (!visited.Add(iface.Name))
                continue;

            result.Add(iface);

            // Add base interfaces
            foreach (var baseIface in iface.Interfaces)
            {
                queue.Enqueue(baseIface);
            }
        }

        return result;
    }

    /// <summary>
    /// Collect all methods implemented by a class and its base classes.
    /// </summary>
    private HashSet<string> CollectImplementedMethods(TypeSymbol type)
    {
        var result = new HashSet<string>();

        var currentType = type;
        while (currentType != null)
        {
            foreach (var method in currentType.Methods)
            {
                var signature = GetMethodSignature(method);
                result.Add(signature);
            }
            currentType = currentType.BaseType;
        }

        return result;
    }

    private string GetMethodSignature(FunctionSymbol method)
    {
        // Skip 'self' parameter in signature
        var params = method.Parameters
            .Where(p => p.Name != "self")
            .Select(p => p.Type.GetDisplayName());
        return $"{method.Name}({string.Join(",", params)})";
    }

    private void AddError(string message, int? line, int? column)
    {
        _errors.Add(new SemanticError(message, line, column));
        _logger.LogError(message, line ?? 0, column ?? 0);
    }
}
```

2. **Integrate into TypeChecker**:

In `TypeChecker.Definitions.cs`, add to `CheckClass`:

```csharp
private void CheckClass(ClassDef classDef)
{
    // ... existing code ...

    // Validate interface implementations
    var interfaceValidator = new InterfaceValidator(_symbolTable, _logger);
    interfaceValidator.ValidateImplementation(classSymbol);
    
    foreach (var error in interfaceValidator.Errors)
    {
        _errors.Add(error);
    }

    // ... rest of existing code ...
}
```

**Verification**:
- Create class that claims to implement an interface but is missing methods
- Verify compilation fails with clear error message
- Test multi-level interface inheritance validation

---

## Test Cases

### Test Case 1: Three-Level Class Inheritance Across Files

**Status**: Existing test at `cross_module_inheritance/three_level_class_inheritance`

This test should pass after Task 1 is complete.

### Test Case 2: Interface Inheritance Chain Across Files

**Status**: Existing test at `cross_module_inheritance/interface_inheritance_chain`

This test should pass after Tasks 1, 2, and 4 are complete.

### Test Case 3: Diamond Interface Implementation

**Status**: Existing test at `cross_module_inheritance/diamond_interface`

This test verifies that diamond inheritance (implementing multiple interfaces with common base) works correctly.

### Test Case 4: .NET Exception Inheritance (NEW)

Create new test: `cross_module_inheritance/net_exception_inheritance`

**custom_error.spy**:
```python
from system import Exception

class ValidationError(Exception):
    field_name: str
    
    def __init__(self, message: str, field: str):
        super().__init__(message)
        self.field_name = field

def main():
    try:
        raise ValidationError("Invalid input", "email")
    except ValidationError as e:
        print(e.field_name)
```

**Expected Output**:
```
email
```

### Test Case 5: .NET IComparable Implementation (NEW)

Create new test: `cross_module_inheritance/net_icomparable`

**sortable.spy**:
```python
from system import IComparable

class SortableItem(IComparable):
    value: int
    
    def __init__(self, value: int):
        self.value = value
    
    def compare_to(self, other: object) -> int:
        if other is None:
            return 1
        other_item: SortableItem = other as SortableItem
        if other_item is None:
            return 1
        if self.value < other_item.value:
            return -1
        if self.value > other_item.value:
            return 1
        return 0

def main():
    a: SortableItem = SortableItem(10)
    b: SortableItem = SortableItem(20)
    print(a.compare_to(b))
    print(b.compare_to(a))
```

**Expected Output**:
```
-1
1
```

### Test Case 6: Missing Interface Implementation Error

Create new test: `cross_module_inheritance/missing_interface_method`

**incomplete.spy**:
```python
interface IComplete:
    def method_a(self) -> int: ...
    def method_b(self) -> str: ...

class Incomplete(IComplete):
    def method_a(self) -> int:
        return 42
    # Missing method_b!

def main():
    pass
```

**Expected**: Compilation error mentioning missing `method_b`.

---

## Implementation Order

| Order | Task | Dependency | Effort | Blocks |
|-------|------|------------|--------|--------|
| 1 | Task 1: Fix NameResolver Instance Isolation | None | 2-3h | Tasks 2,3,4,5 |
| 2 | Task 2: Extract Full Type Info from Imports | Task 1 | 4-6h | Tasks 3,5 |
| 3 | Task 4: Propagate Interface Methods | Task 1 | 2-3h | Task 5 |
| 4 | Task 5: Interface Implementation Validation | Tasks 2,4 | 4-5h | None |
| 5 | Task 3: .NET Type Import Support | Task 2 | 6-8h | None |

**Recommended Implementation Schedule**:

- **Day 1**: Task 1 (critical fix, enables all other work)
- **Day 2**: Task 2 (enables cross-module member access)
- **Day 3**: Tasks 4 + 5 (interface support)
- **Day 4-5**: Task 3 (.NET interop)

---

## Appendix: Files Modified Summary

| File | Tasks | Changes |
|------|-------|---------|
| `ProjectCompiler.cs` | 1 | Fix CollectTypeDeclarations |
| `ImportResolver.cs` | 2, 3 | Add full type extraction, .NET type support |
| `NameResolver.cs` | 4 | Add method propagation |
| `Symbol.cs` | 2 | Add UnresolvedBaseClasses property |
| `ModuleRegistry.cs` | 3 | Add type discovery methods |
| `TypeChecker.Definitions.cs` | 5 | Integrate interface validation |
| `InterfaceValidator.cs` | 5 | New file |
| New test files | All | 3 new test fixtures |

---

## References

- Prior investigation: `docs/implementation_planning/cross_module_inheritance_investigation.md`
- Edge cases document: `docs/implementation_planning/edge_cases_multifile_inheritance.md`
- Language specification: `docs/specification/`
