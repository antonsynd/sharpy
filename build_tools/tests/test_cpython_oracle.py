"""Unit tests for the CPython test-suite classifier (build_tools/cpython_oracle, #1030).

These exercise the classifier heuristics on small synthetic snippets — never on
real CPython files — so they run under the existing python-build-tools workflow
(`python -m pytest build_tools/tests/`).
"""

import textwrap

from build_tools.cpython_oracle import classify_source
from build_tools.cpython_oracle.classifier import Category
from build_tools.cpython_oracle.report import render_markdown, render_yaml
from build_tools.cpython_oracle.skeleton import render_skeleton


def _classify(src: str):
    return classify_source(textwrap.dedent(src), module="test_synthetic")


def _by_name(report, name):
    for m in report.methods:
        if m.method == name or m.qualified_name == name:
            return m
    raise AssertionError(f"method {name!r} not found in {[m.qualified_name for m in report.methods]}")


# --------------------------------------------------------------------------- #
# Method collection
# --------------------------------------------------------------------------- #
def test_collects_class_and_module_test_methods():
    report = _classify(
        """
        import unittest

        class T(unittest.TestCase):
            def test_a(self):
                assert 1 == 1
            def helper(self):
                pass

        def test_module_level():
            assert True
        """
    )
    names = {m.qualified_name for m in report.methods}
    assert names == {"T.test_a", "test_module_level"}


def test_variant_mixin_methods_counted_once():
    # Python/C accelerator variants inherit the mixin's methods; the mixin defines
    # them once, so they must not be double-counted.
    report = _classify(
        """
        import unittest
        class Mixin:
            def test_shared(self):
                assert 1 == 1
        class PyVariant(Mixin, unittest.TestCase):
            pass
        class CVariant(Mixin, unittest.TestCase):
            pass
        """
    )
    assert [m.qualified_name for m in report.methods] == ["Mixin.test_shared"]


# --------------------------------------------------------------------------- #
# PORTABLE
# --------------------------------------------------------------------------- #
def test_plain_value_assertion_is_portable():
    report = _classify(
        """
        class T:
            def test_ok(self):
                assert bisect_left([1, 2, 3], 2) == 1
        """
    )
    assert _by_name(report, "test_ok").category is Category.PORTABLE


def test_homogeneous_list_is_portable():
    report = _classify(
        """
        class T:
            def test_h(self):
                xs = [1, 2, 3, 4]
                assert sum(xs) == 10
        """
    )
    assert _by_name(report, "test_h").category is Category.PORTABLE


def test_generated_loop_is_portable_with_unroll_note():
    report = _classify(
        """
        class T:
            def test_table(self):
                for a, b in [(1, 2), (3, 4)]:
                    self.assertEqual(a + 1, b - 0)
        """
    )
    m = _by_name(report, "test_table")
    assert m.category is Category.PORTABLE
    assert any(n.code == "generated-loop-unroll" for n in m.notes)


# --------------------------------------------------------------------------- #
# NEEDS_REWRITE
# --------------------------------------------------------------------------- #
def test_heterogeneous_list_needs_rewrite():
    report = _classify(
        """
        class T:
            def test_het(self):
                xs = [1, "two", 3.0]
                assert len(xs) == 3
        """
    )
    m = _by_name(report, "test_het")
    assert m.category is Category.NEEDS_REWRITE
    assert any(r.code == "heterogeneous-literal" for r in m.reasons)


def test_heterogeneous_tuple_is_allowed():
    # Sharpy tuples are typed/heterogeneous; a tuple is not a rewrite trigger.
    report = _classify(
        """
        class T:
            def test_tup(self):
                t = (1, "two", 3.0)
                assert len(t) == 3
        """
    )
    assert _by_name(report, "test_tup").category is Category.PORTABLE


def test_list_with_nonliteral_elements_is_not_flagged():
    report = _classify(
        """
        class T:
            def test_calls(self):
                xs = [f(), g(), 3]
                assert len(xs) == 3
        """
    )
    assert _by_name(report, "test_calls").category is Category.PORTABLE


def test_dunder_fixture_reference_needs_rewrite():
    report = _classify(
        """
        class CmpErr:
            def __lt__(self, other):
                raise ZeroDivisionError
        class T:
            def test_cmp(self):
                assert do_something(CmpErr()) is None
        """
    )
    m = _by_name(report, "test_cmp")
    assert m.category is Category.NEEDS_REWRITE
    assert any(r.code == "dunder-fixture" for r in m.reasons)


def test_builtin_subclass_fixture_needs_rewrite():
    report = _classify(
        """
        class MyInt(int):
            pass
        class T:
            def test_sub(self):
                assert MyInt(5) == 5
        """
    )
    m = _by_name(report, "test_sub")
    assert m.category is Category.NEEDS_REWRITE
    assert any(r.code == "builtin-subclass" for r in m.reasons)


def test_test_support_helper_needs_rewrite():
    report = _classify(
        """
        from test.support import import_helper
        class T:
            def test_s(self):
                mod = import_helper.import_module("x")
                assert mod is not None
        """
    )
    m = _by_name(report, "test_s")
    assert m.category is Category.NEEDS_REWRITE
    assert any(r.code == "test-support" for r in m.reasons)


def test_whitelisted_support_name_is_not_flagged():
    report = _classify(
        """
        from test import support
        class T:
            def test_v(self):
                if support.verbose:
                    pass
                assert True
        """
    )
    assert _by_name(report, "test_v").category is Category.PORTABLE


# --------------------------------------------------------------------------- #
# DYNAMIC
# --------------------------------------------------------------------------- #
def test_exec_is_dynamic():
    report = _classify(
        """
        class T:
            def test_e(self):
                exec("x = 1")
                assert True
        """
    )
    m = _by_name(report, "test_e")
    assert m.category is Category.DYNAMIC
    assert any(r.code == "exec-eval" for r in m.reasons)


def test_three_arg_type_factory_is_dynamic():
    report = _classify(
        """
        class T:
            def test_t(self):
                cls = type("X", (object,), {})
                assert cls.__name__ == "X"
        """
    )
    assert _by_name(report, "test_t").category is Category.DYNAMIC


def test_metaclass_local_is_dynamic():
    report = _classify(
        """
        class T:
            def test_m(self):
                class M(metaclass=Meta):
                    pass
                assert M is not None
        """
    )
    assert _by_name(report, "test_m").category is Category.DYNAMIC


# --------------------------------------------------------------------------- #
# IMPL_DETAIL
# --------------------------------------------------------------------------- #
def test_cpython_only_decorator_is_impl_detail():
    report = _classify(
        """
        from test import support
        class T:
            @support.cpython_only
            def test_c(self):
                assert True
        """
    )
    m = _by_name(report, "test_c")
    assert m.category is Category.IMPL_DETAIL
    assert any(r.code == "impl-detail-decorator" for r in m.reasons)


def test_getsizeof_is_impl_detail():
    report = _classify(
        """
        import sys
        class T:
            def test_z(self):
                assert sys.getsizeof(0) > 0
        """
    )
    assert _by_name(report, "test_z").category is Category.IMPL_DETAIL


def test_gc_usage_is_impl_detail():
    report = _classify(
        """
        import gc
        class T:
            def test_g(self):
                gc.collect()
                assert gc.is_tracked([]) is True
        """
    )
    assert _by_name(report, "test_g").category is Category.IMPL_DETAIL


# --------------------------------------------------------------------------- #
# DIVERGENT
# --------------------------------------------------------------------------- #
def test_huge_int_literal_is_divergent():
    report = _classify(
        """
        class T:
            def test_big(self):
                assert 12345678901234567890123 > 0
        """
    )
    m = _by_name(report, "test_big")
    assert m.category is Category.DIVERGENT
    assert any(r.deviation_id == "int-overflow-checked" for r in m.reasons)


def test_power_literal_not_materialized_but_flagged():
    report = _classify(
        """
        class T:
            def test_pow(self):
                assert 10 ** 30000 > 0
        """
    )
    m = _by_name(report, "test_pow")
    assert m.category is Category.DIVERGENT
    assert any(r.code == "huge-int" for r in m.reasons)


def test_small_power_not_flagged():
    report = _classify(
        """
        class T:
            def test_small(self):
                assert 2 ** 10 == 1024
        """
    )
    assert _by_name(report, "test_small").category is Category.PORTABLE


def test_floor_div_is_portable():
    # `//` (and `%`) floor in Sharpy, matching CPython — no longer a divergence (#1153).
    report = _classify(
        """
        class T:
            def test_fd(self):
                assert (7 // 2) == 3
        """
    )
    m = _by_name(report, "test_fd")
    assert m.category is Category.PORTABLE
    assert not any(r.deviation_id == "integer-division-floor" for r in m.reasons)


def test_global_keyword_is_divergent():
    report = _classify(
        """
        class T:
            def test_gl(self):
                global counter
                counter = 1
                assert counter == 1
        """
    )
    m = _by_name(report, "test_gl")
    assert m.category is Category.DIVERGENT
    assert any(r.deviation_id == "global-nonlocal-keywords" for r in m.reasons)


# --------------------------------------------------------------------------- #
# Precedence
# --------------------------------------------------------------------------- #
def test_most_severe_category_wins():
    report = _classify(
        """
        import sys
        class T:
            def test_mix(self):
                xs = [1, "two"]           # NEEDS_REWRITE
                assert 10 ** 999 > 0      # DIVERGENT
                assert sys.getsizeof(xs)  # IMPL_DETAIL
        """
    )
    # IMPL_DETAIL outranks DIVERGENT outranks NEEDS_REWRITE.
    assert _by_name(report, "test_mix").category is Category.IMPL_DETAIL


# --------------------------------------------------------------------------- #
# Module-level dynamic generation
# --------------------------------------------------------------------------- #
def test_dynamic_test_generation_module_note():
    report = _classify(
        """
        class T:
            pass
        for i in range(3):
            setattr(T, "test_%d" % i, lambda self: None)
        """
    )
    assert any("setattr" in n for n in report.module_notes)


# --------------------------------------------------------------------------- #
# Reports & skeletons
# --------------------------------------------------------------------------- #
def test_markdown_report_has_headline_and_table():
    report = _classify(
        """
        class T:
            def test_a(self):
                assert True
        """
    )
    md = render_markdown(report)
    assert "yield report" in md
    assert "| Category | Count | % |" in md
    assert "Portable: 1" in md


def test_yaml_report_is_parseable():
    import yaml

    report = _classify(
        """
        class T:
            def test_a(self):
                assert 10 ** 999 > 0
        """
    )
    data = yaml.safe_load(render_yaml(report))
    assert data["module"] == "test_synthetic"
    assert data["total_methods"] == 1
    assert data["methods"][0]["category"] == "DIVERGENT"


def test_skeleton_emits_only_portable_methods_with_provenance():
    report = _classify(
        """
        class T:
            def test_ok(self):
                assert True
            def test_big(self):
                assert 10 ** 999 > 0
        """
    )
    spy = render_skeleton(report)
    assert "@test" in spy
    assert "def t_test_ok():" in spy or "def test_ok():" in spy
    assert "PSF License" in spy
    assert "ported from CPython 3.12" in spy
    # The DIVERGENT method must not get a skeleton.
    assert "test_big" not in spy


def test_skeleton_deduplicates_colliding_names():
    report = _classify(
        """
        class Foo:
            def test_x(self):
                assert True
        class Bar:
            def test_x(self):
                assert True
        """
    )
    spy = render_skeleton(report)
    # Two distinct function definitions, no duplicate name.
    defs = [ln for ln in spy.splitlines() if ln.startswith("def ")]
    assert len(defs) == 2
    assert len(set(defs)) == 2


def test_classifier_deviation_ids_exist_in_ledger():
    # Every deviation_id the classifier can emit must resolve to a real entry in
    # docs/deviations.yaml, so DIVERGENT reasons are honest ledger references.
    import yaml
    from pathlib import Path

    import build_tools.cpython_oracle.classifier as clf

    repo_root = Path(clf.__file__).resolve().parents[2]
    ledger = repo_root / "docs" / "deviations.yaml"
    if not ledger.exists():
        import pytest

        pytest.skip("docs/deviations.yaml not found")
    catalog = yaml.safe_load(ledger.read_text(encoding="utf-8"))
    ids = {e["id"] for e in catalog["deviations"]}
    used = {
        v
        for k, v in vars(clf).items()
        if k.startswith("DEVIATION_") and isinstance(v, str)
    }
    assert used, "expected DEVIATION_* constants"
    missing = used - ids
    assert not missing, f"classifier references unknown deviation ids: {missing}"


def test_determinism_same_source_same_report():
    src = """
        class T:
            def test_a(self):
                assert 1 == 1
            def test_b(self):
                assert 10 ** 999 > 0
    """
    assert render_yaml(_classify(src)) == render_yaml(_classify(src))
