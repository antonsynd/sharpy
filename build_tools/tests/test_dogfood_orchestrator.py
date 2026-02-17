"""Unit tests for dogfood orchestrator pure functions.

Tests _outputs_equivalent, _numbers_close, and _is_internal_compiler_error
which were added/enhanced as part of the dogfood verification pipeline fixes.
"""

import pytest

from build_tools.sharpy_dogfood.orchestrator import (
    _is_internal_compiler_error,
    _numbers_close,
    _outputs_equivalent,
)


class TestNumbersClose:
    """Tests for _numbers_close() decimal-point-aware numeric comparison."""

    def test_both_float_within_tolerance(self):
        assert _numbers_close("3.14", "3.140000000000001", 1e-9) is True

    def test_both_float_outside_tolerance(self):
        assert _numbers_close("3.14", "3.20", 1e-9) is False

    def test_decimal_presence_mismatch_float_vs_int(self):
        assert _numbers_close("22.0", "22", 1e-9) is False

    def test_decimal_presence_mismatch_int_vs_float(self):
        assert _numbers_close("5", "5.0", 1e-9) is False

    def test_both_int_equal(self):
        assert _numbers_close("22", "22", 1e-9) is True

    def test_both_int_unequal(self):
        assert _numbers_close("22", "23", 1e-9) is False

    def test_negative_float_within_tolerance(self):
        assert _numbers_close("-3.14", "-3.140000000000001", 1e-9) is True

    def test_zero_with_small_delta(self):
        assert _numbers_close("0.0", "0.000000001", 1e-9) is True

    def test_zero_int_equal(self):
        assert _numbers_close("0", "0", 1e-9) is True

    def test_zero_decimal_vs_int(self):
        assert _numbers_close("0.0", "0", 1e-9) is False

    def test_one_point_zero_vs_one(self):
        assert _numbers_close("1.0", "1", 1e-9) is False


class TestOutputsEquivalent:
    """Tests for _outputs_equivalent() line-by-line output comparison."""

    # --- Exact matches ---

    def test_exact_match_single_line(self):
        assert _outputs_equivalent("hello", "hello") is True

    def test_exact_match_multiline(self):
        assert _outputs_equivalent("hello\nworld", "hello\nworld") is True

    def test_exact_match_integers(self):
        assert _outputs_equivalent("42", "42") is True

    # --- Pure number float tolerance ---

    def test_pure_float_tolerance_match(self):
        assert _outputs_equivalent("3.14", "3.140000000000001") is True

    def test_pure_float_tolerance_multiline(self):
        assert _outputs_equivalent(
            "3.14\n7.85", "3.140000000000001\n7.8500000000000005"
        ) is True

    # --- Pure number decimal format mismatch ---

    def test_pure_float_decimal_format_mismatch(self):
        assert _outputs_equivalent("22.0", "22") is False

    def test_pure_float_decimal_format_mismatch_reversed(self):
        assert _outputs_equivalent("22", "22.0") is False

    def test_five_point_zero_vs_five(self):
        assert _outputs_equivalent("5.0", "5") is False

    # --- Embedded number tolerance ---

    def test_embedded_float_tolerance_match(self):
        assert _outputs_equivalent(
            "result = 3.14", "result = 3.140000000000001"
        ) is True

    def test_embedded_multiple_floats(self):
        assert _outputs_equivalent(
            "x=3.14, y=2.71", "x=3.140000000000001, y=2.710000000000001"
        ) is True

    # --- Embedded number decimal format mismatch ---

    def test_embedded_decimal_format_mismatch(self):
        assert _outputs_equivalent("15 + 7 = 22.0", "15 + 7 = 22") is False

    # --- Non-numeric text mismatch ---

    def test_non_numeric_text_differs(self):
        assert _outputs_equivalent("result = 3.14", "answer = 3.14") is False

    # --- Line count mismatch ---

    def test_different_line_counts(self):
        assert _outputs_equivalent("a\nb", "a") is False

    def test_extra_trailing_line(self):
        assert _outputs_equivalent("a\nb\nc", "a\nb") is False

    # --- Mixed lines ---

    def test_multiline_mixed_types(self):
        assert _outputs_equivalent(
            "3.14\nhello\n42", "3.140000000000001\nhello\n42"
        ) is True

    def test_multiline_one_mismatch(self):
        assert _outputs_equivalent("3.14\nhello\n22.0", "3.14\nhello\n22") is False

    # --- Edge cases ---

    def test_empty_outputs(self):
        assert _outputs_equivalent("", "") is True

    def test_whitespace_stripping(self):
        assert _outputs_equivalent("  hello  ", "  hello  ") is True


class TestIsInternalCompilerError:
    """Tests for _is_internal_compiler_error() SPY09xx detection."""

    def test_spy0900_matches(self):
        assert _is_internal_compiler_error("error: SPY0900 CompilationFailed") is True

    def test_spy0904_matches(self):
        assert _is_internal_compiler_error("SPY0904: InvariantViolation") is True

    def test_spy0907_matches(self):
        assert _is_internal_compiler_error("SPY0907 UnexpectedUnknownType") is True

    def test_spy0200_does_not_match(self):
        assert _is_internal_compiler_error("SPY0200: Unknown symbol 'foo'") is False

    def test_spy0500_does_not_match(self):
        assert _is_internal_compiler_error("SPY0500: CodeGen error") is False

    def test_spy1000_does_not_match(self):
        assert _is_internal_compiler_error("SPY1000: info diagnostic") is False

    def test_empty_string(self):
        assert _is_internal_compiler_error("") is False

    def test_no_spy_code(self):
        assert _is_internal_compiler_error("some random error message") is False

    def test_multiline_with_spy09xx(self):
        assert _is_internal_compiler_error(
            "line 1 error\nSPY0907: unexpected\nmore text"
        ) is True
