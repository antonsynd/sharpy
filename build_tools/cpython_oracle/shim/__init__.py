"""CPython execution shim for Sharpy ``.spy`` oracle tests (#1030).

Phase 3 of the golden-oracle plan: the ported CPython tests under
``src/Sharpy.Stdlib.Tests/Spy/cpython/`` must *also* run under stock ``python3``.
If a port passes under both Sharpy and CPython, it is a faithful oracle; if it
passes under Sharpy but fails under CPython, either the port is unfaithful or we
have found a genuine Sharpy-vs-Python divergence (which feeds the divergence
ledger, plan Phase 8 task 4).

The ``.spy`` test dialect is (deliberately) a subset of Python: PEP 526 typed
locals, function annotations, ``import bisect`` etc. resolving to the real CPython
stdlib. Only two constructs are *not* plain Python and this shim supplies them:

* ``@test`` — the Sharpy test-entry decorator (and its ``@test.<sub>`` family).
  Plain Python has no such name, so the runner injects it into each file's exec
  namespace (see :func:`exec_globals`).
* ``from unittest import approx`` — Sharpy exposes ``approx`` on its ``unittest``
  module; CPython's does not. :func:`install` monkeypatches the real ``unittest``
  module so the import resolves, mirroring ``Sharpy.Unittest.Approx``'s semantics.

Everything the three pilot ports (bisect, colorsys, textwrap) touch beyond these
two is a real CPython stdlib API with an identical name, so no PascalCase/camelCase
aliases or kwarg adapters are needed *yet*. The extension seams for those —
:data:`MODULE_ALIASES` and :data:`MODULE_KWARG_ALIASES` — are wired through
:func:`apply_module_adapters` so later ports can register renames without touching
the runner.
"""

from __future__ import annotations

import functools
import importlib
from typing import Any, Callable, Dict, List, Tuple

# Attribute stamped on functions decorated with ``@test`` so the runner can
# discover them after executing a file.
TEST_MARKER = "__spy_oracle_test__"


class _TestDecorator:
    """The ``@test`` decorator, plus its ``@test.<sub>`` family.

    Bare ``@test`` marks a nullary function as a runnable oracle test. The
    sub-decorators (``@test.skip``, ``@test.parametrize`` ...) mirror the Sharpy
    test-framework decorator names (``DecoratorNames.KnownTestDecorators``) so
    ports that use them execute rather than raising ``NameError``. Only bare
    ``@test`` is exercised by the current pilots; the sub-decorators are the
    known extension surface of the same decorator, not speculative API.
    """

    def __call__(self, func: Callable[[], Any]) -> Callable[[], Any]:
        setattr(func, TEST_MARKER, True)
        return func

    # --- @test.<sub> family (mirrors DecoratorNames.KnownTestDecorators) ---

    def skip(self, *dargs: Any, **dkwargs: Any) -> Callable[[Callable], Callable]:
        reason = dargs[0] if dargs else dkwargs.get("reason", "")

        def deco(func: Callable[[], Any]) -> Callable[[], Any]:
            setattr(func, TEST_MARKER, True)
            func.__spy_skip__ = (True, reason)  # type: ignore[attr-defined]
            return func

        return deco

    def skip_if(self, condition: Any, reason: str = "") -> Callable[[Callable], Callable]:
        def deco(func: Callable[[], Any]) -> Callable[[], Any]:
            setattr(func, TEST_MARKER, True)
            if condition:
                func.__spy_skip__ = (True, reason)  # type: ignore[attr-defined]
            return func

        return deco

    def parametrize(self, _argnames: Any, argvalues: Any) -> Callable[[Callable], Callable]:
        """Run the body once per row, spreading each row as positional args."""

        def deco(func: Callable[..., Any]) -> Callable[[], Any]:
            @functools.wraps(func)
            def runner() -> None:
                for row in argvalues:
                    if isinstance(row, tuple):
                        func(*row)
                    else:
                        func(row)

            setattr(runner, TEST_MARKER, True)
            return runner

        return deco

    def fixture(self, func: Callable[..., Any]) -> Callable[..., Any]:
        # Fixtures are helpers, not entry points: return unchanged, unmarked.
        return func

    def mark(self, *_args: Any, **_kwargs: Any) -> Callable[[Callable], Callable]:
        # Metadata annotation — identity decorator.
        return lambda func: func

    def collection(self, *_args: Any, **_kwargs: Any) -> Callable[[Callable], Callable]:
        return lambda func: func


#: The singleton injected as ``test`` into every ported file's namespace.
test = _TestDecorator()


class _Approx:
    """Tolerance-comparison object for ``actual == approx(expected, ...)``.

    Faithful to ``Sharpy.Unittest.Approx`` (``src/Sharpy.Stdlib/Unittest/__Init__.cs``):
    ``abs`` takes precedence over ``places``; ``places`` uses round-to-N-decimals
    semantics (as ``unittest.TestCase.assertAlmostEqual`` does). ``rel`` is a
    pytest-style relative tolerance included for forward compatibility.
    """

    __slots__ = ("expected", "places", "abs", "rel")

    def __init__(self, expected: float, places: int = 7, abs: float = 0.0, rel: float | None = None):
        self.expected = expected
        self.places = places
        self.abs = abs
        self.rel = rel

    def _close(self, actual: float) -> bool:
        diff = builtins_abs(actual - self.expected)
        if self.abs and self.abs > 0.0:
            return diff <= self.abs
        if self.rel is not None:
            return diff <= self.rel * builtins_abs(self.expected)
        # places-based: matches assertAlmostEqual(round(a-b, places) == 0)
        return round(actual - self.expected, self.places) == 0

    def __eq__(self, other: object) -> bool:
        try:
            return self._close(float(other))  # type: ignore[arg-type]
        except (TypeError, ValueError):
            return NotImplemented

    def __ne__(self, other: object) -> bool:
        result = self.__eq__(other)
        if result is NotImplemented:
            return result
        return not result

    def __repr__(self) -> str:
        if self.abs:
            return f"approx({self.expected!r}, abs={self.abs!r})"
        return f"approx({self.expected!r}, places={self.places!r})"

    __hash__ = None  # type: ignore[assignment]


# Local reference so _Approx does not shadow the builtin via its ``abs`` slot.
builtins_abs = abs


def approx(expected: float, places: int = 7, abs: float = 0.0, rel: float | None = None) -> _Approx:
    """Mirror of ``Sharpy.Unittest.Approx`` for use under CPython."""
    return _Approx(expected, places=places, abs=abs, rel=rel)


# --------------------------------------------------------------------------- #
# Extension seams for later ports (empty while the pilots need nothing).
# --------------------------------------------------------------------------- #

#: module name -> {alias attr: real attr}. Applied after the module is imported
#: so ``mod.PascalCaseName`` resolves to the real snake_case symbol. Register
#: entries here when a future port hits a Sharpy-renamed stdlib symbol.
MODULE_ALIASES: Dict[str, Dict[str, str]] = {}

#: module name -> {func attr: {spy kwarg: cpython kwarg}}. A hook for kwarg
#: renames; unused by the pilots (bisect's a/x/lo/hi already match CPython).
MODULE_KWARG_ALIASES: Dict[str, Dict[str, Dict[str, str]]] = {}


def apply_module_adapters() -> None:
    """Apply :data:`MODULE_ALIASES` / kwarg adapters to already-importable modules.

    No-op while both registries are empty (the pilot state). Import failures are
    swallowed: a module only needs adapting if a port actually imports it.
    """
    for module_name, aliases in MODULE_ALIASES.items():
        try:
            mod = importlib.import_module(module_name)
        except ImportError:
            continue
        for alias_attr, real_attr in aliases.items():
            if not hasattr(mod, alias_attr) and hasattr(mod, real_attr):
                setattr(mod, alias_attr, getattr(mod, real_attr))

    for module_name, func_map in MODULE_KWARG_ALIASES.items():
        try:
            mod = importlib.import_module(module_name)
        except ImportError:
            continue
        for func_attr, kwarg_map in func_map.items():
            target = getattr(mod, func_attr, None)
            if target is None or not kwarg_map:
                continue
            setattr(mod, func_attr, _wrap_kwargs(target, kwarg_map))


def _wrap_kwargs(target: Callable[..., Any], kwarg_map: Dict[str, str]) -> Callable[..., Any]:
    @functools.wraps(target)
    def wrapper(*args: Any, **kwargs: Any) -> Any:
        renamed = {kwarg_map.get(k, k): v for k, v in kwargs.items()}
        return target(*args, **renamed)

    return wrapper


def install() -> None:
    """Make the ``.spy`` dialect importable under CPython.

    Idempotent. Monkeypatches ``unittest.approx`` (Sharpy exposes it; CPython does
    not) and applies any registered module adapters. Call once before executing
    ported files.
    """
    import unittest

    if not hasattr(unittest, "approx"):
        unittest.approx = approx  # type: ignore[attr-defined]

    apply_module_adapters()


def exec_globals(module_name: str, file_path: str) -> Dict[str, Any]:
    """Build the globals namespace a ported file executes in.

    Injects ``test`` (the decorator has no import in the dialect) and standard
    module dunders. ``approx`` is *not* injected here — ports import it via
    ``from unittest import approx`` after :func:`install` patches the module.
    """
    return {
        "__name__": module_name,
        "__file__": file_path,
        "__builtins__": __builtins__,
        "test": test,
    }


def discover_tests(namespace: Dict[str, Any]) -> List[Tuple[str, Callable[[], Any]]]:
    """Return ``(name, func)`` for ``@test``-marked functions in definition order.

    Insertion order of the exec namespace (a dict) preserves source definition
    order under CPython 3.7+, so tests run top-to-bottom.
    """
    found: List[Tuple[str, Callable[[], Any]]] = []
    for name, value in namespace.items():
        if callable(value) and getattr(value, TEST_MARKER, False):
            found.append((name, value))
    return found
