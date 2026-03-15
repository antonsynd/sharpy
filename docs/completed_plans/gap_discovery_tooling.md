<!-- Verified by /verify-plan on 2026-03-14 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Systematic LSP & Compiler Gap Discovery Tooling

## Context

The Sharpy compiler has ~1,395 test fixtures and a full LSP server with 24 handlers, but no systematic tooling to discover gaps between what the compiler/LSP *should* handle and what it *actually* handles. Currently, bugs are found reactively (dogfooding, manual testing, user reports). This plan adds proactive, repeatable gap discovery across three dimensions:

- **A)** Diagnostic sweep — run semantic analysis on all fixtures and catch anomalies (crashes, unexpected diagnostics, missing diagnostics)
- **B)** Semantic token coverage — measure how much of each source file gets semantic highlighting, find AST node types with no token coverage
- **C)** Hover/completion fuzz — hit every identifier position in every fixture and catch crashes, missing type info, or null results

Together these create a safety net that can be run after any compiler/LSP change to catch regressions and highlight gaps.

## Current State

**What exists:**
- CLI `emit` subcommands: `tokens` (lexer), `ast`, `parse`, `csharp` — but no `diagnostics` or `semantic-tokens`
- `CompilerApi` with `Parse()`, `Analyze()`, `Compile()` — full programmatic access
- `SharpySemanticTokensHandler.CollectTokens()` — walks AST and emits declaration-level tokens (13 types, 5 modifiers) [CORRECTED: class name is `SharpySemanticTokensHandler`, not `SemanticTokensHandler`]
- `ISemanticQuery` — `GetEffectiveType()`, `GetIdentifierSymbol()`, `GetCallTarget()` for hover/completion
- `FileBasedIntegrationTests` — discovers and runs all fixtures, but only validates expected output/errors
- LSP E2E tests via `LspTestClient` — process-based JSON-RPC, but only covers a handful of scenarios

**What's missing:**
- No CLI command to output diagnostics for a file (useful for scripting and CI)
- No way to detect that a positive fixture suddenly emits unexpected warnings
- No measurement of semantic token coverage (which AST nodes produce tokens, which don't)
- No systematic check that hover returns type info at every identifier position
- No fuzz testing of completion at member-access positions

## Design Decisions

1. **Tests over scripts** — All sweep/fuzz tools are xUnit tests in the existing test projects, not standalone scripts. This keeps them in CI, uses existing infrastructure (`IntegrationTestBase`, fixture discovery), and follows project conventions.

2. **CompilerApi over LSP protocol** — For A and B, use `CompilerApi.Analyze()` and `SemanticTokensHandler.CollectTokens()` directly (in-process). Much faster than spawning LSP server processes. Reserve LSP protocol tests for C where we specifically want to test handler behavior.

3. **CLI command for developer UX** — The `emit diagnostics` command serves a different purpose than the tests: it's a developer-facing tool for quick inspection of any `.spy` file, like the existing `emit ast` and `emit csharp` commands.

4. **Reports, not assertions** — The sweep/fuzz tests produce structured reports (written to `.claude/tmp/`) rather than hard-failing on every anomaly. This allows triage — some "gaps" may be intentional. A separate summary assertion catches crashes and unexpected exceptions.

5. **Incremental value** — Each phase is independently useful and committable. Phase 1 (CLI command) is the simplest. Phase 2 (diagnostic sweep) catches compiler bugs. Phase 3 (semantic tokens) catches LSP highlighting gaps. Phase 4 (hover/completion fuzz) catches LSP query gaps.

## Implementation

### Phase 1: CLI `emit diagnostics` Command

**Goal:** Add a developer-facing CLI command that outputs all diagnostics for a `.spy` file in human-readable or JSON format.

#### Tasks

1. **Add `emit diagnostics` subcommand** — `src/Sharpy.Cli/Program.cs`
   - Register new `diagnostics` command under the existing `emit` parent command
   - Accept same file argument as other emit commands, plus `--format` option (`text` default, `json`)
   - Accept `--max-errors`, `--warn-as-error`, `--nowarn` options (matching existing patterns)
   - Handler calls `CompilerApi.Analyze(source)` (or `Compile()` if `--include-codegen` flag is set)
   - Text format: one diagnostic per line as `{severity} {code} ({line}:{col}): {message}` (similar to `DiagnosticRenderer` output)
   - JSON format: array of objects with `severity`, `code`, `line`, `column`, `message`, `phase` fields
   - Exit code 0 if no errors, 1 if errors present
   - Acceptance: `dotnet run --project src/Sharpy.Cli -- emit diagnostics src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy` outputs diagnostics or "No diagnostics." for clean files [CORRECTED: fixture path was `tests/fixtures/basics/hello.spy`, actual path is `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy`]
   - Commit: `feat(cli): add emit diagnostics subcommand with text and JSON output`

2. **Add `emit diagnostics` skill** — `.claude/skills/`
   - Mirror the pattern of existing `spy-emit-csharp` skill
   - Logs full output to `.claude/tmp/last-spy-emit-diagnostics.log`
   - Commit: `feat(skills): add spy-emit-diagnostics skill`

### Phase 2: Diagnostic Sweep Over All Test Fixtures

**Goal:** A test that runs `CompilerApi.Analyze()` on every `.spy` fixture and reports anomalies — crashes, unexpected diagnostics on positive fixtures, missing diagnostics on negative fixtures.

#### Tasks

3. **Create `DiagnosticSweepTests` class** — `src/Sharpy.Compiler.Tests/Integration/DiagnosticSweepTests.cs`
   - Reuse `FileBasedIntegrationTests.GetTestFixtures()` discovery (or extract shared helper if needed)
   - For each fixture, run `CompilerApi.Analyze(source)` wrapped in try/catch
   - Collect results into a structured report:
     - `CrashFixtures` — fixtures where Analyze() threw an exception (always a bug)
     - `UnexpectedDiagnostics` — positive fixtures (`.expected` exists, no `.error`) that emit Error-level diagnostics
     - `UnexpectedWarnings` — positive fixtures that emit Warning-level diagnostics not covered by a `.warning` file
     - `MissingErrors` — negative fixtures (`.error` exists) where Analyze() produces no errors
     - `SummaryStats` — total fixtures analyzed, pass/fail/crash counts by category
   - Write report to `.claude/tmp/diagnostic-sweep-report.json`
   - Hard assertion: zero crashes (any exception = test failure)
   - Soft reporting: log anomaly counts but don't fail (allows triage)
   - Commit: `feat(tests): add diagnostic sweep test over all fixtures`

4. **Create `FixtureDiscoveryHelper`** — `src/Sharpy.Compiler.Tests/Integration/FixtureDiscoveryHelper.cs`
   - Extract the fixture discovery logic from `FileBasedIntegrationTests.GetTestFixtures()` into a reusable static helper
   - Returns structured `TestFixtureInfo` records: `{ SpyFilePath, ExpectedFile?, ErrorFile?, WarningFile?, SkipFile?, IsMultiFile, Category }`
   - Both `FileBasedIntegrationTests` and `DiagnosticSweepTests` use this helper
   - Commit: `refactor(tests): extract fixture discovery into reusable FixtureDiscoveryHelper`

   **Note:** Task 4 should be done before Task 3, since Task 3 depends on it. Listed here for logical grouping.

### Phase 3: Semantic Token Coverage Analysis

**Goal:** Measure how well the LSP semantic token handler covers source files, identify AST node types that produce no tokens, and find token types from the legend that are never emitted.

#### Tasks

5. **Verify `Sharpy.Lsp` reference and `InternalsVisibleTo`** — already in place [CORRECTED: `Sharpy.Lsp.Tests.csproj` already has a `ProjectReference` to `Sharpy.Lsp.csproj`, and `Sharpy.Lsp.csproj` already has `<InternalsVisibleTo Include="Sharpy.Lsp.Tests" />`. `CollectTokens()` is `internal static` and already accessible from test code. This task is a no-op — skip to Task 6.]

6. **Create `SemanticTokenCoverageTests`** — `src/Sharpy.Lsp.Tests/Analysis/SemanticTokenCoverageTests.cs`
   - For each test fixture (duplicate minimal fixture discovery logic from `FileBasedIntegrationTests.GetTestFixtures()` — do NOT add a project reference to Sharpy.Compiler.Tests, as test-to-test project references are architecturally problematic) [CORRECTED: changed from "add project reference to Sharpy.Compiler.Tests" to duplicating discovery logic]:
     - Parse the source with `CompilerApi.Parse()`
     - Run `SharpySemanticTokensHandler.CollectTokens(module.Body, tokens)` on the parsed module's body [CORRECTED: `CollectTokens()` takes `IEnumerable<Statement>` (the module body), not a `Module` directly]
     - Calculate coverage metrics:
       - **Character coverage**: % of non-whitespace, non-comment characters covered by at least one token span
       - **Identifier coverage**: % of `Identifier` AST nodes that have a corresponding semantic token
       - **Declaration coverage**: % of definition AST nodes (`FunctionDef`, `ClassDef`, `VariableDeclaration`, etc.) with tokens
   - Aggregate across all fixtures:
     - **Token type usage**: which of the 13 token types were actually emitted (detect dead types)
     - **AST node type gap list**: node types that appear in fixtures but never produce tokens
     - **Low-coverage files**: fixtures with < 20% identifier coverage (likely highlighting gaps)
   - Write report to `.claude/tmp/semantic-token-coverage-report.json`
   - Hard assertion: no crashes during token collection
   - Summary assertion: log coverage stats, flag any token type with 0 emissions
   - Commit: `feat(tests): add semantic token coverage analysis over all fixtures`

7. **Create `AstNodeTypeCollector` utility** — `src/Sharpy.Lsp.Tests/Analysis/AstNodeTypeCollector.cs`
   - Simple AST walker that collects all distinct node types present in a `Module`
   - Used by Task 6 to compute the "AST node types with no token coverage" metric
   - Returns `Dictionary<string, int>` of node type name → occurrence count
   - Commit: `feat(tests): add AstNodeTypeCollector utility for coverage analysis`

### Phase 4: Hover & Completion Fuzz Testing

**Goal:** For every identifier position in every fixture, verify that hover returns type info without crashing, and test completion at member-access positions.

#### Tasks

8. **Create `IdentifierPositionCollector` utility** — `src/Sharpy.Lsp.Tests/Analysis/IdentifierPositionCollector.cs`
   - AST walker that collects all `Identifier` nodes with their (line, column) positions
   - Also collects `MemberAccess` nodes (for completion testing at `.` positions)
   - Returns `List<(int Line, int Column, string Name, string NodeType)>`
   - Commit: `feat(tests): add IdentifierPositionCollector for fuzz testing`

9. **Create `HoverFuzzTests`** — `src/Sharpy.Lsp.Tests/Analysis/HoverFuzzTests.cs`
   - For each positive test fixture (skip `.error`-only fixtures since they may not analyze cleanly):
     - Run `CompilerApi.Analyze(source)` to get `SemanticResult`
     - If analysis fails (expected for some fixtures), skip
     - Collect all identifier positions via `IdentifierPositionCollector`
     - For each identifier, call `CompilerApi.FindNodeAtPosition(result.Ast, line, col)` and then `result.SemanticInfo.GetIdentifierSymbol()` / `result.SemanticInfo.GetEffectiveType()` [CORRECTED: there is no `SemanticQuery` class — `SemanticInfo` implements `ISemanticQuery` directly; access via `SemanticResult.SemanticInfo`]
     - Record results:
       - **Crashes**: positions where any of these calls throw (always a bug)
       - **NullSymbol**: identifiers where `GetIdentifierSymbol()` returns null (potential gap)
       - **NullType**: identifiers where `GetEffectiveType()` returns null (potential gap)
       - **UnknownType**: identifiers resolved to `UnknownType` (error recovery — may indicate gap)
   - Write report to `.claude/tmp/hover-fuzz-report.json`
   - Hard assertion: zero crashes
   - Summary: log % of identifiers with full type info, list top gaps
   - Commit: `feat(tests): add hover fuzz test over all fixture identifiers`

10. **Create `CompletionFuzzTests`** — `src/Sharpy.Lsp.Tests/Analysis/CompletionFuzzTests.cs`
    - For each positive test fixture:
      - Run `CompilerApi.Analyze(source)` to get `SemanticResult`
      - Collect all `MemberAccess` positions via `IdentifierPositionCollector`
      - For each member access, get the receiver's type via `GetEffectiveType()` on the receiver expression
      - Verify the type has the accessed member (check `TypeSymbol.Methods`, `.Fields`, `.Properties`)
      - Record results:
        - **Crashes**: positions where type resolution throws
        - **NullReceiverType**: member access where receiver has no type
        - **MissingMember**: member access where the type doesn't contain the accessed member (may indicate incomplete type info)
    - Write report to `.claude/tmp/completion-fuzz-report.json`
    - Hard assertion: zero crashes
    - Commit: `feat(tests): add completion fuzz test over all fixture member accesses`

### Phase 5: CI Integration & Reporting

**Goal:** Make all gap discovery tests runnable in CI with clear reporting and regression detection.

#### Tasks

11. **Add test category/trait annotations** — all new test classes
    - Add `[Trait("Category", "GapDiscovery")]` to all new test classes
    - This allows running gap discovery separately: `dotnet test --filter "Category=GapDiscovery"`
    - Add to CI workflow as an optional/nightly job (not blocking on PRs initially)
    - Commit: `feat(tests): add GapDiscovery trait for selective test execution`

12. **Create gap discovery summary skill** — `.claude/skills/`
    - New `/gap-analysis` skill that:
      - Runs `dotnet test --filter "Category=GapDiscovery"`
      - Reads the JSON reports from `.claude/tmp/`
      - Presents a unified summary: crash count, anomaly count, coverage metrics
    - Commit: `feat(skills): add gap-analysis skill for running all discovery tests`

## Testing Strategy

- **Phase 1**: Manual verification — run `emit diagnostics` on known-good and known-bad fixtures, verify output format
- **Phase 2**: The sweep test itself is the test — verify it catches at least one known anomaly (or confirm zero anomalies = healthy codebase)
- **Phase 3**: Verify coverage report identifies the known gap (semantic tokens only cover declarations, not identifier usages in expressions)
- **Phase 4**: Verify hover fuzz catches any known missing type info cases

**Edge cases:**
- Multi-file fixtures need project-level analysis (`AnalyzeProject`), not single-file `Analyze()`
- Fixtures with `.skip` files should be skipped in all sweeps
- Negative fixtures (`.error`) may not parse/analyze cleanly — handle gracefully
- Very large fixtures should have reasonable timeouts
- Fixtures that import stdlib modules need module paths configured

## Issues to Close

No existing GitHub issues — this is a new capability. Consider creating tracking issues for each phase before implementation.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-14
**Plan file:** `~/.claude/plans/plan-d8ae15.md`

### Corrections Made
1. **Class name**: `SemanticTokensHandler` → `SharpySemanticTokensHandler` (line 18, file: `src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs`)
2. **Fixture path**: `tests/fixtures/basics/hello.spy` → `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy` (Task 1 acceptance criteria)
3. **Task 5 is a no-op**: `Sharpy.Lsp.Tests` already has project reference to `Sharpy.Lsp`, and `InternalsVisibleTo` is already configured. `CollectTokens()` is `internal static` and already accessible.
4. **Test project reference**: Changed Task 6 from "add project reference to Sharpy.Compiler.Tests" to "duplicate minimal fixture discovery logic" — test-to-test project references are architecturally problematic.
5. **SemanticQuery class doesn't exist**: `ISemanticQuery` is implemented by `SemanticInfo` directly. Changed Task 9 to use `result.SemanticInfo.GetIdentifierSymbol()` / `result.SemanticInfo.GetEffectiveType()`.
6. **CollectTokens signature**: Takes `IEnumerable<Statement>` (module body), not a `Module`. Corrected Task 6 to call `CollectTokens(module.Body, tokens)`.

### Warnings
- **Task 4 ordering note**: Plan correctly notes Task 4 (FixtureDiscoveryHelper) should be done before Task 3 (DiagnosticSweepTests). Good — just ensure this is reflected in implementation order.
- **Multi-file fixture handling**: Task 3/6/9/10 should use `CompilerApi.AnalyzeProject()` for multi-file fixtures (directories with multiple .spy files), not `Analyze()`. The plan mentions this in edge cases but the task descriptions default to single-file `Analyze()`.

### Missing Steps Added
- None — the plan is comprehensive across all 5 phases.

### Unchecked Claims
- **"only covers a handful of scenarios"** (LSP E2E tests): Verified — 12 test methods in ProtocolTests.cs + 6 in MultiFileTests.cs = 18 E2E test methods. "Handful" is fair characterization.
- **Token count claims**: Verified — 13 token types (indices 0–12) and 5 modifiers (bits 0–4) confirmed from `SharpySemanticTokensHandler`.
- **24 handlers**: Verified — 26 files in `src/Sharpy.Lsp/Handlers/`, minus 2 helpers (`SymbolLocationHelper.cs`, `TypeHierarchyHelper.cs`) = 24 handlers.
- **~1,395 test fixtures**: Verified — exactly 1,395 `.spy` files found.
- **CompilerApi methods**: `Parse()`, `Analyze()`, `Compile()`, `FindNodeAtPosition()` all verified.
- **ISemanticQuery methods**: `GetEffectiveType()`, `GetIdentifierSymbol()`, `GetCallTarget()` all verified.
- **TypeSymbol members**: `.Methods`, `.Fields`, `.Properties` all verified on `TypeSymbol` record.
