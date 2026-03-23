---
name: close-issues
description: Close GitHub issues that have been implemented, with verification
argument-hint: "<plan.md or issue numbers: 123,456>"
---

Close GitHub issues after verifying their implementation is complete. Accepts either a plan file path (to extract referenced issues) or a comma-separated list of issue numbers.

**Usage:**
- `/close-issues 208,210` — verify and close specific issues
- `/close-issues plans/my-plan.md` — find and close issues referenced in a plan

## Argument Handling

Parse `$ARGUMENTS` to determine input mode:

- **Comma-separated numbers** (e.g., `123,456`): treat as explicit issue list
- **File path** (e.g., `plans/foo.md`): read the file and extract issue numbers from `#NNN` references
- **Empty**: ask the user which issues to close

## Steps

### 1. Gather issue list

If a plan file was provided:
- Read the plan file
- Extract all `#NNN` issue references (regex: `#(\d+)`)
- Deduplicate and present the list to the user for confirmation before proceeding

If explicit issue numbers were provided, use them directly.

### 2. Fetch issue details

For each issue, fetch its title and body (**run `gh` with `dangerouslyDisableSandbox: true`** — required due to TLS cert issues in sandbox):

```bash
gh issue view <number> --json title,body,state,labels
```

Skip any issues that are already closed — report them as "already closed" in the final summary.

### 3. Verify implementation

For each open issue, verify the fix exists in the codebase:

1. Read the issue title and body to understand what was requested
2. Search for relevant commits on the current branch:
   ```bash
   git log --oneline --all --grep="#<number>"
   ```
3. If no commit references the issue, search by keywords from the issue title:
   ```bash
   git log --oneline -20 --grep="<keyword>"
   ```
4. Use Grep/Glob to spot-check that the described change actually exists in the codebase (e.g., if the issue says "add validator X", confirm the validator file exists)

Categorize each issue as:
- **VERIFIED** — implementation found, tests pass
- **PARTIAL** — some work done but gaps remain
- **NOT FOUND** — no evidence of implementation

### 4. Close verified issues

For each VERIFIED issue:

```bash
gh issue close <number> --reason completed --comment "Closed — implemented in $(git log --oneline --all --grep='#<number>' | head -1 | cut -d' ' -f1). Verified in codebase."
```

If the commit hash isn't found via `--grep`, reference the branch name instead:

```bash
gh issue close <number> --reason completed --comment "Closed — implemented on branch $(git branch --show-current)."
```

### 5. Handle unverified issues

For PARTIAL issues:
- Do **not** close the original issue
- Add a comment noting what was implemented and what remains:
  ```bash
  gh issue comment <number> --body "Partial implementation found: <description>. Remaining work: <gaps>."
  ```

For NOT FOUND issues:
- Do **not** close them
- Report to the user that no implementation was found

### 6. Update plan file (if applicable)

If a plan file was provided:
- Add a comment at the bottom of the plan noting which issues were closed and when:
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
