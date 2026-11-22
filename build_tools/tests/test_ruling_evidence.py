"""Tests for build_tools/ruling_evidence.py: a ruling's evidence is measured, and its door derived.

The door table is checked as a pure function. The two measuring functions are checked against the
real python interpreter and, where one is built, the real compiler: nothing about what a program
does is faked. `recheck` is then run over every evidence program recorded in the tree.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import ruling_evidence as mod

REPO = Path(__file__).resolve().parents[2]
RAN = "runs:1\n"
OTHER = "runs:2\n"
REFUSED = "refused:SPY0402"

# python-build-tools.yml builds sharpyc and sets SHARPYC, so these run (never skip) in CI.
needs_compiler = pytest.mark.skipif(
    not os.environ.get("SHARPYC") and not mod.DEFAULT_SHARPYC.exists(),
    reason="needs a built sharpyc",
)


# --- the door -------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    "before, after, python, door, breaks_running, undo_breaks",
    [
        # A program that is refused stays refused: nothing to take back.
        (REFUSED, "refused:SPY0611", "no-twin", "two-way", False, False),
        # A refused program is admitted: programs will be written that need it.
        (REFUSED, RAN, "no-twin", "one-way", False, True),
        # A running program is refused: it breaks today, and the refusal can be relaxed later.
        (RAN, REFUSED, "no-twin", "two-way", True, False),
        # A running program prints something else: breaks today, and cannot be taken back.
        (RAN, OTHER, "no-twin", "one-way", True, True),
        # Same output, python agrees or has no twin: nothing observable changes.
        (RAN, RAN, RAN, "two-way", False, False),
        (RAN, RAN, "no-twin", "two-way", False, False),
        # Today's result is kept although python refuses the program (R-HT's `01`), or prints
        # something else: users come to rely on the difference.
        (RAN, RAN, "refused:SyntaxError", "one-way", False, True),
        (RAN, RAN, OTHER, "one-way", False, True),
        # A run-time error is a result too: from raising to printing is a value change.
        ("raises:TypeError", RAN, RAN, "one-way", True, True),
    ],
)
def test_derive_door(before, after, python, door, breaks_running, undo_breaks):
    got = mod.derive_door(before, after, python)
    assert (got["door"], got["breaks_running"], got["undo_breaks"]) == (
        door,
        breaks_running,
        undo_breaks,
    )
    assert got["why"]


def test_derive_door_refuses_what_is_not_an_observation():
    with pytest.raises(ValueError, match="before: not an observation"):
        mod.derive_door("it prints 1", RAN)


@pytest.mark.parametrize(
    "text, ok",
    [
        ("runs:", True),
        ("runs:a\nb\n", True),
        ("refused:SPY0402", True),
        ("refused:SyntaxError", True),
        ("raises:IndexError", True),
        ("no-twin", True),
        ("refused:", False),
        ("prints 1", False),
        ("refused:SPY0402 and more", False),
        (None, False),
    ],
)
def test_is_observation(text, ok):
    assert mod.is_observation(text) is ok


# --- python ---------------------------------------------------------------------------------------


def test_python_runs():
    assert mod.measure_python("print(1)\nprint('x')\n") == "runs:1\nx\n"


def test_python_syntax_error_is_a_refusal():
    # CPython refuses the whole file before any statement runs: nothing is printed.
    assert mod.measure_python("print('before')\nprint(01)\n") == "refused:SyntaxError"


def test_python_uncaught_exception_is_raises_with_the_type():
    assert (
        mod.measure_python("print('before')\nprint(1 // 0)\n")
        == "raises:ZeroDivisionError"
    )
    assert mod.measure_python("class P:\n    pass\nP() < P()\n") == "raises:TypeError"


# --- the compiler ---------------------------------------------------------------------------------


@needs_compiler
def test_sharpy_runs_and_the_banner_is_not_output():
    assert (
        mod.measure_sharpy("def main() -> None:\n    print(1)\n    print('x')\n")
        == "runs:1\nx\n"
    )


@needs_compiler
def test_sharpy_refusal_carries_the_first_error_code():
    assert (
        mod.measure_sharpy('def main() -> None:\n    x: int = 1\n    print(x + "a")\n')
        == "refused:SPY0222"
    )


@needs_compiler
def test_sharpy_uncaught_exception_is_raises_with_the_type():
    assert (
        mod.measure_sharpy(
            "def main() -> None:\n    xs: list[int] = [1]\n    print(xs[5])\n"
        )
        == "raises:IndexError"
    )


def test_a_missing_compiler_is_an_error_not_a_verdict(tmp_path, monkeypatch):
    monkeypatch.setenv("SHARPYC", str(tmp_path / "absent"))
    with pytest.raises(mod.EvidenceError, match="no built compiler"):
        mod.measure_sharpy("def main() -> None:\n    pass\n")


# --- recheck --------------------------------------------------------------------------------------


def test_the_tree_records_evidence():
    # A positive control for the recheck below: there is something to re-run.
    records = mod._all_evidence(REPO)
    assert len(records) >= 12
    assert any(owner.startswith("R-HT") for owner, _ in records)


@needs_compiler
def test_every_recorded_program_still_does_what_its_record_says():
    assert mod.recheck(mod._all_evidence(REPO), None, None) == []


@needs_compiler
def test_recheck_reports_a_record_that_no_longer_matches():
    program = "def main() -> None:\n    print(1)\n"
    stale = {
        "program": program,
        "sharpy": "refused:SPY0402",
        "twin": "print(1)\n",
        "python": "runs:2\n",
    }
    drift = mod.recheck([("R-X case 1", stale)], None, None)
    assert drift == [
        "R-X case 1: sharpy recorded 'refused:SPY0402', now 'runs:1\\n'",
        "R-X case 1: python recorded 'runs:2\\n', now 'runs:1\\n'",
    ]


def test_door_command_prints_the_derivation(capsys):
    assert mod.main(["door", "--before", REFUSED, "--after", RAN]) == 0
    assert "door: one-way" in capsys.readouterr().out
    assert mod.main(["door", "--before", "nonsense", "--after", RAN]) == 2
