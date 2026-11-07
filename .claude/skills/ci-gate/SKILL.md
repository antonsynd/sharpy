---
name: ci-gate
description: Run the whole-solution commit gate on GitHub CI at a pushed branch's tip and report it as passed/failed/skipped @ sha. This is the default gate; the local run is the fallback.
argument-hint: "[--ref BRANCH | --run RUN_ID]"
---

Run the commit gate (contract §6) on GitHub CI: about 20 min, against about an hour for an unsharded local run. It dispatches `dotnet10.yml` and `python-build-tools.yml` at the exact commit on the branch tip, without opening a PR.

**Never open a do-not-merge PR just to get CI.** Nothing blocks merging one, and every PR already runs the gate. A branch without a PR uses this skill.

## Steps

1. **Push first.** The gate measures the origin tip, and the script refuses a branch whose local tip differs. Use `/push` on the branch. It runs the staleness gates, and CI would catch the same ones later.

2. **Run it in the background** and carry on with read-only work:

   ```bash
   .claude/scripts/ci-gate $ARGUMENTS
   ```

   Use `dangerouslyDisableSandbox: true` (`gh` fails TLS inside the sandbox) and `run_in_background: true`. Don't poll it: you're notified on exit.

3. **Act on the exit code:**

   | Exit | Meaning | Next |
   |---|---|---|
   | 0 | `CI GATE GREEN: passed/failed/skipped @ sha (measured, run N)` | Record that line as the gate result |
   | 1 | Red, with one `FAILED <test>` line per failed test, or a failed job with no test named | Read the failed job (`gh run view N --log-failed`), then attribute any red with a control run (below) |
   | 2 | Not runnable: unpushed, differs from origin, or the branch predates the trigger | Do what the message says. A branch that predates the trigger merges `origin/mainline`, or reads an existing PR run with `--run` |
   | 3 | CI gave no verdict: not started within 15 min, cancelled, unreachable, or no counts | **Fall back to the local gate** (below) and say in the report that you did |

## Reading a PR's run

Every PR runs both workflows when its diff reaches their paths. Pass the `.NET 10` run's id to `.claude/scripts/ci-gate --run <run-id>`. It also checks the `Python Build Tools` run of the same commit and event, and says so if there is none. The count is labelled `the PR merge ref of <sha>`, because a PR run tests the merge into the base and not the head commit. Use a dispatch when the claim has to be about the commit itself, which a plan's close-out gate does.

## Avoiding wasted runs

- A push to a PR cancels that PR's run in progress, so a superseded commit costs little. Dispatched and `mainline` runs are never cancelled.
- A commit that changes only plans or docs goes in with `[skip ci]` in its message, and the gate cites the last code commit. Without it, the PR re-runs the full suite, because the path filter reads the whole PR diff.

## Control runs

A red is attributed only after a control at the base commit (contract §5). In order of preference:

1. **An existing run:** if the base is on `mainline`, its push run usually exists. Find it with `gh run list --workflow dotnet10.yml --commit <sha>`.
2. **A dispatch:** `git push origin <base-sha>:refs/heads/ci/control-<short-sha>`, then `.claude/scripts/ci-gate --ref ci/control-<short-sha>`, then delete that branch. This needs the trigger at that commit, so an older base goes to option 3.
3. **Local:** the same test in a `git worktree` at the base, which is the quickest answer for a single test anyway.

## The local fallback

The local gate is still the same command:

```bash
.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"
```

Use it when this skill exits 3, and for what CI does not cover:

- `MetamorphicCorpusInvarianceTests`, left out of CI on purpose (it draws a fresh sample each run; `/property-stress` covers it).
- A failure that only shows on macOS: CI is Linux plus a Windows smoke job.

The edit loop, mutation runs and single-test probes stay local too. They were never the gate.
