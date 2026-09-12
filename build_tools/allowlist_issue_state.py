#!/usr/bin/env python3
"""Check that allowlist rows and C# Skip attributes cite only OPEN GitHub issues.

A row (or Skip) citing a CLOSED issue is stale — the fix landed but the entry was
not drained. Exit 0 if clean, 1 if offences found, 2 if gh is unavailable.

Usage:
    python3 build_tools/allowlist_issue_state.py [--paths FILE ...]
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import subprocess
import sys
from dataclasses import dataclass, field

ISSUE_RE = re.compile(r"#(\d+)")
SKIP_RE = re.compile(
    r'\[\s*(?:Fact|Theory)\s*\(\s*(?:[^)]*,\s*)?Skip\s*=\s*"([^"]*)"'
)
DEVIATIONS_YAML = "deviations.yaml"
NO_ISSUE_EXEMPT = "no-issue:"


@dataclass
class Row:
    file: str
    line: int
    text: str
    cites: list[int] = field(default_factory=list)
    exempt: bool = False


def _is_comment(line: str) -> bool:
    return line.lstrip().startswith("#")


def _extract_issues(text: str) -> list[int]:
    return [int(m) for m in ISSUE_RE.findall(text)]


def scan_allowlist(path: str) -> list[Row]:
    rows: list[Row] = []
    with open(path) as f:
        lines = f.readlines()

    paragraph_cites: list[int] = []
    paragraph_has_deviations = False
    in_paragraph = False

    for i, raw in enumerate(lines, 1):
        line = raw.rstrip("\n")
        stripped = line.strip()

        if not stripped:
            if in_paragraph:
                pass
            in_paragraph = False
            paragraph_cites = []
            paragraph_has_deviations = False
            continue

        if _is_comment(stripped):
            in_paragraph = True
            paragraph_cites.extend(_extract_issues(stripped))
            if DEVIATIONS_YAML in stripped:
                paragraph_has_deviations = True
            continue

        cites = _extract_issues(stripped)
        exempt = DEVIATIONS_YAML in stripped or paragraph_has_deviations

        if not cites:
            cites = list(paragraph_cites)

        rows.append(Row(
            file=path, line=i, text=stripped,
            cites=cites, exempt=exempt,
        ))

        in_paragraph = False
        paragraph_cites = []
        paragraph_has_deviations = False

    return rows


def scan_cs_file(path: str) -> list[Row]:
    rows: list[Row] = []
    with open(path) as f:
        content = f.read()

    for m in SKIP_RE.finditer(content):
        skip_text = m.group(1)
        offset = content[:m.start()].count("\n") + 1
        cites = _extract_issues(skip_text)
        exempt = NO_ISSUE_EXEMPT in skip_text

        if cites:
            rows.append(Row(
                file=path, line=offset, text=skip_text,
                cites=cites, exempt=False,
            ))
        elif not exempt:
            rows.append(Row(
                file=path, line=offset, text=skip_text,
                cites=[], exempt=False,
            ))

    return rows


def scan(paths: list[str]) -> list[Row]:
    rows: list[Row] = []
    for path in paths:
        if path.endswith(".txt"):
            rows.extend(scan_allowlist(path))
        elif path.endswith(".cs"):
            rows.extend(scan_cs_file(path))
    return rows


def query_states(numbers: list[int]) -> dict[int, str]:
    try:
        subprocess.run(
            ["gh", "--version"],
            capture_output=True, check=True,
        )
    except (FileNotFoundError, subprocess.CalledProcessError):
        print("cannot verify issue state — gh not available or not authenticated",
              file=sys.stderr)
        sys.exit(2)

    states: dict[int, str] = {}
    for num in sorted(set(numbers)):
        try:
            result = subprocess.run(
                ["gh", "issue", "view", str(num), "--json", "state", "-q", ".state"],
                capture_output=True, text=True, check=True,
            )
            states[num] = result.stdout.strip()
        except subprocess.CalledProcessError:
            print(f"cannot verify issue state for #{num} — gh failed",
                  file=sys.stderr)
            sys.exit(2)
    return states


def _default_paths() -> list[str]:
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

    txt_paths = glob.glob(
        os.path.join(repo, "src", "**", "Conformance", "*-allowlist.txt"),
        recursive=True,
    )

    cs_globs = glob.glob(
        os.path.join(repo, "src", "*.Tests", "**", "*.cs"),
        recursive=True,
    )
    cs_paths = [
        p for p in cs_globs
        if "/bin/" not in p
        and "/obj/" not in p
        and not p.endswith(".expected.cs")
        and os.sep + "bin" + os.sep not in p
        and os.sep + "obj" + os.sep not in p
    ]

    return sorted(txt_paths + cs_paths)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Check allowlist rows and C# Skips cite only OPEN issues",
    )
    parser.add_argument(
        "--paths", nargs="+", default=None,
        help="Files to scan (default: src/**/Conformance/*-allowlist.txt + src/*.Tests/**/*.cs)",
    )
    args = parser.parse_args()

    paths = args.paths if args.paths else _default_paths()

    all_rows = scan(paths)

    all_cites: list[int] = []
    uncited_rows: list[Row] = []
    cited_rows: list[Row] = []

    for row in all_rows:
        if row.exempt:
            continue
        if row.cites:
            all_cites.extend(row.cites)
            cited_rows.append(row)
        elif row.file.endswith(".cs"):
            uncited_rows.append(row)

    if not all_cites and not uncited_rows:
        sys.exit(0)

    if all_cites:
        states = query_states(all_cites)
    else:
        states = {}

    offences: list[str] = []

    for row in cited_rows:
        for num in row.cites:
            state = states.get(num, "UNKNOWN")
            if state == "CLOSED":
                offences.append(
                    f"{row.file}:{row.line}  {row.text}  → #{num} CLOSED"
                )

    for row in uncited_rows:
        offences.append(
            f"{row.file}:{row.line}  Skip without issue cite: \"{row.text}\"  "
            f"→ missing #NNNN"
        )

    if offences:
        for o in offences:
            print(o)
        sys.exit(1)
    else:
        sys.exit(0)


if __name__ == "__main__":
    main()
