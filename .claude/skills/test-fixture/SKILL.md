---
name: test-fixture
description: Run a specific file-based integration test by name
argument-hint: "<test_name>"
---

Run a specific file-based integration test by its display name.

**Usage:** `/test-fixture <test_name>`

**Behavior:**
- Shows "TEST NAME PASSED" + summary on success
- Shows "TEST NAME FAILED" + last 80 lines on failure + points to full log

**Log location:** `.claude/tmp/last-test-fixture.log`

**Examples:**
```
/test-fixture hello
/test-fixture class_simple
/test-fixture list_append
```

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-test-fixture.log`
4. Build first: `dotnet build sharpy.sln --nologo -v q >> .claude/tmp/last-test-fixture.log 2>&1`. If build fails, print "=== BUILD FAILED — cannot run test ===" then `tail -30 .claude/tmp/last-test-fixture.log` and stop.
5. Run: `dotnet test --filter "DisplayName~$ARGUMENTS" --no-build >> .claude/tmp/last-test-fixture.log 2>&1`
6. Check exit code:
   - Exit 0: Print "=== TEST '$ARGUMENTS' PASSED ===" then `tail -20 .claude/tmp/last-test-fixture.log`
   - Exit non-zero: Print "=== TEST '$ARGUMENTS' FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-test-fixture.log`, then echo "=== Full log: .claude/tmp/last-test-fixture.log ==="
7. Return the actual exit code
