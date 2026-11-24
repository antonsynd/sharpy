#!/usr/bin/env python3
"""Gate: the ruling records, the law table and the charter agree with each other.

An owner ruling used to be one row of prose in a charter table. The 2026-11-21 audit
(docs/audits/ruling-audit-2026-11-21.md) found what prose could not hold: rulings that name no
law, rows replaced without a mark, an exception whose law was never changed, two verdicts for one
case. This script makes each of those a failed check. It reads

    docs/process/rulings/rulings.yaml         one record per ruling
    docs/process/rulings/laws.yaml            the written laws and the audit's draft principles
    docs/process/rulings/axes.yaml            the closed lists a case is named from
    docs/process/rulings/policy.yaml          which decisions an agent may take alone
    docs/process/rulings/known-findings.yaml  findings that wait for the owner, each with a reference
    docs/process/rounds/*-batching.md         the charters (section 4 rows)

and runs nothing: evidence is re-run by `ruling_evidence.py recheck`.

Two kinds of record. A record with `seed:` was written by the audit from a charter row: its labels
are a model's reading, kept under `audit:`, and it is held only to the structural checks. Any
other record is *strict*: it is what a new ruling must provide.

Checks (the name is what a finding and a known-findings row carry):

  schema              a record, law, family, policy or known-findings row is malformed. A strict
                      ruling needs: date, issue, kind, rule, options (one chosen, one recommended),
                      basis, and laws unless the basis is `choice`; a `language` or `library` one
                      also needs at least one case with measured evidence.
  charter-totality    a charter row has no record, or a seed record has no charter row.
  law-anchor          a written law's `anchor` text is not in its `source` file, or is there more
                      than once.
  reference           a law id, ruling id, family, axis or value that does not exist.
  supersession        `supersedes`/`amends` on one record without `superseded_by`/`amended_by`
                      on the other, or the reverse.
  charter-mark        a wholly superseded ruling whose charter row is not struck (or a struck row
                      whose record names no successor), or an amended ruling whose charter row
                      does not name the ruling that amended it.
  exception-in-law    a ruling with `exception_to: L` that L does not name: for a written law, the
                      law's own section of its source (from its anchor to the next law), not the
                      whole file; for a draft, the `against` list of its entry.
  collision           two live cases with the same family and coordinates and different outcomes,
                      where neither owner supersedes, amends or declares an exception to the other.
  invariance          a law says an axis does not matter, and two live cases that differ only
                      along it have different outcomes, without `differs_because` naming the law.
  door                a strict ruling whose `door`, `breaks_running` or `undo_breaks` is not what
                      ruling_evidence.derive_door computes from its cases' evidence.
  considered          a strict ruling that does not answer, in `considered`, every live ruling
                      and law with a case in the same family. This is how "I looked and nothing
                      decides it" becomes a list that can be checked for completeness.
  owner-required      a strict ruling an agent decided (`decided_by: agent`) whose derived door is
                      not one policy.yaml lets an agent take alone.

The ratchet: every finding must be absent or listed in known-findings.yaml, and every row there
must still fire. A new finding fails; so does a row whose finding is gone (delete the row). Only
the findings that wait for the owner or for a text can be listed (PARKABLE); a row names its
finding exactly (check, subjects and, for a collision or an invariance, the case in `where`), so
one row never covers two findings. A malformed record is fixed, never listed.

What this cannot see: whether a label is *right*. A `basis: follows` naming a law that does not
decide the case passes. The case coordinates are the narrow part that is checked for truth: the
coordinates decide which rulings collide, and evidence is measured, not asserted.

Exit status: 0 clean, 1 findings, 2 the inputs could not be read.
"""

from __future__ import annotations

import argparse
import itertools
import re
import sys
from pathlib import Path
from typing import Dict, Iterable, List, NamedTuple, Optional, Set, Tuple

import yaml

try:  # the package form (tests, `python -m`) and the script form both work
    from build_tools import ruling_evidence
except ImportError:  # pragma: no cover - exercised only when run as a bare script
    import ruling_evidence  # type: ignore[no-redef]

REPO = Path(__file__).resolve().parents[1]
RULINGS_DIR = Path("docs/process/rulings")
ROUNDS_DIR = Path("docs/process/rounds")
ROW_RE = re.compile(r"^\| *(~~)?(R-[A-Z]{1,3})(~~)? *\|(.*)$")
ID_RE = re.compile(r"^R-[A-Z]{1,3}$")
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")

KINDS = (
    "language",
    "library",
    "diagnostic",
    "implementation",
    "cli-lsp",
    "test-instrument",
    "process",
)
SEMANTIC_KINDS = ("language", "library")
BASES = ("follows", "precedent", "exception", "new-law", "choice")
VERDICTS = ("run", "refuse", "open")
LAW_STATUS = ("written", "draft")
LINKS = (("supersedes", "superseded_by"), ("amends", "amended_by"))
# The checks whose findings may wait in known-findings.yaml: they need the owner, or a text brought
# into line. Each of their findings has a key no other finding shares, so a row covers exactly one.
# Everything else is a defect in a record and is fixed, not parked.
PARKABLE = ("charter-mark", "exception-in-law", "collision", "invariance")


class Finding(NamedTuple):
    check: str
    subjects: Tuple[str, ...]
    message: str
    where: str = ""  # tells two findings of one check about the same subjects apart

    @property
    def key(self) -> Tuple[str, Tuple[str, ...], str]:
        return self.check, tuple(sorted(self.subjects)), self.where


class Case(NamedTuple):
    owner: str  # a ruling id or a law id
    index: int
    family: str
    coords: Tuple[Tuple[str, str], ...]
    outcome: Tuple[str, Optional[str]]  # (verdict, answer)
    raw: dict


class Inputs(NamedTuple):
    rulings: List[dict]
    laws: List[dict]
    families: Dict[str, dict]
    policy: dict
    known: List[dict]
    charter: Dict[str, Tuple[bool, str]]  # id -> (struck, row text)
    root: Path
    charter_duplicates: Tuple[str, ...] = ()


class InputError(Exception):
    pass


def _load(path: Path, key: str, kind: type, required: bool = True) -> object:
    if not path.is_file():
        if required:
            raise InputError(f"missing {path}")
        return kind()
    try:
        data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        raise InputError(f"{path}: {exc}") from exc
    if not isinstance(data, dict) or not isinstance(data.get(key, kind()), kind):
        raise InputError(f"{path}: expected a mapping whose `{key}` is a {kind.__name__}")
    return data.get(key, kind())


def read_charter(root: Path) -> Tuple[Dict[str, Tuple[bool, str]], Tuple[str, ...]]:
    """The ruling rows of every charter's section 4, and the ids that have more than one row."""
    rows: Dict[str, Tuple[bool, str]] = {}
    duplicates: List[str] = []
    for path in sorted((root / ROUNDS_DIR).glob("*-batching.md")):
        in_rulings = False
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.startswith("## "):
                in_rulings = line.startswith("## 4.")
                continue
            match = ROW_RE.match(line) if in_rulings else None
            if match:
                if match.group(2) in rows:
                    duplicates.append(match.group(2))
                rows[match.group(2)] = (bool(match.group(1) and match.group(3)), match.group(4))
    return rows, tuple(duplicates)


def read_inputs(root: Path) -> Inputs:
    base = root / RULINGS_DIR
    charter, duplicates = read_charter(root)
    return Inputs(
        rulings=_load(base / "rulings.yaml", "rulings", list),  # type: ignore[arg-type]
        laws=_load(base / "laws.yaml", "laws", list),  # type: ignore[arg-type]
        families=_load(base / "axes.yaml", "families", dict),  # type: ignore[arg-type]
        policy=_load(base / "policy.yaml", "agent_may_decide", dict, required=False),  # type: ignore[arg-type]
        known=_load(base / "known-findings.yaml", "findings", list, required=False),  # type: ignore[arg-type]
        charter=charter,
        root=root,
        charter_duplicates=duplicates,
    )


def sanitized(inputs: Inputs) -> Inputs:
    """The inputs without the entries `schema` has already reported as not even the right shape.

    Every other check reads through this, so a string where a record belongs is one schema finding
    and never a traceback (which would exit 1 and read as findings).
    """
    seen: Set[str] = set()
    rulings: List[dict] = []
    for record in inputs.rulings:
        if not (isinstance(record, dict) and isinstance(record.get("id"), str) and ID_RE.match(record["id"])):
            continue
        if record["id"] in seen:
            continue
        seen.add(record["id"])
        rulings.append(dict(record, cases=_well_formed_cases(record.get("cases"))))
    laws: List[dict] = []
    for law in inputs.laws:
        if isinstance(law, dict) and isinstance(law.get("id"), str) and law["id"] not in {x["id"] for x in laws}:
            invariants = [
                i
                for i in (law.get("invariants") if isinstance(law.get("invariants"), list) else [])
                if isinstance(i, dict) and isinstance(i.get("family"), str)
            ]
            laws.append(dict(law, cases=_well_formed_cases(law.get("cases")), invariants=invariants))
    families = {
        name: family
        for name, family in inputs.families.items()
        if isinstance(family, dict)
        and isinstance(family.get("axes"), dict)
        and all(isinstance(values, list) for values in family["axes"].values())
    }
    known = [row for row in inputs.known if isinstance(row, dict)]
    return inputs._replace(rulings=rulings, laws=laws, families=families, known=known)


def _well_formed_cases(cases: object) -> List[dict]:
    if not isinstance(cases, list):
        return []
    return [
        case
        for case in cases
        if isinstance(case, dict) and isinstance(case.get("coords"), dict) and isinstance(case.get("family"), str)
    ]


def _ids(value: object) -> List[str]:
    return [str(v) for v in value] if isinstance(value, list) else []


def is_live(record: dict) -> bool:
    return not _ids(record.get("superseded_by"))


# --- schema ---------------------------------------------------------------------------------------


def _case_problems(owner: str, index: int, case: object, strict: bool) -> Iterable[str]:
    where = f"case {index + 1}"
    if not isinstance(case, dict):
        yield f"{where}: not a mapping"
        return
    if not isinstance(case.get("family"), str):
        yield f"{where}: `family` is required"
    if not isinstance(case.get("coords"), dict) or not case["coords"]:
        yield f"{where}: `coords` is required"
    if case.get("verdict") not in VERDICTS:
        yield f"{where}: `verdict` must be one of {', '.join(VERDICTS)}"
    if "differs_because" in case and not (
        isinstance(case["differs_because"], str) and case.get("exception_to")
    ):
        yield f"{where}: `differs_because` needs text and an `exception_to` law"
    evidence = case.get("evidence")
    if evidence is None:
        if strict:
            yield f"{where}: a new ruling's case needs measured `evidence` (ruling_evidence.py measure)"
        return
    if not isinstance(evidence, dict) or not isinstance(evidence.get("program"), str):
        yield f"{where}: evidence needs a `program`"
        return
    for field, kinds in (("sharpy", ruling_evidence.SHARPY_KINDS), ("python", ruling_evidence.PYTHON_KINDS)):
        if not ruling_evidence.is_observation(evidence.get(field), kinds):
            yield f"{where}: evidence `{field}` is not an observation of kind {', '.join(kinds)}: {evidence.get(field)!r}"
    if ("twin" in evidence) == (evidence.get("python") == "no-twin"):
        yield f"{where}: evidence has a `twin` exactly when `python` is not `no-twin`"
    for field in ("files", "twin_files"):
        beside = evidence.get(field)
        if beside is not None and not (
            isinstance(beside, dict) and beside and all(isinstance(k, str) and isinstance(v, str) for k, v in beside.items())
        ):
            yield f"{where}: evidence `{field}` maps a file name to its text"
    if "twin_files" in evidence and "twin" not in evidence:
        yield f"{where}: evidence has `twin_files` only beside a `twin`"
    if strict and not ruling_evidence.is_observation(evidence.get("after"), ruling_evidence.DECIDED_KINDS):
        yield f"{where}: a new ruling's evidence needs `after`, the observation the ruling decides ({', '.join(ruling_evidence.DECIDED_KINDS)})"


def _ruling_problems(record: dict) -> Iterable[str]:
    strict = "seed" not in record
    if record.get("kind") not in KINDS:
        yield f"`kind` must be one of {', '.join(KINDS)}"
    if not isinstance(record.get("rule"), str) or not record["rule"].strip():
        yield "`rule` (the decision in one sentence) is required"
    for field in ("supersedes", "superseded_by", "amends", "amended_by"):
        if field in record and not isinstance(record[field], list):
            yield f"`{field}` must be a list of ruling ids"
    cases = record.get("cases") or []
    if not isinstance(cases, list):
        yield "`cases` must be a list"
        cases = []
    for index, case in enumerate(cases):
        yield from _case_problems(str(record.get("id")), index, case, strict)
    if not strict:
        return
    for field in ("date", "issue"):
        if not record.get(field):
            yield f"a new ruling needs `{field}`"
    if record.get("date") and not DATE_RE.match(str(record["date"])):
        yield f"`date` must be YYYY-MM-DD (rulings are ordered by it): {record['date']!r}"
    options = record.get("options")
    if (
        not isinstance(options, list)
        or len(options) < 2
        or not all(isinstance(o, dict) and o.get("text") for o in options)
    ):
        yield "a new ruling needs `options`: at least two, each with `text`"
    else:
        for flag in ("chosen", "recommended"):
            if sum(1 for o in options if o.get(flag) is True) != 1:
                yield f"exactly one option must be `{flag}: true`"
    if record.get("basis") not in BASES:
        yield f"`basis` must be one of {', '.join(BASES)}"
    elif record["basis"] != "choice" and not _ids(record.get("laws")):
        yield f"basis `{record['basis']}` needs `laws`: the law or ruling it rests on"
    if record.get("basis") == "exception" and not record.get("exception_to"):
        yield "basis `exception` needs `exception_to`: the law it departs from"
    if record.get("decided_by") not in ("owner", "agent"):
        yield "`decided_by` must be `owner` or `agent`"
    if record.get("kind") in SEMANTIC_KINDS and not cases:
        yield "a new language or library ruling needs at least one case"


def check_schema(inputs: Inputs) -> Iterable[Finding]:
    seen: Set[str] = set()
    for record in inputs.rulings:
        rid = record.get("id") if isinstance(record, dict) else None
        if not isinstance(rid, str) or not ID_RE.match(rid):
            yield Finding("schema", (str(rid),), "a ruling needs an `id` like R-AB")
            continue
        if rid in seen:
            yield Finding("schema", (rid,), "duplicate id")
        seen.add(rid)
        for problem in _ruling_problems(record):
            yield Finding("schema", (rid,), problem)
    seen_laws: Set[str] = set()
    for law in inputs.laws:
        lid = law.get("id") if isinstance(law, dict) else None
        if not isinstance(lid, str) or lid in seen_laws:
            yield Finding("schema", (str(lid),), "a law needs a unique `id`")
            continue
        seen_laws.add(lid)
        if law.get("status") not in LAW_STATUS:
            yield Finding(
                "schema", (lid,), f"`status` must be one of {', '.join(LAW_STATUS)}"
            )
        for field in ("statement", "source", "anchor"):
            if not isinstance(law.get(field), str) or not law[field].strip():
                yield Finding("schema", (lid,), f"`{field}` is required")
        for index, case in enumerate(law.get("cases") or []):
            for problem in _case_problems(lid, index, case, strict=False):
                yield Finding("schema", (lid,), problem)
        for invariant in law.get("invariants") or []:
            if not (
                isinstance(invariant, dict)
                and isinstance(invariant.get("family"), str)
                and _ids(invariant.get("along"))
            ):
                yield Finding(
                    "schema",
                    (lid,),
                    "an invariant needs `family` and `along`: a list of axes",
                )
    for name, family in inputs.families.items():
        axes = family.get("axes") if isinstance(family, dict) else None
        if not isinstance(axes, dict) or not all(
            isinstance(v, list) and v for v in axes.values()
        ):
            yield Finding(
                "schema",
                (name,),
                "a family needs `axes`: each a non-empty list of values",
            )
    for combo, allowed in inputs.policy.items():
        if combo not in POLICY_KEYS or not isinstance(allowed, bool):
            yield Finding(
                "schema",
                (str(combo),),
                f"policy keys are {', '.join(POLICY_KEYS)}, each true or false",
            )
    for rid in inputs.charter_duplicates:
        yield Finding("schema", (rid,), "the charters hold more than one section-4 row for this ruling")
    rows_seen: Set[Tuple[str, Tuple[str, ...], str]] = set()
    for row in inputs.known:
        if not (isinstance(row, dict) and isinstance(row.get("check"), str) and _ids(row.get("subjects")) and row.get("ref")):
            yield Finding("schema", ("known-findings",), f"a row needs `check`, `subjects` and `ref`: {row!r}")
            continue
        if row["check"] not in PARKABLE:
            yield Finding(
                "schema",
                ("known-findings",),
                f"a `{row['check']}` finding cannot wait in known-findings.yaml (only {', '.join(PARKABLE)} can); fix it",
            )
        key = known_key(row)
        if key in rows_seen:
            yield Finding("schema", ("known-findings",), f"two rows for {row['check']}: {' '.join(_ids(row['subjects']))}")
        rows_seen.add(key)


def known_key(row: dict) -> Tuple[str, Tuple[str, ...], str]:
    return str(row.get("check")), tuple(sorted(_ids(row.get("subjects")))), str(row.get("where") or "")


# --- cross-reference ------------------------------------------------------------------------------


def check_charter_totality(inputs: Inputs) -> Iterable[Finding]:
    records = {r["id"]: r for r in inputs.rulings if isinstance(r.get("id"), str)}
    for rid in inputs.charter:
        if rid not in records:
            yield Finding(
                "charter-totality",
                (rid,),
                "the charter has this ruling and rulings.yaml has no record",
            )
    for rid, record in records.items():
        if "seed" in record and rid not in inputs.charter:
            yield Finding(
                "charter-totality", (rid,), "a seed record with no charter row"
            )


def check_law_anchor(inputs: Inputs) -> Iterable[Finding]:
    for law in inputs.laws:
        if law.get("status") != "written" or not isinstance(law.get("source"), str):
            continue
        path = inputs.root / law["source"]
        if not path.is_file():
            yield Finding("law-anchor", (law["id"],), f"source file {law['source']} does not exist")
            continue
        count = path.read_text(encoding="utf-8").count(str(law.get("anchor")))
        if count == 0:
            yield Finding("law-anchor", (law["id"],), f"anchor text not found in {law['source']}: {law.get('anchor')!r}")
        elif count > 1:
            # The law's section is read from the anchor (exception-in-law); a second occurrence
            # would let an earlier mention of the law stand for its text.
            yield Finding("law-anchor", (law["id"],), f"anchor text occurs {count} times in {law['source']}; it must pick out one place: {law.get('anchor')!r}")


def _case_refs(
    owner: str, case: dict, inputs: Inputs, law_ids: Set[str]
) -> Iterable[Finding]:
    family = inputs.families.get(str(case.get("family")))
    if family is None:
        yield Finding("reference", (owner,), f"unknown family {case.get('family')!r}")
        return
    axes = family["axes"]
    coords = case.get("coords") or {}
    for axis in axes:
        if axis not in coords:
            yield Finding(
                "reference",
                (owner,),
                f"family {case['family']}: coordinate `{axis}` is missing",
            )
    for axis, value in coords.items():
        if axis not in axes:
            yield Finding(
                "reference", (owner,), f"family {case['family']} has no axis `{axis}`"
            )
        elif value not in axes[axis]:
            yield Finding(
                "reference",
                (owner,),
                f"axis `{axis}` has no value {value!r} (add it to axes.yaml first)",
            )
    answers = family.get("answers")
    if answers and case.get("verdict") != "open" and case.get("answer") not in answers:
        yield Finding(
            "reference",
            (owner,),
            f"family {case['family']}: `answer` must be one of {', '.join(answers)}",
        )
    if case.get("exception_to") and case["exception_to"] not in law_ids:
        yield Finding(
            "reference",
            (owner,),
            f"exception_to names no law: {case['exception_to']!r}",
        )


def check_reference(inputs: Inputs) -> Iterable[Finding]:
    ruling_ids = {r["id"] for r in inputs.rulings if isinstance(r.get("id"), str)}
    law_ids = {law["id"] for law in inputs.laws if isinstance(law.get("id"), str)}
    for record in inputs.rulings:
        rid = str(record.get("id"))
        for field in ("supersedes", "superseded_by", "amends", "amended_by"):
            for other in _ids(record.get(field)):
                if other not in ruling_ids:
                    yield Finding(
                        "reference", (rid,), f"`{field}` names no ruling: {other}"
                    )
        if "seed" not in record:
            for law in _ids(record.get("laws")):
                if law not in law_ids and law not in ruling_ids:
                    yield Finding(
                        "reference",
                        (rid,),
                        f"`laws` names neither a law nor a ruling: {law}",
                    )
            if record.get("exception_to") and record["exception_to"] not in law_ids:
                yield Finding(
                    "reference",
                    (rid,),
                    f"exception_to names no law: {record['exception_to']!r}",
                )
        for case in record.get("cases") or []:
            if isinstance(case, dict):
                yield from _case_refs(rid, case, inputs, law_ids)
    for law in inputs.laws:
        for case in law.get("cases") or []:
            if isinstance(case, dict):
                yield from _case_refs(str(law.get("id")), case, inputs, law_ids)
        for invariant in law.get("invariants") or []:
            family = (
                inputs.families.get(str(invariant.get("family")))
                if isinstance(invariant, dict)
                else None
            )
            if family is None:
                yield Finding(
                    "reference",
                    (str(law.get("id")),),
                    f"invariant names no family: {invariant!r}",
                )
                continue
            for axis in _ids(invariant.get("along")):
                if axis not in family["axes"]:
                    yield Finding(
                        "reference",
                        (str(law.get("id")),),
                        f"invariant axis `{axis}` is not in family {invariant['family']}",
                    )


def check_supersession(inputs: Inputs) -> Iterable[Finding]:
    records = {r["id"]: r for r in inputs.rulings if isinstance(r.get("id"), str)}
    for record in records.values():
        for forward, back in LINKS:
            for other in _ids(record.get(forward)):
                if other in records and record["id"] not in _ids(
                    records[other].get(back)
                ):
                    yield Finding(
                        "supersession",
                        (record["id"], other),
                        f"{record['id']} `{forward}` {other}, but {other} has no `{back}: [{record['id']}]`",
                    )
            for other in _ids(record.get(back)):
                if other in records and record["id"] not in _ids(
                    records[other].get(forward)
                ):
                    yield Finding(
                        "supersession",
                        (other, record["id"]),
                        f"{record['id']} is `{back}` {other}, but {other} has no `{forward}: [{record['id']}]`",
                    )


def check_charter_mark(inputs: Inputs) -> Iterable[Finding]:
    for record in inputs.rulings:
        rid = record.get("id")
        if rid not in inputs.charter:
            continue
        struck, text = inputs.charter[rid]
        successors = _ids(record.get("superseded_by"))
        if successors and not struck:
            yield Finding(
                "charter-mark",
                (rid,),
                f"superseded by {', '.join(successors)}, but its charter row is not struck through",
            )
        if struck and not successors:
            yield Finding(
                "charter-mark",
                (rid,),
                "its charter row is struck through, but the record names no `superseded_by`",
            )
        for amender in _ids(record.get("amended_by")):
            if not re.search(rf"(?<![\w-]){re.escape(amender)}(?![A-Za-z])", text):
                yield Finding(
                    "charter-mark",
                    (rid, amender),
                    f"amended by {amender}, but the charter row of {rid} does not name {amender}",
                )


def _exceptions(inputs: Inputs) -> Iterable[Tuple[str, str]]:
    for record in inputs.rulings:
        if not is_live(record):
            continue
        if record.get("exception_to"):
            yield record["id"], record["exception_to"]
        for case in record.get("cases") or []:
            if isinstance(case, dict) and case.get("exception_to"):
                yield record["id"], case["exception_to"]


def law_section(text: str, anchor: str) -> Optional[str]:
    """The part of a law's source that is that law: from its anchor to where the next one starts.

    A heading anchor runs to the next heading of its level or above. Any other anchor (a bold
    paragraph, a list item, a table row) is its own line and the indented lines that continue it:
    the first line that is blank or not indented ends it, so a neighbouring paragraph, list item
    or table row is never read as part of the law.
    """
    start = text.find(anchor)
    if start < 0:
        return None
    line_start = text.rfind("\n", 0, start) + 1
    heading = re.match(r"(#{1,6}) ", text[line_start:])
    if heading:
        end = re.compile(rf"^#{{1,{len(heading.group(1))}}} ", re.M).search(text, start + len(anchor))
        return text[line_start : end.start() if end else len(text)]
    lines = text[line_start:].split("\n")
    kept = [lines[0]]
    for line in lines[1:]:
        if not line.strip() or not line[0].isspace():
            break
        kept.append(line)
    return "\n".join(kept) + "\n"


def check_exception_in_law(inputs: Inputs) -> Iterable[Finding]:
    laws = {law["id"]: law for law in inputs.laws}
    for rid, lid in sorted(set(_exceptions(inputs))):
        law = laws.get(lid)
        if law is None:
            continue
        if law.get("status") == "draft":
            # A draft has no law text to amend; its own entry lists the rulings that go against it.
            if rid not in _ids(law.get("against")):
                yield Finding("exception-in-law", (rid, lid), f"{rid} is an exception to the draft {lid}, which does not list it under `against`")
            continue
        path = inputs.root / str(law.get("source"))
        section = law_section(path.read_text(encoding="utf-8"), str(law.get("anchor"))) if path.is_file() else None
        if section is None:
            continue  # `law-anchor` reports a missing source or anchor
        if not re.search(rf"(?<![\w-]){re.escape(rid)}(?![A-Za-z])", section):
            yield Finding("exception-in-law", (rid, lid), f"{rid} is an exception to {lid}, but the text of {lid} in {law['source']} does not name {rid}")


# --- cases ----------------------------------------------------------------------------------------


def live_cases(inputs: Inputs) -> List[Case]:
    cases: List[Case] = []
    owners = [(r, is_live(r)) for r in inputs.rulings] + [
        (law, True) for law in inputs.laws
    ]
    for owner, live in owners:
        if not live or not isinstance(owner.get("id"), str):
            continue
        for index, raw in enumerate(owner.get("cases") or []):
            if (
                not isinstance(raw, dict)
                or not isinstance(raw.get("coords"), dict)
                or raw.get("family") not in inputs.families
            ):
                continue
            coords = tuple(sorted((str(k), str(v)) for k, v in raw["coords"].items()))
            cases.append(
                Case(
                    owner["id"],
                    index,
                    raw["family"],
                    coords,
                    (str(raw.get("verdict")), raw.get("answer")),
                    raw,
                )
            )
    return cases


def _related(inputs: Inputs) -> Dict[str, Set[str]]:
    """For each ruling: the rulings and laws it supersedes, amends or is declared an exception to."""
    related: Dict[str, Set[str]] = {}
    for record in inputs.rulings:
        if isinstance(record.get("id"), str):
            links = set(
                itertools.chain.from_iterable(
                    _ids(record.get(f))
                    for f in ("supersedes", "amends", "superseded_by", "amended_by")
                )
            )
            related[record["id"]] = links
    for rid, lid in _exceptions(inputs):
        related.setdefault(rid, set()).add(lid)
    return related


def _where(family: str, coords: Iterable[Tuple[str, str]]) -> str:
    return (family + " " + ",".join(f"{axis}={value}" for axis, value in coords)).strip()


def check_collision(inputs: Inputs) -> Iterable[Finding]:
    related = _related(inputs)
    cases = live_cases(inputs)
    for a, b in itertools.combinations(cases, 2):
        if (a.family, a.coords) != (b.family, b.coords) or a.outcome == b.outcome:
            continue
        # One owner giving one case two verdicts is a collision too; only a recorded relation
        # between two owners (supersedes, amends, a declared exception) explains a difference.
        if a.owner != b.owner and (b.owner in related.get(a.owner, ()) or a.owner in related.get(b.owner, ())):
            continue
        yield Finding(
            "collision",
            (a.owner, b.owner),
            f"{a.family} {dict(a.coords)}: {a.owner} says {_show(a)}, {b.owner} says {_show(b)}",
            _where(a.family, a.coords),
        )


def _show(case: Case) -> str:
    verdict, answer = case.outcome
    return f"{verdict} ({answer})" if answer else verdict


def check_invariance(inputs: Inputs) -> Iterable[Finding]:
    cases = live_cases(inputs)
    for law in inputs.laws:
        for invariant in law.get("invariants") or []:
            if not isinstance(invariant, dict):
                continue
            along = set(_ids(invariant.get("along")))
            compare_answer = bool(
                inputs.families.get(str(invariant.get("family")), {}).get("answers")
            )
            groups: Dict[Tuple[Tuple[str, str], ...], List[Case]] = {}
            for case in cases:
                if case.family == invariant.get("family") and case.outcome[0] != "open":
                    if case.raw.get("exception_to") == law.get("id") and case.raw.get(
                        "differs_because"
                    ):
                        continue
                    fixed = tuple(pair for pair in case.coords if pair[0] not in along)
                    groups.setdefault(fixed, []).append(case)
            for fixed, members in sorted(groups.items()):
                outcome = (
                    (lambda c: c.outcome[1])
                    if compare_answer
                    else (lambda c: c.outcome[0])
                )
                if len({outcome(c) for c in members}) > 1:
                    owners = tuple(sorted({c.owner for c in members}))
                    detail = "; ".join(
                        f"{c.owner} {dict(p for p in c.coords if p[0] in along)} -> {_show(c)}"
                        for c in members
                    )
                    yield Finding(
                        "invariance",
                        owners + (law["id"],),
                        f"{law['id']} says {', '.join(sorted(along))} must not matter in {invariant['family']} {dict(fixed)}: {detail}",
                        _where(str(invariant["family"]), fixed),
                    )


# --- strict rulings -------------------------------------------------------------------------------

POLICY_KEYS = (
    "no_change",
    "reversible_refusal",
    "breaking_refusal",
    "admission_or_value_change",
    "kept_divergence",
)


def doors_of(record: dict) -> List[Dict[str, object]]:
    """The derived door of each case that has complete evidence."""
    doors: List[Dict[str, object]] = []
    for case in record.get("cases") or []:
        evidence = case.get("evidence") if isinstance(case, dict) else None
        if not isinstance(evidence, dict) or not all(
            ruling_evidence.is_observation(evidence.get(field), kinds)
            for field, kinds in (
                ("sharpy", ruling_evidence.SHARPY_KINDS),
                ("after", ruling_evidence.DECIDED_KINDS),
                ("python", ruling_evidence.PYTHON_KINDS),
            )
        ):
            continue
        door = ruling_evidence.derive_door(evidence["sharpy"], evidence["after"], evidence["python"])
        door["class"] = policy_class(evidence["sharpy"], evidence["after"], door)
        doors.append(door)
    return doors


def door_of(record: dict) -> Optional[Dict[str, object]]:
    """The door of a ruling over all its cases, or None when no case has complete evidence.

    A ruling breaks running programs if any of its cases does, and cannot be taken back cheaply if
    any of its cases cannot: one case that refuses a running program and another that admits a new
    one make a ruling that does both. The policy class is judged per case (check_owner_required).
    """
    doors = doors_of(record)
    if not doors:
        return None
    breaks = any(d["breaks_running"] for d in doors)
    undo = any(d["undo_breaks"] for d in doors)
    deciding = [d for d in doors if d["undo_breaks"] or d["breaks_running"]] or doors
    return {
        "breaks_running": breaks,
        "undo_breaks": undo,
        "door": "one-way" if undo else "two-way",
        "why": "; ".join(sorted({str(d["why"]) for d in deciding})),
    }


def policy_class(before: str, after: str, door: Dict[str, object]) -> str:
    if before == after:
        return "kept_divergence" if door["undo_breaks"] else "no_change"
    if ruling_evidence.kind_of(after) == "refused":
        return "breaking_refusal" if door["breaks_running"] else "reversible_refusal"
    return "admission_or_value_change"


def check_door(inputs: Inputs) -> Iterable[Finding]:
    for record in inputs.rulings:
        if "seed" in record or not isinstance(record.get("id"), str):
            continue
        door = door_of(record)
        if door is None:
            continue
        for field in ("door", "breaks_running", "undo_breaks"):
            if record.get(field) != door[field]:
                yield Finding(
                    "door",
                    (record["id"],),
                    f"`{field}` is {record.get(field)!r}; the evidence gives {door[field]!r} ({door['why']})",
                )


def check_owner_required(inputs: Inputs) -> Iterable[Finding]:
    for record in inputs.rulings:
        if "seed" in record or record.get("decided_by") != "agent":
            continue
        doors = doors_of(record)
        if not doors:
            if record.get("kind") in SEMANTIC_KINDS:
                yield Finding("owner-required", (record["id"],), "an agent decided it and no case has measured evidence to derive the door from")
            continue
        # Every case is judged: one reserved case reserves the ruling, whichever case is "widest".
        reserved = sorted({str(d["class"]) for d in doors if inputs.policy.get(d["class"]) is not True})
        if reserved:
            why = "; ".join(sorted({str(d["why"]) for d in doors if d["class"] in reserved}))
            yield Finding("owner-required", (record["id"],), f"an agent decided a `{', '.join(reserved)}` decision; policy.yaml reserves it for the owner ({why})")


def check_considered(inputs: Inputs) -> Iterable[Finding]:
    cases = live_cases(inputs)
    dates = {r["id"]: str(r["date"]) for r in inputs.rulings if isinstance(r.get("id"), str) and r.get("date")}
    for record in inputs.rulings:
        if "seed" in record or not isinstance(record.get("id"), str):
            continue
        families = {c.family for c in cases if c.owner == record["id"]}
        neighbours = {
            c.owner for c in cases if c.family in families and c.owner != record["id"]
        }
        answered = {
            str(item.get("id"))
            for item in record.get("considered") or []
            if isinstance(item, dict) and item.get("why")
        }
        linked = (
            set(_ids(record.get("laws")))
            | set(_ids(record.get("supersedes")))
            | set(_ids(record.get("amends")))
        )
        # A ruling answers what stood when it was made. One made on a later date could not have
        # been considered; it is the later one that must answer this one.
        mine = dates.get(record["id"])
        later = {n for n in neighbours if mine and n in dates and dates[n] > mine}
        for missing in sorted(neighbours - answered - linked - later):
            yield Finding(
                "considered",
                (record["id"], missing),
                f"{missing} has a case in the same family; say in `considered` why it does not decide this one (or name it in `laws`)",
            )


CHECKS = {
    "schema": check_schema,
    "charter-totality": check_charter_totality,
    "law-anchor": check_law_anchor,
    "reference": check_reference,
    "supersession": check_supersession,
    "charter-mark": check_charter_mark,
    "exception-in-law": check_exception_in_law,
    "collision": check_collision,
    "invariance": check_invariance,
    "door": check_door,
    "considered": check_considered,
    "owner-required": check_owner_required,
}


def run_checks(inputs: Inputs) -> List[Finding]:
    findings: List[Finding] = list(check_schema(inputs))
    clean = sanitized(inputs)
    for name, check in CHECKS.items():
        if name != "schema":
            findings.extend(check(clean))
    return findings


def judge(inputs: Inputs) -> Tuple[List[Finding], List[Finding], List[dict]]:
    """Split the findings into (new, known) and list the known-findings rows that no longer fire."""
    findings = run_checks(inputs)
    rows = {known_key(row): row for row in inputs.known if isinstance(row, dict) and row.get("check") in PARKABLE}
    fired = {f.key for f in findings}
    new = [f for f in findings if f.key not in rows]
    known = [f for f in findings if f.key in rows]
    stale = [row for key, row in rows.items() if key not in fired]
    return new, known, stale


def _at(finding: Finding) -> str:
    return f" [where: {finding.where}]" if finding.where else ""


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--root", type=Path, default=REPO)
    parser.add_argument(
        "--list-known",
        action="store_true",
        help="also print the findings that known-findings.yaml covers",
    )
    args = parser.parse_args(argv)
    try:
        inputs = read_inputs(args.root)
    except InputError as exc:
        print(f"check_rulings: {exc}", file=sys.stderr)
        return 2
    new, known, stale = judge(inputs)
    for finding in new:
        print(f"NEW   {finding.check}: {' '.join(finding.subjects)}{_at(finding)}: {finding.message}")
    for row in stale:
        print(
            f"STALE {row.get('check')}: {' '.join(_ids(row.get('subjects')))}: listed in known-findings.yaml and no longer fires; delete the row"
        )
    if args.list_known:
        for finding in known:
            print(
                f"known {finding.check}: {' '.join(finding.subjects)}: {finding.message}"
            )
    print(
        f"check_rulings: {len(inputs.rulings)} rulings, {len(inputs.laws)} laws, {len(live_cases(sanitized(inputs)))} live cases; {len(new)} new, {len(stale)} stale, {len(known)} known findings"
    )
    return 1 if new or stale else 0


if __name__ == "__main__":
    sys.exit(main())
