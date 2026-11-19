#!/usr/bin/env python3
"""Every reference the procedure text makes resolves (skills, agents, CLAUDE.md, the process README).

The skills cite each other, CLAUDE.md, the process README and the verification contract by repo path,
by skill name, by section and by markdown link. A renamed heading or a moved file leaves such a
reference pointing at nothing, silently; this check makes it red. The reference kinds, from a census
of the scanned files:

* ``path`` -- a repo-relative file path, extracted with ``check_plan_refs.PATH_RE`` (the plans'
  extractor), plus what its grammar cannot spell (no listed extension, a directory, ``editors/``),
  read from code spans and fences only. A gitignored path (``git check-ignore``: ``.claude/plans/``,
  ``.claude/tmp/``, build output) is a session's file, not a missing one; so is a family example
  (``audit-YYYY-MM-DD.md``, ``plan-a1b2c3.md``: see ``_example``). A placeholder or glob
  (``<plan-id>``, ``*``) is never extracted.
* ``skill`` -- ``/name`` at a word start or opening a code span: a directory under ``.claude/skills/``
  or one of ``BUILTIN_SKILLS``. ``/name*`` is a glob, not a citation.
* ``section`` -- ``TARGET › Name › Name[, step|item N]``; ``TARGET Name §N``; ``TARGET item N``;
  ``§Name`` and ``Name step|item|§ N`` (the citing file's own sections); the shorthands in
  ``ALIASES`` (``stage trail item N``, ``README item N``, ``Rule N``, ``Dimension N``). A TARGET is a
  backticked path or ``/skill``, a bare ``CLAUDE.md``, or a markdown link; a ``›`` chain with no
  target names the plan template's sections (``Defect Class › Violated contract``), which live in
  ``/create-plan``'s fenced template. A Name is a heading (any level, outside fences), a bold lead-in
  (``**Commit gate**``, ``- **Layer ladder:**``) or a numbered heading (``### 1.25.``); a number is a
  top-level ``N.`` list item or an ``N.`` subheading inside the named section.
* ``contract`` -- ``contract §N``, ``§N–§M``, ``verification-contract.md §N``: a ``## N.`` heading of
  ``docs/design/verification-contract.md``.
* ``link`` -- ``[text](relative/target#anchor)``: the file exists and the anchor is a heading slug.

Out of scope, counted (``OUT_OF_SCOPE``): a bare ``§N`` / ``step N`` / ``item N`` with no named target
(its target is the enclosing text), ``charter §N`` (a round charter, not a fixed file), and absolute
URLs. Anything the grammar starts to parse but cannot finish (a ``›`` with nothing resolvable on its
left) is a failure, never a skip.

Usage::

    python3 build_tools/check_skill_refs.py [--repo <root>] [-v]

Exit 1 when any reference does not resolve. Never edits anything.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterator, List, Optional, Set, Tuple

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from build_tools.check_plan_refs import FENCE_LINE_RE, PATH_RE, heading_name

SCANNED_GLOBS = (
    "CLAUDE.md",
    ".claude/skills/*/SKILL.md",
    ".claude/skills/README.md",
    ".claude/agents/*.md",
    "docs/process/README.md",
    "docs/design/verification-contract.md",
)
CONTRACT = "docs/design/verification-contract.md"
PLAN_TEMPLATE = ".claude/skills/create-plan/SKILL.md"
PROCESS = "docs/process/README.md"
KINDS = ("path", "skill", "section", "contract", "link")
OUT_OF_SCOPE = {
    "bare-number": "a bare §N / step N / item N names no target; it means the enclosing text",
    "charter": "charter §N cites a round charter, a per-round file, not a fixed target",
    "url": "an absolute URL is outside the repository",
}
# Harness built-ins the text cites as /name; each must still be cited (the test checks it).
BUILTIN_SKILLS = {
    "simplify",  # /implement-plan Final Verification step 2
}
# Shorthand -> (target file, section name), from the census.
ALIASES = (
    (
        re.compile(r"\bstage trail,? (items?) (\d+)(?: and (\d+))?"),
        PROCESS,
        "The stage trail",
    ),
    (
        re.compile(r"\b(?:README|Process) (items?) (\d+)(?: and (\d+))?"),
        PROCESS,
        "The stage trail",
    ),
    (
        re.compile(r"\b(?:CLAUDE\.md )?(?:Critical )?(Rules?) (\d+)(?: and (\d+))?"),
        "CLAUDE.md",
        "Critical Rules",
    ),
    (
        re.compile(r"\b(Dimensions?) (\d+)(?: and (\d+))?"),
        ".claude/skills/verify-plan/SKILL.md",
        "Verification Dimensions",
    ),
)

ATX_RE = re.compile(r"^ {0,3}(#{1,6})[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")
LEADIN_RE = re.compile(r"^(?:[-*]|\d+\.)?[ \t]*\*\*([^*]+)\*\*")
ITEM_RE = re.compile(r"^(\d+)\.[ \t]")
NUMBERED_RE = re.compile(r"^(\d+(?:\.\d+)*)\.?(?:\s|$)")
WS_RE = re.compile(r"\s+")

TICK_TARGET = r"`(/[a-z][\w-]*|[\w./-]+\.md)`"
TARGET_RE = re.compile(
    r"(?:" + TICK_TARGET + r"|\[[^\]\n]*\]\(([^)\s]+)\)|\b(CLAUDE\.md)\b)\s*$"
)
TARGET_AHEAD_RE = re.compile(
    TICK_TARGET
    + r"(?: ((?:[A-Z][\w-]*)(?: [A-Z][\w-]*)*))? (?:§(\d+)|(items?|step) (\d+))"
)
CHAIN_HEAD_RE = re.compile(r"((?:[A-Z][\w-]*)(?: (?:[A-Z&][\w-]*|and))*)\s*$")
SEG_END_RE = re.compile(r"[)(,;:]|\.(?:\s|$)|\s(?:step|item|items) \d|\s§\d|\s›|$")
TAIL_RE = re.compile(r",?\s*(step|items?) (\d+)(?: and (\d+))?")
NAMED_NUM_RE = re.compile(
    r"(?<![\w`/.-])((?:[A-Z][\w-]*)(?: [A-Z][\w-]*)*) (?:§(\d+)|(step|items?) (\d+))"
)
SELF_SECTION_RE = re.compile(r"(?<![\w§])§([A-Z][\w-]+(?: [A-Z][\w-]*)*)")
CONTRACT_RE = re.compile(
    r"(?:\bcontract|verification-contract\.md`?) §(\d+)(?:\.\d+)?(?:[–-]§(\d+))?"
)
CHARTER_RE = re.compile(r"\bcharter §\d")
BARE_NUM_RE = re.compile(r"§\d+|\b[Ss]tep \d+|\bitems? \d+")
SKILL_RE = re.compile(r"(?<![^\s(\[`\"'])/([a-z][\w-]*)(?![/\w.*-])(:[\w-]+)?")
LINK_RE = re.compile(r"\[[^\]\n]*\]\(([^)\s]+)\)")
CODE_PATH_RE = re.compile(
    r"(?<![\w/.$}-])((?:src|docs|build_tools|benchmarks|editors|\.claude|\.github)/[\w./-]*)"
)
GLOB_CHARS = ("*", "<", "{", "$", "[")
CODE_SPAN_RE = re.compile(r"(`+)([^`].*?)\1", re.S)


def code_regions(text: str) -> Iterator[Tuple[int, int]]:
    """(start, end) offsets of fenced block bodies and of inline code spans outside them."""
    pos, fence, prose = 0, None, []
    for line in text.splitlines(keepends=True):
        m = FENCE_LINE_RE.match(line.rstrip("\r\n"))
        if fence is None and m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
            fence = (m.group(1)[0], len(m.group(1)), pos + len(line))
        elif (
            fence is not None
            and m
            and m.group(1)[0] == fence[0]
            and len(m.group(1)) >= fence[1]
            and not m.group(2).strip()
        ):
            yield fence[2], pos
            fence = None
        elif fence is None:
            prose.append((pos, pos + len(line)))
        pos += len(line)
    for a, b in prose:
        for m in CODE_SPAN_RE.finditer(text[a:b]):
            yield a + m.start(2), a + m.end(2)


@dataclass
class Ref:
    kind: str
    file: str
    line: int
    cite: str
    error: Optional[str] = None  # None = resolved


@dataclass
class Section:
    names: Tuple[str, ...]
    start: int  # line index, inclusive
    end: int  # line index, exclusive
    level: int


def norm(s: str) -> str:
    return WS_RE.sub(" ", s.replace("*", "").replace("`", "").strip(" \"'")).casefold()


def _names(text: str) -> Tuple[str, ...]:
    out = {norm(text), norm(heading_name(text)), norm(text.rstrip(":."))}
    m = NUMBERED_RE.match(text)
    if m:
        out.add(m.group(1))
        out.add(norm(text[m.end() :]))
        out.add(norm(heading_name(text[m.end() :])))
    return tuple(n for n in out if n)


class Doc:
    """A target file's sections: headings, bold lead-ins and the numbers inside each."""

    def __init__(self, text: str, *, with_fences: bool = False):
        self.lines = text.splitlines()
        heads: List[Tuple[int, int, str]] = []  # (line, level, text)
        leadins: List[Tuple[int, str]] = []
        fence = None
        for i, line in enumerate(self.lines):
            m = FENCE_LINE_RE.match(line)
            if fence is None and m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
                fence = (m.group(1)[0], len(m.group(1)))
                continue
            if fence is not None:
                if (
                    m
                    and m.group(1)[0] == fence[0]
                    and len(m.group(1)) >= fence[1]
                    and not m.group(2).strip()
                ):
                    fence = None
                    continue
                if not with_fences:
                    continue
            h = ATX_RE.match(line)
            if h:
                heads.append((i, len(h.group(1)), h.group(2).strip()))
                continue
            b = LEADIN_RE.match(line.strip())
            if b:
                leadins.append((i, b.group(1)))
        self.sections: List[Section] = []
        n = len(self.lines)
        for k, (i, level, text) in enumerate(heads):
            end = next((j for j, lv, _ in heads[k + 1 :] if lv <= level), n)
            self.sections.append(Section(_names(text), i, end, level))
        for i, text in leadins:
            end = next((j for j, _, _ in heads if j > i), n)
            self.sections.append(Section(_names(text), i, end, 7))
        self.whole = Section((), 0, n, 0)

    def find(self, cited: str, within: Optional[Section] = None) -> List[Section]:
        """Every section (inside `within`) whose name is the cited one, or starts it, or starts with it
        at a word end (prose runs on after a citation; a citation may shorten a heading)."""
        c = norm(cited)
        return [
            s
            for s in self.sections
            if (within is None or within.start < s.start < within.end)
            and any(c == n or _prefix(n, c) or _prefix(c, n) for n in s.names)
        ]

    def has_number(self, sec: Section, num: str) -> bool:
        for i in range(sec.start + (1 if sec.level else 0), sec.end):
            line = self.lines[i]
            m = ITEM_RE.match(line)
            if m and m.group(1) == num:
                return True
            h = ATX_RE.match(line)
            if (
                h
                and NUMBERED_RE.match(h.group(2))
                and NUMBERED_RE.match(h.group(2)).group(1) == num
            ):
                return True
        return False


def _prefix(long: str, short: str) -> bool:
    """`long` starts with `short` and the next character ends a word. A number (`1`, `1.25`) is
    matched only whole, so `### 1.` never answers a citation of `1.25`."""
    if NUMBER_RE.fullmatch(long) or NUMBER_RE.fullmatch(short):
        return False
    return (
        long.startswith(short) and not long[len(short)].isalnum()
        if len(long) > len(short)
        else False
    )


NUMBER_RE = re.compile(r"\d+(?:\.\d+)*")


def scanned_files(repo: Path) -> List[str]:
    out: List[str] = []
    for g in SCANNED_GLOBS:
        out.extend(
            sorted(p.relative_to(repo).as_posix() for p in repo.glob(g) if p.is_file())
        )
    return out


def _ignored(repo: Path, rel: str) -> bool:
    """A gitignored location (`.claude/tmp/…`, `.claude/plans/…`): a session's file, never tracked.
    A missing path may name a directory, so `rel/` is asked too (a `dir/` pattern needs the slash)."""
    rel = rel.rstrip("/")
    r = subprocess.run(
        ["git", "-C", str(repo), "check-ignore", "--no-index", rel, rel + "/"],
        capture_output=True,
        text=True,
    )
    if r.returncode not in (0, 1):
        raise RuntimeError(f"git check-ignore failed: {r.stderr.strip()}")
    return r.returncode == 0


# A file-name token that stands for a family member: a mask (`YYYY`, `MM`, `NNN`) or one holding a
# digit (a date part, a hex plan-id).
GENERIC_TOKEN_RE = re.compile(
    r"(?<![^.-])(?:([A-Z])\1+|[0-9A-Za-z]*\d[0-9A-Za-z]*)(?![^.-])"
)


def _family(name: str) -> str:
    return GENERIC_TOKEN_RE.sub("#", name)


def _example(repo: Path, rel: str) -> bool:
    """`docs/audits/audit-YYYY-MM-DD.md`, `docs/process/plans/plan-a1b2c3.md`: a missing name with a
    generic token whose directory holds a real file of the same family (the tokens generalised)."""
    p = repo / rel
    fam = _family(p.name)
    if fam == p.name or not p.parent.is_dir():
        return False
    return any(_family(q.name) == fam for q in p.parent.iterdir() if q.is_file())


def slug(heading: str) -> str:
    """GitHub's heading anchor: lowercase, punctuation dropped, spaces to hyphens."""
    s = re.sub(r"[^\w\- ]", "", heading.replace("`", "").strip().lower())
    return s.replace(" ", "-")


class Checker:
    def __init__(self, repo: Path):
        self.repo = repo
        self.docs: Dict[Tuple[str, bool], Optional[Doc]] = {}
        self.skills = {
            p.name
            for p in (repo / ".claude/skills").iterdir()
            if (p / "SKILL.md").is_file()
        }
        self.cited_builtins: Set[str] = set()
        self.out_of_scope: Dict[str, int] = {k: 0 for k in OUT_OF_SCOPE}

    def doc(self, rel: str, with_fences: bool = False) -> Optional[Doc]:
        key = (rel, with_fences)
        if key not in self.docs:
            p = self.repo / rel
            self.docs[key] = (
                Doc(p.read_text(encoding="utf-8"), with_fences=with_fences)
                if p.is_file()
                else None
            )
        return self.docs[key]

    def target_file(self, token: str, citing: str) -> Optional[str]:
        """The repo path a TARGET token names (a /skill, a path, CLAUDE.md, or a relative link)."""
        if token.startswith("/"):
            name = token[1:]
            return f".claude/skills/{name}/SKILL.md" if name in self.skills else None
        if token.endswith(".md") and (self.repo / token).is_file():
            return token
        rel = (Path(citing).parent / token.split("#")[0]).as_posix()
        p = (self.repo / rel).resolve()
        try:
            return (
                p.relative_to(self.repo.resolve()).as_posix() if p.is_file() else None
            )
        except ValueError:
            return None

    def resolve(
        self, target: Optional[str], names: List[str], nums: List[str], *, fences=False
    ) -> Optional[str]:
        """None when the target, each name inside the previous one, and each number resolve."""
        if target is None:
            return "target file not found"
        d = self.doc(target, fences)
        if d is None:
            return f"{target} does not exist"
        # Several sections can share a name (the plan template has a light and a full copy): the
        # reference resolves when any path of nested sections carries every name and number.
        frontier: List[Optional[Section]] = [None]
        for k, n in enumerate(names):
            frontier = [s for sec in frontier for s in d.find(n, sec)]
            if not frontier:
                where = f" inside '{names[k - 1]}'" if k else ""
                return f"no section '{n}' in {target}{where}"
        for num in nums:
            if not any(d.has_number(sec or d.whole, num) for sec in frontier):
                return f"no item/section {num} in {target}" + (
                    f" › {names[-1]}" if names else ""
                )
        return None

    # -- extraction -------------------------------------------------------------------------------

    def check_file(self, rel: str) -> List[Ref]:
        text = (self.repo / rel).read_text(encoding="utf-8")
        flat = text.replace("\n", " ")  # same offsets; references wrap across lines
        refs: List[Ref] = []

        def line_of(pos: int) -> int:
            return text.count("\n", 0, pos) + 1

        def add(kind: str, pos: int, cite: str, error: Optional[str]) -> None:
            refs.append(Ref(kind, rel, line_of(pos), cite.strip(), error))

        claimed: List[
            Tuple[int, int]
        ] = []  # spans a section/contract reference consumed

        def free(a: int, b: int) -> bool:
            return all(b <= s or a >= e for s, e in claimed)

        # paths: PATH_RE's grammar, then what it cannot spell (no listed extension, a directory, the
        # `editors/` root), read only from code, where a path is not prose like "docs/spec work"
        found = [(m.start(1), m.group(1)) for m in PATH_RE.finditer(text)]
        for a, b in code_regions(text):
            for m in CODE_PATH_RE.finditer(text, a, b):
                tok = m.group(1).rstrip(".")
                if text[m.end() : m.end() + 1] in GLOB_CHARS or PATH_RE.fullmatch(tok):
                    continue  # a glob or placeholder (`round-*`, `plans/<id>`), or found above
                found.append((m.start(1), tok))
        for pos, ref in sorted(found):
            ok = (
                (self.repo / ref).exists()
                or _ignored(self.repo, ref)
                or _example(self.repo, ref)
            )
            add(
                "path",
                pos,
                ref,
                None if ok else "no such file (not gitignored, not a family example)",
            )

        # skills
        for line_no, line in enumerate(text.splitlines(), 1):
            for m in SKILL_RE.finditer(line):
                if (
                    line[m.start() - 1 : m.start()] == "`"
                    and line[: m.start()].count("`") % 2 == 0
                ):
                    continue  # the backtick before it closes a code span: `a`/b is not a citation
                name, suffix = m.group(1), m.group(2)
                cite = "/" + name + (suffix or "")
                if suffix:
                    err = f"'{cite}': not a skill citation (write /{suffix[1:]})"
                elif name in self.skills:
                    err = None
                elif name in BUILTIN_SKILLS:
                    self.cited_builtins.add(name)
                    err = None
                else:
                    err = f"no skill '{name}' under .claude/skills/ and not a listed built-in"
                refs.append(Ref("skill", rel, line_no, cite, err))

        # links
        for m in LINK_RE.finditer(text):
            tgt = m.group(1)
            if re.match(r"[a-z]+:", tgt):
                self.out_of_scope["url"] += 1
                continue
            path, _, anchor = tgt.partition("#")
            dest = self.target_file(path, rel) if path else rel
            if dest is None:
                add("link", m.start(1), tgt, "link target does not exist")
                continue
            err = None
            if anchor and dest.endswith(".md"):
                d = self.doc(dest)
                slugs = {
                    slug(d.lines[s.start].lstrip("#").strip())
                    for s in d.sections
                    if s.level <= 6
                }
                if anchor not in slugs:
                    err = f"no heading with anchor #{anchor} in {dest}"
            add("link", m.start(1), tgt, err)

        # section chains: TARGET › Name › Name[, step N]
        for m in re.finditer("›", flat):
            if not free(m.start(), m.end()):
                continue
            base = max(0, m.start() - 200)
            left = flat[base : m.start()]
            t = TARGET_RE.search(left)
            fences = False
            if t:
                token = t.group(1) or t.group(2) or t.group(3)
                target = self.target_file(token, rel)
                start = base + t.start()
            else:
                h = CHAIN_HEAD_RE.search(left)
                if not h:
                    add(
                        "section",
                        m.start(),
                        flat[max(0, m.start() - 30) : m.end() + 30],
                        "unparsed › reference: no target or section name on its left",
                    )
                    claimed.append((m.start(), m.end()))
                    continue
                # The head is the longest tail of the capitalised run that names a template
                # section ("Read Defect Class" -> "Defect Class"); none -> the whole run, red.
                words = h.group(1).split(" ")
                tpl = self.doc(PLAN_TEMPLATE, True)
                k = next(
                    (
                        k
                        for k in range(len(words))
                        if tpl and tpl.find(" ".join(words[k:]))
                    ),
                    0,
                )
                target, fences = PLAN_TEMPLATE, True
                start = base + h.start() + len(" ".join(words[:k])) + (1 if k else 0)
                names = [" ".join(words[k:])]
            names = [] if t else names
            pos = m.start()
            while True:
                seg_start = pos + 1
                rest = flat[seg_start:].lstrip()
                off = len(flat) - len(rest)
                if rest.startswith('"'):
                    close = rest.find('"', 1)
                    seg, seg_len = rest[1:close], close + 1
                else:
                    e = SEG_END_RE.search(rest)
                    seg, seg_len = rest[: e.start()], e.start()
                names.append(seg.strip())
                pos = off + seg_len
                nxt = re.match(r"\s*›", flat[pos:])
                if not nxt:
                    break
                pos += nxt.end() - 1
            nums: List[str] = []
            tail = TAIL_RE.match(flat[pos:])
            if tail:
                nums = [n for n in tail.groups()[1:] if n]
                pos += tail.end()
            claimed.append((start, pos))
            add(
                "section",
                start,
                flat[start:pos],
                self.resolve(target, names, nums, fences=fences),
            )

        # contract §N[–§M]
        for m in CONTRACT_RE.finditer(flat):
            claimed.append((m.start(), m.end()))
            lo, hi = int(m.group(1)), int(m.group(2) or m.group(1))
            d = self.doc(CONTRACT)
            have = {s.names for s in d.sections if s.level == 2} if d else set()
            nums = {n for names in have for n in names if n.isdigit()}
            missing = [str(k) for k in (lo, hi) if str(k) not in nums]
            add(
                "contract",
                m.start(),
                m.group(0),
                f"no '## {', '.join(missing)}.' in {CONTRACT}" if missing else None,
            )

        # TARGET Name §N / TARGET item N
        for m in TARGET_AHEAD_RE.finditer(flat):
            if not free(m.start(), m.end()):
                continue
            target = self.target_file(m.group(1), rel)
            names = [m.group(2)] if m.group(2) else []
            num = m.group(3) or m.group(5)
            claimed.append((m.start(), m.end()))
            add("section", m.start(), m.group(0), self.resolve(target, names, [num]))

        # shorthands
        for rx, target, section in ALIASES:
            for m in rx.finditer(flat):
                if not free(m.start(), m.end()):
                    continue
                claimed.append((m.start(), m.end()))
                nums = [n for n in m.groups()[1:] if n]
                add(
                    "section",
                    m.start(),
                    m.group(0),
                    self.resolve(target, [section], nums),
                )

        # the citing file's own sections: §Name, Name step|item|§ N
        for m in SELF_SECTION_RE.finditer(flat):
            if free(m.start(), m.end()):
                claimed.append((m.start(), m.end()))
                add(
                    "section",
                    m.start(),
                    m.group(0),
                    self.resolve(rel, [m.group(1)], []),
                )
        for m in NAMED_NUM_RE.finditer(flat):
            if not free(m.start(), m.end()):
                continue
            num = m.group(2) or m.group(4)
            claimed.append((m.start(), m.end()))
            add(
                "section", m.start(), m.group(0), self.resolve(rel, [m.group(1)], [num])
            )

        # out of scope, counted
        for m in CHARTER_RE.finditer(flat):
            claimed.append((m.start(), m.end()))
            self.out_of_scope["charter"] += 1
        for m in BARE_NUM_RE.finditer(flat):
            if free(m.start(), m.end()):
                self.out_of_scope["bare-number"] += 1
        return refs

    def run(self) -> List[Ref]:
        refs: List[Ref] = []
        for rel in scanned_files(self.repo):
            refs.extend(self.check_file(rel))
        return refs


def check(repo: Path) -> Tuple[List[Ref], Checker]:
    c = Checker(repo)
    return c.run(), c


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    ap.add_argument(
        "-v", "--verbose", action="store_true", help="list resolved references too"
    )
    a = ap.parse_args(argv)
    refs, c = check(a.repo.resolve())
    broken = [r for r in refs if r.error]
    for r in refs if a.verbose else broken:
        status = "BROKEN" if r.error else "ok"
        print(
            f"{status} {r.kind}: {r.file}:{r.line}: {r.cite}"
            + (f" -- {r.error}" if r.error else "")
        )
    counts = ", ".join(f"{k} {sum(1 for r in refs if r.kind == k)}" for k in KINDS)
    print(
        f"checked: {counts}; out of scope: "
        + ", ".join(f"{k} {v}" for k, v in c.out_of_scope.items())
    )
    print(f"RESULT: {'FAIL' if broken else 'OK'} ({len(broken)} broken of {len(refs)})")
    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(main())
