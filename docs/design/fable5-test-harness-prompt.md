# Fable 5 Prompt: Enterprise Test Harness & Developer Tooling Design

> **Usage:** Feed this entire document to Fable 5 as the prompt. It is self-contained but references `docs/design/test-harness-hardening-proposal.md` for the full infrastructure audit and subsystem sketches. Include that file as context alongside this prompt.

---

## Context

You are designing enterprise-grade testing infrastructure and developer tooling for **Sharpy**, a Python-to-.NET compiler. Sharpy compiles `.spy` files (Python-like syntax with static types) through a pipeline: `Lexer → Parser (AST) → Semantic Analysis → Validation → Roslyn CodeGen → C# → .NET IL`.

The project already has strong testing foundations:
- ~9,600 xUnit tests across 5 test projects
- 95+ property test files using CsCheck with full AST generators, 10 metamorphic transforms, and shrinkers
- Custom fuzzing (xUnit-integrated, seeded randomness — not coverage-guided)
- 2,185 file-based integration test fixtures (`.spy` + `.expected`/`.error` golden files)
- Comprehensive LSP tests (30+ handler test files, E2E JSON-RPC protocol tests)
- BenchmarkDotNet compiler benchmarks + weekly cross-language CI comparisons
- AI-powered dogfooding that generates random Sharpy programs and compiles them

**What's missing** (see the companion proposal document for full audit):
- Code coverage (completely absent)
- Mutation testing (no Stryker.NET)
- Coverage-guided fuzzing (no SharpFuzz/libFuzzer — only xUnit random testing)
- Visual regression testing for LSP/editor integration
- Differential oracle (tests assert `.expected` values without independent Python ground-truth verification)
- Spec-driven TDD scaffolding (manual multi-layer test creation)

**Tech stack:** .NET 10, C#, Roslyn, xUnit, CsCheck, System.CommandLine CLI, OmniSharp LSP, Blazor WASM playground. Multi-target: `net10.0` + `netstandard2.1` (C# 9.0 floor for Core/Stdlib).

---

## Your Task

Design six subsystems that harden Sharpy's testing infrastructure to enterprise grade. For each subsystem, produce:

1. **Architecture** — components, interfaces, data flow diagrams, and how they compose
2. **Interfaces** — full C# interface definitions (target C# 14 / .NET 10) with XML doc comments explaining contracts and invariants
3. **Data models** — records/classes for test cases, results, reports, configurations
4. **Integration plan** — how this subsystem connects to the existing test infrastructure, CI workflows, and Claude Code skills
5. **Edge cases and failure modes** — what can go wrong, how the system degrades gracefully
6. **Implementation roadmap** — phased delivery with clear milestones and dependencies between subsystems

The six subsystems are described below. The companion document (`docs/design/test-harness-hardening-proposal.md`) contains initial sketches — use them as starting points but improve, challenge, and extend them.

---

### Subsystem 1: Vision-Based LSP Visual Regression Harness

**Goal:** Detect visual regressions in editor integration that protocol-level tests miss — wrong syntax coloring, mispositioned hover tooltips, broken completion popups, diagnostic squiggles on incorrect lines.

**Design requirements:**

- **Abstract vision backend** — `IVisualVerifier` interface supporting:
  - Local ollama models (llava, moondream, or similar vision models)
  - Cloud vision APIs (Anthropic Claude vision, OpenAI GPT-4V)
  - Pixel-diff baseline comparison (fast, no LLM cost, but brittle to theme/font changes)
  - Composite strategy: pixel-diff first, escalate to vision LLM only on diff failures
- **Editor driver abstraction** — `IEditorDriver` for:
  - Blazor WASM playground (headless Chromium via Playwright)
  - VS Code with Sharpy extension (headless, `--disable-gpu` mode)
  - Must handle: navigate to file, set cursor position, trigger hover/completion/signature help, wait for UI to settle, capture screenshot of a specific region
- **Declarative test definitions** — YAML or C# record-based test case format:
  ```yaml
  - file: hover_on_variable.spy
    action: hover
    position: { line: 3, col: 5 }
    expect: "Hover tooltip shows type `int`"
    region: tooltip  # capture just the tooltip, not the whole editor
    golden: baselines/hover_on_variable.png
  ```
- **Structured verification prompts** — design the prompt templates that ask the vision model to evaluate specific visual properties. The prompts must be:
  - Deterministic in structure (JSON schema for responses)
  - Specific to the UI element being tested (hover vs completion vs diagnostics)
  - Calibrated to avoid false positives (theme differences, font rendering) and false negatives (wrong content in right position)
- **Confidence scoring** — the verifier should return a confidence score so tests can be configured with thresholds (e.g., fail at <0.8 confidence, warn at <0.9)
- **Golden file management** — workflow for updating baselines when intentional visual changes are made, similar to `UPDATE_SNAPSHOTS=true` for C# snapshots
- **Cost control** — vision LLM calls are expensive; the composite strategy should minimize them, and CI should cache verification results keyed by (screenshot hash + prompt hash)

**Design considerations:**
- How to handle flaky screenshots (anti-aliasing, cursor blink, animation timing)?
- How to make golden files platform-independent (macOS vs Linux CI)?
- How to test dark mode vs light mode themes?
- Should verification results be cached and reused across CI runs if the screenshot hasn't changed?

---

### Subsystem 2: Hallucination-Resistant Differential Oracle

**Goal:** Independently verify that Sharpy test fixtures produce correct output by running equivalent Python programs and comparing results. This makes it impossible for a hallucinated `.expected` file to persist undetected.

**Design requirements:**

- **Spy-to-Python transpiler** — converts `.spy` source to equivalent Python 3:
  - Strip type annotations (Python ignores them at runtime, but syntax must be valid)
  - Map Sharpy-specific syntax to Python equivalents (e.g., `match` → Python 3.10 `match`, `struct` → dataclass)
  - Map Sharpy stdlib imports to Python stdlib equivalents
  - Flag untranslatable constructs (`.NET interop`, `Result<T,E>`, `struct` value semantics, `@property` with `.setter`/`.deleter` that differ) as `SKIP_ORACLE` with a reason
  - Handle intentional deviations cataloged in `docs/deviations.yaml`
- **Differential execution engine:**
  - Run Python 3 subprocess with timeout and resource limits
  - Run Sharpy compilation + execution (via existing `IntegrationTestBase` infrastructure)
  - Compare outputs with configurable normalization:
    - Exact match (default)
    - Float tolerance (for floating-point operations)
    - Dict ordering normalization (Python dicts are ordered since 3.7, but iteration order may differ)
    - Whitespace normalization (trailing newlines, spaces)
    - Error category matching (for error test cases: both should fail, error type should match)
- **Trust classification per fixture:**
  - `VERIFIED` — outputs match, high confidence in correctness
  - `VERIFIED_WITH_DEVIATION` — outputs differ but deviation is cataloged and expected
  - `DIVERGENT` — outputs differ unexpectedly (potential bug)
  - `SKIP_ORACLE` — uses Sharpy-only features, no Python equivalent
  - `TRANSLATION_FAILED` — transpiler couldn't produce valid Python
  - `PYTHON_ERROR` — Python itself errors on the translated code (transpiler bug or unsupported syntax)
- **Oracle report** — aggregate view:
  - Per-fixture trust status
  - Overall verification rate (% VERIFIED out of translatable fixtures, target >70%)
  - Divergence details with diffs
  - Trend tracking over time (are we verifying more or fewer fixtures?)
- **CI integration:**
  - Run oracle on all translatable fixtures nightly
  - On PR: run oracle only on new/modified fixtures
  - Block merge if any fixture changes from `VERIFIED` to `DIVERGENT`

**Design considerations:**
- The transpiler doesn't need to handle 100% of Sharpy — it's a verification tool, not a production transpiler. Prefer `SKIP_ORACLE` over incorrect translation.
- Some Sharpy behaviors intentionally diverge from Python (integer division, string indexing). The deviation catalog must be machine-readable and cross-referenced automatically.
- How to handle non-deterministic outputs (e.g., set iteration order, dict repr)?
- How to handle tests that depend on Sharpy.Core/Stdlib runtime behavior that has no Python equivalent?
- Should the transpiler use an LLM for complex translations, or be purely rule-based? (Rule-based is more trustworthy for a verification tool.)

---

### Subsystem 3: Coverage-Guided Fuzzing Infrastructure

**Goal:** Find deep compiler bugs that seeded random testing misses, using coverage feedback to explore new code paths.

**Design requirements:**

- **Standalone fuzzer project** (`Sharpy.Fuzz`) — not xUnit, runs independently
- **Per-stage fuzzing targets:**
  - Lexer: arbitrary bytes → tokenize, assert no unhandled exceptions
  - Parser: arbitrary bytes → lex + parse, assert no unhandled exceptions
  - Semantic: arbitrary bytes → full analysis pipeline, assert no unhandled exceptions, assert determinism (same input → same diagnostics)
  - CodeGen: arbitrary bytes → full pipeline → emit C#, assert generated C# parses without Roslyn syntax errors
  - RoundTrip: arbitrary bytes → parse → unparse → reparse, assert structural equality
- **Corpus management:**
  - Seed corpus derived from existing 2,185 test fixtures (minimize to ~500 representative inputs)
  - Crash inputs auto-saved with minimization
  - Corpus merge/dedup tooling
  - Corpus stored in repo (compressed) for reproducibility
- **Framework choice:** Evaluate SharpFuzz (AFL-based) vs `Microsoft.Testing.Extensions.Fuzz` (if available in .NET 10) vs custom coverage-guided harness using `System.Diagnostics.CodeAnalysis`
- **CI integration:**
  - PR job: fuzz each target for 2 minutes, fail on crashes
  - Nightly job: fuzz for 30 minutes per target, archive new corpus entries
  - Crash triage: auto-create GitHub issue with minimized repro, stack trace, and affected compiler stage
- **Regression tests:** Every crash input that gets fixed becomes a new test fixture (automatic crash-to-fixture pipeline)

**Design considerations:**
- .NET assembly instrumentation overhead — how to keep fuzzing throughput high?
- Memory limits per fuzzing target (compiler allocates heavily during semantic analysis)
- How to fuzz multi-file compilation (import graphs)?
- Should the fuzzer also check for excessive memory/time consumption (OOM/timeout bugs)?

---

### Subsystem 4: Mutation Testing (Stryker.NET)

**Goal:** Measure test suite effectiveness by injecting small faults (mutations) into the compiler and checking if tests catch them. Surviving mutants = test gaps.

**Design requirements:**

- **Configuration:**
  - Target the three main projects: `Sharpy.Compiler`, `Sharpy.Core`, `Sharpy.Stdlib`
  - Exclude generated code, benchmarks, test infrastructure, `obj/` directories
  - Use all standard mutators: arithmetic, boolean, string, equality, logical, assignment, unary, LINQ, regex, null-coalescing
  - Configure timeout multiplier for slow tests (property tests can be slow)
- **Per-component strategy:**
  - `Semantic/` — highest priority, subtle type-checking bugs are most dangerous
  - `CodeGen/` — high priority, wrong Roslyn tree shapes produce valid but incorrect C#
  - `Parser/` — medium priority, well-covered by round-trip property tests
  - `Lexer/` — lower priority, exhaustively tested by token enumeration tests
  - `Core/` — high priority for collection semantics (Python-compatible indexing, slicing)
- **Thresholds:**
  - Overall mutation score: ≥70% to pass, ≥80% for no warnings
  - Per-component thresholds (semantic/codegen stricter than lexer/parser)
  - No PR may decrease mutation score of changed files
- **CI integration:**
  - Weekly full mutation run (scheduled, takes hours)
  - PR runs: mutate only changed files + their direct callers (fast, focused)
  - Publish Stryker HTML report as CI artifact
- **Actionable output:**
  - Surviving mutants → suggest test cases to kill them
  - Track mutation score trend over time
  - Correlate surviving mutants with code coverage gaps

**Design considerations:**
- Stryker.NET can be slow on large projects. How to parallelize effectively?
- How to handle equivalent mutants (mutations that don't change behavior)?
- Should the mutation score feed into PR review decisions?

---

### Subsystem 5: Code Coverage Infrastructure

**Goal:** Measure what the test suite exercises, set coverage gates, identify untested code paths.

**Design requirements:**

- **Collection:** Coverlet (already a transitive SDK dependency)
  - Collect per test project via `--collect:"XPlat Code Coverage"`
  - Output Cobertura XML
- **Reporting:**
  - ReportGenerator to merge per-project Cobertura into unified HTML report
  - Per-component breakdown (Compiler, Core, Stdlib, CLI, LSP)
  - Trend charts over time
- **CI gates:**
  - `Sharpy.Core`: ≥90% line coverage
  - `Sharpy.Compiler`: ≥80% line coverage
  - `Sharpy.Stdlib`: ≥75% line coverage
  - `Sharpy.Lsp`: ≥70% line coverage
  - No PR may decrease overall project coverage by >1%
  - Coverage diff comment on PRs showing uncovered new lines
- **Local developer experience:**
  - `dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator` → open `coverage/index.html`
  - Claude skill `/coverage` that collects and opens the report
- **Integration with other subsystems:**
  - Coverage data feeds into mutation testing prioritization (low-coverage areas → higher mutation priority)
  - Coverage data feeds into fuzzing target selection (low-coverage compiler stages → more fuzzing time)

**Design considerations:**
- How to exclude generated code from coverage metrics?
- How to handle conditional compilation (`#if NET10_0_OR_GREATER`) in coverage reports?
- Should coverage be collected with or without property/fuzz tests (they inflate coverage numbers)?

---

### Subsystem 6: Spec-Driven TDD Scaffolding Engine

**Goal:** Given a feature description or language spec section, automatically generate test skeletons across all compiler layers, ensuring complete test coverage from the start.

**Design requirements:**

- **Spec reader** — parses `docs/language_specification/` markdown:
  - Extract grammar rules (EBNF/PEG notation in spec) → lexer/parser test expectations
  - Extract type rules and constraints → semantic test expectations
  - Extract code examples → integration fixture seeds
  - Extract "must not" / "error" / "raises" language → error test cases
  - Extract cross-references between spec sections → interaction test cases
- **Test skeleton generator** — produces test files per layer:
  - **Lexer tests:** Assert new `TokenType` is recognized, spans are correct, edge cases (EOF, line boundaries)
  - **Parser tests:** Assert AST node shape (type, children, precedence), error recovery on malformed input
  - **Semantic tests:** Assert type inference results, error codes for invalid usage, scope resolution
  - **Validation tests:** Assert validator triggers correct diagnostic code with correct span
  - **CodeGen tests:** `.expected.cs` skeleton showing expected Roslyn tree shape
  - **Integration fixtures:** `.spy` + `.expected` pairs for golden path + edge cases
  - **LSP tests:** Hover shows correct type, completion includes new syntax, semantic tokens color correctly
- **TDD checklist** — markdown checklist for tracking progress:
  - One item per test, grouped by layer
  - Automatically marks items as done when the test file exists and passes
  - Can be posted as a GitHub issue or PR description
- **Edge case derivation:**
  - Boundary values from type rules
  - Unicode edge cases (emoji identifiers, zero-width characters)
  - Nesting depth limits
  - Interaction with existing features (does the new feature compose with decorators? generics? async?)
- **CLI integration:** `sharpyc scaffold-tests <feature-name>` reads the spec, generates tests, outputs the checklist
- **Claude skill integration:** `/scaffold-tests <description>` for AI-assisted scaffolding with spec awareness

**Design considerations:**
- The spec reader doesn't need NLP — the spec uses consistent formatting (headers, code blocks, admonitions) that can be parsed structurally
- Generated test skeletons should have `[Fact(Skip = "Not implemented")]` so they compile but are skipped until the feature is built
- How to handle spec sections that are ambiguous or underspecified?
- Should the scaffolder also generate the AST node record and empty emitter visit method, or just tests?

---

## Cross-Cutting Concerns

### Configuration

Design a unified configuration system for all six subsystems:
```
sharpy-test-harness.json (or section in .spyproj)
├── coverage: { thresholds, excludes, format }
├── mutation: { targets, thresholds, mutators }
├── fuzzing: { targets, duration, corpus_path }
├── oracle: { python_path, timeout, deviation_catalog }
├── visual: { driver, verifier_backend, golden_path, confidence_threshold }
└── scaffold: { spec_path, output_path, skip_attribute }
```

### Reporting

Design a unified reporting format so all subsystems can feed into a single dashboard:
- Per-PR summary comment combining coverage diff, mutation score, oracle status, fuzz results
- Weekly health report aggregating all metrics
- Trend tracking over time

### Cost Management

Several subsystems have running costs:
- Vision LLM verification: API calls per screenshot
- Cloud fuzzing: compute time
- Mutation testing: CPU hours
Design a budget system with per-subsystem limits and alerts.

---

## Deliverables

For each of the six subsystems, provide:

1. **Architecture diagram** (text/ASCII is fine)
2. **Complete C# interface definitions** with XML doc comments
3. **Data model definitions** (C# records/classes)
4. **Configuration schema** (JSON schema or C# options pattern)
5. **CI workflow additions** (GitHub Actions YAML snippets)
6. **Claude Code skill definitions** (skill YAML + instructions)
7. **Test plan for the harness itself** (yes, test the test infrastructure)
8. **Risk assessment** — what could go wrong, mitigations
9. **Phased implementation roadmap** with dependencies between subsystems

Additionally, provide:

10. **Cross-subsystem integration design** — how the six subsystems share data, configuration, and reporting
11. **Developer experience walkthrough** — narrate a developer's experience using all six subsystems during a typical feature implementation
12. **Migration plan** — how to roll these out incrementally without disrupting existing test workflows

---

## Constraints

- Target .NET 10 / C# 14 for new code (but `Sharpy.Core` interfaces must work under `netstandard2.1` / C# 9.0 if they're consumed by Core)
- xUnit as the test framework (no NUnit or MSTest)
- CsCheck for property testing (no FsCheck)
- GitHub Actions for CI (no Jenkins, CircleCI, etc.)
- Must work on macOS (primary dev) and Linux (CI)
- Vision LLM interface must be abstract — no hard dependency on any specific provider
- All new projects must integrate with the existing `sharpy.sln` solution
- Generated test skeletons must follow existing test conventions (see `src/Sharpy.Compiler.Tests/` for patterns)
- The differential oracle transpiler must be rule-based, not LLM-based (a verification tool must be deterministic)
