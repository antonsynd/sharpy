"""Tests for build_tools/allowlist_issue_state.py — the allowlist issue-state gate."""
from __future__ import annotations

import os
import sys
from unittest.mock import patch

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import allowlist_issue_state as mod


def _write(path, content: str) -> str:
    with open(path, "w") as f:
        f.write(content)
    return str(path)


def _stub_states(mapping: dict[int, str]):
    """Return a patched query_states that uses the given mapping."""
    def fake_query(numbers):
        return {n: mapping.get(n, "OPEN") for n in numbers}
    return fake_query


# ── Allowlist (.txt) tests ─────────────────────────────────────────────────────


def test_row_citing_closed_issue(tmp_path):
    path = _write(tmp_path / "test-allowlist.txt", "fixture::x # #1234\n")
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [1234]

    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_row_citing_open_issue(tmp_path):
    path = _write(tmp_path / "test-allowlist.txt", "fixture::x # #5678\n")
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [5678]

    with patch.object(mod, "query_states", _stub_states({5678: "OPEN"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_row_with_paragraph_cite(tmp_path):
    content = "# Known defect (#1234)\nfixture::y\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [1234]

    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_row_with_deviations_yaml_exempt(tmp_path):
    content = "# deviations.yaml: some-id. Permanent (#9999).\nfixture::perm\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].exempt is True

    with patch.object(mod, "query_states", _stub_states({9999: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_multi_row_block_all_inherit_paragraph_cite(tmp_path):
    # A comment header governs the WHOLE run of rows beneath it, not just the first. Under the
    # paragraph-reset bug, rows 2..N were stranded as uncited (cites == []) and — post-DD14 —
    # flagged as offences. All three must inherit the header's cite.
    content = "# Known block (#1234)\nfixture::a\nfixture::b\nfixture::c\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    rows = mod.scan([path])
    assert [r.cites for r in rows] == [[1234], [1234], [1234]]

    # With the cite inherited by every row, an OPEN issue is green (buggy scanner would have
    # reported rows 2..N as uncited offences → exit 1).
    with patch.object(mod, "query_states", _stub_states({1234: "OPEN"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_multi_row_block_all_flag_when_cite_closes(tmp_path):
    # Positive control for the row above: the same block citing a CLOSED issue flags every row.
    content = "# Known block (#1234)\nfixture::a\nfixture::b\nfixture::c\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_multi_row_deviations_block_all_exempt(tmp_path):
    # A deviations.yaml header exempts every row beneath it, not just the first.
    content = "# deviations.yaml: some-id. Permanent.\nfixture::a\nfixture::b\nfixture::c\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    rows = mod.scan([path])
    assert [r.exempt for r in rows] == [True, True, True]


def test_blank_line_ends_the_block(tmp_path):
    # Paragraph state persists across rows but a blank line is the block boundary: a bare row after
    # the blank is genuinely uncited, and a fresh header starts a fresh cite (no cross-block bleed).
    content = "# Block one (#1111)\nfixture::a\nfixture::b\n\nfixture::orphan\n# Block two (#2222)\nfixture::c\n"
    path = _write(tmp_path / "test-allowlist.txt", content)
    rows = mod.scan([path])
    by_line = {r.line: r.cites for r in rows}
    assert by_line[2] == [1111]
    assert by_line[3] == [1111]
    assert by_line[5] == []      # orphan after blank line: uncited
    assert by_line[7] == [2222]  # new block: its own cite, not 1111


# ── C# Skip tests ─────────────────────────────────────────────────────────────


def test_cs_skip_citing_closed_issue(tmp_path):
    cs = '''
    [Theory(Skip = "Known defect #1234")]
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [1234]

    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_cs_skip_citing_open_issue(tmp_path):
    cs = '''
    [Fact(Skip = "Waiting on #5678")]
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [5678]

    with patch.object(mod, "query_states", _stub_states({5678: "OPEN"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_cs_skip_without_issue_cite(tmp_path):
    cs = '''
    [Fact(Skip = "F51 — comprehension contextual typing, lead's Wave B")]
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == []
    assert rows[0].exempt is False

    sys.argv = ["prog", "--paths", path]
    with pytest.raises(SystemExit) as exc:
        mod.main()
    assert exc.value.code == 1


def test_cs_skip_with_no_issue_exemption(tmp_path):
    cs = '''
    [Fact(Skip = "no-issue: known limitation of the heap")]
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 0  # exempt skips produce no row


# ── C# comment-roster tests (#1939) ────────────────────────────────────────────


def test_cs_comment_roster_citing_closed_issue(tmp_path):
    cs = '''
    // BUG(#1234): the emitter drops the second element — enable when it closes.
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [1234]

    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_cs_comment_roster_citing_open_issue(tmp_path):
    cs = '''
    // TODO(#5678): pending the parser change.
    public void SomeTest() { }
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [5678]

    with patch.object(mod, "query_states", _stub_states({5678: "OPEN"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_knownred_cell_construction_citing_closed_issue(tmp_path):
    cs = '''
    private static readonly KnownRedStoreCell Row =
        new KnownRedStoreCell("#1234", "SPY0908", "the store mis-lowers");
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == [1234]

    with patch.object(mod, "query_states", _stub_states({1234: "CLOSED"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 1


def test_knownred_drained_count_comment_is_not_a_row(tmp_path):
    # A drained-count annotation cites the issue that CLOSED the row, not a live suppression, so it
    # must NOT be scanned — otherwise draining a row would immediately re-flag its own close note.
    cs = '''
    private const int MistypedStoreKnownRedCount = 0; // #1234: DRAINED (Phase 2)
    '''
    path = _write(tmp_path / "SomeTests.cs", cs)
    rows = mod.scan([path])
    assert rows == []


# ── Uncited .txt-row offence (#1939 / DD14) ─────────────────────────────────────


def test_txt_row_without_cite_is_offence(tmp_path):
    # DD14: an uncited .txt allowlist row is an offence (not silently skipped), so the widened glob
    # is non-vacuous. Positive control: the same fixture with the row cited is green.
    path = _write(tmp_path / "spec_blocks_allowlist.txt", "some.md::abc123  # SPY0200 first\n")
    rows = mod.scan([path])
    assert len(rows) == 1
    assert rows[0].cites == []
    assert rows[0].exempt is False

    sys.argv = ["prog", "--paths", path]
    with pytest.raises(SystemExit) as exc:
        mod.main()
    assert exc.value.code == 1


def test_txt_row_with_cite_is_green(tmp_path):
    # The same row, now citing an OPEN issue, passes — the offence discriminates on the cite.
    path = _write(tmp_path / "spec_blocks_allowlist.txt", "some.md::abc123  # SPY0200 first #4242\n")
    rows = mod.scan([path])
    assert rows[0].cites == [4242]

    with patch.object(mod, "query_states", _stub_states({4242: "OPEN"})):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 0


def test_gh_unavailable_exits_2(tmp_path):
    path = _write(tmp_path / "test-allowlist.txt", "fixture::z # #7777\n")

    def failing_query(numbers):
        print("cannot verify issue state — gh not available or not authenticated",
              file=sys.stderr)
        sys.exit(2)

    with patch.object(mod, "query_states", failing_query):
        sys.argv = ["prog", "--paths", path]
        with pytest.raises(SystemExit) as exc:
            mod.main()
        assert exc.value.code == 2
