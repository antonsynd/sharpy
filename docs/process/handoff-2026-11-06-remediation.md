# Remediation Handoff — from the 2026-11-06 health check to the orchestrator

> **For:** a Fable 5.1 orchestrator session that coordinates Opus subagents (each may spawn its own for verification). Starts 2026-11-06.
> **From:** the health-check session (`docs/audits/health-check-2026-11-06.md`, artifact `NBauE8wv83sCoatKnUzf9o`), which also filed #2443 and placed it in the charter as P82.
> **Authority:** owner rulings in the charter §4 (R-AW … R-LK) and on #2443 are binding and are not re-asked. Everything below that needs a new ruling is in §7, "Decisions needed", and waits.
> **This is not a plan and not a charter.** It is the brief that tells the orchestrator what to do first, how to tell whether it is working, and when to stop.

---

## 1. Intent

Three goals, in priority order. Every item in §4 names which goal it serves; an item that serves none is cruft and is dropped.

1. **Stable and trustable.** Programs that compile run the way Python would (silent-wrong is the worst class), programs that should compile do, and the compiler never leaks a C# error. The structural lever is #2443 (the lowering IR becomes the emitter's only input), because the SPY0908 class (76 open, ~340 filed in six months) regenerates from 88 node-keyed side-tables. The immediate levers are the live bugs the audit reproduced that no harness owns.
2. **Efficient sessions.** A fresh session, cloud or local, can build, probe a program in under a second, run the owning test project, and read the tracker's vocabulary, without hitting a contradiction between CLAUDE.md and a hook or spending the first hour on procedure.
3. **Less process per unit of fixed behaviour, not more.** The audit measured October at 42% `test` commits and 3% `feat`, closures down from 483 to 113 a month, allowlist rows up 3.4× in three weeks, and about 130 KB of procedure to read per group. The remediation must bend those lines. A wave that fixes bugs while making the process heavier has failed on goal 3 even if it passed on goal 1.

### Non-goals

- No new hand-written matrix tests (Rule 13 already says this; the 124 "residue" titles say it is not followed).
- No new node-keyed `SemanticInfo` tables (R-LK). A new lowering fact is an IR node property, or it waits for P82.
- No new process document, skill, label, tier letter or ruling vocabulary beyond the per-wave recap in §6. If something must be written down, it goes in an existing file.
- No re-ordering of the charter's execution order without an owner ruling. The audit's items are a parallel band of zero-seam and Core work, not a new queue.
- No whole-solution test run in a container under 16 GB. The Compiler test host peaks at 16.1 GB; use CI shards or the owner's machine for the gate, and the owning project locally.

---

## 2. Before touching anything: reconcile in-flight work

The owner started task cards from the audit on 2026-11-06. Some of the items below may already have a branch, a PR or a commit. **Check first, then claim.**

```bash
git fetch origin --prune
git branch -r | grep -v mainline
gh api -X GET "repos/antonsynd/sharpy/pulls?state=open" --jq '.[] | [.number, .head.ref, .title] | @tsv'
gh api -X GET "repos/antonsynd/sharpy/issues/2443/comments" --jq '.[].body' | head -50
```

Task cards the owner started (check their state before duplicating):

| Card | Item below | What it does |
|---|---|---|
| Fix three dead Claude Code hooks | W0.1 | hooks read `tool_input.file_path` from stdin; mutation-tested |
| Add compilation options to the incremental cache key | W1.3 | features / references / TFM / configuration in `ProjectFactsOf`; cold/warm axes |
| Make dict preserve insertion order after removal | W1.1 | compact ordered `Dict<K,V>`; property test vs the Python model |
| Freeze SemanticInfo and assert disjoint MergeFrom | W0.5 | the band half of #2443 |
| Point the plan workflow at docs/process | W3.3 | 19 references in CLAUDE.md and skills |

Also: the branch `claude/inspiring-euler-0rexli` holds the audit, the committed charter (`docs/process/rounds/`) and the process README. **Merging it to `dev` and `mainline` is step zero**; nothing below is readable to other sessions until it lands.

If an item is in flight, the orchestrator's job for it is review and gate, not a second implementation. Post on its PR or issue; do not open a parallel branch.

---

## 3. Operating rules for the orchestrator

These are the project's existing rules, restated because each has been violated by a session before and the audit found the consequences.

- **Models.** The lead is Fable; workers are Opus (`model: "opus"`). A worker may spawn its own Opus sub-workers for refutation or measurement. Never Sonnet or Haiku for compiler work.
- **Shared tree.** Workers never run `git checkout` / `restore` / `clean` / `stash` / `reset` / `rm` on repo paths. Say this in every worker prompt verbatim; "read-only" has failed twice. Commit lead work in small slices and re-check `git diff --stat` after each worker wave.
- **dotnet.** Only through `.claude/scripts/dotnet-serialized`, with `--logger "console;verbosity=normal"` on any test run (the stall watchdog depends on it). In a cloud container: install with `dotnet-install.sh --channel 10.0 --install-dir /root/.dotnet`, set `DOTNET_ROOT=/root/.dotnet`, and use the prebuilt `src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` for probes (0.75 s, no lock). `gh issue` returns 403 in cloud sessions; use `gh api`.
- **Rule 1 and Rule 12.** Never change an expected value to pass. Every new guard is mutation-tested (break → red, restore → green) and the record goes in the commit body. A guard with a SKIP path asserts that the SKIP path is not what ran.
- **Python first.** `python3 -c "..."` before implementing any Python semantics (Rule 6). Oracle is 3.12 in CI, 3.13 locally; say which you used.
- **Class, not cell.** A bug found while working is a comment on its class tracker or one class issue with a cell table, labeled `lane:*` + `reach:*`. Workers append to a findings ledger in the lead's scratchpad; the lead files once per wave.
- **Record in the open.** Rulings and findings go to issues and to the committed charter. Nothing that another session needs may live only in a scratchpad or `.claude/plans/`.
- **Budget per item.** At most two whole-solution gates (one at base if the item changes emitted output, one at final). A plan, if one is needed, is at most 15 KB. Procedure reading is CLAUDE.md plus the item's own issue; not the full skill chain unless the item is `full` tier in the charter.

---

## 4. Work program

Items are grouped in waves. A wave ends with the scorecard in §5. Items within a wave are independent unless a dependency is stated and can run in parallel across workers. Each item lists the goal it serves, its charter status, its done-when, its guard, and its size.

### Wave 0 — today: zero-seam tooling and the band half of #2443

No emitted C# changes in this wave. One gate at the end, shared.

| # | Item | Goal | Charter | Done-when | Guard | Size |
|---|---|---|---|---|---|---|
| W0.1 | **Dead hooks.** `block-secrets.sh`, `format-on-save.sh`, the `.expected/.error` warning read `$TOOL_INPUT_FILE_PATH`, which is never set. Read `tool_input.file_path` from stdin JSON like `enforce-dotnet-serialized.sh`. Make format-on-save single-file or drop the `.cs` branch. Extend the dotnet hook regex to `run|format|pack|msbuild|stryker` or route `/spy-run` through the prebuilt binary. | 2 | none (owner card) | a `.pem` write is denied; an `.expected` edit warns; `/spy-run` does not MSBuild outside the lock | synthetic stdin JSON per hook, mutation recorded | S |
| W0.2 | **CLAUDE.md repair.** Wrapper-form commands only; one gate script (`.claude/scripts/gate`: the five test projects in sequence with the console logger) referenced from CLAUDE.md, the wrapper header and the skills; Lowering stage in the pipeline line; a six-line "Inner loop" (build once; probe with the prebuilt `sharpyc`; one fixture by `DisplayName~`; the owning project; gate at phase close); a cloud-session note; the `global`/`nonlocal` axiom row marked superseded by R-KD. Move measurements into the scripts. Target ≤8 KB. | 2, 3 | P2 not chartered; P79 removes the axiom row later | a fresh session can run every command in CLAUDE.md as written; file ≤8 KB | `bash -n` on every fenced command; the hook does not block any of them | S |
| W0.3 | **Logs and orphans.** Unique log path per wrapper invocation printed to stdout; skills read the path from stdout, never a fixed name; a waiter kills an orphaned child whose holder is dead and whose log is silent past the stall window; `/gap-analysis` gets the console logger. | 2 | none | two concurrent `/run-tests` do not clobber; a killed holder's child is reaped | `build_tools/tests/test_dotnet_serialized_lock.py` gains both cases | S |
| W0.4 | **CI hygiene.** Nightly checks out `mainline` and prints the soaked SHA in the job summary; `timeout-minutes` and `--blame-hang-timeout 10m` on every test job; `concurrency` with `cancel-in-progress` for PRs; NuGet cache; split Compiler shard 5 (15–19 min against 3–6 for the others) and merge shards 1 and 6 to stay under the 20-job cap; run the spec-block, allowlist-state and allocation gates on mainline pushes too; `shard-partition` warns when max/median shard time exceeds 2. | 2 | CI items not chartered (R-IX covers the shard 5 cost cut only) | PR median wall clock ≤12 min; slowest shard ≤8 min; nightly cites the right SHA | the existing `shard-partition` and `CiFilterCoverageConformanceTests`; one dispatched nightly run | M |
| W0.5 | **Band half of #2443.** `SemanticInfo.Freeze()` after the four project-level writes, before `BuildLoweringIr`; `MergeFrom` asserts disjointness (throws with table name and node position); `SemanticInfoMergeConformanceTests` pins the node-keyed table count at 88 as a ratchet. | 1 | R-LK; charter row 0 | `.expected.cs` unchanged; a late write throws; a planted duplicate key throws | two mutations (remove `Freeze()`; remove the assertion), recorded | S–M |

Wave 0 gate: one whole-solution run on the owner's machine or CI, plus the scorecard.

### Wave 1 — live silent-wrong bugs and the cache key

Each changes printed or emitted behaviour, so each lands alone with its own gate (Rule 13), or rides a chartered gate where the charter names one.

| # | Item | Goal | Charter | Done-when | Guard | Size |
|---|---|---|---|---|---|---|
| W1.1 | **`dict` insertion order** after remove-then-insert; `popitem` O(1) and insertion-last; null-key ordinal follows the order array. Compact ordered layout (entries + index with tombstones) or `OrderedDictionary` on net10 with a netstandard2.1 fallback. | 1 | none (owner card); file as `lane:silent-wrong` | `d.pop("a"); d["d"]=4; list(d)` matches python3; `popitem` drains in O(n) total | CsCheck property: random insert/remove/reinsert/popitem sequences vs a reference ordered model; one fixture; allocation gate unchanged | M |
| W1.2 | **`round()`** per CPython `float.__round__`: negative `ndigits`, `ndigits > 15`, NaN → `ValueError`, ±inf → `OverflowError`, no unchecked `(int)` cast; `round(int, int)` overload. | 1 | none; R-IS precedent | the digits × special-values matrix matches python3 | differential matrix digits ∈ {−3…20} × {NaN, ±inf, ±0.5 ties, 1e10, 2.675} | S |
| W1.3 | **Incremental cache key** covers features, reference identities, package references, TFM, configuration; skip IL emission when no file and no option changed. | 1, 2 | the #2114 class (P28, row 28); owner card | the four-step `opt_const_fold` sequence gives cold = warm; warm no-change build < 0.5 s | `ColdWarmBuildDifferentialTests` gains a feature axis and a reference axis; mutation: drop one fact from the key | M |
| W1.4 | **CLR exceptions where Python raises `ValueError`.** One class issue with a cell table (datetime constructors, numpy's 44 CLR throws, `np.clip` asserting a throw numpy does not make, nine spy tests importing `system` exceptions). Boundary wrapper for `date`/`time`/`datetime` constructors; numpy `ArgumentException` → `ValueError`. | 1 | P35 owns only parse/overflow cells; the rest is not chartered | `except ValueError` catches `date(2023, 2, 29)` and a numpy broadcast error; no spy test asserts a CLR exception type | source scan over `Sharpy.Stdlib/**` banning public `throw new (Argument\|InvalidOperation\|NotSupported\|ArgumentOutOfRange)Exception` with a per-site allowlist citing the class issue; twin scan over spy tests | M |
| W1.5 | **Exception MRO table test** (P33's generator): every Core exception's base chain vs `python3 -c "print(X.__mro__)"`, then one fix. | 1 | **P33, light, charter row 8** — run at its position or ask the owner to pull it forward (§7) | the table test passes with zero allowlist rows | the table test itself; mutation: swap one base | M |

### Wave 2 — P82 first tranche (#2443)

Runs at its charter position (after P80) unless the owner rules otherwise (§7). The orchestrator may prepare it now: measure the 88 tables, classify each as type / symbol / diagnostic / lowering, and draft the migration order from `lowering-ir.md` §6. That preparation is a comment on #2443, not a plan file.

| # | Item | Done-when |
|---|---|---|
| W2.1 | Table census: the 88 tables classified, with for each lowering table the IR node that will carry it | posted on #2443 |
| W2.2 | First tranche: the three most-read lowering tables moved onto IR nodes, emitter reads them from the IR only, tables deleted, pin lowered | `.expected.cs` unchanged; emitter LOC down; pin < 88 |
| W2.3 | Carrier-guard narrowing: `SemanticType` and `Symbol` out of `CarrierHierarchyRoots`; the inspection sites become an allowlist | allowlist file exists with every site cited to #2443; count falls each tranche |

### Wave 3 — process reset (each needs the owner's ruling in §7 first)

| # | Item | Goal |
|---|---|---|
| W3.1 | Per-group budget as a charter fact: ≤2 gates, plan ≤15 KB, `/verify-plan --adequacy` dropped for `light` in favour of a ≤40-line checklist in `/implement-plan` | 3 |
| W3.2 | Allowlist policy: total rows in every recap; cap rows per cited issue; structured `cite=#N`; one ledger per generator; one band round draining or fragment-marking the 466 spec blocks | 3 |
| W3.3 | Plans and charters committed under `docs/process/`; skills re-pointed (owner card); a generated per-call index (group → issues → tier → position → current rulings) | 2 |
| W3.4 | Branch policy: full CI on `dev` pushes or drop `dev`; ≤100-char commit subjects enforced | 2 |
| W3.5 | Measure before adding tests: coverlet; dedupe the 29 duplicate fixture groups; count-pinning census tests become set membership | 3 |
| W3.6 | Pin one Python oracle minor in one file; netstandard2.1 smoke job | 1 |

---

## 5. Interim checks: is this improving things or adding cruft?

Two instruments. The **scorecard** is measured at the start and at the end of every wave. The **cruft test** is applied to every change before it is committed.

### 5.1 Scorecard

Baseline is 2026-11-06 @ ee9a007 as measured in the audit. The direction column is the only thing that matters; a wave that moves two metrics the wrong way triggers the stop rule.

| Metric | How to measure | Baseline | Direction | Wave-0 target |
|---|---|---|---|---|
| Open issues; closed ÷ filed this wave | `gh api` counts | 284; Oct 113 ÷ 211 | closed ≥ filed | ≥ 1.0 |
| Open SPY0908 / CS-leak issues | title match `SPY0908\|CS[0-9]{4}` | 76 | down | ≤ 76 |
| Allowlist rows, total | `grep -cvE '^\s*(#\|$)' **/*allowlist*.txt` summed | 2,047 | down, or flat with a named seed plan | ≤ 2,047 |
| Node-keyed `SemanticInfo` tables | the pin in `SemanticInfoMergeConformanceTests` | 88 | down | 88 pinned |
| Test LOC ÷ product LOC | `wc -l` over `*.Tests` vs `Sharpy.Compiler`+`Core`+`Stdlib` | 1.75 | not up | ≤ 1.75 |
| Procedure to read per `light` group | bytes of CLAUDE.md + the skills the tier invokes | ~130 KB | down | CLAUDE.md ≤ 8 KB |
| Whole-solution gates per item | count from the wave recap | 2–4 | ≤ 2 | ≤ 2 |
| Commit mix this wave | conventional prefix over the wave's commits | Oct: test 42%, feat 3% | `test` share ≤ product-change share | report |
| PR CI median wall / slowest shard | Actions API, last 20 push runs | 19 min / 15–19 min | down | ≤ 12 / ≤ 8 |
| Nightly: real finds vs self-inflicted reds | run log | 1 vs 3 in 12 runs | real finds up, self-inflicted 0 | soaks `mainline` |
| Warm no-change incremental build | `sharpyc project --incremental --metrics-format text` on the stdlib `.spyproj` | 2.0 s (1.5 s IL) | down | report |
| Hello-world `sharpyc run`, warm | `time` | 0.75 s | not up | ≤ 0.75 s |
| Fresh-session test | a new session with only the repo: build, probe, one fixture, read a tracker issue's vocabulary, all without a contradiction | fails today (hook blocks CLAUDE.md commands; plan IDs unresolvable) | passes | passes after W0.2 + merge of `docs/process` |
| Hooks that run | feed each hook synthetic stdin | 1 of 4 | 4 of 4 | 4 of 4 |

### 5.2 The cruft test (every change, before commit)

A change passes when every answer is yes, or the "no" is written in the commit body with the reason.

1. **Does it name the measured defect or footgun it removes**, with the audit section or issue number? A change that cannot name one is scope creep.
2. **Does it delete or shrink something?** Rule text, a table, a hand matrix, allowlist rows, a save/restore site, a fixed log path. A pure addition needs an explicit "net-zero" argument: what it adds is smaller than what it will prevent, stated in the same units (KB of procedure, rows, minutes of gate).
3. **Is every new guard falsifiable** (Rule 12), and does it assert on its own SKIP path? A guard that can pass vacuously is not a guard.
4. **Does it add a document, skill, label, tier, letter, or vocabulary?** If yes, it fails unless §7 has a ruling for it. Put the content in an existing file.
5. **Does it need an owner ruling** (a charter reorder, a one-way door, a change to what the checker refuses)? If yes, it waits in §7; do not implement under an assumption.
6. **Could a fresh session understand it from the repo alone?** Issue number in the code comment, record on the issue, charter updated if a row changed.

### 5.3 Stop rules

Stop the wave, write the recap, and hand the decision to the owner when any of these holds:

- The scorecard moves the wrong way on two metrics in one wave, or on allowlist rows or procedure size in any wave without a named seed plan.
- An item has consumed two whole-solution gates and is not done.
- A worker proposes a new node-keyed `SemanticInfo` table, a new hand matrix, or a new allowlist file.
- A guard's mutation test does not go red.
- The same class produces a third cell after its class fix landed (the fix was not structural).
- A plan passes 15 KB, or a `/verify-plan` pass is requested a third time on the same plan.

### 5.4 Checkpoints

- **Start of wave:** reconcile in-flight work (§2); baseline scorecard; claim items by posting on their issues.
- **Per item:** done-when met; guard mutation recorded; cruft test in the commit body; owning test project green locally; findings ledger appended.
- **End of wave:** one shared gate where the wave allows it; scorecard; recap (§6) posted as a comment on #2443 for Waves 0 and 2 and on the class issues for Wave 1; decisions needed (§7) refreshed; the committed charter's §1 row 0 and §7 history updated and committed.
- **Retrospective, three questions, in the recap:** What did this wave delete? What did it add that a future session has to read? Which stop rule came closest to firing?

---

## 6. Recap format (one per wave, ≤ 25 lines)

```
Wave N recap — <date> @ <sha>
Landed: <item ids> (<commits>); gates run: <n>; gate result: passed/failed/skipped @ sha
Not landed: <item ids> and why
Scorecard: <metric: baseline → now> for every row that moved; unchanged rows listed by name
Deleted: <what got smaller>
Added that others must read: <files, bytes>
Findings filed: <issue numbers, class trackers appended>
Decisions needed: <§7 ids>
Closest stop rule: <which, how close>
Next wave starts with: <item ids>
```

---

## 7. Decisions needed from the owner

Open doors the orchestrator must not walk through on its own. Each has a default the orchestrator assumes while waiting, and the default is always the one that changes less.

| # | Decision | Default while waiting | Door |
|---|---|---|---|
| D1 | Pull the Core silent-wrong groups P33, P34, P35 ahead of the codemod groups P69, P70 and P37 in the execution order? (They sit at positions 15–17.) | run them at their charter position; W1.5 waits | two-way |
| D2 | Run P82 (#2443) now on a parallel branch against the current carriers, or wait for P80 as placed? | wait; prepare W2.1 only | one-way (rebase cost rises with each table added) |
| D3 | Per-group budget as a charter fact: ≤2 gates, plan ≤15 KB, drop `/verify-plan --adequacy` for `light` | keep today's procedure; report gates and plan bytes per item | two-way |
| D4 | Allowlist policy: cap rows per cited issue (proposed 50), structured `cite=#N`, one ledger per generator | no cap; report total rows | two-way |
| D5 | Branch policy: full CI on `dev` pushes, or drop `dev` and protect `mainline` | unchanged | two-way |
| D6 | Python oracle: pin 3.12 (CI) or 3.13 (owner machine, #2350's measurements) in one file | 3.12 | two-way |
| D7 | `dict.get` returns `Optional<V>` (today) or `V?` (spec `collection_types.md:41`) | unchanged; fix the spec text to match or mark pending | one-way for callers |
| D8 | Should the audit's remaining untracked items (W1.1, W1.2, W1.4 class issue, L7's `isalnum`, dead `src/test_discovery.cs`, hand-written plans under `docs/stdlib/`) be filed now or at the next batching call? | file the class issue for W1.4 when its guard lands; the rest at the next call | two-way |
| D9 | Historical plans and the 2026-08-30 charter: commit under `docs/process/` or leave machine-local | leave; only the current charter is committed | two-way |

---

## 8. Known hazards, in the audit's words

- `#2438`: vstest can OOM on the result send, so a passing run prints "Test Run Failed" with 0 failed rows. Count the Failed rows; exit 1 alone is not a failure.
- The watchdog and the console logger are coupled (#2257). Omit the logger and a green gate looks like a stall.
- The `-latest.log` symlink can point at a peer's run. Read the path the wrapper printed.
- Generated C# (`regenerate_spy_stdlib.sh` MODULES, 18 modules) is pinned byte-for-byte and must be whitespace-clean; never hand-edit it; an emitter layout change ripples into it and into 148 `.expected.cs` snapshots.
- A row's reason text in an allowlist counts every `#N` as a cite; closing that issue reddens the row.
- `.features` fixtures come in gated/ungated pairs; do not dedupe them.
- The incremental cache ignores feature flags until W1.3 lands: use `--clean` when toggling a flag.

---

## 9. Kickoff prompt for the orchestrator session

Paste this to start:

```
Read docs/process/handoff-2026-11-06-remediation.md in full, then docs/audits/health-check-2026-11-06.md §1, §9 and §10, then issue #2443 and the charter rows for P82 and row 0 in docs/process/rounds/remediation-round-2026-09-20-batching.md §1.

Then: (1) run §2 of the handoff and report which items are already in flight; (2) take the Wave-0 baseline scorecard (§5.1) and post it as a comment on #2443; (3) claim the Wave-0 items that are not in flight, one Opus worker per item, each prompt carrying the shared-tree prohibition and the dotnet wrapper rule verbatim; (4) apply the cruft test (§5.2) to every commit; (5) stop on any §5.3 rule and write the recap (§6) instead of continuing.

Use only Opus subagents. Do not run a whole-solution test in a container under 16 GB. Do not re-ask any ruling in the charter §4. Put every decision you cannot make into §7's format and wait.
```

---

## 10. References

- Audit: `docs/audits/health-check-2026-11-06.md` (§2 architecture, §3 runtime, §5 process, §9 charter cross-reference, §10 program). Artifact: `https://claude.ai/artifact/NBauE8wv83sCoatKnUzf9o`.
- Charter: `docs/process/rounds/remediation-round-2026-09-20-batching.md` (§1 row 0 and 6c⁶⁷″, §3.20o, §4 R-LK). Policy: `docs/process/README.md`.
- Trackers: #2443 (P82), #2114 (cache key class), #2124 / P33 (exception tree), #2438 (OOM false red), #2365 (R-KK pin), #1952 and #2423 (omnibus allowlist cites).
- Design: `docs/design/lowering-ir.md` (E1/E2), `docs/design/spy0908-policy.md`, `docs/design/verification-contract.md` §11–§12 (tiers), `docs/design/python-fidelity-scope.md`.
- Scripts: `.claude/scripts/dotnet-serialized`, `.claude/hooks/*`, `build_tools/check_allowlist_issue_state.sh`, `build_tools/check_spec_blocks.sh`.
