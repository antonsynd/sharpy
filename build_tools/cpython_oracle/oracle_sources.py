"""Locate the pinned CPython test suite without vendoring it.

The oracle is pinned to **CPython 3.12.x**. Test files are read from a local
CPython 3.12 install's ``Lib/test`` directory; they are deliberately NOT vendored
into this repo (PSF-2.0 material, large, and re-syncs must be deliberate).

Resolution order:
  1. ``--cpython-lib`` path passed on the CLI (a ``.../lib/pythonX.Y/test`` dir).
  2. ``SHARPY_CPYTHON_TEST_DIR`` environment variable.
  3. The running interpreter's stdlib ``test`` dir, if it is 3.12 and populated.
  4. Common Homebrew / python.org framework install locations for 3.12.

If none resolve, callers get a clear message pointing at the README.
"""

from __future__ import annotations

import glob
import os
import sys
import sysconfig
from pathlib import Path
from typing import Optional

PINNED_MAJOR_MINOR = "3.12"


def _looks_like_test_dir(path: Path) -> bool:
    return path.is_dir() and (path / "test_bisect.py").exists()


def _candidate_dirs() -> list[Path]:
    candidates: list[Path] = []

    env = os.environ.get("SHARPY_CPYTHON_TEST_DIR")
    if env:
        candidates.append(Path(env))

    if sys.version_info[:2] == (3, 12):
        stdlib = sysconfig.get_path("stdlib")
        if stdlib:
            candidates.append(Path(stdlib) / "test")

    # Homebrew Cellar (macOS) and Linux-style prefixes.
    patterns = [
        "/opt/homebrew/Cellar/python@3.12/*/Frameworks/Python.framework/Versions/3.12/lib/python3.12/test",
        "/usr/local/Cellar/python@3.12/*/Frameworks/Python.framework/Versions/3.12/lib/python3.12/test",
        "/Library/Frameworks/Python.framework/Versions/3.12/lib/python3.12/test",
        "/usr/lib/python3.12/test",
        "/usr/local/lib/python3.12/test",
    ]
    for pat in patterns:
        for hit in sorted(glob.glob(pat)):
            candidates.append(Path(hit))

    return candidates


def find_cpython_test_dir(explicit: Optional[str] = None) -> Optional[Path]:
    if explicit:
        p = Path(explicit)
        return p if _looks_like_test_dir(p) else None
    for cand in _candidate_dirs():
        if _looks_like_test_dir(cand):
            return cand
    return None


def resolve_test_file(name: str, explicit_dir: Optional[str] = None) -> Optional[Path]:
    """Resolve a module name (``test_bisect`` or ``bisect``) to a file path."""
    if name.endswith(".py") and Path(name).exists():
        return Path(name)
    stem = name if name.startswith("test_") else f"test_{name}"
    test_dir = find_cpython_test_dir(explicit_dir)
    if test_dir is None:
        return None
    candidate = test_dir / f"{stem}.py"
    return candidate if candidate.exists() else None


def guidance() -> str:
    return (
        "Could not locate a CPython 3.12 Lib/test directory. Install CPython 3.12\n"
        "(e.g. `brew install python@3.12`) and pass its test dir explicitly:\n"
        "  --cpython-lib /path/to/lib/python3.12/test\n"
        "or set SHARPY_CPYTHON_TEST_DIR. Test files are not vendored (PSF-2.0)."
    )
