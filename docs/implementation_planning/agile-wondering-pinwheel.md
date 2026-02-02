# Sharpy Compiler Hardening: Phased Implementation Guide

## Purpose of This Document

This document describes a series of improvements to the Sharpy compiler aimed at:
- **Robustness:** Making bugs obvious at compile time rather than hiding until runtime
- **LSP readiness:** Laying the foundation for a language server without building one yet
- **Usability:** Improving the experience of writing Sharpy code (better errors, more warnings)
- **Debuggability:** Making both compiler development and compiled program debugging easier
- **Contributability:** Reducing tech debt so new contributors don't trip over legacy decisions

Each phase is self-contained and shippable independently. Phases are ordered so earlier work reduces the cost of later work.

---

## Guiding Principles

When implementing any of these items, apply these principles:

1. **The Three Axioms still govern all decisions.** .NET > Type Safety > Python Syntax. If any improvement conflicts with these, the axioms win.

2. **Never break existing tests to make new tests pass.** Fix the implementation, not the expectations.

3. **Prefer surgical changes over rewrites.** Each item below should be achievable without touching unrelated code. If you find yourself changing 20 files for a "small" improvement, stop and reconsider the approach.

4. **When in doubt, add a diagnostic.** If you encounter an ambiguous situation (should this be an error? a warning? silently accepted?), the answer is almost always "emit a diagnostic." It's much easier to downgrade a warning later than to add one that was missing.

5. **Keep the existing architecture.** The pipeline is `Lexer -> Parser -> NameResolver -> ImportResolver -> InheritanceResolver -> TypeChecker -> ValidationPipeline -> RoslynEmitter`. Don't add new phases or reorder existing ones unless explicitly stated.

6. **Tests first.** For each item, write the test that demonstrates the problem or expected behavior before writing the fix.

---

## Phase 1: Diagnostic Infrastructure

**Goal:** Make the diagnostic system capable of carrying full location information, and harden the most dangerous assertion gaps.

**Why this first:** Every subsequent phase emits diagnostics. Getting the diagnostic plumbing right means all future work benefits automatically. This is also the #1 prerequisite for LSP.

### 1.1: Add `TextSpan` to `CompilerDiagnostic`

**Problem:** `CompilerDiagnostic` carries `int? Line` and `int? Column` -- a point, not a range. When you tell a user "error at line 3, column 5," they don't know if the problem is the single character at that position, the whole word, or the whole expression. Every modern language tool (LSP red squiggles, CLI error rendering, IDE integration) needs a *range*.

**The infrastructure already exists:** AST nodes have `TextSpan? Span`. Tokens have `Position` + length info. The `TextSpan` type in `Text/TextSpan.cs` supports start/length, contains, overlap, union -- everything you need. It's just not threaded into diagnostics.

**Direction:** The `CompilerDiagnostic` record gets a new `TextSpan? Span` property. The `DiagnosticBag.AddError` / `AddWarning` methods get new overloads that accept a `TextSpan`. Existing call sites continue to work (span is optional/nullable). Over time, call sites are updated to pass spans when the triggering AST node is available.

**Decision guidance:** If you're unsure whether to add a span to a particular diagnostic call site, check if the code has access to an AST `Node` or `Expression` at that point. If it does, pass `node.Span`. If it doesn't (e.g., compiler infrastructure errors), leave span as null.

**Tasks:**
- [ ] Add `TextSpan? Span { get; init; }` to the `CompilerDiagnostic` record in `Diagnostics/DiagnosticBag.cs`
- [ ] Add overloads to `DiagnosticBag.AddError` and `AddWarning` that accept `TextSpan? span`
- [ ] Add a convenience overload that accepts an `ILocatable` (extracts span automatically)
- [ ] Update `CompilerDiagnostic.ToString()` to include span info when available
- [ ] Update 10-15 high-traffic diagnostic call sites in `TypeChecker` to pass spans (pick the most common error types: type mismatch, undefined variable, wrong argument count)
- [ ] Add tests verifying that diagnostics produced by the updated call sites carry correct spans
- [ ] Leave remaining call sites for gradual migration (they still work, just without spans)

### 1.2: Promote Critical Assertions to Release Builds

**Problem:** Several important safety checks are `[Conditional("DEBUG")]` in `Compiler.cs` and `SemanticBinding.cs`. This means:
- `AssertGeneratedCSharpParses` -- In Release, if codegen produces invalid C#, the user gets a cryptic Roslyn compilation error instead of "internal compiler error, please report this."
- Freeze-point violations -- In Release, if a phase writes to SemanticBinding after the freeze point, the data silently corrupts.
- `AssertStatementsHaveSpans` -- Missing span data passes silently.

**Direction:** Not all assertions need to be always-on. The goal is to promote the ones that catch *data corruption* or *security-relevant* issues. Performance-only assertions can stay DEBUG.

**Decision guidance:** Ask "if this assertion fails in production, does the user get a confusing error or silent wrong behavior?" If yes, promote it. If the assertion is just a development-time sanity check (like "at least one expression type was recorded"), leave it as DEBUG.

**Tasks:**
- [ ] Change `AssertGeneratedCSharpParses` in `Compiler.cs` from `[Conditional("DEBUG")]` to always-on. On failure, add a diagnostic: `SHP0599: Internal error: generated C# contains syntax errors. This is a compiler bug -- please report it.` (Use a new code in the CodeGen range.)
- [ ] Change `SemanticBinding` freeze violations from `Debug.Assert` to a logged warning that always runs. Use `_logger.LogWarning(...)` instead of `Debug.Assert(...)` for the freeze checks.
- [ ] Keep `AssertStatementsHaveSpans`, `AssertAllSymbolsHaveNames`, `AssertNoDuplicateTypeNames` as DEBUG-only (these are development aids, not production safeguards)
- [ ] Add a test that verifies the generated-C#-parse check catches a deliberately malformed output (mock/override a small codegen path to produce bad C#, verify the diagnostic is emitted)

### 1.3: Audit Symbol Record Equality

**Problem:** `Symbol`, `VariableSymbol`, `TypeSymbol`, `FunctionSymbol` are C# `record` types. Records derive `Equals`/`GetHashCode` from all their properties. But several properties are mutated after creation via `internal set` (`Type`, `BaseType`, `CodeGenInfo`). If a symbol is used as a dictionary key with default equality, then mutated, the dictionary entry becomes unreachable.

`SemanticInfo` and `SemanticBinding` correctly use `ReferenceEqualityComparer`, but any *other* code that puts symbols in dictionaries or hash sets is at risk.

**Direction:** The safest fix is to override `Equals`/`GetHashCode` on the base `Symbol` record to use only the immutable identity fields (`Name`, `Kind`, `DeclarationLine`, `DeclarationColumn`). This preserves value semantics for comparison purposes while making symbols safe to use in any collection regardless of comparer.

Alternatively, if value equality on symbols is never actually needed (check usage), consider converting `Symbol` and its subtypes to `class` instead of `record`. Records are the right choice for AST nodes (which are truly immutable), but symbols are mutable by design.

**Decision guidance:** Search for all places where symbols are used in collections. If you find cases using default equality, this is a real bug. If all cases already use `ReferenceEqualityComparer`, the override is preventive (still worthwhile -- it makes the safe behavior the default so future code can't get it wrong).

**Tasks:**
- [ ] Grep the entire `src/Sharpy.Compiler/` for `Dictionary<.*Symbol` and `HashSet<.*Symbol` (and `ImmutableDictionary`, `ConcurrentDictionary`, etc.). Document which ones use `ReferenceEqualityComparer` and which use default equality.
- [ ] For any collections using default equality: determine if this is a bug (mutated symbols as keys) or safe (symbols used as values only, or never mutated).
- [ ] Override `Equals` and `GetHashCode` on the base `Symbol` record to use identity-based equality (`ReferenceEquals`), making symbols safe by default. Alternatively, override using only immutable fields (`Name + Kind + DeclarationLine`).
- [ ] If choosing the `ReferenceEquals` approach: note that this changes the behavior of `==` for symbols. Search for any code that relies on `symbol1 == symbol2` meaning "same name and kind" and update it.
- [ ] Add a test: create two `VariableSymbol` records with same name, mutate one's `Type`, verify they're still findable in a `Dictionary<Symbol, ...>` without `ReferenceEqualityComparer`.

---

## Phase 2: Parser Error Recovery

**Goal:** Let the parser report multiple errors per compilation instead of stopping at the first few.

**Why now:** This is the single biggest user experience improvement. Currently, one syntax error means the user sees nothing about the remaining 95% of their file. After this phase, the parser produces a "best effort" AST even in the presence of errors, and the user sees all the problems at once.

### 2.1: Add Synchronization Points to the Parser

**Problem:** When the parser encounters a syntax error, it throws `ParserAbortException` and unwinds the call stack. There's a 5-error limit (`MaxErrors = 5`), but in practice most errors cascade from the first one because there's no recovery. The parser doesn't attempt to skip past the bad code and resume at the next statement.

**How parser error recovery works (background):** The standard technique is "synchronization" or "panic mode recovery." When an error is detected:
1. Record the diagnostic.
2. Skip tokens until you reach a "synchronization token" -- a token that reliably starts a new construct. For Sharpy, good synchronization tokens are: `NEWLINE` at the same or lower indentation level, `DEDENT`, `def`, `class`, `if`, `for`, `while`, `return`, `import`, `from`, or `EOF`.
3. Resume parsing from that token as if starting a new statement.

**Direction:** Add a `Synchronize()` method to the parser that skips tokens until it hits a synchronization point. Replace `throw new ParserAbortException()` at statement-level parse sites with calls to `Synchronize()` and continue the parse loop. Keep `ParserAbortException` for truly unrecoverable situations (e.g., missing EOF, corrupted token stream).

**Decision guidance:**
- Only recover at **statement boundaries** initially. Don't try to recover mid-expression -- that's much harder and the payoff is smaller.
- If a definition (`def`, `class`) fails to parse, synchronize to the next top-level definition (indentation level 0 or 1).
- If a statement inside a block fails, synchronize to the next statement at the same indentation level.
- When in doubt about whether to skip a token, skip it. Over-skipping is better than infinite loops.
- The recovered AST may have gaps (missing statements). That's fine -- the semantic phases should handle null/missing gracefully.

**Tasks:**
- [ ] Add a `Synchronize()` method to `Parser.cs` that skips tokens until it finds a synchronization point (NEWLINE/DEDENT at appropriate indentation, or a statement-starting keyword)
- [ ] In `Parser.Statements.cs` `ParseStatement()`, wrap the main switch in a try/catch for `ParserAbortException`. On catch, call `Synchronize()` and continue the loop.
- [ ] In `Parser.Definitions.cs`, add similar recovery for `ParseClassDefinition()` and `ParseFunctionDefinition()`
- [ ] Increase `MaxErrors` from 5 to 20-30 (with recovery, errors are more independent and less likely to cascade)
- [ ] Add file-based test fixtures: `.spy` files with multiple deliberate syntax errors, `.error` files expecting multiple distinct error messages
- [ ] Verify that existing tests still pass (recovery should not affect valid input parsing)
- [ ] Verify that semantic analysis handles partially-parsed modules gracefully (it should already bail on the first semantic error, but check)

---

## Phase 3: Compiler Warnings

**Goal:** Have the compiler emit warnings for common issues that aren't errors but indicate probable bugs.

**Why now:** The diagnostic infrastructure from Phase 1 supports warnings, the CFG from the existing `ControlFlowValidator` can detect unreachable code, and adding warnings exercises the warning path of the diagnostic system (which is currently nearly unused, meaning any bug in warning handling has been latent).

### 3.1: Unreachable Code Warning

**Problem:** Code after `return`, `raise`, `break`, or `continue` is dead code. The CFG's `FindUnreachableBlocks()` already detects this, but nothing emits a warning.

**Direction:** Add a new validator or extend `ControlFlowValidator` to emit `SHP04xx` warnings for unreachable basic blocks. Only warn about blocks that contain user-written code (not synthetic blocks added by the CFG builder).

**Tasks:**
- [ ] In `ControlFlowValidator` (or a new `UnreachableCodeWarningValidator`), after building the CFG, call `FindUnreachableBlocks()`. For each unreachable block that contains statements, emit a warning with the line number of the first statement.
- [ ] Choose a diagnostic code (e.g., `SHP0450: Unreachable code detected`)
- [ ] Add to `DiagnosticCodes.cs`
- [ ] Add 3-5 file-based test fixtures: unreachable after return, after raise, after break, and a case where code IS reachable (if/else where both branches return but code follows -- this should warn)
- [ ] Add a negative test: code after `if ... return` where the else branch falls through -- this should NOT warn

### 3.2: Unused Variable Warning

**Problem:** Variables that are assigned but never read are almost always bugs (typos in variable names, leftover debugging code, etc.).

**Direction:** Track variable reads in the TypeChecker. After type checking a function/module, compare defined variables against read variables. Emit a warning for any variable that was defined but never read.

**Decision guidance:**
- Don't warn about parameters (they're part of the function's API)
- Don't warn about variables starting with `_` (Python convention for intentionally unused)
- Don't warn about loop variables (for/comprehension iteration variables)
- Warn about variables assigned in `except` clauses that are never used

**Tasks:**
- [ ] Add a `HashSet<string> _readVariables` to TypeChecker (or a new analysis pass)
- [ ] When an `Identifier` is visited for reading (not as an assignment target), add it to `_readVariables`
- [ ] At the end of function/module scope, compare defined variables against read variables
- [ ] Emit `SHP0451: Local variable 'x' is assigned but never used` for unused variables
- [ ] Add to `DiagnosticCodes.cs`
- [ ] Skip variables matching `_` prefix convention
- [ ] Add 5+ test fixtures covering: unused local, used local, underscore-prefixed, parameter (no warn), loop variable (no warn)

### 3.3: Unused Import Warning

**Problem:** Imports that are never referenced clutter the code and slow down compilation (the import resolver does work for nothing).

**Tasks:**
- [ ] Track which imported symbols are actually referenced during type checking
- [ ] After type checking, emit `SHP0452: Imported name 'x' is never used` for unreferenced imports
- [ ] Add test fixtures

---

## Phase 4: Usability & Developer Experience

**Goal:** Make error output professional-grade and make compiled programs debuggable.

### 4.1: Rich Error Rendering in CLI

**Problem:** Error messages look like `file.spy(3,5): error SHP0201: Type 'str' is not assignable to 'int'`. This is functional but not helpful. Modern compilers show the source line with a visual indicator.

**Direction:** Build a `DiagnosticRenderer` that takes a `CompilerDiagnostic` + `SourceText` and produces formatted output:
```
error[SHP0201]: Type 'str' is not assignable to 'int'
  --> file.spy:3:5
   |
 3 |     x: int = "hello"
   |              ^^^^^^^ expected 'int', found 'str'
   |
```

**Prerequisite:** Phase 1.1 (TextSpan on diagnostics) must be done first, at least for the call sites you want to render nicely.

**Decision guidance:**
- If a diagnostic has a `Span`, render the source context with underline.
- If a diagnostic only has `Line`/`Column` (no span yet), render just the line with a caret (`^`) at the column.
- If a diagnostic has neither, render just the message (infrastructure errors).
- Color output is nice but optional. Check if stdout is a terminal before using ANSI codes.

**Tasks:**
- [ ] Create `DiagnosticRenderer` class (in `Sharpy.Compiler` or `Sharpy.Cli`)
- [ ] Accept `CompilerDiagnostic` + `string sourceText` (or `SourceText`)
- [ ] Render: header line (severity + code + message), location line (file:line:col), source line with underline
- [ ] Handle edge cases: multi-line spans (show first line only), missing source text, column past end of line
- [ ] Integrate into `Sharpy.Cli/Program.cs` error output path
- [ ] Add unit tests for the renderer (input diagnostic + source -> expected string output)

### 4.2: `#line` Directives for Source Mapping

**Problem:** When a compiled Sharpy program throws a runtime exception, the stack trace shows generated C# file/line numbers. Users have to read generated C# to debug their Sharpy code.

**Direction:** Emit `#line N "file.spy"` directives in the generated C# before each statement. Roslyn provides `SyntaxFactory.LineDirectiveTrivia()` for this. When the compiled program runs, .NET's runtime uses these directives to show `.spy` file names and line numbers in stack traces.

**Decision guidance:**
- Only emit `#line` for statements, not for every expression (too noisy).
- Use the `LineStart` from the AST node as the `#line` number.
- Use the original `.spy` file path as the filename.
- If a statement doesn't have span info (shouldn't happen after Phase 1, but defensive), skip the directive.

**Tasks:**
- [ ] In `RoslynEmitter.Statements.cs`, before emitting each statement's C# syntax, prepend a `#line` directive trivia using the statement's `LineStart` and source file path from `CodeGenContext.SourceFilePath`
- [ ] Add a helper method `CreateLineDirective(int line, string filePath)` that returns the trivia
- [ ] Add an integration test: compile a `.spy` file, execute it with a deliberate runtime error (e.g., divide by zero), capture the stack trace, verify it references the `.spy` file and correct line number
- [ ] Add a way to disable `#line` directives (e.g., a flag on `CodeGenContext`) for when users want to inspect the raw generated C#

---

## Phase 5: Tech Debt Cleanup

**Goal:** Remove dead code, extract misplaced logic, and convert TODOs to tracked issues.

### 5.1: Remove V2 Validators

**Problem:** Both `ControlFlowValidatorV2` (AST-walking) and `ControlFlowValidatorV3` (CFG-based) exist. V3 is the default. Having two implementations of the same analysis is a maintenance burden that causes confusion.

**Decision guidance:** If all tests pass with V3 only (they should -- V3 is already the default), remove V2. If there are edge cases where V2 catches something V3 doesn't, fix V3 rather than keeping V2.

**Tasks:**
- [x] Run full test suite confirming V3 is the default and all tests pass
- [x] Search for any references to V2 validators (imports, factory registrations, tests)
- [x] Remove `ControlFlowValidatorV2.cs` and any V2-specific tests
- [x] Update `ValidationPipelineFactory` if it still references V2
- [x] If other V2 validators exist (e.g., `ModuleLevelValidatorV2`, `DecoratorValidatorV2`), determine if corresponding V3s exist. If not, the "V2" suffix is misleading -- rename them to drop the version suffix.

### 5.2: Extract Import Logic from Compiler.cs

**Problem:** `Compiler.cs` lines 182-310 contain 130 lines of detailed import resolution logic (building nested module structures, handling aliased imports, handling `from ... import *`, etc.). This is semantic-layer logic embedded in the compiler driver. It makes `Compiler.Compile()` hard to read and makes the import logic hard to test independently.

**Direction:** Move this logic into `ImportResolver` (or a new class). The compiler driver should call something like:
```csharp
importResolver.ResolveAllImports(module, symbolTable, currentDir);
```

**Tasks:**
- [x] Create a method on `ImportResolver` (e.g., `ResolveAllImports(Module, SymbolTable, string currentDir)`) that encapsulates the import loop currently in `Compiler.cs`
- [x] Move the import/from-import handling logic there
- [x] Update `Compiler.Compile()` to call the new method
- [x] Verify existing import-related tests still pass
- [x] Add a unit test for `ImportResolver.ResolveAllImports()` that doesn't require a full compilation

### 5.3: Convert TODOs to Issues

**Problem:** There are 15 TODO comments in the compiler source. Some are trivial, some are real feature gaps. They're invisible to project management.

**Tasks:**
- [x] For each TODO in the codebase, create a GitHub issue with the TODO text, file path, and line number
- [x] Label each issue appropriately (enhancement, bug, tech-debt)
- [x] Replace the TODO comment with `// See: #<issue-number>` (or remove if the context is obvious from the issue)
- [x] Key TODOs to address:
  - `NameResolver.cs:573` -- Module loading stub (feature gap) → #102
  - `ControlFlowAnalysis.cs:140` -- Async state machine (feature gap) → #105
  - `NameMangler.cs:51` -- Unconditional string mapping (tech debt/bug risk) → #99
  - `CompilationMetrics.cs:130` -- Hardcoded version string (trivial) → #97
  - `TypeChecker.Expressions.cs:1446` -- Tuple unpacking in comprehensions (feature gap) → #104

---

## Phase 6: LSP Foundation (Optional, When Ready)

**Goal:** Final prep work that should be done immediately before starting LSP development, but not before.

### 6.1: Thread `SourceText` Through the Pipeline

**Problem:** The lexer takes `string sourceCode`. For LSP, you need `SourceText` which supports efficient incremental editing (replace a range, re-lex only the changed region). The `SourceText` class already exists in `Text/SourceText.cs`.

**Tasks:**
- [x] Change `Lexer` constructor to accept `SourceText` instead of `string`
- [x] Update `Compiler.Compile()` to create `SourceText` from the source string and pass it down
- [x] Store `SourceText` in `CompilationResult` for downstream use
- [x] Verify all tests pass
- [x] Thread `SourceText` to Lexer in `ProjectCompiler.ParseAllFiles()` (was using raw string constructor)
- [x] Thread `SourceText` to Lexer in CLI emit commands (4 locations were creating SourceText but passing raw string)

### 6.2: Enrich `CompilationResult`

**Problem:** `CompilationResult` doesn't carry all intermediate artifacts. For tooling, you need the token list, `SemanticBinding`, and resolved imports.

**Tasks:**
- [x] Add `SemanticBinding? SemanticBinding` to `CompilationResult`
- [x] Add `IReadOnlyList<Token>? Tokens` to `CompilationResult`
- [x] Add `SourceText? SourceText` to `CompilationResult`
- [x] Populate these fields in `Compiler.Compile()`
- [x] Consider adding `ImportResolver? ImportResolver` for access to resolved module info
- [x] Preserve artifacts in catch-all exception handlers (OperationCanceledException, generic Exception)

### 6.3: Add Fuzz Testing Harness

**Tasks:**
- [x] Create a random `.spy` program generator (random valid/invalid token sequences, random indentation)
- [x] Run `Compiler.Compile()` on generated inputs, assert no unhandled exceptions
- [x] Track inputs that cause crashes as regression tests
- [x] Focus initially on lexer and parser (highest surface area for unexpected input)
