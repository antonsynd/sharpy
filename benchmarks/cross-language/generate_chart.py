#!/usr/bin/env python3
"""Generate an SVG performance chart from benchmark history.json.

Produces a two-panel SVG: Spy/Py ratios (top) and Spy/C# ratios (bottom).
No external dependencies — pure stdlib SVG generation.

Usage:
    python3 generate_chart.py [--months N] [--output FILE]
"""

import argparse
import json
import sys
from datetime import datetime, timedelta
from pathlib import Path

RESULTS_DIR = Path(__file__).resolve().parent / "results"
COLORS = ["#e6194b", "#3cb44b", "#4363d8", "#f58231", "#911eb4", "#469990"]


def load_history(months: int) -> list[dict]:
    history_file = RESULTS_DIR / "history.json"
    if not history_file.exists():
        print("No history.json found.", file=sys.stderr)
        sys.exit(1)
    with open(history_file) as f:
        history = json.load(f)
    if months > 0:
        cutoff = (datetime.now() - timedelta(days=months * 30)).strftime("%Y-%m-%d")
        history = [h for h in history if h["date"] >= cutoff]
    return history


def compute_ratios(
    history: list[dict], numerator: str, denominator: str,
) -> tuple[list[str], dict[str, list[float | None]]]:
    """Returns (dates, {benchmark_name: [ratio_or_None_per_date]})."""
    dates = [h["date"] for h in history]
    bench_names: set[str] = set()
    for h in history:
        for r in h["results"]:
            if r["language"] in (numerator, denominator):
                bench_names.add(r["name"])

    ratios: dict[str, list[float | None]] = {name: [] for name in sorted(bench_names)}
    for h in history:
        by_name: dict[str, dict[str, dict]] = {}
        for r in h["results"]:
            by_name.setdefault(r["name"], {})[r["language"]] = r
        for name in sorted(bench_names):
            langs = by_name.get(name, {})
            num = langs.get(numerator)
            den = langs.get(denominator)
            if num and num.get("success") and den and den.get("success"):
                num_t = num.get("execute_seconds") or num.get("elapsed_seconds", 0)
                den_t = den.get("execute_seconds") or den.get("elapsed_seconds", 0)
                ratios[name].append(num_t / den_t if den_t > 0 else None)
            else:
                ratios[name].append(None)
    return dates, ratios


def _render_panel(
    dates: list[str],
    ratios: dict[str, list[float | None]],
    title: str,
    subtitle: str,
    parity_label: str,
    y_offset: float,
    width: float,
    margin_left: float,
    margin_right: float,
    chart_height: float,
) -> tuple[list[str], float]:
    """Render one chart panel. Returns (svg_lines, total_panel_height)."""
    margin_top = 45
    margin_bottom = 60

    plot_w = width - margin_left - margin_right
    plot_h = chart_height

    all_vals = [v for vs in ratios.values() for v in vs if v is not None]
    if not all_vals:
        return [f'<text x="{width / 2}" y="{y_offset + 50}" text-anchor="middle">No data</text>'], 100

    y_min = 0.0
    y_max = max(all_vals) * 1.15
    if y_max < 1.5:
        y_max = 1.5

    def x_pos(i: int) -> float:
        if len(dates) == 1:
            return margin_left + plot_w / 2
        return margin_left + i * plot_w / (len(dates) - 1)

    def y_pos(v: float) -> float:
        return y_offset + margin_top + plot_h - (v - y_min) / (y_max - y_min) * plot_h

    lines: list[str] = []

    # Title
    lines.append(f'<text x="{width / 2}" y="{y_offset + 20}" text-anchor="middle" font-size="14" font-weight="600" fill="#1a1a1a">{title}</text>')
    lines.append(f'<text x="{width / 2}" y="{y_offset + 36}" text-anchor="middle" font-size="10" fill="#666">{subtitle}</text>')

    # Grid lines and Y axis labels
    y_ticks = [i * 0.25 for i in range(int(y_max / 0.25) + 2) if i * 0.25 <= y_max]
    for tick in y_ticks:
        yp = y_pos(tick)
        color = "#e0e0e0" if tick != 1.0 else "#ff6b6b"
        dash = "" if tick != 1.0 else ' stroke-dasharray="6,3"'
        stroke_w = "0.5" if tick != 1.0 else "1.5"
        lines.append(f'<line x1="{margin_left}" y1="{yp:.1f}" x2="{width - margin_right}" y2="{yp:.1f}" stroke="{color}" stroke-width="{stroke_w}"{dash}/>')
        lines.append(f'<text x="{margin_left - 8}" y="{yp + 4:.1f}" text-anchor="end" font-size="10" fill="#666">{tick:.2f}x</text>')

    # Parity label
    lines.append(f'<text x="{width - margin_right + 4}" y="{y_pos(1.0) + 4:.1f}" font-size="9" fill="#ff6b6b">{parity_label}</text>')

    # X axis labels
    for i, date in enumerate(dates):
        xp = x_pos(i)
        short = date[5:]
        lines.append(f'<text x="{xp:.1f}" y="{y_offset + margin_top + plot_h + 16}" text-anchor="middle" font-size="9" fill="#666" transform="rotate(-45 {xp:.1f} {y_offset + margin_top + plot_h + 16})">{short}</text>')

    # Data lines
    bench_names = sorted(ratios.keys())
    for bi, name in enumerate(bench_names):
        color = COLORS[bi % len(COLORS)]
        vals = ratios[name]
        points = [(x_pos(i), y_pos(v)) for i, v in enumerate(vals) if v is not None]
        if len(points) >= 2:
            path = " ".join(f"{'M' if j == 0 else 'L'}{x:.1f},{y:.1f}" for j, (x, y) in enumerate(points))
            lines.append(f'<path d="{path}" fill="none" stroke="{color}" stroke-width="2" stroke-linejoin="round"/>')
        for x, y in points:
            lines.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="3" fill="{color}"/>')

    panel_height = margin_top + plot_h + margin_bottom
    return lines, panel_height


def generate_svg(
    dates: list[str],
    spy_py_ratios: dict[str, list[float | None]],
    spy_cs_ratios: dict[str, list[float | None]],
) -> str:
    if not dates:
        return '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="100"><text x="20" y="50">No data</text></svg>'

    width = 800
    margin_left = 60
    margin_right = 20
    chart_height = 280
    panel_gap = 20
    legend_height = 25

    # Collect all benchmark names for the shared legend
    all_names = sorted(set(list(spy_py_ratios.keys()) + list(spy_cs_ratios.keys())))
    legend_rows = (len(all_names) + 3) // 4

    # Render both panels
    panel1_lines, panel1_h = _render_panel(
        dates, spy_py_ratios,
        "Sharpy / Python Execution Time Ratio",
        "Below 1.0 = Sharpy faster than CPython",
        "parity",
        y_offset=0,
        width=width, margin_left=margin_left, margin_right=margin_right,
        chart_height=chart_height,
    )

    panel2_lines, panel2_h = _render_panel(
        dates, spy_cs_ratios,
        "Sharpy / C# Execution Time Ratio",
        "Near 1.0 = minimal overhead vs hand-written C#",
        "parity",
        y_offset=panel1_h + panel_gap,
        width=width, margin_left=margin_left, margin_right=margin_right,
        chart_height=chart_height,
    )

    total_height = panel1_h + panel_gap + panel2_h + legend_rows * legend_height + 10
    plot_w = width - margin_left - margin_right

    svg_lines: list[str] = []
    svg_lines.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {total_height:.0f}" width="{width}" height="{total_height:.0f}" font-family="system-ui, -apple-system, sans-serif">')
    svg_lines.append(f'<rect width="{width}" height="{total_height:.0f}" fill="white"/>')
    svg_lines.extend(panel1_lines)
    svg_lines.extend(panel2_lines)

    # Shared legend
    legend_y = panel1_h + panel_gap + panel2_h + 5
    items_per_row = 4
    for bi, name in enumerate(all_names):
        color = COLORS[bi % len(COLORS)]
        row = bi // items_per_row
        col = bi % items_per_row
        lx = margin_left + col * (plot_w / items_per_row)
        ly = legend_y + row * legend_height
        svg_lines.append(f'<line x1="{lx}" y1="{ly:.0f}" x2="{lx + 20}" y2="{ly:.0f}" stroke="{color}" stroke-width="2"/>')
        svg_lines.append(f'<circle cx="{lx + 10}" cy="{ly:.0f}" r="3" fill="{color}"/>')
        svg_lines.append(f'<text x="{lx + 26}" y="{ly + 4:.0f}" font-size="11" fill="#333">{name}</text>')

    svg_lines.append('</svg>')
    return "\n".join(svg_lines)


def main():
    parser = argparse.ArgumentParser(description="Generate benchmark trend SVG chart")
    parser.add_argument("--months", type=int, default=6, help="Months of history to include (0 = all)")
    parser.add_argument("--output", type=str, default=None, help="Output file (default: results/trend.svg)")
    args = parser.parse_args()

    history = load_history(args.months)
    if not history:
        print("No history data in the requested range.", file=sys.stderr)
        sys.exit(1)

    dates_py, spy_py_ratios = compute_ratios(history, "Sharpy", "Python")
    dates_cs, spy_cs_ratios = compute_ratios(history, "Sharpy", "C#")

    # Both use the same date axis
    dates = dates_py or dates_cs
    svg = generate_svg(dates, spy_py_ratios, spy_cs_ratios)

    output = Path(args.output) if args.output else RESULTS_DIR / "trend.svg"
    output.write_text(svg)
    print(f"Chart written to {output} ({len(dates)} data points, {len(spy_py_ratios)} benchmarks)")


if __name__ == "__main__":
    main()
