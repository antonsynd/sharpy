# Remaining Compiler Hardening Concerns

> **Date:** 2026-02-03 (updated)
> **Status:** Previous P0/P1 items completed; new concerns identified
> **Context:** Follow-up assessment after reviewing implementation progress

---

## Executive Summary

The compiler is in **excellent shape**. The three correctness/UX concerns from the original assessment have been implemented. This document now tracks new hardening concerns identified during a comprehensive architecture review.

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
| 4 | `null!` usage in ProjectCompiler | Robustness | Medium | Low |
| 5 | No fuzzing/property-based tests | Robustness | Medium | Medium |
| 6 | TypeChecker size (~4,600 lines) | Maintainability | Low | High |

**Recommendation:** Address #1-3 before v1.0 (production correctness). Address #4-5 for robustness. Defer #6 until LSP work begins.

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

### Analysis

**What:** When incremental compilation skips an unchanged file, it restores cached symbols without verifying that their type references are still valid.

**Scenario:**
1. `A.spy` imports `B.spy` and uses `B.MyClass`
2. First build: both compiled, symbols cached
3. `B.spy` changes: `MyClass` renamed to `MyNewClass`
4. Second build: `A.spy` unchanged (same hash), restored from cache
5. **Bug:** `A`'s restored symbol still references `MyClass` (no longer exists)

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

**Approach:** After restoring symbols, validate that all type references resolve in current SymbolTable.

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

**Tests to add:**
- `IncrementalCompilationTests.RecompilesWhenImportedTypeChanges`
- `IncrementalCompilationTests.RecompilesWhenBaseClassChanges`

**Estimated effort:** 4-6 hours

---

## Concern 4: `null!` Usage in ProjectCompiler

### Analysis

**What:** `ProjectCompiler` uses `null!` (null-forgiving operator) for fields that are initialized in `Compile()` rather than the constructor.

**Current code:**
```csharp
// ProjectCompiler.cs:31-46
private SymbolTable _symbolTable = null!;
private SemanticInfo _semanticInfo = null!;
private SemanticBinding _semanticBinding = null!;
private ProjectModel _projectModel = null!;
```

**Problem:** If `Compile()` throws early (e.g., no source files), subsequent code accessing these fields gets `NullReferenceException` instead of a clear error.

### Impact

- **Unclear error messages** — NRE instead of "compilation not started"
- **Defensive code burden** — Callers must handle unexpected NRE
- **IDE analysis weakened** — Nullability warnings suppressed

### Plan

**Approach:** Replace `null!` with explicit initialization or guard checks.

**Option A — Lazy initialization with guard:**
```csharp
private SymbolTable? _symbolTable;
private SymbolTable SymbolTable => _symbolTable
    ?? throw new InvalidOperationException("Compile() has not been called");
```

**Option B — Initialize in constructor with empty defaults:**
```csharp
private SymbolTable _symbolTable = new();  // Empty, replaced in Compile()
```

**Option C — Make Compile() return a result object:**
```csharp
public ProjectCompilationResult Compile() =>
    new ProjectCompilationResultBuilder(config)
        .Parse()
        .ResolveTypes()
        .Generate()
        .Build();
```

**Recommendation:** Option A is simplest and makes errors clear.

**Estimated effort:** 1-2 hours

---

## Concern 5: No Fuzzing/Property-Based Tests

### Analysis

**What:** The test suite has 332+ file-based integration tests but no:
- Fuzz tests (random/malformed input)
- Property-based tests (QuickCheck-style)
- Stress tests (large files, many symbols)

**Risk:** Lexer/Parser may crash or hang on malformed input not covered by handwritten tests.

### Plan

**Approach:** Add property-based tests using a library like FsCheck or Hedgehog.

**Tests to add:**

1. **Lexer robustness:**
   ```csharp
   [Property]
   public bool LexerNeverCrashes(string randomInput)
   {
       var lexer = new Lexer(randomInput);
       try { lexer.TokenizeAll(); return true; }
       catch (Exception) { return false; }  // Should never throw
   }
   ```

2. **Parser robustness:**
   ```csharp
   [Property]
   public bool ParserNeverCrashes(List<Token> tokens)
   {
       var parser = new Parser(tokens);
       try { parser.ParseModule(); return true; }
       catch (Exception) { return false; }
   }
   ```

3. **Stress tests:**
   - 10,000-line file with deeply nested scopes
   - 1,000 imports in a single file
   - 10,000 symbols in SymbolTable

**Estimated effort:** 4-8 hours

---

## Concern 6: TypeChecker Size (~4,600 lines)

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

**Estimated effort:** 8-16 hours

---

## Implementation Order

| Priority | Item | Rationale |
|----------|------|-----------|
| **P0** | Cache compiler version (#1) | Silent correctness bugs in incremental builds |
| **P0** | SymbolSerializer versioning (#2) | Schema evolution safety |
| **P1** | Restored symbol validation (#3) | Incremental build correctness |
| **P1** | Replace `null!` (#4) | Clearer error messages |
| **P2** | Fuzz/property tests (#5) | Robustness against malformed input |
| **P3** | TypeChecker refactor (#6) | Deferred until LSP |

**Total estimated effort for P0+P1:** 10-15 hours

---

## Future Considerations (Not Yet Concerns)

These items are well-designed but worth noting for future tooling:

### LSP Readiness

- **SemanticInfo not thread-safe** — Documented; use per-file instances for LSP
- **Parser lacks CancellationToken** — Add when implementing LSP
- **Type narrowing not persisted in SemanticBinding** — Add for cross-file LSP analysis

### Parallel Compilation

- **ProjectCompiler loops are sequential** — SemanticBinding is thread-safe, ready for parallelization
- **ImportResolver uses Stack for cycle detection** — Would need thread-local for parallel imports

### Performance

- **No benchmarks** — Add BenchmarkDotNet harness when optimizing
- **Large file stress tests** — Add when targeting enterprise codebases

---

## Appendix: Related Files

| Concern | Primary Files |
|---------|---------------|
| Cache versioning | `Project/IncrementalCompilationCache.cs` |
| Symbol serialization | `Project/SymbolSerializer.cs`, `Project/SymbolCache.cs` |
| Restored symbol validation | `Project/ProjectCompiler.cs:386-389` |
| Null-forgiving operators | `Project/ProjectCompiler.cs:31-46` |
| TypeChecker | `Semantic/TypeChecker*.cs` (5 files) |
