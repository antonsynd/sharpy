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

Exit code 1 when any path is missing or a ``file:line`` points past end-of-file (with ``--strict``,
also when any symbol or code is unknown); 0 otherwise. Never edits anything.
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
PATH_RE = re.compile(r"(?<![\w/.\-])((?:src|docs|build_tools|benchmarks|\.claude|\.github)/[\w\-./]*\w\." + PATH_EXTS + r")\b")
FILELINE_RE = re.compile(r"(?<![\w/])((?:[\w\-]+/)*[\w\-]+(?:\.[\w\-]+)*\.(?:cs|py|spy|md|sh|ya?ml)):(\d+)(?:[–\-](\d+))?")
SPY_RE = re.compile(r"\bSPY\d{4}\b")
ISSUE_RE = re.compile(r"(?<![\w&/])#(\d{3,5})\b")
BACKTICK_RE = re.compile(r"`([^`\n]{2,120})`")
IDENT_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
SYMBOL_RE = re.compile(r"^[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+$")  # ≥2 humps: TypeChecker, MergeFrom
FENCE_RE = re.compile(r"```.*?```", re.S)
SKIP_DIRS = {".git", "bin", "obj", "node_modules", "__pycache__", ".claude"}


def list_files(repo: Path) -> list[str]:
    """Tracked files via git; falls back to a walk when the root is not a git checkout."""
    try:
        out = subprocess.run(["git", "-C", str(repo), "ls-files"], capture_output=True, text=True, check=True).stdout
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
    result: dict = {"paths": {"ok": [], "missing": [], "unresolved": []},
                    "filelines": {"ok": [], "past_eof": [], "missing": [], "ambiguous": []},
                    "codes": {"ok": [], "unknown": []},
                    "symbols": {"ok": [], "unknown": []},
                    "issues": {}}

    # paths (whole text — commands in code blocks name real files too)
    for ref in sorted(set(PATH_RE.findall(plan_text))):
        if (repo / ref).exists():
            result["paths"]["ok"].append(ref)
        elif ref.endswith(".spy") or ref.startswith(".claude/plans/"):
            result["paths"]["unresolved"].append(ref)  # fixture-relative or gitignored; the verifier decides
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
            by_suffix_cache[key] = exact + shorthand  # both: `Format.cs` names Partial.Builtins/Format.cs AND StringExtensions.Format.cs
        cands = by_suffix_cache[key]
        label = f"{token}:{line_s}" + (f"-{line_end_s}" if line_end_s else "")
        if not cands:
            result["filelines"]["missing"].append(label)
            continue
        counts = {c: line_count(repo / c) for c in cands if (repo / c).is_file()}
        if len(cands) > 1:
            result["filelines"]["ambiguous"].append(f"{label} → {len(cands)} candidates")
        if any(n >= want for n in counts.values()):
            result["filelines"]["ok"].append(label)
        else:
            result["filelines"]["past_eof"].append(f"{label} (file has {max(counts.values(), default=0)} lines)")

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
                (result["symbols"]["ok"] if part in idents else result["symbols"]["unknown"]).append(part)
    result["symbols"]["ok"].sort(); result["symbols"]["unknown"].sort()

    # issues
    numbers = sorted({int(n) for n in ISSUE_RE.findall(plan_text)})
    if with_issues:
        for n in numbers:
            try:
                out = subprocess.run(["gh", "issue", "view", str(n), "--json", "state,title,stateReason"],
                                     capture_output=True, text=True, check=True).stdout
                d = json.loads(out)
                result["issues"][str(n)] = {"state": d.get("state"), "reason": d.get("stateReason"), "title": d.get("title")}
            except (OSError, subprocess.CalledProcessError, json.JSONDecodeError) as e:
                result["issues"][str(n)] = {"state": "ERROR", "reason": str(e)[:80], "title": ""}
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
        lines.append(f"  - unresolved (fixture-relative .spy or gitignored plan path): {u}")
    fl = r["filelines"]
    lines.append(f"file:line: {len(fl['ok'])} ok, {len(fl['past_eof'])} PAST-EOF, {len(fl['missing'])} MISSING, {len(fl['ambiguous'])} ambiguous")
    for b in fl["past_eof"]: lines.append(f"  - past eof: {b}")
    for b in fl["missing"]: lines.append(f"  - missing: {b}")
    for b in fl["ambiguous"]: lines.append(f"  - ambiguous: {b}")
    sec("SPY codes", r["codes"]["ok"], r["codes"]["unknown"], "not in DiagnosticCodes.cs (new code proposed?)")
    sec("symbols (prose, PascalCase)", r["symbols"]["ok"], r["symbols"]["unknown"], "not found in src/**/*.cs (new symbol proposed?)")
    closed = {n: v for n, v in r["issues"].items() if v.get("state") == "CLOSED"}
    errs = {n: v for n, v in r["issues"].items() if v.get("state") == "ERROR"}
    unchecked = sum(1 for v in r["issues"].values() if v.get("state") == "unchecked")
    lines.append(f"issues: {len(r['issues'])} referenced; {len(closed)} CLOSED; {len(errs)} lookup errors; {unchecked} unchecked (use --issues)")
    for n, v in closed.items(): lines.append(f"  - #{n} CLOSED ({v.get('reason')}): {v.get('title')}")
    for n, v in errs.items(): lines.append(f"  - #{n} ERROR: {v.get('reason')}")
    hard = bool(r["paths"]["missing"] or fl["past_eof"] or fl["missing"])
    soft = bool(r["codes"]["unknown"] or r["symbols"]["unknown"])
    code = 1 if hard or (strict and soft) else 0
    lines.append("RESULT: " + ("FAIL" if code else "OK") + (" (strict)" if strict else ""))
    return "\n".join(lines), code


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("plan", type=Path)
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    ap.add_argument("--issues", action="store_true", help="look up every #NNNN with gh (network)")
    ap.add_argument("--strict", action="store_true", help="unknown symbols/codes also fail")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv)
    r = check(a.plan.read_text(), a.repo.resolve(), with_issues=a.issues)
    text, code = render(r, strict=a.strict)
    print(json.dumps(r, indent=2) if a.json else text)
    return code


if __name__ == "__main__":
    sys.exit(main())
