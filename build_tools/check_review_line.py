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

Verdicts, in the order they are decided:

  not required   the base is not `dev` (a promotion into `mainline`, a stacked PR), the head is
                 `dev`, the author is a bot (a login ending in `[bot]`: dependabot, github-actions),
                 or no changed path is a code path (a sync of `mainline` into `dev` changes none);
  missing        no line starts with `Independent review @` (list markers, `>`, `**` and backticks
                 before it are skipped);
  malformed      the last such line has no `review @ <hex sha>: <verdict>` with a verdict of
                 `merge`, `merge after fixes` or `do not merge`;
  do not merge   the last review written on the line(s) says so;
  sha not an ancestor of head
                 a reviewed sha, or the end of a `fixes <a>..<b>` range, is not an ancestor of (or
                 equal to) the head, or this clone does not have it;
  stale          a non-merge commit after the covered point (the last review's sha, or the fix
                 range's end when that descends from it), not reachable from the base, touches a
                 code path;
  ok             otherwise.

What this does NOT judge, deliberately: that the reviewer was independent or that the fix range was
read (a line is text anyone can write); the `<n> applied, <m> declined` counts (real lines vary:
`0 blocking, 3 optional notes`, `no new findings`); whether a fix round needed a second review (an
added abstraction or guard, a public surface, the 100-line threshold); whether the commits inside
the fix range are only the review's fixes; and whether a merge commit's conflict resolution changed
code (every merge is treated as a `dev` merge).

Usage:
    python3 build_tools/check_review_line.py --base <sha> --head <sha> [--body-env PR_BODY]
        [--base-ref dev] [--head-ref <branch>] [--author <login>] [--draft]

The body is read from the named environment variable (default PR_BODY), never from argv, so a
workflow never puts PR text on a command line. Changed paths are `git diff --name-only base...head`.
Exit 0 for `ok` / `not required` (and for any verdict on a draft, which prints a warning), 1 for
any other verdict, 2 if git fails.
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

NOT_REQUIRED = "not required"
OK = "ok"
MISSING = "missing"
MALFORMED = "malformed"
DO_NOT_MERGE = "do not merge"
NOT_ANCESTOR = "sha not an ancestor of head"
STALE = "stale"
PASSING = (NOT_REQUIRED, OK)

# A line that starts, after list markers, quote markers, bold and backticks, with the line's head.
LINE_HEAD = re.compile(r"^[\s>*`_-]*independent review @", re.IGNORECASE)
# One review: `Independent review @ <sha>`, `second review @ <sha> (the head)`, then `: <verdict>`
# ending at punctuation or the end of the line, so `merge after fix;` is not read as `merge`.
REVIEW = re.compile(
    r"\b(?:independent|second|third|fourth|fifth|final)\s+review\s+@\s+([0-9a-f]{7,40})\b"
    r"(?:\s*\([^)]*\))?\s*:\s*(do not merge|merge after fixes|merge)(?=\s*(?:[;,.(`]|$))",
    re.IGNORECASE,
)
# `fixes <a>..<b> read by the lead`, `the second review read the fix commit <a>..<b> itself`.
FIX_RANGE = re.compile(
    r"\bfix(?:es|\s+commits?)?\s+([0-9a-f]{7,40})\.\.([0-9a-f]{7,40})\b", re.IGNORECASE
)


def is_code_path(path: str) -> bool:
    return path.startswith(CODE_PREFIXES)


@dataclass(frozen=True)
class Review:
    sha: str
    verdict: str


@dataclass
class ParsedLine:
    lines: List[str] = field(default_factory=list)
    reviews: List[Review] = field(default_factory=list)
    fix_end: Optional[str] = None
    last_line_parses: bool = False


def parse_body(body: str) -> ParsedLine:
    """Every review and the last fix range on every line that starts with `Independent review @`."""
    parsed = ParsedLine()
    for line in (body or "").splitlines():
        if not LINE_HEAD.match(line):
            continue
        parsed.lines.append(line.strip())
        found = [
            Review(m.group(1).lower(), m.group(2).lower())
            for m in REVIEW.finditer(line)
        ]
        parsed.reviews += found
        parsed.last_line_parses = bool(found)
        for m in FIX_RANGE.finditer(line):
            parsed.fix_end = m.group(2).lower()
    return parsed


@dataclass(frozen=True)
class Result:
    verdict: str
    detail: str

    @property
    def passing(self) -> bool:
        return self.verdict in PASSING


class Git:
    """The two questions the check asks of history. Tests pass a fake or a scratch repository."""

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
        """Non-merge commits in `covered..head` not reachable from `base` that touch a code path."""
        r = self._run(
            "log",
            "--no-merges",
            "--format=@%H",
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
                sha = line[1:]
            elif line and sha and is_code_path(line) and sha not in out:
                out.append(sha)
        return out

    def changed_paths(self, base: str, head: str) -> List[str]:
        r = self._run("diff", "--name-only", f"{base}...{head}")
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
    head_ref: str = "",
    author: str = "",
) -> Result:
    if base_ref != RULE_BASE:
        return Result(
            NOT_REQUIRED,
            f"base is {base_ref!r}; the rule covers pull requests into {RULE_BASE!r}",
        )
    if head_ref == RULE_BASE:
        return Result(NOT_REQUIRED, f"head is {RULE_BASE!r}: a promotion, not a lane")
    if author.endswith("[bot]"):
        return Result(NOT_REQUIRED, f"author {author} is a bot")
    code = [p for p in paths if is_code_path(p)]
    if not code:
        return Result(
            NOT_REQUIRED, f"no changed path is a code path ({len(paths)} changed)"
        )

    parsed = parse_body(body)
    if not parsed.lines:
        return Result(
            MISSING,
            f"{len(code)} code path(s) changed (e.g. {code[0]}) and no line starts with `Independent review @`",
        )
    if not parsed.last_line_parses:
        return Result(
            MALFORMED,
            f"no `review @ <sha>: <verdict>` read from: {parsed.lines[-1][:160]}",
        )

    last = parsed.reviews[-1]
    if last.verdict == DO_NOT_MERGE:
        return Result(DO_NOT_MERGE, f"the last review, @ {last.sha}, says do not merge")
    for review in parsed.reviews:
        if not git.is_ancestor(review.sha, head):
            return Result(
                NOT_ANCESTOR,
                f"reviewed sha {review.sha} is not an ancestor of head {head[:9]} (or is unknown here)",
            )
    if parsed.fix_end and not git.is_ancestor(parsed.fix_end, head):
        return Result(
            NOT_ANCESTOR,
            f"fix range end {parsed.fix_end} is not an ancestor of head {head[:9]} (or is unknown here)",
        )

    covered = last.sha
    if parsed.fix_end and git.is_ancestor(last.sha, parsed.fix_end):
        covered = parsed.fix_end
    after = git.code_commits_after(covered, head, base)
    if after:
        return Result(
            STALE,
            f"{len(after)} code commit(s) after {covered[:9]}, the last point the line covers: {', '.join(s[:9] for s in after[:5])}",
        )
    return Result(
        OK, f"review @ {last.sha}: {last.verdict}; covered through {covered[:9]}"
    )


def main(argv: Optional[List[str]] = None, git_factory: Callable[[], Git] = Git) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--base", required=True, help="the base sha (the PR's base tip)"
    )
    parser.add_argument("--head", required=True, help="the PR's head sha")
    parser.add_argument(
        "--body-env", default="PR_BODY", help="environment variable holding the PR body"
    )
    parser.add_argument("--base-ref", default=RULE_BASE)
    parser.add_argument("--head-ref", default="")
    parser.add_argument("--author", default="")
    parser.add_argument(
        "--draft", action="store_true", help="a draft: report, never fail"
    )
    args = parser.parse_args(argv)

    git = git_factory()
    try:
        paths = git.changed_paths(args.base, args.head)
        result = judge(
            os.environ.get(args.body_env, ""),
            paths,
            args.head,
            args.base,
            git,
            base_ref=args.base_ref,
            head_ref=args.head_ref,
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
