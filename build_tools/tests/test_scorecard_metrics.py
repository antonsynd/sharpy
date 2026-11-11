"""scorecard_metrics: the scorecard's offline rows and the both-way baseline ratchet."""

import json
import subprocess
from pathlib import Path

import pytest

from build_tools import scorecard_metrics as sm


def _git(repo: Path, *args: str) -> None:
    subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True)


@pytest.fixture
def repo(tmp_path: Path) -> Path:
    (tmp_path / ".claude/skills/commit").mkdir(parents=True)
    (tmp_path / "CLAUDE.md").write_text("# rules\n")  # 8 bytes
    (tmp_path / ".claude/skills/commit/SKILL.md").write_text("commit\n")  # 7 bytes
    (tmp_path / "src/Sharpy.Compiler/Semantic").mkdir(parents=True)
    (tmp_path / sm.SEMANTIC_INFO).write_text(
        "    private readonly Dictionary<int, int> _a = new();\n"
        "    private readonly ConcurrentDictionary<int, int> _b = new();\n"
        "    private readonly HashSet<int> _c = new();\n"
        "    // private readonly Dictionary<int, int> _d;\n"
    )
    (tmp_path / "src/Sharpy.Compiler.Tests/Semantic").mkdir(parents=True)
    (tmp_path / sm.SEMANTIC_INFO_PIN).write_text(
        "const int MaximumSideTableFieldCount = 2;\n"
    )
    (tmp_path / "src/T").mkdir()
    (tmp_path / "src/T/foo-allowlist.txt").write_text(
        "# header #999\n\nrow one # #10\n  row two #11 #10\n   \n  # indented comment\nlast #12"
    )
    (tmp_path / "src/T/empty-allowlist.txt").write_text("# nothing yet\n")
    (tmp_path / "src/T/name-resolution-differential-allowlist.txt").write_text(
        "a #20\nb\n"
    )
    (tmp_path / "src/T/untracked-allowlist.txt").write_text("never counted\n")
    _git(tmp_path, "init", "-q")
    _git(
        tmp_path,
        "add",
        "CLAUDE.md",
        ".claude",
        "src/Sharpy.Compiler",
        "src/Sharpy.Compiler.Tests",
        "src/T/foo-allowlist.txt",
        "src/T/empty-allowlist.txt",
        "src/T/name-resolution-differential-allowlist.txt",
    )
    return tmp_path


def _baseline(repo: Path, **over) -> Path:
    m = sm.measure(repo)
    data = {k: getattr(m, k) for k in sm.RATCHETED}
    data.update(over)
    path = repo / "baseline.json"
    path.write_text(json.dumps(data))
    return path


def test_measure(repo):
    m = sm.measure(repo)
    assert m.procedure_bytes == 15 and m.claude_md_bytes == 8
    assert m.allowlist_files == 3 and m.allowlist_nonempty_files == 2
    assert m.allowlist_rows_total == 5  # 3 in foo (last line has no newline) + 2 seeded
    assert m.allowlist_rows_seed == 2 and m.allowlist_rows_outside_seed == 3
    assert m.distinct_cites == 4  # #10 #11 #12 #20; the comment's #999 is not a row
    assert m.semanticinfo_dictionaries == 2 and m.semanticinfo_pin == 2


def test_equal_is_green(repo):
    path = _baseline(repo)
    assert sm.compare(sm.measure(repo), sm.load_baseline(path), "b.json") == ([], [])
    assert sm.main(["--check", "--repo", str(repo), "--baseline", str(path)]) == 0


def test_tracked_file_one_byte_larger_is_red(repo):
    # mutation record: one byte added to a tracked procedure file -> red with number, baseline, owner rule
    path = _baseline(repo)
    with open(repo / ".claude/skills/commit/SKILL.md", "a") as f:
        f.write("x")
    problems, notices = sm.compare(sm.measure(repo), sm.load_baseline(path), "b.json")
    assert len(problems) == 1 and notices == []
    msg = problems[0]
    assert "procedure_bytes" in msg and "is 16" in msg and "baseline 15" in msg
    assert "owner decision" in msg and "PR body" in msg
    assert sm.main(["--check", "--repo", str(repo), "--baseline", str(path)]) == 1


def test_claude_md_and_rows_rise_are_red(repo):
    path = _baseline(repo)
    with open(repo / "CLAUDE.md", "a") as f:
        f.write("y")
    with open(repo / "src/T/empty-allowlist.txt", "a") as f:
        f.write("new row #13\n")
    with open(repo / "src/T/name-resolution-differential-allowlist.txt", "a") as f:
        f.write("c #21\n")
    problems, _ = sm.compare(sm.measure(repo), sm.load_baseline(path), "b")
    assert len(problems) == len(sm.RATCHETED)
    assert all(any(f"({k}) is" in p for p in problems) for k in sm.RATCHETED)


def test_semanticinfo_fields_are_reported_not_ratcheted(repo):
    # the C# pin MaximumSideTableFieldCount owns this count; the ratchet must not duplicate it
    path = _baseline(repo)
    assert "semanticinfo_dictionaries" not in sm.RATCHETED
    with open(repo / sm.SEMANTIC_INFO, "a") as f:
        f.write("    private readonly Dictionary<int, int> _e = new();\n")
    m = sm.measure(repo)
    assert m.semanticinfo_dictionaries == 3  # still measured and reported
    assert "2 / 3" in sm.render_row("semanticinfo", m)
    assert sm.compare(m, sm.load_baseline(path), "b") == ([], [])


def test_baseline_with_unknown_key_is_refused(repo):
    path = _baseline(repo, semanticinfo_dictionaries=2)
    with pytest.raises(ValueError, match="unknown keys"):
        sm.load_baseline(path)


def test_seed_harness_rows_have_their_own_ratchet(repo):
    # mutation record: seeded rows above their baseline -> red; outside-seed rows do not move
    path = _baseline(repo)
    with open(repo / "src/T/name-resolution-differential-allowlist.txt", "a") as f:
        f.write("c #21\n")
    m = sm.measure(repo)
    assert m.allowlist_rows_seed == 3 and m.allowlist_rows_outside_seed == 3
    (problem,) = sm.compare(m, sm.load_baseline(path), "b")[0]
    assert "(allowlist_rows_seed) is 3" in problem


def test_seed_entry_matches_the_exact_basename_only(repo):
    # mutation record: a substring match makes this file a seed harness -> red
    assert sm.is_seed_harness("src/T/name-resolution-differential-allowlist.txt")
    assert not sm.is_seed_harness(
        "src/T/old-name-resolution-differential-allowlist.txt"
    )
    assert not sm.is_seed_harness("src/T/differential-allowlist.txt")


# The seed list is an owner-ruled list (contract §11 stage 0). To add or remove an entry, change this
# pin in the same reviewed diff, citing the owner's ruling in the PR body.
SEED_PLAN_FILES_PIN = ("name-resolution-differential-allowlist.txt",)


def test_seed_plan_files_are_pinned():
    # mutation record: an entry added to SEED_PLAN_FILES without this pin -> red
    assert sm.SEED_PLAN_FILES == SEED_PLAN_FILES_PIN, (
        "SEED_PLAN_FILES changed: adding a seed harness is an owner-ruled step (contract §11); "
        "update SEED_PLAN_FILES_PIN in the same diff and cite the ruling in the PR body"
    )


def test_each_seed_entry_names_exactly_one_tracked_allowlist():
    names = [Path(rel).name for rel in sm.allowlist_files(sm.REPO_ROOT)]
    for entry in sm.SEED_PLAN_FILES:
        assert names.count(entry) == 1, (
            f"{entry} names {names.count(entry)} tracked allowlists"
        )


def test_procedure_bytes_below_baseline_are_red_until_lowered(repo):
    path = _baseline(repo, procedure_bytes=16, claude_md_bytes=9)
    problems, notices = sm.compare(sm.measure(repo), sm.load_baseline(path), "b.json")
    assert notices == []
    assert len(problems) == 2 and all("Lower the baseline" in p for p in problems)
    assert sm.main(["--check", "--repo", str(repo), "--baseline", str(path)]) == 1
    assert sm.lower(sm.measure(repo), path) == [
        "procedure_bytes: 16 -> 15",
        "claude_md_bytes: 9 -> 8",
    ]
    assert sm.compare(sm.measure(repo), sm.load_baseline(path), "b.json") == ([], [])


def test_allowlist_rows_below_baseline_are_a_notice(repo, capsys):
    # draining rows is routine: a notice, not a failure, and no forced baseline edit
    path = _baseline(repo, allowlist_rows_outside_seed=4)
    problems, notices = sm.compare(sm.measure(repo), sm.load_baseline(path), "b.json")
    assert problems == []
    assert len(notices) == 1 and "allowlist_rows_outside_seed" in notices[0]
    assert sm.main(["--check", "--repo", str(repo), "--baseline", str(path)]) == 0
    assert "NOTICE:" in capsys.readouterr().out
    assert sm.lower(sm.measure(repo), path) == ["allowlist_rows_outside_seed: 4 -> 3"]


def test_lower_never_raises(repo):
    path = _baseline(repo)
    with open(repo / "CLAUDE.md", "a") as f:
        f.write("z")
    assert sm.lower(sm.measure(repo), path) == []
    assert json.loads(path.read_text())["claude_md_bytes"] == 8


def test_set_baseline_raises_only_named_keys_loudly(repo, capsys):
    path = _baseline(repo, procedure_bytes=14, allowlist_rows_outside_seed=2)
    assert sm.main(["--check", "--repo", str(repo), "--baseline", str(path)]) == 1
    assert sm.main(["--lower", "--repo", str(repo), "--baseline", str(path)]) == 0
    assert json.loads(path.read_text())["procedure_bytes"] == 14  # --lower never raises
    rc = sm.main(
        [
            "--set-baseline",
            "allowlist_rows_outside_seed",
            "--repo",
            str(repo),
            "--baseline",
            str(path),
        ]
    )
    out = capsys.readouterr().out
    assert rc == 0 and "RAISED allowlist_rows_outside_seed: 2 -> 3 (+1)" in out
    assert "owner decision" in out and "PR body" in out
    data = json.loads(path.read_text())
    assert (
        data["allowlist_rows_outside_seed"] == 3 and data["procedure_bytes"] == 14
    )  # unnamed key kept
    with pytest.raises(SystemExit):  # the key must be named and ratcheted
        sm.main(["--set-baseline", "semanticinfo_dictionaries", "--repo", str(repo)])


def test_archive_extraction_is_measured_like_a_checkout(repo, tmp_path_factory):
    # `git archive <sha> | tar -x` has no .git: the walk must find the same tracked allowlists
    out = tmp_path_factory.mktemp("tree")
    _git(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "x")
    subprocess.run(
        f"git -C {repo} archive HEAD | tar -x -C {out}", shell=True, check=True
    )
    assert sm.allowlist_files(out) == sm.allowlist_files(repo)
    assert sm.measure(out) == sm.measure(repo)


def test_missing_tracked_allowlist_is_a_clear_failure(repo, capsys):
    (repo / "src/T/foo-allowlist.txt").unlink()
    with pytest.raises(sm.MeasureError, match="src/T/foo-allowlist.txt is missing"):
        sm.measure(repo)
    assert sm.main(["--row", "allowlists", "--repo", str(repo)]) == 1
    err = capsys.readouterr().err
    assert "src/T/foo-allowlist.txt" in err and "Traceback" not in err


def test_repository_matches_its_baseline():
    """The ratchet itself: the checkout's values equal build_tools/scorecard-baseline.json."""
    problems, _ = sm.compare(
        sm.measure(sm.REPO_ROOT),
        sm.load_baseline(sm.DEFAULT_BASELINE),
        str(sm.DEFAULT_BASELINE),
    )
    assert problems == [], "\n".join(problems)


def test_scorecard_reads_these_rows_from_here():
    """One definition: the bash scorecard prints these rows through --row, not a second copy."""
    script = (sm.REPO_ROOT / ".claude/scripts/scorecard").read_text()
    for row in ("allowlists", "cites", "semanticinfo", "procedure"):
        assert f"build_tools/scorecard_metrics.py --row {row}" in script
    for forked in ("SEED_PLAN_FILES=", "PROC_FILES=", "Dictionary<"):
        assert forked not in script
