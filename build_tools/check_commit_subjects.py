#!/usr/bin/env python3
"""Gate: every commit subject in a range is at most 72 characters (W3.4, owner ruling D5 of 2026-11-07),
and no commit message in it has a GitHub closing keyword.

The figure is the `/commit` skill's ("Keep the first line under 72 characters"); this script is what
makes it a gate instead of advice. Some subjects are exempt because another tool writes them
and quotes text the author did not choose: a merge commit (more than one parent — GitHub's
"Merge pull request #N from owner/branch" runs past 72 on a long branch name) and a `Revert "..."`
commit (it quotes the reverted subject verbatim). A third kind is Dependabot's generated
"chore: Bump ..." subject (owner, 2026-11-17): exempt only when the subject has one of
DEPENDABOT_SUBJECT's shapes, the author email is Dependabot's, and, on a pull-request run
(`--pr-author` given), the PR author login is `dependabot[bot]` or the commit is already on the
remote-tracking `origin/dev` or `origin/mainline` (a promotion or sync PR carries one its own PR
run judged). A git author is free text; the login is GitHub's. Residual: a push run has no login,
so there the shape and email decide; both branches take only PRs, so it re-reads commits a PR run
judged. A fourth kind is exempt because it predates the gate: any commit reachable from the
commit that introduced this script (GATE_COMMIT, 2026-11-07) was written when the figure was
advice, so a range that reaches back past it — the dev → mainline promotion PR #2522 spanned 187
commits, six of them long — is judged only on what came after.
Override the grandfather line with `--grandfather <sha>` (or `--grandfather none`).

Closing keywords (owner, 2026-11-18): GitHub closes an issue when a commit whose message says
`close`/`fix`/`resolve` (any tense or case, an optional colon) before a reference to it reaches
the default branch, `mainline`. The promotion #2736 closed the in-flight tracker #2422 that way
from a commit body quoting a ruling ("Move cell, close #2422"). Every commit of the range is
judged, merges, reverts and Dependabot's included (none of them is exempt from GitHub's reading),
except those behind the grandfather line. On a PR run, `--pr-title` judges the title too: merging
the PR writes it into the merge commit's body, which no PR run sees. `--pr-body` judges a PR's
description, which GitHub reads only when the PR targets the default branch: the workflow passes
it for a PR into `mainline` (the promotion; owner, 2026-11-18). An issue is cited as `Refs #N`.

Usage:
    python3 build_tools/check_commit_subjects.py [<range>] [--max 72] [--grandfather <sha>|none]
        [--pr-author=<login>] [--pr-title=<title>] [--pr-body=<description>]

<range> is any `git rev-list` range (`origin/dev..HEAD`, `A...B`); it defaults to `@{upstream}..HEAD`.
An empty range is clean. An unknown grandfather sha (a clone that lacks it) exempts nothing. Exit 0 if clean, 1 if any subject is over the limit
or any message has a closing keyword (one `::error::` line per offence, so a CI log shows the sha and
the subject or the keyword), 2 if git fails or gives no message for a commit of the range.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass

DEFAULT_MAX = 72
DEFAULT_RANGE = "@{upstream}..HEAD"
REVERT_PREFIX = 'Revert "'
# ci(build_tools): gate commit subjects at 72 chars on PRs and dev pushes — the commit that made the
# figure a gate. Everything reachable from it is pre-gate history and is not judged.
GATE_COMMIT = "026e6e19d03c211e1212e1f49ffb981369226a43"


# GitHub's noreply address for the Dependabot app (user id 49699333).
DEPENDABOT_EMAIL = "49699333+dependabot[bot]@users.noreply.github.com"
DEPENDABOT_LOGIN = "dependabot[bot]"
# Every shape in `git log --author=dependabot origin/dev origin/mainline` (2026-11-17), anchored:
# `Bump <pkg> from <a> to <b>`, a security update's bare `Bump <pkg>`, `Bump the <g> group with
# <n> update(s)` and `... group across <n> directory with ...`; the prefix is `chore`, or with
# .github/dependabot.yml's `include: scope` `chore(deps-dev)` (seen) or `chore(deps)`.
DEPENDABOT_SUBJECT = re.compile(
    r"chore(?:\(deps(?:-dev)?\))?: Bump (?:"
    r"\S+ from \S+ to \S+"
    r"|the \S+ group(?: across \d+ director(?:y|ies))? with \d+ updates?"
    r"|\S+)"
)
# Branches that take only pull requests: a commit on one was judged by the PR run that landed it.
# Full names: git resolves a short `origin/dev` to a tag `refs/tags/origin/dev` first.
JUDGED_REFS = ("refs/remotes/origin/dev", "refs/remotes/origin/mainline")
# GitHub's closing keywords (docs: "Linking a pull request to an issue"), then an issue reference:
# `#N`, `owner/repo#N` or the issue's URL. `\b` keeps `prefix #1` out; `issue #N` is not a keyword.
# Whether GitHub reads a reference wrapped onto the next line is not documented; it is judged as one
# (a false positive costs a reword, a false negative closes an issue). A blank line between is not.
CLOSING_KEYWORD = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)"
    r"(?::[ \t]*(?:\n[ \t]*)?|[ \t]*\n[ \t]*|[ \t]+)"
    r"(?:#\d+|[\w.-]+/[\w.-]+#\d+|https://github\.com/[\w.-]+/[\w.-]+/issues/\d+)",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class Commit:
    sha: str
    subject: str
    parent_count: int
    author_email: str = ""


def is_dependabot(
    commit: Commit, pr_author: str | None = None, judged: frozenset = frozenset()
) -> bool:
    """Dependabot's shape and email, and on a PR run (`pr_author` not None) its login or a commit
    already on a judged branch (`judged`: the shas reachable from JUDGED_REFS)."""
    return (
        DEPENDABOT_SUBJECT.fullmatch(commit.subject) is not None
        and commit.author_email.lower() == DEPENDABOT_EMAIL
        and (pr_author is None or pr_author == DEPENDABOT_LOGIN or commit.sha in judged)
    )


def is_exempt(
    commit: Commit, pr_author: str | None = None, judged: frozenset = frozenset()
) -> bool:
    """Merge, `Revert "..."` and Dependabot commits carry subjects another tool wrote."""
    return (
        commit.parent_count > 1
        or commit.subject.startswith(REVERT_PREFIX)
        or is_dependabot(commit, pr_author, judged)
    )


def offences(
    commits: list[Commit],
    max_len: int = DEFAULT_MAX,
    pr_author: str | None = None,
    judged: frozenset = frozenset(),
) -> list[Commit]:
    """The commits whose subject is longer than `max_len`, exempt ones removed, in the given order."""
    return [
        c
        for c in commits
        if not is_exempt(c, pr_author, judged) and len(c.subject) > max_len
    ]


def closing_keywords(message: str) -> list[str]:
    """Each closing keyword and the reference it names in `message`, in order, on one line. CR and
    CRLF count as a line break: a description edited on GitHub arrives through argv with them."""
    text = message.replace("\r\n", "\n").replace("\r", "\n")
    return [" ".join(m.group(0).split()) for m in CLOSING_KEYWORD.finditer(text)]


def parse_messages(text: str) -> dict[str, str]:
    """Parse `git log -z --format=%H%n%B` output (NUL between records; a message has no NUL) into
    {sha: full message}."""
    out: dict[str, str] = {}
    for record in text.split("\0"):
        record = record.lstrip("\n")
        if not record:
            continue
        sha, _, message = record.partition("\n")
        out[sha] = message
    return out


def messages(
    rev_range: str, cwd: str | None = None, exclude_reachable_from: str | None = None
) -> dict[str, str]:
    """The full message of each commit of `rev_range` (minus the ancestors of `exclude_reachable_from`)."""
    # `log.showSignature` would print gpg lines into the records (rev_list's rev-list ignores it).
    args = [
        "git",
        "log",
        "-z",
        "--no-show-signature",
        "--no-color",
        "--format=%H%n%B",
        rev_range,
    ]
    if exclude_reachable_from:
        args.append(f"^{exclude_reachable_from}")
    result = subprocess.run(args, capture_output=True, text=True, cwd=cwd, check=False)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"git log {rev_range} failed")
    return parse_messages(result.stdout)


def parse_rev_list(text: str) -> list[Commit]:
    """Parse `git rev-list --format=%H%x00%P%x00%s%x00%ae` output (one `commit <sha>` header line, then a
    record; the author email is optional, so a record without it parses with an empty one)."""
    commits: list[Commit] = []
    for line in text.splitlines():
        if not line or line.startswith("commit "):
            continue
        sha, parents, subject, *rest = line.split("\0")
        commits.append(
            Commit(
                sha,
                subject,
                len(parents.split()) if parents.strip() else 0,
                rest[0] if rest else "",
            )
        )
    return commits


def commit_exists(sha: str, cwd: str | None = None) -> bool:
    """True when `sha` names a commit this clone has (a shallow clone or another repository may not)."""
    return (
        subprocess.run(
            ["git", "cat-file", "-e", f"{sha}^{{commit}}"],
            capture_output=True,
            cwd=cwd,
            check=False,
        ).returncode
        == 0
    )


def judged_shas(shas: list[str], cwd: str | None = None) -> frozenset:
    """Those of `shas` reachable from a JUDGED_REFS branch this clone has (none when it has none)."""
    out = set()
    for ref in JUDGED_REFS:
        if not commit_exists(ref, cwd):
            continue
        for sha in shas:
            if sha not in out and subprocess.run(
                ["git", "merge-base", "--is-ancestor", sha, ref],
                capture_output=True,
                cwd=cwd,
                check=False,
            ).returncode == 0:
                out.add(sha)
    return frozenset(out)


def rev_list(
    rev_range: str, cwd: str | None = None, exclude_reachable_from: str | None = None
) -> list[Commit]:
    """The commits of `rev_range`; with `exclude_reachable_from`, minus every ancestor of that commit."""
    args = ["git", "rev-list", "--format=%H%x00%P%x00%s%x00%ae", rev_range]
    if exclude_reachable_from:
        args.append(f"^{exclude_reachable_from}")
    result = subprocess.run(args, capture_output=True, text=True, cwd=cwd, check=False)
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
    parser.add_argument(
        "--grandfather",
        default=GATE_COMMIT,
        help="commits reachable from this sha are pre-gate and exempt; `none` judges the whole range"
        f" (default {GATE_COMMIT[:9]}, the commit that introduced the gate)",
    )
    parser.add_argument(
        "--pr-title",
        help="the pull request's title (a PR run): it becomes the merge commit's body, so it is judged"
        " for closing keywords",
    )
    parser.add_argument(
        "--pr-body",
        help="the description of a pull request into the default branch: merging it closes the"
        " issues its closing keywords name, so it is judged",
    )
    parser.add_argument(
        "--pr-author",
        help="the pull request author's login (a PR run); a Dependabot commit is exempt only when it"
        f" is {DEPENDABOT_LOGIN} or the commit is already on {' or '.join(JUDGED_REFS)}",
    )
    args = parser.parse_args(argv)

    grandfather = None if args.grandfather.lower() == "none" else args.grandfather
    if grandfather and not commit_exists(grandfather):
        grandfather = None

    try:
        all_commits = rev_list(args.range)
        commits = rev_list(args.range, exclude_reachable_from=grandfather) if grandfather else all_commits
        bodies = messages(args.range, exclude_reachable_from=grandfather)
    except RuntimeError as exc:
        print(f"check_commit_subjects: {exc}", file=sys.stderr)
        return 2
    pre_gate = len(all_commits) - len(commits)

    judged = frozenset()
    if args.pr_author is not None and args.pr_author != DEPENDABOT_LOGIN:
        # Only commits that could be exempt cost a lookup.
        judged = judged_shas(
            [c.sha for c in commits if is_dependabot(c, None) and len(c.subject) > args.max]
        )
    bad = offences(commits, args.max, args.pr_author, judged)
    checked = sum(1 for c in commits if not is_exempt(c, args.pr_author, judged))
    for c in bad:
        print(
            f"::error::commit {c.sha[:9]} subject is {len(c.subject)} chars (max {args.max}): {c.subject}"
        )
    unread = [c.sha for c in commits if c.sha not in bodies]
    if unread or len(bodies) != len(commits):
        print(
            f"check_commit_subjects: git log gave {len(bodies)} messages for {len(commits)} commits"
            f" of {args.range}" + (f", none for {unread[0][:9]}" if unread else ""),
            file=sys.stderr,
        )
        return 2
    keyed = [(c, closing_keywords(bodies[c.sha])) for c in commits]
    keyed = [(c, found) for c, found in keyed if found]
    title_keys = closing_keywords(args.pr_title) if args.pr_title else []
    if title_keys:
        print(
            f"::error::the PR title has a closing keyword ({'; '.join(title_keys)}): merging writes it"
            f" into the merge commit's body, which closes that issue on mainline; use `Refs #N`"
        )
    body_keys = closing_keywords(args.pr_body) if args.pr_body else []
    if body_keys:
        print(
            f"::error::the PR description has a closing keyword ({'; '.join(body_keys)}): merging"
            f" this PR into the default branch closes that issue; use `Refs #N`"
        )
    for c, found in keyed:
        print(
            f"::error::commit {c.sha[:9]} message has a closing keyword ({'; '.join(found)}): GitHub"
            f" closes that issue when the commit reaches mainline; reword it to `Refs #N`: {c.subject}"
        )
    if bad or keyed or title_keys or body_keys:
        if bad:
            print(
                f"check_commit_subjects: {len(bad)} of {checked} subjects over {args.max} chars in {args.range}"
            )
        if keyed:
            print(
                f"check_commit_subjects: {len(keyed)} of {len(commits)} messages have a closing keyword"
                f" in {args.range}"
            )
        return 1
    print(
        f"check_commit_subjects: {checked} subjects checked in {args.range}, none over {args.max} chars"
        + (
            f" ({len(commits) - checked} merge/revert exempt)"
            if len(commits) != checked
            else ""
        )
        + f"; {len(commits)} messages, no closing keyword"
        + (f" ({pre_gate} pre-gate, before {grandfather[:9]}, not judged)" if pre_gate else "")
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
