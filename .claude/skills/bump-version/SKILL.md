---
name: bump-version
description: Suggest and apply a semver version bump based on commits since the last tag
argument-hint: "[--apply] [--major|--minor|--patch]"
---

Analyze commits since the last git tag and suggest (or apply) the appropriate semver bump to `Directory.Build.props`, `editors/vscode/package.json`, and `editors/vscode/CHANGELOG.md`.

Only runs on the `dev` or `mainline` branch. Exits early on other branches.

**Usage:**
- `/bump-version` — analyze and suggest, do not apply
- `/bump-version --apply` — analyze, suggest, and apply the bump
- `/bump-version --minor --apply` — force a minor bump and apply it

## Steps

### 1. Check branch

```bash
git branch --show-current
```

If the branch is not `dev` or `mainline`, report that version bumps are only suggested on `dev`/`mainline` and stop.

### 2. Get last tag and current version

Run in parallel:
```bash
git describe --tags --abbrev=0 2>/dev/null || echo "none"
```
```bash
grep -oP '(?<=<SharpyVersion>)[^<]+' Directory.Build.props
```

If no tags exist, treat last tag as `0.0.0` and all commits as unreleased.

### 3. Collect commits since last tag

```bash
git log --oneline <last-tag>..HEAD
```

If the current version already differs from the last tag (a bump is already pending), note this and still show the commit analysis — the user may want to revise the bump.

### 4. Classify commits

Scan commit subjects for conventional commit prefixes:

| Trigger | Bump |
|---------|------|
| `!` after type (e.g., `feat!:`) or `BREAKING CHANGE` in body | **major** |
| `feat:` or `feat(...):`  | **minor** |
| `fix:`, `perf:`, `refactor:`, `chore:`, `docs:`, `test:`, `build:`, `ci:` | **patch** |

Take the highest-priority bump across all commits. If no conventional commits found, default to **patch**.

### 5. Compute suggested version

Parse `<current-tag>` (strip leading `v`) as `MAJOR.MINOR.PATCH` and apply the bump:
- **major** → `(MAJOR+1).0.0`
- **minor** → `MAJOR.(MINOR+1).0`
- **patch** → `MAJOR.MINOR.(PATCH+1)`

If `--major`, `--minor`, or `--patch` was passed in `$ARGUMENTS`, override the computed bump level.

### 6. Report

Print a summary:
```
Last tag:    v0.3.0
Current:     0.4.0  (already bumped — bump pending)
Commits:     126 since v0.3.0
Suggestion:  minor bump → 0.4.0
  Reason: 21 feat commits detected
```

If the current version already matches the suggested version, say so and stop (nothing to do).

### 7. Apply (if --apply)

If `--apply` was in `$ARGUMENTS` **and** the suggested version differs from the current version:

Edit `Directory.Build.props`:
- Replace `<SharpyVersion>X.Y.Z</SharpyVersion>` with the new version

Edit `editors/vscode/package.json`:
- Replace `"version": "X.Y.Z"` with the new version

Edit `editors/vscode/CHANGELOG.md` (**required** — a version bump without a changelog
entry is a gate failure, not a warning):
- Rename the `## [Unreleased]` heading to `## [X.Y.Z] - YYYY-MM-DD` using the new version
  and today's date, then add a fresh empty `## [Unreleased]` section above it.
- If there is no `## [Unreleased]` section, create the `## [X.Y.Z] - YYYY-MM-DD` entry from
  the extension-facing commits since the last tag:
  ```bash
  git log --format="%h %ad %s" --date=short <last-tag>..HEAD -- editors/vscode/
  ```
  If none of those commits changed extension behavior, the entry reads
  "No extension-facing changes; version bumped with the toolchain."
  Never leave the version bumped with no entry at all.

Report which files were updated and remind the user to commit the changes (suggest `/commit`).

Print a checklist, marking a missing or unwritten changelog entry as **FAILED** — the bump
is not complete until every line passes:
```
[ok]     Directory.Build.props     <SharpyVersion>0.9.0</SharpyVersion>
[ok]     editors/vscode/package.json    "version": "0.9.0"
[FAILED] editors/vscode/CHANGELOG.md    no [0.9.0] entry — write one before committing
```

## Rules

- Never apply a bump without `--apply`
- Never leave `editors/vscode/CHANGELOG.md` without an entry for the new version — the marketplace listing renders it, so a bump with no entry ships a release users cannot read about
- Never downgrade the version (if current > suggested, report the discrepancy and stop)
- If already bumped past the suggestion (e.g., manual bump to a higher version), congratulate and stop
