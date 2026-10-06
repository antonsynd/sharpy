#!/usr/bin/env python3
"""Audit every `= None` parameter default in the spy-sourced stdlib against CPython (#2236, R-EV).

A spy-sourced signature spelled `param: T | None = None` makes an OMITTED argument and an
explicit `None` the same call. That is faithful only when CPython's own default for the
parameter IS `None`. When CPython's default is anything else -- a private sentinel object
(`socket.create_connection(timeout=_GLOBAL_DEFAULT_TIMEOUT)`), or an ordinary value
(`re.Pattern.search(endpos=sys.maxsize)`) -- an explicit `None` means something different in
CPython (blocking; a TypeError) from the omitted call, and the Sharpy spelling silently merges
the two. Owner ruling R-EV (2026-10-05): such a default becomes an OVERLOAD WITHOUT that
parameter, written in the spy source (rung 1).

The universe is every parameter whose default is `None` (or `None()`) in a module named by the
`MODULES` mapping of `regenerate_spy_stdlib.sh` -- the spy-sourced modules whose C# is generated.
Each parameter is mapped to its CPython callable (module function, class constructor, or class
method), and `inspect.signature` in the running python3 decides the verdict:

    pass     CPython's default is None
    finding  CPython's default is anything else (sentinel, value, or required) -- must be
             listed in `spy_none_default_allowlist.txt`, which cites the tracking issue and
             drains on fix (a stale row fails)
    skip     CPython has no comparable callable/parameter (Sharpy-only helper, internal
             constructor, uninspectable builtin) -- must be listed in SKIPS below with a
             reason; a skip entry that no longer matches a skip-status cell fails

Usage:
    python3 build_tools/spy_none_default_audit.py          # print the audit matrix
    python3 build_tools/spy_none_default_audit.py --check  # exit 1 on any ratchet violation
"""
from __future__ import annotations

import argparse
import importlib
import inspect
import re
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
REGEN_SCRIPT = REPO_ROOT / "build_tools" / "regenerate_spy_stdlib.sh"
SPY_DIR = REPO_ROOT / "src" / "Sharpy.Stdlib" / "spy"
ALLOWLIST = REPO_ROOT / "build_tools" / "spy_none_default_allowlist.txt"

# Spy class name -> CPython class name, where the spy spelling differs from CPython's.
# Keyed by the CPython module name (the MODULES python_module_name).
CLASS_MAP: dict[str, dict[str, str]] = {
    "csv": {
        "CsvReader": "reader",
        "CsvWriter": "writer",
        "CsvDictReader": "DictReader",
        "CsvDictWriter": "DictWriter",
    },
    "re": {"MatchResult": "Match"},
}

# Cells CPython cannot compare, each with the reason. Key: "<module>::<callable>::<param>".
# A key here whose cell is no longer a skip-status cell (it now resolves, or it was removed
# from the spy source) is stale and fails the check.
SKIPS: dict[str, str] = {
    "csv::dict_reader::fieldnames": (
        "Sharpy-only factory (csv has no dict_reader); it forwards to CsvDictReader, whose "
        "`fieldnames` is audited as csv::CsvDictReader.__init__::fieldnames"
    ),
    "re::MatchResult.__init__::compiled_pattern": (
        "internal constructor wrapping a .NET Match; CPython's re.Match is not "
        "user-constructible (inspect.signature(re.Match) is `()`)"
    ),
}

_NONE_DEFAULTS = {"None", "None()"}


@dataclass(frozen=True)
class SpyParam:
    module: str        # CPython module name, e.g. "socket"
    spy_file: str      # spy filename, e.g. "socket_module.spy"
    line: int          # 1-based line of the `def`
    cls: str | None    # enclosing spy class name, or None for a module function
    func: str          # function / method name
    param: str         # parameter name
    default: str       # default text as written ("None" / "None()")

    @property
    def callable_name(self) -> str:
        return f"{self.cls}.{self.func}" if self.cls else self.func

    @property
    def key(self) -> str:
        return f"{self.module}::{self.callable_name}::{self.param}"


@dataclass(frozen=True)
class Verdict:
    cell: SpyParam
    status: str        # "pass" | "finding" | "skip"
    cpython: str       # CPython default repr, or the reason it could not be compared


# --------------------------------------------------------------------------- spy parsing


def read_modules(script: Path = REGEN_SCRIPT) -> list[tuple[str, str]]:
    """(spy stem, CPython module name) for every entry of the MODULES=( ... ) array."""
    text = script.read_text()
    m = re.search(r"^MODULES=\((.*?)^\)", text, re.S | re.M)
    if not m:
        raise ValueError(f"no MODULES=( ... ) array in {script}")
    out = []
    for entry in re.findall(r'"([^"]+)"', m.group(1)):
        stem, python_name, _cs = entry.split(":")
        out.append((stem, python_name))
    if not out:
        raise ValueError(f"MODULES array in {script} is empty")
    return out


def mask_source(text: str) -> str:
    """Blank out string-literal contents and comments, keeping every newline and column.

    The quotes themselves are kept so a default such as `"sha256"` stays visibly non-None.
    A non-triple-quoted string ends at the end of its line, so an unusual literal can only
    disturb its own line.
    """
    out: list[str] = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "#":
            while i < n and text[i] != "\n":
                out.append(" ")
                i += 1
            continue
        if c in "\"'":
            quote = text[i:i + 3] if text[i:i + 3] in ('"""', "'''") else c
            out.append(quote)
            i += len(quote)
            while i < n:
                if text[i] == "\\" and i + 1 < n and text[i + 1] != "\n":
                    out.append("  ")
                    i += 2
                    continue
                if text.startswith(quote, i):
                    out.append(quote)
                    i += len(quote)
                    break
                if text[i] == "\n":
                    out.append("\n")
                    i += 1
                    if len(quote) == 1:
                        break
                    continue
                out.append(" ")
                i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def _balanced_end(text: str, start: int) -> int:
    """Index just past the bracket that closes the one opened at text[start]."""
    depth = 0
    for j in range(start, len(text)):
        if text[j] in "([{":
            depth += 1
        elif text[j] in ")]}":
            depth -= 1
            if depth == 0:
                return j + 1
    raise ValueError(f"unbalanced bracket opened at offset {start}")


def split_top_level(s: str, sep: str = ",") -> list[str]:
    parts, depth, cur = [], 0, []
    for ch in s:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == sep and depth == 0:
            parts.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
    parts.append("".join(cur))
    return [p.strip() for p in parts if p.strip()]


def _default_of(param: str) -> tuple[str, str | None]:
    """(name, default text or None) for one parameter spelling `name[: T][ = default]`."""
    depth = 0
    for j, ch in enumerate(param):
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif (ch == "=" and depth == 0
              and (j == 0 or param[j - 1] not in "<>!=:")
              and (j + 1 >= len(param) or param[j + 1] != "=")):
            head, default = param[:j], param[j + 1:].strip()
            break
    else:
        head, default = param, None
    name = head.split(":", 1)[0].strip().lstrip("*").strip()
    return name, default


_CLASS_RE = re.compile(r"^([ \t]*)class[ \t]+([A-Za-z_]\w*)")
_DEF_RE = re.compile(r"^([ \t]*)(?:async[ \t]+)?def[ \t]+([A-Za-z_]\w*)[ \t]*")


def parse_spy(text: str) -> list[tuple[int, str | None, str, str, str | None]]:
    """Every parameter of every def: (line, enclosing class, func, param, default text)."""
    masked = mask_source(text)
    lines = masked.split("\n")
    offsets = [0]
    for ln in lines:
        offsets.append(offsets[-1] + len(ln) + 1)

    result = []
    class_stack: list[tuple[int, str]] = []
    skip_until = -1
    for idx, line in enumerate(lines):
        if idx < skip_until or not line.strip():
            continue
        indent = len(line) - len(line.lstrip(" \t"))
        while class_stack and indent <= class_stack[-1][0]:
            class_stack.pop()
        cm = _CLASS_RE.match(line)
        if cm:
            class_stack.append((len(cm.group(1)), cm.group(2)))
            continue
        dm = _DEF_RE.match(line)
        if not dm:
            continue
        pos = offsets[idx] + dm.end()
        if masked[pos] == "[":                       # generic parameter list
            pos = _balanced_end(masked, pos)
            while masked[pos] in " \t":
                pos += 1
        if masked[pos] != "(":
            raise ValueError(f"line {idx + 1}: cannot find the parameter list of `{dm.group(2)}`")
        end = _balanced_end(masked, pos)
        skip_until = masked.count("\n", 0, end)      # header continuation lines
        cls = class_stack[-1][1] if class_stack and class_stack[-1][0] < len(dm.group(1)) else None
        for p in split_top_level(masked[pos + 1:end - 1]):
            name, default = _default_of(p)
            if name in ("", "/", "self", "cls"):
                continue
            result.append((idx + 1, cls, dm.group(2), name, default))
    return result


def collect_none_defaults(spy_dir: Path = SPY_DIR, modules=None) -> list[SpyParam]:
    cells: dict[str, SpyParam] = {}
    for stem, python_name in (modules if modules is not None else read_modules()):
        path = spy_dir / f"{stem}.spy"
        for line, cls, func, param, default in parse_spy(path.read_text()):
            if default in _NONE_DEFAULTS:
                cell = SpyParam(python_name, path.name, line, cls, func, param, default)
                cells.setdefault(cell.key, cell)     # overloads share a key
    return sorted(cells.values(), key=lambda c: c.key)


# --------------------------------------------------------------------------- CPython side


def _describe_default(value) -> str:
    if value is inspect.Parameter.empty:
        return "<required>"
    if type(value) is object:                        # the classic `_SENTINEL = object()`
        return "<sentinel object()>"
    return repr(value)


def classify(cell: SpyParam, cpython_callable) -> Verdict:
    """Compare one spy None-default parameter with the CPython callable that owns it."""
    try:
        sig = inspect.signature(cpython_callable)
    except (TypeError, ValueError) as ex:
        return Verdict(cell, "skip", f"uninspectable: {ex}")
    p = sig.parameters.get(cell.param)
    if p is None:
        return Verdict(cell, "skip", f"no parameter `{cell.param}` in CPython {sig}")
    if p.default is None:
        return Verdict(cell, "pass", "None")
    return Verdict(cell, "finding", _describe_default(p.default))


def resolve(cell: SpyParam):
    """The CPython callable for a cell, or a reason string when CPython has none."""
    mod = importlib.import_module(cell.module)
    if cell.cls is None:
        return getattr(mod, cell.func, f"no function {cell.module}.{cell.func}")
    cls_name = CLASS_MAP.get(cell.module, {}).get(cell.cls, cell.cls)
    cls = getattr(mod, cls_name, None)
    if cls is None:
        return f"no class {cell.module}.{cls_name}"
    if cell.func == "__init__":
        return cls
    return getattr(cls, cell.func, f"no method {cell.module}.{cls_name}.{cell.func}")


def audit(cells: list[SpyParam]) -> list[Verdict]:
    out = []
    for cell in cells:
        target = resolve(cell)
        if isinstance(target, str):
            out.append(Verdict(cell, "skip", target))
        else:
            out.append(classify(cell, target))
    return out


# --------------------------------------------------------------------------- ratchet


def load_allowlist(path: Path = ALLOWLIST) -> dict[str, str]:
    """key -> the row's trailing comment (which must cite an issue)."""
    rows: dict[str, str] = {}
    for raw in path.read_text().splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        key, _, comment = line.partition("#")
        rows[key.strip()] = comment.strip()
    return rows


def violations(verdicts: list[Verdict], allowlist: dict[str, str],
               skips: dict[str, str]) -> list[str]:
    errors = []
    by_key = {v.cell.key: v for v in verdicts}
    for v in verdicts:
        k = v.cell.key
        if v.status == "finding" and k not in allowlist:
            errors.append(f"NEW FINDING {k}: CPython default {v.cpython}, spy `= {v.cell.default}` "
                          f"({v.cell.spy_file}:{v.cell.line}) -- cure with an overload without "
                          f"the parameter (R-EV), or allowlist it citing an issue")
        if v.status == "skip" and k not in skips:
            errors.append(f"UNCLASSIFIED {k}: {v.cpython} ({v.cell.spy_file}:{v.cell.line}) -- "
                          f"add a reasoned SKIPS entry or fix the CLASS_MAP")
    for k, comment in allowlist.items():
        if not re.search(r"#\d+", comment):
            errors.append(f"UNCITED allowlist row {k}: every row must cite an issue")
        if k not in by_key or by_key[k].status != "finding":
            errors.append(f"STALE allowlist row {k}: no longer a finding -- delete it (drain on fix)")
    for k in skips:
        if k not in by_key or by_key[k].status != "skip":
            errors.append(f"STALE skip {k}: no longer a skip-status cell -- delete it")
    return errors


def render(verdicts: list[Verdict]) -> str:
    rows = [("module", "callable", "param", "CPython default", "verdict")]
    for v in verdicts:
        rows.append((v.cell.module, v.cell.callable_name, v.cell.param, v.cpython, v.status))
    widths = [max(len(r[i]) for r in rows) for i in range(5)]
    return "\n".join(" | ".join(c.ljust(w) for c, w in zip(r, widths)).rstrip() for r in rows)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="exit 1 on any ratchet violation")
    args = ap.parse_args(argv)
    verdicts = audit(collect_none_defaults())
    print(render(verdicts))
    if args.check:
        errors = violations(verdicts, load_allowlist(), SKIPS)
        for e in errors:
            print(e, file=sys.stderr)
        return 1 if errors else 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
