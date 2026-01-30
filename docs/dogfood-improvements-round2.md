# Dogfood Improvements: Round 2 - Implementation Report

This document describes the changes made across 4 commits on the `dev` branch to improve the Sharpy compiler's dogfooding success rate. The dogfood pipeline generates Sharpy programs using an LLM, compiles them, runs them, and checks output correctness. Before these changes, the success rate was ~40%. These changes target the most frequent failure modes identified from analyzing dogfood run results.

## Commits

| Commit | Description |
|--------|-------------|
| `b35fe3d5` | Fix IdentifierName bug, add exception types, auto-discover Sharpy.Core types |
| `91f6bcd4` | Restrict re-export to package `__init__.spy` files only |
| `a11d390c` | Add `emit parse` command, replace Python `ast.parse` with Sharpy parser |
| `5dc535c1` | Add string operation restrictions and exception types to all prompt sections |

---

## Item 1: Fix `IdentifierName("global::...")` Roslyn Bug

### Problem

Roslyn's `SyntaxFactory.IdentifierName()` accepts only simple identifiers (e.g., `"Result"`, `"x"`). Four call sites were passing qualified names containing dots or `::`, such as `"global::Sharpy.Core.Result"`. Roslyn silently accepted this at the syntax level but the resulting C# was invalid: `global` was treated as a standalone variable name, producing `CS0103: The name 'global' does not exist in the current context`.

This manifested in dogfood failures whenever f-string interpolation contained a `try` expression, since the `try` expression codegen path references `global::Sharpy.Core.Result.Try<T>()`.

### Fix

Replaced `IdentifierName()` with `ParseName()` at all 4 sites. `ParseName()` correctly handles qualified names, namespace aliases, and `global::` prefixes by parsing them into the appropriate `QualifiedNameSyntax` / `AliasQualifiedNameSyntax` tree.

### Files Changed

| File | Lines | Before | After |
|------|-------|--------|-------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs` | 288, 301 | `IdentifierName("global::Sharpy.Core.Result")` | `ParseName("global::Sharpy.Core.Result")` |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` | 585, 596 | `IdentifierName("System.Diagnostics.Debug")` | `ParseName("System.Diagnostics.Debug")` |

### Verification

- `dotnet build sharpy.sln` passes
- `dotnet test` passes (4314 tests)
- Write a `.spy` file with `f"result: {try int(s)}"` and confirm it compiles and runs without CS0103

### Risks / Review Notes

- `ParseName()` is strictly more capable than `IdentifierName()` and handles all the same inputs, so no regressions are possible for existing correct usages.
- Grep the codebase for other `IdentifierName()` calls that might pass qualified names. The 4 fixed sites were the only ones found.

---

## Item 2: Add Missing Python Exception Types to Sharpy.Core

### Problem

The Sharpy standard library (`Sharpy.Core`) only had `TypeError` and `ValueError` exception classes. LLM-generated code frequently uses `RuntimeError`, `NotImplementedError`, `ZeroDivisionError`, etc., which caused "Undefined identifier" errors during semantic analysis.

### Fix

Added 5 new exception classes to `src/Sharpy.Core/Builtins/Exceptions.cs`:

- `RuntimeError`
- `NotImplementedError`
- `AttributeError`
- `ZeroDivisionError`
- `OverflowError`

Each follows the existing pattern: a public class inheriting `Exception` with a single `(string message)` constructor. `KeyError` and `IndexError` already existed in separate files (`KeyError.cs`, `IndexError.cs`).

### Verification

- `dotnet build sharpy.sln` passes
- After Item 3 is also applied: `raise RuntimeError("msg")` and `except ZeroDivisionError as e:` should compile and run correctly

### Risks / Review Notes

- These are simple data classes with no behavior. Unlikely to cause issues.
- The exception hierarchy is flat (all inherit directly from `System.Exception`). This matches Python's hierarchy where these are all direct children of `Exception`. If Sharpy later needs `ArithmeticError` as a base for `ZeroDivisionError`/`OverflowError`, that's a compatible additive change.

---

## Item 3: Auto-Discover Sharpy.Core Public Types in BuiltinRegistry

### Problem

The compiler's `BuiltinRegistry` only knew about hardcoded primitive types (`int`, `str`, etc.) and collection types (`list`, `dict`, `set`). All other public types in `Sharpy.Core` — including exception classes — were invisible to the semantic analyzer, causing "Undefined identifier" errors. Adding new types to `Sharpy.Core` required manually registering them in `BuiltinRegistry`, which was fragile and error-prone.

### Design Decision

Rather than manually registering each exception type, the solution extends the existing assembly reflection pipeline (`OverloadIndexBuilder` → `OverloadIndex` → `CachedModuleDiscovery`) to discover not just functions but also types. This means any future public type added to `Sharpy.Core` is automatically available to Sharpy code without compiler changes.

### Implementation

#### 3a. Data model: `DiscoveredTypeInfo` in `OverloadIndex.cs`

Added a `DiscoveredTypeInfo` class and a `Types` list to `ModuleOverloads`:

```csharp
public class DiscoveredTypeInfo
{
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string ClrTypeName { get; set; }  // Assembly-qualified for Type.GetType()
    public bool IsException { get; set; }     // typeof(Exception).IsAssignableFrom()
    public string? BaseTypeName { get; set; }
    public string TypeKind { get; set; }      // "Class", "Struct", "Enum", "Interface"
}
```

This is serialized to the JSON overload index cache alongside function signatures.

#### 3b. Discovery: `DiscoverPublicTypes()` in `OverloadIndexBuilder.cs`

Scans the assembly for all public types, filtering out:
- Compiler-generated types (names starting with `<`)
- `Exports` classes (function containers, not constructible types)
- `Str` wrapper (already mapped to `str` primitive)
- Static classes (`IsAbstract && IsSealed` in CLR terms, excluding interfaces)

Maps namespace to module name: `Sharpy.Core.*` → `"builtins"`, others → lowercase dot-to-underscore.

#### 3c. Conversion: `GetModuleTypes()` / `ConvertToTypeSymbol()` in `CachedModuleDiscovery.cs`

Converts `DiscoveredTypeInfo` to `TypeSymbol` with CLR type resolution. Uses `AppDomain.CurrentDomain.GetAssemblies()` as a fallback if `Type.GetType()` fails (common for types in non-system assemblies).

#### 3d. Registration: `LoadBuiltinTypes()` in `BuiltinRegistry.cs`

Called after `LoadBuiltinFunctions()`. Iterates discovered types and registers them, skipping any already registered (primitives, collections). Also explicitly registers `System.Exception` as a fallback, needed as the base type for catch clauses.

Added `"Sharpy.Core"` to the `TryFindClrType()` namespace search list so the semantic analyzer can resolve Sharpy.Core types by name.

#### 3e. Type resolution priority fix in `TypeMapper.GetMappedTypeName()`

**Critical ordering change.** The method now checks types in this order:

1. **Static `_builtinTypeMap`** (primitives + collections) — fastest, immutable
2. **Symbol table** (user-defined types from current file/project) — allows shadowing builtins
3. **Builtin registry** (auto-discovered types from `Sharpy.Core`) — exception types, etc.
4. **Fallback** — PascalCase conversion for unresolved types

Previously, builtin registry was checked before the symbol table. This caused a bug where a user-defined class named `Complex` was mapped to `global::Sharpy.Core.Complex` (an existing struct in the stdlib) instead of the user's own class. The fix ensures user-defined types always take priority.

For builtin registry types, namespace-aware qualification is applied:
- `System.*` types → simple name (e.g., `Exception`)
- `Sharpy.Core.*` types → `global::Sharpy.Core.X` (e.g., `global::Sharpy.Core.ValueError`)

#### 3f. `super().__init__()` validation relaxation in `TypeChecker.Utilities.cs`

Auto-discovered types (e.g., `Exception`) have `ClrType` set but no `Constructors` list (that would require deep reflection of all constructor overloads). When code calls `super().__init__(msg)` on a class inheriting from `Exception`, the type checker couldn't find the constructor.

Fix: for any type with a non-null `ClrType`, skip `__init__` argument count validation and return a `FunctionType` with `SkipArgumentValidation = true`. This lets C# handle constructor overload resolution at compile time, which is correct since CLR types have well-defined constructors.

### Files Changed

| File | What |
|------|------|
| `src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs` | Added `DiscoveredTypeInfo`, `Types` list |
| `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs` | Added `DiscoverPublicTypes()`, `DeriveModuleNameFromNamespace()` |
| `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs` | Added `GetModuleTypes()`, `ConvertToTypeSymbol()` |
| `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Added `LoadBuiltinTypes()`, `Exception` registration, Sharpy.Core namespace search |
| `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Reordered `GetMappedTypeName()` for correct priority, added namespace-aware qualification |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` | Relaxed `super().__init__` validation for CLR types |

### Test Changes

| File | What |
|------|------|
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterStatementTests.cs` | Updated `RaiseWithException` test: `throw Exception("error")` → `throw new Exception("error")` (correct C# after `Exception` became a registered constructible type) |

### Verification

- `dotnet test` passes (4314 tests)
- Clear the overload index cache (`~/.sharpy/cache/overload-index/`) and rebuild to test cold-cache path
- Compile: `raise ValueError("bad")` — should work
- Compile: `except RuntimeError as e:` — should work
- Compile: a class named `Complex` with methods — should NOT resolve to `Sharpy.Core.Complex`

### Risks / Review Notes

- **Cache invalidation**: The overload index cache is keyed by assembly identity (name + version + hash). If `Sharpy.Core.dll` is rebuilt, the cache auto-invalidates. However, during development, if you add types and the assembly hash doesn't change (e.g., debug builds), you may need to manually clear `~/.sharpy/cache/overload-index/`.
- **`SkipArgumentValidation`** on CLR types means the Sharpy type checker won't catch wrong constructor argument counts for inherited CLR types. This is acceptable because C# will catch these at compile time, and the alternative (reflecting all CLR constructors) adds complexity for marginal benefit.
- **Type shadowing**: User types now always shadow builtins. If someone names a class `ValueError`, it will shadow the builtin. This is consistent with Python semantics and is the desired behavior.

---

## Item 4: Fix Cross-Module Re-Export Ambiguity

### Problem

When module B has `from A import foo`, the compiler generates a delegating method `foo()` in B's `Exports` class that calls A's `foo()`. If `main.spy` imports from both A and B, two `using static` directives bring `foo` into scope from both classes, causing C# error `CS0229: Ambiguity between 'A.Exports.foo' and 'B.Exports.foo'`.

This was the #3 most common dogfood failure.

### Root Cause

The re-export logic in `RoslynEmitter.CompilationUnit.cs` generated delegating members for ALL non-entry-point files. The condition was simply `if (!_context.IsEntryPoint)`. This meant every library module re-exported everything it imported, creating unavoidable ambiguities.

### Fix

Re-exports are now restricted to `__init__.spy` package facade files. Regular library modules no longer generate delegating members for their imports.

#### 4a. `IsPackageInit` property on `CodeGenContext`

Added `bool IsPackageInit` (default `false`) to `CodeGenContext`. Set in `ProjectCompiler.cs`:

```csharp
var isPackageInit = Path.GetFileNameWithoutExtension(sourceFile) == "__init__";
```

#### 4b. Re-export condition change

In `RoslynEmitter.CompilationUnit.cs`, changed:

```csharp
// Before:
if (!_context.IsEntryPoint)
// After:
if (!_context.IsEntryPoint && _context.IsPackageInit)
```

### Files Changed

| File | What |
|------|------|
| `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs` | Added `IsPackageInit` property with doc comment |
| `src/Sharpy.Compiler/Project/ProjectCompiler.cs` | Set `IsPackageInit` based on filename |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` | Tightened re-export condition |

### Deferred Work

Items 4c-4e from the original plan (ambiguity detection safety net, source module tracking, qualified call emission) were deferred. The core fix eliminates the problem at its source — there are no delegating members to create ambiguity. The safety net would only be needed if we revert to broader re-export behavior.

### Verification

- `dotnet test` passes (4314 tests)
- Create a multi-file project:
  - `utils.spy`: defines `def helper() -> int: return 42`
  - `math_ops.spy`: `from utils import helper`, defines its own functions
  - `main.spy`: `from utils import helper` and `from math_ops import some_func`
  - Should compile without CS0229 ambiguity

### Risks / Review Notes

- **Breaking change for `__init__.spy` pattern**: If any existing project relies on non-`__init__` modules re-exporting symbols, those projects will break. However, the current test suite passes, indicating this pattern isn't used in tests.
- **`__init__` detection**: Uses filename stem comparison (`Path.GetFileNameWithoutExtension`). This is simple and correct for the Sharpy module system where `__init__.spy` is always the package facade file.

---

## Item 5: Replace Python `ast.parse()` with Sharpy Parser in Dogfood

### Problem

The dogfood pipeline used Python's `ast.parse()` to pre-validate LLM-generated code before compilation. This was fundamentally wrong: Sharpy is not Python. Keywords like `interface`, `struct`, `enum` are valid Sharpy but invalid Python, causing the validator to reject correct code. This was the #1 and #2 most common dogfood skip reason.

### Implementation

#### 5a. `emit parse` CLI command

Added a new subcommand to the CLI at `src/Sharpy.Cli/Program.cs`:

```
sharpyc emit parse <file.spy>
```

Runs only the Lexer and Parser (no semantic analysis, no code generation). On success, prints `PARSE_OK` to stdout and exits 0. On `LexerError` or `ParserError`, prints the error to stderr with line/column info and exits 1.

This is intentionally minimal — it validates syntax only, not semantics. The dogfood pipeline has separate compilation and validation steps for semantic correctness.

#### 5b. `parse_file()` in `compiler.py`

Added `async def parse_file(source_path, timeout=10.0) -> CompilationResult` to `SharpyCompiler`. Invokes `dotnet run --project <cli> -- emit parse <source_path>` and returns success/failure.

#### 5c. `_validate_sharpy_syntax()` in `orchestrator.py`

Replaced the static `_validate_python_syntax()` method (which used `ast.parse()`) with an async instance method `_validate_sharpy_syntax()`:

```python
async def _validate_sharpy_syntax(self, code: str) -> Optional[str]:
    with TempSourceFile(code) as temp_path:
        result = await self.compiler.parse_file(temp_path, timeout=10.0)
        if result.success:
            return None
        return result.error or "Unknown parse error"
```

Updated both call sites (single-file at line 884, multi-file at line 1144).

### Files Changed

| File | What |
|------|------|
| `src/Sharpy.Cli/Program.cs` | Added `emit parse` subcommand and `EmitParse()` handler |
| `build_tools/sharpy_dogfood/compiler.py` | Added `parse_file()` async method |
| `build_tools/sharpy_dogfood/orchestrator.py` | Replaced `_validate_python_syntax` → `_validate_sharpy_syntax`, updated 2 call sites |

### Verification

- `dotnet build sharpy.sln` passes
- `dotnet test` passes
- Manual test: `dotnet run --project src/Sharpy.Cli -- emit parse test_file.spy` on valid and invalid files
- Run dogfood: code using `interface`, `struct`, `enum` keywords should no longer be skipped
- `import ast` no longer appears in `orchestrator.py`

### Risks / Review Notes

- **Performance**: `emit parse` spawns `dotnet run` which has JIT warmup overhead. For dogfood use (validating one file at a time with network latency between generations), this is acceptable. If parse validation were needed in a tight loop, a persistent process or in-memory call would be better.
- **Static → instance method**: `_validate_python_syntax` was `@staticmethod`. `_validate_sharpy_syntax` is an instance method because it needs `self.compiler`. Both call sites already had `self` available, so this is a clean change.
- **Timeout**: 10 seconds for parse-only validation is generous. Parsing is near-instant for reasonable file sizes.

---

## Item 6: Update Dogfood Prompting Guidance

### Problem

The LLM prompts used by the dogfood pipeline didn't document available exception types and didn't warn about unsupported string operations. This caused two categories of failures:

1. The LLM would use exception types not knowing which ones are available, sometimes inventing non-existent types.
2. The LLM would use `s[i]` (string indexing) or `char in "abc"` (membership testing on strings), which produce type mismatches because string indexing returns `char` in C# but the Sharpy type system expects `Str`.

### Fix

Updated `build_tools/sharpy_dogfood/prompts.py` in all 4 prompt templates:

1. **Single-file generation prompt** (already done in earlier edit)
2. **Multi-file generation prompt** (~line 568, 618)
3. **Regeneration prompt** (~line 768, 784)
4. **Validation prompt** (~line 946, 983)

Each prompt template has both an "allowed features" section and a "forbidden features" section. Changes:

**Added to allowed features / exception handling sections:**
```
Available exception types: ValueError, TypeError, KeyError, IndexError,
RuntimeError, NotImplementedError, AttributeError, ZeroDivisionError,
OverflowError, Exception
```

**Added to forbidden features sections:**
```
- NO string indexing (s[i]) — not yet fully supported, use string methods instead
- NO 'in' operator on strings (char in "abc") — not yet fully supported
- NO character-by-character string iteration — use range(len(s)) and string methods instead
```

### Files Changed

| File | What |
|------|------|
| `build_tools/sharpy_dogfood/prompts.py` | Added exception types to 4 exception handling sections, string restrictions to 4 FORBIDDEN sections |

### Verification

- Read `prompts.py` and verify all 4 prompt templates have consistent FORBIDDEN lists
- Run a dogfood session and verify LLM-generated code avoids string indexing and uses valid exception types

### Risks / Review Notes

- These are prompt-level guardrails, not compiler-level enforcement. The LLM may still occasionally ignore them. The compiler will catch violations at compile time.
- String indexing restrictions should be removed from prompts once the compiler properly supports Pythonic string indexing (returning `str` instead of `char`).

---

## Architecture Impact Summary

### Data flow changes

```
Before:
  Sharpy.Core assembly
    → OverloadIndexBuilder discovers Exports classes only
    → OverloadIndex stores function signatures only
    → CachedModuleDiscovery returns functions only
    → BuiltinRegistry manually registers a few types

After:
  Sharpy.Core assembly
    → OverloadIndexBuilder discovers Exports classes AND public types
    → OverloadIndex stores function signatures AND type descriptors
    → CachedModuleDiscovery returns functions AND TypeSymbols
    → BuiltinRegistry auto-registers all discovered types
```

### Name resolution priority (TypeMapper.GetMappedTypeName)

```
1. Static primitive/collection map  (int→int, list→List, etc.)
2. Symbol table (user-defined types)  ← takes priority over builtins
3. Builtin registry (auto-discovered) ← namespace-aware qualification
4. PascalCase fallback
```

### Code generation changes

```
Before: All non-entry-point modules generate delegating re-export members
After:  Only __init__.spy package files generate delegating re-export members
```

---

## How to Verify These Changes

### Automated

```bash
dotnet build sharpy.sln           # Must succeed
dotnet test                        # 4314+ tests must pass
```

### Manual Compiler Tests

```bash
# Item 1: f-string with try expression
echo 'def main():
    s: str = "42"
    print(f"parsed: {try int(s)}")' > /tmp/test_fstring_try.spy
dotnet run --project src/Sharpy.Cli -- run /tmp/test_fstring_try.spy

# Item 2+3: Exception types
echo 'def main():
    try:
        raise RuntimeError("test error")
    except RuntimeError as e:
        print(f"caught: {e}")' > /tmp/test_exceptions.spy
dotnet run --project src/Sharpy.Cli -- run /tmp/test_exceptions.spy

# Item 4: Cross-module imports (no ambiguity)
mkdir -p /tmp/test_project
echo 'def helper() -> int:
    return 42' > /tmp/test_project/utils.spy
echo 'from utils import helper
def double_helper() -> int:
    return helper() * 2' > /tmp/test_project/math_ops.spy
echo 'from utils import helper
from math_ops import double_helper
def main():
    print(helper())
    print(double_helper())' > /tmp/test_project/main.spy
dotnet run --project src/Sharpy.Cli -- run /tmp/test_project/main.spy

# Item 5: emit parse command
echo 'interface IFoo:
    def bar(self) -> int: ...' > /tmp/test_parse.spy
dotnet run --project src/Sharpy.Cli -- emit parse /tmp/test_parse.spy
# Should print PARSE_OK

echo 'def foo(
    # missing close paren
def bar():
    pass' > /tmp/test_bad.spy
dotnet run --project src/Sharpy.Cli -- emit parse /tmp/test_bad.spy
# Should print error to stderr and exit 1
```

### Dogfood Pipeline

```bash
cd build_tools
python -m sharpy_dogfood.cli --iterations 10
# Verify: interface/struct/enum code is no longer skipped
# Verify: exception types compile correctly
# Verify: no CS0229 ambiguity on multi-file tests
```

---

## Known Limitations and Future Work

1. **String indexing**: The compiler still returns `char` for `s[i]`. This is a design decision pending resolution (Pythonic `str` return vs .NET `char` return). The prompt restriction is a workaround.

2. **CLR constructor validation**: `super().__init__()` argument validation is skipped for all CLR-backed types. This means Sharpy won't catch wrong argument counts when inheriting from CLR types; C# catches them instead.

3. **Overload index cache format**: Adding `Types` to the cache is backward-compatible (JSON deserialization ignores missing fields), but old caches won't have type data. The cache auto-rebuilds on assembly hash mismatch, so this is only relevant during development.

4. **Items 4c-4e deferred**: The ambiguity detection safety net was not implemented. If the re-export restriction (4a-4b) is reverted or loosened, the safety net would need to be built.

5. **`emit parse` performance**: Each invocation spawns a full `dotnet run` process. For batch validation, a persistent compiler service or in-process call would be more efficient.
