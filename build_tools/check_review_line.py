#!/usr/bin/env python3
"""Check: a pull request into `dev` that changes code carries a current independent-review line.

The rule (docs/process/README.md item 6; `/implement-plan` > Cleanup and Report step 4): a pull
request into `dev` whose diff touches a code path (CODE_PREFIXES) merges only with a body line

    Independent review @ <sha>: <verdict>; <n> applied, <m> declined (reasons below); fixes <sha>..<head> read by the lead

whose verdict is not `do not merge`, and which is current: no code other than the review's fixes or
a `dev` merge landed after it. A second (third, ...) review's `@ <sha>: <verdict>` follows on the
same line; the last one written is the one that counts. Nothing enforced this before: PRs #2615
(src/) and #2612 (build_tools/) merged on 2026-11-11 with no line. This script makes a missing or
malformed line visible; it is advisory unless the owner makes its check required.

Which lines count. A line counts when, after at most three spaces, an optional list marker, bold or
a backtick, it starts with `Independent review @`. Recognised and never read, because they quote the
rule or an example rather than state a review: fenced code (``` or ~~~, an info string allowed on
the opening fence; a fence closes only on a bare fence line), HTML comments outside inline code
spans (a `<!--` inside backticks is text), blockquotes (`>`), and any line indented four spaces or a
tab (indented code, but also a nested list item, which therefore never counts). As GitHub renders
it, a comment closed on its own line is removed wherever it sits, but an unclosed `<!--` hides the
lines after it only when it STARTS a line (after at most three spaces): mid-line it is literal
text, and in an indented or quoted line it is never a comment. A line that starts with `<!--`
hides what follows even inside a paragraph, e.g. after an inline code span left open on the line
before. NOT recognised: a
line opening with a zero-width or other invisible character, a heading, a table cell or link text
(none of these starts with the head, so none counts), and lazy paragraph continuations. A counted
line parses only when its FIRST clause is `Independent review @ <hex sha>[ (note)]: <verdict>` and
every later `review @` on it is a `second|third|fourth|fifth|final review @ <hex sha>[ (note)]:
<verdict>`; a verdict is `merge`, `merge after fixes` or `do not merge` (bold allowed), followed by
punctuation, a dash or the end of the line. So `Independent review @ head pending; ... second
review @ X: merge` is malformed, not passed on the later clause.

Verdicts, in the order they are decided (the sha checks come before `do not merge`, so a rebased
branch gets the rebase hint; every one of them fails the check):

  not required   the base is not `dev` (a promotion into `mainline`, a stacked PR), or no changed
                 path is a code path (a sync of `mainline` into `dev` changes none). README item 6
                 names no exemption for a head named `dev`; the only bot exemption (owner,
                 2026-11-17) is `--author dependabot[bot]` with every changed code path a package
                 manifest under `src/` (`*.csproj`, `Directory.Packages.props`,
                 `packages.lock.json`) modified in place, and every hunk of its diff a version
                 change (version_only): as many removed as added lines, the i-th removed paired
                 with the i-th added, each pair identical but for a different version value, in
                 the `Version` attribute of a `<PackageReference>`/`<PackageVersion>` tag that
                 parses as a strict attribute sequence (a concrete version on both sides, never a
                 float or range) or a lock-file `resolved`, `requested`, `contentHash` or
                 `"<package>": "<version>"` value. The diff is read as bytes and decoded as UTF-8
                 with no newline translation. Anything else (an added or binary file, no patch,
                 a moved line, a `<Target>`, a property, a new package, a line holding a line
                 separator other than `\n`, a diff that is not UTF-8) needs the line;
  missing        no line counts;
  malformed      the last counted line does not parse;
  sha not an ancestor of head
                 a reviewed sha, or a `fixes <a>..<b>` range's end, is not the head or one of its
                 ancestors, or this clone does not have it;
  sha on the base
                 a reviewed sha is already reachable from the base: it reviewed `dev`, not the PR;
  do not merge   any review says so that no LATER-written review of a commit strictly descending
                 from the one it judged clears (a later review of the same or an older commit does
                 not clear it; each do-not-merge must be cleared on its own). Reviews are read in
                 the order written, so an older do-not-merge written on a later line still blocks;
  bad fix range  the range's start is not the same commit as one of the line's reviewed shas, or
                 is not an ancestor of the range's end;
  stale          a commit after the covered point (the last review's sha, or the fix range's end
                 when that descends from it), not reachable from the base, touches a code path. A
                 merge commit counts by its remerge diff (`git log --remerge-diff`, git >= 2.36):
                 a clean `dev` merge changes nothing, code written while resolving it does, and so
                 does ANY merge whose conflict touched a code path, even one resolved as `dev`'s
                 side (the remerge diff shows the resolution against the conflict markers). A merge
                 of more than two parents (octopus) is always stale: git computes no remerge diff
                 for it;
  ok             otherwise.

What this does NOT judge, deliberately: that the reviewer was independent or that the fix range was
read (a line is text anyone can write); the `<n> applied, <m> declined` counts (real lines vary:
`0 blocking, 3 optional notes`, `no new findings`); whether a fix round needed a second review (an
added abstraction or guard, a public surface, the 100-line threshold); whether the commits inside
the fix range are only the review's fixes; and a history rewrite with an empty diff (CLAUDE.md: the
line stays current when `git diff <old> <new>` is empty), because the old sha is usually not
fetchable: such a PR reads as `sha not an ancestor of head` until the line's shas are updated.

Usage:
    python3 build_tools/check_review_line.py --head <sha> [--base <rev>] [--base-ref dev]
        [--body-env PR_BODY] [--draft] [--author <login>]

--base defaults to `origin/<base-ref>`: the base branch's tip in this clone, not the event payload's
`base.sha`, which is frozen when the PR opens (#2626 was opened on eabb480c6 and merged onto
978b3d47f). The body is read from the named environment variable (default PR_BODY), never from argv,
so a workflow never puts PR text on a command line. Changed paths are `git diff --no-renames
--name-only base...head`, so a code file moved out of a code path still counts. Exit 0 for `ok` /
`not required` (and for any verdict on a draft, which prints a warning), 1 for any other verdict,
2 if git fails.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from dataclasses import dataclass, field
from typing import Callable, List, Optional, Sequence

# README item 6: "It changes code when its diff touches ...".
CODE_PREFIXES = (
    "src/",
    "build_tools/",
    ".claude/scripts/",
    ".claude/hooks/",
    ".github/",
    "docs/language_specification/",
)
RULE_BASE = "dev"
# Owner ruling (2026-11-17): a dependency bump opened by Dependabot needs no review line. The author
# login is set by GitHub and cannot be chosen by a PR (a branch name can), and the exemption holds
# only while every changed code path is a package manifest whose diff changes versions and nothing
# else (version_only), so a bot PR that also edits source, scripts, workflows or MSBuild logic still
# needs the line.
DEPENDENCY_BOT = "dependabot[bot]"
MANIFEST_NAMES = ("Directory.Packages.props", "packages.lock.json")
MANIFEST_SUFFIXES = (".csproj",)
# A version change in an MSBuild manifest: one PackageReference/PackageVersion tag on the line, in
# attribute form (`/>`, or `>` opening an element whose unchanged children stay context), its
# attributes a strict `name="value"` / `name='value'` sequence, so a `Version="..."` inside another
# attribute's value is text, not the attribute.
VERSION_TAG = re.compile(
    r"\s*<(?:PackageReference|PackageVersion)"
    r"((?:\s+[\w.:-]+\s*=\s*(?:\"[^\"]*\"|'[^']*'))*)\s*/?>\s*"
)
TAG_ATTR = re.compile(r"\s+([\w.:-]+)\s*=\s*(?:\"([^\"]*)\"|'([^']*)')")
# A lock-file line `"<key>": "<value>",`; `resolved`/`requested`/`contentHash` take any value, any
# other key (a dependency's name) only a version or version range on both sides.
LOCK_LINE = re.compile(r'\s*"([^"]+)": "([^"]*)",?\s*')
LOCK_ANY_VALUE = ("resolved", "requested", "contentHash")
# A version or NuGet range (`1.2.3-rc.1`, `10.*`, `[1.0, )`): never `$(...)`, which MSBuild evaluates.
VERSION = re.compile(r"[\[(]?\d[0-9A-Za-z.*+-]*(?:\s*,\s*[0-9A-Za-z.*+-]*)?[\])]?")
# A concrete version (`10.0.15`, `1.2.3-rc.1+build`), the only kind a manifest tag may change to or
# from: a float (`1.*`) or range (`[1.0,)`) lets later restores pick unreviewed versions.
CONCRETE_VERSION = re.compile(r"\d+(?:\.\d+)*(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?")
# Characters Python or other readers may take as a line end inside one git diff line (a `\r` is
# allowed only as the line's last character, a CRLF file).
LINE_BREAKS = re.compile("[\r\x0b\x0c\x1c\x1d\x1e\x85\u2028\u2029]")

NOT_REQUIRED = "not required"
OK = "ok"
MISSING = "missing"
MALFORMED = "malformed"
DO_NOT_MERGE = "do not merge"
NOT_ANCESTOR = "sha not an ancestor of head"
ON_BASE = "sha on the base"
BAD_RANGE = "bad fix range"
STALE = "stale"
PASSING = (NOT_REQUIRED, OK)

REWRITE_HINT = (
    " (if the branch was rewritten with an empty diff, update the shas in the line)"
)

# Markup a line may open with: up to three spaces, a list marker, bold/italic marks, a backtick.
# Never `>`: a blockquote quotes a line, it does not state a review.
PREFIX = r"^ {0,3}(?:[-*+]\s+)?[*_`]*"
LINE_HEAD = re.compile(PREFIX + r"independent review @", re.IGNORECASE)
SHA = r"([0-9a-fA-F]{7,40})\b"
NOTE = r"(?:\s*\([^)]*\))?"
VERDICT = r"[*_]{0,2}(do not merge|merge after fixes|merge)[*_]{0,2}(?=\s*(?:[;,.(`]|—|–|-(?=\s)|$))"
FIRST = re.compile(
    PREFIX + r"independent\s+review\s+@\s+" + SHA + NOTE + r"\s*:\s*" + VERDICT,
    re.IGNORECASE,
)
LATER = re.compile(
    r"\b(?:second|third|fourth|fifth|final)\s+review\s+@\s+"
    + SHA
    + NOTE
    + r"\s*:\s*"
    + VERDICT,
    re.IGNORECASE,
)
ANY_REVIEW = re.compile(r"\breview\s+@", re.IGNORECASE)
# `fixes <a>..<b> read by the lead`, `the second review read the fix commit <a>..<b> itself`.
FIX_RANGE = re.compile(
    r"\bfix(?:es|\s+commits?)?\s+([0-9a-fA-F]{7,40})\.\.([0-9a-fA-F]{7,40})\b",
    re.IGNORECASE,
)
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})")
FENCE_CLOSE = re.compile(r"^ {0,3}(`{3,}|~{3,})\s*$")
CODE_SPAN = re.compile(r"`+")
REBASE_HINT = "; if the branch was rebased with changes, the review must be repeated at the new head"


def is_code_path(path: str) -> bool:
    return path.startswith(CODE_PREFIXES)


def is_manifest_path(path: str) -> bool:
    # Under src/ only: a `.csproj` under `.github/` or `build_tools/` is not a package manifest.
    return path.startswith("src/") and (
        path.endswith(MANIFEST_SUFFIXES) or path.rsplit("/", 1)[-1] in MANIFEST_NAMES
    )


def _version_key(path: str, line: str) -> Optional[tuple]:
    """(the line with its version value blanked, the version value(s)) for a changed manifest line
    that may be one side of a version change; None when it cannot be."""
    if LINE_BREAKS.search(line[:-1] if line.endswith("\r") else line):
        return None
    if path.endswith("packages.lock.json"):
        m = LOCK_LINE.fullmatch(line)
        if not m or (
            m.group(1) not in LOCK_ANY_VALUE and not VERSION.fullmatch(m.group(2))
        ):
            return None
        return line[: m.start(2)] + line[m.end(2) :], (m.group(2),)
    m = VERSION_TAG.fullmatch(line)
    if not m:
        return None
    attrs = [a for a in TAG_ATTR.finditer(line, m.start(1), m.end(1)) if a.group(1) == "Version"]
    if len(attrs) != 1:
        return None
    group = 2 if attrs[0].group(2) is not None else 3
    value = attrs[0].group(group)
    if not CONCRETE_VERSION.fullmatch(value):
        return None
    return line[: attrs[0].start(group)] + line[attrs[0].end(group) :], (value,)


NO_NEWLINE = "\\ No newline at end of file"


def version_only(diff, paths: Sequence[str]) -> Optional[str]:
    """None when `diff` (`git diff -U0` of `paths`, bytes or str) changes nothing but versions in
    every one of `paths`, each modified in place; else why not. No patch for a path is a reason, and
    so is a diff that is not UTF-8."""
    if isinstance(diff, bytes):
        try:
            diff = diff.decode("utf-8")
        except UnicodeDecodeError as exc:
            return f"the manifest diff is not UTF-8 ({exc.reason} at byte {exc.start})"
    changes: dict = {}  # path -> [(removed lines, added lines)], one entry per hunk
    path = None
    in_hunk = False
    # git ends a diff line at "\n" only (str.splitlines also splits on \r, U+2028, ...).
    lines = diff.split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    for line in lines:
        if line.startswith("diff --git "):
            rest = line[len("diff --git a/") :]
            half = (len(rest) - 3) // 2
            path = rest[:half] if rest == rest[:half] + " b/" + rest[:half] else None
            if path is None or path in changes:
                return f"unreadable diff header: {line[:120]}"
            changes[path], in_hunk = [], False
        elif path is None:
            return f"diff text before a header: {line[:120]}"
        elif line.startswith("@@"):
            in_hunk = True
            changes[path].append(([], []))
        elif not in_hunk:
            # Extended headers: only the index line and the two file names are a modification.
            if not line.startswith(("index ", "--- a/", "+++ b/")):
                return f"{path}: {line.strip()[:120]}"
        elif line.startswith("-"):
            changes[path][-1][0].append(line[1:])
        elif line.startswith("+"):
            changes[path][-1][1].append(line[1:])
        elif line != NO_NEWLINE:
            return f"{path}: unexpected diff line: {line[:120]}"
    for p in paths:
        if p not in changes or not any(r or a for r, a in changes[p]):
            return f"{p}: no patch"
    for p, hunks in changes.items():
        if p not in paths:
            return f"{p}: not a changed manifest"
        for removed, added in hunks:
            for text in removed + added:
                if _version_key(p, text) is None:
                    return f"{p}: not a version change: {text.strip()[:120]}"
            if len(removed) != len(added):
                return (
                    f"{p}: a hunk removes {len(removed)} and adds {len(added)} line(s)"
                )
            # Paired by position: a line that moves (another ItemGroup, a Condition) is no pair.
            for old, new in zip(removed, added):
                (old_rest, old_v), (new_rest, new_v) = (
                    _version_key(p, old),
                    _version_key(p, new),
                )
                if old_rest != new_rest or old_v == new_v:
                    return f"{p}: not a version change: {old.strip()[:60]} -> {new.strip()[:60]}"
    return None


def strip_comments(line: str, in_comment: bool) -> tuple:
    """`line` without HTML comments, leaving inline code spans (a backtick run up to the next run of
    the same length on the line) untouched; and whether a comment is still open at its end. A
    comment closed on the line is removed; an unclosed `<!--` opens one only at the start of the
    line (after at most three spaces), and is literal text anywhere else."""
    out: List[str] = []
    i = 0
    while i < len(line):
        if in_comment:
            close = line.find("-->", i)
            if close < 0:
                return "".join(out), True
            i, in_comment = close + 3, False
            continue
        if line.startswith("<!--", i):
            close = line.find("-->", i + 4)
            if close >= 0:
                i = close + 3
                continue
            if not line[:i].strip(" ") and i <= 3:
                return "".join(out), True
            out.append("<!--")
            i += 4
            continue
        tick = CODE_SPAN.match(line, i)
        if tick:
            close = line.find(tick.group(0), tick.end())
            stop = close + len(tick.group(0)) if close >= 0 else tick.end()
            out.append(line[i:stop])
            i = stop
            continue
        out.append(line[i])
        i += 1
    return "".join(out), in_comment


def visible_lines(body: str) -> List[str]:
    """The body's lines minus what the module docstring says is never read: fenced code, HTML
    comments outside inline code spans, and lines indented four spaces or a tab. Blockquotes are
    left to the line head, which never accepts `>`. Lines split on newlines only (not U+2028), CRLF
    accepted."""
    out: List[str] = []
    fence: Optional[str] = None
    in_comment = False
    for line in (body or "").replace("\r\n", "\n").replace("\r", "\n").split("\n"):
        if fence is not None:
            m = FENCE_CLOSE.match(line)
            if m and m.group(1)[0] == fence[0] and len(m.group(1)) >= len(fence):
                fence = None
            continue
        if not in_comment:
            m = FENCE.match(line)
            if m:
                fence = m.group(1)
                continue
        # An indented (4+ spaces, tab) or quoted (`>`) line can never open a comment that carries
        # over: strip_comments opens one only when `<!--` starts the line after at most 3 spaces.
        line, in_comment = strip_comments(line, in_comment)
        out.append(line)
    return out


@dataclass(frozen=True)
class Review:
    sha: str
    verdict: str


@dataclass
class ParsedLine:
    lines: List[str] = field(default_factory=list)
    reviews: List[Review] = field(default_factory=list)
    fix_range: Optional[tuple] = None
    last_line_parses: bool = False


def parse_line(line: str) -> Optional[List[Review]]:
    """The reviews a counted line states, or None when it does not parse."""
    first = FIRST.match(line)
    if not first:
        return None
    reviews = [Review(first.group(1).lower(), first.group(2).lower())]
    reviews += [
        Review(m.group(1).lower(), m.group(2).lower())
        for m in LATER.finditer(line, first.end())
    ]
    if len(reviews) != len(ANY_REVIEW.findall(line)):
        return None
    return reviews


def parse_body(body: str) -> ParsedLine:
    """Every review and the last fix range on every counted line."""
    parsed = ParsedLine()
    for line in visible_lines(body):
        if not LINE_HEAD.match(line):
            continue
        parsed.lines.append(line.strip())
        reviews = parse_line(line)
        parsed.last_line_parses = reviews is not None
        if reviews:
            parsed.reviews += reviews
            for m in FIX_RANGE.finditer(line):
                parsed.fix_range = (m.group(1).lower(), m.group(2).lower())
    return parsed


@dataclass(frozen=True)
class Result:
    verdict: str
    detail: str

    @property
    def passing(self) -> bool:
        return self.verdict in PASSING


class Git:
    """The questions the check asks of history. Tests pass a fake or a scratch repository."""

    def __init__(self, cwd: Optional[str] = None) -> None:
        self.cwd = cwd

    def _run(self, *args: str) -> subprocess.CompletedProcess:
        return subprocess.run(
            ["git", *args], capture_output=True, text=True, cwd=self.cwd, check=False
        )

    def is_ancestor(self, ancestor: str, head: str) -> bool:
        """True when `ancestor` names a commit of this clone that is `head` or one of its ancestors."""
        if self._run("cat-file", "-e", f"{ancestor}^{{commit}}").returncode != 0:
            return False
        return self._run("merge-base", "--is-ancestor", ancestor, head).returncode == 0

    def code_commits_after(self, covered: str, head: str, base: str) -> List[str]:
        """Commits in `covered..head` not reachable from `base` that touch a code path; a merge by
        what its own resolution changed (its remerge diff)."""
        r = self._run(
            "log",
            "--no-renames",
            "--remerge-diff",
            "--format=@%H %P",
            "--name-only",
            f"{covered}..{head}",
            f"^{base}",
        )
        if r.returncode != 0:
            raise RuntimeError(r.stderr.strip() or "git log failed")
        out: List[str] = []
        sha = None
        for line in r.stdout.splitlines():
            if line.startswith("@"):
                sha, *parents = line[1:].split()
                # git computes no remerge diff for an octopus: unknowable, so stale.
                if len(parents) > 2 and sha not in out:
                    out.append(sha)
            elif line and sha and is_code_path(line) and sha not in out:
                out.append(sha)
        return out

    def manifest_diff(self, base: str, head: str, paths: Sequence[str]) -> bytes:
        """`git diff -U0 base...head` of `paths`, the same comparison as changed_paths, as bytes:
        text mode would turn a lone `\r` into `\n` and fail on bytes that are not UTF-8."""
        r = subprocess.run(
            [
                "git",
                "diff",
                "--no-renames",
                "--no-color",
                "--no-ext-diff",
                "--no-textconv",
                "--src-prefix=a/",
                "--dst-prefix=b/",
                "-U0",
                f"{base}...{head}",
                "--",
                *paths,
            ],
            capture_output=True,
            cwd=self.cwd,
            check=False,
        )
        if r.returncode != 0:
            message = r.stderr.decode("utf-8", "replace").strip()
            raise RuntimeError(message or "git diff failed")
        return r.stdout

    def changed_paths(self, base: str, head: str) -> List[str]:
        r = self._run("diff", "--no-renames", "--name-only", f"{base}...{head}")
        if r.returncode != 0:
            raise RuntimeError(r.stderr.strip() or "git diff failed")
        return [p for p in r.stdout.splitlines() if p]


def judge(
    body: str,
    paths: Sequence[str],
    head: str,
    base: str,
    git: Git,
    base_ref: str = RULE_BASE,
    author: Optional[str] = None,
) -> Result:
    if base_ref != RULE_BASE:
        return Result(
            NOT_REQUIRED,
            f"base is {base_ref!r}; the rule covers pull requests into {RULE_BASE!r}",
        )
    code = [p for p in paths if is_code_path(p)]
    if not code:
        return Result(
            NOT_REQUIRED, f"no changed path is a code path ({len(paths)} changed)"
        )

    not_exempt = ""
    if author == DEPENDENCY_BOT and all(is_manifest_path(p) for p in code):
        why = version_only(git.manifest_diff(base, head, code), code)
        if why is None:
            return Result(
                NOT_REQUIRED,
                f"{DEPENDENCY_BOT} changed only package versions ({len(code)} manifest(s))",
            )
        not_exempt = f" ({DEPENDENCY_BOT} exemption not taken: {why})"

    parsed = parse_body(body)
    if not parsed.lines:
        return Result(
            MISSING,
            f"{len(code)} code path(s) changed (e.g. {code[0]}) and no line starts with `Independent review @`"
            " outside code, quotes and comments" + not_exempt,
        )
    if not parsed.last_line_parses:
        return Result(
            MALFORMED,
            f"no `Independent review @ <sha>: <verdict>` read from: {parsed.lines[-1][:160]}",
        )

    last = parsed.reviews[-1]
    for review in parsed.reviews:
        if not git.is_ancestor(review.sha, head):
            return Result(
                NOT_ANCESTOR,
                f"reviewed sha {review.sha} is not an ancestor of head {head[:9]} (or is unknown here)"
                + REWRITE_HINT[:-1]
                + REBASE_HINT
                + ")",
            )
        if git.is_ancestor(review.sha, base):
            return Result(
                ON_BASE,
                f"reviewed sha {review.sha} is already on the base: it names `dev`, not this PR",
            )

    if parsed.fix_range and not git.is_ancestor(parsed.fix_range[1], head):
        return Result(
            NOT_ANCESTOR,
            f"fix range end {parsed.fix_range[1]} is not an ancestor of head {head[:9]} (or is unknown here)"
            + REWRITE_HINT[:-1]
            + REBASE_HINT
            + ")",
        )

    # Every do-not-merge stands until a LATER-written review of a strictly descending commit
    # clears it; one review may clear several.
    blocking: List[Review] = []
    for review in parsed.reviews:
        if review.verdict == DO_NOT_MERGE:
            blocking.append(review)
        else:
            blocking = [
                b
                for b in blocking
                if not (
                    git.is_ancestor(b.sha, review.sha)
                    and not git.is_ancestor(review.sha, b.sha)
                )
            ]
    if blocking:
        shas = ", ".join(b.sha for b in blocking)
        return Result(
            DO_NOT_MERGE,
            f"the review(s) @ {shas} say do not merge, and no later review of a commit after each clears it",
        )
    covered = last.sha
    if parsed.fix_range:
        start, end = parsed.fix_range
        if not any(
            git.is_ancestor(r.sha, start) and git.is_ancestor(start, r.sha)
            for r in parsed.reviews
        ):
            return Result(
                BAD_RANGE,
                f"fix range start {start} is not one of the line's reviewed shas",
            )
        if not git.is_ancestor(start, end):
            return Result(BAD_RANGE, f"fix range {start}..{end} does not run forward")
        if git.is_ancestor(last.sha, end):
            covered = end
    after = git.code_commits_after(covered, head, base)
    if after:
        return Result(
            STALE,
            f"{len(after)} code commit(s) after {covered[:9]}, the last point the line covers: "
            f"{', '.join(s[:9] for s in after[:5])}" + REWRITE_HINT,
        )
    return Result(
        OK, f"review @ {last.sha}: {last.verdict}; covered through {covered[:9]}"
    )


def main(argv: Optional[List[str]] = None, git_factory: Callable[[], Git] = Git) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--head", required=True, help="the PR's head sha")
    parser.add_argument("--base", help="the base tip (default origin/<base-ref>)")
    parser.add_argument("--base-ref", default=RULE_BASE)
    parser.add_argument(
        "--author", help="the PR author's login (exempts a Dependabot manifest-only PR)"
    )
    parser.add_argument(
        "--body-env", default="PR_BODY", help="environment variable holding the PR body"
    )
    parser.add_argument(
        "--draft", action="store_true", help="a draft: report, never fail"
    )
    args = parser.parse_args(argv)

    base = args.base or f"origin/{args.base_ref}"
    git = git_factory()
    try:
        if args.base_ref != RULE_BASE:
            result = judge("", [], args.head, base, git, base_ref=args.base_ref)
        else:
            paths = git.changed_paths(base, args.head)
            result = judge(
                os.environ.get(args.body_env, ""),
                paths,
                args.head,
                base,
                git,
                base_ref=args.base_ref,
                author=args.author,
            )
    except RuntimeError as exc:
        print(f"check_review_line: {exc}", file=sys.stderr)
        return 2

    message = f"review-line: {result.verdict}: {result.detail}"
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a") as f:
            f.write(f"**review-line: {result.verdict}**: {result.detail}\n")
    if result.passing:
        print(message)
        return 0
    if args.draft:
        print(
            f"::warning::{message} (draft: not failing; marking it ready re-runs this check)"
        )
        return 0
    print(f"::error::{message}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
