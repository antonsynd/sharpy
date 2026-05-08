---
name: property-stress
description: Stress-test property tests across many rounds to find rare bugs. Each round uses fresh random seeds.
argument-hint: "[rounds=10] [filter]"
---

Run property tests repeatedly with fresh random seeds each round to surface rare failures that normal test runs miss. CsCheck generates different random inputs each invocation, so N rounds = N × (100–200) unique inputs per test.

**Usage:**
- `/property-stress` — 10 rounds of all property tests (~10 min)
- `/property-stress 50` — 50 rounds (~50 min)
- `/property-stress 10 Parser` — 10 rounds, only parser property tests
- `/property-stress 20 Metamorphic` — 20 rounds, only metamorphic tests

**Output:** A bug report with each unique failure, its CsCheck reproduction seed, the failing test, and which round it occurred in.

**Log location:** `.claude/tmp/property-stress/` (one log per round + summary)

## Steps

### 1. Parse arguments

Parse `$ARGUMENTS` to extract rounds and optional filter:
- If first token is a number, use it as rounds count (default 10, max 100)
- Any remaining text is the test filter
- Construct the dotnet test filter: if user filter is set, use `"Category=Property&FullyQualifiedName~{filter}"`, otherwise `"Category=Property"`

### 2. Setup

```bash
mkdir -p .claude/tmp/property-stress
rm -f .claude/tmp/property-stress/*.log
```

### 3. Build

Run `dotnet build sharpy.sln --nologo -v q 2>&1 | tail -5`. If build fails, print "BUILD FAILED — cannot stress test" and stop.

### 4. Run rounds

For each round 1..N:
1. Print progress: `=== Round {i}/{N} ===`
2. Run: `dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "{filter}" --no-build --logger "console;verbosity=normal" > .claude/tmp/property-stress/round-{i}.log 2>&1`
3. If exit code is non-zero:
   - Extract failure blocks from the log. Look for lines containing `[FAIL]` to get test names, and lines containing `Set seed:` to get CsCheck reproduction seeds.
   - Print a brief summary: `Round {i}: FAIL — {test_name} (seed: {seed})`
4. If exit code is zero:
   - Print: `Round {i}: PASS`

### 5. Generate summary report

After all rounds complete, produce a summary by scanning all round logs:

```bash
# Count passes/failures
passes=$(grep -c "^Round.*PASS$" output)
failures=$(grep -c "^Round.*FAIL" output)

# Extract unique failures (deduplicate by test name)
grep -h "FAIL" .claude/tmp/property-stress/*.log | sort -u
```

Print the final report in this format:

```
=== Property Stress Test Report ===
Rounds: N
Passed: X
Failed: Y
Failure rate: Z%

Unique failures:
  1. TestClass.TestMethod
     Seed: "xxxxx" (reproduce: CsCheck_Seed=xxxxx dotnet test --filter "DisplayName~TestMethod")
     Error: <first line of error message>
     Rounds: 3, 7, 12

  2. ...

To reproduce any failure:
  CsCheck_Seed=<seed> dotnet test --filter "DisplayName~<test>" --no-build

Full logs: .claude/tmp/property-stress/
```

### 6. Key implementation details

- Use `--no-build` after the initial build to avoid rebuilding each round
- Parse CsCheck seed from output: look for the pattern `Set seed: "([^"]+)"` or `CsCheck_Seed=(\S+)`
- Parse failing test names from: lines matching `Failed\s+(\S+)\s+\[` in the console output
- Parse error messages from: lines between `Error Message:` and `Stack Trace:` in the console output
- Group failures by test name across rounds (same test may fail in multiple rounds with different seeds — report all seeds)
- If a test fails in >50% of rounds, flag it as "consistently failing" vs "flaky"
- Total runtime estimate: ~1 min per round. Print this upfront so the user knows what to expect.
