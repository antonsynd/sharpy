"""Tests for build_tools/ruling_evidence.py: a ruling's evidence is measured, and its door derived.

The door table is checked as a pure function. The two measuring functions are checked against the
real python interpreter and, where one is built, the real compiler: nothing about what a program
does is faked. `recheck` is then run over every evidence program recorded in the tree.
"""

from __future__ import annotations

import os
import re
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


CLASSVAR = """from __future__ import annotations
from dataclasses import dataclass, fields
from typing import ClassVar


@dataclass
class C:
    a: int
    b: ClassVar[int] = 0


print(len(fields(C)))
"""


@pytest.mark.parametrize(
    "program, printed",
    [
        # dataclasses resolves the string annotation `ClassVar[int]` through sys.modules: under a
        # runner that is not `__main__` it counted `b` as a field and printed 2, silently
        # (second review @ 87da77896).
        (CLASSVAR, "1\n"),
        ("x = 5\nimport __main__\nprint(getattr(__main__, 'x', 'missing'))\n", "5\n"),
        ("import pickle\nclass P:\n    pass\nprint(type(pickle.loads(pickle.dumps(P()))).__name__)\n", "P\n"),
        ("from __future__ import annotations\nimport typing\nclass N:\n    nxt: N\nprint(typing.get_type_hints(N)['nxt'].__name__)\n", "N\n"),
        ("import sys\nprint(__name__, sys.argv[0] == __file__, sys.modules['__main__'].__name__)\n", "__main__ True __main__\n"),
        ("print(type(__builtins__).__name__, __spec__, __package__, __doc__)\n", "module None None None\n"),
        # A twin may close stdout or replace open; how it ended is still reported.
        ("import sys\nprint('a')\nsys.stdout.close()\n", "a\n"),
        ("import builtins\nprint('a')\nbuiltins.open = None\n", "a\n"),
    ],
)
def test_the_twin_runs_as_the_real_main_module(program, printed):
    assert mod.measure_python(program) == "runs:" + printed
    # The control: the same file under the interpreter directly.
    import subprocess
    import tempfile

    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "main.py"
        path.write_text(program, encoding="utf-8")
        plain = subprocess.run([sys.executable, "-I", str(path)], capture_output=True, text=True, cwd=tmp)
    assert plain.returncode == 0 and plain.stdout == printed


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


@pytest.mark.parametrize(
    "code, kind",
    [
        ("SPY0222", "refused"),
        ("SPY0899", "refused"),
        ("SPY0900", "crashes"),
        ("SPY0913", "crashes"),
        ("SPY0999", "crashes"),
        ("SPY1000", "refused"),
        # The CodeGen range holds both: a name collision is a ruled refusal (R-AW), an emit
        # failure is the emitter's own (second review @ 87da77896).
        ("SPY0523", "refused"),
        ("SPY0526", "refused"),
        ("SPY0550", "refused"),
        ("SPY0500", "crashes"),
        ("SPY0519", "crashes"),
        ("SPY0599", "crashes"),
    ],
)
def test_which_codes_are_a_crash(tmp_path, code, kind):
    text = f"Compilation failed:\n\nerror[{code}]: something"
    assert mod.measure_sharpy("x", fake_sharpyc(tmp_path, text)) == f"{kind}:{code}"


def test_an_unclassified_codegen_code_is_an_error_not_a_guess(tmp_path):
    text = "Compilation failed:\n\nerror[SPY0530]: a code reserved today"
    with pytest.raises(mod.EvidenceError, match="SPY0530 is a CodeGen code that ruling_evidence.py has not classified"):
        mod.measure_sharpy("x", fake_sharpyc(tmp_path, text))


def test_every_active_codegen_code_is_classified_exactly_once():
    # The roster is DiagnosticCodes.cs: a CodeGen code made Active there must be put in one of the
    # two sets here, or this fails (and measuring a program that hits it raises). Every SPY05nn
    # constant counts unless its line says Retired or Reserved, so a new code is seen however its
    # comment is written (third review @ 7e9f191e6).
    source = (REPO / "src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs").read_text(encoding="utf-8")
    constants = re.findall(r'^.*const string \w+\s*=\s*"(SPY05\d\d)".*$', source, re.M)
    lines = {code: line for line in source.splitlines() for code in re.findall(r'"(SPY05\d\d)"', line) if "const string" in line}
    assert len(constants) == len(lines) == 26, sorted(lines)  # no code declared twice
    active = {code for code, line in lines.items() if not re.search(r"//\s*(Retired|Reserved)", line)}
    assert len(active) == 18, sorted(active)  # the count the file's own header states
    assert not (mod.CODEGEN_INTERNAL & mod.CODEGEN_REFUSAL)
    assert mod.CODEGEN_INTERNAL | mod.CODEGEN_REFUSAL == active


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


# --- a case of more than one file -----------------------------------------------------------------

LOCAL_JSON = 'def dumps(x: int) -> str:\n    return "local"\n'
USES_JSON = "from json import dumps\n\nprint(dumps(1))\n"


def test_a_module_beside_the_twin_is_what_the_twin_imports():
    # Isolated mode leaves the script's directory off the path. With a module beside it, the twin
    # must see it first, as `python main.py` would; without one, the stdlib's json answers.
    assert mod.measure_python(USES_JSON) == "runs:1\n"
    assert mod.measure_python(USES_JSON, None, {"json.py": LOCAL_JSON}) == "runs:local\n"


def beside_reader(tmp_path: Path) -> Path:
    """A stand-in compiler that "runs" a program by printing the file `beside.txt` next to it."""
    path = tmp_path / "sharpyc"
    path.write_text('#!/bin/sh\necho "=== Running Program ==="\ncat "$(dirname "$2")/beside.txt"\n', encoding="utf-8")
    path.chmod(0o755)
    return path


def test_files_beside_the_program_are_written_where_the_compiler_finds_them(tmp_path):
    assert mod.measure_sharpy("x", beside_reader(tmp_path), {"beside.txt": "seen\n"}) == "runs:seen\n"


def test_a_twin_with_nothing_beside_it_stays_isolated():
    # The runner sits in the twin's directory. In isolated mode that directory is off the path, so
    # the twin cannot import it; only a case that has files beside it gets the directory.
    assert mod.measure_python("import runner\n") == "raises:ModuleNotFoundError"


@pytest.mark.parametrize("name", ["../up.spy", "sub/in.spy", "main.spy", "Main.spy", "..", ""])
def test_a_file_beside_the_program_has_a_plain_name_of_its_own(tmp_path, name):
    with pytest.raises(mod.EvidenceError, match="plain name"):
        mod.measure_sharpy("x", beside_reader(tmp_path), {name: "y"})
    with pytest.raises(mod.EvidenceError, match="plain name"):
        mod.measure_python("pass\n", None, {name.replace(".spy", ".py"): "y"})


def test_two_files_beside_a_program_differ_by_more_than_case(tmp_path):
    with pytest.raises(mod.EvidenceError, match="plain name"):
        mod.measure_sharpy("x", beside_reader(tmp_path), {"a.spy": "1", "A.spy": "2"})


def test_the_measure_command_refuses_files_it_would_lose(tmp_path, capsys):
    (tmp_path / "a").mkdir(); (tmp_path / "b").mkdir()
    for path in (tmp_path / "prog.spy", tmp_path / "a" / "x.spy", tmp_path / "b" / "x.spy", tmp_path / "x.py"):
        path.write_text("pass\n", encoding="utf-8")
    compiler = ["--sharpyc", str(beside_reader(tmp_path))]
    same_name = ["measure", str(tmp_path / "prog.spy"), "--with", str(tmp_path / "a" / "x.spy"), "--with", str(tmp_path / "b" / "x.spy")]
    assert mod.main(same_name + compiler) == 2
    assert "one name" in capsys.readouterr().err
    no_twin = ["measure", str(tmp_path / "prog.spy"), "--python-with", str(tmp_path / "x.py")]
    assert mod.main(no_twin + compiler) == 2
    assert "--python-with needs --python" in capsys.readouterr().err


def test_recheck_runs_a_record_with_the_files_it_was_measured_with(tmp_path):
    record = {
        "program": "x",
        "files": {"beside.txt": "seen\n"},
        "sharpy": "runs:seen\n",
        "twin": USES_JSON,
        "twin_files": {"json.py": LOCAL_JSON},
        "python": "runs:local\n",
    }
    assert mod.recheck([("R-X case 1", record)], beside_reader(tmp_path), None) == []


def test_the_evidence_block_records_the_files_it_ran_with(tmp_path):
    block = mod.evidence_block("x", USES_JSON, beside_reader(tmp_path), None, {"beside.txt": "seen\n"}, {"json.py": LOCAL_JSON})
    assert list(block) == ["program", "files", "twin", "twin_files", "sharpy", "python"]
    assert (block["sharpy"], block["python"]) == ("runs:seen\n", "runs:local\n")
    plain = mod.evidence_block("x", None, beside_reader(tmp_path), None, {"beside.txt": "a\n"})
    assert list(plain) == ["program", "files", "sharpy", "python"] and plain["python"] == "no-twin"


@needs_compiler
def test_the_compiler_sees_a_module_written_beside_the_program():
    program = "from helper import seven\n\ndef main() -> None:\n    print(seven())\n"
    assert mod.measure_sharpy(program, None, {"helper.spy": "def seven() -> int:\n    return 7\n"}) == "runs:7\n"


def test_door_command_prints_the_derivation(capsys):
    assert mod.main(["door", "--before", REFUSED, "--after", RAN]) == 0
    assert "door: one-way" in capsys.readouterr().out
    assert mod.main(["door", "--before", "nonsense", "--after", RAN]) == 2
