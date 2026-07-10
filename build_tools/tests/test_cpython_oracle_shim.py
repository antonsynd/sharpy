"""Tests for the CPython dual-execution shim + runner (build_tools/cpython_oracle, #1030).

These exercise the shim primitives and the ``dual_run`` file runner on small
synthetic ``.spy`` snippets written to a tmp dir — never on the real ported files,
so they stay fast and self-contained under the ``python-build-tools`` workflow.
"""

from __future__ import annotations

import textwrap
from pathlib import Path

import pytest

from build_tools.cpython_oracle import dual_run, shim


# --------------------------------------------------------------------------- #
# @test decorator + discovery
# --------------------------------------------------------------------------- #
def test_bare_test_decorator_marks_function():
    @shim.test
    def sample():
        return 1

    assert getattr(sample, shim.TEST_MARKER) is True
    assert sample() == 1


def test_discover_tests_preserves_definition_order():
    ns: dict = {}
    exec(
        textwrap.dedent(
            """
            def _helper():
                pass

            @test
            def test_b():
                pass

            @test
            def test_a():
                pass
            """
        ),
        {"test": shim.test, "__builtins__": __builtins__, **ns},
        ns,
    )
    names = [name for name, _ in shim.discover_tests(ns)]
    assert names == ["test_b", "test_a"]
    assert "_helper" not in names


def test_skip_subdecorator_marks_and_skips():
    @shim.test.skip("not ready")
    def sample():
        raise AssertionError("should not run")

    assert getattr(sample, shim.TEST_MARKER) is True
    assert sample.__spy_skip__ == (True, "not ready")


def test_parametrize_spreads_rows():
    seen = []

    @shim.test.parametrize("x, y", [(1, 2), (3, 4)])
    def sample(x, y):
        seen.append((x, y))

    sample()
    assert seen == [(1, 2), (3, 4)]


# --------------------------------------------------------------------------- #
# approx
# --------------------------------------------------------------------------- #
def test_approx_abs_tolerance():
    assert 1.0000001 == shim.approx(1.0, abs=1e-6)
    assert not (1.001 == shim.approx(1.0, abs=1e-6))


def test_approx_abs_takes_precedence_over_places():
    # places=7 would reject, but abs=1e-2 accepts (abs wins).
    assert 1.005 == shim.approx(1.0, places=7, abs=1e-2)


def test_approx_places_default():
    # Round(actual-expected, 7) == 0 -> equal to 7 decimals.
    assert 0.30000000 == shim.approx(0.3)
    assert not (0.3001 == shim.approx(0.3))


def test_approx_symmetric_and_reflected():
    # float.__eq__ returns NotImplemented, Python reflects to _Approx.__eq__.
    assert shim.approx(1.0, abs=1e-6) == 1.0000001
    assert 1.0000001 == shim.approx(1.0, abs=1e-6)


# --------------------------------------------------------------------------- #
# install() monkeypatches unittest.approx
# --------------------------------------------------------------------------- #
def test_install_patches_unittest_approx():
    import unittest

    shim.install()
    assert hasattr(unittest, "approx")
    assert unittest.approx(1.0, abs=1e-6) == 1.0000001


# --------------------------------------------------------------------------- #
# dual_run.run_file
# --------------------------------------------------------------------------- #
def _write_spy(tmp_path: Path, name: str, body: str) -> Path:
    path = tmp_path / name
    path.write_text(textwrap.dedent(body), encoding="utf-8")
    return path


def test_run_file_passes_on_good_port(tmp_path: Path):
    shim.install()
    spy = _write_spy(
        tmp_path,
        "cpython_good_tests.spy",
        """
        from unittest import approx

        import bisect

        @test
        def test_bisect_right():
            assert bisect.bisect_right([1, 2, 3], 2) == 2

        @test
        def test_approx_ok():
            assert 0.1 + 0.2 == approx(0.3, abs=1e-9)
        """,
    )
    outcome = dual_run.run_file(spy)
    assert outcome.ok
    assert outcome.passed == 2
    assert outcome.failed == 0


def test_run_file_reports_failing_assertion(tmp_path: Path):
    shim.install()
    spy = _write_spy(
        tmp_path,
        "cpython_bad_tests.spy",
        """
        @test
        def test_wrong():
            assert 1 == 2
        """,
    )
    outcome = dual_run.run_file(spy)
    assert not outcome.ok
    assert outcome.failed == 1
    assert outcome.outcomes[0].name == "test_wrong"


def test_run_file_reports_load_error(tmp_path: Path):
    spy = _write_spy(
        tmp_path,
        "cpython_broken_tests.spy",
        """
        @test
        def test_syntax(:
            pass
        """,
    )
    outcome = dual_run.run_file(spy)
    assert outcome.load_error
    assert not outcome.ok


def test_run_file_honors_typed_annotations(tmp_path: Path):
    # PEP 526 typed locals + function annotations are plain Python and must run.
    shim.install()
    spy = _write_spy(
        tmp_path,
        "cpython_typed_tests.spy",
        """
        def helper(a: float, b: float) -> float:
            return a + b

        @test
        def test_typed():
            data: list[int] = [3, 1, 2]
            data.sort()
            assert data == [1, 2, 3]
            assert helper(1.0, 2.0) == 3.0
        """,
    )
    outcome = dual_run.run_file(spy)
    assert outcome.ok
    assert outcome.passed == 1


# --------------------------------------------------------------------------- #
# dual_run.main over a directory
# --------------------------------------------------------------------------- #
def test_main_directory_all_pass(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_a_tests.spy",
        """
        @test
        def test_ok():
            assert 1 == 1
        """,
    )
    assert dual_run.main([str(tmp_path)]) == 0


def test_main_returns_1_on_failure(tmp_path: Path):
    _write_spy(
        tmp_path,
        "cpython_b_tests.spy",
        """
        @test
        def test_fail():
            assert False
        """,
    )
    assert dual_run.main([str(tmp_path)]) == 1


def test_main_returns_2_on_missing_path():
    assert dual_run.main(["/nonexistent/path/here"]) == 2


def test_main_returns_2_on_empty_directory(tmp_path: Path):
    assert dual_run.main([str(tmp_path)]) == 2


def test_pilot_ports_pass_under_cpython():
    """The three committed pilots must dual-execute cleanly (guards regressions)."""
    repo_root = Path(__file__).resolve().parents[2]
    pilots = repo_root / "src/Sharpy.Stdlib.Tests/Spy/cpython"
    if not pilots.is_dir():
        pytest.skip("pilot port directory not present")
    assert dual_run.main([str(pilots)]) == 0
