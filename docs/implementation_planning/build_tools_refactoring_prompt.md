# Build Tools Refactoring - Reusable Agent Prompt

Use this prompt with Claude Sonnet to incrementally work through the task list.

---

## Prompt

```
You are working on refactoring the `build_tools/` directory for the Sharpy compiler project. Your goal is to make incremental progress on the task list, completing one task per session.

## Instructions

1. **Read the task list** at `docs/implementation_planning/build_tools_refactoring_tasks.md`

2. **Identify the next incomplete task** by finding:
   - The first task in the highest-priority phase (P0 > P1 > P2 > P3) where:
     - All acceptance criteria checkboxes are unchecked `[ ]`
     - All prerequisite tasks in earlier phases are complete

3. **Before implementing**, read the relevant existing code:
   - For extraction tasks: Read the source files mentioned in the task
   - For migration tasks: Read both the source and target locations
   - Understand what you're working with before changing anything

4. **Implement the task**:
   - Follow the acceptance criteria exactly
   - Use the code patterns shown in the task description
   - Write clean, typed Python code with docstrings
   - If the task mentions tests, write them

5. **Update the task list**:
   - Check off completed acceptance criteria: `[ ]` → `[x]`
   - Add any notes about implementation decisions below the task
   - If you encountered blockers or made design changes, document them

6. **Commit your changes**:
   - Stage all modified/created files
   - Use commit message format: `build_tools: <short description>`
   - Include "Co-Authored-By: Claude Sonnet 4 <noreply@anthropic.com>"

7. **Report what you did**:
   - Which task you completed
   - Files created/modified
   - Any decisions or deviations from the plan
   - What the next task would be

## Important Guidelines

- **One task per session** - Don't try to do multiple tasks
- **Preserve existing functionality** - Migration tasks must not break anything
- **Test before committing** - Run `python -c "import ..."` to verify imports work
- **Ask if unclear** - If a task description is ambiguous, ask for clarification rather than guessing
- **Skip if blocked** - If a task has unmet dependencies, document why and move to the next available task

## File Locations

- Task list: `docs/implementation_planning/build_tools_refactoring_tasks.md`
- Existing tools:
  - `build_tools/generate_code_walkthroughs.py`
  - `build_tools/sharpy_auto_builder/`
  - `build_tools/sharpy_dogfood/`
- New shared module (create if doesn't exist): `build_tools/shared/`

## Example Session Flow

1. Read task list → Find "Task 1.2: Extract rate limiting detection" is next
2. Read `generate_code_walkthroughs.py` lines ~159-173
3. Read `sharpy_dogfood/backends.py` for comparison
4. Create `build_tools/shared/rate_limiting/detector.py`
5. Write unit tests in `build_tools/tests/test_rate_limiting.py`
6. Verify: `python -c "from build_tools.shared.rate_limiting import is_rate_limit_error"`
7. Update task list with [x] checkboxes
8. Commit changes
9. Report completion

Now, read the task list and begin working on the next available task.
```

---

## Usage Notes

- **No modification needed** - Just paste this prompt as-is
- **Sonnet will figure out** which task is next by reading the task list
- **Progress is tracked** via checkbox updates in the task list
- **Each session** produces one commit with incremental progress
- **Opus escalation** - If Sonnet struggles with a task marked for Opus, it should say so

## For Opus-Level Tasks

If the agent encounters a task marked "Estimated Model: Opus", use this modified opener:

```
[Same prompt as above, but add this before "Now, read the task list..."]

Note: You may encounter tasks marked "Estimated Model: Opus". These require more complex reasoning. If you're running as Sonnet and encounter such a task, you may:
1. Attempt it if you feel confident
2. Skip it and move to the next Sonnet-appropriate task
3. Document that it needs Opus-level attention

Proceed with the next available task appropriate for your capability level.
```

## Checking Progress

To see overall progress, ask:

```
Read the task list at `docs/implementation_planning/build_tools_refactoring_tasks.md` and give me a summary of:
1. How many tasks are complete vs remaining
2. Which phase we're currently in
3. What the next 3 tasks are
```
