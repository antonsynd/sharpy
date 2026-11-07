# Remediation Handoff — from the 2026-11-06 health check to the orchestrator

> **For:** a Fable orchestrator session that coordinates Opus subagents (each may spawn its own for verification).
> **From:** the health-check session (`docs/audits/health-check-2026-11-06.md`), which also filed #2443 and placed it in the charter as P82.
> **Revision 2, 2026-11-07 @ 1fa2340f7**, after a read-only smoke run of revision 1 by three Opus agents (§11). Revision 1 was written at ee9a007a1; five of its items landed the same day and PR #2455 changed the gate, so its §2, its gate text and 11 of its 14 scorecard rows were stale or unmeasurable. This revision records what landed, builds on #2455, and makes every metric one command.
> **Revision 3, 2026-11-07 @ 18a11bbb7:** #2453 merged, `dev` and `mainline` synced, D9 ruled, P77b's close-out (T12) assigned here (§4), the `gh api` comment form corrected (§3), rules that lived only in the owner's machine-local memory moved to §8, kickoff updated (§9). Everything else is revision 2's text, so its "Now" column is @ 1fa2340f7; §5.1 has the newer reading.
> **Authority:** owner rulings in the charter §4 and on #2443 are binding and are not re-asked. For Wave 0 the only ruling that matters is R-LK; find it with `grep -n '^| R-LK' docs/process/rounds/remediation-round-2026-09-20-batching.md`. Everything that needs a new ruling is in §7 and waits.
> **This is not a plan and not a charter.** It says what to do first, how to tell whether it is working, and when to stop.

---

## 1. Intent

Three goals, in priority order. Every item in §4 names which goal it serves; an item that serves none is cruft and is dropped.

1. **Stable and trustable.** Programs that compile run the way Python would (silent-wrong is the worst class), programs that should compile do, and the compiler never leaks a C# error. The structural lever is #2443 (the lowering IR becomes the emitter's only input), because the SPY0908 class (77 open, ~340 filed in six months) regenerates from 88 `SemanticInfo` dictionaries. The immediate levers are the live bugs the audit reproduced that no harness owns.
2. **Efficient sessions.** A fresh session, cloud or local, can build, probe a program in about two seconds, run the owning test project, and read the tracker's vocabulary, without hitting a contradiction between CLAUDE.md and a hook or spending the first hour on procedure.
3. **Less process per unit of fixed behaviour, not more.** The audit measured October at 42% `test` commits and 3% `feat`, closures down from 483 to 113 a month, allowlist rows up 3.4× in three weeks, and about 130 KB of procedure to read per group (139 KB at this revision). The remediation must bend those lines. A wave that fixes bugs while making the process heavier has failed on goal 3 even if it passed on goal 1.

### Non-goals

- No new hand-written matrix tests (Rule 13; contract §11 defines a hand matrix as ≥2 axes × ≥3 values written by hand instead of generated).
- No new node-keyed `SemanticInfo` tables (R-LK). A new lowering fact is an IR node property, or it waits for P82.
- No new process document, skill, label, tier letter or ruling vocabulary beyond the per-wave recap in §6. If something must be written down, it goes in an existing file. (The `/ci-gate` skill from #2455 and the `scorecard` script from this revision are recorded in §7 as D10 and D11 with their net-zero arguments; they are not precedents.)
- No re-ordering of the charter's execution order without an owner ruling. The audit's items are a parallel band of zero-seam and Core work, not a new queue.
- No whole-solution test run in a container under 16 GB. The gate is `/ci-gate` (CLAUDE.md "Commit gate", contract §6); the local wrapper command is the fallback when it exits 3.

---

## 2. State at this revision, and how to re-check it

Everything revision 1 listed as an owner task card has landed on `dev` and `mainline` (the two branches were synced at 9ab9f3d5a and again at 1fa2340f7):

| Item | Landed as | Note |
|---|---|---|
| W0.1 dead hooks | 7c9567288 (#2442) | 4 of 4 hooks run; `test-hooks.sh` 61/0/0; dotnet regex now also blocks `run`, `format`, `pack`, `publish`, `msbuild`, `clean`, `stryker` |
| W0.5 band half of #2443 | 70608d421, 235400bb4 (#2448) | `Freeze()` before `BuildLoweringIr`; pin ≤ 88; **`MergeFrom` tolerates equal values** and throws only on a conflicting value (D12) |
| W1.1 dict insertion order | 7fc67e55f (#2447) | #2440 still open; allocation baseline was already red at base |
| W1.3 cache key | 764737b1d (#2441), ac2437b5f (#2451) | key covers features, references, TFM, configuration; IL-skip on no-change deferred; in-process rebuilt-reference cell open on #2114 |
| W3.3 plan workflow → `docs/process` | 84c3c8922 (#2445) | skills re-pointed; `docs/process/plans/` appears with #2453 |
| Step zero (merge the audit branch) | c9c076112, 6f13a627d | done |
| CI as the default gate | #2455 (1fa2340f7) | `/ci-gate`; every PR runs the full gate whatever its base; concurrency groups; `workflow_dispatch` |

Nothing was in flight at revision 3: no PR was open and W0.2–W0.4 were unclaimed. **#2453** (P77b) merged as 87ac3853e, and `dev` and `mainline` were synced at 18a11bbb7. The name-resolution allowlist is 2,595 rows by design. `plan-3eba57` and 62 older plans are under `docs/process/plans/` (D9). The #2438 OOM fix came back with #2453, and #2438 is **open** until T12 closes it (§4).

Re-check before claiming anything:

```bash
git fetch origin --prune
git log --oneline --no-merges origin/dev --since=2026-11-06 | head -40
git branch -r --no-merged origin/dev
gh api -X GET "repos/antonsynd/sharpy/pulls?state=all&sort=updated&direction=desc&per_page=10" --jq '.[] | [.number, .state, .head.ref, .title] | @tsv'
gh api -X GET "repos/antonsynd/sharpy/issues/2443/comments" --jq '.[] | .created_at + " " + (.body | split("\n")[0])'
```

If an item is in flight, the orchestrator's job for it is review and gate, not a second implementation. Post on its PR or issue; do not open a parallel branch.

---

## 3. Operating rules for the orchestrator

These are the project's existing rules, restated because each has been violated by a session before and the audit found the consequences.

- **Models.** The lead is Fable; workers are Opus (`model: "opus"`). A worker may spawn its own Opus sub-workers for refutation or measurement. Never Sonnet or Haiku for compiler work.
- **Worker preamble.** Paste this block into every worker prompt, unchanged:

  > The working tree is SHARED with other agents. Never run `git checkout` / `git restore` / `git clean` / `git stash` / `git reset` / `rm` on repo paths; the gitignored `.claude/tmp/` scratch files the skills clear are the only exception. Build once with `.claude/scripts/dotnet-serialized build sharpy.sln` before probing, then probe with `src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` (a binary older than HEAD reproduces fixed bugs). Run dotnet only through `.claude/scripts/dotnet-serialized`; any local test run carries `--logger "console;verbosity=normal"`. Never run a whole-solution test: the lead runs `/ci-gate`. Append findings to the ledger path the lead gives you; never file issues. Put the cruft trailer (§5.2) in every commit body.

  Commit lead work in small slices and re-check `git diff --stat` after each worker wave.
- **The gate.** `/ci-gate` on the pushed branch tip (CLAUDE.md "Commit gate"; contract §6). The local fallback is `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`, only when `/ci-gate` exits 3 or for what CI does not cover. **One gate** = one `passed/failed/skipped @ sha` line cited in a commit body or recap, by CI run id or local log. Automatic PR runs are evidence but are not counted as gates spent.
- **Cloud container.** If `dotnet --version` fails: `dotnet-install.sh --channel 10.0 --install-dir /root/.dotnet` and `export DOTNET_ROOT=/root/.dotnet`. `gh issue`/`gh pr` return 403; read with `gh api -X GET …` and write with `gh api -X POST repos/antonsynd/sharpy/issues/N/comments -F body=@file.md` (capital `-F`: `-f` posts the literal text `@file.md`). `gh api --paginate` is refused by the proxy; page with `&page=N`. **Not verified from a container:** `/ci-gate` calls `gh workflow run` and `gh run view`. If they are refused it exits 3, and the local fallback is forbidden under 16 GB, so cite the PR's automatic run instead (§9).
- **Rule 1 and Rule 12.** Never change an expected value to pass. Every new guard is mutation-tested (break → red, restore → green) and the record goes in the commit body. A guard with a SKIP path asserts that the SKIP path is not what ran.
- **Python first.** `python3 -c "..."` before implementing any Python semantics (Rule 6). Oracle is 3.12 in CI, 3.13 locally; say which you used (D6).
- **Class, not cell.** A bug found while working is a comment on its class tracker or one class issue with a cell table, labeled `lane:*` + `reach:*`. Workers append to a findings ledger in the lead's scratchpad; the lead files once per wave, and the ledger is flushed to issues at wave end so nothing another session needs stays in a scratchpad.
- **Charter edits.** The charter is edited at batching calls or on an owner ruling (`docs/process/README.md`). A row that this work makes stale (row 0 still says the #2443 band half is OPEN; line 9 lists landed cards as unfiled; line 16 has the local gate command) is posted as a proposed diff in the wave recap for the next `/batch-issues`, not edited by the orchestrator.
- **Budget per item, reported not enforced until D3.** Report gates spent and plan bytes for every item. The two-gate and 15 KB figures in §5.3 are triggers for escalating to the owner, not rules to plan against. Procedure reading is CLAUDE.md plus the item's own issue; not the full skill chain unless the item is `full` tier in the charter. Items W0.2–W0.4 have no tier because they are not chartered; treat them as `band` (contract §12: one seam-free chore, no verify stage, mutation of its own guard only).

---

## 4. Work program

Items are grouped in waves. A wave ends with the scorecard in §5. Each item lists the goal it serves, its charter status, its done-when, its guard, and its size. Items marked LANDED are reviewed, not re-done.

### Wave 0 — zero-seam tooling

No emitted C# changes in this wave. W0.2–W0.4 share one `/ci-gate` run at the end. **Order: W0.3 before W0.2**, or one worker owns both, because both rewrite the log-path and gate lines in CLAUDE.md, contract §6 and five skills. Claim W0.2–W0.4 on one class issue the lead files first (title: "tooling band from the 2026-11-06 audit: CLAUDE.md, wrapper logs, CI hygiene", cell table = the three items, labels `lane:tooling` plus the nearest existing `reach:*`; create no label); the scorecard and the Wave-0 recap go there too, and only the pin row goes on #2443.

| # | Item | Goal | Status | Done-when | Guard | Size |
|---|---|---|---|---|---|---|
| W0.1 | **Dead hooks.** | 2 | LANDED 7c9567288 | review only: `bash .claude/hooks/test-hooks.sh` is 61/0/0 @ HEAD; no CI job runs it yet, which W0.4 adds | — | — |
| W0.2 | **CLAUDE.md repair, on top of #2455.** First: the `## Commands` block lists raw `dotnet build`/`test`/`run`, every line of which the hook now blocks (8 of 8 at this revision); rewrite in wrapper and skill form. Then: Lowering stage in the pipeline line; a six-line "Inner loop" (build once; probe with the built `sharpyc`; one fixture by `DisplayName~`; the owning project; `/ci-gate` at phase close); a cloud-session note (the four lines in §3); the `global`/`nonlocal` axiom row marked superseded by R-KD. Measurements and their shas move to the file that owns the mechanism: gate timings to contract §6, wrapper and lock facts to the `dotnet-serialized` header, blast-radius list to contract §6. Target ≤ 8 KB from 19.4 KB; **no new gate script** (`/ci-gate` is the gate). | 2, 3 | OPEN | every fenced command in CLAUDE.md passes the hook (`scorecard` row "Fresh-session test" reads 0 of N); file ≤ 8 KB; `bash -n` on every fenced command | the scorecard row is the guard; mutation: put one raw `dotnet test` back → the row reads 1 | S |
| W0.3 | **Logs and orphans.** Unique log path per wrapper invocation, printed on stdout and also written to a fixed pointer file the backgrounding skills can read (`.claude/tmp/dotnet-serialized-latest.path`); skills read the path, never a slot name; a waiter kills an orphaned child whose holder PID is dead and whose log is silent past `STALL_SECONDS`; `/gap-analysis` gets the console logger. Retention: the wrapper deletes logs older than 7 days under `.claude/tmp/` only. | 2 | OPEN | two concurrent `/run-tests` do not clobber; a killed holder's child is reaped | `build_tools/tests/test_dotnet_serialized_lock.py` gains both cases, each mutation-recorded | S |
| W0.4 | **CI hygiene.** Nightly checks out `mainline` and prints the soaked SHA in the job summary; `timeout-minutes` and `--blame-hang-timeout 10m` on every test job; NuGet cache; split Compiler shard 5 (16 min median against 3–6 for the others) and merge shards 1 and 6 to stay under the 20-job cap; the spec-block, allowlist-state and allocation gates run on `mainline` pushes too; `shard-partition` warns when max/median shard time exceeds 2; a job runs `.claude/hooks/test-hooks.sh`. (Concurrency groups landed with #2455; strike that bullet.) R-IX owns the shard-5 cost cut as a charter item; the split here is the mechanical half and is recorded on R-IX's issue. | 2 | PARTIAL | the nightly job summary cites a `mainline` SHA; every test job has both timeouts; `test-hooks.sh` runs in CI. The wall-clock targets (median ≤ 12 min, slowest shard ≤ 8 min) are **measured over the next 20 push runs and reported in the Wave-1 recap**, not checked inside this wave | `shard-partition`, `CiFilterCoverageConformanceTests`; one dispatched nightly run if the lead has dispatch rights, otherwise the next scheduled run | M |
| W0.5 | **Band half of #2443.** | 1 | LANDED 70608d421 | review only: `SemanticInfoFreezeTests` and `SemanticInfoMergeConformanceTests` green @ HEAD; the equal-value tolerance is D12, not a defect | — | — |

### P77b close-out (T12): run it once, right after the baseline scorecard

This is not a Wave 0 item. It is the last unchecked line of `docs/process/plans/plan-3eba57.md`, unblocked by #2453's merge; the owner assigned it here on 2026-11-07. It changes no code. Report its filed and closed counts apart from Wave 0's in the recap. The plan's "Issues to Close", Acceptance 10 and Implementation Verification Record are the terms; what follows is what they do not say.

1. **Check first.** No row may cite the issues being closed: `git grep -E '#(2433|2438)([^0-9]|$)' -- src build_tools .github`, with `#2423` as the positive control. `\b` in `git grep -E` matched nothing on the owner's Mac, so that spelling is not a valid check, and this check has not been run at revision 3. Confirm `MuteCompilePath` is on `origin/dev`. If either check fails, stop and report.
2. **Drain-set comments.** Go by what `src/Sharpy.Compiler.Tests/Conformance/name-resolution-differential-allowlist.txt` cites, not the plan's list. Rows per cite @ feaadc8d0: #2423 1154, #2425 359, #1925 291, #2397 207, #2422 176, #2424 139, #2147 128, #2151 33, #2432 29, #2435 20, #2434 12, #2111 10, #2412 9, #2249 9, #2417 6, #2398 6, #2420 3, #1974 3, #2439 1. The plan names #2150, which now has no row, and omits #2111 and #2439. A GitHub comment is capped at 65,536 characters, so #2423's list probably needs more than one. Comments from the reverted attempt already sit on some of these trackers, so say that this one supersedes them.
3. **Two more comments the plan owes:** the D7 rows on #2426, and the two unruled shapes `global-creates-binding` and `same-block-redeclaration-after-closure` on #2427.
4. **D9 on #2425 and #2427.** Post the plan's D9 (owner's option A, rule `S2-walrus`) on both, and say that the 2026-11-07 ruling on #2427 narrows it for a binding of the same function. A filtered search found no D9 comment dated 2026-11-06 on either issue.
5. **One new class issue** (owner, 2026-11-07), labels `lane:tooling`, `reach:probe`, `class-tracker`: a cell table on top, then the residue moved from the comments of 2026-11-07T07:13 on #2433 and #2438 (guard limits, nine hand arrays, refuted Outs, axes with no cell, walrus at an initializer, not-reached list, the 243 rows, the ceiling's reach, message sites with no guard). Copy the text rather than rewriting it, and link it both ways. Walrus at an initializer is lane `ice`; say so in its cell. The next `/batch-issues` places the issue.
6. **Close #2433** with Acceptance 1–9's evidence, the report path and counts (83727/0/9 @ c976ba05b, run 42350273916; pool 8889, allowlist 2595). The report path was not looked up at this revision. **Close #2438** on Acceptance 8 (largest result 918,768 @ c976ba05b).
7. **Finish:** run `bash build_tools/check_allowlist_issue_state.sh`, tick T12 in the plan, and commit that with `[skip ci]`.

**Do not read** #2444, #2446 or branch `remote/p77b-implement-plan`: the owner compares the two attempts independently. When reading the trackers, filter comments with `select((.body|test("#2444|#2446|remote/p77b"))|not)`. **Do not rule on** what the owner left open: a walrus over a `let`, whether `nonlocal` gets the `global` refusal, and #2397's three new cells. Those wait for `/batch-issues`, with the five rulings already on the issues.

### Wave 1 — live silent-wrong bugs

These are not charter groups, so Rule 13's "lands alone" does not bind them; each lands on its own PR, which runs the full gate automatically, and that run is cited as the item's gate. Audit §10 and the charter line 9 call them Core band chores.

| # | Item | Goal | Status | Done-when | Guard | Size |
|---|---|---|---|---|---|---|
| W1.1 | **`dict` insertion order.** | 1 | LANDED 7fc67e55f | close #2440 citing the PR's CI run; file the allocation-baseline drift (red at base, +10–16%) as its own chore | — | — |
| W1.2 | **`round()`** per CPython `float.__round__`: negative `ndigits`, `ndigits > 15`, NaN → `ValueError`, ±inf → `OverflowError`, no unchecked `(int)` cast; `round(int, int)` overload. File it first (D8 default allows it: it is a class issue with a cell table). | 1 | OPEN; R-IS precedent | the digits × special-values matrix matches python3 | differential matrix digits ∈ {−3…20} × {NaN, ±inf, ±0.5 ties, 1e10, 2.675}, generated, not hand-written | S |
| W1.3 | **Incremental cache: the remaining half.** Skip IL emission when no file and no option changed (today 1.8 s of a 2.4 s warm no-change build); the in-process rebuilt-reference cell on #2114. | 1, 2 | PARTIAL 764737b1d | warm no-change build < 0.5 s; the #2114 cell closes | `ColdWarmBuildDifferentialTests`; mutation: force IL emission → the timing assertion reds | M |
| W1.4 | **CLR exceptions where Python raises `ValueError`.** One class issue with a cell table (datetime constructors, numpy's 44 CLR throws, `np.clip` asserting a throw numpy does not make, nine spy tests importing `system` exceptions). Boundary wrapper for `date`/`time`/`datetime` constructors; numpy `ArgumentException` → `ValueError`. | 1 | OPEN; P35 owns only parse/overflow cells | `except ValueError` catches `date(2023, 2, 29)` and a numpy broadcast error; no spy test asserts a CLR exception type | source scan over `Sharpy.Stdlib/**` banning public `throw new (Argument\|InvalidOperation\|NotSupported\|ArgumentOutOfRange)Exception` with a per-site allowlist citing the class issue (this allowlist is named here, so §5.3 does not fire on it); twin scan over spy tests | M |
| W1.5 | **Exception MRO table test** (P33's generator): every Core exception's base chain vs `python3 -c "print(X.__mro__)"`, then one fix. | 1 | OPEN; **P33, light, charter row 8** | the table test passes with zero allowlist rows | the table test; mutation: swap one base | M; waits on D1 |

### Wave 2 — P82 first tranche (#2443)

Runs at its charter position (after P80) unless D2 rules otherwise. Preparation may start now and is posted as a comment on #2443, not a plan file.

| # | Item | Done-when |
|---|---|---|
| W2.1 | Table census: the 88 dictionaries (85 node-keyed, 3 symbol-keyed) classified as type / symbol / diagnostic / lowering, with for each lowering table the IR node that will carry it (`docs/design/lowering-ir.md` §6) | posted on #2443 |
| W2.2 | First tranche: the three most-read lowering tables moved onto IR nodes, emitter reads them from the IR only, tables deleted, pin lowered | `.expected.cs` unchanged; emitter LOC down; pin < 88 |
| W2.3 | Carrier-guard narrowing: `SemanticType` and `Symbol` out of `CarrierHierarchyRoots`; the inspection sites become an allowlist (named here; §5.3 does not fire on it) | allowlist exists with every site cited to #2443; count falls each tranche |

### Wave 3 — process reset (each needs a ruling in §7 first)

| # | Item | Goal | Status |
|---|---|---|---|
| W3.1 | Per-group budget as a charter fact, in CI-run units: ≤ 2 cited gates, plan ≤ 15 KB, `/verify-plan --adequacy` dropped for `light` in favour of a ≤ 40-line checklist in `/implement-plan` | 3 | needs D3 |
| W3.2 | Allowlist policy: the three allowlist rows of the scorecard in every recap; cap rows per cited issue; structured `cite=#N`; one ledger per generator; one band round draining or fragment-marking the 466 spec blocks | 3 | needs D4 |
| W3.3 | Plans and charters under `docs/process/`; a generated per-call index (group → issues → tier → position → current rulings) | 2 | PARTIAL: policy, charter and skills landed; index open |
| W3.4 | Branch policy: `dev` pushes run only `dev-staleness.yml` while PRs into `dev` run the full gate; ≤ 72-char commit subjects (the `/commit` skill's figure) enforced | 2 | needs D5 |
| W3.5 | Measure before adding tests: coverlet; dedupe the 29 duplicate fixture groups; count-pinning census tests become set membership | 3 | OPEN |
| W3.6 | Pin one Python oracle minor in one file (today `3.12` is repeated across six workflows); netstandard2.1 smoke job | 1 | needs D6 |

---

## 5. Interim checks: is this improving things or adding cruft?

Two instruments. The **scorecard** is measured at the start and the end of every wave. The **cruft test** is applied to every change before it is committed.

### 5.1 Scorecard

One command, run from the repo root with `gh` and the network (`dangerouslyDisableSandbox: true` in Claude Code), after a build so the probe rows use a current binary:

```bash
.claude/scripts/scorecard --since <wave start date> --base <wave start sha>
```

It prints every row below with the HEAD sha and how it was measured. Baseline is the audit @ ee9a007a1; "now" is this revision @ 1fa2340f7, which already includes the five landed cards. The direction column is what matters; two metrics moving the wrong way in one wave is a stop rule.

**Reading at revision 3, @ feaadc8d0, with P77b in:** 287 open; 77 SPY0908/CS; allowlist 3,234 total, 639 outside seed-plan harnesses; 41 distinct cites; pin 88 / 88; test ÷ compiler LOC **1.78**; procedure 144,617 and CLAUDE.md 19,360; 63 committed plans; 8 of 8 CLAUDE.md commands blocked. Judge Wave 0 against its own start reading, because the rise from 1.75 to 1.78 predates it. `--base` is `origin/dev` at session start: 1fa2340f7 would count all of P77b as the wave's commits.

| Metric | Baseline @ ee9a007 | Now @ 1fa2340f7 | Direction | Notes |
|---|---|---|---|---|
| Open issues; closed ÷ filed in the wave | 284; Oct 113 ÷ 211 | 286; 2 ÷ 5 since 11-06 | closed ≥ filed | class fixes close several at once; a wave that only files has failed |
| Open SPY0908 / CS-leak issues (title) | 76 | 77 | down | +1 is #2452 |
| Allowlist rows **outside seed-plan harnesses** | 639 | 639 | down | the total (2,047 → 3,234 when #2453 lands) is reported, not targeted: P77b's plan seeds its rows on purpose |
| Distinct issues cited by allowlist rows | 36 | 36 (41 with #2453) | down when a class is fixed; up only for a new pinned class | rows citing a closed issue: 0, enforced by `check_allowlist_issue_state.sh` |
| `SemanticInfo` dictionary fields (pin / counted) | 88 / 88 | 88 / 88 | down | pin lands in W0.5; it falls in W2.2 |
| Test LOC ÷ compiler LOC | 1.75 | 1.75 | not up | `Sharpy.Compiler.Tests` ÷ `Sharpy.Compiler`, `.cs`, the audit's definition |
| Procedure bytes per `light` group; CLAUDE.md | 133,196; 18,944 | 144,617; 19,360 | down; CLAUDE.md ≤ 8 KB after W0.2 | CLAUDE.md + seven skills; it has grown 11 KB since the audit, 4.4 KB of that the `/ci-gate` skill |
| Commit mix in the wave | Oct: test 42%, feat 3% | since 11-06: test 30, chore 12, ci 7, docs 6, fix 5, feat 1 of 66 (44 are P77b's reverted range) | `test` share ≤ product-change share | report; exclude reverted ranges by hand |
| Gate citations per item | 2–4 (inferred) | 1 citation in 66 commits | ≤ 2 per item | counted from commit bodies; the recap maps citations to items |
| CI median wall / slowest job, last 20 `mainline` push runs | 19 / 15–19 min | 20 / 16 min (Compiler shard 5) | down | W0.4's targets ≤ 12 / ≤ 8 are read in the Wave-1 recap |
| Nightly, last 12: green / red, and which branch it soaks | 9 / 3, `dev` | 9 / 3, `dev` | self-inflicted reds 0; soaks `mainline` | classify each red from its log: a named failing test is a real find; a hang abort or "Total tests: Unknown" is self-inflicted. Today: 1 real (11-01, CS0023), 2 self-inflicted (10-27, 10-28) |
| Warm no-change build, `stdlib.spyproj` | 2.0 s (1.5 s IL) | 2.4 s (1.8 s IL) | down; < 0.5 s after W1.3 | on a scratch copy: an in-place build writes `bin/` into `src/Sharpy.Stdlib/spy/` |
| Hello world `sharpyc run`, warm, exit 0 | 0.75 s (**wrong**: that was the compile-error path) | 1.8–2.1 s | not up | `def main(): print("hello, world")`, three runs |
| Fresh-session test: CLAUDE.md commands the hook blocks; committed plans | 8 of 8; 0 | 8 of 8; 0 | 0 of N; ≥ 1 | the full test (a new session builds, probes, runs one fixture, reads a tracker's vocabulary) is run by the owner once per wave; this row is the half a script can check |
| Hooks that run | 1 of 4 | 61 passed, 0 failed, 0 skipped | all pass | `test-hooks.sh` |

### 5.2 The cruft test (every change, before commit)

A change passes when every answer is yes, or the "no" is written in the commit body with the reason. The record is one trailer line in the commit body:

```
Cruft: 1 #NNNN or audit §A1 · 2 −N lines/rows/bytes, or guard-for-silent-behaviour · 3 M1–Mk red/green · 4 none · 5 R-LK · 6 issue+recap
```

1. **Does it name the measured defect or footgun it removes?** An issue number or an audit section (`audit §A1`, `§P3`) both count; unchartered items cite the audit until D8 files them.
2. **Does it delete or shrink something?** Rule text, a table, a hand matrix, allowlist rows, a save/restore site, a fixed log path. A pure addition needs a net-zero argument in the same units (KB of procedure, rows, minutes of gate). A guard that replaces a silent behaviour with a thrown or failed one is exempt when its mutation record is in the body: the thing it shrinks is the class of silent-wrong programs, which has no line count.
3. **Is every new guard falsifiable** (Rule 12), and does it assert on its own SKIP path? N/A when it has none; say so.
4. **Does it add a document, skill, label, tier, letter, or process vocabulary?** If yes, it fails unless §7 has a ruling for it. A code type, test class or script is not vocabulary; a new way of naming work (a tier, a lane, a letter) is.
5. **Does it need an owner ruling** (a charter reorder, a one-way door, a change to what the checker refuses)? If yes, it waits in §7; do not implement under an assumption.
6. **Could a fresh session understand it from the repo alone?** Issue or audit cite in the code comment, record on the issue or in the recap, charter diff proposed if a row changed.

### 5.3 Stop rules

Stop the wave, write the recap, and hand the decision to the owner when any of these holds:

- The scorecard moves the wrong way on two metrics in one wave, or allowlist rows outside seed-plan harnesses or procedure bytes rise in any wave. A seed-plan harness is one listed in `SEED_PLAN_FILES` in `.claude/scripts/scorecard`; P77b is the only one today. The older plans committed under D9 do not extend that list, whatever they say about rows.
- An item has two gate citations and is not done.
- A worker proposes a new `SemanticInfo` dictionary (the pin reds), a hand matrix (contract §11: ≥2 axes × ≥3 values written out by hand), or an allowlist file not named in §4.
- A guard's mutation test does not go red.
- The same class produces a third cell after its class fix landed. "Landed" means the fix commit is on `dev` and cited on the class issue; a cell is a new issue or comment on that class after that commit's date.
- A plan passes 15 KB, or `/verify-plan` reaches CONVERGED on a NEEDS REVISION (contract §12), which means the plan did not converge.

### 5.4 Checkpoints

- **Start of wave:** re-check §2; run the scorecard; file the tooling class issue (Wave 0) or claim on the item's issue; post the scorecard there.
- **Per item:** done-when met; guard mutation recorded; cruft trailer in the commit body; owning test project green locally; findings ledger appended.
- **End of wave:** one shared `/ci-gate` where the wave allows it; scorecard; recap (§6) posted on the tooling issue for Wave 0, on the class issues for Wave 1, on #2443 for Wave 2; §7 refreshed; the ledger flushed to issues; the proposed charter diff attached for the next `/batch-issues`.
- **Retrospective, three questions, in the recap:** What did this wave delete? What did it add that a future session has to read? Which stop rule came closest to firing?

---

## 6. Recap format (one per wave, ≤ 25 lines)

```
Wave N recap — <date> @ <sha>
Landed: <item ids> (<commits>); gates cited: <n>; gate result: passed/failed/skipped @ sha (run id or log)
Not landed: <item ids> and why
Scorecard: <metric: baseline → now> for every row that moved; unchanged rows listed by name
Deleted: <what got smaller>
Added that others must read: <files, bytes>
Findings filed: <issue numbers, class trackers appended>
Charter diff proposed: <rows, for the next /batch-issues>
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
| D3 | Per-group budget as a charter fact, in CI-run units: ≤ 2 cited gates, plan ≤ 15 KB, drop `/verify-plan --adequacy` for `light` | keep today's procedure; report gates and plan bytes per item | two-way |
| D4 | Allowlist policy: cap rows per cited issue (proposed 50), structured `cite=#N`, one ledger per generator | no cap; report the three allowlist rows | two-way |
| D5 | Branch policy, now that PRs into `dev` run the full gate: run it on `dev` pushes too, or drop `dev` and protect `mainline` | unchanged | two-way |
| D6 | Python oracle: pin 3.12 (CI) or 3.13 (owner machine, #2350's measurements) in one file | 3.12 | two-way |
| D7 | `dict.get` returns `Optional<V>` (today) or `V?` (spec `collection_types.md:41`) | unchanged; fix the spec text to match or mark pending | one-way for callers |
| D8 | File the audit's remaining untracked items now (W1.2, W1.4 class issue, the tooling class issue for W0.2–W0.4, L7's `isalnum`, dead `src/test_discovery.cs`, hand-written plans under `docs/stdlib/`) or at the next batching call? | file the class issues the waves need (W0 tooling, W1.2, W1.4) when their first commit is ready; the rest at the next call | two-way |
| D9 | **Ruled 2026-11-07, option B:** 62 older plans and the 2026-08-30 charter are committed under `docs/process/` as they stood (feaadc8d0); they carry no stamp and may include drafts. The current charter's 22 per-call backups stayed machine-local, so the evidence condensation removed is not in the repo. | done; `/batch-issues` records it | one-way in practice: the repo is public, so deleted files stay in history |
| D10 | **Record:** #2455 added the `/ci-gate` skill and script (4.4 KB of procedure, 290-line script, 674-line test) against §1's "no new skill". Net-zero argument: it ends do-not-merge PRs (one was merged by accident and reverted) and turns a ~1 h local gate into ~20 min of CI. Accept as recorded, or ask for the skill to fold into `/push`? | accepted as recorded | two-way |
| D11 | **Record:** this revision adds `.claude/scripts/scorecard` (one script, no skill, no document). Net-zero argument: it replaces the 14 prose "how to measure" cells, 11 of which could not be followed, and the smoke agents spent ~150 k tokens re-deriving them. Keep, or inline the commands in §5.1? | keep | two-way |
| D12 | W0.5 landed `MergeFrom` as **equal-value tolerant** (a key in both operands passes when the values are equal; #2443 item 4 permits it) rather than strictly disjoint as the issue title and charter row 0 say. Accept as the item-4 design and reword the title and row, or open a P82 cell to measure whether any per-file infos actually share keys and tighten to disjoint? | accept; reword at the next `/batch-issues` | one-way once P82 tranches rely on the tolerance |

---

## 8. Known hazards

- **The #2438 ceiling binds `IntegrationTestBase` subclasses only.** vstest can OOM on the result send, so a passing local run can print "Test Run Failed" with 0 failed rows. The fix came back with #2453, but the two largest results sit outside the base class (#2438's comment of 2026-11-07). In a local log, count the Failed rows; exit 1 alone is not a failure. CI gates are unaffected: `ci-summary` counts from `.trx`.
- **Rules that lived only in the owner's machine-local memory, which a cloud session does not have:**
  - `close`, `fix` or `resolve` in any form directly before `#N` in a commit message closes that issue when the commit reaches `mainline`, even after a negation or with a colon ("not fixed: #2036" closed #2036). A wrongly closed issue reddens the allowlist gate and every `TODO(#N)`. Comments and issue bodies do not trigger this.
  - Agent `isolation: "worktree"` bases on `origin/mainline`, not `dev`; switch to the dev sha and cherry-pick back.
  - In a mutation run, restore with `shutil.copy` plus a `touch`, never `copy2`: the kept mtime makes incremental builds skip the restored file and reports a false green.
  - An `if (false)` mutation fails the build (CS0162), which proves nothing about the guard. Use `if (x.Length < 0)` and check the build's exit code.
  - Subagent results truncate near 6k characters; have workers write reports to a file.
  - Worker summaries need repo-relative paths, and a worker's "no route" claim from a grep needs a run.
- **`test-hooks.sh` read 60 passed, 1 failed on the owner's Mac @ feaadc8d0**, against the 61/0/0 in §2 and §4. The failing case is `cs: dotnet format run from the file's project dir with a relative --include`. The cause was not investigated. Run it in the container; if it is red there too, that is a W0.1 finding for the ledger.
- **`scorecard` uses GNU `date -d`.** On macOS the CI-wall row crashes and the nightly row may too. Linux containers should be unaffected; this was not run on one.
- The stall watchdog and the console logger are coupled (#2257) on the **local fallback** only. Omit the logger and a green local gate looks like a stall.
- The `-latest.log` symlink can point at a peer's run until W0.3 lands. Read the path the wrapper printed.
- A `sharpyc` binary older than HEAD reproduces fixed bugs (the dict fix and the freeze landed after the audit's build). Build once before probing.
- Generated C# (`regenerate_spy_stdlib.sh` MODULES, 18 modules) is pinned byte-for-byte and must be whitespace-clean; never hand-edit it; an emitter layout change ripples into it and into 148 `.expected.cs` snapshots.
- A row's reason text in an allowlist counts every `#N` as a cite; closing that issue reddens the row.
- `.features` fixtures come in gated/ungated pairs; do not dedupe them.
- The incremental cache key now covers features and references (764737b1d). The remaining cell: an in-process cold compile after a same-path rebuilt reference still binds the old assembly (#2114).
- Building `stdlib.spyproj` in place writes untracked `bin/` and `obj/` into `src/Sharpy.Stdlib/spy/`. Probe on a copy; the scorecard does.

---

## 9. Kickoff prompt for the orchestrator session

Next steps, in order: §2 re-check → baseline scorecard → P77b close-out (§4) → Wave-0 tooling issue → W0.3, W0.2, W0.4 → one PR into `dev` with its gate → recap (§6) and proposed charter diff → the owner merges and rules on §7 → Wave 1.

Paste this to start:

```
Read docs/process/handoff-2026-11-06-remediation.md in full (skip §10). Then read the comments on issue #2443 (gh api -X GET repos/antonsynd/sharpy/issues/2443/comments) and the R-LK row of the charter (grep -n '^| R-LK' docs/process/rounds/remediation-round-2026-09-20-batching.md). Do not read the audit or the rest of the charter unless an item sends you there.

Then: (1) run the §2 re-check and report what has landed since 18a11bbb7; (2) build once with the serialized wrapper, run .claude/scripts/scorecard --since <today> --base <origin/dev at session start>, and keep the output for the recap; (3) run the P77b close-out in §4 yourself, and stop if its first check fails; (4) file the Wave-0 tooling class issue (§4, Wave 0 header) and post the scorecard on it; (5) run W0.3 then W0.2 with one Opus worker each, and W0.4 in parallel, every prompt carrying the §3 worker preamble verbatim; (6) put the §5.2 cruft trailer in every commit; (7) stop on any §5.3 rule and write the §6 recap instead of continuing.

Landing: work on one branch cut from origin/dev and open one PR into dev when Wave 0 is ready. Do not merge it, and do not push Wave 0 work to dev or mainline; the owner merges. The close-out's plan tick is the one exception: commit it on that branch too, so it lands with the PR.

Gate: use /ci-gate. If it exits 3, or GitHub refuses its gh calls from this container, cite the PR's automatic run instead (.claude/scripts/ci-gate --run RUN_ID, or read the run with gh api). If that fails too, stop and report. Never fall back to a whole-solution test in a container under 16 GB.

Never put close, fix or resolve in any form directly before #N in a commit message, even in a negated sentence or with a colon: it closes the issue when the commit reaches mainline.

Use only Opus subagents. Do not re-ask any ruling in the charter §4 or any §7 default. Put every decision you cannot make into §7's format and wait.
```

---

## 10. References

- Audit: `docs/audits/health-check-2026-11-06.md` (§2 architecture, §3 runtime, §5 process, §9 charter cross-reference, §10 program). Artifact: `https://claude.ai/artifact/NBauE8wv83sCoatKnUzf9o`.
- Charter: `docs/process/rounds/remediation-round-2026-09-20-batching.md` (§1 row 0 and 6c⁶⁷″, §4 R-LK). Policy: `docs/process/README.md`.
- Trackers: #2443 (P82), #2114 (cache key class), #2124 / P33 (exception tree), #2438 (OOM false red; open until T12, fix back with #2453), #2440 (dict order; close after W1.1 review), #2365 (R-KK pin), #1952 and #2423 (omnibus allowlist cites), #2433 / #2453 (P77b).
- Design: `docs/design/lowering-ir.md` (E1/E2), `docs/design/spy0908-policy.md`, `docs/design/verification-contract.md` §6 (gate), §11–§12 (tiers), `docs/design/python-fidelity-scope.md`.
- Scripts: `.claude/scripts/ci-gate`, `.claude/scripts/scorecard`, `.claude/scripts/dotnet-serialized`, `.claude/hooks/test-hooks.sh`, `build_tools/check_allowlist_issue_state.sh`, `build_tools/check_spec_blocks.sh`.

---

## 11. Smoke run of revision 1 (2026-11-07)

Three read-only Opus agents took revision 1 as a new orchestrator would: one measured every scorecard row, one audited every item against what had landed, one followed the kickoff prompt and kept a friction log (28 entries, 20 contradictions). What changed and why, so the next revision does not relearn it:

- **§2 was a day stale.** All five task cards had landed; a literal reader would have re-implemented W0.1 and W0.5 and probed W1.1 with a binary that predated its fix. §2 is now a dated table plus commands that distinguish live work (`--no-merged`, `state=all`).
- **The gate changed under the handoff.** #2455 made CI the default gate; W0.2's `.claude/scripts/gate` would have been a second gate. Every gate reference now says `/ci-gate`, and "one gate" is defined so the budget and the stop rule are decidable.
- **11 of 14 scorecard rows could not be measured as written.** A markdown-escaped `\|` in a regex, `**` without globstar, a LOC denominator that disagreed with its own baseline, a hello-world baseline that was the compile-error path, no `.spyproj` path, no program, and `gh api --paginate` refused by the proxy. The rows are now one script, and §5.1 carries baseline, now, and direction only.
- **The allowlist direction would have fired the stop rule on a planned outcome.** P77b adds 1,187 rows by design. The metric is now rows outside seed-plan harnesses, plus distinct issues cited.
- **Two items were forbidden by a stop rule** (W1.4 and W2.3 each need an allowlist file). The rule now exempts allowlists named in §4.
- **The handoff contradicted itself on budgets** (§3 enforced what D3's default deferred) and contradicted the repo on `rm` (eleven skills clear `.claude/tmp/`), on charter edits (`docs/process/README.md` reserves them for batching calls), on the third `/verify-plan` (contract §12 allows it), and on commit subject length (72, not 100). Each is resolved toward the repo.
- **The cruft test failed the two best commits of the day** on question 2 (a guard is a pure addition) and questions 1 and 6 (an unchartered item has no issue to cite). It now accepts audit sections as cites and exempts guards that replace silent behaviour, and has a one-line trailer form.
- **The reading list cost 75 KB, of which 25 KB was needed.** Audit §9–§10 restate §4; §3.20o restates #2443. The kickoff now reads the handoff, the #2443 comments and one grep.
- **Hazards:** the #2438 trap (closed, fix reverted) was invisible from the issue state; the feature-flag cache hazard was stale.

The scorecard measured by the smoke run @ 1f94bfb06 and the three agent reports are the "Now" column above; the reports themselves were session scratch and are not committed.
