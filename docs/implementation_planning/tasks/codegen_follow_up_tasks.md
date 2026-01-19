# Follow-Up Tasks: Enhance CodeGenInfoComputer for Execution Order Detection

**Status:** ✅ COMPLETED (F.1-F.5) with remaining follow-up work identified

**Original Blocker:** Part A cannot fully remove legacy fallback code until `CodeGenInfoComputer` detects all execution order issues that `GenerateModuleClass()` currently detects.

**Resolution:** Created `ExecutionOrderAnalyzer` class and integrated it into `CodeGenInfoComputer`. The emitter now uses CodeGenInfo for execution order detection when available.

---

## Completed Tasks ✅

### F.1: Understand the Legacy Detection Logic ✅

Documented what the legacy code detects:
1. **Assigned before declared**: Variable used in Assignment before VariableDeclaration
2. **Multiple declarations**: Same variable name declared more than once
3. **References assignment variables**: Initializer references a variable created by Assignment (no type annotation)
4. **References other module variables**: Initializer references non-const module variable
5. **Transitive closure**: If A references B and B has issues, A has issues

---

### F.2: Create ExecutionOrderAnalyzer Class ✅

Created `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs`:
- Multi-pass analysis for execution order detection
- Pass 1: Collect type/function names and const variables
- Pass 2: Track variable positions, detect basic issues
- Pass 3: Detect initializer dependencies with transitive closure

---

### F.3: Integrate into CodeGenInfoComputer ✅

Updated `CodeGenInfoComputer.cs`:
- Added `_variablesWithExecutionOrderIssues` field
- Run `ExecutionOrderAnalyzer.Analyze()` in `ComputeForModule()`
- Use analyzer result for `IsModuleLevel` and `HasExecutionOrderIssues` flags

---

### F.4: Add ExecutionOrderAnalyzer Tests ✅

Created `src/Sharpy.Compiler.Tests/Semantic/ExecutionOrderAnalyzerTests.cs`:
- 29 unit tests covering all execution order scenarios
- Tests for assignment-before-declaration, multiple declarations, transitive dependencies, etc.

---

### F.5: Refactor RoslynEmitter.ModuleClass ✅

Refactored `GenerateModuleClass()`:
- Replaced inline multi-pass detection with `PopulateModuleVariableTracking()`
- Uses CodeGenInfo when available, falls back to legacy for compatibility
- Legacy code moved to `PopulateModuleVariableTrackingLegacy()` for fallback

---

## Remaining Follow-Up Work

### F.7: Remove Legacy Module Variable Tracking (Future)

Once confident that CodeGenInfo is always computed, remove:
- `_moduleConstVariables` field
- `_moduleVariables` field
- `_variablesWithExecutionOrderIssues` field (emitter-side)
- `PopulateModuleVariableTrackingLegacy()` method

**Precondition:** All compilation paths must enable `UsePrecomputedCodeGenInfo`

---

### F.8: Address Local Variable Versioning (Future)

The `_variableVersions` dictionary is still needed for local variable redeclarations because they happen during emission, not semantic analysis.

**Options:**
1. **Keep as-is**: Local variables continue to use runtime versioning during emission
2. **Pre-compute versions**: Extend CodeGenInfo to track local variable versions per scope (requires semantic analysis changes)
3. **Disallow redeclarations**: Enforce unique names per scope (language change)

**Note:** This is lower priority since it doesn't affect the primary CodeGenInfo migration goal.

---

### F.9: Remove [LEGACY FALLBACK] Markers (Future)

After F.7 is complete, remove the `[LEGACY FALLBACK]` comments from:
- `RoslynEmitter.cs` (lines 192, 199)
- `RoslynEmitter.Statements.cs` (line 440)

---

## Summary

The primary goal is achieved: `CodeGenInfoComputer` now detects all execution order issues via `ExecutionOrderAnalyzer`. The emitter uses CodeGenInfo as the primary path with legacy code as a fallback.

Remaining work is cleanup that can be done incrementally:
- F.7: Remove legacy tracking fields (moderate effort)
- F.8: Address local variable versioning (complex, optional)
- F.9: Remove legacy fallback markers (trivial, after F.7)
