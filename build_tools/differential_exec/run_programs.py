#!/usr/bin/env python3
"""CPython differential-EXECUTION runner for the Sharpy differential oracle.

Companion to ``build_tools/differential_parse/compare_ast.py``. Where that script
asks CPython whether it *parses* the shared Sharpy/Python subset, this one asks
what CPython *prints* when it *executes* it, so the C# harness
(``DifferentialExecutionTests``) can compare byte-for-byte against the output of
the same program compiled and run by Sharpy.

Batch protocol (the C# harness spawns ONE ``python3`` for the whole batch, not one
per program — the batch process then runs each program in its own short-lived,
isolated child so a hang or crash in one cannot poison the others):

    input  (``--batch <file>`` JSONL, one object per line):
        {"id": <int>, "source": "<python source text>"}
    stdout (JSONL, one verdict per line, keyed by the same id):
        {"id": <int>, "ok": <bool>, "stdout": "<text>", "stderr": "<text>",
         "exit": <int>, "timed_out": <bool>, "syntax_error": <bool>}

``ok`` is true iff the program exited 0 without timing out. ``syntax_error`` is
true when the program failed to *parse* as Python at all (e.g. it carries a
Sharpy-only type annotation like ``T?`` or ``!E`` that CPython cannot parse) —
the harness treats that as "not in the shared executable subset" (a skip) rather
than a runtime divergence, since parse-level differences are already covered by
the differential-parse oracle. Each program runs
under a fixed, minimal environment (``PYTHONHASHSEED=0`` for run-to-run set/dict
hash determinism, no user site) with a per-program wall-clock timeout and an
output cap so a runaway loop or a firehose ``print`` cannot wedge the batch.

With no input the module is import-only (used by the pytest coverage in
``build_tools/tests``). Deterministic and dependency-free; pinned to CPython 3.12
by the caller.
"""
from __future__ import annotations

import importlib
import json
import os
import subprocess
import sys
from typing import Any, Dict, Iterable, List

# Per-program wall-clock budget. Overridable so a stress sweep can widen it; the
# C# harness keeps generated programs well under this by bounding loop fuel.
DEFAULT_TIMEOUT_SECONDS = float(os.environ.get("DIFF_EXEC_PY_TIMEOUT", "5"))

# Cap captured stdout/stderr so a runaway print loop cannot exhaust memory. A
# program that overflows this is a divergence anyway (Sharpy would not match a
# truncated stream), so truncation only bounds cost.
OUTPUT_CAP_BYTES = 256 * 1024


#: Third-party (non-stdlib) modules the oracle is allowed to compare against, and therefore
#: the only ones whose install location is put back on the child's path. Must stay in step
#: with ``CPythonAvailableModules`` in DifferentialExecutionTests: a module admitted there but
#: missing here is compared against a CPython that cannot import it, which scores every cell
#: as a name-missing SKIP and makes the comparison silently vacuous — that is exactly how the
#: yaml widening looked non-vacuous while never comparing anything (#1338 follow-up).
ORACLE_THIRD_PARTY = ("yaml",)


def _third_party_paths() -> List[str]:
    """Import locations for :data:`ORACLE_THIRD_PARTY`, resolved in THIS interpreter.

    Resolved here, in the parent, because the parent runs with ``site`` enabled while the
    children deliberately do not. A module that is genuinely not installed contributes
    nothing and its cells fall back to the existing name-missing skip, which is the honest
    outcome on a machine without it.
    """
    paths: List[str] = []
    for name in ORACLE_THIRD_PARTY:
        try:
            module = importlib.import_module(name)
        except ImportError:
            continue
        origin = getattr(module, "__file__", None)
        if not origin:
            continue
        # <site-packages>/<pkg>/__init__.py -> <site-packages>
        site_dir = os.path.dirname(os.path.dirname(os.path.abspath(origin)))
        if site_dir and site_dir not in paths:
            paths.append(site_dir)
    return paths


def _child_env() -> Dict[str, str]:
    """Minimal, deterministic environment for the executed program.

    ``PYTHONHASHSEED=0`` pins hash randomization so set/dict iteration order is
    stable run-to-run (whether Sharpy *agrees* with that order is a separate
    question the harness's subset filter and allowlist handle). PATH is preserved
    so ``sys.executable`` resolves; nothing else is inherited.

    ``PYTHONPATH`` carries the install locations of :data:`ORACLE_THIRD_PARTY` and nothing
    else. The children keep ``-S``: hermeticity is the point, and inheriting the developer's
    whole site-packages would let an unrelated install change an oracle verdict. This adds
    back exactly the modules the oracle has declared it compares.
    """
    env = {
        "PYTHONHASHSEED": "0",
        "PYTHONDONTWRITEBYTECODE": "1",
        "PYTHONIOENCODING": "utf-8",
    }
    for key in ("PATH", "SYSTEMROOT", "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH"):
        if key in os.environ:
            env[key] = os.environ[key]

    third_party = _third_party_paths()
    if third_party:
        env["PYTHONPATH"] = os.pathsep.join(third_party)
    return env


def run_one(source: str, timeout: float = DEFAULT_TIMEOUT_SECONDS) -> Dict[str, Any]:
    """Execute one program in an isolated child ``python3 -B`` and capture its output.

    ``-B`` skips ``.pyc`` writes; ``-S`` skips ``site`` so the run does not depend
    on user site-packages. (We deliberately do NOT use ``-I`` because isolated mode
    ignores ``PYTHONHASHSEED``, which we need for deterministic hashing.)
    """
    try:
        proc = subprocess.run(
            [sys.executable, "-B", "-S", "-c", source],
            capture_output=True,
            text=True,
            timeout=timeout,
            env=_child_env(),
            check=False,
        )
    except subprocess.TimeoutExpired as exc:
        partial = exc.stdout or ""
        if isinstance(partial, bytes):
            partial = partial.decode("utf-8", "replace")
        return {
            "ok": False,
            "stdout": partial[:OUTPUT_CAP_BYTES],
            "stderr": "TimeoutExpired",
            "exit": -1,
            "timed_out": True,
            "syntax_error": False,
        }
    except Exception as exc:  # pragma: no cover - defensive
        return {
            "ok": False,
            "stdout": "",
            "stderr": f"{type(exc).__name__}: {exc}",
            "exit": -1,
            "timed_out": False,
            "syntax_error": False,
        }

    stderr = (proc.stderr or "")[:OUTPUT_CAP_BYTES]
    return {
        "ok": proc.returncode == 0,
        "stdout": (proc.stdout or "")[:OUTPUT_CAP_BYTES],
        "stderr": stderr,
        "exit": proc.returncode,
        "timed_out": False,
        "syntax_error": proc.returncode != 0 and _is_syntax_error(stderr),
    }


def _is_syntax_error(stderr: str) -> bool:
    """True when the program failed to parse (as opposed to failing at runtime).

    A parse failure surfaces before any user code runs, so the source is simply
    not valid Python — most often a Sharpy-only type annotation the harness's AST
    subset filter does not reject at the node level.
    """
    return (
        "SyntaxError" in stderr
        or "IndentationError" in stderr
        or "TabError" in stderr
    )


def run_batch(lines: Iterable[str], timeout: float = DEFAULT_TIMEOUT_SECONDS) -> List[str]:
    """Map a JSONL request stream to a JSONL verdict stream (both keyed by ``id``)."""
    out: List[str] = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        request = json.loads(line)
        verdict = run_one(request["source"], timeout)
        verdict["id"] = request["id"]
        out.append(json.dumps(verdict))
    return out


def main(argv: List[str]) -> int:
    if len(argv) > 2 and argv[1] == "--batch":
        with open(argv[2], "r", encoding="utf-8") as handle:
            lines = handle.readlines()
    elif not sys.stdin.isatty():
        lines = sys.stdin.readlines()
    else:
        # Import-only / no input: nothing to do.
        return 0

    for verdict_line in run_batch(lines):
        print(verdict_line)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
