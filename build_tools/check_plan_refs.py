#!/usr/bin/env python3
"""Mechanical reference check for a plan file (``/verify-plan`` Dimension 1).

Extracts every concrete reference a plan makes and checks it against the checkout:

* repo-relative **file paths** (``src/…``, ``docs/…``, ``build_tools/…``, ``.claude/…``) — MISSING is an error,
  except ``.spy`` paths (usually a fixture project's layout, e.g. ``src/main.spy``) and gitignored
  ``.claude/plans/…`` paths, which are reported as UNRESOLVED (warning);
* ``file:line`` references (``TypeChecker.cs:731``, ``Semantic/Foo.cs:12–20``, and the plans' shorthand
  ``Access.Calls.cs:6222`` for ``TypeChecker.Expressions.Access.Calls.cs``) — resolved by path suffix,
  on a ``/`` boundary and as a bare character suffix (both sets are candidates); a line past the end of
  every candidate is an error, several candidates is AMBIGUOUS (warning);
* ``SPYnnnn`` diagnostic codes — checked against ``DiagnosticCodes.cs`` (warning: a plan may propose one);
* PascalCase **symbols** in backticks, prose only (fenced code is skipped) — checked against the
  identifier set of ``src/**/*.cs`` (warning: a plan may introduce one);
* ``#NNNN`` **issue** numbers — with ``--issues``, ``gh issue view`` reports state (needs the network).

Usage::

    python3 build_tools/check_plan_refs.py <plan.md> [--repo <root>] [--issues] [--strict] [--json]

    python3 build_tools/check_plan_refs.py --caps-all [--repo <root>]

Exit code 1 when any path is missing or a ``file:line`` points past end-of-file (with ``--strict``,
also when any symbol or code is unknown), or when the plan breaks an R-LW cap (body > 20 KB, more
than 2 verify passes without an owner-ruling line; see ``cap_violations``); 0 otherwise.
``--caps-all`` checks only the caps, on every ``docs/process/plans/plan-*.md``. Both modes also print a
NOTE for each section budget (``SECTION_BUDGETS``) a plan that is not grandfathered exceeds; a note
never changes the exit code. Never edits anything.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

PATH_EXTS = r"(?:cs|md|py|sh|spy|txt|json|ya?ml|props|csproj|sln|expected|error|warning|features)"
PATH_RE = re.compile(
    r"(?<![\w/.\-])((?:src|docs|build_tools|benchmarks|\.claude|\.github)/[\w\-./]*\w\."
    + PATH_EXTS
    + r")\b"
)
FILELINE_RE = re.compile(
    r"(?<![\w/])((?:[\w\-]+/)*[\w\-]+(?:\.[\w\-]+)*\.(?:cs|py|spy|md|sh|ya?ml)):(\d+)(?:[–\-](\d+))?"
)
SPY_RE = re.compile(r"\bSPY\d{4}\b")
ISSUE_RE = re.compile(r"(?<![\w&/])#(\d{3,5})\b")
BACKTICK_RE = re.compile(r"`([^`\n]{2,120})`")
IDENT_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
SYMBOL_RE = re.compile(
    r"^[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+$"
)  # ≥2 humps: TypeChecker, MergeFrom
FENCE_RE = re.compile(r"```.*?```", re.S)
SKIP_DIRS = {".git", "bin", "obj", "node_modules", "__pycache__", ".claude"}


def list_files(repo: Path) -> list[str]:
    """Tracked files via git; falls back to a walk when the root is not a git checkout."""
    try:
        out = subprocess.run(
            ["git", "-C", str(repo), "ls-files"],
            capture_output=True,
            text=True,
            check=True,
        ).stdout
        files = [l for l in out.splitlines() if l]
        if files:
            return files
    except (OSError, subprocess.CalledProcessError):
        pass
    files = []
    for root, dirs, names in os.walk(repo):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for n in names:
            files.append(os.path.relpath(os.path.join(root, n), repo))
    return files


def line_count(path: Path) -> int:
    with open(path, "rb") as f:
        return sum(1 for _ in f)


def identifier_set(repo: Path, files: list[str]) -> set[str]:
    idents: set[str] = set()
    for rel in files:
        if not rel.startswith("src/") or not rel.endswith(".cs"):
            continue
        try:
            idents.update(IDENT_RE.findall((repo / rel).read_text(errors="replace")))
        except OSError:
            continue
    return idents


def known_codes(repo: Path) -> set[str]:
    p = repo / "src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs"
    return set(SPY_RE.findall(p.read_text(errors="replace"))) if p.exists() else set()


def check(plan_text: str, repo: Path, *, with_issues: bool = False) -> dict:
    files = list_files(repo)
    fileset = set(files)
    prose = FENCE_RE.sub("", plan_text)
    result: dict = {
        "paths": {"ok": [], "missing": [], "unresolved": []},
        "filelines": {"ok": [], "past_eof": [], "missing": [], "ambiguous": []},
        "codes": {"ok": [], "unknown": []},
        "symbols": {"ok": [], "unknown": []},
        "issues": {},
    }

    # paths (whole text — commands in code blocks name real files too)
    for ref in sorted(set(PATH_RE.findall(plan_text))):
        if (repo / ref).exists():
            result["paths"]["ok"].append(ref)
        elif ref.endswith(".spy") or ref.startswith(".claude/plans/"):
            result["paths"]["unresolved"].append(
                ref
            )  # fixture-relative or gitignored; the verifier decides
        else:
            result["paths"]["missing"].append(ref)

    # file:line
    by_suffix_cache: dict[str, list[str]] = {}
    for m in sorted(set(FILELINE_RE.findall(plan_text))):
        token, line_s, line_end_s = m
        want = int(line_end_s or line_s)
        key = token.lstrip("./")
        if key not in by_suffix_cache:
            exact = [f for f in files if f == key or f.endswith("/" + key)]
            # the plans' partial-class shorthand: `Access.Calls.cs` for `…/TypeChecker.Expressions.Access.Calls.cs`,
            # `Cli/Commands/RunCommand.cs` for `src/Sharpy.Cli/Commands/RunCommand.cs`
            shorthand = [f for f in files if f.endswith(key) and f not in exact]
            by_suffix_cache[key] = (
                exact + shorthand
            )  # both: `Format.cs` names Partial.Builtins/Format.cs AND StringExtensions.Format.cs
        cands = by_suffix_cache[key]
        label = f"{token}:{line_s}" + (f"-{line_end_s}" if line_end_s else "")
        if not cands:
            result["filelines"]["missing"].append(label)
            continue
        counts = {c: line_count(repo / c) for c in cands if (repo / c).is_file()}
        if len(cands) > 1:
            result["filelines"]["ambiguous"].append(
                f"{label} → {len(cands)} candidates"
            )
        if any(n >= want for n in counts.values()):
            result["filelines"]["ok"].append(label)
        else:
            result["filelines"]["past_eof"].append(
                f"{label} (file has {max(counts.values(), default=0)} lines)"
            )

    # SPY codes
    codes = known_codes(repo)
    for c in sorted(set(SPY_RE.findall(plan_text))):
        (result["codes"]["ok"] if c in codes else result["codes"]["unknown"]).append(c)

    # symbols (prose only)
    idents = identifier_set(repo, files)
    seen: set[str] = set()
    for tick in BACKTICK_RE.findall(prose):
        for part in re.split(r"[.<>\[\](),:; ]+", tick):
            if part and SYMBOL_RE.match(part) and part not in seen:
                seen.add(part)
                (
                    result["symbols"]["ok"]
                    if part in idents
                    else result["symbols"]["unknown"]
                ).append(part)
    result["symbols"]["ok"].sort()
    result["symbols"]["unknown"].sort()

    # issues
    numbers = sorted({int(n) for n in ISSUE_RE.findall(plan_text)})
    if with_issues:
        for n in numbers:
            try:
                out = subprocess.run(
                    [
                        "gh",
                        "issue",
                        "view",
                        str(n),
                        "--json",
                        "state,title,stateReason",
                    ],
                    capture_output=True,
                    text=True,
                    check=True,
                ).stdout
                d = json.loads(out)
                result["issues"][str(n)] = {
                    "state": d.get("state"),
                    "reason": d.get("stateReason"),
                    "title": d.get("title"),
                }
            except (OSError, subprocess.CalledProcessError, json.JSONDecodeError) as e:
                result["issues"][str(n)] = {
                    "state": "ERROR",
                    "reason": str(e)[:80],
                    "title": "",
                }
    else:
        result["issues"] = {str(n): {"state": "unchecked"} for n in numbers}
    return result


def render(r: dict, *, strict: bool) -> tuple[str, int]:
    lines = []

    def sec(title, ok, bad, bad_label):
        lines.append(f"{title}: {len(ok)} ok, {len(bad)} {bad_label}")
        for b in bad:
            lines.append(f"  - {b}")

    sec("paths", r["paths"]["ok"], r["paths"]["missing"], "MISSING")
    for u in r["paths"]["unresolved"]:
        lines.append(
            f"  - unresolved (fixture-relative .spy or gitignored plan path): {u}"
        )
    fl = r["filelines"]
    lines.append(
        f"file:line: {len(fl['ok'])} ok, {len(fl['past_eof'])} PAST-EOF, {len(fl['missing'])} MISSING, {len(fl['ambiguous'])} ambiguous"
    )
    for b in fl["past_eof"]:
        lines.append(f"  - past eof: {b}")
    for b in fl["missing"]:
        lines.append(f"  - missing: {b}")
    for b in fl["ambiguous"]:
        lines.append(f"  - ambiguous: {b}")
    sec(
        "SPY codes",
        r["codes"]["ok"],
        r["codes"]["unknown"],
        "not in DiagnosticCodes.cs (new code proposed?)",
    )
    sec(
        "symbols (prose, PascalCase)",
        r["symbols"]["ok"],
        r["symbols"]["unknown"],
        "not found in src/**/*.cs (new symbol proposed?)",
    )
    closed = {n: v for n, v in r["issues"].items() if v.get("state") == "CLOSED"}
    errs = {n: v for n, v in r["issues"].items() if v.get("state") == "ERROR"}
    unchecked = sum(1 for v in r["issues"].values() if v.get("state") == "unchecked")
    lines.append(
        f"issues: {len(r['issues'])} referenced; {len(closed)} CLOSED; {len(errs)} lookup errors; {unchecked} unchecked (use --issues)"
    )
    for n, v in closed.items():
        lines.append(f"  - #{n} CLOSED ({v.get('reason')}): {v.get('title')}")
    for n, v in errs.items():
        lines.append(f"  - #{n} ERROR: {v.get('reason')}")
    hard = bool(r["paths"]["missing"] or fl["past_eof"] or fl["missing"])
    soft = bool(r["codes"]["unknown"] or r["symbols"]["unknown"])
    code = 1 if hard or (strict and soft) else 0
    lines.append(
        "RESULT: " + ("FAIL" if code else "OK") + (" (strict)" if strict else "")
    )
    return "\n".join(lines), code


# --- plan caps (charter ruling R-LW) -----------------------------------------------------------------
# A plan's BODY is at most BODY_CAP_BYTES: the whole file except
# * the stamp/marker comment lines on top;
# * each record section the skills write (RECORD_SECTIONS), from its `## ` heading to the next `## `
#   heading, its `###` subsections included;
# * everything from the first TERMINAL_RECORDS heading to the end: /verify-implementation appends its
#   record last, after implementation, when the plan text is final.
# Every other `## ` section is body wherever it sits, so a record placed mid-plan hides nothing; a
# heading-like line inside a fenced code block is not a heading. `## Revision log` is body: under the
# pass cap it is a few lines, and as a record it would be an uncapped place to answer findings, which a
# revision does by rewriting the plan's text.
# A plan records at most PASS_CAP `/verify-plan` passes; each pass beyond needs its own owner-ruling line:
#     Extra verify pass 3: owner 2026-11-12 "<the owner's words>"
# The skills state these numbers; build_tools/tests/test_plan_caps.py pins each statement to its constant.

BODY_CAP_BYTES = 20_480
PASS_CAP = 2
# Net body growth one revision may add (/create-plan › Revising a plan). Stated, not measured: the file
# holds only its current text, so one revision's growth is not visible in it.
REVISION_GROWTH_BYTES = 3_072
PLAN_GLOB = "docs/process/plans/plan-*.md"

# The `## ` record headings the skills write: /verify-plan's summary, /implement-plan's checklist,
# /verify-implementation's record and the report it holds. Tests pin the list to the skills.
RECORD_SECTIONS = (
    "Verification Summary",
    "Implementation Checklist",
    "Implementation Verification Record",
    "Implementation Verification Report",
)
TERMINAL_RECORDS = (
    "Implementation Verification Record",
    "Implementation Verification Report",
)
STAMP_LINE_RE = re.compile(r"^\s*(?:<!--.*-->\s*)?$")
VERIFY_STAMP_RE = re.compile(r"^<!-- Verified by /verify-plan\b", re.MULTILINE)
REVISION_LOG_RE = re.compile(
    r"^## Revision log\b.*?(?=^## |\Z)", re.MULTILINE | re.DOTALL
)
# Sections a verifier writes: `## Verification Summary` (any suffix, e.g. "— round 6").
SUMMARY_SECTION_RE = re.compile(
    r"^## Verification Summary\b.*?(?=^## (?!Verification Summary\b)|\Z)",
    re.MULTILINE | re.DOTALL,
)
SUMMARY_ROUND_RE = re.compile(
    r"^## Verification Summary\W+(?:round|pass) (\d+)\b", re.MULTILINE | re.IGNORECASE
)
PASS_HEADING_RE = re.compile(r"^### Pass (\d+)\b", re.MULTILINE)
RESULT_PASS_RE = re.compile(r"^\*\*Result:\*\*.*?\(pass (\d+)\b", re.MULTILINE)
STAMP_ROUND_RE = re.compile(
    r"^<!-- Verified by /verify-plan[^>]*?\((?:round|pass) (\d+)", re.MULTILINE
)
# A revision-log entry that records a failed pass, as the revising session writes it
# (`/verify-plan` skill: "naming the pass's sha, its result and dimension as
# (NEEDS REVISION, Dimension N)"): a bullet or bold line that opens with an optional date and
# `@ <sha>`, then the verdict in parentheses with no other parenthesis before it. Spellings in the
# committed plans: `- @ bb6c693ce — revised after the 1st pass (NEEDS REVISION, Dimension 6): …`,
# `- 2026-11-07 @ c61599c63, pass 1: \`(NEEDS REVISION, Dimension 6)\` …`,
# `**2026-11-03 @ 89fba5eec — after verify round 1 (NEEDS REVISION / CELL), by the plan author:**`.
FAILED_PASS_ENTRY_RE = re.compile(
    r"^(?:[-*] |\*\*)(?:\d{4}-\d{2}-\d{2} )?@ [0-9a-f]{7,40}\b[^()\n]*?`?\(NEEDS REVISION\b"
)
EXTRA_PASS_RE = re.compile(
    r"^\W*Extra verify pass (\d+):\s*owner,? \d{4}-\d{2}-\d{2},? [\"“].+[\"”]",
    re.MULTILINE,
)

# Plans that exceeded a cap when the check landed (@ fd876b842 on origin/dev): name -> (body bytes,
# verify passes). A grandfathered plan may not grow on either axis. The sizes are re-measured only
# when the body rule itself changes, a correction of the measure, not growth, listed old -> new in
# the change that makes it. A plan joins only when such a correction shows that a plan written before
# the cap was already over it (plan-5c21c8: its plan sections sat below a mid-plan checklist). No plan
# written under the cap joins.
GRANDFATHERED: dict[str, tuple[int, int]] = {
    "plan-05dd79.md": (68488, 1),
    "plan-0667c5.md": (88262, 1),
    "plan-0c1382.md": (77212, 1),
    "plan-0ca7b7.md": (207103, 1),
    "plan-11eef9.md": (46251, 1),
    "plan-14853b.md": (70933, 1),
    "plan-155752.md": (22574, 1),
    "plan-16cca3.md": (109139, 2),
    "plan-17ad85.md": (91720, 1),
    "plan-2003b4.md": (74045, 1),
    "plan-202526.md": (30583, 1),
    "plan-21d2f5.md": (22145, 4),
    "plan-299c1b.md": (53054, 1),
    "plan-311458.md": (80526, 1),
    "plan-3eba57.md": (70199, 2),
    "plan-43f241.md": (77732, 1),
    "plan-460cbf.md": (50042, 6),
    "plan-46b871.md": (37076, 1),
    "plan-499995.md": (83591, 1),
    "plan-4d6288.md": (67455, 1),
    "plan-4e3055.md": (31634, 1),
    "plan-52c6fe.md": (68164, 1),
    "plan-5379fe.md": (94720, 1),
    "plan-5c21c8.md": (31395, 1),
    "plan-638e8b.md": (46717, 3),
    "plan-683c8b.md": (60387, 1),
    "plan-6ca898.md": (86622, 1),
    "plan-6ce1a5.md": (54464, 1),
    "plan-6e12b5.md": (24608, 1),
    "plan-757fbb.md": (76731, 1),
    "plan-78c581.md": (71032, 1),
    "plan-791d3a.md": (69883, 1),
    "plan-82187d.md": (67791, 1),
    "plan-88736c.md": (23088, 5),
    "plan-92335a.md": (32862, 1),
    "plan-950124.md": (70014, 1),
    "plan-96f8e0.md": (73755, 1),
    "plan-99d863.md": (76475, 1),
    "plan-9ef825.md": (72295, 1),
    "plan-a79696.md": (92559, 1),
    "plan-add686.md": (54854, 1),
    "plan-be3b1f.md": (50072, 1),
    "plan-be89c1.md": (51379, 1),
    "plan-bf0244.md": (110626, 1),
    "plan-c068ff.md": (83049, 1),
    "plan-cbdf79.md": (76991, 1),
    "plan-d31acd.md": (67654, 1),
    "plan-d35e69.md": (99271, 1),
    "plan-d923d3.md": (77148, 1),
    "plan-de9fe3.md": (109209, 1),
    "plan-e31e76.md": (40362, 1),
    "plan-e39946.md": (61214, 1),
    "plan-eaaed2.md": (30960, 3),
    "plan-ebd58b.md": (48375, 1),
    "plan-ed89b6.md": (179854, 10),
    "plan-ee5544.md": (50484, 1),
    "plan-f164be.md": (61762, 1),
    "plan-f4fb34.md": (51370, 1),
    "plan-f8f73c.md": (72602, 1),
    "plan-f92797.md": (73051, 1),
    "plan-remediation-round-2026-08-13.md": (52988, 0),
    "plan-remediation-round-2026-08-18.md": (24955, 0),
    "plan-remediation-round-2026-08-22.md": (23443, 0),
    "plan-remediation-round-2026-08-27.md": (70133, 1),
}


FENCE_LINE_RE = re.compile(r"^ {0,3}(```|~~~)")
# A heading's name is its text before a `(`, `:`, an en or em dash, or a spaced hyphen.
HEADING_SUFFIX_RE = re.compile(r"\s*[(:]|\s*[—–]|\s+-\s")


def h2_headings(text: str) -> list[tuple[int, str]]:
    """(offset, heading text) of each `## ` line outside fenced code blocks."""
    out, pos, fence = [], 0, None
    for line in text.splitlines(keepends=True):
        bare = line.rstrip("\r\n")
        m = FENCE_LINE_RE.match(bare)
        if m:
            if fence is None:
                fence = m.group(1)
            elif bare.lstrip().startswith(fence):
                fence = None
        elif fence is None and bare.startswith("## "):
            out.append((pos, bare[3:].strip()))
        pos += len(line)
    return out


def heading_name(heading: str) -> str:
    return HEADING_SUFFIX_RE.split(heading, maxsplit=1)[0].strip().casefold()


_RECORD_KEYS = {r.casefold(): r for r in RECORD_SECTIONS}


def record_key(heading: str) -> str | None:
    """The RECORD_SECTIONS name a `## ` heading carries, or None for a plan section."""
    return _RECORD_KEYS.get(heading_name(heading))


def sections(text: str) -> list[tuple[str, str]]:
    """(heading text, section text from its `## ` line to the next one) for each `## ` section."""
    hs = h2_headings(text)
    return [
        (h, text[p : hs[i + 1][0] if i + 1 < len(hs) else len(text)])
        for i, (p, h) in enumerate(hs)
    ]


def plan_body(text: str) -> str:
    """The plan without its records (see the comment above BODY_CAP_BYTES)."""
    hs = h2_headings(text)
    first = next((i for i, (_, h) in enumerate(hs) if record_key(h)), None)
    head = text[: hs[first][0]] if first is not None else text
    below = []
    for i in range(first if first is not None else len(hs), len(hs)):
        start, heading = hs[i]
        key = record_key(heading)
        if key in TERMINAL_RECORDS:
            break
        if not key:
            below.append(text[start : hs[i + 1][0] if i + 1 < len(hs) else len(text)])
    lines = head.split("\n")
    i = 0
    while i < len(lines) and STAMP_LINE_RE.match(lines[i]):
        i += 1
    return "\n".join(lines[i:]) + "".join(below)


def body_bytes(text: str) -> int:
    return len(plan_body(text).encode("utf-8"))


def verify_passes(text: str) -> int:
    """The number of `/verify-plan` passes the plan records.

    The largest of: a numbered pass a verifier wrote (`### Pass N` inside a `## Verification Summary`
    section, `## Verification Summary — round N`, `**Result:** … (pass N`, a stamp's `(round N`);
    the failed-pass entries of the `## Revision log` (`FAILED_PASS_ENTRY_RE`), plus one for the
    current stamp; one when the plan carries a stamp. Free text that merely mentions a pass, a
    NEEDS REVISION verdict or a `### Pass N` design heading in the body is not counted.
    """
    stamped = 1 if VERIFY_STAMP_RE.search(text) else 0
    numbered = [int(n) for n in SUMMARY_ROUND_RE.findall(text)]
    numbered += [int(n) for n in STAMP_ROUND_RE.findall(text)]
    for section in SUMMARY_SECTION_RE.findall(text):
        numbered += [int(n) for n in PASS_HEADING_RE.findall(section)]
        numbered += [int(n) for n in RESULT_PASS_RE.findall(section)]
    failed = sum(
        1
        for log in REVISION_LOG_RE.findall(text)
        for line in log.split("\n")
        if FAILED_PASS_ENTRY_RE.match(line)
    )
    return max([stamped, failed + stamped] + numbered)


# --- section budgets ---------------------------------------------------------------------------------
# The `## ` headings of the plan templates in `.claude/skills/create-plan/SKILL.md` (full and light),
# plus the `## Revision log` a revision appends. The one list: tests pin it to the templates.
PLAN_SECTIONS = (
    "Context",
    "Current State",
    "Scope",
    "Defect Class",
    "Adversarial Review",
    "Design Decisions",
    "Implementation",
    "Acceptance",
    "Testing Strategy",
    "Issues to Close",
    "Class trackers this plan may post cells to",
    "Revision log",
)
# Per-section byte budgets for a plan that is not grandfathered: reported, never a failure (the body
# cap is the one gate). (label, sections, bytes).
SECTION_BUDGETS = (
    ("Context + Current State", ("Context", "Current State"), 2_560),
    ("Adversarial Review", ("Adversarial Review",), 1_536),
    (
        "Scope + Issues to Close + Class trackers",
        ("Scope", "Issues to Close", "Class trackers this plan may post cells to"),
        2_048,
    ),
)
_SECTION_KEYS = {s.casefold(): s for s in PLAN_SECTIONS}


def section_key(heading: str) -> str | None:
    """The PLAN_SECTIONS name a `## ` heading carries (its text before any suffix), or None."""
    return _SECTION_KEYS.get(heading_name(heading))


def section_bytes(text: str) -> dict[str, int]:
    """Body bytes per PLAN_SECTIONS name (a name that heads several sections sums them)."""
    out: dict[str, int] = defaultdict(int)
    for heading, sec in sections(plan_body(text)):
        key = section_key(heading)
        if key:
            out[key] += len(sec.encode("utf-8"))
    return dict(out)


def budget_report(text: str) -> list[tuple[str, int, int]]:
    """(label, bytes, budget) for each SECTION_BUDGETS row."""
    sizes = section_bytes(text)
    return [
        (label, sum(sizes.get(s, 0) for s in names), cap)
        for label, names, cap in SECTION_BUDGETS
    ]


def budget_notes(name: str, text: str) -> list[str]:
    """A notice per budget the plan exceeds; none for a grandfathered plan."""
    if name in GRANDFATHERED:
        return []
    return [
        f"{name}: {label} is {size:,} bytes, over its {cap:,}-byte budget "
        "(a notice; the body cap is the gate)"
        for label, size, cap in budget_report(text)
        if size > cap
    ]


def extra_pass_rulings(text: str) -> set[int]:
    return {int(n) for n in EXTRA_PASS_RE.findall(text)}


def cap_violations(name: str, text: str) -> list[str]:
    """Messages for each R-LW cap the plan breaks; a grandfathered plan is held to its frozen values."""
    size, passes = body_bytes(text), verify_passes(text)
    grandfathered = GRANDFATHERED.get(name)
    ruled, allowed = extra_pass_rulings(text), PASS_CAP
    while allowed + 1 in ruled:
        allowed += 1
    problems = []
    if grandfathered:
        g_size, g_passes = grandfathered
        if size > g_size:
            problems.append(
                f"{name}: body is {size:,} bytes, above its grandfathered {g_size:,} "
                f"(the R-LW cap is {BODY_CAP_BYTES:,}); a grandfathered plan may not grow: condense it, "
                "or put a kept summary below `## Verification Summary`"
            )
        if passes > max(allowed, g_passes):
            problems.append(
                f"{name}: records {passes} verify passes, above its grandfathered {g_passes}; "
                "the open finding goes to the owner, and a further pass needs the line "
                f'Extra verify pass {passes}: owner YYYY-MM-DD "<the owner\'s words>"'
            )
        if size <= BODY_CAP_BYTES and passes <= PASS_CAP:
            problems.append(
                f"{name}: is within both caps now ({size:,} bytes, {passes} passes); "
                "delete its GRANDFATHERED row in build_tools/check_plan_refs.py"
            )
        return problems
    if size > BODY_CAP_BYTES:
        problems.append(
            f"{name}: body is {size:,} bytes, above the R-LW cap of {BODY_CAP_BYTES:,} "
            "(stamp lines and the skills' record sections excluded; every other section counts)"
        )
    if passes > allowed:
        problems.append(
            f"{name}: records {passes} verify passes; R-LW allows {PASS_CAP} before the open finding "
            f"goes to the owner. Each pass beyond {PASS_CAP} needs its own line "
            f'Extra verify pass N: owner YYYY-MM-DD "<the owner\'s words>"'
        )
    return problems


def check_all_caps(repo: Path) -> list[str]:
    problems = []
    plans = sorted(repo.glob(PLAN_GLOB))
    names = {p.name for p in plans}
    for p in plans:
        problems += cap_violations(p.name, p.read_text(encoding="utf-8"))
    for name in sorted(set(GRANDFATHERED) - names):
        problems.append(
            f"{name}: grandfathered but no longer under docs/process/plans/; delete its row"
        )
    return problems


def budget_notes_all(repo: Path) -> list[str]:
    return [
        n
        for p in sorted(repo.glob(PLAN_GLOB))
        for n in budget_notes(p.name, p.read_text(encoding="utf-8"))
    ]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("plan", type=Path, nargs="?")
    ap.add_argument(
        "--caps-all",
        action="store_true",
        help="check only the R-LW caps, on every docs/process/plans/plan-*.md",
    )
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    ap.add_argument(
        "--issues", action="store_true", help="look up every #NNNN with gh (network)"
    )
    ap.add_argument(
        "--strict", action="store_true", help="unknown symbols/codes also fail"
    )
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv)
    if a.caps_all:
        problems = check_all_caps(a.repo.resolve())
        for p in problems:
            print(f"CAP: {p}")
        for n in budget_notes_all(a.repo.resolve()):
            print(f"NOTE: {n}")
        print("RESULT: " + ("FAIL" if problems else "OK"))
        return 1 if problems else 0
    if a.plan is None:
        ap.error("a plan path is required unless --caps-all is given")
    plan_text = a.plan.read_text()
    r = check(plan_text, a.repo.resolve(), with_issues=a.issues)
    text, code = render(r, strict=a.strict)
    caps = cap_violations(a.plan.name, plan_text)
    r["caps"] = {
        "body_bytes": body_bytes(plan_text),
        "verify_passes": verify_passes(plan_text),
        "violations": caps,
        "budgets": {
            label: [size, cap] for label, size, cap in budget_report(plan_text)
        },
        "budget_notes": budget_notes(a.plan.name, plan_text),
    }
    if not a.json:
        text += (
            f"\ncaps (R-LW): body {r['caps']['body_bytes']:,} / {BODY_CAP_BYTES:,} bytes; "
            f"{r['caps']['verify_passes']} verify passes / {PASS_CAP}"
        )
        text += "".join(f"\n  - CAP: {c}" for c in caps)
        text += "\nsection budgets (a notice, not a gate): " + "; ".join(
            f"{label} {size:,} / {cap:,}"
            for label, size, cap in budget_report(plan_text)
        )
        text += "".join(f"\n  - NOTE: {n}" for n in r["caps"]["budget_notes"])
        if caps:
            text += "\nRESULT: FAIL (caps)"
    print(json.dumps(r, indent=2) if a.json else text)
    return 1 if caps else code


if __name__ == "__main__":
    sys.exit(main())
