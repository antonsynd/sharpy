#!/usr/bin/env python3
"""Gate: every commit subject in a range is at most 72 characters (W3.4, owner ruling D5 of 2026-11-07).

The figure is the `/commit` skill's ("Keep the first line under 72 characters"); this script is what
makes it a gate instead of advice. Two kinds of subject are exempt because another tool writes them
and quotes text the author did not choose: a merge commit (more than one parent — GitHub's
"Merge pull request #N from owner/branch" runs past 72 on a long branch name) and a `Revert "..."`
commit (it quotes the reverted subject verbatim).

Usage:
    python3 build_tools/check_commit_subjects.py [<range>] [--max 72]

<range> is any `git rev-list` range (`origin/dev..HEAD`, `A...B`); it defaults to `@{upstream}..HEAD`.
An empty range is clean. Exit 0 if clean, 1 if any subject is over the limit (one `::error::` line
per offence, so a CI log shows the sha, the length and the subject), 2 if git fails.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass

DEFAULT_MAX = 72
DEFAULT_RANGE = "@{upstream}..HEAD"
REVERT_PREFIX = 'Revert "'


@dataclass(frozen=True)
class Commit:
    sha: str
    subject: str
    parent_count: int


def is_exempt(commit: Commit) -> bool:
    """Merge commits and `Revert "..."` commits carry subjects another tool wrote."""
    return commit.parent_count > 1 or commit.subject.startswith(REVERT_PREFIX)


def offences(commits: list[Commit], max_len: int = DEFAULT_MAX) -> list[Commit]:
    """The commits whose subject is longer than `max_len`, exempt ones removed, in the given order."""
    return [c for c in commits if not is_exempt(c) and len(c.subject) > max_len]


def parse_rev_list(text: str) -> list[Commit]:
    """Parse `git rev-list --format=%H%x00%P%x00%s` output (one `commit <sha>` header line, then a record)."""
    commits: list[Commit] = []
    for line in text.splitlines():
        if not line or line.startswith("commit "):
            continue
        sha, parents, subject = line.split("\0", 2)
        commits.append(
            Commit(sha, subject, len(parents.split()) if parents.strip() else 0)
        )
    return commits


def rev_list(rev_range: str, cwd: str | None = None) -> list[Commit]:
    result = subprocess.run(
        ["git", "rev-list", "--format=%H%x00%P%x00%s", rev_range],
        capture_output=True,
        text=True,
        cwd=cwd,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"git rev-list {rev_range} failed")
    return parse_rev_list(result.stdout)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "range",
        nargs="?",
        default=DEFAULT_RANGE,
        help=f"git rev-list range (default {DEFAULT_RANGE})",
    )
    parser.add_argument(
        "--max",
        type=int,
        default=DEFAULT_MAX,
        help=f"maximum subject length (default {DEFAULT_MAX})",
    )
    args = parser.parse_args(argv)

    try:
        commits = rev_list(args.range)
    except RuntimeError as exc:
        print(f"check_commit_subjects: {exc}", file=sys.stderr)
        return 2

    bad = offences(commits, args.max)
    checked = sum(1 for c in commits if not is_exempt(c))
    for c in bad:
        print(
            f"::error::commit {c.sha[:9]} subject is {len(c.subject)} chars (max {args.max}): {c.subject}"
        )
    if bad:
        print(
            f"check_commit_subjects: {len(bad)} of {checked} subjects over {args.max} chars in {args.range}"
        )
        return 1
    print(
        f"check_commit_subjects: {checked} subjects checked in {args.range}, none over {args.max} chars"
        + (
            f" ({len(commits) - checked} merge/revert exempt)"
            if len(commits) != checked
            else ""
        )
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
