#!/usr/bin/env python3
"""CPython numeric-literal oracle for the Sharpy lexer's numeric twin (#2300, P68 row C1).

``NumericLiteralTwinTests`` (src/Sharpy.Compiler.Tests/Lexer/) enumerates numeric-literal
spellings in the grammar Sharpy shares with CPython and asks each side ONE question per
spelling: "is this text a single numeric constant with value V, or is it refused?"

The oracle sits at the PROGRAM altitude, not the token altitude: ``ast.parse(source,
mode="eval")`` whose body is an ``ast.Constant`` holding an ``int`` or a ``float``. CPython's
``tokenize`` splits ``.5e`` into NUMBER ``.5`` + NAME ``e`` and refuses only at parse time,
while Sharpy refuses in the lexer; comparing token boundaries would manufacture divergences
the language does not have, so token-boundary agreement is deliberately NOT asked.

A ``complex`` constant (``1j``) is reported ``ok: false, error: "imaginary"`` — Sharpy has no
complex literal, the twin excludes the imaginary family from its matrix, and the distinct
error lets that exclusion be checked.

Batch protocol (the C# twin spawns one ``python3`` per batch, not per spelling):

    stdin  : one JSON object per line -> {"id": <int>, "source": "<spelling>"}
    stdout : one JSON object per line -> {"id": <int>, "ok": true,
                                          "kind": "int" | "float", "value": "<repr>"}
                                     or  {"id": <int>, "ok": false, "error": "<message>"}

``--batch <file>`` reads the JSONL from a file instead of stdin. With no input the module is
import-only (used by build_tools/tests/test_number_oracle.py).

Deterministic and dependency-free; pinned to CPython 3.12 by the caller.
"""
from __future__ import annotations

import ast
import json
import sys
import warnings
from typing import Any, Dict, Iterable, List


def classify(source: str) -> Dict[str, Any]:
    """One spelling -> ``{"ok": True, "kind", "value"}`` or ``{"ok": False, "error"}``."""
    try:
        with warnings.catch_warnings():
            # A SyntaxWarning (e.g. "invalid decimal literal" before a keyword) is not a
            # refusal: CPython runs the program. Never let a warnings filter promote it.
            warnings.simplefilter("ignore")
            tree = ast.parse(source, mode="eval")
    except SyntaxError as exc:
        return {"ok": False, "error": str(exc.msg)}
    except ValueError as exc:  # e.g. a null byte in the source
        return {"ok": False, "error": f"{type(exc).__name__}: {exc}"}

    body = tree.body
    if not isinstance(body, ast.Constant):
        return {"ok": False, "error": f"not a constant: {type(body).__name__}"}
    value = body.value
    # Exact type checks: bool is an int subclass, and True/False are not numeric literals.
    if type(value) is int:
        return {"ok": True, "kind": "int", "value": str(value)}
    if type(value) is float:
        return {"ok": True, "kind": "float", "value": repr(value)}
    if type(value) is complex:
        return {"ok": False, "error": "imaginary"}
    return {"ok": False, "error": f"not a numeric constant: {type(value).__name__}"}


def run_batch(lines: Iterable[str]) -> List[str]:
    """Map a JSONL request stream to a JSONL verdict stream (both keyed by ``id``)."""
    out: List[str] = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        request = json.loads(line)
        verdict = classify(request["source"])
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
