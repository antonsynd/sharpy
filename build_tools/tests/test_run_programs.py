"""Tests for the differential-execution runner (build_tools/differential_exec).

The runner is the CPython half of the Sharpy differential-EXECUTION oracle. These
check the contracts the C# side relies on: the per-program verdict shape
(ok / stdout / stderr / exit / timed_out / syntax_error), that a parse failure is
reported as ``syntax_error`` (so the harness can treat Sharpy-only annotations as
out-of-subset rather than a runtime divergence), timeout handling, and the JSONL
batch protocol that keeps one ``python3`` process driving many programs.
"""

from __future__ import annotations

import json

from build_tools.differential_exec import run_programs


# --------------------------------------------------------------------------- #
# per-program verdict
# --------------------------------------------------------------------------- #
def test_clean_program_reports_ok_and_stdout():
    verdict = run_programs.run_one("print(1 + 2)")
    assert verdict["ok"] is True
    assert verdict["stdout"] == "3\n"
    assert verdict["exit"] == 0
    assert verdict["timed_out"] is False
    assert verdict["syntax_error"] is False


def test_runtime_error_reports_not_ok_without_syntax_flag():
    verdict = run_programs.run_one("raise ValueError('boom')")
    assert verdict["ok"] is False
    assert verdict["exit"] != 0
    assert verdict["syntax_error"] is False
    assert "ValueError" in verdict["stderr"]


def test_syntax_error_is_flagged():
    # A Sharpy-only annotation like `T?` is not valid Python; the runner must
    # distinguish this parse failure so the harness can skip it (out of subset).
    verdict = run_programs.run_one("def f(x: int?) -> None:\n    pass\n")
    assert verdict["ok"] is False
    assert verdict["syntax_error"] is True


def test_indentation_error_is_flagged_as_syntax():
    verdict = run_programs.run_one("def f():\nprint(1)\n")
    assert verdict["ok"] is False
    assert verdict["syntax_error"] is True


def test_timeout_is_flagged():
    verdict = run_programs.run_one("while True:\n    pass\n", timeout=1.0)
    assert verdict["ok"] is False
    assert verdict["timed_out"] is True
    assert verdict["syntax_error"] is False


def test_deterministic_hashing_env():
    # PYTHONHASHSEED is pinned so set/dict hash ordering is stable run-to-run; two
    # runs of the same program must produce identical stdout.
    src = "print(sorted({3, 1, 2}))\nprint({'b': 2, 'a': 1})"
    first = run_programs.run_one(src)
    second = run_programs.run_one(src)
    assert first["ok"] is True
    assert first["stdout"] == second["stdout"]


# --------------------------------------------------------------------------- #
# batch protocol
# --------------------------------------------------------------------------- #
def test_run_batch_preserves_ids_and_skips_blank_lines():
    lines = [
        json.dumps({"id": 7, "source": "print('a' * 3)"}),
        "",  # blank lines are skipped
        json.dumps({"id": 3, "source": "def f(x: str?):\n    pass\n"}),
        json.dumps({"id": 9, "source": "print(-7 % 3)"}),
    ]
    verdicts = [json.loads(line) for line in run_programs.run_batch(lines, timeout=5.0)]

    by_id = {v["id"]: v for v in verdicts}
    assert set(by_id) == {7, 3, 9}
    assert by_id[7]["ok"] is True and by_id[7]["stdout"] == "aaa\n"
    assert by_id[3]["ok"] is False and by_id[3]["syntax_error"] is True
    assert by_id[9]["ok"] is True and by_id[9]["stdout"] == "2\n"
