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

### 1.25. Version bump check (dev/mainline only)

If the current branch is `dev` or `mainline`, check whether a version bump is needed before pushing:

```bash
git describe --tags --abbrev=0 2>/dev/null || echo "none"
grep -oP '(?<=<SharpyVersion>)[^<]+' Directory.Build.props
```

If the current `SharpyVersion` in `Directory.Build.props` equals the last tag version (strip leading `v`), warn:

> ⚠️ **Version not bumped:** `Directory.Build.props` is still at `X.Y.Z`, which matches the last tag `vX.Y.Z`. If this push is destined for a release, run `/bump-version --apply` first.

This is advisory — the push is not blocked. Skip silently if the version already exceeds the last tag.

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

- If a staleness check reports STALE/MISSING files: run the corresponding regeneration script (`build_tools/regenerate_spy_stdlib.sh` or `build_tools/regenerate_spy_tests.sh`), commit the regenerated files, and re-run the check.
- If the docs generator leaves `docs/stdlib` dirty: commit the regenerated docs (`docs(stdlib): regenerate ...`). Never hand-edit generated docs or generated C#.
- Compiler changes matter too: codegen changes alter the *generated* spy-test C# even when no `.spy` file changed — that's why `src/Sharpy.Compiler/` is in the trigger column.

### 2. Push

```bash
git push -u origin <current-branch>
```

If the push fails due to diverged history, **do not force push**. Instead, report the error and suggest `git pull --rebase` or ask the user how to proceed.

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
