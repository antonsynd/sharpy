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

**Output:** A bug report with each unique failure, its CsCheck reproduction seed, the failing test, and which round it occurred in. A round that *crashes the test host* (rather than failing an assertion) is captured by the crash watchdog: the running test is named, a dump is preserved, and the round's seed is logged (#1033).

**Log location:** `.claude/tmp/property-stress/` (one log per round + summary; `round-N-blame/` per crashed round)

## CsCheck seed semantics (verified against CsCheck 4.7.0)

The watchdog exports a fresh `CsCheck_Seed` per round. What that env var does was verified empirically before relying on it (issue #1033, design D3):

- **`CsCheck_Seed` pins only the *first* generated case of each test** — the CsCheck docs say "the seed to use for the first iteration", and this is exactly what happens: the first generated value is identical across same-seed runs and differs across seeds, but iterations 2..N draw fresh entropy and diverge even with an identical seed.
- **Consequence for stress diversity — none lost.** Only iteration 1 shares a starting point across tests in a round; every test still explores its full `iter` count, and a fresh per-round seed varies iteration 1 across rounds too. Within-round and cross-round diversity are both intact.
- **Consequence for reproducibility — partial, so blame is primary.** Only a crash triggered by a test's *first* generated case is reproducible via `CsCheck_Seed=<round seed>`. A crash on a later iteration is identified by the blame artifacts (`Sequence_*.xml` names the running test; a dump is kept), not by seed replay. So `--blame-crash` is the primary crash-identity mechanism and the logged seed is a supplementary handle.
- **Seed format is strict.** The seed is a base-64 string over the alphabet `0-9a-zA-Z_-`; a malformed value makes CsCheck's `SeedString.Parse` throw at startup and aborts *every* test in the round. The generator below encodes a random 64-bit PCG state as 11 big-endian base-64 digits + a `0` stream digit — byte-identical to CsCheck's own `SeedString.ToString(state, stream=0)`. Do not hand-roll a seed (e.g. a plain decimal number) — those throw.

## Steps

### 1. Parse arguments

Parse `$ARGUMENTS` to extract rounds and optional filter:
- If first token is a number, use it as rounds count (default 10, max 100)
- Any remaining text is the test filter
- Construct the dotnet test filter: if user filter is set, use `"Category=RandomProperty&FullyQualifiedName~{filter}"`, otherwise `"Category=RandomProperty"`

### 2. Setup

```bash
mkdir -p .claude/tmp/property-stress
rm -rf .claude/tmp/property-stress/*.log .claude/tmp/property-stress/round-*-blame .claude/tmp/property-stress/round-*-results
```

### 3. Build

Run `.claude/scripts/dotnet-serialized build sharpy.sln --nologo -v q 2>&1 | tail -5`. If build fails, print "BUILD FAILED — cannot stress test" and stop.

### 4. Run rounds

For each round 1..N:

1. Print progress: `=== Round {i}/{N} ===`
2. Generate a fresh valid CsCheck seed and snapshot the pre-round test hosts:
   ```bash
   SEED=$(python3 -c 'import random;C="0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-";s=random.getrandbits(64);print("".join(C[(s>>(6*i))&63] for i in range(10,-1,-1))+"0")')
   # Snapshot testhost PIDs that already exist (including OTHER sessions' hosts)
   # so the post-round cleanup in step 4 can target only PIDs this round spawns.
   BEFORE_HOSTS=$(pgrep -f testhost 2>/dev/null || true)
   ```
3. Run the round with the watchdog flags, seed in the log header, artifacts scoped to a per-round results dir:
   ```bash
   LOG=".claude/tmp/property-stress/round-${i}.log"
   RESULTS=".claude/tmp/property-stress/round-${i}-results"
   rm -rf "$RESULTS"
   echo "=== Round ${i}/${N}  (CsCheck_Seed=${SEED}) ===" > "$LOG"
   CsCheck_Seed="$SEED" .claude/scripts/dotnet-serialized test \
     src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj \
     --filter "{filter}" --no-build --logger "console;verbosity=normal" \
     --blame-crash --blame-hang --blame-hang-timeout 15m \
     --results-directory "$RESULTS" >> "$LOG" 2>&1
   RC=$?
   ```
4. Kill only the test hosts **this round** spawned, to bound memory accumulation
   without touching other sessions' hosts. Diff a fresh `pgrep` against the
   `$BEFORE_HOSTS` snapshot from step 4.2 and kill only the newly-appeared PIDs:
   ```bash
   # Scoped cleanup — do NOT use a bare `pkill -f testhost`. A blanket pkill
   # reaps test hosts belonging to OTHER concurrent agent sessions (the same
   # cross-session-kill incident class as the `pkill -f dotnet` crashes seen
   # 4× during plan-f08ccf; #1132). Kill only PIDs absent from the pre-round
   # snapshot. Residual race: a host another session starts mid-round is not in
   # $BEFORE_HOSTS and could be caught here — accepted as strictly safer than a
   # blanket pkill, and rare (test hosts are long-lived; the window is one
   # round). This scoped diff is mirrored in `build_tools/bin/build_sharpy`
   # `property_test` (kill_new_testhosts) — keep both in sync.
   AFTER_HOSTS=$(pgrep -f testhost 2>/dev/null || true)
   for pid in $AFTER_HOSTS; do
     if ! printf '%s\n' "$BEFORE_HOSTS" | grep -qx "$pid"; then
       kill "$pid" 2>/dev/null || true
     fi
   done
   ```
5. If `RC` is non-zero, distinguish a **host crash** from an ordinary **assertion failure**
   (this logic is mirrored in `build_tools/bin/build_sharpy` `property_test` — keep both in sync):
   ```bash
   SEQ=$(find "$RESULTS" -name 'Sequence_*.xml' 2>/dev/null | head -1)
   if [ -n "$SEQ" ]; then
     # Host crash — blame watchdog fired. Preserve identity + dump.
     BLAME=".claude/tmp/property-stress/round-${i}-blame"; mkdir -p "$BLAME"
     cp "$SEQ" "$BLAME/"
     find "$RESULTS" \( -name '*.dmp' -o -name '*.crashdump' -o -name 'core.*' \) -exec cp {} "$BLAME/" \; 2>/dev/null
     CRASHED=$(python3 - "$SEQ" <<'PY'
import sys, re
t = open(sys.argv[1]).read()
r = re.findall(r'<Test\b(?=[^>]*\bCompleted="False")[^>]*\bName="([^"]*)"', t) \
    or re.findall(r'<Test\b[^>]*\bName="([^"]*)"', t)
print(", ".join(r) if r else "(unknown)")
PY
)
     DUMP=$(find "$BLAME" \( -name '*.dmp' -o -name '*.crashdump' -o -name 'core.*' \) | head -1)
     echo "Round ${i}: CRASH — test ${CRASHED}, seed ${SEED}, dump ${DUMP:-(none)}, blame ${BLAME}"
   elif [ ! -d "$RESULTS" ]; then
     # Results dir never created: if this was a host crash it predated blame attachment,
     # so no Sequence/dump artifacts exist. Flag it distinctly — don't let it pass as a
     # plain assertion failure.
     echo "Round ${i}: FAIL — round seed ${SEED}; results dir absent (a crash this early predates blame attachment — no artifacts)"
   else
     # Ordinary assertion failure — CsCheck printed its own reproduction seed.
     echo "Round ${i}: FAIL — round seed ${SEED} (grep the log for 'Set seed:' for the shrunk case)"
   fi
   rm -rf "$RESULTS"
   ```
   Extract failure blocks from the log: `[FAIL]` for test names, `Set seed:` for CsCheck reproduction seeds.
6. If `RC` is zero: `echo "Round ${i}: PASS (seed ${SEED})"` and `rm -rf "$RESULTS"`.

### 5. Generate summary report

After all rounds complete, produce a summary by scanning all round logs:

```bash
# Count passes / assertion-failures / host-crashes
passes=$(grep -c "^Round.*PASS" output)
failures=$(grep -c "^Round.*FAIL" output)
crashes=$(grep -c "^Round.*CRASH" output)

# Extract unique failures (deduplicate by test name)
grep -h "FAIL" .claude/tmp/property-stress/*.log | sort -u
```

Print the final report in this format:

```
=== Property Stress Test Report ===
Rounds: N
Passed: X
Failed: Y   (assertion failures)
Crashed: Z  (host crashes — see round-*-blame/)
Failure rate: (Y+Z)/N %

Unique failures:
  1. TestClass.TestMethod
     Seed: "xxxxx" (reproduce: CsCheck_Seed=xxxxx dotnet test --filter "DisplayName~TestMethod")
     Error: <first line of error message>
     Rounds: 3, 7, 12

Host crashes:
  1. CRASH — test TestClass.TestMethod
     Round seed: <round seed>   (pins iteration 1 only — see seed-semantics note)
     Dump: .claude/tmp/property-stress/round-N-blame/<dump>
     Sequence: .claude/tmp/property-stress/round-N-blame/Sequence_*.xml

To reproduce any assertion failure:
  CsCheck_Seed=<seed> dotnet test --filter "DisplayName~<test>" --no-build

Full logs: .claude/tmp/property-stress/
```

### 6. Key implementation details

- Use `--no-build` after the initial build to avoid rebuilding each round
- **Always run dotnet via `.claude/scripts/dotnet-serialized`** (flock-serialized; hook-enforced)
- **Seed each round** with a fresh valid CsCheck seed via the Python one-liner in step 4.2 — never a hand-written decimal (see seed-semantics note; a bad seed aborts the round)
- **Watchdog flags** `--blame-crash --blame-hang --blame-hang-timeout 15m` make a host crash name the running test and drop a dump instead of vanishing
- A **host crash** produces a `Sequence_*.xml`; an **assertion failure** does not — that is how step 5 distinguishes them
- Parse CsCheck seed from failure output: look for the pattern `Set seed: "([^"]+)"` or `CsCheck_Seed=(\S+)`
- Parse failing test names from: lines matching `Failed\s+(\S+)\s+\[` in the console output
- Parse error messages from: lines between `Error Message:` and `Stack Trace:` in the console output
- Group failures by test name across rounds (same test may fail in multiple rounds with different seeds — report all seeds)
- If a test fails in >50% of rounds, flag it as "consistently failing" vs "flaky"
- Total runtime estimate: ~1 min per round. Print this upfront so the user knows what to expect.

### 7. Report a bug

When a round **crashes the host**, do not let it vanish. Open (or update) an issue with:
- the crashing test name (from `round-N-blame/Sequence_*.xml`),
- the round's `CsCheck_Seed` (reproduces the crash only if it was triggered by the test's first generated case — otherwise it is context, not a repro; see the seed-semantics note),
- the dump path (`round-N-blame/*.dmp` / `*.crashdump`),
- the round log tail showing the abort.

When a round **fails an assertion**, report the failing test plus the `Set seed:` value CsCheck printed (that seed reproduces the shrunk case directly via `CsCheck_Seed=<seed> dotnet test --filter "DisplayName~<test>" --no-build`).
