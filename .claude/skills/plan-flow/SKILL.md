---
name: plan-flow
description: Run /create-plan and /verify-plan as one flow, with this session as the orchestrator and each stage in a fresh agent
argument-hint: "<issue numbers or description> [--light | --full] | resume <plan path or id>"
---

Read `docs/design/verification-contract.md` (§10–§12) and `docs/process/README.md` › The stage trail before proceeding. This skill adds no rule to `/create-plan` or `/verify-plan`: it says who runs them and what passes between them. Where it and a stage's own `SKILL.md` disagree about that stage, the stage's skill wins.

Take a group from its issues to a plan `/implement-plan` will accept: draft, verify, and revise until the stamp reads PASS, PASS WITH CORRECTIONS or CONVERGED. It ends there. Implementation is a separate session (`/implement-plan <path>`), and so is `/verify-implementation`.

**Usage:**
- `/plan-flow 222,223,224` (or a description, with `--light` / `--full` as `/create-plan` takes them): start a new plan
- `/plan-flow resume plan-a1b2c3`: pick an existing plan up at whatever its first line says (the table under **The loop**)
- `/plan-flow`: ask what to plan

## The orchestrator's job

You, the invoking session, are the orchestrator. You do not write the plan, grade it, or revise it: every stage runs in an agent you spawn, so each read is fresh (`/verify-plan` Output §5: the verifier that stamped a plan does not revise it, and the next pass is a fresh read). You hold what the agents cannot: the owner's rulings, the thread between stages, and the outward actions.

Yours alone, never an agent's:

- asking the owner, and recording each answer (below);
- everything posted on GitHub: issues, comments, the pull request's title and body, a CI re-run;
- filing cells found outside the plan's contract, by class (contract §10): agents report them, you de-duplicate against the open trackers and file once per stage;
- checking each stage's result against the repository before you believe it or relay it.

## The loop

Sync first (`git fetch origin`), then read the plan's first line to find where it stands. A new plan starts at the first row.

| The plan's first line | Next stage | Agent's instruction |
|---|---|---|
| no plan yet | draft | follow `.claude/skills/create-plan/SKILL.md` for `<the arguments>` |
| `<!-- DRAFT, not verified …` | verify | follow `.claude/skills/verify-plan/SKILL.md` for `<path>` |
| stamp, **NEEDS REVISION** | ask the owner what is owed (below), then revise | follow `create-plan`'s **Revising a plan** for `revise <path>` |
| stamp, **PASS**, **PASS WITH CORRECTIONS** or **CONVERGED** | finish (below) | none |

A revision returns the plan to DRAFT, so the loop is draft → verify → (revise → verify)\* and it stops on a decision, not on a count (contract §12 Convergence).

**The pass cap.** Two verify passes (R-LW). Before spawning a third or later pass, ask the owner once. A pass beyond the cap runs only on the owner's words, recorded as below and written into the plan's `## Scope` as `Extra verify pass <n>: owner, <date> "<words>"` by the revising agent, or by the verifier when no revision came between.

**Owner-only questions, once per stage, in one batch.** After each stage, collect what only the owner can answer: a NEEDS REVISION finding that is a decision (a hand-listed axis to close or rule out, a change that turns working code into a refusal, a tier or scope change, an unruled door, a scorecard baseline raise) and every Unchecked Claim that needs the owner's word. Ask them together, each with its options and your recommendation, before the revision starts. Do not start a revision around a question you have not asked.

**Recording a ruling.** Before the next agent starts, post the owner's exact words as a comment on the plan's class tracker, with the date and what the ruling decides, and give the agent the comment's id to read. A quote that exists only in your brief has no source the next verifier can check. A scope change the owner makes mid-flow (a decision moved to another tracker, a group split) is recorded the same way and is the authority to revise a stamped plan.

## Briefing an agent

Spawn one `general-purpose` agent per stage, in the background, with no model override unless the user asks for one. Each brief is the agent's whole context. It carries:

- the stage's `SKILL.md` to read and follow, the plan path, the branch and its tip sha;
- the owner rulings that apply, as comment ids to read, never as your paraphrase;
- for a verifier, the previous agent's notable claims **listed as claims to grade, not as facts**, with what it said it measured and what it said it inferred. Hand over the code and the question, not your conclusion: an agent given a hypothesis tends to return it confirmed;
- the environment rules from CLAUDE.md the agent will otherwise trip on: `.claude/scripts/dotnet-serialized` for every dotnet call, the cloud-session prefixes, `run` not `emit` for a crash claim, the CPython oracle version;
- the shared-tree contract (contract §9): a temporary mutation is copied aside first and restored with `cp` and checked with `cmp`; never `git checkout`, `restore`, `clean`, `stash`, `reset` or `rm` on repo paths; never `--no-build` after an edit; scratch files in a named scratchpad directory;
- its limits: which files it may commit, the branch it pushes, and that it opens no pull request, posts nothing on GitHub, files no issue and merges nothing. What its skill would have it post, it lists as owed;
- what to report: the verdict or result, each finding with its measurement (command, sha, output), what it measured against what it inferred, what it did not run, the sha it pushed and `git status --short`.

One agent holds the working tree at a time. While it runs, do not switch branches, edit tracked files or run dotnet in that tree; work that cannot wait goes in a `git worktree`. Uncommitted changes that appear during a run are the agent's mutation in flight: never commit them, whatever a hook asks.

## After each stage

Check before you relay. An agent's mistakes arrive in the same confident register as its findings.

1. `git status --short` is empty and the branch equals its remote.
2. The plan's first line matches the result the agent reported.
3. `git diff --name-only origin/dev...HEAD` lists only the plan file and, for a `full` generator group, its stage-0 test files (contract §11). A product path there is a stage that went wrong: stop and report it.
4. File the out-of-contract cells it reported (contract §10), and point any exclusion or allowlist citation that borrowed an issue number at the right tracker in the next agent's brief.
5. Tell the user the result in a few lines: the verdict, what was measured and what was only inferred, what is owed, and what you are starting next. Say whose claim each figure is.

## The pull request

A `full` plan has one plan pull request into `dev` for all its stages (stage trail item 3); a `light` plan has none and rides in its implementation's. After the first stage of a `full` plan, make sure the pull request exists, and after every stage bring its title and body up to date with what the branch now holds: the current stamp and sha, the rulings, the stage-0 commits, the issues filed and the cells posted, what is owed. A title left from an earlier pass misleads whoever reads it next.

When stage-0 commits ride the pull request it changes code, so it needs the independent review before it merges (stage trail items 4 and 6; `/implement-plan` › Cleanup and Report step 4). Request it once the stamp is PASS, PASS WITH CORRECTIONS or CONVERGED: one reviewer that wrote none of the code, given the diff and CPython and never the plan. Write its line into the body when it returns.

Read the pull request's CI at its tip. A red that is not the plan's (red on the base, or the previous tip was green and the diff between them cannot cause it) is recorded on its tracker and re-run once; any other red is fixed.

Merge as stage trail item 6 says, and only when the session's own instructions allow a merge without the owner's ask. Otherwise bring the pull request to green with a current review line and stop for the owner's word.

## Finishing

Report: the plan path and its stamp line; the passes used and each owner ruling with its comment link; the stage-0 sha and counts, if any; the issues filed and cells posted; the pull request and its state (checks, review line); what the last verifier did not run; the deferred findings that become the implementation's first task. The next step is `/implement-plan <path>` **in a new session**: it is long, runs its own agents and needs the working tree to itself.
