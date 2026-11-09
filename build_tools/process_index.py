#!/usr/bin/env python3
"""Generate the one-page process index of the live batching charter (handoff W3.3).

The round charter (`docs/process/rounds/remediation-round-<date>-batching.md`) is hand-edited prose
of 1,500 lines with single lines of several KB; every batching call and audit re-extracted
group → issues → tier → execution position → rulings from it by hand (`docs/audits/
health-check-2026-11-06.md` §9). This script extracts that join once, mechanically, and writes it to
`docs/process/rounds/index.md`:

- a header: the charter path, the call number and sha of the newest §7 history row, the census line;
- one table row per §1 execution position: group (P-id, name, §ref), tier (with its letter), issues
  (FIRST marked; struck issues shown as closed, done or moved), status (DONE / LANDED / OPEN from the
  §1 row, plus any §2 ledger row naming the group) and the current rulings — the rulings cited on the
  §1 row, its §3 heading and its `**Tier:**` line, each superseded one replaced by its successor,
  following the chain to its end;
- a ruling index (active, superseded, superseded in part, cited but not in §4);
- the parked list (§3's last section) and the open items pending owner confirmation;
- an "Unparsed" section: every row or ruling the parser could not read, by charter line. Nothing is
  dropped silently.

Supersession, as the charter writes it (§4's preamble: "A struck ruling is SUPERSEDED — the
strike-through names the ruling that replaced it"):

- FULL: a §4 row whose ruling id (or whole content) is struck through, pointing at its successor
  ("superseded by R-yy"); or prose in a later ruling that names an earlier one with nothing else
  ("Supersedes R-xx", "R-xx is superseded", "~~R-xx~~").
- IN PART: the same prose with a qualifier ("Supersedes R-BI's sequencing only", "the READ half of
  R-DW", "~~R-DX part 1~~"). The earlier ruling stays active for the rest; the index shows the part.

Usage:
    python3 -m build_tools.process_index [--charter PATH] [--write | --check]

Without --write/--check the index is printed to stdout. --check exits 1 when the committed index
differs from what the charter generates. The output is a pure function of the charter text: no
timestamps, no git or GitHub calls.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
ROUNDS_DIR = REPO_ROOT / "docs" / "process" / "rounds"
INDEX_PATH = ROUNDS_DIR / "index.md"
CHARTER_GLOB = "remediation-round-*-batching.md"
REGENERATE = "python3 -m build_tools.process_index --write"

RULING_RE = re.compile(r"(?<![\w-])R-([A-Z]{1,3})(?![A-Za-z])")
ISSUE_RE = re.compile(r"(?<![\w&])#(\d+)\b")
PID_RE = re.compile(r"(?<![\w])P\d+[a-z]*(?![\w])")
STRIKE_RE = re.compile(r"~~(.+?)~~", re.S)
SHA_RE = re.compile(r"\b[0-9a-f]{7,40}\b")
TIER_RE = re.compile(r"^\s*(?:\*\*)?(band|light|full)(?:\*\*)?\s*(\((.*)\))?", re.S)
LETTERS_RE = re.compile(r"^\s*([a-e](?:\s*,\s*[a-e])*)\s*(?:[,;:]|$)")
SECTION_RE = re.compile(r"^## (\d+)\.")
SUBSECTION_RE = re.compile(r"^### (\S+)\s*(.*)$")

# Words that do not make a supersession partial ("R-xx is superseded" is the whole ruling).
_FILLER = {
    "is",
    "are",
    "was",
    "were",
    "the",
    "and",
    "its",
    "now",
    "entirely",
    "wholly",
    "in",
    "full",
    "ruling",
    "rulings",
    "this",
    "also",
    "here",
    "s",
}


class CharterParseError(Exception):
    """The charter lacks a section the index cannot be built without."""


# --------------------------------------------------------------------------- small helpers


def ruling_key(rid: str) -> tuple[int, str]:
    """Sequence order of ruling ids: R-A … R-Z, R-AA … R-AZ, R-BA …"""
    letters = rid[2:] if rid.startswith("R-") else rid
    return (len(letters), letters)


def rulings_in(text: str) -> list[str]:
    """Ruling ids in order of first appearance."""
    seen: list[str] = []
    for m in RULING_RE.finditer(text):
        rid = "R-" + m.group(1)
        if rid not in seen:
            seen.append(rid)
    return seen


def strip_struck(text: str) -> str:
    return STRIKE_RE.sub(" ", text)


def plain(text: str) -> str:
    """Drop bold markers and squeeze whitespace (for display)."""
    return re.sub(r"\s+", " ", text.replace("**", "")).strip()


def excerpt(text: str, n: int = 100) -> str:
    t = plain(text)
    return t if len(t) <= n else t[: n - 1] + "…"


def split_row(line: str) -> list[str]:
    """Split a markdown table row on unescaped pipes; the outer empties are dropped."""
    cells = re.split(r"(?<!\\)\|", line.strip())
    if cells and cells[0].strip() == "":
        cells = cells[1:]
    if cells and cells[-1].strip() == "":
        cells = cells[:-1]
    return [c.strip() for c in cells]


def is_separator(line: str) -> bool:
    return bool(re.fullmatch(r"\|?(\s*:?-{3,}:?\s*\|)+\s*:?-*:?\s*\|?", line.strip()))


def cell(text: str) -> str:
    """Make text safe inside a markdown table cell."""
    return re.sub(r"(?<!\\)\|", r"\\|", text)


def depth_zero_spans(text: str) -> list[tuple[int, int]]:
    """Spans of `text` outside any parentheses."""
    spans, depth, start = [], 0, 0
    for i, ch in enumerate(text):
        if ch == "(":
            if depth == 0 and i > start:
                spans.append((start, i))
            depth += 1
        elif ch == ")" and depth > 0:
            depth -= 1
            if depth == 0:
                start = i + 1
    if depth == 0 and start < len(text):
        spans.append((start, len(text)))
    return spans


def following_paren(text: str, pos: int) -> str:
    """The parenthetical that starts at `pos` (after optional spaces), or ''."""
    i = pos
    while i < len(text) and text[i] in " *":
        i += 1
    if i >= len(text) or text[i] != "(":
        return ""
    depth = 0
    for j in range(i, len(text)):
        if text[j] == "(":
            depth += 1
        elif text[j] == ")":
            depth -= 1
            if depth == 0:
                return text[i : j + 1]
    return text[i:]


# --------------------------------------------------------------------------- model


@dataclass
class Unparsed:
    line: int
    where: str
    reason: str
    text: str


@dataclass
class IssueRef:
    number: int
    first: bool = False
    state: str = "open"  # open | closed | done | landed | moved
    moved_to: str = ""

    def render(self) -> str:
        n = f"#{self.number}"
        if self.state == "moved":
            return f"~~{n}~~ → {self.moved_to}"
        if self.state in ("closed", "done"):
            return f"~~{n}~~ {self.state}"
        s = f"**{n} FIRST**" if self.first else n
        return s + (" (landed)" if self.state == "landed" else "")


@dataclass
class Ruling:
    rid: str
    line: int
    issue: str
    content: str
    struck: bool = False
    superseded_by: str = ""  # full supersession, the immediate successor
    partial: list[tuple[str, str]] = field(default_factory=list)  # (by, qualifier)


@dataclass
class Group:
    line: int
    position: str
    pids: list[str]
    name: str
    sref: str
    tier: str
    letters: str
    issues: list[IssueRef]
    status: str
    rulings: list[str]
    predecessor: str = ""


@dataclass
class Index:
    charter: str
    call: str = ""
    call_date: str = ""
    sha: str = ""
    history_line: int = 0
    census: str = ""
    census_line: int = 0
    release: str = ""
    groups: list[Group] = field(default_factory=list)
    rulings: dict[str, Ruling] = field(default_factory=dict)
    parked: list[tuple[int, list[str]]] = field(default_factory=list)
    parked_title: str = ""
    parked_line: int = 0
    open_items: str = ""
    unparsed: list[Unparsed] = field(default_factory=list)


# --------------------------------------------------------------------------- parsing


def split_sections(lines: list[str]) -> dict[str, tuple[int, int]]:
    """`## N.` section number → (first line index, end index exclusive), 0-based."""
    starts = [
        (m.group(1), i) for i, ln in enumerate(lines) if (m := SECTION_RE.match(ln))
    ]
    out: dict[str, tuple[int, int]] = {}
    for k, (num, i) in enumerate(starts):
        end = starts[k + 1][1] if k + 1 < len(starts) else len(lines)
        out.setdefault(num, (i, end))
    return out


def parse_subsections(
    lines: list[str], lo: int, hi: int
) -> dict[str, tuple[int, int, str]]:
    """`### key title` within [lo, hi) → (line index, end, title)."""
    heads = [(i, m) for i in range(lo, hi) if (m := SUBSECTION_RE.match(lines[i]))]
    out: dict[str, tuple[int, int, str]] = {}
    for k, (i, m) in enumerate(heads):
        end = heads[k + 1][0] if k + 1 < len(heads) else hi
        out.setdefault(m.group(1), (i, end, m.group(2)))
    return out


def parse_issues(text: str) -> list[IssueRef]:
    """Issues named at depth 0 of an issues cell; struck ones classified closed/done/moved."""
    refs: list[IssueRef] = []
    seen: set[int] = set()
    # Struck spans first: the issues before the span's first parenthesis are the struck items.
    for m in STRIKE_RE.finditer(text):
        inner = m.group(1)
        head = inner.split("(", 1)[0]
        context = inner + " " + following_paren(text, m.end())
        moved = re.search(r"→\s*\**\s*(P\d+[a-z]*)", context)
        if moved:
            state, to = "moved", moved.group(1)
        elif "DONE" in context:
            state, to = "done", ""
        else:
            state, to = "closed", ""
        for im in ISSUE_RE.finditer(head):
            n = int(im.group(1))
            if n not in seen:
                seen.add(n)
                refs.append(IssueRef(n, state=state, moved_to=to))
    live = strip_struck(text)
    for a, b in depth_zero_spans(live):
        chunk = live[a:b]
        for im in ISSUE_RE.finditer(chunk):
            n = int(im.group(1))
            if n in seen:
                continue
            seen.add(n)
            ref = IssueRef(n)
            # FIRST belongs to the issue it follows, up to the next issue or separator; a
            # parenthetical between them (`#2045 (R-CO) FIRST`) is skipped.
            tail = live[a + im.end() :]
            while re.search(r"\([^()]*\)", tail):
                tail = re.sub(r"\([^()]*\)", " ", tail)
            nxt = re.search(r"#\d+|·", tail)
            if "FIRST" in (tail[: nxt.start()] if nxt else tail):
                ref.first = True
            if re.search(r"\bLANDED\b", following_paren(live, a + im.end())):
                ref.state = "landed"
            refs.append(ref)
    order = {r.number: i for i, r in enumerate(refs)}
    # Keep charter order: struck items were collected first, re-sort by position in the text.
    pos = {int(m.group(1)): m.start() for m in reversed(list(ISSUE_RE.finditer(text)))}
    return sorted(refs, key=lambda r: (pos.get(r.number, 0), order[r.number]))


def parse_tier(text: str) -> tuple[str, str]:
    m = TIER_RE.match(text)
    if not m:
        return "", ""
    letters = ""
    if m.group(3) is not None:
        lm = LETTERS_RE.match(m.group(3))
        if lm:
            letters = ", ".join(x.strip() for x in lm.group(1).split(","))
    return m.group(1), letters


def parse_group_cell(text: str) -> tuple[list[str], str, str, str, str]:
    """→ (P-ids, name, §ref, predecessor note, raw current text)."""
    predecessor = ""
    current = text
    m = re.search(r"DONE[^→]*→", text)
    if m:
        before = plain(strip_struck(text[: m.start()]))
        struck = " ".join(plain(s) for s in STRIKE_RE.findall(text[: m.end()]))
        predecessor = f"{(struck or before).strip()} DONE".strip()
        current = text[m.end() :]
    srefs = re.findall(r"\(§([^;)\s]+)", current)
    sref = srefs[-1] if srefs else ""
    body = strip_struck(current)
    body = re.sub(r"\(§[^)]*\)\s*$", "", body.strip()).strip()
    name_src = plain(body)
    if " — " in name_src:
        left, name = name_src.split(" — ", 1)
    else:
        left, name = name_src, name_src
    pids = PID_RE.findall(left) if " — " in name_src else []
    if not pids and " — " not in name_src:
        name = name_src
    return pids, name.strip(), sref, predecessor, current


def parse_order(
    idx: Index,
    lines: list[str],
    lo: int,
    hi: int,
    sec3: dict,
    ledger2: list[tuple[int, str, str]],
) -> None:
    for i in range(lo, hi):
        ln = lines[i]
        if not ln.startswith("|") or is_separator(ln):
            continue
        cells = split_row(ln)
        if cells and cells[0] == "#":
            continue  # header
        if len(cells) != 5:
            idx.unparsed.append(
                Unparsed(i + 1, "§1", f"{len(cells)} cells, expected 5", excerpt(ln))
            )
            continue
        pos, gtext, ttext, itext, ctext = cells
        if plain(gtext).lower().startswith("parked"):
            continue  # rendered from §3's last section
        if plain(gtext).lower() == "release":
            idx.release = plain(itext) + (f" — {plain(ctext)}" if ctext else "")
            continue
        pids, name, sref, predecessor, current = parse_group_cell(gtext)
        if not pos or not name:
            idx.unparsed.append(
                Unparsed(i + 1, "§1", "no position or group name", excerpt(ln))
            )
            continue
        tier, letters = parse_tier(ttext)
        if not tier:
            idx.unparsed.append(
                Unparsed(i + 1, "§1", "tier is not band/light/full", excerpt(ttext))
            )
        if not sref:
            idx.unparsed.append(
                Unparsed(i + 1, "§1", "group cites no §ref", excerpt(gtext))
            )
        elif sref not in sec3:
            idx.unparsed.append(
                Unparsed(i + 1, "§1", f"§{sref} has no §3 section", excerpt(gtext))
            )
        # Status: the group cell decides DONE / LANDED; anything else is OPEN.
        cur_plain = plain(strip_struck(current))
        if "DONE" in cur_plain:
            status = "DONE"
        elif "LANDED" in cur_plain:
            status = "LANDED"
        else:
            status = "OPEN"
        hits = [
            f"§2 {plain(rnd)} (line {ln2})"
            for ln2, rnd, groups in ledger2
            if pids
            and any(
                re.search(rf"(?<![\w]){re.escape(p)}(?![\w])", groups) for p in pids
            )
        ]
        if hits:
            status += (
                "; part landed: " + "; ".join(hits)
                if status == "OPEN"
                else " (" + "; ".join(hits) + ")"
            )
        # Rulings cited on the row (struck text excluded) + the §3 heading and its Tier line.
        cites_text = " ".join(strip_struck(c) for c in (current, ttext, itext, ctext))
        if sref in sec3:
            s_lo, s_hi, title = sec3[sref]
            cites_text += " " + strip_struck(title)
            for j in range(s_lo + 1, s_hi):
                if lines[j].startswith("**Tier:**"):
                    cites_text += " " + strip_struck(
                        lines[j].split("**Class tracker:**")[0]
                    )
                    break
        idx.groups.append(
            Group(
                line=i + 1,
                position=pos,
                pids=pids,
                name=name,
                sref=sref,
                tier=tier,
                letters=letters,
                issues=parse_issues(itext),
                status=status,
                rulings=sorted(rulings_in(cites_text), key=ruling_key),
                predecessor=predecessor,
            )
        )


def parse_ledger(lines: list[str], lo: int, hi: int) -> list[tuple[int, str, str]]:
    out = []
    for i in range(lo, hi):
        ln = lines[i]
        if not ln.startswith("|") or is_separator(ln):
            continue
        cells = split_row(ln)
        if len(cells) >= 2 and cells[0] != "Round":
            out.append((i + 1, cells[0], cells[1]))
    return out


def _qualifier(window: str, ids: list[str]) -> str:
    rest = window
    for rid in ids:
        rest = rest.replace(rid, " ")
    words = [w for w in re.findall(r"[A-Za-z0-9]+", rest) if w.lower() not in _FILLER]
    if not words:
        return ""
    q = plain(window).strip(" *")
    return re.sub(r"\s+(?:is|are)$", "", q)


def _add_edge(idx: Index, old: str, new: str, qualifier: str, line: int) -> None:
    if old == new:
        return
    r = idx.rulings.get(old)
    if r is None:
        return  # a predecessor or unknown id; the index lists it under "not in §4"
    if qualifier:
        if (new, qualifier) not in r.partial:
            r.partial.append((new, qualifier))
    elif not r.superseded_by:
        r.superseded_by = new
    elif r.superseded_by != new:
        idx.unparsed.append(
            Unparsed(
                line,
                "§4",
                f"{old} fully superseded by both {r.superseded_by} and {new}",
                "",
            )
        )


def parse_rulings(idx: Index, lines: list[str], lo: int, hi: int) -> None:
    rows: list[tuple[int, list[str]]] = []
    for i in range(lo, hi):
        ln = lines[i]
        if not ln.startswith("|") or is_separator(ln):
            continue
        cells = split_row(ln)
        if cells and cells[0] == "Ruling":
            continue
        ids = rulings_in(cells[0]) if cells else []
        if (
            len(cells) < 3
            or len(ids) != 1
            or plain(strip_struck(cells[0])).strip("~ ") not in ("", ids[0])
        ):
            idx.unparsed.append(
                Unparsed(
                    i + 1, "§4", "not a `| R-xx | issue | text |` row", excerpt(ln)
                )
            )
            continue
        rid = ids[0]
        content = " | ".join(cells[2:])
        struck = cells[0].startswith("~~") or content.lstrip().startswith("~~")
        if rid in idx.rulings:
            idx.unparsed.append(
                Unparsed(i + 1, "§4", f"{rid} defined twice", excerpt(ln))
            )
            continue
        idx.rulings[rid] = Ruling(rid, i + 1, plain(cells[1]), content, struck=struck)
        rows.append((i + 1, [rid, content]))

    for line, (rid, content) in rows:
        r = idx.rulings[rid]
        if r.struck:
            m = re.search(
                r"[Ss]uperseded by\s*\**\s*(R-[A-Z]{1,3})", content
            ) or re.search(r"(?:replaced by|→)\s*\**\s*(R-[A-Z]{1,3})", content)
            succ = (
                m.group(1)
                if m
                else next((x for x in rulings_in(content) if x != rid), "")
            )
            if succ:
                _add_edge(idx, rid, succ, "", line)
            else:
                idx.unparsed.append(
                    Unparsed(line, "§4", f"{rid} is struck but names no successor", "")
                )
            continue
        live = content
        # "R-xx … superseded by R-zz" (subject defaults to this ruling).
        for m in re.finditer(r"[Ss]uperseded by\s*\**\s*(R-[A-Z]{1,3})", live):
            window = _back_window(live, m.start())
            subj = [x for x in rulings_in(window)] or [rid]
            for s in subj:
                _add_edge(
                    idx,
                    s,
                    m.group(1),
                    _qualifier(window, subj) if s != rid else "",
                    line,
                )
        # "Supersedes X" — X runs to the clause end.
        for m in re.finditer(r"[Ss]upersedes\b", live):
            window = _fwd_window(live, m.end())
            ids = [x for x in rulings_in(window) if x != rid]
            q = _qualifier(window, ids)
            for x in ids:
                _add_edge(idx, x, rid, q, line)
        # "X (is|are) superseded" without "by".
        for m in re.finditer(r"[Ss]uperseded\b(?!\s*by)", live):
            window = _back_window(live, m.start())
            ids = [x for x in rulings_in(window) if x != rid]
            q = _qualifier(window, ids)
            for x in ids:
                _add_edge(idx, x, rid, q, line)
        # "~~R-xx …~~" inside a later ruling's text.
        for m in STRIKE_RE.finditer(live):
            ids = [x for x in rulings_in(m.group(1)) if x != rid]
            q = _qualifier(m.group(1), ids)
            for x in ids:
                _add_edge(idx, x, rid, q, line)


def _back_window(text: str, end: int) -> str:
    start = 0
    for m in re.finditer(r";|\.\s|—| \(|\*\*", text[:end]):
        start = m.end()
    return text[start:end]


def _fwd_window(text: str, start: int) -> str:
    m = re.search(r";|\.\s|\)| \(|—", text[start:])
    end = start + (m.start() if m else len(text) - start)
    return text[start : min(end, start + 200)]


def parse_history(idx: Index, lines: list[str], lo: int, hi: int) -> None:
    newest = None
    for i in range(lo, hi):
        ln = lines[i]
        if not ln.startswith("|") or is_separator(ln):
            continue
        cells = split_row(ln)
        if cells and cells[0] == "Date":
            continue
        if len(cells) != 4:
            idx.unparsed.append(
                Unparsed(i + 1, "§7", f"{len(cells)} cells, expected 4", excerpt(ln))
            )
            continue
        newest = (i + 1, cells)
    if newest is None:
        raise CharterParseError("§7 history has no rows")
    line, (date, sha_cell, event, _census) = newest
    idx.history_line = line
    idx.call_date = plain(date)
    shas = SHA_RE.findall(sha_cell)
    idx.sha = shas[0] if shas else ""
    m = re.search(r"\bcall (\d+)", event)
    idx.call = m.group(1) if m else ""
    if not idx.sha or not idx.call:
        idx.unparsed.append(
            Unparsed(
                line, "§7", "newest row names no call number or sha", excerpt(event)
            )
        )


def parse_header(idx: Index, lines: list[str], hi: int) -> None:
    for i in range(hi):
        m = re.match(r"\*\*State at call (\d+)", lines[i])
        if not m:
            continue
        idx.census_line = i + 1
        c = re.search(r"\*\*(\d+ open = [^*]+?)\*\*", lines[i])
        idx.census = plain(c.group(1)) if c else ""
        if not c:
            idx.unparsed.append(
                Unparsed(
                    i + 1,
                    "header",
                    "State line carries no `N open = …` census",
                    excerpt(lines[i]),
                )
            )
        if idx.call and m.group(1) != idx.call:
            idx.unparsed.append(
                Unparsed(
                    i + 1,
                    "header",
                    f"newest State line is call {m.group(1)}, newest §7 row is call {idx.call}",
                    "",
                )
            )
        return
    idx.unparsed.append(Unparsed(1, "header", "no `**State at call N` line", ""))


def parse_parked(idx: Index, lines: list[str], sec3: dict) -> None:
    key = next((k for k in sec3 if k.endswith("last")), None) or (
        list(sec3)[-1] if sec3 else None
    )
    if key is None:
        raise CharterParseError("§3 has no sections")
    lo, hi, title = sec3[key]
    idx.parked_title = f"§{key} {plain(title)}"
    idx.parked_line = lo + 1
    para = ""
    for j in range(lo + 1, hi):
        if lines[j].strip():
            para = lines[j]
            break
    lead = para.split("—", 1)[0]
    numbers = [int(n) for n in ISSUE_RE.findall(lead)]
    notes: dict[int, list[str]] = {}
    # A parked issue's note is the sentence that opens with its bold number.
    for m in re.finditer(r"\*\*#(\d+)\*\*(.*?)(?=\.\s|\*\*#\d+\*\*|$)", para, re.S):
        notes.setdefault(int(m.group(1)), []).extend(rulings_in(m.group(2)))
    idx.parked = [(n, sorted(set(notes.get(n, [])), key=ruling_key)) for n in numbers]
    cm = re.search(r"\((\d+)\)", title)
    if cm and int(cm.group(1)) != len(numbers):
        idx.unparsed.append(
            Unparsed(
                lo + 1,
                "§3",
                f"heading counts {cm.group(1)} parked, the list names {len(numbers)}",
                "",
            )
        )


def parse_open_items(idx: Index, lines: list[str]) -> None:
    for ln in lines:
        if ln.startswith("**Open items pending owner confirmation:**"):
            text = ln[len("**Open items pending owner confirmation:**") :]
            idx.open_items = text.split(" **Call ", 1)[0].strip()
            return


def parse_charter(text: str, charter: str) -> Index:
    lines = text.splitlines()
    secs = split_sections(lines)
    for need in ("1", "2", "3", "4", "7"):
        if need not in secs:
            raise CharterParseError(f"{charter}: no `## {need}.` section")
    idx = Index(charter=charter)
    s3 = parse_subsections(lines, *secs["3"])
    ledger2 = parse_ledger(lines, *secs["2"])
    parse_history(idx, lines, *secs["7"])
    parse_header(idx, lines, secs["1"][0])
    parse_rulings(idx, lines, *secs["4"])
    parse_order(idx, lines, secs["1"][0], secs["1"][1], s3, ledger2)
    parse_parked(idx, lines, s3)
    parse_open_items(idx, lines)
    return idx


# --------------------------------------------------------------------------- resolution


def chain(idx: Index, rid: str) -> tuple[list[str], bool, str]:
    """Follow successors from `rid`: (path incl. rid, any hop partial, cycle note)."""
    path, partial, cur = [rid], False, rid
    while True:
        r = idx.rulings.get(cur)
        if r is None:
            return path, partial, ""
        if r.superseded_by:
            nxt = r.superseded_by
        elif r.partial and cur == rid:
            nxt, partial = r.partial[0][0], True
        elif r.partial and partial:
            nxt = r.partial[0][0]
        else:
            return path, partial, ""
        if nxt in path:
            return path, partial, f"cycle at {nxt}"
        path.append(nxt)
        cur = nxt


def current_rulings(idx: Index, cited: list[str]) -> str:
    items: list[tuple[tuple[int, str], str]] = []
    covered: set[str] = set()
    full_paths = {}
    for rid in cited:
        path, partial, note = chain(idx, rid)
        if len(path) > 1 and not partial and not note:
            full_paths[rid] = path
            covered.update(path[1:])
    for rid in cited:
        if rid in covered:
            continue  # a hop of another cited ruling's chain: shown once, there
        if rid in full_paths:
            items.append((ruling_key(rid), " → ".join(full_paths[rid])))
            continue
        path, partial, _ = chain(idx, rid)
        if partial and len(path) > 1:
            items.append((ruling_key(rid), f"{rid} (in part → {' → '.join(path[1:])})"))
        else:
            items.append((ruling_key(rid), rid))
    seen, out = set(), []
    for _, s in sorted(items):
        if s not in seen:
            seen.add(s)
            out.append(s)
    return " · ".join(out) if out else "—"


# --------------------------------------------------------------------------- rendering


def render(idx: Index) -> str:
    out: list[str] = []
    w = out.append
    w("# Process index — the live batching charter on one page")
    w("")
    w(
        f"<!-- Generated by `{REGENERATE}` from `{idx.charter}`; do not edit by hand. -->"
    )
    w("")
    w(
        f"- **Charter:** [`{Path(idx.charter).name}`]({Path(idx.charter).name}) (`{idx.charter}`)"
    )
    w(
        f"- **Call:** {idx.call or '?'} ({idx.call_date}) @ {idx.sha or '?'} — the newest §7 history row (charter line {idx.history_line})"
    )
    w(f"- **Census:** {idx.census or '?'} (charter line {idx.census_line})")
    if idx.release:
        w(f"- **Release:** {idx.release}")
    w(
        "- **Reading:** one row per §1 execution position. Issues: **FIRST** marked; struck ones as the "
        "charter left them (closed, done, or → the group they moved to). Rulings: those cited on the §1 "
        "row, its §3 heading and its `**Tier:**` line; a superseded one is shown as `R-xx → R-yy` "
        "(its chain to the end), one superseded in part as `R-xx (in part → R-yy)`."
    )
    w("")
    w("## Execution order (§1)")
    w("")
    w("| # | Group (§ref) | Tier | Issues | Status | Current rulings |")
    w("|---|---|---|---|---|---|")
    for g in idx.groups:
        pid = " → ".join(g.pids)
        name = f"**{pid}** — {g.name}" if pid else f"**{g.name}**"
        if g.sref:
            name += f" (§{g.sref})"
        if g.predecessor:
            name = f"{g.predecessor} → " + name
        tier = (f"{g.tier} ({g.letters})" if g.letters else g.tier) or "?"
        issues = " · ".join(r.render() for r in g.issues) or "—"
        w(
            f"| {cell(g.position)} | {cell(name)} | {tier} | {cell(issues)} | {cell(g.status)} | "
            f"{cell(current_rulings(idx, g.rulings))} |"
        )
    w("")
    counts: dict[str, list[str]] = {}
    for g in idx.groups:
        counts.setdefault(g.tier or "?", []).append(g.letters)
    letters = sorted(
        {x.strip() for ls in counts.get("full", []) for x in ls.split(",") if x.strip()}
    )
    parts = []
    if "full" in counts:
        parts.append(
            f"full: {len(counts['full'])} groups (letters {', '.join(letters) or '—'})"
        )
    for t in ("light", "band", "?"):
        if t in counts:
            parts.append(f"{t}: {len(counts[t])}")
    w(f"{len(idx.groups)} rows · " + " · ".join(parts))
    w("")

    w("## Ruling index (§4)")
    w("")
    active = [r for r in idx.rulings.values() if not r.superseded_by and not r.partial]
    sup = [r for r in idx.rulings.values() if r.superseded_by]
    part = [r for r in idx.rulings.values() if r.partial and not r.superseded_by]
    w(
        f"{len(idx.rulings)} rulings in §4: {len(active)} active, {len(part)} active but superseded in part, {len(sup)} superseded."
    )
    w("")
    if sup:
        w("**Superseded** (chain followed to its end):")
        w("")
        w("| Ruling | Status | §4 line |")
        w("|---|---|---|")
        for r in sorted(sup, key=lambda r: ruling_key(r.rid)):
            path, _, note = chain(idx, r.rid)
            status = f"superseded by {r.superseded_by}"
            if len(path) > 2:
                status += f" (chain {' → '.join(path)})"
            if note:
                status += f"; {note}"
            w(f"| {r.rid} | {status} | {r.line} |")
        w("")
    if part:
        w("**Superseded in part** (the ruling stays active for the rest):")
        w("")
        w("| Ruling | By | The part, as the charter words it | §4 line |")
        w("|---|---|---|---|")
        for r in sorted(part, key=lambda r: ruling_key(r.rid)):
            for by, q in r.partial:
                w(f"| {r.rid} | {by} | {cell(excerpt(q, 120))} | {r.line} |")
        w("")
    w(
        f"**Active** ({len(active)}): "
        + ", ".join(r.rid for r in sorted(active, key=lambda r: ruling_key(r.rid)))
    )
    w("")
    cited = sorted(
        {rid for g in idx.groups for rid in g.rulings}
        | {rid for _, rs in idx.parked for rid in rs},
        key=ruling_key,
    )
    missing = [rid for rid in cited if rid not in idx.rulings]
    if missing:
        first = min((r.rid for r in idx.rulings.values()), key=ruling_key, default="")
        older = [r for r in missing if first and ruling_key(r) < ruling_key(first)]
        newer = [r for r in missing if r not in older]
        if older:
            w(
                f"**Cited, from the predecessor charter** (before {first}): "
                + ", ".join(older)
            )
            w("")
        if newer:
            w("**Cited but not in §4:** " + ", ".join(newer))
            w("")

    w(f"## Parked ({idx.parked_title})")
    w("")
    if idx.parked:
        w(
            " · ".join(
                f"#{n}" + (f" ({', '.join(rs)})" if rs else "") for n, rs in idx.parked
            )
            + f" — {len(idx.parked)} issues (charter line {idx.parked_line})."
        )
    else:
        w("None.")
    w("")
    if idx.open_items:
        w("## Open items pending owner confirmation")
        w("")
        w(idx.open_items)
        w("")
    w("## Unparsed")
    w("")
    if idx.unparsed:
        w(
            "Rows the generator could not read; fix the charter row or the generator. Nothing above is guessed from them."
        )
        w("")
        w("| Charter line | Section | Reason | Text |")
        w("|---|---|---|---|")
        for u in sorted(idx.unparsed, key=lambda u: (u.line, u.reason)):
            w(f"| {u.line} | {u.where} | {cell(u.reason)} | {cell(u.text)} |")
    else:
        w("None.")
    w("")
    return "\n".join(out)


# --------------------------------------------------------------------------- CLI


def newest_charter(rounds: Path = ROUNDS_DIR) -> Path:
    """The newest charter by the date in its name (deterministic, unlike mtime after a checkout)."""
    found = sorted(rounds.glob(CHARTER_GLOB))
    if not found:
        raise CharterParseError(f"no {CHARTER_GLOB} under {rounds}")
    return found[-1]


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def generate(charter: Path) -> str:
    return render(
        parse_charter(charter.read_text(encoding="utf-8"), display_path(charter))
    )


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        prog="python3 -m build_tools.process_index",
        description=__doc__.split("\n\n")[0],
    )
    ap.add_argument(
        "--charter", type=Path, help="charter to index (default: the newest by name)"
    )
    ap.add_argument("--index", type=Path, default=INDEX_PATH, help=argparse.SUPPRESS)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument(
        "--write", action="store_true", help=f"write {display_path(INDEX_PATH)}"
    )
    mode.add_argument(
        "--check", action="store_true", help="exit 1 if the committed index is stale"
    )
    args = ap.parse_args(argv)

    try:
        charter = args.charter or newest_charter()
        rendered = generate(charter)
    except (CharterParseError, OSError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    index: Path = args.index
    if args.check:
        if not index.exists():
            print(
                f"error: {display_path(index)} does not exist. Generate it with `{REGENERATE}`.",
                file=sys.stderr,
            )
            return 1
        if index.read_text(encoding="utf-8") != rendered:
            print(
                f"error: {display_path(index)} is stale against {display_path(charter)}. "
                f"Regenerate with `{REGENERATE}` and commit it with the charter.",
                file=sys.stderr,
            )
            return 1
        print(f"process index up to date: {display_path(index)}")
        return 0
    if args.write:
        index.write_text(rendered, encoding="utf-8")
        print(f"wrote {display_path(index)} from {display_path(charter)}")
        return 0
    sys.stdout.write(rendered)
    return 0


if __name__ == "__main__":
    sys.exit(main())
