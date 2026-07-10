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


def main(argv: Optional[list[str]] = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)
    if args.command == "classify":
        return _run_classify(args)
    parser.error(f"unknown command {args.command}")
    return 2


if __name__ == "__main__":
    sys.exit(main())
