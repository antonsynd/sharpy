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
    block_key,
    extract_blocks,
    load_allowlist,
    run_sweep,
    save_allowlist,
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
