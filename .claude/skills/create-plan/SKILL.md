---
name: create-plan
description: Create an implementation plan from GitHub issues or a description
argument-hint: "<issue numbers or description> [--light | --full] | revise <plan path or id>"
---

Read `docs/design/verification-contract.md` before proceeding (§10 file-by-class, §11 one-group-per-plan and §12 the verification tier included); every section below applies it. Read `docs/design/python-fidelity-scope.md` when any input is a Python-parity cell.

Create an implementation plan for an engineer to follow, at the **tier the charter set for the group** (contract §12): a `full` plan carries the phases and task list below; a `light` plan is the one-page design-of-record of the light template, and `/verify-plan` grades only its matrix, guards and design predicates. Writes the working copy in `.claude/plans/` and commits it as a **draft** to `docs/process/plans/<plan-id>.md` on the plan's branch (§3.1; `docs/process/README.md` › The stage trail).

**Usage:**
- `/create-plan 222,223,224,225,226` — read GitHub issues and create a plan at the charter's tier
- `/create-plan 222,223 --light` / `--full` — force a tier; recorded as `override:` in `## Scope` when it differs from the charter
- `/create-plan fix the 5 bugs from the audit` — create a plan from a description (no charter row → the tier is derived from the §12 letters and stated)
- `/create-plan revise docs/process/plans/plan-a1b2c3.md` (or `revise plan-a1b2c3`) — revise that plan: after a NEEDS REVISION pass, or a draft on the owner's instruction (see **Revising a plan**)
- `/create-plan` — ask the user what to plan

## Argument Handling

Parse `$ARGUMENTS`:
- Strip a trailing `--light` or `--full` and remember it as the requested tier
- If it looks like comma-separated numbers (e.g., `222,223,224`), treat them as GitHub issue numbers
- If it looks like a description, use it as the planning goal
- If the first word is `revise`, the rest names ONE existing plan: a path under `docs/process/plans/` or `.claude/plans/`, or a bare plan-id (`plan-a1b2c3`, resolved to `docs/process/plans/plan-a1b2c3.md`). Go to **Revising a plan** below; the Scope check, the Tier check and Steps 1–3 as written are for a NEW plan and do not run. `revise` with no plan, or with a name that resolves to no file, lists the newest plans (the two commands below) and asks which — never guess. A `--light` / `--full` flag next to `revise` is a tier change, which is the owner's: ask, and record the answer as `override:` in `## Scope`
- If empty, ask the user what they want to plan. To re-plan an existing plan, list the three newest (`ls -t docs/process/plans/*.md | head -3`), ask which — never pick silently — and follow **Revising a plan**.

## Revising a plan (`revise <plan path or id>`)

A revision is plan-author work on an existing file of record. It runs in a **fresh context**, never in the session that stamped the plan (`/verify-plan` Output §5), and it changes only what it was asked to change.

1. **Sync and locate.** `git fetch origin` and read the file of record at the tip of the plan's branch (working-copy rules: `docs/process/README.md` › The stage trail). The revision ends by committing it (§3.1).
2. **Read the first line — it decides what a revision may do** (contract §12 Convergence):

   | First line | What `revise` does |
   |---|---|
   | `<!-- DRAFT, not verified …` | Revise as the user directs; there is no pass to answer. |
   | stamp, result **NEEDS REVISION** | Revise only what the `## Verification Summary` names — every finding of every graded dimension, the design-decision predicates and the Unchecked Claims. |
   | stamp, result **CONVERGED** | Do not revise: say the next step is `/implement-plan <path>` (the `### Deferred findings` rows are its first task) and stop. |
   | stamp, result **PASS** / **PASS WITH CORRECTIONS** | A revision discards that verdict. Say so and ask before editing anything. |

3. **Owner-only questions first, in one batch.** An Unchecked Claim that needs the owner's word, a `worked` → refused change (contract §3), a tier or scope change and an unruled door are asked once, each with its options and a recommendation, before the design is rewritten around them. Each answer is recorded in the plan as the owner's words with the date (`## Scope` `override:` for a tier or scope answer; the Defect Class `Owner rulings` row for the rest), lettered at the next `/batch-issues`.
4. **Answer each finding at its class, by rewriting the text it names.** Replace, never append: no answer paragraph, pass history or superseded decision beside its replacement. Net body growth per revision ≤ +3 KB. Re-measure what the pass measured before building on it (`run`, at a named sha). A finding against a generator's enumeration, map or totality assertion is answered by a new stage-0 commit (contract §11) and the counts the plan cites — never by adding the cell to the plan. A finding the revision declines keeps its reason in the revision-log line, not dropped.
5. **Reset the record.** Delete the stale stamp lines and the whole `## Verification Summary` (a left-over stamp is read by `/implement-plan` as the current verdict). Append ONE `## Revision log` line: `@ <the pass's sha> — revised after the <n>th pass (NEEDS REVISION, Dimension N): <the decisions changed>`. The stamp and summary are gone, so this line alone carries the dimension the Convergence rule needs and, when the pass drafted the Defect Class, `drafted Defect Class confirmed` or `replaced`. A revised draft that had no pass writes `@ <sha> — revised before the first pass: …`.
6. **Check, commit, report.** `check_plan_refs.py`, then §3.1 with the subject `process(plan): <plan-id> (<group>) revised after pass <n>`, then the Report (§4) plus each finding and how it was answered, the body bytes before → after, the owner's answers, what was re-measured, and the next step `/verify-plan <path>` **in a fresh context**.

## Scope check (before any research)

A plan implements **one non-chore group** of the round charter and lands on **one gate** (contract §11). If the issue list spans more than one group, or bundles a group that changes printed output or emitted layout with anything else, **stop and ask** which group to plan — never bundle. Chores ride along only when they touch no seam the group touches. Record the answer in the plan's `## Scope` block; a deliberate exception is written there as `override: <owner's words>`.

## Tier check (contract §12, before any research)

Find the group's row in the newest charter (`ls -t docs/process/rounds/remediation-round-*-batching.md | head -1`, §1 table and §3 row) and read its **Tier**. Then:
- **Charter says `band`** (or no charter row and no §12 letter) — stop: it has no plan; implement it as one commit, or as the no-plan lane (`docs/process/README.md`), and say so; either way its PR merges only after the independent review.
- **Charter says `light`** (and no `--full`) — write the light template (below); **`full`** — the full template (§3).
- **No charter row** (description input, or an issue batched since the last call) — derive the tier yourself from the §12 letters (a–e), write the letter or "none → light" into `## Scope`, and say in the report that the tier is derived, not ruled.
- **A flag disagrees with the charter** — obey the flag, write `override: <the user's words>` in `## Scope`, and say so in the report. Never silently promote or demote.
- **Research moves a light group onto a §12 letter** (an unruled decision surfaces, the seam turns out to be in `Semantic/`·`Lowering/`·`CodeGen/`, the matrix needs a generator) — stop, say which letter, and ask whether to promote; a promotion is written into `## Scope` as `tier: full (<letter>, promoted at /create-plan: <reason>)`.

**Light template.** One page is the goal (the cap is the body's 20,480 bytes, §3): the decisions an implementer must not re-derive, and no phases, tasks or testing strategy — the implementer sequences the work. Defect Class and Adversarial Review follow §3's rule for bug inputs. Run `python3 build_tools/check_plan_refs.py <plan>` before reporting: the `--adequacy` pass corrects nothing inline, so this is the author's reference check, and the report quotes its result.

```markdown
# <Plan Title>

## Context

<The issues and the measured state @ <sha>, ≤ 2.5 KB: numbers, no narrative>

## Scope

- **Group:** <P<n> — name> · **Tier:** light — <charter §3.<k> | derived, not ruled | override: <words>> · **Gates:** 1

## Defect Class

- **Violated contract:** <one sentence> · **Meta-class → cure:** <§1 row> · **Known cells:** #NNN …
- **Matrix:** <the arms of every touched dispatch site, beyond the repro lists> · **Generator:** not required: <why>
- **Standing harness:** <name, or the A<n> that adds one> · **Lane / reach / fidelity row:** <…> · **Owner rulings:** <links>

## Adversarial Review

- **Inert fix:** <the fallback one call later, and the probe that rules it out>
- **Blast radius:** <test projects and sweeps reached>

## Design Decisions

- **D<n>.** <decision> — rung <1–4>: <why no lower rung carries it>; predicate: <the claim about the code a reader checks>

## Acceptance

- **A<n>.** <matrix/sweep that goes green, allowlist drain, or execution evidence @ sha> — **M<n>:** <break → red>; positive control for an absence

## Issues to Close

- #NNN — **close criterion:** A<n> @ sha

## Class trackers this plan may post cells to

- #NNN — <class>
```

## Steps

### 1. Gather context

**If GitHub issues were specified:**
- Read each issue via `gh issue view <number>` (`dangerouslyDisableSandbox: true` — `gh` fails TLS verification in the sandbox)
- Read comments on each issue via `gh api repos/antonsynd/sharpy/issues/<number>/comments` — an owner ruling in a comment is authoritative; record the link, do not re-ask
- Understand the full scope across all issues
- If any input is a bug, ICE, or regression: the issue's repro list is a **symptom report, not a test plan**. Identify the class it belongs to (verification-contract.md §1) before designing anything
- For each issue record its **lane** and **reach** (from its labels, or assign them: `lane:*` / `reach:program|probe`, contract §10) and, for a Python-parity cell, its row in `python-fidelity-scope.md` — a DEVIATION-by-default surface is not planned without the promotion ruling

**If a description was provided:**
- Research the relevant codebase areas using Glob, Grep, and Read
- Identify the files and components involved

### 2. Research the codebase

Before writing the plan:
- Read the relevant source files to understand current state
- Check `docs/language_specification/` for any applicable specs (spec is authoritative — the plan changes the implementation to match, never the reverse)
- **Survey the language before the compiler** (CLAUDE.md › Core & Stdlib Conventions › layer ladder): for every behaviour the plan adds or fixes, check whether a `.spy` source, the dunder table (`docs/language_specification/dunder_methods.md`), a Core protocol interface (`ISized`/`IBoolConvertible`/`IReverseEnumerable<T>`), an operator overload (`src/Sharpy.Core/Dict.cs` is the model), a public `Contains(T)`/`IEnumerable<T>`, or an extension method already expresses it and is discovered by reflection (`ProtocolMembership.HasClrProtocol`, `TypeInferenceService.TryInferClrBinaryOp` — which unions operators from BOTH operand types). A checker or emitter rule keyed on a builtin name is rung 4 and needs a sentence naming what the CLR surface cannot express. Read the CALLER of any function named `*Fallback`/`*Default` before citing it as the seam
- Check existing tests in `src/Sharpy.Compiler.Tests/` and `src/Sharpy.Core.Tests/`, and the standing class harnesses in `docs/design/gap-discovery-contracts.md` — which one *should* have caught this?
- Verify Python behavior with `python3 -c "..."` where applicable
- Check for related GitHub issues with `gh issue list --search "..."` and the class's tracker (`gh issue list --label class-tracker --state open`) — sibling cells of the same class are usually already recorded there
- For a bug/ICE: reproduce with `run`, not `emit` (SPY0908 surfaces only under `run`); probe `b: bool = expr` to split mistyped (SPY0220) from untyped (silence) before choosing a seam
- Find mirrored/parallel sites: if the fix lands in one arm of a switch, dispatch table, or per-position handler, enumerate the other arms — a plan that patches one arm without a completeness scan is not a plan
- Note which generated artifacts the change reaches (spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger) and which test projects (CLAUDE.md › Testing › Commit gate)

Research `dotnet` runs go through `.claude/scripts/dotnet-serialized` (CLAUDE.md › Operational Contracts); prefer `/spy-run`, `/spy-emit`, `/quick-check`.

### 2.1. Stage 0 (generator groups)

If the matrix has ≥2 axes × ≥3 values or the group is a residue, the group is `full`: cut the plan's branch (§3.1's first command) and land stage 0 now, as contract §11 says; the plan cites its sha and counts.

### 3. Generate the plan

Write a plan file to `.claude/plans/` with a random name (use `openssl rand -hex 3` for a short hex suffix, e.g., `plan-a1b2c3.md`; the stem is the plan-id, and the plan's file of record is `docs/process/plans/plan-a1b2c3.md`). §3.1 commits the draft there once it is written.

**Caps (R-LW), at both tiers:** body ≤ 20,480 bytes, ≤ 2 verify passes, checked by `check_plan_refs.py` (§3.1), which also reports the section budgets the templates state.

The plan must follow this structure. `## Defect Class` and `## Adversarial Review` are **mandatory when any input is a bug, ICE, or regression issue**; omit them (with the line "Defect Class: N/A — feature work") only for pure feature work.

```markdown
# <Plan Title>

## Context

<The problem and its issues. With Current State ≤ 2.5 KB: numbers at a sha, no narrative>

## Current State

<What exists today, what's broken or missing — measured @ <sha>>

## Scope

<With Issues to Close and Class trackers ≤ 2 KB>

- **Group:** <P<n> — name> (one non-chore group; chores riding along: <none | #… — seam-free because …>)
- **Tier:** <light | full (<letter a–e>: <one clause>)> — <from charter §3.<k> | derived, not ruled | override: <words>> (contract §12)
- **Alone:** <yes — changes printed output / emitted layout | no>
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** <none | the owner's words>

## Defect Class

- **Violated contract:** <one sentence — what is supposed to be uniform across which axis>
- **Meta-class:** <from the standard-cure table, verification-contract.md §1> → **standard cure:** <seam / sweep / harness>
- **Known member cells:** #NNN … (the issues)
- **Sibling cells this plan must also cover:** <enumerated matrix — the issue repro list is a symptom report. With stage 0: none; see Generator>
- **Lane / reach:** <lane · program|probe>; **fidelity-scope row:** <COMPARED row | promoted by R-xx>
- **Standing harness that should have caught it:** <name> — or "none; Phase N adds one"
- **Generator:** <existing harness the cells join | stage 0 (§2.1) `<Type>` @ <sha>: <n> cases, <k> known-red rows; excluded axes with reasons; rows this plan turns green, by D<n> | not required: <the axis a generator cannot enumerate>>
- **Owner rulings:** <issue-comment links; authoritative, do not re-ask>

## Adversarial Review (pre-mortem)

<≤ 1.5 KB>
- **Alternative root cause:** <what else explains the symptom; the probe that discriminates>
- **Alternative layer:** <the lower ladder rung that could carry the fact — `.spy` source, dunder-table C#, protocol interface, operator overload — and the reason it cannot, or the plan moves there>
- **How the fix could be inert:** <fallback path one call later? decision duplicated in a sibling arm?>
- **Before/after by direction:** <the probe that distinguishes "ICE → diagnostic" from "restricts working code">
- **Blast radius:** <Stdlib.Tests / Cli.Tests / LSP parity / interop / metamorphic / differential / warm-cold>
- **Generated artifacts touched:** <spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger — regen early>

## Design Decisions

<Key architectural choices with rationale. Reference Sharpy axioms where relevant:
- Axiom 1 (.NET compatibility) > Axiom 2 (Type safety) > Axiom 3 (Python syntax)
- Reference docs/language_specification/ where applicable
- Every fact codegen reads is materialized in semantic analysis (Symbol.CodeGenInfo or a node-keyed SemanticInfo dictionary added to MergeFrom)
- Every decision names its layer-ladder rung (1 `.spy` / 2 Core C# in dunder spelling / 3 CLR-identity bridge rule / 4 name-keyed semantic rule) and why the lower rungs cannot carry the fact>

## Implementation

### Phase N: <Phase Name>

**Goal:** <What this phase achieves>

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: <harness or cell matrix, with the cells outside the issues' repro lists>
- Guard mutation: <the mutation that turns the new test/guard red — and the positive control for any absence assertion>
- Allowlist entries that drain: <file + entries, or "none">
- Execution evidence the close-out will cite: <what runs @ sha — program output, sweep counts, spec examples>

#### Tasks

- **T<n>. <Task title>** — <file(s) involved>
  - <Specific change description>
  - <Acceptance criteria — which bullet above this task satisfies>

### Phase N+1: ...

## Testing Strategy

- <New test fixtures needed (.spy + .expected/.error)> — for every negative fixture, the positive control that must keep passing
- <Edge cases to cover — cells outside the issues' repro lists; change axis when the spellings you vary all agree>
- <Outputs that discriminate — an example that prints the same thing with the bug present proves nothing>
- <For language changes: the spec section in docs/language_specification/ and its executed examples — a deliverable of equal weight to the code>
- <Blast radius: which sweeps and test projects the change reaches, and when regen of generated artifacts runs (early, not pre-push)>

## Issues to Close

- #NNN — <title> — closed by T<m>; **close criterion:** <the acceptance bullet and the evidence it needs @ sha>
- ...

## Class trackers this plan may post cells to

- #NNN — <class> — cells found outside this plan's contract go here as comments (contract §10); the lead files a NEW tracker only for a class none of these covers. No implementer runs `gh issue create`.
```

**Plan quality requirements:**
- Tasks follow the feature implementation order: Lexer -> Parser -> Semantic -> Validation -> Lowering (if an IR shape changes) -> CodeGen -> LSP -> Tests
- Tasks, decisions and mutations are named by id (`T3`, `D2`, `M1`); a plan carries no commit messages
- Enough rationale for any engineer, or a smaller model, to implement unambiguously
- Incremental commits — each task is independently committable
- A plan that fixes one arm of a mirrored/parallel-site structure without a completeness scan is not a plan
- A plan with a second non-chore group, or an output/layout-changing phase sharing a gate, is not a plan (contract §11) — split it
- A residue group or a ≥2-axis × ≥3-value matrix without its stage 0 (§2.1; a generator needing a product hook is T1 instead) is not a plan; one whose axes are hand lists is a hand matrix wearing a generator
- The plan names the class trackers it posts cells to and never instructs an implementer to `gh issue create` (contract §10)
- A design decision that adds a name-keyed (`BuiltinNames.X`, type-name string) rule in `Semantic/` or `CodeGen/` without a rung justification is not a plan (layer ladder, CLAUDE.md › Core & Stdlib Conventions)
- Every new test/guard/harness comes with the mutation that turns it red (verification-contract.md §2)
- The plan names which generated artifacts it touches and schedules their regeneration early (§7), and names its blast radius (CLAUDE.md › Testing › Commit gate)
- GitHub issues referenced and mapped to closing tasks, each with its close criterion
- No hard-coded model names or commit trailers

### 3.1. Commit the draft

A draft is committed from the moment it is written: the gitignored working copy does not survive its container. Run `python3 build_tools/check_plan_refs.py .claude/plans/<plan-id>.md` and fix what it flags. The branch is cut once from `origin/dev` and carries every stage (`docs/process/README.md` › The stage trail): `claude/<group>-plan` for a `full` plan, `claude/<group>` for a `light` one.

```bash
git fetch origin dev && git switch -c <branch> origin/dev   # first stage only; later stages `git switch <branch>`
{ echo '<!-- DRAFT, not verified: written by /create-plan on YYYY-MM-DD @ <sha>; no /verify-plan pass has stamped this text. -->'
  echo; cat .claude/plans/<plan-id>.md; } > docs/process/plans/<plan-id>.md
git add docs/process/plans/<plan-id>.md           # explicit pathspec; `git diff --cached --stat` shows this one file
git commit -m "process(plan): <plan-id> (<group>) draft"
```

Only the committed copy carries the DRAFT marker, so `/verify-plan` can stamp the working copy's first line. A revision writes `revised by /create-plan on YYYY-MM-DD @ <sha> after pass <n>` in the marker. Then `/push`; a `full` plan opens its one pull request into `dev` on the first push (body: group, tier, issues, the `check_plan_refs.py` result, the next step), and a `light` plan opens none — it rides in the implementation's.

### 4. Report

Tell the user:
- The committed path, its commit and branch (and a `full` plan's pull request URL)
- A brief summary of phases and task count
- For bug/ICE inputs: the class, the matrix size, and the standing harness named (or the phase that adds one)
- The scope line: group, **tier with its letter and its source (charter / derived / override)**, alone?, gates, generator (existing / built first / not required), and the trackers named
- The next step: `/verify-plan <path>` (nine dimensions at `full`; `--adequacy` at `light`: matrix arms, guard mutations, design predicates, by a fresh reader) before `/implement-plan <path>`, quoting the `check_plan_refs.py` result
