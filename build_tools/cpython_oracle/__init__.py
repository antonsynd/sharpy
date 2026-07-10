"""CPython test-suite classifier for the Sharpy golden oracle (#1030).

Classifies each test *method* of a CPython ``Lib/test/test_*.py`` file into one
of five portability buckets so we know which methods are worth porting to
``.spy`` and which are structurally out of reach.

Categories (see :mod:`build_tools.cpython_oracle.classifier`):

* ``PORTABLE``     — value-level assertions on typed APIs; port directly
                     (generated-test loops are portable-with-unrolling).
* ``NEEDS_REWRITE`` — portable in principle but leans on constructs that need a
                     manual rewrite (heterogeneous list literals, dunder-fixture
                     classes, ``test.support`` helpers beyond the whitelist).
* ``DYNAMIC``       — ``exec``/``eval``/``type()`` factories, namespace mutation;
                     no static analogue.
* ``IMPL_DETAIL``   — refcounts, ``sys.getsizeof``, ``gc`` internals,
                     ``@cpython_only``; CPython-internal by definition.
* ``DIVERGENT``     — hits a documented Sharpy divergence (int64 ints, ``//``
                     truncation, …) from ``docs/deviations.yaml``.

The classifier is the first phase of #1030; the DYNAMIC/DIVERGENT report doubles
as a systematic map of Sharpy's distance from Python.
"""

from .classifier import (
    Category,
    MethodClassification,
    ModuleReport,
    Reason,
    classify_file,
    classify_source,
)

__all__ = [
    "Category",
    "Reason",
    "MethodClassification",
    "ModuleReport",
    "classify_source",
    "classify_file",
]
