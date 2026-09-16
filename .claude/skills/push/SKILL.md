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

> **Pushing a version bump to `mainline` auto-tags and releases — do NOT `git tag` by hand.**
> The `.github/workflows/auto-tag.yml` workflow triggers on any push to `mainline` that touches
> `Directory.Build.props`. It reads `<SharpyVersion>`, and if `v<version>` does not already exist it
> creates and pushes that tag, then dispatches `release.yml` (which packs sharpyc for all RIDs, pushes
> the NuGet packages, and creates the GitHub Release). So a bump commit reaching `mainline` **is** the
> release cut. Consequences to respect:
> - **Never `git tag v<version>` + `git push` the tag yourself.** CI already does it; a manual tag
>   races the workflow (you will see a terse `Everything up-to-date` when your local tag happens to
>   match the CI-created one) and, if it lands first on a different commit, points the release at the
>   wrong tree. Let the push to `mainline` create the tag.
> - The tag lands on the **bump commit**. Make sure that commit is green *before* it reaches
>   `mainline` — run the whole-solution gate and the whitespace check (§1.5) first — because the tag
>   (and the published NuGet/GitHub release) freeze on it. A follow-up fix pushed afterward does not
>   move the already-created tag; re-pointing a published release tag is disruptive and can re-trigger
>   `release.yml` into a duplicate-NuGet-push failure. Prefer to push a clean bump commit once.
> - Two pushes delivering the same bump (e.g. `dev` then `mainline`, or a peer also pushing) fire
>   `auto-tag` per push event; the second `release.yml` run failing at "Create GitHub Release" with a
>   duplicate-release conflict is benign (the first run already published).

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
| **any `.cs` file** (hand-written OR generated) | `.claude/scripts/dotnet-serialized format whitespace sharpy.sln --verify-no-changes` (CI step "Verify generated C# is whitespace-clean (#1641)" — despite the name, this gate runs `dotnet format whitespace sharpy.sln --verify-no-changes` over the **whole solution**, so a stray indented blank line or brace mis-indent in ANY source file fails it, not just generated C#). If it reports files, fix them: for hand-written C# run `dotnet format whitespace sharpy.sln` (it edits in place) and commit; for generated C# fix the emitter and regenerate — never hand-format generated files. A per-agent format that each touched only its own files is NOT sufficient — a file no single agent formatted (e.g. one created by a peer) slips through; the pre-push check is whole-solution for this reason. |
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
