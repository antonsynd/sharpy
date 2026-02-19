"""Tests for remediation hint generation in retry prompts."""

from sharpy_dogfood.prompts import _get_remediation_hint


def test_remediation_hint_spy0456():
    hint = _get_remediation_hint("error SPY0456: __hash__ requires __eq__")
    assert "__eq__(self, other: object)" in hint


def test_remediation_hint_spy0018():
    hint = _get_remediation_hint("error SPY0018: unexpected character `")
    assert "backtick" in hint.lower()


def test_remediation_hint_spy0220_nullable_list():
    hint = _get_remediation_hint(
        "error SPY0220: cannot infer type for list[int?] from mixed literals"
    )
    assert ".append()" in hint


def test_remediation_hint_spy0301_no_exported_symbol():
    hint = _get_remediation_hint(
        "error SPY0301: module 'utils' has no exported symbol 'Helper'"
    )
    assert "case-sensitive" in hint


def test_remediation_hint_spy0907():
    hint = _get_remediation_hint("error SPY0907: internal compiler error")
    assert "simplifying" in hint


def test_remediation_hint_format_exception_hex():
    hint = _get_remediation_hint(
        "System.FormatException: Input string '0xFF' was not in a correct format"
    )
    assert "hex" in hint.lower()


def test_remediation_hint_no_match():
    hint = _get_remediation_hint("some unknown error")
    assert hint == ""


def test_remediation_hint_multiple_matches():
    """When multiple patterns match, all hints are included."""
    error = "error SPY0456: __hash__ requires __eq__; also SPY0018: backtick"
    hint = _get_remediation_hint(error)
    assert "__eq__" in hint
    assert "backtick" in hint.lower()
