"""Tests for build_tools/check_commit_subjects.py — the ≤ 72-char commit-subject gate (W3.4 / D5)."""

from __future__ import annotations

import os
import subprocess
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_commit_subjects as mod


def _c(subject: str, parents: int = 1, sha: str = "a" * 40) -> mod.Commit:
    return mod.Commit(sha, subject, parents)


def test_subject_at_the_limit_is_clean():
    assert mod.offences([_c("x" * 72)]) == []


def test_subject_one_over_the_limit_is_an_offence():
    long = _c("x" * 73)
    assert mod.offences([long]) == [long]


def test_merge_commit_is_exempt():
    # GitHub's own merge subject for a long branch name runs past 72.
    merge = _c(
        "Merge pull request #2465 from antonsynd/claude/w1.2-round-python-contract",
        parents=2,
    )
    assert len(merge.subject) > 72
    assert mod.offences([merge]) == []


def test_revert_commit_is_exempt():
    revert = _c('Revert "' + "x" * 80 + '"')
    assert mod.offences([revert]) == []


def test_revert_lookalike_without_quote_is_not_exempt():
    # The exemption is for git's own `Revert "..."` spelling; a hand-written long subject that
    # happens to start with Revert is still an offence (the positive control for is_exempt).
    bad = _c("Revert the " + "x" * 70)
    assert mod.offences([bad]) == [bad]


def test_custom_max():
    assert mod.offences([_c("x" * 50)], max_len=49) != []
    assert mod.offences([_c("x" * 50)], max_len=50) == []


def test_parse_rev_list_counts_parents_and_keeps_subject_verbatim():
    text = (
        "commit 1111\n1111\0p1 p2\0Merge branch 'a' into b\n"
        "commit 2222\n2222\0p1\0fix(core): a: subject with: colons\n"
        "commit 3333\n3333\0\0root commit\n"
    )
    commits = mod.parse_rev_list(text)
    assert [(c.sha, c.parent_count, c.subject) for c in commits] == [
        ("1111", 2, "Merge branch 'a' into b"),
        ("2222", 1, "fix(core): a: subject with: colons"),
        ("3333", 0, "root commit"),
    ]


# ── Against a real repository ─────────────────────────────────────────────────


def _git(repo, *args, env=None):
    base_env = {
        **os.environ,
        "GIT_AUTHOR_NAME": "t",
        "GIT_AUTHOR_EMAIL": "t@example.com",
        "GIT_COMMITTER_NAME": "t",
        "GIT_COMMITTER_EMAIL": "t@example.com",
    }
    if env:
        base_env.update(env)
    return subprocess.run(
        ["git", *args],
        cwd=repo,
        check=True,
        capture_output=True,
        text=True,
        env=base_env,
    ).stdout


@pytest.fixture
def repo(tmp_path):
    _git(tmp_path, "init", "-q", "-b", "main")
    (tmp_path / "f").write_text("0\n")
    _git(tmp_path, "add", "f")
    _git(tmp_path, "commit", "-q", "-m", "chore: base")
    return tmp_path


def _commit(repo, subject: str):
    (repo / "f").write_text(subject + "\n")
    _git(repo, "commit", "-q", "-am", subject)


def test_range_with_a_long_subject_exits_1_and_names_it(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    result = subprocess.run(
        [sys.executable, mod.__file__, f"{base}..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 1, result.stdout + result.stderr
    assert "::error::" in result.stdout and "81 chars" in result.stdout


def test_range_of_short_subjects_and_a_merge_exits_0(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): short")
    _git(repo, "checkout", "-q", "-b", "side", base)
    (repo / "g").write_text("side\n")
    _git(repo, "add", "g")
    _git(repo, "commit", "-q", "-m", "test(side): short")
    _git(repo, "checkout", "-q", "main")
    _git(
        repo,
        "merge",
        "-q",
        "--no-ff",
        "-m",
        "Merge pull request #1 from someone/" + "b" * 60,
        "side",
    )
    result = subprocess.run(
        [sys.executable, mod.__file__, f"{base}..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stdout + result.stderr
    assert (
        "2 subjects checked" in result.stdout
        and "1 merge/revert exempt" in result.stdout
    )


def test_empty_range_is_clean(repo):
    result = subprocess.run(
        [sys.executable, mod.__file__, "HEAD..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0 and "0 subjects checked" in result.stdout


def test_bad_range_exits_2(repo):
    result = subprocess.run(
        [sys.executable, mod.__file__, "nosuchref..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 2
