# CLAUDE.md

Rules and workflow for Claude Code in this repository. Architecture reference lives in [.github/copilot-instructions.md](.github/copilot-instructions.md), the agent registry in [.github/agents.md](.github/agents.md), and the authoritative language spec in `docs/language_specification/`. `build_tools/` has its own CLAUDE.md.

**GitHub:** `antonsynd/sharpy` · **Pipeline:** `.spy → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL`

## Commands

> **Prerequisites:** .NET 10 SDK (`net10.0` TFM). Python 3.9+ for `build_tools/`.

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # All tests (slow; prefer per-project filters for iteration)
dotnet run --project src/Sharpy.Cli -- run file.spy          # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C# (also: ast, tokens, diagnostics)
dotnet run --project src/Sharpy.Cli -- project x.spyproj     # Multi-file build (--incremental skips unchanged files; --clean forces full rebuild)
```

## Operational Contracts

Each of these exists because violating it has crashed sessions or destroyed work. Do not relax them.

- **Sandbox:** all `dotnet` commands hang in the default sandbox, and `gh` fails TLS certificate verification. Run both with `dangerouslyDisableSandbox: true`.
- **Serialized dotnet:** when multiple agents run in parallel, **NEVER** call `dotnet build`/`dotnet test` directly — use `.claude/scripts/dotnet-serialized` (drop-in wrapper: same args, output, exit code; exclusive **mkdir-based** lock at `~/.claude/locks/dotnet.lock`, atomic on all platforms). Concurrent `dotnet test` runs each consume 5–10 GB RAM; three in parallel will OOM the machine. Enforced by the PreToolUse hook `.claude/hooks/enforce-dotnet-serialized.sh`.
- **Parallel agents share one working tree.** "Read-only" is not a sufficient instruction — it has failed twice; agents read `git restore` as cleanup rather than modification. Enumerate the prohibition in agent prompts: *never run `git checkout` / `restore` / `clean` / `stash` / `reset` / `rm` on repo paths*, and say the tree is shared. A silent revert leaves no stash and no reflog entry, so commit lead work in small slices and re-check `git diff --stat` after each agent wave.
- **Test logs:** the serialized wrapper tees stdout+stderr to `.claude/tmp/dotnet-serialized-{0,1,2}.log` (3-slot rotation, so an older log isn't clobbered by a new run; `-latest.log` symlinks the newest). Grep these instead of re-running suites (~22 min wall clock). `Sharpy.Compiler.Tests` dominates because the `[Trait("Category","GapDiscovery")]` sweeps run by default — filter them out for a fast pass.
- **Prefer skills over raw commands:** `/build`, `/run-tests`, `/spy-emit`, `/spy-run`, `/quick-check`, `/commit`, `/push`, … (the harness injects the full list each session; sources in `.claude/skills/`). Skills handle logging, truncation, and temp files; the `/spy-*` skills accept inline source via the Write tool, avoiding bash-escaping issues with `#` and backticks in Sharpy source. Investigate failures by reading `.claude/tmp/*.log`, not by re-running.

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation.
2. **RoslynEmitter is a pure translator** — `SyntaxFactory` exclusively (no string templating); makes no type/lowering decisions and performs no reflection. All such decisions are made in semantic analysis and materialized for the emitter to read via one of two patterns, each with its own materialization boundary: (a) **symbol-keyed** on `Symbol.CodeGenInfo`, frozen at `MaterializeCodeGenInfo` — use when a discovered `Symbol` owns the fact; (b) **node-keyed** in a `SemanticInfo` dictionary (e.g. `BinaryOpLowering`, `IndexAccessLowering`, resolved CLR member names), merged at `SemanticInfo.MergeFrom` — use when the fact belongs to an AST node with no owning symbol. CLR inspection belongs to `Discovery/ClrTypeBridge`/`ClrTypeHelper`. Enforced by `EmitterCarrierOnlyConformanceTests`: the deny universe is every type in the compiler's `Semantic` namespaces (enumerated by reflection, so it cannot go stale), and CodeGen may name only the materialized-fact carriers, ratcheted per-file-per-type against issues that drain on fix. `EmitterBannedTokenScanTests` is a five-substring backstop, **not** Rule-2 evidence (#1475). **Any new node-keyed `SemanticInfo` dictionary must be added to `SemanticInfo.MergeFrom`** or its entries are silently dropped in the per-file→project merge that code generation reads from.
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes.
4. **Axiom precedence**: .NET > Type Safety > Python Syntax (see Axioms below).
5. **C# 9.0 minimum for Sharpy.Core** — `Sharpy.Core` and `Sharpy.Stdlib` multi-target `net10.0;netstandard2.1`. On `netstandard2.1`: `LangVersion 9.0` (no global usings, file-scoped namespaces, or record structs). On `net10.0`: `LangVersion 14`; use `#if NET10_0_OR_GREATER` for net10.0-only paths. `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0` with `LangVersion latest`.
6. **Always verify Python behavior first** — run `python3 -c "..."` (or `/verify-python`) before implementing Python semantics.
7. **Language spec is authoritative** — check `docs/language_specification/` before implementing; change the implementation to match the spec, never the reverse.
8. **TODO/BUG/FIXME comments must have GitHub issues** — create the issue first (`gh issue create`) and reference it in the comment (`// TODO(#123): ...`).
9. **Warnings are errors** — `TreatWarningsAsErrors` solution-wide via `Directory.Build.props`.
10. **Every active diagnostic code needs a `DiagnosticExplanations` entry** (`Diagnostics/DiagnosticExplanations.cs`, guarded by `DiagnosticExplanationsTests`).

## Axioms

Precedence when the three axioms conflict: **.NET > Type Safety > Python Syntax**. If a conflict resolves at zero cost, satisfy all three. Standing resolutions:

| Conflict | Resolution |
|----------|------------|
| `//` and `%` semantics | Both **floored** (Python semantics, zero cost — see `arithmetic_operators.md`): `//` lowers to `Math.Floor`, `%` to `Sharpy.Builtins.FloorMod`; the divmod identity `a == (a // b) * b + (a % b)` holds |
| String indexing | Axiom 1 wins — UTF-16 code units with helper methods |
| `global`/`nonlocal` | Axiom 1 wins — C# scoping rules apply |
| Duck typing | Axioms 1+3 win — explicit interfaces |

Axiom 1 (.NET-first) governs the **implementation**, not the user-facing API — the surface stays Pythonic.

## Design Anti-Patterns

| Pattern | Problem |
|---------|---------|
| "Add X because Python has it" | Feature creep — each feature must earn its complexity |
| Runtime type checking | Should be compile-time |
| Wrapper types for Pythonic API | Use extension methods instead |
| Multiple ways to do the same thing | Consistency issue |
| Magic behavior | Unpredictable; prefer explicit |
| Raw .NET collections in public APIs | Use `Sharpy.List<T>`, `Dict<K,V>`, `Set<T>` |
| `Optional<T>` return from stdlib | Return nullable; users opt in via `maybe` |
| Throwing for expected failures | Return `Result<T, E>`; reserve exceptions for bugs |

## Implementing Features

Touch components in dependency order:

```
Lexer (Lexer/) → Parser (Parser/Ast/) → Semantic (Semantic/) → Validation (Semantic/Validation/) → CodeGen (CodeGen/) → LSP (src/Sharpy.Lsp/Handlers/) → Tests
```

- **Experimental features** are default-off behind a flag ([docs/design/feature-lifecycle.md](docs/design/feature-lifecycle.md)): register in `FeatureFlags.KnownFeatures`, gate via the `FeatureGateChecker` registry (ungated use → SPY0331), then graduate or delete per that policy.
- **TypeChecker vs. ValidationPipeline**: TypeChecker owns type mismatches and anything needing in-progress inference; validators own self-contained AST analyses. New validators subclass `ValidatingAstWalker` (visitor) or `SemanticValidatorBase` (custom traversal) and set `Order` (full registry in copilot-instructions.md).
- **LSP handlers** must use `Symbol.EffectiveNameLine/Column` for text edits and highlight ranges (falls back from name-token position to statement start).

## Diagnostic Code Allocation

All diagnostics use the `SPY` prefix (`Diagnostics/DiagnosticCodes.cs`):

| Range | Level | Component |
|-------|-------|-----------|
| SPY0001–SPY0099 | Error | Lexer |
| SPY0100–SPY0199 | Error | Parser |
| SPY0200–SPY0399 | Error | Semantic |
| SPY0400–SPY0449 | Error | Validation (fully allocated — overflow to SPY0490–SPY0499) |
| SPY0450–SPY0489 | Warning | Validation |
| SPY0490–SPY0499 | Error | Validation overflow |
| SPY0500–SPY0599 | Error | Code generation |
| SPY0600–SPY0699 | Error | Semantic overflow |
| SPY0700–SPY0799 | Error/Warning | Validation overflow |
| SPY0900–SPY0999 | Error | Infrastructure |
| SPY1000–SPY1099 | Info | Informational |

## Core & Stdlib Conventions

- **Wrap .NET internally, expose the Python API** — `list.append()`, not `Add()`. Partial-class layout: `Partial.{Type}/` directories; `partial class Builtins` split per function (`Print.cs`, `Len.cs`, …). Match Python semantics: negative indexing, slicing, Python-matching exceptions.
- **Error handling:** absence → return `T?` (users opt into `Optional<T>` via `maybe`); expected failure → return `Result<T, E>` with typed errors; bugs → throw. **No dual APIs** — never provide both throwing and Result-returning variants; `try`/`maybe` bridge .NET interop generically.
- **Public API surface:** Sharpy collection types (`List<T>`, `Dict<K,V>`, `Set<T>`) in public signatures — never raw or namespace-aliased .NET collections (internal code may use them). Optional parameters are nullable with `= null` (C#) or `T? = None()` (`.spy`), never `Optional<T>`.
- **`None` semantics:** bare `None` is null for `T?` (nullable); `None()` constructs the `Optional` union variant.
- **Spy-sourced stdlib modules:** for modules in the `MODULES` mapping of `build_tools/regenerate_spy_stdlib.sh`, the C# under `<Module>/` is **generated** from the `.spy` source in `spy/` — never hand-edit it; run the script (CI gate: `check_spy_staleness.sh`).
- **The compiler has zero compile-time dependency on Stdlib** — modules are discovered at runtime via `ModuleRegistry.LoadReference()`.
- **Dunder-driven protocol synthesis:** the emitter implicitly adds `ISized`/`IBoolConvertible`/`IReverseEnumerable<T>` to a class when `__len__`/`__bool__`/`__reversed__` is present (emits SPY1001).

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"                       # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"   # File-based tests
dotnet test --filter "DisplayName~test_name"                          # By test name
```

**File-based fixtures** (`src/Sharpy.Compiler.Tests/Integration/TestFixtures/`; scaffold with `/add-test-fixture`):

- `.spy` + `.expected` (exact stdout match) or `.error` (substring match; a line ending `@line:col` also verifies diagnostic location).
- Multi-file: a subdirectory with a `main.spy` entry point plus `main.expected`/`main.error`.
- `.warning` — empty file means expect **no** warnings; non-empty lines are expected substrings. Combinable with `.expected`.
- `.expected.cs` — C# snapshot (Roslyn-normalized), used for a selective set of representative fixtures. Regenerate via `/regenerate-snapshots` or `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`.
- `.features` — one experimental feature name per line (`main.features` for multi-file, applies project-wide). Unknown names fail fixture discovery loudly. Pair each gated fixture with an ungated twin expecting SPY0331 — see `TestFixtures/experimental/`.
- `.skip` — skip the fixture.

**Programmatic tests** inherit `IntegrationTestBase` and assert on `CompileAndExecute(source)` (`result.Success`, `result.StandardOutput`). Multi-file tests use `ProjectCompilationHelper` (`WithRootNamespace(...).AddSourceFile(...).CreateProjectFile()` then `Compile()`).

**Gap-discovery sweeps** are standing conformance harnesses that hunt whole defect classes (contracts and roster: [docs/design/gap-discovery-contracts.md](docs/design/gap-discovery-contracts.md); run via `/gap-analysis`). They **ratchet** against an allowlist file next to the test: a non-allowlisted failure fails the suite; every allowlist entry cites an issue and is deleted when fixed ("drain on fix" — stale entries fail); allowlists must trend to empty. Never add an entry without an issue reference; never close a member bug by patching one cell — enforce the class contract.

## Git & Release

Use `/commit` and `/push` — `/push` runs the generated-artifact staleness gates (spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger) that CI also enforces. Run `/bump-version` before release-destined pushes.

## MCP Navigation

Filename patterns → Glob; text/regex → Grep. For structural queries prefer the MCP servers when connected (fall back down this list otherwise): `code-review-graph` (`.mcp.json`) for risk-scored review context, impact radius, call chains, and architecture overviews; CodeGraphContext (user-configured) for complexity triage and dead-code queries.
