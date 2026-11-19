#!/usr/bin/env python3
"""
Allocation-regression gate for the Sharpy compiler benchmarks.

BenchmarkDotNet's ``[MemoryDiagnoser]`` reports allocated bytes per operation for every
benchmark. This gate compares the ``Allocated`` column of a fresh BenchmarkDotNet JSON export
against a checked-in baseline (``benchmarks/allocation-baseline.json``).

Allocated bytes are NOT exact. Identical code measures differently from run to run: about 1.2%
between two CI runs of the same tree (5bb23cba4 vs its mainline merge 45829650b, 2026-11-19) and
up to 1.6% between two local runs (#2759). So the gate has three bands, both widths stored in
the baseline:

* within ``threshold`` (default 10%) over the baseline: OK;
* over ``threshold`` but within ``threshold + noise`` (default 2%): passes with a warning, because
  a run-to-run wobble alone can land there;
* over ``threshold + noise``: a regression, the gate fails.

CI only (#2462). Allocation also differs between machines: the same tree read 10-16% over the
baseline locally while CI read it green. So the baseline is measured on the CI runners
(``.github/workflows/benchmarks.yml``) and ``check`` refuses to run anywhere else
(``GITHUB_ACTIONS`` unset). To compare two refs locally, measure both on the same machine
instead (``build_tools/bench_ab.py`` for wall clock; two ``--exporters json`` runs for bytes).

The baseline is a *deliberate* artifact, mirroring the ``.expected.cs`` snapshot discipline: it
is only updated on purpose, via ``update``, from CI runs' ``benchmark-results-N`` artifacts, and
the change is reviewed like any other. With several result directories, ``update`` takes the
median per benchmark. A silent "regenerate to green" is exactly what this gate exists to
prevent. ``check`` prints every benchmark's bytes, so a CI log alone also records a run.

Usage::

    # In CI: fail (exit 1) on a regression, warn inside the noise band:
    python -m build_tools.allocation_gate check \\
        --baseline benchmarks/allocation-baseline.json --results BenchmarkResults

    # Deliberately refresh the baseline from downloaded CI artifacts (review the diff!):
    python -m build_tools.allocation_gate update \\
        --baseline benchmarks/allocation-baseline.json \\
        --results run1/ --results run2/ --results run3/
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import sys
from dataclasses import dataclass
from typing import Optional

# Benchmarks may legitimately allocate more, up to this fraction over baseline,
# before the gate fails. 0.10 == 10%.
DEFAULT_THRESHOLD = 0.10

# Run-to-run spread of identical code, as a fraction of the baseline; a result over the
# threshold by no more than this passes with a warning (#2759).
DEFAULT_NOISE = 0.02

# The platform the baseline is measured on, recorded in the baseline document (#2462).
CI_PLATFORM = "GitHub-hosted ubuntu-latest runner, .github/workflows/benchmarks.yml"


@dataclass(frozen=True)
class Regression:
    """A single benchmark whose allocation exceeded the allowed threshold."""

    name: str
    baseline_bytes: int
    current_bytes: int

    @property
    def delta_bytes(self) -> int:
        return self.current_bytes - self.baseline_bytes

    @property
    def ratio(self) -> float:
        # Fractional increase over baseline; +inf when the baseline was zero.
        if self.baseline_bytes == 0:
            return float("inf")
        return self.delta_bytes / self.baseline_bytes


def _report_patterns(results_dir: str) -> list[str]:
    """Glob patterns for BenchmarkDotNet JSON reports, most-specific first."""
    names = ["*-report-full-compressed.json", "*-report-full.json", "*-report.json"]
    return [os.path.join(results_dir, "results", n) for n in names] + [
        os.path.join(results_dir, n) for n in names
    ]


def parse_bdn_results(results_dir: str) -> dict[str, int]:
    """
    Parse BenchmarkDotNet ``*-report-full.json`` exports under ``results_dir`` and
    return a mapping of benchmark full name -> allocated bytes per operation.

    BenchmarkDotNet's ``json`` exporter writes ``<results_dir>/results/*-report-full-compressed.json``
    (and, with ``jsonfull``, ``*-report-full.json``); for robustness we also accept the brief
    ``*-report.json`` and report JSON placed directly in ``results_dir``. Benchmarks without a
    ``Memory`` section (MemoryDiagnoser disabled) are skipped.
    """
    patterns = _report_patterns(results_dir)
    files: list[str] = []
    for pattern in patterns:
        files.extend(glob.glob(pattern))
    # De-duplicate while preserving order (full and brief reports overlap).
    seen: set[str] = set()
    ordered_files = [f for f in files if not (f in seen or seen.add(f))]

    if not ordered_files:
        raise FileNotFoundError(
            f"No BenchmarkDotNet report JSON found under {results_dir!r} "
            "(looked for results/*-report-full.json). Did the benchmark run with "
            "--exporters json?"
        )

    allocations: dict[str, int] = {}
    for path in ordered_files:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
        for bench in data.get("Benchmarks", []):
            name = bench.get("FullName") or bench.get("Method")
            memory = bench.get("Memory")
            if name is None or not memory:
                continue
            allocated = memory.get("BytesAllocatedPerOperation")
            if allocated is None:
                continue
            # Later reports win; full and brief agree on Allocated anyway.
            allocations[name] = int(allocated)
    return allocations


def load_baseline(baseline_path: str) -> dict[str, int]:
    """Load the checked-in baseline; return benchmark full name -> allocated bytes."""
    with open(baseline_path, encoding="utf-8") as handle:
        data = json.load(handle)
    return {
        name: int(entry["allocated_bytes"])
        for name, entry in data["benchmarks"].items()
    }


def load_limits(baseline_path: str) -> tuple[float, float]:
    """Return the baseline document's (threshold, noise), defaulting each when absent."""
    with open(baseline_path, encoding="utf-8") as handle:
        data = json.load(handle)
    return (
        float(data.get("threshold", DEFAULT_THRESHOLD)),
        float(data.get("noise", DEFAULT_NOISE)),
    )


def find_regressions(
    baseline: dict[str, int],
    current: dict[str, int],
    threshold: float = DEFAULT_THRESHOLD,
    noise: float = 0.0,
) -> list[Regression]:
    """
    Return the benchmarks that grew by more than ``threshold + noise`` (fractions).
    Only benchmarks present in *both* the baseline and the current run are checked;
    new/missing benchmarks are surfaced separately by :func:`describe_coverage`.
    A benchmark exactly at the limit (e.g. +12.0% with 10% and 2%) is within tolerance.
    """
    regressions: list[Regression] = []
    for name, baseline_bytes in baseline.items():
        if name not in current:
            continue
        current_bytes = current[name]
        allowed = baseline_bytes * (1.0 + threshold + noise)
        if current_bytes > allowed:
            regressions.append(Regression(name, baseline_bytes, current_bytes))
    return regressions


def find_noise_band(
    baseline: dict[str, int],
    current: dict[str, int],
    threshold: float = DEFAULT_THRESHOLD,
    noise: float = DEFAULT_NOISE,
) -> list[Regression]:
    """Return the benchmarks over ``threshold`` but within ``threshold + noise``: warn, not fail."""
    over_threshold = find_regressions(baseline, current, threshold, 0.0)
    failing = {r.name for r in find_regressions(baseline, current, threshold, noise)}
    return [r for r in over_threshold if r.name not in failing]


def describe_coverage(
    baseline: dict[str, int], current: dict[str, int]
) -> tuple[list[str], list[str]]:
    """Return (missing_from_current, new_in_current) benchmark names, sorted."""
    missing = sorted(name for name in baseline if name not in current)
    new = sorted(name for name in current if name not in baseline)
    return missing, new


def build_baseline_document(
    current: dict[str, int],
    threshold: float,
    descriptions: Optional[dict[str, str]] = None,
    noise: float = DEFAULT_NOISE,
) -> dict:
    """Build the JSON document for a refreshed baseline."""
    descriptions = descriptions or {}
    benchmarks: dict[str, dict] = {}
    for name in sorted(current):
        entry: dict = {"allocated_bytes": current[name]}
        if name in descriptions:
            entry["description"] = descriptions[name]
        benchmarks[name] = entry
    return {
        "_comment": (
            "Allocation-regression baseline (BenchmarkDotNet [MemoryDiagnoser] "
            "BytesAllocatedPerOperation), measured in CI only (#2462). Deliberately updated "
            "only via `python -m build_tools.allocation_gate update` from CI runs' "
            "benchmark-results artifacts (the median of several); review the diff like a "
            "snapshot. Over `threshold` warns while within `threshold + noise`, then fails."
        ),
        "measured_on": CI_PLATFORM,
        "threshold": threshold,
        "noise": noise,
        "benchmarks": benchmarks,
    }


def median_allocations(runs: list[dict[str, int]]) -> dict[str, int]:
    """Per benchmark, the median of its bytes over the runs that measured it (lower middle)."""
    names = sorted({name for run in runs for name in run})
    medians: dict[str, int] = {}
    for name in names:
        values = sorted(run[name] for run in runs if name in run)
        medians[name] = values[(len(values) - 1) // 2]
    return medians


def parse_descriptions(results_dir: str) -> dict[str, str]:
    """Best-effort map of benchmark full name -> human description (from BDN reports)."""
    descriptions: dict[str, str] = {}
    for pattern in _report_patterns(results_dir):
        for path in glob.glob(pattern):
            with open(path, encoding="utf-8") as handle:
                data = json.load(handle)
            for bench in data.get("Benchmarks", []):
                name = bench.get("FullName") or bench.get("Method")
                display = bench.get("MethodTitle") or bench.get("DisplayInfo")
                if name and display:
                    # BenchmarkDotNet wraps the [Benchmark(Description=…)] title in single quotes.
                    descriptions[name] = display.strip().strip("'")
    return descriptions


def _cmd_check(args: argparse.Namespace) -> int:
    if os.environ.get("GITHUB_ACTIONS") != "true":
        print(
            "allocation_gate check runs in CI only (#2462): the baseline is measured on "
            f"{CI_PLATFORM}, and allocation differs between machines. To compare two refs "
            "locally, measure both on this machine (two `--exporters json` runs).",
            file=sys.stderr,
        )
        return 2

    baseline = load_baseline(args.baseline)
    current = parse_bdn_results(args.results)
    doc_threshold, doc_noise = load_limits(args.baseline)
    threshold = doc_threshold if args.threshold is None else args.threshold
    noise = doc_noise if args.noise is None else args.noise

    regressions = find_regressions(baseline, current, threshold, noise)
    warnings = find_noise_band(baseline, current, threshold, noise)
    missing, new = describe_coverage(baseline, current)

    print(f"{'benchmark':<60} {'baseline':>12} {'current':>12} {'delta':>8}")
    for name in sorted(n for n in baseline if n in current):
        delta = (current[name] - baseline[name]) / baseline[name] if baseline[name] else float("inf")
        print(f"{name:<60} {baseline[name]:>12,} {current[name]:>12,} {delta:>+8.1%}")
    print()

    for name in new:
        print(
            f"note: new benchmark not in baseline (not gated): {name} "
            f"= {current[name]:,} B — run `update` to add it"
        )
    for name in missing:
        print(f"warning: baseline benchmark absent from this run: {name}")

    for reg in sorted(warnings, key=lambda r: r.ratio, reverse=True):
        # A GitHub Actions annotation: visible on the run without failing it.
        print(
            f"::warning::{reg.name} is +{reg.ratio:.1%} over its allocation baseline: past the "
            f"{threshold:.0%} threshold but within its {noise:.0%} noise band"
        )

    if regressions:
        print()
        print(
            f"ALLOCATION REGRESSION: {len(regressions)} benchmark(s) exceeded "
            f"the {threshold:.0%} threshold plus {noise:.0%} noise:"
        )
        for reg in sorted(regressions, key=lambda r: r.ratio, reverse=True):
            print(f"  {reg.name}")
            print(
                f"    baseline={reg.baseline_bytes:,} B  current={reg.current_bytes:,} B  "
                f"(+{reg.delta_bytes:,} B, +{reg.ratio:.1%})"
            )
        print()
        print("If this increase is intentional, refresh the baseline deliberately from CI runs:")
        print(
            "  python -m build_tools.allocation_gate update "
            f"--baseline {args.baseline} --results <run1> --results <run2> --results <run3>"
        )
        return 1

    checked = sum(1 for name in baseline if name in current)
    print(
        f"OK: {checked} benchmark(s) within the {threshold:.0%} allocation threshold "
        f"plus {noise:.0%} noise ({len(warnings)} in the noise band)."
    )
    return 0


def _cmd_update(args: argparse.Namespace) -> int:
    runs = [parse_bdn_results(results) for results in args.results]
    current = median_allocations(runs)
    descriptions: dict[str, str] = {}
    for results in args.results:
        descriptions.update(parse_descriptions(results))
    threshold = DEFAULT_THRESHOLD if args.threshold is None else args.threshold
    noise = DEFAULT_NOISE if args.noise is None else args.noise
    document = build_baseline_document(current, threshold, descriptions, noise)
    with open(args.baseline, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")
    print(
        f"Wrote baseline for {len(current)} benchmark(s) to {args.baseline} "
        f"(median of {len(runs)} run(s))"
    )
    return 0


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m build_tools.allocation_gate",
        description="Compare BenchmarkDotNet Allocated columns against a checked-in baseline.",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    for name, help_text in (
        ("check", "fail if any benchmark regressed (CI only)"),
        ("update", "refresh the baseline from a fresh export"),
    ):
        p = sub.add_parser(name, help=help_text)
        p.add_argument(
            "--baseline", required=True, help="path to allocation-baseline.json"
        )
        if name == "check":
            p.add_argument("--results", required=True, help="BenchmarkDotNet artifacts dir")
        else:
            p.add_argument(
                "--results",
                required=True,
                action="append",
                help="a CI run's BenchmarkDotNet artifacts dir; repeat for a median",
            )
        p.add_argument(
            "--threshold",
            type=float,
            default=None,
            help=f"growth threshold as a fraction (default: the baseline's, else {DEFAULT_THRESHOLD})",
        )
        p.add_argument(
            "--noise",
            type=float,
            default=None,
            help=f"noise band above the threshold (default: the baseline's, else {DEFAULT_NOISE})",
        )

    args = parser.parse_args(argv)
    if args.command == "check":
        return _cmd_check(args)
    if args.command == "update":
        return _cmd_update(args)
    parser.error(f"unknown command {args.command!r}")
    return 2  # unreachable


if __name__ == "__main__":
    sys.exit(main())
