# Implementation Plan: Task 0.1.10.4 - Multi-File Compilation

## Executive Summary

The current `Compiler.CompileProject()` method already implements a working multi-file compilation pipeline. This task focuses on **refactoring and enhancing** the existing implementation to:
1. Extract the multi-file compilation logic into a dedicated `ProjectCompiler` class
2. Implement proper two-phase type declaration collection (cross-file type visibility)
3. Add a proper symbol dependency graph for correct compilation order

## Current State Analysis

### What Already Works
- `Compiler.CompileProject()` (lines 62-360) handles multi-file compilation
- 5-phase pipeline: Parse → Import Resolution → Semantic Analysis → Code Gen → Assembly
- Import resolution via `ImportResolver` with module caching
- Shared `SymbolTable` across all files
- Project configuration via `.spyproj` files

### Key Limitation to Address
The current implementation processes each file's semantic analysis **sequentially** without first collecting all type declarations across files. This means:
- File A can't reference a class defined in File B unless imports are set up
- Cross-file inheritance may fail if types aren't declared in correct order
- The compilation order is essentially file-discovery order, not dependency-based

## Compilation Pipeline (Enhanced)

```
1. Discover all .spy files in project
       ↓
2. Parse all files (AST generation)
       ↓
3. Phase 1: Collect type declarations (classes, structs, enums, interfaces)
   - Scan ALL files to register type names in shared symbol table
   - No inheritance/method resolution yet - just type existence
       ↓
4. Phase 2: Resolve imports and build dependency graph
   - Process import statements
   - Build file dependency DAG
   - Detect circular dependencies
       ↓
5. Phase 3: Semantic Analysis (in topological order)
   - Name resolution (declarations + inheritance)
   - Type checking per file
       ↓
6. Phase 4: Code Generation (per file)
       ↓
7. Phase 5: Assembly Compilation (single assembly from all C#)
```

## Implementation Approach

### Step 1: Create ProjectCompiler Class
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

Extract multi-file compilation from `Compiler.cs` into a dedicated class:

```csharp
namespace Sharpy.Compiler.Project;

public class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    // Track parsed modules
    private Dictionary<string, Module> _parsedModules = new();

    // Track file dependencies for ordering
    private Dictionary<string, HashSet<string>> _dependencies = new();

    public ProjectCompilationResult Compile(ProjectConfig config);

    // Pipeline phases as private methods
    private bool ParseAllFiles(ProjectConfig config);
    private bool CollectTypeDeclarations();
    private bool ResolveImportsAndBuildDependencyGraph(ProjectConfig config);
    private bool PerformSemanticAnalysis();
    private bool GenerateCode(ProjectConfig config);
    private ProjectCompilationResult CompileAssembly(ProjectConfig config);
}
```

### Step 2: Implement Type Declaration Collection Phase
**New behavior in Phase 3:**

Before running full `NameResolver.ResolveDeclarations()`, do a preliminary pass:

```csharp
private bool CollectTypeDeclarations()
{
    // First pass: Just register type names (no members, no inheritance)
    foreach (var (filePath, module) in _parsedModules)
    {
        foreach (var statement in module.Body)
        {
            switch (statement)
            {
                case ClassDef classDef:
                    RegisterTypeShell(classDef.Name, TypeKind.Class, filePath);
                    break;
                case StructDef structDef:
                    RegisterTypeShell(structDef.Name, TypeKind.Struct, filePath);
                    break;
                case InterfaceDef interfaceDef:
                    RegisterTypeShell(interfaceDef.Name, TypeKind.Interface, filePath);
                    break;
                case EnumDef enumDef:
                    RegisterTypeShell(enumDef.Name, TypeKind.Enum, filePath);
                    break;
            }
        }
    }
    return true;
}
```

### Step 3: Build File Dependency Graph
Track which files depend on which for compilation ordering:

```csharp
private void BuildDependencyGraph()
{
    foreach (var (filePath, module) in _parsedModules)
    {
        var deps = new HashSet<string>();

        foreach (var statement in module.Body)
        {
            if (statement is ImportStatement import)
            {
                // Resolve and track dependency
                foreach (var alias in import.Names)
                {
                    var resolved = ResolveModulePath(alias.Name, filePath);
                    if (resolved != null)
                        deps.Add(resolved);
                }
            }
            else if (statement is FromImportStatement fromImport)
            {
                var resolved = ResolveModulePath(fromImport.Module, filePath);
                if (resolved != null)
                    deps.Add(resolved);
            }
        }

        _dependencies[filePath] = deps;
    }
}

private List<string> GetTopologicalOrder()
{
    // Kahn's algorithm for topological sort
    // Returns files in dependency order (dependencies first)
}
```

### Step 4: Update Compiler.cs
Delegate to `ProjectCompiler`:

```csharp
public ProjectCompilationResult CompileProject(ProjectConfig projectConfig)
{
    var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry);
    return projectCompiler.Compile(projectConfig);
}
```

## Files to Modify/Create

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Project/ProjectCompiler.cs` | **CREATE** | New class for project compilation |
| `src/Sharpy.Compiler/Project/FileDependencyGraph.cs` | **CREATE** | Dependency tracking and topological sort |
| `src/Sharpy.Compiler/Compiler.cs` | MODIFY | Delegate to ProjectCompiler |
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | MODIFY | Add `CollectTypeDeclarations()` for preliminary pass |

## Tests to Verify

### New Tests (in `ProjectCompilationTests.cs`)

1. **Cross-file type reference without imports**
   - File A defines `class Foo`, File B uses `Foo` - should work via shared symbol table

2. **Cross-file inheritance**
   - File A: `class Base`, File B: `class Derived(Base)` - should compile regardless of file order

3. **Circular file dependencies**
   - File A imports B, File B imports A - should either work or error gracefully

4. **Compilation order independence**
   - Same source files in different discovery orders should produce identical output

5. **Large project test**
   - 10+ files with complex dependencies - verify correct ordering and compilation

### Existing Tests to Verify Still Pass
- `Compiler_CompileProject_CompilesMultipleFiles`
- `Compiler_CompileProject_DetectsCrossFileImportErrors`
- `Compiler_CompileProject_GeneratesCorrectNamespaces`
- `ImportResolver_ResolveModulePath_Finds__init__spy`

## Potential Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Symbol table collisions (same name in different files) | High | Track source file with each symbol; report duplicate errors with file paths |
| Circular dependencies | Medium | Already detected in `ImportResolver`; add file-level cycle detection |
| Breaking existing compilation behavior | High | Run all existing tests first; ensure backwards compatibility |
| Performance on large projects | Low | Parsing is already done; just adding a lightweight preprocessing phase |

## Questions for Clarification

1. **Should types be implicitly visible across files, or require explicit imports?**
   - Python model: imports required
   - C# model: same namespace = visible
   - Current: imports required - recommend keeping this

2. **How should we handle forward references within a single file?**
   - Already handled by two-pass NameResolver (ResolveDeclarations + ResolveInheritance)

3. **Should `ProjectCompiler` replace `CompileProject` entirely or wrap it?**
   - Recommend: `Compiler.CompileProject` delegates to `ProjectCompiler.Compile`

## Implementation Order

1. Create `FileDependencyGraph.cs` with topological sort
2. Create `ProjectCompiler.cs` skeleton with pipeline phases
3. Move parsing loop from `Compiler.cs` to `ProjectCompiler`
4. Add type declaration collection phase
5. Integrate dependency graph for compilation ordering
6. Move remaining phases (semantic analysis, code gen, assembly)
7. Update `Compiler.CompileProject` to delegate
8. Write new tests for cross-file scenarios
9. Verify all existing tests pass

## Success Criteria

- [ ] All existing `ProjectCompilationTests` pass
- [ ] Cross-file type references work (File B can use type from File A without import)
- [ ] Cross-file inheritance works regardless of file order
- [ ] Circular dependencies are detected and reported clearly
- [ ] Compilation produces correct output for 10+ file projects
- [ ] `ProjectCompiler` class is well-documented and testable in isolation
