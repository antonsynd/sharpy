#!/usr/bin/env python3
"""Check that a closed `class-tracker` issue accounted for every cell of its cell table.

Convention (close-issues skill, batch-issues templates):

- A cell table is a markdown table, in the issue body or in a comment, whose header names a cell
  column (`cell…`, `program…` or `axis values`), or else an observation column (`observed…`,
  `expected…`, `python…`, `cpython…`, `sharpy…`) unless the nearest non-blank line above it says
  `not a cell table`; it carries a `status` column. Rows whose `tracker` column names
  another issue are not this issue's cells. A row's key is its `#`, `cell`, `program` or
  `axis values` column (else its first column); two rows of one table with the same key are told
  apart by the whole row. A row in a later source (a later comment) with an earlier row's key
  supersedes it, so a closing comment can restate cells with their final status.
- Any other table in the BODY with a data row and no `status` column fails as unrecognised: name
  its key column `cell` and give it a `status` column, or write `not a cell table` on the line above.
  Unrecognised comment tables (evidence, measurements, batching summaries) are not read.
- At close every row's status is one of
    fixed <evidence>   -- a commit sha (7-40 hex, at least one digit, not a yyyymmdd date),
                          a backticked test or fixture name, or `PR #N`
    moved #N           -- #N is an issue (not a pull request) that was open when the tracker closed
    control            -- the row never was a defect
- A row of a cell table with no `status` column is unaccounted unless a later source restates its
  key in a table that has one; the table fails, listing those rows. An issue with no cell table
  (and no unrecognised body table) passes. Closures as `duplicate` or `not_planned` are not checked.

The strict check applies to closures on or after CUTOFF and to OPEN issues named on the command line
(the `/close-issues` pre-close run). `--report` also reads older closures and, for a table written
before the convention, counts a row as evidenced when a later comment table with an `after` column
restates its key or the row names an open issue; that heuristic is for the report only.
`--comment-on-failure` (the issue-closed workflow) posts one comment listing the unaccounted rows.

Exit 0 when every checked issue is accounted for, 1 when a row is not, 2 when the tracker cannot be
read.

Usage:
    python3 build_tools/check_class_closure.py 2461 2477
    python3 build_tools/check_class_closure.py --comment-on-failure 2461
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
SKIPPED_REASONS = ("duplicate", "not_planned")
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

KEY_COLUMNS = ("#", "cell", "program", "axis values")
ISSUE_RE = re.compile(r"#(\d+)")
HEX_RE = re.compile(r"\b[0-9a-f]{7,40}\b")
TEST_NAME_RE = re.compile(r"`([A-Za-z_][\w./:~-]*)`")
SEPARATOR_RE = re.compile(r"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")
GONE_MARKERS = ("Not Found", "HTTP 404", "HTTP 410", "Gone")


class FetchError(Exception):
    pass


def _token() -> str | None:
    return os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")


def api(method: str, path: str, data: dict | None = None):
    """Call the REST API (`path` relative to its root) and return the decoded JSON.

    The only network access in this module: tests replace it with recorded JSON. A 404 or 410
    returns None. `gh` is tried first; REST with GH_TOKEN / GITHUB_TOKEN when `gh` is absent or fails.
    """
    gh_error = ""
    if shutil.which("gh"):
        proc = subprocess.run(
            ["gh", "api", "-X", method, path]
            + (["--input", "-"] if data is not None else []),
            input=json.dumps(data) if data is not None else None,
            capture_output=True,
            text=True,
            check=False,
            cwd=REPO_ROOT,
        )
        if proc.returncode == 0:
            return json.loads(proc.stdout) if proc.stdout.strip() else {}
        out = proc.stdout + proc.stderr
        if any(m in out for m in GONE_MARKERS):
            return None
        gh_error = out.strip()
    token = _token()
    if not token:
        raise FetchError(
            gh_error or "neither gh nor GH_TOKEN / GITHUB_TOKEN is available"
        )
    req = urllib.request.Request(
        f"https://api.github.com/{path}",
        method=method,
        data=json.dumps(data).encode() if data is not None else None,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read().decode()
            return json.loads(raw) if raw.strip() else {}
    except urllib.error.HTTPError as e:
        if e.code in (404, 410):
            return None
        raise FetchError(f"{method} {path}: HTTP {e.code}") from e
    except urllib.error.URLError as e:
        raise FetchError(f"{method} {path}: {e.reason}") from e


def fetch(path: str):
    return api("GET", path)


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


CELL_MARKER_RE = re.compile(r"(cells?|program|axis values)\b")
OBSERVATION_MARKER_RE = re.compile(r"(observed|expected|python3?|cpython|sharpy)\b")
OPT_OUT_RE = re.compile(r"\bnot[\s-]+a[\s-]+cell[\s-]+table\b", re.IGNORECASE)
OPT_OUT = "not a cell table"


@dataclass
class Table:
    header: list[str]
    rows: list[list[str]]
    # The nearest non-blank line above the header: where a `not a cell table` opt-out is written.
    above: str = ""

    def col(self, name: str) -> int | None:
        for i, h in enumerate(self.header):
            if h.strip().strip("*").lower() == name:
                return i
        return None

    def has_status(self) -> bool:
        return self.col("status") is not None

    def _marked(self, marker: re.Pattern) -> bool:
        return any(marker.match(_norm(h)) for h in self.header)

    def opted_out(self) -> bool:
        return bool(OPT_OUT_RE.search(self.above))

    def is_cell_table(self) -> bool:
        """A cell column always makes a cell table; an observation column does unless opted out."""
        if self._marked(CELL_MARKER_RE):
            return True
        return not self.opted_out() and self._marked(OBSERVATION_MARKER_RE)


def _fence(line: str) -> str | None:
    m = re.match(r"^(`{3,}|~{3,})", line)
    return m.group(1)[0] if m else None


def parse_tables(text: str) -> list[Table]:
    """GFM tables outside ``` / ~~~ fences; the leading and trailing pipes are optional."""
    tables: list[Table] = []
    lines = (text or "").splitlines()
    fence: str | None = None
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        marker = _fence(line)
        if marker and (fence is None or marker == fence):
            fence = None if fence else marker
            i += 1
            continue
        if (
            fence is None
            and "|" in line
            and i + 1 < len(lines)
            and "|" in lines[i + 1]
            and SEPARATOR_RE.match(lines[i + 1].strip())
        ):
            header = _split_row(line)
            rows = []
            j = i + 2
            while j < len(lines) and lines[j].strip() and "|" in lines[j]:
                rows.append(_split_row(lines[j]))
                j += 1
            k = i - 1
            while k >= 0 and not lines[k].strip():
                k -= 1
            tables.append(Table(header, rows, lines[k].strip() if k >= 0 else ""))
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


def own_rows(table: Table, number: int) -> list[list[str]]:
    """Rows of `table` that are this issue's: a `tracker` column must name #number."""
    tracker = table.col("tracker")
    if tracker is None:
        return table.rows
    return [
        r
        for r in table.rows
        if f"#{number}" in (r[tracker] if tracker < len(r) else "")
    ]


def table_keys(table: Table, rows: list[list[str]]) -> list[list[str]]:
    """Keys per row; rows of one table that share a key are told apart by the whole row."""
    keys = [row_keys(table, r) for r in rows]
    counts: dict[str, int] = {}
    for ks in keys:
        for k in ks:
            counts[k] = counts.get(k, 0) + 1
    si = table.col("status")
    out = []
    for r, ks in zip(rows, keys):
        ident = " | ".join(_norm(c) for i, c in enumerate(r) if i != si)
        out.append([f"{k} [{ident}]" if counts[k] > 1 else k for k in ks])
    return out


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


def _sources(body: str, comments: list[dict]) -> list[tuple[str, str]]:
    return [("body", body)] + [
        (c.get("created_at", "comment"), c.get("body") or "") for c in comments
    ]


def collect_cells(
    number: int, body: str, comments: list[dict]
) -> tuple[dict[str, Cell], list[str]]:
    """This issue's cells from every cell table, body then comments; a later source's key wins.

    Also returns the problems no row status can answer: one per cell table with no status column
    whose rows (of this issue) are not all restated by a LATER table that has one, and one per
    unrecognised body table. A status-less row never replaces a row that has a status.
    """
    cells: dict[str, Cell] = {}
    # key -> sequence number of the latest status-column table that states it
    stated_at: dict[str, int] = {}
    # (sequence number, source, table, its row keys) per status-less cell table
    statusless: list[tuple[int, str, Table, list[str]]] = []
    problems: list[str] = []
    seq = 0
    for source, text in _sources(body, comments):
        for t in parse_tables(text):
            seq += 1
            rows = own_rows(t, number)
            if not rows:
                continue
            if not t.is_cell_table():
                if source == "body" and not t.has_status() and not t.opted_out():
                    problems.append(
                        f"unrecognised table ({' | '.join(t.header)}) in body, "
                        f"{len(rows)} row(s): if its rows are cells, name the key column `cell` "
                        f"and add a `status` column; if not, write `{OPT_OUT}` on the line "
                        "just above it"
                    )
                continue
            si = t.col("status")
            keys = table_keys(t, rows)
            if si is None:
                flat = list(dict.fromkeys(k for ks in keys for k in ks))
                statusless.append((seq, source, t, flat))
                continue
            for row, ks in zip(rows, keys):
                status = row[si] if si < len(row) else ""
                for key in ks:
                    cells[key] = Cell(key, status.strip(), source, " | ".join(row))
                    stated_at[key] = seq
    for at, source, t, keys in statusless:
        unstated = [k for k in keys if stated_at.get(k, 0) <= at]
        if unstated:
            shown = ", ".join(repr(k) for k in unstated[:10])
            more = f" and {len(unstated) - 10} more" if len(unstated) > 10 else ""
            problems.append(
                f"cell table ({' | '.join(t.header)}) in {source} has no status column; "
                f"{len(unstated)} of its {len(keys)} row(s) have no status: {shown}{more}. "
                "Add a `status` column to it, or restate those keys in a later comment's "
                "table that has one"
            )
    return cells, problems


def _is_sha(token: str) -> bool:
    return any(c.isdigit() for c in token) and not re.fullmatch(r"(19|20)\d{6}", token)


def _is_test_name(name: str) -> bool:
    return len(name) >= 6 and bool(re.search(r"[_./]|[a-z][A-Z]", name))


def classify(status: str) -> tuple[str, int | None]:
    """Return (kind, moved-target); kind is fixed/moved/control/bad-fixed/bad-moved/none."""
    s = re.sub(r"^[^A-Za-z0-9]+", "", status.strip())  # a leading emoji or checkmark
    word = s.split(None, 1)
    head = word[0].lower().rstrip(":") if word else ""
    rest = word[1] if len(word) > 1 else ""
    if head == "fixed":
        if (
            any(_is_sha(t) for t in HEX_RE.findall(rest))
            or any(_is_test_name(t) for t in TEST_NAME_RE.findall(rest))
            or re.search(r"\bPR\s*#\d+", rest, re.IGNORECASE)
        ):
            return "fixed", None
        return "bad-fixed", None
    if head == "moved":
        m = ISSUE_RE.search(rest)
        return ("moved", int(m.group(1))) if m else ("bad-moved", None)
    if head == "control":
        return "control", None
    return "none", None


class IssueCache:
    def __init__(self):
        self._issues: dict[int, dict | None] = {}

    def get(self, n: int) -> dict | None:
        if n not in self._issues:
            self._issues[n] = fetch(f"repos/{REPO}/issues/{n}")
        return self._issues[n]

    def is_open(self, n: int) -> bool:
        data = self.get(n)
        return bool(data) and not data.get("pull_request") and data["state"] == "open"


def moved_problem(
    target: int, number: int, tracker_closed_at: str | None, issues: IssueCache
) -> str | None:
    """None when `moved #target` is a valid disposition, else why not."""
    if target == number:
        return f"moved to itself #{target}"
    data = issues.get(target)
    if data is None:
        return f"moved to #{target}, which does not exist (deleted?)"
    if data.get("pull_request"):
        return f"moved to #{target}, which is a pull request"
    if data["state"] == "open":
        return None
    closed = data.get("closed_at") or ""
    if tracker_closed_at and closed > tracker_closed_at:
        return None  # open when the tracker closed
    when = f"at {tracker_closed_at}" if tracker_closed_at else "now"
    return f"moved to #{target}, which is closed ({closed[:10]}), not open {when}"


def check_strict(
    issue: dict, comments: list[dict], issues: IssueCache
) -> tuple[list[str], int, str]:
    number = issue["number"]
    cells, problems = collect_cells(number, issue.get("body") or "", comments)
    if not cells and not problems:
        return [], 0, "no cell table"
    accounted = 0
    for key, cell in cells.items():
        kind, target = classify(cell.status)
        where = f"row {key!r} ({cell.source})"
        if kind in ("fixed", "control"):
            accounted += 1
        elif kind == "moved":
            why = moved_problem(target, number, issue.get("closed_at"), issues)
            if why is None:
                accounted += 1
            else:
                problems.append(f"{where}: {why}")
        elif kind == "bad-fixed":
            problems.append(
                f"{where}: {cell.status!r} names no commit sha, backticked test name or PR #N"
            )
        elif kind == "bad-moved":
            problems.append(f"{where}: 'moved' names no issue")
        else:
            shown = cell.status or "(empty)"
            problems.append(f"{where}: status {shown!r} is not fixed/moved/control")
    return problems, accounted, ""


def check_legacy(
    issue: dict, comments: list[dict], issues: IssueCache
) -> tuple[list[str], int, str]:
    """Report-only heuristic for tables written before the status column existed."""
    number = issue["number"]
    rows: list[tuple[str, str, str]] = []  # key, source, text
    after_keys: set[str] = set()
    for source, text in _sources(issue.get("body") or "", comments):
        for t in parse_tables(text):
            if t.col("after") is not None:
                for row in t.rows:
                    after_keys.update(row_keys(t, row))
                continue
            if not t.is_cell_table():
                continue
            for row in own_rows(t, number):
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
        if any(issues.is_open(n) for n in cites):
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
    issue: dict, issues: IssueCache, report: bool, explicit: bool
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
    if issue.get("state_reason") in SKIPPED_REASONS:
        v.note = f"closed as {issue['state_reason']}"
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
    cells, _ = collect_cells(number, issue.get("body") or "", comments)
    if in_scope or cells:
        v.problems, v.accounted, v.note = check_strict(issue, comments, issues)
    else:
        v.problems, v.accounted, v.note = check_legacy(issue, comments, issues)
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
                f"repos/{REPO}/issues?labels={LABEL}&state=closed&since={date}T00:00:00Z"
                f"&per_page=100&page={page}"
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


FIX_HINT = (
    "give every row of each cell table a `status` of `fixed` plus a commit sha, a backticked "
    "test name or `PR #N`; `moved #N` (an issue open when this one closed); or `control`. "
    "A body table whose rows are not cells gets `not a cell table` on the line just above it"
)


def failure_comment(v: Verdict) -> str:
    head = (
        f"**Class-closure check: #{v.number} closed with {len(v.problems)} unaccounted "
        f"cell row(s)** (`build_tools/check_class_closure.py`)."
    )
    fix = (
        f"To fix: {FIX_HINT}. A cell first posted in a comment goes in a `status`-column table; "
        "a later comment that restates a row's key supersedes it. Then re-run "
        f"`python3 build_tools/check_class_closure.py {v.number}`."
    )
    return "\n".join([head, "", *(f"- {p}" for p in v.problems), "", fix])


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
    ap.add_argument(
        "--comment-on-failure",
        action="store_true",
        help="post one comment on each issue that fails (the issue-closed workflow)",
    )
    args = ap.parse_args(argv)
    if not args.issues and not args.closed_since:
        ap.error("give issue numbers or --closed-since")

    issues = IssueCache()
    try:
        named = [issues.get(n) for n in args.issues]
        missing = [n for n, i in zip(args.issues, named) if i is None]
        if missing:
            print(
                f"no such issue: {', '.join('#' + str(n) for n in missing)}",
                file=sys.stderr,
            )
            return 2
        explicit = {i["number"] for i in named}
        if args.closed_since:
            named += [
                i
                for i in closed_since(args.closed_since)
                if i["number"] not in explicit
            ]
        verdicts = [
            check_issue(i, issues, args.report, i["number"] in explicit) for i in named
        ]
    except FetchError as e:
        print(f"cannot read the tracker: {e}", file=sys.stderr)
        return 2

    closed_failed = open_failed = 0
    for v in verdicts:
        when = f"closed {v.closed_at[:10]}" if v.closed_at else "open"
        if not v.checked:
            print(f"#{v.number} skip ({v.note})")
            continue
        tag = "FAIL" if v.problems else "ok"
        note = f"; {v.note}" if v.note else ""
        print(
            f"#{v.number} {tag} ({when}; {v.accounted} accounted, {len(v.problems)} not{note}) "
            f"{v.title[:80]}"
        )
        for p in v.problems:
            print(f"    {p}")
        if v.problems:
            if v.closed_at:
                closed_failed += 1
            else:
                open_failed += 1
    if args.report or not (closed_failed or open_failed):
        return 0
    if closed_failed:
        print(
            f"\n{closed_failed} class issue(s) closed with unaccounted cells.",
            file=sys.stderr,
        )
    if open_failed:
        print(
            f"\n{open_failed} open class issue(s) would close with unaccounted cells.",
            file=sys.stderr,
        )
    print(f"To fix: {FIX_HINT}.", file=sys.stderr)
    if args.comment_on_failure:
        try:
            for v in verdicts:
                if v.problems and v.closed_at:
                    api(
                        "POST",
                        f"repos/{REPO}/issues/{v.number}/comments",
                        {"body": failure_comment(v)},
                    )
        except FetchError as e:
            print(f"cannot post the failure comment: {e}", file=sys.stderr)
            return 2
    return 1


if __name__ == "__main__":
    sys.exit(main())
