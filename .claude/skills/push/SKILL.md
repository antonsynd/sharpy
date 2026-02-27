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
