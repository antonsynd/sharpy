"""Guard for #2497 (R-LZ): one pinned CPython minor in one file, read by every workflow.

The oracle minor lives only in the repo-root ``.python-version``. Every ``actions/setup-python``
step reads it through ``python-version-file``; a ``python-version`` key anywhere in a workflow
(a step input or a matrix axis) is a second copy of the fact and fails here.
"""

from __future__ import annotations

import re
from collections.abc import Iterator
from pathlib import Path
from typing import Any

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
PIN_FILE = REPO_ROOT / ".python-version"
WORKFLOWS = sorted((REPO_ROOT / ".github" / "workflows").glob("*.y*ml"))


def _keys(node: Any, path: str) -> Iterator[tuple[str, Any]]:
    if isinstance(node, dict):
        for k, v in node.items():
            here = f"{path}.{k}"
            yield here, k
            yield from _keys(v, here)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from _keys(v, f"{path}[{i}]")


def violations(name: str, workflow: Any) -> tuple[list[str], int]:
    """Return (violations, number of setup-python steps seen) for one parsed workflow."""
    found: list[str] = []
    for where, key in _keys(workflow, name):
        if key == "python-version":
            found.append(
                f"{where}: a python-version literal; read .python-version instead"
            )
    steps_seen = 0
    for job_id, job in ((workflow or {}).get("jobs") or {}).items():
        checked_out = False
        for i, step in enumerate((job or {}).get("steps") or []):
            uses = str(step.get("uses", ""))
            if uses.startswith("actions/checkout@"):
                checked_out = True
            if not uses.startswith("actions/setup-python@"):
                continue
            steps_seen += 1
            where = f"{name}: jobs.{job_id}.steps[{i}]"
            if (step.get("with") or {}).get("python-version-file") != ".python-version":
                found.append(
                    f"{where}: setup-python must read python-version-file: .python-version"
                )
            if not checked_out:
                found.append(
                    f"{where}: setup-python before actions/checkout cannot see .python-version"
                )
    return found, steps_seen


def test_pin_file_holds_exactly_one_minor() -> None:
    lines = [
        ln.strip()
        for ln in PIN_FILE.read_text(encoding="utf-8").splitlines()
        if ln.strip() and not ln.lstrip().startswith("#")
    ]
    assert len(lines) == 1, f".python-version must hold one version line, got {lines}"
    assert re.fullmatch(r"3\.\d+", lines[0]), f"pin a minor (3.N), not {lines[0]!r}"


def test_every_workflow_reads_the_pin_file() -> None:
    all_found: list[str] = []
    total_steps = 0
    for wf in WORKFLOWS:
        found, steps = violations(
            wf.name, yaml.safe_load(wf.read_text(encoding="utf-8"))
        )
        all_found += found
        total_steps += steps
    # Positive control: the scan reaches setup-python steps at all.
    assert total_steps > 0, (
        "no actions/setup-python step found; the guard scans nothing"
    )
    assert not all_found, "\n".join(all_found)


def test_guard_flags_each_way_to_restate_the_version() -> None:
    literal = {
        "jobs": {
            "j": {
                "steps": [
                    {"uses": "actions/checkout@v7"},
                    {
                        "uses": "actions/setup-python@v7",
                        "with": {"python-version": "3.12"},
                    },
                ]
            }
        }
    }
    matrix = {
        "jobs": {
            "j": {
                "strategy": {"matrix": {"python-version": ["3.12"]}},
                "steps": [
                    {"uses": "actions/checkout@v7"},
                    {
                        "uses": "actions/setup-python@v7",
                        "with": {"python-version-file": ".python-version"},
                    },
                ],
            }
        }
    }
    no_checkout = {
        "jobs": {
            "j": {
                "steps": [
                    {
                        "uses": "actions/setup-python@v7",
                        "with": {"python-version-file": ".python-version"},
                    },
                ]
            }
        }
    }
    good = {
        "jobs": {
            "j": {
                "steps": [
                    {"uses": "actions/checkout@v7"},
                    {
                        "uses": "actions/setup-python@v7",
                        "with": {"python-version-file": ".python-version"},
                    },
                ]
            }
        }
    }
    assert len(violations("literal", literal)[0]) == 2
    assert len(violations("matrix", matrix)[0]) == 1
    assert len(violations("no_checkout", no_checkout)[0]) == 1
    assert violations("good", good) == ([], 1)
