#!/usr/bin/env python3
"""Check: may `/implement-plan` start on this plan? (`/implement-plan` Pre-Implementation step 2.)

Reads the plan's stamp lines (the leading HTML-comment block `/verify-plan` writes, or the DRAFT
marker `/create-plan` writes), its `## Scope` block's Tier line, its `## Issues to Close` inputs and
its override records, and prints one verdict:

  READY       the stamps allow implementation; print the Implementation Summary header values.
  REFUSE      stop; the message is step 2's. Also: a malformed stamp, a red `check_plan_refs.py`
              report on a light plan, or an override record that does not verify.
  ASK         the owner's word is needed; the printed question is the one to ask, once.
  UNVERIFIED  a fact the verdict needs lives on GitHub and could not be read (no `gh`, no network):
              an override's comment, or an input issue's labels. Never READY. Under `CI=true` (or
              `1`, any case) it is reported as REFUSE: on CI an unverifiable override fails.

Exit codes: READY 0, REFUSE 1, ASK 3, UNVERIFIED 4 (2 is a usage error or an unreadable plan).

The decision, in step 2's order (every branch has a test in build_tools/tests/test_check_plan_ready.py):

* Tier: the first `Tier:` in the plan's `## Scope` block (`- **Tier:** light — …`, `**Tier:** full (c)`,
  `` `tier: light (…)` ``); no Tier line means `full` (plans created before 2026-10-28).
* light: the stamp must be `Verified by /verify-plan --adequacy (Adequacy-only …)` (a full
  nine-dimension stamp is accepted: it grades Dimensions 6 and 7 too). No stamp, or the DRAFT marker,
  → REFUSE "This light plan has no adequacy pass. …". NEEDS REVISION → REFUSE. Then
  `check_plan_refs.py <plan> --issues` runs in-process; a red report (its exit 1: a missing path, a
  line past end of file, an R-LW cap) → REFUSE. Lookup errors are a NOTE: they never made that
  report red.
* full: no stamp, or the DRAFT marker → ASK "This plan has not been verified. …" (proceed or stop).
  NEEDS REVISION → REFUSE.
* Adequacy (both tiers): CLASS or N/A proceed. CELL proceeds unless a plan input is a bug, ICE or
  regression issue; then PASS / PASS WITH CORRECTIONS asks step 2's cell question and CONVERGED asks
  "converged with CELL …" with the `### Deferred findings` rows. A cited `Override (cell)` record
  that verifies answers either. A stamp without an Adequacy line, verified before 2026-08-26 (on
  that day: when its sha does not contain ADEQUACY_LINE_COMMIT), is N/A for a feature plan and CELL
  for a bug-fix plan unless the plan has a `## Defect Class` section; a later stamp without one is
  malformed.
* Scope (both tiers): `MULTI-GROUP`, or `Generator: MISSING`, asks step 2's scope question unless a
  cited `Override (scope)` record verifies. A stamp before 2026-09-27 (on that day: SCOPE_LINE_COMMIT)
  has no Scope line: the scope is derived from the plan's groups, which is a reading, so it is ASK
  with the group ids the title names as evidence. A later stamp without one is malformed.

Which inputs make a bug-fix plan: the issue numbers on the `## Issues to Close` list items, except
an item that says "stay open". An input is a bug, ICE or regression issue when GitHub gives it the
`bug` label or any `lane:*` label (contract §10: every lane names a defect), or its title says `ICE`
or "regression". No `## Issues to Close` section → ASK (which issues are the inputs?); a section with
no issue number → no bug input.

Override records. An override answers a gate only when it cites where the owner said it:

    - Override (cell): owner 2026-11-06 "override" — https://github.com/antonsynd/sharpy/issues/2433#issuecomment-1234567890
    - **Override (scope):** owner 2026-09-28 "<the owner's words>" — #2423 comment 7050154806

one line, outside fenced code, anywhere in the plan (`## Scope` › Override, `## Revision log`, the
`## Implementation Checklist`). The citation is an issue or pull request comment URL
(`#issuecomment-<id>`), a review comment URL (`#discussion_r<id>`), a review URL
(`#pullrequestreview-<id>`), or `#<issue> comment <id>`; the repository is REPO. The checker reads
the comment with `gh api -X GET` (or REST with GH_TOKEN / GITHUB_TOKEN) and accepts it only when
its `author_association` is `OWNER`, it belongs to the cited issue or pull request, and the quoted
words appear in its body (compared case-folded, with blockquote markers, emphasis marks, curly
quotes and runs of whitespace normalised). A record that fails either test → REFUSE (a record the
owner did not make must be corrected or deleted). Override text without a citation is listed as a
legacy record and answers nothing. Only the gates the plan hits are verified.

What this does NOT judge: whether an `OWNER` comment was typed by the owner or posted by an agent
with the owner's token (both read `OWNER`: a relayed comment that quotes the owner verifies the same
way); a tier override or the charter row behind a Tier line; a light plan whose group should have
been full; an output or layout change sharing a gate beyond what the stamp's Scope line (Dimension 9)
records; promotion in flight; the Implementation Summary header itself.

Usage::

    python3 build_tools/check_plan_ready.py <plan.md> [--repo <root>] [--offline] [--json]

    python3 build_tools/check_plan_ready.py --all [--repo <root>] [--offline]

`--offline` reads nothing from GitHub (overrides and labels are UNVERIFIED; the light tier's
`check_plan_refs.py` runs without `--issues`). `--all` prints one row per `docs/process/plans/*.md`
and exits 0. Never edits anything.
"""

from __future__ import annotations

import argparse
import contextlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Dict, List, Optional, Sequence, Set, Tuple

try:  # imported as build_tools.check_plan_ready (tests) or run as a script
    from build_tools import check_plan_refs as cpr
except ImportError:  # pragma: no cover - script mode
    import check_plan_refs as cpr  # type: ignore

REPO = "antonsynd/sharpy"
REPO_ROOT = Path(__file__).resolve().parent.parent
PLANS_GLOB = "docs/process/plans/*.md"

READY, REFUSE, ASK, UNVERIFIED = "READY", "REFUSE", "ASK", "UNVERIFIED"
EXIT = {READY: 0, REFUSE: 1, ASK: 3, UNVERIFIED: 4}
PRECEDENCE = (REFUSE, UNVERIFIED, ASK, READY)

# Dates from step 2's prose; the commits that added each stamp line to /verify-plan decide a stamp
# written on the cutoff day itself (its sha contains the commit → it should carry the line).
ADEQUACY_CUTOFF, ADEQUACY_LINE_COMMIT = "2026-08-26", "be99a0c4f"
SCOPE_CUTOFF, SCOPE_LINE_COMMIT = "2026-09-27", "95a734eea"

# Step 2's messages, verbatim.
MSG_UNVERIFIED = (
    "This plan has not been verified. Consider running `/verify-plan` first. "
    "Ask the owner whether to proceed or stop."
)
MSG_NEEDS_REVISION = (
    "This plan was flagged as needing revision. Please address the issues in the Verification "
    "Summary before implementing."
)
MSG_LIGHT_ABSENT = (
    "This light plan has no adequacy pass. Run `/verify-plan {path}` (it runs `--adequacy` for a "
    "light plan) first."
)
MSG_CELL = (
    "This plan patches a cell, not its class (see the plan's Adequacy verdict). Add a Defect Class "
    "section — contract, matrix, standard cure — and re-run `/verify-plan`, or say `override` to "
    "implement it as a cell fix knowingly."
)
MSG_CONVERGED_CELL = (
    "converged with CELL — say `override` to implement, or revise. Show the owner the "
    "`### Deferred findings` rows:"
)
MSG_SCOPE = (
    "This plan bundles more than one group (or skips its generator). Split it per the charter, or "
    "say `override` to implement it knowingly."
)
MSG_REFS_RED = (
    "`check_plan_refs.py {path} --issues` is red; fix the references until it is clean (the light "
    "pass corrects nothing inline, so references may have drifted since)."
)
MSG_RECORD = (
    '\n    Record the answer as `Override ({gate}): owner YYYY-MM-DD "<words>" — <comment URL>` once the '
    "owner has said it on GitHub (the checker verifies the comment)."
)

STAMP_RE = re.compile(r"^<!-- Verified by /verify-plan\b(?P<adequacy> --adequacy\b)?")
DRAFT_RE = re.compile(r"^<!-- DRAFT, not verified\b")
STAMP_DATE_RE = re.compile(r"\bon (\d{4}-\d{2}-\d{2})\b")
STAMP_SHA_RE = re.compile(r"@ ([0-9a-f]{7,40})\b")
RESULT_RE = re.compile(
    r"^<!-- Verification result: (PASS WITH CORRECTIONS|PASS|NEEDS REVISION|CONVERGED)\b"
)
RESULT_LINE_RE = re.compile(r"^<!-- Verification result:")
ADEQUACY_LINE_RE = re.compile(r"^<!-- Adequacy:(.*?)(?:-->|$)")
ADEQUACY_TOKEN_RE = re.compile(r"(?<![\w/])(CLASS|CELL|N/A)(?![\w/])")
SCOPE_LINE_RE = re.compile(r"^<!-- Scope:(.*?)(?:-->|$)")
SCOPE_TOKEN_RE = re.compile(r"^\s*(ONE-GROUP|MULTI-GROUP|N/A)\b")
GENERATOR_RE = re.compile(r"\bGenerator:\s*([A-Za-z-]+)")
TIER_RE = re.compile(
    r"\btier:[*_]*\s*`?(?:tier:\s*)?(light|full|band)\b", re.IGNORECASE
)
ISSUE_RE = re.compile(r"(?<![\w&/])#(\d{2,6})\b")
LIST_ITEM_RE = re.compile(r"^\s{0,3}(?:[-*+]|\d+[.)])\s+")
GROUP_ID_RE = re.compile(r"\bP\d+[a-z]*\b")
DEFECT_TITLE_RE = re.compile(r"\bICE\b|\bregression\b", re.IGNORECASE)

OVERRIDE_RE = re.compile(
    r"^\s{0,3}(?:[-*+]\s+)?[*_]*override\s*\((cell|scope)\)[*_]*\s*:[*_]*\s*owner,?\s+"
    r"(\d{4}-\d{2}-\d{2}),?:?\s*[\"“](.+?)[\"”](.*)$",
    re.IGNORECASE,
)
CITE_PATTERNS = (
    (
        "issue-comment",
        re.compile(
            r"https://github\.com/([\w.-]+/[\w.-]+)/(?:issues|pull)/(\d+)(?:/files)?#issuecomment-(\d+)"
        ),
    ),
    (
        "review-comment",
        re.compile(
            r"https://github\.com/([\w.-]+/[\w.-]+)/pull/(\d+)(?:/files)?#discussion_r(\d+)"
        ),
    ),
    (
        "review",
        re.compile(
            r"https://github\.com/([\w.-]+/[\w.-]+)/pull/(\d+)#pullrequestreview-(\d+)"
        ),
    ),
)
CITE_SHORT_RE = re.compile(r"(?<![\w/])#(\d+) comment (\d+)\b")
LEGACY_OVERRIDE_RE = re.compile(r"\boverride\b", re.IGNORECASE)
NO_OVERRIDE_RE = re.compile(r"\boverride:[*_]*\s*none\b", re.IGNORECASE)


class FetchError(Exception):
    pass


# ── GitHub ───────────────────────────────────────────────────────────────────────────────────────


def api_get(path: str):
    """GET a REST path (relative to the API root); None on 404/410. The only network access here:
    tests pass their own fetch. `gh api -X GET` first, then REST with GH_TOKEN / GITHUB_TOKEN."""
    gh_error = ""
    if shutil.which("gh"):
        proc = subprocess.run(
            ["gh", "api", "-X", "GET", path],
            capture_output=True,
            text=True,
            check=False,
        )
        if proc.returncode == 0:
            return json.loads(proc.stdout) if proc.stdout.strip() else {}
        out = proc.stdout + proc.stderr
        if "Not Found" in out or "HTTP 404" in out or "HTTP 410" in out:
            return None
        gh_error = out.strip()[:200]
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if not token:
        raise FetchError(
            gh_error or "neither gh nor GH_TOKEN / GITHUB_TOKEN is available"
        )
    req = urllib.request.Request(
        f"https://api.github.com/{path}",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read().decode()
            return json.loads(raw) if raw.strip() else {}
    except urllib.error.HTTPError as e:
        if e.code in (404, 410):
            return None
        raise FetchError(f"GET {path}: HTTP {e.code}") from e
    except (urllib.error.URLError, OSError) as e:
        raise FetchError(f"GET {path}: {e}") from e


def offline_fetch(path: str):
    raise FetchError("--offline: GitHub is not read")


def on_ci(environ=os.environ) -> bool:
    return environ.get("CI", "").lower() in ("true", "1")


# ── Parsing ──────────────────────────────────────────────────────────────────────────────────────


@dataclass
class Stamp:
    line: int
    adequacy_only: bool
    date: Optional[str]
    sha: Optional[str]
    result: Optional[str]  # None: no or unreadable result line
    result_line: bool
    adequacy: Optional[Set[str]]  # None: no Adequacy line; empty set: unreadable
    scope: Optional[str]  # None: no Scope line; "" unreadable
    generator: Optional[str]


@dataclass
class Override:
    line: int
    gate: str
    date: str
    words: str
    cite: Optional[Tuple[str, str, int, int]]  # (kind, repo, issue/pr number, id)
    text: str


@dataclass
class Plan:
    path: str
    text: str
    draft: bool
    stamp: Optional[Stamp]
    tier: str
    tier_text: Optional[str]
    defect_class: bool
    inputs: Optional[List[int]]  # None: no `## Issues to Close` section
    deferred: List[str]
    overrides: List[Override]
    legacy_overrides: List[Tuple[int, str]]
    title: str


def _outside_fences(text: str) -> List[Tuple[int, str]]:
    """(1-based line number, line) for each line outside fenced code."""
    out, fence = [], None
    for i, line in enumerate(text.split("\n"), 1):
        m = cpr.FENCE_LINE_RE.match(line)
        if fence is None:
            if m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
                fence = (m.group(1)[0], len(m.group(1)))
                continue
            out.append((i, line))
        elif (
            m
            and m.group(1)[0] == fence[0]
            and len(m.group(1)) >= fence[1]
            and not m.group(2).strip()
        ):
            fence = None
    return out


def _section(text: str, name: str) -> Optional[str]:
    """The text of the first `## <name>` section (heading suffixes allowed), or None."""
    for heading, sec in cpr.sections(text):
        if cpr.heading_name(heading) == name.casefold():
            return sec
    return None


def _section_start_line(text: str, name: str) -> Optional[int]:
    for pos, level, heading in cpr.scan_headings(text)[0]:
        if level == 2 and cpr.heading_name(heading) == name.casefold():
            return text.count("\n", 0, pos) + 1
    return None


def parse_stamp(header: List[str]) -> Optional[Stamp]:
    for i, line in enumerate(header):
        m = STAMP_RE.match(line.strip())
        if not m:
            continue
        date = STAMP_DATE_RE.search(line)
        sha = STAMP_SHA_RE.search(line)
        result, result_line, adequacy, scope, generator = None, False, None, None, None
        for other in header:
            s = other.strip()
            if RESULT_LINE_RE.match(s):
                result_line = True
                r = RESULT_RE.match(s)
                result = r.group(1) if r else None
            a = ADEQUACY_LINE_RE.match(s)
            if a and adequacy is None:
                adequacy = set(ADEQUACY_TOKEN_RE.findall(a.group(1)))
            sc = SCOPE_LINE_RE.match(s)
            if sc and scope is None:
                t = SCOPE_TOKEN_RE.match(sc.group(1))
                scope = t.group(1) if t else ""
                g = GENERATOR_RE.search(sc.group(1))
                generator = g.group(1) if g else None
        return Stamp(
            line=i + 1,
            adequacy_only=bool(m.group("adequacy")),
            date=date.group(1) if date else None,
            sha=sha.group(1) if sha else None,
            result=result,
            result_line=result_line,
            adequacy=adequacy,
            scope=scope,
            generator=generator,
        )
    return None


def parse_inputs(text: str) -> Optional[List[int]]:
    sec = _section(text, "Issues to Close")
    if sec is None:
        return None
    nums: List[int] = []
    items: List[str] = []
    for line in sec.split("\n")[1:]:
        if LIST_ITEM_RE.match(line) or line.lstrip().startswith("|"):
            items.append(line)
        elif items and line.strip() and line.startswith((" ", "\t")):
            items[-1] += " " + line.strip()  # a list item's continuation line
    for item in items:
        if re.search(r"\bstay(?:s)? open\b", item, re.IGNORECASE):
            continue
        for n in ISSUE_RE.findall(item):
            if int(n) not in nums:
                nums.append(int(n))
    return nums


def parse_cite(rest: str) -> Optional[Tuple[str, str, int, int]]:
    for kind, rx in CITE_PATTERNS:
        m = rx.search(rest)
        if m:
            return kind, m.group(1), int(m.group(2)), int(m.group(3))
    m = CITE_SHORT_RE.search(rest)
    if m:
        return "issue-comment", REPO, int(m.group(1)), int(m.group(2))
    return None


def parse_overrides(
    text: str, header_len: int
) -> Tuple[List[Override], List[Tuple[int, str]]]:
    cited, legacy = [], []
    record_sections = []
    for name in ("Scope", "Revision log", "Implementation Checklist"):
        start = _section_start_line(text, name)
        sec = _section(text, name)
        if start and sec:
            record_sections.append((start, start + sec.count("\n")))
    for n, line in _outside_fences(text):
        if n <= header_len:
            continue
        m = OVERRIDE_RE.match(line)
        if m:
            cited.append(
                Override(
                    line=n,
                    gate=m.group(1).lower(),
                    date=m.group(2),
                    words=m.group(3),
                    cite=parse_cite(m.group(4)),
                    text=line.strip(),
                )
            )
        elif (
            LEGACY_OVERRIDE_RE.search(line)
            and not NO_OVERRIDE_RE.search(line)
            and re.search(r"\bowner\b", line, re.IGNORECASE)
            and any(a <= n <= b for a, b in record_sections)
        ):
            legacy.append((n, line.strip()))
    return cited, legacy


def parse_deferred(text: str) -> List[str]:
    rows, inside = [], False
    for _, line in _outside_fences(text):
        if re.match(r"^#{2,3}\s", line):
            inside = bool(re.match(r"^###\s+Deferred findings\b", line, re.IGNORECASE))
            continue
        if inside and LIST_ITEM_RE.match(line):
            rows.append(line.strip())
    return rows


def parse_plan(path: str, text: str) -> Plan:
    lines = text.split("\n")
    header: List[str] = []
    for line in lines:
        if not cpr.is_stamp_line(line):
            break
        header.append(line)
    scope = _section(text, "Scope")
    tier, tier_text = "full", None
    if scope is not None:
        for _, line in _outside_fences(scope):
            m = TIER_RE.search(line)
            if m:
                tier, tier_text = m.group(1).lower(), line.strip()
                break
    title = next((l[2:].strip() for l in lines if l.startswith("# ")), "")
    overrides, legacy = parse_overrides(text, len(header))
    return Plan(
        path=path,
        text=text,
        draft=bool(lines and DRAFT_RE.match(lines[0].strip())),
        stamp=parse_stamp(header),
        tier=tier,
        tier_text=tier_text,
        defect_class=_section(text, "Defect Class") is not None,
        inputs=parse_inputs(text),
        deferred=parse_deferred(text),
        overrides=overrides,
        legacy_overrides=legacy,
        title=title,
    )


# ── Deciding ─────────────────────────────────────────────────────────────────────────────────────


@dataclass
class Finding:
    verdict: str
    message: str


@dataclass
class Report:
    plan: str
    tier: str
    stamp: str
    findings: List[Finding] = field(default_factory=list)
    notes: List[str] = field(default_factory=list)
    header: Dict[str, str] = field(default_factory=dict)
    refs_output: str = ""

    @property
    def verdict(self) -> str:
        got = {f.verdict for f in self.findings}
        return next((v for v in PRECEDENCE if v in got), READY)

    def add(self, verdict: str, message: str) -> None:
        self.findings.append(Finding(verdict, message))


def normalise(s: str) -> str:
    s = s.replace("“", '"').replace("”", '"').replace("‘", "'").replace("’", "'")
    s = re.sub(r"(?m)^\s*(?:>\s?)+", "", s)  # blockquote markers
    s = re.sub(r"[*_`]", "", s)
    return re.sub(r"\s+", " ", s).strip().casefold()


def comment_path(cite: Tuple[str, str, int, int]) -> str:
    kind, repo, number, cid = cite
    if kind == "issue-comment":
        return f"repos/{repo}/issues/comments/{cid}"
    if kind == "review-comment":
        return f"repos/{repo}/pulls/comments/{cid}"
    return f"repos/{repo}/pulls/{number}/reviews/{cid}"


def verify_override(o: Override, fetch: Callable) -> Tuple[str, str]:
    """(READY | REFUSE | UNVERIFIED, reason) for one cited override record."""
    where = f"line {o.line}"
    if o.cite is None:
        return (
            REFUSE,
            f"{where}: the override cites no owner comment (a comment URL or `#N comment <id>`)",
        )
    kind, repo, number, cid = o.cite
    if repo.casefold() != REPO.casefold():
        return REFUSE, f"{where}: the cited comment is in {repo}, not {REPO}"
    try:
        c = fetch(comment_path(o.cite))
    except FetchError as e:
        return UNVERIFIED, f"{where}: could not read comment {cid} ({e})"
    if not c:
        return REFUSE, f"{where}: comment {cid} does not exist"
    owner_url = c.get("issue_url") or c.get("pull_request_url") or ""
    if kind != "review" and not re.search(rf"/(?:issues|pulls)/{number}$", owner_url):
        return (
            REFUSE,
            f"{where}: comment {cid} is not on #{number} ({owner_url or 'no parent'})",
        )
    assoc = c.get("author_association")
    if assoc != "OWNER":
        login = (c.get("user") or {}).get("login", "?")
        return (
            REFUSE,
            f"{where}: comment {cid} is by {login} ({assoc}), not the repository OWNER",
        )
    if normalise(o.words) not in normalise(c.get("body") or ""):
        return REFUSE, f'{where}: the quoted words "{o.words}" are not in comment {cid}'
    return READY, f'{where}: owner comment {cid} holds "{o.words}"'


def check_gate(
    plan: Plan,
    report: Report,
    gate: str,
    question: str,
    fetch: Callable,
    extra: Sequence[str] = (),
) -> None:
    """The gate needs the owner's word: a verified cited override answers it, else ASK."""
    records = [o for o in plan.overrides if o.gate == gate]
    outcomes = [verify_override(o, fetch) for o in records]
    for v, reason in outcomes:
        if v == READY:
            report.notes.append(f"override ({gate}) verified — {reason}")
        else:
            report.add(v, f"override ({gate}) — {reason}")
    if records:
        return  # every record verified, or each one that did not is its own finding
    msg = question + "".join("\n    " + e for e in extra) + MSG_RECORD.format(gate=gate)
    for n, line in plan.legacy_overrides:
        msg += f"\n    legacy override text at line {n} (no `Override ({gate}):` record with a citation): {line[:160]}"
    report.add(ASK, msg)


def is_bug_fix(plan: Plan, fetch: Callable) -> Tuple[Optional[bool], str, str]:
    """(True | False | None = undecided, why, the verdict an undecided answer takes)."""
    if plan.inputs is None:
        return (
            None,
            "no `## Issues to Close` section: which issues are this plan's inputs?",
            ASK,
        )
    if not plan.inputs:
        return False, "`## Issues to Close` names no input issue", READY
    for n in plan.inputs:
        try:
            d = fetch(f"repos/{REPO}/issues/{n}")
        except FetchError as e:
            return None, f"could not read #{n}'s labels ({e})", UNVERIFIED
        if d is None:
            return None, f"#{n} does not exist: which issues are this plan's inputs?", ASK
        labels = [l.get("name", "") for l in d.get("labels", [])]
        hit = [l for l in labels if l == "bug" or l.startswith("lane:")]
        if hit or DEFECT_TITLE_RE.search(d.get("title") or ""):
            why = ", ".join(hit) if hit else "its title"
            return True, f"#{n} is a bug/ICE/regression issue ({why})", READY
    return (
        False,
        "no input carries `bug` or a `lane:*` label: "
        + ", ".join(f"#{n}" for n in plan.inputs),
        READY,
    )


def stamp_predates(
    stamp: Stamp, cutoff: str, commit: str, contains: Callable
) -> Optional[bool]:
    """Was the stamp written before /verify-plan wrote this line? None: undecidable."""
    if not stamp.date:
        return None
    if stamp.date != cutoff:
        return stamp.date < cutoff
    if not stamp.sha:
        return None
    has_line = contains(commit, stamp.sha)
    return None if has_line is None else not has_line


def git_contains(repo: Path) -> Callable[[str, str], Optional[bool]]:
    def contains(ancestor: str, sha: str) -> Optional[bool]:
        try:
            r = subprocess.run(
                ["git", "-C", str(repo), "merge-base", "--is-ancestor", ancestor, sha],
                capture_output=True,
                check=False,
            )
        except OSError:
            return None
        return {0: True, 1: False}.get(r.returncode)

    return contains


def run_refs(path: str, repo: Path, with_issues: bool) -> Tuple[int, str]:
    """`check_plan_refs.py <plan> --issues`, in-process: (exit code, its report)."""
    argv = [path, "--repo", str(repo)] + (["--issues"] if with_issues else [])
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        code = cpr.main(argv)
    return code, buf.getvalue()


def describe(stamp: Optional[Stamp], draft: bool) -> str:
    if stamp is None:
        return "DRAFT marker, no stamp" if draft else "no stamp"
    kind = "--adequacy" if stamp.adequacy_only else "full"
    adq = (
        "/".join(sorted(stamp.adequacy))
        if stamp.adequacy
        else ("none" if stamp.adequacy is None else "?")
    )
    return (
        f"{kind} on {stamp.date or '?'} @ {stamp.sha or '?'}: {stamp.result or '?'} · Adequacy {adq}"
        f" · Scope {stamp.scope if stamp.scope is not None else 'none'}"
        + (f" · Generator {stamp.generator}" if stamp.generator else "")
        + (" (and a DRAFT marker)" if draft else "")
    )


def decide(
    plan: Plan,
    *,
    fetch: Callable = api_get,
    refs: Optional[Callable[[str], Tuple[int, str]]] = None,
    contains: Callable[[str, str], Optional[bool]] = lambda a, b: None,
    ci: bool = False,
) -> Report:
    st = plan.stamp
    report = Report(plan=plan.path, tier=plan.tier, stamp=describe(st, plan.draft))
    tier_src = f"`{plan.tier_text[:100]}`" if plan.tier_text else "no Tier line"
    report.header["Tier"] = plan.tier
    if plan.tier not in ("light", "full"):
        report.add(
            ASK,
            f"tier `{plan.tier}` ({tier_src}) has no /implement-plan path: a band group has no plan",
        )
        return _finish(report, ci)

    unverified = st is None or plan.draft
    if plan.tier == "light":
        if unverified:
            report.add(REFUSE, MSG_LIGHT_ABSENT.format(path=plan.path))
            return _finish(report, ci)
    elif unverified:
        report.add(ASK, MSG_UNVERIFIED)
        return _finish(report, ci)

    assert st is not None
    if not st.result_line or st.result is None:
        report.add(
            REFUSE,
            f"the stamp (line {st.line}) has no readable `Verification result:` line; re-run `/verify-plan`",
        )
        return _finish(report, ci)
    if st.result == "NEEDS REVISION":
        report.add(REFUSE, MSG_NEEDS_REVISION)
        return _finish(report, ci)
    if plan.tier == "full" and st.adequacy_only:
        if not (plan.tier_text and "promoted at /implement-plan" in plan.tier_text):
            report.notes.append(
                "a full plan carries an Adequacy-only stamp without a promotion in flight on its Tier line; "
                "step 2 accepts any `/verify-plan` stamp at the full tier"
            )
    if plan.tier == "light" and not st.adequacy_only:
        report.notes.append(
            "a light plan with a full /verify-plan stamp: accepted (the full pass grades Dimensions 6 and 7 too)"
        )

    if plan.tier == "light":
        if refs is None:
            code, out = -1, ""
            report.add(UNVERIFIED, "check_plan_refs.py was not run on this light plan")
        else:
            code, out = refs(plan.path)
        report.refs_output = out
        if code > 0:
            report.add(REFUSE, MSG_REFS_RED.format(path=plan.path))
        errors = re.search(r"; (\d+) lookup errors;", out)
        if errors and int(errors.group(1)):
            report.notes.append(
                f"check_plan_refs: {errors.group(1)} issue lookups failed (issue states unchecked; not red)"
            )
        if "unchecked (use --issues)" in out and re.search(
            r"; [1-9]\d* unchecked", out
        ):
            report.notes.append(
                "check_plan_refs ran without --issues (offline): issue states unchecked"
            )

    # Adequacy
    adequacy = st.adequacy
    derived = ""
    if adequacy is None:
        pre = stamp_predates(st, ADEQUACY_CUTOFF, ADEQUACY_LINE_COMMIT, contains)
        if pre is None:
            report.add(
                ASK,
                f"the stamp has no Adequacy line and its date/sha cannot place it against {ADEQUACY_CUTOFF}: was it verified before the Adequacy line existed?",
            )
            return _finish(report, ci)
        if not pre:
            report.add(
                REFUSE,
                f"the stamp (on {st.date}) has no Adequacy line, which /verify-plan writes since {ADEQUACY_CUTOFF}; re-run `/verify-plan`",
            )
            return _finish(report, ci)
        bug, why, undecided = is_bug_fix(plan, fetch)
        if bug is None:
            report.add(
                undecided,
                f"no Adequacy line (stamped before /verify-plan wrote one, {ADEQUACY_CUTOFF}): "
                f"feature plan (N/A) or bug-fix plan (CELL unless it has a Defect Class section)? {why}",
            )
        elif not bug:
            adequacy, derived = (
                {"N/A"},
                f"N/A (stamped before /verify-plan wrote an Adequacy line, {ADEQUACY_CUTOFF}; feature plan: {why})",
            )
        elif plan.defect_class:
            adequacy, derived = (
                {"N/A"},
                f"proceed (no Adequacy line; bug-fix plan with a Defect Class section: {why})",
            )
        else:
            adequacy, derived = (
                {"CELL"},
                f"CELL (no Adequacy line; bug-fix plan without a Defect Class section: {why})",
            )
    elif not adequacy:
        report.add(
            REFUSE,
            "the stamp's Adequacy line names none of CLASS, CELL, N/A; re-run `/verify-plan`",
        )
        return _finish(report, ci)
    if derived:
        report.notes.append("Adequacy: " + derived)

    if adequacy is not None:
        label = "CELL" if "CELL" in adequacy else "/".join(sorted(adequacy))
        if derived:
            label = f"none (stamp before {ADEQUACY_CUTOFF}; read as {label})"
        report.header["Adequacy"] = label + (
            " (converged)" if st.result == "CONVERGED" else ""
        )
        if "CELL" in adequacy:
            bug, why, undecided = (
                (True, derived, READY)
                if derived.startswith("CELL")
                else is_bug_fix(plan, fetch)
            )
            if bug is None:
                report.add(
                    undecided,
                    f"Adequacy CELL: is any plan input a bug/ICE/regression issue? {why}",
                )
            elif bug:
                if st.result == "CONVERGED":
                    rows = plan.deferred or ["(no `### Deferred findings` rows found)"]
                    check_gate(plan, report, "cell", MSG_CONVERGED_CELL, fetch, rows)
                else:
                    check_gate(plan, report, "cell", MSG_CELL, fetch)
                report.header["Adequacy"] = "CELL (override)" + (
                    " (converged)" if st.result == "CONVERGED" else ""
                )
            else:
                report.notes.append(
                    f"Adequacy CELL on a plan with no bug input: proceeds ({why})"
                )

    # Scope
    if st.scope is None:
        pre = stamp_predates(st, SCOPE_CUTOFF, SCOPE_LINE_COMMIT, contains)
        if pre is None:
            report.add(
                ASK,
                f"the stamp has no Scope line and its date/sha cannot place it against {SCOPE_CUTOFF}: was it stamped before the Scope line existed?",
            )
        elif not pre:
            report.add(
                REFUSE,
                f"the stamp (on {st.date}) has no Scope line, which /verify-plan writes since {SCOPE_CUTOFF}; re-run `/verify-plan`",
            )
        else:
            groups = sorted(set(GROUP_ID_RE.findall(plan.title)))
            chores = "chore" in plan.title.casefold()
            evidence = (
                f"title names {', '.join(groups) if groups else 'no P-group id'}"
                + (" and chores" if chores else "")
            )
            if any(o.gate == "scope" for o in plan.overrides):
                check_gate(plan, report, "scope", MSG_SCOPE, fetch)
            else:
                report.add(
                    ASK,
                    f"no Scope line (stamped before {SCOPE_CUTOFF}): derive the scope from the plan's groups "
                    f"({evidence}). One non-chore group on one gate, no output/layout phase sharing a gate and "
                    f"no generator-required group without its generator → proceed; otherwise: {MSG_SCOPE}",
                )
    elif st.scope == "":
        report.add(
            REFUSE,
            "the stamp's Scope line names none of ONE-GROUP, MULTI-GROUP, N/A; re-run `/verify-plan`",
        )
    else:
        problems = []
        if st.scope == "MULTI-GROUP":
            problems.append("Scope: MULTI-GROUP")
        if (st.generator or "").upper() == "MISSING":
            problems.append("Generator: MISSING")
        if problems:
            check_gate(
                plan,
                report,
                "scope",
                MSG_SCOPE + " (" + "; ".join(problems) + ")",
                fetch,
            )
    return _finish(report, ci)


def _finish(report: Report, ci: bool) -> Report:
    if ci:
        for f in report.findings:
            if f.verdict == UNVERIFIED:
                f.verdict = REFUSE
                f.message += " — on CI a fact that cannot be verified fails"
    return report


# ── Output ───────────────────────────────────────────────────────────────────────────────────────


def render(report: Report) -> str:
    lines = [f"plan: {report.plan}", f"tier: {report.tier}", f"stamp: {report.stamp}"]
    for f in report.findings:
        lines.append(f"{f.verdict}: {f.message}")
    for n in report.notes:
        lines.append(f"NOTE: {n}")
    if report.refs_output:
        lines.append("check_plan_refs:")
        lines += ["  " + l for l in report.refs_output.rstrip().split("\n")]
    if report.verdict == READY and report.header:
        lines.append(
            "Implementation Summary header: "
            + " · ".join(f"{k}: {v}" for k, v in report.header.items())
        )
    lines.append(f"VERDICT: {report.verdict}")
    return "\n".join(lines)


def check_path(path: Path, repo: Path, *, offline: bool, ci: bool) -> Report:
    text = path.read_text(encoding="utf-8")
    try:
        shown = str(path.resolve().relative_to(repo))
    except ValueError:
        shown = str(path)
    plan = parse_plan(shown, text)
    return decide(
        plan,
        fetch=offline_fetch if offline else api_get,
        refs=lambda p: run_refs(str(path), repo, with_issues=not offline),
        contains=git_contains(repo),
        ci=ci,
    )


def main(argv: Optional[Sequence[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("plan", type=Path, nargs="?")
    ap.add_argument(
        "--all", action="store_true", help=f"one row per {PLANS_GLOB}; exit 0"
    )
    ap.add_argument("--repo", type=Path, default=REPO_ROOT)
    ap.add_argument("--offline", action="store_true", help="read nothing from GitHub")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv)
    repo = a.repo.resolve()
    ci = on_ci()
    if a.all:
        for p in sorted(repo.glob(PLANS_GLOB)):
            r = check_path(p, repo, offline=a.offline, ci=ci)
            first = r.findings[0].message.split("\n")[0][:110] if r.findings else ""
            print(f"{p.name}\t{r.verdict}\t{r.tier}\t{r.stamp}\t{first}")
        return 0
    if a.plan is None:
        ap.error("a plan path is required unless --all is given")
    try:
        report = check_path(a.plan, repo, offline=a.offline, ci=ci)
    except OSError as e:
        print(f"check_plan_ready: cannot read {a.plan}: {e}", file=sys.stderr)
        return 2
    if a.json:
        print(
            json.dumps(
                {
                    "plan": report.plan,
                    "verdict": report.verdict,
                    "tier": report.tier,
                    "stamp": report.stamp,
                    "findings": [
                        {"verdict": f.verdict, "message": f.message}
                        for f in report.findings
                    ],
                    "notes": report.notes,
                    "header": report.header,
                },
                indent=2,
            )
        )
    else:
        print(render(report))
    return EXIT[report.verdict]


if __name__ == "__main__":
    sys.exit(main())
