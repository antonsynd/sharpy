# Remaining Compiler Hardening Concerns

> **Date:** 2026-02-03 (updated)
> **Status:** Previous P0/P1 items completed; comprehensive review conducted
> **Context:** Follow-up assessment after architecture review by staff compiler expert

---

## Executive Summary

The compiler is **architecturally sound** with excellent separation of concerns. The three correctness/UX concerns from the original assessment have been implemented. This document now tracks hardening concerns identified during a comprehensive architecture review, covering correctness, robustness, debuggability, and contributability.

The codebase demonstrates strong design decisions: SemanticBinding pattern, immutable dependency graph, SyntaxFactory-only code generation, and comprehensive test coverage (332+ file-based tests, 317 error cases). The concerns below are tactical fixes, not fundamental redesigns.

### Completed Items (Archive)

| # | Concern | Status | Completed |
|---|---------|--------|-----------|
| ~~1~~ | ~~Comparison chain re-evaluation~~ | **DONE** | 2026-02 |
| ~~2~~ | ~~`IsFloatExpression` heuristic~~ | **DONE** | 2026-02 |
| ~~3~~ | ~~Lexer error recovery~~ | **DONE** | 2026-02 |

### Current Concerns

| # | Concern | Type | Impact | Effort |
|---|---------|------|--------|--------|
| 1 | Incremental cache lacks compiler version | Correctness | HIGH | Low |
| 2 | SymbolSerializer format not versioned | Correctness | HIGH | Medium |
| 3 | Restored symbols lack transitive validation | Correctness | HIGH | Medium |
| 4 | Path normalization inconsistent | Correctness | Medium | Low |
| 5 | Name collision in variable versioning | Correctness | Medium | Low |
| 6 | `null!` usage in ProjectCompiler | Robustness | Medium | Low |
| 7 | Dual-write assertions DEBUG-only | Robustness | Medium | Low |
| 8 | No grammar-aware fuzzing/property tests | Robustness | Low | Medium |
| 9 | Missing multi-file integration tests | Coverage | Medium | Medium |
| 10 | TypeChecker size (~4,600 lines) | Maintainability | Low | High |
| 11 | No CancellationToken in semantic analysis | LSP-readiness | Medium | Low |
| 12 | DiagnosticBag deduplication uses strings | UX | Low | Low |
| 13 | Type narrowing not persisted | LSP-readiness | Low | Medium |
| 14 | No Roslyn error mapping to Sharpy source | UX | Medium | Medium |
| 15 | Missing match exhaustiveness warnings | Type Safety | Low | Medium |

**Recommendation:** Address #1-3 before v1.0 (production correctness). Address #4-5, #9, #12 for robustness and coverage. Consider #11, #14 if LSP is on the roadmap. Defer #8, #10, #13, #15 as lower priority.

---

## Completed: Comparison Chain Re-evaluation

> **Status:** IMPLEMENTED
> **Location:** `RoslynEmitter.Expressions.cs:1119-1197`

The implementation uses the C# `is var` pattern to capture intermediate values inline:

```csharp
// a < f() < c now generates:
// a < (f() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < c
```

Key implementation details:
- `IsTrivialExpression()` identifies expressions safe to evaluate multiple times (line 1220-1228)
- `GenerateTempVarName("cmp")` creates unique temp names
- Only intermediate operands (not first/last) are captured

---

## Completed: IsFloatExpression Heuristic

> **Status:** IMPLEMENTED
> **Location:** `RoslynEmitter.Operators.cs:388-418`

The method now consults `SemanticInfo` first:

```csharp
private bool IsFloatExpression(Expression expr)
{
    // First, try to resolve via SemanticInfo (handles variables, function calls, etc.)
    var semanticType = GetExpressionSemanticType(expr);
    if (semanticType != null)
    {
        return semanticType == SemanticType.Float
            || semanticType == SemanticType.Double
            || semanticType == SemanticType.Float32;
    }
    // Fallback to AST-based heuristic...
}
```

---

## Completed: Lexer Error Recovery

> **Status:** IMPLEMENTED
> **Location:** `Lexer.cs:175-265`

The lexer now:
- Catches `LexerAbortException` in `TokenizeAll()` loop
- Calls `RecoverFromError()` to skip to next line
- Respects `MaxErrors` limit (default 25)
- Resets indentation and bracket state after recovery

---

## Concern 1: Incremental Cache Lacks Compiler Version

> **Priority:** P0

### Analysis

**What:** `IncrementalCompilationCache` hashes file contents but not the compiler version. After upgrading the compiler, the cache may contain stale generated C# from the old compiler.

**Current behavior:**
```
obj/Debug/.sharpy-cache    — file content hashes (SHA-256)
obj/Debug/.sharpy-symbols  — serialized symbols + generated C#
```

Neither file includes a compiler version identifier.

**Problem:** If the compiler's code generation changes (e.g., bug fix in operator emission), cached C# won't reflect the fix until the cache is manually deleted.

### Impact

- **Silent correctness bugs** — Old generated C# used after compiler upgrade
- **CI/CD risk** — Build caches from old compiler versions reused
- **Hard to diagnose** — "It works after clean build" is a symptom

### Plan

**Approach:** Add compiler version hash to cache metadata; invalidate on mismatch.

**Files to modify:**
- `src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs`

**Implementation:**

1. **Add version to cache structure:**
   ```csharp
   private record CacheMetadata(string CompilerVersion, Dictionary<string, string> FileHashes);

   private static string GetCompilerVersion()
   {
       var assembly = typeof(IncrementalCompilationCache).Assembly;
       var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
       // Include assembly hash for debug builds where version doesn't change
       var hash = Convert.ToHexStringLower(
           SHA256.HashData(File.ReadAllBytes(assembly.Location))[..8]);
       return $"{version}-{hash}";
   }
   ```

2. **Check version on load:**
   ```csharp
   private Dictionary<string, string> LoadHashCache()
   {
       if (!File.Exists(_cacheFilePath))
           return new();

       var metadata = JsonSerializer.Deserialize<CacheMetadata>(...);
       if (metadata?.CompilerVersion != GetCompilerVersion())
       {
           _logger.LogInfo("Compiler version changed; invalidating cache");
           return new();  // Invalidate entire cache
       }
       return metadata.FileHashes;
   }
   ```

**Tests to add:**
- `IncrementalCompilationCacheTests.InvalidatesOnVersionChange`
- `IncrementalCompilationCacheTests.PreservesOnSameVersion`

**Estimated effort:** 2-3 hours

---

## Concern 2: SymbolSerializer Format Not Versioned

> **Priority:** P0

### Analysis

**What:** `SymbolSerializer` persists symbols and generated C# to `.sharpy-symbols` but includes no schema version. If the serialization format changes, old caches silently produce corrupt data.

**Current behavior:**
- JSON serialization via `System.Text.Json`
- No version field in serialized data
- `PropertyNameCaseInsensitive = true` but no `JsonUnmappedMemberHandling`

**Problem:** Adding/removing/renaming a field in `FileCacheEntry` or symbol types breaks deserialization silently (missing fields get default values).

### Impact

- **Schema evolution blocked** — Can't safely add fields to cached data
- **Silent corruption** — Old cache produces symbols with wrong default values
- **No migration path** — Can't upgrade cache format gracefully

### Plan

**Approach:** Add version header; implement migration or invalidation on version mismatch.

**Files to modify:**
- `src/Sharpy.Compiler/Project/SymbolSerializer.cs`
- `src/Sharpy.Compiler/Project/SymbolCache.cs`

**Implementation:**

1. **Add version to serialization:**
   ```csharp
   private const int CurrentSchemaVersion = 1;

   private record SymbolCacheEnvelope(int SchemaVersion, Dictionary<string, FileCacheEntry> Files);
   ```

2. **Validate on load:**
   ```csharp
   public SymbolCache? Load()
   {
       var envelope = JsonSerializer.Deserialize<SymbolCacheEnvelope>(...);
       if (envelope?.SchemaVersion != CurrentSchemaVersion)
       {
           _logger.LogInfo($"Symbol cache schema {envelope?.SchemaVersion} != {CurrentSchemaVersion}; rebuilding");
           return null;
       }
       return new SymbolCache(envelope.Files);
   }
   ```

3. **Add strict JSON options:**
   ```csharp
   private static readonly JsonSerializerOptions s_strictOptions = new()
   {
       PropertyNameCaseInsensitive = true,
       UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow  // .NET 8+
   };
   ```

**Estimated effort:** 3-4 hours

---

## Concern 3: Restored Symbols Lack Transitive Validation

> **Priority:** P0

### Analysis

**What:** When incremental compilation skips an unchanged file, it restores cached symbols without verifying that their type references are still valid.

**Scenario 1 — Type Existence:**
1. `A.spy` imports `B.spy` and uses `B.MyClass`
2. First build: both compiled, symbols cached
3. `B.spy` changes: `MyClass` renamed to `MyNewClass`
4. Second build: `A.spy` unchanged (same hash), restored from cache
5. **Bug:** `A`'s restored symbol still references `MyClass` (no longer exists)

**Scenario 2 — Signature Staleness (additional concern identified in review):**
1. `A.spy`: `class Foo: def bar(self) -> int: return 1`
2. `B.spy`: `from A import Foo; x = Foo().bar()`
3. First build: cached, B knows `bar()` returns `int`
4. `A.spy` changes: `def bar(self) -> str: return "x"`
5. Second build: A recompiles, B skips (unchanged)
6. **Bug:** B's cached symbol still thinks `bar()` returns `int`

**Current behavior:**
```csharp
// ProjectCompiler.cs:386-389
if (_incremental && _incrementalCache != null && _filesToSkip.Count > 0)
{
    RestoreCachedSymbols();  // No validation of restored symbol references
}
```

### Impact

- **Silent type mismatches** — Restored symbols have stale type references
- **Incremental builds differ from clean builds** — Hard to diagnose
- **Only affects cross-file references** — Single-file projects unaffected

### Plan

**Approach:** After restoring symbols, validate that all type references resolve in current SymbolTable AND that signatures match.

**Files to modify:**
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

**Implementation:**

1. **Add validation after restore:**
   ```csharp
   private void RestoreCachedSymbols()
   {
       // ... existing restore logic ...

       // Validate restored symbols have valid type references
       var invalidFiles = new List<string>();
       foreach (var (file, symbols) in _restoredSymbols.GroupBy(s => s.Value.FilePath))
       {
           foreach (var symbol in symbols)
           {
               if (!ValidateSymbolReferences(symbol.Value))
               {
                   invalidFiles.Add(file);
                   break;
               }
           }
       }

       // Re-add invalid files to recompile set
       foreach (var file in invalidFiles)
       {
           _logger.LogInfo($"Restored symbol validation failed for {file}; recompiling");
           _filesToSkip.Remove(file);
           // Remove from _restoredSymbols
       }
   }

   private bool ValidateSymbolReferences(Symbol symbol)
   {
       // Check that all type references resolve
       return symbol switch
       {
           VariableSymbol v => v.Type == null || ResolveType(v.Type),
           FunctionSymbol f => f.ReturnType == null || ResolveType(f.ReturnType),
           TypeSymbol t => t.BaseType == null || _symbolTable.Lookup(t.BaseType.Name) != null,
           _ => true
       };
   }
   ```

2. **Add signature compatibility check (critical addition):**
   ```csharp
   private bool ValidateRestoredFunctionSymbol(FunctionSymbol cached)
   {
       // Look up current version of the function in SymbolTable
       var current = _symbolTable.Lookup(cached.Name) as FunctionSymbol;
       if (current == null)
           return false;  // Function no longer exists

       // Validate return type matches
       if (!TypesMatch(cached.ReturnType, current.ReturnType))
           return false;

       // Validate parameter count and types match
       if (cached.Parameters.Count != current.Parameters.Count)
           return false;

       for (int i = 0; i < cached.Parameters.Count; i++)
       {
           if (!TypesMatch(cached.Parameters[i].Type, current.Parameters[i].Type))
               return false;
       }

       return true;
   }

   private bool TypesMatch(SemanticType? a, SemanticType? b)
   {
       if (a == null && b == null) return true;
       if (a == null || b == null) return false;
       // Use structural equality for semantic types
       return a.Equals(b);
   }
   ```

**Tests to add:**
- `IncrementalCompilationTests.RecompilesWhenImportedTypeChanges`
- `IncrementalCompilationTests.RecompilesWhenBaseClassChanges`
- `IncrementalCompilationTests.RecompilesWhenMethodSignatureChanges`
- `IncrementalCompilationTests.RecompilesWhenReturnTypeChanges`

**Estimated effort:** 6-8 hours (increased due to signature validation)

---

## Concern 4: Path Normalization Inconsistent

> **Priority:** P1

### Analysis

**What:** Three different `NormalizePath()` implementations exist across the codebase with subtle differences.

**Current implementations:**

```csharp
// IncrementalCompilationCache.cs:435-443
private static string NormalizePath(string path)
{
    var normalized = Path.GetFullPath(path).Replace('\\', '/');  // ✓ GetFullPath
    if (!OperatingSystem.IsLinux())
        normalized = normalized.ToLowerInvariant();
    return normalized;
}

// ProjectCompiler.cs:1175-1183
private static string NormalizePath(string path)
{
    var normalized = path.Replace('\\', '/');  // ✗ NO GetFullPath
    if (!OperatingSystem.IsLinux())
        normalized = normalized.ToLowerInvariant();
    return normalized;
}

// SymbolSerializer.cs:627-635
private static string NormalizePath(string path)
{
    var normalized = Path.GetFullPath(path).Replace('\\', '/');  // ✓ GetFullPath
    if (!OperatingSystem.IsLinux())
        normalized = normalized.ToLowerInvariant();
    return normalized;
}
```

**Problem:** `ProjectCompiler.NormalizePath()` doesn't call `Path.GetFullPath()`, so relative paths aren't normalized consistently. Two paths referring to the same file might hash differently if one is relative and one absolute.

### Impact

- **Cache key mismatches** — Same file may have different keys across build invocations
- **Subtle incremental build bugs** — File appears "changed" due to path difference
- **Cross-platform inconsistency** — Relative vs absolute paths behave differently

### Plan

**Approach:** Extract single `NormalizePath()` to shared utility class.

**Files to modify:**
- Create `src/Sharpy.Compiler/Utilities/PathNormalizer.cs`
- Update `IncrementalCompilationCache.cs`, `ProjectCompiler.cs`, `SymbolSerializer.cs`

**Implementation:**

```csharp
// Utilities/PathNormalizer.cs
namespace Sharpy.Compiler.Utilities;

public static class PathNormalizer
{
    /// <summary>
    /// Normalizes a file path for use as cache keys and cross-platform comparison.
    /// </summary>
    public static string Normalize(string path)
    {
        // Always resolve to absolute path first
        var normalized = Path.GetFullPath(path).Replace('\\', '/');

        // Case-insensitive on Windows/macOS, case-sensitive on Linux
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }

        return normalized;
    }
}
```

**Estimated effort:** 1-2 hours

---

## Concern 5: Name Collision in Variable Versioning

> **Priority:** P1

### Analysis

**What:** `RoslynEmitter` generates versioned names for variable redeclarations (e.g., `x`, `x_1`, `x_2`) but doesn't check if the generated name collides with a user-declared variable.

**Current code:**
```csharp
// RoslynEmitter.cs:155-165
if (isNewDeclaration)
{
    var currentVersion = _variableVersions[baseName];
    var newVersion = currentVersion + 1;
    _variableVersions[baseName] = newVersion;
    return $"{baseName}_{newVersion}";  // No collision check!
}
```

**Problem scenario:**
```spy
x = 1
x_1 = 2      # User-defined variable
x = 3        # Redeclaration — compiler generates x_1
```

**Generated C#:**
```csharp
long x = 1;
long x_1 = 2;   // User's variable
long x_1 = 3;   // ERROR: Compiler's versioned name collides!
```

### Impact

- **C# compilation errors** — Users see cryptic Roslyn errors about duplicate definitions
- **Unusual but valid code breaks** — Users naming variables with `_N` suffix hit this
- **Hard to diagnose** — Error message doesn't explain the collision

### Plan

**Approach:** Track all declared variable names; skip version numbers that collide.

**Files to modify:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Implementation:**

```csharp
private readonly HashSet<string> _declaredNames = new();

private string GetUniqueVariableName(string baseName, bool isNewDeclaration)
{
    if (!isNewDeclaration && _variableVersions.TryGetValue(baseName, out var version))
    {
        return version == 0 ? baseName : $"{baseName}_{version}";
    }

    if (!_variableVersions.ContainsKey(baseName))
    {
        _variableVersions[baseName] = 0;
        _declaredNames.Add(baseName);
        return baseName;
    }

    // Find next version that doesn't collide
    var currentVersion = _variableVersions[baseName];
    string newName;
    do
    {
        currentVersion++;
        newName = $"{baseName}_{currentVersion}";
    } while (_declaredNames.Contains(newName));

    _variableVersions[baseName] = currentVersion;
    _declaredNames.Add(newName);
    return newName;
}
```

**Tests to add:**
- `RoslynEmitterTests.HandlesVariableNameCollision`
- `RoslynEmitterTests.SkipsCollidingVersionNumbers`

**Estimated effort:** 1-2 hours

---

## Concern 6: `null!` Usage in ProjectCompiler

> **Priority:** P2

### Analysis

**What:** `ProjectCompiler` uses `null!` (null-forgiving operator) for fields that are initialized in `Compile()` rather than the constructor.

**Current code:**
```csharp
// ProjectCompiler.cs:31-49
private SymbolTable _symbolTable = null!;
private SemanticInfo _semanticInfo = null!;
private SemanticBinding _semanticBinding = null!;
private ProjectModel _projectModel = null!;
private ImportResolver _importResolver = null!;
private ProjectCompilationMetrics _projectMetrics = null!;
private DependencyGraphBuilder _graphBuilder = null!;
```

**Problem:** If `Compile()` throws early (e.g., no source files), subsequent code accessing these fields gets `NullReferenceException` instead of a clear error.

### Impact

- **Unclear error messages** — NRE instead of "compilation not started"
- **Defensive code burden** — Callers must handle unexpected NRE
- **IDE analysis weakened** — Nullability warnings suppressed

### Plan

**Approach:** Replace `null!` with explicit initialization or guard checks.

**Option A — Lazy initialization with guard (Recommended for simplicity):**
```csharp
private SymbolTable? _symbolTable;
private SymbolTable SymbolTable => _symbolTable
    ?? throw new InvalidOperationException("Compile() has not been called");
```

**Option B — Initialize in constructor with empty defaults:**
```csharp
private SymbolTable _symbolTable = new();  // Empty, replaced in Compile()
```

**Option C — Make Compile() return a result object (Best for API design):**
```csharp
public ProjectCompilationResult Compile() =>
    new ProjectCompilationResultBuilder(config)
        .Parse()
        .ResolveTypes()
        .Generate()
        .Build();
```

**Recommendation:** Option A for immediate fix; consider Option C for future API improvements.

**Estimated effort:** 2-3 hours

---

## Concern 7: Dual-Write Assertions DEBUG-Only

> **Priority:** P2

### Analysis

**What:** The compiler uses `SemanticBinding` as the source of truth during analysis, then materializes data onto `Symbol` properties at phase boundaries. Assertions verify consistency, but they're DEBUG-only.

**Current code:**
```csharp
// SemanticBinding.cs:92-97
[Conditional("DEBUG")]
private static void AssertNotFrozen(string storeName, string symbolName)
{
    Debug.Fail($"SemanticBinding freeze violation: {storeName} for {symbolName}");
}

// DualWriteAssertions.cs — all methods use [Conditional("DEBUG")]
[Conditional("DEBUG")]
public static void AssertCodeGenInfoConsistency(SymbolTable table, SemanticBinding binding) { ... }
```

**Problem:** In Release builds, phase violations (writing after freeze) are logged as warnings but don't error. If `IncrementalCompilationCache` restoration writes after freeze, the violation is silent.

### Impact

- **Silent data loss in Release** — Writes after freeze silently discarded
- **Phase violations invisible** — Only caught if running DEBUG builds
- **False confidence** — Tests pass in DEBUG, bugs lurk in Release

### Plan

**Approach:** Remove `[Conditional("DEBUG")]` and always throw. Phase violations are bugs that should fail loudly.

**Simpler implementation (recommended):**
```csharp
// SemanticBinding.cs
private static void AssertNotFrozen(string storeName, string symbolName)
{
    throw new InvalidOperationException(
        $"SemanticBinding freeze violation: attempted to write {storeName} for {symbolName} after freeze");
}
```

**Alternative — Configurable behavior (if backward compatibility needed):**
```csharp
public enum FreezeViolationBehavior { Ignore, Warn, Throw }

private static FreezeViolationBehavior s_freezeViolationBehavior =
#if DEBUG
    FreezeViolationBehavior.Throw;
#else
    FreezeViolationBehavior.Throw;  // Changed: also throw in Release
#endif
```

**Estimated effort:** 1 hour

---

## Concern 8: No Grammar-Aware Fuzzing/Property-Based Tests

> **Priority:** P3

### Analysis

**What:** The test suite has 332+ file-based integration tests but no:
- Grammar-aware fuzz tests
- Property-based tests (QuickCheck-style)
- Stress tests (large files, many symbols)

**Risk:** Lexer/Parser may crash or hang on malformed input not covered by handwritten tests.

**Mitigating factor:** The existing 317 error test cases provide strong negative coverage. This concern is lower priority given the comprehensive error testing already in place.

**Why grammar-aware matters:** Random strings are mostly garbage and won't exercise interesting code paths. Grammar-aware fuzzing generates structurally valid-ish input, then mutates it to find edge cases.

### Plan

**Approach:** Use grammar-aware fuzzing with [SharpFuzz](https://github.com/Metalnem/sharpfuzz) for coverage-guided testing, and [FsCheck](https://fscheck.github.io/FsCheck/) for property-based tests with structured generators.

**Implementation:**

1. **Grammar-aware lexer fuzzing:**
   ```csharp
   // Generate valid-ish token sequences, then mutate
   public class TokenSequenceGenerator : Arbitrary<List<Token>>
   {
       public override Gen<List<Token>> Generator =>
           from count in Gen.Choose(1, 100)
           from tokens in Gen.ListOf(count, GenToken())
           select MutateTokens(tokens);
   }

   [Property]
   public bool LexerNeverCrashes(string input)
   {
       var lexer = new Lexer(input);
       try { lexer.TokenizeAll(); return true; }
       catch (Exception) { return false; }
   }
   ```

2. **AST-based parser fuzzing:**
   ```csharp
   // Generate valid AST trees, serialize to source, then mutate
   public class SourceGenerator : Arbitrary<string>
   {
       public override Gen<string> Generator =>
           from ast in GenValidAst()
           from source in Gen.Constant(AstSerializer.ToSource(ast))
           from mutated in Gen.OneOf(
               Gen.Constant(source),
               DeleteRandomChars(source),
               SwapRandomLines(source),
               CorruptIndentation(source))
           select mutated;
   }
   ```

3. **Stress tests:**
   - 10,000-line file with 100-level nesting depth
   - 1,000 imports in a single file
   - 10,000 symbols in SymbolTable
   - Deeply nested expression: `(((((...((1))...)))))` (1000 levels)

**Files to add:**
- `src/Sharpy.Compiler.Tests/Fuzzing/LexerFuzzTests.cs`
- `src/Sharpy.Compiler.Tests/Fuzzing/ParserFuzzTests.cs`
- `src/Sharpy.Compiler.Tests/Stress/LargeFileTests.cs`

**Estimated effort:** 6-10 hours

---

## Concern 9: Missing Multi-File Integration Tests

> **Priority:** P1

### Analysis

**What:** The test suite has extensive single-file coverage but sparse multi-file project tests.

**Current multi-file tests:**
- `simple_import_test/` (2-3 files)
- `import_with_classes/` (2-3 files)
- `module_import_access/` (2-3 files)

**Missing scenarios:**
- Circular import detection and error messages
- Transitive import resolution (A imports B imports C imports D)
- Deep import chains with type references
- Diamond dependency patterns
- Cross-file type reference after rename (incremental build)
- File deletion affecting importers (incremental build)
- Package (`__init__.spy`) import scenarios

### Impact

- **Import bugs invisible** — No tests for complex import graphs
- **Incremental build bugs** — No tests for cache invalidation scenarios
- **Cross-file type errors** — Regressions in type reference resolution

### Plan

**Approach:** Add comprehensive multi-file test fixtures.

**Test fixtures to add:**

```
TestFixtures/
  multi_file/
    circular_import_error/
      a.spy           # from b import foo
      b.spy           # from a import bar
      main.spy        # from a import foo
      main.error      # "Circular import detected"

    transitive_imports/
      a.spy           # def a_func(): return 1
      b.spy           # from a import a_func; def b_func(): return a_func()
      c.spy           # from b import b_func; def c_func(): return b_func()
      main.spy        # from c import c_func; print(c_func())
      main.expected   # "1\n"

    diamond_dependency/
      base.spy        # class Base: pass
      left.spy        # from base import Base; class Left(Base): pass
      right.spy       # from base import Base; class Right(Base): pass
      main.spy        # from left import Left; from right import Right
      main.expected   # (no output, just verify compilation)

    cross_file_type_reference/
      types.spy       # class MyType: pass
      user.spy        # from types import MyType; x: MyType = MyType()
      main.spy        # from user import x; print(type(x).__name__)
      main.expected   # "MyType\n"

    package_import/
      mypackage/
        __init__.spy  # from .module import helper
        module.spy    # def helper(): return 42
      main.spy        # from mypackage import helper; print(helper())
      main.expected   # "42\n"
```

**Incremental build tests (programmatic):**

```csharp
[Fact]
public void RecompilesWhenImportedTypeChanges()
{
    using var helper = new ProjectCompilationHelper(output);
    helper.AddSourceFile("types.spy", "class MyClass:\n    x: int = 1");
    helper.AddSourceFile("main.spy", "from types import MyClass\nprint(MyClass().x)");
    helper.CreateProjectFile();

    // First build
    var result1 = helper.Compile(incremental: true);
    Assert.Equal("1\n", result1.StandardOutput);

    // Modify types.spy
    helper.UpdateSourceFile("types.spy", "class MyClass:\n    x: int = 99");

    // Second build — main.spy should recompile even though unchanged
    var result2 = helper.Compile(incremental: true);
    Assert.Equal("99\n", result2.StandardOutput);
}
```

**Estimated effort:** 3-6 hours

---

## Concern 10: TypeChecker Size (~4,600 lines)

> **Status:** DEFERRED
> **Trigger:** LSP implementation or major type system changes

### Analysis

**What:** `TypeChecker` is split across 5 partial files but is still a single class with many responsibilities:
- Type checking
- Type inference (via `_expectedType` context)
- Type narrowing (via `_narrowedTypes` dictionary)
- CodeGenInfo computation
- Validation orchestration
- Error reporting

### Rationale

**Why this matters less now:**
- Correctness bugs are fixed (SHP0255/SHP0256 for unrecognized nodes)
- Error propagation works (Unknown type handled correctly)
- No immediate plans to add major type system features

**Why it would matter for future work:**
- LSP "hover" and "go to definition" need to extract logic from TypeChecker
- Adding `ErrorType` sentinel requires touching narrowing logic
- Adding generic constraints requires modifying inference

**Current state:** The 5 partial files are well-organized by concern:
- `TypeChecker.cs` — orchestration, statement dispatch
- `TypeChecker.Expressions.cs` — expression type checking
- `TypeChecker.Statements.cs` — statement type checking
- `TypeChecker.Definitions.cs` — class/function definitions
- `TypeChecker.Utilities.cs` — error helpers, narrowing utilities

### Plan (When Triggered)

**Extract in this order:**

1. **TypeNarrower** (~200 lines)
   - `_narrowedTypes` dictionary
   - `ExtractNarrowedTypes()`, `ExtractNarrowingKey()`
   - Narrowing logic in `CheckIfStatement()`

2. **ExpressionTypeInference** (~500 lines)
   - Core inference from `TypeChecker.Expressions.cs`
   - `_expectedType` context management
   - Binary/unary operator inference

3. **CodeGenInfoComputer** (move to post-TypeChecker pass)
   - Currently interleaved with type checking
   - Should run after all types are resolved

4. **OverloadResolver** (extract from call checking)
   - Currently inlined in `CallExpression` checking
   - Needed for LSP completion suggestions

5. **TypeCompatibilityChecker** (consolidate scattered logic)
   - `IsAssignableTo`, `FindCommonType` scattered across files
   - Extract for testability and reuse

**Estimated effort:** 8-16 hours

---

## Concern 11: No CancellationToken in Semantic Analysis

> **Priority:** P2
> **Type:** LSP-readiness

### Analysis

**What:** Only 3 files reference `CancellationToken` (Compiler.cs, ProjectCompiler.cs, ValidationPipeline.cs), but the actual analysis passes (TypeChecker, NameResolver, TypeResolver) don't accept it.

**Current state:**
```csharp
// TypeChecker has no cancellation support
public void Check(Module module)
{
    // Long-running analysis with no cancellation points
}
```

**Problem:** For LSP, you need to cancel in-progress analysis when the user types. Without threading `CancellationToken` through the pipeline, you can't cancel mid-analysis.

### Impact

- **Blocks responsive LSP implementation** — Can't cancel stale analysis
- **UI freezes** — Long files block until analysis completes
- **Resource waste** — Completing obsolete analysis

### Plan

**Approach:** Add `CancellationToken` parameter to analysis passes with periodic checks.

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

**Implementation:**

```csharp
public void Check(Module module, CancellationToken cancellationToken = default)
{
    foreach (var statement in module.Body)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckStatement(statement);
    }
}
```

**Estimated effort:** 3-4 hours

---

## Concern 12: DiagnosticBag Deduplication Uses String Comparison

> **Priority:** P1
> **Type:** UX

### Analysis

**What:** Duplicate prevention during merge uses `(Line, Column, Message)` tuple:

```csharp
var existingExact = new HashSet<(int?, int?, string)>(
    _diagnostics.GetAll().Select(e => (e.Line, e.Column, e.Message)));
```

**Problem:** If two validators report the same conceptual error with slightly different wording, both appear. If the same validator emits the same error with a whitespace difference, it's not deduplicated.

### Impact

- **Duplicate errors** — Same problem reported multiple times with variant messages
- **Fragile deduplication** — Whitespace changes break deduplication
- **Noisy output** — Users see redundant diagnostics

### Plan

**Approach:** Deduplicate by `(DiagnosticCode, Line, Column)`, not message text.

**Files to modify:**
- `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`

**Implementation:**

```csharp
var existingByCodeAndLocation = new HashSet<(string?, int?, int?)>(
    _diagnostics.GetAll().Select(e => (e.Code, e.Line, e.Column)));

// When adding:
if (existingByCodeAndLocation.Contains((diagnostic.Code, diagnostic.Line, diagnostic.Column)))
    return;  // Skip duplicate
```

**Estimated effort:** 1 hour

---

## Concern 13: Type Narrowing Not Persisted

> **Priority:** P3
> **Type:** LSP-readiness

### Analysis

**What:** `_narrowedTypes` dictionary in TypeChecker is local and doesn't propagate to `SemanticInfo` or `SemanticBinding`.

**Example:**
```python
def process(x: int?) -> int:
    if x is not None:
        return helper(x)  # x narrowed to int here
    return 0
```

The narrowed type at the call site isn't persisted, so LSP hover/completions won't know `x` is `int` (not `int?`) after the guard.

### Impact

- **LSP hover shows wrong type** — Shows declared type, not narrowed type
- **Completions incomplete** — Won't suggest non-nullable methods after null check
- **Type information lost** — Analysis results not preserved for tooling

### Plan

**Approach:** Store narrowing info in `SemanticInfo` with scope tracking.

**Implementation:**

```csharp
// SemanticInfo.cs
public void SetNarrowedType(Expression expr, SemanticType narrowedTo, TextSpan scope);
public SemanticType? GetNarrowedType(Expression expr, int position);
```

**Estimated effort:** 4-6 hours

---

## Concern 14: No Roslyn Error Mapping to Sharpy Source

> **Priority:** P2
> **Type:** UX

### Analysis

**What:** If Roslyn compilation fails (malformed generated C#), the error is opaque:

```
error CS0103: The name 'x_1' does not exist in the current context
```

**Problem:** Users see C# errors, not Sharpy errors. This breaks the abstraction and makes debugging hard.

**Current code path:** `Compiler.cs` → `AssemblyCompiler.Compile()` → returns raw Roslyn diagnostics.

### Impact

- **Confusing errors** — Users must understand generated C# to debug
- **Abstraction leak** — Implementation details exposed to users
- **Hard to diagnose** — No mapping back to `.spy` source

### Plan

**Approach:** Add source mapping to generated C# and a diagnostic mapper.

**Implementation options:**

**Option A — `#line` directives (Recommended):**
```csharp
// RoslynEmitter generates:
#line 42 "main.spy"
long x = 1;
#line hidden
```

**Option B — Structured comments for mapping:**
```csharp
// Generated:
long x = 1; // @source:main.spy:42:5
```

**Option C — Internal compiler error fallback:**
```csharp
public class RoslynDiagnosticMapper
{
    public CompilerDiagnostic? MapToSharpyDiagnostic(Diagnostic roslynDiag)
    {
        // Attempt to map; if fails, return ICE diagnostic
        return new CompilerDiagnostic(
            $"Internal compiler error: {roslynDiag.GetMessage()}",
            Severity.Error,
            code: "SHP9999"
        );
    }
}
```

**Estimated effort:** 6-8 hours

---

## Concern 15: Missing Match Exhaustiveness Warnings

> **Priority:** P3
> **Type:** Type Safety

### Analysis

**What:** No warning when pattern matching doesn't cover all cases:

```python
match x:
    case 1: print("one")
    case 2: print("two")
    # No case 3, no default — no warning
```

**Why it matters:** Python doesn't warn, but a statically-typed language should (matches Axiom 3: Type Safety).

**Current state:** Match statement codegen exists but exhaustiveness checking is missing.

### Impact

- **Runtime errors** — Unhandled cases cause exceptions
- **Type safety gap** — Compiler doesn't catch missing cases
- **Inconsistent with Axiom 3** — Type system should prevent this

### Plan

**Approach:** Add `ExhaustivenessValidator` to `ValidationPipeline`.

**Implementation:**

```csharp
public class ExhaustivenessValidator : ISemanticValidator
{
    public int Order => 350;  // After type checking, before control flow

    public void Validate(Module module, SemanticContext context)
    {
        // For each match statement:
        // - If matching on enum: check all variants covered
        // - If matching on int/str: require case _ default
        // - If matching on union type (future): check all members covered
    }
}
```

**Estimated effort:** 4-6 hours

---

## Implementation Order

| Priority | Item | Effort | Rationale |
|----------|------|--------|-----------|
| **P0** | Cache compiler version (#1) | 2-3h | Silent correctness bugs in incremental builds |
| **P0** | SymbolSerializer versioning (#2) | 3-4h | Schema evolution safety |
| **P0** | Restored symbol validation (#3) | 6-8h | Incremental build correctness |
| **P1** | Path normalization utility (#4) | 1-2h | Subtle cache key bugs |
| **P1** | Name collision fix (#5) | 1-2h | User-facing C# errors |
| **P1** | Multi-file integration tests (#9) | 3-6h | Import graph coverage |
| **P1** | Diagnostic deduplication (#12) | 1h | Cleaner error output |
| **P2** | Replace `null!` (#6) | 2-3h | Better error messages |
| **P2** | Dual-write assertions (#7) | 1h | Phase violation detection |
| **P2** | CancellationToken threading (#11) | 3-4h | LSP-readiness |
| **P2** | Roslyn error mapping (#14) | 6-8h | Debuggability |
| **P3** | Grammar-aware fuzzing (#8) | 6-10h | Edge case coverage |
| **P3** | Type narrowing persistence (#13) | 4-6h | LSP hover/completion accuracy |
| **P3** | Match exhaustiveness (#15) | 4-6h | Type safety |
| **Deferred** | TypeChecker refactor (#10) | 8-16h | Wait for LSP work |

**Total estimated effort:**
- **P0 (Critical):** 11-15 hours
- **P1 (Important):** 6-11 hours
- **P2 (Recommended):** 12-16 hours
- **P3 (Nice-to-have):** 14-22 hours
- **Total P0+P1:** 17-26 hours (~2 weeks)
- **Total P0+P1+P2:** 29-42 hours (~3-4 weeks)

---

## High-Impact Structural Improvements

Beyond the tactical fixes above, these **architectural improvements** would have the biggest long-term impact:

### 1. Introduce `ErrorType` Sentinel

**Current state:** Type errors propagate as `UnknownType`. The problem: you can't distinguish "I don't know the type yet" from "type checking failed."

**Benefit:** Error cascading becomes explicit. Validators can skip nodes with `ErrorType` instead of re-reporting.

```csharp
public sealed record ErrorType : SemanticType
{
    public static readonly ErrorType Instance = new();
    public string Message { get; init; } = "Type error";
    public TextSpan? OriginatingLocation { get; init; }
}
```

**Effort:** 4-6 hours

### 2. Source Mapping for Generated C#

Add `#line` directives to generated C# that map back to `.spy` source:

```csharp
// Generated:
#line 42 "main.spy"
long x = 1;
#line hidden
```

**Benefit:** Roslyn errors become mappable. Stack traces in runtime exceptions point to `.spy` files.

**Effort:** 8-12 hours

### 3. Typed Diagnostic Parameters

Current diagnostics use string interpolation. Better: structured parameters.

```csharp
// Current
Diagnostics.ReportError($"Cannot assign {actualType} to {expectedType}");

// Better
Diagnostics.ReportTypeMismatch(
    code: DiagnosticCodes.TypeMismatch,
    expected: expectedType,
    actual: actualType,
    location: expr.Location);
```

**Benefit:** Enables programmatic error analysis, better LSP integration, machine-readable diagnostics.

**Effort:** 4-6 hours

### 4. Immutable Symbol Snapshot for Cache

Currently, mutable `Symbol` instances are serialized. If any code mutates a symbol after serialization, the cache becomes inconsistent.

**Fix:** Create `FrozenSymbol` record types that are true snapshots:

```csharp
public record FrozenTypeSymbol(
    string Name,
    FrozenTypeSymbol? BaseType,
    ImmutableArray<FrozenMethodSymbol> Methods,
    // ... all data, no mutation possible
);
```

**Benefit:** Cache serialization is provably correct. No race conditions during multi-file compilation.

**Effort:** 6-8 hours

---

## Debuggability Improvements

For "bugs being obvious without discovery months later":

### Structured Logging in Semantic Analysis

**Current state:** Sparse `_logger.LogInfo()` calls in semantic analysis.

**Improvement:** Add trace-level logging for type inference decisions:

```csharp
// Before: silent inference
var inferredType = InferExpressionType(expr);

// After: traceable inference
_logger.LogTrace($"Inferring type for {expr.GetType().Name} at {expr.Location}");
var inferredType = InferExpressionType(expr);
_logger.LogTrace($"Inferred: {inferredType} (expected: {_expectedType})");
```

**Benefit:** Diagnose "why did the compiler infer X instead of Y?"

### Deterministic Error Ordering

**Current state:** Errors from different validators may interleave non-deterministically.

**Improvement:** Sort errors by source location before reporting:

```csharp
// DiagnosticBag.cs
public IEnumerable<Diagnostic> GetOrderedDiagnostics() =>
    _diagnostics.OrderBy(d => d.Location.Line).ThenBy(d => d.Location.Column);
```

**Benefit:** Reproducible error output for diffing and testing.

### AST Node Identity Tracking

**Current state:** Errors mention "expression at line 5, col 10" — ambiguous when multiple expressions on same line.

**Improvement:** Include breadcrumb path in error context:

```csharp
// Error: Type mismatch in FunctionDef[process].body[2].value.args[0]
//        Expected int, got str at line 5, col 10
```

**Benefit:** Unambiguous error location for complex expressions.

### Debug Dump Mode

**Current state:** `emit ast`, `emit tokens`, `emit csharp` commands exist.

**Improvement:** Add `--dump-semantic` flag to dump SemanticInfo and SymbolTable:

```bash
dotnet run --project src/Sharpy.Cli -- run file.spy --dump-semantic
```

**Output:**
```
=== SymbolTable ===
  main (FunctionSymbol) -> () -> None
  x (VariableSymbol) -> int

=== SemanticInfo ===
  Line 2: x = 1
    Expression: IntegerLiteral(1) -> int
    Assignment target: x (VariableSymbol)
```

**Benefit:** Inspect semantic analysis results for debugging.

---

## Contributability Improvements

For external contributors:

### Architecture Diagram

CLAUDE.md is excellent for AI assistants, but humans benefit from visual diagrams. Add a Mermaid diagram to docs:

```mermaid
graph LR
    Source[".spy source"] --> Lexer
    Lexer --> Tokens
    Tokens --> Parser
    Parser --> AST
    AST --> NameResolver
    NameResolver --> ImportResolver
    ImportResolver --> TypeResolver
    TypeResolver --> TypeChecker
    TypeChecker --> ValidationPipeline
    ValidationPipeline --> RoslynEmitter
    RoslynEmitter --> CSharp["C# code"]
    CSharp --> AssemblyCompiler
    AssemblyCompiler --> IL[".NET IL"]
```

### "How to Add a New AST Node" Guide

Currently requires reading 6+ files. Add a checklist document:

```markdown
## Adding a New AST Node

1. **Lexer** (`Lexer/Lexer.cs`)
   - Add `TokenType` if new keyword/operator
   - Add recognition in `ScanToken()`

2. **Parser** (`Parser/Ast/*.cs`, `Parser/Parser.*.cs`)
   - Add AST record in appropriate `Ast/` file
   - Add parsing rule in appropriate `Parser.*.cs` partial

3. **Semantic** (`Semantic/TypeChecker.*.cs`)
   - Add type checking in appropriate partial file
   - Register in visitor dispatch

4. **CodeGen** (`CodeGen/RoslynEmitter.*.cs`)
   - Add C# emission in appropriate partial file

5. **Tests**
   - Add unit tests for each component
   - Add `.spy`/`.expected` integration test
```

### Integration Test Helper Script

Add a script to quickly create test fixtures:

```bash
#!/bin/bash
# scripts/add-test.sh
NAME=$1
SOURCE=$2
EXPECTED=$3

mkdir -p src/Sharpy.Compiler.Tests/Integration/TestFixtures/$NAME
echo "$SOURCE" > src/Sharpy.Compiler.Tests/Integration/TestFixtures/$NAME/main.spy
echo "$EXPECTED" > src/Sharpy.Compiler.Tests/Integration/TestFixtures/$NAME/main.expected

echo "Created test fixture: $NAME"
echo "Run: dotnet test --filter \"DisplayName~$NAME\""
```

---

## Future Considerations (Not Yet Concerns)

These items are well-designed but worth noting for future tooling:

### LSP Readiness

Beyond thread-safety, LSP requires **incremental re-analysis**:

- **SemanticInfo not thread-safe** — Documented; use per-file instances for LSP
- **Parser lacks CancellationToken** — Add when implementing LSP
- **Type narrowing not persisted in SemanticBinding** — Add for cross-file LSP analysis
- **No incremental re-analysis API** — Currently batch-oriented; need single-file analysis

**Future API shape for LSP:**
```csharp
interface IIncrementalSemanticAnalyzer
{
    void InvalidateFile(string path);
    SemanticInfo GetSemanticInfoForFile(string path, SourceText newText);
    IEnumerable<CompletionItem> GetCompletions(string path, int line, int column);
    HoverInfo? GetHoverInfo(string path, int line, int column);
}
```

### Parallel Compilation

- **ProjectCompiler loops are sequential** — SemanticBinding is thread-safe, ready for parallelization
- **ImportResolver uses Stack for cycle detection** — Would need thread-local for parallel imports
- **DependencyGraph enables parallel file processing** — Files with no dependencies can compile in parallel

### Performance

- **No benchmarks** — Add BenchmarkDotNet harness when optimizing
- **Large file stress tests** — Add when targeting enterprise codebases
- **Memory profiling** — Track allocations in hot paths (TypeChecker, RoslynEmitter)

---

## Positive Findings

Despite identified concerns, the compiler demonstrates **strong design decisions**:

1. **SemanticBinding Pattern** — Elegant separation of semantic data from AST, enabling multiple bindings
2. **Dual-write assertions** — Catches phase violations during DEBUG builds
3. **Immutable dependency graph** — Thread-safe for future parallelization
4. **RoslynEmitter using SyntaxFactory** — Prevents invalid C# generation (type-safe API)
5. **Error recovery in Lexer/Parser** — Panic-mode synchronization for good UX
6. **Comprehensive file-based integration tests** — 332+ test fixtures, 317 error tests
7. **Clear phase pipeline** — Parse → Semantic → CodeGen → Assembly
8. **Module-level validation** — Distinguishes entry-point vs. library files
9. **Reference equality for mutable symbols** — Correctly handles mutable record identity
10. **Well-documented threading constraints** — SemanticInfo vs SemanticBinding clearly documented
11. **No TODOs/FIXMEs in codebase** — Clean, maintained code

---

## Appendix: Related Files

| Concern | Primary Files |
|---------|---------------|
| Cache versioning (#1) | `Project/IncrementalCompilationCache.cs` |
| Symbol serialization (#2) | `Project/SymbolSerializer.cs`, `Project/SymbolCache.cs` |
| Restored symbol validation (#3) | `Project/ProjectCompiler.cs:385-487` |
| Path normalization (#4) | `Project/IncrementalCompilationCache.cs`, `Project/ProjectCompiler.cs`, `Project/SymbolSerializer.cs` |
| Name collision (#5) | `CodeGen/RoslynEmitter.cs:143-200` |
| Null-forgiving operators (#6) | `Project/ProjectCompiler.cs:31-49` |
| Dual-write assertions (#7) | `Semantic/SemanticBinding.cs`, `Semantic/DualWriteAssertions.cs` |
| Fuzzing (#8) | `src/Sharpy.Compiler.Tests/` (new files) |
| Multi-file tests (#9) | `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` |
| TypeChecker (#10) | `Semantic/TypeChecker*.cs` (5 files) |
| CancellationToken (#11) | `Semantic/TypeChecker.cs`, `Semantic/NameResolver.cs`, `Semantic/TypeResolver.cs` |
| Diagnostic deduplication (#12) | `Diagnostics/DiagnosticBag.cs` |
| Type narrowing (#13) | `Semantic/TypeChecker.cs`, `Semantic/SemanticInfo.cs` |
| Roslyn error mapping (#14) | `CodeGen/RoslynEmitter*.cs`, `Compiler.cs` |
| Match exhaustiveness (#15) | `Semantic/Validation/` (new validator) |
