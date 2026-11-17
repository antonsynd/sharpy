# Goals for the name-resolution arc — from the 2026-11-17 health watch

> **For:** any session that plans, implements, verifies or watches work on `dev` from 2026-11-17 on.
> **Replaces:** `handoff-2026-11-06-remediation.md`, whose Waves 0–3 have landed (owner, 2026-11-17:
> "i think we've finished it already, so we should create a new one").
> **This is not a plan and not a charter.** It changes no order, tier or ruling. It says what
> "improving" means until the arc below closes, how to measure it, and when to stop and ask.
> Numbers are measured @ `af14cc69f` unless a row says otherwise.

## 1. Goals

The three goals stand, in priority order. An item that serves none is dropped.

1. **Stable and trustable.** A program that compiles runs as CPython 3.15 would, a program that
   should compile does, and no C# error reaches the user. Silent-wrong is the worst class.
2. **Efficient sessions.** A fresh session builds, probes, runs the owning tests and reads the
   tracker without contradiction between `CLAUDE.md`, the hooks and the skills.
3. **Less process per unit of fixed behaviour.**

Where the last round left each:

| Goal | Reached | Not reached |
|---|---|---|
| 1 | Fixes land by class behind generators (P92, P93, P77 stage 0). 13 of 13 closures of 11-12…11-17 hold when their cells are run; 0 of 20 sampled new issues is a regression. `SemanticInfo` tables 88 → 85. | 3 of 5 behaviour PRs of that week introduced a silent-wrong cell their own reviews missed (#2679 ×2, #2682, #2699). Open issues 284 → 352; SPY0908 titles 76 → 91. |
| 2 | `CLAUDE.md` commands all run (0 of 6 blocked); hooks 61/61; CI is the gate; the warm no-change build is an up-to-date check. | CI median wall 19 min, slowest shard 15 (targets were 12 / 8). `mainline` was red 30 h unseen: its coverage-on path runs on no pull request. |
| 3 | Review lines current on 21 of 21 code PRs; every baseline raise quotes the owner. | Commit mix of the week: process 72, test 57, docs 31, fix 17, feat 9 of 207. The read set (`CLAUDE.md`, the process README, the contract, every skill) grew 286,358 → 299,372 bytes. Verify passes ran 5, 2, 5 against a cap of two. |

## 2. The arc

The charter's order is unchanged (R-PM): **P77 → P78 → P79 → P21b → P21c**, with the compiler lane
P86 → P82 tranche 4 → P95 → P99 beside it. The arc closes when P21c's verification record merges.
P77 is in flight (`claude/p77`, plan-775c50). `_membershipNeedleBindings` moves onto an IR node in
P82 tranche 4 (owner, 2026-11-17).

## 3. Scorecard

One command, after a build: `.claude/scripts/scorecard --since <date> --base <sha>`, plus
`python3 build_tools/scorecard_metrics.py --check`. Read at the start and the end of each group.

| Metric | Now | Wanted by the end of the arc |
|---|---|---|
| Regressions found after merge (correct at the base, wrong at the head) | 2 in the week (#2679) | 0 per group |
| Reviewer-found defects per merged behaviour PR, after the fact | 6 over 5 PRs | falling |
| Open SPY0908 / CS-leak titles | 91 | down |
| Open issues; closed ÷ filed | 352; 15 ÷ 39 | closed ≥ filed over the arc; a rise during a generator's stage 0 is discovery and is said so |
| Known-red rows, by owner of the row | P77 668 · P93 residue 57 · emitter inspection 213 · other 305 (1,243 outside the seed) | each plan drains what its stage 0 added; emitter inspection falls each P82 tranche |
| `SemanticInfo` side tables | 85 | ≤ 84 after tranche 4; never up |
| Read set, bytes | 299,372 | not up; a new skill or section deletes as much as it adds |
| Counted procedure key; `CLAUDE.md` | 133,747; 9,304 | not up |
| Verify passes per plan | 5, 2, 5 | ≤ 2 |
| `mainline` push runs green | red since 2026-11-16 14:30Z | green within a day of any red |
| CI median wall / slowest shard | 19 / 15 min (shard 7 up to 24 on PRs) | not up |

## 4. Stop rules

Stop the group, write the recap, and bring the decision to the owner when one holds:

- A ratchet is raised (rows outside the seed, procedure bytes, `CLAUDE.md` bytes, the side-table
  pin) without the owner's words quoted in the pull request body.
- A second `/verify-plan` pass does not pass. The open finding goes to the owner as a decision; a
  third pass is not offered (R-LW).
- A plan's verification record would merge with more known-red rows than its stage 0 added, beyond
  a residue the owner ruled.
- A program that ran and matched CPython before a change is refused or answers differently after
  it, without a ruling quoted for that direction (contract §3).
- A guard's mutation does not go red, or a row can change its wrong answer and stay green.
- `mainline` or a scheduled run on it is red and no issue or tracker cell names the failing test
  as the log prints it within a day.
- The same class produces a third cell after its class fix landed.

## 5. What a review must run

Each of the week's missed defects sat one axis from the cells its PR tested. A review of a
behaviour change runs the change on a build of the base and of the head, and its hunt list names
the neighbouring kind of every operand the diff touches: the other backing type (int / string),
`None` on each side, a user dunder that could run, a stateful operand, a CLR type that needs a
conversion. `/health-watch` › Reviewing a PR is the procedure; this paragraph adds only the axes.

## 6. Recap

The recap format of `handoff-2026-11-06-remediation.md` §6 stands, one per landed group, posted on
the group's tracker. Its three questions stand: what was deleted, what was added that others must
read, which stop rule came closest.

## 7. Decisions open for the owner

| # | Decision | Default while waiting |
|---|---|---|
| 1 | Fix `a is c` running a user `__eq__` (regression from #2679) in a no-plan lane, and file the untracked cells of the 2026-11-17 watch | not fixed, not filed |
| 2 | Root `Directory.Build.props`, `Directory.Build.targets` and `global.json` are not "code" under the process README item 6, so a PR touching only them needs no review line | unchanged |
| 3 | Coverage-on is seen only on `mainline` pushes and the daily `dev` run; a dependency bump merged into `mainline` meets it first | bumps go through `dev` |
