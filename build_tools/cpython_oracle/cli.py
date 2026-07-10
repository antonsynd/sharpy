"""Command-line entry point for the CPython test-suite classifier.

Examples
--------
    # Classify one file (resolved from a pinned CPython 3.12 install):
    python -m build_tools.cpython_oracle classify test_bisect

    # Classify several, writing markdown reports + skeletons to disk:
    python -m build_tools.cpython_oracle classify test_bisect test_colorsys \
        --report-dir build_tools/cpython_oracle/reports \
        --skeleton-dir build_tools/cpython_oracle/skeletons

    # Point at a specific CPython 3.12 test tree:
    python -m build_tools.cpython_oracle classify test_int \
        --cpython-lib /opt/homebrew/.../python3.12/test
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Optional

from . import ledger as ledger_mod
from . import report as report_mod
from . import skeleton as skeleton_mod
from .classifier import classify_file
from .oracle_sources import PINNED_MAJOR_MINOR, guidance, resolve_test_file


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="python -m build_tools.cpython_oracle",
        description="Classify CPython Lib/test test methods for Sharpy porting (#1030).",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    clf = sub.add_parser("classify", help="Classify one or more CPython test files.")
    clf.add_argument(
        "targets",
        nargs="+",
        help="Module names (test_bisect / bisect) or paths to CPython test_*.py files.",
    )
    clf.add_argument("--cpython-lib", help="Path to a CPython 3.12 Lib/test directory.")
    clf.add_argument(
        "--format",
        choices=["md", "yaml"],
        default="md",
        help="Report format (default: md).",
    )
    clf.add_argument(
        "--report-dir",
        help="Write a per-module report file into this directory instead of stdout.",
    )
    clf.add_argument(
        "--skeleton-dir",
        help="Emit a .spy skeleton for the PORTABLE methods of each module here.",
    )
    clf.add_argument(
        "--summary-only",
        action="store_true",
        help="Print only the one-line yield summary per module.",
    )

    led = sub.add_parser(
        "ledger",
        help="Generate/verify the machine-readable divergence ledger (ledger.yaml).",
    )
    led.add_argument(
        "--deviations",
        help="Path to docs/deviations.yaml (default: repo copy).",
    )
    led.add_argument(
        "--ported-root",
        help="Root of the ported .spy tree to scan (default: Spy/cpython/).",
    )
    led.add_argument(
        "--ledger-path",
        help="Ledger file to write/verify (default: build_tools/cpython_oracle/ledger.yaml).",
    )
    mode = led.add_mutually_exclusive_group()
    mode.add_argument(
        "--write",
        action="store_true",
        help="Write the regenerated ledger to --ledger-path.",
    )
    mode.add_argument(
        "--check",
        action="store_true",
        help="Fail (exit 1) if the committed ledger differs from a fresh regeneration.",
    )
    return parser


def _run_classify(args: argparse.Namespace) -> int:
    report_dir = Path(args.report_dir) if args.report_dir else None
    skeleton_dir = Path(args.skeleton_dir) if args.skeleton_dir else None
    if report_dir:
        report_dir.mkdir(parents=True, exist_ok=True)
    if skeleton_dir:
        skeleton_dir.mkdir(parents=True, exist_ok=True)

    exit_code = 0
    for target in args.targets:
        path: Optional[Path] = resolve_test_file(target, args.cpython_lib)
        if path is None:
            print(f"error: could not resolve '{target}'.", file=sys.stderr)
            print(guidance(), file=sys.stderr)
            exit_code = 2
            continue

        rep = classify_file(path, cpython_version=PINNED_MAJOR_MINOR)
        print(report_mod.summary_line(rep))

        rendered = (
            report_mod.render_yaml(rep)
            if args.format == "yaml"
            else report_mod.render_markdown(rep)
        )
        if report_dir:
            ext = "yaml" if args.format == "yaml" else "md"
            out = report_dir / f"{rep.module}.{ext}"
            out.write_text(rendered, encoding="utf-8")
            print(f"  wrote {out}")
        elif not args.summary_only:
            print()
            print(rendered)

        if skeleton_dir:
            spy = skeleton_dir / f"{rep.module}_skeleton.spy"
            spy.write_text(skeleton_mod.render_skeleton(rep), encoding="utf-8")
            print(f"  wrote {spy}")

    return exit_code


def _run_ledger(args: argparse.Namespace) -> int:
    deviations_path = (
        Path(args.deviations) if args.deviations else ledger_mod.DEFAULT_DEVIATIONS_PATH
    )
    ported_root = (
        Path(args.ported_root) if args.ported_root else ledger_mod.DEFAULT_PORTED_ROOT
    )
    ledger_path = (
        Path(args.ledger_path) if args.ledger_path else ledger_mod.DEFAULT_LEDGER_PATH
    )

    try:
        data = ledger_mod.build_ledger(
            deviations_path=deviations_path,
            ported_root=ported_root,
            cpython_version=PINNED_MAJOR_MINOR,
        )
    except ledger_mod.LedgerError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    rendered = ledger_mod.render_ledger(data)
    n_dev = len(data["deviations"])
    n_entries = len(data["entries"])

    if args.check:
        if not ledger_path.exists():
            print(f"error: {ledger_path} does not exist; run --write.", file=sys.stderr)
            return 1
        current = ledger_path.read_text(encoding="utf-8")
        if current != rendered:
            print(
                f"error: {ledger_path} is stale. Regenerate with "
                f"`python -m build_tools.cpython_oracle ledger --write`.",
                file=sys.stderr,
            )
            return 1
        print(f"ledger up to date: {n_dev} deviations, {n_entries} method entries.")
        return 0

    if args.write:
        ledger_path.write_text(rendered, encoding="utf-8")
        print(f"wrote {ledger_path}: {n_dev} deviations, {n_entries} method entries.")
        return 0

    sys.stdout.write(rendered)
    return 0


def main(argv: Optional[list[str]] = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)
    if args.command == "classify":
        return _run_classify(args)
    if args.command == "ledger":
        return _run_ledger(args)
    parser.error(f"unknown command {args.command}")
    return 2


if __name__ == "__main__":
    sys.exit(main())
