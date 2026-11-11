#!/usr/bin/env python3
"""Check that a closed `class-tracker` issue accounted for every cell of its cell table.

Convention (close-issues skill, batch-issues tracking-issue template):

- A cell table is a markdown table with a `status` column, in the issue body or in a comment.
  A cell posted later in a comment is a row of such a table; a row whose key (its `#`, `cell`,
  `program` or `axis values` column, else its first column) repeats an earlier row's key
  supersedes it, so a closing comment can restate rows with their final status.
- At close every row's status is one of
    fixed <evidence>   -- evidence: a commit sha, a backticked test or fixture name, or `PR #N`
    moved #N           -- #N exists, is an issue (not a pull request) and is OPEN
    control            -- the row never was a defect
- A body table with a cell column (`cell`, `cells …` or `program`) but no `status` column fails:
  the cells cannot be accounted for. An issue with no cell table passes.

The strict check applies to closures on or after CUTOFF and to OPEN issues named on the command line
(the `/close-issues` pre-close run). `--report` also reads older closures and, for a table written
before the convention, counts a row as evidenced when a later comment table with an `after` column
restates its key or the row names an open issue; that heuristic is for the report only.

Exit 0 when every checked issue is accounted for, 1 when a row is not, 2 when the tracker cannot be
read.

Usage:
    python3 build_tools/check_class_closure.py 2461 2477
    python3 build_tools/check_class_closure.py --closed-since 2026-11-12
    python3 build_tools/check_class_closure.py --report --closed-since 2026-11-06
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass, field

REPO = "antonsynd/sharpy"
LABEL = "class-tracker"
CUTOFF = "2026-11-12"
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

KEY_COLUMNS = ("#", "cell", "program", "axis values")
SHA_RE = re.compile(r"\b[0-9a-f]{7,40}\b")
ISSUE_RE = re.compile(r"#(\d+)")
SEPARATOR_RE = re.compile(r"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")


class FetchError(Exception):
    pass


def fetch(path: str):
    """GET `path` (relative to the REST API root) and return the decoded JSON.

    The only network access in this module: tests replace it with recorded JSON.
    """
    if shutil.which("gh"):
        proc = subprocess.run(
            ["gh", "api", "-X", "GET", path],
            capture_output=True,
            text=True,
            check=False,
            cwd=REPO_ROOT,
        )
        if proc.returncode == 0:
            return json.loads(proc.stdout)
        if "Not Found" in proc.stdout + proc.stderr:
            return None
        if not os.environ.get("GITHUB_TOKEN"):
            raise FetchError(
                f"gh api {path}: {proc.stderr.strip() or proc.stdout.strip()}"
            )
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        raise FetchError("neither gh nor GITHUB_TOKEN is available")
    req = urllib.request.Request(
        f"https://api.github.com/{path}",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise FetchError(f"GET {path}: HTTP {e.code}") from e
    except urllib.error.URLError as e:
        raise FetchError(f"GET {path}: {e.reason}") from e


# ── Parsing ──────────────────────────────────────────────────────────────────


def _split_row(line: str) -> list[str]:
    s = line.strip()
    s = s.removeprefix("|")
    if s.endswith("|") and not s.endswith("\\|"):
        s = s[:-1]
    cells, cur, i = [], [], 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s) and s[i + 1] == "|":
            cur.append("|")
            i += 2
            continue
        if s[i] == "|":
            cells.append("".join(cur).strip())
            cur = []
        else:
            cur.append(s[i])
        i += 1
    cells.append("".join(cur).strip())
    return cells


@dataclass
class Table:
    header: list[str]
    rows: list[list[str]]

    def col(self, name: str) -> int | None:
        for i, h in enumerate(self.header):
            if h.strip().strip("*").lower() == name:
                return i
        return None

    def has_status(self) -> bool:
        return self.col("status") is not None

    def is_cell_table(self) -> bool:
        for h in self.header:
            h = h.strip().strip("*").lower()
            if h == "program" or re.match(r"cells?\b", h):
                return True
        return False


def parse_tables(text: str) -> list[Table]:
    tables: list[Table] = []
    lines = (text or "").splitlines()
    in_fence = False
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if line.startswith("```"):
            in_fence = not in_fence
            i += 1
            continue
        if (
            not in_fence
            and line.startswith("|")
            and i + 1 < len(lines)
            and SEPARATOR_RE.match(lines[i + 1].strip())
        ):
            header = _split_row(line)
            rows = []
            j = i + 2
            while j < len(lines) and lines[j].strip().startswith("|"):
                rows.append(_split_row(lines[j]))
                j += 1
            tables.append(Table(header, rows))
            i = j
            continue
        i += 1
    return tables


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", s.replace("`", "").replace("*", "")).strip().lower()


def row_keys(table: Table, row: list[str]) -> list[str]:
    """The keys a row answers to; a numeric `#` like `1–3, 5` expands to each number."""
    for name in KEY_COLUMNS:
        idx = table.col(name)
        if idx is not None and idx < len(row) and row[idx].strip():
            raw = row[idx]
            if name == "#":
                nums: list[str] = []
                for part in re.split(r"[,;]", raw):
                    m = re.match(r"^\s*(\d+)\s*(?:[-–—]|\.\.)\s*(\d+)\s*$", part)
                    if m:
                        nums += [
                            str(n) for n in range(int(m.group(1)), int(m.group(2)) + 1)
                        ]
                    elif part.strip():
                        nums.append(_norm(part))
                return nums
            return [_norm(raw)]
    return [_norm(row[0])] if row else []


@dataclass
class Cell:
    key: str
    status: str
    source: str  # "body" or a comment timestamp
    text: str


@dataclass
class Verdict:
    number: int
    title: str
    closed_at: str | None
    checked: bool
    note: str = ""
    problems: list[str] = field(default_factory=list)
    accounted: int = 0


def collect_status_cells(body: str, comments: list[dict]) -> dict[str, Cell]:
    """Rows of every status-column table, body first then comments in order; a later key wins."""
    cells: dict[str, Cell] = {}
    sources = [("body", body)] + [
        (c.get("created_at", "comment"), c.get("body") or "") for c in comments
    ]
    for source, text in sources:
        for t in parse_tables(text):
            if not t.has_status():
                continue
            si = t.col("status")
            for row in t.rows:
                status = row[si] if si < len(row) else ""
                for key in row_keys(t, row):
                    cells[key] = Cell(key, status.strip(), source, " | ".join(row))
    return cells


def classify(status: str) -> tuple[str, int | None]:
    """Return (kind, moved-target) for a status cell; kind is fixed/moved/control/bad-fixed/none."""
    s = _norm(status)
    if s.startswith("fixed"):
        rest = status.split(None, 1)[1] if len(status.split(None, 1)) > 1 else ""
        if (
            SHA_RE.search(rest.lower())
            or re.search(r"`[^`]+`", rest)
            or re.search(r"\bPR\s*#\d+", rest, re.IGNORECASE)
        ):
            return "fixed", None
        return "bad-fixed", None
    if s.startswith("moved"):
        m = ISSUE_RE.search(status)
        return ("moved", int(m.group(1))) if m else ("bad-moved", None)
    if s.startswith("control"):
        return "control", None
    return "none", None


class StateCache:
    def __init__(self):
        self._states: dict[int, str] = {}

    def state(self, n: int) -> str:
        """OPEN, CLOSED, PULL_REQUEST or MISSING."""
        if n not in self._states:
            data = fetch(f"repos/{REPO}/issues/{n}")
            if data is None:
                self._states[n] = "MISSING"
            elif data.get("pull_request"):
                self._states[n] = "PULL_REQUEST"
            else:
                self._states[n] = data["state"].upper()
        return self._states[n]


def check_strict(
    number: int, body: str, comments: list[dict], states: StateCache
) -> tuple[list[str], int, str]:
    problems: list[str] = []
    for t in parse_tables(body):
        if t.is_cell_table() and not t.has_status():
            problems.append(
                f"body cell table ({' | '.join(t.header)}) has no status column"
            )
    cells = collect_status_cells(body, comments)
    if not cells and not problems:
        return [], 0, "no cell table"
    accounted = 0
    for key, cell in cells.items():
        kind, target = classify(cell.status)
        where = f"row {key!r} ({cell.source})"
        if kind in ("fixed", "control"):
            accounted += 1
        elif kind == "moved":
            st = states.state(target)
            if target == number:
                problems.append(f"{where}: moved to itself #{target}")
            elif st == "OPEN":
                accounted += 1
            else:
                problems.append(
                    f"{where}: moved to #{target}, which is {st.lower().replace('_', ' ')}"
                )
        elif kind == "bad-fixed":
            problems.append(
                f"{where}: 'fixed' names no commit sha, backticked test or PR #N"
            )
        elif kind == "bad-moved":
            problems.append(f"{where}: 'moved' names no issue")
        else:
            shown = cell.status or "(empty)"
            problems.append(f"{where}: status {shown!r} is not fixed/moved/control")
    return problems, accounted, ""


def check_legacy(
    number: int, body: str, comments: list[dict], states: StateCache
) -> tuple[list[str], int, str]:
    """Report-only heuristic for tables written before the status column existed."""
    rows: list[tuple[str, str, str]] = []  # key, source, text
    after_keys: set[str] = set()
    sources = [("body", body)] + [
        (c.get("created_at", "comment"), c.get("body") or "") for c in comments
    ]
    for source, text in sources:
        for t in parse_tables(text):
            if t.col("after") is not None:
                for row in t.rows:
                    after_keys.update(row_keys(t, row))
                continue
            if not t.is_cell_table():
                continue
            tracker = t.col("tracker")
            for row in t.rows:
                if tracker is not None and f"#{number}" not in (
                    row[tracker] if tracker < len(row) else ""
                ):
                    continue  # a ledger row parked for another tracker
                for key in row_keys(t, row):
                    rows.append((key, source, " | ".join(row)))
    if not rows:
        return [], 0, "no cell table"
    seen: dict[str, tuple[str, str]] = {}
    for key, source, text in rows:
        seen[key] = (source, text)
    problems, accounted = [], 0
    for key, (source, text) in seen.items():
        if key in after_keys:
            accounted += 1
            continue
        cites = [int(n) for n in ISSUE_RE.findall(text) if int(n) != number]
        open_cites = [n for n in cites if states.state(n) == "OPEN"]
        if open_cites:
            accounted += 1
            continue
        short = text if len(text) <= 140 else text[:137] + "..."
        why = (
            f"names only closed {', '.join('#' + str(n) for n in cites)}"
            if cites
            else "no after-row, no open issue"
        )
        problems.append(f"row {key!r} ({source}): {why}: {short}")
    return problems, accounted, "legacy table (no status column); heuristic"


def check_issue(
    issue: dict, states: StateCache, report: bool, explicit: bool
) -> Verdict:
    number = issue["number"]
    labels = {lb["name"] for lb in issue.get("labels", [])}
    v = Verdict(number, issue.get("title", ""), issue.get("closed_at"), checked=False)
    if issue.get("pull_request"):
        v.note = "a pull request, not an issue"
        return v
    if LABEL not in labels:
        v.note = f"not labelled {LABEL}"
        return v
    closed_at = issue.get("closed_at")
    in_scope = (closed_at is None and explicit) or (
        closed_at is not None and closed_at[:10] >= CUTOFF
    )
    if not in_scope and not report:
        v.note = (
            f"closed {closed_at[:10]}, before the {CUTOFF} cutoff"
            if closed_at
            else "open"
        )
        return v
    comments = fetch_comments(number)
    body = issue.get("body") or ""
    has_status = bool(collect_status_cells(body, comments))
    if in_scope or has_status:
        v.problems, v.accounted, v.note = check_strict(number, body, comments, states)
    else:
        v.problems, v.accounted, v.note = check_legacy(number, body, comments, states)
    v.checked = True
    return v


def fetch_comments(number: int) -> list[dict]:
    out: list[dict] = []
    page = 1
    while True:
        batch = (
            fetch(f"repos/{REPO}/issues/{number}/comments?per_page=100&page={page}")
            or []
        )
        out += batch
        if len(batch) < 100:
            return out
        page += 1


def closed_since(date: str) -> list[dict]:
    out: list[dict] = []
    page = 1
    while True:
        batch = (
            fetch(
                f"repos/{REPO}/issues?labels={LABEL}&state=closed&since={date}T00:00:00Z&per_page=100&page={page}"
            )
            or []
        )
        out += [
            i
            for i in batch
            if not i.get("pull_request") and (i.get("closed_at") or "")[:10] >= date
        ]
        if len(batch) < 100:
            return sorted(out, key=lambda i: i["number"])
        page += 1


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("issues", nargs="*", type=int, help="issue numbers to check")
    ap.add_argument(
        "--closed-since",
        metavar="YYYY-MM-DD",
        help="check class-tracker issues closed on or after",
    )
    ap.add_argument(
        "--report",
        action="store_true",
        help=f"also read closures before {CUTOFF}; always exit 0",
    )
    args = ap.parse_args(argv)
    if not args.issues and not args.closed_since:
        ap.error("give issue numbers or --closed-since")

    states = StateCache()
    try:
        issues = [fetch(f"repos/{REPO}/issues/{n}") for n in args.issues]
        missing = [n for n, i in zip(args.issues, issues) if i is None]
        if missing:
            print(
                f"no such issue: {', '.join('#' + str(n) for n in missing)}",
                file=sys.stderr,
            )
            return 2
        explicit = {i["number"] for i in issues}
        if args.closed_since:
            issues += [
                i
                for i in closed_since(args.closed_since)
                if i["number"] not in explicit
            ]
        verdicts = [
            check_issue(i, states, args.report, i["number"] in explicit) for i in issues
        ]
    except FetchError as e:
        print(f"cannot read the tracker: {e}", file=sys.stderr)
        return 2

    failed = 0
    for v in verdicts:
        when = f"closed {v.closed_at[:10]}" if v.closed_at else "open"
        if not v.checked:
            print(f"#{v.number} skip ({v.note})")
            continue
        tag = "FAIL" if v.problems else "ok"
        note = f"; {v.note}" if v.note else ""
        print(
            f"#{v.number} {tag} ({when}; {v.accounted} accounted, {len(v.problems)} not{note}) {v.title[:80]}"
        )
        for p in v.problems:
            print(f"    {p}")
        failed += bool(v.problems)
    if failed and not args.report:
        print(
            f"\n{failed} class issue(s) closed with unaccounted cells: give every row of the cell table a "
            "status of `fixed <sha|`test`|PR #N>`, `moved #N` (an open issue) or `control`.",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
