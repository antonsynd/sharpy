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
    with pytest.raises(ValueError, match="before: not an observation of kind"):
        mod.derive_door("it prints 1", RAN)


@pytest.mark.parametrize(
    "text, ok",
    [
        ("runs:", True),
        ("runs:a\nb\n", True),
        ("refused:SPY0402", True),
        ("refused:SyntaxError", True),
        ("crashes:SPY0908", True),
        ("raises:IndexError", True),
        ("raises:IndexError\nprinted first\n", True),
        ("no-twin", True),
        ("refused:", False),
        ("crashes:TypeError", False),
        ("prints 1", False),
        ("refused:SPY0402 and more", False),
        ("raises:IndexError and more", False),
        (None, False),
    ],
)
def test_is_observation(text, ok):
    assert mod.is_observation(text) is ok


def test_is_observation_can_be_limited_to_the_kinds_a_field_may_hold():
    assert mod.is_observation("crashes:SPY0908", mod.SHARPY_KINDS)
    assert not mod.is_observation("crashes:SPY0908", mod.DECIDED_KINDS)
    assert not mod.is_observation("crashes:SPY0908", mod.PYTHON_KINDS)
    assert not mod.is_observation("no-twin", mod.SHARPY_KINDS)


def test_a_ruling_cannot_decide_a_crash_and_python_cannot_crash():
    with pytest.raises(ValueError, match="after: not an observation of kind"):
        mod.derive_door(REFUSED, "crashes:SPY0908")
    with pytest.raises(ValueError, match="python: not an observation of kind"):
        mod.derive_door(REFUSED, RAN, "crashes:SPY0908")


def test_a_program_that_crashes_the_compiler_today_does_not_run_today():
    # Ruling it refused breaks nothing; ruling that it runs is an admission.
    refused = mod.derive_door("crashes:SPY0908", REFUSED)
    assert (refused["door"], refused["breaks_running"]) == ("two-way", False)
    admitted = mod.derive_door("crashes:SPY0908", RAN)
    assert (admitted["door"], admitted["breaks_running"]) == ("one-way", False)


def test_what_a_program_printed_before_it_raised_is_part_of_the_observation():
    # Printing `a` then raising, and printing `b` then raising, are two results.
    got = mod.derive_door("raises:TypeError\na\n", "raises:TypeError\nb\n")
    assert (got["breaks_running"], got["undo_breaks"]) == (True, True)


# --- python ---------------------------------------------------------------------------------------


def test_python_runs():
    assert mod.measure_python("print(1)\nprint('x')\n") == "runs:1\nx\n"
    assert mod.measure_python("import sys\nprint('x')\nsys.exit(0)\n") == "runs:x\n"


def test_python_syntax_error_of_the_file_is_a_refusal():
    # CPython refuses the whole file before any statement runs: nothing is printed.
    assert mod.measure_python("print('before')\nprint(01)\n") == "refused:SyntaxError"
    assert mod.measure_python("if True:\nprint(1)\n") == "refused:IndentationError"


@pytest.mark.parametrize(
    "program",
    [
        "print('ran')\neval('1 +')\n",
        "print('ran')\nraise SyntaxError('x')\n",
        "print('ran')\nexec('print(01)')\n",
    ],
)
def test_a_syntax_error_raised_while_running_is_not_a_refusal(program):
    # The program ran and printed; python did not refuse it. (Independent review @ 993681ab3.)
    assert mod.measure_python(program) == "raises:SyntaxError\nran\n"


def test_python_uncaught_exception_is_raises_with_the_type_and_what_came_first():
    assert mod.measure_python("print('before')\nprint(1 // 0)\n") == "raises:ZeroDivisionError\nbefore\n"
    assert mod.measure_python("class P:\n    pass\nP() < P()\n") == "raises:TypeError"


@pytest.mark.parametrize(
    "program, name",
    [
        # Text on stderr can spell any exception name; the type is read from the exception.
        ("raise ValueError('a\\nTypeError: b')\n", "ValueError"),
        ("e = KeyError('k')\ne.add_note('TypeError: hint')\nraise e\n", "KeyError"),
        ("import sys\nprint('KeyError: lookup', file=sys.stderr)\nclass Boom(Exception):\n    pass\nraise Boom()\n", "Boom"),
        ("raise ExceptionGroup('g', [ValueError()])\n", "ExceptionGroup"),
        ("import sys\nsys.exit('ValueError: bad')\n", "SystemExit"),
        ("import sys\nsys.exit(3)\n", "SystemExit"),
        ("raise KeyboardInterrupt()\n", "KeyboardInterrupt"),
    ],
)
def test_the_python_exception_type_is_not_parsed_from_stderr(program, name):
    assert mod.measure_python(program) == "raises:" + name


def test_a_twin_that_kills_the_interpreter_is_an_error_not_a_verdict():
    with pytest.raises(mod.EvidenceError, match="without reporting how the program ended"):
        mod.measure_python("import os\nos._exit(3)\n")


# --- the compiler ---------------------------------------------------------------------------------


def fake_sharpyc(tmp_path: Path, stdout: str, stderr: str = "", code: int = 1) -> Path:
    """A stand-in compiler that prints fixed text: for the outputs a real program cannot be relied on to give."""
    path = tmp_path / "sharpyc"
    path.write_text(
        "#!/bin/sh\ncat <<'OUT'\n" + stdout + "\nOUT\ncat >&2 <<'ERR'\n" + stderr + "\nERR\nexit " + str(code) + "\n",
        encoding="utf-8",
    )
    path.chmod(0o755)
    return path


def test_an_internal_compiler_error_is_a_crash_not_a_refusal(tmp_path):
    # SPY0908 is "generated C# failed to compile": a compiler defect. Recording it as `refused:`
    # made a leak look like a ruled refusal (independent review @ 993681ab3, R-PK's evidence).
    text = "Compilation failed:\n\nerror[SPY0908]: internal error: generated C# failed to compile (CS0019)"
    assert mod.measure_sharpy("x", fake_sharpyc(tmp_path, text)) == "crashes:SPY0908"


@pytest.mark.parametrize("code, kind", [("SPY0222", "refused"), ("SPY0523", "refused"), ("SPY0899", "refused"), ("SPY0900", "crashes"), ("SPY0913", "crashes"), ("SPY0999", "crashes"), ("SPY1000", "refused")])
def test_only_the_infra_range_is_a_crash(tmp_path, code, kind):
    assert mod.measure_sharpy("x", fake_sharpyc(tmp_path, f"Compilation failed:\n\nerror[{code}]: something")) == f"{kind}:{code}"


def test_the_first_error_decides_and_a_warning_does_not(tmp_path):
    text = "warning[SPY0908]: not an error\nerror[SPY0222]: first\nerror[SPY0908]: second"
    assert mod.measure_sharpy("x", fake_sharpyc(tmp_path, text)) == "refused:SPY0222"


def test_compiler_output_with_no_banner_and_no_error_is_an_error_not_a_verdict(tmp_path):
    with pytest.raises(mod.EvidenceError, match="neither a run banner nor an error code"):
        mod.measure_sharpy("x", fake_sharpyc(tmp_path, "something unexpected"))


def test_a_program_that_exits_non_zero_without_an_exception_is_an_error_not_a_verdict(tmp_path):
    ran = "Successfully compiled to: x.exe\n\n=== Running Program ===\n\nUnhandled exception. Sharpy.KeyError: printed by the program"
    with pytest.raises(mod.EvidenceError, match="without an unhandled-exception report"):
        mod.measure_sharpy("x", fake_sharpyc(tmp_path, ran, code=3))


@needs_compiler
def test_sharpy_runs_and_the_banner_is_not_output():
    assert mod.measure_sharpy("def main() -> None:\n    print(1)\n    print('x')\n") == "runs:1\nx\n"


@needs_compiler
def test_sharpy_refusal_carries_the_first_error_code():
    assert mod.measure_sharpy('def main() -> None:\n    x: int = 1\n    print(x + "a")\n') == "refused:SPY0222"


@needs_compiler
def test_sharpy_uncaught_exception_is_raises_with_the_type_and_what_came_first():
    program = "def main() -> None:\n    xs: list[int] = [1]\n    print('before')\n    print(xs[5])\n"
    assert mod.measure_sharpy(program) == "raises:IndexError\nbefore\n"


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
