---
name: create-plan
description: Create an implementation plan from GitHub issues or a description
argument-hint: "<issue numbers or description>"
---

Read `docs/design/verification-contract.md` before proceeding (§10 file-by-class and §11 one-group-per-plan included); every section below applies it. Read `docs/design/python-fidelity-scope.md` when any input is a Python-parity cell.

Create a detailed implementation plan with context, rationale, and tasks for an engineer to follow. Saves the plan as a markdown file in `.claude/plans/` (repo-local, gitignored).

**Usage:**
- `/create-plan 222,223,224,225,226` — read GitHub issues and create a plan
- `/create-plan fix the 5 bugs from the audit` — create a plan from a description
- `/create-plan` — ask the user what to plan

## Argument Handling

Parse `$ARGUMENTS`:
- If it looks like comma-separated numbers (e.g., `222,223,224`), treat them as GitHub issue numbers
- If it looks like a description, use it as the planning goal
- If empty, ask the user what they want to plan. If they want to extend or re-plan an existing plan, list the three newest across **both** plan directories and ask which one — never silently pick one:
  ```bash
  ls -t .claude/plans/*.md 2>/dev/null | head -3      # current location (round plans and plans created since 2026-08-26)
  ls -t ~/.claude/plans/*.md 2>/dev/null | head -3    # per-batch plans created before 2026-08-26
  ```

## Scope check (before any research)

A plan implements **one non-chore group** of the round charter and lands on **one gate** (contract §11). If the issue list spans more than one group, or bundles a group that changes printed output or emitted layout with anything else, **stop and ask** which group to plan — never bundle. Chores ride along only when they touch no seam the group touches. Record the answer in the plan's `## Scope` block; a deliberate exception is written there as `override: <owner's words>`.

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

Write a plan file to `.claude/plans/` with a random name (use `openssl rand -hex 3` for a short hex suffix, e.g., `plan-a1b2c3.md`).

The plan must follow this structure. `## Defect Class` and `## Adversarial Review` are **mandatory when any input is a bug, ICE, or regression issue**; omit them (with the line "Defect Class: N/A — feature work") only for pure feature work.

```markdown
# <Plan Title>

## Context

<What problem this solves, why it matters, links to GitHub issues>

## Current State

<What exists today, what's broken or missing — measured @ <sha>>

## Scope

- **Group:** <P<n> — name> (one non-chore group; chores riding along: <none | #… — seam-free because …>)
- **Alone:** <yes — changes printed output / emitted layout | no>
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** <none | the owner's words>

## Defect Class

- **Violated contract:** <one sentence — what is supposed to be uniform across which axis>
- **Meta-class:** <from the standard-cure table, verification-contract.md §1> → **standard cure:** <seam / sweep / harness>
- **Known member cells:** #NNN … (the issues)
- **Sibling cells this plan must also cover:** <enumerated matrix — the issue repro list is a symptom report>
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
- A residue group or a ≥2-axis × ≥3-value matrix without a generator-first phase is not a plan (contract §11) — hand matrices found the next axis six rounds running
- The plan names the class trackers it posts cells to and never instructs an implementer to `gh issue create` (contract §10)
- A design decision that adds a name-keyed (`BuiltinNames.X`, type-name string) rule in `Semantic/` or `CodeGen/` without a rung justification is not a plan (layer ladder, CLAUDE.md › Core & Stdlib Conventions)
- Every new test/guard/harness comes with the mutation that turns it red (verification-contract.md §2)
- The plan names which generated artifacts it touches and schedules their regeneration early (§7), and names its blast radius (CLAUDE.md › Testing › Commit gate)
- GitHub issues referenced and mapped to closing tasks, each with its close criterion
- No hard-coded model names or commit trailers — implementers use the trailer the harness provides

### 4. Report

Tell the user:
- The plan file path
- A brief summary of phases and task count
- For bug/ICE inputs: the class, the matrix size, and the standing harness named (or the phase that adds one)
- The scope line: group, alone?, gates, generator (existing / built first / not required), and the trackers named
- Suggest running `/verify-plan <path>` before `/implement-plan <path>` — `/verify-plan` grades adequacy (class vs cell), not just accuracy
