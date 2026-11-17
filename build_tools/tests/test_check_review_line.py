"""Tests for build_tools/check_review_line.py: a code PR into dev carries a current review line.

The table of real lines is copied verbatim from the bodies of merged pull requests into `dev`
(fetched 2026-11-12); each row names its PR. Parsing is checked on the text alone; the verdict on
real history is checked against this clone (base = the PR's merge commit's first parent, as `dev`
stood when it merged) and skipped where the clone lacks those commits. Every other verdict is
checked on a scratch git repository: nothing about ancestry or staleness is faked.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional

import pytest
import yaml

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_review_line as mod

REPO = Path(__file__).resolve().parents[2]

# --- Real lines, verbatim -----------------------------------------------------------------------

# PR: (body, merge commit, head, last review sha, its verdict, fix range or None, verdict on real history)
REAL: Dict[int, tuple] = {
    2629: (
        "Independent review @ b81500193: merge after fixes; 7 applied, 1 info item unchanged; second review @ cad4effbf: merge after fixes; 5 applied; third review @ 4f04dedf1 (the head): merge; 1 applied (the two mutations above), 1 declined (reason below).",
        "6e98f96072e8e90af208dbd8838b4f833cffe681",
        "4f04dedf1b360a828c42ebff33aa3c509d0a6f6b",
        "4f04dedf1",
        "merge",
        None,
        mod.OK,
    ),
    2633: (
        "Two independent reviews changed this PR; the tables above describe the first push. What is different at the head:\n"
        "`Independent review @ dee863792: merge after fixes; 5 applied, 0 declined; second review @ bd0cc25eb: merge after fixes; 4 applied, 1 declined (reason below); fixes bd0cc25eb..f2a8526f2 read by the lead`",
        "23279fa60dab50172175105937ca66a935b0c104",
        "f2a8526f22bf6b09f6a9274860815ac60b078218",
        "bd0cc25eb",
        "merge after fixes",
        ("bd0cc25eb", "f2a8526f2"),
        mod.OK,
    ),
    2634: (
        "Independent review @ e35534ffe: merge after fixes; 2 applied (the missing regression test with its mutation record; a comment that said three paths where there are four), 1 declined (reason below); fixes e35534ffe..22a4b6a13 read by the lead (a test file and one comment word; no change to the script's logic).",
        "89370d8195dfb70831dc0be2f880616185cabb23",
        "22a4b6a133c4c8906a7fb4053f332d059c2f4cd3",
        "e35534ffe",
        "merge after fixes",
        ("e35534ffe", "22a4b6a13"),
        mod.OK,
    ),
    2626: (
        "`Independent review @ 1f9026f9a: merge; 3 applied, 2 declined (reasons below); fixes 1f9026f9a..1ff07e593 read by the lead`",
        "ec5cc378b07a522aa4dfd51d42025c2ab850c7df",
        "1ff07e5939638e7e801f27042e9563b60e891ca9",
        "1f9026f9a",
        "merge",
        ("1f9026f9a", "1ff07e593"),
        mod.OK,
    ),
    2628: (
        "`Independent review @ 0e3404b34: merge after fixes; 5 applied, 0 declined; fixes 0e3404b34..0351f988b read by the lead`",
        "978b3d47f31e98ad1af974a72c6ab206f5bcff8f",
        "0351f988b59b4a68cdca56781784c64262c55977",
        "0e3404b34",
        "merge after fixes",
        ("0e3404b34", "0351f988b"),
        mod.OK,
    ),
    # The range starts at the FIRST review's sha and ends at the second review's: still a valid range.
    2627: (
        "Independent review @ b1d47625a: merge after fixes; 5 applied, 1 declined (reason below); second review @ a184dd64f: merge; 0 applied, 4 declined (reasons below); the second review read the fix commit b1d47625a..a184dd64f itself.",
        "2d04fc0820f149841220bef0b19d9fa351f550aa",
        "a184dd64f2890c81bdfd8d794674325d34931978",
        "a184dd64f",
        "merge",
        ("b1d47625a", "a184dd64f"),
        mod.OK,
    ),
    2622: (
        "Independent review @ 54cfdad3a: merge after fixes; 1 applied (the fork of an existing plan branch), 3 low items folded into the rewrite or this body; second review @ dd6ad4975: merge after fixes; 2 applied (the stray commit lost when the branch is new; fast-forward a stale local branch), 1 declined (reason below); fixes dd6ad4975..7ea1bcbf6 read by the lead (one sentence of skill text, the merge of `dev`, and the re-taken figure).",
        "eabb480c618f324c4e702659e0258b2d52288ae0",
        "7ea1bcbf61fc731eb8160f9e7bf56a8eea476fba",
        "dd6ad4975",
        "merge after fixes",
        ("dd6ad4975", "7ea1bcbf6"),
        mod.OK,
    ),
    2623: (
        "Independent review @ ac3e7fbc0: merge after fixes; 3 applied, 0 declined; second review @ 4cc416b83: merge, no new findings; fixes ac3e7fbc0..4cc416b83 read by the lead. (The first review's fourth item, that the raise is the owner's call, is answered above.)",
        "e4e32dae8f4bcbc25a28adfe0bf741366696141b",
        "4cc416b834ae3eca897d6c7b6ee2dec692dd67e4",
        "4cc416b83",
        "merge",
        ("ac3e7fbc0", "4cc416b83"),
        mod.OK,
    ),
    2624: (
        "Independent review @ 38c5c5a92: merge after fixes; 6 applied, 0 declined; second review @ a714a5b07: merge; 0 applied, 3 declined (reasons below); fixes 38c5c5a92..a714a5b07 read by the lead.",
        "7573c9cbda39f2299c31540b8dc752ab3bf63fcb",
        "a714a5b0730c12e8b0b9293423dfbd1b56ae921b",
        "a714a5b07",
        "merge",
        ("38c5c5a92", "a714a5b07"),
        mod.OK,
    ),
    # The rule quoted in a blockquote is not a line; the backticked line after it is. #2620 changed
    # no code path, so on real history it is not required.
    2620: (
        "> A fix round needs a second review if it adds an abstraction or guard, changes a public surface, or exceeds 100 changed lines outside tests, fixtures, snapshots and generated files; so does a `/verify-implementation` remediation commit that adds code. When the last round is settled, write the body line `Independent review @ <sha>: <verdict>; <n> applied, <m> declined (reasons below); fixes <sha>..<head> read by the lead`; until then the PR does not merge. Stale when code other than its fixes or a `dev` merge landed after its sha, or the fixes needed a second review, whose sha then heads the line.\n"
        "`Independent review @ 23dfac9ee: merge after fixes; 5 applied, 2 declined (reasons below); fixes 23dfac9ee..46b5b0fe3 read by the lead`",
        "bd77d6da8f8d4fbf5a374fdbdcfe5f27f063f707",
        "46b5b0fe3fbb0000b9593a5f2529951f3150c550",
        "23dfac9ee",
        "merge after fixes",
        ("23dfac9ee", "46b5b0fe3"),
        mod.NOT_REQUIRED,
    ),
    # Older spellings (before #2620 merged), reported as they are.
    2604: (
        "### Review round (independent review @ 789d7ad16: merge after fixes)\n"
        "### Second review round (independent review @ 0065fc1d5: merge after fixes)\n"
        "Independent review @ 789d7ad16: merge after fixes; 8 applied, 0 declined\n"
        "Independent review @ 0065fc1d5: merge after fixes; 6 applied, 0 declined\n"
        "Independent review @ 959c1e183: merge; 0 blocking, 5 notes filed as a follow-up issue",
        "5a9c3cab572327d891714361e07b219e60061276",
        "959c1e183684f5ca0e44dd69eedb1c27cfe70742",
        "959c1e183",
        "merge",
        None,
        mod.OK,
    ),
    2610: (
        "Independent review @ 7d57470b7: merge; 0 blocking, 3 optional notes (1 recorded on #2607, 2 declined as out of scope)",
        "56c3f3c163fb45126f3a8175f12955143cf186c2",
        "7d57470b7cf0f37191e0b6ddb2eee7105627d925",
        "7d57470b7",
        "merge",
        None,
        mod.OK,
    ),
    # A prose list of fixes is not a range: the covered point is the review sha, and code followed it.
    2572: (
        "`Independent review @ 24a19d2ea: merge after fixes; 7 applied, 0 declined` — the fixes are 44edb9f18 and dcf363ef2, checked against the finding list and the mutation records by the lead.",
        "b7f492c291302bc89c582af3f675fd3d413ca82f",
        "dcf363ef2037aebc62fe41d727d130c2eef56b8c",
        "24a19d2ea",
        "merge after fixes",
        None,
        mod.STALE,
    ),
    # Merged on 2026-11-11 with no line (src/ and build_tools/).
    2615: (
        "## Summary\n- `socket.socket(...)` leaked a raw `System.Net.Sockets.SocketException` when the host can't create the socket (e.g. `AF_INET6` with no IPv6 stack). CPython raises `OSError`; the ctor now raises `socket.error` via `error.from_socket_exception`.",
        "8ccb3020336b19c239f6025476eac8f24f431f77",
        "2511fccab281418a5424c17d210259d0255a56d0",
        None,
        None,
        None,
        mod.MISSING,
    ),
    2612: (
        "## Summary\n- `setup_env.sh` now passes `--azure-feed https://builds.dotnet.microsoft.com/dotnet` to `dotnet-install.sh`.",
        "52666dfcbc49de7abb20ca30a75fc738c2a10797",
        "4981a3e8a9e2362300f72c284a6bc81e6119b78e",
        None,
        None,
        None,
        mod.MISSING,
    ),
    # The rule mentioned in a list item, with a placeholder sha, is not a line (#2586 changed no code path).
    2586: (
        '- A PR that changes code also needs a current `Independent review @ <sha>` line whose verdict is not `do not merge`. "Changes code" means the diff touches `src/`, `build_tools/`, `.claude/scripts/`, `.claude/hooks/`, `.github/` or `docs/language_specification/`; anything else needs green alone.',
        "cbd7f89d8ad0fba38508b7bc91eb02cab7b7785e",
        "0383b31ab5bba5ab792276069da27e8537a86a25",
        None,
        None,
        None,
        mod.NOT_REQUIRED,
    ),
}


@pytest.mark.parametrize("pr", sorted(REAL), ids=lambda n: f"PR{n}")
def test_real_lines_parse(pr: int) -> None:
    body, _, _, review, verdict, fix_range, _ = REAL[pr]
    parsed = mod.parse_body(body)
    if review is None:
        assert parsed.lines == []
        return
    assert parsed.last_line_parses
    assert (parsed.reviews[-1].sha, parsed.reviews[-1].verdict) == (review, verdict)
    assert parsed.fix_range == fix_range


def _has(sha: str) -> bool:
    return (
        subprocess.run(
            ["git", "-C", str(REPO), "cat-file", "-e", f"{sha}^{{commit}}"],
            capture_output=True,
        ).returncode
        == 0
    )


@pytest.mark.parametrize("pr", sorted(REAL), ids=lambda n: f"PR{n}")
def test_real_lines_on_real_history(pr: int) -> None:
    body, merge, head, _, _, _, expected = REAL[pr]
    if not (_has(merge) and _has(head)):
        pytest.skip("this clone lacks the PR's commits")
    git = mod.Git(str(REPO))
    base = subprocess.run(
        ["git", "-C", str(REPO), "rev-parse", f"{merge}^1"],
        capture_output=True,
        text=True,
        check=True,
    ).stdout.strip()
    result = mod.judge(body, git.changed_paths(base, head), head, base, git)
    assert result.verdict == expected, result.detail


# --- Parsing ------------------------------------------------------------------------------------

LINE = "Independent review @ abc1234: merge after fixes; 2 applied, 0 declined (reasons below); fixes abc1234..def5678 read by the lead"


@pytest.mark.parametrize(
    "body, parses",
    [
        (LINE, True),
        ("Independent review @ <sha>: <verdict>; <n> applied, <m> declined", False),
        ("Independent review @ abc1234: lgtm", False),
        ("Independent review @ abc1234: merge after fix; 1 applied", False),
        ("Independent review @ abc1234 merge; 1 applied", False),
        ("Independent review @ abc12: merge", False),
        ("Independent review @ abc1234: merge-after-fixes", False),
        ("Independent review @ abc1234: merge after fixes 2 applied", False),
        ("Independent review @ abc1234: merge? no - do not merge", False),
        # The first clause must parse; a later clause does not rescue it.
        (
            "Independent review @ head pending; earlier note: second review @ abc1234: merge",
            False,
        ),
        # Every `review @` on a counted line must parse.
        ("Independent review @ abc1234: merge; second review @ TBD: merge", False),
        ("Independent review @ abc1234: **merge**; 0 applied", True),
        ("Independent review @ ABC1234: MERGE — 0 blocking", True),
        ("- **Independent review @ abc1234: merge**", True),
        ("Independent review @ abc1234: merge. Do not merge until #1 lands", True),
    ],
)
def test_line_grammar(body: str, parses: bool) -> None:
    parsed = mod.parse_body(body)
    assert parsed.lines and parsed.last_line_parses is parses


@pytest.mark.parametrize(
    "body",
    [
        "Body\n```\n" + LINE + "\n```",
        "Body\n~~~~\n" + LINE + "\n~~~~",
        "Body\n<!--\n" + LINE + "\n-->",
        "Body <!-- " + LINE + " -->",
        "Body\n<!-- never closed\n" + LINE,
        "Body\n\n    " + LINE,
        "Body\n\n\t" + LINE,
        "> " + LINE,
        "   > " + LINE,
        "We did not get an " + LINE,
        "",
        None,
        "Indepеndent review @ abc1234: merge",
    ],
)
def test_lines_that_do_not_count(body: Optional[str]) -> None:
    assert mod.parse_body(body).lines == []


def test_a_good_earlier_line_does_not_rescue_a_malformed_last_one() -> None:
    parsed = mod.parse_body(LINE + "\nIndependent review @ TBD: pending")
    assert parsed.lines and not parsed.last_line_parses


def test_crlf_body() -> None:
    parsed = mod.parse_body("x\r\n" + LINE + "\r\n")
    assert parsed.last_line_parses and parsed.fix_range == ("abc1234", "def5678")


@pytest.mark.parametrize(
    "path, code",
    [
        ("src/a.cs", True),
        ("build_tools/x.py", True),
        (".claude/scripts/ci-gate", True),
        (".claude/hooks/h.sh", True),
        (".github/workflows/x.yml", True),
        ("docs/language_specification/a.md", True),
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


# --- Scratch history ----------------------------------------------------------------------------


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
def h(tmp_path: Path) -> Dict[str, str]:
    """dev: D0. lane: L1, L2 (reviewed), F1 (fix), DOC (docs only). dev moves to D1; the lane
    merges it cleanly (M). dev moves to D2; an evil merge of it also edits src (EV). Off D0:
    O1, an unrelated branch."""
    r = tmp_path / "repo"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
    # Hermetic: a host-wide commit.gpgsign (or a signing helper) must not decide these tests.
    git(r, "config", "commit.gpgsign", "false")
    s = {"repo": str(r), "D0": commit(r, "src/a.cs", "0")}
    git(r, "switch", "-q", "-c", "lane")
    s["L1"] = commit(r, "src/b.cs", "1")
    s["L2"] = commit(r, "src/b.cs", "2")
    s["F1"] = commit(r, "src/b.cs", "3")
    s["DOC"] = commit(r, "docs/x.md", "1")
    git(r, "switch", "-q", "dev")
    s["D1"] = commit(r, "src/c.cs", "1")
    git(r, "switch", "-q", "lane")
    git(r, "merge", "-q", "--no-edit", "dev")
    s["M"] = git(r, "rev-parse", "HEAD")
    git(r, "switch", "-q", "dev")
    s["D2"] = commit(r, "src/d.cs", "1")
    git(r, "switch", "-q", "-c", "evil", s["M"])
    git(r, "merge", "-q", "--no-commit", "dev")
    (r / "src/b.cs").write_text("evil")
    git(r, "add", "-A")
    git(r, "commit", "-q", "-m", "Merge dev")
    s["EV"] = git(r, "rev-parse", "HEAD")
    git(r, "switch", "-q", "-c", "other", s["D0"])
    s["O1"] = commit(r, "src/x.cs", "1")
    return s


def j(s: Dict[str, str], body: str, head: str, base: str) -> mod.Result:
    g = mod.Git(s["repo"])
    return mod.judge(body, g.changed_paths(base, head), head, base, g)


def rl(
    review: str, fixes: Optional[tuple] = None, verdict: str = "merge after fixes"
) -> str:
    out = f"Independent review @ {review[:9]}: {verdict}; 1 applied, 0 declined"
    return out + (
        f"; fixes {fixes[0][:9]}..{fixes[1][:9]} read by the lead" if fixes else ""
    )


def test_good_line_with_a_fix_range_is_ok(h) -> None:
    assert j(h, rl(h["L2"], (h["L2"], h["F1"])), h["F1"], h["D0"]).verdict == mod.OK


def test_code_after_the_review_without_a_range_is_stale(h) -> None:
    result = j(h, rl(h["L2"]), h["F1"], h["D0"])
    assert (
        result.verdict == mod.STALE
        and h["F1"][:9] in result.detail
        and "empty diff" in result.detail
    )


def test_docs_only_after_the_covered_point_is_ok(h) -> None:
    assert j(h, rl(h["L2"], (h["L2"], h["F1"])), h["DOC"], h["D0"]).verdict == mod.OK


def test_a_clean_dev_merge_after_the_review_is_ok_against_the_moved_base(h) -> None:
    # The base is dev's tip now (D1), not the payload's frozen D0.
    assert j(h, rl(h["L2"], (h["L2"], h["F1"])), h["M"], h["D1"]).verdict == mod.OK


def test_the_frozen_payload_base_reads_dev_commits_as_stale(h) -> None:
    # Why the workflow passes origin/dev: against D0, dev's own D1 looks like code after the review.
    result = j(h, rl(h["L2"], (h["L2"], h["F1"])), h["M"], h["D0"])
    assert result.verdict == mod.STALE and h["D1"][:9] in result.detail


def test_an_evil_merge_after_the_review_is_stale(h) -> None:
    result = j(h, rl(h["L2"], (h["L2"], h["F1"])), h["EV"], h["D2"])
    assert result.verdict == mod.STALE and h["EV"][:9] in result.detail
    # Its first-parent twin, the clean merge, is not.
    assert h["M"][:9] not in result.detail


def test_review_of_the_base_tip_is_not_ok(h) -> None:
    assert j(h, rl(h["D0"]), h["F1"], h["D0"]).verdict == mod.ON_BASE
    assert (
        j(h, rl(h["D0"], (h["D0"], h["F1"])), h["F1"], h["D0"]).verdict == mod.ON_BASE
    )


@pytest.mark.parametrize("start", ["0000000", "O1", "D0", "F1"])
def test_a_fix_range_that_does_not_follow_a_review_is_bad(h, start: str) -> None:
    # Unknown, unrelated branch, base tip, and a start after the end.
    s = h.get(start, start)
    result = j(
        h, rl(h["L2"], (s, h["L2"] if start == "F1" else h["F1"])), h["F1"], h["D0"]
    )
    assert result.verdict == mod.BAD_RANGE, result.detail


def test_a_fix_range_starting_before_every_review_is_bad(h) -> None:
    # On the lane, after the base, an ancestor of the end: only "follows a review" rejects it.
    result = j(h, rl(h["L2"], (h["L1"], h["F1"])), h["F1"], h["D0"])
    assert result.verdict == mod.BAD_RANGE, result.detail


def test_a_range_from_the_first_review_ending_at_the_second_is_ok(h) -> None:
    # #2627's shape: `second review @ F1 ... read the fix commit L2..F1`.
    body = f"Independent review @ {h['L2'][:9]}: merge after fixes; second review @ {h['F1'][:9]}: merge; the second review read the fix commit {h['L2'][:9]}..{h['F1'][:9]} itself."
    assert j(h, body, h["F1"], h["D0"]).verdict == mod.OK


def test_a_range_older_than_the_last_review_covers_up_to_that_review(h) -> None:
    # covered = the later of (last review, range end): here the review at F1, so nothing follows.
    body = f"Independent review @ {h['L1'][:9]}: merge after fixes; fixes {h['L1'][:9]}..{h['L2'][:9]} read by the lead; second review @ {h['F1'][:9]}: merge"
    assert j(h, body, h["F1"], h["D0"]).verdict == mod.OK


@pytest.mark.parametrize("sha", ["O1", "fff"])
def test_a_reviewed_sha_off_the_head_is_not_an_ancestor(h, sha: str) -> None:
    s = h.get(sha, "f" * 40)
    result = j(h, rl(s), h["F1"], h["D0"])
    assert result.verdict == mod.NOT_ANCESTOR and "empty diff" in result.detail


def test_a_fix_end_off_the_head_is_not_an_ancestor(h) -> None:
    assert (
        j(h, rl(h["L2"], (h["L2"], h["O1"])), h["F1"], h["D0"]).verdict
        == mod.NOT_ANCESTOR
    )


def test_is_ancestor_positive_control(h) -> None:
    g = mod.Git(h["repo"])
    assert g.is_ancestor(h["L2"], h["F1"]) and g.is_ancestor(h["F1"], h["F1"])
    assert not g.is_ancestor(h["O1"], h["F1"]) and not g.is_ancestor("f" * 40, h["F1"])


def test_quoted_and_fenced_lines_do_not_count_on_history(h) -> None:
    head, base = h["F1"], h["D0"]
    good = rl(h["L2"], (h["L2"], h["F1"]), verdict="merge")
    for body in (
        f"> {good}",
        f"```\n{good}\n```",
        f"<!--\n{good}\n-->",
        f"\n    {good}",
    ):
        assert j(h, body, head, base).verdict == mod.MISSING, body
    # A real `do not merge` is not undone by a quoted or fenced example after it.
    dnm = f"Independent review @ {h['F1'][:9]}: do not merge; 3 blocking"
    assert j(h, f"{dnm}\n> {good}", head, base).verdict == mod.DO_NOT_MERGE
    assert j(h, f"{dnm}\n```\n{good}\n```", head, base).verdict == mod.DO_NOT_MERGE
    # A later review on the line clears it only when it judges a later commit (see below).
    assert (
        j(h, f"{dnm}; second review @ {h['F1'][:9]}: merge", head, base).verdict
        == mod.DO_NOT_MERGE
    )


def test_a_code_file_renamed_out_of_a_code_path_is_a_code_change(
    tmp_path: Path,
) -> None:
    r = tmp_path / "r"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
    # Hermetic: a host-wide commit.gpgsign (or a signing helper) must not decide these tests.
    git(r, "config", "commit.gpgsign", "false")
    base = commit(r, "src/big.cs", "\n".join(f"line {i}" for i in range(200)))
    (r / "tools").mkdir()
    git(r, "mv", "src/big.cs", "tools/big.cs")
    git(r, "commit", "-q", "-m", "move")
    head = git(r, "rev-parse", "HEAD")
    g = mod.Git(str(r))
    paths = g.changed_paths(base, head)
    assert "src/big.cs" in paths
    assert mod.judge("", paths, head, base, g).verdict == mod.MISSING


def test_no_code_path_and_other_bases_are_not_required() -> None:
    g = mod.Git()
    assert (
        mod.judge("", ["docs/process/README.md", "CLAUDE.md"], "h", "b", g).verdict
        == mod.NOT_REQUIRED
    )
    assert mod.judge("", [], "h", "b", g).verdict == mod.NOT_REQUIRED
    for base_ref in ("mainline", "claude/x"):
        assert (
            mod.judge("", ["src/a.cs"], "h", "b", g, base_ref=base_ref).verdict
            == mod.NOT_REQUIRED
        )


class VersionOnlyGit(mod.Git):
    """Every manifest's diff is a one-line version bump (the content table below uses real git)."""

    def manifest_diff(self, base: str, head: str, paths) -> str:
        return "".join(
            f"diff --git a/{p} b/{p}\nindex 1..2 100644\n--- a/{p}\n+++ b/{p}\n@@ -1 +1 @@\n"
            + (
                '-    "resolved": "1.0.0",\n+    "resolved": "1.1.0",\n'
                if p.endswith(".json")
                else '-<PackageReference Include="A" Version="1.0.0" />\n'
                '+<PackageReference Include="A" Version="1.1.0" />\n'
            )
            for p in paths
        )


def test_dependabot_manifest_only_pr_is_not_required() -> None:
    g, bot = VersionOnlyGit(), "dependabot[bot]"
    paths = [
        "src/Sharpy.Core/Sharpy.Core.csproj",
        "Directory.Packages.props",
        "src/x/packages.lock.json",
    ]
    for outside in (".github/x.csproj", "build_tools/x.csproj"):
        assert (
            mod.judge("", paths + [outside], "h", "b", g, author=bot).verdict
            == mod.MISSING
        )
    result = mod.judge("", paths, "h", "b", g, author=bot)
    assert result.verdict == mod.NOT_REQUIRED, result.detail
    # Controls: the same paths from a human, a lookalike login, and a bot PR that also edits code.
    assert mod.judge("", paths, "h", "b", g).verdict == mod.MISSING
    assert mod.judge("", paths, "h", "b", g, author="dependabot").verdict == mod.MISSING
    assert mod.judge("", paths, "h", "b", g, author="someone").verdict == mod.MISSING
    for extra in ("src/a.cs", ".github/workflows/x.yml", "build_tools/x.py"):
        assert (
            mod.judge("", paths + [extra], "h", "b", g, author=bot).verdict
            == mod.MISSING
        )


def test_main_passes_the_author_to_the_judge(monkeypatch, capsys) -> None:
    class Fake(VersionOnlyGit):
        def changed_paths(self, base: str, head: str) -> List[str]:
            return ["src/Sharpy.Core/Sharpy.Core.csproj"]

    monkeypatch.setenv("PR_BODY", "")
    monkeypatch.delenv("GITHUB_STEP_SUMMARY", raising=False)
    args = ["--base", "b", "--head", "h"]
    code = mod.main(args + ["--author", "dependabot[bot]"], git_factory=Fake)
    assert code == 0 and "not required" in capsys.readouterr().out
    code = mod.main(args, git_factory=Fake)  # control: no author, same paths
    assert code == 1 and "review-line: missing" in capsys.readouterr().out


def test_there_is_no_head_exemption() -> None:
    # No exemption by head ref; the author exemption is manifest-only (tested above).
    assert mod.judge("", ["src/a.cs"], "h", "b", mod.Git()).verdict == mod.MISSING
    with pytest.raises(SystemExit):
        mod.main(["--head", "h", "--head-ref", "dependabot/nuget/x"])


# --- main() -------------------------------------------------------------------------------------


def run_main(h, monkeypatch, capsys, body: str, *args: str) -> tuple:
    monkeypatch.setenv("PR_BODY", body)
    monkeypatch.delenv("GITHUB_STEP_SUMMARY", raising=False)
    code = mod.main(list(args), git_factory=lambda: mod.Git(h["repo"]))
    return code, capsys.readouterr().out


def test_main_reads_the_body_from_env_and_paths_from_git(
    h, monkeypatch, capsys
) -> None:
    code, out = run_main(
        h,
        monkeypatch,
        capsys,
        rl(h["L2"], (h["L2"], h["F1"])),
        "--base",
        h["D0"],
        "--head",
        h["F1"],
    )
    assert code == 0 and "review-line: ok" in out
    code, out = run_main(
        h, monkeypatch, capsys, "no line", "--base", h["D0"], "--head", h["F1"]
    )
    assert code == 1 and "::error::review-line: missing" in out


def test_main_defaults_the_base_to_origin_base_ref(h, monkeypatch, capsys) -> None:
    git(Path(h["repo"]), "update-ref", "refs/remotes/origin/dev", h["D1"])
    code, out = run_main(
        h, monkeypatch, capsys, rl(h["L2"], (h["L2"], h["F1"])), "--head", h["M"]
    )
    assert code == 0 and "review-line: ok" in out


def test_main_other_base_ref_needs_no_git(h, monkeypatch, capsys) -> None:
    code, out = run_main(
        h, monkeypatch, capsys, "", "--head", "nope", "--base-ref", "mainline"
    )
    assert code == 0 and "not required" in out


def test_main_a_draft_warns_but_passes(h, monkeypatch, capsys) -> None:
    code, out = run_main(
        h, monkeypatch, capsys, "", "--base", h["D0"], "--head", h["F1"], "--draft"
    )
    assert code == 0 and "::warning::review-line: missing" in out


def test_main_git_failure_is_exit_2(h, monkeypatch, capsys) -> None:
    code, _ = run_main(h, monkeypatch, capsys, "", "--base", "nope", "--head", "HEAD")
    assert code == 2


def test_main_writes_the_step_summary(h, monkeypatch, tmp_path: Path) -> None:
    summary = tmp_path / "summary.md"
    monkeypatch.setenv("PR_BODY", "")
    monkeypatch.setenv("GITHUB_STEP_SUMMARY", str(summary))
    mod.main(
        ["--base", h["D0"], "--head", h["F1"]], git_factory=lambda: mod.Git(h["repo"])
    )
    assert "review-line: missing" in summary.read_text()


# --- The workflow -------------------------------------------------------------------------------


def test_the_workflow_judges_against_the_fetched_base_branch_not_the_payload() -> None:
    wf = yaml.safe_load((REPO / ".github/workflows/review-line.yml").read_text())
    job = wf["jobs"]["review-line"]
    assert job["name"] == "review-line"
    runs = "\n".join(s.get("run", "") for s in job["steps"])
    assert "origin/$BASE_REF" in runs and "git fetch" in runs
    assert "base.sha" not in yaml.safe_dump(wf)
    events = wf[True]["pull_request"]["types"]
    assert "edited" in events and "ready_for_review" in events


# --- Second review round ------------------------------------------------------------------------


def test_a_fix_range_must_start_at_a_reviewed_sha(h) -> None:
    # Review @ L1, fixes L2..F1: L2 descends from the review but was never reviewed itself.
    result = j(h, rl(h["L1"], (h["L2"], h["F1"])), h["F1"], h["D0"])
    assert result.verdict == mod.BAD_RANGE, result.detail
    # Positive control: the same range starting at the reviewed sha.
    assert j(h, rl(h["L1"], (h["L1"], h["F1"])), h["F1"], h["D0"]).verdict == mod.OK


def test_a_fix_range_must_run_forward(h) -> None:
    # Starts at the reviewed sha (F1) but ends before it: the range covers nothing after the review.
    result = j(h, rl(h["F1"], (h["F1"], h["L2"])), h["F1"], h["D0"])
    assert result.verdict == mod.BAD_RANGE and "does not run forward" in result.detail


def test_do_not_merge_is_cleared_only_by_a_review_of_a_later_commit(h) -> None:
    head, base, IR = h["F1"], h["D0"], "Independent review @ "
    s = lambda k: h[k][:9]
    # A later clause reviewing an OLDER commit does not clear it, even with a range to the head.
    older = f"{IR}{s('F1')}: do not merge; 3 blocking; second review @ {s('L2')}: merge; fixes {s('L2')}..{s('F1')}"
    assert j(h, older, head, base).verdict == mod.DO_NOT_MERGE
    # Nor does a later clause reviewing the SAME commit.
    same = f"{IR}{s('F1')}: do not merge; second review @ {s('F1')}: merge"
    assert j(h, same, head, base).verdict == mod.DO_NOT_MERGE
    # Positive control: a review of a later commit clears it, on the same line or a later one.
    later = f"{IR}{s('L2')}: do not merge; second review @ {s('F1')}: merge"
    assert j(h, later, head, base).verdict == mod.OK
    assert (
        j(h, f"{IR}{s('L2')}: do not merge\n{IR}{s('F1')}: merge", head, base).verdict
        == mod.OK
    )


def test_comment_opener_inside_inline_code_is_text(h) -> None:
    # The reviewer's A6: a literal `<!--` in inline code must not swallow the do-not-merge after it.
    IR, s = "Independent review @ ", h["F1"][:9]
    body = f"{IR}{s}: merge\n\nWe strip `<!--` comments.\n\n{IR}{s}: do not merge; 3 blocking"
    assert j(h, body, h["F1"], h["D0"]).verdict == mod.DO_NOT_MERGE
    # Outside code, an unclosed opener in the middle of a line is literal text too (GFM renders
    # `We strip <!-- comments.` as text), so the do-not-merge after it still counts.
    assert (
        j(h, body.replace("`<!--`", "<!--"), h["F1"], h["D0"]).verdict
        == mod.DO_NOT_MERGE
    )


@pytest.mark.parametrize(
    "line, in_comment, want",
    [
        ("a <!-- b --> c", False, ("a  c", False)),
        ("a `<!--` c", False, ("a `<!--` c", False)),
        ("a ``x <!-- y`` c <!-- d", False, ("a ``x <!-- y`` c <!-- d", False)),
        ("<!-- d", False, ("", True)),
        ("   <!-- d", False, ("   ", True)),
        ("    <!-- d", False, ("    <!-- d", False)),
        ("still hidden --> shown", True, (" shown", False)),
        ("still hidden", True, ("", True)),
        (
            "a ` unclosed <!-- x --> b",
            False,
            ("a `` unclosed  b".replace("``", "`"), False),
        ),
    ],
)
def test_strip_comments(line: str, in_comment: bool, want: tuple) -> None:
    assert mod.strip_comments(line, in_comment) == want


def test_a_fence_with_an_info_string_opens_and_never_closes() -> None:
    body = "```\nx\n```text\nIndependent review @ abc1234: merge\n```"
    assert mod.parse_body(body).lines == []
    # Positive control: after a bare closing fence the line counts.
    assert mod.parse_body(
        "```text\nx\n```\nIndependent review @ abc1234: merge"
    ).last_line_parses


@pytest.mark.parametrize("indent", ["    ", "\t", "     ", "  \t"])
def test_indented_lines_never_count(indent: str) -> None:
    assert mod.parse_body(indent + "Independent review @ abc1234: merge").lines == []
    # Positive control: three spaces is not code.
    assert mod.parse_body("   Independent review @ abc1234: merge").last_line_parses


def test_a_code_file_renamed_after_the_review_is_stale(h) -> None:
    # git log detects renames by default; without --no-renames the move lists only tools/a.cs.
    r = Path(h["repo"])
    git(r, "switch", "-q", "lane")
    git(r, "reset", "-q", "--hard", h["F1"])
    (r / "tools").mkdir()
    # src/a.cs is on the base, so the PR's net diff touches src/ and the check is required.
    git(r, "mv", "src/a.cs", "tools/a.cs")
    git(r, "commit", "-q", "-m", "move")
    head = git(r, "rev-parse", "HEAD")
    result = j(h, rl(h["L2"], (h["L2"], h["F1"])), head, h["D0"])
    assert result.verdict == mod.STALE and head[:9] in result.detail


def test_an_octopus_merge_after_the_review_is_stale(h) -> None:
    r = Path(h["repo"])
    sides = []
    for n in (1, 2):
        git(r, "switch", "-q", "-c", f"side{n}", h["D0"])
        commit(r, f"docs/side{n}.md", "1")
        sides.append(f"side{n}")
    git(r, "switch", "-q", "lane")
    git(r, "reset", "-q", "--hard", h["F1"])
    git(r, "switch", "-q", "-c", "two", h["F1"])
    git(r, "merge", "-q", "--no-edit", "side1")
    two = git(r, "rev-parse", "HEAD")
    git(r, "switch", "-q", "-c", "octo", h["F1"])
    git(r, "merge", "-q", "--no-edit", *sides)
    octo = git(r, "rev-parse", "HEAD")
    assert len(git(r, "log", "-1", "--format=%P", octo).split()) == 3
    line = rl(h["L2"], (h["L2"], h["F1"]))
    # Positive control: the two-parent merge of the same docs-only side is clean.
    assert j(h, line, two, h["D0"]).verdict == mod.OK
    result = j(h, line, octo, h["D0"])
    assert result.verdict == mod.STALE and octo[:9] in result.detail


def test_not_an_ancestor_message_names_a_rebase() -> None:
    class NoHistory(mod.Git):
        def is_ancestor(self, a: str, b: str) -> bool:
            return False

    result = mod.judge(
        "Independent review @ deadbeef1: merge", ["src/a.cs"], "h", "b", NoHistory()
    )
    assert result.verdict == mod.NOT_ANCESTOR
    assert (
        "empty diff" in result.detail
        and "rebased with changes, the review must be repeated" in result.detail
    )


# --- Third review round -------------------------------------------------------------------------


def _linear(tmp_path: Path, n: int = 10) -> tuple:
    r = tmp_path / "lin"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
    git(r, "config", "commit.gpgsign", "false")
    base = commit(r, "README.md", "0")
    git(r, "switch", "-q", "-c", "lane")
    shas = {i: commit(r, "src/l.cs", str(i)) for i in range(1, n + 1)}
    return r, base, shas


def test_every_do_not_merge_must_be_cleared_on_its_own(tmp_path: Path) -> None:
    r, base, L = _linear(tmp_path)
    g = mod.Git(str(r))
    s = lambda i: L[i][:9]
    IR = "Independent review @ "

    def v(body: str) -> mod.Result:
        return mod.judge(body, g.changed_paths(base, L[10]), L[10], base, g)

    # L5 clears the do-not-merge @ L3 but not the one @ L9, which a single slot would forget.
    two = f"{IR}{s(9)}: do not merge; second review @ {s(3)}: do not merge; third review @ {s(5)}: merge; fixes {s(5)}..{s(10)}"
    result = v(two)
    assert (
        result.verdict == mod.DO_NOT_MERGE
        and s(9) in result.detail
        and s(3) not in result.detail
    )
    # Control: the single do-not-merge @ L9 alone is not cleared by L5 either.
    assert (
        v(
            f"{IR}{s(9)}: do not merge; second review @ {s(5)}: merge; fixes {s(5)}..{s(10)}"
        ).verdict
        == mod.DO_NOT_MERGE
    )
    # Control: one later review clears both when it descends from both.
    assert (
        v(
            f"{IR}{s(9)}: do not merge; second review @ {s(3)}: do not merge; third review @ {s(10)}: merge"
        ).verdict
        == mod.OK
    )
    # Documented: an older do-not-merge written on a later line still blocks.
    late = f"{IR}{s(5)}: do not merge; second review @ {s(9)}: merge\n{IR}{s(3)}: do not merge"
    assert v(late).verdict == mod.DO_NOT_MERGE


def test_a_do_not_merge_with_valid_shas_stays_do_not_merge(tmp_path: Path) -> None:
    r, base, L = _linear(tmp_path)
    g = mod.Git(str(r))
    body = f"Independent review @ {L[10][:9]}: do not merge; 3 blocking"
    assert (
        mod.judge(body, g.changed_paths(base, L[10]), L[10], base, g).verdict
        == mod.DO_NOT_MERGE
    )


@pytest.mark.parametrize("where", ["blocking", "clearing", "range end"])
def test_an_unknown_sha_in_a_do_not_merge_line_gets_the_rebase_hint(
    tmp_path: Path, where: str
) -> None:
    r, base, L = _linear(tmp_path)
    g = mod.Git(str(r))
    s = lambda i: L[i][:9]
    gone = "deadbeef1"
    body = {
        "blocking": f"Independent review @ {gone}: do not merge",
        "clearing": f"Independent review @ {s(5)}: do not merge; second review @ {gone}: merge",
        "range end": f"Independent review @ {s(5)}: do not merge; fixes {s(5)}..{gone}",
    }[where]
    result = mod.judge(body, g.changed_paths(base, L[10]), L[10], base, g)
    assert (
        result.verdict == mod.NOT_ANCESTOR and "rebased with changes" in result.detail
    ), result.detail


GOOD_T = "Independent review @ {sha}: merge"
DNM_T = "Independent review @ {sha}: do not merge; 3 blocking"


@pytest.mark.parametrize(
    "trigger",
    [
        "Use a <!-- marker to hide notes.",  # (a) mid-line, unclosed: literal text
        "    x = '<!--'",  # (c) indented code
        "> quoted <!--",  # (d) blockquote
        "   > quoted, indented <!--",
        # Text before the opener within the first three columns: GFM keeps the next line visible
        # (pandoc -f gfm: the quote's HTML block ends with the quote; `a <!-- x` is text).
        "> <!-- x",
        "a <!-- x",
    ],
)
def test_an_unclosed_opener_that_does_not_start_a_line_hides_nothing(
    tmp_path: Path, trigger: str
) -> None:
    r, base, L = _linear(tmp_path)
    g = mod.Git(str(r))
    body = (
        GOOD_T.format(sha=L[10][:9])
        + "\n\n"
        + trigger
        + "\n\n"
        + DNM_T.format(sha=L[10][:9])
    )
    assert (
        mod.judge(body, g.changed_paths(base, L[10]), L[10], base, g).verdict
        == mod.DO_NOT_MERGE
    )


@pytest.mark.parametrize(
    "trigger",
    [
        "<!-- template",  # a line that starts with <!-- opens a comment that spans lines
        "   <!-- template",
        # (b) a code span left open across a soft break: the next line STARTS with <!--, which in
        # GFM opens an HTML block even inside a paragraph (pandoc -f gfm hides what follows), so
        # the do-not-merge after it is hidden from a reader and from this check alike.
        "see `a\n<!--` b",
    ],
)
def test_a_comment_that_starts_a_line_hides_the_lines_after_it(
    tmp_path: Path, trigger: str
) -> None:
    r, base, L = _linear(tmp_path)
    g = mod.Git(str(r))
    body = (
        GOOD_T.format(sha=L[10][:9])
        + "\n\n"
        + trigger
        + "\n\n"
        + DNM_T.format(sha=L[10][:9])
    )
    assert (
        mod.judge(body, g.changed_paths(base, L[10]), L[10], base, g).verdict == mod.OK
    )


def test_a_comment_closed_on_its_line_is_removed_mid_line() -> None:
    lines = mod.visible_lines("a <!-- hidden --> b\nnext")
    assert lines == ["a  b", "next"]
    # And a comment opened at the start of a line and closed later hides only its contents.
    # (Each hidden line stays as an empty string, so line positions are kept.)
    assert mod.visible_lines("<!-- x\ny\nz --> w\nv") == ["", "", " w", "v"]


# --- The Dependabot exemption's content check (owner ruling 2026-11-17) --------------------------
# "make the review-line exemption require every changed line to be a version attribute": the table
# is {author} x {diff kind}, built on a scratch repository and judged through real `git diff`.

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Some.Sdk" Version="1.0.0" />
  <PropertyGroup>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Foo" Version="1.0.0" />
    <PackageReference Include="Bar" Version="2.0.0">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="F1" Version="1.*" />
    <PackageReference Include="F2" Version="[1.0,)" />
    <PackageReference Include="F3" Version="(,2.0]" />
    <PackageReference Include="F4" Version="*" />
    <PackageReference Include="Cond" Version="1.0.0" Condition='x Version="1.0.0" y' />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.1'">
    <Compile Remove="Tarfile/**" />
  </ItemGroup>
</Project>
"""
PROPS = """<Project>
  <ItemGroup>
    <PackageVersion Include="Baz" Version="3.0.0" />
  </ItemGroup>
</Project>
"""
LOCK = """{
  "version": 1,
  "dependencies": {
    "net10.0": {
      "Foo": {
        "type": "Direct",
        "requested": "[1.0.0, )",
        "resolved": "1.0.0",
        "contentHash": "AAAA+/==",
        "dependencies": {
          "Qux": "4.0.0"
        }
      }
    }
  }
}
"""
CRLF_PJ = "src/C/C.csproj"
CRLF_CSPROJ = '<Project>\r\n  <ItemGroup>\r\n    <PackageReference Include="Foo" Version="1.0.0" />\r\n  </ItemGroup>\r\n</Project>\r\n'
PJ, PP, LK = (
    "src/A/A.csproj",
    "src/Directory.Packages.props",
    "src/A/packages.lock.json",
)
FOO_BUMP = ('Include="Foo" Version="1.0.0"', 'Include="Foo" Version="1.1.0"')


# kind -> list of (path, ((old, new), ...)) edits, or a callable(repo) for the odd ones.
KINDS = {
    "csproj version only": [
        (
            PJ,
            (
                FOO_BUMP,
                ('Include="Bar" Version="2.0.0"', 'Include="Bar" Version="2.1.0"'),
            ),
        )
    ],
    "props version only": [(PP, (('Version="3.0.0"', 'Version="3.1.0"'),))],
    "lock file only": [
        (
            LK,
            (
                ('"[1.0.0, )"', '"[1.1.0, )"'),
                ('"resolved": "1.0.0"', '"resolved": "1.1.0"'),
                ('"AAAA+/=="', '"BBBB+/=="'),
                ('"Qux": "4.0.0"', '"Qux": "4.1.0"'),
            ),
        )
    ],
    "csproj with a Target added": [
        (
            PJ,
            (
                FOO_BUMP,
                (
                    "</Project>",
                    '  <Target Name="x" BeforeTargets="Build"><Exec Command="echo hi" /></Target>\n</Project>',
                ),
            ),
        )
    ],
    "version bump plus a property change": [
        (
            PJ,
            (FOO_BUMP, ("<Nullable>enable</Nullable>", "<Nullable>disable</Nullable>")),
        )
    ],
    "an Sdk element's Version changed": [
        (PJ, (('Version="1.0.0" />\n  <Prop', 'Version="6.6.6" />\n  <Prop'),))
    ],
    "a Version that MSBuild evaluates": [
        (
            PJ,
            (
                (
                    'Include="Foo" Version="1.0.0"',
                    'Include="Foo" Version="$([System.IO.File]::ReadAllText(\'x\'))"',
                ),
            ),
        )
    ],
    "a new package": [
        (
            PJ,
            (
                (
                    '    <PackageReference Include="Foo"',
                    '    <PackageReference Include="Evil" Version="1.0.0" />\n    <PackageReference Include="Foo"',
                ),
            ),
        )
    ],
    "a package swapped": [
        (PJ, (('Include="Foo" Version="1.0.0"', 'Include="Evil" Version="1.1.0"'),))
    ],
    "a lock-file type changed": [(LK, (('"type": "Direct"', '"type": "Transitive"'),))],
    "a manifest added": "add",
    "a binary manifest (no patch)": "binary",
    "a .cs file too": [
        (PJ, (FOO_BUMP,)),
        ("src/A/a.cs", (("class A {}", "class A { int x; }"),)),
    ],
}
# Review of PR #2726 @ 54991d373: a line that moves (here into the netstandard-only ItemGroup, the
# shape of src/Sharpy.Stdlib/Sharpy.Stdlib.csproj) is no version change, bumped or not.
FOO_LINE = '    <PackageReference Include="Foo" Version="1.0.0" />\n'
TARFILE = '    <Compile Remove="Tarfile/**" />\n'
for _bump in ("1.1.0", "1.0.0"):
    KINDS[f"a line moved into a conditional ItemGroup, version {_bump}"] = [
        (PJ, ((FOO_LINE, ""), (TARFILE, TARFILE + FOO_LINE.replace("1.0.0", _bump))))
    ]
# A float or range on either side is more than a version change: later restores pick versions.
for _i, _float in enumerate(("1.*", "[1.0,)", "(,2.0]", "*"), 1):
    KINDS[f"Foo pinned to {_float}"] = [
        (PJ, ((FOO_BUMP[0], f'Include="Foo" Version="{_float}"'),))
    ]
    KINDS[f"F{_i} from {_float} to 1.1.0"] = [
        (
            PJ,
            (
                (
                    f'Include="F{_i}" Version="{_float}"',
                    f'Include="F{_i}" Version="1.1.0"',
                ),
            ),
        )
    ]
# The reviewer's line: a version tag, U+2028, then text a str.splitlines() reader would drop.
KINDS["U+2028 after a version tag"] = [
    (
        PJ,
        (
            (
                'Include="Foo" Version="1.0.0" />',
                'Include="Foo" Version="1.1.0" />\u2028\\ junk <Target Name="E"/>',
            ),
        ),
    )
]
# Second review of PR #2726 @ df64f7aa6, all through real git and Git.manifest_diff:
# a lone CR inside an added line (text-mode subprocess turned it into a line end, hiding the rest),
KINDS["a lone CR inside an added line"] = [
    (PJ, ((FOO_BUMP[0] + " />", FOO_BUMP[1] + ' />\r\\ <Target Name="E"/>'),))
]
# and the reviewer's second shape, a lone CR then text a line-splitting reader takes as a hunk header,
KINDS["a lone CR then a hunk header inside an added line"] = [
    (PJ, ((FOO_BUMP[0] + " />", FOO_BUMP[1] + ' />\r@@ <Target Name="E"/>'),))
]
# bytes that are not UTF-8 (a traceback before; now the exemption is not taken),
KINDS["a byte that is not UTF-8"] = [
    (PJ, (FOO_BUMP, ('Include="Bar" Version="2.0.0"', 'Include="B\udcffr" Version="2.0.0"')))
]
# a `Version="..."` inside another attribute's value (text, not the Version attribute),
KINDS["a Version inside another attribute's value"] = [
    (PJ, (("Condition='x Version=\"1.0.0\" y'", "Condition='x Version=\"9.0.0\" y'"),))
]
# and their positive controls: a CRLF file's bump, and the real Version beside that attribute.
KINDS["a CRLF csproj version only"] = [(CRLF_PJ, (FOO_BUMP,))]
KINDS["a tag with a quoted Version text, real Version bumped"] = [
    (PJ, (('Include="Cond" Version="1.0.0"', 'Include="Cond" Version="1.1.0"'),))
]
EXEMPT_KINDS = {
    "csproj version only",
    "props version only",
    "lock file only",
    "a CRLF csproj version only",
    "a tag with a quoted Version text, real Version bumped",
}
AUTHORS = ["dependabot[bot]", "dependabot", "someone", None]


@pytest.fixture(scope="module")
def manifests(tmp_path_factory) -> Dict[str, str]:
    r = tmp_path_factory.mktemp("manifests") / "repo"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
    git(r, "config", "commit.gpgsign", "false")
    git(r, "config", "core.autocrlf", "false")
    for path, text in (
        (PJ, CSPROJ),
        (PP, PROPS),
        (LK, LOCK),
        ("src/A/a.cs", "class A {}"),
        (CRLF_PJ, CRLF_CSPROJ),
    ):
        (r / path).parent.mkdir(parents=True, exist_ok=True)
        (r / path).write_bytes(text.encode("utf-8"))
    git(r, "add", "-A")
    git(r, "commit", "-q", "-m", "base")
    s = {"repo": str(r), "base": git(r, "rev-parse", "HEAD")}
    for i, (kind, edits) in enumerate(KINDS.items()):
        git(r, "switch", "-q", "-c", f"k{i}", s["base"])
        if edits == "add":
            (r / "src/B").mkdir(exist_ok=True)
            (r / "src/B/B.csproj").write_text(
                '<Project>\n<PackageReference Include="Foo" Version="1.0.0" />\n</Project>\n'
            )
        elif edits == "binary":
            (r / PJ).write_bytes(CSPROJ.replace("1.0.0", "1.1.0").encode() + b"\0")
        else:
            for path, pairs in edits:
                # Bytes in and out: no newline translation; a lone surrogate writes a raw byte.
                text = (r / path).read_bytes().decode("utf-8", "surrogateescape")
                for old, new in pairs:
                    assert old in text, (kind, old)
                    text = text.replace(old, new, 1)
                (r / path).write_bytes(text.encode("utf-8", "surrogateescape"))
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", kind)
        s[kind] = git(r, "rev-parse", "HEAD")
    return s


@pytest.mark.parametrize("author", AUTHORS)
@pytest.mark.parametrize("kind", list(KINDS))
def test_dependabot_exemption_content_table(manifests, kind: str, author) -> None:
    g = mod.Git(manifests["repo"])
    base, head = manifests["base"], manifests[kind]
    result = mod.judge("", g.changed_paths(base, head), head, base, g, author=author)
    want = (
        mod.NOT_REQUIRED
        if author == "dependabot[bot]" and kind in EXEMPT_KINDS
        else mod.MISSING
    )
    assert result.verdict == want, result.detail
    # Every refused manifest-only kind is refused by the content check, not by something else.
    content_refused = author == "dependabot[bot]" and kind not in EXEMPT_KINDS | {
        "a .cs file too"
    }
    assert ("exemption not taken" in result.detail) is content_refused, result.detail


@pytest.mark.parametrize(
    "diff",
    [
        "",  # no patch at all
        "diff --git a/src/A/A.csproj b/src/A/A.csproj\nindex 1..2 100644\n",  # header, no hunk
        "diff --git a/src/A/A.csproj b/src/A/A.csproj\n--- a/src/A/A.csproj\n+++ b/src/A/A.csproj\n",
    ],
)
def test_a_missing_or_truncated_patch_is_not_version_only(diff: str) -> None:
    assert mod.version_only(diff, ["src/A/A.csproj"]) is not None


CSPROJ_DIFF = (
    "diff --git a/src/A/A.csproj b/src/A/A.csproj\nindex 1..2 100644\n"
    "--- a/src/A/A.csproj\n+++ b/src/A/A.csproj\n@@ -7 +7 @@\n"
)
TAG = '    <PackageReference Include="Foo" Version="{}" />'


@pytest.mark.parametrize(
    "ch", ["\r", "\x0b", "\x0c", "\x1c", "\x1d", "\x1e", "\x85", "\u2028", "\u2029"]
)
def test_a_changed_line_holding_a_line_separator_is_not_a_version_change(
    ch: str,
) -> None:
    # Git ends a diff line at \n only; text after any of these must not be hidden from the check.
    for added in (
        TAG.format("1.1.0") + ch + " ",
        TAG.format("1.1.0") + ch + '\\ <Target Name="E"/>',
    ):
        diff = CSPROJ_DIFF + "-" + TAG.format("1.0.0") + "\n+" + added + "\n"
        assert mod.version_only(diff, ["src/A/A.csproj"]) is not None, repr(added)
    # The same separator on both sides of the pair: the pairing alone would accept it.
    both = TAG + ch + " "
    diff = (
        CSPROJ_DIFF + "-" + both.format("1.0.0") + "\n+" + both.format("1.1.0") + "\n"
    )
    assert mod.version_only(diff, ["src/A/A.csproj"]) is not None


def test_a_crlf_line_pair_is_a_version_change() -> None:
    # Positive control for the separator rule: a `\r` that ends the line (a CRLF file) is allowed.
    diff = (
        CSPROJ_DIFF + "-" + TAG.format("1.0.0") + "\r\n+" + TAG.format("1.1.0") + "\r\n"
    )
    assert mod.version_only(diff, ["src/A/A.csproj"]) is None


@pytest.mark.parametrize(
    "value", ["10.0.15", "18.12.0", "5.12.0", "11.0.0", "1.2.3-rc.1", "1.0.0+b.5"]
)
def test_concrete_versions(value: str) -> None:
    assert mod.CONCRETE_VERSION.fullmatch(value)


@pytest.mark.parametrize(
    "value", ["1.*", "[1.0,)", "(,2.0]", "*", "$(V)", "1.0 ", "[1.0.0, )"]
)
def test_floats_and_ranges_are_not_concrete(value: str) -> None:
    assert not mod.CONCRETE_VERSION.fullmatch(value)


def test_an_unchanged_version_is_no_version_change() -> None:
    # A removed/added pair that keeps its version is not a version change.
    diff = CSPROJ_DIFF + "-" + TAG.format("1.0.0") + "\n+" + TAG.format("1.0.0") + "\n"
    assert mod.version_only(diff, ["src/A/A.csproj"]) is not None


def test_version_only_positive_control() -> None:
    diff = (
        "diff --git a/src/A/A.csproj b/src/A/A.csproj\nindex 1..2 100644\n"
        "--- a/src/A/A.csproj\n+++ b/src/A/A.csproj\n@@ -7 +7 @@\n"
        '-    <PackageReference Include="Foo" Version="1.0.0" />\n'
        '+    <PackageReference Include="Foo" Version="1.1.0" />\n'
    )
    assert mod.version_only(diff, ["src/A/A.csproj"]) is None
    # One manifest of two without a patch is enough to lose the exemption.
    assert mod.version_only(diff, ["src/A/A.csproj", "src/B/B.csproj"]) is not None


def test_the_missing_verdict_names_why_the_exemption_was_not_taken(manifests) -> None:
    g = mod.Git(manifests["repo"])
    base, head = manifests["base"], manifests["csproj with a Target added"]
    result = mod.judge(
        "", g.changed_paths(base, head), head, base, g, author="dependabot[bot]"
    )
    assert "exemption not taken" in result.detail and "<Target" in result.detail, (
        result.detail
    )


# The eight real Dependabot diffs (GitHub "list pull request files" patches, fetched 2026-11-17,
# context lines dropped: the -U0 view the check reads), hunk by hunk: (path, removed, added).
REAL_DEPENDABOT = {
    2706: [
        (
            "src/Sharpy.Cli.Tests/Sharpy.Cli.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Core.Tests/Sharpy.Core.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Lsp.Tests/Sharpy.Lsp.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Stdlib.Tests/Sharpy.Stdlib.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
    ],
    2707: [
        (
            "src/Sharpy.Compiler/Sharpy.Compiler.csproj",
            [
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.9.0" />',
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.9.0" />',
            ],
            [
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.12.0" />',
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.12.0" />',
            ],
        ),
    ],
    2708: [
        (
            "src/Sharpy.Playground/Sharpy.Playground.csproj",
            [
                '    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.14" />'
            ],
            [
                '    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.15" />'
            ],
        ),
    ],
    2711: [
        (
            "src/Sharpy.Stdlib/Sharpy.Stdlib.csproj",
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.14" />'
            ],
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.15" />'
            ],
        ),
        (
            "src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Sqlite3.csproj",
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.14" />'
            ],
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.15" />'
            ],
        ),
    ],
    2714: [
        (
            "src/Sharpy.Cli/Sharpy.Cli.csproj",
            ['    <PackageReference Include="System.CommandLine" Version="2.0.14" />'],
            ['    <PackageReference Include="System.CommandLine" Version="3.0.0" />'],
        ),
    ],
    2709: [
        (
            "src/Sharpy.Playground/Sharpy.Playground.csproj",
            [
                '    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.14" PrivateAssets="all" />'
            ],
            [
                '    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="11.0.0" PrivateAssets="all" />'
            ],
        ),
    ],
    2710: [
        (
            "src/Sharpy.Core/Sharpy.Core.csproj",
            [
                '    <PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="10.0.14" Condition="\'$(TargetFramework)\' == \'netstandard2.1\'" />'
            ],
            [
                '    <PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="11.0.0" Condition="\'$(TargetFramework)\' == \'netstandard2.1\'" />'
            ],
        ),
    ],
    2712: [
        (
            "src/Sharpy.Cli.Tests/Sharpy.Cli.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Cli/Sharpy.Cli.csproj",
            ['    <PackageReference Include="System.CommandLine" Version="2.0.14" />'],
            ['    <PackageReference Include="System.CommandLine" Version="3.0.0" />'],
        ),
        (
            "src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Compiler/Sharpy.Compiler.csproj",
            [
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.9.0" />',
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.9.0" />',
                '    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="10.0.14" />',
            ],
            [
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.12.0" />',
                '    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.12.0" />',
                '    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="11.0.0" />',
            ],
        ),
        (
            "src/Sharpy.Core.Tests/Sharpy.Core.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Lsp.Tests/Sharpy.Lsp.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Stdlib.Tests/Sharpy.Stdlib.Tests.csproj",
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />'
            ],
            [
                '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />'
            ],
        ),
        (
            "src/Sharpy.Stdlib/Sharpy.Stdlib.csproj",
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.14" />'
            ],
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.15" />'
            ],
        ),
        (
            "src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Sqlite3.csproj",
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.14" />'
            ],
            [
                '    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.15" />'
            ],
        ),
    ],
}


def _diff(hunks) -> str:
    out, seen = [], set()
    for path, removed, added in hunks:
        if path not in seen:
            seen.add(path)
            out.append(
                f"diff --git a/{path} b/{path}\nindex 1..2 100644\n--- a/{path}\n+++ b/{path}"
            )
        out.append(f"@@ -1,{len(removed)} +1,{len(added)} @@")
        out += ["-" + r for r in removed] + ["+" + a for a in added]
    return "\n".join(out) + "\n"


@pytest.mark.parametrize("pr", sorted(REAL_DEPENDABOT))
def test_the_real_dependabot_diffs_are_version_only(pr: int) -> None:
    hunks = REAL_DEPENDABOT[pr]
    assert mod.version_only(_diff(hunks), sorted({h[0] for h in hunks})) is None


def test_a_real_pair_reordered_inside_its_hunk_is_not_version_only() -> None:
    # #2707's two-line hunk with its added lines swapped: the same multiset, a different pairing.
    path, removed, added = REAL_DEPENDABOT[2707][0]
    assert mod.version_only(_diff([(path, removed, added[::-1])]), [path]) is not None


def test_the_lone_cr_reaches_git_and_the_check_as_one_line(manifests) -> None:
    # The CR case above is only meaningful if git keeps it inside one diff line and the check reads
    # it; this is the positive control that the scratch commit holds a raw CR mid-line.
    g = mod.Git(manifests["repo"])
    head = manifests["a lone CR inside an added line"]
    raw = g.manifest_diff(manifests["base"], head, [PJ])
    assert b'/>\r\\ <Target Name="E"/>\n' in raw
    assert "not a version change" in (mod.version_only(raw, [PJ]) or "")


def test_a_diff_that_is_not_utf8_is_a_reason_not_a_crash() -> None:
    diff = (CSPROJ_DIFF + "-" + TAG.format("1.0.0") + "\n+").encode() + b"\xff\n"
    assert "not UTF-8" in (mod.version_only(diff, ["src/A/A.csproj"]) or "")


def test_the_non_utf8_kind_is_refused_for_its_encoding(manifests) -> None:
    g = mod.Git(manifests["repo"])
    base, head = manifests["base"], manifests["a byte that is not UTF-8"]
    result = mod.judge(
        "", g.changed_paths(base, head), head, base, g, author="dependabot[bot]"
    )
    assert result.verdict == mod.MISSING and "not UTF-8" in result.detail, result.detail


def test_only_the_no_newline_marker_is_skipped() -> None:
    # A hunk line starting with a backslash is git's marker only when it is exactly that text.
    pair = "-" + TAG.format("1.0.0") + "\n+" + TAG.format("1.1.0") + "\n"
    marker = "\\ No newline at end of file\n"
    assert mod.version_only(CSPROJ_DIFF + pair + marker, ["src/A/A.csproj"]) is None
    other = '\\ <Target Name="E"/>\n'
    assert mod.version_only(CSPROJ_DIFF + pair + other, ["src/A/A.csproj"]) is not None
