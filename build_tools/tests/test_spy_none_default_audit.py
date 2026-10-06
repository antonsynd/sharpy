"""Tests for build_tools/spy_none_default_audit.py — spy `= None` defaults vs CPython (#2236, R-EV).

The repo test (`test_spy_none_defaults_match_cpython`) is the standing ratchet; the rest pin the
instrument so the ratchet cannot pass vacuously (a parser that finds nothing, a classifier that
calls everything `pass`).
"""
from __future__ import annotations

import sys
import textwrap
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import spy_none_default_audit as audit_mod
from spy_none_default_audit import (
    SpyParam,
    classify,
    collect_none_defaults,
    parse_spy,
    read_modules,
    violations,
    Verdict,
)

_SENTINEL = object()


def _cell(module="m", cls=None, func="f", param="p"):
    return SpyParam(module, "m.spy", 1, cls, func, param, "None")


# ── the standing ratchet ──────────────────────────────────────────────────────


def test_spy_none_defaults_match_cpython():
    verdicts = audit_mod.audit(collect_none_defaults())
    errors = violations(verdicts, audit_mod.load_allowlist(), audit_mod.SKIPS)
    assert not errors, "\n".join(errors) + "\n\n" + audit_mod.render(verdicts)


def test_universe_positive_controls():
    """Known None-default cells must be found, else the ratchet above passes vacuously."""
    keys = {c.key for c in collect_none_defaults()}
    # A module function, a class constructor (pass), and a class method.
    assert "csv::dict_reader::fieldnames" in keys
    assert "csv::CsvDictReader.__init__::fieldnames" in keys
    assert "re::error.__init__::pos" in keys
    assert "re::Pattern.search::endpos" in keys


def test_every_mapped_module_imports_in_cpython():
    import importlib
    modules = read_modules()
    assert len(modules) >= 18
    for stem, python_name in modules:
        assert (audit_mod.SPY_DIR / f"{stem}.spy").is_file(), stem
        importlib.import_module(python_name)


# ── classifier ────────────────────────────────────────────────────────────────


def test_classify_sentinel_default_is_a_finding():
    def f(address, timeout=_SENTINEL):
        pass
    v = classify(_cell(param="timeout"), f)
    assert v.status == "finding"
    assert v.cpython == "<sentinel object()>"


def test_classify_value_default_is_a_finding():
    def f(s, endpos=sys.maxsize):
        pass
    assert classify(_cell(param="endpos"), f).status == "finding"


def test_classify_required_param_is_a_finding():
    def f(x):
        pass
    v = classify(_cell(param="x"), f)
    assert (v.status, v.cpython) == ("finding", "<required>")


def test_classify_none_default_passes():
    def f(x=None):
        pass
    assert classify(_cell(param="x"), f).status == "pass"


def test_classify_absent_param_and_uninspectable_are_skips():
    def f(y=None):
        pass
    assert classify(_cell(param="x"), f).status == "skip"
    assert classify(_cell(param="x"), 42).status == "skip"


def test_resolve_maps_spy_class_names_and_constructors():
    import csv
    import re
    assert audit_mod.resolve(_cell("csv", "CsvDictReader", "__init__", "fieldnames")) is csv.DictReader
    assert audit_mod.resolve(_cell("re", "Pattern", "search", "endpos")) is re.Pattern.search
    assert isinstance(audit_mod.resolve(_cell("csv", None, "dict_reader", "fieldnames")), str)


# ── ratchet logic ─────────────────────────────────────────────────────────────


def _v(status, key_param="p"):
    return Verdict(_cell(param=key_param), status, "x")


def test_unlisted_finding_fails():
    assert any("NEW FINDING" in e for e in violations([_v("finding")], {}, {}))


def test_listed_finding_passes():
    assert violations([_v("finding")], {"m::f::p": "#2236 why"}, {}) == []


def test_stale_allowlist_row_fails():
    errs = violations([_v("pass")], {"m::f::p": "#2236 why"}, {})
    assert any("STALE allowlist" in e for e in errs)


def test_uncited_allowlist_row_fails():
    errs = violations([_v("finding")], {"m::f::p": "no issue"}, {})
    assert any("UNCITED" in e for e in errs)


def test_unclassified_skip_fails_and_stale_skip_fails():
    assert any("UNCLASSIFIED" in e for e in violations([_v("skip")], {}, {}))
    assert violations([_v("skip")], {}, {"m::f::p": "reason"}) == []
    assert any("STALE skip" in e for e in violations([_v("pass")], {}, {"m::f::p": "reason"}))


# ── spy parser ────────────────────────────────────────────────────────────────


def test_parse_multiline_generic_and_class_methods():
    src = textwrap.dedent('''\
        """Module docstring with def fake(x = None): inside."""

        def top[T: IComparable[T]](a: list[T],
                                   key: (T, T) -> int | None = None,
                                   s: str = "a, b = None") -> T:
            """Docstring
        class NotAClass:
            def nope(self, q=None):
            """
            return a[0]

        @final
        class Box(IDisposable):
            # def commented(self, c=None):
            def __init__(self, v: dict[str, int] | None = None, n: int = 0):
                pass

            def get(self, i: int = 0, j: int | None = None) -> int:
                return 0

        def after(z: float | None = None) -> None:
            pass
        ''')
    got = [(cls, f, p, d) for _, cls, f, p, d in parse_spy(src)]
    assert got == [
        (None, "top", "a", None),
        (None, "top", "key", "None"),
        (None, "top", "s", '"' + " " * len("a, b = None") + '"'),  # string masked
        ("Box", "__init__", "v", "None"),
        ("Box", "__init__", "n", "0"),
        ("Box", "get", "i", "0"),
        ("Box", "get", "j", "None"),
        (None, "after", "z", "None"),
    ]


def test_collect_dedupes_overloads(tmp_path):
    (tmp_path / "x_module.spy").write_text(textwrap.dedent('''\
        def f(a: int, t: float | None = None) -> int:
            return a

        def f(a: int, b: int, t: float | None = None) -> int:
            return a

        def g(a: int) -> int:
            return a
        '''))
    cells = collect_none_defaults(tmp_path, modules=[("x_module", "x")])
    assert [c.key for c in cells] == ["x::f::t"]
