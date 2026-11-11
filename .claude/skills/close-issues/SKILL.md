---
name: close-issues
description: Close GitHub issues that have been implemented, with verification
argument-hint: "<plan.md or issue numbers: 123,456>"
---

Close GitHub issues after verifying their implementation. `$ARGUMENTS`: comma-separated numbers (`208,210`), a plan file (extract its `#NNN` references), or empty (ask the user).

## Steps

### 1. Gather issue list

From a plan file, deduplicate its `#NNN` references and confirm the list with the user first.

### 2. Fetch issue details

Run `gh` with `dangerouslyDisableSandbox: true` (sandbox TLS):

```bash
gh issue view <number> --json title,body,state,labels
```

Skip issues that are already closed; report them as "already closed".

### 3. Verify implementation

For each open issue, verify the fix exists in the codebase:

1. Read the issue title and body to understand what was requested
2. Search for relevant commits on the current branch:
   ```bash
   git log --oneline --all --grep="#<number>"
   ```
   A fix counts only once it is on `origin/dev` (`git branch -r --contains <sha>` lists `origin/dev`): a commit that exists only on an open lane branch is PARTIAL until its pull request merges.
3. If no commit references the issue, search by keywords from the issue title:
   ```bash
   git log --oneline -20 --grep="<keyword>"
   ```
4. Use Grep/Glob to spot-check that the described change actually exists in the codebase (e.g., if the issue says "add validator X", confirm the validator file exists)
5. A `class-tracker` issue: every row of every cell table (body or comment) needs a `status` of `fixed` plus a sha, backticked test name or `PR #N`; `moved #N` (an open issue; file it first) or `control`. A later comment restating a row's key supersedes it. Fill the column, then `python3 build_tools/check_class_closure.py <number>` must exit 0; on close a workflow re-runs it and comments the gaps. Closing as duplicate or not planned skips the check. File class issues with the `status` column.

Categorize: **VERIFIED** (found, tests pass), **PARTIAL** (gaps remain), **NOT FOUND**.

### 4. Close verified issues

For each VERIFIED issue:

```bash
gh issue close <number> --reason completed --comment "Closed — implemented in $(git log --oneline --all --grep='#<number>' | head -1 | cut -d' ' -f1). Verified in codebase."
```

If the commit hash isn't found via `--grep`, cite the merged pull request that carried the change into `dev` instead (`gh api "repos/{owner}/{repo}/commits/<sha>/pulls" --jq '.[0].html_url'`), never a lane branch name — lane branches are restarted after their pull request merges.

### 5. Handle unverified issues

PARTIAL: do **not** close; comment what landed and what remains:
  ```bash
  gh issue comment <number> --body "Partial implementation found: <description>. Remaining work: <gaps>."
  ```

NOT FOUND: do not close; report it.

### 6. Update plan file (if applicable)

If a plan file was provided, note the closures at its bottom:
  ```markdown
  <!-- Issues closed by /close-issues on YYYY-MM-DD: #123, #456 -->
  ```

### 7. Report

Present a summary:

```
## Issues Summary

| Issue | Title | Status | Action |
|-------|-------|--------|--------|
| #123  | ...   | VERIFIED | Closed |
| #456  | ...   | PARTIAL  | Commented (gaps: ...) |
| #789  | ...   | NOT FOUND | Skipped |
| #101  | ...   | Already closed | Skipped |
```
