# Process records

Round charters and the plans that implemented them. These are the design-of-record for *why* the
tracker looks the way it does: issue bodies cite plan IDs (`P77a`), execution rows (`6c⁶⁷″`) and
ruling letters (`R-LK`) that resolve only here. They are excluded from the published docs site
(`mkdocs.yml` `exclude_docs`) and are not gated by `check_spec_blocks.sh`.

Policy (owner, 2026-11-06):

- `rounds/<charter>.md` is the live charter for a remediation round. It is edited in place at every
  batching call and committed with that call; git history replaces the per-call backup files the
  charter used to name. A condensation is a commit.
- A plan is committed at every stage, and every stage ends in a pull request into `dev` (owner,
  2026-11-10; see "The stage trail" below). This replaces the 2026-11-06 rule that only a passing
  `/verify-plan` and a closed `/verify-implementation` commit: a draft or a NEEDS REVISION pass that
  lived only in the gitignored `.claude/plans/` directory was lost with its container, and the next
  session had nothing to revise.
- A generated one-page index per batching call (group → issues → tier → execution position → current
  rulings with supersession chains resolved) is `rounds/index.md`, written by
  `python3 -m build_tools.process_index --write` and committed with the charter (CI runs `--check`);
  see `docs/audits/health-check-2026-11-06.md` §9 for why.

## The stage trail

`/create-plan`, `/verify-plan`, `/implement-plan` and `/verify-implementation` each leave a commit, a
pushed lane branch and a pull request into `dev`. The skills carry the stage-specific step; the shared
rules are here.

1. **File of record.** `docs/process/plans/<plan-id>.md`, from the first draft on. Its first line says
   what state it is in: the `<!-- DRAFT, not verified … -->` marker `/create-plan` writes, or a
   `/verify-plan` stamp. Presence here is not a verdict: `/implement-plan` reads the stamp and refuses a
   DRAFT or a NEEDS REVISION.
2. **Working copy.** `.claude/plans/<plan-id>.md` (gitignored) is where a stage edits the plan inside
   one session; the stage copies it over the file of record when it commits. A stage that finds no
   working copy (a new container) copies the file of record back first and drops the DRAFT marker. If
   both exist and differ beyond that marker, stop and ask which is current.
3. **Lane branch.** `dev` refuses direct pushes. Each stage works on `claude/<group>-<stage>`, cut from
   the tip of `origin/dev`: `-plan` for `/create-plan` and `/verify-plan`, the group or slice name for
   `/implement-plan`, `-verify` for `/verify-implementation`. While a lane's pull request is open, the
   next stage of the same lane pushes to that branch and the pull request carries it. Once it has
   merged, restart the branch from `origin/dev` (`git switch -C <branch> origin/dev`) and open a new
   pull request; never stack on merged history.
4. **Commit.** Explicit pathspecs, `git diff --cached --stat` before committing, a subject of at most
   72 characters (the `subjects` check), the trailer the harness provides. A plan-only commit stages
   the one plan file and nothing else.
5. **Pull request.** Push with `/push`, then open the pull request into `dev` unless the branch already
   has an open one: `gh pr create --base dev --head <branch> --title "<the stage commit's subject>"
   --body-file <file>`; where `gh pr` is refused (a cloud session), the GitHub MCP `create_pull_request`
   tool or `gh api -X POST repos/<owner>/<repo>/pulls`. The body names the plan, the stage and its
   result, what the next stage is, and for a code stage the gate `passed/failed/skipped @ sha
   (measured)`. The tip commit never carries `[skip ci]` (contract §6). The stage reports the pull
   request's URL; merging is the owner's, and a stage does not merge its own pull request unless the
   owner said so in the session.

### Every other change, and the release

The same lane shape holds for every change that is not a plan stage — a `/batch-issues` charter
call, a chore or band commit, a skill or doc edit, a version bump (owner, 2026-11-11): cut
`claude/<lane>` from `origin/dev`, commit there, `/push`, and open a pull request into `dev`. `dev`
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
checklist and verification record say how far it got, not its presence here. The current charter's
pre-policy per-call backups stayed machine-local.
