# Implementation Plan: Task 0.1.10.2 - Module Resolution

## Summary

The task asks for a `ModuleResolver.cs` file to resolve module paths to source files. However, **the core resolution logic already exists** in `ImportResolver.cs:333-392`. The implementation should extract this into a dedicated, testable `ModuleResolver` class that:

1. Provides a cleaner separation of concerns
2. Supports multiple search paths from `ProjectConfig.ModulePaths`
3. Is independently testable
4. Can be extended for standard library and external package paths

---

## Step-by-Step Implementation

### Step 1: Create ModuleResolver Class

**File:** `src/Sharpy.Compiler/Semantic/ModuleResolver.cs`

Create a new class that encapsulates module path resolution:

```csharp
public class ModuleResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<string> _searchPaths;

    public ModuleResolver(ICompilerLogger? logger = null)
    public ModuleResolver(ICompilerLogger? logger, IEnumerable<string>? searchPaths)

    // Configure the current module's directory for relative imports
    public void SetCurrentModulePath(string modulePath)

    // Add a search path (for project directories, stdlib, packages)
    public void AddSearchPath(string path)

    // Main resolution method
    public ModuleResolutionResult? Resolve(string moduleName)
}
```

**Key features:**
- Convert dotted module names to file paths: `utils.helpers` → `utils/helpers.spy`
- Search in order: current module dir → configured search paths → CWD
- Support package directories with `__init__.spy`
- Return detailed resolution result with path and resolution type

### Step 2: Define ModuleResolutionResult

**File:** `src/Sharpy.Compiler/Semantic/ModuleResolver.cs`

```csharp
public class ModuleResolutionResult
{
    public string FullPath { get; init; }           // Absolute path to .spy file
    public string ModuleName { get; init; }         // Original module name
    public ModuleResolutionKind Kind { get; init; } // How it was resolved
    public string? SearchPath { get; init; }        // Which search path matched
}

public enum ModuleResolutionKind
{
    RelativeToCurrentModule,  // Found relative to importing file
    ProjectSearchPath,        // Found in project's ModulePath
    StandardLibrary,          // Found in stdlib (future)
    ExternalPackage,          // Found in packages (future)
    CurrentWorkingDirectory   // Found in CWD
}
```

### Step 3: Implement Resolution Algorithm

The resolution algorithm should search in this order:

1. **Relative to current module** (if `_currentModulePath` is set)
   - Try `{currentDir}/{module/path}.spy`
   - Try `{currentDir}/{module/path}/__init__.spy` (package)

2. **Project search paths** (from `ProjectConfig.ModulePaths`)
   - For each configured path, try `{searchPath}/{module/path}.spy`
   - For each configured path, try `{searchPath}/{module/path}/__init__.spy`

3. **Current working directory** (fallback)
   - Try `{cwd}/{module/path}.spy`
   - Try `{cwd}/{module/path}/__init__.spy`

### Step 4: Refactor ImportResolver to Use ModuleResolver

**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

Modify `ImportResolver` to delegate path resolution to `ModuleResolver`:

```csharp
public class ImportResolver
{
    private readonly ModuleResolver _moduleResolver;

    public ImportResolver(ICompilerLogger? logger = null,
                          ModuleRegistry? moduleRegistry = null,
                          ModuleResolver? moduleResolver = null)
    {
        _moduleResolver = moduleResolver ?? new ModuleResolver(logger);
    }

    public void SetCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
        _moduleResolver.SetCurrentModulePath(modulePath);
    }

    // Replace ResolveModulePath() call with _moduleResolver.Resolve()
}
```

This maintains backward compatibility while using the new resolver.

### Step 5: Update Compiler Integration

**File:** `src/Sharpy.Compiler/Compiler.cs`

Update the compiler to pass `ProjectConfig.ModulePaths` to the resolver:

```csharp
// In CompileProject method
var moduleResolver = new ModuleResolver(_logger, projectConfig.ModulePaths);
moduleResolver.AddSearchPath(projectConfig.ProjectDirectory);
var importResolver = new ImportResolver(_logger, _moduleRegistry, moduleResolver);
```

---

## Key Files to Modify

| File | Action |
|------|--------|
| `src/Sharpy.Compiler/Semantic/ModuleResolver.cs` | **Create** - New module resolution class |
| `src/Sharpy.Compiler/Semantic/ImportResolver.cs` | **Modify** - Delegate to ModuleResolver |
| `src/Sharpy.Compiler/Compiler.cs` | **Modify** - Create ModuleResolver with search paths |
| `src/Sharpy.Compiler.Tests/Semantic/ModuleResolverTests.cs` | **Create** - Unit tests |

---

## Tests to Verify

### Unit Tests (ModuleResolverTests.cs)

1. **Basic resolution**
   - `Resolve_SimpleModuleName_ReturnsCorrectPath` - `mymodule` → `mymodule.spy`
   - `Resolve_DottedModuleName_ReturnsCorrectPath` - `utils.helpers` → `utils/helpers.spy`
   - `Resolve_DeepNestedModule_ReturnsCorrectPath` - `a.b.c.d` → `a/b/c/d.spy`

2. **Package support**
   - `Resolve_PackageWithInit_ReturnsInitPath` - `mypackage` → `mypackage/__init__.spy`
   - `Resolve_NestedPackage_ReturnsInitPath` - `pkg.subpkg` → `pkg/subpkg/__init__.spy`

3. **Search path priority**
   - `Resolve_RelativePathFirst_WhenCurrentModuleSet`
   - `Resolve_UsesSearchPaths_WhenNotRelative`
   - `Resolve_FallsBackToCwd_WhenNotInSearchPaths`

4. **Error cases**
   - `Resolve_NonExistentModule_ReturnsNull`
   - `Resolve_EmptyModuleName_ReturnsNull`

5. **Configuration**
   - `AddSearchPath_AppendsToList`
   - `Resolve_WithMultipleSearchPaths_FindsFirst`

### Integration Tests

1. `ImportResolver_WithModuleResolver_ResolvesSpyFiles`
2. `Compiler_WithModulePaths_ResolvesCrossProjectImports`

---

## Potential Risks and Questions

### Risks

1. **Breaking change to ImportResolver API**
   - Mitigation: Add optional `moduleResolver` parameter, create default internally
   - Backward compatibility: existing code without resolver still works

2. **Path separator issues on Windows vs Unix**
   - Mitigation: Use `Path.DirectorySeparatorChar` consistently (already done in existing code)

3. **Case sensitivity**
   - Risk: Module `Utils` vs `utils` may behave differently on different OSs
   - Mitigation: Consider adding case-insensitive search option for Windows

### Questions for Clarification

1. **Should `__init__.spy` take precedence over `module.spy`?**
   - Current design: Try `module.spy` first, then `module/__init__.spy`
   - Python behavior: Package directory takes precedence if both exist
   - Recommendation: Follow Python semantics for familiarity

2. **Standard library path**
   - The task mentions "Standard library paths (future)"
   - Should we add a placeholder for this now, or defer entirely?
   - Recommendation: Add `StandardLibraryPath` property but leave unimplemented

3. **External package paths**
   - How will external packages be installed/discovered?
   - Recommendation: Defer to future task, just support generic search paths for now

4. **Relative imports (e.g., `from . import sibling`)**
   - Not mentioned in task, but Python supports this
   - Recommendation: Defer to separate task

---

## Implementation Order

1. Create `ModuleResolver.cs` with basic resolution logic
2. Add `ModuleResolutionResult` and `ModuleResolutionKind`
3. Write unit tests for `ModuleResolver`
4. Refactor `ImportResolver` to use `ModuleResolver`
5. Update `Compiler.cs` to configure search paths
6. Run all tests to verify no regressions
