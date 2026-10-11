"""Tests for build_tools/spec_blocks.py — spec-block sweep."""

import json
import os
import subprocess
import sys
import textwrap
from pathlib import Path
from unittest.mock import patch

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from spec_blocks import (
    Block,
    BlockResult,
    CompileResult,
    RunResult,
    block_key,
    extract_blocks,
    files_without_output_assertions,
    load_allowlist,
    normalize_output,
    report,
    run_sweep,
    save_allowlist,
    seed_output_candidates,
    wrap_in_main,
    _compile_block,
)


def _make_block(relpath: str, text: str, marker=None, expected_error=None):
    return Block(
        relpath=relpath,
        line=1,
        text=text,
        key=block_key(relpath, text),
        marker=marker,
        expected_error=expected_error,
    )


def _stub_compiler_success(sharpyc, source):
    return CompileResult(success=True)


def _stub_compiler_fail(code="SPY0200"):
    def _compile(sharpyc, source):
        diags = [{"severity": "error", "code": code, "line": 1, "column": 1, "message": "err"}]
        return CompileResult(success=False, first_error_code=code, stdout=json.dumps(diags))
    return _compile


def _stub_compiler_spy0340_only(sharpyc, source):
    if "def main() -> None:" in source:
        return CompileResult(success=True)
    diags = [{"severity": "error", "code": "SPY0340", "line": 1, "column": 1, "message": "top-level"}]
    return CompileResult(success=False, first_error_code="SPY0340", stdout=json.dumps(diags))


# ---------- Extraction ----------

def test_extraction_python_fence(tmp_path):
    md = tmp_path / "test.md"
    md.write_text("# Title\n\n```python\nx: int = 1\n```\n")
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 1
    assert blocks[0].text == "x: int = 1"
    assert blocks[0].relpath == "test.md"


def test_extraction_spy_fence(tmp_path):
    md = tmp_path / "test.md"
    md.write_text("# Title\n\n```spy\ny: str = 'hi'\n```\n")
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 1
    assert blocks[0].text == "y: str = 'hi'"


def test_extraction_fragment_marker(tmp_path):
    md = tmp_path / "test.md"
    md.write_text("some text\n<!-- spec-sweep: fragment -->\n```python\nx = 1\n```\n")
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 1
    assert blocks[0].marker == "fragment"


def test_extraction_error_marker(tmp_path):
    md = tmp_path / "test.md"
    md.write_text("some text\n<!-- spec-sweep: error SPY0200 -->\n```python\nx = bad\n```\n")
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 1
    assert blocks[0].marker == "error"
    assert blocks[0].expected_error == "SPY0200"


# ---------- Prelude (#1939) ----------

def test_prelude_applies_to_later_unmarked_block(tmp_path):
    md = tmp_path / "test.md"
    md.write_text(
        "<!-- spec-sweep: prelude -->\n```python\nclass Widget:\n    pass\n```\n\n"
        "```python\nw: Widget = Widget()\n```\n"
    )
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 2
    # The prelude block compiles standalone — no prelude prepended to itself.
    assert blocks[0].prelude is None
    # The later unmarked block carries the prelude's declaration.
    assert blocks[1].prelude == "class Widget:\n    pass"
    # The dependent block's KEY is the sha1 of its OWN text — preludes do not churn keys.
    assert blocks[1].key == block_key("test.md", "w: Widget = Widget()")


def test_prelude_not_applied_to_fragment_or_error(tmp_path):
    md = tmp_path / "test.md"
    md.write_text(
        "<!-- spec-sweep: prelude -->\n```python\nclass Widget:\n    pass\n```\n\n"
        "<!-- spec-sweep: fragment -->\n```python\nw.frobnicate()\n```\n\n"
        "<!-- spec-sweep: error SPY0200 -->\n```python\nx = bad\n```\n"
    )
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 3
    assert blocks[1].marker == "fragment" and blocks[1].prelude is None
    assert blocks[2].marker == "error" and blocks[2].prelude is None


def test_prelude_prepended_at_compile(tmp_path):
    # A block that only compiles WITH the prelude's declaration present proves the prepend fires.
    def stub(sharpyc, source):
        return CompileResult(success="class Widget" in source)

    dependent = Block(
        relpath="a.md", line=1, text="w: Widget = Widget()",
        key=block_key("a.md", "w: Widget = Widget()"),
        prelude="class Widget:\n    pass",
    )
    with patch("spec_blocks.compile_one", stub):
        br = _compile_block(("sharpyc", dependent))
    assert br.classification == "compiled_as_is"

    # Drop the prepend (mutation control): the same block now fails.
    dependent_no_prelude = Block(
        relpath="a.md", line=1, text="w: Widget = Widget()",
        key=block_key("a.md", "w: Widget = Widget()"), prelude=None,
    )
    with patch("spec_blocks.compile_one", stub):
        br2 = _compile_block(("sharpyc", dependent_no_prelude))
    assert br2.classification == "failing"


def test_prelude_block_itself_must_compile(tmp_path):
    # A prelude with a deliberate error is a normal unmarked block: it fails standalone and, unlisted,
    # reddens the sweep — a prelude cannot hide an error in the code it declares.
    prelude_block = Block(
        relpath="a.md", line=1, text="class Widget:\n    bad syntax",
        key=block_key("a.md", "class Widget:\n    bad syntax"), prelude=None,
    )
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0200")):
        results, reds = run_sweep("sharpyc", [prelude_block], {}, 1)
    assert results[0].classification == "failing"
    assert any(r.red for r in results)


# ---------- Block classification ----------

def test_block_compiles_as_is():
    block = _make_block("a.md", "x: int = 1")
    with patch("spec_blocks.compile_one", _stub_compiler_success):
        br = _compile_block(("sharpyc", block))
    assert br.classification == "compiled_as_is"


def test_block_compiles_wrapped():
    block = _make_block("a.md", "x: int = 1")
    with patch("spec_blocks.compile_one", _stub_compiler_spy0340_only):
        br = _compile_block(("sharpyc", block))
    assert br.classification == "compiled_wrapped"
    assert br.wrapped is True


def test_block_fragment_marker():
    block = _make_block("a.md", "partial code", marker="fragment")
    br = _compile_block(("sharpyc", block))
    assert br.classification == "fragment"


def test_block_error_marker_correct():
    block = _make_block("a.md", "bad code", marker="error", expected_error="SPY0200")
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0200")):
        br = _compile_block(("sharpyc", block))
    assert br.classification == "failing"
    # run_sweep promotes to error_asserted
    allowlist = {}
    results, reds = run_sweep.__wrapped__(block, allowlist) if hasattr(run_sweep, '__wrapped__') else (None, None)


def test_block_error_marker_but_compiles():
    block = _make_block("a.md", "good code", marker="error", expected_error="SPY0200")
    with patch("spec_blocks.compile_one", _stub_compiler_success):
        results, reds = run_sweep("sharpyc", [block], {}, 1)
    assert any(r.red for r in results)
    assert any("compiles" in r for r in reds)


def test_block_error_marker_matching_code_is_asserted():
    block = _make_block("a.md", "bad code", marker="error", expected_error="SPY0322")
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0322")):
        results, reds = run_sweep("sharpyc", [block], {}, 1)
    assert results[0].classification == "error_asserted"
    assert reds == []


def test_block_error_marker_wrong_code_is_red():
    # #2026: the marker names a code; a block failing with a DIFFERENT first error is red.
    block = _make_block("a.md", "bad code", marker="error", expected_error="SPY0322")
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0200")):
        results, reds = run_sweep("sharpyc", [block], {}, 1)
    assert results[0].red
    assert any("error marker SPY0322 but first error is SPY0200" in r for r in reds)


def test_allowlisted_still_failing():
    block = _make_block("a.md", "bad code")
    allowlist = {block.key: "SPY0200 first"}
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0200")):
        results, reds = run_sweep("sharpyc", [block], allowlist, 1)
    assert results[0].classification == "allowlisted"
    assert len(reds) == 0


def test_allowlisted_now_compiles():
    block = _make_block("a.md", "now good")
    allowlist = {block.key: "SPY0200 first"}
    with patch("spec_blocks.compile_one", _stub_compiler_success):
        results, reds = run_sweep("sharpyc", [block], allowlist, 1)
    assert any("STALE" in r for r in reds)


def test_allowlisted_key_missing():
    allowlist = {"nonexistent.md::abcdef1234": "SPY0200 first"}
    with patch("spec_blocks.compile_one", _stub_compiler_success):
        results, reds = run_sweep("sharpyc", [], allowlist, 1)
    assert any("no longer exists" in r for r in reds)


def test_unmarked_failing():
    block = _make_block("a.md", "bad code")
    with patch("spec_blocks.compile_one", _stub_compiler_fail("SPY0200")):
        results, reds = run_sweep("sharpyc", [block], {}, 1)
    assert results[0].classification == "failing"
    assert results[0].red is True
    assert any("unmarked" in r for r in reds)


def test_pool_includes_spy_fences(tmp_path):
    md = tmp_path / "test.md"
    md.write_text("```spy\na: int = 1\n```\n\n```python\nb: int = 2\n```\n")
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 2
    texts = {b.text for b in blocks}
    assert "a: int = 1" in texts
    assert "b: int = 2" in texts


# ---------- Allowlist I/O ----------

def test_allowlist_roundtrip(tmp_path):
    path = str(tmp_path / "allowlist.txt")
    entries = {"a.md::abc1234567": "SPY0200 first", "b.md::def9876543": "SPY0300 first"}
    save_allowlist(path, entries)
    loaded = load_allowlist(path)
    assert loaded == entries


# ---------- Wrap rule ----------

def test_wrap_in_main():
    source = "x: int = 1\nprint(x)"
    wrapped = wrap_in_main(source)
    assert "def main() -> None:" in wrapped
    assert "    x: int = 1" in wrapped
    assert "    print(x)" in wrapped


# ---------- Output marker (#2286) ----------

def _one_block(tmp_path, markdown):
    (tmp_path / "out.md").write_text(markdown)
    blocks = extract_blocks(str(tmp_path))
    assert len(blocks) == 1
    return blocks[0]


def test_output_inline_form(tmp_path):
    b = _one_block(tmp_path, '<!-- spec-sweep: output "3\\n4" -->\n```python\nprint(3)\nprint(4)\n```\n')
    assert (b.marker, b.output_form, b.expected_output, b.output_error) == ("output", "inline", "3\n4", None)


def test_output_fenced_form_skips_blank_lines(tmp_path):
    b = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nprint(1)\n```\n\n```output\n1\n```\n")
    assert (b.output_form, b.expected_output) == ("fenced", "1")
    b2 = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nprint(1)\n```\n```text\n1\n```\n")
    assert (b2.output_form, b2.expected_output) == ("fenced", "1")


def test_output_comment_form(tmp_path):
    b = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nx: int = 2\nprint(x + 1)  # 3\nprint('#')  # #\n```\n")
    assert (b.output_form, b.expected_output, b.output_error) == ("comment", "3\n#", None)


def test_output_comment_form_needs_every_print_commented(tmp_path):
    b = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nprint(1)  # 1\nprint(2)\n```\n")
    assert b.expected_output is None
    assert "line 2 prints without" in b.output_error


def test_output_marker_with_nothing_to_assert(tmp_path):
    b = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nx: int = 1\n```\n")
    assert "no `print(...)  # value` line" in b.output_error


def test_output_inline_and_fence_together_is_ambiguous(tmp_path):
    b = _one_block(tmp_path, '<!-- spec-sweep: output "1" -->\n```python\nprint(1)\n```\n```output\n1\n```\n')
    assert "both an inline string and" in b.output_error


def test_output_bare_following_fence_is_not_the_fenced_form(tmp_path):
    # Only ```output / ```text is an expected-output block; a bare fence is prose, so this block falls
    # back to the comment form and states nothing.
    b = _one_block(tmp_path, "<!-- spec-sweep: output -->\n```python\nprint(1)\n```\n```\n1\n```\n")
    assert b.output_form == "comment" and b.output_error is not None


def test_output_block_takes_prelude(tmp_path):
    (tmp_path / "p.md").write_text(
        "<!-- spec-sweep: prelude -->\n```python\nclass W:\n    pass\n```\n\n"
        '<!-- spec-sweep: output "ok" -->\n```python\nprint("ok")\n```\n'
    )
    blocks = extract_blocks(str(tmp_path))
    assert blocks[1].marker == "output" and blocks[1].prelude == "class W:\n    pass"


def test_normalize_output_rule():
    assert normalize_output("a  \r\nb\n\n\n") == ["a", "b"]
    assert normalize_output(" a") == [" a"]  # leading whitespace is significant
    assert normalize_output("") == []


def _output_block(expected, form="inline", error=None, text='print("x")'):
    return Block(relpath="o.md", line=3, text=text, key=block_key("o.md", text),
                 marker="output", expected_output=expected, output_form=form, output_error=error)


def _stub_run(output, ran=True, exit_code=0):
    def _run(sharpyc, source):
        _stub_run.sources.append(source)
        return RunResult(ran=ran, output=output, exit_code=exit_code)
    _stub_run.sources = []
    return _run


def _sweep_output(block, run_stub, compile_stub=_stub_compiler_success, allowlist=None):
    with patch("spec_blocks.compile_one", compile_stub), patch("spec_blocks.run_one", run_stub):
        return run_sweep("sharpyc", [block], allowlist or {}, 1)


def test_output_asserted_when_stdout_matches():
    results, reds = _sweep_output(_output_block("x"), _stub_run("x\n"))
    assert results[0].classification == "output_asserted" and reds == []


def test_output_mismatch_is_red_with_a_diff():
    # The mutation the issue names: one expected value changed -> red.
    results, reds = _sweep_output(_output_block("4"), _stub_run("3\n"))
    assert results[0].classification == "output_mismatch" and results[0].red
    assert any("output mismatch (inline form)" in r and "-4" in r and "+3" in r for r in reds)


def test_output_block_that_does_not_compile_is_red_not_allowlistable():
    block = _output_block("x")
    results, reds = _sweep_output(block, _stub_run("x"), compile_stub=_stub_compiler_fail("SPY0200"))
    assert any("does not compile (first error: SPY0200)" in r for r in reds)
    results, reds = _sweep_output(block, _stub_run("x"), allowlist={block.key: "SPY0200 first"})
    assert any("output-marked block is in the allowlist" in r for r in reds)


def test_output_block_that_exits_nonzero_or_never_runs_is_red():
    _, reds = _sweep_output(_output_block("x"), _stub_run("x", exit_code=1))
    assert any("exited 1" in r for r in reds)
    _, reds = _sweep_output(_output_block("x"), _stub_run("", ran=False))
    assert any("did not run" in r for r in reds)


def test_malformed_output_marker_is_red_and_never_runs():
    run = _stub_run("x")
    _, reds = _sweep_output(_output_block(None, form="comment", error="output marker but nothing"), run)
    assert any("output marker but nothing" in r for r in reds)
    assert _stub_run.sources == []


def test_output_block_runs_in_the_shape_that_compiled():
    # A block that compiles only wrapped in main() is RUN wrapped too.
    run = _stub_run("x")
    _sweep_output(_output_block("x"), run, compile_stub=_stub_compiler_spy0340_only)
    assert _stub_run.sources and "def main() -> None:" in _stub_run.sources[0]


def test_report_counts_output_classes():
    asserted, _ = _sweep_output(_output_block("x"), _stub_run("x"))
    mismatched, _ = _sweep_output(_output_block("y"), _stub_run("x"))
    line = report(asserted + mismatched)
    assert "output-asserted 1" in line and "output-mismatch 1" in line


def test_files_without_output_assertions_are_listed():
    plain = _make_block("a.md", "x: int = 1")
    marked = _output_block("x")
    assert files_without_output_assertions([plain, marked]) == ["a.md"]
    assert files_without_output_assertions([marked]) == []


def test_seed_output_lists_unmarked_blocks_by_form(tmp_path):
    (tmp_path / "s.md").write_text(
        "```python\nprint(1)  # 1\n```\n\n"
        "```python\nprint(1)  # 1\nprint(2)\n```\n\n"
        "```python\nprint(1)\n```\n\n```\n1\n```\n\n"
        "This prints the value:\n\n```python\nx: int = 1\n```\n\n"
        "```python\ny: int = 2\n```\n\n"
        "<!-- spec-sweep: output -->\n```python\nprint(9)  # 9\n```\n"
    )
    forms = [(line, form) for _, line, form, _ in seed_output_candidates(str(tmp_path))]
    assert forms == [(1, "comment"), (5, "comment-partial"), (10, "fence-follows"), (20, "prose")]


# ---------- Real compiler smoke test ----------

@pytest.mark.skipif(
    not os.environ.get("SHARPYC") and not os.path.exists("src/Sharpy.Cli"),
    reason="needs sharpyc",
)
def test_real_compiler_smoke():
    from spec_blocks import compile_one

    sharpyc = os.environ.get("SHARPYC", "dotnet run --project src/Sharpy.Cli --")
    good = compile_one(sharpyc, "def main() -> None:\n    x: int = 1\n    print(x)\n")
    assert good.success is True

    bad = compile_one(sharpyc, "x: int = 'not an int'\n")
    assert bad.success is False
    assert bad.first_error_code is not None
