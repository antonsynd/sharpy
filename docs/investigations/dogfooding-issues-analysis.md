# Dogfooding Issues Analysis

Analysis of issues found during dogfooding run on 2026-01-26.

## Summary

- **1 Success**: struct_definition test passed
- **5 Failures**: Compilation or execution errors (compiler bugs or prompt issues)
- **11 Skips**: Invalid code generation or unsupported features

---

## Failures Analysis

### Issue 0000: `generic_function/medium` - Execution Failed

**Errors**:
```
Type 'IComparable' has no member 'value'
Inferred type 'T' does not satisfy constraint 'IComparable' for type parameter 'T'
```

**Root Cause**: LLM generated invalid code:
1. Accessed `.value` on an `IComparable` interface type (interfaces have no concrete members)
2. Used incorrect multi-constraint syntax: `[T: IComparable, IFormattable]`

**Fix Applied**: Updated prompts to:
- Warn that interface types have no concrete members
- Document that multiple constraints are NOT SUPPORTED

---

### Issue 0001: `module_imports/medium` - Execution Failed

**Error**: `Cannot assign type 'double' to variable of type 'int'`

**Root Cause**: LLM named a function `double`, which shadows the `double` type (float64).
The line `doubled: int = double(x)` was interpreted as trying to use the `double` type.

**Fix Applied**: Strengthened naming rules warning to explicitly list `double` and other
reserved type names that should never be used as function names.

---

### Issue 0002 & 0004: `module_imports/medium`, `module_utils/medium` - Compilation Failed

**Error**: C# namespace/type not found errors (e.g., `'MathUtils' does not exist`)

**Root Cause**: Multi-file module compilation issue. The dogfood tool's `TempProjectDir`
creates files in a flat temp directory, but the compiler may not be correctly discovering
and compiling all module files.

**Investigation Needed**: See Multi-File Compilation section below.

---

### Issue 0003: `from_import/simple` - Compilation Failed

**Error**: `Cannot implicitly convert type 'uint' to 'int'`

**Root Cause**: The `len()` function returns `uint` but the code expected `int`:
```python
newline_len: int = len(Environment.NewLine)
```

**Investigation Needed**: See len() Return Type section below.

---

## Skip Patterns Analysis

### Pattern 1: "Invalid expected output (Python says: )" - 6 cases

**Root Cause**: The `_sharpy_to_python()` conversion was incomplete:
1. Didn't add `main()` call at the end (Python needs explicit call)
2. Tried to run Sharpy-only features (struct, enum, interfaces) as Python

**Fix Applied**:
1. Add `main()` call at end of converted Python code
2. Skip Python verification for Sharpy-only features (struct, interface, enum, decorators)

---

### Pattern 2: "X.spy invalid per spec" (module files) - 3 cases

**Root Cause**: Validator incorrectly required `main()` in imported module files.
Library modules are NOT entry points and should NOT have `main()`.

**Fix Applied**: Added guidance that library modules with only declarations are valid without `main()`.

---

### Pattern 3: "Unsupported feature: with statement" - 2 cases

**Root Cause**: LLM generated `with` statements despite being on forbidden list.

**Status**: Already in forbidden patterns, but LLM still generated them occasionally.

---

## Outstanding Issues

### 1. Multi-File Module Compilation

**Status**: Needs investigation

**Key Files**:
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- `src/Sharpy.Cli/Commands/RunCommand.cs`
- `build_tools/sharpy_dogfood/compiler.py`

**Observed Behavior**:
- `TempProjectDir` creates files in flat temp directory
- `run_project()` runs `sharpyc run main.spy` with `cwd=project_dir`
- CLI receives only entry point path, must discover modules itself

**Questions to Answer**:
1. Does CLI's `run` command auto-discover .spy files in the same directory?
2. Does `ModuleResolver` search the entry file's directory for imports?
3. Are all discovered modules compiled together?

**Manual Test**:
```bash
# Create temp dir with main.spy + module.spy
mkdir /tmp/multifile_test
cat > /tmp/multifile_test/main.spy << 'EOF'
from helpers import greet

def main():
    print(greet("World"))
EOF

cat > /tmp/multifile_test/helpers.spy << 'EOF'
def greet(name: str) -> str:
    return f"Hello, {name}!"
EOF

# Test compilation
dotnet run --project src/Sharpy.Cli -- run /tmp/multifile_test/main.spy
```

---

### 2. len() Return Type (uint vs int)

**Status**: Breaking change - deferred

**Issue**: `len()` returns `uint` but Python's `len()` returns `int`. This causes:
- Type mismatch when assigning to `int` variables
- Inconsistency with Python semantics

**Scope of Change**:
Changing `ISized.__Len__()` to return `int` requires updating:
- `Index.Normalize()` - takes `uint max` parameter
- `IndexExtensions.ToNormalizedUint32()` - takes `uint max`
- `RangeExtensions.ToSlice()` - takes `uint max`
- `Slice.Normalize()` - takes `uint max`
- All collection implementations (List, Set, Dict, Str)
- All indexer implementations in `ISequence` and `IMutableSequence`
- Internal slice/delete/set methods that use `uint` loop variables

**Recommendation**: Create a separate task to refactor the entire indexing system
from `uint` to `int`. This is a significant breaking change affecting ~15+ files.

**Workaround**: Users can cast: `length: int = int(len(items))`

---

## Fixes Applied

| Issue | Fix | Commit |
|-------|-----|--------|
| Generic multi-constraint syntax | Document in prompts | 5e41c26 |
| Interface member access | Warn in prompts | 5e41c26 |
| Reserved type names | Strengthen warning | 5e41c26 |
| Module file validation | Add library module guidance | 5e41c26 |
| Python verification empty output | Add main() call, skip Sharpy features | 5e41c26 |

---

## Next Steps

1. **Investigate multi-file compilation** - Manual test and trace through CLI/compiler
2. **Plan len() refactoring** - Create task for uint→int migration in indexing system
3. **Re-run dogfooding** - Verify prompt fixes reduce skip/failure rates
