"""Dual-execution runner: run ported CPython ``.spy`` tests under ``python3`` (#1030).

Phase 8 task 3 of the Wave 2 plan. A CPython port only counts as a *golden oracle*
if it also passes under stock CPython — this proves the ported assertions are a
faithful record of Python's behavior, not just of Sharpy's. This runner executes
each ported ``.spy`` file under the current interpreter with the CPython shim
installed (see :mod:`build_tools.cpython_oracle.shim`) and reports pass/fail per
``@test`` function.

Usage
-----
::

    python3 -m build_tools.cpython_oracle.dual_run <file-or-dir> [<file-or-dir> ...]
    python3 -m build_tools.cpython_oracle.dual_run src/Sharpy.Stdlib.Tests/Spy/cpython/

A directory argument is scanned recursively for ``*.spy`` files. Exit status is
0 when every discovered test passes, 1 when any test fails or errors, and 2 for
usage / discovery problems (bad path, directory with no ``.spy`` files).
"""

from __future__ import annotations

import argparse
import sys
import traceback
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from . import ledger as ledger_mod
from . import shim


@dataclass
class TestOutcome:
    name: str
    passed: bool
    skipped: bool = False
    detail: str = ""


@dataclass
class FileOutcome:
    path: Path
    outcomes: List[TestOutcome] = field(default_factory=list)
    load_error: str = ""

    @property
    def passed(self) -> int:
        return sum(1 for o in self.outcomes if o.passed and not o.skipped)

    @property
    def failed(self) -> int:
        return sum(1 for o in self.outcomes if not o.passed)

    @property
    def skipped(self) -> int:
        return sum(1 for o in self.outcomes if o.skipped)

    @property
    def ok(self) -> bool:
        return not self.load_error and self.failed == 0


def run_file(path: Path) -> FileOutcome:
    """Execute one ported ``.spy`` file under CPython and run its ``@test`` funcs."""
    result = FileOutcome(path=path)
    module_name = f"spy_oracle_{path.stem}"
    namespace = shim.exec_globals(module_name, str(path))

    try:
        source = path.read_text(encoding="utf-8")
        code = compile(source, str(path), "exec")
        exec(code, namespace)  # noqa: S102 - executing trusted ported test files
    except Exception:  # noqa: BLE001 - report any load-time failure, don't abort the run
        result.load_error = traceback.format_exc()
        return result

    for name, func in shim.discover_tests(namespace):
        skip = getattr(func, "__spy_skip__", None)
        if skip and skip[0]:
            result.outcomes.append(
                TestOutcome(name=name, passed=True, skipped=True, detail=skip[1])
            )
            continue
        try:
            func()
            result.outcomes.append(TestOutcome(name=name, passed=True))
        except AssertionError as exc:
            result.outcomes.append(
                TestOutcome(name=name, passed=False, detail=_short_assert(exc))
            )
        except Exception:  # noqa: BLE001 - a raised exception is a failing test
            result.outcomes.append(
                TestOutcome(name=name, passed=False, detail=traceback.format_exc())
            )

    return result


def _short_assert(exc: AssertionError) -> str:
    message = str(exc).strip()
    return message or "assertion failed"


def _collect_spy_files(targets: List[str]) -> Optional[List[Path]]:
    files: List[Path] = []
    for target in targets:
        path = Path(target)
        if not path.exists():
            print(f"error: path not found: {target}", file=sys.stderr)
            return None
        if path.is_dir():
            found = sorted(path.rglob("*.spy"))
            if not found:
                print(f"error: no .spy files under {target}", file=sys.stderr)
                return None
            files.extend(found)
        else:
            files.append(path)
    # De-duplicate while preserving order.
    seen = set()
    unique: List[Path] = []
    for f in files:
        resolved = f.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique.append(f)
    return unique


def _print_file_report(outcome: FileOutcome, verbose: bool) -> None:
    rel = outcome.path
    if outcome.load_error:
        print(f"FAIL  {rel}  (load error)")
        print(_indent(outcome.load_error))
        return

    status = "ok" if outcome.ok else "FAIL"
    summary = f"{outcome.passed} passed"
    if outcome.failed:
        summary += f", {outcome.failed} failed"
    if outcome.skipped:
        summary += f", {outcome.skipped} skipped"
    print(f"{status:<5} {rel}  ({summary})")

    for o in outcome.outcomes:
        if not o.passed:
            print(f"        FAIL {o.name}")
            if o.detail:
                print(_indent(o.detail, prefix="             "))
        elif o.skipped:
            print(f"        skip {o.name}" + (f"  ({o.detail})" if o.detail else ""))
        elif verbose:
            print(f"        pass {o.name}")


def _indent(text: str, prefix: str = "        ") -> str:
    return "\n".join(prefix + line for line in text.rstrip().splitlines())


def _collect_results(outcomes: List[FileOutcome]) -> Dict[Tuple[str, str], str]:
    """Flatten per-file outcomes into a ``(module, test) -> status`` map, where
    status is ``pass`` / ``fail`` / ``skip``. Load errors are surfaced separately
    (they have no per-test names) so they always fail the run."""
    results: Dict[Tuple[str, str], str] = {}
    for outcome in outcomes:
        module = outcome.path.stem
        for o in outcome.outcomes:
            status = "skip" if o.skipped else ("pass" if o.passed else "fail")
            results[(module, o.name)] = status
    return results


def _print_ledger_report(
    enforcement: "ledger_mod.EnforcementResult", load_error_files: List[Path]
) -> None:
    print()
    print("ledger enforcement:")
    if enforcement.satisfied:
        print(f"  {len(enforcement.satisfied)} documented divergence(s) confirmed:")
        for module, test in enforcement.satisfied:
            print(f"        expected-fail-cpython {module}::{test} (failed as documented)")
    if enforcement.unexplained_failures:
        print(f"  {len(enforcement.unexplained_failures)} UNEXPLAINED divergence(s):")
        for module, test in enforcement.unexplained_failures:
            print(
                f"        {module}::{test} failed under CPython with no ledger entry — "
                f"add an expected-fail-cpython annotation or fix the port"
            )
    if enforcement.stale_entries:
        print(f"  {len(enforcement.stale_entries)} STALE ledger entry(ies):")
        for module, test in enforcement.stale_entries:
            print(
                f"        expected-fail-cpython {module}::{test} unexpectedly passed — "
                f"the divergence is gone; remove the entry"
            )
    if load_error_files:
        print(f"  {len(load_error_files)} file(s) failed to load:")
        for path in load_error_files:
            print(f"        {path}")
    if enforcement.ok and not load_error_files:
        print("  ok — every CPython divergence is documented and current.")


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m build_tools.cpython_oracle.dual_run",
        description="Run ported CPython .spy oracle tests under python3 (#1030).",
    )
    parser.add_argument(
        "targets",
        nargs="+",
        help="Ported .spy files or directories (scanned recursively for *.spy).",
    )
    parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Also list passing tests.",
    )
    parser.add_argument(
        "--ledger",
        help=(
            "Path to the divergence ledger (ledger.yaml). When given, a CPython "
            "failure is excused only by a covering expected-fail-cpython entry, and "
            "an entry whose test unexpectedly passes fails the run as stale."
        ),
    )
    args = parser.parse_args(argv)

    files = _collect_spy_files(args.targets)
    if files is None:
        return 2

    ledger_data = None
    if args.ledger:
        try:
            ledger_data = ledger_mod.load_ledger(Path(args.ledger))
        except ledger_mod.LedgerError as exc:
            print(f"error: {exc}", file=sys.stderr)
            return 2

    shim.install()

    total_pass = total_fail = total_skip = 0
    file_failures = 0
    outcomes: List[FileOutcome] = []
    for path in files:
        outcome = run_file(path)
        outcomes.append(outcome)
        _print_file_report(outcome, args.verbose)
        total_pass += outcome.passed
        total_fail += outcome.failed
        total_skip += outcome.skipped
        if not outcome.ok:
            file_failures += 1

    print()
    print(
        f"dual-run: {len(files)} file(s), "
        f"{total_pass} passed, {total_fail} failed, {total_skip} skipped"
    )

    if ledger_data is not None:
        results = _collect_results(outcomes)
        load_error_files = [o.path for o in outcomes if o.load_error]
        enforcement = ledger_mod.evaluate_ledger(results, ledger_data)
        _print_ledger_report(enforcement, load_error_files)
        if load_error_files or not enforcement.ok:
            return 1
        if total_pass == 0 and not results:
            print("error: no @test functions were discovered", file=sys.stderr)
            return 2
        return 0

    if file_failures or total_fail:
        return 1
    if total_pass == 0:
        print("error: no @test functions were discovered", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
