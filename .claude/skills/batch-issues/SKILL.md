---
name: batch-issues
description: Read every open GitHub issue with comments, batch them into class-level remediation groups, surface the open-ended decisions with options and door-type, then (after the owner rules) write the round charter, post placements and rulings, and file/close what the rulings create
argument-hint: "[--charter <path>] [--since <sha>] [--issues 123,456]"
---

Read `docs/design/verification-contract.md` §1 (class contract, standard cures) before proceeding. This skill runs in **two owner turns**: an analysis turn that ends in a decision list, and an application turn after the owner rules. Never post to GitHub or write the charter before the owner has ruled.

**Usage:**
- `/batch-issues` — read all open issues, batch, present groups + per-issue recommendations + decisions
- `/batch-issues --issues 1946,1947` — batch only these into the CURRENT round's charter (incremental placement after a verify round files new issues)
- `/batch-issues --charter .claude/plans/remediation-round-YYYY-MM-DD-batching.md` — extend an existing charter instead of starting a new one

The owner replies with rulings ("D1 a, D2 a-then-c, …"); the application turn then follows §"Turn 2".

## Turn 1 — analysis (no side effects)

### 1. Pin the state
- `git rev-parse --short HEAD`, `git status --porcelain` — record the sha; batching is done on a clean tree at HEAD == origin/dev.
- Find the newest charter: `ls -t .claude/plans/remediation-round-*-batching.md | head -1`. Read its §1 order, §2 done ledger, §3.10 parked list, §4 rulings and §6 cross-cutting notes. **Standing rulings are never re-asked**; the ruling sequence (R-A, R-B, …) continues from the last letter used.
- Read the memory index for "don't re-ask" / "don't re-propose" entries (parked designs, rejected alternatives).

### 2. Read every open issue in full
Run `gh` with `dangerouslyDisableSandbox: true` (TLS fails in the sandbox). Dump bodies + comments to the scratchpad and read the dump end to end — comments carry rulings, sibling cells and escalations that change placement:

```bash
S=<scratchpad>; mkdir -p $S/issues
for n in $(gh issue list --repo antonsynd/sharpy --state open --limit 200 --json number --jq '.[].number'); do
  gh issue view $n --repo antonsynd/sharpy --json number,title,body,labels,comments,createdAt > $S/issues/issue-$n.json
done
for f in $S/issues/issue-*.json; do jq -r '"=== #\(.number) \(.title)\nLABELS: \([.labels[].name]|join(","))\n\(.body)\n" + ([.comments[] | "--- COMMENT by \(.author.login) \(.createdAt[:10]):\n\(.body)\n"] | join("\n"))' $f; done > $S/all-issues.txt
```
Read `all-issues.txt` in ≤450-line chunks with the Read tool (a single `cat` over 60 KB gets persisted out of context).

### 3. Anchor every recommendation on a real seam (grep, not dotnet)
For each non-parked issue, confirm by grep that the seam the issue names exists and note the file:line; where the recommendation hinges on a design fact (is a merge a feature? is there an error sentinel? what does a lowering switch fall through to?) read the ~40 lines around it. **No `dotnet build`/`test` during batching** — if a premise is doubtful, probe with the built binary (`src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run`, lock-free) and record `measured @ <sha>`; otherwise the plan author measures. Verify Python behavior with `python3 -c` for any Python-semantics claim you make.

### 4. Classify
1. **Duplicates and fold-ins** — same cell filed twice → close the later as duplicate; a multi-cell issue whose cells belong to other open issues → post each cell on its home and close the multi-cell issue; a cell of a broader open issue → fold and close. One open issue per cell.
2. **Class groups** — name the violated contract ("every X reaches the same verdict in every position Y") and the cell matrix (axes × values), the standing harness that should have caught it (or "none; the plan adds one"), and the cure seam. An issue's repro list is a symptom report; the group's matrix is the deliverable. Prefer extending an existing matrix test over forking one.
3. **Order** — silent-wrong first, then ICEs on common shapes, then refusal parity, then vocabulary; Core-only groups early (no compiler blast radius); groups that turn silent acceptance into refusals late (own matrix, measured gate). Record hard dependencies (who builds the carrier/helper another group reads).
4. **Decisions** — anything where two readings lead to materially different work: policy (refuse vs rename vs relax), diagnostic identity (reuse vs new code), semantics (Python fidelity vs .NET), infrastructure (a new sentinel/switch). Each decision gets a **D-number**.
5. **Parked** — the predecessor's parked list is carried unchanged unless an issue's own comments promote it.

### 5. Present (message 1)
Three headers at most; bullets, not paragraphs:
- **Groups table** — order, group name, issues, one-line why-here; order rationale and hard dependencies below it.
- **Per-issue notes** — grouped; one or two sentences each: what it is + the class-level recommendation (seam, matrix, guard). Name duplicates/fold-ins to action.
- **Decisions** — numbered D1…Dn, one line each: the question, the options, your recommendation. Offer the page as an artifact in one line if the write-up exceeds ~150 lines.

Stop. Do not apply anything.

### 6. Decision detail (message 2, when asked)
For each D-number, in this order:
- **Repro or internals** — a fenced minimal source + emitted C#/diagnostic, or the file:line and the ~3-line mechanism for compiler-internals decisions. Say how common the user-facing shape is.
- **Options** — lettered; each with its consequence, including interop-surface and snapshot effects.
- **Stacking / doors** — say explicitly which options stack (a now, c later) and which are one-way (a refusal can always be relaxed later; a rename/lowering that users or C# consumers can depend on cannot be taken back; a runtime-value change is one-way). "Two-way door" / "one-way direction" wording.
- **Recommendation** — one option, one sentence of why. If reading the code changed an earlier recommendation, say so and why.

Stop again. Wait for the owner's rulings.

## Turn 2 — application (after the owner rules)

Apply in this order; every step is idempotent and reported.

1. **Assign ruling codes** continuing the predecessor's sequence (R-AV → R-AW …), one per decision, in D-number order.
2. **File the tracking issues the rulings create** (a new phase, a new cell the ruling exposes, a design the owner scheduled "next"): `gh issue create --body-file`; body = Class / Ruling / Acceptance / charter pointer. Capture the numbers before writing the charter.
3. **Write the charter** from `charter-template.md` (this directory) to `.claude/plans/remediation-round-<date>-batching.md` (gitignored). When extending a charter, add the new groups/rulings/changelog entry and update the census; never delete a ruling — supersede it with a strike-through and a pointer. Census line = `N open = P parked + B batched`, and it must reconcile with `gh issue list … | jq length` after the closures.
4. **Post one comment per batched issue** (`comment-templates.md`): header with date + sha + charter path + §, group + order, the class contract in one sentence, the cure seam/matrix, and — on the decision's issue — the ruling text with the rejected alternatives. Write bodies to files, post in one loop, report `posted=N failed=[…]`.
5. **Close duplicates/fold-ins** with `gh issue close --reason "not planned" --comment` naming the home issue; resolved design notes close with `--reason completed`.
6. **Recount**: `gh issue list --state open | jq length` must equal the charter's census; fix the charter if not.
7. **Memory**: write a `project_issue_batching_<date>.md` memory (state, rulings with "do not re-ask", order, next step) and add its index line; add a `feedback_` memory only for a NEW owner preference about the flow itself.
8. **Do not commit.** The charter is gitignored; nothing in the tree changes. If skill files changed, say so and leave the commit to the owner.

### Recap (final message)
Lead with the census and the order; list the rulings by code in one line each; list what was filed and closed; name the next step (`/create-plan` on the first group, or chores first). No closing offer.

## Guardrails
- A "closed" issue with a pinned wrong-behaviour test is not finished — check `KnownRed`/N/A rosters citing it before closing anything.
- Never recommend a spot fix for one cell; if the class is unclear, say so and make the matrix the plan's first task.
- Never re-litigate a parked design or a rejected alternative recorded in a ruling; cite the ruling instead.
- Rulings are the owner's words; quote the chosen option and the rejected ones, do not paraphrase the sentiment.
- Everything measured carries `@ <sha>`; everything grepped says "grep-only".
