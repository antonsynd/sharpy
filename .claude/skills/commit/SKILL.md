---
name: commit
description: Stage and commit current changes with an auto-generated message
argument-hint: "[optional commit message override]"
---

Commit current changes following the project's git commit conventions. Analyzes staged and unstaged changes, generates a descriptive commit message, and creates the commit.

**Usage:**
- `/commit` — auto-generate commit message from changes
- `/commit fix typo in TypeMapper` — use the provided message

## Steps

### 1. Assess the state

Run these in parallel:
- `git status` — see all staged, unstaged, and untracked files
- `git diff --cached` — see staged changes
- `git diff` — see unstaged changes
- `git log --oneline -5` — see recent commit message style

### 2. Stage files

If there are unstaged or untracked changes:
- Stage specific files by name — **never** use `git add -A` or `git add .`
- Do **not** stage files that likely contain secrets (`.env`, `credentials.json`, etc.) — warn the user if such files are present
- If only some files are relevant (e.g., mix of unrelated changes), ask the user which to include

### 3. Generate commit message

If `$ARGUMENTS` is non-empty, use it as the commit message body.

If `$ARGUMENTS` is empty, analyze the staged diff and generate a message:
- Use the commit style from the `git log` output (this repo uses conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`)
- Focus on the **why**, not the **what**
- Keep the first line under 72 characters
- Add a body paragraph if the change is non-trivial

### 4. Create the commit

```bash
git commit -m "$(cat <<'EOF'
<generated or provided message>

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
EOF
)"
```

### 5. Verify

Run `git status` after the commit to confirm success. Report the commit hash.

## Rules

- **Never** amend a previous commit unless the user explicitly asks
- If a pre-commit hook fails, fix the issue and create a **new** commit (do not use `--amend`)
- If there are no changes to commit, say so and stop
- If `dotnet format whitespace` hasn't been run (formatting changes appear in diff), run `/format` first before committing
