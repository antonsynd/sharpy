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
    # A later review on the line clears it.
    assert (
        j(h, f"{dnm}; second review @ {h['F1'][:9]}: merge", head, base).verdict
        == mod.OK
    )


def test_a_code_file_renamed_out_of_a_code_path_is_a_code_change(
    tmp_path: Path,
) -> None:
    r = tmp_path / "r"
    r.mkdir()
    git(r, "init", "-q", "-b", "dev")
    git(r, "config", "user.email", "t@example.invalid")
    git(r, "config", "user.name", "t")
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


def test_there_is_no_bot_or_head_exemption() -> None:
    # README item 6 names none; the CLI no longer takes an author or a head ref.
    assert mod.judge("", ["src/a.cs"], "h", "b", mod.Git()).verdict == mod.MISSING
    with pytest.raises(SystemExit):
        mod.main(["--head", "h", "--author", "dependabot[bot]"])


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
