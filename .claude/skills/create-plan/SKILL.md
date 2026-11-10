---
name: create-plan
description: Create an implementation plan from GitHub issues or a description
argument-hint: "<issue numbers or description> [--light | --full] | revise <plan path or id>"
---

Read `docs/design/verification-contract.md` before proceeding (§10 file-by-class, §11 one-group-per-plan and §12 the verification tier included); every section below applies it. Read `docs/design/python-fidelity-scope.md` when any input is a Python-parity cell.

Create an implementation plan for an engineer to follow, at the **tier the charter set for the group** (contract §12): a `full` plan carries the task list, phases and commit messages below; a `light` plan is a one-page design-of-record (Scope, Defect Class, Design Decisions with rungs, acceptance bullets with their mutations, Issues to Close with close criteria) and no task list — the implementer sequences the work, and `/verify-plan` grades only its matrix, guards and design predicates. Writes the plan as a working copy in `.claude/plans/` (repo-local, gitignored), then commits it as a **draft** to `docs/process/plans/<plan-id>.md` under a DRAFT marker and opens a pull request into `dev` (§3.1); `/verify-plan` commits its stamped pass over that file at every result, and the committed copy is what `/implement-plan` and `/verify-implementation` read (policy: `docs/process/README.md` › The stage trail).

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
- If empty, ask the user what they want to plan. If they want to extend or re-plan an existing plan, list the three newest drafts and the three newest committed plans and ask which one — never silently pick one — then follow **Revising a plan** exactly as `revise <that path>` would:
  ```bash
  ls -t docs/process/plans/*.md 2>/dev/null | head -3     # files of record — the first line of each is the DRAFT marker or a /verify-plan stamp
  ls -t .claude/plans/*.md 2>/dev/null | head -3          # working copies (gitignored) — this container's only
  ```

## Revising a plan (`revise <plan path or id>`)

A revision is plan-author work on an existing file of record. It runs in a **fresh context**, never in the session that stamped the plan (`/verify-plan` Output §5), and it changes only what it was asked to change.

1. **Sync and locate.** `git fetch origin dev` and read the file of record at the tip of `origin/dev` (or on its open lane `claude/<group>-plan`): the pass being answered may have merged after this container was cloned. The revision edits the working copy `.claude/plans/<plan-id>.md` and ends by committing it over the file of record (§3.1). When the working copy is missing (a new container), copy the file of record back to `.claude/plans/` first; when both exist and differ beyond the DRAFT marker or the stamp, ask which is current (`docs/process/README.md` › The stage trail).
2. **Read the first line — it decides what a revision may do** (contract §12 Convergence):

   | First line | What `revise` does |
   |---|---|
   | `<!-- DRAFT, not verified …` | Revise as the user directs; there is no pass to answer. |
   | stamp, result **NEEDS REVISION** | Revise only what the `## Verification Summary` names — every finding of every graded dimension, the design-decision predicates and the Unchecked Claims. |
   | stamp, result **CONVERGED** | Do not revise: say the next step is `/implement-plan <path>` (the `### Deferred findings` rows are its first task) and stop. |
   | stamp, result **PASS** / **PASS WITH CORRECTIONS** | A revision discards that verdict. Say so and ask before editing anything. |

3. **Owner-only questions first, in one batch.** An Unchecked Claim that needs the owner's word, a `worked` → refused change (contract §3), a tier or scope change and an unruled door are asked once, each with its options and a recommendation, before the design is rewritten around them. Each answer is recorded in the plan as the owner's words with the date (`## Scope` `override:` for a tier or scope answer; the Defect Class `Owner rulings` row for the rest) and repeated in the pull request body: it has no issue comment yet and is lettered at the next `/batch-issues`.
4. **Answer each finding at its class.** Re-measure what the pass measured before building on it (`run`, at a named sha). A finding that asks for a roster is answered by naming the compiler enumeration, the map and its totality assertion with its mutation — never by adding the cell the verifier found, and never by copying a census count into the plan: the count is the test's literal constant (as `ScopeKindRosterTests` keeps its own) and goes stale at the next commit. A finding the revision declines is answered in the plan with its reason, not dropped.
5. **Reset the record.** Delete the stale stamp lines and the whole `## Verification Summary` (a left-over stamp is read by `/implement-plan` as the current verdict). Append ONE `## Revision log` line: `@ <the pass's sha> — revised after the <n>th pass (NEEDS REVISION, Dimension N): <the decisions changed>`. The stamp is gone, so this line is the only carrier of the dimension the next verifier needs for the Convergence rule. A revised draft that had no pass writes `@ <sha> — revised before the first pass: …`.
6. **Check, commit, report.** `python3 build_tools/check_plan_refs.py .claude/plans/<plan-id>.md`, then §3.1 with the revision subject (`process(plan): <plan-id> (<group>) revised after pass <n>`), then the Report (§4), which adds: each finding and how it was answered, the owner's answers recorded, what was re-measured and what was not, and the next step `/verify-plan docs/process/plans/<plan-id>.md` **in a fresh context**. `/implement-plan` stays refused until a new stamp reads PASS or CONVERGED.

## Scope check (before any research)

A plan implements **one non-chore group** of the round charter and lands on **one gate** (contract §11). If the issue list spans more than one group, or bundles a group that changes printed output or emitted layout with anything else, **stop and ask** which group to plan — never bundle. Chores ride along only when they touch no seam the group touches. Record the answer in the plan's `## Scope` block; a deliberate exception is written there as `override: <owner's words>`.

## Tier check (contract §12, before any research)

Find the group's row in the newest charter (`ls -t docs/process/rounds/remediation-round-*-batching.md | head -1`, §1 table and §3 row) and read its **Tier**. Then:
- **Charter says `band`** — stop: a chore has no plan; implement it as one commit and say so.
- **Charter says `light`** and no `--full` was given — write the **light template** (below). The report names `/verify-plan <path>` as the next step; on a light plan it runs its `--adequacy` pass (Dimensions 4-design, 6 and 7 by a fresh reader, nothing else).
- **Charter says `full`** — write the full template; the report names `/verify-plan` as the next step.
- **No charter row** (description input, or an issue batched since the last call) — derive the tier yourself from the §12 letters (a–e), write the letter or "none → light" into `## Scope`, and say in the report that the tier is derived, not ruled.
- **A flag disagrees with the charter** — obey the flag, write `override: <the user's words>` in `## Scope`, and say so in the report. Never silently promote or demote.
- **Research moves a light group onto a §12 letter** (an unruled decision surfaces, the seam turns out to be in `Semantic/`·`Lowering/`·`CodeGen/`, the matrix needs a generator) — stop, say which letter, and ask whether to promote; a promotion is written into `## Scope` as `tier: full (<letter>, promoted at /create-plan: <reason>)`.

**Light template** = the full template with these omissions and nothing else: `## Current State` is one measured paragraph, `## Adversarial Review` keeps only the inert-fix and blast-radius lines, `## Implementation` is **replaced** by `## Acceptance` — the class-level acceptance bullets (matrix/sweep that goes green, guard mutation, allowlist drain, execution evidence) with no phases, tasks or commit messages — and `## Testing Strategy` folds into it. Everything else (`## Scope` with `tier: light`, `## Defect Class`, `## Design Decisions` with rungs, `## Issues to Close` with close criteria, `## Class trackers`) is written in full: those are the decisions an implementer must not re-derive. Run `python3 build_tools/check_plan_refs.py <plan>` before reporting — the light `--adequacy` pass corrects nothing inline, so this is the author's reference check, and the report quotes its result.

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

Any `dotnet` command you run while researching goes through `.claude/scripts/dotnet-serialized` with `dangerouslyDisableSandbox: true` (raw `dotnet` is hook-blocked); prefer `/spy-run`, `/spy-emit`, `/quick-check`.

### 3. Generate the plan

Write a plan file to `.claude/plans/` with a random name (use `openssl rand -hex 3` for a short hex suffix, e.g., `plan-a1b2c3.md`; the stem is the plan-id, and the plan's file of record is `docs/process/plans/plan-a1b2c3.md`). §3.1 commits the draft there once it is written.

The plan must follow this structure. `## Defect Class` and `## Adversarial Review` are **mandatory when any input is a bug, ICE, or regression issue**; omit them (with the line "Defect Class: N/A — feature work") only for pure feature work.

```markdown
# <Plan Title>

## Context

<What problem this solves, why it matters, links to GitHub issues>

## Current State

<What exists today, what's broken or missing — measured @ <sha>>

## Scope

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
- **Sibling cells this plan must also cover:** <enumerated matrix — the issue repro list is a symptom report. When the plan IS the generator, this is NOT a cell list: name each axis as the image of a compiler enumeration (an enum, a switch's arms, a roster test's census) under an explicit map, the totality assertion that keeps the map closed (and its mutation), the excluded axes with reasons, and the verdict as a RULE keyed on axis-value classes; hand-measured cells go in one "measured at base" table as the red floor, never as matrix rows — a cell a later reader finds by hand means a roster is open, and the revision closes the roster, never adds the cell>
- **Lane / reach:** <lane · program|probe>; **fidelity-scope row:** <COMPARED row | promoted by R-xx>
- **Standing harness that should have caught it:** <name> — or "none; Phase N adds one"
- **Generator:** <existing differential/generative harness the cells join | REQUIRED FIRST — the matrix has ≥2 axes × ≥3 values or the group is a residue (P11b, P14c): Phase 1 builds the generator and its allowlist ratchet and the fix drains it | not required: <the axis a generator cannot enumerate>>
- **Owner rulings:** <issue-comment links; authoritative, do not re-ask>

## Adversarial Review (pre-mortem)

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

1. **<Task title>** — <file(s) involved>
   - <Specific change description>
   - <Acceptance criteria — which bullet above this task satisfies>
   - Commit: `<conventional commit message>`

2. ...

### Phase N+1: ...

## Testing Strategy

- <Generator first (contract §11): the differential/generative harness over the matrix and its allowlist, or the axis it cannot enumerate and the hand matrix that covers only that axis>
- <New test fixtures needed (.spy + .expected/.error)> — for every negative fixture, the positive control that must keep passing
- <Edge cases to cover — cells outside the issues' repro lists; change axis when the spellings you vary all agree>
- <Outputs that discriminate — an example that prints the same thing with the bug present proves nothing>
- <For language changes: the spec section in docs/language_specification/ and its executed examples — a deliverable of equal weight to the code>
- <Blast radius: which sweeps and test projects the change reaches, and when regen of generated artifacts runs (early, not pre-push)>

## Issues to Close

- #NNN — <title> — closed by Phase N, Task M; **close criterion:** <the acceptance bullet and the evidence it needs @ sha>
- ...

## Class trackers this plan may post cells to

- #NNN — <class> — cells found outside this plan's contract go here as comments (contract §10); the lead files a NEW tracker only for a class none of these covers. No implementer runs `gh issue create`.
```

**Plan quality requirements:**
- Tasks follow the feature implementation order: Lexer -> Parser -> Semantic -> Validation -> Lowering (if an IR shape changes) -> CodeGen -> LSP -> Tests
- Each task has a specific conventional commit message
- Enough context and rationale for a junior/senior engineer (or a smaller model) to implement unambiguously
- Incremental commits — each task is independently committable
- A plan that fixes one arm of a mirrored/parallel-site structure without a completeness scan is not a plan
- A plan with a second non-chore group, or an output/layout-changing phase sharing a gate, is not a plan (contract §11) — split it
- A residue group or a ≥2-axis × ≥3-value matrix without a generator-first phase is not a plan (contract §11) — hand matrices found the next axis six rounds running; and a generator plan whose axes are hand LISTS rather than rosters closed against a compiler enumeration is a hand matrix wearing a generator (eight adequacy passes on plan-ed89b6 each found one more value of one listed axis)
- The plan names the class trackers it posts cells to and never instructs an implementer to `gh issue create` (contract §10)
- A design decision that adds a name-keyed (`BuiltinNames.X`, type-name string) rule in `Semantic/` or `CodeGen/` without a rung justification is not a plan (layer ladder, CLAUDE.md › Core & Stdlib Conventions)
- Every new test/guard/harness comes with the mutation that turns it red (verification-contract.md §2)
- The plan names which generated artifacts it touches and schedules their regeneration early (§7), and names its blast radius (CLAUDE.md › Testing › Commit gate)
- GitHub issues referenced and mapped to closing tasks, each with its close criterion
- No hard-coded model names or commit trailers — implementers use the trailer the harness provides

### 3.1. Commit the draft and open the pull request

A draft is committed development history from the moment it is written (owner, 2026-11-10; `docs/process/README.md` › The stage trail): the gitignored working copy does not survive its container, and the next session needs something to verify or revise. Run `python3 build_tools/check_plan_refs.py .claude/plans/<plan-id>.md` first and fix what it flags, then:

```bash
git fetch origin dev
git switch -c claude/<group>-plan origin/dev     # e.g. claude/p33-plan; the lane exists with an open PR → `git switch` to it;
                                                 # its PR has merged → `git switch -C claude/<group>-plan origin/dev`
mkdir -p docs/process/plans
{ echo '<!-- DRAFT, not verified: written by /create-plan on YYYY-MM-DD @ <sha>; no /verify-plan pass has stamped this text. -->'
  echo
  cat .claude/plans/<plan-id>.md
} > docs/process/plans/<plan-id>.md
git add docs/process/plans/<plan-id>.md           # explicit pathspec, never -A or .
git diff --cached --stat                          # exactly this one file
git commit -m "process(plan): <plan-id> (<group>) draft"
```

The DRAFT marker lives only in the committed copy; the working copy never carries it, so `/verify-plan` can stamp the working copy's first line. A **revision** (`revise <plan>`, **Revising a plan** above) commits the same way — the revising session has already deleted the stale stamp and Verification Summary, so the marker is written again, saying `revised by /create-plan on YYYY-MM-DD @ <sha> after pass <n>` in place of `written by …` — with the subject `process(plan): <plan-id> (<group>) revised after pass <n>`.

Then `/push` and open the pull request into `dev` unless the lane already has an open one (the push updates it): title = the commit subject; body = the group and tier, the issues, the scope line, the `check_plan_refs.py` result, and the next step `/verify-plan docs/process/plans/<plan-id>.md`. Never commit the charter, another plan or any other file alongside. Merging is the owner's.

### 4. Report

Tell the user:
- The committed path (`docs/process/plans/<plan-id>.md`), its commit and the pull request's URL; the working copy stays at `.claude/plans/<plan-id>.md`
- A brief summary of phases and task count
- For bug/ICE inputs: the class, the matrix size, and the standing harness named (or the phase that adds one)
- The scope line: group, **tier with its letter and its source (charter / derived / override)**, alone?, gates, generator (existing / built first / not required), and the trackers named
- The next step **by tier** (contract §12): `full` → `/verify-plan <path>` (nine dimensions) before `/implement-plan <path>`; `light` → `/verify-plan <path>` (runs `--adequacy`: matrix arms, guard mutations, design predicates — by a fresh reader) before `/implement-plan <path>`, quoting the `check_plan_refs.py` result
