#!/usr/bin/env python3
"""CPython differential-parse comparer for the Sharpy parser fuzzers (#1037).

The Sharpy side (``CPythonDifferentialParseTests``) generates programs in the
shared Sharpy/Python subset, unparses them, and asks two questions of CPython:

1. **Biconditional** — does ``ast.parse`` accept the same text the Sharpy parser
   accepts? A source that one parser takes and the other rejects is a divergence.
2. **Skeleton** — for text both parsers accept, do their high-level structures
   agree? We compare a coarse, canonical multiset of *landmark* node kinds whose
   Sharpy<->CPython correspondence is unambiguous. Leaf / among-ambiguous kinds
   (Name, Constant, BinOp, BoolOp, Compare, Slice, JoinedStr, ...) are
   deliberately NOT counted so the comparison stays robust against benign
   representational differences (e.g. ``x[a, b]`` vs a multi-axis subscript both
   surface a ``Subscript`` + ``Tuple``).

Batch protocol (the C# harness spawns one ``python3`` per batch, not per sample):

    stdin  : one JSON object per line -> {"id": <int>, "source": "<text>"}
    stdout : one JSON object per line -> {"id": <int>, "ok": true,
                                          "landmarks": {"Call": 2, ...}}
                                     or  {"id": <int>, "ok": false,
                                          "error": "<message>"}

``--batch <file>`` reads the JSONL from a file instead of stdin. With no input
the module is import-only (used by the pytest coverage in build_tools/tests).

Deterministic and dependency-free; pinned to CPython 3.12 by the caller.
"""
from __future__ import annotations

import ast
import json
import sys
from typing import Any, Dict, Iterable, List

# Python ast node type name -> canonical landmark token. The C# LandmarkCounter
# in CPythonDifferentialParseTests must map the corresponding Sharpy AST nodes to
# exactly these tokens (see that file for the node<->token table).
_LANDMARKS = {
    # statement kinds (not produced by the current expression-statement fuzzer,
    # kept so the comparer generalizes to statement-level samples)
    "FunctionDef": "FunctionDef",
    "If": "If",
    "While": "While",
    "For": "For",
    "Return": "Return",
    "Assign": "Assign",
    "AugAssign": "AugAssign",
    "AnnAssign": "AnnAssign",
    # expression kinds
    "Call": "Call",
    "Lambda": "Lambda",
    "ListComp": "ListComp",
    "SetComp": "SetComp",
    "DictComp": "DictComp",
    "IfExp": "IfExp",
    "Subscript": "Subscript",
    "Attribute": "Attribute",
    "List": "List",
    "Dict": "Dict",
    "Set": "Set",
    "Tuple": "Tuple",
}

# The stable set of landmark tokens, exported for the pytest cross-check.
LANDMARK_TOKENS = frozenset(_LANDMARKS.values())


def landmarks(tree: ast.AST) -> Dict[str, int]:
    """Coarse multiset of landmark node kinds reachable from ``tree``."""
    counts: Dict[str, int] = {}
    for node in ast.walk(tree):
        token = _LANDMARKS.get(type(node).__name__)
        if token is not None:
            counts[token] = counts.get(token, 0) + 1
    return counts


def parse_source(source: str) -> Dict[str, Any]:
    """Parse one source string; return an ``ok``/``landmarks`` or ``ok``/``error`` verdict."""
    try:
        tree = ast.parse(source)
    except SyntaxError as exc:
        return {"ok": False, "error": str(exc.msg)}
    except ValueError as exc:  # e.g. null bytes in source
        return {"ok": False, "error": f"{type(exc).__name__}: {exc}"}
    except Exception as exc:  # pragma: no cover - defensive
        return {"ok": False, "error": f"{type(exc).__name__}: {exc}"}
    return {"ok": True, "landmarks": landmarks(tree)}


def run_batch(lines: Iterable[str]) -> List[str]:
    """Map a JSONL request stream to a JSONL verdict stream (both keyed by ``id``)."""
    out: List[str] = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        request = json.loads(line)
        verdict = parse_source(request["source"])
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
