# Process records

Round charters and the plans that implemented them. These are the design-of-record for *why* the
tracker looks the way it does: issue bodies cite plan IDs (`P77a`), execution rows (`6c⁶⁷″`) and
ruling letters (`R-LK`) that resolve only here. They are excluded from the published docs site
(`mkdocs.yml` `exclude_docs`) and are not gated by `check_spec_blocks.sh`.

Policy (owner, 2026-11-06):

- `rounds/<charter>.md` is the live charter for a remediation round. It is edited in place at every
  batching call and committed with that call; git history replaces the per-call backup files the
  charter used to name. A condensation is a commit. The charter and `rounds/index.md` are the record
  of placements and rulings; neither is posted as an issue comment (owner, 2026-11-11).
- A plan is committed at every stage, on one branch with one pull request (owner, 2026-11-11; "The
  stage trail" below): a draft or a NEEDS REVISION pass that lived only in the gitignored
  `.claude/plans/` directory was lost with its container.
- A generated one-page index per batching call (group → issues → tier → execution position → current
  rulings with supersession chains resolved) is `rounds/index.md`, written by
  `python3 -m build_tools.process_index --write` and committed with the charter (CI runs `--check`).

## The stage trail

The shared rules for `/create-plan`, `/verify-plan`, `/implement-plan` and `/verify-implementation`
(owner, 2026-11-11):

1. **File of record.** `docs/process/plans/<plan-id>.md`, from the first draft on. Line 1 is the
   `<!-- DRAFT, not verified … -->` marker or a `/verify-plan` stamp; `/implement-plan` reads it and
   refuses a DRAFT or a NEEDS REVISION.
2. **Working copy.** `.claude/plans/<plan-id>.md` (gitignored) is edited inside one session and copied
   over the file of record at each commit. With no working copy (a new container), copy the file of
   record back and drop the DRAFT marker; if both exist and differ beyond it, ask which is current.
3. **One branch, one pull request per plan.** Each stage (the draft, each verify pass, each revision)
   is a commit on the plan's branch, cut once from `origin/dev`, pushed after every commit. A `full`
   plan's stages share `claude/<group>-plan` and one pull request into `dev`; the implementation then
   lands on `claude/<group>` with its own. A `light` plan has no plan pull request: its stages are
   commits on `claude/<group>` and ride in the implementation's pull request. `/verify-implementation`
   commits onto the open implementation pull request; only when that has merged does its record go on
   a fresh `claude/<group>-verify` lane. Never stack on merged history.
4. **Commit.** Explicit pathspecs, `git diff --cached --stat` first, a subject of at most 72
   characters, the trailer the harness provides. A plan-stage commit stages the one plan file; a
   `full` generator group's stage-0 commits (contract §11) also ride the plan pull request and make
   it one that changes code: the session that opened it requests the independent review
   (`/implement-plan` › Cleanup and Report step 4), writes its line in the body, and merges (item 6).
5. **Pull request.** `/push`, then `gh pr create --base dev` (in a cloud session, the GitHub MCP
   `create_pull_request` tool or `gh api -X POST repos/<owner>/<repo>/pulls`). An implementation
   pull request's body names the plan, the gate `passed/failed/skipped @ sha (measured)`, **the issues
   it closes and the issues its work filed**, and its independent review line
   (`/implement-plan` › Cleanup and Report step 4). A cell found outside the plan's scope is filed
   as contract §10 says. The tip never carries the skip-ci marker (contract §6).
6. **Merge on green** (owner, 2026-11-11). The session that opened a pull request into `dev` merges
   it once every check at its tip has completed green (where path filters start no gate, the subject
   check alone) and, if it changes code, its `Independent review @ <sha>` line is current and its
   verdict is not `do not merge` (`/implement-plan` › Cleanup and Report step 4). It changes code when
   its diff touches `src/`, `build_tools/`, `.claude/scripts/`, `.claude/hooks/`, `.github/` or
   `docs/language_specification/`; any other pull request needs green alone. Green never stands in
   for a required review. Merge with a merge commit, the head sha the gate measured as the expected
   head, so a later push cannot ride in unmeasured. A red or a pending check is never merged over:
   the red is fixed, or — a flake with an open issue — its job is re-run once and the re-run decides.
   After the merge, `git pull origin dev`. `mainline` stays the owner's.

### No-plan lane

An issue whose charter tier is `band`, or that has no charter row and earns no §12 letter (a)–(e),
whose behaviour has an owner ruling (or needs none) and whose fix is contained, goes straight to a
pull request with a generated or differential test and its mutation record (Rule 12): no plan file,
no verify stage, the body and the independent review as in item 5, with `Refs #N`. A `light` or `full` group takes the plan path
(contract §12: no tier is lowered). After the merge, `/close-issues` closes the issue on evidence.

### Every other change, and the release

The same lane shape holds for every change that is not a plan stage — a `/batch-issues` charter
call, a chore or band commit, a skill or doc edit, a version bump (owner, 2026-11-11): cut
`claude/<lane>` from `origin/dev`, commit there, `/push`, open a pull request into `dev`, and merge it
as item 6 says (a band commit that changes code waits for its review). `dev`
is the integration branch; nothing is pushed to it directly, and local `dev` advances only by
`git pull origin dev` after a merge.

`mainline` is the release branch and is reached from `dev` only, never from a lane: the owner merges
`dev` into `mainline` (`Merge dev into mainline — release X.Y.Z`, or a pull request with base
`mainline` and head `dev`) once the version bump has merged into `dev` through its own lane
(`claude/bump-X.Y.Z`, `/bump-version`). A bump reaching `mainline` is the release cut: `auto-tag.yml`
tags it and dispatches `release.yml` (`/push` › 1.25). A sync in the other direction
(`Merge mainline into dev`) is also a pull request into `dev`. A session never merges into
`mainline` unless the owner said so in that session.

The predecessor charter (`rounds/remediation-round-2026-08-30-batching.md`, rulings R-A…R-AV) and the
plans that predate this policy were committed as they stood on the owner's machine (owner, 2026-11-07,
handoff D9 option B). Those plans carry no commit stamp and may include drafts, so a plan's own
checklist and verification record say how far it got, not its presence here.
