#!/usr/bin/env python3
"""Every reference the procedure text makes resolves (skills, agents, CLAUDE.md, the process README).

The skills cite each other, CLAUDE.md, the process README and the verification contract by repo path,
by skill name, by section and by markdown link. A renamed heading or a moved file leaves such a
reference pointing at nothing, silently; this check makes it red. The reference kinds, from a census
of the scanned files:

* ``path`` -- a repo-relative file path, extracted with ``check_plan_refs.PATH_RE`` (the plans'
  extractor) over the whole text, fences included (commands name real files), plus what its grammar
  cannot spell (no listed extension, a directory, ``editors/``), read from code spans and fences
  only. A path any ``.gitignore`` rule matches (``git check-ignore``: ``.claude/tmp/``, build
  output, ``lib/``) is never tracked, so not missing; so is a family example (``audit-YYYY-MM-DD.md``,
  ``plan-a1b2c3.md``: see ``_example``). A placeholder or glob (``<plan-id>``, ``*``) is never
  extracted.
* ``skill`` -- ``/name`` at a word start, after ``**`` or opening a code span (any backtick run),
  ending at a word end (a sentence's ``.`` included): a directory under ``.claude/skills/`` or one of
  ``BUILTIN_SKILLS``. ``/name*`` is a glob and ``/name.ext`` a path, not citations; ``/name:x`` is
  red.
* ``section`` -- ``TARGET › Name › Name[, step|item N | §N]``; ``TARGET [Name] §N``,
  ``TARGET [Name] step|item N``, ``TARGET §Name``; ``§Name`` and ``Name step|item|§ N`` for the
  citing file's own sections; the shorthands in ``ALIASES`` (``stage trail item N``, ``README item
  N``, ``Rule N``, ``Dimension N``). A TARGET is a backticked path or ``/skill`` (a bare ``/skill``
  too, before a Name or ``§``), a bare ``CLAUDE.md``, or a markdown link; a ``›`` chain with no target
  names the plan template's sections (``Defect Class › Violated contract``), which live in
  ``/create-plan``'s fenced template. A Name is a heading (any level, outside fences), a bold lead-in
  (``**Commit gate**``, ``- **Layer ladder:**``) or a numbered heading (``### 1.25.``, ``### Step
  2:``). A number N of a section is an ``N.`` subheading inside it, or an ``N.`` list item at its
  list's own indent, outside fences and before its first subheading (a subsection's items are the
  subsection's). With no Name (``TARGET step N``), N must be such an item of some section of the
  target, or a numbered heading in it: the weakest of the forms, since a target file has many lists.
  Every number of a list or range (``Dimensions 2, 3 and 9``, ``§1–§5``, ``steps 8 and 9``) is
  checked.
* ``contract`` -- ``contract §N``, ``Contract §N``, lists and ranges (``§1, §10``, ``§10–§12``),
  ``verification-contract.md §N``: a ``## N.`` heading of ``docs/design/verification-contract.md``.
* ``link`` -- ``[text](target#anchor)`` in prose: the target resolves as GitHub resolves it, against
  the citing file's directory (a leading ``/`` is the repo root), to a file or directory, and the
  anchor is a heading slug of the target (duplicate headings get GitHub's ``-1``, ``-2``).

A fence with an info string (```bash) is code: no skill, link, section or shorthand is read there;
a bare fence is read as prose (here it holds agent prompts). Links are not read in inline code; in a
code span only a ``/name`` that starts it is a skill citation.

Out of scope, counted (``OUT_OF_SCOPE``): a bare ``§N`` / ``step N`` / ``item N`` with no named target
(its target is the enclosing text), or ``Name step|item|§ N`` whose Name is not exactly a section
of the citing file (prose: ``Repeat step 2``); ``charter §N`` (a round charter, not a fixed file);
absolute URLs; a slash-less file name in a code span (``sharpy.sln``). Anything the grammar starts
to parse but cannot finish (a ``›`` with nothing resolvable on its left) is a failure, never a skip.

Missed forms (never red): an own-file citation whose heading was renamed, or that shortens it
(``Cleanup step 4``), counts as bare; a heading cut to a prefix of its citation stays green; a family
example also passes a deleted or renamed member (another digit); reference-style links
(``[r]: path``); ``, N`` / ``and N`` without ``§`` after a contract citation; ``N.M`` where the
target has no ``N.x`` heading is checked as ``N``; a ``/name`` not at the start of its code span.

Can go red on text that is fine (reword the text): a code span split by a hard wrap (the orphan
backtick turns the following prose into code, where paths are read); a ``›`` chain whose target is
outside the repository (``~/.claude/CLAUDE.md › Testing``); a link to ``/`` alone or with ``%20``.

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
from build_tools.check_plan_refs import FENCE_LINE_RE, PATH_EXTS, PATH_RE, heading_name

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
    "bare-number": "a §N / step N / item N with no named target, or one whose Name is no section of "
    "the citing file: it means the enclosing text",
    "charter": "charter §N cites a round charter, a per-round file, not a fixed target",
    "url": "an absolute URL is outside the repository",
    "filename": "a slash-less file name in a code span (`sharpy.sln`) does not say its directory",
}
# Harness built-ins the text cites as /name; each must still be cited (the test checks it).
BUILTIN_SKILLS = {
    "simplify",  # /implement-plan Final Verification step 2
}
# Filesystem roots that open a code span (`/tmp`): never a skill. Each must be a directory under `/`.
NOT_SKILLS = {"tmp"}

# A list or range of numbers: `2, 3, 5, 8 and 9`, `1–5`, `8 and 9`; with `§` on each for sections.
_SEP = r"(?:\s*,\s*(?:and\s+|or\s+)?|\s+(?:and|or)\s+|\s*[–-]\s*)"
NUM = r"\d+(?:\.\d+)*(?:-[a-z]+)?"  # 1.25, 4-design (Dimension 4's design half)
NUMS = NUM + r"(?:" + _SEP + NUM + r"(?![\w-]))*"
SECT_NUMS = r"§\d+(?:\.\d+)?(?:" + _SEP + r"§\d+(?:\.\d+)?)*"
# `§1–99` is a range; a bare `, 40` or `and 15` after it is prose.
CONTRACT_NUMS = SECT_NUMS + r"(?:[–-]\d+(?![\w-]|\.\d))?"
STEP = r"(?:steps?|items?)"
# After a chain, `Step 2` is a citation too.
STEP_ANY = r"(?:[Ss]teps?|[Ii]tems?)"

# Shorthand -> (target file, section name), from the census.
ALIASES = (
    (
        re.compile(r"\bstage trail,? " + STEP + r" (" + NUMS + ")"),
        PROCESS,
        "The stage trail",
    ),
    (
        re.compile(r"\b(?:README|Process) " + STEP + r" (" + NUMS + ")"),
        PROCESS,
        "The stage trail",
    ),
    (
        re.compile(r"\b(?:CLAUDE\.md )?(?:Critical )?Rules? (" + NUMS + ")"),
        "CLAUDE.md",
        "Critical Rules",
    ),
    (
        re.compile(r"\bDimensions? (" + NUMS + ")"),
        ".claude/skills/verify-plan/SKILL.md",
        "Verification Dimensions",
    ),
)

ATX_RE = re.compile(r"^ {0,3}(#{1,6})[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")
LEADIN_RE = re.compile(r"^(?:[-*]|\d+\.)?[ \t]*\*\*([^*]+)\*\*")
ITEM_RE = re.compile(r"^( *)(\d+)\.[ \t]")
NUMBERED_RE = re.compile(r"^(?:Step\s+)?(\d+(?:\.\d+)*)[.:]?(?:\s|$)")
NUMBER_RE = re.compile(r"\d+(?:\.\d+)*")
WS_RE = re.compile(r"\s+")
NAME = r"(?:[A-Z][\w-]*)(?: [A-Z][\w-]*)*"
# A section name in a citation: `Cleanup and Report`, `Core & Stdlib Conventions`.
SECTION_NAME = r"(?:[A-Z][\w-]*)(?: (?:[A-Z&][\w-]*|and))*(?<! and)"

TICK_TARGET = r"`(/[a-z][\w-]*|[\w./-]+\.md)`"
# A bare CLAUDE.md, never the tail of build_tools/CLAUDE.md (that one is its own file).
CLAUDE_TARGET = r"(?<![\w/.-])((?:[\w.-]+/)*CLAUDE\.md)"
# Text may wrap one line; the target may carry a "title".
LINK_TARGET = r"\[(?:[^\]\n]|\n(?!\n))*\]\(\s*([^)\s]+)(?:\s+\"[^\"]*\")?\s*\)"
AHEAD_TARGETS = CLAUDE_TARGET + r"|" + LINK_TARGET
TARGET_RE = re.compile(
    r"(?:" + TICK_TARGET + r"|" + LINK_TARGET + r"|" + CLAUDE_TARGET + r")\s*$"
)
# `/x` Name §N, `/x` §N, `/x` Name step 4, `path` item 6, `/x` §Name; `/x` may be unticked.
TARGET_AHEAD_RE = re.compile(
    r"(?:" + TICK_TARGET + r"|(?<![\w/`.-])(/[a-z][\w-]*)|" + AHEAD_TARGETS + r")"
    r"(?: ("
    + SECTION_NAME
    + r"))? (?:("
    + SECT_NUMS
    + r")|"
    + STEP
    + r" ("
    + NUMS
    + r")|§("
    + NAME
    + r"))"
)
CHAIN_HEAD_RE = re.compile(r"((?:[A-Z][\w-]*)(?: (?:[A-Z&][\w-]*|and))*)\s*$")
SEG_END_RE = re.compile(r"[)(,;:]|\.(?:\s|$)|\s" + STEP_ANY + r" \d|\s§\d|\s›|$")
TAIL_RE = re.compile(r",?\s*(?:" + STEP_ANY + r" (" + NUMS + r")|(" + SECT_NUMS + r"))")
NAMED_NUM_RE = re.compile(
    r"(?<![\w`/.-])("
    + SECTION_NAME
    + r") (?:("
    + SECT_NUMS
    + r")|"
    + STEP
    + r" ("
    + NUMS
    + r"))"
)
SELF_SECTION_RE = re.compile(
    r"(?<![\w§])§(" + NAME.replace("[\\w-]*", "[\\w-]+", 1) + r")"
)
CONTRACT_RE = re.compile(
    r"(?:\b[Cc]ontract|verification-contract\.md`?) (" + CONTRACT_NUMS + r")"
)
CHARTER_RE = re.compile(r"(?i)\bcharter §\d")
BARE_NUM_RE = re.compile(r"§\d+|\b[Ss]teps? \d+|\b[Ii]tems? \d+")
SKILL_RE = re.compile(r"/([a-z][\w-]*)(:[\w-]+)?")
LINK_RE = re.compile(LINK_TARGET)
FILENAME_RE = re.compile(r"[\w.-]*\w\." + PATH_EXTS + r"|\.[a-z]+(?:-[a-z]+)+")
CODE_PATH_RE = re.compile(
    r"(?<![\w/.$}-])((?:src|docs|build_tools|benchmarks|editors|\.claude|\.github)/[\w./-]*)"
)
GLOB_CHARS = ("*", "<", "{", "$", "[")
CODE_SPAN_RE = re.compile(r"(`+)([^`].*?)\1", re.S)


def numbers(text: str) -> List[str]:
    """`2, 3 and 9` -> 2 3 9; `§10–§12` -> 10 11 12; `4-design` -> 4; `1.25` stays whole."""
    text = re.sub(r"(\d)-[a-z]+", r"\1", text.replace("§", ""))
    out: List[str] = []
    for part in re.split(r"\s*,\s*(?:and\s+|or\s+)?|\s+(?:and|or)\s+", text.strip()):
        m = re.fullmatch(r"(\d+)\s*[–-]\s*(\d+)", part.strip())
        if m and 0 <= int(m.group(2)) - int(m.group(1)) < 50:
            out.extend(str(k) for k in range(int(m.group(1)), int(m.group(2)) + 1))
        elif m:
            out.extend([m.group(1), m.group(2)])
        else:
            out.append(part.strip())
    return out


def fence_lines(text: str) -> Iterator[Tuple[int, str]]:
    """(line index, kind) for each line: "" prose, "delim" a fence line, "code" the body of a fence
    with an info string (```bash), "text" the body of a bare fence (a prompt or template)."""
    fence = None
    for i, line in enumerate(text.splitlines()):
        m = FENCE_LINE_RE.match(line)
        if fence is None:
            if m and not (m.group(1)[0] == "`" and "`" in m.group(2)):
                fence = (m.group(1)[0], len(m.group(1)), bool(m.group(2).strip()))
                yield i, "delim"
            else:
                yield i, ""
        elif (
            m
            and m.group(1)[0] == fence[0]
            and len(m.group(1)) >= fence[1]
            and not m.group(2).strip()
        ):
            fence = None
            yield i, "delim"
        else:
            yield i, "code" if fence[2] else "text"


def _blank(s: str) -> str:
    return re.sub(r"[^\n]", " ", s)


def prose_of(text: str, *, spans: bool = False) -> str:
    """`text` with code fences (and, with `spans`, inline code) blanked, offsets kept. A fence with
    an info string is code; a bare fence's body stays: here it is an agent prompt or a template the
    reader follows (`/ci-gate` › Control runs inside /verify-implementation's auditor brief)."""
    lines = text.split("\n")
    fenced = {i for i, f in fence_lines(text) if f in ("delim", "code")}
    out = [_blank(l) if i in fenced else l for i, l in enumerate(lines)]
    if (
        spans
    ):  # inline code, one line at a time (a stray backtick cannot pair across lines)
        out = [CODE_SPAN_RE.sub(lambda m: _blank(m.group(0)), l) for l in out]
    return "\n".join(out)


def code_regions(text: str) -> Iterator[Tuple[int, int]]:
    """(start, end) offsets of fenced block bodies and of inline code spans outside them."""
    pos, fenced = 0, dict(fence_lines(text))
    for i, line in enumerate(text.splitlines(keepends=True)):
        if fenced.get(i):
            yield pos, pos + len(line)
        else:
            for m in CODE_SPAN_RE.finditer(line):
                yield pos + m.start(2), pos + m.end(2)
        pos += len(line)


def code_spans(prose: str) -> List[Tuple[int, int]]:
    """(start, end) of each inline code span's content, one line at a time."""
    out, pos = [], 0
    for line in prose.split("\n"):
        out.extend(
            (pos + m.start(2), pos + m.end(2)) for m in CODE_SPAN_RE.finditer(line)
        )
        pos += len(line) + 1
    return out


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
    level: int  # 1-6 heading, 7 bold lead-in, 0 the whole file


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


def slug(heading: str) -> str:
    """GitHub's heading anchor: lowercase, punctuation dropped, spaces to hyphens."""
    s = re.sub(r"[^\w\- ]", "", heading.replace("`", "").strip().lower())
    return s.replace(" ", "-")


class Doc:
    """A target file's sections: headings, bold lead-ins and the numbers inside each."""

    def __init__(self, text: str, *, with_fences: bool = False):
        self.lines = text.splitlines()
        kinds = dict(fence_lines(text))
        self.fenced = {i for i, f in kinds.items() if f}
        # A number in a bare fence counts (an agent prompt's numbered steps are cited); one in a
        # fence with an info string (```bash) is code, never a step.
        self.code_lines = {i for i, f in kinds.items() if f in ("delim", "code")}
        heads: List[Tuple[int, int, str]] = []  # (line, level, text)
        leadins: List[Tuple[int, str]] = []
        for i, line in enumerate(self.lines):
            if i in self.fenced and (not with_fences or FENCE_LINE_RE.match(line)):
                continue
            h = ATX_RE.match(line)
            if h:
                heads.append((i, len(h.group(1)), h.group(2).strip()))
                continue
            b = LEADIN_RE.match(line.strip())
            if b:
                leadins.append((i, b.group(1)))
        # The plan template is a markdown fence: its body is the document.
        if with_fences:
            self.fenced = self.code_lines = {
                i for i, f in kinds.items() if f == "delim"
            }
        self.sections: List[Section] = []
        n = len(self.lines)
        for k, (i, level, text) in enumerate(heads):
            end = next((j for j, lv, _ in heads[k + 1 :] if lv <= level), n)
            self.sections.append(Section(_names(text), i, end, level))
        for i, text in leadins:
            end = next((j for j, _, _ in heads if j > i), n)
            self.sections.append(Section(_names(text), i, end, 7))
        self.whole = Section((), 0, n, 0)
        self.slugs: Set[str] = set()
        seen: Dict[str, int] = {}
        for i, _, text in heads:
            s = slug(text)
            self.slugs.add(f"{s}-{seen[s]}" if s in seen else s)
            seen[s] = seen.get(s, 0) + 1

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
        """`num` is a numbered subheading of `sec`, or an item of `sec`'s own list: at the list's
        indent, outside fences, before `sec`'s first subheading."""
        items: List[Tuple[int, str]] = []
        in_child = False
        for i in range(sec.start + (1 if sec.level else 0), sec.end):
            if i in self.code_lines:
                continue
            line = self.lines[i]
            h = None if i in self.fenced else ATX_RE.match(line)  # `# Role` in a prompt
            if h:
                m = NUMBERED_RE.match(h.group(2))
                if m and m.group(1) == num:
                    return True
                in_child = True
                continue
            m = ITEM_RE.match(line)
            if m and not in_child:
                items.append((len(m.group(1)), m.group(2)))
        if not items:
            return False
        base = min(ind for ind, _ in items)
        return any(n == num for ind, n in items if ind == base)


def scanned_files(repo: Path) -> List[str]:
    out: List[str] = []
    for g in SCANNED_GLOBS:
        out.extend(
            sorted(p.relative_to(repo).as_posix() for p in repo.glob(g) if p.is_file())
        )
    return out


def _ignored(repo: Path, rel: str) -> bool:
    """A location any `.gitignore` rule matches (`.claude/tmp/…`, build output): never tracked.
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


def _example(repo: Path, rel: str) -> Optional[str]:
    """`docs/audits/audit-YYYY-MM-DD.md`, `docs/process/plans/plan-a1b2c3.md`: a missing name with a
    generic token whose directory holds a real file of the same family (the tokens generalised).
    Returns that file."""
    p = repo / rel
    fam = _family(p.name)
    if fam == p.name or not p.parent.is_dir():
        return None
    for q in sorted(p.parent.iterdir()):
        if q.is_file() and _family(q.name) == fam:
            return q.relative_to(repo).as_posix()
    return None


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
        # Every repo file or directory a verdict depended on (the workflow-relevance test reads it).
        self.reads: Set[str] = set()

    def doc(self, rel: str, with_fences: bool = False) -> Optional[Doc]:
        key = (rel, with_fences)
        if key not in self.docs:
            p = self.repo / rel
            self.docs[key] = (
                Doc(p.read_text(encoding="utf-8"), with_fences=with_fences)
                if p.is_file()
                else None
            )
        if self.docs[key] is not None:
            self.reads.add(rel)
        return self.docs[key]

    def target_file(self, token: str) -> Optional[str]:
        """The file a backticked TARGET names: a /skill's SKILL.md, or a repo-root-relative path."""
        if token.startswith("/"):
            name = token[1:]
            return f".claude/skills/{name}/SKILL.md" if name in self.skills else None
        return token if (self.repo / token).is_file() else None

    def link_dest(self, path: str, citing: str) -> Optional[str]:
        """A link target as GitHub resolves it: against the citing file's directory, or the repo root
        for a leading `/`; a file or a directory inside the repository."""
        base = Path(path[1:]) if path.startswith("/") else Path(citing).parent / path
        p = (self.repo / base).resolve()
        try:
            rel = p.relative_to(self.repo.resolve()).as_posix()
        except ValueError:
            return None
        return rel if p.exists() and rel != "." else None

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
        frontier: List[Section] = [d.whole] + (
            [] if names else [s for s in d.sections if s.level <= 6]
        )
        for k, n in enumerate(names):
            frontier = [
                s for sec in frontier for s in d.find(n, sec if sec.level else None)
            ]
            if not frontier:
                where = f" inside '{names[k - 1]}'" if k else ""
                return f"no section '{n}' in {target}{where}"
        # `step 4.2` is item 2 of step 4, checked as 4, unless the target numbers headings 4.x.
        heads = [n for s in d.sections for n in s.names if NUMBER_RE.fullmatch(n)]
        tops = {h.split(".")[0] for h in heads if "." in h}
        nums = [n if n.split(".")[0] in tops else n.split(".")[0] for n in nums]
        missing = [
            num for num in nums if not any(d.has_number(sec, num) for sec in frontier)
        ]
        if missing:
            return f"no item/section {', '.join(missing)} in {target}" + (
                f" › {names[-1]}" if names else ""
            )
        return None

    def own_section(self, rel: str, run: str) -> Optional[str]:
        """The longest tail of a capitalised run that names a section of `rel` ("Then Cleanup" ->
        "Cleanup"), or None: then the run is prose, not a citation."""
        d = self.doc(rel)
        words = run.split(" ")
        for k in range(len(words)):
            name = norm(" ".join(words[k:]))
            if d is not None and any(name in s.names for s in d.sections):
                return " ".join(words[k:])  # exactly: "Do" is not "**Do not**"
        return None

    # -- extraction -------------------------------------------------------------------------------

    def check_file(self, rel: str) -> List[Ref]:
        text = (self.repo / rel).read_text(encoding="utf-8")
        self.reads.add(rel)
        prose = prose_of(text)
        flat = prose.replace("\n", " ")  # same offsets; references wrap across lines
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

        # paths: PATH_RE's grammar over the whole text (commands in fences name real files), then
        # what it cannot spell (no listed extension, a directory, the `editors/` root), read only
        # from code, where a path is not prose like "docs/spec work"
        found = [(m.start(1), m.group(1)) for m in PATH_RE.finditer(text)]
        for a, b in code_regions(text):
            for m in CODE_PATH_RE.finditer(text, a, b):
                tok = m.group(1).rstrip(".")
                if text[m.end() : m.end() + 1] in GLOB_CHARS or PATH_RE.fullmatch(tok):
                    continue  # a glob or placeholder (`round-*`, `plans/<id>`), or found above
                found.append((m.start(1), tok))
        for pos, ref in sorted(found):
            err = None
            if (self.repo / ref).exists():
                self.reads.add(ref.rstrip("/"))
            elif _ignored(self.repo, ref):
                self.reads.add(".gitignore")
            else:
                example = _example(self.repo, ref)
                if example:
                    self.reads.add(example)
                else:
                    err = "no such file (not gitignored, not a family example)"
            add("path", pos, ref, err)

        # skills: prose and inline code, never fenced code
        spans = code_spans(prose)
        links = [m.span(1) for m in LINK_RE.finditer(prose_of(text, spans=True))]
        for m in SKILL_RE.finditer(prose):
            a, b = m.start(), m.end()
            before, after = prose[a - 1 : a] if a else "", prose[b : b + 1]
            if any(s < a < e for s, e in spans) or any(s <= a < e for s, e in links):
                continue  # mid-span (`msbuild /nologo`) or a link target ([d](/docs))
            if m.group(1) in NOT_SKILLS:
                continue
            if before == "`":
                if not any(s == a for s, _ in spans):
                    continue  # the backtick closes a span: `a`/b is not a citation
            elif before not in ("", " ", "\n", "\t", "(", "[", '"', "'", "*"):
                continue  # inside a word or a path: a/b, src/x, https://x.org/push
            if after in ("/", "-") or after.isalnum() or after == "_":
                continue
            if after == "." and prose[b + 1 : b + 2].isalnum():
                continue  # /name.ext is a path
            if after == "*" and not (
                prose[a - 2 : a] == "**" and prose[b : b + 2] == "**"
            ):
                continue  # /spy-* is a glob; **/name** is bold
            name, suffix = m.group(1), m.group(2)
            cite = "/" + name + (suffix or "")
            if suffix:
                err = f"'{cite}': not a skill citation (write /{suffix[1:]})"
            elif name in self.skills:
                err = None
                self.reads.add(f".claude/skills/{name}/SKILL.md")
            elif name in BUILTIN_SKILLS:
                self.cited_builtins.add(name)
                err = None
            else:
                err = (
                    f"no skill '{name}' under .claude/skills/ and not a listed built-in"
                )
            add("skill", a, cite, err)

        # links: prose only, inline code blanked (`a[i](x)` is code)
        for m in LINK_RE.finditer(prose_of(text, spans=True)):
            tgt = m.group(1)
            if re.match(r"[a-z]+:", tgt):
                self.out_of_scope["url"] += 1
                continue
            path, _, anchor = tgt.partition("#")
            dest = self.link_dest(path, rel) if path else rel
            if dest is None:
                add(
                    "link",
                    m.start(1),
                    tgt,
                    "link target does not exist (GitHub resolves it "
                    "against the citing file's directory)",
                )
                continue
            self.reads.add(dest)
            err = None
            if anchor and dest.endswith(".md"):
                d = self.doc(dest)
                if d is None or anchor not in d.slugs:
                    err = f"no heading with anchor #{anchor} in {dest}"
            add("link", m.start(1), tgt, err)

        # section chains: TARGET › Name › Name[, step N | §N]
        for m in re.finditer("›", flat):
            if not free(m.start(), m.end()):
                continue
            base = max(0, m.start() - 200)
            left = flat[base : m.start()]
            t = TARGET_RE.search(left)
            fences = False
            names: List[str] = []
            if t:
                if t.group(2):
                    target = self.link_dest(t.group(2).split("#")[0], rel)
                else:
                    target = self.target_file(t.group(1) or t.group(3))
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
            pos = m.start()
            while True:
                rest = flat[pos + 1 :].lstrip()
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
                nums = numbers(tail.group(1) or tail.group(2))
                pos += tail.end()
            claimed.append((start, pos))
            add(
                "section",
                start,
                flat[start:pos],
                self.resolve(target, names, nums, fences=fences),
            )

        # contract §N, lists and ranges (whole text: `contract §N` is unambiguous even in a fence)
        text_flat = text.replace("\n", " ")
        for m in CONTRACT_RE.finditer(text_flat):
            claimed.append((m.start(), m.end()))
            d = self.doc(CONTRACT)
            have = {
                n for s in (d.sections if d else []) if s.level == 2 for n in s.names
            }
            nums = numbers(re.sub(r"(\d+)\.\d+", r"\1", m.group(1)))  # §2.1: section 2
            missing = [k for k in nums if k not in have]
            add(
                "contract",
                m.start(),
                m.group(0),
                f"no '## {', '.join(missing)}.' in {CONTRACT}" if missing else None,
            )

        for m in CHARTER_RE.finditer(text_flat):  # before any own-section reading
            claimed.append((m.start(), m.end()))
            self.out_of_scope["charter"] += 1

        # TARGET [Name] §N / TARGET [Name] step|item N / TARGET §Name
        for m in TARGET_AHEAD_RE.finditer(flat):
            if not free(m.start(), m.end()):
                continue
            if m.group(4):
                target = self.link_dest(m.group(4).split("#")[0], rel)
            else:
                target = self.target_file(m.group(1) or m.group(2) or m.group(3))
            names = [n for n in (m.group(5), m.group(8)) if n]
            nums = numbers(m.group(6) or m.group(7) or "") if not m.group(8) else []
            claimed.append((m.start(), m.end()))
            add("section", m.start(), m.group(0), self.resolve(target, names, nums))

        # shorthands
        for rx, target, section in ALIASES:
            for m in rx.finditer(flat):
                if not free(m.start(), m.end()):
                    continue
                claimed.append((m.start(), m.end()))
                add(
                    "section",
                    m.start(),
                    m.group(0),
                    self.resolve(target, [section], numbers(m.group(1))),
                )

        # the citing file's own sections: §Name, and Name step|item|§ N where Name is a section
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
            name = self.own_section(rel, m.group(1))
            if name is None:
                continue  # prose ("Repeat step 2"): counted below as a bare number
            nums = numbers(m.group(2) or m.group(3))
            claimed.append((m.start(), m.end()))
            add(
                "section",
                m.end(1) - len(name),
                flat[m.end(1) - len(name) : m.end()],
                self.resolve(rel, [name], nums),
            )

        # out of scope, counted
        for a, b in spans:
            self.out_of_scope["filename"] += sum(
                1 for tok in prose[a:b].split() if FILENAME_RE.fullmatch(tok)
            )
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
