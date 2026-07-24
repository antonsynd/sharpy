"""
Unit tests for the property-stress crash watchdog helpers in ``build_sharpy`` (#1033).

Covers:
- ``generate_cscheck_seed`` — produces valid CsCheck seed strings (strict base-64
  format; a malformed ``CsCheck_Seed`` aborts every test in the invocation).
- ``extract_crashing_tests`` — names the test(s) in flight from a ``--blame-crash``
  ``Sequence_*.xml`` file.

``build_sharpy`` is an extensionless executable, so it is loaded via
``SourceFileLoader`` rather than a normal import.
"""

import importlib.util
from importlib.machinery import SourceFileLoader
from pathlib import Path

import pytest

# CsCheck's SeedString.Chars64 alphabet (verified against CsCheck 4.7.0).
CSCHECK_CHARS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-"


def _load_build_sharpy():
    path = Path(__file__).parent.parent / "bin" / "build_sharpy"
    loader = SourceFileLoader("build_sharpy_module", str(path))
    spec = importlib.util.spec_from_loader("build_sharpy_module", loader)
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)
    return module


bs = _load_build_sharpy()


def _reference_encode(state: int) -> str:
    """Independent re-implementation of CsCheck's SeedString.ToString(state, 0)."""
    s = state & 0xFFFFFFFFFFFFFFFF
    return "".join(CSCHECK_CHARS[(s >> (6 * i)) & 63] for i in range(10, -1, -1)) + "0"


class TestGenerateCsCheckSeed:
    def test_seed_is_twelve_char_base64(self):
        for _ in range(200):
            seed = bs.generate_cscheck_seed()
            assert len(seed) == 12, f"expected 12 chars, got {seed!r}"
            assert all(c in CSCHECK_CHARS for c in seed), f"non-base64 char in {seed!r}"

    def test_seed_stream_digit_is_zero(self):
        # stream=0 encoding always ends in the '0' digit.
        for _ in range(50):
            assert bs.generate_cscheck_seed().endswith("0")

    def test_seeds_are_distinct(self):
        seeds = {bs.generate_cscheck_seed() for _ in range(500)}
        # 64-bit state space — collisions are astronomically unlikely.
        assert len(seeds) == 500

    @pytest.mark.parametrize(
        "state",
        [0, 1, 63, 64, 65, 4096, 1234567890, 2**32, 2**64 - 1],
    )
    def test_matches_cscheck_encoder_for_known_states(self, state, monkeypatch):
        monkeypatch.setattr(bs.random, "getrandbits", lambda _bits: state)
        assert bs.generate_cscheck_seed() == _reference_encode(state)


class TestExtractCrashingTests:
    def _write(self, tmp_path: Path, body: str) -> Path:
        p = tmp_path / "Sequence_abc.xml"
        p.write_text(body)
        return p

    def test_names_the_incomplete_test(self, tmp_path):
        seq = self._write(
            tmp_path,
            '<?xml version="1.0"?>\n<TestSequence>\n'
            '  <Test Name="Foo.Bar" DisplayName="Foo.Bar" '
            'Source="x.dll" Completed="False" />\n'
            "</TestSequence>\n",
        )
        assert bs.extract_crashing_tests(seq) == "Foo.Bar"

    def test_prefers_incomplete_over_completed(self, tmp_path):
        seq = self._write(
            tmp_path,
            "<TestSequence>\n"
            '  <Test Name="Done.One" Completed="True" />\n'
            '  <Test Name="Running.Two" Completed="False" />\n'
            "</TestSequence>\n",
        )
        assert bs.extract_crashing_tests(seq) == "Running.Two"

    def test_multiple_incomplete_joined(self, tmp_path):
        seq = self._write(
            tmp_path,
            "<TestSequence>\n"
            '  <Test Name="A.One" Completed="False" />\n'
            '  <Test Name="B.Two" Completed="False" />\n'
            "</TestSequence>\n",
        )
        assert bs.extract_crashing_tests(seq) == "A.One, B.Two"

    def test_falls_back_to_any_name_when_none_incomplete(self, tmp_path):
        seq = self._write(
            tmp_path,
            '<TestSequence>\n  <Test Name="Only.Test" Completed="True" />\n</TestSequence>\n',
        )
        assert bs.extract_crashing_tests(seq) == "Only.Test"

    def test_unknown_for_empty_sequence(self, tmp_path):
        seq = self._write(tmp_path, "<TestSequence>\n</TestSequence>\n")
        assert bs.extract_crashing_tests(seq) == "(unknown)"

    def test_unknown_for_missing_file(self, tmp_path):
        assert bs.extract_crashing_tests(tmp_path / "nope.xml") == "(unknown)"
