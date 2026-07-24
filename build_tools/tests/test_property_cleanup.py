"""
Unit tests for the scoped per-round test-host cleanup helpers in ``build_sharpy``
(#1132).

The property-stress round loop must kill only the test hosts a round spawned —
never a blanket ``pkill -f testhost``, which reaps hosts owned by OTHER
concurrent agent sessions (the same cross-session-kill incident class as the
``pkill -f dotnet`` crashes seen 4x during plan-f08ccf). ``new_testhost_pids``
is the pure decision (set difference); ``snapshot_testhost_pids`` and
``kill_new_testhosts`` wrap ``pgrep``/``os.kill`` and tolerate a missing
``pgrep`` by skipping cleanup entirely (zero behavior change).

``build_sharpy`` is an extensionless executable, so it is loaded via
``SourceFileLoader`` rather than a normal import.
"""

import importlib.util
from importlib.machinery import SourceFileLoader
from pathlib import Path


def _load_build_sharpy():
    path = Path(__file__).parent.parent / "bin" / "build_sharpy"
    loader = SourceFileLoader("build_sharpy_module", str(path))
    spec = importlib.util.spec_from_loader("build_sharpy_module", loader)
    assert spec is not None, f"could not load spec for {path}"
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)
    return module


bs = _load_build_sharpy()


class TestNewTesthostPids:
    """The pure set-difference decision — no processes involved."""

    def test_empty_before_returns_all_after(self):
        # First round on a clean host: every running test host is ours.
        assert bs.new_testhost_pids(set(), {1, 2, 3}) == {1, 2, 3}

    def test_empty_after_returns_empty(self):
        assert bs.new_testhost_pids({1, 2, 3}, set()) == set()

    def test_overlapping_returns_only_new(self):
        # {1, 2} pre-existed (other sessions); only {3, 4} are this round's.
        assert bs.new_testhost_pids({1, 2}, {2, 3, 4}) == {3, 4}

    def test_all_new(self):
        assert bs.new_testhost_pids({1, 2}, {9, 10}) == {9, 10}

    def test_identical_returns_empty(self):
        assert bs.new_testhost_pids({1, 2, 3}, {1, 2, 3}) == set()

    def test_after_subset_of_before_returns_empty(self):
        # A host that existed before but vanished during the round is never our
        # responsibility and must not appear in the kill set.
        assert bs.new_testhost_pids({1, 2, 3}, {2}) == set()

    def test_does_not_mutate_inputs(self):
        before = {1, 2}
        after = {2, 3}
        bs.new_testhost_pids(before, after)
        assert before == {1, 2}
        assert after == {2, 3}


class TestSnapshotTesthostPids:
    """``pgrep`` wrapper — parses PIDs, tolerates no-match and a missing binary."""

    def test_parses_pgrep_output_into_int_set(self, monkeypatch):
        class _Result:
            stdout = "111\n222\n333\n"

        monkeypatch.setattr(bs.subprocess, "run", lambda *_a, **_k: _Result())
        assert bs.snapshot_testhost_pids() == {111, 222, 333}

    def test_no_matches_returns_empty(self, monkeypatch):
        # pgrep exits 1 with empty stdout when nothing matches — not an error.
        class _Result:
            stdout = ""

        monkeypatch.setattr(bs.subprocess, "run", lambda *_a, **_k: _Result())
        assert bs.snapshot_testhost_pids() == set()

    def test_missing_pgrep_returns_empty(self, monkeypatch):
        def _boom(*_a, **_k):
            raise FileNotFoundError("pgrep")

        monkeypatch.setattr(bs.subprocess, "run", _boom)
        assert bs.snapshot_testhost_pids() == set()

    def test_ignores_non_integer_tokens(self, monkeypatch):
        class _Result:
            stdout = "111\ngarbage\n222\n"

        monkeypatch.setattr(bs.subprocess, "run", lambda *_a, **_k: _Result())
        assert bs.snapshot_testhost_pids() == {111, 222}


class TestKillNewTesthosts:
    """The kill wrapper — kills only new PIDs, tolerant of races and no pgrep."""

    def test_kills_only_new_pids(self, monkeypatch):
        killed = []
        monkeypatch.setattr(bs, "snapshot_testhost_pids", lambda: {1, 2, 3})
        monkeypatch.setattr(bs.os, "kill", lambda pid, _sig: killed.append(pid))
        bs.kill_new_testhosts({1})
        assert sorted(killed) == [2, 3]

    def test_no_new_pids_kills_nothing(self, monkeypatch):
        killed = []
        monkeypatch.setattr(bs, "snapshot_testhost_pids", lambda: {1, 2})
        monkeypatch.setattr(bs.os, "kill", lambda pid, _sig: killed.append(pid))
        bs.kill_new_testhosts({1, 2})
        assert killed == []

    def test_missing_pgrep_kills_nothing(self, monkeypatch):
        # snapshot returns empty when pgrep is unavailable -> nothing to kill,
        # even though hosts existed before (silent skip, zero behavior change).
        killed = []
        monkeypatch.setattr(bs, "snapshot_testhost_pids", lambda: set())
        monkeypatch.setattr(bs.os, "kill", lambda pid, _sig: killed.append(pid))
        bs.kill_new_testhosts({1, 2, 3})
        assert killed == []

    def test_tolerates_already_dead_pid(self, monkeypatch):
        # os.kill raises OSError if the PID already exited between snapshot and
        # kill — must be swallowed rather than aborting the round loop.
        def _kill(pid, _sig):
            raise ProcessLookupError(pid)

        monkeypatch.setattr(bs, "snapshot_testhost_pids", lambda: {5})
        monkeypatch.setattr(bs.os, "kill", _kill)
        bs.kill_new_testhosts(set())  # must not raise
