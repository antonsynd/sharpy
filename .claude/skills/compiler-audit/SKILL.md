---
name: compiler-audit
description: Run a comprehensive compiler health audit — class-contract, materialization, and instrument health, not just file sizes
argument-hint: "[focus-area]"
---

Read `docs/design/verification-contract.md` before proceeding; every dimension below applies it.

Run a health audit of the Sharpy compiler with parallel **read-only** agents, one per dimension, and synthesize a report whose spine is *classes to retire*, not file sizes. Every prompt briefs its agent to **find what is wrong** — an agent reports "no finding" only after naming what it probed and what result would have been red (D2: refute, don't confirm).

If `$ARGUMENTS` is non-empty, narrow to that focus area (e.g. `semantic`, `codegen`, `class`, `instruments`, `lsp`). Otherwise run all dimensions.

## Ground rules for every agent

- **Read-only.** Read files, search, run tests. No edits, no `git` mutations. Shared-tree block (contract §9): *The working tree is shared. Never run `git checkout`, `git restore`, `git clean`, `git stash`, `git reset`, or `rm` on repo paths. REPORT `git status`; do not "make it clean".*
- **dotnet only via the wrapper**, with `dangerouslyDisableSandbox: true` (a PreToolUse hook blocks raw `dotnet`; sandboxed callers exit 125): `.claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~X" --no-build`. Filtered runs are for the agent's own probes.
- **The whole-solution run happens once**, started by the lead in the background before spawning (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"`, ~22 min). Hand every agent the log path (`.claude/tmp/dotnet-serialized-N.log`, the wrapper prints which slot) — agents read it, they do not re-run it. Note the log's binaries must postdate HEAD's build; a log older than the last build is not a measurement.
- **Every count carries its sha**: `value @ <sha> (measured)`, never derived. `git rev-parse --short HEAD` first.
- **MCP fallback stated.** Prefer `code-review-graph` (`.mcp.json`) for impact radius / call chains / architecture overview and CodeGraphContext (user-configured) for complexity and dead-code queries when connected; each prompt names the Grep/Read fallback to use when the server is absent. Never report a query as run if the server was not connected.
- **Output shape:** Critical / Warning / Opportunity, each finding with the file:line, the probe that produced it, and what a non-finding would have looked like.

## Steps

### 1. Baseline and harness check

1. `git rev-parse --short HEAD`; `git status --short` (REPORT it; a dirty tree is a fact for the report, not something to fix).
2. Start the whole-solution gate in the background via the wrapper; record the log slot.
3. Check the session's tool list for team/task tools (`TeamCreate`, `TaskCreate`, `TaskUpdate`, `TaskList`). If present, a `compiler-audit` team with one task per dimension is fine. If absent (the common case), spawn each dimension as a background `Agent` and keep the dimension checklist in your own scratch notes — there is no task board.
4. Locate the **previous audit** in `docs/audits/` (`ls -t docs/audits/audit-*.md | head -1`; two prior reports exist) — its census tables are the trend baseline.

### 2. Dimensions

Spawn all selected dimensions simultaneously. Agent types: `code-reviewer` (sonnet), `verification-expert` and `net-axiom-guardian` (haiku by default — pass `model: sonnet` or higher for the three new dimensions below; they are refutation briefs, not count-and-report), `Explore` for metrics.

| Dimension | Agent | Focus |
|---|---|---|
| **Class-contract health** (new) | `verification-expert`, `model` raised | Allowlist census + trend, stale entries, unclosed SPY0908/CS-leak classes, sweep coverage %, seed discipline |
| **Mirrored facts & materialization** (new) | `code-reviewer` | `SemanticInfo` dictionaries vs `MergeFrom`, `EmitterCarrierOnly` ratchet, CodeGen improvisation sites (#1618 shape) |
| **Instrument health** (new) | `verification-expert`, `model` raised | Vacuous tests, `Skip =` and `.skip` censuses, generated-artifact staleness at HEAD |
| Architecture & Modularity | `code-reviewer` | Coupling, duplication, TypeChecker/ValidationPipeline/Lowering/emitter seam violations |
| Type Safety & Correctness | `verification-expert` | Gate results from the shared log, error-recovery paths, diagnostic coverage |
| .NET Compliance | `net-axiom-guardian` | C# 9.0 on `netstandard2.1`, `#if NET10_0_OR_GREATER` discipline, type-mapping completeness |
| Testing Health | `verification-expert` | Fixture/test census by kind, coverage gaps by component |
| Codebase Metrics | `Explore` (very thorough) | Large files, TODO/FIXME issue compliance, unused diagnostic codes, churn |
| LSP Health | `verification-expert` | Handler/test coverage, thread safety, position conversion, `FrontEndParityTests` allowlist |
| Future Readiness | `Explore` (very thorough) | Parallel-build / REPL / formatter / debugger readiness |

Prompts follow. Paste the **Ground rules** block above into each.

**Class-contract health** (`verification-expert`, raised model):
```
Refute the claim "every defect class in docs/design/gap-discovery-contracts.md is retired or ratcheting toward zero." HEAD = <sha>.

1. Census. Enumerate every allowlist by glob: `find src -name '*allowlist*.txt' -not -path '*/bin/*' -not -path '*/obj/*'`. Count lines (excluding blank/comment lines) in each and report `count @ <sha> (measured)`.
   Also count the EmitterCarrierOnly ratchet array in src/Sharpy.Compiler.Tests/CodeGen/EmitterCarrierOnlyConformanceTests.cs (`Ratchet =`) and the DeliberatelyPermissive ratchet in src/Sharpy.Compiler.Tests/Conformance/deliberately-permissive-allowlist.txt.
   Trend against (a) the "Starting census (measured @ 8bacf3d34)" table in docs/design/spy0908-policy.md and (b) the previous audit at <path>. A count that rose without a cited issue is Critical.
2. Stale entries (drain-on-fix debt). For each allowlist entry, resolve its issue reference (`gh issue view N --json state,title`, unsandboxed). An entry citing a CLOSED issue is a stale entry — the fix should have deleted it in the same commit. An entry with no issue reference is a contract violation. List both.
3. Unclosed classes. `gh issue list --state open --search "SPY0908" --limit 200` and the same for "CS0", "CS1", "ICE", "silently". Group the results BY CLASS using the meta-class table in verification-contract.md §1 (SPY0908 as a net / silent wrong behavior / qualified-bare & alias / position missing a shared check / warm≠cold / vacuous instrument). Output is a class table (class → member issues → standing harness or "none"), not an issue list. A class with ≥2 open members and no standing harness is Critical.
4. Sweep coverage. From the shared gate log, extract the coverage statements the sweeps print (DifferentialExecutionTests prints "Fixture coverage: N/M eligible run (P%)"; MetamorphicCorpusSweepTests and InteropConformanceTests print their cell counts). Report each `@ sha`. Coverage below the contract's stated target (100% for the differential fixture arm since #1202) is Warning.
5. Seed discipline: does `PropertySeedDisciplineTests.NoUnseededRandom_InPropertyTests` pass in the gate log? Any `Random()` without a seed in src/Sharpy.Compiler.Tests/Properties/ is Critical.
6. Positive control: name one allowlist entry you resolved to an OPEN issue, so the stale-entry check is shown to discriminate.
Report "no finding" for a step only with the probe you ran and the red it would have produced.
```

**Mirrored facts & materialization** (`code-reviewer`):
```
Refute the claim "every fact the emitter reads is materialized by semantic analysis and survives the per-file → project merge" (CLAUDE.md Rule 2). HEAD = <sha>.

1. MergeFrom scan (mechanical). In src/Sharpy.Compiler/Semantic/SemanticInfo.cs list every `ConcurrentDictionary<...> _field` (grep `ConcurrentDictionary<`; ~55 at last count). Then read `MergeFrom` (grep `void MergeFrom`) and check that every field appears in it. Any field absent from MergeFrom whose entries codegen reads is Critical (its entries are silently dropped in multi-file builds). Fields deliberately excluded must have a comment saying why — quote it. Cross-check: src/Sharpy.Compiler.Tests/Project/ProjectConfigWithInMemorySourcesTests.cs and CodeGen/EmitterCarrierOnlyConformanceTests.cs reference MergeFrom — say whether a guard exists that would go red for a new unmerged dictionary, or whether this scan is the only guard.
2. EmitterCarrierOnly ratchet state. Read the `Ratchet` and `ReadOnlyCatalogs` arrays in EmitterCarrierOnlyConformanceTests.cs: entries `@ sha`, each with its issue and state (`gh issue view`). Ratchet entries citing closed issues = stale. Confirm from the gate log that `EmitterCarrierOnlyConformanceTests` passed at HEAD. EmitterBannedTokenScanTests is a five-substring backstop, not Rule-2 evidence (#1475) — do not cite it as such.
3. CodeGen improvisation sites (the #1618 census shape — "lowering facts codegen needs but semantic never materializes", closed audit). In src/Sharpy.Compiler/CodeGen/ find places where the emitter decides a type, a lowering, or a CLR member name locally instead of reading `Symbol.CodeGenInfo` or a node-keyed `SemanticInfo` lowering: probes = `GetExpressionSemanticType(` followed by a branch on the result; `is GenericType`/`is UserDefinedType` pattern matches; string comparisons on type names; `switch` over `SemanticType` kinds; any `typeof(`/`GetMethod(`/`GetProperty(` (reflection). Report each site as unmaterialized-lowering debt with file:line and the carrier it should read instead. Use code-review-graph `get_impact_radius`/call chains on the sites if connected; fallback is Grep over CodeGen/ plus reading the enclosing method.
4. Lowering stage. src/Sharpy.Compiler/Lowering/ runs after ValidationPipeline and before RoslynEmitter (ProjectCompiler.CodeGen.cs `BuildLoweringIr`). List which emitter reads come from IR side-tables (FoldedConstants, optimized comprehensions, stack-allocated literals) vs directly from SemanticInfo; a decision duplicated in both places (mirrored fact) is Warning.
5. Positive control: name one dictionary you confirmed IS in MergeFrom and one carrier read you confirmed IS materialized, so the scan is shown to discriminate.
```

**Instrument health** (`verification-expert`, raised model):
```
Refute the claim "a green run means the guards fired." HEAD = <sha>. Exclude bin/ and obj/ from every search.

1. Vacuous-pass candidates in src/*.Tests/ (not bin/obj):
   - Absence assertions with no positive control: `Should().NotContain(`, `Should().BeEmpty(`, `DoesNotContain(`, `.Should().HaveCount(0)` where the same test (or a sibling) never shows the probe hitting on a known-present input. List the file:line and whether a positive control exists in the same class.
   - `[Fact]`/`[Theory]` methods with no `Assert`/`Should()`/`Throws` in their body.
   - Drain loops / conditional assertions: `if (…) return;` before the first assertion, `foreach` whose body asserts only when a collection is non-empty, try/catch that swallows the assertion.
   - Guards whose exemption is the subject (contract §2): a test that exempts via the same predicate the production code uses.
   For each candidate say what mutation would prove it live; do NOT perform mutations (read-only).
2. `Skip =` census: `grep -rn 'Skip *=' src --include='*.cs' | grep -v '/bin/\|/obj/'` — each with its issue reference or "none" (an unreferenced skip is a contract §8 violation). Note the two in TestFixtures/unittest/test_skip.expected.cs are a fixture's expected output, not skipped tests.
3. `.skip` fixture census: `find src -name '*.skip' -not -path '*/bin/*'` (~54 at last count) — group by directory, each with the issue its content cites or "none".
4. Generated-artifact staleness at HEAD, using the same checks /push runs (all unsandboxed, wrapper for anything dotnet):
   `bash build_tools/check_spy_staleness.sh`; `bash build_tools/check_spy_tests_staleness.sh`;
   `python3 -m build_tools stdlib generate --force` then `git status --short -- docs/stdlib` (REPORT the dirty files; do not revert them);
   `python3 -m build_tools.cpython_oracle ledger --write` then `git status --short -- build_tools/cpython_oracle/ledger.yaml`.
   Any STALE/MISSING or dirty result is Critical (a fossil artifact votes green in CI — contract §7). State which checks you could not run and why.
5. Gate-log sanity: from the shared log confirm the run's build timestamp postdates the HEAD commit and that every test project appears (`Test run for …` lines for Cli, Core, Compiler, Stdlib, Lsp). A missing project is a silent instrument.
6. CiFilterCoverageConformanceTests (src/Sharpy.Compiler.Tests/Conformance/): does it pass, and does every `[Trait("Category","GapDiscovery")]` class appear in a dotnet10.yml step or in its documented exclusion list?
7. Positive control: name one absence assertion that DOES have a positive control, so the scan is shown to discriminate.
```

**Architecture & Modularity** (`code-reviewer`):
```
Find seam violations and structural debt in src/Sharpy.Compiler/ and src/Sharpy.Lsp/. HEAD = <sha>.
- Seams: TypeChecker vs ValidationPipeline (type mismatches and in-progress inference belong to TypeChecker; self-contained AST analyses to validators — find a validator doing inference or a TypeChecker check that is a self-contained walk); Semantic vs CodeGen (Rule 2 — the emitter makes no type/lowering decisions; overlap with the Materialization dimension is fine, cite it); Lowering/ vs CodeGen (a transform open-coded in the emitter that lowering-ir.md assigns to the IR).
- Coupling: circular dependencies between Semantic/, CodeGen/, Lowering/, Parser/. Use code-review-graph `get_architecture_overview`/`get_impact_radius` if connected; fallback: grep `using Sharpy.Compiler.` per directory and read the offending files.
- Duplication: parallel-site structures (the #1145 class — mirrored facts kept in two switch statements). Probe: two `switch`/`if` chains over the same enum or node kind in different files; report pairs with no completeness scan guarding them.
- Large files: list files > 800 lines with a one-line reason each is or is not a split candidate (a partial-file split is not a finding by itself).
Do not report file or partial-class counts; they rot.
```

**Type Safety & Correctness** (`verification-expert`):
```
Refute the claim "the compiler is correct where it is silent." HEAD = <sha>.
- Gate results: read the shared log at <log path> (do not re-run). Report passed/failed/skipped per project `@ sha (measured)` and every failure with its test name. Confirm the log's build postdates HEAD.
- Error recovery: in Parser/ and Semantic/TypeChecker*.cs find `UnknownType`/error-node paths that swallow a diagnostic (an Unknown assigned without a diagnostic being reported is a silent net — the SPY0908 class). Probe: grep `UnknownType.Instance` and check that each producing site reports or inherits a diagnostic.
- Diagnostic coverage: every code in Diagnostics/DiagnosticCodes.cs is (a) reported somewhere (grep the constant) and (b) has a DiagnosticExplanations entry; list codes failing either.
- SemanticInfo integrity: expression kinds that reach codegen with no `_expressionTypes` entry — probe with `.claude/scripts/dotnet-serialized run --project src/Sharpy.Cli -- run <file>` on 3 small programs exercising unusual expression shapes (walrus, nested comprehension, conditional lambda); an SPY0908 is a finding (it surfaces only under `run`, never `emit`).
```

**.NET Compliance** (`net-axiom-guardian`):
```
Find Axiom-1 violations. HEAD = <sha>.
- Sharpy.Core and Sharpy.Stdlib multi-target `net10.0;netstandard2.1`: LangVersion 9.0 on netstandard2.1 (no global usings, file-scoped namespaces, record structs), LangVersion 14 on net10.0 behind `#if NET10_0_OR_GREATER`. Probe: grep both projects for `global using`, `namespace .*;$`, `record struct`, and for APIs absent from netstandard2.1 used outside an `#if NET10_0_OR_GREATER` block. Confirm with the wrapper: `.claude/scripts/dotnet-serialized build src/Sharpy.Core -f netstandard2.1`.
- Generated code is C# 9.0: grep CodeGen/ for SyntaxFactory calls that produce C# 10+ syntax (FileScopedNamespaceDeclaration, RecordStructDeclaration, raw string literals, list patterns) — each is Critical unless gated.
- Type mapping completeness: CodeGen/TypeSyntaxMapper.cs and Discovery/ClrTypeMapper.cs — a SemanticType kind or CLR primitive with no mapping arm.
State the probe for every "no finding".
```

**Testing Health** (`verification-expert`):
```
Census the test estate at HEAD = <sha> (exclude bin/obj) and find the components with no guard:
- Fixture counts: `.spy`+`.expected`, `.spy`+`.error`, `.spy`+`.warning`, `.expected.cs` snapshots, multi-file dirs, `.features`, `.skip` — per test project.
- Programmatic tests per component directory (Lexer, Parser, Semantic, Validation, CodeGen, Lowering, Project, Lsp, Core, Stdlib) vs source lines in the matching src directory; flag ratios far below the median.
- Negative coverage: diagnostic codes with no `.error` fixture or programmatic test asserting them.
- Conformance harnesses present per class in docs/design/gap-discovery-contracts.md — a class row whose harness file is missing or whose test is skipped is Critical.
All counts `@ sha (measured)`.
```

**Codebase Metrics** (`Explore`, very thorough):
```
Metrics for src/Sharpy.Compiler/, src/Sharpy.Core/, src/Sharpy.Stdlib/ at HEAD = <sha> (exclude bin/obj/generated):
- 15 largest .cs files with line counts; git churn hotspots (`git log --since=90.days --name-only --format= | sort | uniq -c | sort -rn | head -20`).
- TODO/FIXME/BUG audit: every comment and whether it references an issue (`TODO(#123)`); unreferenced ones are Rule-8 violations. Resolve referenced issues (`gh issue view`, unsandboxed) — a TODO citing a CLOSED issue is stale.
- Unused diagnostic codes (defined in DiagnosticCodes.cs, never referenced outside it and DiagnosticExplanations).
- Magic strings in CodeGen/ and Semantic/ that name CLR members or Sharpy.Core types outside Shared/CSharpTypeNames.cs.
CodeGraphContext `find_most_complex_functions`/`find_dead_code` if connected; fallback: the greps above plus `wc -l`.
```

**LSP Health** (`verification-expert`):
```
Find LSP defects at HEAD = <sha>:
- Handler coverage: src/Sharpy.Lsp/Handlers/ vs standard LSP features; tests per handler in src/Sharpy.Lsp.Tests/; handlers with none.
- Front-end parity: src/Sharpy.Lsp.Tests/Conformance/FrontEndParityTests.cs and its allowlist frontend-parity-allowlist.txt — entries `@ sha`, each with issue state; stale entries are findings.
- Thread safety in LanguageService.cs / SharpyWorkspace.cs (locking, ConcurrentDictionary, cancellation propagation); position conversion (PositionConverter.cs, 0-based LSP vs 1-based compiler); DiagnosticPublisher mapping; ICodeActionProvider implementations and their tests; incremental analysis (AstFingerprint, ScopedTypeChecker) — a partial re-analysis path that skips a validator is Critical.
- Text-edit positions use `Symbol.EffectiveNameLine/Column` (CLAUDE.md) — grep handlers for `NameLine`/`Line` used directly.
```

**Future Readiness** (`Explore`, very thorough):
```
Rate Ready / Partial / Not Ready with evidence at HEAD = <sha>:
- Parallel compilation (docs/design/parallel-compilation.md): mutable statics or shared state in CompilerServices, SymbolTable, SemanticInfo, registries.
- REPL: statement-level compilation entry points; partial-input tolerance in Parser.
- Formatter: trivia/whitespace preservation through Lexer → Parser; roundtrip capability.
- Debugger: TextSpan/location preservation into emitted C# (#line directives or equivalent).
```

### 3. Synthesize

Wait for every agent. Before writing, do the two lead-side checks:

- **Census cross-check:** the class-contract agent's allowlist counts must equal your own `wc -l` (minus blank/comment lines) on the same files at the same sha. A mismatch is a finding about the instrument, and the report says which number is measured.
- **Gate log:** confirm the background run finished, its binaries postdate HEAD, and all five test projects reported. Cite `passed/failed/skipped @ <sha> (measured)`.

Report structure:

```markdown
# Sharpy Compiler Health Audit — YYYY-MM-DD @ <sha>

## Executive Summary
(2–3 paragraphs: overall health, the classes still open, top three actions)

## Classes to retire

| Class (contract) | Member issues (open) | Standing harness / allowlist | Standard cure (verification-contract.md §1) | Guard that proves retirement |
|---|---|---|---|---|
| e.g. SPY0908 as a net — bare type in value position | #… | none | semantic-time refusal | `.error` fixture per position + ILCompiles seed stays green |

(One row per class. "Guard that proves retirement" names the test that must go red if the class recurs, and how it was shown live — contract §2.)

## Critical Findings
## Warnings
## Opportunities
(each finding: file:line, probe, agent, what a non-finding would have looked like)

## Metrics Dashboard (all @ <sha>, measured)

| Metric | Value | Previous audit | Trend |
|--------|-------|----------------|-------|
| Gate: passed / failed / skipped | | | |
| Allowlist entries — generic-reference / interop / metamorphic / path-agreement / rewrite-shadowing / warm-diagnostic-fidelity / qualified-bare / differential-exec / frontend-parity / deliberately-permissive / EmitterCarrierOnly ratchet | | spy0908-policy.md census @ 8bacf3d34 | |
| Stale allowlist / ratchet entries (closed issue) | | | |
| Allowlist entries with no issue ref | | | |
| Open SPY0908/CS-leak issues, by class | | | |
| Sweep coverage % — differential fixture arm / metamorphic cells / interop cells | | | |
| SemanticInfo dictionaries / present in MergeFrom | | | |
| CodeGen improvisation sites (unmaterialized-lowering debt) | | | |
| Vacuous-pass candidates (no positive control / no assert / drain loop) | | | |
| `Skip =` tests (with issue / without) | | | |
| `.skip` fixtures (with issue / without) | | | |
| Generated artifacts stale at HEAD | | | |
| TODO/FIXME (with issue / without / citing closed issue) | | | |
| Diagnostic codes unused / without explanation | | | |
| Largest file (lines) | | | |
| File-based fixtures / snapshots | | | |

## Detailed Findings
### Class-contract health
### Mirrored facts & materialization
### Instrument health
### Architecture & Modularity
### Type Safety & Correctness
### .NET Compliance
### Testing Health
### Codebase Metrics
### LSP Health
### Future Readiness
(full agent report under each)

## Instrument notes
(which probes could not run and why; census cross-check result; gate-log provenance)
```

### 4. Save and present

1. Write the report to `docs/audits/audit-YYYY-MM-DD.md` (the directory exists; keep the path).
2. Present the Executive Summary, the Classes-to-retire table, and the Critical findings inline; point to the file for the rest.
3. If a team was created, shut down teammates via `SendMessage` (`shutdown_request`) and delete the team; background `Agent`s need no cleanup.
4. Do not open issues from the audit automatically — list the classes without a tracking issue as the first action item for the owner.
