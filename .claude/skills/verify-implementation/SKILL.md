---
name: verify-implementation
description: Verify completed plan implementation, fix gaps/bugs/regressions, and commit fixes
argument-hint: "<path/to/plan.md>"
---

Verify that a plan has been fully and correctly implemented. Reads the plan, checks every step against the actual codebase, runs the full test suite, spawns a team of agents to audit different dimensions, fixes any gaps/bugs/regressions found, and commits all fixes.

## Argument Handling

If `$ARGUMENTS` is non-empty, use it as the path to the plan file.

If `$ARGUMENTS` is empty, find the most recently modified `.md` file in `~/.claude/plans/`:
```bash
ls -t ~/.claude/plans/*.md 2>/dev/null | head -1
```

If no plan file is found (directory doesn't exist or contains no `.md` files), ask the user to provide the plan path explicitly.

Read the plan file completely before proceeding.

## Pre-Verification Checklist

Before spawning any agents, perform these checks yourself:

### 1. Validate plan file

- Confirm the file exists and is readable
- Check for the `/verify-plan` stamp — search for `<!-- Verified by /verify-plan` near the top of the file
  - If **absent**: warn the user that the plan was never verified but proceed anyway
  - If present and result says **NEEDS REVISION**: warn the user that the plan was flagged as needing revision — proceed but note this in the final report
  - If present and result says **PASS** or **PASS WITH CORRECTIONS**: proceed normally
- Check for the `/implement-plan` evidence — look for implementation commits by checking `git log --oneline` for commit messages referencing the plan

### 2. Establish baseline

- Run `dotnet build sharpy.sln` — must succeed. If it fails, **stop and report the build error** (the implementation is broken)
- Run `dotnet test` — record pass/fail/skip counts as the **current baseline**
- Run `dotnet format whitespace --verify-no-changes` — record whether formatting is clean

### 3. Identify the plan's scope

Extract from the plan:
- Every **file path** mentioned (files that should have been created or modified)
- Every **step/task** described (numbered items, checkboxes, sections describing work)
- Every **test** mentioned (new test files, test cases, test fixtures)
- Every **diagnostic code** added or modified
- Every **AST node**, **type**, **validator**, **emitter method** introduced
- Every **Sharpy.Core** type or method added

Build a **completeness checklist** — a structured list of every deliverable the plan describes.

## Team Formation

Create a team using `TeamCreate` with name `verify-implementation`.

Spawn the following agents in parallel using the `Task` tool with `team_name: "verify-implementation"`:

### Agent 1: Completeness Auditor (`code-reviewer`)

```
You are auditing whether a plan was fully implemented. You have been given a completeness checklist extracted from a plan file.

For EVERY item in the checklist:
1. Use Glob to verify referenced files exist
2. Use Grep to verify referenced functions, classes, methods, types, and diagnostic codes exist
3. Use Read to verify the implementation matches what the plan describes (not just that a file exists, but that the content is correct)
4. For test fixtures (.spy + .expected/.error/.warning), verify both files exist and the .expected content is plausible

Report each item as:
- DONE — fully implemented as described
- PARTIAL — partially implemented (describe what's missing)
- MISSING — not implemented at all
- DIVERGED — implemented differently than planned (describe the divergence)

Output a structured checklist with status for every item.
```

Provide this agent with the full completeness checklist you extracted.

### Agent 2: Regression Detector (`verification-expert`)

```
You are checking for regressions introduced by recent implementation work. Focus on:

1. Run the full test suite: `dotnet test`
   - Report any failing tests
   - Compare against the baseline counts provided

2. Run file-based integration tests specifically:
   `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`
   - Report any failures with details

3. Check for .skip files that may have been added to hide failures:
   - Search for .skip files in src/Sharpy.Compiler.Tests/Integration/TestFixtures/
   - Flag any .skip files that appear to be recently created (check git status)

4. Run representative snippets through the compiler:
   - Pick 5 .spy files from snippets/ directory
   - Run each with: `dotnet run --project src/Sharpy.Cli -- run <file>`
   - Report any runtime errors

5. Check for .expected files that were modified (a plan violation):
   - Run `git diff mainline...HEAD -- "*.expected"` to find modified .expected files
   - Flag any modifications as potential test expectation tampering

Output a regression report with PASS/FAIL for each check.
```

Provide this agent with the baseline test counts.

### Agent 3: Architectural Reviewer (`code-reviewer`)

```
You are reviewing the architectural quality of recently implemented changes. Focus on code that was added or modified as part of the plan implementation.

Check the git diff to identify changed files:
`git diff mainline...HEAD --name-only`

For each changed file, review for:

1. **Sharpy Convention Compliance**
   - Feature implementation order respected (Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests)
   - Immutable AST rule: no mutable state added to AST node records; annotations in SemanticInfo
   - SyntaxFactory-only rule: CodeGen changes use Roslyn SyntaxFactory, never string templating
   - Axiom precedence: .NET > Type Safety > Python Syntax
   - C# 9.0 constraint: Sharpy.Core and generated code must be C# 9.0 compatible
   - Language spec consistency: check docs/language_specification/ for contradictions

2. **Code Quality**
   - No dead code, commented-out code, or debug leftovers (console.writeline, print debugging)
   - No TODO/FIXME/BUG comments without GitHub issue references
   - No magic numbers or strings that should be constants
   - No copy-paste duplication that should be abstracted
   - Error handling covers edge cases (null, empty, UnknownType)

3. **Maintainability & Scalability**
   - New code follows existing patterns in its file/module
   - No tight coupling introduced between pipeline phases
   - Validators registered at correct Order values (no conflicts)
   - Symbol lifecycle respected (progressive population across passes)
   - Materialization at correct phase boundaries
   - CompilerServices integration follows the adapter pattern

4. **Design Anti-Patterns**
   - "Add X because Python has it" — feature creep
   - Runtime type checking where compile-time would work
   - Wrapper types instead of extension methods
   - Multiple ways to do the same thing
   - Magic behavior that's unpredictable

Output findings as: CRITICAL (must fix before merge), WARNING (should fix), SUGGESTION (nice to have).
```

### Agent 4: Test Coverage Auditor (`verification-expert`)

```
You are auditing test coverage for recently implemented changes. Focus on code added or modified as part of the plan.

1. Identify changed source files:
   `git diff mainline...HEAD --name-only -- "src/Sharpy.Compiler/" "src/Sharpy.Core/"`

2. For each changed source file, check if corresponding tests exist:
   - Parser changes -> Parser tests in Sharpy.Compiler.Tests/Parser/
   - Semantic changes -> Semantic tests in Sharpy.Compiler.Tests/Semantic/
   - CodeGen changes -> CodeGen tests in Sharpy.Compiler.Tests/CodeGen/
   - Core changes -> Sharpy.Core.Tests/
   - New language features -> file-based integration tests (.spy + .expected)

3. Check test quality:
   - Positive tests (happy path) exist
   - Negative tests (.spy + .error) exist for error cases
   - Edge cases covered (empty collections, None/null, boundary values, single elements)
   - Warning tests (.warning) exist if new warnings were added

4. Check for untested code paths:
   - New switch/match arms without corresponding tests
   - New exception types or error paths without negative tests
   - New diagnostic codes without test coverage

5. Verify Python behavior alignment:
   - For any new Python semantics, run `python3 -c "..."` to verify behavior matches
   - Flag any semantic divergences

Output a coverage report with: COVERED, PARTIALLY COVERED (missing cases listed), NOT COVERED.
```

## Collect Audit Results

Wait for all four agents to complete and return their reports. Do **not** begin remediation until every agent has reported back. Compile a unified list of issues from all reports, categorized as:

## Remediation Phase

Address every issue from the unified list:

| Category | Action |
|----------|--------|
| MISSING implementation | Implement it yourself or delegate to an appropriate agent |
| PARTIAL implementation | Complete it yourself or delegate |
| Regression (test failure) | Fix the root cause — never modify .expected files |
| Architectural violation | Fix the code to follow conventions |
| Missing tests | Write the missing tests |
| TODO without issue | Create the GitHub issue with `gh issue create` and update the comment |
| Formatting | Run `dotnet format whitespace` |
| Dead code / debug leftovers | Remove them |

### Remediation Rules

1. **Fix in priority order**: build failures > test regressions > missing implementations > architectural violations > missing tests > formatting/cleanup
2. **Never modify .expected files** to make tests pass — fix the implementation
3. **Verify each fix**: after fixing an issue, run the relevant tests to confirm the fix works
4. **Stage specific files**: never use `git add -A` or `git add .`
5. **Incremental commits**: group related fixes into logical commits, e.g.:
   - `fix: Complete missing implementation for [plan step X]`
   - `fix: Resolve regression in [component]`
   - `fix: Add missing test coverage for [feature]`
   - `chore: Fix architectural violations from plan implementation`
6. **Include co-author**: all commits must include `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>`
7. **Run full suite after all fixes**: `dotnet build sharpy.sln && dotnet test && dotnet format whitespace --verify-no-changes`

### Delegating Fixes

For fixes in specialized areas, delegate to domain agents via the `Task` tool with `team_name: "verify-implementation"`:

| Area | Agent Type |
|------|-----------|
| Parser/Lexer/AST fixes | `parser-expert` |
| Semantic/TypeChecker fixes | `semantic-expert` |
| CodeGen/RoslynEmitter fixes | `codegen-expert` |
| Sharpy.Core/stdlib fixes | `core-library-expert` |
| Test writing | `test-expert` |
| General implementation | `implementer` |

Provide each agent with:
- The specific issue to fix
- The relevant plan section
- The file(s) involved
- Clear acceptance criteria

## Final Verification

After all fixes are committed:

1. `dotnet build sharpy.sln` — must succeed
2. `dotnet test` — no failures (compare against original baseline — pass count should be equal or higher)
3. `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` — all pass
4. `dotnet format whitespace --verify-no-changes` — clean
5. Run 3 representative `.spy` snippets through `dotnet run --project src/Sharpy.Cli -- run <file>` — no crashes
6. `git diff mainline...HEAD --stat` — summarize all changes

If any final verification step fails, loop back to remediation. Maximum 3 remediation loops — if issues persist after 3 attempts, report them as unresolved.

## Cleanup and Report

After final verification passes:

1. Shut down all teammates via `SendMessage` with `type: "shutdown_request"`
2. Delete the team via `TeamDelete`
3. Present the verification report to the user:

```markdown
## Implementation Verification Report

**Plan:** [plan file path]
**Branch:** [current branch]
**Verified on:** YYYY-MM-DD

### Completeness

| Status | Count |
|--------|-------|
| Fully implemented | N |
| Was partial (now fixed) | N |
| Was missing (now fixed) | N |
| Diverged from plan (acceptable) | N |
| Unresolved | N |

### Regressions

- **Baseline:** X passed, Y failed, Z skipped
- **Post-implementation:** X passed, Y failed, Z skipped
- **Post-remediation:** X passed, Y failed, Z skipped
- **Regressions found:** N (N fixed)

### Architectural Review

- **Critical issues found:** N (N fixed)
- **Warnings found:** N (N fixed)
- **Suggestions:** N

### Test Coverage

- **New tests added (by plan):** N
- **Missing tests added (by remediation):** N
- **Coverage gaps remaining:** N

### Fixes Applied

| Commit | Description | Category |
|--------|-------------|----------|
| abc1234 | ... | missing-impl / regression / arch-violation / missing-test / cleanup |

### Issues Created

| Issue | Title | Reason |
|-------|-------|--------|
| #NNN | ... | TODO without issue / deferred work / discovered tech debt |

### Unresolved Items

(List any items that could not be fixed, with explanation)

### Files Changed (total, including plan implementation + fixes)

(output of `git diff mainline...HEAD --stat`)
```
