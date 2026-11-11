#!/usr/bin/env python3
"""The scorecard's offline rows, measured once, and the ratchet that keeps them from rising.

``.claude/scripts/scorecard`` prints its allowlist, ``SemanticInfo`` and procedure-bytes rows by
calling ``--row`` here, so the scorecard and the ratchet share one definition of each number.

Usage::

    python3 build_tools/scorecard_metrics.py --row {allowlists,cites,semanticinfo,procedure}
    python3 build_tools/scorecard_metrics.py --check [--baseline <json>] [--repo <root>]
    python3 build_tools/scorecard_metrics.py --lower [--baseline <json>] [--repo <root>]
    python3 build_tools/scorecard_metrics.py --set-baseline KEY [--set-baseline KEY ...]

``--check`` exits 1 when a ratcheted value is above ``build_tools/scorecard-baseline.json`` (a
regression). Below it, procedure and CLAUDE.md bytes also fail until the baseline is lowered in the
same change, so it cannot go stale; allowlist rows below baseline only print a notice, because a
fix that drains rows is routine. ``--lower`` writes the lower values and never raises one;
``--set-baseline KEY`` is the only way to raise one, names the key, and prints the raise.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_BASELINE = REPO_ROOT / "build_tools" / "scorecard-baseline.json"

# Harnesses that landed with a charter plan that seeds rows on purpose. Each entry is the exact
# basename of one tracked allowlist; their rows are ratcheted on their own key, allowlist_rows_seed.
# Adding an entry is an owner-ruled step (contract §11 stage 0); test_scorecard_metrics.py pins this
# tuple so an addition is a deliberate, reviewed diff.
SEED_PLAN_FILES = (
    "name-resolution-differential-allowlist.txt",  # P77b (#2433), plan-3eba57
)

# The procedure a light group reads: CLAUDE.md plus seven skills.
PROCEDURE_FILES = (
    "CLAUDE.md",
    ".claude/skills/create-plan/SKILL.md",
    ".claude/skills/verify-plan/SKILL.md",
    ".claude/skills/implement-plan/SKILL.md",
    ".claude/skills/verify-implementation/SKILL.md",
    ".claude/skills/commit/SKILL.md",
    ".claude/skills/push/SKILL.md",
    ".claude/skills/ci-gate/SKILL.md",
)

SEMANTIC_INFO = "src/Sharpy.Compiler/Semantic/SemanticInfo.cs"
SEMANTIC_INFO_PIN = (
    "src/Sharpy.Compiler.Tests/Semantic/SemanticInfoMergeConformanceTests.cs"
)
DICT_FIELD_RE = re.compile(r"^\s*private readonly (Concurrent)?Dictionary<")
PIN_RE = re.compile(r"MaximumSideTableFieldCount = ([0-9]+)")
CITE_RE = re.compile(r"#[0-9]+")

# Rows that must not rise, in report order: key -> label. The SemanticInfo field count is reported,
# not ratcheted here: the C# pin `MaximumSideTableFieldCount` already holds it.
RATCHETED = {
    "procedure_bytes": "Procedure bytes per light group (CLAUDE.md + seven skills)",
    "claude_md_bytes": "CLAUDE.md bytes",
    "allowlist_rows_outside_seed": "Allowlist rows outside seed-plan harnesses",
    "allowlist_rows_seed": "Allowlist rows in seed-plan harnesses",
}
# Ratcheted rows whose fall below baseline is a notice, not a failure.
NOTICE_WHEN_BELOW = frozenset({"allowlist_rows_outside_seed", "allowlist_rows_seed"})


class MeasureError(Exception):
    """The checkout cannot be measured (e.g. a tracked file is missing from the working tree)."""


@dataclass
class Metrics:
    procedure_bytes: int
    claude_md_bytes: int
    allowlist_files: int
    allowlist_nonempty_files: int
    allowlist_rows_total: int
    allowlist_rows_seed: int
    allowlist_rows_outside_seed: int
    distinct_cites: int
    semanticinfo_dictionaries: int
    semanticinfo_pin: int | None


def _lines(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8", errors="replace")
    lines = text.split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    return lines


def _is_row(line: str) -> bool:
    """A row is any line that is not blank and not a ``#`` comment (leading whitespace ignored)."""
    stripped = line.lstrip(" \t\r\f\v")
    return stripped != "" and not stripped.startswith("#")


def allowlist_files(repo: Path) -> list[str]:
    """Tracked ``*allowlist*.txt`` paths (git pathspec: ``*`` crosses ``/``).

    A directory that is not a git checkout (e.g. a ``git archive <sha>`` extraction, which holds
    exactly the tracked files) is walked with the same pattern instead.
    """
    if not (repo / ".git").exists():
        return sorted(
            rel
            for rel in (
                p.relative_to(repo).as_posix() for p in repo.rglob("*") if p.is_file()
            )
            if fnmatch.fnmatchcase(rel, "*allowlist*.txt")
        )
    out = subprocess.run(
        ["git", "-C", str(repo), "ls-files", "*allowlist*.txt"],
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    return [line for line in out.splitlines() if line]


def is_seed_harness(rel: str) -> bool:
    return Path(rel).name in SEED_PLAN_FILES


def allowlist_rows(repo: Path) -> dict[str, list[str]]:
    """Each tracked allowlist's rows (non-blank, non-comment lines), keyed by repo path."""
    out = {}
    for rel in allowlist_files(repo):
        if not (repo / rel).is_file():
            raise MeasureError(
                f"tracked allowlist {rel} is missing from the working tree; restore it or "
                "`git rm` it, then measure again"
            )
        out[rel] = [line for line in _lines(repo / rel) if _is_row(line)]
    return out


def measure(repo: Path) -> Metrics:
    by_file = allowlist_rows(repo)
    files = list(by_file)
    total = seeded = nonempty = 0
    cites: set[str] = set()
    for rel, rows in by_file.items():
        total += len(rows)
        if rows:
            nonempty += 1
        if is_seed_harness(rel):
            seeded += len(rows)
        for row in rows:
            cites.update(CITE_RE.findall(row))

    proc = sum(
        (repo / f).stat().st_size for f in PROCEDURE_FILES if (repo / f).is_file()
    )
    claude_md = repo / "CLAUDE.md"

    si = repo / SEMANTIC_INFO
    fields = (
        sum(1 for line in _lines(si) if DICT_FIELD_RE.match(line))
        if si.is_file()
        else 0
    )
    pin_file = repo / SEMANTIC_INFO_PIN
    pin_match = (
        PIN_RE.search(pin_file.read_text(errors="replace"))
        if pin_file.is_file()
        else None
    )

    return Metrics(
        procedure_bytes=proc,
        claude_md_bytes=claude_md.stat().st_size if claude_md.is_file() else 0,
        allowlist_files=len(files),
        allowlist_nonempty_files=nonempty,
        allowlist_rows_total=total,
        allowlist_rows_seed=seeded,
        allowlist_rows_outside_seed=total - seeded,
        distinct_cites=len(cites),
        semanticinfo_dictionaries=fields,
        semanticinfo_pin=int(pin_match.group(1)) if pin_match else None,
    )


def render_row(name: str, m: Metrics) -> str:
    """The scorecard's markdown row for one metric (`| Metric | Value | How it was measured |`)."""
    if name == "allowlists":
        cells = (
            "Allowlist rows: total / outside seed-plan harnesses",
            f"{m.allowlist_rows_total} / {m.allowlist_rows_outside_seed}",
            (
                f"{m.allowlist_files} files ({m.allowlist_nonempty_files} non-empty); seed-plan harnesses: "
                f"{', '.join(SEED_PLAN_FILES)} ({m.allowlist_rows_seed} rows)"
            ),
        )
    elif name == "cites":
        cells = (
            "Distinct issues cited by allowlist rows",
            str(m.distinct_cites),
            "rows citing a closed issue must be 0: `build_tools/check_allowlist_issue_state.sh`",
        )
    elif name == "semanticinfo":
        pin = "?" if m.semanticinfo_pin is None else str(m.semanticinfo_pin)
        cells = (
            "SemanticInfo dictionary fields (pin ≤ / counted)",
            f"{pin} / {m.semanticinfo_dictionaries}",
            "pin in SemanticInfoMergeConformanceTests; private readonly dictionary fields in SemanticInfo.cs",
        )
    elif name == "procedure":
        cells = (
            "Procedure bytes per light group; CLAUDE.md",
            f"{m.procedure_bytes}; {m.claude_md_bytes}",
            "CLAUDE.md + create-plan, verify-plan, implement-plan, verify-implementation, commit, push, ci-gate skills",
        )
    else:
        raise ValueError(f"unknown row {name!r}")
    return "| " + " | ".join(cells) + " |"


def load_baseline(path: Path) -> dict[str, int]:
    data = json.loads(path.read_text())
    missing = [k for k in RATCHETED if k not in data]
    extra = [k for k in data if k not in RATCHETED]
    if missing or extra:
        raise ValueError(f"{path}: missing keys {missing}, unknown keys {extra}")
    return {k: int(data[k]) for k in RATCHETED}


def compare(
    current: Metrics, baseline: dict[str, int], baseline_path: str
) -> tuple[list[str], list[str]]:
    """(failures, notices): one message per ratcheted value that differs from its baseline."""
    problems, notices = [], []
    for key, label in RATCHETED.items():
        now, base = getattr(current, key), baseline[key]
        if now > base:
            problems.append(
                f"{label} ({key}) is {now}, above its baseline {base} in {baseline_path} by {now - base}. "
                f"Bring it back to {base} or below. Raising the baseline in the same PR is an owner "
                f"decision: state it, with the owner's words, in the PR body."
            )
        elif now < base:
            msg = (
                f"{label} ({key}) is {now}, below its baseline {base} in {baseline_path} by {base - now}. "
                f"Lower the baseline to {now} (python3 build_tools/scorecard_metrics.py --lower)"
            )
            if key in NOTICE_WHEN_BELOW:
                notices.append(msg + " when convenient.")
            else:
                problems.append(msg + " in this PR so it cannot go stale.")
    return problems, notices


def lower(current: Metrics, path: Path) -> list[str]:
    data = json.loads(path.read_text())
    changed = []
    for key in RATCHETED:
        now = getattr(current, key)
        if now < int(data[key]):
            changed.append(f"{key}: {data[key]} -> {now}")
            data[key] = now
    path.write_text(json.dumps(data, indent=2) + "\n")
    return changed


def set_baseline(current: Metrics, path: Path, keys: list[str]) -> list[str]:
    """Set each NAMED key to its measured value, raising it if need be. Never touches another key.

    The only way to raise a baseline, so a raise is always a named, printed act: the caller states
    it, with the owner's words, in the PR body.
    """
    unknown = [k for k in keys if k not in RATCHETED]
    if unknown or not keys:
        raise ValueError(
            f"--set-baseline needs ratcheted keys from {list(RATCHETED)}; got {keys}"
        )
    data = json.loads(path.read_text())
    lines = []
    for key in keys:
        old, now = int(data[key]), getattr(current, key)
        if now > old:
            lines.append(
                f"RAISED {key}: {old} -> {now} (+{now - old}). This is an owner decision: state it, "
                "with the owner's words and what raised it, in the PR body."
            )
        elif now < old:
            lines.append(f"lowered {key}: {old} -> {now}")
        else:
            lines.append(f"unchanged {key}: {now}")
        data[key] = now
    path.write_text(json.dumps(data, indent=2) + "\n")
    return lines


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument(
        "--row", choices=("allowlists", "cites", "semanticinfo", "procedure")
    )
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--lower", action="store_true")
    mode.add_argument(
        "--set-baseline",
        metavar="KEY",
        action="append",
        choices=tuple(RATCHETED),
        help="set the named key to its measured value, raising it if need be (repeatable)",
    )
    mode.add_argument("--json", action="store_true", help="print every measured value")
    ap.add_argument("--repo", type=Path, default=REPO_ROOT)
    ap.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    a = ap.parse_args(argv)
    try:
        m = measure(a.repo.resolve())
    except MeasureError as e:
        print(f"scorecard_metrics: FAIL: {e}", file=sys.stderr)
        return 1
    if a.row:
        print(render_row(a.row, m))
        return 0
    if a.json:
        print(json.dumps(m.__dict__, indent=2))
        return 0
    if a.set_baseline:
        for line in set_baseline(m, a.baseline, a.set_baseline):
            print(line)
        return 0
    if a.lower:
        for line in lower(m, a.baseline) or ["nothing to lower"]:
            print(line)
        return 0
    problems, notices = compare(m, load_baseline(a.baseline), str(a.baseline))
    for key in RATCHETED:
        print(f"{key}: {getattr(m, key)}")
    for n in notices:
        print(f"NOTICE: {n}")
    for p in problems:
        print(f"FAIL: {p}")
    print("RESULT: " + ("FAIL" if problems else "OK"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
