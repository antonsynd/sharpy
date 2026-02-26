"""Tests for remediation hint generation and XML extraction in prompts."""

from sharpy_dogfood.prompts import (
    _get_remediation_hint,
    extract_code_from_xml,
    extract_expected_from_xml,
    extract_multifile_from_xml,
    has_unclosed_code_tags,
    extract_code_block,
    extract_multifile_code,
    extract_expected_output_from_response,
)


# =============================================================================
# Remediation hint tests
# =============================================================================


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


def test_remediation_hint_spy0414():
    hint = _get_remediation_hint("error SPY0414: direct dunder invocation")
    assert "builtin" in hint.lower()


def test_remediation_hint_spy0107_self_field():
    hint = _get_remediation_hint(
        "error SPY0107: unexpected token at self.name: str = name"
    )
    assert "self.field" in hint.lower() or "self.name" in hint.lower()


# =============================================================================
# XML extraction tests
# =============================================================================


class TestExtractCodeFromXml:
    """Tests for extract_code_from_xml()."""

    def test_basic_extraction(self):
        response = '<code>\nprint("hello")\n</code>'
        result = extract_code_from_xml(response)
        assert result == 'print("hello")'

    def test_with_surrounding_text(self):
        response = (
            "Here is the code:\n<code>\ndef main():\n    print(1)\n</code>\nDone."
        )
        result = extract_code_from_xml(response)
        assert result == "def main():\n    print(1)"

    def test_no_code_tag(self):
        response = "Just some text without code tags"
        result = extract_code_from_xml(response)
        assert result is None

    def test_does_not_match_file_attr(self):
        """Bare <code> extraction should not match <code file="..."> tags."""
        response = '<code file="main.spy">\nprint(1)\n</code>'
        result = extract_code_from_xml(response)
        # Should not match since it has a file attribute
        assert result is None

    def test_multiline_code(self):
        response = "<code>\nclass Foo:\n    x: int\n    def __init__(self):\n        self.x = 0\n</code>"
        result = extract_code_from_xml(response)
        assert "class Foo:" in result
        assert "self.x = 0" in result


class TestExtractExpectedFromXml:
    """Tests for extract_expected_from_xml()."""

    def test_basic_extraction(self):
        response = "<expected>\n42\n</expected>"
        result = extract_expected_from_xml(response)
        assert result == "42\n"

    def test_multiline_expected(self):
        response = "<expected>\nhello\nworld\n</expected>"
        result = extract_expected_from_xml(response)
        assert result == "hello\nworld\n"

    def test_no_expected_tag(self):
        response = "No expected output here"
        result = extract_expected_from_xml(response)
        assert result is None

    def test_empty_expected(self):
        response = "<expected>\n</expected>"
        result = extract_expected_from_xml(response)
        assert result is None

    def test_with_code_and_expected(self):
        response = "<code>\nprint(1)\n</code>\n<expected>\n1\n</expected>"
        result = extract_expected_from_xml(response)
        assert result == "1\n"


class TestExtractMultifileFromXml:
    """Tests for extract_multifile_from_xml()."""

    def test_basic_two_files(self):
        response = (
            '<code file="utils.spy">\ndef helper() -> int:\n    return 42\n</code>\n'
            '<code file="main.spy">\nfrom utils import helper\ndef main():\n    print(helper())\n</code>'
        )
        result = extract_multifile_from_xml(response)
        assert result is not None
        assert "utils.spy" in result
        assert "main.spy" in result
        assert "def helper()" in result["utils.spy"]
        assert "from utils import helper" in result["main.spy"]

    def test_no_main_spy(self):
        """Must have main.spy to be valid."""
        response = (
            '<code file="a.spy">\ndef a(): pass\n</code>\n'
            '<code file="b.spy">\ndef b(): pass\n</code>'
        )
        result = extract_multifile_from_xml(response)
        assert result is None

    def test_single_file_returns_none(self):
        """Must have at least 2 files."""
        response = '<code file="main.spy">\nprint(1)\n</code>'
        result = extract_multifile_from_xml(response)
        assert result is None

    def test_three_files(self):
        response = (
            '<code file="models.spy">\nclass Item:\n    pass\n</code>\n'
            '<code file="utils.spy">\ndef helper(): pass\n</code>\n'
            '<code file="main.spy">\ndef main(): pass\n</code>'
        )
        result = extract_multifile_from_xml(response)
        assert result is not None
        assert len(result) == 3

    def test_filename_case_normalization(self):
        response = (
            '<code file="Utils.spy">\ndef helper(): pass\n</code>\n'
            '<code file="Main.spy">\ndef main(): pass\n</code>'
        )
        result = extract_multifile_from_xml(response)
        assert result is not None
        assert "utils.spy" in result
        assert "main.spy" in result


class TestHasUnclosedCodeTags:
    """Tests for has_unclosed_code_tags()."""

    def test_balanced_tags(self):
        response = "<code>\nprint(1)\n</code>"
        assert has_unclosed_code_tags(response) is False

    def test_unclosed_tag(self):
        response = "<code>\nprint(1)\n"
        assert has_unclosed_code_tags(response) is True

    def test_multiple_balanced(self):
        response = (
            '<code file="a.spy">\nfoo\n</code>\n<code file="b.spy">\nbar\n</code>'
        )
        assert has_unclosed_code_tags(response) is False

    def test_one_unclosed_of_two(self):
        response = '<code file="a.spy">\nfoo\n</code>\n<code file="b.spy">\nbar\n'
        assert has_unclosed_code_tags(response) is True

    def test_no_tags(self):
        response = "Just some text"
        assert has_unclosed_code_tags(response) is False


class TestExtractCodeBlockXmlFallback:
    """Tests that extract_code_block tries XML first, then markdown."""

    def test_xml_preferred_over_markdown(self):
        response = "<code>\nxml_code()\n</code>\n```python\nmarkdown_code()\n```"
        result = extract_code_block(response)
        assert result == "xml_code()"

    def test_falls_back_to_markdown(self):
        response = "```python\nmarkdown_code()\n```"
        result = extract_code_block(response)
        assert result == "markdown_code()"

    def test_falls_back_to_raw_code(self):
        response = "def main():\n    print(1)"
        result = extract_code_block(response)
        assert "def main():" in result


class TestExtractMultifileCodeXmlFallback:
    """Tests that extract_multifile_code tries XML first, then markers."""

    def test_xml_preferred_over_markers(self):
        response = (
            '<code file="utils.spy">\ndef xml_helper(): pass\n</code>\n'
            '<code file="main.spy">\ndef main(): pass\n</code>\n'
            "=== FILE: utils.spy ===\ndef marker_helper(): pass\n"
            "=== FILE: main.spy ===\ndef main(): pass\n"
        )
        result = extract_multifile_code(response)
        assert result is not None
        assert "xml_helper" in result["utils.spy"]

    def test_falls_back_to_markers(self):
        response = (
            "=== FILE: utils.spy ===\ndef marker_helper(): pass\n\n"
            "=== FILE: main.spy ===\ndef main(): pass\n"
        )
        result = extract_multifile_code(response)
        assert result is not None
        assert "marker_helper" in result["utils.spy"]


class TestExtractExpectedOutputFromResponseXml:
    """Tests that extract_expected_output_from_response tries XML first."""

    def test_xml_expected(self):
        response = "<code>\nprint(42)\n</code>\n<expected>\n42\n</expected>"
        result = extract_expected_output_from_response(response)
        assert result == "42\n"

    def test_falls_back_to_comment(self):
        response = "```python\nprint(42)\n# EXPECTED OUTPUT:\n# 42\n```"
        result = extract_expected_output_from_response(response)
        assert result == "42\n"
