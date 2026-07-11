"""Tests for the differential-parse comparer (build_tools/differential_parse, #1037).

The comparer is the CPython half of the Sharpy parser differential-parse property
test. These check the two contracts the C# side relies on: the parse verdict
(ok / syntax-error) and the canonical landmark multiset, plus the JSONL batch
protocol that keeps one ``python3`` process serving many samples.
"""

from __future__ import annotations

import ast
import json

from build_tools.differential_parse import compare_ast


# --------------------------------------------------------------------------- #
# parse verdict
# --------------------------------------------------------------------------- #
def test_valid_source_reports_ok():
    verdict = compare_ast.parse_source("f(lambda x: x[0], (a, b))")
    assert verdict["ok"] is True
    assert "landmarks" in verdict
    assert "error" not in verdict


def test_syntax_error_reports_not_ok_with_message():
    verdict = compare_ast.parse_source("maybe x")
    assert verdict["ok"] is False
    assert isinstance(verdict["error"], str) and verdict["error"]


def test_sharpy_only_syntax_is_rejected_by_cpython():
    # These are Sharpy-only forms; the C# filter drops them, but if one leaks
    # through the comparer must still report a clean not-ok rather than crash.
    for src in ("x?", "try x", "ref y", "a ?? b"):
        assert compare_ast.parse_source(src)["ok"] is False


# --------------------------------------------------------------------------- #
# landmark multiset
# --------------------------------------------------------------------------- #
def test_landmarks_count_shared_shapes():
    counts = compare_ast.landmarks(ast.parse("f(lambda x: x[0], (a, b))"))
    assert counts == {"Call": 1, "Lambda": 1, "Subscript": 1, "Tuple": 1}


def test_multiaxis_subscript_surfaces_subscript_and_tuple():
    # x[a, b:c] is a multi-axis subscript in Sharpy; CPython models the index as
    # a Tuple, so both a Subscript and a Tuple landmark must appear. The C# side
    # mirrors this by emitting Subscript+Tuple for MultiAxisAccess.
    assert compare_ast.landmarks(ast.parse("x[a, b:c]")) == {"Subscript": 1, "Tuple": 1}


def test_super_call_counts_as_call():
    # super() is a bare keyword expression in Sharpy but a Call in CPython.
    assert compare_ast.landmarks(ast.parse("super()")) == {"Call": 1}


def test_ambiguous_leaf_kinds_are_not_counted():
    # Compare / BoolOp / BinOp / Name / Constant are intentionally excluded so
    # benign representational differences never register as a skeleton divergence.
    assert compare_ast.landmarks(ast.parse("a < b < c and d + e")) == {}


def test_landmark_tokens_are_exported_and_consistent():
    assert "Subscript" in compare_ast.LANDMARK_TOKENS
    assert "Tuple" in compare_ast.LANDMARK_TOKENS
    # Every value the counter can emit must be an advertised token.
    emitted = set(compare_ast.landmarks(ast.parse("f(x)[0].y")))
    assert emitted <= set(compare_ast.LANDMARK_TOKENS)


# --------------------------------------------------------------------------- #
# batch protocol
# --------------------------------------------------------------------------- #
def test_run_batch_preserves_ids_and_order_independence():
    lines = [
        json.dumps({"id": 7, "source": "x[a, b]"}),
        "",  # blank lines are skipped
        json.dumps({"id": 3, "source": "maybe x"}),
        json.dumps({"id": 9, "source": "super()"}),
    ]
    verdicts = [json.loads(line) for line in compare_ast.run_batch(lines)]

    by_id = {v["id"]: v for v in verdicts}
    assert set(by_id) == {7, 3, 9}
    assert by_id[7]["ok"] is True
    assert by_id[7]["landmarks"] == {"Subscript": 1, "Tuple": 1}
    assert by_id[3]["ok"] is False
    assert by_id[9]["landmarks"] == {"Call": 1}
