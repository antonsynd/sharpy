"""Tests for the CPython numeric-literal oracle (build_tools/differential_lex, #2300).

The oracle is the CPython half of ``NumericLiteralTwinTests``. These pin the cells the P68
plan measured on CPython 3.12 (its "Measured cells" table) at the oracle's altitude —
``ast.parse(source, mode="eval")`` is one numeric ``Constant`` or the spelling is refused —
plus the JSONL batch protocol that keeps one ``python3`` process serving every spelling.
"""

from __future__ import annotations

import json

import pytest

from build_tools.differential_lex import number_oracle


# --------------------------------------------------------------------------- #
# verdicts: the measured cells
# --------------------------------------------------------------------------- #
@pytest.mark.parametrize(
    "source, kind, value",
    [
        (".5e-3", "float", "0.0005"),
        ("0x_FF", "int", "255"),
        ("0_0", "int", "0"),
        ("0XFF", "int", "255"),
        ("1E+5", "float", "100000.0"),
        ("1_000.000_1", "float", "1000.0001"),
        ("00", "int", "0"),
        # Trailing-dot floats are numbers to CPython; Sharpy refuses them by design
        # (float_literals.md) and the twin routes them to its Sharpy-only rows.
        ("1.", "float", "1.0"),
        ("1.e5", "float", "100000.0"),
        # Leading zeros are allowed in a float's integer part.
        ("01.5", "float", "1.5"),
        ("0B101", "int", "5"),
        ("0O755", "int", "493"),
        ("1e500", "float", "inf"),
    ],
)
def test_numeric_constant_is_ok_with_kind_and_repr(source, kind, value):
    assert number_oracle.classify(source) == {"ok": True, "kind": kind, "value": value}


@pytest.mark.parametrize(
    "source, message",
    [
        (".1__2", "invalid decimal literal"),
        ("0b1_", "invalid binary literal"),
        ("0o8", "invalid digit '8' in octal literal"),
        ("01", "leading zeros in decimal integer literals are not permitted"),
        # ast.parse tokenizes `.5e` as NUMBER `.5` + NAME `e`; at the constant altitude it is
        # simply refused, which is why the twin does not compare token boundaries.
        (".5e", "invalid decimal literal"),
        ("1e5.", "invalid syntax"),
    ],
)
def test_refused_spelling_reports_the_syntax_error(source, message):
    verdict = number_oracle.classify(source)
    assert verdict["ok"] is False
    assert verdict["error"].startswith(message)


def test_imaginary_constant_is_refused_as_imaginary():
    assert number_oracle.classify("1j") == {"ok": False, "error": "imaginary"}


@pytest.mark.parametrize("source", ["_1", "0xFFe+5", "-1", "True", "x"])
def test_non_numeric_constant_programs_are_refused(source):
    # A Name, a BinOp, a UnaryOp and a bool constant are not ONE numeric literal.
    verdict = number_oracle.classify(source)
    assert verdict["ok"] is False
    assert verdict["error"] != "imaginary"


# --------------------------------------------------------------------------- #
# batch protocol
# --------------------------------------------------------------------------- #
def test_run_batch_preserves_ids_out_of_order_and_skips_blank_lines():
    lines = [
        json.dumps({"id": 7, "source": ".5e-3"}),
        "",
        json.dumps({"id": 3, "source": "01"}),
        json.dumps({"id": 5, "source": "1j"}),
    ]
    verdicts = [json.loads(line) for line in number_oracle.run_batch(lines)]

    assert [v["id"] for v in verdicts] == [7, 3, 5]
    by_id = {v["id"]: v for v in verdicts}
    assert by_id[7] == {"id": 7, "ok": True, "kind": "float", "value": "0.0005"}
    assert by_id[3]["ok"] is False
    assert by_id[5] == {"id": 5, "ok": False, "error": "imaginary"}


def test_main_reads_a_batch_file(tmp_path, capsys):
    batch = tmp_path / "batch.jsonl"
    batch.write_text(json.dumps({"id": 1, "source": "0x_FF"}) + "\n", encoding="utf-8")
    assert number_oracle.main(["number_oracle.py", "--batch", str(batch)]) == 0
    out = capsys.readouterr().out.strip().splitlines()
    assert [json.loads(line) for line in out] == [{"id": 1, "ok": True, "kind": "int", "value": "255"}]
