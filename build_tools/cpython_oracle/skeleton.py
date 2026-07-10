"""Emit ``.spy`` skeletons for the PORTABLE methods of a classified module.

A skeleton carries the test signature and a provenance comment only — the body
port is manual/agent work later (issue #1030 phase 1). Every emitted file starts
with the PSF-2.0 attribution required for CPython-derived material.
"""

from __future__ import annotations

from .classifier import Category, ModuleReport

_PSF_HEADER = (
    "# Skeletons derived from the CPython {version} standard-library test suite\n"
    "# (Lib/test/{module}.py), which is licensed under the PSF License Agreement.\n"
    "# See https://docs.python.org/3/license.html. Method bodies are NOT ported\n"
    "# here — these are signature-only skeletons for #1030 phase 1.\n"
)


def _spy_method_name(class_name: str | None, method: str) -> str:
    """Make a Spy-tree-unique test function name. File stems must be unique across
    the Spy/ tree, and method names must be unique within a file; prefixing with a
    lowered class name keeps the Python/C mixin variants from colliding."""
    if class_name:
        base = _snake(class_name)
        return f"{base}_{method}" if not method.startswith(base) else method
    return method


def _snake(name: str) -> str:
    out = []
    for i, ch in enumerate(name):
        if ch.isupper() and i > 0 and not name[i - 1].isupper():
            out.append("_")
        out.append(ch.lower())
    return "".join(out)


def render_skeleton(report: ModuleReport) -> str:
    lines: list[str] = []
    lines.append(_PSF_HEADER.format(version=report.cpython_version, module=report.module))
    lines.append("")
    portable = [m for m in report.methods if m.category is Category.PORTABLE]
    seen: set[str] = set()
    for m in portable:
        rel = report.source_path or f"Lib/test/{report.module}.py"
        prov = f"CPython {report.cpython_version} {rel}::{m.qualified_name}"
        name = _spy_method_name(m.class_name, m.method)
        # Guard against intra-file collisions (unique-stem/name constraint).
        original = name
        n = 2
        while name in seen:
            name = f"{original}_{n}"
            n += 1
        seen.add(name)
        note = ""
        if any(x.code.startswith("generated-loop") for x in m.notes):
            note = "  # table-driven in CPython — unroll the loop when porting"
        lines.append(f"# ported from {prov}{note}")
        lines.append("@test")
        lines.append(f"def {name}():")
        lines.append("    pass  # TODO(#1030): port body from CPython")
        lines.append("")
    if not portable:
        lines.append("# (no PORTABLE methods classified for this module)")
        lines.append("")
    return "\n".join(lines)
