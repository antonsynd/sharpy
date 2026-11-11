"""Tests for build_tools/check_allowlist_severity.py — no allowlist row moves toward a worse class."""

from __future__ import annotations

import itertools
import os
import re
import subprocess
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_allowlist_severity as mod

NAME_RES = mod.COVERED[0]
HEADER = (
    "# P77a D4: the name-resolution differential ratchet. One generated cell per row with its verdict class:\n"
    "#   <cell-id> <Ice|FalseRefusal|SilentWrong|RuntimeThrow> # #<issue> <lane>\n"
)
P85_COMMIT = "8d0367e0a"
P85_ROW = "store/attribute/SelfDot/StaticField/Augmented/InitBody"
IN_CI = bool(os.environ.get("GITHUB_ACTIONS"))


def _file(*rows: str) -> str:
    return HEADER + "".join(r + "\n" for r in rows)


def _row(key: str, status: str) -> str:
    return f"{key} {status} # #1 lane"


def _git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=mod.REPO_ROOT, capture_output=True, text=True, check=False
    )


def _has_commit(ref: str) -> bool:
    return _git("cat-file", "-e", f"{ref}^{{commit}}").returncode == 0


# ── The order ───────────────────────────────────────────────────────────────────


def test_order_is_worst_first():
    assert [mod.SEVERITY_NAMES[i] for i in range(6)] == [
        "silent-wrong",
        "wrong-throw",
        "ice",
        "false-refusal",
        "message-only",
        "pass",
    ]
    assert mod.WORST == mod.SILENT_WRONG


@pytest.mark.parametrize(
    "was,now", list(itertools.product(mod.NAME_RESOLUTION_SEVERITY, repeat=2))
)
def test_every_status_transition_is_judged_by_rank(was, now):
    base = _file(_row("k", was))
    head = _file(_row("k", now))
    findings = mod.compare(NAME_RES, base, head)
    worse = mod.NAME_RESOLUTION_SEVERITY[now] < mod.NAME_RESOLUTION_SEVERITY[was]
    assert bool(findings) == worse, (was, now, findings)
    # The same transition with a ruling token passes.
    ruled = HEADER + "# owner-ruling D1 k\n" + _row("k", now) + "\n"
    assert mod.compare(NAME_RES, base, ruled) == []


@pytest.mark.parametrize("status", list(mod.NAME_RESOLUTION_SEVERITY))
def test_a_new_row_fails_only_at_the_worst_class(status):
    findings = mod.compare(NAME_RES, _file(), _file(_row("k", status)))
    assert bool(findings) == (mod.NAME_RESOLUTION_SEVERITY[status] == mod.WORST)


@pytest.mark.parametrize("status", list(mod.NAME_RESOLUTION_SEVERITY))
def test_a_disappearing_row_passes(status):
    assert mod.compare(NAME_RES, _file(_row("k", status)), _file()) == []


def test_base_absent_makes_every_row_new():
    findings = mod.compare(
        NAME_RES, None, _file(_row("a", "SilentWrong"), _row("b", "Ice"))
    )
    assert [f.key for f in findings] == ["a"]


# ── Covered files never skip silently ───────────────────────────────────────────


def test_missing_at_head_is_red():
    findings = mod.compare(NAME_RES, _file(_row("k", "Ice")), None)
    assert len(findings) == 1 and "missing at HEAD" in findings[0].message


@pytest.mark.parametrize(
    "bad",
    [
        "k Silent # #1 lane",  # unknown class
        "k",  # no class
        "k|Ice # #1 lane",  # changed separator
        "Ice # #1 lane",  # no key
    ],
)
def test_an_unparsable_row_at_head_is_red(bad):
    findings = mod.compare(NAME_RES, _file(), _file(bad))
    assert len(findings) == 1 and "format changed" in findings[0].message


def test_a_duplicate_key_is_red():
    findings = mod.compare(NAME_RES, _file(), _file(_row("k", "Ice"), _row("k", "Ice")))
    assert "listed twice" in findings[0].message


def test_a_malformed_ruling_token_is_red():
    findings = mod.compare(
        NAME_RES,
        _file(),
        HEADER + "# owner-ruling because k\n" + _row("k", "SilentWrong") + "\n",
    )
    assert "format changed" in findings[0].message


def test_a_ruling_naming_no_row_is_red():
    findings = mod.compare(NAME_RES, _file(), HEADER + "# owner-ruling D18 gone\n")
    assert findings and findings[0].key == "gone"


def test_a_ruling_exempts_only_its_own_key():
    head = (
        HEADER
        + "# owner-ruling R-KU a\n"
        + _row("a", "SilentWrong")
        + "\n"
        + _row("b", "SilentWrong")
        + "\n"
    )
    assert [f.key for f in mod.compare(NAME_RES, _file(), head)] == ["b"]


def test_the_vocabulary_is_the_csharp_loaders():
    """The C# loader's error message names the classes it accepts; a class added or renamed there must
    be ranked here."""
    path = os.path.join(
        mod.REPO_ROOT,
        "src",
        "Sharpy.Compiler.Tests",
        "Conformance",
        "NameResolutionDifferentialTests.cs",
    )
    with open(path, encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"'<cell-id> <([A-Za-z|]+)> # #<issue> <lane>'", text)
    assert m, "LoadAllowlist's row-shape message moved; re-derive the vocabulary"
    assert set(m.group(1).split("|")) == set(mod.NAME_RESOLUTION_SEVERITY)


def test_every_covered_file_exists_and_parses():
    for cov in mod.COVERED:
        with open(os.path.join(mod.REPO_ROOT, cov.path), encoding="utf-8") as f:
            parsed = cov.parse(f.read())
        assert parsed.rows, f"{cov.path} parsed to no rows"
        assert set(parsed.rows.values()) <= set(cov.severity)


# ── P85 (PR #2504): Ice -> SilentWrong, the transition this gate exists for ────


def test_p85_synthetic():
    base = _file(f"{P85_ROW} Ice # #2435 ice")
    head = _file(f"{P85_ROW} SilentWrong # #2435 missing-refusal")
    findings = mod.compare(NAME_RES, base, head)
    assert [f.key for f in findings] == [P85_ROW]
    assert "Ice (ice) -> SilentWrong (silent-wrong)" in findings[0].message


def test_p85_from_history():
    if not _has_commit(P85_COMMIT):
        if IN_CI:
            pytest.fail(
                f"{P85_COMMIT} is not in this clone; the suite needs full history in CI"
            )
        pytest.skip(f"{P85_COMMIT} not in this clone (shallow?)")
    base = mod.show(f"{P85_COMMIT}^1", NAME_RES.path)
    head = mod.show(P85_COMMIT, NAME_RES.path)
    assert base is not None and head is not None
    findings = mod.compare(NAME_RES, base, head)
    assert [f.key for f in findings] == [P85_ROW], findings


# ── The repo, against its merge base ───────────────────────────────────────────


def test_repo_against_merge_base():
    target = mod.default_target()
    head = mod.resolve("HEAD")
    base = mod.merge_base(target, "HEAD") if head else None
    if base is None:
        if IN_CI:
            pytest.fail(
                f"no merge base between {target} and HEAD: CI must fetch full history and {target}"
            )
        pytest.skip(f"no merge base between {target} and HEAD")
    findings = mod.check(base, head)
    assert findings == [], "\n".join(map(str, findings))
    assert mod.main(["--base", base, "--head", head]) == 0


def test_main_fails_on_the_p85_merge():
    """Positive control for the repo test above: the same entry point is red on a known-bad range."""
    if not _has_commit(P85_COMMIT):
        if IN_CI:
            pytest.fail(
                f"{P85_COMMIT} is not in this clone; the suite needs full history in CI"
            )
        pytest.skip(f"{P85_COMMIT} not in this clone (shallow?)")
    assert mod.main(["--base", f"{P85_COMMIT}^1", "--head", P85_COMMIT]) == 1


def test_main_exit_2_on_an_unresolvable_base():
    assert mod.main(["--base", "no-such-ref-xyz"]) == 2


# ── Rulings waive only when new in this change ─────────────────────────────────


def _ruling(target: str, rid: str = "D1") -> str:
    return f"# owner-ruling {rid} {target}"


def test_a_ruling_already_at_base_does_not_waive_a_new_worsening():
    base = _file(_ruling("k"), _row("k", "Ice"))
    head = _file(_ruling("k"), _row("k", "SilentWrong"))
    assert [f.key for f in mod.compare(NAME_RES, base, head)] == ["k"]


def test_a_ruling_already_at_base_is_inert():
    """Present at both refs, it fails nothing: not with its row unchanged, not with the row gone."""
    unchanged = _file(_ruling("k"), _row("k", "SilentWrong"))
    assert mod.compare(NAME_RES, unchanged, unchanged) == []
    assert mod.compare(NAME_RES, unchanged, _file(_ruling("k"))) == []


def test_a_new_exact_ruling_on_an_unflagged_row_passes():
    """Documenting a past decision on a row that is already on the base."""
    base = _file(_row("k", "SilentWrong"))
    head = _file(_ruling("k", "D18"), _row("k", "SilentWrong"))
    assert mod.compare(NAME_RES, base, head) == []


def test_the_failure_message_says_regeneration_drops_rulings():
    findings = mod.compare(NAME_RES, _file(), _file(_row("k", "SilentWrong")))
    assert mod.REGENERATION_NOTE in findings[0].message


# ── Prefix rulings ─────────────────────────────────────────────────────────────


def test_a_prefix_ruling_covers_new_and_worsened_rows_under_it():
    base = _file(_row("a/x", "Ice"), _row("b/x", "Ice"))
    head = _file(
        _ruling("a/*", "R-KU"),
        _row("a/x", "SilentWrong"),  # worsened, covered
        _row("a/y", "SilentWrong"),  # new, covered
        _row("b/x", "SilentWrong"),  # worsened, not covered
        _row("b/y", "SilentWrong"),  # new, not covered
    )
    assert sorted(f.key for f in mod.compare(NAME_RES, base, head)) == ["b/x", "b/y"]


def test_a_prefix_ruling_matching_no_flagged_row_is_red():
    base = _file(_row("a/x", "SilentWrong"))
    head = _file(_ruling("a/*"), _row("a/x", "SilentWrong"), _row("b/y", "Ice"))
    findings = mod.compare(NAME_RES, base, head)
    assert [f.key for f in findings] == ["a/*"]
    assert "matches no flagged row" in findings[0].message


def test_a_prefix_ruling_already_at_base_is_inert():
    base = _file(_ruling("a/*"), _row("a/x", "Ice"))
    head = _file(_ruling("a/*"), _row("a/x", "SilentWrong"), _row("a/y", "SilentWrong"))
    assert sorted(f.key for f in mod.compare(NAME_RES, base, head)) == ["a/x", "a/y"]


@pytest.mark.parametrize("target", ["*", "a*b", "a/**", "*a"])
def test_glob_syntax_other_than_one_trailing_star_is_red(target):
    findings = mod.compare(
        NAME_RES, _file(), _file(_ruling(target), _row("a/x", "SilentWrong"))
    )
    assert len(findings) == 1 and "malformed owner-ruling" in findings[0].message


# ── A base that cannot be read ─────────────────────────────────────────────────


def test_an_unparsable_base_is_red_and_names_the_ref():
    base = _file("k Silent # #1 lane", _row("j", "Ice"))
    head = _file(_row("j", "SilentWrong"))
    findings = mod.compare(NAME_RES, base, head, base_ref="abc123")
    assert len(findings) == 1
    assert "abc123" in findings[0].message and "does not parse" in findings[0].message
    assert findings[0].path == NAME_RES.path


# ── The range: base == HEAD judges the first parent; no range is exit 2 ─────────


def _tmp_repo(tmp_path, *versions: str) -> str:
    repo = str(tmp_path / "repo")
    os.makedirs(os.path.join(repo, os.path.dirname(NAME_RES.path)))

    def git(*args):
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=t",
                "-c",
                "user.email=t@t",
                "-c",
                "commit.gpgsign=false",
                *args,
            ],
            cwd=repo,
            capture_output=True,
            text=True,
            check=True,
        )

    git("init", "-q")
    for i, text in enumerate(versions):
        with open(os.path.join(repo, NAME_RES.path), "w", encoding="utf-8") as f:
            f.write(text)
        git("add", "-A")
        git("commit", "-q", "-m", f"v{i}")
    return repo


def test_base_equal_to_head_judges_the_first_parent(tmp_path, monkeypatch, capsys):
    repo = _tmp_repo(tmp_path, _file(_row("k", "Ice")), _file(_row("k", "SilentWrong")))
    monkeypatch.setattr(mod, "REPO_ROOT", repo)
    assert mod.main(["--target", "HEAD"]) == 1  # merge-base(HEAD, HEAD) is HEAD
    out = capsys.readouterr().out
    assert "judging first parent" in out and "k: Ice (ice) -> SilentWrong" in out
    assert mod.main(["--base", "HEAD"]) == 1


def test_base_equal_to_head_without_a_parent_is_exit_2(tmp_path, monkeypatch, capsys):
    repo = _tmp_repo(tmp_path, _file(_row("k", "Ice")))
    monkeypatch.setattr(mod, "REPO_ROOT", repo)
    assert mod.main(["--target", "HEAD"]) == 2
    assert "no range to judge" in capsys.readouterr().err
