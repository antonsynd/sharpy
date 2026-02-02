# Sharpy Compiler Hardening Assessment

> **Author:** Staff Compiler Engineer Assessment (Claude Opus 4.5)
> **Date:** 2026-02-02
> **Reviewed:** 2026-02-02 (independent verification pass — see Verification Notes below)
> **Branch:** `dev` (commit `578d0dd3`)
> **Scope:** Robustness, usability, ergonomics, efficiency, debuggability, contributability

---

## Executive Summary

The Sharpy compiler has strong fundamentals: clean architecture, ~4,800 test annotations + 315 file-based fixtures + fuzz testing, zero TODO/FIXME/HACK comments, zero mutable static state, a Rust-style diagnostic system, and CancellationToken-aware pipeline. The codebase is ahead of most compilers at this stage.

This document identifies 25 improvements across 5 priority tiers. Each item includes rationale, actionable subtasks suitable for a junior engineer or Claude Sonnet, and implementation guidance aligned with the codebase's axioms (`.NET > Type Safety > Python Syntax`).

### Metrics at Time of Assessment

| Metric | Value |
|--------|-------|
| C# source files | 533 |
| Compiler lines | ~42,700 |
| Test annotations | ~4,800 |
| File-based fixtures | 315 `.spy` files |
| Build warnings | 57 |
| Skipped tests | 16 (3 file-based + 13 unit) |
| `NotImplementedException` in codegen | 15 |
| Silent statement drops in emitter | 4 locations |
| Open `See: #NNN` references | 22 across 14 files |

### Verification Notes (2026-02-02)

An independent verification pass confirmed the assessment's accuracy with the following corrections and additions:

**Corrections made:**
1. **1.1c, 1.1 Guidance** — Original text stated `CodeGenContext` doesn't have a `DiagnosticBag`. Verified this is incorrect: `CodeGenContext.cs` already has `_diagnostics` (line 14), `Diagnostics` property (line 30), and `AddError` method (line 40). The real issue is that the 4 silent-drop locations don't call `_context.AddError()`. Text updated.
2. **2.1a** — Diagnostic codes `SHP0510`–`SHP0514` conflicted with `SHP0510` already allocated to item 1.1a. Renumbered to `SHP0515`–`SHP0519`.
3. **2.1c** — Referenced a prerequisite from 1.1c that doesn't exist (adding DiagnosticBag to CodeGenContext). Rewritten to say "use `_context.AddError()`" since the infrastructure already exists.

**Items added by verification:**
4. **2.6** — TypeChecker silently discards errors after `MaxErrors` when `ContinueAfterError=true` (`TypeChecker.Utilities.cs:934-938`). No truncation notice.
5. **5.4** — Priority promotion note added. Determinism is a hard prerequisite for incremental compilation (5.1).
6. **5.6** — ~227 scattered dunder method name string literals should be extracted to a constants class.
7. **Appendix D** — Recommended implementation order added.

**Additional observations (not added as items):**
- Diagnostic deduplication logic (`TypeChecker.cs:216-244`) uses position+message and position+code matching — more fragile than the 5.5 description implies, but the recommended fix (prevent duplication at source) is correct.
- `ImportResolver` logs warnings to `ICompilerLogger` instead of `DiagnosticBag` for missing re-exported symbols. This could be a separate item but is minor.
- Semantic data materialization is duplicated between `Compiler.cs` and `ProjectCompiler.cs`. Not a correctness issue but a maintenance concern for future refactoring.

---

## Tier 1: Silent Correctness Bugs

**Priority:** Fix immediately. These can produce wrong output or confuse users without any indication that something went wrong.

---

### 1.1 Silent Statement Drops in Code Generation

**What:** Four places in `RoslynEmitter` have `_ => null` or `default: // Ignore` that silently drop AST nodes from generated code with zero diagnostic output.

**Why it matters:** This is the most dangerous class of compiler bug. If a user writes valid code that the compiler silently ignores, the program compiles but doesn't do what they wrote. Worse, when a contributor adds a new AST node and forgets to update the emitter, the omission is invisible.

**Locations:**
- `RoslynEmitter.Statements.cs:35` — `GenerateBodyStatement` default case
- `RoslynEmitter.ModuleClass.cs:446` — `GenerateStatement` default case
- `RoslynEmitter.ClassMembers.cs:109-111` — `GenerateClassMembers` default case
- `RoslynEmitter.ClassMembers.cs:572-574` — `GenerateInterfaceMembers` default case

**Tasks:**

- [ ] **1.1a** Add a new diagnostic code `SHP0510` ("Internal: unrecognized statement type '{0}' was not emitted. This is a compiler bug — please report it.") to `DiagnosticCodes.cs`
- [ ] **1.1b** Add a corresponding entry in `DiagnosticExplanations.cs` with the "compiler bug" category, linking to the GitHub issues page
- [ ] **1.1c** In each of the 4 locations, replace the silent null/ignore with a call to `_context.AddError(...)` emitting `SHP0510`. (`CodeGenContext` already has a `DiagnosticBag` via the `_diagnostics` field, a `Diagnostics` property, and an `AddError` method — see `CodeGenContext.cs:14,30,40`.)
- [ ] **1.1d** Add a unit test that constructs a mock AST with a custom statement type and verifies the diagnostic is emitted
- [ ] **1.1e** Verify all existing tests still pass — no existing statement type should be hitting these default cases

**Guidance:**
- `CodeGenContext` already has a `DiagnosticBag` (`_diagnostics` field at line 14, `Diagnostics` property at line 30, `AddError` method at line 40 of `CodeGenContext.cs`). The fix is simply calling `_context.AddError(...)` at each of the 4 silent-drop locations.
- Do NOT throw an exception here. The compiler should emit the diagnostic and continue generating the rest of the file, so the user sees the full picture.
- The `SHP0510` code should use the `SHP05xx` range reserved for codegen errors, consistent with existing codes like `SHP0500`–`SHP0507`.

---

### 1.2 `UnknownType.IsAssignableTo` Always Returns `true`

**What:** `SemanticType.cs:139` — `UnknownType` passes all type checks. Combined with `TypeChecker.Expressions.cs:49` returning `Unknown` for unrecognized expression types (and the fallback going only to `ICompilerLogger`, not `DiagnosticBag`), there's a silent pass-through path.

**Why it matters:** If a new expression AST node is added and the TypeChecker isn't updated, the expression silently type-checks as `Unknown` and passes every subsequent assignment, argument, and return type check. This creates bugs that are invisible during development and only surface when a user hits the unrecognized expression in the emitter.

**Tasks:**

- [ ] **1.2a** In `TypeChecker.cs` (the `CheckStatement` default case at line 332), change the `_logger.LogWarning(...)` to also emit a diagnostic via `AddError(...)` with a new code `SHP0255` ("Internal: unrecognized statement type")
- [ ] **1.2b** In `TypeChecker.Expressions.cs:49`, change the `_ => SemanticType.Unknown` fallback to also emit a diagnostic with a new code `SHP0256` ("Internal: unrecognized expression type")
- [ ] **1.2c** Add a DEBUG-only assertion in `Compiler.cs` (alongside the existing `AssertStatementsHaveSpans` etc.) that verifies: if `SemanticInfo` contains any `UnknownType` entries, the `DiagnosticBag` must also contain at least one error. This catches the invariant violation early.
- [ ] **1.2d** Add unit tests for the new diagnostic codes

**Guidance:**
- The `Unknown` type's `IsAssignableTo` returning `true` is correct and should NOT be changed — it genuinely prevents cascading errors. The fix is at the entry point (where `Unknown` is first produced), not at the consumption point.
- The diagnostic message should include the AST node type name (e.g., `"Internal: unrecognized expression type 'AwaitExpression'"`) so contributors immediately know which handler is missing.
- Use the `SHP02xx` range for these since they're semantic analysis errors.

---

### 1.3 Comparison Chain Re-evaluation (Issue #101)

**What:** `RoslynEmitter.Expressions.cs:~1093` — For `a < f() < c`, the generated C# evaluates `f()` twice. This is semantically incorrect for expressions with side effects.

**Why it matters:** Python guarantees single evaluation of intermediate expressions in chained comparisons. This is a spec violation that produces wrong behavior for any chained comparison involving function calls, property accesses, or other side-effectful expressions.

**Tasks:**

- [ ] **1.3a** In the comparison chain generation code, introduce a temp variable for any intermediate expression that is not a simple identifier or literal. Use the existing `_variableVersions` pattern for naming (`__cmp_temp_0`, `__cmp_temp_1`, etc.)
- [ ] **1.3b** Add a file-based test fixture: `type_system/comparison_chain_side_effects.spy` that calls a function with a side effect (e.g., incrementing a counter) in a comparison chain and verifies the function is called exactly once
- [ ] **1.3c** Add a file-based test fixture for the existing working case (literals/identifiers) to prevent regression
- [ ] **1.3d** Verify against Python behavior: `python3 -c "..."` per the codebase rule (CLAUDE.md item 6)

**Guidance:**
- The temp variable approach is standard for this problem. Roslyn SyntaxFactory makes this straightforward: generate a `var __cmp_temp_0 = <expr>;` statement before the comparison, then reference the temp in both comparison operands.
- Only introduce temps for non-trivial expressions. Simple identifiers and literals don't need them (checking `expr is Identifier or expr is IntLiteral or expr is FloatLiteral or expr is StringLiteral or expr is BoolLiteral` is sufficient).
- Axiom 2 (Python Syntax) demands Python semantics for comparison chains. This is a case where all three axioms align.

---

### 1.4 `IsFloatExpression` Heuristic in Floor Division

**What:** `RoslynEmitter.Operators.cs:406` — For variables and function calls, `IsFloatExpression` assumes integer. Floor division on float-returning expressions generates incorrect code.

**Why it matters:** `x // get_float()` would produce integer division behavior instead of float floor division, silently giving wrong results.

**Tasks:**

- [ ] **1.4a** Modify `IsFloatExpression` (or its callers) to consult `SemanticInfo` for the resolved type of the expression, rather than guessing from the AST shape. The `SemanticInfo` is already available to the emitter via `_context`.
- [ ] **1.4b** Add a file-based test: a function returning `float`, used in `//` floor division, asserting the correct Python-equivalent result
- [ ] **1.4c** Verify against Python: `python3 -c "print(7.5 // 2.0)"` etc.

**Guidance:**
- The comment at the location says "A full type system would resolve these properly." The type system already resolves them — the information is in `SemanticInfo`. This is a case of the emitter not using available data.
- If `SemanticInfo` lookup fails (shouldn't happen for well-typed code), fall back to the existing heuristic with a debug warning.

---

### 1.5 Fix Build Warnings (57 warnings)

**What:** The solution builds with 57 warnings, including nullable reference warnings (CS8604, CS8602) that indicate potential `NullReferenceException` at runtime.

**Why it matters:** Warnings erode signal-to-noise ratio. The CS8602 in `ProjectCompiler.cs:308` is a genuine null dereference risk. Zero-warning builds should be enforced in CI.

**Tasks:**

- [ ] **1.5a** Fix CS8602 in `ProjectCompiler.cs:308` — add proper null check rather than null-forgiving operator
- [ ] **1.5b** Fix CS8604 warnings in `Sharpy.Core` (DictItemsView, DictValuesView, Dict, List.operators) — these are all `Exports.Eq()` calls that may pass null. Add null guards or use `!` with a comment explaining why null is safe in context
- [ ] **1.5c** Fix CS8604 in `TypeChecker.Expressions.cs:970` — `overloads` null check before `.Where()`
- [ ] **1.5d** Fix CS0219 in `Parser.Types.cs:320` — remove unused `hasTrailingComma` variable
- [ ] **1.5e** Fix CS1574/CS1580 XML doc cref in `Dict.cs:330` (use angle brackets `<>` not `{}`) and `Str.cs:31`
- [ ] **1.5f** Fix xUnit2013 in `FrozenSetConversionTests.cs:13` and xUnit2012 in `ModuleLoaderTests.cs:320`
- [ ] **1.5g** For the intentional CS0252/CS0253 and CS1718 warnings in test code (reference comparison tests), add `#pragma warning disable`/`restore` around the specific lines with a comment explaining they're intentional
- [ ] **1.5h** Add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to `Directory.Build.props` or each `.csproj` to enforce zero warnings going forward
- [ ] **1.5i** Verify CI passes with zero warnings

**Guidance:**
- For the CS8604 warnings in `Sharpy.Core`, the `Exports.Eq()` null handling depends on whether the Python semantics of `==` should support `None` comparisons. Check Python behavior first (`python3 -c "print([1,2] == None)"`). If Python returns `False` for `x == None`, the `Eq()` function should handle null gracefully.
- Do NOT use `#pragma warning disable` for the compiler source warnings — fix them properly. `#pragma` is only acceptable for the intentional test cases.
- The `TreatWarningsAsErrors` in 1.5h is the most impactful sub-task: it prevents future regressions permanently.

---

## Tier 2: Robustness & Debuggability

**Priority:** Address soon. These make bugs self-revealing during development rather than hiding until a user finds them.

---

### 2.1 Convert `NotImplementedException` Throws to Proper Diagnostics

**What:** 15 `NotImplementedException` throws in `RoslynEmitter` surface as generic "Compilation failed: [message]" via the top-level `catch (Exception)` in `Compiler.cs:427`. Users can't tell if they wrote bad code or hit a compiler limitation.

**Why it matters:** User trust. A compiler that says "Compilation failed" for valid-looking code with no actionable suggestion is a compiler people stop using. These are known limitations, not bugs — they deserve their own diagnostic codes with helpful messages.

**Locations (grouped by feature):**

| Feature | Files:Lines |
|---------|------------|
| Nested comprehensions | `RoslynEmitter.Expressions.cs:628,696,765` |
| Tuple unpacking in comprehensions | `RoslynEmitter.Expressions.cs:599,669,738` |
| Complex tuple unpacking | `RoslynEmitter.Statements.cs:283,869` |
| Unsupported target types | `RoslynEmitter.Statements.cs:286,872` |
| Complex function expressions | `RoslynEmitter.Expressions.cs:241` |
| Catch-all expression/operator | `RoslynEmitter.Expressions.cs:77,391,461` |
| Augmented assignment | `RoslynEmitter.Statements.cs:307` |
| Comparison chain operator | `RoslynEmitter.Expressions.cs:1117` |

**Tasks:**

- [ ] **2.1a** Define new diagnostic codes in `DiagnosticCodes.cs` (note: `SHP0510` is already allocated to 1.1a for silent statement drops):
  - `SHP0515` — "Nested comprehensions (multiple `for` clauses) are not yet supported"
  - `SHP0516` — "Tuple unpacking in comprehensions is not yet supported"
  - `SHP0517` — "Complex tuple unpacking (non-identifier targets) is not yet supported"
  - `SHP0518` — "Unsupported expression/statement type in code generation"
  - `SHP0519` — "Unsupported operator in code generation"
- [ ] **2.1b** Add entries in `DiagnosticExplanations.cs` for each code. Include a workaround in the "Fix" field (e.g., "Use a `for` loop instead of a nested comprehension")
- [ ] **2.1c** Use `_context.AddError(...)` at each throw site (`CodeGenContext` already has a `DiagnosticBag` — see 1.1c verification note)
- [ ] **2.1d** Replace each `throw new NotImplementedException(...)` with a diagnostic emission + a safe fallback:
  - For expression-position throws: emit the diagnostic, then return a `ThrowExpression` of `NotImplementedException` (so the generated C# at least compiles and throws at runtime with a clear message)
  - For statement-position throws: emit the diagnostic, then return an empty statement or a comment
- [ ] **2.1e** Also use `CodeGenException` (which is defined but unused at `CodeGen/CodeGenException.cs`) for truly unexpected conditions that represent compiler bugs. Distinguish "not yet implemented" (user-facing limitation with a workaround) from "should never happen" (internal error, please report).
- [ ] **2.1f** Add test fixtures in `errors/` for at least nested comprehensions and tuple unpacking in comprehensions, verifying the new diagnostic codes appear
- [ ] **2.1g** Delete `CodeGenException` if it remains unused after this work, or repurpose it as the "internal compiler error" exception type

**Guidance:**
- The key UX principle: if the user can work around the limitation (and these all have workarounds), the diagnostic message should say how. "Use a `for` loop instead" is more valuable than "not yet supported."
- The fallback-expression approach (generating `throw new NotImplementedException("...")` in the C# output) is a common compiler strategy. It means the generated code still compiles, and if the code path is actually reached at runtime, the user gets a clear exception rather than missing behavior.
- Group related `NotImplementedException` calls under the same diagnostic code where the workaround is the same. Don't over-proliferate codes.

---

### 2.2 Promote SemanticBinding Freeze Violations

**What:** `SemanticBinding.cs:105-107,131-133,152-154` — Writing after freeze only logs a warning via `ICompilerLogger`, which is often `NullLogger`. Data corruption happens silently.

**Why it matters:** The freeze-then-materialize pattern is a critical architectural invariant. If a contributor accidentally writes to `SemanticBinding` after a phase boundary, the materialized `Symbol` properties become inconsistent with the binding stores. This class of bug is extremely hard to diagnose because the symptoms appear far from the cause.

**Tasks:**

- [ ] **2.2a** Add `[Conditional("DEBUG")] private void AssertNotFrozen(string storeName, string phase)` method to `SemanticBinding` that calls `Debug.Fail(...)` with a descriptive message
- [ ] **2.2b** Call `AssertNotFrozen` at each of the three freeze-check locations, in addition to the existing logger warning
- [ ] **2.2c** Add a unit test that attempts to write after freeze in DEBUG mode and verifies the assertion fires (use `Assert.Throws` or equivalent)
- [ ] **2.2d** Keep the logger warning for Release builds as a defense-in-depth measure

**Guidance:**
- Use `Debug.Fail()` rather than `Debug.Assert(false, ...)` because `Debug.Fail` always fires (it doesn't evaluate a condition), making the intent clearer.
- This should NOT throw in Release builds — a warning is appropriate there, since the compiler should be robust against its own bugs. But in Debug builds, failing fast is critical.
- The `DualWriteAssertions` class (already `internal static`, `[Conditional("DEBUG")]`) is the pattern to follow.

---

### 2.3 Promote DEBUG-Only Invariant Checks

**What:** `Compiler.cs:450-527` — `AssertStatementsHaveSpans`, `AssertSymbolsHaveNames`, `AssertNoDuplicateTypeNames`, `AssertNoUnresolvedInheritance`, `AssertNoUnknownTypes` are `[Conditional("DEBUG")]`. They only run in debug builds.

**Why it matters:** These invariants catch regressions in the semantic pipeline. They're cheap (single-pass over small collections) and finding violations in CI is infinitely better than finding them via a user bug report.

**Tasks:**

- [ ] **2.3a** Remove the `[Conditional("DEBUG")]` attribute from all five assertion methods
- [ ] **2.3b** Change each method to emit an `SHP09xx` infrastructure diagnostic instead of calling `Debug.Assert`. Use `SHP0904` ("Internal invariant violation: {message}") or similar.
- [ ] **2.3c** Since these now run unconditionally, add a `Stopwatch` or simple timer around the assertion block and log the elapsed time to `ICompilerLogger` at `Debug` level, to ensure they don't become a bottleneck
- [ ] **2.3d** Add one test per assertion method that constructs a scenario violating the invariant and verifies the diagnostic is emitted

**Guidance:**
- These assertions are post-phase checks, not hot-path code. Even for large programs, iterating the symbol table and statement list is sub-millisecond. The performance concern is negligible.
- The diagnostic should say "This is a compiler bug" in the message, consistent with how `SHP0599` (generated C# parse error) already works.
- If any of these assertions fire in the existing test suite after removing `[Conditional("DEBUG")]`, that's a bug to fix — not a reason to keep them debug-only.

---

### 2.4 Lexer Error Recovery

**What:** The lexer aborts on the first error (`Lexer.cs:183-187`). A single unterminated string prevents the parser from reporting any errors.

**Why it matters:** Users fix one error at a time in a loop: edit, compile, read error, repeat. If a trivial lexer error (unterminated string, invalid character) blocks ALL parser diagnostics, the user can't see the other 5 errors they also need to fix. This is one of the highest-impact UX improvements for a compiler.

**Tasks:**

- [ ] **2.4a** In `Lexer.TokenizeAll()`, after catching `LexerAbortException`, instead of immediately adding EOF and returning, attempt to skip to the next newline and continue tokenizing
- [ ] **2.4b** Implement a `RecoverFromError()` method (analogous to the parser's `Synchronize()`) that advances `_position` past the error to the next newline character, then resumes normal tokenization
- [ ] **2.4c** Add a `MaxErrors` field to the lexer (default 25, matching the parser) to prevent infinite error loops
- [ ] **2.4d** Add test cases:
  - Unterminated string on line 1, valid code on line 2 → should report lexer error for line 1 AND either tokenize line 2 correctly or report parser errors for it
  - Multiple lexer errors in one file → should report more than one
- [ ] **2.4e** Update the fuzz tests to verify the lexer still never crashes with recovery enabled

**Guidance:**
- The recovery strategy should be conservative: skip to next newline, then resume as if starting a new line. Don't try to be clever about recovering mid-token.
- The existing `LexerAbortException` pattern (throw-after-recording-diagnostic) is fine for the internal mechanics. The change is in `TokenizeAll()`: instead of catching once and stopping, catch in a loop and continue.
- Edge case: if the error is in an indentation-sensitive context, the indent stack may be corrupted. Consider resetting `_indentStack` to a known state (e.g., just the base level) during recovery.
- Test that the lexer still produces an EOF token at the end, even after recovery.

---

### 2.5 Route `Console.Error.WriteLine` Through the Logger

**What:** 5 places in `OverloadIndexBuilder.cs` and `OverloadIndexCache.cs` write directly to stderr, bypassing the structured logging system.

**Why it matters:** Library code should never write to console. When the compiler is used as a library (e.g., by an LSP server), these writes pollute the host process's stderr with messages the host can't control or suppress.

**Locations:**
- `Discovery/Caching/OverloadIndexBuilder.cs:154,159,164`
- `Discovery/Caching/OverloadIndexCache.cs:196,229`

**Tasks:**

- [ ] **2.5a** Add an `ICompilerLogger` parameter to `OverloadIndexBuilder`'s constructor (or the method that currently writes to stderr)
- [ ] **2.5b** Replace each `Console.Error.WriteLine(...)` with `_logger.LogWarning(...)` or `_logger.LogDebug(...)` as appropriate
- [ ] **2.5c** For `OverloadIndexCache`, same pattern — pass logger through
- [ ] **2.5d** Also address the silent `ReflectionTypeLoadException` swallowing in `ModuleRegistry.cs:234` — add a `_logger.LogDebug(...)` call so it's at least visible in debug log output
- [ ] **2.5e** Verify no other `Console.Error.Write` or `Console.Write` calls exist in `Sharpy.Compiler` (only `Sharpy.Cli` should touch console)

**Guidance:**
- Use `LogDebug` for messages that are only interesting during development (e.g., "Skipping unmappable method"). Use `LogWarning` for conditions that might indicate a problem (e.g., "Failed to delete cache file").
- The `ICompilerLogger` is already threaded through most of the compiler. The `Discovery/Caching/` classes are outliers because they predate the logging infrastructure.

---

### 2.6 TypeChecker Silent Error Discarding After MaxErrors

**What:** `TypeChecker.Utilities.cs:934-938` — When `ContinueAfterError=true` and the error count reaches `MaxErrors`, the `AddError` method silently returns without recording the diagnostic. No truncation notice is emitted.

**Why it matters:** The user sees N errors but has no indication that additional errors were suppressed. Other compilers (GCC, Clang, Roslyn) emit a "too many errors; stopping" diagnostic so the user knows the output is truncated.

**Tasks:**

- [ ] **2.6a** When `MaxErrors` is reached, emit one final diagnostic: `SHP0905` ("Too many errors ({0}); further errors suppressed. Use `--max-errors` to increase the limit.")
- [ ] **2.6b** Add a boolean flag `_maxErrorsReported` to prevent emitting the truncation notice more than once
- [ ] **2.6c** Add a test: set `MaxErrors=3`, produce 5 errors, verify exactly 4 diagnostics (3 errors + 1 truncation notice)

**Guidance:**
- The truncation notice should be a warning, not an error, so it doesn't count toward the error limit.
- This also applies to the Lexer and Parser if they adopt `MaxErrors` (see 2.4c).

---

## Tier 3: Usability & Ergonomics

**Priority:** Address for real project usage. These are the features that make the compiler feel "production ready" to users.

---

### 3.1 Warning Suppression and Warning-as-Error

**What:** No way to suppress specific warnings or treat warnings as errors. For CI pipelines and real projects, this is table stakes.

**Why it matters:** Without `--warn-as-error`, CI can't enforce warning-free code. Without per-warning suppression, users are stuck with warnings they've deliberately chosen to ignore (e.g., unused variables in test scaffolding).

**Tasks:**

- [ ] **3.1a** Add `WarningsAsErrors` (bool) and `SuppressedWarnings` (list of string codes) to `CompilerOptions`
- [ ] **3.1b** Add `--warn-as-error` and `--nowarn=SHP0451,SHP0452` CLI flags
- [ ] **3.1c** In `DiagnosticBag.AddWarning()`, check if the warning code is in the suppressed list; if so, skip adding it
- [ ] **3.1d** In `DiagnosticBag.AddWarning()`, if `WarningsAsErrors` is true, promote the warning to an error (change severity to `Error`)
- [ ] **3.1e** Add `warn-as-error` and `nowarn` properties to `.spyproj` project file schema
- [ ] **3.1f** Add tests: suppress a specific warning, verify it's gone; enable warn-as-error, verify compilation fails on warning
- [ ] **3.1g** (Future consideration) Add inline suppression syntax (`# noqa: SHP0451` or similar) — this can be a separate issue

**Guidance:**
- The suppression should happen in `DiagnosticBag`, not in individual validators. This keeps the feature centralized and guarantees it works for all diagnostics.
- `--nowarn` should accept both short codes (`SHP0451`) and the full format (`SHP0451`). Don't accept bare numbers (`451`) — ambiguity with future diagnostic systems.
- Model after C#'s `/nowarn` and `/warnaserror` flags for familiarity.

---

### 3.2 Configurable Error Limits

**What:** TypeChecker's `MaxErrors` (100) and Parser's `MaxErrors` (25) are hardcoded constants.

**Why it matters:** Different workflows have different needs. IDE integrations want all errors. Quick edit-compile loops want to stop early. CI wants everything.

**Tasks:**

- [ ] **3.2a** Add `MaxParserErrors` and `MaxSemanticErrors` to `CompilerOptions` with defaults matching current values (25 and 100)
- [ ] **3.2b** Thread these through to `Parser` constructor and `TypeChecker` constructor
- [ ] **3.2c** Add `--max-errors=N` CLI flag (applies to both parser and semantic; or two separate flags if you prefer)
- [ ] **3.2d** Add a test that sets `MaxErrors=1` and verifies only one error is reported

**Guidance:**
- Use a single `--max-errors` flag that sets both parser and semantic limits. Power users who need separate control can use `CompilerOptions` directly via the library API. Don't over-expose knobs in the CLI.

---

### 3.3 Phase Labels in Build/Run Error Output

**What:** The `emit` commands label errors by phase ("Name resolution errors:", "Type checking errors:") but `build` and `run` do not. Users see an undifferentiated list of errors.

**Why it matters:** When a user gets 5 errors from different phases, knowing which are parse errors (fix syntax first) vs type errors (fix after syntax is clean) helps prioritize.

**Tasks:**

- [ ] **3.3a** In `Program.cs`, modify the diagnostic rendering for `build` and `run` commands to group errors by `CompilerPhase`
- [ ] **3.3b** Only add phase headers if there are errors from multiple phases (don't add noise for the common single-phase case)
- [ ] **3.3c** Add integration test verifying grouped output format

**Guidance:**
- Keep it simple. Something like:
  ```
  Parse errors:
    error[SHP0100]: ...

  Type errors:
    error[SHP0200]: ...
  ```
- Warnings should be listed separately after all errors, consistent with current behavior.

---

### 3.4 Fix `emit csharp` Skipping Import Resolution

**What:** `Program.cs:620-706` — The CLI's `EmitCSharp` method manually constructs the pipeline and skips Pass 1.5 (import resolution). `emit csharp` fails for any file with imports.

**Why it matters:** `emit csharp` is a debugging/inspection tool. If it doesn't work for files with imports, it's useless for multi-file projects, which is exactly when you need to inspect generated code.

**Tasks:**

- [ ] **3.4a** Replace the manual pipeline construction in `EmitCSharp` with a call to `Compiler.Compile()`, then extract `result.GeneratedCSharpCode`
- [ ] **3.4b** Keep the intermediate output (tokens, AST, per-phase errors) by also checking `result.Tokens`, `result.Module`, and `result.Diagnostics` for the verbose emit output
- [ ] **3.4c** Add a test: create a two-file project, run `emit csharp` on the entry point, verify it succeeds
- [ ] **3.4d** This also partially addresses Tier 4 item 4.1 (API unification)

**Guidance:**
- `CompilationResult` already exposes everything the emit path needs: `Tokens`, `Module` (AST), `GeneratedCSharpCode`, `Diagnostics`. There's no reason to bypass the facade.
- The emit-tokens and emit-ast commands can continue using `Lexer` and `Parser` directly since they don't need the full pipeline.

---

## Tier 4: LSP & Tooling Readiness

**Priority:** Address to prepare for LSP server, IDE integration, and advanced tooling. These don't build the tools themselves but remove blockers and establish the right contracts.

---

### 4.1 Unify Compilation Paths Through `Compiler.Compile()`

**What:** The CLI has two compilation paths: the `Compiler.Compile()` facade (used by build/run) and manual pipeline construction (used by emit). These can drift.

**Why it matters:** An LSP server will use `Compiler.Compile()`. If the emit path uses a different code path, bugs fixed in one won't be fixed in the other. One compilation entry point = one place to test = one place to fix.

**Tasks:**

- [ ] **4.1a** Complete 3.4 (fix `emit csharp`) which handles the biggest bypass
- [ ] **4.1b** Audit `Program.cs` for any other direct usage of compiler internals (`NameResolver`, `TypeChecker`, `RoslynEmitter`, etc.)
- [ ] **4.1c** If the `Compiler.Compile()` result doesn't expose something the emit commands need, add it to `CompilationResult` rather than bypassing the facade
- [ ] **4.1d** Document the intended API contract: `Compiler.Compile()` is THE entry point. Everything else is internal.

**Guidance:**
- The `CompilationResult` is already well-designed for tooling (exposes tokens, AST, semantic info, generated code). Resist the urge to add "convenience" methods that bypass it.
- This is a prerequisite for tightening the public API surface (4.3).

---

### 4.2 Document `SemanticInfo` Threading Model

**What:** `SemanticInfo` uses `Dictionary<K,V>` while `SemanticBinding` uses `ConcurrentDictionary`. No documentation explains the intended threading contract.

**Why it matters:** When an LSP server does per-file analysis in parallel, the thread-safety guarantees of each data structure matter. Without documentation, a contributor might assume `SemanticInfo` is thread-safe because `SemanticBinding` is.

**Tasks:**

- [ ] **4.2a** Add XML doc comment to `SemanticInfo` class stating: "This type is not thread-safe. Each compilation creates its own instance. For parallel per-file analysis, create one SemanticInfo per file."
- [ ] **4.2b** Add XML doc comment to `SemanticBinding` class explaining why it uses `ConcurrentDictionary`: "Thread-safe for cross-file symbol materialization in project compilation."
- [ ] **4.2c** Consider whether `SemanticInfo` should be per-file in `ProjectCompiler`. If it already is, document that. If it's shared, file an issue for the LSP work.

**Guidance:**
- Don't change the implementation here — just document the contract. The actual threading work is an LSP task.
- "Per-file SemanticInfo, shared SymbolTable/SemanticBinding" is the likely correct model for parallel compilation.

---

### 4.3 Tighten Public API Surface

**What:** ~80+ public types in `Sharpy.Compiler`, many are implementation details (`ControlFlowGraphBuilder`, `OverloadIndex`, `NameMangler`, `TypeMapper`, etc.). Only 2 types are `internal`.

**Why it matters:** A large public API surface is a maintenance burden. Every public type is a compatibility promise. External tools (LSP, build systems) should use a curated API, not reach into internals.

**Tasks:**

- [ ] **4.3a** Complete 4.1 first (unify compilation paths) — this is the blocker
- [ ] **4.3b** Mark the following type groups as `internal`:
  - `Analysis/ControlFlow/` — `ControlFlowGraphBuilder`, `ControlFlowGraph`, `BasicBlock`, `ControlFlowEdge`, `ControlFlowAnalysis`
  - `Discovery/Caching/` — `OverloadIndex`, `ModuleOverloads`, `DiscoveredTypeInfo`, `FunctionSignature`, `ParameterSignature`, `TypeSignature`, `OverloadIndexCache`
  - `CodeGen/` — `CodeGenContext`, `RoslynEmitter`, `NameMangler`, `TypeMapper` (codegen), `CodeValidator`
  - `Semantic/` — `NameResolver`, `InheritanceResolver`, `TypeResolver`, `ExecutionOrderAnalyzer`, `CodeGenInfoComputer`
  - `Semantic/Validation/` — Individual validators (keep `ValidationPipeline` public if needed, but individual validators should be internal)
- [ ] **4.3c** Add `[assembly: InternalsVisibleTo("Sharpy.Cli")]` if the CLI needs any of the newly-internal types
- [ ] **4.3d** Verify all tests still compile (test project already has `InternalsVisibleTo`)
- [ ] **4.3e** Keep the following public (for LSP/tooling):
  - `Compiler`, `CompilationResult`, `ProjectCompilationResult`, `CompilerOptions`, `ProjectConfig`
  - `CompilerServices`, `CompilerServicesBuilder`, service interfaces
  - `SemanticInfo`, `SemanticBinding`, `SemanticType` hierarchy, `Symbol` hierarchy
  - `DiagnosticBag`, `CompilerDiagnostic`, `DiagnosticCodes`, `DiagnosticRenderer`, `CompilationMetrics`
  - `Lexer`, `Token`, `TokenType`, `Parser`, AST nodes
  - `SourceText`, `TextSpan`, `ILocatable`
  - `ICompilerLogger`, `CompilerLogLevel`, loggers

**Guidance:**
- Do this in one batch commit to avoid churn. The change is mechanical (add `internal` keyword) but touches many files.
- If you're unsure whether a type should be public or internal, ask: "Would an LSP server or build tool need to reference this type directly?" If no, make it internal.
- The `InternalsVisibleTo` for the CLI is a temporary measure. Long-term, the CLI should only use the public API (which is what item 4.1 enables).

---

### 4.4 Add Generated C# Snapshot Tests

**What:** No golden-file tests for the generated C# code. Only execution output is tested via `.expected` files.

**Why it matters:** When refactoring the emitter, you need to know if the generated code changed — even if the output is the same. Snapshot tests catch changes in generated code quality (unnecessary allocations, wrong visibility modifiers, missing attributes) that don't affect behavior.

**Tasks:**

- [ ] **4.4a** Create a new test fixture pattern: `.spy` + `.expected.cs` (generated C# snapshot)
- [ ] **4.4b** Update `FileBasedIntegrationTests` to recognize `.expected.cs` files and compare generated C# against them
- [ ] **4.4c** Add snapshots for 10-15 representative fixtures covering: basic functions, classes, inheritance, generics, module-level code, operators, comprehensions
- [ ] **4.4d** Add a test helper to regenerate snapshots: `dotnet test --filter "UpdateSnapshots"` or similar
- [ ] **4.4e** Document the snapshot update workflow in the test section of CLAUDE.md

**Guidance:**
- Snapshot tests are inherently brittle (any formatting change breaks them). Use them selectively for important code patterns, not for every fixture. 10-15 is the right number.
- The snapshot comparison should normalize whitespace (Roslyn formatting may vary between versions). Consider using `Microsoft.CodeAnalysis.Formatting` to format both sides before comparison.
- Store snapshots alongside the `.spy` and `.expected` files in `TestFixtures/`.

---

### 4.5 Minor Access Control Fixes

**What:** `CompilationUnit.GeneratedCSharp` has `public set` while all other progressive properties use `internal set`. `CompilerServices.SemanticBinding` has `public set` while `ProjectModel.SemanticBinding` has `internal set`.

**Tasks:**

- [ ] **4.5a** Change `CompilationUnit.GeneratedCSharp` setter to `internal set`
- [ ] **4.5b** Change `CompilerServices.SemanticBinding` setter to `internal set`
- [ ] **4.5c** Verify compilation — if external code needs these setters, it's a design issue to address

**Guidance:**
- These are one-line changes. The inconsistency suggests these were overlooked during a broader `internal set` sweep.

---

## Tier 5: Efficiency & Contributability

**Priority:** Longer-term quality of life. These improve the development experience and compiler performance for multi-file projects.

---

### 5.1 Wire Up Incremental Compilation

**What:** `ProjectCompiler` recompiles all files every time, despite having content hashing (`CompilationUnit.ContentHash`), dependency tracking (`DependencyGraph.GetAffectedFiles()`), and parallelizable groups (`DependencyGraph.GetParallelizableGroups()`) already implemented.

**Why it matters:** For a 20-file project, recompiling all files on every save is noticeable. Incremental compilation is the most impactful performance improvement for real project usage.

**Tasks:**

- [ ] **5.1a** Add a `--incremental` flag to the `build` CLI command (default off initially)
- [ ] **5.1b** In `ProjectCompiler`, before compiling each file, check `unit.IsStale(cachedHash)`. Skip unchanged files whose dependencies are also unchanged.
- [ ] **5.1c** Use `DependencyGraph.GetAffectedFiles(changedFile)` to determine which downstream files need recompilation when a dependency changes
- [ ] **5.1d** Cache `SymbolTable` entries from unchanged files so they're available for downstream type checking
- [ ] **5.1e** Add tests: compile project, modify one file, recompile — verify only affected files are recompiled (check `CompilationMetrics`)
- [ ] **5.1f** Add a `--clean` flag that forces full recompilation (already exists in CLI, verify it clears cached state)

**Guidance:**
- Start with the simplest correct implementation: if ANY dependency of a file changed, recompile it. Don't try to be clever about partial reuse of semantic info — that's a much harder problem.
- The existing `DependencyGraph.GetBuildOrder()` already gives topological order. Process files in that order, skipping those that are clean.
- Be conservative about cache invalidation. If in doubt, recompile. False positives (unnecessary recompilation) are annoying; false negatives (stale cache used) are bugs.
- This is a larger feature — consider filing a GitHub issue and breaking it into sub-PRs.

---

### 5.2 Legacy Dead Code Cleanup

**What:** Several unused code artifacts remain in the codebase.

**Tasks:**

- [ ] **5.2a** Remove `CodeGenContext._indentLevel`, `Indent()`, `Dedent()`, `GetIndent()` — legacy string-based codegen, confirmed unused
- [ ] **5.2b** Either repurpose `CodeGenException` (see 2.1e) or remove it if still unused after 2.1
- [ ] **5.2c** Remove unused `hasTrailingComma` variable in `Parser.Types.cs:320` (also fixes build warning)
- [ ] **5.2d** Consider moving `Expression.Future.cs`, `Statement.Future.cs`, `Pattern.cs` (419 lines of v0.2.x placeholder AST nodes) to a `future/` branch or behind a `#if FUTURE_FEATURES` conditional. If keeping them, add a prominent comment: "These types are defined for forward compatibility but have NO parser, semantic, or codegen support. Do not reference them."

**Guidance:**
- For 5.2d, keeping the placeholder types in-tree is fine IF they don't confuse contributors. The current `Future` suffix in the filenames helps. If you keep them, ensure the `_ => null` / `_ => Unknown` fallbacks in the TypeChecker and RoslynEmitter don't silently pass them through (which items 1.1 and 1.2 address).
- Do 5.2a-c in one commit since they're trivial cleanups.

---

### 5.3 Add Benchmark Suite

**What:** Only `CachedDiscoveryPerformanceTests` exists. No systematic compilation benchmarks.

**Why it matters:** Without baselines, you can't detect performance regressions. Without benchmarks, optimization work is guesswork.

**Tasks:**

- [ ] **5.3a** Add a `Sharpy.Compiler.Benchmarks` project using BenchmarkDotNet
- [ ] **5.3b** Create benchmark scenarios:
  - **Throughput**: compile a representative 500-line program, measure lines/sec
  - **Memory**: measure peak memory allocation during compilation
  - **Lexer isolation**: tokenize a 1000-line file
  - **Parser isolation**: parse a 500-line file (pre-tokenized)
  - **Full pipeline**: lex → parse → semantic → codegen for a 200-line program with classes, functions, and imports
- [ ] **5.3c** Add a corpus directory `benchmarks/corpus/` with representative `.spy` files
- [ ] **5.3d** Add a CI job that runs benchmarks on PRs and comments with a comparison (BenchmarkDotNet has this support)
- [ ] **5.3e** Document baseline numbers in a `benchmarks/BASELINE.md` file

**Guidance:**
- BenchmarkDotNet is the standard .NET benchmarking tool. Use `[MemoryDiagnoser]` for allocation tracking.
- Don't benchmark test execution (too noisy due to process spawning). Benchmark compilation phases only.
- The corpus should be committed to the repo so benchmarks are reproducible.

---

### 5.4 Compiler Determinism Test

> **Priority note:** Consider promoting this to Tier 2. Determinism is a hard prerequisite for incremental compilation (5.1) — if the compiler isn't deterministic, content-hash-based caching will always miss. Implementing this test early catches non-determinism before it becomes entrenched.

**What:** No test verifying that the same input always produces the same generated C# output.

**Why it matters:** Determinism is a prerequisite for content-based caching (incremental compilation). If the compiler produces different C# for the same input, the content hash will change and the cache will always miss.

**Tasks:**

- [ ] **5.4a** Add a test that compiles a representative program 3 times and asserts the generated C# is byte-identical each time
- [ ] **5.4b** Include programs with: classes, functions, imports, type aliases, comprehensions, f-strings — anything that exercises different emitter paths
- [ ] **5.4c** If any non-determinism is found, fix it (common causes: dictionary iteration order, hash-based ordering, timestamps in output)

**Guidance:**
- This is a small test but catches an important class of bugs. Run it in CI.
- If using `Dictionary<K,V>` iteration order in the emitter, consider switching to `SortedDictionary` or sorting before iteration.

---

### 5.5 Diagnostic Deduplication Cleanup

**What:** `TypeChecker.cs:216-244` — Elaborate deduplication between TypeChecker (SHP0222) and OperatorValidator (SHP0402) using position+message and position+code matching. Fragile and hard to maintain.

**Why it matters:** The dedup logic is a maintenance hazard. If diagnostic codes or positions shift, it could either miss duplicates or suppress valid diagnostics. The right fix is to prevent the duplication at the source.

**Tasks:**

- [ ] **5.5a** Determine whether the OperatorValidator and TypeChecker should both report the same error, or whether one should defer to the other. The `ValidationPipeline` order (TypeChecker runs first, OperatorValidator at Order 500) suggests the TypeChecker should handle it and the OperatorValidator should skip already-reported errors.
- [ ] **5.5b** Have the OperatorValidator check `SemanticInfo` for expressions that already have errors before adding new ones, or pass the diagnostic bag so it can check for existing diagnostics at the same span
- [ ] **5.5c** Remove the deduplication code in `TypeChecker.CheckModule` once the root cause is addressed
- [ ] **5.5d** Add a test that an operator error is reported exactly once (not zero, not two)

**Guidance:**
- The principle is: each error should be reported by exactly one component. The TypeChecker reports type-level errors during traversal; validators report structural/pattern errors post-traversal. If there's overlap, the later component should check whether the error was already reported.
- A simple approach: `OperatorValidator` checks `diagnostics.GetErrors().Any(e => e.Span == currentSpan)` before adding a new error. This is O(n) per check but the error list is typically small.

---

### 5.6 Extract Dunder Method Name Constants

**What:** ~227 scattered string literals like `"__init__"`, `"__len__"`, `"__iter__"`, `"__str__"`, `"__add__"`, etc. across semantic analysis, validators, and code generation. Each is a separate typo risk.

**Why it matters:** A typo in a dunder name string (e.g., `"__inti__"` instead of `"__init__"`) creates a silent bug — the method won't be recognized as special, and no error is emitted. Extracting these to a constants class provides compile-time safety and a single source of truth.

**Tasks:**

- [ ] **5.6a** Create `Semantic/DunderNames.cs` (or `Constants/DunderNames.cs`) with `internal static class DunderNames` containing `public const string Init = "__init__";`, `public const string Len = "__len__";`, etc.
- [ ] **5.6b** Replace all scattered string literals with references to `DunderNames.*` across the codebase. Use `replace_all` or a bulk search-and-replace to catch all instances.
- [ ] **5.6c** Verify all tests pass — this is a mechanical refactor with no behavior change
- [ ] **5.6d** Add a code review note or analyzer rule that prohibits new dunder name string literals outside `DunderNames.cs`

**Guidance:**
- This is a tedious but safe refactor. Do it in a single commit to avoid churn.
- Include names used in `NameMangler`, `TypeChecker`, `SignatureValidator`, `ProtocolValidator`, `OperatorValidator`, `BuiltinRegistry`, and `RoslynEmitter`.
- Don't go overboard extracting every string — focus on dunder names (`__xxx__`) which have the highest typo risk and semantic significance.

---

## Appendix A: Items Explicitly NOT Recommended

| Item | Why Not |
|------|---------|
| Property-based testing (FsCheck) | Fuzz tests + 315 file fixtures provide similar crash-safety coverage. PBT is high effort for incremental gain here. |
| Start LSP server | Focus on items 4.1-4.3 which make the compiler LSP-ready without building one. |
| Refactor large files | TypeChecker.Expressions (2,031 lines) and RoslynEmitter.Expressions (1,598 lines) are already in well-named partials. Further splitting adds indirection. |
| Feature flags | The compiler is in feature-building phase, not optimization phase. Feature flags add complexity without current benefit. |
| Async compilation | CancellationToken is already threaded. Async compilation needs a use case (LSP) to justify the complexity. |

---

## Appendix B: Reference — Codebase Axioms

From the language specification and CLAUDE.md:

1. **Axiom 1 (.NET):** Generated code must be idiomatic .NET. Sharpy is a .NET language first.
2. **Axiom 2 (Python Syntax):** Use Python syntax and semantics where they don't conflict with Axiom 1.
3. **Axiom 3 (Type Safety):** All types resolved at compile time. No runtime type checking.

**Precedence:** Axiom 1 > Axiom 3 > Axiom 2.

When implementing these items, always verify Python behavior (`python3 -c "..."`) before assuming Python semantics, and always prefer the .NET-idiomatic approach when there's a conflict.

---

## Appendix C: File Quick Reference

| Component | Key Files |
|-----------|-----------|
| Diagnostic codes | `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` |
| Diagnostic explanations | `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs` |
| Diagnostic bag | `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs` |
| Diagnostic renderer | `src/Sharpy.Compiler/Diagnostics/DiagnosticRenderer.cs` |
| Compiler facade | `src/Sharpy.Compiler/Compiler.cs` |
| Project compiler | `src/Sharpy.Compiler/Project/ProjectCompiler.cs` |
| Compiler options | `src/Sharpy.Compiler/Compiler.cs` (line 748) |
| RoslynEmitter (entry) | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` |
| RoslynEmitter (expressions) | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` |
| RoslynEmitter (statements) | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` |
| RoslynEmitter (class members) | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` |
| RoslynEmitter (module class) | `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` |
| CodeGenContext | `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs` |
| CodeGenException (unused) | `src/Sharpy.Compiler/CodeGen/CodeGenException.cs` |
| TypeChecker (entry) | `src/Sharpy.Compiler/Semantic/TypeChecker.cs` |
| TypeChecker (expressions) | `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` |
| TypeChecker (utilities) | `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` |
| SemanticInfo | `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` |
| SemanticBinding | `src/Sharpy.Compiler/Semantic/SemanticBinding.cs` |
| SemanticType hierarchy | `src/Sharpy.Compiler/Semantic/SemanticType.cs` |
| Symbol hierarchy | `src/Sharpy.Compiler/Semantic/Symbol.cs` |
| Lexer | `src/Sharpy.Compiler/Lexer/Lexer.cs` |
| Parser (entry) | `src/Sharpy.Compiler/Parser/Parser.cs` |
| CompilationUnit | `src/Sharpy.Compiler/Model/CompilationUnit.cs` |
| CompilerServices | `src/Sharpy.Compiler/Services/CompilerServices.cs` |
| CLI | `src/Sharpy.Cli/Program.cs` |
| Test base | `src/Sharpy.Compiler.Tests/Integration/IntegrationTestBase.cs` |
| File-based tests | `src/Sharpy.Compiler.Tests/Integration/FileBasedIntegrationTests.cs` |
| Test fixtures | `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` |
| Fuzz tests | `src/Sharpy.Compiler.Tests/Fuzz/FuzzTests.cs` |

---

## Appendix D: Recommended Implementation Order

Items ordered by impact-to-effort ratio, accounting for dependencies. Earlier items unblock later ones.

| # | Item | Rationale |
|---|------|-----------|
| 1 | **1.1** Silent statement drops | Highest severity: wrong output with no indication. Small fix (4 call sites). |
| 2 | **1.2** UnknownType pass-through | Second highest severity: same class of silent-wrong-output bug. |
| 3 | **1.5** Fix build warnings + `TreatWarningsAsErrors` | 1.5h alone prevents entire categories of future bugs. High leverage. |
| 4 | **2.1** NotImplementedException → diagnostics | Biggest UX improvement. Uses infrastructure proven by #1. |
| 5 | **2.6** MaxErrors truncation notice | Small fix, disproportionate UX value. |
| 6 | **2.2** SemanticBinding freeze assertions | Protects the most critical architectural invariant. Small change. |
| 7 | **2.4** Lexer error recovery | High UX impact for multi-error workflows. Medium complexity. |
| 8 | **1.3** Comparison chain re-evaluation | Correctness bug with spec violation. Moderate complexity. |
| 9 | **5.4** Compiler determinism test | Small test, prerequisite for incremental compilation (5.1). |
| 10 | **1.4** IsFloatExpression heuristic | Correctness bug, but narrower impact than 1.3. |

Items 11–25 (remaining) can be addressed in tier order after the top 10. Items 3.4 and 4.1 (unify compilation paths) are high-value prerequisites for LSP work and should be prioritized when LSP becomes a focus.
