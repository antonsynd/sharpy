#!/usr/bin/env python3
"""
Interleaved A/B benchmark orchestrator for the Sharpy compiler (#1318).

Why this exists
---------------
Run **position** alone swings wall-clock 7-10% on this corpus: whatever runs second
appears faster, and BenchmarkDotNet's 99.9% confidence intervals come out
non-overlapping *in both directions* depending on which arm you ran first. So the
usual comparison — build A, measure, build B, measure, subtract — cannot distinguish
a code effect from an ordering artifact, and "the intervals don't overlap" does not
rescue it. Three measurements in round 8 were corrupted this way before anyone
noticed the mechanism.

Two remedies, both built in here:

1. **Interleave.** Alternate A, B, A, B, ... so each arm is measured the same number
   of times in each position. A position bias that affects both arms equally cancels
   in the pooled mean instead of loading onto whichever arm ran second.

2. **Require sign agreement between positions.** Pool separately by position — arm A
   when it ran first vs arm A when it ran second — and report a delta only when both
   pools agree on its direction. When they disagree, the honest answer is
   "unmeasured: position-dominated", not a number with a confidence interval.

3. **Discard the unsettled opening rounds** (``--discard-rounds``, #1418). The artifact
   turned out to have a session component as well as a positional one: on identical code
   the first ~8 invocations run measurably slower and far noisier than everything after
   them, ACROSS both arms. Interleaving cannot cancel that, because it inflates whichever
   arm happens to be measured early. So the opening rounds are run and then dropped, and
   the early-vs-late split is printed on every run — a pooled delta cannot tell you that
   the machine had not settled, and that is exactly what a reader needs to know.

What it does not do
-------------------
It does not decide whether a delta matters, and it does not touch allocations, which
are measured precisely and are immune to the artifact (see ``allocation_gate.py`` --
that remains the load-bearing regression signal for allocation changes).

Usage
-----
::

    python -m build_tools.bench_ab <refA> <refB> [--rounds N] [--filter PATTERN]

Each ref is anything ``git rev-parse`` accepts. Two worktrees are prepared and built
in Release once; the rounds then only run the already-built binaries::

    python -m build_tools.bench_ab HEAD~1 HEAD --rounds 4
    python -m build_tools.bench_ab main my-branch --filter '*Fibonacci*'

The null control -- comparing a ref to itself -- must report no significant delta.
An instrument that cannot say "nothing" cannot be trusted to say "something"::

    python -m build_tools.bench_ab HEAD HEAD --rounds 4
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import shutil
import statistics
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from typing import Iterable, Optional

DEFAULT_FILTER = "*CompilerBenchmarks*"
DEFAULT_ROUNDS = 3

#: Opening rounds discarded before pooling (#1418). The position artifact turned out to have a
#: SESSION component: measured on identical code (HEAD vs HEAD), the parse mean decayed
#: 7.632 6.991 6.140 6.017 5.542 5.468 4.955 4.408 | 4.416 4.385 ... µs — flat from the ninth
#: INVOCATION, with StdDev collapsing 3.45 -> 0.04. A round is TWO invocations, so the settling
#: point is the fourth round boundary: N=4 discards 8 invocations. N=2 would still pool
#: invocations 5-8, which sit 26%/24%/12%/0% above the settled floor — most of the artifact this
#: exists to remove. The transient spans ARMS, which is why #1318's pre-warm-each-arm remedy made
#: it worse rather than better.
DEFAULT_DISCARD_ROUNDS = 4

#: Below this, a pooled difference is reported as noise regardless of sign agreement.
#: Chosen from the measured position effect (7-10%): a delta the artifact alone can
#: produce is not evidence, so the floor sits above it.
#:
#: Deliberately NOT lowered when the discard window landed (#1418). Settled rounds agree to
#: ±0.3% on identical code, so a smaller floor is plausible — but "plausible" is not a
#: measurement, and re-deriving this number requires an A/A run on a QUIET machine, which is
#: the one condition an agent-shared checkout cannot supply. Deferred with its reason recorded
#: in benchmarks/BASELINE.md; until then the floor keeps its pre-#1418 value and the early/late
#: split below is what tells a reader whether a given run had settled at all.
SIGNIFICANCE_THRESHOLD_PERCENT = 15.0


# --------------------------------------------------------------------------------------
# Parsing
# --------------------------------------------------------------------------------------


def parse_bdn_means(results_dir: str) -> dict[str, float]:
    """
    Parse BenchmarkDotNet report JSON under *results_dir* into benchmark -> mean ns.

    Mirrors ``allocation_gate.parse_bdn_results``, reading ``Statistics.Mean`` instead of
    the allocation column. Raises when nothing is found: an empty parse silently pooling
    to "no difference" is the failure mode this whole module exists to prevent.
    """
    patterns = [
        os.path.join(results_dir, "results", "*-report-full-compressed.json"),
        os.path.join(results_dir, "results", "*-report-full.json"),
        os.path.join(results_dir, "results", "*-report.json"),
        os.path.join(results_dir, "*-report-full.json"),
        os.path.join(results_dir, "*-report.json"),
    ]
    files: list[str] = []
    for pattern in patterns:
        files.extend(sorted(glob.glob(pattern)))

    if not files:
        raise FileNotFoundError(
            f"No BenchmarkDotNet report JSON under {results_dir!r}. "
            "Did the run use --exporters json?"
        )

    means: dict[str, float] = {}
    for path in files:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
        for bench in data.get("Benchmarks", []):
            name = bench.get("FullName") or bench.get("Method")
            stats = bench.get("Statistics")
            if name is None or not stats:
                continue
            mean = stats.get("Mean")
            if mean is None:
                continue
            means[name] = float(mean)

    if not means:
        raise ValueError(
            f"Report JSON under {results_dir!r} contained no benchmark means. "
            "A run that produced no measurements must not pool to 'no difference'."
        )
    return means


# --------------------------------------------------------------------------------------
# Pooling and the sign gate
# --------------------------------------------------------------------------------------


@dataclass
class Measurement:
    """One benchmark's mean from one arm in one position."""

    arm: str
    position: int  # 0 = ran first in its round, 1 = ran second
    benchmark: str
    mean_ns: float
    #: Which round produced this, counted from 0 across the whole orchestration. Carried so the
    #: opening rounds can be dropped from the pool (#1418) and shown separately; defaulted so
    #: hand-built measurements in tests stay valid.
    round_index: int = 0


@dataclass
class Verdict:
    benchmark: str
    delta_first_percent: Optional[float]
    delta_second_percent: Optional[float]
    pooled_delta_percent: Optional[float]
    measured: bool
    reason: str
    samples: dict[str, int] = field(default_factory=dict)

    def describe(self, arm_a: str, arm_b: str) -> str:
        if not self.measured or self.pooled_delta_percent is None:
            return f"  {self.benchmark}: UNMEASURED — {self.reason}"
        assert self.delta_first_percent is not None
        assert self.delta_second_percent is not None
        direction = "slower" if self.pooled_delta_percent > 0 else "faster"
        return (
            f"  {self.benchmark}: {arm_b} is {abs(self.pooled_delta_percent):.1f}% "
            f"{direction} than {arm_a} "
            f"({arm_a} first: {self.delta_first_percent:+.1f}%, "
            f"{arm_b} first: {self.delta_second_percent:+.1f}%)"
        )


def round_was_interrupted(before: Optional[int], after: Optional[int]) -> bool:
    """
    Did the lock change hands between a round's two arms? (#1420)

    Unknown is NOT interrupted: when the wrapper cannot report a generation (no wrapper in this
    checkout, an older copy without the flag), the honest answer is "cannot tell", and dropping
    every round on that basis would delete the whole run rather than protect it. The detector
    reports what it can see and stays silent about what it cannot — the opposite of the
    fail-open that treated unobservable as absent.
    """
    if before is None or after is None:
        return False
    return before != after


def split_discarded(
    measurements: Iterable[Measurement], discard_rounds: int
) -> tuple[list[Measurement], list[Measurement]]:
    """
    Partition into (discarded opening rounds, pooled tail) by round index (#1418).

    Whole ROUNDS are dropped, never individual invocations: a round is one A and one B, so
    dropping rounds keeps every (arm, position) cell balanced in the tail for any window size,
    while dropping invocations would bias the pool toward whichever arm survived the cut.
    """
    measurements = list(measurements)
    early = [m for m in measurements if m.round_index < discard_rounds]
    late = [m for m in measurements if m.round_index >= discard_rounds]
    return early, late


def warmup_split_lines(
    measurements: Iterable[Measurement], discard_rounds: int
) -> list[str]:
    """
    Per-benchmark medians of the discarded rounds vs the pooled tail.

    Printed on EVERY run, including when nothing was discarded. The transient this window exists
    for is invisible in a pooled delta — it inflates both arms — so a reader who cannot see the
    early-vs-late gap has no way to tell a settled run from an unsettled one (#1418, option 3).
    """
    early, late = split_discarded(measurements, discard_rounds)
    if not early:
        return [f"Warm-up split: no rounds discarded (--discard-rounds {discard_rounds})."]

    early_by_benchmark: dict[str, list[float]] = {}
    late_by_benchmark: dict[str, list[float]] = {}
    for measurement in early:
        early_by_benchmark.setdefault(measurement.benchmark, []).append(measurement.mean_ns)
    for measurement in late:
        late_by_benchmark.setdefault(measurement.benchmark, []).append(measurement.mean_ns)

    lines = [
        f"Warm-up split: {discard_rounds} discarded round(s) vs the pooled tail "
        "(a large positive gap means the run had not settled; the pooled delta cannot show this)",
    ]
    for benchmark in sorted(set(early_by_benchmark) | set(late_by_benchmark)):
        early_values = early_by_benchmark.get(benchmark)
        late_values = late_by_benchmark.get(benchmark)
        if not early_values or not late_values:
            lines.append(f"  {benchmark}: not present in both halves")
            continue
        early_median = statistics.median(early_values)
        late_median = statistics.median(late_values)
        lines.append(
            f"  {benchmark}: discarded {early_median:,.0f} ns vs pooled {late_median:,.0f} ns "
            f"({_percent(late_median, early_median):+.1f}% in the discarded rounds)"
        )
    return lines


def pool_by_position(measurements: Iterable[Measurement]) -> dict[tuple[str, str, int], float]:
    """Median of each (benchmark, arm, position) cell. Median, not mean: a single
    scheduling hiccup in one round should not move the cell."""
    buckets: dict[tuple[str, str, int], list[float]] = {}
    for m in measurements:
        buckets.setdefault((m.benchmark, m.arm, m.position), []).append(m.mean_ns)
    return {key: statistics.median(values) for key, values in buckets.items()}


def evaluate(
    measurements: Iterable[Measurement],
    arm_a: str,
    arm_b: str,
    threshold_percent: float = SIGNIFICANCE_THRESHOLD_PERCENT,
) -> list[Verdict]:
    """
    Per benchmark: compute B-vs-A at each position, and report a pooled delta only when
    the two positions agree in sign.

    Sign agreement is the whole gate. The position effect is large enough to invert a
    real difference, so a delta that changes direction depending on which arm ran first
    is not a measurement of the code — it is a measurement of the ordering.
    """
    pooled = pool_by_position(measurements)
    benchmarks = sorted({m.benchmark for m in measurements})
    counts: dict[tuple[str, str, int], int] = {}
    for m in measurements:
        key = (m.benchmark, m.arm, m.position)
        counts[key] = counts.get(key, 0) + 1

    verdicts: list[Verdict] = []
    for benchmark in benchmarks:
        cells = {
            (arm, position): pooled.get((benchmark, arm, position))
            for arm in (arm_a, arm_b)
            for position in (0, 1)
        }

        missing = [
            f"{arm}@position{position + 1}"
            for (arm, position), value in cells.items()
            if value is None
        ]
        if missing:
            verdicts.append(Verdict(
                benchmark, None, None, None, False,
                "not measured in every (arm, position) cell — interleaving incomplete: "
                + ", ".join(missing),
            ))
            continue

        a_first, a_second = cells[(arm_a, 0)], cells[(arm_a, 1)]
        b_first, b_second = cells[(arm_b, 0)], cells[(arm_b, 1)]
        assert a_first is not None and a_second is not None
        assert b_first is not None and b_second is not None

        # Each comparison is one round's ACTUAL ordering: A-first-vs-B-second is what the
        # even rounds ran, A-second-vs-B-first is what the odd rounds ran. Both orderings are
        # therefore represented, and a position bias enters the two with opposite sign — which
        # is what the sign test below detects and the geometric pooling below cancels. Pairing
        # same-position cells instead would keep the bias in with a single sign.
        delta_first = _percent(a_first, b_second)
        delta_second = _percent(a_second, b_first)

        # Pooled GEOMETRICALLY, not by averaging the two percentages. Percent change is
        # asymmetric — 100→92 is -8.0% but 92→100 is +8.7% — so the arithmetic mean of two
        # equal-and-opposite ratios is not zero, and a pure position artifact would pool to a
        # small nonzero "delta". The geometric mean of the ratios makes the null control exactly
        # zero, which is what a null control has to be.
        pooled_delta = _geometric_percent(b_second / a_first, b_first / a_second)
        samples = {
            f"{arm}@position{position + 1}": counts.get((benchmark, arm, position), 0)
            for arm in (arm_a, arm_b)
            for position in (0, 1)
        }

        if (delta_first > 0) != (delta_second > 0):
            verdicts.append(Verdict(
                benchmark, delta_first, delta_second, pooled_delta, False,
                f"position-dominated: {delta_first:+.1f}% when {arm_b} ran second, "
                f"{delta_second:+.1f}% when {arm_b} ran first. The two orderings disagree on "
                "the SIGN, so there is no delta to report",
                samples,
            ))
            continue

        if abs(pooled_delta) < threshold_percent:
            verdicts.append(Verdict(
                benchmark, delta_first, delta_second, pooled_delta, False,
                f"below the {threshold_percent:.0f}% floor "
                f"(pooled {pooled_delta:+.1f}%) — inside the range the position artifact "
                "alone can produce",
                samples,
            ))
            continue

        verdicts.append(Verdict(
            benchmark, delta_first, delta_second, pooled_delta, True,
            "both positions agree in sign and the pooled delta clears the floor",
            samples,
        ))

    return verdicts


def _percent(baseline_ns: float, candidate_ns: float) -> float:
    """Signed percent change from *baseline_ns* to *candidate_ns*; positive = slower."""
    if baseline_ns == 0:
        return 0.0
    return (candidate_ns - baseline_ns) / baseline_ns * 100.0


def _geometric_percent(*ratios: float) -> float:
    """Percent change corresponding to the geometric mean of *ratios* (1.0 = no change)."""
    product = 1.0
    for ratio in ratios:
        product *= ratio
    return (product ** (1.0 / len(ratios)) - 1.0) * 100.0


# --------------------------------------------------------------------------------------
# Worktree preparation and running
# --------------------------------------------------------------------------------------


#: Repo wrapper that serialises dotnet invocations behind an exclusive lock. Concurrent
#: `dotnet build`/`test` runs consume 5-10 GB each and OOM the machine, so the repo forbids
#: raw `dotnet` — and a benchmark orchestrator has a second reason to care: another dotnet
#: process competing for cores is exactly the contention that makes a timing meaningless.
SERIALIZED_DOTNET = os.path.join(".claude", "scripts", "dotnet-serialized")


def _wrapper_path(repo_root: str) -> Optional[str]:
    """The serialising wrapper, or None when this checkout has no executable copy."""
    wrapper = os.path.join(repo_root, SERIALIZED_DOTNET)
    return wrapper if os.access(wrapper, os.X_OK) else None


def _dotnet_command(repo_root: str, args: list[str]) -> list[str]:
    """Prefix a dotnet invocation with the serialising wrapper when the repo provides one."""
    wrapper = _wrapper_path(repo_root)
    return [wrapper] + args if wrapper else ["dotnet"] + args


def acquire_round_lock(repo_root: str) -> bool:
    """
    Hold the dotnet mutex across a whole (A,B) pair (#1420). Returns False when this checkout
    has no wrapper to hold.

    One lock per INVOCATION leaves the mutex free between the two arms a round compares, so a
    queued peer's 15-minute suite can land in the gap. The peer cannot overlap arm B — arm B
    would queue behind it — but the two arms then get measured in different machine states,
    which is precisely the artifact this orchestrator exists to cancel. Holding across the pair
    closes that. The lock is released BETWEEN rounds: holding it for a whole multi-round
    orchestration would starve every peer for an hour, which is the same failure from the other
    side.
    """
    wrapper = _wrapper_path(repo_root)
    if wrapper is None:
        return False

    result = subprocess.run([wrapper, "--acquire-lock", str(os.getpid())])
    if result.returncode == 125:
        raise RuntimeError(
            "the dotnet lock path is not writable from this shell — it is sandboxed. Re-run "
            "with the sandbox disabled. Do NOT fall back to unserialised dotnet: concurrent "
            "runs cost 5-10 GB each and OOM the machine.")
    if result.returncode != 0:
        raise RuntimeError(f"could not acquire the dotnet lock (exit {result.returncode})")
    return True


def release_round_lock(repo_root: str) -> None:
    """Release a lock taken by :func:`acquire_round_lock`. Safe to call when already free."""
    wrapper = _wrapper_path(repo_root)
    if wrapper is not None:
        subprocess.run([wrapper, "--release-lock", str(os.getpid())])


def lock_generation(repo_root: str) -> Optional[int]:
    """
    The wrapper's monotonic count of lock acquisitions, or None when unavailable.

    Read before and after a round, it answers "did anyone else acquire while I was measuring?".
    With the hold above in place a wrapper-using peer CANNOT interleave, so in the happy path
    this can never fire — it is kept as a defence against the hold failing (an old wrapper copy
    without --acquire-lock, exit 125 in a sandboxed shell, a crash between arms). It also cannot
    see contamination from anything that bypasses the wrapper entirely, so a quiet generation is
    not proof of a quiet machine. Note that the stale-lock steal which used to make this fire in
    practice was fixed alongside it: the lock now survives an orphaned child and a pidless
    directory, which removes the one mechanism by which a wrapper-using peer could jump a held
    lock.
    """
    wrapper = _wrapper_path(repo_root)
    if wrapper is None:
        return None
    result = subprocess.run([wrapper, "--lock-generation"], capture_output=True, text=True)
    try:
        return int(result.stdout.strip())
    except (ValueError, AttributeError):
        return None


def _run(command: list[str], cwd: Optional[str] = None) -> None:
    result = subprocess.run(command, cwd=cwd)
    if result.returncode != 0:
        raise RuntimeError(f"command failed ({result.returncode}): {' '.join(command)}")


def prepare_worktree(repo_root: str, ref: str, destination: str) -> str:
    """
    Create a detached worktree at *ref* and build Sharpy.Compiler.Benchmarks in Release.

    Restore is explicit and the build is ``--no-restore``, mirroring ``benchmarks.yml``. That is
    not stylistic: letting ``dotnet build`` restore implicitly resolves
    ``Microsoft.CodeAnalysis.CSharp`` to BenchmarkDotNet's 4.14.0 rather than Sharpy.Compiler's
    5.6.0 and the build dies with CS1705. A warm ``obj/`` hides it, so it only appears on a fresh
    checkout — which is exactly what every worktree is.
    """
    _run(["git", "worktree", "add", "--detach", destination, ref], cwd=repo_root)

    # cwd INSIDE the worktree, project paths RELATIVE. Measured, not stylistic: with an absolute
    # project path from outside, restore produces a graph in which the Benchmarks compile binds
    # BenchmarkDotNet's Microsoft.CodeAnalysis.CSharp 4.14.0 instead of Sharpy.Compiler's 5.6.0,
    # and the build dies with CS1705. On macOS the tempdir is /var/... symlinked to /private/var/...,
    # and the successful invocation is the one where the path resolves before MSBuild sees it.
    # Both failing spellings restore "successfully" first, so the symptom lands on the build.
    worktree = os.path.realpath(destination)
    project = os.path.join("src", "Sharpy.Compiler.Benchmarks")
    _run(_dotnet_command(repo_root, [
        "restore", os.path.join(project, "Sharpy.Compiler.Benchmarks.csproj")]), cwd=worktree)
    _run(_dotnet_command(repo_root, [
        "build", "-c", "Release", "--no-restore", project]), cwd=worktree)
    return worktree


def run_arm(
    repo_root: str, worktree: str, artifacts: str, bdn_filter: str, job: Optional[str] = None,
    lock_held: bool = False,
) -> dict[str, float]:
    """
    Run one arm's benchmarks and return benchmark -> mean ns.

    When *lock_held* is true the caller already owns the mutex for this whole round, so dotnet is
    invoked RAW — going through the wrapper here would deadlock against the lock we are holding.
    """
    args = [
        "run", "--project", os.path.join("src", "Sharpy.Compiler.Benchmarks"),
        "-c", "Release", "--no-build", "--",
        "--filter", bdn_filter, "--exporters", "json", "--artifacts", artifacts,
    ]
    # Same relative-path-from-inside-the-worktree rule as prepare_worktree.
    command = ["dotnet"] + args if lock_held else _dotnet_command(repo_root, args)
    # A shorter BenchmarkDotNet job trades per-run precision for more rounds. That is the
    # right trade here: the thing being averaged out is run POSITION, and more A/B pairs
    # cancel it better than longer individual runs do.
    if job:
        command += ["--job", job]
    _run(command, cwd=worktree)
    return parse_bdn_means(artifacts)


def orchestrate(
    repo_root: str,
    ref_a: str,
    ref_b: str,
    rounds: int,
    bdn_filter: str,
    workdir: str,
    job: Optional[str] = None,
    discard_rounds: int = 0,
) -> list[Measurement]:
    """
    Alternate the arms so each is measured in each position an equal number of times.

    Round parity decides which arm goes first, so over an even number of rounds every
    (arm, position) cell gets the same count. An odd round count leaves one cell short
    and ``evaluate`` will say so rather than pooling an unbalanced comparison.

    Runs ``discard_rounds + rounds`` rounds and tags each measurement with its round index;
    the opening ``discard_rounds`` are dropped before pooling (#1418). They are still RUN —
    the transient is a property of the machine warming up, so the only way to get past it is
    to spend the invocations.
    """
    worktrees = {
        ref_a: prepare_worktree(repo_root, ref_a, os.path.join(workdir, "arm_a")),
        ref_b: prepare_worktree(repo_root, ref_b, os.path.join(workdir, "arm_b")),
    }

    measurements: list[Measurement] = []
    total_rounds = discard_rounds + rounds
    for round_index in range(total_rounds):
        order = (ref_a, ref_b) if round_index % 2 == 0 else (ref_b, ref_a)
        disposition = "discarded" if round_index < discard_rounds else "pooled"

        # One acquisition per ROUND, not per invocation, so the two compared arms cannot be
        # separated by a peer's run; released in `finally` between rounds so peers can schedule
        # (and so an exception here cannot wedge every agent for the 45-minute timeout).
        lock_held = acquire_round_lock(repo_root)
        round_measurements: list[Measurement] = []
        try:
            generation_before = lock_generation(repo_root)
            for position, ref in enumerate(order):
                artifacts = os.path.join(workdir, f"artifacts_r{round_index}_p{position}")
                print(f"[bench_ab] round {round_index + 1}/{total_rounds} ({disposition}), "
                      f"position {position + 1}: {ref}", flush=True)
                arm_means = run_arm(repo_root, worktrees[ref], artifacts, bdn_filter, job,
                                    lock_held=lock_held)
                for benchmark, mean_ns in arm_means.items():
                    round_measurements.append(
                        Measurement(ref, position, benchmark, mean_ns, round_index))
            generation_after = lock_generation(repo_root)
        finally:
            if lock_held:
                release_round_lock(repo_root)

        if round_was_interrupted(generation_before, generation_after):
            # Someone acquired the lock between this round's two arms, so the arms were measured
            # either side of another dotnet run. Dropping the round is the same "this round is
            # not comparable" mechanism the discard window uses, which is why they share one
            # pooling path rather than two.
            print(f"[bench_ab] round {round_index + 1} DROPPED: the lock changed hands between "
                  f"its arms (generation {generation_before} -> {generation_after}), so the two "
                  "arms were measured in different machine states", flush=True)
            continue

        measurements.extend(round_measurements)

    return measurements


def cleanup_worktrees(repo_root: str, workdir: str) -> None:
    for name in ("arm_a", "arm_b"):
        path = os.path.join(workdir, name)
        if os.path.isdir(path):
            subprocess.run(["git", "worktree", "remove", "--force", path], cwd=repo_root)
    shutil.rmtree(workdir, ignore_errors=True)


# --------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------


def report(
    verdicts: list[Verdict],
    arm_a: str,
    arm_b: str,
    rounds: int,
    discard_rounds: int = 0,
    split_lines: Optional[list[str]] = None,
    dropped_rounds: int = 0,
) -> str:
    measured = [v for v in verdicts if v.measured]
    total_rounds = rounds + discard_rounds
    lines = [
        "",
        f"Interleaved A/B: {arm_a} (A) vs {arm_b} (B), {rounds} pooled round(s) of "
        f"{total_rounds} run, {total_rounds * 2} benchmark invocations "
        f"({discard_rounds * 2} discarded as unsettled)",
        f"{len(measured)} of {len(verdicts)} benchmark(s) produced a measurable delta.",
        "",
    ]
    if dropped_rounds:
        lines.insert(2, f"{dropped_rounds} round(s) DROPPED as interrupted — the lock changed "
                        "hands between their arms, so those arms were measured either side of "
                        "another dotnet run (#1420).")
    lines.extend(v.describe(arm_a, arm_b) for v in verdicts)
    if split_lines:
        lines.append("")
        lines.extend(split_lines)
    lines.extend([
        "",
        "A delta is reported only when both positions agree in sign AND the pooled "
        f"magnitude clears {SIGNIFICANCE_THRESHOLD_PERCENT:.0f}%. Run position alone moves "
        "wall clock 7-10% on this corpus (#1318), so anything smaller is unmeasured, not zero.",
        "The floor has NOT been re-derived since the discard window landed (#1418): settled "
        "rounds agree far more tightly, but lowering it needs an A/A run on a quiet machine. "
        "Deferred — see benchmarks/BASELINE.md.",
    ])
    return "\n".join(lines)


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m build_tools.bench_ab",
        description="Interleaved A/B compiler benchmarking with position sign agreement.",
    )
    parser.add_argument("ref_a", help="baseline git ref (A)")
    parser.add_argument("ref_b", help="candidate git ref (B)")
    parser.add_argument("--rounds", type=int, default=DEFAULT_ROUNDS,
                        help=f"A/B round pairs to POOL (default {DEFAULT_ROUNDS}); "
                             "an EVEN count balances the positions")
    parser.add_argument("--discard-rounds", type=int, default=DEFAULT_DISCARD_ROUNDS,
                        help=f"opening rounds run but not pooled (default "
                             f"{DEFAULT_DISCARD_ROUNDS}); the machine needs ~8 invocations to "
                             "settle (#1418). 0 pools everything and prints the split anyway")
    parser.add_argument("--filter", dest="bdn_filter", default=DEFAULT_FILTER,
                        help=f"BenchmarkDotNet --filter (default {DEFAULT_FILTER!r})")
    parser.add_argument("--job", default=None,
                        help="BenchmarkDotNet --job (e.g. short, medium); omit for the default. "
                             "A shorter job buys more rounds, which is what cancels position.")
    parser.add_argument("--keep-worktrees", action="store_true",
                        help="leave the prepared worktrees in place for inspection")
    args = parser.parse_args(argv)

    if args.rounds < 2:
        print("--rounds must be at least 2: one round measures each arm in exactly one "
              "position, which is the sequential comparison this tool replaces.",
              file=sys.stderr)
        return 2
    if args.rounds % 2 == 1:
        print(f"[bench_ab] note: {args.rounds} rounds leaves the positions unbalanced; "
              "an even count gives every (arm, position) cell the same sample count.",
              file=sys.stderr)
    if args.discard_rounds < 0:
        print("--discard-rounds cannot be negative.", file=sys.stderr)
        return 2

    repo_root = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        capture_output=True, text=True, check=True).stdout.strip()

    workdir = tempfile.mkdtemp(prefix="sharpy_bench_ab_")
    try:
        measurements = orchestrate(
            repo_root, args.ref_a, args.ref_b, args.rounds, args.bdn_filter, workdir, args.job,
            discard_rounds=args.discard_rounds)
        # Pool the settled tail only; the discarded rounds still reach the reader, as the
        # early/late split, because "this run had not settled" is a conclusion no pooled
        # number can express (#1418).
        _, pooled = split_discarded(measurements, args.discard_rounds)
        # A round dropped as interrupted never reaches `measurements`, so the gap in the
        # retained round indices is what counts them — reported rather than silently shrinking
        # the sample.
        expected_rounds = set(range(args.discard_rounds, args.discard_rounds + args.rounds))
        dropped = len(expected_rounds - {m.round_index for m in pooled})
        verdicts = evaluate(pooled, args.ref_a, args.ref_b)
        print(report(verdicts, args.ref_a, args.ref_b, args.rounds,
                     discard_rounds=args.discard_rounds,
                     split_lines=warmup_split_lines(measurements, args.discard_rounds),
                     dropped_rounds=dropped))
    finally:
        if not args.keep_worktrees:
            cleanup_worktrees(repo_root, workdir)
        else:
            print(f"[bench_ab] worktrees kept under {workdir}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
