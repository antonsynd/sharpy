---
name: create-plan
description: Create an implementation plan from GitHub issues or a description
argument-hint: "<issue numbers or description>"
---

Create a detailed implementation plan with context, rationale, and tasks for an engineer to follow. Saves the plan as a markdown file in `~/.claude/plans/`.

**Usage:**
- `/create-plan 222,223,224,225,226` — read GitHub issues and create a plan
- `/create-plan fix the 5 bugs from the audit` — create a plan from a description
- `/create-plan` — ask the user what to plan

## Argument Handling

Parse `$ARGUMENTS`:
- If it looks like comma-separated numbers (e.g., `222,223,224`), treat them as GitHub issue numbers
- If it looks like a description, use it as the planning goal
- If empty, ask the user what they want to plan

## Steps

### 1. Gather context

**If GitHub issues were specified:**
- Read each issue via `gh issue view <number>`
- Read comments on each issue via `gh api repos/antonsynd/sharpy/issues/<number>/comments`
- Understand the full scope across all issues

**If a description was provided:**
- Research the relevant codebase areas using Glob, Grep, and Read
- Identify the files and components involved

### 2. Research the codebase

Before writing the plan:
- Read the relevant source files to understand current state
- Check `docs/language_specification/` for any applicable specs
- Check existing tests in `src/Sharpy.Compiler.Tests/` and `src/Sharpy.Core.Tests/`
- Verify Python behavior with `python3 -c "..."` where applicable
- Check for related GitHub issues with `gh issue list --search "..."`

### 3. Generate the plan

Write a plan file to `~/.claude/plans/` with a random name (use `openssl rand -hex 3` for a short hex suffix, e.g., `plan-a1b2c3.md`).

The plan must follow this structure:

```markdown
# <Plan Title>

## Context

<What problem this solves, why it matters, links to GitHub issues>

## Current State

<What exists today, what's broken or missing>

## Design Decisions

<Key architectural choices with rationale. Reference Sharpy axioms where relevant:
- Axiom 1 (.NET compatibility) > Axiom 3 (Type safety) > Axiom 2 (Python syntax)
- Reference docs/language_specification/ where applicable>

## Implementation

### Phase N: <Phase Name>

**Goal:** <What this phase achieves>

#### Tasks

1. **<Task title>** — <file(s) involved>
   - <Specific change description>
   - <Acceptance criteria>
   - Commit: `<conventional commit message>`

2. ...

### Phase N+1: ...

## Testing Strategy

- <New test fixtures needed (.spy + .expected/.error)>
- <Edge cases to cover>
- <Negative test cases>

## Issues to Close

- #NNN — <title> (closed by Phase N, Task M)
- ...
```

**Plan quality requirements:**
- Tasks follow the feature implementation order: Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
- Each task has a specific conventional commit message
- Enough context and rationale for a junior/senior engineer (or Claude Sonnet) to implement unambiguously
- Incremental commits — each task is independently committable
- Root-cause fixes, not patches/bandaids
- GitHub issues referenced and mapped to closing tasks

### 4. Report

Tell the user:
- The plan file path
- A brief summary of phases and task count
- Suggest running `/verify-plan <path>` before `/implement-plan <path>`
