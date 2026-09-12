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
