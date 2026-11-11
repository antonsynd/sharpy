#!/usr/bin/env python3
"""Gate: no allowlist row moves toward a worse failure class.

Some allowlists record, per row, HOW a known-red cell fails. Those statuses order by how badly the
user is served, worst first:

    0 silent-wrong     the program compiles, runs and prints something other than Python
    1 wrong-throw      the program throws at run time where Python does not (or throws another kind)
    2 ice              an internal compiler error (SPY09xx, a C# leak): loud, the user is told
    3 false-refusal    a valid program is rejected with a diagnostic
    4 message-only     the verdict is right; only the message differs
    5 pass             no row: the cell is green

A loud failure is strictly better than a silent one, so a change that trades an ICE for wrong output
is a regression even when the row count does not move. This script compares each COVERED allowlist
at a base ref and at HEAD, row by row (by the row's key), and fails on:

  * a row whose class got worse (lower number above);
  * a NEW row (a key absent at base) whose class is the worst one (silent-wrong);
  * a covered file that is missing at HEAD, or holds a row that does not parse (a changed format
    must be taught here, never skipped);

unless the row at HEAD carries an owner-ruling token (see below). Rows that improve, stay or
disappear pass.

Covered files are named explicitly in COVERED. An allowlist without a per-row status is out of scope.

Owner-ruling token. The name-resolution differential's only free text after the class is its cite,
and NameResolutionDifferentialTests pins that cite to the route's ProposedCite exactly, so a token
there would turn the C# suite red. The token is therefore a comment line, which the C# loader skips,
naming the ruling and the row key:

    # owner-ruling D18 store/attribute/SelfDot/StaticField/Augmented/InitBody

A ruling id is `D<n>` or `R-<LETTERS>` (the owner's ruling vocabulary). The token exempts that key in
that file from both failure kinds.

Usage:
    python3 build_tools/check_allowlist_severity.py [--base <ref>] [--head <ref>] [--target <ref>]

--base defaults to `git merge-base <target> <head>`; --target defaults to origin/$GITHUB_BASE_REF
when set (a pull_request run), else origin/dev. Exit 0 if clean, 1 on any finding, 2 if git
cannot resolve the refs.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from collections.abc import Callable
from dataclasses import dataclass

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SILENT_WRONG = 0
WRONG_THROW = 1
ICE = 2
FALSE_REFUSAL = 3
MESSAGE_ONLY = 4
PASS = 5
SEVERITY_NAMES = {
    SILENT_WRONG: "silent-wrong",
    WRONG_THROW: "wrong-throw",
    ICE: "ice",
    FALSE_REFUSAL: "false-refusal",
    MESSAGE_ONLY: "message-only",
    PASS: "pass",
}
WORST = SILENT_WRONG

RULING_RE = re.compile(r"^#\s*owner-ruling\s+(D\d+|R-[A-Z]+)\s+(\S+)\s*$")


class FormatError(Exception):
    """A covered file holds a line its parser does not recognise."""


@dataclass(frozen=True)
class Parsed:
    rows: dict[str, str]  # key -> status, as spelled in the file
    rulings: dict[str, str]  # key -> ruling id


@dataclass(frozen=True)
class Covered:
    path: str
    severity: dict[str, int]  # status -> severity rank
    parse: Callable[[str], Parsed]


def _parse_name_resolution(text: str) -> Parsed:
    """`<cell-id> <Ice|FalseRefusal|SilentWrong|RuntimeThrow> # #<issue> <lane>` — the shape
    NameResolutionDifferentialTests.LoadAllowlist reads (the class is the last token before ` #`)."""
    rows: dict[str, str] = {}
    rulings: dict[str, str] = {}
    for n, raw in enumerate(text.splitlines(), 1):
        line = raw.strip()
        if not line:
            continue
        if line.startswith("#"):
            m = RULING_RE.match(line)
            if m:
                rulings[m.group(2)] = m.group(1)
            elif line.startswith(("# owner-ruling", "#owner-ruling")):
                raise FormatError(f"line {n}: malformed owner-ruling token '{line}'")
            continue
        split = line.find(" #")
        head = line if split < 0 else line[:split].strip()
        parts = head.rsplit(" ", 1)
        if (
            len(parts) != 2
            or parts[1] not in NAME_RESOLUTION_SEVERITY
            or not parts[0].strip()
        ):
            raise FormatError(
                f"line {n}: '{line}' is not '<cell-id> <class> # #<issue> <lane>'"
            )
        key = parts[0].strip()
        if key in rows:
            raise FormatError(f"line {n}: '{key}' is listed twice")
        rows[key] = parts[1]
    return Parsed(rows, rulings)


# VerdictClass in NameResolutionDifferentialTests.cs; RuntimeThrow is an uncaught CLR exception where the
# cell's tag required a run or a refusal (Judge), so it is the wrong-throw class.
NAME_RESOLUTION_SEVERITY = {
    "SilentWrong": SILENT_WRONG,
    "RuntimeThrow": WRONG_THROW,
    "Ice": ICE,
    "FalseRefusal": FALSE_REFUSAL,
}

COVERED: tuple[Covered, ...] = (
    Covered(
        "src/Sharpy.Compiler.Tests/Conformance/name-resolution-differential-allowlist.txt",
        NAME_RESOLUTION_SEVERITY,
        _parse_name_resolution,
    ),
)


@dataclass(frozen=True)
class Finding:
    path: str
    key: str
    message: str

    def __str__(self) -> str:
        return (
            f"{self.path}: {self.key}: {self.message}"
            if self.key
            else f"{self.path}: {self.message}"
        )


def compare(
    cov: Covered, base_text: str | None, head_text: str | None
) -> list[Finding]:
    """Compare one covered file's base and head contents. None means the file is absent at that ref."""
    if head_text is None:
        return [
            Finding(
                cov.path,
                "",
                "covered allowlist is missing at HEAD (a covered file never disappears silently)",
            )
        ]
    try:
        head = cov.parse(head_text)
    except FormatError as e:
        return [Finding(cov.path, "", f"format changed at HEAD: {e}")]
    base = Parsed({}, {})
    if base_text is not None:
        try:
            base = cov.parse(base_text)
        except FormatError:
            # A base this parser cannot read gives no row to compare against; every head row is new.
            base = Parsed({}, {})

    findings: list[Finding] = []
    for key, status in head.rows.items():
        now = cov.severity[status]
        if key in head.rulings:
            continue
        if key in base.rows:
            was_status = base.rows[key]
            was = cov.severity[was_status]
            if now < was:
                findings.append(
                    Finding(
                        cov.path,
                        key,
                        (
                            f"{was_status} ({SEVERITY_NAMES[was]}) -> {status} ({SEVERITY_NAMES[now]}): worse; "
                            f"needs '# owner-ruling <id> {key}'"
                        ),
                    )
                )
        elif now == WORST:
            findings.append(
                Finding(
                    cov.path,
                    key,
                    (
                        f"new row is {status} ({SEVERITY_NAMES[now]}), the worst class; "
                        f"needs '# owner-ruling <id> {key}'"
                    ),
                )
            )
    for key in head.rulings:
        if key not in head.rows:
            findings.append(Finding(cov.path, key, "owner-ruling token names no row"))
    return findings


def _git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=REPO_ROOT, capture_output=True, text=True, check=False
    )


def show(ref: str, path: str) -> str | None:
    r = _git("show", f"{ref}:{path}")
    return r.stdout if r.returncode == 0 else None


def resolve(ref: str) -> str | None:
    r = _git("rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}")
    return r.stdout.strip() if r.returncode == 0 else None


def default_target() -> str:
    base_ref = os.environ.get("GITHUB_BASE_REF")
    return f"origin/{base_ref}" if base_ref else "origin/dev"


def merge_base(target: str, head: str) -> str | None:
    r = _git("merge-base", target, head)
    return r.stdout.strip() if r.returncode == 0 and r.stdout.strip() else None


def check(base: str, head: str) -> list[Finding]:
    findings: list[Finding] = []
    for cov in COVERED:
        findings.extend(compare(cov, show(base, cov.path), show(head, cov.path)))
    return findings


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    ap.add_argument(
        "--base", help="base ref (default: merge base of --target and --head)"
    )
    ap.add_argument("--head", default="HEAD", help="head ref (default: HEAD)")
    ap.add_argument(
        "--target",
        default=None,
        help="target branch for the merge base (default: origin/$GITHUB_BASE_REF or origin/dev)",
    )
    args = ap.parse_args(argv)

    head = resolve(args.head)
    if head is None:
        print(f"::error::cannot resolve head '{args.head}'", file=sys.stderr)
        return 2
    if args.base:
        base = resolve(args.base)
        if base is None:
            print(f"::error::cannot resolve base '{args.base}'", file=sys.stderr)
            return 2
    else:
        target = args.target or default_target()
        base = merge_base(target, head)
        if base is None:
            print(
                f"::error::no merge base between '{target}' and '{args.head}' (shallow clone or missing ref?)",
                file=sys.stderr,
            )
            return 2

    findings = check(base, head)
    covered = ", ".join(c.path for c in COVERED)
    if not findings:
        print(f"allowlist severity: clean ({base[:9]}..{head[:9]}; covered: {covered})")
        return 0
    for f in findings:
        print(f"::error file={f.path}::{f.key + ': ' if f.key else ''}{f.message}")
    print(
        f"allowlist severity: {len(findings)} finding(s) ({base[:9]}..{head[:9]})",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
