"""Tests for the machine-readable divergence ledger (build_tools/cpython_oracle, #1030).

These cover the generator (deviations seeding + annotation scanning + validation),
the deterministic render/round-trip, and the two CI enforcement failure modes
(unexplained CPython failure, stale ledger entry) — all on synthetic fixtures
written to a tmp dir, never on the committed pilots, so they stay fast and hermetic
under the ``python-build-tools`` workflow.
"""

from __future__ import annotations

import textwrap
from pathlib import Path

import pytest
import yaml

from build_tools.cpython_oracle import dual_run, ledger


# --------------------------------------------------------------------------- #
# Fixtures: a tiny deviations catalog + ported tree on disk
# --------------------------------------------------------------------------- #
_DEVIATIONS = """
deviations:
  - id: integer-division-floor
    code: null
    category: operators
    audience: python
    severity: none
    spec_ref: docs/language_specification/arithmetic_operators.md
    existing_diagnostic: null
    planned_diagnostic: null
  - id: int-overflow-checked
    code: null
    category: semantics
    audience: python
    severity: none
    spec_ref: docs/language_specification/primitive_types.md
    existing_diagnostic: null
    planned_diagnostic: null
"""


def _write_deviations(tmp_path: Path, text: str = _DEVIATIONS) -> Path:
    path = tmp_path / "deviations.yaml"
    path.write_text(textwrap.dedent(text), encoding="utf-8")
    return path


def _write_spy(tmp_path: Path, name: str, body: str) -> Path:
    ported = tmp_path / "ported"
    ported.mkdir(exist_ok=True)
    path = ported / name
    path.write_text(textwrap.dedent(body), encoding="utf-8")
    return path


def _build(tmp_path: Path, dev_text: str = _DEVIATIONS):
    dev = _write_deviations(tmp_path, dev_text)
    return ledger.build_ledger(
        deviations_path=dev,
        ported_root=tmp_path / "ported",
        repo_root=tmp_path,
    )


# --------------------------------------------------------------------------- #
# Deviations seeding
# --------------------------------------------------------------------------- #
def test_seeds_deviations_from_catalog(tmp_path: Path):
    (tmp_path / "ported").mkdir()
    data = _build(tmp_path)
    ids = [d["id"] for d in data["deviations"]]
    assert ids == ["int-overflow-checked", "integer-division-floor"]  # sorted
    # Machine-relevant fields are projected; prose fields are dropped.
    entry = data["deviations"][0]
    assert set(entry) == {
        "id",
        "category",
        "audience",
        "severity",
        "code",
        "spec_ref",
        "existing_diagnostic",
        "planned_diagnostic",
    }


def test_real_catalog_projects_all_entries():
    # Guards against the repo catalog drifting away from the ledger's projection.
    deviations = ledger.load_deviations()
    assert len(deviations) >= 50
    assert "integer-division-floor" in ledger.deviation_ids(deviations)


# --------------------------------------------------------------------------- #
# Annotation scanning + validation
# --------------------------------------------------------------------------- #
def test_scans_not_ported_annotation(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_demo_tests.spy",
        """
        # oracle-ledger:
        #   kind: not-ported
        #   test: DemoTest.test_omitted
        #   side: sharpy
        #   bug: 1063
        #   reason: known Sharpy bug, omitted rather than encoding the wrong result

        @test
        def test_kept():
            assert 1 == 1
        """,
    )
    data = _build(tmp_path)
    assert len(data["entries"]) == 1
    entry = data["entries"][0]
    assert entry["kind"] == "not-ported"
    assert entry["module"] == "cpython_demo_tests"
    assert entry["bug"] == 1063
    assert entry["source"].endswith("cpython_demo_tests.spy:2")


def test_scans_expected_fail_cpython_annotation(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_div_tests.spy",
        """
        # oracle-ledger:
        #   kind: expected-fail-cpython
        #   test: test_trunc_div
        #   deviation: integer-division-floor
        #   reason: encodes Sharpy's truncating // result, which CPython floors

        @test
        def test_trunc_div():
            assert (-7 // 2) == -3
        """,
    )
    data = _build(tmp_path)
    entry = data["entries"][0]
    assert entry["kind"] == "expected-fail-cpython"
    assert entry["deviation"] == "integer-division-floor"
    assert entry["side"] == "cpython"  # defaulted from kind


def test_unknown_deviation_id_is_rejected(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_bad_tests.spy",
        """
        # oracle-ledger:
        #   kind: expected-fail-cpython
        #   test: test_x
        #   deviation: no-such-deviation
        #   reason: cites a deviation that does not exist

        @test
        def test_x():
            assert True
        """,
    )
    with pytest.raises(ledger.LedgerError, match="unknown deviation"):
        _build(tmp_path)


def test_expected_fail_cpython_requires_deviation(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_nodv_tests.spy",
        """
        # oracle-ledger:
        #   kind: expected-fail-cpython
        #   test: test_x
        #   bug: 999
        #   reason: a CPython failure must be a designed divergence, not a bug

        @test
        def test_x():
            assert True
        """,
    )
    with pytest.raises(ledger.LedgerError, match="must cite a 'deviation'"):
        _build(tmp_path)


def test_expected_fail_missing_test_is_rejected(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_missing_tests.spy",
        """
        # oracle-ledger:
        #   kind: expected-fail-cpython
        #   test: test_absent
        #   deviation: integer-division-floor
        #   reason: references a @test function that is not in the file

        @test
        def test_present():
            assert True
        """,
    )
    with pytest.raises(ledger.LedgerError, match="not a @test function"):
        _build(tmp_path)


def test_not_ported_present_is_rejected(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_present_tests.spy",
        """
        # oracle-ledger:
        #   kind: not-ported
        #   test: test_here
        #   bug: 1063
        #   reason: claims omission but the function is actually present

        @test
        def test_here():
            assert True
        """,
    )
    with pytest.raises(ledger.LedgerError, match="actually present"):
        _build(tmp_path)


def test_entry_without_deviation_or_bug_is_rejected(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_bare_tests.spy",
        """
        # oracle-ledger:
        #   kind: not-ported
        #   test: SomeTest.test_x
        #   reason: no deviation and no bug — cannot classify
        """,
    )
    with pytest.raises(ledger.LedgerError, match="must cite a 'deviation' id"):
        _build(tmp_path)


# --------------------------------------------------------------------------- #
# Render determinism + round-trip
# --------------------------------------------------------------------------- #
def test_render_is_deterministic_and_reloadable(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_rt_tests.spy",
        """
        # oracle-ledger:
        #   kind: not-ported
        #   test: T.test_x
        #   bug: 1063
        #   reason: round-trip check

        @test
        def test_kept():
            assert True
        """,
    )
    data = _build(tmp_path)
    first = ledger.render_ledger(data)
    second = ledger.render_ledger(_build(tmp_path))
    assert first == second
    reloaded = yaml.safe_load(first)
    assert reloaded["schema_version"] == ledger.SCHEMA_VERSION
    assert reloaded["entries"][0]["test"] == "T.test_x"


# --------------------------------------------------------------------------- #
# Enforcement (evaluate_ledger) — the two CI failure modes
# --------------------------------------------------------------------------- #
def _ledger_with(*entries: dict) -> dict:
    return {"schema_version": 1, "deviations": [], "entries": list(entries)}


def test_unexplained_cpython_failure_fails(tmp_path: Path):
    led = _ledger_with()  # empty ledger
    results = {("cpython_x_tests", "test_a"): "fail"}
    enforcement = ledger.evaluate_ledger(results, led)
    assert not enforcement.ok
    assert enforcement.unexplained_failures == [("cpython_x_tests", "test_a")]


def test_covered_cpython_failure_is_excused(tmp_path: Path):
    led = _ledger_with(
        {
            "kind": "expected-fail-cpython",
            "module": "cpython_x_tests",
            "test": "test_a",
            "deviation": "integer-division-floor",
        }
    )
    results = {("cpython_x_tests", "test_a"): "fail"}
    enforcement = ledger.evaluate_ledger(results, led)
    assert enforcement.ok
    assert enforcement.satisfied == [("cpython_x_tests", "test_a")]
    assert not enforcement.unexplained_failures


def test_stale_entry_fails_when_test_passes(tmp_path: Path):
    led = _ledger_with(
        {
            "kind": "expected-fail-cpython",
            "module": "cpython_x_tests",
            "test": "test_a",
            "deviation": "integer-division-floor",
        }
    )
    results = {("cpython_x_tests", "test_a"): "pass"}
    enforcement = ledger.evaluate_ledger(results, led)
    assert not enforcement.ok
    assert enforcement.stale_entries == [("cpython_x_tests", "test_a")]


def test_expected_fail_sharpy_passing_under_cpython_is_fine(tmp_path: Path):
    # A Sharpy-side bug: the ported test is faithful Python and passes under CPython.
    led = _ledger_with(
        {
            "kind": "expected-fail-sharpy",
            "module": "cpython_x_tests",
            "test": "test_b",
            "bug": 1234,
        }
    )
    results = {("cpython_x_tests", "test_b"): "pass"}
    enforcement = ledger.evaluate_ledger(results, led)
    assert enforcement.ok


def test_expected_fail_sharpy_failing_under_cpython_is_unexplained(tmp_path: Path):
    # If a "Sharpy bug" test actually fails under CPython too, the entry is wrong:
    # it is not excused by an expected-fail-cpython entry.
    led = _ledger_with(
        {
            "kind": "expected-fail-sharpy",
            "module": "cpython_x_tests",
            "test": "test_b",
            "bug": 1234,
        }
    )
    results = {("cpython_x_tests", "test_b"): "fail"}
    enforcement = ledger.evaluate_ledger(results, led)
    assert not enforcement.ok
    assert enforcement.unexplained_failures == [("cpython_x_tests", "test_b")]


# --------------------------------------------------------------------------- #
# dual_run --ledger integration (end-to-end over tmp .spy files)
# --------------------------------------------------------------------------- #
def _write_ledger_file(tmp_path: Path, *entries: dict) -> Path:
    data = _ledger_with(*entries)
    path = tmp_path / "ledger.yaml"
    path.write_text(ledger.render_ledger(data), encoding="utf-8")
    return path


def test_dual_run_ledger_excuses_expected_failure(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_ef_tests.spy",
        """
        @test
        def test_diverges():
            assert (-7 // 2) == -3   # Sharpy's result; CPython floors to -4
        """,
    )
    led = _write_ledger_file(
        tmp_path,
        {
            "kind": "expected-fail-cpython",
            "module": "cpython_ef_tests",
            "test": "test_diverges",
            "deviation": "integer-division-floor",
        },
    )
    code = dual_run.main([str(tmp_path / "ported"), "--ledger", str(led)])
    assert code == 0


def test_dual_run_ledger_flags_unexplained(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_un_tests.spy",
        """
        @test
        def test_broken():
            assert 1 == 2
        """,
    )
    led = _write_ledger_file(tmp_path)  # empty
    code = dual_run.main([str(tmp_path / "ported"), "--ledger", str(led)])
    assert code == 1


def test_dual_run_ledger_flags_stale(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_st_tests.spy",
        """
        @test
        def test_passes_now():
            assert 1 == 1
        """,
    )
    led = _write_ledger_file(
        tmp_path,
        {
            "kind": "expected-fail-cpython",
            "module": "cpython_st_tests",
            "test": "test_passes_now",
            "deviation": "integer-division-floor",
        },
    )
    code = dual_run.main([str(tmp_path / "ported"), "--ledger", str(led)])
    assert code == 1


def test_dual_run_missing_ledger_is_usage_error(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_ok_tests.spy",
        """
        @test
        def test_ok():
            assert True
        """,
    )
    code = dual_run.main(
        [str(tmp_path / "ported"), "--ledger", str(tmp_path / "nope.yaml")]
    )
    assert code == 2


# --------------------------------------------------------------------------- #
# The committed ledger stays in sync with its sources
# --------------------------------------------------------------------------- #
def test_committed_ledger_is_up_to_date():
    """CI staleness guard, mirrored as a unit test: the committed ledger.yaml must
    equal a fresh regeneration from the repo catalog + pilot tree."""
    if not ledger.DEFAULT_LEDGER_PATH.exists():
        pytest.skip("ledger.yaml not generated")
    fresh = ledger.render_ledger(ledger.build_ledger())
    committed = ledger.DEFAULT_LEDGER_PATH.read_text(encoding="utf-8")
    assert committed == fresh, (
        "ledger.yaml is stale; run "
        "`python -m build_tools.cpython_oracle ledger --write`"
    )


def test_committed_pilot_annotation_is_valid():
    """The colorsys not-ported annotation must parse into exactly one entry."""
    data = ledger.build_ledger()
    not_ported = [e for e in data["entries"] if e["kind"] == "not-ported"]
    assert any(e.get("bug") == 1063 for e in not_ported)
