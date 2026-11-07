# CLAUDE.md

Architecture: [docs/architecture.md](docs/architecture.md). "Contract §N": [verification-contract.md](docs/design/verification-contract.md).

**GitHub:** `antonsynd/sharpy` · **Pipeline:** `.spy → Lexer → Parser (AST) → Semantic → ValidationPipeline → Lowering (IR) → RoslynEmitter → C# → .NET IL`

## Commands

Needs .NET 10, Python 3.9+. Prefer skills: `/build`, `/run-tests`, `/spy-run`, `/spy-emit`, `/quick-check`, `/ci-gate`, `/commit`, `/push`.

```bash
.claude/scripts/dotnet-serialized build sharpy.sln
src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run file.spy
src/Sharpy.Cli/bin/Debug/net10.0/sharpyc emit csharp file.spy
src/Sharpy.Cli/bin/Debug/net10.0/sharpyc project x.spyproj
.claude/scripts/dotnet-serialized test src/Sharpy.Compiler.Tests --filter "DisplayName~name" --logger "console;verbosity=normal"
```

**Inner loop:**
1. Build once (`/build`).
2. Probe with the built `sharpyc` (a stale one reproduces fixed bugs).
3. One fixture: the `DisplayName~` line above.
4. The owning test project, filtered (`FullyQualifiedName~X`).
5. `/ci-gate` at phase close.
6. Read the log the wrapper printed; do not re-run.

**Cloud session:**
- No `dotnet`: `dotnet-install.sh --channel 10.0 --install-dir /root/.dotnet`; `export DOTNET_ROOT=/root/.dotnet`.
- `gh issue`/`gh pr` return 403: read with `gh api -X GET …`; write with `gh api -X POST …/issues/N/comments -F body=@file.md`.
- `gh api --paginate` is refused: page with `&page=N`.
- Run dotnet and gh with `dangerouslyDisableSandbox: true`.

## Operational Contracts

- **Serialized dotnet:** never call `dotnet build`/`test`/`run` directly; use `.claude/scripts/dotnet-serialized` (same args, output, exit code), enforced by a PreToolUse hook.
- **Parallel agents share one working tree.** Tell agents *never run `git checkout` / `restore` / `clean` / `stash` / `reset` / `rm` on repo paths*. Commit in small slices; re-check `git diff --stat` after each wave.
- **Test logs:** every `.claude/scripts/dotnet-serialized` call tees stdout+stderr to its own `.claude/tmp/dotnet-serialized-<yyyymmdd-HHMMSS>-<pid>.log` and prints `dotnet-serialized: log -> <path>` as its first stdout line, before it queues. `.claude/tmp/dotnet-serialized-latest.path` names the newest invocation's log, which may be a peer's. Read your own printed path, and grep it instead of re-running a suite. Logs older than 7 days are deleted. A waiter reaps a dead holder's dotnet child once that child's log has been silent past the stall window. Timings and lock facts: the wrapper's header.
- **Plans** are scratch in `.claude/plans/` until `/verify-plan` commits them ([process](docs/process/README.md)).

## Critical Rules

1. **Never modify expected values to make tests pass**; fix the implementation.
2. **RoslynEmitter is a pure translator**: `SyntaxFactory` only, no type/lowering decisions or reflection. Semantic analysis materializes (a) symbol-keyed on `Symbol.CodeGenInfo` (frozen at `MaterializeCodeGenInfo`) or (b) node-keyed in `SemanticInfo`, where **a new dictionary must join `MergeFrom`** or its entries are dropped. CLR inspection: `ClrTypeBridge`/`ClrTypeHelper`. [Guards](docs/architecture.md#code-generation).
3. **Immutable AST**: annotations go in `SemanticInfo`.
4. **Axiom precedence**: .NET > Type Safety > Python Syntax.
5. **C# 9.0 minimum for Sharpy.Core**: Core/Stdlib also target `netstandard2.1` (no global usings, file-scoped namespaces, record structs); net10.0-only code goes under `#if NET10_0_OR_GREATER`.
6. **Verify Python behavior first** (`python3 -c` or `/verify-python`).
7. **The spec is authoritative**: read `docs/language_specification/` first; change the implementation, never the spec.
8. **TODO/BUG/FIXME need an issue**, created first: `// TODO(#123): ...`.
9. **Warnings are errors** (`Directory.Build.props`).
10. **Every active diagnostic code has a `DiagnosticExplanations` entry.**
11. **Fix the class, not the cell**: name the contract and its cell matrix; a repro list is a symptom report (contract §1).
12. **Guards are falsifiable**: mutation-test every new guard (break -> red, restore -> green, both in the commit body); the exemption is never the subject; absences need a positive control; refusals are verified by direction with `run` (contract §2–§3).
13. **One group per plan, one gate**: chores only when seam-free; output/layout changes land alone; a residue group or ≥2-axis × ≥3-value matrix builds a generator first; the tier (`band`/`light`/`full`) is a charter fact, never lowered (contract §11–§12).
14. **File by class, label at creation** (`lane:*` + `reach:*`): one class issue (cell table) or a tracker comment; lane agents append to the ledger; the lead files per wave; sibling-cell probing gets the matrix plus two axis changes; a DEVIATION-by-default divergence is a `docs/deviations.yaml` row (contract §10, [fidelity scope](docs/design/python-fidelity-scope.md)).

## Axioms

Satisfy all three when free. Axiom 1 governs the implementation, not the Pythonic surface.

| Conflict | Resolution |
|---|---|
| `//`, `%` | Floored, as Python (`Builtins.FloorDiv`/`FloorMod`) |
| String indexing | Axiom 1: UTF-16 code units, helper methods |
| `global`/`nonlocal` | Superseded by R-KD: N6 (#2424) reinstates them; SPY0134 until it lands ([name-resolution](docs/design/name-resolution.md)) |
| Duck typing | Axioms 1+3: explicit interfaces |

## Design Anti-Patterns

Avoid "add X because Python has it"; runtime type checking; wrapper types (use extension methods); multiple ways to do the same thing; magic behavior; raw .NET collections in public APIs; `Optional<T>` stdlib returns; throwing for expected failures.

## Implementing Features

Work in pipeline order, then LSP, then tests. Experimental features are default-off: `FeatureFlags.KnownFeatures`, `FeatureGateChecker` (SPY0331), [lifecycle](docs/design/feature-lifecycle.md). TypeChecker vs. validators, LSP positions: architecture.

**Diagnostics** (`DiagnosticCodes.cs`, errors unless noted): Lexer 0001-0099; Parser 0100-0199; Semantic 0200-0399, 0600-0699; Validation 0400-0449, 0490-0499, 0700-0799 (also warnings), warnings 0450-0489; CodeGen 0500-0599; Infra 0900-0999; Info 1000-1099.

## Core & Stdlib Conventions

- **Wrap .NET, expose Python** (`list.append()`, not `Add()`; negative indexing, slicing, Python exceptions).
- **Errors:** absence -> `T?`; expected failure -> `Result<T, E>`; bugs -> throw. No dual APIs; `try`/`maybe` bridge .NET.
- **Public API:** Sharpy `List`/`Dict`/`Set`; optional parameters `= null` / `T? = None()`, never `Optional<T>`. Bare `None` is null; `None()` is the `Optional` variant.
- **Spy-sourced modules**: C# is generated by `build_tools/regenerate_spy_stdlib.sh`; never hand-edit it.
- **No compile-time compiler -> Stdlib reference** (`ModuleRegistry.LoadReference()`).
- **Layer ladder:** a Python behaviour goes on the lowest rung that carries it, named in the plan: (1) Sharpy source, (2) Core/Stdlib C# in `dunder_methods.md` spelling, (3) a CLR-keyed bridge rule, (4) a name-keyed rule (last resort). [More](docs/architecture.md#sharpystdlib).

## Testing

**Fixtures** (`Integration/TestFixtures/`, `/add-test-fixture`): `.spy` + `.expected` (exact stdout) or `.error` (substring; `@line:col` checks location); multi-file = a dir with `main.spy`; `.warning` (empty = none); `.expected.cs` snapshot (`/regenerate-snapshots`); `.features` (with an ungated SPY0331 twin); `.skip`.

**Gap-discovery sweeps** ratchet on an allowlist; each row cites an open issue, deleted on fix ([gap-discovery-contracts](docs/design/gap-discovery-contracts.md)). **Spec blocks** compile or carry a marker (`check_spec_blocks.sh`).

**Commit gate** = the whole solution: `/ci-gate` on the pushed tip. On exit 3, the local fallback, in the background:

```bash
.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"
```

Report `passed/failed/skipped @ sha (measured)`; a red needs a control run at the base. Timings, blast radius: contract §5–§6.

## Git & Release

`/commit`, `/push` (runs CI's staleness gates); `/bump-version` before release-destined pushes.
