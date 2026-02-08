# Compiler Robustness & Tooling Readiness Improvements

## Context

An architectural assessment of the Sharpy compiler identified 8 areas where targeted improvements would have outsized impact on robustness, debuggability, usability, and future tooling readiness (LSP, parallel builds). The compiler is already in good shape — the diagnostic infrastructure (`DiagnosticRenderer`, `DiagnosticBag`), invariant checking (`CompilerInvariants`, `DualWriteAssertions`), and service architecture (`CompilerServices`) are well-designed. These improvements build on that foundation.

### Guiding Principles

- **Bugs should be obvious** — not discovered months later
- **Pave the road for LSP/tooling** — but don't build the tooling itself
- **Minimal tech debt** — each change should simplify future work, not add complexity
- **Always-on correctness checks** — prefer runtime invariant checks over DEBUG-only assertions

### Current State Summary

| Area | Status |
|------|--------|
| DiagnosticRenderer | Fully implemented (ANSI colors, source context, span underlines) |
| TextSpan on diagnostics | ~78% of AddError calls provide spans; parser and ImportResolver use none |
| CancellationToken | 6 files have it; Lexer, TypeResolver, ImportResolver, ModuleLoader, RoslynEmitter do not |
| Fuzz testing | Custom fuzzer (~1,500 iterations), no property-based framework |
| CompilerInvariants | Phase boundary checks exist (always-on, emit SPY0904 warnings) |
| AstPositionService | Fully implemented (LSP-style position queries) |
| .expected.cs snapshots | ~15 representative codegen snapshots exist |
| Integration test fixtures | 368 .spy files across 30 categories |

---

## Changes Overview

1. **TextSpan coverage** — Ensure all diagnostics carry TextSpan data so DiagnosticRenderer can show source context with underlines
2. **Fuzz testing expansion** — Add property-based correctness tests and semantic fuzzing
3. **CancellationToken propagation** — Thread cancellation through all pipeline stages
4. **Compiler API surface** — Create a clean programmatic API for tooling consumers
5. **Unknown type invariants** — Distinguish "type inference produced Unknown" (bug) from "error recovery used Unknown" (expected)
6. **Error message location tests** — Test that diagnostics point to the right source locations
7. **Codegen snapshot expansion** — Broaden .expected.cs coverage for regression detection
8. **Structured logging consistency** — Audit and standardize logging across pipeline stages

---

## Phase 1: TextSpan Coverage for Rich Error Messages

> **Goal**: Every diagnostic that can possibly carry a source span should carry one. This makes DiagnosticRenderer show underlined source context (the `^^^^^^^` markers) for all errors, not just some. This is the single most impactful usability improvement.

### Rationale

The `DiagnosticRenderer` already renders beautiful Rust-style error messages:
```
error[SPY0201]: Type 'str' is not assignable to 'int'
  --> file.spy:3:5
   |
 3 |     x: int = "hello"
   |              ^^^^^^^ expected 'int', found 'str'
   |
```

But this only works when diagnostics carry `TextSpan` data. Currently, ~78% of `AddError` calls provide spans — the TypeChecker, NameResolver, and validators have high span coverage. The remaining ~22% that lack spans are concentrated in the **Parser** (which uses `ReportError()` without spans) and **ImportResolver** (whose `AddError` helper doesn't accept a span parameter). Filling these gaps would make rich error rendering universal.

### 1a. Audit all AddError/AddWarning call sites in TypeChecker

The TypeChecker has the most diagnostics (~119 AddError calls across 5 partial files). Most use a private `AddError()` helper that accepts `TextSpan? span = null`, and callers overwhelmingly provide spans already (~95% of calls pass `span:`). The remaining few calls that omit spans should be audited and fixed.

**Files to modify**:
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs`

**Checklist**:

- [x] Read the private `AddError()` helper in `TypeChecker.Utilities.cs` to understand how spans flow to `DiagnosticBag`
- [x] Search for the ~5% of `AddError(` calls across the 5 TypeChecker partial files that do NOT pass a `span:` argument
- [x] For each call, determine if the surrounding code has access to an AST node with span information (most do — they have the `Node` being type-checked)
- [x] Add `span: node.Span` (or equivalent) to each call where a node is available
- [x] Verify that `Node.Span` returns a valid `TextSpan` (check the `ILocatable` interface on `Node`)
- [x] Run the existing test suite to confirm no regressions: `dotnet test --filter "FullyQualifiedName~Semantic"`

> **Fork-in-the-road**: Some AddError calls report errors about *relationships* between two nodes (e.g., "type X is not assignable to type Y"). Which node's span should be used? **Decision**: Use the *target* node's span (the node receiving the assignment), since that's where the user needs to fix the code. Document this convention in a code comment near the AddError helper.

### 1b. Audit all AddError/AddWarning call sites in Validators

The validation pipeline (~37 AddError/AddWarning calls across ~13 validator files) has good span coverage already, but should be audited to ensure all calls provide spans.

**Files to modify**: All files in `src/Sharpy.Compiler/Semantic/Validation/`

**Checklist**:

- [x] Search for all `AddError(` and `AddWarning(` calls in `src/Sharpy.Compiler/Semantic/Validation/` that do NOT pass a `span:` argument
- [x] For each call, add `span: node.Span` using the AST node available in context
- [x] Pay special attention to `ControlFlowValidator` — it works with `BasicBlock` which may not directly expose spans; you may need to use the block's first statement's span
- [x] Run validator tests: `dotnet test --filter "FullyQualifiedName~Validation"`

### 1c. Add TextSpan to Parser diagnostics

The Parser currently uses only line/column for all its diagnostic calls. It reports errors through a `ReportError(message, line, column, code)` method that calls `_diagnostics.AddError()` without a span. There are ~26 `ReportError()` call sites across the 6 partial files. Since tokens have position information during parsing, the Parser should propagate spans.

**Files to modify**:
- `src/Sharpy.Compiler/Parser/Parser.cs` (main file — contains `ReportError()` helper)
- `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`
- `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`
- `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`
- `src/Sharpy.Compiler/Parser/Parser.Statements.cs`
- `src/Sharpy.Compiler/Parser/Parser.Types.cs`

**Checklist**:

- [x] Modify `ReportError()` in `Parser.cs` to accept an optional `TextSpan?` parameter and pass it through to `_diagnostics.AddError()`
- [x] Search for all `ReportError(` calls across all 6 Parser partial files
- [x] Determine if the parser has access to token positions that can form a `TextSpan` at each call site (tokens have line/column but may need conversion to spans)
- [x] For error recovery sites where a span is naturally available (e.g., unexpected token), use the token's span
- [x] For sites where only line/column are available, leave as-is (this is acceptable for parser errors where the exact span is ambiguous)
- [x] Run parser tests: `dotnet test --filter "FullyQualifiedName~Parser"`

### 1d. Add TextSpan to NameResolver and ImportResolver diagnostics

**Files to modify**:
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

**Checklist**:

- [x] Search for all `AddError(` calls in NameResolver that do NOT pass spans (~2 of 20 calls lack spans; the helper already accepts `TextSpan? span = null`)
- [x] Add spans using the AST nodes being resolved (declarations, identifiers)
- [x] **ImportResolver prerequisite**: The ImportResolver's private `AddError` helper has signature `AddError(string message, int? line, int? column, string? code = null)` — it does **not** accept a `TextSpan` parameter. First modify this helper to accept `Text.TextSpan? span = null` and pass it through to `_diagnostics.AddError()`
- [x] Search for all `AddError(` calls in ImportResolver (2 call sites at lines 368 and 376) and add spans
- [x] For import errors, use the ImportStatement/FromImportStatement node's span
- [x] Run tests: `dotnet test --filter "FullyQualifiedName~NameResol" && dotnet test --filter "FullyQualifiedName~Import"`

### 1e. Verify end-to-end rendering

**Checklist**:

- [x] Pick 5 existing `.error` test fixtures that test different error categories (type error, undefined name, syntax error, access violation, control flow)
- [x] For each, manually run `dotnet run --project src/Sharpy.Cli -- run <fixture.spy>` and verify the error output shows `^^^^^^^` underlines (not just `^`)
- [x] If any fixture shows only `^` (single caret) where a span should be shown, investigate which AddError call is missing the span
- [x] Add 2-3 new `.error` test fixtures specifically designed to verify rich error rendering covers multiple error categories

---

## Phase 2: Fuzz Testing Expansion

> **Goal**: Expand the custom fuzzer to test semantic correctness properties (not just crash prevention) and add coverage for the full pipeline.

### Rationale

The existing fuzzer (`FuzzTests.cs`, `SharpyFuzzer.cs`) is well-built but focuses exclusively on "the compiler should not crash." It doesn't verify any semantic properties. Adding property-based tests like "parsing then pretty-printing should round-trip" or "well-typed programs should compile without errors" catches entire categories of bugs that unit tests miss.

### 2a. Add round-trip property tests for the Lexer

**File to create**: `src/Sharpy.Compiler.Tests/Fuzz/LexerPropertyTests.cs`

**Checklist**:

- [x] Create a new test class `LexerPropertyTests`
- [x] **Property: Token positions are monotonically increasing and non-overlapping** — This catches off-by-one bugs in the lexer.
  - Generate random valid-looking source strings using `SharpyFuzzer`
  - Tokenize with the Lexer
  - Verify each token's start position >= previous token's end position
  - Note: The Lexer uses `SkipWhitespace()` and synthetic tokens (Indent/Dedent with empty text, EOF), so tokens do NOT cover the entire source — gaps between tokens represent consumed whitespace and synthetic boundaries. Do not assert total token length equals source length.
- [x] **Property: Non-synthetic token text matches source** — For tokens that have non-empty text (excluding Indent, Dedent, EOF, Newline), the token's text should appear at the corresponding source position
- [x] **Property: No token text extends beyond source bounds** — Every token's reported position + length should be within the source string boundaries
- [x] Use at least 5 seeds with 100 iterations each
- [x] Run: `dotnet test --filter "FullyQualifiedName~LexerProperty"`

### 2b. Add semantic correctness property tests

**File to create**: `src/Sharpy.Compiler.Tests/Fuzz/SemanticPropertyTests.cs`

**Checklist**:

- [x] Create a new test class `SemanticPropertyTests`
- [x] **Property: Well-formed programs compile without internal errors** — Programs generated by `SharpyFuzzer.GenerateValidLooking()` should produce only user-facing errors (SPY codes), never `InternalCompilerErrorException`
  - Compile each generated program
  - If compilation fails, verify all diagnostics have proper SPY codes
  - Assert no `InternalCompilerErrorException` was thrown
- [x] **Property: Error count is deterministic** — Compiling the same source twice should produce the same number and codes of diagnostics
  - Generate a program, compile it twice
  - Assert identical diagnostic counts and codes
- [x] **Property: Adding a trailing newline doesn't change semantics** — Source with/without trailing newline should produce the same diagnostics
- [x] Use at least 5 seeds with 50 iterations each
- [x] Run: `dotnet test --filter "FullyQualifiedName~SemanticProperty"`

### 2c. Enhance the fuzzer to generate more diverse programs

**File to modify**: `src/Sharpy.Compiler.Tests/Fuzz/SharpyFuzzer.cs`

**Checklist**:

- [x] Add a `GenerateClassHierarchy()` method that generates classes with inheritance, interfaces, and method overrides — this exercises the inheritance resolution and type checking paths
- [x] Add a `GenerateGenericUsage()` method that generates generic type usage (`list[int]`, `dict[str, int]`, custom generic classes) — this exercises `GenericTypeInferenceService`
- [x] Add a `GenerateImportGraph()` method that generates multi-file import scenarios — this exercises `ImportResolver` and `ModuleLoader`
- [x] Add a `GenerateTypeAnnotations()` method that generates functions and variables with type annotations (including optional types `T?`, tuple types, function types) — this exercises `TypeResolver`
- [x] Integrate new generators into existing fuzz tests or create new test methods
- [x] Run full fuzz suite: `dotnet test --filter "FullyQualifiedName~Fuzz"`

### 2d. Add codegen round-trip fuzz test

**File to create**: `src/Sharpy.Compiler.Tests/Fuzz/CodeGenPropertyTests.cs`

**Checklist**:

- [x] Create a new test class `CodeGenPropertyTests`
- [x] **Property: Generated C# is valid** — Any Sharpy program that compiles without errors should produce C# that parses without Roslyn syntax errors
  - This is partially covered by `CompilerInvariants.AssertPostCodeGen()` but that's a warning, not a test assertion
  - Generate valid-looking programs, compile them, if no Sharpy errors, parse the generated C# with `CSharpSyntaxTree.ParseText()` and assert no diagnostics
- [x] **Property: Generated C# compiles** — Take it further: the generated C# should also compile to IL without errors (use `CSharpCompilation.Create()`)
- [x] Use at least 3 seeds with 25 iterations each (codegen is slower)
- [x] Run: `dotnet test --filter "FullyQualifiedName~CodeGenProperty"`

---

## Phase 3: CancellationToken Propagation

> **Goal**: Thread `CancellationToken` through all pipeline stages so that an LSP server (or any tooling host) can cancel long-running compilations without waiting for them to finish.

### Rationale

Currently, CancellationToken is accepted by 6 files: `Compiler.cs`, `ProjectCompiler.cs`, `Parser`, `NameResolver`, `TypeChecker`, and `ValidationPipeline` — but is missing from the Lexer, TypeResolver, ImportResolver, ModuleLoader, and all 8 RoslynEmitter partial files. An LSP server needs to cancel compilations when the user types (every keystroke can trigger a new compilation). Without cancellation, stale compilations waste CPU and delay results.

The pattern is mechanical: accept `CancellationToken` as a parameter, call `cancellationToken.ThrowIfCancellationRequested()` at loop boundaries (not inside tight inner loops — just at statement/declaration granularity).

### 3a. Add CancellationToken to Lexer

**File to modify**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

**Checklist**:

- [x] Add `CancellationToken cancellationToken = default` parameter to the Lexer constructor (or the main `Tokenize()` / `NextToken()` method)
- [x] Store it as a `private readonly CancellationToken _cancellationToken` field
- [x] Add `_cancellationToken.ThrowIfCancellationRequested()` at the top of the main token loop (the loop that produces each token) — NOT inside character-level scanning
- [x] Update `Compiler.cs` to pass its CancellationToken to the Lexer
- [x] Run: `dotnet test --filter "FullyQualifiedName~Lexer"`

> **Gotcha**: The Lexer is typically fast enough that cancellation isn't critical for performance, but it's important for correctness — if the Lexer hangs on pathological input (deeply nested strings, huge files), cancellation provides an escape hatch.

### 3b. Add CancellationToken to TypeResolver

**File to modify**: `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

**Checklist**:

- [x] Add `CancellationToken cancellationToken = default` parameter to `ResolveTypes()` (the main entry point)
- [x] Add `cancellationToken.ThrowIfCancellationRequested()` at the top of the loop that iterates over declarations
- [x] Update the caller in `Compiler.cs` to pass the token
- [x] Run: `dotnet test --filter "FullyQualifiedName~TypeResol"`

### 3c. Add CancellationToken to ImportResolver and ModuleLoader

**Files to modify**:
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- `src/Sharpy.Compiler/Semantic/ModuleLoader.cs`

**Checklist**:

- [x] Add `CancellationToken cancellationToken = default` parameter to `ImportResolver.ResolveAllImports()` (the main entry point)
- [x] Add `cancellationToken.ThrowIfCancellationRequested()` before each module load (import resolution can trigger parsing of other files)
- [x] Add `CancellationToken cancellationToken = default` parameter to `ModuleLoader.LoadModule()` (or equivalent)
- [x] Pass the token through to the Parser when ModuleLoader parses imported files
- [x] Update callers in `Compiler.cs`
- [x] Run: `dotnet test --filter "FullyQualifiedName~Import"`

### 3d. Add CancellationToken to RoslynEmitter

**Files to modify** (all 8 partial files):
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`

**Checklist**:

- [x] Add `CancellationToken _cancellationToken` field to the main `RoslynEmitter.cs` partial class
- [x] Accept `CancellationToken cancellationToken = default` in the constructor or `Emit()` entry point
- [x] Add `_cancellationToken.ThrowIfCancellationRequested()` at statement-level granularity:
  - Top of `EmitStatement()` (or equivalent top-level statement dispatch)
  - Top of `EmitClassDeclaration()` / `EmitFunctionDeclaration()` (or equivalent declaration dispatch)
  - NOT inside expression emission (too fine-grained, would hurt performance)
- [x] Update `Compiler.cs` to pass the token to RoslynEmitter
- [x] Run: `dotnet test --filter "FullyQualifiedName~CodeGen" && dotnet test --filter "FullyQualifiedName~RoslynEmitter"`

> **Gotcha**: RoslynEmitter is ~6,225 lines across 8 files. Don't try to add cancellation checks to every method — only add them at the top-level dispatch points where the emitter loops over statements or declarations. The goal is O(n) cancellation granularity where n is the number of top-level statements/declarations, not O(n) where n is the number of AST nodes.

### 3e. Add cancellation test

**File to create**: `src/Sharpy.Compiler.Tests/CancellationTests.cs`

**Checklist**:

- [x] Create a test that compiles a moderately large program with an already-cancelled `CancellationToken` and asserts that `OperationCanceledException` is thrown
- [x] Create a test that compiles with a `CancellationTokenSource` that cancels after a short delay and asserts the compilation terminates promptly (within 2x the delay)
- [x] Run: `dotnet test --filter "FullyQualifiedName~Cancellation"`

---

## Phase 4: Compiler API Surface for Tooling

> **Goal**: Create a clean, documented entry point for programmatic compilation that tooling (LSP, IDE plugins, build systems) can use without depending on CLI internals.

### Rationale

Currently, the CLI (`Program.cs`) directly orchestrates compilation, rendering, and output. An LSP server or IDE plugin would need to duplicate this orchestration. A clean `CompilerApi` class provides a stable interface that hides internal pipeline details and provides structured results (not console output).

The building blocks already exist: `Compiler.cs` handles compilation, `CompilerServices` provides the service container, `AstPositionService` provides position queries, and `DiagnosticRenderer` handles formatting. This phase wires them together with a clean API.

### 4a. Create CompilerApi entry point

**File to create**: `src/Sharpy.Compiler/CompilerApi.cs`

**Checklist**:

- [x] Create `public sealed class CompilerApi`
- [x] Add a `CompileResult Compile(string source, CompilerOptions? options = null, string? filePath = null, CancellationToken cancellationToken = default)` method that:
  - Creates a `SourceText` from the source string
  - Runs the full pipeline (Lexer → Parser → Semantic → CodeGen)
  - Returns a `CompileResult` (see below)
- [x] Add a `CompileResult CompileFile(string filePath, CompilerOptions? options = null, CancellationToken cancellationToken = default)` method
- [x] Add a `ParseResult Parse(string source, CancellationToken cancellationToken = default)` method that runs only Lexer → Parser and returns the AST + diagnostics (useful for tooling that only needs syntax information)
- [x] Add a `SemanticResult Analyze(string source, CancellationToken cancellationToken = default)` method that runs Lexer → Parser → Semantic (no codegen) and returns the AST + SemanticInfo + diagnostics (useful for LSP hover/completion)
- [x] Ensure the class is easy to instantiate (no complex setup required; use sensible defaults)

### 4b. Create result types

**File to create**: `src/Sharpy.Compiler/CompilerApiTypes.cs`

**Checklist**:

- [x] Reuse the existing `CompilerOptions` class (defined in `Compiler.cs`, includes `WarningsAsErrors`, `SuppressedWarnings`, `MaxErrors`, `ModulePaths`, `References`, `Incremental`, `OutputType`). Do **not** create a new options record — `CompilerOptions` already exists and is the canonical configuration type. If additional API-specific options are needed (e.g., `RootNamespace`, `FilePath`), either extend the existing class or accept them as method parameters.
- [x] Create `public record CompileResult`:
  - `bool Success` (no errors)
  - `IReadOnlyList<CompilerDiagnostic> Diagnostics`
  - `string? GeneratedCSharp` (the emitted C# source, null if codegen failed)
  - `Module? Ast` (the parsed AST, null if parsing failed)
  - `SemanticInfo? SemanticInfo` (type information, null if semantic analysis failed)
  - `CompilationMetrics? Metrics` (optional timing data)
- [x] Create `public record ParseResult`:
  - `bool Success`
  - `IReadOnlyList<CompilerDiagnostic> Diagnostics`
  - `Module? Ast`
- [x] Create `public record SemanticResult`:
  - `bool Success`
  - `IReadOnlyList<CompilerDiagnostic> Diagnostics`
  - `Module? Ast`
  - `SemanticInfo? SemanticInfo`
  - `SymbolTable? SymbolTable`
- [x] Ensure all result types are immutable (no mutable collections exposed)

### 4c. Wire existing AstPositionService into the API

**File to modify**: `src/Sharpy.Compiler/CompilerApi.cs`

**Checklist**:

- [x] Add a `Node? FindNodeAtPosition(Module module, int line, int column)` convenience method that delegates to `AstPositionService.FindInnermostNode()`
- [x] Add a `T? FindNodeOfType<T>(Module module, int line, int column) where T : Node` convenience method
- [x] Add a `string FormatDiagnostic(CompilerDiagnostic diagnostic, string? source = null)` method that delegates to `DiagnosticRenderer`
- [x] Document in XML comments that line/column are 1-based (matching LSP conventions)

### 4d. Add tests for CompilerApi

**File to create**: `src/Sharpy.Compiler.Tests/CompilerApiTests.cs`

**Checklist**:

- [x] Test `Compile()` with a valid program — assert Success, non-null GeneratedCSharp, no error diagnostics
- [x] Test `Compile()` with a type error — assert !Success, at least one error diagnostic with SPY code and TextSpan
- [x] Test `Parse()` with a syntax error — assert !Success, error diagnostic with location
- [x] Test `Analyze()` with valid code — assert SemanticInfo is populated
- [x] Test `FindNodeAtPosition()` — parse a small program, find an identifier by position
- [x] Test `FormatDiagnostic()` — assert output contains `^^^` underlines when a span is present
- [x] Test cancellation — assert `OperationCanceledException` with a pre-cancelled token
- [x] Run: `dotnet test --filter "FullyQualifiedName~CompilerApi"`

### 4e. Refactor CLI to use CompilerApi

**File to modify**: `src/Sharpy.Cli/Program.cs`

**Checklist**:

- [x] Identify the core compilation logic in Program.cs that can be replaced with `CompilerApi.Compile()` calls
- [x] Refactor the `build` and `run` commands to use `CompilerApi`
- [x] Keep the CLI-specific concerns (console output, ANSI colors, exit codes) in Program.cs
- [x] Verify all CLI commands still work: `dotnet run --project src/Sharpy.Cli -- run snippets/hello.spy`
- [x] Run the full integration test suite: `dotnet test --filter "FullyQualifiedName~FileBasedIntegration"`

> **Gotcha**: Don't try to refactor the `project` (multi-file) command in this phase. ProjectCompiler has its own orchestration that's more complex. Focus on single-file compilation first.

---

## Phase 5: Unknown Type Invariant Tightening

> **Goal**: Distinguish between `UnknownType` used for error recovery (expected) and `UnknownType` appearing in fully-resolved contexts (compiler bug). Make the latter fail loudly.

### Rationale

`UnknownType` serves two purposes today: (1) it's the error recovery type when the user writes something invalid, and (2) it can silently appear when the type inference engine fails to resolve a type. Case (1) is expected; case (2) is a bug that should be caught immediately, not discovered months later when a user reports wrong codegen.

`CompilerInvariants` already has an `UnknownTypes` flag but it's marked as "aspirational" — it fires for case (1) too, making it too noisy to enable. This phase makes it precise.

### 5a. Track error-recovery Unknown types

**File to modify**: `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` (or wherever type recording happens)

**Checklist**:

- [ ] Read `SemanticInfo` to understand how types are recorded for AST nodes
- [ ] Add a `HashSet<Node>` (or equivalent) called `_errorRecoveryNodes` that tracks which nodes had their type set to `UnknownType` due to a user error (i.e., when `DiagnosticBag` already has an error for that node)
- [ ] When a type is set to `UnknownType`, check if a diagnostic was also emitted for the same location — if yes, add the node to `_errorRecoveryNodes`
- [ ] Add a public method `IsErrorRecoveryType(Node node)` that checks membership

> **Fork-in-the-road**: Should this tracking live in `SemanticInfo` or in a separate `ErrorRecoveryTracker`? **Decision**: Put it in `SemanticInfo` since that's where type information lives. Adding a separate class creates unnecessary indirection.

### 5b. Tighten the CompilerInvariants check

**File to modify**: `src/Sharpy.Compiler/Diagnostics/CompilerInvariants.cs`

**Checklist**:

- [ ] Read the existing `UnknownTypes` invariant check in `CompilerInvariants`
- [ ] Modify it to only flag `UnknownType` occurrences that are NOT in the error recovery set
- [ ] When a non-error-recovery Unknown is found, emit a diagnostic with severity Error (not Warning) and the next available code (SPY0905 is already `TooManyErrors`, SPY0906 is `ParserLoopStall`, so use **SPY0907** or add a new named constant like `UnexpectedUnknownType`):
  - Message: `"Internal: type inference produced UnknownType for '{nodeName}' without a corresponding error diagnostic. This is a compiler bug."`
  - Include the node's span for source context
- [ ] Enable the `UnknownTypes` flag by default (currently disabled because it was too noisy)
- [ ] Run the full test suite to verify no false positives: `dotnet test`

### 5c. Add assertions in TypeChecker for Unknown type propagation

**File to modify**: `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs`

**Checklist**:

- [ ] Find the helper method(s) that set a node's type to `UnknownType` in the TypeChecker
- [ ] For each, verify that a diagnostic is always emitted before or alongside setting the type to Unknown
- [ ] If any path sets Unknown without a diagnostic, either add the missing diagnostic or mark it as intentional error recovery with a comment explaining why
- [ ] Add a helper method `SetErrorRecoveryType(Node node)` that sets the type to `UnknownType` AND records it in the error recovery set — use this consistently instead of raw Unknown assignment
- [ ] Run: `dotnet test --filter "FullyQualifiedName~TypeChecker"`

---

## Phase 6: Error Message Location Testing

> **Goal**: Test that diagnostics point to the correct source locations, not just that they contain the right message text. Currently, `.error` test fixtures only check message substrings.

### Rationale

A diagnostic with the right message but wrong location is nearly as bad as no diagnostic at all — the user looks at the wrong line. The existing `.error` fixture format checks message substrings but not locations. Adding location assertions catches a class of bugs where error messages are correct but point to the wrong code.

### 6a. Design the location test format

**Checklist**:

- [ ] Read the existing test fixture runner in `src/Sharpy.Compiler.Tests/Integration/FileBasedIntegrationTests.cs` to understand how `.error` files are processed
- [ ] Design an extension to the `.error` format that optionally includes location assertions. Proposed format:
  ```
  Type 'str' is not assignable to 'int'
  @3:5
  ```
  Where `@3:5` on a line by itself means "the previous error should be on line 3, column 5". Lines without `@` continue to work as substring matches only.
- [ ] Alternative format: `Type 'str' is not assignable to 'int' @3:5` (inline). Choose whichever is simpler to implement.
- [ ] Implement the format extension in the test runner
- [ ] Ensure existing `.error` files continue to pass unchanged (backward compatible)

### 6b. Add location-aware error test fixtures

**Checklist**:

- [ ] Add 5-10 new `.error` test fixtures (or update existing ones) that include `@line:column` location assertions covering:
  - Type mismatch error (TypeChecker)
  - Undefined identifier error (NameResolver)
  - Access violation error (AccessValidator)
  - Missing return error (ControlFlowValidator)
  - Syntax error (Parser)
- [ ] For each fixture, verify the location is correct by running the compiler and checking the rendered output
- [ ] Run: `dotnet test --filter "FullyQualifiedName~FileBasedIntegration"`

### 6c. Add span-coverage test

**File to create**: `src/Sharpy.Compiler.Tests/Diagnostics/DiagnosticSpanCoverageTests.cs`

**Checklist**:

- [ ] Create a test class that compiles programs with known errors and asserts that the resulting `CompilerDiagnostic` objects have non-null `Span` values
- [ ] Test at least one error from each compiler phase: Lexer, Parser, NameResolution, TypeChecking, Validation
- [ ] For each, assert:
  - `diagnostic.Span.HasValue` is true
  - `diagnostic.Span.Value.Start >= 0`
  - `diagnostic.Span.Value.Length > 0`
- [ ] Run: `dotnet test --filter "FullyQualifiedName~DiagnosticSpanCoverage"`

---

## Phase 7: Codegen Snapshot Expansion

> **Goal**: Expand `.expected.cs` snapshot coverage from ~15 to ~40 representative fixtures, covering all major language features.

### Rationale

The `.expected.cs` snapshots detect codegen regressions that don't affect runtime output. For example, a change that emits `(int)x` instead of `Convert.ToInt32(x)` produces the same runtime result but may indicate an unintended codegen change. Currently only ~15 fixtures have snapshots, leaving many language features uncovered.

### 7a. Identify coverage gaps

**Checklist**:

- [ ] List all existing `.expected.cs` files: `find src/Sharpy.Compiler.Tests/Integration/TestFixtures -name "*.expected.cs"` (or use Glob)
- [ ] Categorize them by language feature (classes, functions, control flow, etc.)
- [ ] Identify the 25 most important uncovered categories. Priority areas:
  - Generic types and functions
  - Inheritance and interface implementation
  - Operator overloading (dunder methods)
  - Exception handling (try/except/finally)
  - List/dict/set comprehensions
  - String formatting (f-strings)
  - Type annotations and type narrowing
  - Enums and structs
  - Default parameters
  - Lambda expressions
  - Slicing
  - Multiple assignment / tuple unpacking

### 7b. Generate snapshots for uncovered features

**Checklist**:

- [ ] For each identified gap, pick an existing `.spy` fixture that exercises that feature (or create a minimal new one if none exists)
- [ ] Generate the `.expected.cs` snapshot: `UPDATE_SNAPSHOTS=true dotnet test --filter "DisplayName~<test_name>"`
- [ ] Review each generated snapshot to verify the C# output looks correct
- [ ] Target ~25 new snapshots (bringing total to ~40)
- [ ] Run the full snapshot test suite to verify: `dotnet test --filter "FullyQualifiedName~FileBasedIntegration"`

### 7c. Add snapshot documentation

**Checklist**:

- [ ] Add a brief comment at the top of each new `.expected.cs` file explaining what language feature it covers (follow the pattern of existing snapshots if they have this)
- [ ] If existing snapshots don't have header comments, add them to the existing ones too for consistency

---

## Phase 8: Structured Logging Consistency

> **Goal**: Ensure every pipeline stage logs entry/exit with timing, and that the logging is consistent and useful for debugging slow compilations.

### Rationale

`CompilationMetrics` already tracks per-phase timing, but the logging within individual phases is inconsistent. Some phases log detailed progress (e.g., "resolving imports for module X"), others log nothing. Consistent structured logging makes it possible to diagnose slow compilations by looking at logs without attaching a debugger.

### 8a. Audit current logging

**Checklist**:

- [ ] Search for all uses of `ICompilerLogger` across `src/Sharpy.Compiler/`
- [ ] For each pipeline stage (Lexer, Parser, NameResolver, ImportResolver, TypeResolver, TypeChecker, ValidationPipeline, RoslynEmitter), document:
  - Does it log entry/exit?
  - Does it log per-item progress (e.g., "checking function foo")?
  - Does it use `CompilationMetrics` for timing?
- [ ] Create a checklist of gaps (stages that don't log consistently)

### 8b. Add consistent entry/exit logging

**Checklist**:

- [ ] For each pipeline stage that lacks entry/exit logging, add:
  ```csharp
  _logger.LogDebug($"Starting {phaseName}...");
  // ... phase work ...
  _logger.LogDebug($"Completed {phaseName} ({count} items processed)");
  ```
- [ ] The codebase uses the **Null Object pattern** (`NullLogger.Instance`), not nullable loggers — `_logger` is never null, so do NOT use `_logger?.`. All constructors default to `logger ?? NullLogger.Instance`. Follow this existing convention.
- [ ] Verify that `CompilationMetrics` is used for timing in all stages (it should already be — just verify)
- [ ] Run: `dotnet test`

### 8c. Enhance verbose mode in CLI

**File to modify**: `src/Sharpy.Cli/Program.cs`

**Checklist**:

- [ ] The CLI already has a `--log-level` option (accepts `CompilerLogLevel` enum: None, Error, Warning, Info, Debug, Trace). This should be the basis for verbose output — do NOT add a separate `--verbose` flag.
- [ ] When `--log-level` is set to `Info` or higher, write a `CompilationMetrics` summary and per-phase timing to stderr after compilation completes
- [ ] When `--log-level` is set to `Debug` or higher, include per-validator timing from `ValidatorTimes`
- [ ] Ensure verbose output goes to stderr (not stdout) so it doesn't interfere with program output
- [ ] Test: `dotnet run --project src/Sharpy.Cli -- run --log-level Info snippets/hello.spy`

---

## Implementation Order

Phases are listed in priority order. Each phase is independent — they can be implemented in any order, though the recommended sequence maximizes value delivered early:

| Priority | Phase | Impact | Effort | LSP Prep |
|----------|-------|--------|--------|----------|
| 1 | Phase 1: TextSpan Coverage | High | Low-Med | Yes |
| 2 | Phase 2: Fuzz Testing | High | Medium | No |
| 3 | Phase 3: CancellationToken | High | Low | Yes |
| 4 | Phase 4: Compiler API | Med-High | Medium | Yes |
| 5 | Phase 5: Unknown Invariants | Medium | Low | No |
| 6 | Phase 6: Error Location Tests | Medium | Low | Yes |
| 7 | Phase 7: Codegen Snapshots | Medium | Medium | No |
| 8 | Phase 8: Logging Consistency | Low-Med | Low | No |

**Phases 1, 3, and 6 are the highest ROI** — they're relatively small changes that dramatically improve error quality and LSP readiness. Phase 4 is the largest and should be started only after Phases 1 and 3 are complete (so the API exposes cancellation and rich diagnostics from day one).
