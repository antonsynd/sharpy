"""AST-based per-test-method classifier for CPython ``Lib/test/test_*.py`` files.

The classifier never executes the test file — it parses with :mod:`ast` and walks
each test method, collecting *reasons* (structured findings). Each method's final
:class:`Category` is the most severe category among its reasons; a method with no
disqualifying reason is ``PORTABLE``.

Design notes
------------
* Detection is deliberately conservative: when a signal is ambiguous (e.g. a list
  literal whose elements aren't all constant), the detector stays silent rather
  than risk a false ``NEEDS_REWRITE``. Under-reporting a blocker is cheaper than
  mislabelling a portable method — the port attempt surfaces the truth anyway.
* Huge-int detection avoids materialising values like ``10 ** 30_000``: a literal
  power with a large constant exponent is judged by magnitude bounds, never
  computed.
"""

from __future__ import annotations

import ast
import enum
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Optional

# int64 bounds — Sharpy ints are System.Int64 (Axiom 1); literals outside this
# range are DIVERGENT by design (SPY0328 / deviations.yaml::int-overflow-checked).
INT64_MAX = 2**63 - 1
INT64_MIN = -(2**63)

# ``test.support`` names that are runner scaffolding rather than semantics, and so
# do not by themselves disqualify a method. Intentionally conservative for v1 —
# widen only with evidence that the helper has a clean port.
SUPPORT_WHITELIST = frozenset({"verbose", "requires"})

# ``test.support`` attributes that mean the test probes CPython internals.
SUPPORT_IMPL_DETAIL = frozenset(
    {
        "cpython_only",
        "impl_detail",
        "check_impl_detail",
        "refcount_test",
        "check_sizeof",
        "gc_collect",
        "get_recursionlimit",
    }
)

# Builtins whose (3-arg) or namespace-mutating use is genuinely dynamic.
DYNAMIC_BUILTINS = frozenset(
    {"exec", "eval", "compile", "globals", "locals", "vars", "__import__"}
)
# Reflection builtins — rewriteable sometimes, so NEEDS_REWRITE rather than DYNAMIC.
REFLECTION_BUILTINS = frozenset({"setattr", "delattr", "getattr"})

# Names that reference CPython-internal machinery.
IMPL_DETAIL_SYS_ATTRS = frozenset(
    {"getsizeof", "getrefcount", "intern", "_getframe", "gettotalrefcount"}
)

# Builtin types whose subclassing does not port (Axiom 3 — no builtin subclassing).
BUILTIN_TYPES = frozenset(
    {
        "int",
        "float",
        "str",
        "bytes",
        "bytearray",
        "list",
        "dict",
        "set",
        "frozenset",
        "tuple",
        "complex",
        "bool",
    }
)

# AST-feature → docs/deviations.yaml id. These make the DIVERGENT bucket double as
# a machine-checkable map onto the divergence ledger.
DEVIATION_HUGE_INT = "int-overflow-checked"
DEVIATION_GLOBAL_NONLOCAL = "global-nonlocal-keywords"
DEVIATION_METACLASS = "no-metaclasses"
DEVIATION_DOUBLE_STAR_KWARGS = "no-double-star-kwargs"
DEVIATION_CLASSMETHOD = "no-classmethod"
DEVIATION_MULTIPLE_INHERITANCE = "no-multiple-class-inheritance"


class Category(enum.Enum):
    """Portability bucket for a test method. ``value`` is the severity rank —
    higher wins when a method carries reasons from several categories."""

    PORTABLE = 0
    NEEDS_REWRITE = 1
    DIVERGENT = 2
    DYNAMIC = 3
    IMPL_DETAIL = 4

    @property
    def label(self) -> str:
        return self.name.replace("_", "-")


@dataclass(frozen=True)
class Reason:
    """One structured finding attached to a method."""

    code: str
    category: Category
    detail: str
    line: int
    deviation_id: Optional[str] = None


@dataclass
class MethodClassification:
    module: str
    class_name: Optional[str]
    method: str
    line: int
    reasons: list[Reason] = field(default_factory=list)
    notes: list[Reason] = field(default_factory=list)

    @property
    def category(self) -> Category:
        if not self.reasons:
            return Category.PORTABLE
        return max((r.category for r in self.reasons), key=lambda c: c.value)

    @property
    def qualified_name(self) -> str:
        if self.class_name:
            return f"{self.class_name}.{self.method}"
        return self.method


@dataclass
class ModuleReport:
    module: str
    source_path: Optional[str]
    cpython_version: str
    methods: list[MethodClassification] = field(default_factory=list)
    module_notes: list[str] = field(default_factory=list)

    def counts(self) -> dict[Category, int]:
        result = {c: 0 for c in Category}
        for m in self.methods:
            result[m.category] += 1
        return result

    def portable_pct(self) -> float:
        if not self.methods:
            return 0.0
        portable = self.counts()[Category.PORTABLE]
        return 100.0 * portable / len(self.methods)


# --------------------------------------------------------------------------- #
# Literal signature helpers (heterogeneous-collection detection)
# --------------------------------------------------------------------------- #
def _literal_sig(node: ast.AST) -> Optional[str]:
    """Return a canonical type signature for a *constant* literal subtree, or
    ``None`` when the node is not a pure literal (a Name, Call, BinOp, …). Tuples
    are allowed to be heterogeneous (Sharpy tuples are typed), so their signature
    encodes the per-element shape."""
    if isinstance(node, ast.Constant):
        v = node.value
        if isinstance(v, bool):
            return "bool"
        if isinstance(v, int):
            return "int"
        if isinstance(v, float):
            return "float"
        if isinstance(v, complex):
            return "complex"
        if isinstance(v, str):
            return "str"
        if isinstance(v, bytes):
            return "bytes"
        if v is None:
            return "none"
        return "const"
    if isinstance(node, ast.UnaryOp) and isinstance(node.op, (ast.USub, ast.UAdd)):
        return _literal_sig(node.operand)
    if isinstance(node, ast.Tuple):
        maybe_parts = [_literal_sig(e) for e in node.elts]
        parts = [p for p in maybe_parts if p is not None]
        if len(parts) != len(maybe_parts):
            return None
        return "(" + ",".join(parts) + ")"
    if isinstance(node, (ast.List, ast.Set)):
        maybe_parts = [_literal_sig(e) for e in node.elts]
        parts = [p for p in maybe_parts if p is not None]
        if len(parts) != len(maybe_parts):
            return None
        uniq = set(parts)
        inner = next(iter(uniq)) if len(uniq) == 1 else "?"
        return "[" + inner + "]"
    return None


def _is_heterogeneous(node: ast.AST) -> bool:
    """A list/set literal is heterogeneous when *every* element is a pure literal
    and at least two distinct element signatures appear."""
    if not isinstance(node, (ast.List, ast.Set)):
        return False
    sigs = [_literal_sig(e) for e in node.elts]
    if not sigs or any(s is None for s in sigs):
        return False
    return len({s for s in sigs} - {"none"}) >= 2


# --------------------------------------------------------------------------- #
# Huge-int detection
# --------------------------------------------------------------------------- #
def _const_int(node: ast.AST) -> Optional[int]:
    if isinstance(node, ast.Constant) and isinstance(node.value, int) and not isinstance(
        node.value, bool
    ):
        return node.value
    if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub):
        inner = _const_int(node.operand)
        return -inner if inner is not None else None
    return None


def _exceeds_int64(node: ast.AST) -> bool:
    """True if an integer literal / simple power / shift provably overflows int64,
    without materialising astronomically large values."""
    v = _const_int(node)
    if v is not None:
        return v > INT64_MAX or v < INT64_MIN
    if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Pow):
        base = _const_int(node.left)
        exp = _const_int(node.right)
        if base is not None and exp is not None and exp >= 0 and abs(base) >= 2:
            if exp >= 63:  # 2**63 already overflows; never compute the value
                return True
            return abs(base) ** exp > INT64_MAX
    if isinstance(node, ast.BinOp) and isinstance(node.op, ast.LShift):
        base = _const_int(node.left)
        shift = _const_int(node.right)
        if base is not None and shift is not None and base != 0:
            return shift >= 63 or (abs(base) << shift) > INT64_MAX
    return False


# --------------------------------------------------------------------------- #
# Module-level fixture analysis
# --------------------------------------------------------------------------- #
def _class_bases(node: ast.ClassDef) -> list[str]:
    names = []
    for b in node.bases:
        if isinstance(b, ast.Name):
            names.append(b.id)
        elif isinstance(b, ast.Attribute):
            names.append(b.attr)
    return names


def _is_testcase_class(node: ast.ClassDef) -> bool:
    return any(b == "TestCase" for b in _class_bases(node))


def _defines_dunder(node: ast.ClassDef) -> bool:
    for item in node.body:
        if isinstance(item, (ast.FunctionDef, ast.AsyncFunctionDef)):
            if item.name.startswith("__") and item.name.endswith("__"):
                if item.name != "__init__":
                    return True
    return False


def _collect_fixture_classes(tree: ast.Module) -> dict[str, str]:
    """Map module-level class name → fixture kind for classes that are *not* test
    cases but define dunders or subclass a builtin. Referencing one of these from a
    test method is a dunder-fixture signal."""
    fixtures: dict[str, str] = {}
    for node in tree.body:
        if not isinstance(node, ast.ClassDef):
            continue
        if _is_testcase_class(node):
            continue
        bases = _class_bases(node)
        if any(b in BUILTIN_TYPES for b in bases):
            fixtures[node.name] = "builtin-subclass"
        elif _defines_dunder(node):
            fixtures[node.name] = "dunder-fixture"
    return fixtures


def _mentions_test_name(node: ast.AST) -> bool:
    """True if a (possibly composed) expression carries a ``test``-prefixed string
    literal — e.g. ``"test_%d" % i`` or ``"test_" + name``."""
    for child in ast.walk(node):
        if isinstance(child, ast.Constant) and isinstance(child.value, str):
            if child.value.startswith("test"):
                return True
    return False


def _detect_dynamic_test_generation(tree: ast.Module) -> list[str]:
    """Flag module/class scopes that synthesise ``test_*`` methods dynamically
    (setattr / locals mutation / FunctionType) — those never survive porting."""
    notes: list[str] = []

    class Visitor(ast.NodeVisitor):
        def visit_Call(self, node: ast.Call) -> None:
            func = node.func
            name = func.id if isinstance(func, ast.Name) else (
                func.attr if isinstance(func, ast.Attribute) else None
            )
            if name == "setattr" and len(node.args) >= 2:
                if _mentions_test_name(node.args[1]):
                    notes.append(
                        f"dynamic test-method generation via setattr "
                        f"(line {node.lineno})"
                    )
            self.generic_visit(node)

    Visitor().visit(tree)
    return notes


# --------------------------------------------------------------------------- #
# Per-method visitor
# --------------------------------------------------------------------------- #
class _MethodVisitor(ast.NodeVisitor):
    def __init__(
        self,
        fixture_classes: dict[str, str],
        support_names: set[str],
        depth: int = 0,
    ) -> None:
        self.fixtures = fixture_classes
        self.support_names = support_names
        self.reasons: list[Reason] = []
        self.notes: list[Reason] = []
        self._depth = depth  # nesting depth relative to the method body

    def _add(self, reason: Reason) -> None:
        self.reasons.append(reason)

    # -- names / attributes -------------------------------------------------- #
    def visit_Name(self, node: ast.Name) -> None:
        if node.id in self.fixtures:
            kind = self.fixtures[node.id]
            self._add(
                Reason(
                    code=kind,
                    category=Category.NEEDS_REWRITE,
                    detail=f"references fixture class '{node.id}' ({kind})",
                    line=node.lineno,
                )
            )
        if node.id == "gc":
            self._add(
                Reason(
                    code="impl-detail-gc",
                    category=Category.IMPL_DETAIL,
                    detail="uses the gc module (CPython internal)",
                    line=node.lineno,
                )
            )
        self.generic_visit(node)

    def visit_Attribute(self, node: ast.Attribute) -> None:
        # sys.getsizeof / sys.getrefcount / ...
        if isinstance(node.value, ast.Name):
            if node.value.id == "sys" and node.attr in IMPL_DETAIL_SYS_ATTRS:
                self._add(
                    Reason(
                        code="impl-detail-sys",
                        category=Category.IMPL_DETAIL,
                        detail=f"uses sys.{node.attr} (CPython internal)",
                        line=node.lineno,
                    )
                )
            if node.value.id in self.support_names or node.value.id == "support":
                if node.attr in SUPPORT_IMPL_DETAIL:
                    self._add(
                        Reason(
                            code="impl-detail-support",
                            category=Category.IMPL_DETAIL,
                            detail=f"uses test.support.{node.attr}",
                            line=node.lineno,
                        )
                    )
                elif node.attr not in SUPPORT_WHITELIST:
                    self._add(
                        Reason(
                            code="test-support",
                            category=Category.NEEDS_REWRITE,
                            detail=f"uses test.support.{node.attr}",
                            line=node.lineno,
                        )
                    )
        self.generic_visit(node)

    # -- calls --------------------------------------------------------------- #
    def visit_Call(self, node: ast.Call) -> None:
        func = node.func
        if isinstance(func, ast.Name):
            fname = func.id
            if fname in DYNAMIC_BUILTINS:
                self._add(
                    Reason(
                        code="exec-eval" if fname in {"exec", "eval", "compile"} else "dynamic-namespace",
                        category=Category.DYNAMIC,
                        detail=f"calls {fname}()",
                        line=node.lineno,
                    )
                )
            elif fname == "type" and len(node.args) == 3:
                self._add(
                    Reason(
                        code="dynamic-type-factory",
                        category=Category.DYNAMIC,
                        detail="uses 3-arg type() class factory",
                        line=node.lineno,
                    )
                )
            elif fname in REFLECTION_BUILTINS:
                self._add(
                    Reason(
                        code="reflection",
                        category=Category.NEEDS_REWRITE,
                        detail=f"calls {fname}()",
                        line=node.lineno,
                    )
                )
            elif fname in self.support_names and fname in SUPPORT_IMPL_DETAIL:
                self._add(
                    Reason(
                        code="impl-detail-support",
                        category=Category.IMPL_DETAIL,
                        detail=f"calls test.support.{fname}()",
                        line=node.lineno,
                    )
                )
            elif fname in self.support_names and fname not in SUPPORT_WHITELIST:
                self._add(
                    Reason(
                        code="test-support",
                        category=Category.NEEDS_REWRITE,
                        detail=f"calls test.support.{fname}()",
                        line=node.lineno,
                    )
                )
        self.generic_visit(node)

    # -- literals ------------------------------------------------------------ #
    def visit_List(self, node: ast.List) -> None:
        self._check_heterogeneous(node)
        self.generic_visit(node)

    def visit_Set(self, node: ast.Set) -> None:
        self._check_heterogeneous(node)
        self.generic_visit(node)

    def _check_heterogeneous(self, node: "ast.List | ast.Set") -> None:
        if _is_heterogeneous(node):
            kind = "list" if isinstance(node, ast.List) else "set"
            self._add(
                Reason(
                    code="heterogeneous-literal",
                    category=Category.NEEDS_REWRITE,
                    detail=f"heterogeneous {kind} literal (not a single list[T])",
                    line=node.lineno,
                )
            )

    def visit_Constant(self, node: ast.Constant) -> None:
        if _exceeds_int64(node):
            self._add(
                Reason(
                    code="huge-int",
                    category=Category.DIVERGENT,
                    detail="integer literal exceeds int64",
                    line=node.lineno,
                    deviation_id=DEVIATION_HUGE_INT,
                )
            )
        self.generic_visit(node)

    # -- operators / statements --------------------------------------------- #
    def visit_BinOp(self, node: ast.BinOp) -> None:
        # `//` and `%` both floor in Sharpy (sign of divisor), matching CPython —
        # no divergence (#1153). Only integer overflow past int64 diverges here.
        if _exceeds_int64(node):
            self._add(
                Reason(
                    code="huge-int",
                    category=Category.DIVERGENT,
                    detail="integer expression exceeds int64",
                    line=node.lineno,
                    deviation_id=DEVIATION_HUGE_INT,
                )
            )
        self.generic_visit(node)

    def visit_Global(self, node: ast.Global) -> None:
        self._add(
            Reason(
                code="global-nonlocal",
                category=Category.DIVERGENT,
                detail="uses global (C# scoping rules apply)",
                line=node.lineno,
                deviation_id=DEVIATION_GLOBAL_NONLOCAL,
            )
        )
        self.generic_visit(node)

    def visit_Nonlocal(self, node: ast.Nonlocal) -> None:
        self._add(
            Reason(
                code="global-nonlocal",
                category=Category.DIVERGENT,
                detail="uses nonlocal (C# scoping rules apply)",
                line=node.lineno,
                deviation_id=DEVIATION_GLOBAL_NONLOCAL,
            )
        )
        self.generic_visit(node)

    # -- nested defs --------------------------------------------------------- #
    def visit_ClassDef(self, node: ast.ClassDef) -> None:
        detail = "local class fixture"
        deviation = None
        category = Category.NEEDS_REWRITE
        if any(kw.arg == "metaclass" for kw in node.keywords):
            detail = "local class with metaclass"
            category = Category.DYNAMIC
            deviation = DEVIATION_METACLASS
        elif any(b in BUILTIN_TYPES for b in _class_bases(node)):
            detail = f"local class subclasses builtin ({', '.join(_class_bases(node))})"
        elif len([b for b in _class_bases(node) if b != "object"]) >= 2:
            detail = "local class with multiple inheritance"
            category = Category.DIVERGENT
            deviation = DEVIATION_MULTIPLE_INHERITANCE
        self._add(
            Reason(
                code="local-class",
                category=category,
                detail=detail,
                line=node.lineno,
                deviation_id=deviation,
            )
        )
        # Do not descend into the nested class body — its dunders are the fixture,
        # already captured by the reason above.

    def visit_FunctionDef(self, node: ast.FunctionDef) -> None:
        # Nested helper function — descend, but flag **kwargs.
        if node.args.kwarg is not None:
            self._add(
                Reason(
                    code="double-star-kwargs",
                    category=Category.DIVERGENT,
                    detail="nested function uses **kwargs",
                    line=node.lineno,
                    deviation_id=DEVIATION_DOUBLE_STAR_KWARGS,
                )
            )
        self.generic_visit(node)

    # -- generated-loop note (portable-with-unrolling) ---------------------- #
    def visit_For(self, node: ast.For) -> None:
        if _loop_body_asserts(node):
            unrollable = isinstance(node.iter, (ast.List, ast.Tuple, ast.Set))
            self.notes.append(
                Reason(
                    code="generated-loop-unroll" if unrollable else "generated-loop",
                    category=Category.PORTABLE,
                    detail=(
                        "table-driven asserts (unroll on port)"
                        if unrollable
                        else "loop-generated asserts"
                    ),
                    line=node.lineno,
                )
            )
        self.generic_visit(node)


def _loop_body_asserts(node: ast.For) -> bool:
    for child in ast.walk(node):
        if isinstance(child, ast.Assert):
            return True
        if isinstance(child, ast.Call):
            f = child.func
            if isinstance(f, ast.Attribute) and f.attr.startswith("assert"):
                return True
    return False


def _method_decorator_reasons(method: ast.FunctionDef, support_names: set[str]) -> list[Reason]:
    reasons: list[Reason] = []
    for dec in method.decorator_list:
        # bare name or attribute
        name = None
        if isinstance(dec, ast.Name):
            name = dec.id
        elif isinstance(dec, ast.Attribute):
            name = dec.attr
        elif isinstance(dec, ast.Call):
            f = dec.func
            name = f.id if isinstance(f, ast.Name) else (
                f.attr if isinstance(f, ast.Attribute) else None
            )
        if name is None:
            continue
        if name in SUPPORT_IMPL_DETAIL:
            reasons.append(
                Reason(
                    code="impl-detail-decorator",
                    category=Category.IMPL_DETAIL,
                    detail=f"@{name} decorator (CPython-only)",
                    line=dec.lineno,
                )
            )
        elif name == "classmethod":
            reasons.append(
                Reason(
                    code="classmethod",
                    category=Category.DIVERGENT,
                    detail="@classmethod (no classmethod in Sharpy)",
                    line=dec.lineno,
                    deviation_id=DEVIATION_CLASSMETHOD,
                )
            )
        elif name in support_names and name not in SUPPORT_WHITELIST:
            reasons.append(
                Reason(
                    code="test-support",
                    category=Category.NEEDS_REWRITE,
                    detail=f"@{name} decorator from test.support",
                    line=dec.lineno,
                )
            )
    return reasons


# --------------------------------------------------------------------------- #
# Public API
# --------------------------------------------------------------------------- #
def _iter_test_methods(
    tree: ast.Module,
) -> Iterable[tuple[Optional[str], ast.FunctionDef]]:
    for node in tree.body:
        if isinstance(node, ast.ClassDef):
            for item in node.body:
                if isinstance(item, ast.FunctionDef) and item.name.startswith("test"):
                    yield node.name, item
        elif isinstance(node, ast.FunctionDef) and node.name.startswith("test_"):
            yield None, node


def _collect_support_names(tree: ast.Module) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.ImportFrom) and node.module and "test.support" in node.module:
            for alias in node.names:
                names.add(alias.asname or alias.name)
    return names


def classify_source(
    source: str,
    module: str,
    source_path: Optional[str] = None,
    cpython_version: str = "3.12",
) -> ModuleReport:
    """Classify every test method in *source*. ``module`` is the reporting name
    (e.g. ``test_bisect``)."""
    tree = ast.parse(source, filename=source_path or module)
    fixture_classes = _collect_fixture_classes(tree)
    support_names = _collect_support_names(tree)

    report = ModuleReport(
        module=module,
        source_path=source_path,
        cpython_version=cpython_version,
        module_notes=_detect_dynamic_test_generation(tree),
    )

    for class_name, method in _iter_test_methods(tree):
        visitor = _MethodVisitor(fixture_classes, support_names)
        for stmt in method.body:
            visitor.visit(stmt)
        reasons = _method_decorator_reasons(method, support_names) + visitor.reasons
        report.methods.append(
            MethodClassification(
                module=module,
                class_name=class_name,
                method=method.name,
                line=method.lineno,
                reasons=reasons,
                notes=visitor.notes,
            )
        )
    return report


def _provenance_path(p: Path) -> str:
    """Reduce an absolute CPython test path to a stable ``Lib/test/<file>`` form so
    committed reports/skeletons don't embed machine-specific install locations."""
    parts = p.parts
    if "test" in parts:
        idx = len(parts) - 1 - parts[::-1].index("test")
        tail = parts[idx:]
        # Prefer the `.../lib/pythonX.Y/test/...` slice as `Lib/test/...`.
        return "Lib/" + "/".join(tail)
    return str(p)


def classify_file(path: str | Path, cpython_version: str = "3.12") -> ModuleReport:
    p = Path(path)
    source = p.read_text(encoding="utf-8")
    return classify_source(
        source,
        module=p.stem,
        source_path=_provenance_path(p),
        cpython_version=cpython_version,
    )
