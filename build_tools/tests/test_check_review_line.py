"""Tests for build_tools/check_review_line.py: a code PR into dev carries a current review line.

The table of real lines is copied verbatim from the bodies of merged pull requests into `dev`
(fetched 2026-11-12); each row names its PR. History is faked for the parsing table and real (a
scratch git repository) for the ancestry, staleness and command-line tests.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Set

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_review_line as mod

CODE = ["src/Sharpy.Stdlib/Socket/SocketModule.cs"]


class FakeGit(mod.Git):
    """Every sha in `known` is an ancestor of every head; `after` lists the code commits found."""

    def __init__(
        self,
        known: Optional[Set[str]] = None,
        after: Optional[List[str]] = None,
        everything: bool = True,
    ):
        super().__init__()
        self.known = known or set()
        self.everything = everything
        self.after = after or []
        self.asked: List[tuple] = []

    def is_ancestor(self, ancestor: str, head: str) -> bool:
        return self.everything or ancestor in self.known

    def code_commits_after(self, covered: str, head: str, base: str) -> List[str]:
        self.asked.append((covered, head, base))
        return list(self.after)


# --- Real lines, verbatim -----------------------------------------------------------------------

REAL: Dict[int, tuple] = {
    # PR: (body line(s), head sha, verdict, the review sha that counts, the covered point)
    2629: (
        "Independent review @ b81500193: merge after fixes; 7 applied, 1 info item unchanged; second review @ cad4effbf: merge after fixes; 5 applied; third review @ 4f04dedf1 (the head): merge; 1 applied (the two mutations above), 1 declined (reason below).",
        "4f04dedf1b360a828c42ebff33aa3c509d0a6f6b",
        mod.OK,
        "4f04dedf1",
        "4f04dedf1",
    ),
    2633: (
        "Two independent reviews changed this PR; the tables above describe the first push. What is different at the head:\n"
        "`Independent review @ dee863792: merge after fixes; 5 applied, 0 declined; second review @ bd0cc25eb: merge after fixes; 4 applied, 1 declined (reason below); fixes bd0cc25eb..f2a8526f2 read by the lead`",
        "f2a8526f22bf6b09f6a9274860815ac60b078218",
        mod.OK,
        "bd0cc25eb",
        "f2a8526f2",
    ),
    2634: (
        "Independent review @ e35534ffe: merge after fixes; 2 applied (the missing regression test with its mutation record; a comment that said three paths where there are four), 1 declined (reason below); fixes e35534ffe..22a4b6a13 read by the lead (a test file and one comment word; no change to the script's logic).",
        "22a4b6a133c4c8906a7fb4053f332d059c2f4cd3",
        mod.OK,
        "e35534ffe",
        "22a4b6a13",
    ),
    2626: (
        "`Independent review @ 1f9026f9a: merge; 3 applied, 2 declined (reasons below); fixes 1f9026f9a..1ff07e593 read by the lead`",
        "1ff07e5939638e7e801f27042e9563b60e891ca9",
        mod.OK,
        "1f9026f9a",
        "1ff07e593",
    ),
    2628: (
        "`Independent review @ 0e3404b34: merge after fixes; 5 applied, 0 declined; fixes 0e3404b34..0351f988b read by the lead`",
        "0351f988b59b4a68cdca56781784c64262c55977",
        mod.OK,
        "0e3404b34",
        "0351f988b",
    ),
    2627: (
        "Independent review @ b1d47625a: merge after fixes; 5 applied, 1 declined (reason below); second review @ a184dd64f: merge; 0 applied, 4 declined (reasons below); the second review read the fix commit b1d47625a..a184dd64f itself.",
        "a184dd64f2890c81bdfd8d794674325d34931978",
        mod.OK,
        "a184dd64f",
        "a184dd64f",
    ),
    2622: (
        "Independent review @ 54cfdad3a: merge after fixes; 1 applied (the fork of an existing plan branch), 3 low items folded into the rewrite or this body; second review @ dd6ad4975: merge after fixes; 2 applied (the stray commit lost when the branch is new; fast-forward a stale local branch), 1 declined (reason below); fixes dd6ad4975..7ea1bcbf6 read by the lead (one sentence of skill text, the merge of `dev`, and the re-taken figure).",
        "7ea1bcbf61fc731eb8160f9e7bf56a8eea476fba",
        mod.OK,
        "dd6ad4975",
        "7ea1bcbf6",
    ),
    2623: (
        "Independent review @ ac3e7fbc0: merge after fixes; 3 applied, 0 declined; second review @ 4cc416b83: merge, no new findings; fixes ac3e7fbc0..4cc416b83 read by the lead. (The first review's fourth item, that the raise is the owner's call, is answered above.)",
        "4cc416b834ae3eca897d6c7b6ee2dec692dd67e4",
        mod.OK,
        "4cc416b83",
        "4cc416b83",
    ),
    2624: (
        "Independent review @ 38c5c5a92: merge after fixes; 6 applied, 0 declined; second review @ a714a5b07: merge; 0 applied, 3 declined (reasons below); fixes 38c5c5a92..a714a5b07 read by the lead.",
        "a714a5b0730c12e8b0b9293423dfbd1b56ae921b",
        mod.OK,
        "a714a5b07",
        "a714a5b07",
    ),
    # The rule quoted as a `>` block mid-line is not a line; the backticked line after it is.
    2620: (
        "> A fix round needs a second review if it adds an abstraction or guard, changes a public surface, or exceeds 100 changed lines outside tests, fixtures, snapshots and generated files; so does a `/verify-implementation` remediation commit that adds code. When the last round is settled, write the body line `Independent review @ <sha>: <verdict>; <n> applied, <m> declined (reasons below); fixes <sha>..<head> read by the lead`; until then the PR does not merge. Stale when code other than its fixes or a `dev` merge landed after its sha, or the fixes needed a second review, whose sha then heads the line.\n"
        "`Independent review @ 23dfac9ee: merge after fixes; 5 applied, 2 declined (reasons below); fixes 23dfac9ee..46b5b0fe3 read by the lead`",
        "46b5b0fe3fbb0000b9593a5f2529951f3150c550",
        mod.OK,
        "23dfac9ee",
        "46b5b0fe3",
    ),
    # Older spellings (before #2620 merged), reported as they are.
    2604: (
        "### Review round (independent review @ 789d7ad16: merge after fixes)\n"
        "### Second review round (independent review @ 0065fc1d5: merge after fixes)\n"
        "Independent review @ 789d7ad16: merge after fixes; 8 applied, 0 declined\n"
        "Independent review @ 0065fc1d5: merge after fixes; 6 applied, 0 declined\n"
        "Independent review @ 959c1e183: merge; 0 blocking, 5 notes filed as a follow-up issue",
        "959c1e183684f5ca0e44dd69eedb1c27cfe70742",
        mod.OK,
        "959c1e183",
        "959c1e183",
    ),
    2610: (
        "Independent review @ 7d57470b7: merge; 0 blocking, 3 optional notes (1 recorded on #2607, 2 declined as out of scope)",
        "7d57470b7cf0f37191e0b6ddb2eee7105627d925",
        mod.OK,
        "7d57470b7",
        "7d57470b7",
    ),
    # A prose list of fixes is not a range: the covered point is the review sha (stale on real history).
    2572: (
        "`Independent review @ 24a19d2ea: merge after fixes; 7 applied, 0 declined` — the fixes are 44edb9f18 and dcf363ef2, checked against the finding list and the mutation records by the lead.",
        "dcf363ef2037aebc62fe41d727d130c2eef56b8c",
        mod.OK,
        "24a19d2ea",
        "24a19d2ea",
    ),
    # Merged on 2026-11-11 with no line (src/ and build_tools/).
    2615: (
        "## Summary\n- `socket.socket(...)` leaked a raw `System.Net.Sockets.SocketException` when the host can't create the socket (e.g. `AF_INET6` with no IPv6 stack). CPython raises `OSError`; the ctor now raises `socket.error` via `error.from_socket_exception`.",
        "2511fccab281418a5424c17d210259d0255a56d0",
        mod.MISSING,
        None,
        None,
    ),
    2612: (
        "## Summary\n- `setup_env.sh` now passes `--azure-feed https://builds.dotnet.microsoft.com/dotnet` to `dotnet-install.sh`.",
        "4981a3e8a9e2362300f72c284a6bc81e6119b78e",
        mod.MISSING,
        None,
        None,
    ),
    # The rule mentioned in a list item, with a placeholder sha, is not a line.
    2586: (
        '- A PR that changes code also needs a current `Independent review @ <sha>` line whose verdict is not `do not merge`. "Changes code" means the diff touches `src/`, `build_tools/`, `.claude/scripts/`, `.claude/hooks/`, `.github/` or `docs/language_specification/`; anything else needs green alone.',
        "0383b31ab5bba5ab792276069da27e8537a86a25",
        mod.MISSING,
        None,
        None,
    ),
}


@pytest.mark.parametrize("pr", sorted(REAL), ids=lambda n: f"PR{n}")
def test_real_lines(pr: int) -> None:
    body, head, verdict, review, covered = REAL[pr]
    git = FakeGit()
    result = mod.judge(body, CODE, head, "b" * 40, git)
    assert result.verdict == verdict, result.detail
    if review:
        assert mod.parse_body(body).reviews[-1].sha == review
        assert git.asked == [(covered, head, "b" * 40)]


# --- Exemptions ---------------------------------------------------------------------------------


def test_no_code_path_is_not_required() -> None:
    result = mod.judge(
        "",
        ["docs/process/README.md", ".claude/skills/x/SKILL.md", "CLAUDE.md"],
        "h",
        "b",
        FakeGit(),
    )
    assert result.verdict == mod.NOT_REQUIRED


def test_a_sync_with_no_changed_file_is_not_required() -> None:
    # PRs #2618 and #2632 (mainline into dev) changed no file.
    assert mod.judge("", [], "h", "b", FakeGit()).verdict == mod.NOT_REQUIRED


@pytest.mark.parametrize(
    "path, code",
    [
        ("src/a.cs", True),
        ("build_tools/x.py", True),
        (".claude/scripts/ci-gate", True),
        (".claude/hooks/h.sh", True),
        (".github/workflows/x.yml", True),
        ("docs/language_specification/a.md", True),
        # Neighbours that are not code paths.
        (".claude/skills/a/SKILL.md", False),
        (".claude/settings.json", False),
        ("docs/language_specification_notes.md", False),
        ("docs/process/README.md", False),
        ("srcx/a.cs", False),
        ("a/src/b.cs", False),
        ("build_tools", False),
        ("README.md", False),
    ],
)
def test_code_path_classifier(path: str, code: bool) -> None:
    assert mod.is_code_path(path) is code


def test_base_mainline_is_not_required() -> None:
    result = mod.judge(
        "", CODE, "h", "b", FakeGit(), base_ref="mainline", head_ref="dev"
    )
    assert result.verdict == mod.NOT_REQUIRED and "mainline" in result.detail


def test_head_dev_is_not_required() -> None:
    # A head of `dev` with a base of `dev` cannot exist on GitHub; judged on the head alone here.
    result = mod.judge("", CODE, "h", "b", FakeGit(), base_ref="dev", head_ref="dev")
    assert result.verdict == mod.NOT_REQUIRED and "promotion" in result.detail


def test_a_stacked_pr_into_a_lane_is_not_required() -> None:
    assert (
        mod.judge("", CODE, "h", "b", FakeGit(), base_ref="claude/x").verdict
        == mod.NOT_REQUIRED
    )


@pytest.mark.parametrize("author", ["dependabot[bot]", "github-actions[bot]"])
def test_bot_authors_are_not_required(author: str) -> None:
    assert (
        mod.judge("", CODE, "h", "b", FakeGit(), author=author).verdict
        == mod.NOT_REQUIRED
    )


@pytest.mark.parametrize("author", ["antonsynd", "bot", "botanist", "x[bot]y"])
def test_people_are_judged(author: str) -> None:
    # The positive control for the bot exemption: only a login ending in `[bot]` is exempt.
    assert (
        mod.judge("", CODE, "h", "b", FakeGit(), author=author).verdict == mod.MISSING
    )


# --- Synthetic negatives ------------------------------------------------------------------------

LINE = "Independent review @ abc1234: merge after fixes; 2 applied, 0 declined (reasons below); fixes abc1234..def5678 read by the lead"


@pytest.mark.parametrize(
    "body, verdict",
    [
        (LINE, mod.OK),
        (
            "Independent review @ <sha>: <verdict>; <n> applied, <m> declined",
            mod.MALFORMED,
        ),
        ("Independent review @ abc1234: lgtm", mod.MALFORMED),
        ("Independent review @ abc1234: merge after fix; 1 applied", mod.MALFORMED),
        ("Independent review @ abc1234 merge; 1 applied", mod.MALFORMED),
        ("Independent review @ abc12: merge", mod.MALFORMED),
        ("Independent review @ abc1234: do not merge; 3 blocking", mod.DO_NOT_MERGE),
        # A do-not-merge cleared by a second review is a merge.
        (
            "Independent review @ abc1234: do not merge; second review @ def5678: merge; 2 applied",
            mod.OK,
        ),
        # A second review that says do not merge stands.
        (
            "Independent review @ abc1234: merge; second review @ def5678: do not merge",
            mod.DO_NOT_MERGE,
        ),
        ("independent review abc1234: merge", mod.MISSING),
        ("We will add the Independent review @ line later.", mod.MISSING),
        ("", mod.MISSING),
        # A good earlier line does not rescue a malformed last one.
        (LINE + "\nIndependent review @ TBD: pending", mod.MALFORMED),
    ],
)
def test_synthetic(body: str, verdict: str) -> None:
    assert mod.judge(body, CODE, "h", "b", FakeGit()).verdict == verdict


def test_unknown_reviewed_sha_is_not_an_ancestor() -> None:
    git = FakeGit(known={"def5678"}, everything=False)
    result = mod.judge(LINE, CODE, "h", "b", git)
    assert result.verdict == mod.NOT_ANCESTOR and "abc1234" in result.detail


def test_unknown_fix_end_is_not_an_ancestor() -> None:
    git = FakeGit(known={"abc1234"}, everything=False)
    result = mod.judge(LINE, CODE, "h", "b", git)
    assert result.verdict == mod.NOT_ANCESTOR and "def5678" in result.detail


def test_code_after_the_covered_point_is_stale() -> None:
    result = mod.judge(LINE, CODE, "h", "b", FakeGit(after=["1234567890"]))
    assert result.verdict == mod.STALE and "123456789" in result.detail


def test_counts_are_not_judged() -> None:
    # The counts are free text: `0 blocking, 3 optional notes` (#2610) parses as well as the template.
    assert (
        mod.judge(
            "Independent review @ abc1234: merge; no counts at all",
            CODE,
            "h",
            "b",
            FakeGit(),
        ).verdict
        == mod.OK
    )


# --- Real history: a scratch repository ---------------------------------------------------------


def git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args], check=True, capture_output=True, text=True
    ).stdout.strip()


def commit(repo: Path, path: str, text: str = "x") -> str:
    (repo / path).parent.mkdir(parents=True, exist_ok=True)
    (repo / path).write_text(text)
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", f"edit {path}")
    return git(repo, "rev-parse", "HEAD")


@pytest.fixture
def repo(tmp_path: Path) -> Path:
    r = tmp_path / "repo"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
    commit(r, "README.md", "0")
    return r


def lane(repo: Path) -> Dict[str, str]:
    """dev: base. lane: reviewed (src), fix (src), docs (docs only), then dev moves and is merged in."""
    shas = {"base": git(repo, "rev-parse", "HEAD")}
    git(repo, "switch", "-q", "-c", "lane")
    shas["reviewed"] = commit(repo, "src/a.cs", "1")
    shas["fix"] = commit(repo, "src/a.cs", "2")
    shas["docs"] = commit(repo, "docs/x.md", "1")
    git(repo, "switch", "-q", "dev")
    shas["dev2"] = commit(repo, "src/other.cs", "1")
    git(repo, "switch", "-q", "lane")
    git(repo, "merge", "-q", "--no-edit", "dev")
    shas["head"] = git(repo, "rev-parse", "HEAD")
    git(repo, "switch", "-q", "--detach", shas["base"])
    git(repo, "switch", "-q", "-c", "elsewhere")
    shas["foreign"] = commit(repo, "src/z.cs", "1")
    return shas


def line(reviewed: str, fixes: Optional[str] = None) -> str:
    out = (
        f"Independent review @ {reviewed[:9]}: merge after fixes; 1 applied, 0 declined"
    )
    return out + (
        f"; fixes {reviewed[:9]}..{fixes[:9]} read by the lead" if fixes else ""
    )


def test_history_fix_range_and_a_dev_merge_after_it_is_ok(repo: Path) -> None:
    s = lane(repo)
    g = mod.Git(str(repo))
    # dev2 (code, from dev) lands after the fix through a merge: neither counts.
    assert (
        mod.judge(line(s["reviewed"], s["fix"]), CODE, s["head"], s["dev2"], g).verdict
        == mod.OK
    )


def test_history_code_after_the_review_without_a_range_is_stale(repo: Path) -> None:
    s = lane(repo)
    g = mod.Git(str(repo))
    result = mod.judge(line(s["reviewed"]), CODE, s["head"], s["dev2"], g)
    assert result.verdict == mod.STALE and s["fix"][:9] in result.detail
    # The docs-only commit and the dev commit are not named.
    assert s["docs"][:9] not in result.detail and s["dev2"][:9] not in result.detail


def test_history_review_at_the_last_code_commit_is_ok(repo: Path) -> None:
    s = lane(repo)
    g = mod.Git(str(repo))
    # Only docs and a dev merge follow the fix commit.
    assert mod.judge(line(s["fix"]), CODE, s["head"], s["dev2"], g).verdict == mod.OK


def test_history_a_sha_off_the_head_is_not_an_ancestor(repo: Path) -> None:
    s = lane(repo)
    g = mod.Git(str(repo))
    result = mod.judge(line(s["foreign"]), CODE, s["head"], s["dev2"], g)
    assert result.verdict == mod.NOT_ANCESTOR
    result = mod.judge(line(s["reviewed"], s["foreign"]), CODE, s["head"], s["dev2"], g)
    assert result.verdict == mod.NOT_ANCESTOR
    # A sha this clone does not have.
    assert (
        mod.judge(line("f" * 40), CODE, s["head"], s["dev2"], g).verdict
        == mod.NOT_ANCESTOR
    )


def test_history_is_ancestor_positive_control(repo: Path) -> None:
    s = lane(repo)
    g = mod.Git(str(repo))
    assert g.is_ancestor(s["reviewed"], s["head"]) and g.is_ancestor(
        s["head"], s["head"]
    )
    assert not g.is_ancestor(s["foreign"], s["head"]) and not g.is_ancestor(
        "f" * 40, s["head"]
    )


def run_main(
    repo: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture,
    body: str,
    *args: str,
) -> tuple:
    monkeypatch.setenv("PR_BODY", body)
    monkeypatch.delenv("GITHUB_STEP_SUMMARY", raising=False)
    code = mod.main(list(args), git_factory=lambda: mod.Git(str(repo)))
    return code, capsys.readouterr().out


def test_main_reads_the_body_from_env_and_paths_from_git(
    repo: Path, monkeypatch, capsys
) -> None:
    s = lane(repo)
    code, out = run_main(
        repo,
        monkeypatch,
        capsys,
        line(s["reviewed"], s["fix"]),
        "--base",
        s["dev2"],
        "--head",
        s["head"],
    )
    assert code == 0 and "review-line: ok" in out
    code, out = run_main(
        repo, monkeypatch, capsys, "no line", "--base", s["dev2"], "--head", s["head"]
    )
    assert code == 1 and "::error::review-line: missing" in out


def test_main_a_draft_warns_but_passes(repo: Path, monkeypatch, capsys) -> None:
    s = lane(repo)
    code, out = run_main(
        repo,
        monkeypatch,
        capsys,
        "",
        "--base",
        s["dev2"],
        "--head",
        s["head"],
        "--draft",
    )
    assert code == 0 and "::warning::review-line: missing" in out


def test_main_docs_only_range_is_not_required(repo: Path, monkeypatch, capsys) -> None:
    s = lane(repo)
    code, out = run_main(
        repo, monkeypatch, capsys, "", "--base", s["fix"], "--head", s["docs"]
    )
    assert code == 0 and "not required" in out


def test_main_git_failure_is_exit_2(repo: Path, monkeypatch, capsys) -> None:
    code, _ = run_main(
        repo, monkeypatch, capsys, "", "--base", "nope", "--head", "HEAD"
    )
    assert code == 2


def test_main_writes_the_step_summary(
    repo: Path, monkeypatch, capsys, tmp_path: Path
) -> None:
    s = lane(repo)
    summary = tmp_path / "summary.md"
    monkeypatch.setenv("PR_BODY", "")
    monkeypatch.setenv("GITHUB_STEP_SUMMARY", str(summary))
    mod.main(
        ["--base", s["dev2"], "--head", s["head"]],
        git_factory=lambda: mod.Git(str(repo)),
    )
    assert "review-line: missing" in summary.read_text()
