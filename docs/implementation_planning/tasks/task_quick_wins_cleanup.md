# Task List: Quick Wins and Cleanup

**Goal:** Address small issues that improve code quality, developer experience, and test coverage with minimal effort.

**Priority:** Quick Win - 1-2 hours total for significant improvements.

**Prerequisites:** None

**Estimated Total Effort:** 1-2 hours

---

## Tasks

### Task 1: Remove NuGet Package Warnings
**File:** `src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj`
**Description:** Remove unused package references that generate warnings during build.

Current warnings:
```
warning NU1510: PackageReference System.Net.Http will not be pruned.
warning NU1510: PackageReference System.Text.RegularExpressions will not be pruned.
```

**Fix:** Check if these packages are actually used, and remove if not:

```bash
# Check for usages
cd /Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests
grep -r "System.Net.Http" --include="*.cs" .
grep -r "System.Text.RegularExpressions" --include="*.cs" .
```

If not used, remove from csproj:
```xml
<!-- REMOVE these if unused -->
<PackageReference Include="System.Net.Http" Version="..." />
<PackageReference Include="System.Text.RegularExpressions" Version="..." />
```

**Verification:**
- [ ] Build produces no NU1510 warnings
- [ ] All tests still pass

**Commit:** `chore(tests): Remove unused package references`

---

### Task 2: Add DependencyGraph Integration Test
**File:** `src/Sharpy.Compiler.Tests/Integration/DependencyGraphIntegrationTests.cs` (NEW)
**Description:** Add end-to-end test verifying DependencyGraph works with ProjectCompiler.

```csharp
using Xunit;
using Sharpy.Compiler.Project;

namespace Sharpy.Compiler.Tests.Integration;

public class DependencyGraphIntegrationTests
{
    [Fact]
    public void ProjectCompiler_BuildsCorrectDependencyGraph()
    {
        // Create a multi-file project with known dependencies
        var files = new Dictionary<string, string>
        {
            ["utils.spy"] = @"
def helper() -> int:
    return 42
",
            ["models.spy"] = @"
from utils import helper

class Model:
    value: int
    
    def __init__(self):
        self.value = helper()
",
            ["main.spy"] = @"
from models import Model

def main() -> None:
    m = Model()
"
        };
        
        // Compile and get dependency graph
        var result = CompileProject(files);
        
        // Verify graph structure
        Assert.NotNull(result.DependencyGraph);
        
        // main depends on models
        var mainDeps = result.DependencyGraph.GetDirectDependencies("main.spy");
        Assert.Contains("models.spy", mainDeps);
        
        // models depends on utils
        var modelDeps = result.DependencyGraph.GetDirectDependencies("models.spy");
        Assert.Contains("utils.spy", modelDeps);
        
        // utils has no dependencies
        var utilsDeps = result.DependencyGraph.GetDirectDependencies("utils.spy");
        Assert.Empty(utilsDeps);
        
        // Build order should be: utils, models, main
        var buildOrder = result.DependencyGraph.GetBuildOrder();
        Assert.Equal(3, buildOrder.Count);
        Assert.True(IndexOf(buildOrder, "utils.spy") < IndexOf(buildOrder, "models.spy"));
        Assert.True(IndexOf(buildOrder, "models.spy") < IndexOf(buildOrder, "main.spy"));
    }
    
    [Fact]
    public void ProjectCompiler_DetectsCircularDependencies()
    {
        var files = new Dictionary<string, string>
        {
            ["a.spy"] = @"from b import foo",
            ["b.spy"] = @"from a import bar"
        };
        
        var result = CompileProject(files);
        
        // Should have circular dependency error
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Contains("circular") || e.Contains("Circular"));
    }
    
    [Fact]
    public void DependencyGraph_GetParallelizableGroups_WorksCorrectly()
    {
        // Diamond dependency: a depends on b,c; b,c depend on d
        var files = new Dictionary<string, string>
        {
            ["d.spy"] = "x: int = 1",
            ["b.spy"] = "from d import x\ny: int = x",
            ["c.spy"] = "from d import x\nz: int = x",
            ["a.spy"] = "from b import y\nfrom c import z\nresult: int = y + z"
        };
        
        var result = CompileProject(files);
        var groups = result.DependencyGraph!.GetParallelizableGroups();
        
        // Group 0: d (no deps)
        // Group 1: b, c (depend on d)
        // Group 2: a (depends on b, c)
        Assert.Equal(3, groups.Count);
        Assert.Single(groups[0]); // d
        Assert.Equal(2, groups[1].Count); // b, c
        Assert.Single(groups[2]); // a
    }
    
    private static int IndexOf(IReadOnlyList<string> list, string item)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].EndsWith(item)) return i;
        return -1;
    }
    
    // Helper - implement using existing test infrastructure
    private static ProjectCompilationResult CompileProject(Dictionary<string, string> files)
    {
        // TODO: Use TempDirectory and ProjectCompiler
        throw new NotImplementedException();
    }
}

public class ProjectCompilationResult
{
    public DependencyGraph? DependencyGraph { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Errors.Count > 0;
}
```

**Verification:**
- [ ] Tests compile
- [ ] Tests pass (may need infrastructure work)

**Commit:** `test(integration): Add DependencyGraph integration tests`

---

### Task 3: Add Missing CompilationUnit Tests
**File:** `src/Sharpy.Compiler.Tests/Model/CompilationUnitTests.cs`
**Description:** Ensure CompilationUnit has adequate test coverage.

```csharp
using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class CompilationUnitTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var unit = new CompilationUnit(
            "/path/to/file.spy", 
            "mymodule.file", 
            "x: int = 42");
        
        Assert.Equal("/path/to/file.spy", unit.FilePath);
        Assert.Equal("mymodule.file", unit.ModulePath);
        Assert.Equal("x: int = 42", unit.SourceText);
        Assert.NotEmpty(unit.ContentHash);
        Assert.Equal(CompilationPhase.Created, unit.Phase);
    }
    
    [Fact]
    public void ContentHash_IsDeterministic()
    {
        var source = "def foo() -> int:\n    return 42";
        var unit1 = new CompilationUnit("/a.spy", "a", source);
        var unit2 = new CompilationUnit("/b.spy", "b", source);
        
        // Same content = same hash (regardless of path)
        Assert.Equal(unit1.ContentHash, unit2.ContentHash);
    }
    
    [Fact]
    public void ContentHash_DiffersForDifferentContent()
    {
        var unit1 = new CompilationUnit("/a.spy", "a", "x = 1");
        var unit2 = new CompilationUnit("/a.spy", "a", "x = 2");
        
        Assert.NotEqual(unit1.ContentHash, unit2.ContentHash);
    }
    
    [Fact]
    public void IsStale_ReturnsTrueForNullCache()
    {
        var unit = new CompilationUnit("/a.spy", "a", "x = 1");
        
        Assert.True(unit.IsStale(null));
        Assert.True(unit.IsStale(""));
    }
    
    [Fact]
    public void IsStale_ReturnsFalseForMatchingHash()
    {
        var unit = new CompilationUnit("/a.spy", "a", "x = 1");
        
        Assert.False(unit.IsStale(unit.ContentHash));
    }
    
    [Fact]
    public void IsStale_ReturnsTrueForDifferentHash()
    {
        var unit = new CompilationUnit("/a.spy", "a", "x = 1");
        
        Assert.True(unit.IsStale("different-hash"));
    }
    
    [Fact]
    public void Diagnostics_IsThreadSafe()
    {
        var unit = new CompilationUnit("/a.spy", "a", "x = 1");
        
        // Add diagnostics from multiple threads
        Parallel.For(0, 100, i =>
        {
            unit.Diagnostics.AddError($"Error {i}", i, 0);
        });
        
        Assert.Equal(100, unit.Diagnostics.ErrorCount);
    }
    
    [Fact]
    public void HasErrors_ReflectsDiagnosticState()
    {
        var unit = new CompilationUnit("/a.spy", "a", "x = 1");
        
        Assert.False(unit.HasErrors);
        
        unit.Diagnostics.AddError("Error", 1, 0);
        
        Assert.True(unit.HasErrors);
    }
}
```

**Verification:**
- [ ] Tests pass
- [ ] Coverage improved

**Commit:** `test(model): Add CompilationUnit tests`

---

### Task 4: Clean Up Skipped Tests
**Description:** Review the 13 skipped tests and either fix or remove them.

```bash
cd /Users/anton/Documents/github/sharpy/src
dotnet test Sharpy.Compiler.Tests --filter "TestCategory=Skip" --list-tests
# Or search for [Fact(Skip = ...)]
grep -r "Skip\s*=" --include="*.cs" Sharpy.Compiler.Tests/
```

For each skipped test:
1. If obsolete, delete it
2. If blocked by known issue, add comment with issue reference
3. If fixable, fix it

**Verification:**
- [ ] Skipped tests reviewed
- [ ] Either fixed, documented, or removed

**Commit:** `test: Clean up skipped tests`

---

### Task 5: Add .editorconfig Consistency
**File:** `.editorconfig` (if not exists) or update existing
**Description:** Ensure consistent code style across the project.

Check if `.editorconfig` exists and has C# settings:

```ini
# .editorconfig

root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C# specific
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_preserve_single_line_statements = false

# Prefer explicit types
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# Naming conventions
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_underscore
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camel_case_underscore.capitalization = camel_case
dotnet_naming_style.camel_case_underscore.required_prefix = _
```

**Verification:**
- [ ] .editorconfig exists and is sensible
- [ ] No major formatting conflicts

**Commit:** `chore: Add/update .editorconfig for code consistency`

---

### Task 6: Add README to Key Directories
**Description:** Add README.md files to directories that don't have them.

Check these directories:
- `src/Sharpy.Compiler/Analysis/` - Has README ✅
- `src/Sharpy.Compiler/CodeGen/` - Missing?
- `src/Sharpy.Compiler/Semantic/Validation/` - Missing?
- `src/Sharpy.Compiler/Project/` - Missing?

Add brief READMEs explaining:
- Purpose of the directory
- Key files
- Usage patterns

Example for `CodeGen/`:
```markdown
# Code Generation

This directory contains the Roslyn-based C# code generator.

## Key Files

- `RoslynEmitter.cs` - Main emitter, orchestrates code generation
- `RoslynEmitter.*.cs` - Partial classes for different AST node types
- `CodeGenContext.cs` - Shared context for emission
- `TypeMapper.cs` - Maps Sharpy types to C# types
- `NameMangler.cs` - Converts snake_case to PascalCase, etc.

## Architecture

The emitter follows a visitor pattern over the AST:
1. Module → CompilationUnit
2. Top-level statements → Module class with Main method
3. Type definitions → C# classes/structs/interfaces/enums
4. Functions → Methods
5. Expressions → Roslyn expression syntax

Name resolution uses `CodeGenInfo` computed during semantic analysis.
```

**Verification:**
- [ ] Key directories have READMEs
- [ ] READMEs are accurate

**Commit:** `docs: Add README files to key directories`

---

## Summary

After completing these tasks:

1. ✅ Build is warning-free
2. ✅ DependencyGraph has integration tests
3. ✅ CompilationUnit has comprehensive tests
4. ✅ Skipped tests cleaned up
5. ✅ Code style is consistent
6. ✅ Directories are documented

Total effort: 1-2 hours for meaningful improvements to project hygiene.
