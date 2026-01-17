# Implementation Plan: Task 0.1.10.CG5 - Handle Re-export Syntax in `__init__.spy`

## Problem Summary

The test `PackageInit_WithReExports_ExportsModuleMembers` fails with parser error:
```
Parser error at line 3, column 6: Expected identifier, got Dot
```

This occurs when parsing relative import syntax:
```python
from .helpers import utility_func
from .data import DATA_VALUE
```

The root cause is that `ParseDottedName()` expects an identifier as the first token, but relative imports start with a `.` (dot).

## Analysis

### Current Code Flow

1. **Parser.cs:1354-1418** - `ParseFromImportStatement()`:
   - Calls `ParseDottedName()` immediately after `from` keyword
   - `ParseDottedName()` (line 1420-1431) expects an identifier first
   - When encountering `.helpers`, it fails on the dot

2. **Statement.cs:368-373** - `FromImportStatement`:
   - Has `Module` as plain string
   - No way to distinguish relative vs absolute imports

3. **RoslynEmitter.cs:392-437** - `GenerateFromImportUsings()`:
   - Assumes module names are absolute
   - No resolution of relative paths

### Expected Behavior

| Syntax | Meaning |
|--------|---------|
| `from .module import X` | Import X from sibling module in same package |
| `from ..module import X` | Import X from module in parent package |
| `from . import X` | Import X from current package's `__init__` |

---

## Step-by-Step Implementation

### Step 1: Modify AST Node (`Statement.cs`)

**File:** `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

Add relative import support to `FromImportStatement`:

```csharp
/// <summary>
/// From-import statement (from module import name1, name2)
/// </summary>
public record FromImportStatement : Statement
{
    /// <summary>
    /// The module name (without relative prefix dots).
    /// For "from .helpers import X", this is "helpers".
    /// For "from . import X", this is empty string.
    /// </summary>
    public string Module { get; init; } = "";

    /// <summary>
    /// Number of leading dots indicating relative import level.
    /// 0 = absolute import (from module import X)
    /// 1 = current package (from .module import X)
    /// 2 = parent package (from ..module import X)
    /// </summary>
    public int RelativeLevel { get; init; } = 0;

    /// <summary>
    /// True if this is a relative import (RelativeLevel > 0)
    /// </summary>
    public bool IsRelativeImport => RelativeLevel > 0;

    public List<ImportAlias> Names { get; init; } = new();
    public bool ImportAll { get; init; }  // from module import *
}
```

### Step 2: Update Parser (`Parser.cs`)

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

#### 2a: Modify `ParseFromImportStatement()` (lines 1354-1418)

Replace the module parsing section to handle leading dots:

```csharp
private FromImportStatement ParseFromImportStatement()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    Expect(TokenType.From);

    // Parse relative import prefix (leading dots)
    int relativeLevel = 0;
    while (Current.Type == TokenType.Dot)
    {
        relativeLevel++;
        Advance();
    }

    // Parse module name (may be empty for "from . import X")
    string module = "";
    if (Current.Type == TokenType.Identifier)
    {
        module = ParseDottedName();
    }

    Expect(TokenType.Import);

    // ... rest of method unchanged (parse names/importAll)

    return new FromImportStatement
    {
        Module = module,
        RelativeLevel = relativeLevel,
        Names = names,
        ImportAll = importAll,
        // ... location info
    };
}
```

### Step 3: Update CodeGenContext (`CodeGenContext.cs`)

**File:** `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

Add method to get current module's package path for relative import resolution:

```csharp
/// <summary>
/// Gets the package path components for the current source file.
/// For "src/mypackage/subpkg/module.spy" with root "src/", returns ["mypackage", "subpkg"].
/// For "__init__.spy" files, includes the containing directory.
/// </summary>
public string[] GetCurrentPackagePath()
{
    if (string.IsNullOrEmpty(SourceFilePath) || string.IsNullOrEmpty(ProjectRootPath))
        return Array.Empty<string>();

    var relativePath = Path.GetRelativePath(ProjectRootPath, SourceFilePath);
    var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

    if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
        return Array.Empty<string>();

    return relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
}
```

### Step 4: Update RoslynEmitter (`RoslynEmitter.cs`)

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

#### 4a: Add relative import resolution method

```csharp
/// <summary>
/// Resolves a relative import to an absolute module path.
/// </summary>
/// <param name="fromImport">The from-import statement with relative level</param>
/// <returns>The resolved absolute module path (e.g., "mypackage.helpers")</returns>
private string ResolveRelativeImport(FromImportStatement fromImport)
{
    if (!fromImport.IsRelativeImport)
        return fromImport.Module;

    // Get current package path
    var packagePath = _context.GetCurrentPackagePath().ToList();

    // Navigate up for each dot beyond the first
    // Level 1 (.) = current package, Level 2 (..) = parent package, etc.
    int levelsUp = fromImport.RelativeLevel - 1;
    for (int i = 0; i < levelsUp && packagePath.Count > 0; i++)
    {
        packagePath.RemoveAt(packagePath.Count - 1);
    }

    // Append the module name if present
    if (!string.IsNullOrEmpty(fromImport.Module))
    {
        packagePath.AddRange(fromImport.Module.Split('.'));
    }

    return string.Join(".", packagePath);
}
```

#### 4b: Update `GenerateFromImportUsings()` (lines 392-437)

Modify to resolve relative imports first:

```csharp
private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
{
    // Resolve relative imports to absolute module path
    var resolvedModule = fromImport.IsRelativeImport
        ? ResolveRelativeImport(fromImport)
        : fromImport.Module;

    var isNetFramework = IsNetFrameworkNamespace(resolvedModule);

    if (isNetFramework)
    {
        var namespaceName = ConvertModuleNameToNamespace(resolvedModule);
        yield return UsingDirective(ParseName(namespaceName));
    }
    else
    {
        var moduleNamespacePath = ConvertModuleNameToNamespace(resolvedModule);
        const string moduleClassName = "Exports";

        string fullModuleClass;
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            fullModuleClass = $"{_context.ProjectNamespace}.{moduleNamespacePath}.{moduleClassName}";
        }
        else
        {
            fullModuleClass = $"{moduleNamespacePath}.{moduleClassName}";
        }

        yield return UsingDirective(ParseName(fullModuleClass))
            .WithStaticKeyword(Token(SyntaxKind.StaticKeyword));
    }
}
```

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | Add `RelativeLevel` and `IsRelativeImport` to `FromImportStatement` |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Update `ParseFromImportStatement()` to parse leading dots |
| `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs` | Add `GetCurrentPackagePath()` method |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `ResolveRelativeImport()`, update `GenerateFromImportUsings()` |

---

## Tests to Verify

### Existing Test (Must Pass)
- `PackageInit_WithReExports_ExportsModuleMembers` - The original failing test

### Additional Tests to Add

1. **Parser Tests** (`src/Sharpy.Compiler.Tests/Parser/`):
   - `FromImport_SingleDotRelative_ParsesCorrectly`
   - `FromImport_DoubleDotRelative_ParsesCorrectly`
   - `FromImport_DotOnlyImport_ParsesCorrectly` (`from . import X`)
   - `FromImport_AbsoluteImport_HasZeroRelativeLevel`

2. **Integration Tests** (`Phase0110IntegrationTests.cs`):
   - `PackageInit_WithReExports_NestedPackage` - Test `..` imports
   - `PackageInit_ReExportWithAlias` - Test `from .mod import X as Y`
   - `PackageInit_ReExportAll` - Test `from .mod import *`

---

## Potential Risks & Questions

### Risks

1. **Breaking existing imports**: Ensure absolute imports (no leading dots) continue to work. The `RelativeLevel = 0` default preserves existing behavior.

2. **Edge case: root-level relative imports**: What happens if someone writes `from .module import X` in a file at the project root? Should emit a warning/error.

3. **Circular imports**: Re-exports could create circular import chains. This is a runtime/semantic issue, not a parser issue—out of scope for this task.

4. **`from . import *`**: Need to handle the case where `Module` is empty string and `ImportAll` is true.

### Questions for Clarification

1. **Should `..` (parent package) imports be supported?**
   - Recommended: Yes, Python supports this and it's useful for complex packages.

2. **What error message for invalid relative imports?**
   - Example: `from ...module import X` when only 2 package levels exist.
   - Recommendation: Emit compile error "Relative import goes beyond package root"

3. **Should `from . import X` (import from package itself) work?**
   - This imports `X` from the current `__init__.spy` which is typically a re-export pattern.
   - Recommendation: Support this syntax.

---

## Implementation Order

1. **Step 1**: Modify AST node (non-breaking, additive)
2. **Step 2**: Update parser (enables syntax, existing tests may still fail on codegen)
3. **Step 3**: Update CodeGenContext (adds helper, non-breaking)
4. **Step 4**: Update RoslynEmitter (completes the feature)
5. **Step 5**: Verify `PackageInit_WithReExports_ExportsModuleMembers` passes
6. **Step 6**: Add additional parser and integration tests
