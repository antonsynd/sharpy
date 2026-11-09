# Process records

Round charters and the plans that implemented them. These are the design-of-record for *why* the
tracker looks the way it does: issue bodies cite plan IDs (`P77a`), execution rows (`6c⁶⁷″`) and
ruling letters (`R-LK`) that resolve only here. They are excluded from the published docs site
(`mkdocs.yml` `exclude_docs`) and are not gated by `check_spec_blocks.sh`.

Policy (owner, 2026-11-06):

- `rounds/<charter>.md` is the live charter for a remediation round. It is edited in place at every
  batching call and committed with that call; git history replaces the per-call backup files the
  charter used to name. A condensation is a commit.
- Plans move here at two points: when `/verify-plan` passes (the design of record the implementation
  follows) and when `/verify-implementation` closes (the final state, with the verify record). Drafts
  and intermediate adequacy passes stay in the gitignored `.claude/plans/` scratch directory.
- A generated one-page index per batching call (group → issues → tier → execution position → current
  rulings with supersession chains resolved) is `rounds/index.md`, written by
  `python3 -m build_tools.process_index --write` and committed with the charter (CI runs `--check`);
  see `docs/audits/health-check-2026-11-06.md` §9 for why.

The predecessor charter (`rounds/remediation-round-2026-08-30-batching.md`, rulings R-A…R-AV) and the
plans that predate this policy were committed as they stood on the owner's machine (owner, 2026-11-07,
handoff D9 option B). Those plans carry no commit stamp and may include drafts, so a plan's own
checklist and verification record say how far it got, not its presence here. The current charter's
pre-policy per-call backups stayed machine-local.
