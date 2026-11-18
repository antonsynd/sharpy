# Goals for the name-resolution arc — from the 2026-11-17 health watch

> **For:** any session that plans, implements, verifies or watches work on `dev` from 2026-11-17 on.
> **Replaces:** `handoff-2026-11-06-remediation.md`, whose Waves 0–3 have landed (owner, 2026-11-17:
> "i think we've finished it already, so we should create a new one").
> **This is not a plan and not a charter.** It changes no order, tier or ruling. It says what
> "improving" means until the arc below closes, how to measure it, and when to stop and ask. Where
> its stop rules depart from the 11-06 handoff's §5.3, §4 lists the change; §3's scorecard
> replaces §5.1's, and the line under its table lists what changed.
> Numbers are measured @ `af14cc69f` unless a row says otherwise.

## 1. Goals

The three goals stand, in priority order. An item that serves none is dropped.

1. **Stable and trustable.** A program that compiles runs as CPython 3.15 would, a program that
   should compile does, and no C# error reaches the user. Silent-wrong is the worst class.
2. **Efficient sessions.** A fresh session builds, probes, runs the owning tests and reads the
   tracker without contradiction between `CLAUDE.md`, the hooks and the skills.
3. **Less process per unit of fixed behaviour.**

The 11-06 handoff's non-goals stand: no new hand matrix (contract §11); no new node-keyed
`SemanticInfo` table (R-LK); no new process document, skill, label, tier letter or ruling vocabulary
(this file replaces one); no re-ordering of the charter without an owner ruling; no whole-solution
test run in a container under 16 GB (the gate is `/ci-gate`; CLAUDE.md's local command is the
fallback when it exits 3).

Where the last round left each:

| Goal | Reached | Not reached |
|---|---|---|
| 1 | Fixes land by class behind generators (P92, P93, P77 stage 0). 13 of 13 closures of 11-12…11-17 hold when their cells are run; 0 of 20 sampled new issues is a regression. `SemanticInfo` tables 88 → 84, then 85 (#2699's raise, ruled no precedent). | 3 of 5 behaviour PRs of that week introduced a silent-wrong cell their own reviews missed (#2679 ×2, #2682, #2699). Open issues 284 → 352; SPY0908 titles 76 → 91. |
| 2 | `CLAUDE.md` commands all run (0 of 6 blocked); hooks 61/61; CI is the gate; the warm no-change build is an up-to-date check. | CI median wall 19 min, slowest shard 15 (targets were 12 / 8). `mainline` was red 30 h unseen: its coverage-on path runs on no pull request. |
| 3 | Review lines current on 21 of 21 code PRs; every scorecard-baseline raise quotes the owner. | The side-table pin's raise (#2699) did not. Commit mix of the week: process 72, test 57, docs 31, fix 17, feat 9 of 207. The read set grew 286,358 → 299,372 bytes. Verify passes ran 6, 2, 5 against a cap of two. |

## 2. The arc

The charter's order is unchanged (R-PM): **P77 → P78 → P79 → P21b → P21c**, with the compiler lane
P86 → P82 tranche 4 → P95 → P99 beside it. P77 and P21b's codemod land alone; every lane holds for
their gates (R-PM). The arc closes when P21c's verification record merges. P77 is in flight
(`claude/p77`, plan-775c50). `_membershipNeedleBindings` moves onto an IR node in P82 tranche 4
(owner, 2026-11-17).

## 3. Scorecard

Read at the start and the end of each group. Sources, after a build:

- `.claude/scripts/scorecard --since <date> --base <sha>`: open issues, closed ÷ filed, SPY0908
  titles, side tables, commit mix. Its footer still names the 11-06 handoff's §5.1; this table
  replaces it.
- `python3 build_tools/scorecard_metrics.py --check` (`--json` for the numbers): procedure key,
  `CLAUDE.md`, rows outside the seed.
- `python3 build_tools/check_plan_refs.py docs/process/plans/<plan>.md`: its `caps (R-LW)` line
  gives body bytes and verify passes; `--caps-all` prints only cap violations across every plan.
- Known-red rows by owner: the row count of each `*allowlist*.txt`, attributed by the issues it cites.
- Read set: the bytes of `CLAUDE.md`, `docs/process/README.md`, `docs/design/verification-contract.md`
  and every `.claude/skills/*/SKILL.md`.
- PR shard times, regressions, reviewer-found defects and `mainline` reds: the `/health-watch`
  report (the script prints the `mainline` CI wall and nightly).

| Metric | Now | Wanted by the end of the arc |
|---|---|---|
| Regressions found after merge (correct at the base, wrong at the head; a compile-time refusal of a program CPython rejects is correct, tie-break 2) | 4 in the week (#2679 ×2; #2682 ×2, the helper cells on #2110) | 0 per group |
| Reviewer-found defects per merged behaviour PR, after the fact | 6 over 5 PRs | falling |
| Open SPY0908 / CS-leak titles | 91 | down |
| Open issues; closed ÷ filed | 352; 15 ÷ 39 | closed ≥ filed over the arc; a rise during a generator's stage 0 is discovery and is said so |
| Known-red rows, by owner of the row | P77 668 · membership rows 57 (P94: #2674 46 · #2644 2; band #2651 6; both #2674 and #2651 2; P17 #1931 1) · emitter inspection 213 (#2443) · other 305 (1,243 outside the seed) | each plan drains what its stage 0 added; emitter inspection falls each P82 tranche |
| `SemanticInfo` side tables | 85 | ≤ 84 after tranche 4; never up |
| Read set, bytes | 299,372 (299,488 @ `0b8d894c0`, after #2725 on the owner's word) | not up; a new skill or section deletes as much as it adds |
| Counted procedure key; `CLAUDE.md` | 143,159 (133,747 before #2725 counted `/plan-flow`, owner's word quoted there); 9,304 | down; `CLAUDE.md` ≤ 8 KB (the 11-06 targets) |
| Verify passes per plan | 6, 2, 5 (the counter read 5 for the first before #2725 fixed it) | ≤ 2 |
| `mainline` push runs green | `.NET 10` and Dev Staleness Gates red since 2026-11-16 14:30Z; the `.NET 10` fix (#2724) is on `dev`; both clear at the next promotion, after the sync #2728 | green within a day of any red |
| CI median wall / slowest shard | 19 / 15 min (shard 7 up to 24 on PRs) | down, to ≤ 12 / ≤ 8 (W0.4's targets) |

Changed from §5.1: no target is relaxed. Dropped here (the script still prints them): test ÷
compiler LOC, distinct cites, gate citations per item (now a stop rule), nightly, warm build, hello
world, fresh-session commands, hooks.

## 4. Stop rules

Stop the group, write the recap, and bring the decision to the owner when one holds:

- The scorecard moves the wrong way on two metrics in one group.
- Procedure bytes, `CLAUDE.md` bytes or the side-table pin rise in a group, or allowlist rows
  outside the seed rise outside a generator's stage 0, even on the owner's word (a raise lands only
  with it quoted in the pull request body).
- A plan's verification record would merge with a case its stage 0 listed as known-red still
  listed, beyond a residue the owner ruled (`git diff <stage-0 base>..<record sha> -- <the
  allowlist files its plan names>`).
- A plan body passes 20 KB (R-LW; `check_plan_refs.py --caps-all`).
- A second `/verify-plan` pass stamps NEEDS REVISION. The open finding goes to the owner as a
  decision; no further pass is offered unless the owner asks for one (R-LW; owner 2026-11-17, #2725).
- An item has two gate citations and is not done.
- A worker proposes a new `SemanticInfo` dictionary, a hand matrix, or an allowlist file its plan
  does not name.
- A program that ran and matched CPython before a change is refused or answers differently after
  it, without a ruling quoted for that direction (contract §3).
- A guard's mutation does not go red, or a row this group adds or regenerates can change its wrong
  answer and stay green.
- `mainline` or a scheduled run on it is red and no issue or tracker cell names the failing test
  as the log prints it within a day.
- The same class produces a third cell after its class fix landed (the fix commit is on `dev` and
  cited on the class issue; a cell is a new issue or comment on that class after that commit's date).

Changed from the 11-06 handoff §5.3. The three revisions the watch recommended and the owner
accepted ("all your recs", decision 7): the 15 KB plan cap is R-LW's 20 KB; "CONVERGED on a NEEDS
REVISION" is the second-pass rule above (decision 4); the rule on rows outside the seed allows a
generator's stage 0 and judges a plan at its record. Proposed here, for the owner's acceptance:
`CLAUDE.md` bytes join the rise rule; the `mainline` rule; the direction rule (contract §3); the
regenerated-row clause, which widens decision 5 from P77 to every group.

## 5. What a review must run

Each of the week's missed defects sat one axis from the cells its PR tested. The hunt list of a
`/health-watch` review of a behaviour change names the neighbouring kind of every operand the diff
touches: the other backing type (int / string), `None` on each side, a user dunder that could run, a
stateful operand, a CLR type that needs a conversion; and it runs the change on a build of the base
and of the head. Whether `/implement-plan` step 4 takes the same axes is the owner's (§7).

## 6. Recap

The recap format of `handoff-2026-11-06-remediation.md` §6 and its §8 hazards stand, one recap per
landed group, posted on the group's tracker. Its three questions stand: what was deleted, what was
added that others must read, which stop rule came closest.

## 7. Decisions open for the owner

| # | Decision | Default while waiting |
|---|---|---|
| 1 | Add root `Directory.Build.props`, `Directory.Build.targets` and `global.json` to the process README item 6's code paths, so a PR touching them needs a review line? | not added: green alone, as item 6 reads today |
| 2 | Run the coverage-on path on pull requests that change package versions? | no: bumps go through `dev`, and the daily coverage run meets them |
| 3 | Whether `:=` keeps its Python role once `let`/`var` land (11-06 handoff D17; owner note 2026-11-08); charter row 19 carries it to P21b's plan (R-LR) | unchanged: `:=` stays as the spec has it |
| 4 | Add §5's axes to `/implement-plan` step 4 (raises the procedure key)? | not added: the watch's reviews carry them |

Ruled since this document was drafted (owner, 2026-11-17), answering the draft's first decision,
since removed, in two parts (fix `a is c`, file the watch's cells): "1) your rec; 2) yes, file
them". The cells are filed; `a is c` running a user `__eq__` goes to a no-plan lane (Refs #2680).
Then "yes" to closing #2713 and to a Dependabot rule for version 11: #2713 is closed, and #2729 holds
six packages versioned with .NET below 11 until #2548. The hold and #2719's `target-branch: "dev"`
both live in `.github/dependabot.yml`, which Dependabot reads from the default branch, `mainline`,
so both take effect at the next promotion.
