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
    refused:<code>      it did not compile; <code> is the first SPYnnnn in the diagnostics
                        (for python: `refused:SyntaxError`, raised before any statement ran)
    raises:<type>       it compiled and ended on an uncaught exception of that type
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
    r"^(runs:.*|refused:[A-Za-z0-9_.]+|raises:[A-Za-z0-9_.]+|no-twin)$", re.S
)
# `sharpyc run` prints the compile banner, a blank line, this line, a blank line, then the program's output.
RUN_BANNER = "=== Running Program ===\n"
SPY_ERROR_RE = re.compile(r"error\[(SPY\d{4})\]")
# The last frame line of a python traceback, or of a .NET unhandled-exception report.
PY_EXCEPTION_RE = re.compile(
    r"^([A-Za-z_][\w.]*(?:Error|Exception|Exit|Interrupt|Warning|StopIteration))\b",
    re.M,
)
NET_EXCEPTION_RE = re.compile(r"Unhandled exception\.\s+([\w.]+)")
TIMEOUT_S = 120


class EvidenceError(Exception):
    """A program could not be measured at all (no compiler, a timeout, unreadable output)."""


def is_observation(text: object) -> bool:
    return isinstance(text, str) and OBSERVATION_RE.match(text) is not None


def kind_of(observation: str) -> str:
    return observation.split(":", 1)[0]


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


def measure_sharpy(program: str, sharpyc: Optional[Path] = None) -> str:
    """Compile and run `program` (it needs a `main()`) with the built compiler; return its observation."""
    sharpyc = sharpyc or sharpyc_path()
    with tempfile.TemporaryDirectory(prefix="ruling-evidence-") as tmp:
        source = Path(tmp) / "main.spy"
        source.write_text(program, encoding="utf-8")
        code, out, err = _run([str(sharpyc), "run", str(source)], Path(tmp))
    if RUN_BANNER not in out:
        refusal = SPY_ERROR_RE.search(out + "\n" + err)
        if refusal:
            return "refused:" + refusal.group(1)
        raise EvidenceError(f"sharpyc exited {code} with neither a run banner nor an error code:\n{(out + err).strip()[:800]}")
    printed = out.split(RUN_BANNER, 1)[1]
    printed = printed[1:] if printed.startswith("\n") else printed
    if code == 0:
        return "runs:" + printed
    net = NET_EXCEPTION_RE.search(err + "\n" + out)
    if net:
        return "raises:" + net.group(1).rsplit(".", 1)[-1]
    raise EvidenceError(f"the program exited {code} without an unhandled-exception report:\n{err.strip()[:800]}")


def measure_python(program: str, python: Optional[str] = None) -> str:
    """Run `program` under the python oracle in isolated mode; return its observation."""
    python = python or sys.executable
    with tempfile.TemporaryDirectory(prefix="ruling-evidence-") as tmp:
        source = Path(tmp) / "main.py"
        source.write_text(program, encoding="utf-8")
        code, out, err = _run([python, "-I", str(source)], Path(tmp))
    if code == 0:
        return "runs:" + out
    names = PY_EXCEPTION_RE.findall(err)
    if not names:
        raise EvidenceError(
            f"python exited {code} with no exception line:\n{err.strip()[:800]}"
        )
    name = names[-1].rsplit(".", 1)[-1]
    # A SyntaxError is raised before any statement runs: python's compile-time refusal.
    return (
        "refused:"
        if name in ("SyntaxError", "IndentationError", "TabError")
        else "raises:"
    ) + name


def derive_door(before: str, after: str, python: str = "no-twin") -> Dict[str, object]:
    """The door a decision is, from what the program did before, does after, and does in python."""
    for name, value in (("before", before), ("after", after), ("python", python)):
        if not is_observation(value):
            raise ValueError(f"{name}: not an observation: {value!r}")
    ran_before = kind_of(before) != "refused"
    runs_after = kind_of(after) != "refused"
    changed = before != after
    breaks_running = ran_before and changed
    if not runs_after:
        undo_breaks, why = False, "a refusal can be relaxed later"
    elif not ran_before:
        undo_breaks, why = (
            True,
            "it lets a program run that was refused; programs will be written that need it",
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
    program: str, twin: Optional[str], sharpyc: Optional[Path], python: Optional[str]
) -> Dict[str, object]:
    block: Dict[str, object] = {
        "program": program,
        "sharpy": measure_sharpy(program, sharpyc),
    }
    if twin is None:
        block["python"] = "no-twin"
    else:
        block["twin"] = twin
        block["python"] = measure_python(twin, python)
    return block


def recheck(
    records: List[Tuple[str, Dict[str, object]]],
    sharpyc: Optional[Path],
    python: Optional[str],
) -> List[str]:
    """Re-run each (owner, evidence) pair; return one line per program that drifted from its record."""
    drift: List[str] = []
    for owner, evidence in records:
        seen = measure_sharpy(str(evidence["program"]), sharpyc)
        if seen != evidence.get("sharpy"):
            drift.append(
                f"{owner}: sharpy recorded {evidence.get('sharpy')!r}, now {seen!r}"
            )
        if "twin" in evidence:
            seen = measure_python(str(evidence["twin"]), python)
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
