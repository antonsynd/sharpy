"""Unit tests for the cross-language benchmark trend chart.

Run with: pytest benchmarks/cross-language/test_generate_chart.py
"""
import importlib.util
import re
from pathlib import Path

_MODULE_PATH = Path(__file__).parent / "generate_chart.py"
_spec = importlib.util.spec_from_file_location("generate_chart", _MODULE_PATH)
assert _spec is not None and _spec.loader is not None
generate_chart = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(generate_chart)


def _entry(name, language, seconds):
    return {"name": name, "language": language, "execute_seconds": seconds, "success": True}


def test_panels_cover_python_csharp_and_boo():
    assert [denominator for denominator, _, _ in generate_chart.PANELS] == ["Python", "C#", "Boo"]


def test_compute_ratios_leaves_gaps_before_a_language_joined():
    history = [
        {"date": "2026-10-26", "results": [_entry("fib", "Sharpy", 0.04)]},
        {"date": "2026-10-31", "results": [_entry("fib", "Sharpy", 0.04), _entry("fib", "Boo", 0.02)]},
    ]
    dates, ratios = generate_chart.compute_ratios(history, "Sharpy", "Boo")
    assert dates == ["2026-10-26", "2026-10-31"]
    assert ratios == {"fib": [None, 2.0]}


def test_benchmark_keeps_its_legend_color_in_a_panel_missing_another_benchmark():
    # "a_numpy" sorts before "b_sort" and is absent from the second panel (as the numpy
    # variant is from the Boo panel); b_sort must still use its legend color there.
    dates = ["2026-10-31"]
    full = {"a_numpy": [1.0], "b_sort": [0.5]}
    partial = {"b_sort": [0.6]}
    svg = generate_chart.generate_svg(dates, [("T1", "s1", full), ("T2", "s2", partial)])

    legend_color = re.search(r'stroke="(#[0-9a-f]{6})" stroke-width="2"/>\s*<circle[^>]*/>\s*<text[^>]*>b_sort<', svg).group(1)
    panel2 = svg.split(">T2<", 1)[1]
    assert re.findall(r'<circle cx="[^"]+" cy="[^"]+" r="3" fill="(#[0-9a-f]{6})"/>', panel2.split('stroke-width="2"/>', 1)[0]) == [legend_color]
