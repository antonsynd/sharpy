---
name: create-plan
description: Create an implementation plan from GitHub issues or a description
argument-hint: "<issue numbers or description>"
---

Read `docs/design/verification-contract.md` before proceeding; every section below applies it.

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

## Steps

### 1. Gather context

**If GitHub issues were specified:**
- Read each issue via `gh issue view <number>` (`dangerouslyDisableSandbox: true` — `gh` fails TLS verification in the sandbox)
- Read comments on each issue via `gh api repos/antonsynd/sharpy/issues/<number>/comments` — an owner ruling in a comment is authoritative; record the link, do not re-ask
- Understand the full scope across all issues
- If any input is a bug, ICE, or regression: the issue's repro list is a **symptom report, not a test plan**. Identify the class it belongs to (verification-contract.md §1) before designing anything

**If a description was provided:**
- Research the relevant codebase areas using Glob, Grep, and Read
- Identify the files and components involved

### 2. Research the codebase

Before writing the plan:
- Read the relevant source files to understand current state
- Check `docs/language_specification/` for any applicable specs (spec is authoritative — the plan changes the implementation to match, never the reverse)
- Check existing tests in `src/Sharpy.Compiler.Tests/` and `src/Sharpy.Core.Tests/`, and the standing class harnesses in `docs/design/gap-discovery-contracts.md` — which one *should* have caught this?
- Verify Python behavior with `python3 -c "..."` where applicable
- Check for related GitHub issues with `gh issue list --search "..."` — sibling cells of the same class are usually already filed
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

## Defect Class

- **Violated contract:** <one sentence — what is supposed to be uniform across which axis>
- **Meta-class:** <from the standard-cure table, verification-contract.md §1> → **standard cure:** <seam / sweep / harness>
- **Known member cells:** #NNN … (the issues)
- **Sibling cells this plan must also cover:** <enumerated matrix — the issue repro list is a symptom report>
- **Standing harness that should have caught it:** <name> — or "none; Phase N adds one"
- **Owner rulings:** <issue-comment links; authoritative, do not re-ask>

## Adversarial Review (pre-mortem)

- **Alternative root cause:** <what else explains the symptom; the probe that discriminates>
- **How the fix could be inert:** <fallback path one call later? decision duplicated in a sibling arm?>
- **Before/after by direction:** <the probe that distinguishes "ICE → diagnostic" from "restricts working code">
- **Blast radius:** <Stdlib.Tests / Cli.Tests / LSP parity / interop / metamorphic / differential / warm-cold>
- **Generated artifacts touched:** <spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger — regen early>

## Design Decisions

<Key architectural choices with rationale. Reference Sharpy axioms where relevant:
- Axiom 1 (.NET compatibility) > Axiom 2 (Type safety) > Axiom 3 (Python syntax)
- Reference docs/language_specification/ where applicable
- Every fact codegen reads is materialized in semantic analysis (Symbol.CodeGenInfo or a node-keyed SemanticInfo dictionary added to MergeFrom)>

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

- <New test fixtures needed (.spy + .expected/.error)> — for every negative fixture, the positive control that must keep passing
- <Edge cases to cover — cells outside the issues' repro lists; change axis when the spellings you vary all agree>
- <Outputs that discriminate — an example that prints the same thing with the bug present proves nothing>
- <For language changes: the spec section in docs/language_specification/ and its executed examples — a deliverable of equal weight to the code>
- <Blast radius: which sweeps and test projects the change reaches, and when regen of generated artifacts runs (early, not pre-push)>

## Issues to Close

- #NNN — <title> — closed by Phase N, Task M; **close criterion:** <the acceptance bullet and the evidence it needs @ sha>
- ...
```

**Plan quality requirements:**
- Tasks follow the feature implementation order: Lexer -> Parser -> Semantic -> Validation -> Lowering (if an IR shape changes) -> CodeGen -> LSP -> Tests
- Each task has a specific conventional commit message
- Enough context and rationale for a junior/senior engineer (or a smaller model) to implement unambiguously
- Incremental commits — each task is independently committable
- A plan that fixes one arm of a mirrored/parallel-site structure without a completeness scan is not a plan
- Every new test/guard/harness comes with the mutation that turns it red (verification-contract.md §2)
- The plan names which generated artifacts it touches and schedules their regeneration early (§7), and names its blast radius (CLAUDE.md › Testing › Commit gate)
- GitHub issues referenced and mapped to closing tasks, each with its close criterion
- No hard-coded model names or commit trailers — implementers use the trailer the harness provides

### 4. Report

Tell the user:
- The plan file path
- A brief summary of phases and task count
- For bug/ICE inputs: the class, the matrix size, and the standing harness named (or the phase that adds one)
- Suggest running `/verify-plan <path>` before `/implement-plan <path>` — `/verify-plan` grades adequacy (class vs cell), not just accuracy
