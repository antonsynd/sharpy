# Sharpy Compiler Robustness Improvements

## Overview

This plan outlines 11 improvements to increase the Sharpy compiler's robustness, debuggability, and maintainability while preparing for future LSP integration. Each section is self-contained and can be implemented independently.

**Target Audience**: Junior engineers or AI assistants (Claude Sonnet)
**Estimated Total Effort**: 12-18 days
**Sections 1-8**: Original plan (validated February 2026)
**Sections 9-11**: Additional high-impact recommendations
**Priority Order**: See Implementation Order table at end

---

## Assessment Summary (February 2026)

**Overall Health Score: 7.5-8/10**

The Sharpy compiler is architecturally excellent with mature error handling infrastructure, clear phase boundaries, and good separation of concerns. The original 8 sections below are **validated as correct and high-value**. Additional high-impact recommendations have been added as sections 9-11.

### Architectural Strengths (No Changes Needed)

1. **Diagnostic Infrastructure** — `DiagnosticBag` with deduplication, `DiagnosticExplanations` with fix guidance, comprehensive `DiagnosticCodes` (SHP0001-SHP0999)
2. **Phase Boundary Assertions** — `DualWriteAssertions.cs` runs production assertions (not DEBUG-only) to catch materialization inconsistencies
3. **Generated C# Validation** — `AssertGeneratedCSharpParses()` always verifies codegen produces valid C#
4. **Incremental Compilation** — `DependencyGraph`, `IncrementalCompilationCache`, `SymbolCacheEnvelope` are production-ready
5. **Services Architecture** — Clean adapter pattern (`CompilerServices`, `CompilerServicesBuilder`) for gradual migration
6. **Validation Pipeline** — Pluggable `ISemanticValidator` with ordering makes adding validators easy

### Known Tech Debt (Tracked Separately)

- `NameMangler.cs:51` — `#99` unconditional method mapping should use type info
- `TypeChecker.Expressions.cs:1516` — `#104` tuple unpacking in comprehensions
- `TypeChecker.Expressions.cs:1085` — duplicate member access evaluation (needs symbol caching in SemanticInfo)
- `SemanticBinding.cs:44-47` — dead `_logger` field with pragma suppression

---

## Guiding Principles (Apply Throughout)

1. **Never break existing tests** — Run `dotnet test` after each change. If tests fail, fix the implementation, not the tests.
2. **Follow existing patterns** — Look at similar code in the codebase before creating new patterns.
3. **Minimize API surface changes** — Prefer internal/private classes. Only make public what's necessary.
4. **Preserve thread-safety** — Any shared state must use `ConcurrentDictionary` or explicit locking.
5. **Add tests for new code** — Every new public method needs at least one unit test.
6. **Use nullable reference types** — All new code should have `#nullable enable` and handle nulls explicitly.
7. **No string concatenation for errors** — Use `DiagnosticBag.AddError()` with proper diagnostic codes.
8. **Immutable where possible** — Prefer `record` types and `readonly` fields.

---

## 1. Type Narrowing Context Extraction

### Context & Rationale

The `TypeChecker` class uses a `Dictionary<string, SemanticType> _narrowedTypes` field to track type narrowing (e.g., after `if x is not None:`, the type of `x` narrows from `T?` to `T`). This dictionary:
- Uses string keys (variable names), which is fragile with shadowing
- Requires manual cleanup when exiting scopes
- Has no explicit contract for when narrowings are valid

Bugs in scope cleanup are silent and can cause incorrect type inference downstream.

### Key Files

- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` — Contains `_narrowedTypes` field
- `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` — Uses narrowing in if/while/match
- `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` — Queries narrowed types

### Guiding Principles

- The new class should be disposable so `using` statements enforce scope cleanup
- Narrowings are keyed by variable name AND scope depth (not just name)
- When in doubt, clear narrowings (safer to lose optimization than have wrong types)
- The existing behavior must be preserved exactly — this is a refactor, not a feature change

### Tasks

- [ ] **1.1** Create `src/Sharpy.Compiler/Semantic/TypeNarrowingContext.cs`:
  ```csharp
  public sealed class TypeNarrowingContext
  {
      private readonly Stack<Dictionary<string, SemanticType>> _scopeStack = new();

      public TypeNarrowingContext() => _scopeStack.Push(new());

      public IDisposable EnterScope();  // Pushes new dict, returns disposable that pops
      public void Narrow(string name, SemanticType type);  // Adds to current scope
      public SemanticType? GetNarrowedType(string name);  // Searches stack top-down
      public void ClearNarrowings();  // Clears current scope only
      public void ClearAllNarrowings();  // Clears entire stack
  }
  ```

- [ ] **1.2** Add unit tests in `src/Sharpy.Compiler.Tests/Semantic/TypeNarrowingContextTests.cs`:
  - Test basic narrow/get roundtrip
  - Test scope isolation (inner scope doesn't affect outer)
  - Test scope cleanup via Dispose
  - Test shadowing (inner scope overrides outer for same name)
  - Test `GetNarrowedType` returns null for unknown names

- [ ] **1.3** Replace `_narrowedTypes` in `TypeChecker.cs`:
  - Change field from `Dictionary<string, SemanticType>` to `TypeNarrowingContext`
  - Replace direct dictionary access with method calls
  - Wrap scope-introducing constructs (if/while/for/match/try) with `using (_narrowingContext.EnterScope())`

- [ ] **1.4** Update `TypeChecker.Statements.cs`:
  - Find all places that modify `_narrowedTypes` and use new API
  - Ensure `if` branches each get their own scope
  - Ensure `while`/`for` bodies get their own scope

- [ ] **1.5** Update `TypeChecker.Expressions.cs`:
  - Replace `_narrowedTypes.TryGetValue()` with `_narrowingContext.GetNarrowedType()`

- [ ] **1.6** Run full test suite and fix any regressions

### Verification

```bash
dotnet test --filter "FullyQualifiedName~TypeNarrowingContext"  # New tests pass
dotnet test  # All existing tests still pass
```

---

## 2. Symbol Reference Equality Wrappers

### Context & Rationale

`Symbol` is a record type with mutable properties (`Type`, `BaseType`, `CodeGenInfo`). Records use value equality by default, but symbols must use reference equality because:
- The same symbol's properties change across compilation phases
- Two symbols with the same name are not the same symbol

The codebase uses `ReferenceEqualityComparer.Instance` in many places, but this is easy to forget. A `HashSet<Symbol>` or `Dictionary<Symbol, T>` created without the comparer will silently fail lookups.

### Key Files

- `src/Sharpy.Compiler/Semantic/Symbol.cs` — Base symbol class (already has reference equality)
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` — Uses symbols in collections
- `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` — Uses ReferenceEqualityComparer

### Guiding Principles

- Wrapper classes should be drop-in replacements for standard collections
- Keep the wrappers minimal — only override constructors, not behavior
- Place in a `Collections` folder within Semantic
- Make the default constructor enforce reference equality

### Tasks

- [ ] **2.1** Create `src/Sharpy.Compiler/Semantic/Collections/SymbolSet.cs`:
  ```csharp
  public sealed class SymbolSet : HashSet<Symbol>
  {
      public SymbolSet() : base(ReferenceEqualityComparer.Instance) { }
      public SymbolSet(IEnumerable<Symbol> collection)
          : base(collection, ReferenceEqualityComparer.Instance) { }
      public SymbolSet(int capacity)
          : base(capacity, ReferenceEqualityComparer.Instance) { }
  }
  ```

- [ ] **2.2** Create `src/Sharpy.Compiler/Semantic/Collections/SymbolDictionary.cs`:
  ```csharp
  public sealed class SymbolDictionary<TValue> : Dictionary<Symbol, TValue>
  {
      public SymbolDictionary() : base(ReferenceEqualityComparer.Instance) { }
      public SymbolDictionary(int capacity)
          : base(capacity, ReferenceEqualityComparer.Instance) { }
  }
  ```

- [ ] **2.3** Add unit tests in `src/Sharpy.Compiler.Tests/Semantic/Collections/SymbolCollectionTests.cs`:
  - Test that two symbols with same name but different instances are distinct
  - Test that the same symbol instance is found after property mutation
  - Test basic add/contains/remove operations

- [ ] **2.4** Search codebase for `HashSet<Symbol>` and replace with `SymbolSet`:
  ```bash
  # Find usages (read-only search)
  grep -r "HashSet<Symbol>" src/Sharpy.Compiler/
  grep -r "HashSet<.*Symbol>" src/Sharpy.Compiler/
  ```

- [ ] **2.5** Search codebase for `Dictionary<Symbol,` and replace with `SymbolDictionary<T>`:
  ```bash
  grep -r "Dictionary<Symbol," src/Sharpy.Compiler/
  grep -r "Dictionary<.*Symbol," src/Sharpy.Compiler/
  ```

- [ ] **2.6** Run full test suite to verify no behavioral changes

### Verification

```bash
dotnet test --filter "FullyQualifiedName~SymbolCollection"  # New tests pass
dotnet test  # All existing tests still pass
```

---

## 3. Position-to-AST Node Service

### Context & Rationale

For LSP integration (hover, go-to-definition, completions), we need to find "which AST node is at line 5, column 10?" The infrastructure exists:
- All AST nodes have `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- `SourceText` has position conversion utilities
- `TextSpan` has `Contains()` method

But there's no service that combines these to answer position queries.

### Key Files

- `src/Sharpy.Compiler/Parser/Ast/Node.cs` — Base node with location properties
- `src/Sharpy.Compiler/Text/SourceText.cs` — Position conversion utilities
- `src/Sharpy.Compiler/Text/TextSpan.cs` — Span operations

### Guiding Principles

- Return the most specific (innermost) node at a position
- Provide options to get all nodes containing a position (for context)
- Use 1-based line/column (matching AST node conventions and editor conventions)
- Performance: O(n) traversal is acceptable for now; can optimize later if needed
- Return `null` rather than throwing for positions outside the AST

### Tasks

- [ ] **3.1** Create `src/Sharpy.Compiler/Services/AstPositionService.cs`:
  ```csharp
  public sealed class AstPositionService
  {
      /// <summary>
      /// Finds the innermost AST node containing the given position.
      /// </summary>
      /// <param name="module">The module to search</param>
      /// <param name="line">1-based line number</param>
      /// <param name="column">1-based column number</param>
      /// <returns>The innermost node, or null if position is outside all nodes</returns>
      public Node? FindInnermostNode(ModuleDeclaration module, int line, int column);

      /// <summary>
      /// Finds all AST nodes containing the given position, from outermost to innermost.
      /// </summary>
      public IReadOnlyList<Node> FindAllContainingNodes(ModuleDeclaration module, int line, int column);

      /// <summary>
      /// Finds the node of a specific type at the given position.
      /// </summary>
      public T? FindNodeOfType<T>(ModuleDeclaration module, int line, int column) where T : Node;
  }
  ```

- [ ] **3.2** Implement a recursive visitor pattern to traverse AST:
  - Start at `ModuleDeclaration`
  - Check if position is within node's span
  - Recursively check children
  - Track the path of containing nodes
  - Return deepest match

- [ ] **3.3** Add unit tests in `src/Sharpy.Compiler.Tests/Services/AstPositionServiceTests.cs`:
  - Test finding identifier in simple expression
  - Test finding function call target
  - Test position between nodes returns parent
  - Test position outside AST returns null
  - Test finding specific node type (e.g., `FindNodeOfType<Identifier>`)

- [ ] **3.4** Add integration test with real parsed code:
  ```csharp
  var source = "def foo():\n    x = 1 + 2\n    print(x)";
  // Parse, then query positions
  // Line 2, column 5 should find identifier "x"
  // Line 2, column 9 should find literal "1"
  ```

- [ ] **3.5** Document the service in code comments with usage examples

### Verification

```bash
dotnet test --filter "FullyQualifiedName~AstPositionService"  # New tests pass
```

---

## 4. Consolidated Name Resolution Service

### Context & Rationale

`RoslynEmitter` resolves variable names to C# identifiers through multiple paths:
1. `GetMangledVariableName()` — For local variables with versioning
2. `TryGetCSharpNameFromCodeGenInfo()` — For module-level symbols
3. `GetCSharpNameForSymbol()` — Fallback path
4. Direct `NameMangler` calls

This scattered logic makes it hard to understand the resolution order and debug naming issues.

### Key Files

- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` — Main emitter with resolution methods
- `src/Sharpy.Compiler/CodeGen/NameMangler.cs` — Snake_case to PascalCase conversion
- `src/Sharpy.Compiler/Semantic/CodeGenInfo.cs` — Pre-computed C# names

### Guiding Principles

- Create a single entry point for name resolution
- Make the resolution order explicit and documented
- Don't change the actual resolution logic, just consolidate it
- The service should be stateless where possible (pass context as parameters)
- Log/trace resolution decisions for debugging (optional, via ICompilerLogger)

### Tasks

- [ ] **4.1** Create `src/Sharpy.Compiler/CodeGen/NameResolutionService.cs`:
  ```csharp
  public sealed class NameResolutionService
  {
      private readonly NameMangler _mangler;
      private readonly ICompilerLogger? _logger;

      /// <summary>
      /// Resolves a symbol to its C# identifier name.
      /// Resolution order:
      /// 1. CodeGenInfo.CSharpName (precomputed during semantic analysis)
      /// 2. Local variable versioning (for redeclared locals: x, x_1, x_2)
      /// 3. NameMangler fallback (snake_case → PascalCase)
      /// </summary>
      public string ResolveName(
          Symbol symbol,
          IReadOnlyDictionary<string, int>? variableVersions = null,
          IReadOnlySet<string>? sourceVariableNames = null);

      /// <summary>
      /// Resolves a local variable name with versioning support.
      /// </summary>
      public string ResolveLocalName(
          string originalName,
          IReadOnlyDictionary<string, int> variableVersions,
          IReadOnlySet<string> sourceVariableNames);
  }
  ```

- [ ] **4.2** Add unit tests in `src/Sharpy.Compiler.Tests/CodeGen/NameResolutionServiceTests.cs`:
  - Test CodeGenInfo takes priority
  - Test variable versioning (x → x, x_1 when redeclared)
  - Test fallback to NameMangler
  - Test collision avoidance with source names

- [ ] **4.3** Refactor `RoslynEmitter` to use the new service:
  - Inject `NameResolutionService` in constructor
  - Replace `GetMangledVariableName()` calls with service calls
  - Replace `TryGetCSharpNameFromCodeGenInfo()` calls with service calls
  - Keep `_variableVersions` and `_sourceVariableNames` as context passed to service

- [ ] **4.4** Remove or deprecate the old methods in `RoslynEmitter`:
  - Mark as `[Obsolete]` initially, remove in follow-up if no external usage

- [ ] **4.5** Run CodeGen tests to verify behavior unchanged:
  ```bash
  dotnet test --filter "FullyQualifiedName~CodeGen"
  dotnet test --filter "FullyQualifiedName~FileBasedIntegration"
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~NameResolution"  # New tests pass
dotnet test --filter "FullyQualifiedName~RoslynEmitter"   # Existing tests pass
dotnet test  # Full suite
```

---

## 5. Phase Violation Exceptions

### Context & Rationale

The compiler has explicit phase boundaries enforced by `SemanticBinding.FreezeXxx()` methods. When a phase violation occurs (e.g., trying to set a symbol's type after type checking is frozen), it throws `InvalidOperationException`.

This is correct, but the generic exception makes debugging harder. A dedicated exception type would:
- Make phase violations immediately identifiable in stack traces
- Include context about which phase was expected vs actual
- Enable targeted catch blocks if needed

### Key Files

- `src/Sharpy.Compiler/Semantic/SemanticBinding.cs` — Contains freeze methods
- `src/Sharpy.Compiler/Diagnostics/CompilerPhase.cs` — Phase enum (if exists) or create

### Guiding Principles

- Exception should inherit from `InvalidOperationException` for compatibility
- Include both expected phase and current phase in message
- Include the symbol/node name that triggered the violation
- Keep it simple — this is for debugging, not flow control

### Tasks

- [ ] **5.1** Create `src/Sharpy.Compiler/Diagnostics/PhaseViolationException.cs`:
  ```csharp
  public sealed class PhaseViolationException : InvalidOperationException
  {
      public string Operation { get; }
      public string? SymbolName { get; }
      public string ExpectedPhase { get; }

      public PhaseViolationException(string operation, string expectedPhase, string? symbolName = null)
          : base(FormatMessage(operation, expectedPhase, symbolName))
      {
          Operation = operation;
          ExpectedPhase = expectedPhase;
          SymbolName = symbolName;
      }

      private static string FormatMessage(string operation, string expectedPhase, string? symbolName)
      {
          var target = symbolName != null ? $" for '{symbolName}'" : "";
          return $"Phase violation: Cannot {operation}{target} after {expectedPhase} phase is frozen.";
      }
  }
  ```

- [ ] **5.2** Add unit test in `src/Sharpy.Compiler.Tests/Diagnostics/PhaseViolationExceptionTests.cs`:
  - Test message formatting with symbol name
  - Test message formatting without symbol name
  - Test properties are set correctly

- [ ] **5.3** Update `SemanticBinding.cs` to throw `PhaseViolationException`:
  - Find all `throw new InvalidOperationException` for phase violations
  - Replace with `throw new PhaseViolationException(...)`
  - Include operation name (e.g., "set variable type", "set base type")

- [ ] **5.4** Update any tests that catch `InvalidOperationException` for phase violations:
  - Change to catch `PhaseViolationException` specifically

### Verification

```bash
dotnet test --filter "FullyQualifiedName~PhaseViolation"  # New tests pass
dotnet test --filter "FullyQualifiedName~SemanticBinding"  # Existing tests pass
dotnet test  # Full suite
```

---

## 6. Error Recovery Test Fixtures

### Context & Rationale

The compiler intentionally continues after many errors to provide comprehensive diagnostics. However, there are few tests verifying that error recovery works correctly, especially for:
- Import errors followed by type errors
- Missing base class with method calls
- Circular imports with additional errors

Without these tests, error recovery bugs go undetected.

### Key Files

- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/` — Error test fixtures
- `src/Sharpy.Compiler.Tests/Integration/FileBasedIntegrationTests.cs` — Test runner

### Guiding Principles

- Error tests use `.error` files with expected error substrings (one per line)
- Multi-file tests use subdirectories with `main.spy` entry point
- Each test should verify a specific error recovery scenario
- Focus on cascading errors that might mask the root cause

### Tasks

- [ ] **6.1** Create `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/error_recovery/` directory

- [ ] **6.2** Create `import_error_then_type_error/` test:
  ```
  # main.spy
  from nonexistent import foo

  x: int = "not an int"  # Should still report this error
  print(x)
  ```
  ```
  # main.error
  Cannot find module 'nonexistent'
  Cannot assign
  ```

- [ ] **6.3** Create `missing_base_class_methods/` test:
  ```
  # main.spy
  from nonexistent import BaseClass

  class Derived(BaseClass):
      def method(self) -> int:
          return self.base_method()  # Should report unknown method
  ```
  ```
  # main.error
  Cannot find module
  Unknown method 'base_method'
  ```

- [ ] **6.4** Create `circular_with_type_error/` test:
  ```
  # a.spy
  from b import B
  class A:
      def get_b(self) -> B:
          return B()
  x: str = 123  # Type error

  # b.spy
  from a import A
  class B:
      def get_a(self) -> A:
          return A()

  # main.spy
  from a import A
  print(A())
  ```
  ```
  # main.error
  Circular import
  Cannot assign
  ```

- [ ] **6.5** Create `multiple_undefined_in_expression/` test:
  ```
  # main.spy
  result = undefined1 + undefined2 * undefined3
  print(result)
  ```
  ```
  # main.error
  Undefined identifier 'undefined1'
  Undefined identifier 'undefined2'
  Undefined identifier 'undefined3'
  ```

- [ ] **6.6** Create `type_error_in_function_body/` test:
  ```
  # main.spy
  def broken() -> int:
      x: int = "string"
      return x

  def caller() -> None:
      y = broken()  # Should still infer return type
      print(y)

  caller()
  ```
  ```
  # main.error
  Cannot assign
  ```

- [ ] **6.7** Run the new tests and verify they pass:
  ```bash
  dotnet test --filter "FullyQualifiedName~FileBasedIntegration"
  ```

### Verification

```bash
dotnet test --filter "DisplayName~error_recovery"  # New fixtures discovered and run
dotnet test --filter "FullyQualifiedName~FileBasedIntegration"  # All integration tests pass
```

---

## 7. Sealed SemanticType Hierarchy

### Context & Rationale

The `SemanticType` hierarchy has 14 subclasses. When pattern matching on types, it's easy to miss cases:

```csharp
// If a new type is added, this silently falls through
return type switch {
    IntType => "int",
    StrType => "str",
    _ => "unknown"  // Catches new types silently
};
```

Sealing all leaf types enables the compiler to warn about non-exhaustive switches (with appropriate analyzers) and documents that the hierarchy is closed.

### Key Files

- `src/Sharpy.Compiler/Semantic/SemanticType.cs` — Type hierarchy

### Guiding Principles

- Seal all leaf types (types with no further subclasses)
- Don't seal abstract base types that are meant for extension
- This is a safe change — sealing only restricts future inheritance
- Document why types are sealed (closed hierarchy for exhaustive matching)

### Tasks

- [ ] **7.1** Audit `SemanticType.cs` and identify all leaf types:
  - Read through the file and list all types
  - Identify which are abstract (keep unsealed) vs concrete (seal)

- [ ] **7.2** Add `sealed` modifier to leaf types. Expected list:
  ```csharp
  public sealed record IntType : BuiltinType { ... }
  public sealed record LongType : BuiltinType { ... }
  public sealed record FloatType : BuiltinType { ... }
  public sealed record DoubleType : BuiltinType { ... }
  public sealed record Float32Type : BuiltinType { ... }
  public sealed record BoolType : BuiltinType { ... }
  public sealed record StrType : BuiltinType { ... }
  public sealed record GenericType : SemanticType { ... }  // if no subclasses
  public sealed record UserDefinedType : SemanticType { ... }
  public sealed record NullableType : SemanticType { ... }
  public sealed record OptionalType : SemanticType { ... }
  public sealed record FunctionType : SemanticType { ... }
  public sealed record GenericFunctionType : SemanticType { ... }
  public sealed record TupleType : SemanticType { ... }
  public sealed record ModuleType : SemanticType { ... }
  public sealed record TypeParameterType : SemanticType { ... }
  public sealed record ResultType : SemanticType { ... }
  public sealed record UnionType : SemanticType { ... }
  public sealed record TaskType : SemanticType { ... }
  public sealed record VoidType : SemanticType { ... }
  public sealed record UnknownType : SemanticType { ... }
  ```

- [ ] **7.3** Add XML doc comment to `SemanticType` explaining the closed hierarchy:
  ```csharp
  /// <summary>
  /// Base type for all semantic types in the Sharpy type system.
  /// This hierarchy is closed — all leaf types are sealed to enable
  /// exhaustive pattern matching.
  /// </summary>
  ```

- [ ] **7.4** Run full test suite to verify no code was depending on inheritance:
  ```bash
  dotnet build
  dotnet test
  ```

### Verification

```bash
dotnet build  # No compilation errors
dotnet test   # All tests pass
```

---

## 8. Granular Compilation Metrics

### Context & Rationale

`CompilationMetrics` exists but lacks per-phase and per-validator timing. This makes it hard to identify performance bottlenecks when compilation is slow.

### Key Files

- `src/Sharpy.Compiler/Diagnostics/CompilationMetrics.cs` — Existing metrics class
- `src/Sharpy.Compiler/Compiler.cs` — Main compilation driver
- `src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.cs` — Validator execution

### Guiding Principles

- Metrics should be opt-in (don't slow down normal compilation)
- Use `Stopwatch` for timing (not `DateTime`)
- Metrics should be immutable after compilation completes
- Expose via properties, not methods (for serialization compatibility)

### Tasks

- [ ] **8.1** Extend `CompilationMetrics.cs` with new properties:
  ```csharp
  public sealed record CompilationMetrics
  {
      // Existing properties...
      public TimeSpan TotalTime { get; init; }

      // New per-phase timings
      public TimeSpan LexerTime { get; init; }
      public TimeSpan ParserTime { get; init; }
      public TimeSpan NameResolutionTime { get; init; }
      public TimeSpan ImportResolutionTime { get; init; }
      public TimeSpan TypeCheckingTime { get; init; }
      public TimeSpan ValidationTime { get; init; }
      public TimeSpan CodeGenTime { get; init; }

      // New per-validator timings
      public IReadOnlyDictionary<string, TimeSpan> ValidatorTimes { get; init; }

      // New counts for context
      public int TokenCount { get; init; }
      public int AstNodeCount { get; init; }
      public int SymbolCount { get; init; }
      public int DiagnosticCount { get; init; }
  }
  ```

- [ ] **8.2** Create `src/Sharpy.Compiler/Diagnostics/CompilationMetricsBuilder.cs`:
  ```csharp
  public sealed class CompilationMetricsBuilder
  {
      private readonly Stopwatch _total = new();
      private readonly Stopwatch _current = new();
      private readonly Dictionary<string, TimeSpan> _phaseTimes = new();
      private readonly Dictionary<string, TimeSpan> _validatorTimes = new();

      public void Start() => _total.Start();
      public void StartPhase(string phase);
      public void EndPhase(string phase);
      public void RecordValidatorTime(string validatorName, TimeSpan time);
      public CompilationMetrics Build(int tokenCount, int nodeCount, int symbolCount, int diagCount);
  }
  ```

- [ ] **8.3** Update `Compiler.cs` to track phase timings:
  - Create `CompilationMetricsBuilder` at start
  - Call `StartPhase`/`EndPhase` around each phase
  - Pass builder to `ValidationPipeline` for validator timing
  - Call `Build()` at end and include in `CompilationResult`

- [ ] **8.4** Update `ValidationPipeline.cs` to track per-validator timing:
  - Accept optional metrics builder
  - Time each validator's `Validate()` call
  - Report via `RecordValidatorTime()`

- [ ] **8.5** Add unit tests for metrics builder:
  - Test phase timing recording
  - Test validator timing recording
  - Test Build() produces correct totals

- [ ] **8.6** Add integration test verifying metrics are populated:
  ```csharp
  var result = CompileAndExecute("print(1)");
  Assert.True(result.Metrics.LexerTime > TimeSpan.Zero);
  Assert.True(result.Metrics.ParserTime > TimeSpan.Zero);
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~CompilationMetrics"  # New tests pass
dotnet test  # Full suite
```

---

## 9. Exception Context Preservation

### Context & Rationale

Two locations in the compiler catch exceptions and lose debugging context:

1. **`Compiler.cs:467`** — Catch-all handler loses stack trace:
   ```csharp
   catch (Exception ex)
   {
       _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
       diagnostics.AddError($"Compilation failed: {ex.Message}", ...);
   }
   ```

2. **`ValidationPipeline.cs:94-101`** — Validator exceptions swallowed:
   ```csharp
   catch (Exception ex)
   {
       _logger.LogError($"Validator {validator.Name} threw an exception: {ex.Message}");
       context.Diagnostics.AddError($"Internal compiler error in {validator.Name}: {ex.Message}", ...);
   }
   ```

When unexpected bugs occur, developers lose the exception type, stack trace, and inner exception chain—making debugging significantly harder.

### Key Files

- `src/Sharpy.Compiler/Compiler.cs` — Main compilation driver (~line 467)
- `src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.cs` — Validator execution (~line 94)
- `src/Sharpy.Compiler/Diagnostics/` — Exception types

### Guiding Principles

- Log full `ex.ToString()` including stack trace, not just `ex.Message`
- Include `ex.GetType().Name` in diagnostic messages for identification
- Consider creating `InternalCompilerErrorException` for typed catch blocks
- Don't change exception behavior for user-facing errors—only internal compiler bugs

### Tasks

- [ ] **9.1** Create `src/Sharpy.Compiler/Diagnostics/InternalCompilerErrorException.cs`:
  ```csharp
  public sealed class InternalCompilerErrorException : Exception
  {
      public string Component { get; }

      public InternalCompilerErrorException(string component, string message, Exception innerException)
          : base($"Internal compiler error in {component}: {message}", innerException)
      {
          Component = component;
      }
  }
  ```

- [ ] **9.2** Update `Compiler.cs` catch block (~line 467):
  - Log `ex.ToString()` (full stack trace) instead of just `ex.Message`
  - Include `ex.GetType().Name` in the diagnostic message
  - If `ex` is `InternalCompilerErrorException`, extract component info

- [ ] **9.3** Update `ValidationPipeline.cs` catch block (~line 94):
  - Log full exception with `_logger.LogError($"...: {ex}")`
  - Include exception type in diagnostic: `$"Internal error ({ex.GetType().Name}) in {validator.Name}"`
  - Consider wrapping in `InternalCompilerErrorException` for consistent handling

- [ ] **9.4** Add test in `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`:
  - Create a mock validator that throws `NullReferenceException`
  - Verify diagnostic message includes exception type name
  - Verify compilation continues (doesn't crash)

### Verification

```bash
dotnet test --filter "FullyQualifiedName~ValidationPipeline"  # New tests pass
dotnet test  # Full suite
```

---

## 10. Import Error Recovery

### Context & Rationale

When imports fail, the compiler continues type checking (`Compiler.cs:273-279`), which produces cascading errors that mask the root cause:

```
# User sees:
Error: Cannot find module 'math'
Error: Undefined function 'sqrt'      <- Noise, caused by import failure
Error: Undefined function 'floor'     <- Noise
Error: Cannot call undefined 'sqrt'   <- Noise
```

The user gets 4 errors when there's really only 1 problem. This makes error output harder to parse.

### Key Files

- `src/Sharpy.Compiler/Compiler.cs` — Import/type checking orchestration
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs` — Import resolution
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` — Symbol storage

### Guiding Principles

- When imports fail, inject "error recovery" placeholder symbols
- Mark placeholders distinctly (e.g., `IsErrorRecovery` property on Symbol)
- Filter or annotate downstream errors caused by error recovery symbols
- Don't change successful compilation behavior at all

### Tasks

- [ ] **10.1** Add `IsErrorRecovery` property to `Symbol.cs`:
  ```csharp
  public abstract record Symbol
  {
      // ... existing properties
      public bool IsErrorRecovery { get; init; } = false;
  }
  ```

- [ ] **10.2** Create error recovery symbol factory in `ImportResolver.cs`:
  ```csharp
  private ModuleSymbol CreateErrorRecoveryModule(string moduleName)
  {
      return new ModuleSymbol(moduleName, "<error-recovery>")
      {
          IsErrorRecovery = true
      };
  }
  ```

- [ ] **10.3** Update `ImportResolver.Resolve()` to inject placeholders:
  - When `from X import Y` fails to find module X
  - Register an error recovery `ModuleSymbol` for X
  - This prevents "undefined identifier" cascading errors

- [ ] **10.4** Update `TypeChecker` to detect error recovery symbols:
  - When resolving a symbol, check `IsErrorRecovery`
  - If true, skip adding new errors (the import error was already reported)
  - Optionally add note: "This error may be caused by failed import of 'X'"

- [ ] **10.5** Add tests in `src/Sharpy.Compiler.Tests/Semantic/ImportErrorRecoveryTests.cs`:
  - Test: import error + usage → only 1 error (import), not cascading
  - Test: successful import → no behavior change
  - Test: error recovery symbols don't leak into codegen

### Verification

```bash
dotnet test --filter "FullyQualifiedName~ImportErrorRecovery"  # New tests pass
dotnet test --filter "FullyQualifiedName~FileBasedIntegration"  # Error fixtures still work
dotnet test  # Full suite
```

---

## 11. Comprehension Code Consolidation

### Context & Rationale

List, set, and dict comprehension handling has near-identical code in 6 locations:

**Type Checking** (`TypeChecker.Expressions.cs`):
- `CheckListComprehension()` (~line 1480)
- `CheckSetComprehension()` (~line 1547)
- `CheckDictComprehension()` (~line 1620)

**Code Generation** (`RoslynEmitter.Expressions.cs`):
- `GenerateListComprehension()` (~line 620)
- `GenerateSetComprehension()` (~line 690)
- `GenerateDictComprehension()` (~line 748)

The logic for iterating clauses, checking `ForClause`/`IfClause`, validating types, and handling loop variables is ~95% identical. Bug fixes must be replicated 3+ times.

### Key Files

- `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

### Guiding Principles

- Extract common traversal logic into shared helpers
- Use generic type parameters or delegates for element-type-specific behavior
- Preserve exact existing behavior—this is pure refactoring
- Keep the public method signatures unchanged

### Tasks

- [ ] **11.1** Create `CheckComprehensionCore()` helper in `TypeChecker.Expressions.cs`:
  ```csharp
  private SemanticType CheckComprehensionCore(
      IReadOnlyList<ComprehensionClause> clauses,
      Func<SemanticType> checkElement,
      Func<SemanticType, SemanticType> wrapResult)
  {
      // Common clause iteration, scope management, type checking
      // Call checkElement() for the element expression
      // Call wrapResult() to wrap in list/set/dict generic type
  }
  ```

- [ ] **11.2** Refactor `CheckListComprehension` to use helper:
  ```csharp
  private SemanticType CheckListComprehension(ListComprehension comp)
  {
      return CheckComprehensionCore(
          comp.Clauses,
          () => CheckExpression(comp.Element),
          elementType => new GenericType("list", elementType));
  }
  ```

- [ ] **11.3** Refactor `CheckSetComprehension` and `CheckDictComprehension` similarly

- [ ] **11.4** Create `GenerateComprehensionCore()` helper in `RoslynEmitter.Expressions.cs`:
  ```csharp
  private ExpressionSyntax GenerateComprehensionCore(
      IReadOnlyList<ComprehensionClause> clauses,
      Func<ExpressionSyntax> generateElement,
      string collectionType,
      string addMethod)
  {
      // Common LINQ generation, clause handling
  }
  ```

- [ ] **11.5** Refactor `GenerateListComprehension`, `GenerateSetComprehension`, `GenerateDictComprehension`

- [ ] **11.6** Run existing comprehension tests to verify no behavioral changes:
  ```bash
  dotnet test --filter "DisplayName~comprehension"
  dotnet test --filter "FullyQualifiedName~FileBasedIntegration"
  ```

### Verification

```bash
dotnet test --filter "DisplayName~comprehension"  # Existing tests pass
dotnet test  # Full suite
```

---

## Implementation Order

Complete sections in this order for maximum impact:

| Priority | Section | Rationale |
|----------|---------|-----------|
| 1 | **§1 Type Narrowing Context** | Highest bug risk in current code |
| 2 | **§9 Exception Context Preservation** | Immediate debugging benefit |
| 3 | **§2 Symbol Reference Equality Wrappers** | Eliminates silent failures |
| 4 | **§10 Import Error Recovery** | Reduces cascading error noise |
| 5 | **§5 Phase Violation Exceptions** | Improves phase debugging |
| 6 | **§3 Position-to-AST Node Service** | LSP prerequisite |
| 7 | **§6 Error Recovery Test Fixtures** | Catches regressions |
| 8 | **§4 Consolidated Name Resolution** | Reduces codegen debugging |
| 9 | **§11 Comprehension Consolidation** | Reduces maintenance burden |
| 10 | **§7 Sealed SemanticType Hierarchy** | Compile-time safety |
| 11 | **§8 Granular Compilation Metrics** | Performance visibility |

---

## Final Verification

After completing all sections:

```bash
# Full build
dotnet build sharpy.sln

# Full test suite
dotnet test

# Format check
dotnet format whitespace --verify-no-changes

# Run a sample program
dotnet run --project src/Sharpy.Cli -- run snippets/hello.spy
```

All tests should pass and the sample program should execute correctly.
