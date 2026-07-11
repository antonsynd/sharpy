"""Machine-readable divergence ledger for the CPython golden oracle (#1030).

Phase 8 task 4 of the Wave 2 plan. The ledger records, at *method granularity*,
every place a ported CPython test is expected to behave differently across the two
sides of the oracle — Sharpy (via the compiler + .NET) and stock CPython (via
:mod:`build_tools.cpython_oracle.dual_run`). It exists so that a divergence is
either **documented** (with a reason tying it to an axiom decision + spec section)
or **a bug** — never a silent surprise in CI.

Generated, not hand-maintained
-------------------------------
``build_tools/cpython_oracle/ledger.yaml`` is a *generated* artifact. Its two
sources of truth are:

* ``docs/deviations.yaml`` — the ~55 designed Python-vs-Sharpy divergences. The
  machine-relevant projection of each entry (id, category, audience, severity,
  diagnostic code, spec ref) is copied into the ledger's ``deviations`` section so
  a method entry can cite one by id without re-stating the axiom rationale.
* ``# oracle-ledger:`` annotation blocks embedded in the ported ``.spy`` files
  under ``src/Sharpy.Stdlib.Tests/Spy/cpython/``. These carry the per-method
  entries — they live next to the code they describe so the human knowledge is not
  divorced from the test.

Entry kinds
-----------
Every method entry is one of three kinds (see :data:`ENTRY_KINDS`):

* ``expected-fail-cpython`` — the ported test *passes under Sharpy* but is expected
  to *fail under CPython*. This is a genuine, designed Sharpy-vs-Python divergence
  (e.g. ``//`` truncation, int64 wrap): the ported assertions encode Sharpy's
  result, so CPython disagrees. Cited ``deviation`` is required.
* ``expected-fail-sharpy`` — the ported test is faithful Python (passes under
  CPython) but is expected to *fail under Sharpy* because of a Sharpy bug or gap.
  Tracked by a ``bug`` issue number. Under CPython these must still pass.
* ``not-ported`` — a CPython test method that was deliberately *omitted* from the
  port (never appears in the ``.spy`` file). Used to record, e.g., a case whose
  Sharpy behavior is a known bug so the omission is auditable rather than silent.

CI enforcement (dual-execution)
-------------------------------
:mod:`build_tools.cpython_oracle.dual_run` grows a ``--ledger`` flag. Running the
ported tree under CPython, it fails on either of two divergences from the ledger:

* an **unexplained** divergence — a test that fails under CPython with no covering
  ``expected-fail-cpython`` entry; and
* a **stale** entry — an ``expected-fail-cpython`` entry whose test unexpectedly
  *passes* (the divergence was fixed, or the test renamed), so the ledger lies.
"""

from __future__ import annotations

import ast
import io
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Set, Tuple

import yaml

SCHEMA_VERSION = 1

# Entry kinds ----------------------------------------------------------------- #
EXPECTED_FAIL_CPYTHON = "expected-fail-cpython"
EXPECTED_FAIL_SHARPY = "expected-fail-sharpy"
NOT_PORTED = "not-ported"
ENTRY_KINDS = frozenset({EXPECTED_FAIL_CPYTHON, EXPECTED_FAIL_SHARPY, NOT_PORTED})

# Sides a divergence can land on.
SIDE_CPYTHON = "cpython"
SIDE_SHARPY = "sharpy"
SIDES = frozenset({SIDE_CPYTHON, SIDE_SHARPY})

# The ``side`` implied by a kind when the annotation omits it.
_KIND_DEFAULT_SIDE = {
    EXPECTED_FAIL_CPYTHON: SIDE_CPYTHON,
    EXPECTED_FAIL_SHARPY: SIDE_SHARPY,
    NOT_PORTED: SIDE_SHARPY,
}

# The marker introducing an annotation block inside a ``.spy`` file.
ANNOTATION_MARKER = "# oracle-ledger:"

# Machine-relevant fields copied from each docs/deviations.yaml entry.
_DEVIATION_FIELDS = (
    "id",
    "category",
    "audience",
    "severity",
    "code",
    "spec_ref",
    "existing_diagnostic",
    "planned_diagnostic",
)

# Repo-relative default locations (this file lives at build_tools/cpython_oracle/).
_REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DEVIATIONS_PATH = _REPO_ROOT / "docs" / "deviations.yaml"
DEFAULT_PORTED_ROOT = _REPO_ROOT / "src" / "Sharpy.Stdlib.Tests" / "Spy" / "cpython"
DEFAULT_LEDGER_PATH = Path(__file__).resolve().parent / "ledger.yaml"


class LedgerError(ValueError):
    """Raised when the ported tree or the deviations catalog is inconsistent."""


@dataclass
class LedgerEntry:
    """One method-granularity divergence entry."""

    kind: str
    module: str
    test: str
    side: str
    reason: str
    deviation: Optional[str] = None
    bug: Optional[int] = None
    spec_ref: Optional[str] = None
    origin: Optional[str] = None
    source: Optional[str] = None

    def key(self) -> Tuple[str, str]:
        """The (module, test) identity the dual-run matches outcomes against."""
        return (self.module, self.test)

    def to_dict(self) -> Dict[str, object]:
        data: Dict[str, object] = {
            "kind": self.kind,
            "module": self.module,
            "test": self.test,
            "side": self.side,
        }
        if self.deviation is not None:
            data["deviation"] = self.deviation
        if self.bug is not None:
            data["bug"] = self.bug
        if self.spec_ref is not None:
            data["spec_ref"] = self.spec_ref
        if self.origin is not None:
            data["origin"] = self.origin
        data["reason"] = self.reason
        if self.source is not None:
            data["source"] = self.source
        return data


# --------------------------------------------------------------------------- #
# Deviations catalog (docs/deviations.yaml → machine-relevant projection)
# --------------------------------------------------------------------------- #
def load_deviations(path: Path = DEFAULT_DEVIATIONS_PATH) -> List[Dict[str, object]]:
    """Return the machine-relevant projection of every ``docs/deviations.yaml``
    entry, sorted by id. Raises :class:`LedgerError` if the catalog is missing or
    malformed — the ledger cannot be generated without its reason vocabulary."""
    if not path.exists():
        raise LedgerError(f"deviations catalog not found: {path}")
    catalog = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    raw = catalog.get("deviations")
    if not isinstance(raw, list):
        raise LedgerError(f"{path} has no top-level 'deviations' list")
    projected: List[Dict[str, object]] = []
    for entry in raw:
        if not isinstance(entry, dict) or "id" not in entry:
            raise LedgerError(f"deviation entry missing 'id': {entry!r}")
        projected.append({field: entry.get(field) for field in _DEVIATION_FIELDS})
    projected.sort(key=lambda d: str(d["id"]))
    return projected


def deviation_ids(deviations: Sequence[Dict[str, object]]) -> Set[str]:
    return {str(d["id"]) for d in deviations}


# --------------------------------------------------------------------------- #
# Annotation scanning (ported .spy tree → per-method entries)
# --------------------------------------------------------------------------- #
def _test_functions(source: str) -> Optional[Set[str]]:
    """Names of ``@test``-decorated top-level functions in a ``.spy`` file, or
    ``None`` if the file is not parseable as Python (Sharpy-only syntax)."""
    try:
        tree = ast.parse(source)
    except SyntaxError:
        return None
    names: Set[str] = set()
    for node in tree.body:
        if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            continue
        for dec in node.decorator_list:
            target = dec.func if isinstance(dec, ast.Call) else dec
            base = target.value if isinstance(target, ast.Attribute) else target
            if isinstance(base, ast.Name) and base.id == "test":
                names.add(node.name)
                break
    return names


def _strip_comment(line: str) -> Optional[str]:
    """Return the text of a ``#`` comment line (one optional leading space
    removed), or ``None`` if the line is not a comment."""
    stripped = line.lstrip()
    if not stripped.startswith("#"):
        return None
    content = stripped[1:]
    return content[1:] if content.startswith(" ") else content


def _parse_annotation_blocks(source: str) -> List[Tuple[int, Dict[str, object]]]:
    """Extract ``# oracle-ledger:`` annotation blocks as ``(line, mapping)``.

    A block is the marker line followed by contiguous comment lines; its body
    (the comment text below the marker) is parsed as a YAML mapping."""
    lines = source.splitlines()
    blocks: List[Tuple[int, Dict[str, object]]] = []
    i = 0
    while i < len(lines):
        if lines[i].strip() != ANNOTATION_MARKER:
            i += 1
            continue
        marker_line = i + 1  # 1-based
        body: List[str] = []
        j = i + 1
        while j < len(lines):
            # A subsequent marker begins a new block, even without a blank
            # separator line between them.
            if lines[j].strip() == ANNOTATION_MARKER:
                break
            content = _strip_comment(lines[j])
            if content is None or content.strip() == "":
                break
            body.append(content)
            j += 1
        text = "\n".join(body)
        try:
            parsed = yaml.safe_load(text)
        except yaml.YAMLError as exc:  # noqa: PERF203 - surface the offending block
            raise LedgerError(
                f"invalid YAML in oracle-ledger block at line {marker_line}: {exc}"
            ) from exc
        if not isinstance(parsed, dict):
            raise LedgerError(
                f"oracle-ledger block at line {marker_line} is not a mapping"
            )
        blocks.append((marker_line, parsed))
        i = j
    return blocks


def _entry_from_mapping(
    mapping: Dict[str, object],
    *,
    module: str,
    source_ref: str,
    line: int,
    test_funcs: Optional[Set[str]],
    known_deviations: Set[str],
) -> LedgerEntry:
    kind = mapping.get("kind")
    if kind not in ENTRY_KINDS:
        raise LedgerError(
            f"{source_ref}: unknown ledger kind {kind!r} "
            f"(expected one of {sorted(ENTRY_KINDS)})"
        )
    test = mapping.get("test")
    if not isinstance(test, str) or not test:
        raise LedgerError(f"{source_ref}: entry missing string 'test'")
    reason = mapping.get("reason")
    if not isinstance(reason, str) or not reason.strip():
        raise LedgerError(f"{source_ref}: entry {test!r} missing 'reason'")

    side = mapping.get("side") or _KIND_DEFAULT_SIDE[kind]
    if side not in SIDES:
        raise LedgerError(f"{source_ref}: entry {test!r} has invalid side {side!r}")

    deviation = mapping.get("deviation")
    if deviation is not None:
        if not isinstance(deviation, str):
            raise LedgerError(f"{source_ref}: entry {test!r} 'deviation' must be a string")
        if deviation not in known_deviations:
            raise LedgerError(
                f"{source_ref}: entry {test!r} cites unknown deviation "
                f"'{deviation}' (not in docs/deviations.yaml)"
            )

    bug = mapping.get("bug")
    if bug is not None and not isinstance(bug, int):
        raise LedgerError(f"{source_ref}: entry {test!r} 'bug' must be an integer issue number")

    if deviation is None and bug is None:
        raise LedgerError(
            f"{source_ref}: entry {test!r} must cite a 'deviation' id (designed "
            f"divergence) or a 'bug' issue number (Sharpy defect)"
        )
    if kind == EXPECTED_FAIL_CPYTHON and deviation is None:
        raise LedgerError(
            f"{source_ref}: {EXPECTED_FAIL_CPYTHON} entry {test!r} must cite a "
            f"'deviation' id — a CPython failure is a designed divergence"
        )

    # Method-presence invariants tie the entry to the actual ported tree.
    if test_funcs is not None:
        present = test in test_funcs
        if kind in (EXPECTED_FAIL_CPYTHON, EXPECTED_FAIL_SHARPY) and not present:
            raise LedgerError(
                f"{source_ref}: {kind} entry references '{test}', which is not a "
                f"@test function in {module}"
            )
        if kind == NOT_PORTED and present:
            raise LedgerError(
                f"{source_ref}: not-ported entry '{test}' is actually present as a "
                f"@test function in {module}"
            )

    spec_ref = mapping.get("spec_ref")
    if spec_ref is not None and not isinstance(spec_ref, str):
        raise LedgerError(f"{source_ref}: entry {test!r} 'spec_ref' must be a string")
    origin = mapping.get("origin")
    if origin is not None and not isinstance(origin, str):
        raise LedgerError(f"{source_ref}: entry {test!r} 'origin' must be a string")

    return LedgerEntry(
        kind=kind,
        module=module,
        test=test,
        side=side,
        reason=" ".join(reason.split()),
        deviation=deviation,
        bug=bug,
        spec_ref=spec_ref,
        origin=origin,
        source=f"{source_ref}:{line}",
    )


def scan_ported_tree(
    ported_root: Path,
    known_deviations: Set[str],
    repo_root: Path = _REPO_ROOT,
) -> List[LedgerEntry]:
    """Scan every ``.spy`` file under *ported_root* for annotation blocks and
    return the validated :class:`LedgerEntry` list (sorted for determinism)."""
    entries: List[LedgerEntry] = []
    if not ported_root.exists():
        return entries
    for path in sorted(ported_root.rglob("*.spy")):
        source = path.read_text(encoding="utf-8")
        blocks = _parse_annotation_blocks(source)
        if not blocks:
            continue
        test_funcs = _test_functions(source)
        try:
            source_ref = str(path.relative_to(repo_root))
        except ValueError:
            source_ref = str(path)
        module = path.stem
        for line, mapping in blocks:
            entries.append(
                _entry_from_mapping(
                    mapping,
                    module=module,
                    source_ref=source_ref,
                    line=line,
                    test_funcs=test_funcs,
                    known_deviations=known_deviations,
                )
            )
    entries.sort(key=lambda e: (e.module, e.kind, e.test))
    return entries


# --------------------------------------------------------------------------- #
# Ledger assembly + rendering
# --------------------------------------------------------------------------- #
def build_ledger(
    deviations_path: Path = DEFAULT_DEVIATIONS_PATH,
    ported_root: Path = DEFAULT_PORTED_ROOT,
    cpython_version: str = "3.12",
    repo_root: Path = _REPO_ROOT,
) -> Dict[str, object]:
    """Assemble the full ledger data structure from the catalog + ported tree."""
    deviations = load_deviations(deviations_path)
    entries = scan_ported_tree(ported_root, deviation_ids(deviations), repo_root)
    return {
        "schema_version": SCHEMA_VERSION,
        "generated_by": "python -m build_tools.cpython_oracle ledger",
        "cpython_version": cpython_version,
        "deviations": deviations,
        "entries": [e.to_dict() for e in entries],
    }


_HEADER = (
    "# GENERATED — do not edit by hand.\n"
    "#\n"
    "# Machine-readable divergence ledger for the CPython golden oracle (#1030).\n"
    "# Sources of truth:\n"
    "#   docs/deviations.yaml                     designed Python-vs-Sharpy divergences\n"
    "#   src/Sharpy.Stdlib.Tests/Spy/cpython/**   '# oracle-ledger:' method annotations\n"
    "#\n"
    "# Regenerate:  python -m build_tools.cpython_oracle ledger --write\n"
    "# Verify (CI): python -m build_tools.cpython_oracle ledger --check\n"
    "# Enforce:     python -m build_tools.cpython_oracle.dual_run --ledger <path> <tree>\n"
    "#\n"
)


def render_ledger(data: Dict[str, object]) -> str:
    """Render the ledger to deterministic YAML with a provenance header."""
    buffer = io.StringIO()
    buffer.write(_HEADER)
    yaml.safe_dump(
        data,
        buffer,
        sort_keys=False,
        default_flow_style=False,
        allow_unicode=True,
        width=100,
    )
    return buffer.getvalue()


def write_ledger(
    data: Dict[str, object], path: Path = DEFAULT_LEDGER_PATH
) -> None:
    path.write_text(render_ledger(data), encoding="utf-8")


def load_ledger(path: Path = DEFAULT_LEDGER_PATH) -> Dict[str, object]:
    if not Path(path).exists():
        raise LedgerError(f"ledger not found: {path}")
    data = yaml.safe_load(Path(path).read_text(encoding="utf-8")) or {}
    if not isinstance(data, dict):
        raise LedgerError(f"{path} did not parse to a mapping")
    return data


# --------------------------------------------------------------------------- #
# Enforcement (consumed by dual_run --ledger)
# --------------------------------------------------------------------------- #
def expected_cpython_failures(ledger: Dict[str, object]) -> Dict[Tuple[str, str], Dict[str, object]]:
    """Map (module, test) → entry for every ``expected-fail-cpython`` entry: the
    tests dual-run should observe *failing* under CPython."""
    result: Dict[Tuple[str, str], Dict[str, object]] = {}
    for entry in ledger.get("entries", []) or []:
        if entry.get("kind") == EXPECTED_FAIL_CPYTHON:
            result[(str(entry.get("module")), str(entry.get("test")))] = entry
    return result


@dataclass
class EnforcementResult:
    """Outcome of checking dual-run results against the ledger."""

    unexplained_failures: List[Tuple[str, str]] = field(default_factory=list)
    stale_entries: List[Tuple[str, str]] = field(default_factory=list)
    satisfied: List[Tuple[str, str]] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not self.unexplained_failures and not self.stale_entries


def evaluate_ledger(
    results: Dict[Tuple[str, str], str],
    ledger: Dict[str, object],
) -> EnforcementResult:
    """Compare per-test CPython *results* (``pass`` / ``fail`` / ``skip`` keyed by
    ``(module, test)``) against the ledger's expected CPython failures.

    A failing test is *excused* only by a covering ``expected-fail-cpython`` entry;
    such an entry is *satisfied* when its test indeed failed and *stale* when it
    passed, was skipped, or is absent.
    """
    expected = expected_cpython_failures(ledger)
    result = EnforcementResult()

    for key, status in sorted(results.items()):
        if status != "fail":
            continue
        if key in expected:
            result.satisfied.append(key)
        else:
            result.unexplained_failures.append(key)

    for key in sorted(expected):
        if results.get(key) != "fail":
            result.stale_entries.append(key)

    return result
