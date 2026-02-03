# Phase 3: Robustness and Phase Integrity

> **Priority:** P2 (Recommended)
> **Estimated Effort:** 3-4 hours
> **Prerequisite:** None
> **Concerns Addressed:** #6, #7 from remaining-hardening-concerns.md

---

## Overview

This phase addresses internal code quality issues that don't directly cause user-visible bugs but make the codebase harder to maintain and debug. These are "defense in depth" improvements.

### Why These Matter

1. **`null!` usage** — Suppresses compiler warnings, hides potential NullReferenceExceptions
2. **DEBUG-only assertions** — Phase violations only caught in debug builds, may silently corrupt data in release

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 6 | `null!` usage in ProjectCompiler | 2-3h | Medium |
| 7 | Dual-write assertions DEBUG-only | 1h | Medium |

---

## Task 3.1: Replace `null!` Pattern in ProjectCompiler

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Background

`ProjectCompiler` uses `null!` (null-forgiving operator) for fields initialized in `Compile()` rather than the constructor:

```csharp
// Lines 31-45 of ProjectCompiler.cs (5 fields)
private SymbolTable _symbolTable = null!;
private SemanticInfo _semanticInfo = null!;
private ImportResolver _importResolver = null!;
private ProjectCompilationMetrics _projectMetrics = null!;
private DependencyGraphBuilder _graphBuilder = null!;
```

**Problems:**
1. If `Compile()` throws early, accessing these fields gives unhelpful `NullReferenceException`
2. IDE nullability analysis is disabled for these fields
3. Contributors might use these fields before `Compile()` without realizing

### Implementation Options

**Option A — Lazy initialization with guard (Recommended):**
```csharp
private SymbolTable? _symbolTable;
private SymbolTable SymbolTable => _symbolTable
    ?? throw new InvalidOperationException("Compile() has not been called");
```

**Option B — Initialize with empty defaults:**
```csharp
private SymbolTable _symbolTable = new();  // Replaced in Compile()
```

**Option C — Compilation result builder pattern:**
```csharp
public ProjectCompilationResult Compile() =>
    new ProjectCompilationResultBuilder(config).Parse().Check().Generate().Build();
```

**Recommendation:** Option A is the best balance of safety and minimal refactoring. Option C is better architecture but requires significant refactoring.

### Implementation Checklist

- [ ] **3.1.1** Inventory all `null!` fields
  ```bash
  grep -n "= null!" src/Sharpy.Compiler/Project/ProjectCompiler.cs
  ```

- [ ] **3.1.2** Create backing fields and property accessors for each
  ```csharp
  // Before:
  private SymbolTable _symbolTable = null!;

  // After:
  private SymbolTable? _symbolTableBacking;
  private SymbolTable _symbolTable => _symbolTableBacking
      ?? throw new InvalidOperationException(
          "Cannot access SymbolTable before Compile() is called.");
  ```

- [ ] **3.1.3** Alternatively, use a simpler pattern with a single guard
  ```csharp
  private bool _compileStarted = false;

  private void EnsureCompileStarted([CallerMemberName] string? caller = null)
  {
      if (!_compileStarted)
      {
          throw new InvalidOperationException(
              $"Cannot access {caller} before Compile() is called.");
      }
  }

  // Then at the start of each property that was null!:
  public SymbolTable SymbolTable
  {
      get
      {
          EnsureCompileStarted();
          return _symbolTable!;
      }
  }
  ```

- [ ] **3.1.4** Set the flag/backing field at the start of Compile()
  ```csharp
  public CompilationResult Compile()
  {
      _compileStarted = true;
      // or:
      _symbolTableBacking = new SymbolTable();
      // ... rest of compilation
  }
  ```

- [ ] **3.1.5** Update any internal usages
  Some methods might access these fields directly. Update to use the property accessor.

- [ ] **3.1.6** Add test for early access error
  ```csharp
  [Fact]
  public void ThrowsIfAccessedBeforeCompile()
  {
      var compiler = new ProjectCompiler(config);

      // Should throw before Compile() is called
      Assert.Throws<InvalidOperationException>(() =>
      {
          var _ = compiler.SymbolTable;  // If exposed publicly
      });
  }
  ```

### Detailed Implementation

Here's the complete refactoring pattern:

```csharp
public class ProjectCompiler
{
    // Configuration (set in constructor, never null)
    private readonly ProjectCompilerConfiguration _config;
    private readonly ILogger? _logger;

    // Compilation state (set during Compile, null before)
    private SymbolTable? _symbolTable;
    private SemanticInfo? _semanticInfo;
    private SemanticBinding? _semanticBinding;
    private ProjectModel? _projectModel;
    private ImportResolver? _importResolver;
    private ProjectCompilationMetrics? _projectMetrics;
    private DependencyGraphBuilder? _graphBuilder;

    // Internal accessors with guards
    private SymbolTable SymbolTable => _symbolTable
        ?? throw CompilationNotStarted(nameof(SymbolTable));

    private SemanticInfo SemanticInfo => _semanticInfo
        ?? throw CompilationNotStarted(nameof(SemanticInfo));

    private SemanticBinding SemanticBinding => _semanticBinding
        ?? throw CompilationNotStarted(nameof(SemanticBinding));

    private ProjectModel ProjectModel => _projectModel
        ?? throw CompilationNotStarted(nameof(ProjectModel));

    private ImportResolver ImportResolver => _importResolver
        ?? throw CompilationNotStarted(nameof(ImportResolver));

    private ProjectCompilationMetrics ProjectMetrics => _projectMetrics
        ?? throw CompilationNotStarted(nameof(ProjectMetrics));

    private DependencyGraphBuilder GraphBuilder => _graphBuilder
        ?? throw CompilationNotStarted(nameof(GraphBuilder));

    private static InvalidOperationException CompilationNotStarted(string fieldName)
    {
        return new InvalidOperationException(
            $"Cannot access {fieldName}: Compile() has not been called yet. " +
            "This is a compiler bug - please report it.");
    }

    public CompilationResult Compile()
    {
        // Initialize all state at the start
        _symbolTable = new SymbolTable();
        _semanticInfo = new SemanticInfo();
        _semanticBinding = new SemanticBinding();
        _projectModel = new ProjectModel(_config.RootNamespace);
        _projectMetrics = new ProjectCompilationMetrics();
        _graphBuilder = new DependencyGraphBuilder();
        _importResolver = new ImportResolver(/* ... */);

        // ... rest of compilation ...
    }
}
```

### Verification

```bash
# Ensure no null! remains
grep -c "= null!" src/Sharpy.Compiler/Project/ProjectCompiler.cs
# Should output 0

# Run tests
dotnet test --filter "FullyQualifiedName~ProjectCompiler"
```

### Migration Notes

- This is a refactoring with no behavior change for correct usage
- Incorrect usage (accessing before Compile) now fails fast with clear message
- All existing tests should pass unchanged

---

## Task 3.2: Make Dual-Write Assertions Always Active

**Files:**
- `src/Sharpy.Compiler/Semantic/SemanticBinding.cs`
- `src/Sharpy.Compiler/Semantic/DualWriteAssertions.cs`

### Background

The compiler uses a "dual-write" pattern where semantic data is stored in `SemanticBinding` during analysis, then "materialized" onto `Symbol` properties at phase boundaries. Assertions verify this happens correctly, but they're DEBUG-only:

```csharp
[Conditional("DEBUG")]
private static void AssertNotFrozen(string storeName, string symbolName)
{
    Debug.Fail($"SemanticBinding freeze violation: {storeName} for {symbolName}");
}
```

**Problems:**
1. In Release builds, writes after freeze are silently ignored
2. Phase violations in production code go undetected
3. Only caught if running DEBUG builds

### Why This Matters

The `SemanticBinding` freeze pattern is critical for correctness:
1. **During analysis:** Data is mutable, written to `SemanticBinding`
2. **At phase boundary:** `Freeze()` is called, data is copied to `Symbol`
3. **After freeze:** Any write is a bug (stale data)

In DEBUG, violations fail loudly. In Release, they're silent data corruption.

### Implementation Checklist

- [ ] **3.2.1** Find all `[Conditional("DEBUG")]` attributes in semantic analysis
  ```bash
  grep -rn "Conditional.*DEBUG" src/Sharpy.Compiler/Semantic/
  ```

- [ ] **3.2.2** Remove `[Conditional("DEBUG")]` from `AssertNotFrozen`
  ```csharp
  // Before:
  [Conditional("DEBUG")]
  private static void AssertNotFrozen(string storeName, string symbolName)
  {
      Debug.Fail($"SemanticBinding freeze violation: {storeName} for {symbolName}");
  }

  // After:
  private static void AssertNotFrozen(string storeName, string symbolName)
  {
      throw new InvalidOperationException(
          $"SemanticBinding freeze violation: attempted to write {storeName} for " +
          $"symbol '{symbolName}' after freeze. This is a compiler bug.");
  }
  ```

- [ ] **3.2.3** Update `DualWriteAssertions.cs` similarly
  ```csharp
  // Before:
  [Conditional("DEBUG")]
  public static void AssertCodeGenInfoConsistency(SymbolTable table, SemanticBinding binding)
  {
      // ... validation logic ...
      Debug.Assert(condition, message);
  }

  // After:
  public static void AssertCodeGenInfoConsistency(SymbolTable table, SemanticBinding binding)
  {
      // ... validation logic ...
      if (!condition)
      {
          throw new InvalidOperationException($"CodeGenInfo consistency violation: {message}");
      }
  }
  ```

- [ ] **3.2.4** Consider performance impact

  These assertions run at phase boundaries, not in hot loops. However, for very large projects, we might want a flag:

  ```csharp
  public static class CompilerAssertions
  {
      // Can be disabled for performance-critical scenarios
      // Default: always enabled (safety over speed)
      public static bool EnablePhaseAssertions { get; set; } = true;

      public static void AssertNotFrozen(string storeName, string symbolName)
      {
          if (!EnablePhaseAssertions)
              return;

          throw new InvalidOperationException(/* ... */);
      }
  }
  ```

  **Recommendation:** Don't add the flag initially. Keep it simple. Add only if profiling shows significant overhead.

- [ ] **3.2.5** Replace Debug.Assert with explicit checks
  ```csharp
  // Before:
  Debug.Assert(_codeGenInfo.ContainsKey(symbol), "Missing CodeGenInfo");

  // After:
  if (!_codeGenInfo.ContainsKey(symbol))
  {
      throw new InvalidOperationException(
          $"Missing CodeGenInfo for symbol '{symbol.Name}'. " +
          "This indicates a compiler bug in the semantic analysis phase.");
  }
  ```

- [ ] **3.2.6** Add helpful error messages

  Phase violations are compiler bugs. Make the error messages helpful for debugging:

  ```csharp
  throw new InvalidOperationException(
      $"SemanticBinding freeze violation: attempted to write {storeName} for " +
      $"symbol '{symbolName}' after freeze.\n" +
      $"This is a compiler bug. Please report it with:\n" +
      $"  - The source file being compiled\n" +
      $"  - The phase that called this write (if known)\n" +
      $"  - Stack trace below");
  ```

- [ ] **3.2.7** Test that violations throw in Release builds
  ```csharp
  [Fact]
  public void FreezeViolationThrowsInRelease()
  {
      var binding = new SemanticBinding();
      var symbol = new VariableSymbol("x", null);

      // Write before freeze - OK
      binding.SetCodeGenInfo(symbol, new CodeGenInfo("X"));

      // Freeze
      binding.Freeze();

      // Write after freeze - should throw
      Assert.Throws<InvalidOperationException>(() =>
          binding.SetCodeGenInfo(symbol, new CodeGenInfo("X2")));
  }
  ```

### Verification

```bash
# Build in Release mode
dotnet build -c Release

# Run tests in Release mode
dotnet test -c Release --filter "FullyQualifiedName~SemanticBinding"

# Verify no DEBUG-conditional assertions remain
grep -n "Conditional.*DEBUG" src/Sharpy.Compiler/Semantic/SemanticBinding.cs
# Should output nothing
```

### Impact Assessment

**What breaks if we remove DEBUG conditionals?**

1. **Tests:** Some tests might intentionally trigger violations to test error handling. These should be updated.
2. **Performance:** Negligible — assertions only run at phase boundaries, not per-node.
3. **Error messages:** Users will see internal error messages. Make them actionable.

**What happens if there ARE violations in production?**

With DEBUG-only assertions: Silent data corruption, incorrect compilation.
With always-active assertions: Immediate failure with clear error message.

The second is strictly better.

---

## Phase Completion Criteria

- [ ] No `null!` fields in ProjectCompiler
- [ ] No `[Conditional("DEBUG")]` on phase-critical assertions
- [ ] All tests pass in both Debug and Release configurations
- [ ] Code review completed
- [ ] Accessing ProjectCompiler state before Compile() throws clear error

---

## Notes for Implementers

### Order of Tasks

Both tasks are independent. Work on whichever you prefer first.

### Testing Both Configurations

It's important to run tests in both Debug and Release:

```bash
# Debug (default)
dotnet test

# Release
dotnet test -c Release
```

Any test that passes in Debug but fails in Release indicates a problem with DEBUG-conditional code.

### Common Pitfalls

1. **Forgot to remove `[Conditional("DEBUG")]`** — grep for it after changes
2. **Left Debug.Fail() calls** — These silently do nothing in Release; replace with throws
3. **Property accessors calling methods** — Ensure the guarded property is used consistently

### Future Consideration: Compile-Once Pattern

A cleaner long-term solution for `ProjectCompiler` would be to make `Compile()` return a result object that holds all the state:

```csharp
public sealed class ProjectCompilationContext
{
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }
    // ... etc
}

public ProjectCompilationContext Compile()
{
    var symbolTable = new SymbolTable();
    // ... compilation ...
    return new ProjectCompilationContext(symbolTable, semanticInfo, ...);
}
```

This eliminates the "accessed before Compile" problem entirely. However, it requires more refactoring and can be done in a future phase.
