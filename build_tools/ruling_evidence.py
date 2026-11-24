#!/usr/bin/env python3
"""Measured evidence for a ruling: what a program does, and what kind of door a decision is.

A ruling record (docs/process/rulings/rulings.yaml) carries *cases*, and a case carries
*evidence*: a Sharpy program, optionally its python twin, and what each did when it was run.
This module is the one place that runs them, so a record holds a measurement and never a claim.

Three uses:

    python3 build_tools/ruling_evidence.py measure prog.spy [--python twin.py]
        Run the program (and the twin) and print the evidence block to paste into a case.

    python3 build_tools/ruling_evidence.py door --before <observation> --after <observation> [--python <observation>]
        Print the door a decision is, from the three observations (see derive_door).

    python3 build_tools/ruling_evidence.py recheck
        Re-run every evidence program in rulings.yaml and laws.yaml against the built compiler and
        the python oracle; exit 1 when one no longer does what its record says.

An *observation* is one of:
    runs:<stdout>       the program compiled and exited 0; <stdout> is its exact standard output
    refused:<code>      it did not compile; <code> is the first SPYnnnn error in the diagnostics
                        (for python: `refused:SyntaxError`, from compiling the file, before any
                        statement ran; a SyntaxError raised while running is a `raises:`)
    crashes:<code>      (Sharpy only) the compiler reported an internal error: a code in the Infra
                        range SPY0900-0999, such as SPY0908 "generated C# failed to compile", or
                        one of the emitter's own failures in the CodeGen range (CODEGEN_INTERNAL).
                        It is a defect and never a ruled refusal, so a ruling cannot decide it
                        (`after`).
    raises:<type>       it compiled and ended on an uncaught exception of that type; whatever it
                        printed first follows on the next line (`raises:IndexError\nbefore\n`)
    no-twin             (python only) python3 has no program that means the same thing

The door (derive_door). Two facts are computed, and neither is asked of the author:

    breaks_running   the decision changes what a program that runs today does: it ran and is now
                     refused, or it ran and now prints or raises something else.
    undo_breaks      taking the decision back later would break programs written under it: the
                     decision lets a program run (it was refused, or it ran differently), or it
                     keeps a result that python's twin does not give, which users come to rely on.

`door` is `one-way` when undo_breaks, else `two-way`: the sense of `.claude/skills/batch-issues`
("a refusal can always be relaxed later; ... a runtime-value change is one-way"). The rows of the
charter use the word in two senses, which is why both facts are reported: of the 61 admissions the
2026-11-21 audit classified, 13 are called "additive", 10 "two-way" and 9 "one-way" (keyword
count). Who may decide which combination is docs/process/rulings/policy.yaml, not this module.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Dict, List, Optional, Tuple

REPO = Path(__file__).resolve().parents[1]
DEFAULT_SHARPYC = REPO / "src" / "Sharpy.Cli" / "bin" / "Debug" / "net10.0" / "sharpyc"
OBSERVATION_RE = re.compile(
    r"^(runs:.*|refused:[A-Za-z0-9_.]+|crashes:SPY\d{4}|raises:[A-Za-z0-9_.]+(\n.*)?|no-twin)$",
    re.S,
)
# Which kinds each field of an evidence block may hold. `after` is what a ruling decides.
SHARPY_KINDS = ("runs", "refused", "crashes", "raises")
PYTHON_KINDS = ("runs", "refused", "raises", "no-twin")
DECIDED_KINDS = ("runs", "refused", "raises")
# `sharpyc run` prints the compile banner, a blank line, this line, a blank line, then the program's output.
RUN_BANNER = "=== Running Program ===\n"
SPY_ERROR_RE = re.compile(r"error\[(SPY\d{4})\]")
NET_EXCEPTION_RE = re.compile(r"Unhandled exception\.\s+([\w.]+)")
# The CodeGen range (SPY0500-0599) mixes ruled refusals with the emitter's own failures, so it is
# classified code by code from src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs and its
# explanations. A code in neither set is an error, never a guess; test_ruling_evidence.py holds
# the two sets to the Active CodeGen codes of that file.
CODEGEN_INTERNAL = frozenset(
    {
        "SPY0500",  # EmitError: "typically an internal compiler error"
        "SPY0501",  # UnsupportedFeature: valid Sharpy the generator cannot compile yet
        "SPY0508",  # PositionalPatternFallback: "an internal compiler warning"
        "SPY0510",  # UnrecognizedStatementType
        "SPY0518",  # UnsupportedExpressionType
        "SPY0519",  # UnsupportedOperator
        "SPY0524",  # EmittedTreePrecedenceInversion
        "SPY0599",  # InternalGeneratedCSharpParseError
    }
)
CODEGEN_REFUSAL = frozenset(
    {
        "SPY0522",  # MemberNameCollision
        "SPY0523",  # FunctionModuleClassCollision
        "SPY0525",  # MemberEnclosingTypeCollision
        "SPY0526",  # PackageModuleNameCollision
        "SPY0550",  # GeneratorExecutionError: the user's source generator failed
        "SPY0551",  # GeneratorTimeout
        "SPY0552",  # GeneratorInvalidSource
        "SPY0553",  # GeneratorCycleDetected
        "SPY0554",  # GeneratorEmptyOutput
        "SPY0555",  # GeneratorUnsupportedShape
    }
)
TIMEOUT_S = 120

# Runs the python twin and reports how it ended through a side file, never through stderr text:
# a message, a note or a line printed to stderr can spell any exception name. `compile` first, so
# only a SyntaxError of the file itself is a refusal; one raised while running (eval, an import)
# is an exception like any other. The twin runs as the real `__main__` module, in
# `sys.modules`, as `python main.py` would run it: dataclasses, typing, pickle and
# `import __main__` all look the running module up there.
PY_RUNNER = r"""
import builtins
import sys
import types

path, result = sys.argv[1], sys.argv[2]
if len(sys.argv) > 3:
    # The twin has modules beside it: isolated mode leaves the script's directory off the
    # path, so put it first, where `python main.py` would have it.
    import os

    sys.path.insert(0, os.path.dirname(os.path.abspath(path)))
sys.argv = [path]
_open = open  # the twin may replace builtins.open


def done(text):
    try:
        sys.stdout.flush()
    except Exception:
        pass  # the twin closed or replaced stdout; what it wrote before is already out
    with _open(result, "w", encoding="utf-8") as handle:
        handle.write(text)


with _open(path, encoding="utf-8") as handle:
    source = handle.read()
try:
    code = compile(source, path, "exec")
except SyntaxError as exc:
    done("refused:" + type(exc).__name__)
    raise SystemExit(0)
main = types.ModuleType("__main__")
main.__file__ = path
main.__builtins__ = builtins  # a module, as in a script; exec would insert the dict
sys.modules["__main__"] = main
try:
    exec(code, main.__dict__)
except SystemExit as exc:
    done("runs" if exc.code in (None, 0) else "raises:SystemExit")
except BaseException as exc:
    done("raises:" + type(exc).__name__)
else:
    done("runs")
"""


class EvidenceError(Exception):
    """A program could not be measured at all (no compiler, a timeout, unreadable output)."""


def is_observation(text: object, kinds: Optional[Tuple[str, ...]] = None) -> bool:
    """True when `text` is an observation, of one of `kinds` when given."""
    if not isinstance(text, str) or OBSERVATION_RE.match(text) is None:
        return False
    return kinds is None or kind_of(text) in kinds


def kind_of(observation: str) -> str:
    return observation.split(":", 1)[0]


def is_internal(code: str) -> bool:
    """True when the code means the compiler failed, not that it refused the program."""
    number = int(code[3:])
    if 900 <= number <= 999:
        return True
    if 500 <= number <= 599:
        if code in CODEGEN_INTERNAL:
            return True
        if code in CODEGEN_REFUSAL:
            return False
        raise EvidenceError(
            f"{code} is a CodeGen code that ruling_evidence.py has not classified; "
            "add it to CODEGEN_INTERNAL or CODEGEN_REFUSAL"
        )
    return False


def _raised(name: str, printed: str) -> str:
    return "raises:" + name + ("\n" + printed if printed else "")


def sharpyc_path() -> Path:
    path = Path(os.environ.get("SHARPYC") or DEFAULT_SHARPYC)
    if not path.is_file():
        raise EvidenceError(
            f"no built compiler at {path}; build the solution or set SHARPYC"
        )
    return path


def _run(argv: List[str], cwd: Path) -> Tuple[int, str, str]:
    try:
        done = subprocess.run(
            argv, cwd=cwd, capture_output=True, text=True, timeout=TIMEOUT_S
        )
    except subprocess.TimeoutExpired as exc:
        raise EvidenceError(f"timed out after {TIMEOUT_S}s: {' '.join(argv)}") from exc
    return done.returncode, done.stdout, done.stderr


def _write_beside(directory: Path, files: Optional[Dict[str, str]], taken: Tuple[str, ...]) -> None:
    """Write a case's other modules next to its main file."""
    for name, text in (files or {}).items():
        if not isinstance(name, str) or not isinstance(text, str) or not name or Path(name).name != name or name in taken:
            raise EvidenceError(f"a file beside the program needs a plain name other than {', '.join(taken)}: {name!r}")
        (directory / name).write_text(text, encoding="utf-8")


def measure_sharpy(program: str, sharpyc: Optional[Path] = None, files: Optional[Dict[str, str]] = None) -> str:
    """Compile and run `program` (it needs a `main()`) with the built compiler; return its observation.

    `files` are other modules written beside it, for a case that needs more than one file.
    """
    sharpyc = sharpyc or sharpyc_path()
    with tempfile.TemporaryDirectory(prefix="ruling-evidence-") as tmp:
        source = Path(tmp) / "main.spy"
        source.write_text(program, encoding="utf-8")
        _write_beside(Path(tmp), files, ("main.spy",))
        code, out, err = _run([str(sharpyc), "run", str(source)], Path(tmp))
    if RUN_BANNER not in out:
        error = SPY_ERROR_RE.search(out + "\n" + err)
        if error:
            return ("crashes:" if is_internal(error.group(1)) else "refused:") + error.group(1)
        raise EvidenceError(
            f"sharpyc exited {code} with neither a run banner nor an error code:\n{(out + err).strip()[:800]}"
        )
    printed = out.split(RUN_BANNER, 1)[1]
    printed = printed[1:] if printed.startswith("\n") else printed
    if code == 0:
        return "runs:" + printed
    net = NET_EXCEPTION_RE.search(err)
    if net:
        return _raised(net.group(1).rsplit(".", 1)[-1], printed)
    raise EvidenceError(
        f"the program exited {code} without an unhandled-exception report:\n{err.strip()[:800]}"
    )


def measure_python(program: str, python: Optional[str] = None, files: Optional[Dict[str, str]] = None) -> str:
    """Run `program` under the python oracle in isolated mode; return its observation.

    `files` are other modules written beside it; with them the twin's directory is first on the path.
    """
    python = python or sys.executable
    with tempfile.TemporaryDirectory(prefix="ruling-evidence-") as tmp:
        source, runner, result = (Path(tmp) / n for n in ("main.py", "runner.py", "result.txt"))
        source.write_text(program, encoding="utf-8")
        runner.write_text(PY_RUNNER, encoding="utf-8")
        _write_beside(Path(tmp), files, ("main.py", "runner.py", "result.txt"))
        command = [python, "-I", str(runner), str(source), str(result)] + (["beside"] if files else [])
        code, out, err = _run(command, Path(tmp))
        ended = result.read_text(encoding="utf-8") if result.is_file() else None
    if ended is None:
        raise EvidenceError(
            f"python exited {code} without reporting how the program ended:\n{err.strip()[:800]}"
        )
    if ended == "runs":
        return "runs:" + out
    if ended.startswith("refused:"):
        return ended
    return _raised(ended.split(":", 1)[1], out)


def derive_door(before: str, after: str, python: str = "no-twin") -> Dict[str, object]:
    """The door a decision is, from what the program did before, does after, and does in python."""
    for name, value, kinds in (
        ("before", before, SHARPY_KINDS),
        ("after", after, DECIDED_KINDS),
        ("python", python, PYTHON_KINDS),
    ):
        if not is_observation(value, kinds):
            raise ValueError(
                f"{name}: not an observation of kind {', '.join(kinds)}: {value!r}"
            )
    ran_before = kind_of(before) in ("runs", "raises")
    runs_after = kind_of(after) in ("runs", "raises")
    changed = before != after
    breaks_running = ran_before and changed
    if not runs_after:
        undo_breaks, why = False, "a refusal can be relaxed later"
    elif not ran_before:
        undo_breaks, why = (
            True,
            "it lets a program run that did not compile; programs will be written that need it",
        )
    elif changed:
        undo_breaks, why = True, "it changes what a running program prints or raises"
    elif python != "no-twin" and python != after:
        undo_breaks, why = (
            True,
            "it keeps a result python's twin does not give; users come to rely on it",
        )
    else:
        undo_breaks, why = False, "nothing a program can observe changes"
    return {
        "breaks_running": breaks_running,
        "undo_breaks": undo_breaks,
        "door": "one-way" if undo_breaks else "two-way",
        "why": why,
    }


def evidence_block(
    program: str,
    twin: Optional[str],
    sharpyc: Optional[Path],
    python: Optional[str],
    files: Optional[Dict[str, str]] = None,
    twin_files: Optional[Dict[str, str]] = None,
) -> Dict[str, object]:
    block: Dict[str, object] = {"program": program}
    if files:
        block["files"] = dict(files)
    if twin is not None:
        block["twin"] = twin
        if twin_files:
            block["twin_files"] = dict(twin_files)
    block["sharpy"] = measure_sharpy(program, sharpyc, files)
    block["python"] = "no-twin" if twin is None else measure_python(twin, python, twin_files)
    return block


def recheck(
    records: List[Tuple[str, Dict[str, object]]],
    sharpyc: Optional[Path],
    python: Optional[str],
) -> List[str]:
    """Re-run each (owner, evidence) pair; return one line per program that drifted from its record."""
    drift: List[str] = []
    for owner, evidence in records:
        seen = measure_sharpy(str(evidence["program"]), sharpyc, evidence.get("files"))
        if seen != evidence.get("sharpy"):
            drift.append(
                f"{owner}: sharpy recorded {evidence.get('sharpy')!r}, now {seen!r}"
            )
        if "twin" in evidence:
            seen = measure_python(str(evidence["twin"]), python, evidence.get("twin_files"))
            if seen != evidence.get("python"):
                drift.append(
                    f"{owner}: python recorded {evidence.get('python')!r}, now {seen!r}"
                )
    return drift


def _all_evidence(root: Path) -> List[Tuple[str, Dict[str, object]]]:
    import yaml

    found: List[Tuple[str, Dict[str, object]]] = []
    base = root / "docs" / "process" / "rulings"
    for file, key in (("rulings.yaml", "rulings"), ("laws.yaml", "laws")):
        for owner in (
            yaml.safe_load((base / file).read_text(encoding="utf-8")) or {}
        ).get(key) or []:
            for index, case in enumerate(owner.get("cases") or []):
                if isinstance(case.get("evidence"), dict):
                    found.append(
                        (f"{owner.get('id')} case {index + 1}", case["evidence"])
                    )
    return found


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = parser.add_subparsers(dest="command", required=True)
    measure = sub.add_parser(
        "measure", help="run a program and print its evidence block"
    )
    measure.add_argument("program", type=Path)
    measure.add_argument(
        "--python", dest="twin", type=Path, help="the python twin of the program"
    )
    measure.add_argument(
        "--with", dest="beside", type=Path, action="append", default=[],
        help="another Sharpy module to write beside the program (repeatable)",
    )
    measure.add_argument(
        "--python-with", dest="twin_beside", type=Path, action="append", default=[],
        help="another python module to write beside the twin (repeatable)",
    )
    door = sub.add_parser("door", help="print the door for three observations")
    door.add_argument("--before", required=True)
    door.add_argument("--after", required=True)
    door.add_argument("--python", default="no-twin")
    again = sub.add_parser("recheck", help="re-run every recorded evidence program")
    again.add_argument("--root", type=Path, default=REPO)
    for command in (measure, again):
        command.add_argument("--sharpyc", type=Path, default=None)
        command.add_argument(
            "--python-exe",
            default=None,
            help="the python oracle (default: this interpreter)",
        )
    args = parser.parse_args(argv)

    try:
        if args.command == "measure":
            import yaml

            twin = args.twin.read_text(encoding="utf-8") if args.twin else None
            block = evidence_block(
                args.program.read_text(encoding="utf-8"),
                twin,
                args.sharpyc,
                args.python_exe,
                {f.name: f.read_text(encoding="utf-8") for f in args.beside},
                {f.name: f.read_text(encoding="utf-8") for f in args.twin_beside},
            )
            print(
                yaml.safe_dump(
                    {"evidence": block},
                    sort_keys=False,
                    allow_unicode=True,
                    default_style=None,
                ),
                end="",
            )
            return 0
        if args.command == "door":
            for key, value in derive_door(args.before, args.after, args.python).items():
                print(f"{key}: {value}")
            return 0
        records = _all_evidence(args.root)
        drift = recheck(records, args.sharpyc, args.python_exe)
        for line in drift:
            print(line)
        print(f"ruling evidence: {len(records)} programs re-run, {len(drift)} drifted")
        return 1 if drift else 0
    except (EvidenceError, ValueError) as exc:
        print(f"ruling evidence: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
