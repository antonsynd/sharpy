---
name: push
description: Push current branch to remote origin
argument-hint: "[--close-issues 123,456]"
---

Push the current branch to the remote and optionally close GitHub issues.

**Usage:**
- `/push` — push current branch
- `/push --close-issues 208,210` — push and close the specified issues

## Argument Handling

Parse `$ARGUMENTS` for:
- `--close-issues` — comma-separated list of issue numbers to close after pushing

## Steps

### 1. Pre-flight checks

Run in parallel:
- `git status` — verify working tree is clean (warn if dirty)
- `git log --oneline @{upstream}..HEAD 2>/dev/null || echo "no upstream"` — show commits that will be pushed
- `git branch --show-current` — get current branch name

If the branch is `main` or `mainline`, **warn the user** that they're about to push directly to the main branch and confirm before proceeding.

If the branch is `dev`, **stop**: `dev` accepts only pull requests (ruleset, 2026-11-08, requiring the `subjects` check). Push a `claude/<lane>` branch and open a PR into `dev`; local `dev` advances only by `git pull origin dev`.

### 1.1. Commit-subject length gate

CI's `Commit Subjects` workflow fails a PR whose non-merge, non-revert subjects exceed 72 characters. Run it on the outgoing commits:

```bash
bash build_tools/check_commit_subjects.sh "$(git rev-parse --abbrev-ref --symbolic-full-name @{upstream} 2>/dev/null || echo origin/dev)..HEAD"
```

A red means reword before pushing: `git commit --amend` for the tip, a non-interactive `git rebase` for older ones, your own unpushed commits only.

### 1.25. Version bump check (bump lanes and mainline only)

If the current branch is a `claude/bump-*` lane or `mainline`, check whether a version bump is needed before pushing (`dev` is never pushed, §1; a release reaches `mainline` by the owner's merge from `dev`):

```bash
git describe --tags --abbrev=0 2>/dev/null || echo "none"
grep -oP '(?<=<SharpyVersion>)[^<]+' Directory.Build.props
```

If the current `SharpyVersion` in `Directory.Build.props` equals the last tag version (strip leading `v`), warn:

> ⚠️ **Version not bumped:** `Directory.Build.props` is still at `X.Y.Z`, which matches the last tag `vX.Y.Z`. If this push is destined for a release, run `/bump-version --apply` first.

This is advisory — the push is not blocked. Skip silently if the version already exceeds the last tag.

A bump reaching `mainline` **is** the release cut: `auto-tag.yml` tags `v<version>` on the bump commit and dispatches `release.yml` (NuGet packages, GitHub Release). Never `git tag` by hand (it races the workflow and can point the release at the wrong tree), and have the bump commit green (`/ci-gate`, §1.5) before it reaches `mainline`: a later fix does not move the tag. A second `release.yml` run failing on a duplicate release is benign; do not re-tag.

### 1.5. Generated-artifact staleness gate

CI fails (`check_spy_staleness.sh` / `check_spy_tests_staleness.sh`) when generated C# or generated docs fall out of sync with their sources. Catch this **before** pushing.

Check which paths the outgoing commits touch:

```bash
git diff --name-only @{upstream}..HEAD 2>/dev/null || git diff --name-only origin/mainline...HEAD
```

Then run the matching regeneration checks (use `dangerouslyDisableSandbox` — these invoke dotnet):

| Outgoing changes touch | Run |
|------------------------|-----|
| `src/Sharpy.Stdlib/spy/` or `src/Sharpy.Compiler/` or `src/Sharpy.Core/` | `bash build_tools/check_spy_staleness.sh` |
| `src/Sharpy.Stdlib.Tests/Spy/` or `src/Sharpy.Compiler/` or `src/Sharpy.Core/` | `bash build_tools/check_spy_tests_staleness.sh` |
| `src/Sharpy.Core/` or `src/Sharpy.Stdlib/` (public API / doc comments) | `python3 -m build_tools stdlib generate --force` then `git status --short -- docs/stdlib` |
| **any `.cs` file** (hand-written OR generated) | `.claude/scripts/dotnet-serialized format whitespace sharpy.sln --verify-no-changes` — the CI step "Verify generated C# is whitespace-clean (#1641)" runs this over the **whole solution**, so a file no agent formatted (a peer's) fails it too. Fix hand-written C# with `/format` and commit; for generated C# fix the emitter and regenerate, never hand-format. |
| `docs/deviations.yaml` or `src/Sharpy.Stdlib.Tests/Spy/cpython/` | `python3 -m build_tools.cpython_oracle ledger --write` then `git status --short -- build_tools/cpython_oracle/ledger.yaml` (commit if dirty — the pytest gate `test_committed_ledger_is_up_to_date` and the dual-execute-oracle CI job both fail on a stale ledger) |
| `src/**/Conformance/*-allowlist.txt` or any `*.Tests/**/*.cs` | `bash build_tools/check_allowlist_issue_state.sh` (exit 1 = a row or Skip cites a CLOSED issue; drain it) |
| `docs/language_specification/**` or `build_tools/spec_blocks*` | `bash build_tools/check_spec_blocks.sh` (exit 1 = an unmarked failing block or a stale allowlist entry) |

- If a staleness check reports STALE/MISSING files: run the corresponding regeneration script (`build_tools/regenerate_spy_stdlib.sh` or `build_tools/regenerate_spy_tests.sh`), commit the regenerated files, and re-run the check.
- If the docs generator leaves `docs/stdlib` dirty: commit the regenerated docs (`docs(stdlib): regenerate ...`). Never hand-edit generated docs or generated C#.
- Compiler changes matter too: codegen changes alter the *generated* spy-test C# even when no `.spy` file changed — that's why `src/Sharpy.Compiler/` is in the trigger column.

### 2. Push

```bash
git push -u origin <current-branch>
```

If the push fails due to diverged history, **do not force push**. Instead, report the error and suggest `git pull --rebase` or ask the user how to proceed. A `GH013: Repository rule violations` refusal on `dev` is the PR-only ruleset, not a transient: move the commits to a lane branch (`git branch claude/<lane>` then open the PR) rather than retrying.

### 3. Close issues (if requested)

If `--close-issues` was provided, close each issue:

```bash
gh issue close <number> --reason completed
```

Report which issues were closed.

### 4. Report

Show:
- Branch name pushed
- Number of commits pushed
- Remote URL
- Any issues closed
