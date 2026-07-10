"""Yield-report renderers for classifier output (markdown + yaml)."""

from __future__ import annotations

from typing import Iterable

from .classifier import Category, ModuleReport

_CATEGORY_ORDER = [
    Category.PORTABLE,
    Category.NEEDS_REWRITE,
    Category.DIVERGENT,
    Category.DYNAMIC,
    Category.IMPL_DETAIL,
]


def render_markdown(report: ModuleReport) -> str:
    counts = report.counts()
    total = len(report.methods)
    lines: list[str] = []
    lines.append(f"# CPython oracle yield report — `{report.module}`")
    lines.append("")
    lines.append(f"- CPython source: {report.source_path or '(inline)'}")
    lines.append(f"- CPython version pin: {report.cpython_version}")
    lines.append(f"- Test methods: {total}")
    lines.append(f"- Portable: {counts[Category.PORTABLE]} ({report.portable_pct():.1f}%)")
    lines.append("")
    lines.append("## Category counts")
    lines.append("")
    lines.append("| Category | Count | % |")
    lines.append("|---|---:|---:|")
    for cat in _CATEGORY_ORDER:
        pct = 100.0 * counts[cat] / total if total else 0.0
        lines.append(f"| {cat.label} | {counts[cat]} | {pct:.1f}% |")
    lines.append("")
    lines.append("## Per-method classification")
    lines.append("")
    lines.append("| Method | Category | Reasons |")
    lines.append("|---|---|---|")
    for m in report.methods:
        if m.reasons:
            reason_txt = "; ".join(
                f"`{r.code}`" + (f" ({r.deviation_id})" if r.deviation_id else "")
                for r in _dedup(m.reasons)
            )
        elif m.notes:
            reason_txt = "; ".join(f"_{n.code}_" for n in _dedup(m.notes))
        else:
            reason_txt = "—"
        lines.append(f"| `{m.qualified_name}` | {m.category.label} | {reason_txt} |")
    if report.module_notes:
        lines.append("")
        lines.append("## Module notes")
        lines.append("")
        for note in report.module_notes:
            lines.append(f"- {note}")
    lines.append("")
    return "\n".join(lines)


def render_yaml(report: ModuleReport) -> str:
    counts = report.counts()
    lines: list[str] = []
    lines.append(f"module: {report.module}")
    lines.append(f"source_path: {report.source_path or 'null'}")
    lines.append(f"cpython_version: '{report.cpython_version}'")
    lines.append(f"total_methods: {len(report.methods)}")
    lines.append("counts:")
    for cat in _CATEGORY_ORDER:
        lines.append(f"  {cat.label}: {counts[cat]}")
    lines.append("methods:")
    for m in report.methods:
        lines.append(f"  - name: {m.qualified_name}")
        lines.append(f"    line: {m.line}")
        lines.append(f"    category: {m.category.label}")
        if m.reasons:
            lines.append("    reasons:")
            for r in _dedup(m.reasons):
                lines.append(f"      - code: {r.code}")
                lines.append(f"        category: {r.category.label}")
                lines.append(f"        line: {r.line}")
                if r.deviation_id:
                    lines.append(f"        deviation_id: {r.deviation_id}")
                lines.append(f"        detail: {_yaml_str(r.detail)}")
    if report.module_notes:
        lines.append("module_notes:")
        for note in report.module_notes:
            lines.append(f"  - {_yaml_str(note)}")
    return "\n".join(lines) + "\n"


def summary_line(report: ModuleReport) -> str:
    counts = report.counts()
    parts = [f"{cat.label}={counts[cat]}" for cat in _CATEGORY_ORDER]
    return (
        f"{report.module}: {len(report.methods)} methods, "
        f"{report.portable_pct():.0f}% portable [" + " ".join(parts) + "]"
    )


def _dedup(reasons: Iterable) -> list:
    seen = set()
    out = []
    for r in reasons:
        key = (r.code, r.deviation_id)
        if key not in seen:
            seen.add(key)
            out.append(r)
    return out


def _yaml_str(s: str) -> str:
    if any(ch in s for ch in ":#'\"\n") or s != s.strip():
        return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'
    return s
