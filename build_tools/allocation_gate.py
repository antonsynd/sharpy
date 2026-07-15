#!/usr/bin/env python3
"""
Allocation-regression gate for the Sharpy compiler benchmarks.

BenchmarkDotNet's ``[MemoryDiagnoser]`` reports allocated bytes per operation for
every benchmark. Allocated bytes are measured *precisely* (not statistically), so
they are stable across runs and machines and make a good regression signal. This
gate compares the ``Allocated`` column of a fresh BenchmarkDotNet JSON export against
a checked-in baseline (``benchmarks/allocation-baseline.json``) and fails when any
benchmark regresses by more than a threshold (default 10%).

The baseline is a *deliberate* artifact, mirroring the ``.expected.cs`` snapshot
discipline: it is only updated on purpose, via the ``update`` subcommand, and the
change is reviewed like any other. A silent "regenerate to green" is exactly what
this gate exists to prevent.

Usage::

    # Fail (exit 1) if any benchmark's Allocated regressed > 10% vs the baseline:
    python -m build_tools.allocation_gate check \\
        --baseline benchmarks/allocation-baseline.json \\
        --results BenchmarkResults

    # Deliberately refresh the baseline from a fresh export (review the diff!):
    python -m build_tools.allocation_gate update \\
        --baseline benchmarks/allocation-baseline.json \\
        --results BenchmarkResults

To produce the export, run the compiler benchmarks with the JSON exporter (a short
job is sufficient — allocation is precise regardless of job length)::

    dotnet run -c Release --project src/Sharpy.Compiler.Benchmarks -- \\
        --filter "*CompilerBenchmarks*" --job short --exporters json \\
        --artifacts BenchmarkResults
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
    return {name: int(entry["allocated_bytes"]) for name, entry in data["benchmarks"].items()}


def find_regressions(
    baseline: dict[str, int],
    current: dict[str, int],
    threshold: float = DEFAULT_THRESHOLD,
) -> list[Regression]:
    """
    Return the benchmarks that regressed by more than ``threshold`` (a fraction).
    Only benchmarks present in *both* the baseline and the current run are checked;
    new/missing benchmarks are surfaced separately by :func:`describe_coverage`.
    A benchmark exactly at the threshold (e.g. +10.0%) is within tolerance.
    """
    regressions: list[Regression] = []
    for name, baseline_bytes in baseline.items():
        if name not in current:
            continue
        current_bytes = current[name]
        allowed = baseline_bytes * (1.0 + threshold)
        if current_bytes > allowed:
            regressions.append(Regression(name, baseline_bytes, current_bytes))
    return regressions


def describe_coverage(baseline: dict[str, int], current: dict[str, int]) -> tuple[list[str], list[str]]:
    """Return (missing_from_current, new_in_current) benchmark names, sorted."""
    missing = sorted(name for name in baseline if name not in current)
    new = sorted(name for name in current if name not in baseline)
    return missing, new


def build_baseline_document(
    current: dict[str, int],
    threshold: float,
    descriptions: Optional[dict[str, str]] = None,
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
            "BytesAllocatedPerOperation). Deliberately updated only via "
            "`python -m build_tools.allocation_gate update`; review the diff like a snapshot."
        ),
        "threshold": threshold,
        "benchmarks": benchmarks,
    }


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
    baseline = load_baseline(args.baseline)
    current = parse_bdn_results(args.results)
    threshold = args.threshold

    regressions = find_regressions(baseline, current, threshold)
    missing, new = describe_coverage(baseline, current)

    for name in new:
        print(f"note: new benchmark not in baseline (not gated): {name} "
              f"= {current[name]:,} B — run `update` to add it")
    for name in missing:
        print(f"warning: baseline benchmark absent from this run: {name}")

    if regressions:
        print()
        print(f"ALLOCATION REGRESSION: {len(regressions)} benchmark(s) exceeded "
              f"the {threshold:.0%} threshold:")
        for reg in sorted(regressions, key=lambda r: r.ratio, reverse=True):
            print(f"  {reg.name}")
            print(f"    baseline={reg.baseline_bytes:,} B  current={reg.current_bytes:,} B  "
                  f"(+{reg.delta_bytes:,} B, +{reg.ratio:.1%})")
        print()
        print("If this increase is intentional, refresh the baseline deliberately:")
        print("  python -m build_tools.allocation_gate update "
              f"--baseline {args.baseline} --results {args.results}")
        return 1

    checked = sum(1 for name in baseline if name in current)
    print(f"OK: {checked} benchmark(s) within the {threshold:.0%} allocation threshold.")
    return 0


def _cmd_update(args: argparse.Namespace) -> int:
    current = parse_bdn_results(args.results)
    descriptions = parse_descriptions(args.results)
    document = build_baseline_document(current, args.threshold, descriptions)
    with open(args.baseline, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")
    print(f"Wrote baseline for {len(current)} benchmark(s) to {args.baseline}")
    return 0


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m build_tools.allocation_gate",
        description="Compare BenchmarkDotNet Allocated columns against a checked-in baseline.",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    for name, help_text in (("check", "fail if any benchmark regressed"),
                            ("update", "refresh the baseline from a fresh export")):
        p = sub.add_parser(name, help=help_text)
        p.add_argument("--baseline", required=True, help="path to allocation-baseline.json")
        p.add_argument("--results", required=True, help="BenchmarkDotNet artifacts dir")
        p.add_argument("--threshold", type=float, default=DEFAULT_THRESHOLD,
                       help=f"regression threshold as a fraction (default {DEFAULT_THRESHOLD})")

    args = parser.parse_args(argv)
    if args.command == "check":
        return _cmd_check(args)
    if args.command == "update":
        return _cmd_update(args)
    parser.error(f"unknown command {args.command!r}")
    return 2  # unreachable


if __name__ == "__main__":
    sys.exit(main())
