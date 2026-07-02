# Test Harness Hardening & Developer Tooling Proposal

> **Status:** Proposal — pending design review
> **Date:** 2026-07-02
> **Scope:** Enterprise-grade testing infrastructure, hallucination-resistant test verification, autonomous LSP testing, and developer adoption tooling

## Motivation

Sharpy has strong testing foundations — ~9,600 tests, 95+ property test files with CsCheck generators, metamorphic transforms, custom fuzzing, 2,185 file-based integration fixtures, comprehensive LSP unit + E2E protocol tests, and BenchmarkDotNet benchmarks. However, several enterprise-grade capabilities are absent:

- **No code coverage** — no way to measure what the test suite actually exercises
- **No mutation testing** — no way to measure test *effectiveness* (surviving mutants = test gaps)
- **No coverage-guided fuzzing** — existing fuzzing is xUnit-integrated with fixed seeds, not corpus-driven
- **No visual regression testing** for LSP/editor integration
- **No differential oracle** — tests assert expected values without independent ground-truth verification against Python
- **No spec-driven test scaffolding** — new features require manual test creation across 6+ layers

This document proposes six new subsystems to close these gaps.

---

## Current Infrastructure Audit

### What Exists (strong)

| Area | State | Key assets |
|------|-------|------------|
| Property testing | 95+ files | CsCheck, full AST generators (`GenSharpy`, 18 typed generators), 10 metamorphic transforms, shrinkers |
| Custom fuzzing | 5 harness files | `SharpyFuzzer` (random programs, mutations, class hierarchies, generics), lexer/semantic/codegen property tests |
| LSP tests | 30+ test files | Unit handlers, E2E JSON-RPC protocol tests (`LspTestClient`), refactoring tests, fuzz tests for hover/completion |
| Snapshot testing | 2,185 fixtures | `.spy` + `.expected`/`.error`/`.warning`/`.expected.cs`, multi-file directories, `UPDATE_SNAPSHOTS` workflow |
| Benchmarks | BenchmarkDotNet | 5 compiler benchmarks, lexer/parser isolation, cross-language CI comparison |
| Integration tests | Mature | `IntegrationTestBase` (compile → IL → execute → capture stdout), `ProjectCompilationHelper` for multi-file |
| Dogfooding | AI-driven | `sharpy_dogfood/` generates random programs via LLM, compiles, reports crashes |
| CI | 9 workflows | .NET 10, benchmarks, cross-language, release, docs, VS Code extension, staleness checks |

### What's Missing

| Gap | Impact | Effort |
|-----|--------|--------|
| Code coverage | Can't measure what's tested | Low (coverlet config) |
| Mutation testing | Can't measure test effectiveness | Medium (Stryker.NET setup) |
| Coverage-guided fuzzing | Miss deep bugs that random testing won't find | Medium (SharpFuzz integration) |
| Visual LSP regression | Editor rendering bugs invisible to protocol tests | High (novel harness) |
| Differential oracle | Tests may encode wrong behavior if `.expected` is wrong | High (Python equivalence engine) |
| TDD scaffolding | Manual multi-layer test creation is slow and incomplete | Medium (spec reader + template engine) |

---

## Proposed Subsystems

### 1. Vision-Based LSP Visual Regression Harness

**Problem:** LSP protocol tests verify JSON responses but cannot detect visual regressions — wrong syntax coloring, misplaced hover tooltips, broken completion popups, diagnostic squiggles on the wrong line.

**Design:**

```
┌─────────────┐     ┌──────────────┐     ┌──────────────────┐     ┌────────────────┐
│ Test Runner  │────▶│ Editor Driver │────▶│ Screenshot Capture│────▶│ Visual Verifier │
│ (xUnit)      │     │ (VS Code /   │     │ (headless browser │     │ (IVisualVerifier)│
│              │     │  Playground) │     │  or Playwright)  │     │                │
└─────────────┘     └──────────────┘     └──────────────────┘     └────────────────┘
                                                                          │
                                                    ┌─────────────────────┼──────────────┐
                                                    │                     │              │
                                              ┌─────▼─────┐    ┌────────▼──────┐ ┌─────▼──────┐
                                              │ Golden File │    │ Vision LLM    │ │ Pixel Diff │
                                              │ Comparator  │    │ (ollama/cloud)│ │ (fallback) │
                                              └────────────┘    └──────────────┘ └────────────┘
```

**Key abstractions:**

- `IEditorDriver` — launches editor/playground, navigates to position, triggers hover/completion/etc.
  - `PlaygroundDriver` — headless Chromium against the Blazor playground
  - `VsCodeDriver` — launches VS Code with the Sharpy extension in headless mode
- `IScreenshotCapture` — captures a region of the editor at a specific state
- `IVisualVerifier` — judges whether the screenshot matches expectations
  - `GoldenFileVerifier` — pixel-diff against a stored reference image (fast, brittle to theme changes)
  - `VisionLlmVerifier` — sends screenshot + structured prompt to a vision model
    - `OllamaVisionBackend` — local ollama (e.g., llava, moondream)
    - `CloudVisionBackend` — Anthropic/OpenAI vision API
  - `CompositeVerifier` — golden file first, vision LLM on diff failures (reduces API costs)
- `VisualTestCase` — declarative test definition:
  ```
  file: hover_type.spy
  position: 3:5
  action: hover
  expect: "Shows type `int` in hover tooltip"
  golden: hover_type_baseline.png
  ```

**Prompt templates for vision verification:**

```
You are verifying a Sharpy IDE screenshot. The editor should be showing:
- File: {filename}
- Action: {action} at line {line}, column {col}
- Expected: {expectation}

Evaluate:
1. Is the expected UI element visible? (hover tooltip / completion menu / diagnostic squiggle)
2. Does it contain the correct content?
3. Is it positioned at the correct location?
4. Are there any unexpected visual artifacts?

Return JSON: { "pass": bool, "confidence": float, "issues": [string] }
```

**Test integration:** Runs as a separate test class `VisualRegressionTests` in `Sharpy.Lsp.Tests`, gated behind a `[Trait("Category", "Visual")]` so it doesn't run in normal CI (requires a display or headless browser).

### 2. Hallucination-Resistant Differential Oracle

**Problem:** 2,185 test fixtures assert expected output, but the `.expected` files were written by humans (or AI). If an expected value is wrong, the test encodes a bug as correct behavior.

**Design:**

```
┌───────────────┐     ┌──────────────────┐     ┌─────────────┐     ┌──────────────┐
│ .spy fixture  │────▶│ Spy→Python       │────▶│ Python exec │────▶│ Oracle Judge │
│               │     │ Transpiler       │     │ (subprocess)│     │              │
└───────────────┘     └──────────────────┘     └─────────────┘     └──────────────┘
                                                                          │
                       ┌──────────────────┐     ┌─────────────┐          │
                       │ Sharpy compile + │────▶│ .NET exec   │──────────┘
                       │ execute          │     │             │
                       └──────────────────┘     └─────────────┘
```

**Components:**

- `SpyToPythonTranspiler` — converts `.spy` source to equivalent Python 3
  - Handles Sharpy→Python surface differences (type annotations stripped, `print()` semantics aligned, collection types mapped)
  - Flags untranslatable constructs (`.NET interop`, `struct`, `Result<T,E>`) as `SKIP_ORACLE`
  - Outputs a `.py` file alongside each `.spy` fixture
- `DifferentialOracle` — runs both programs and compares:
  - Exact stdout match (primary)
  - Normalized comparison (whitespace, float precision, dict ordering)
  - Semantic comparison for error cases (both fail → check error category matches)
- `OracleReport` — per-fixture trust classification:
  - `VERIFIED` — Sharpy output matches Python output
  - `DIVERGENT` — outputs differ (potential bug or intentional deviation)
  - `SKIP_ORACLE` — fixture uses Sharpy-only features, can't be verified against Python
  - `PYTHON_ERROR` — Python itself errors (may indicate incorrect Python translation)
- `OracleCI` — CI integration that runs the oracle on all translatable fixtures, flags new divergences

**Deviation catalog:** Not all divergences are bugs. Sharpy intentionally deviates from Python in documented cases (see `docs/deviations.yaml`). The oracle cross-references this catalog and suppresses known deviations.

**Coverage metrics:**
- % of fixtures with `VERIFIED` status (target: >70% of non-interop fixtures)
- % with `SKIP_ORACLE` (acceptable, tracks interop-heavy features)
- % with `DIVERGENT` (should be 0 after triage)

### 3. Coverage-Guided Fuzzing (SharpFuzz/libFuzzer)

**Problem:** Existing fuzzing uses seeded randomness in xUnit — effective for smoke testing but misses deep bugs that coverage-guided fuzzers find by tracking code paths.

**Design:**

- `Sharpy.Fuzz/` — standalone console project, not xUnit
- Fuzzing targets (one per compiler stage):
  - `LexerTarget` — tokenize arbitrary byte input
  - `ParserTarget` — parse arbitrary token streams (or UTF-8 input)
  - `SemanticTarget` — full pipeline: lex → parse → analyze
  - `CodeGenTarget` — full pipeline: lex → parse → analyze → emit C#
  - `RoundTripTarget` — parse → unparse → reparse, assert structural equality
- Corpus management:
  - `corpus/` directory with seed inputs derived from existing test fixtures
  - Minimized crash inputs auto-saved to `crashes/`
  - `corpus-merge.sh` — deduplicates and minimizes the corpus
- CI integration:
  - PR fuzzing: run each target for 2 minutes, fail on new crashes
  - Nightly fuzzing: run for 30 minutes, archive new corpus entries
  - Crash → auto-create GitHub issue with minimized input + stack trace
- Framework: SharpFuzz (instruments .NET assemblies for AFL/libFuzzer)
  - Alternative: built-in `System.Runtime.Fuzzing` if available on .NET 10

### 4. Mutation Testing (Stryker.NET)

**Problem:** 9,600 tests pass, but how many would still pass if the code were subtly wrong? Mutation testing measures test effectiveness by injecting small faults and checking if tests catch them.

**Design:**

- Stryker.NET configuration (`stryker-config.json`):
  - **Target projects:** `Sharpy.Compiler`, `Sharpy.Core`, `Sharpy.Stdlib`
  - **Test projects:** Corresponding `*.Tests` projects
  - **Mutators:** Arithmetic, boolean, string, equality, logical, assignment, unary, LINQ, regex
  - **Exclusions:** Generated code (`obj/`, `Spy/generated/`), benchmark code, test infrastructure
  - **Thresholds:** Break at <70% mutation score, warn at <80%
- Per-component targeting:
  - `Semantic/` — highest priority (subtle type-checking bugs)
  - `CodeGen/` — high priority (wrong Roslyn tree shapes)
  - `Parser/` — medium priority (well-covered by round-trip properties)
  - `Lexer/` — lower priority (well-covered by exhaustive token tests)
- CI integration:
  - Weekly scheduled run (full mutation, takes hours)
  - PR runs: mutate only changed files (fast, focused)
  - Dashboard: Stryker HTML report published as CI artifact
- Actionable output:
  - Surviving mutants → auto-generate test skeleton suggestions
  - Track mutation score trend over time

### 5. Code Coverage Infrastructure

**Problem:** No visibility into what the test suite exercises. Cannot set coverage gates or identify untested code paths.

**Design:**

- Collection: `coverlet.collector` (already a transitive dependency of the test SDK)
  - Add `<CollectCoverage>true</CollectCoverage>` and `<CoverletOutputFormat>cobertura</CoverletOutputFormat>` to `Directory.Build.props` or pass via CLI
  - Collect per test project: `dotnet test --collect:"XPlat Code Coverage"`
- Reporting:
  - `ReportGenerator` to merge per-project coverage into a unified HTML report
  - Cobertura XML for CI tooling
- CI integration (in `dotnet10.yml`):
  - Collect coverage on every PR
  - Upload HTML report as artifact
  - Coverage diff comment on PR (show lines added without coverage)
- Thresholds (enforced in CI):
  - `Sharpy.Core`: ≥90% line coverage
  - `Sharpy.Compiler`: ≥80% line coverage
  - `Sharpy.Stdlib`: ≥75% line coverage
  - No PR may decrease overall coverage by >1%
- Local workflow: `dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator` → open `coverage/index.html`

### 6. Spec-Driven TDD Scaffolding Engine

**Problem:** Implementing a new language feature requires creating tests across 6+ layers (lexer, parser, semantic, validation, codegen, integration, LSP). This is manual, error-prone, and incomplete — developers forget layers or edge cases.

**Design:**

```
┌────────────────────┐     ┌───────────────┐     ┌──────────────────┐
│ Feature Description│────▶│ Spec Reader   │────▶│ Test Skeleton    │
│ (issue / spec page)│     │ (parses spec  │     │ Generator        │
│                    │     │  markdown)    │     │                  │
└────────────────────┘     └───────────────┘     └──────────────────┘
                                                        │
                    ┌───────────────────────────────────┬┴┬──────────────────┐
                    │                    │               │                    │
              ┌─────▼─────┐    ┌────────▼──────┐ ┌─────▼──────┐  ┌─────────▼────────┐
              │ Lexer Tests│    │ Parser Tests  │ │ Semantic   │  │ Integration      │
              │ (tokens)   │    │ (AST shape)   │ │ Tests      │  │ Fixtures         │
              └───────────┘    └──────────────┘ └────────────┘  │ (.spy + .expected)│
                                                                 └──────────────────┘
```

**Components:**

- `SpecReader` — parses `docs/language_specification/` markdown to extract:
  - Syntax grammar rules (for lexer/parser test generation)
  - Type rules and constraints (for semantic test generation)
  - Example code blocks (for integration fixture generation)
  - Edge cases mentioned in prose (for targeted edge-case tests)
- `TestSkeletonGenerator` — produces test files per layer:
  - **Lexer:** Token type assertions for new keywords/operators
  - **Parser:** AST shape assertions (node type, children, spans)
  - **Semantic:** Type-checking assertions (accepted types, rejected types, error codes)
  - **Validation:** Validator trigger assertions (diagnostic codes, spans)
  - **CodeGen:** `.expected.cs` skeleton (Roslyn tree shape)
  - **Integration:** `.spy` + `.expected` fixture pairs
  - **LSP:** Hover/completion/semantic-token assertions for new syntax
- `TddChecklist` — generates a markdown checklist:
  ```markdown
  ## Feature: walrus operator (:=)
  - [ ] Lexer: TokenType.WalrusOp recognized
  - [ ] Parser: AssignmentExpression with WalrusOp
  - [ ] Semantic: type inference through walrus binding
  - [ ] Semantic: walrus in comprehension scope
  - [ ] Validation: walrus not allowed at module level
  - [ ] CodeGen: emits C# variable declaration + assignment
  - [ ] Integration: basic_walrus.spy passes
  - [ ] Integration: walrus_in_if.spy passes
  - [ ] Integration: walrus_scope_error.spy → SPY0XXX
  - [ ] LSP: hover shows inferred type at walrus binding
  ```
- CLI integration: `sharpyc scaffold-tests <feature-name>` or Claude skill `/scaffold-tests <desc>`
- Edge case derivation:
  - Boundary values from type rules (empty collections, max nesting, Unicode edge cases)
  - Error cases from the spec's "must not" / "error" language
  - Interaction cases from cross-references between spec sections

---

## Project Structure

```
src/
├── Sharpy.TestHarness/                    # Shared infrastructure
│   ├── Visual/
│   │   ├── IEditorDriver.cs
│   │   ├── PlaygroundDriver.cs
│   │   ├── VsCodeDriver.cs
│   │   ├── IScreenshotCapture.cs
│   │   ├── IVisualVerifier.cs
│   │   ├── GoldenFileVerifier.cs
│   │   ├── VisionLlmVerifier.cs
│   │   ├── CompositeVerifier.cs
│   │   ├── OllamaVisionBackend.cs
│   │   ├── CloudVisionBackend.cs
│   │   └── VisualTestCase.cs
│   ├── Oracle/
│   │   ├── SpyToPythonTranspiler.cs
│   │   ├── DifferentialOracle.cs
│   │   ├── OracleReport.cs
│   │   ├── DeviationCatalog.cs
│   │   └── OracleCI.cs
│   ├── Scaffold/
│   │   ├── SpecReader.cs
│   │   ├── TestSkeletonGenerator.cs
│   │   └── TddChecklist.cs
│   └── Sharpy.TestHarness.csproj
├── Sharpy.Fuzz/                           # Standalone fuzzing
│   ├── Targets/
│   │   ├── LexerTarget.cs
│   │   ├── ParserTarget.cs
│   │   ├── SemanticTarget.cs
│   │   ├── CodeGenTarget.cs
│   │   └── RoundTripTarget.cs
│   ├── corpus/                            # Seed inputs
│   ├── crashes/                           # Minimized crash reproductions
│   └── Sharpy.Fuzz.csproj
└── Sharpy.TestHarness.Tests/              # Tests for the harness itself
    ├── Visual/
    ├── Oracle/
    └── Scaffold/
```

**Stryker config** at repo root: `stryker-config.json`

**Coverage** configured in `Directory.Build.props` + CI workflow changes.

---

## Integration Points

| Subsystem | Integrates with | How |
|-----------|----------------|-----|
| Visual LSP harness | `Sharpy.Lsp.Tests` | Shares test fixture `.spy` files, runs as `[Trait("Category", "Visual")]` |
| Differential oracle | `Sharpy.Compiler.Tests` | Reads existing `.spy` + `.expected` fixtures, outputs oracle report |
| Fuzzing | CI (`dotnet10.yml`) | New workflow job, artifacts for corpus and crashes |
| Mutation testing | CI | Weekly scheduled workflow, HTML report artifact |
| Coverage | CI (`dotnet10.yml`) | Added collection step, report artifact, threshold gate |
| TDD scaffolding | Claude skills, CLI | New `/scaffold-tests` skill, new `sharpyc scaffold-tests` command |

---

## Priority Order

1. **Code coverage** — lowest effort, highest immediate visibility (days)
2. **Differential oracle** — directly addresses hallucination risk, leverages existing fixtures (1-2 weeks)
3. **TDD scaffolding** — accelerates all future development (1-2 weeks)
4. **Mutation testing** — measures test quality, Stryker.NET is mostly config (1 week)
5. **Coverage-guided fuzzing** — finds deep bugs, requires SharpFuzz setup (2 weeks)
6. **Vision-based LSP testing** — most novel, highest design complexity (3-4 weeks)
