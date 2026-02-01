# Sharpy Compiler: State Assessment & Improvement Roadmap

## Overall Assessment

The compiler has solid foundational architecture — clear pipeline phases, immutable AST, good test volume (3,576 tests, 148 integration fixtures). The codebase is in an **active refactoring state** where modern patterns (DiagnosticBag, ValidationPipeline, CompilerServices, AstTraversalContext) have been introduced but the old patterns haven't been fully retired. This half-migrated state is the single biggest source of tech debt and latent bugs.

Below are the most impactful improvements, ranked by how much they buy across your stated goals (robustness, usability, debuggability, contributability, LSP-readiness, bug obviousness).

---

## Tier 1: High Impact / Foundation (do these first)

### 1. Unify Error Handling onto DiagnosticBag (the single biggest win)

**Problem:** Three incompatible error paradigms coexist:
- Lexer/Parser **throw exceptions** (`LexerError`, `ParserError` — both extend `Exception`)
- Semantic phase **collects** `List<SemanticError>` (also extends `Exception` despite never being thrown)
- Newer validators use `DiagnosticBag` via `SemanticContext`

The compiler driver (`Compiler.cs:349`) catches `Exception` generically, discarding stack traces, location info, and severity. `CompilationResult.Errors` is `List<string>` — all structure is lost.

**What this costs you:**
- Compiler stops at the first lexer/parser error (no recovery, no "here are all 5 problems")
- No error codes (can't document, search, suppress, or test specific errors)
- CLI can't format errors with source context (squiggly underlines, surrounding lines)
- LSP integration will need structured diagnostics — you'd have to retrofit everything later
- Contributors can't tell which pattern to use

**Recommendation:**
- Make `CompilerDiagnostic` the universal error representation across all phases
- Replace `LexerError`/`ParserError`/`SemanticError` exception throwing with collecting into a `DiagnosticBag`
- Change `CompilationResult.Errors` from `List<string>` to `DiagnosticBag`
- Add error codes (`SHP0001`, `SHP0002`, etc.) — the `Code` field on `CompilerDiagnostic` already exists but is unused
- Introduce basic error recovery in the parser (synchronize on statement boundaries) so users see multiple errors per compilation

**Files:** `Compiler.cs`, `LexerError.cs`, `ParserError.cs`, `SemanticError.cs`, `DiagnosticBag.cs`, `CompilationResult`

---

### 2. Complete the Validation Pipeline Migration

**Problem:** `ValidationPipelineFactory.CreateDefault()` returns an **empty pipeline** — no validators are registered. All validation is hardcoded inside `TypeChecker.CheckModule()`. V2 validators exist (`ControlFlowValidatorV2`, `AccessValidatorV2`, `OperatorValidatorV2`, etc.) alongside legacy validators, but they aren't wired into the pipeline.

**What this costs you:**
- Can't selectively run/skip validators (needed for incremental/LSP)
- Can't add new validation rules without touching the TypeChecker monolith
- Contributors must understand the TypeChecker internals to add validation
- Legacy+V2 coexistence means duplicated logic and confusion about which is canonical

**Recommendation:**
- Wire all V2 validators into `ValidationPipelineFactory.CreateDefault()`
- Extract remaining inline validation from TypeChecker into validators
- Remove legacy validators and `LegacyValidatorAdapter`
- The pipeline is already well-designed (ordered execution, error limits, logging) — just needs to be used

**Files:** `ValidationPipelineFactory.cs`, `TypeChecker.cs`, all `*Validator.cs` and `*ValidatorV2.cs` files

---

### 3. Expand Error Path Testing (9 → 50+ error fixtures)

**Problem:** 117 success test fixtures vs only 9 error test fixtures. Error paths are significantly undertested. This means bugs in error reporting, error recovery, and edge case handling can hide for months.

**What this costs you:**
- Regressions in error messages go unnoticed
- New features can silently break error detection
- Contributors can't verify their changes don't degrade error quality
- Users hit confusing error messages that were never tested

**Recommendation:**
- Add error fixtures for every error category: lexer errors (unterminated strings, invalid numbers, bad indentation), parser errors (missing colons, bad nesting, invalid syntax), semantic errors (undefined variables, type mismatches, access violations, missing returns, invalid inheritance)
- Target: every `AddError`/`throw` call site should have at least one corresponding test fixture
- This also serves as documentation of every error the compiler can produce

---

## Tier 2: High Impact / Structural

### 4. Make CompilationResult Carry Structured Diagnostics

**Problem:** `CompilationResult.Errors` is `List<string>`. All the structured information (line, column, file, severity, code) available in `CompilerDiagnostic` is serialized into a string and the structure is thrown away.

**Recommendation:**
- Change `CompilationResult` to carry a `DiagnosticBag` (or `IReadOnlyList<CompilerDiagnostic>`)
- Keep `Errors` as a convenience property (`get => Diagnostics.GetErrors().Select(d => d.Message).ToList()`) for backward compatibility during migration
- Same for `ProjectCompilationResult.Errors` and `.Warnings`
- The CLI can then format errors with location context, and future LSP gets diagnostics for free

**Files:** `Compiler.cs`, `CompilationResult`, `ProjectCompilationResult`, `ProjectCompiler.cs`, CLI `Program.cs`

---

### 5. Resolve Symbol Mutability

**Problem:** `Symbol` is a `record` (value equality) but has mutable `set` properties: `CodeGenInfo`, `BaseType`, `UnresolvedInterfaceNames`, `Interfaces`. This is a documented compromise, and `SemanticBinding.cs` exists as a stub for the fix.

**What this costs you:**
- Symbols can't be safely shared across threads (needed for parallel compilation)
- Mutation after creation means the "same" symbol can behave differently depending on when you look at it
- Records with mutable properties are a footgun (equality comparisons use the initial values)

**Recommendation:**
- Complete the `SemanticBinding` pattern: separate "symbol declaration" (immutable, created by NameResolver) from "semantic annotation" (CodeGenInfo, resolved BaseType, resolved interfaces), stored in SemanticInfo alongside expression types
- This mirrors how `SemanticInfo` already separates expression types from AST nodes
- Intermediate step: make the mutable fields internal and add assertions that they're only set during the appropriate phase

**Files:** `Symbol.cs`, `SemanticBinding.cs`, `SemanticInfo.cs`, `NameResolver.cs`, `TypeChecker.cs`, `RoslynEmitter.cs`

---

### 6. Consolidate Inheritance Resolution

**Problem:** Inheritance resolution is scattered across three places:
1. `NameResolver.ResolveInheritance()` — resolves inheritance for locally-defined types
2. `Compiler.ResolveImportedTypeInheritance()` — resolves for imported types
3. `Compiler.ResolveTransitiveBaseTypes()` — fixpoint loop for transitive imports (max 100 iterations)

This is called from `Compiler.Compile()` between import resolution and type checking, with complex ordering dependencies.

**Recommendation:**
- Extract into a single `InheritanceResolver` class with a clear contract: "given a SymbolTable with all types registered, resolve all inheritance relationships"
- The fixpoint loop, transitive import resolution, and interface resolution can all live in one place
- This also makes it testable in isolation

**Files:** `Compiler.cs:385-486`, `NameResolver.cs`, new `InheritanceResolver.cs`

---

## Tier 3: Quality of Life / LSP Preparation

### 7. Unify Position Tracking on TextSpan

**Problem:** AST nodes carry both `LineStart/LineEnd/ColumnStart/ColumnEnd` (1-based) and `Span` (TextSpan, 0-based character offsets). Error messages use line/column. TextSpan exists but isn't consistently populated.

**For LSP:** The Language Server Protocol uses character offsets, not line/column. You need TextSpan to be reliably populated on all AST nodes.

**Recommendation:**
- Ensure Lexer populates TextSpan on all tokens
- Ensure Parser propagates TextSpan to all AST nodes
- Add a `SourceText` utility that converts between line/column and offset (the class exists in `Text/SourceText.cs`)
- Error messages can still display line/column (computed from TextSpan + SourceText), but the canonical representation becomes TextSpan
- Don't remove line/column from nodes yet — just ensure TextSpan is always populated

---

### 8. Add a `CancellationToken` Threading Model

**For LSP/IDE:** An LSP needs to cancel in-progress compilations when the user edits a file. Without cancellation support, the server has to wait for compilation to finish or kill the thread.

**Recommendation:**
- Thread a `CancellationToken` parameter through `Compiler.Compile()`, each phase, and the validation pipeline
- Check cancellation at natural boundaries (between phases, between validator passes, periodically during type checking)
- This is a mechanical change — add the parameter, sprinkle `token.ThrowIfCancellationRequested()` at phase boundaries
- Low effort, high LSP payoff

---

### 9. Make ImportResolver Testable and Smaller

**Problem:** `ImportResolver.cs` is 1200+ lines handling: module discovery, file caching, transitive imports, re-exports, error collection. It's hard to test in isolation and hard for contributors to understand.

**Recommendation:**
- Extract `ModuleDiscovery` (finding .spy files on disk) from `ImportResolver` (resolving import statements to symbols)
- Extract `ModuleCache` (caching parsed modules) — this is also needed for incremental compilation
- The existing `CachedModuleDiscovery` in `Discovery/` could be leveraged

---

### 10. Integrate the Control Flow Graph

**Problem:** A `ControlFlowGraph` infrastructure exists in `Analysis/ControlFlow/` with immutable CFG, basic blocks, entry/exit nodes — but it's not used. `ControlFlowValidatorV2` does manual AST traversal instead.

**Recommendation:**
- Wire the CFG into `ControlFlowValidatorV2`
- This enables more sophisticated analysis: unreachable code detection, definite assignment, exhaustive return checking
- CFG is also a prerequisite for future optimizations and data flow analysis
- The infrastructure is already built — this is about integration, not new development

---

## Tier 4: Smaller Wins

### 11. Add Internal Consistency Assertions

**Goal:** Make bugs obvious without having to discover them months later.

- Add `Debug.Assert` / conditional checks at phase boundaries:
  - After NameResolver: all type symbols have names, no duplicate definitions
  - After ImportResolver: all import statements resolved, no dangling UnresolvedBaseName without corresponding symbol
  - After TypeChecker: all expressions have types in SemanticInfo, no `Unknown` types remaining (except intentional)
  - After CodeGen: generated C# parses without errors
- These assertions are free in Release builds and catch invariant violations immediately in development

---

### 12. Add a Compiler `--explain` Mode

**Goal:** Debuggability for users and contributors.

- When an error occurs, `--explain SHP0042` prints a detailed explanation with examples and common fixes
- This requires error codes (Tier 1, item 1) to be in place first
- Even without `--explain`, error codes let users search for help online

---

### 13. Document the Contribution Path

**Goal:** Contributability.

- Add a `CONTRIBUTING.md` that describes: how the pipeline works, how to add a new language feature (lexer tokens → parser AST → semantic rules → codegen emission → test fixtures), how to add a new validation rule (implement `ISemanticValidator`, register in pipeline)
- The agent documentation in `.github/agents/` is excellent for AI assistants but not formatted for human contributors

---

## What NOT to Build Yet

These are explicitly **not recommended now**, but the above work paves the way:

- **LSP server** — wait until diagnostics are structured and TextSpan is reliable
- **Incremental compilation** — wait until ImportResolver is decomposed and symbols are immutable
- **Parallel compilation** — wait until DiagnosticBag is the universal error channel and symbols don't mutate
- **REPL** — wait until error recovery exists in the parser

---

## Suggested Execution Order

| Phase | Items | Rationale |
|-------|-------|-----------|
| A | 1 (Unified errors), 3 (Error test fixtures) | Foundation — everything else depends on structured diagnostics |
| B | 4 (Structured CompilationResult), 2 (Pipeline migration) | Wire the new error system through, clean up validation |
| C | 5 (Symbol immutability), 6 (Inheritance consolidation) | Structural cleanup that unblocks parallelism |
| D | 7 (TextSpan), 8 (CancellationToken), 11 (Assertions) | LSP preparation |
| E | 9 (ImportResolver), 10 (CFG), 12-13 (Polish) | Quality of life |

Each phase is independently valuable and shippable. Phase A alone would be a significant improvement to the compiler's robustness and usability.

---

## Phase C Completion Notes

**Status: Complete**

### Item 5: Symbol Immutability (SemanticBinding Pattern)

The SemanticBinding pattern is fully implemented and production-ready:

- **SemanticBinding** (`Semantic/SemanticBinding.cs`) is the sole write target for all mutable semantic data during analysis. Uses `ConcurrentDictionary` with `ReferenceEqualityComparer` for thread-safe, reference-stable lookups. Interface collections use `ConcurrentQueue` (not `ConcurrentBag`) to preserve insertion order for deterministic builds.
- **Materialization** at phase boundaries copies data from SemanticBinding stores onto Symbol properties (`MaterializeInheritance()`, `MaterializeVariableTypes()`, `MaterializeCodeGenInfo()`), enabling downstream consumers (RoslynEmitter, SemanticType) to read Symbol properties directly.
- **Phase-gating** via `FreezeInheritance()`, `FreezeVariableTypes()`, `FreezeCodeGenInfo()` prevents writes after a phase completes (DEBUG-only assertions).
- **DualWriteAssertions** (`Semantic/DualWriteAssertions.cs`) verify materialization consistency in DEBUG builds. Bidirectional checks: forward (Symbol → SemanticBinding) catches rogue Symbol writes, reverse (SemanticBinding → Symbol) catches materialization failures.
- **Dual-read pattern** throughout the codebase: readers prefer `SemanticBinding.GetXxx()` with fallback to Symbol properties.
- **Zero direct Symbol property writes** outside of materialization — verified by grep.
- **All writers** (NameResolver, InheritanceResolver, TypeChecker, CodeGenInfoComputer, ImportResolver) write exclusively to SemanticBinding.

Symbol properties retain `internal set` accessors to support materialization. This is the pragmatic design: SemanticBinding is authoritative during analysis, materialization bridges to consumers that read Symbol properties. Removing `internal set` entirely would require all downstream consumers to accept a SemanticBinding parameter.

**Test coverage:** 26 SemanticBinding unit tests (materialization, freeze, module resolution, HasCodeGenInfo, defaults) + 20 DualWriteAssertions tests (15 forward-direction + 5 reverse-direction).

### Item 6: Consolidate Inheritance Resolution

Inheritance resolution is consolidated into a clear two-tier architecture:

- **NameResolver** handles local type inheritance (types defined in the current file) during `ResolveInheritance()`.
- **InheritanceResolver** (`Semantic/InheritanceResolver.cs`) handles stages 2–3:
  - `ResolveTransitiveBaseTypes()` — auto-imports base types from loaded modules (fixpoint iteration, max 100 rounds)
  - `ResolveImportedTypeInheritance()` — resolves string-based `UnresolvedBaseName`/`UnresolvedInterfaceNames` to actual TypeSymbol references
- Wired into both `Compiler.Compile()` and `ProjectCompiler.Compile()` via `InheritanceResolver.ResolveAll()`.
- The old scattered methods (`Compiler.ResolveImportedTypeInheritance()`, `Compiler.ResolveTransitiveBaseTypes()`) have been removed.

**Test coverage:** 13 InheritanceResolver unit tests (resolution, edge cases, dual-read pattern) + 19 cross-module integration tests (10 Semantic + 9 Integration) + 26 file-based test fixtures.
