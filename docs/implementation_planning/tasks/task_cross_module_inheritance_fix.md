# Task List: Fix Cross-Module Inheritance

**Goal:** Fix inheritance and interface implementation resolution across module boundaries to unblock Phase 0.1.7 (Inheritance & Interfaces).

**Priority:** HIGH - This is a blocker for language implementation progress.

**Prerequisites:** None

**Estimated Total Effort:** 3-5 days

**Related Documents:**
- `cross_module_inheritance_investigation.md` - Problem analysis
- `cross_module_inheritance_tasks.md` - Original task outline
- `phases.md` - Phase 0.1.7 requirements

---

## Problem Summary

The current compiler has three gaps in cross-module inheritance:

1. **NameResolver instance isolation**: ~~Each file gets a new `NameResolver`, so `_classDefs`, `_structDefs`, `_interfaceDefs` are cleared between files~~ **ALREADY FIXED** - `ProjectCompiler` uses a single `NameResolver` instance with `SetCurrentFilePath()`
2. **Imported types not in inheritance resolution**: ~~`ImportResolver` adds symbols to SymbolTable but not to NameResolver's type lists~~ **ALREADY FIXED** - cross-module Sharpy type inheritance works
3. **.NET base class inheritance**: Inheriting from .NET types (e.g., `System.Exception`) **DOES NOT WORK** - ImportResolver doesn't register .NET types for inheritance resolution

**Status Update (2026-01-21):** Issues 1 and 2 have already been fixed in the current implementation. Only issue 3 (.NET base class inheritance) remains to be fixed.

### Root Cause Analysis (Issue 3)

The compilation phases are ordered as:
1. **Phase 2**: CollectTypeDeclarations - parses `.spy` files and collects type definitions
2. **Phase 2b**: ResolveInheritance - resolves base classes/interfaces using types in symbol table
3. **Phase 3**: ResolveImports - processes import statements including .NET modules

The problem is that **inheritance resolution (Phase 2b) happens BEFORE import resolution (Phase 3)**.
When `from system import Exception` is processed in Phase 3, the inheritance resolution has already
completed in Phase 2b and reported "Base type 'Exception' not found".

**Fix approach**: Either:
1. Run Phase 3 before Phase 2b (but this risks circular import issues)
2. Add a "pre-import" phase that registers .NET types before inheritance resolution
3. Re-run inheritance resolution after imports are resolved (simpler but less elegant)

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Shared NameResolver for inheritance pass**: Use a single NameResolver instance across all files for the inheritance resolution pass
2. **Type registration callback**: ImportResolver notifies when types are imported so they can be added to inheritance resolution

### One-Way Door Decisions (Commit Now)
1. **Two-phase inheritance resolution**: Phase 1 collects all types, Phase 2 resolves inheritance (already the intent, just not working)
2. **TypeSymbol as source of truth for inheritance**: Base classes and interfaces stored on TypeSymbol, not reconstructed from AST

---

## Phase 0: Preparation (30 minutes)

### Task 0.1: Create Failing Integration Tests
**File:** `src/Sharpy.Compiler.Tests/Integration/CrossModuleInheritanceTests.cs` (NEW)
**Description:** Create tests that demonstrate the current failures. These will guide the fix.

```csharp
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

public class CrossModuleInheritanceTests
{
    [Fact]
    public void ClassInheritance_FromImportedModule_ResolvesCorrectly()
    {
        // base.spy defines Animal class
        // derived.spy does: from base import Animal; class Dog(Animal): ...
        var baseModule = @"
class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def speak(self) -> str:
        return ""...""
";
        
        var derivedModule = @"
from base import Animal

class Dog(Animal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
    
    @override
    def speak(self) -> str:
        return ""Woof!""
";
        
        var result = CompileMultiFile(new Dictionary<string, string>
        {
            ["base.spy"] = baseModule,
            ["derived.spy"] = derivedModule
        });
        
        Assert.Empty(result.Errors);
        Assert.Contains("class Dog : Animal", result.GeneratedCSharp["derived.spy"]);
    }
    
    [Fact]
    public void InterfaceImplementation_FromImportedModule_ResolvesCorrectly()
    {
        var interfaceModule = @"
interface IDrawable:
    def draw(self) -> None:
        ...
";
        
        var implementerModule = @"
from shapes import IDrawable

class Circle(IDrawable):
    radius: float
    
    def draw(self) -> None:
        pass
";
        
        var result = CompileMultiFile(new Dictionary<string, string>
        {
            ["shapes.spy"] = interfaceModule,
            ["circle.spy"] = implementerModule
        });
        
        Assert.Empty(result.Errors);
        Assert.Contains(": IDrawable", result.GeneratedCSharp["circle.spy"]);
    }
    
    [Fact]
    public void AbstractClass_FromImportedModule_ResolvesCorrectly()
    {
        var abstractModule = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...
";
        
        var concreteModule = @"
from geometry import Shape

class Rectangle(Shape):
    width: float
    height: float
    
    @override
    def area(self) -> float:
        return self.width * self.height
";
        
        var result = CompileMultiFile(new Dictionary<string, string>
        {
            ["geometry.spy"] = abstractModule,
            ["rectangle.spy"] = concreteModule
        });
        
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void MultipleInheritance_InterfacesFromDifferentModules_Works()
    {
        var drawable = @"
interface IDrawable:
    def draw(self) -> None:
        ...
";
        var serializable = @"
interface ISerializable:
    def serialize(self) -> str:
        ...
";
        var combined = @"
from drawing import IDrawable
from persistence import ISerializable

class Widget(IDrawable, ISerializable):
    def draw(self) -> None:
        pass
    
    def serialize(self) -> str:
        return ""{}""
";
        
        var result = CompileMultiFile(new Dictionary<string, string>
        {
            ["drawing.spy"] = drawable,
            ["persistence.spy"] = serializable,
            ["widget.spy"] = combined
        });
        
        Assert.Empty(result.Errors);
        Assert.Contains(": IDrawable, ISerializable", result.GeneratedCSharp["widget.spy"]);
    }
    
    [Fact]
    public void ChainedInheritance_AcrossThreeModules_Works()
    {
        var grandparent = @"
class Grandparent:
    @virtual
    def greet(self) -> str:
        return ""Hello from Grandparent""
";
        var parent = @"
from family import Grandparent

class Parent(Grandparent):
    @override
    def greet(self) -> str:
        return ""Hello from Parent""
";
        var child = @"
from middle import Parent

class Child(Parent):
    @override
    def greet(self) -> str:
        return ""Hello from Child""
";
        
        var result = CompileMultiFile(new Dictionary<string, string>
        {
            ["family.spy"] = grandparent,
            ["middle.spy"] = parent,
            ["child.spy"] = child
        });
        
        Assert.Empty(result.Errors);
    }
    
    // Helper method - implement based on existing test infrastructure
    private static MultiFileCompilationResult CompileMultiFile(Dictionary<string, string> files)
    {
        // TODO: Use ProjectCompiler or similar infrastructure
        throw new NotImplementedException("Implement using ProjectCompiler");
    }
}

public class MultiFileCompilationResult
{
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, string> GeneratedCSharp { get; set; } = new();
}
```

**Verification:**
- [x] Tests compile
- [x] Tests pass (cross-module inheritance already works in current implementation)

**Notes:** The tests were created to verify cross-module inheritance behavior. Upon implementation, it was discovered that the feature already works correctly. The tests now serve as regression tests to ensure the feature continues working.

**Commit:** `test(integration): Add cross-module inheritance tests`

---

## Phase 1: Fix NameResolver Instance Isolation (2-4 hours)

### Task 1.1: Add Shared Type Collection to NameResolver
**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Description:** Add ability to share type definition lists across NameResolver instances or use a single instance.

**Current Problem:**
```csharp
public class NameResolver
{
    private readonly List<ClassDef> _classDefs = new();      // Cleared per file!
    private readonly List<StructDef> _structDefs = new();    // Cleared per file!
    private readonly List<InterfaceDef> _interfaceDefs = new(); // Cleared per file!
}
```

**Solution:** Create a shared context class:

```csharp
// NEW: Add at top of NameResolver.cs or in separate file

/// <summary>
/// Shared context for type definitions across multiple files.
/// Used during the inheritance resolution pass to ensure all types are visible.
/// </summary>
public class TypeDefinitionContext
{
    public List<(string FilePath, ClassDef ClassDef)> ClassDefs { get; } = new();
    public List<(string FilePath, StructDef StructDef)> StructDefs { get; } = new();
    public List<(string FilePath, InterfaceDef InterfaceDef)> InterfaceDefs { get; } = new();
    public List<(string FilePath, EnumDef EnumDef)> EnumDefs { get; } = new();
    
    /// <summary>
    /// Register type definitions from a parsed module.
    /// Called during the type collection phase.
    /// </summary>
    public void RegisterFromModule(string filePath, Module module)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ClassDefs.Add((filePath, classDef));
                    break;
                case StructDef structDef:
                    StructDefs.Add((filePath, structDef));
                    break;
                case InterfaceDef interfaceDef:
                    InterfaceDefs.Add((filePath, interfaceDef));
                    break;
                case EnumDef enumDef:
                    EnumDefs.Add((filePath, enumDef));
                    break;
            }
        }
    }
}
```

**Verification:**
- [ ] TypeDefinitionContext class compiles
- [ ] Existing tests still pass

**Commit:** `feat(semantic): Add TypeDefinitionContext for cross-module type sharing`

---

### Task 1.2: Update NameResolver to Accept Shared Context
**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Description:** Modify NameResolver to optionally use a shared TypeDefinitionContext.

```csharp
public class NameResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    
    // Legacy per-file lists (kept for backward compatibility)
    private readonly List<ClassDef> _classDefs = new();
    private readonly List<StructDef> _structDefs = new();
    private readonly List<InterfaceDef> _interfaceDefs = new();
    private readonly List<EnumDef> _enumDefs = new();
    
    // Shared context for multi-file compilation
    private readonly TypeDefinitionContext? _sharedContext;
    private readonly string? _currentFilePath;
    
    // Existing constructor (backward compatible)
    public NameResolver(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _sharedContext = null;
        _currentFilePath = null;
    }
    
    // NEW: Constructor for multi-file compilation
    public NameResolver(
        SymbolTable symbolTable, 
        TypeDefinitionContext sharedContext,
        string currentFilePath,
        ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _sharedContext = sharedContext;
        _currentFilePath = currentFilePath;
    }
    
    // ... rest of class
}
```

**Verification:**
- [ ] Both constructors work
- [ ] Existing tests still pass (using old constructor)

**Commit:** `feat(semantic): Add shared context support to NameResolver`

---

### Task 1.3: Update ResolveInheritance to Use Shared Context
**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Description:** Modify `ResolveInheritance` to iterate over shared context when available.

```csharp
public void ResolveInheritance()
{
    // Use shared context if available, otherwise use per-file lists
    var classDefs = _sharedContext?.ClassDefs.Select(x => x.ClassDef) 
                    ?? _classDefs.AsEnumerable();
    var structDefs = _sharedContext?.StructDefs.Select(x => x.StructDef) 
                     ?? _structDefs.AsEnumerable();
    var interfaceDefs = _sharedContext?.InterfaceDefs.Select(x => x.InterfaceDef) 
                        ?? _interfaceDefs.AsEnumerable();
    
    foreach (var classDef in classDefs)
    {
        ResolveClassInheritance(classDef);
    }
    
    foreach (var structDef in structDefs)
    {
        ResolveStructInterfaces(structDef);
    }
    
    // Interfaces don't have inheritance to resolve (they can extend other interfaces)
    foreach (var interfaceDef in interfaceDefs)
    {
        ResolveInterfaceExtension(interfaceDef);
    }
}
```

**Verification:**
- [ ] Method compiles
- [ ] Existing single-file tests still pass

**Commit:** `feat(semantic): Update ResolveInheritance to use shared context`

---

### Task 1.4: Update ProjectCompiler to Use Shared Context
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Modify the type collection phase to use a single shared context.

Find the `CollectTypeDeclarations` method and update it:

```csharp
private void CollectTypeDeclarations(ProjectConfig config)
{
    _logger.LogInfo("Phase 3: Collecting type declarations across all files");
    
    // Create shared context for all type definitions
    var typeContext = new TypeDefinitionContext();
    
    // Pass 1: Register all type names (shells) and collect definitions
    foreach (var (sourceFile, module) in _parsedModules)
    {
        _logger.LogDebug($"Collecting types from {sourceFile}");
        
        // Register types in shared context
        typeContext.RegisterFromModule(sourceFile, module);
        
        // Create NameResolver for declaration pass (registers type shells)
        var nameResolver = new NameResolver(_symbolTable, _logger);
        nameResolver.ResolveDeclarations(module);
        
        // Track metrics
        if (_fileMetrics.TryGetValue(sourceFile, out var metrics))
        {
            metrics.TypesResolved = CountTypes(module);
        }
    }
    
    // Pass 2: Resolve inheritance using shared context
    _logger.LogInfo("Phase 3b: Resolving inheritance across all files");
    
    // Use a single NameResolver with the shared context for inheritance resolution
    var inheritanceResolver = new NameResolver(_symbolTable, typeContext, "project", _logger);
    inheritanceResolver.ResolveInheritance();
    
    _logger.LogInfo($"Type collection complete. Total types: {typeContext.ClassDefs.Count + typeContext.StructDefs.Count + typeContext.InterfaceDefs.Count + typeContext.EnumDefs.Count}");
}

private int CountTypes(Module module)
{
    return module.Body.Count(s => s is ClassDef or StructDef or InterfaceDef or EnumDef);
}
```

**Verification:**
- [ ] Compilation succeeds
- [ ] Single-file tests still pass
- [ ] Cross-module inheritance tests progress (may still fail on import resolution)

**Commit:** `feat(project): Use shared TypeDefinitionContext in ProjectCompiler`

---

## Phase 2: Fix Imported Types Not Registered (2-4 hours)

### Task 2.1: Add Type Registration Callback to ImportResolver
**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
**Description:** Add a callback mechanism so imported types can be registered for inheritance resolution.

```csharp
public class ImportResolver
{
    // ... existing fields ...
    
    /// <summary>
    /// Callback invoked when a type is imported.
    /// Used to register imported types for inheritance resolution.
    /// </summary>
    public Action<TypeSymbol>? OnTypeImported { get; set; }
    
    // In the method that processes from-imports, add:
    private void ProcessFromImportedSymbol(Symbol symbol, string alias)
    {
        // ... existing logic ...
        
        // NEW: Notify about imported types
        if (symbol is TypeSymbol typeSymbol && OnTypeImported != null)
        {
            OnTypeImported(typeSymbol);
        }
    }
}
```

**Verification:**
- [ ] Callback can be set
- [ ] Existing tests pass

**Commit:** `feat(semantic): Add OnTypeImported callback to ImportResolver`

---

### Task 2.2: Create ImportedTypeRegistry
**File:** `src/Sharpy.Compiler/Semantic/ImportedTypeRegistry.cs` (NEW)
**Description:** Track imported types so they can be considered during inheritance resolution.

```csharp
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Tracks types imported into each file for inheritance resolution.
/// </summary>
public class ImportedTypeRegistry
{
    // File path -> List of imported TypeSymbols
    private readonly Dictionary<string, List<TypeSymbol>> _importedTypes = new();
    
    /// <summary>
    /// Register an imported type for a file.
    /// </summary>
    public void RegisterImportedType(string filePath, TypeSymbol typeSymbol)
    {
        if (!_importedTypes.TryGetValue(filePath, out var types))
        {
            types = new List<TypeSymbol>();
            _importedTypes[filePath] = types;
        }
        
        // Avoid duplicates
        if (!types.Any(t => t.Name == typeSymbol.Name))
        {
            types.Add(typeSymbol);
        }
    }
    
    /// <summary>
    /// Get all imported types for a file.
    /// </summary>
    public IReadOnlyList<TypeSymbol> GetImportedTypes(string filePath)
    {
        return _importedTypes.TryGetValue(filePath, out var types) 
            ? types 
            : Array.Empty<TypeSymbol>();
    }
    
    /// <summary>
    /// Get all imported types across all files.
    /// </summary>
    public IEnumerable<TypeSymbol> GetAllImportedTypes()
    {
        return _importedTypes.Values.SelectMany(x => x).Distinct();
    }
}
```

**Verification:**
- [ ] Class compiles
- [ ] Basic unit tests pass

**Commit:** `feat(semantic): Add ImportedTypeRegistry for tracking imported types`

---

### Task 2.3: Wire Up Import Registry in ProjectCompiler
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Connect ImportResolver to ImportedTypeRegistry during import resolution phase.

```csharp
private ImportedTypeRegistry _importedTypeRegistry = new();

private void ResolveImports(ProjectConfig config)
{
    _logger.LogInfo("Phase 4: Resolving imports");
    
    foreach (var (sourceFile, module) in _parsedModules)
    {
        var importResolver = new ImportResolver(
            _symbolTable, 
            _moduleResolver, 
            _moduleRegistry, 
            _packageResolver,
            _semanticInfo,
            _logger);
        
        // Wire up type import tracking
        importResolver.OnTypeImported = typeSymbol =>
        {
            _importedTypeRegistry.RegisterImportedType(sourceFile, typeSymbol);
            _logger.LogDebug($"Registered imported type {typeSymbol.Name} in {sourceFile}");
        };
        
        importResolver.ResolveImports(module, sourceFile);
    }
}
```

**Verification:**
- [ ] Import resolution still works
- [ ] Imported types are tracked

**Commit:** `feat(project): Wire ImportedTypeRegistry into ProjectCompiler`

---

### Task 2.4: Include Imported Types in Inheritance Resolution
**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Description:** When resolving inheritance, also check imported types.

Update `ResolveClassInheritance`:

```csharp
private void ResolveClassInheritance(ClassDef classDef)
{
    var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
    if (typeSymbol == null)
    {
        _logger.LogWarning($"Could not find type symbol for class {classDef.Name}");
        return;
    }
    
    // Resolve base class
    if (classDef.BaseClass != null)
    {
        var baseTypeName = GetTypeName(classDef.BaseClass);
        
        // Look up in symbol table (includes imported types)
        var baseSymbol = _symbolTable.Lookup(baseTypeName);
        
        if (baseSymbol is TypeSymbol baseTypeSymbol)
        {
            typeSymbol.BaseType = new UserDefinedType(baseTypeSymbol);
            _logger.LogDebug($"Resolved base class {baseTypeName} for {classDef.Name}");
        }
        else
        {
            _logger.LogWarning($"Could not resolve base class {baseTypeName} for {classDef.Name}");
        }
    }
    
    // Resolve interfaces
    foreach (var interfaceAnnotation in classDef.Interfaces)
    {
        var interfaceName = GetTypeName(interfaceAnnotation);
        var interfaceSymbol = _symbolTable.Lookup(interfaceName);
        
        if (interfaceSymbol is TypeSymbol { TypeKind: TypeKind.Interface } interfaceTypeSymbol)
        {
            typeSymbol.Interfaces.Add(interfaceTypeSymbol);
            _logger.LogDebug($"Resolved interface {interfaceName} for {classDef.Name}");
        }
        else
        {
            _logger.LogWarning($"Could not resolve interface {interfaceName} for {classDef.Name}");
        }
    }
}

private string GetTypeName(TypeAnnotation annotation)
{
    return annotation switch
    {
        SimpleTypeAnnotation simple => simple.Name,
        GenericTypeAnnotation generic => generic.Name,
        _ => annotation.ToString() ?? "unknown"
    };
}
```

**Verification:**
- [ ] Cross-module inheritance tests start passing
- [ ] Existing tests still pass

**Commit:** `feat(semantic): Include imported types in inheritance resolution`

---

## Phase 3: Fix .NET Base Class Inheritance (2-4 hours)

### Task 3.1: Add .NET Type Import Support to ImportResolver
**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
**Description:** Ensure .NET types are registered as TypeSymbols when imported.

Check the `TryResolveNetModule` method and ensure types are registered:

```csharp
private void RegisterNetTypes(string moduleName, Type[] types)
{
    foreach (var type in types)
    {
        if (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum)
        {
            var typeKind = type.IsInterface ? TypeKind.Interface 
                         : type.IsEnum ? TypeKind.Enum
                         : type.IsValueType ? TypeKind.Struct 
                         : TypeKind.Class;
            
            var typeSymbol = new TypeSymbol(type.Name, typeKind)
            {
                ClrType = type,
                IsFromNet = true
            };
            
            // Register in symbol table
            _symbolTable.Define(typeSymbol);
            
            // Notify about imported type
            OnTypeImported?.Invoke(typeSymbol);
        }
    }
}
```

**Verification:**
- [ ] .NET types can be imported
- [ ] Inheriting from .NET types works

**Commit:** `feat(semantic): Register .NET types as TypeSymbols during import`

---

### Task 3.2: Handle .NET Base Classes in Inheritance Resolution
**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Description:** Ensure .NET base classes are properly linked during inheritance resolution.

```csharp
private void ResolveClassInheritance(ClassDef classDef)
{
    // ... existing code ...
    
    if (classDef.BaseClass != null)
    {
        var baseTypeName = GetTypeName(classDef.BaseClass);
        var baseSymbol = _symbolTable.Lookup(baseTypeName);
        
        if (baseSymbol is TypeSymbol baseTypeSymbol)
        {
            typeSymbol.BaseType = new UserDefinedType(baseTypeSymbol);
            
            // Copy virtual methods from .NET base class for override validation
            if (baseTypeSymbol.IsFromNet && baseTypeSymbol.ClrType != null)
            {
                CopyNetVirtualMethods(typeSymbol, baseTypeSymbol.ClrType);
            }
        }
        // ... error handling ...
    }
}

private void CopyNetVirtualMethods(TypeSymbol derived, Type baseClrType)
{
    var virtualMethods = baseClrType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Where(m => m.IsVirtual && !m.IsFinal);
    
    foreach (var method in virtualMethods)
    {
        // Register as overridable in the type symbol
        // This enables @override validation for .NET base classes
        _logger.LogDebug($"Registered virtual method {method.Name} from .NET type {baseClrType.Name}");
    }
}
```

**Verification:**
- [ ] Can inherit from System.Exception
- [ ] Can override .NET virtual methods

**Commit:** `feat(semantic): Support .NET base class inheritance`

---

### Task 3.3: Add .NET Inheritance Integration Test
**File:** `src/Sharpy.Compiler.Tests/Integration/CrossModuleInheritanceTests.cs`
**Description:** Add test for inheriting from .NET base classes.

```csharp
[Fact]
public void ClassInheritance_FromNetBaseClass_Works()
{
    var source = @"
from system import Exception

class CustomError(Exception):
    code: int
    
    def __init__(self, message: str, code: int):
        super().__init__(message)
        self.code = code
";
    
    var result = CompileSingleFile(source);
    
    Assert.Empty(result.Errors);
    Assert.Contains(": Exception", result.GeneratedCSharp);
}
```

**Verification:**
- [ ] Test passes

**Commit:** `test(integration): Add .NET base class inheritance test`

---

## Phase 4: Verification and Cleanup (1-2 hours)

### Task 4.1: Run All Integration Tests
**File:** N/A (command line)
**Description:** Verify all cross-module inheritance tests pass.

```bash
cd /Users/anton/Documents/github/sharpy/src
dotnet test Sharpy.Compiler.Tests --filter "FullyQualifiedName~CrossModuleInheritance" --verbosity normal
```

**Verification:**
- [ ] All cross-module inheritance tests pass

---

### Task 4.2: Run Full Test Suite
**File:** N/A (command line)
**Description:** Ensure no regressions.

```bash
dotnet test Sharpy.Compiler.Tests --verbosity minimal
```

**Verification:**
- [ ] All tests pass
- [ ] Test count hasn't decreased

---

### Task 4.3: Update Documentation
**File:** `docs/implementation_planning/cross_module_inheritance_investigation.md`
**Description:** Mark issues as resolved and document the solution.

Add a "Resolution" section at the end:

```markdown
## Resolution (Completed: [DATE])

The cross-module inheritance issues were fixed by:

1. **Shared TypeDefinitionContext**: A new `TypeDefinitionContext` class collects all type definitions across files, enabling the inheritance resolution pass to see all types.

2. **ImportedTypeRegistry**: Tracks types imported via `from X import Y` statements so they're available for inheritance resolution.

3. **.NET Type Registration**: .NET types are now registered as `TypeSymbol` instances during import, enabling inheritance from .NET base classes.

Key changes:
- `NameResolver` now accepts an optional shared context
- `ImportResolver` has an `OnTypeImported` callback
- `ProjectCompiler` uses shared context for inheritance resolution
```

**Verification:**
- [ ] Documentation updated

**Commit:** `docs: Mark cross-module inheritance issues as resolved`

---

### Task 4.4: Final Commit and PR
**Description:** Create a clean commit history and open PR.

```bash
git rebase -i main  # Squash fixup commits if needed
git push origin feature/cross-module-inheritance
```

**Verification:**
- [ ] PR created with clear description
- [ ] CI passes

---

## Summary

After completing these tasks:

1. ✅ Classes can inherit from classes defined in other modules
2. ✅ Classes can implement interfaces defined in other modules
3. ✅ Inheritance chains across 3+ modules work
4. ✅ .NET base class inheritance works
5. ✅ Existing single-file compilation unchanged

This unblocks Phase 0.1.7 (Inheritance & Interfaces) for language implementation.
