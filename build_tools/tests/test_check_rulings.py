"""Tests for build_tools/check_rulings.py: the ruling records, the law table and the charter agree.

Every check is exercised on a scratch tree that starts clean: one edit makes exactly the named
finding appear, so each test is a mutation of the data with its control (`test_base_is_clean`).
The real tree is then judged as it stands, with positive controls: the conflicts the 2026-11-21
audit verified by reading the rows must be the ones the checks report.
"""

from __future__ import annotations

import copy
import os
import sys
from pathlib import Path
from typing import Callable, Dict, List, Optional, Set, Tuple

import pytest
import yaml

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_rulings as mod
import ruling_evidence

REPO = Path(__file__).resolve().parents[2]

LAW_TEXT = (
    "# Laws\n\n### L1. Sites agree\n\nOne verdict at every site. Exception: R-C.\n"
)
CHARTER = (
    "## 4. Rulings\n\n| Ruling | Issue | Content |\n|---|---|---|\n"
    "| R-A | #1 | first; amended by R-C |\n"
    "| ~~R-B~~ | #2 | second; superseded by R-C |\n"
    "| R-C | #3 | third |\n"
)
RAN = "runs:1\n"
REFUSED = "refused:SPY0402"


def seed(rid: str, **extra: object) -> dict:
    record = {
        "id": rid,
        "seed": "audit",
        "kind": "language",
        "rule": f"what {rid} decided",
    }
    record.update(extra)
    return record


def case(site: str, verdict: str, **extra: object) -> dict:
    data = {
        "family": "eq",
        "coords": {"site": site, "operand": "ref"},
        "verdict": verdict,
    }
    data.update(extra)
    return data


def evidence(before: str, after: str, python: str = "no-twin") -> dict:
    block = {
        "program": "def main() -> None:\n    pass\n",
        "sharpy": before,
        "after": after,
        "python": python,
    }
    if python != "no-twin":
        block["twin"] = "pass\n"
    return block


def strict(rid: str = "R-D", **extra: object) -> dict:
    """A new ruling that meets the strict schema: an agent keeps a refusal that is already there."""
    record = {
        "id": rid,
        "date": "2026-11-22",
        "issue": "#9",
        "kind": "language",
        "rule": "a refused program stays refused",
        "options": [
            {"text": "keep the refusal", "chosen": True, "recommended": True},
            {"text": "admit it"},
        ],
        "basis": "follows",
        "laws": ["L1"],
        "decided_by": "agent",
        "door": "two-way",
        "breaks_running": False,
        "undo_breaks": False,
        "cases": [case("slice", "refuse", evidence=evidence(REFUSED, REFUSED))],
        "considered": [{"id": "R-A", "why": "R-A is the operator site; this is a slice"}],
    }
    record.update(extra)
    return record


def base() -> Dict[str, object]:
    return {
        "rulings": [
            seed("R-A", amended_by=["R-C"], cases=[case("operator", "refuse")]),
            seed("R-B", superseded_by=["R-C"], cases=[case("operator", "run")]),
            seed("R-C", supersedes=["R-B"], amends=["R-A"]),
        ],
        "laws": [
            {
                "id": "L1",
                "status": "written",
                "source": "docs/laws.md",
                "anchor": "### L1. Sites agree",
                "statement": "one verdict per site",
            }
        ],
        "families": {
            "eq": {
                "axes": {
                    "site": ["operator", "search", "slice"],
                    "operand": ["ref", "value"],
                }
            }
        },
        "policy": {
            "no_change": True,
            "reversible_refusal": True,
            "breaking_refusal": False,
            "admission_or_value_change": False,
            "kept_divergence": False,
        },
        "known": [],
        "charter": CHARTER,
        "law_text": LAW_TEXT,
    }


def write(root: Path, data: Dict[str, object]) -> Path:
    rulings = root / mod.RULINGS_DIR
    rulings.mkdir(parents=True, exist_ok=True)
    (root / mod.ROUNDS_DIR).mkdir(parents=True, exist_ok=True)
    (root / mod.ROUNDS_DIR / "round-x-batching.md").write_text(
        str(data["charter"]), encoding="utf-8"
    )
    (root / "docs" / "laws.md").write_text(str(data["law_text"]), encoding="utf-8")
    for name, key, value in (
        ("rulings.yaml", "rulings", data["rulings"]),
        ("laws.yaml", "laws", data["laws"]),
        ("axes.yaml", "families", data["families"]),
        ("policy.yaml", "agent_may_decide", data["policy"]),
        ("known-findings.yaml", "findings", data["known"]),
    ):
        (rulings / name).write_text(
            yaml.safe_dump({key: value}, sort_keys=False), encoding="utf-8"
        )
    return root


def findings(
    tmp_path: Path, edit: Optional[Callable[[Dict[str, object]], None]] = None
) -> List[mod.Finding]:
    data = base()
    if edit:
        edit(data)
    return mod.run_checks(mod.read_inputs(write(tmp_path, data)))


def names(found: List[mod.Finding]) -> Set[str]:
    return {f.check for f in found}


def keys(found: List[mod.Finding]) -> Set[Tuple[str, Tuple[str, ...]]]:
    return {f.key[:2] for f in found}


def rulings(data: Dict[str, object]) -> List[dict]:
    return data["rulings"]  # type: ignore[return-value]


# --- the control ----------------------------------------------------------------------------------


def test_base_is_clean(tmp_path):
    assert findings(tmp_path) == []


def test_a_strict_ruling_that_meets_the_schema_is_clean(tmp_path):
    assert findings(tmp_path, lambda d: rulings(d).append(strict())) == []


# --- schema ---------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    "drop, message",
    [
        ("date", "needs `date`"),
        ("issue", "needs `issue`"),
        ("options", "needs `options`"),
        ("basis", "`basis` must be one of"),
        ("laws", "needs `laws`"),
        ("decided_by", "`decided_by` must be"),
        ("cases", "needs at least one case"),
        ("rule", "`rule`"),
    ],
)
def test_a_strict_ruling_missing_a_field_is_refused(tmp_path, drop, message):
    def edit(data):
        record = strict()
        del record[drop]
        rulings(data).append(record)

    found = [f for f in findings(tmp_path, edit) if f.check == "schema"]
    assert any(message in f.message for f in found), found


def test_a_seed_record_is_not_held_to_the_strict_fields(tmp_path):
    # R-A in the base has no date, options, basis or evidence and is clean: the audit's records are
    # grandfathered, a new ruling is not.
    assert "schema" not in names(findings(tmp_path))


def test_exactly_one_option_is_chosen_and_one_recommended(tmp_path):
    def edit(data):
        record = strict()
        record["options"][1]["recommended"] = True
        rulings(data).append(record)

    found = findings(tmp_path, edit)
    assert any(
        "exactly one option must be `recommended: true`" in f.message for f in found
    )


def test_a_strict_case_needs_measured_evidence_and_the_decided_observation(tmp_path):
    def no_evidence(data):
        rulings(data).append(strict(cases=[case("slice", "refuse")]))

    assert any(
        "needs measured `evidence`" in f.message
        for f in findings(tmp_path, no_evidence)
    )

    def no_after(data):
        block = evidence(REFUSED, REFUSED)
        del block["after"]
        rulings(data).append(strict(cases=[case("slice", "refuse", evidence=block)]))

    assert any("needs `after`" in f.message for f in findings(tmp_path, no_after))


def test_an_observation_must_be_one_of_the_four_shapes(tmp_path):
    def edit(data):
        rulings(data).append(
            strict(
                cases=[case("slice", "refuse", evidence=evidence("prints 1", REFUSED))]
            )
        )

    assert any("not an observation" in f.message for f in findings(tmp_path, edit))


def test_duplicate_ids_and_bad_enums_are_schema_findings(tmp_path):
    def edit(data):
        rulings(data).append(seed("R-A"))
        rulings(data).append(seed("R-E", kind="vibes"))
        data["policy"]["anything"] = True

    messages = [f.message for f in findings(tmp_path, edit) if f.check == "schema"]
    assert any("duplicate id" in m for m in messages)
    assert any("`kind` must be one of" in m for m in messages)
    assert any("policy keys are" in m for m in messages)


# --- charter and law text -------------------------------------------------------------------------


def test_a_charter_row_without_a_record_fails(tmp_path):
    def edit(data):
        data["charter"] += "| R-Z | #4 | a ruling nobody recorded |\n"

    assert ("charter-totality", ("R-Z",)) in keys(findings(tmp_path, edit))


def test_a_seed_record_without_a_charter_row_fails_and_a_strict_one_does_not(tmp_path):
    assert ("charter-totality", ("R-E",)) in keys(
        findings(tmp_path, lambda d: rulings(d).append(seed("R-E")))
    )
    assert "charter-totality" not in names(
        findings(tmp_path, lambda d: rulings(d).append(strict()))
    )


def test_a_written_law_whose_anchor_is_gone_fails(tmp_path):
    def edit(data):
        data["law_text"] = LAW_TEXT.replace("### L1. Sites agree", "### L1. Renamed")

    assert ("law-anchor", ("L1",)) in keys(findings(tmp_path, edit))


def test_a_draft_law_is_not_held_to_an_anchor(tmp_path):
    def edit(data):
        data["laws"].append(
            {
                "id": "P-x",
                "status": "draft",
                "source": "docs/nowhere.md",
                "anchor": "x",
                "statement": "s",
            }
        )

    assert "law-anchor" not in names(findings(tmp_path, edit))


# --- references -----------------------------------------------------------------------------------


def test_unknown_family_axis_value_and_law_are_reference_findings(tmp_path):
    def edit(data):
        rulings(data).append(
            seed(
                "R-E",
                cases=[{"family": "nope", "coords": {"a": "b"}, "verdict": "run"}],
            )
        )
        rulings(data)[0]["cases"].append(
            {
                "family": "eq",
                "coords": {"site": "operator", "operand": "martian"},
                "verdict": "run",
            }
        )
        rulings(data)[0]["cases"].append(
            {"family": "eq", "coords": {"site": "operator"}, "verdict": "run"}
        )
        rulings(data).append(strict(laws=["L99"]))

    messages = [f.message for f in findings(tmp_path, edit) if f.check == "reference"]
    assert any("unknown family 'nope'" in m for m in messages)
    assert any("no value 'martian'" in m for m in messages)
    assert any("coordinate `operand` is missing" in m for m in messages)
    assert any("names neither a law nor a ruling: L99" in m for m in messages)


def test_a_strict_ruling_may_rest_on_an_earlier_ruling(tmp_path):
    assert "reference" not in names(
        findings(
            tmp_path,
            lambda d: rulings(d).append(strict(basis="precedent", laws=["R-A"])),
        )
    )


# --- supersession ---------------------------------------------------------------------------------


def test_one_sided_supersession_fails_in_both_directions(tmp_path):
    def forward_only(data):
        del rulings(data)[1]["superseded_by"]

    found = findings(tmp_path, forward_only)
    assert ("supersession", ("R-B", "R-C")) in keys(found)

    def back_only(data):
        del rulings(data)[2]["amends"]

    assert ("supersession", ("R-A", "R-C")) in keys(findings(tmp_path, back_only))


def test_a_superseded_ruling_needs_a_struck_charter_row(tmp_path):
    def edit(data):
        data["charter"] = CHARTER.replace("~~R-B~~", "R-B")

    assert ("charter-mark", ("R-B",)) in keys(findings(tmp_path, edit))


def test_a_struck_charter_row_needs_a_successor_in_the_record(tmp_path):
    def edit(data):
        data["charter"] = CHARTER.replace("| R-C |", "| ~~R-C~~ |")

    assert ("charter-mark", ("R-C",)) in keys(findings(tmp_path, edit))


def test_an_amended_rulings_row_must_name_the_amending_ruling(tmp_path):
    def edit(data):
        data["charter"] = CHARTER.replace("first; amended by R-C", "first")

    assert ("charter-mark", ("R-A", "R-C")) in keys(findings(tmp_path, edit))


def test_naming_a_longer_id_is_not_naming_the_ruling(tmp_path):
    # R-CA is another ruling; the row must name R-C itself.
    def edit(data):
        data["charter"] = CHARTER.replace("first; amended by R-C", "first; see R-CA")

    assert ("charter-mark", ("R-A", "R-C")) in keys(findings(tmp_path, edit))


# --- exceptions -----------------------------------------------------------------------------------


def test_an_exception_the_law_text_does_not_name_fails(tmp_path):
    def edit(data):
        rulings(data)[2]["cases"] = [
            case(
                "search",
                "run",
                exception_to="L1",
                differs_because="python documents it",
            )
        ]
        data["law_text"] = LAW_TEXT.replace(" Exception: R-C.", "")

    assert ("exception-in-law", ("L1", "R-C")) in keys(findings(tmp_path, edit))


def test_an_exception_the_law_text_names_is_clean(tmp_path):
    def edit(data):
        rulings(data)[2]["cases"] = [
            case(
                "search",
                "run",
                exception_to="L1",
                differs_because="python documents it",
            )
        ]

    assert "exception-in-law" not in names(findings(tmp_path, edit))


# --- collision ------------------------------------------------------------------------------------


def test_two_live_rulings_with_one_case_and_two_verdicts_collide(tmp_path):
    def edit(data):
        rulings(data)[2]["cases"] = [case("operator", "run")]
        del rulings(data)[2]["amends"], rulings(data)[0]["amended_by"]
        data["charter"] = CHARTER.replace("first; amended by R-C", "first")

    assert keys(findings(tmp_path, edit)) == {("collision", ("R-A", "R-C"))}


def test_a_superseded_ruling_does_not_collide(tmp_path):
    # R-B (superseded) says run where R-A says refuse, in the base: no finding.
    assert "collision" not in names(findings(tmp_path))


def test_a_ruling_that_amends_the_other_does_not_collide(tmp_path):
    def edit(data):
        rulings(data)[2]["cases"] = [case("operator", "run")]

    assert "collision" not in names(findings(tmp_path, edit))


def test_an_open_verdict_collides_with_a_decided_one(tmp_path):
    def edit(data):
        rulings(data).append(seed("R-E", cases=[case("operator", "open")]))
        data["charter"] += "| R-E | #5 | parks it |\n"

    assert ("collision", ("R-A", "R-E")) in keys(findings(tmp_path, edit))


def test_a_laws_own_case_collides_with_a_ruling(tmp_path):
    def edit(data):
        data["laws"][0]["cases"] = [case("operator", "run")]

    assert ("collision", ("L1", "R-A")) in keys(findings(tmp_path, edit))


# --- invariance -----------------------------------------------------------------------------------


def invariant(data):
    data["laws"][0]["invariants"] = [{"family": "eq", "along": ["site"]}]


def test_an_invariant_axis_with_two_verdicts_fails(tmp_path):
    def edit(data):
        invariant(data)
        rulings(data)[2]["cases"] = [case("search", "run")]

    assert ("invariance", ("L1", "R-A", "R-C")) in keys(findings(tmp_path, edit))


def test_the_same_two_cases_without_the_invariant_are_clean(tmp_path):
    def edit(data):
        rulings(data)[2]["cases"] = [case("search", "run")]

    assert findings(tmp_path, edit) == []


def test_a_stated_exception_to_the_invariant_is_clean(tmp_path):
    def edit(data):
        invariant(data)
        rulings(data)[2]["cases"] = [
            case(
                "search",
                "run",
                exception_to="L1",
                differs_because="python documents it",
            )
        ]

    assert findings(tmp_path, edit) == []


def test_cases_that_differ_on_another_axis_are_not_compared(tmp_path):
    def edit(data):
        invariant(data)
        rulings(data)[2]["cases"] = [
            {
                "family": "eq",
                "coords": {"site": "search", "operand": "value"},
                "verdict": "run",
            }
        ]

    assert findings(tmp_path, edit) == []


def test_a_family_with_answers_is_compared_by_answer(tmp_path):
    def edit(data):
        invariant(data)
        data["families"]["eq"]["answers"] = ["identity", "value"]
        rulings(data)[0]["cases"] = [case("operator", "run", answer="identity")]
        rulings(data)[1]["cases"] = [case("operator", "run", answer="identity")]
        rulings(data)[2]["cases"] = [case("search", "run", answer="value")]

    assert ("invariance", ("L1", "R-A", "R-C")) in keys(findings(tmp_path, edit))


def test_a_ruling_answers_what_stood_when_it_was_made_not_what_came_after(tmp_path):
    # R-D (2026-11-22) and a later R-E share a family. R-E must answer R-D; R-D cannot answer R-E.
    def later(considered):
        return strict("R-E", date="2026-11-23", cases=[case("search", "refuse", evidence=evidence(REFUSED, REFUSED))], considered=considered)

    def edit(data, considered):
        rulings(data).extend([strict(), later(considered)])

    both = [{"id": "R-A", "why": "the operator site"}, {"id": "R-D", "why": "R-D is the slice"}]
    assert findings(tmp_path / "ok", lambda d: edit(d, both)) == []
    # `subjects` is (the ruling that must answer, the one it did not); a finding's key sorts them.
    assert [f.subjects for f in findings(tmp_path / "missing", lambda d: edit(d, both[:1]))] == [("R-E", "R-D")]

    # Made on the same day, each must answer the other.
    def same_day(data):
        rulings(data).extend([strict(), later(both)])
        rulings(data)[-1]["date"] = "2026-11-22"

    assert [f.subjects for f in findings(tmp_path / "same", same_day)] == [("R-D", "R-E")]


# --- the door, and who may decide -----------------------------------------------------------------


def test_a_door_that_is_not_what_the_evidence_gives_fails(tmp_path):
    def edit(data):
        rulings(data).append(strict(door="one-way"))

    found = [f for f in findings(tmp_path, edit) if f.check == "door"]
    assert len(found) == 1 and "the evidence gives 'two-way'" in found[0].message


def test_a_ruling_that_refuses_one_case_and_admits_another_does_both(tmp_path):
    # One case refuses a running program (breaks it; relaxable), another admits a refused one
    # (breaks nothing; cannot be taken back). The ruling breaks running programs AND is one-way.
    both = dict(
        decided_by="owner",
        cases=[
            case("slice", "refuse", evidence=evidence(RAN, REFUSED)),
            case("search", "run", evidence=evidence(REFUSED, RAN)),
        ],
    )
    for order in (0, 1):
        cases = both["cases"][::-1] if order else both["cases"]

        def edit(data, flags=None, cases=cases):
            rulings(data).append(strict(**{**both, "cases": cases, **(flags or {})}))

        truth = dict(door="one-way", breaks_running=True, undo_breaks=True)
        assert findings(tmp_path / f"ok{order}", lambda d: edit(d, truth)) == [], order
        # Either single case's own door is not the ruling's.
        for n, wrong in enumerate((dict(door="one-way", breaks_running=False, undo_breaks=True), dict(door="two-way", breaks_running=True, undo_breaks=False))):
            found = findings(tmp_path / f"bad{order}{n}", lambda d, w=wrong: edit(d, w))
            assert found and {f.check for f in found} == {"door"}, (order, wrong)


def test_an_admission_is_one_way_and_reserved_for_the_owner(tmp_path):
    admit = dict(
        door="one-way",
        breaks_running=False,
        undo_breaks=True,
        cases=[case("slice", "run", evidence=evidence(REFUSED, RAN))],
    )
    assert ("owner-required", ("R-D",)) in keys(
        findings(tmp_path, lambda d: rulings(d).append(strict(**admit)))
    )
    assert (
        findings(
            tmp_path, lambda d: rulings(d).append(strict(decided_by="owner", **admit))
        )
        == []
    )


def test_the_policy_file_decides_what_an_agent_may_take(tmp_path):
    admit = dict(
        door="one-way",
        breaks_running=False,
        undo_breaks=True,
        cases=[case("slice", "run", evidence=evidence(REFUSED, RAN))],
    )

    def edit(data):
        data["policy"]["admission_or_value_change"] = True
        rulings(data).append(strict(**admit))

    assert findings(tmp_path, edit) == []


def test_refusing_a_running_program_is_reserved_for_the_owner(tmp_path):
    breaking = dict(
        door="two-way",
        breaks_running=True,
        undo_breaks=False,
        cases=[case("slice", "refuse", evidence=evidence(RAN, REFUSED))],
    )
    found = findings(tmp_path, lambda d: rulings(d).append(strict(**breaking)))
    assert (
        keys(found) == {("owner-required", ("R-D",))}
        and "breaking_refusal" in found[0].message
    )


def test_keeping_a_result_python_does_not_give_is_one_way(tmp_path):
    kept = dict(
        decided_by="owner",
        door="two-way",
        cases=[
            case("slice", "run", evidence=evidence(RAN, RAN, "refused:SyntaxError"))
        ],
    )
    found = [
        f
        for f in findings(tmp_path, lambda d: rulings(d).append(strict(**kept)))
        if f.check == "door"
    ]
    assert any(
        "`door` is 'two-way'; the evidence gives 'one-way'" in f.message for f in found
    )


# --- considered -----------------------------------------------------------------------------------


def test_a_new_ruling_must_answer_every_ruling_in_its_family(tmp_path):
    def edit(data):
        rulings(data).append(strict(laws=["R-C"], basis="precedent", considered=[]))

    assert ("considered", ("R-A", "R-D")) in keys(findings(tmp_path, edit))


def test_answering_in_considered_or_resting_on_it_in_laws_is_clean(tmp_path):
    def answered(data):
        rulings(data).append(
            strict(
                laws=["R-C"],
                basis="precedent",
                considered=[
                    {"id": "R-A", "why": "R-A is the operator site; this is a slice"}
                ],
            )
        )

    assert findings(tmp_path, answered) == []
    assert (
        findings(
            tmp_path,
            lambda d: rulings(d).append(strict(laws=["R-A"], basis="precedent", considered=[])),
        )
        == []
    )


def test_an_empty_why_is_not_an_answer(tmp_path):
    def edit(data):
        rulings(data).append(
            strict(
                laws=["R-C"], basis="precedent", considered=[{"id": "R-A", "why": ""}]
            )
        )

    assert ("considered", ("R-A", "R-D")) in keys(findings(tmp_path, edit))


# --- the ratchet ----------------------------------------------------------------------------------


def judged(tmp_path: Path, edit: Callable[[Dict[str, object]], None]):
    data = base()
    edit(data)
    return mod.judge(mod.read_inputs(write(tmp_path, data)))


def collide(data):
    """R-C gives the operator site a second verdict, with no relation to R-A recorded."""
    rulings(data)[2]["cases"] = [case("operator", "run")]
    del rulings(data)[2]["amends"], rulings(data)[0]["amended_by"]
    data["charter"] = CHARTER.replace("first; amended by R-C", "first")


WHERE = "eq operand=ref,site=operator"


def test_a_listed_finding_is_known_and_an_unlisted_one_is_new(tmp_path):
    def edit(data):
        collide(data)
        data["charter"] += "| R-Z | #4 | unrecorded |\n"
        data["known"].append({"check": "collision", "subjects": ["R-A", "R-C"], "where": WHERE, "ref": "audit 6.1"})

    new, known, stale = judged(tmp_path, edit)
    assert [f.key for f in new] == [("charter-totality", ("R-Z",), "")]
    assert [f.key for f in known] == [("collision", ("R-A", "R-C"), WHERE)]
    assert stale == []


def test_a_row_whose_finding_is_gone_is_stale(tmp_path):
    def edit(data):
        data["known"].append({"check": "collision", "subjects": ["R-A", "R-C"], "where": WHERE, "ref": "audit 6.1"})

    new, known, stale = judged(tmp_path, edit)
    assert new == [] and known == [] and len(stale) == 1


def test_a_row_covers_one_case_and_not_another_between_the_same_rulings(tmp_path):
    # Two collisions between R-A and R-C, one row: the second collision is new, not hidden.
    def edit(data):
        collide(data)
        rulings(data)[0]["cases"].append(case("search", "refuse"))
        rulings(data)[2]["cases"].append(case("search", "run"))
        data["known"].append({"check": "collision", "subjects": ["R-A", "R-C"], "where": WHERE, "ref": "audit 6.1"})

    new, known, stale = judged(tmp_path, edit)
    assert [f.key for f in new] == [("collision", ("R-A", "R-C"), "eq operand=ref,site=search")]
    assert len(known) == 1 and stale == []


def test_a_row_without_the_case_does_not_cover_a_collision(tmp_path):
    def edit(data):
        collide(data)
        data["known"].append({"check": "collision", "subjects": ["R-A", "R-C"], "ref": "audit 6.1"})

    new, known, stale = judged(tmp_path, edit)
    assert [f.check for f in new] == ["collision"] and known == [] and len(stale) == 1


@pytest.mark.parametrize("check", ["schema", "reference", "door", "considered", "owner-required", "charter-totality", "vibes"])
def test_only_findings_that_wait_for_the_owner_can_be_listed(tmp_path, check):
    # A malformed record is fixed, not parked: a `schema: [R-D]` row would hide every later schema
    # finding about R-D behind one key.
    def edit(data):
        record = strict()
        del record["issue"], record["basis"]
        rulings(data).append(record)
        data["known"].append({"check": check, "subjects": ["R-D"], "ref": "x"})

    new, known, _ = judged(tmp_path, edit)
    assert known == []
    assert any(f.check == "schema" and "cannot wait in known-findings.yaml" in f.message for f in new)
    assert sum(1 for f in new if f.check == "schema" and f.subjects == ("R-D",)) >= 2


def test_two_identical_known_rows_are_refused(tmp_path):
    def edit(data):
        collide(data)
        row = {"check": "collision", "subjects": ["R-A", "R-C"], "where": WHERE, "ref": "audit 6.1"}
        data["known"] += [row, dict(row, subjects=["R-C", "R-A"])]

    new, _, _ = judged(tmp_path, edit)
    assert any(f.check == "schema" and "two rows for collision" in f.message for f in new)


def test_main_exit_codes(tmp_path, capsys):
    write(tmp_path, base())
    assert mod.main(["--root", str(tmp_path)]) == 0
    data = base()
    data["charter"] += "| R-Z | #4 | unrecorded |\n"
    write(tmp_path, data)
    assert mod.main(["--root", str(tmp_path)]) == 1
    assert "NEW   charter-totality: R-Z" in capsys.readouterr().out
    (tmp_path / mod.RULINGS_DIR / "rulings.yaml").unlink()
    assert mod.main(["--root", str(tmp_path)]) == 2


# --- findings of the independent review @ 993681ab3 --------------------------------------------


def test_an_exception_named_only_under_another_laws_heading_fails(tmp_path):
    # The source names R-C, but in L2's section; L1's own text does not.
    def edit(data):
        rulings(data)[2]["cases"] = [case("search", "run", exception_to="L1", differs_because="python documents it")]
        data["law_text"] = "# Laws\n\n### L1. Sites agree\n\nOne verdict.\n\n### L2. Other\n\nException: R-C.\n"

    assert ("exception-in-law", ("L1", "R-C")) in keys(findings(tmp_path, edit))


def test_law_section_stops_at_the_next_law():
    text = "intro R-A\n\n### L1. One\n\nbody R-B\n\n#### detail\n\nR-C\n\n### L2. Two\n\nR-D\n\n## Higher\n"
    section = mod.law_section(text, "### L1. One")
    assert "R-B" in section and "R-C" in section and "R-A" not in section and "R-D" not in section
    assert mod.law_section(text, "absent") is None


def test_a_law_that_is_not_a_heading_is_its_line_and_the_indented_lines_after_it():
    # A neighbour is never read as part of the law: the next list item, an adjacent bold
    # paragraph with no blank line between, the next table row (second review @ 87da77896).
    items = "1. **First.** R-A\n   wrapped R-B\n2. **Second.** R-C\n"
    assert mod.law_section(items, "1. **First.**") == "1. **First.** R-A\n   wrapped R-B\n"
    bold = "**L1:** R-A\n**L2:** R-B\n"
    assert mod.law_section(bold, "**L1:**") == "**L1:** R-A\n"
    table = "| L1 | text R-A |\n| L2 | text R-B |\n"
    assert mod.law_section(table, "| L1 |") == "| L1 | text R-A |\n"
    bullets = "- **Wrap** R-A\n\n  not this R-B\n"
    assert mod.law_section(bullets, "**Wrap**") == "- **Wrap** R-A\n"


def test_an_anchor_that_occurs_twice_fails(tmp_path):
    # exception-in-law reads the law from its anchor; an earlier mention must not stand for it.
    def edit(data):
        data["law_text"] = "See ### L1. Sites agree, and R-C.\n\n" + LAW_TEXT

    found = [f for f in findings(tmp_path, edit) if f.check == "law-anchor"]
    assert len(found) == 1 and "occurs 2 times" in found[0].message


def test_an_exception_to_a_draft_law_must_be_in_its_against_list(tmp_path):
    def edit(listed):
        def apply(data):
            draft = {"id": "P-x", "status": "draft", "source": "docs/laws.md", "anchor": "x", "statement": "s"}
            if listed:
                draft["against"] = ["R-C"]
            data["laws"].append(draft)
            # docs/laws.md names R-C, as the audit document names nearly every ruling: not enough.
            rulings(data)[2]["cases"] = [case("search", "run", exception_to="P-x", differs_because="why")]

        return apply

    assert ("exception-in-law", ("P-x", "R-C")) in keys(findings(tmp_path, edit(False)))
    assert "exception-in-law" not in names(findings(tmp_path, edit(True)))


@pytest.mark.parametrize(
    "bad",
    [
        "a string where a record belongs",
        {"id": ["R-Q"], "seed": "x", "kind": "language", "rule": "r"},
        {"id": 7, "seed": "x", "kind": "language", "rule": "r"},
        {"id": "R-Q", "seed": "x", "kind": "language", "rule": "r", "cases": "none"},
        {"id": "R-Q", "seed": "x", "kind": "language", "rule": "r", "cases": ["text", {"family": "eq", "coords": "x", "verdict": "run"}]},
        {"id": "R-Q", "seed": "x", "kind": "language", "rule": "r", "superseded_by": "R-C"},
    ],
)
def test_a_malformed_record_is_a_schema_finding_and_never_a_traceback(tmp_path, bad):
    def edit(data):
        rulings(data).append(bad)
        data["charter"] += "| R-Q | #7 | q |\n"

    found = findings(tmp_path, edit)
    assert "schema" in names(found)


def test_malformed_laws_families_and_known_rows_do_not_crash(tmp_path, capsys):
    def edit(data):
        data["laws"] += ["a string", {"id": "L3", "status": "written", "source": "docs/laws.md", "anchor": "# Laws", "statement": "s", "cases": "x", "invariants": "y"}]
        data["families"]["broken"] = {"axes": "not a mapping"}
        data["families"]["text"] = "not a mapping"
        data["known"].append("a string")

    data = base()
    edit(data)
    write(tmp_path, data)
    assert mod.main(["--root", str(tmp_path)]) == 1
    out = capsys.readouterr().out
    assert "Traceback" not in out and out.count("NEW   schema") >= 4


def test_one_ruling_giving_one_case_two_verdicts_collides_with_itself(tmp_path):
    def edit(data):
        rulings(data)[0]["cases"] = [case("search", "run"), case("search", "refuse")]

    assert ("collision", ("R-A", "R-A")) in keys(findings(tmp_path, edit))


def test_every_case_is_judged_against_the_policy_not_only_the_widest(tmp_path):
    # Two cases that tie on both facts: nothing changes, and a refusal with another code. With
    # refusals reserved, the ruling is reserved whichever case comes first.
    for order in (0, 1):
        cases = [case("slice", "refuse", evidence=evidence(REFUSED, REFUSED)), case("search", "refuse", evidence=evidence(REFUSED, "refused:SPY0611"))]

        def edit(data, cases=cases[::-1] if order else cases):
            data["policy"]["reversible_refusal"] = False
            rulings(data).append(strict(cases=cases))

        found = findings(tmp_path / str(order), edit)
        assert keys(found) == {("owner-required", ("R-D",))}, order
        assert "`reversible_refusal`" in found[0].message


def test_the_same_refusal_is_no_change_and_another_code_is_a_reversible_refusal():
    same = ruling_evidence.derive_door(REFUSED, REFUSED)
    other = ruling_evidence.derive_door(REFUSED, "refused:SPY0611")
    assert mod.policy_class(REFUSED, REFUSED, same) == "no_change"
    assert mod.policy_class(REFUSED, "refused:SPY0611", other) == "reversible_refusal"
    assert mod.policy_class(RAN, REFUSED, ruling_evidence.derive_door(RAN, REFUSED)) == "breaking_refusal"


def test_only_section_4_rows_are_rulings_and_a_second_row_for_one_id_fails(tmp_path):
    def outside(data):
        data["charter"] = "## 1. Order\n\n| R-Z | not a ruling row |\n\n" + CHARTER + "\n## 5. Notes\n\n| R-Y | #9 | nor this |\n"

    assert findings(tmp_path / "a", outside) == []

    def twice(data):
        data["charter"] += "| R-A | #1 | first, again; amended by R-C |\n"

    found = findings(tmp_path / "b", twice)
    assert any(f.check == "schema" and f.subjects == ("R-A",) and "more than one section-4 row" in f.message for f in found)


def test_an_internal_compiler_error_is_evidence_but_never_a_decision(tmp_path):
    crash = "crashes:SPY0908"

    def measured(data):
        rulings(data).append(strict(cases=[case("slice", "refuse", evidence=evidence(crash, REFUSED))]))

    # The program does not compile today and is ruled refused: nothing running breaks.
    assert findings(tmp_path / "a", measured) == []

    def decided(data):
        rulings(data).append(strict(cases=[case("slice", "refuse", evidence=evidence(REFUSED, crash))]))

    assert any("needs `after`" in f.message for f in findings(tmp_path / "b", decided))


# --- the real tree --------------------------------------------------------------------------------


@pytest.fixture(scope="module")
def real():
    return mod.read_inputs(REPO)


def test_the_real_tree_has_no_new_and_no_stale_finding(real):
    new, _, stale = mod.judge(real)
    assert [f"{f.check}: {' '.join(f.subjects)}: {f.message}" for f in new] == []
    assert stale == []


def test_every_charter_ruling_has_a_record(real):
    # Every charter row has a record, and every seed record a charter row. A new (strict) ruling
    # may be recorded before its row is written, so the two sets need not be equal.
    seeds = {r["id"] for r in real.rulings if "seed" in r}
    assert len(seeds) >= 460 and seeds <= set(real.charter) <= {r["id"] for r in real.rulings}


def test_the_written_laws_are_all_anchored_in_their_sources(real):
    written = [law for law in real.laws if law["status"] == "written"]
    assert len(written) >= 20
    assert list(mod.check_law_anchor(real)) == []


# Positive controls: what the audit verified by reading the rows is what the checks report. If a
# check stopped firing, the ratchet's stale row would say so; these name the expectation outright.
AUDITED = [
    ("charter-mark", ("R-CN", "R-GV")),  # 6.5: amended, the earlier row does not say so
    ("charter-mark", ("R-DX", "R-ES")),
    ("charter-mark", ("R-LK", "R-MB")),
]

# The audit's conflicts that the owner ruled on 2026-11-24 (R-QS ... R-RK). Each fired before those
# records existed (they were the positive controls above); none may fire now.
RULED = [
    ("collision", ("R-GV", "R-LR")),  # C1 -> R-QS
    ("collision", ("R-HH", "R-HT")),  # C2 -> R-QT
    ("invariance", ("P-uniform-verdict-across-forms", "R-PI", "R-PL")),  # U2 -> R-QX
    ("invariance", ("P-uniform-verdict-across-forms", "R-CH", "R-IM")),  # U3 -> R-QY
    ("exception-in-law", ("N5", "R-MH")),  # L1 -> R-QS; N5's text is corrected
    ("charter-mark", ("R-BK", "R-GV")),
    ("charter-mark", ("R-BL", "R-GV")),
]


@pytest.mark.parametrize("key", AUDITED, ids=lambda k: f"{k[0]}:{'+'.join(k[1])}")
def test_the_audited_conflicts_are_what_the_checks_report(real, key):
    assert key in keys(mod.run_checks(real))


@pytest.mark.parametrize("key", RULED, ids=lambda k: f"{k[0]}:{'+'.join(k[1])}")
def test_a_ruled_conflict_no_longer_fires(real, key):
    assert key not in keys(mod.run_checks(real))


def _without(real, rid):
    """The real records as they would be had ruling `rid` not been recorded."""
    rulings_ = [copy.deepcopy(r) for r in real.rulings if r["id"] != rid]
    for record in rulings_:
        for field in ("superseded_by", "amended_by"):
            if rid in record.get(field, []):
                record[field] = [x for x in record[field] if x != rid]
                if not record[field]:
                    del record[field]
    return real._replace(rulings=rulings_)


def test_the_walrus_ruling_is_what_settles_the_let_collision(real):
    # Control for RULED: take R-QS away and R-LR is live again, so C1 fires as the audit found it.
    assert ("collision", ("R-GV", "R-LR")) in keys(mod.run_checks(_without(real, "R-QS")))


def test_the_enum_law_holds_both_backings_to_one_verdict(real):
    # L-enum-own-type says the backing must not matter. Give the string backing another verdict
    # for one cell and the law reports it, naming the int-backed ruling it now disagrees with.
    assert not [f for f in mod.run_checks(real) if f.check == "invariance" and "L-enum-own-type" in f.subjects]
    rulings_ = copy.deepcopy(real.rulings)
    cell = {"backing": "string", "operator": "order", "operands": "one-type"}
    (flipped,) = [c for c in next(r for r in rulings_ if r["id"] == "R-QW")["cases"] if c["coords"] == cell]
    flipped["verdict"] = "refuse"
    found = [f for f in mod.run_checks(real._replace(rulings=rulings_)) if f.check == "invariance"]
    assert [f.subjects for f in found] == [("R-OW", "R-QW", "L-enum-own-type")]


def test_every_ruling_of_2026_11_24_is_the_owners_and_has_a_charter_row(real):
    new = [r for r in real.rulings if "seed" not in r]
    assert [r["id"] for r in new] == ["R-QS", "R-QT", "R-QU", "R-QV", "R-QW", "R-QX", "R-QY", "R-QZ", "R-RA", "R-RB", "R-RC", "R-RD", "R-RE", "R-RF", "R-RG", "R-RH", "R-RI", "R-RJ", "R-RK"]
    for record in new:
        assert record["decided_by"] == "owner" and record["id"] in real.charter, record["id"]
        assert not real.charter[record["id"]][0], record["id"]  # not struck


def test_properly_marked_amendments_are_not_reported(real):
    # R-ON's row says "Amended at call 46 by R-QG"; R-MJ's is narrowed by R-MH and says so.
    found = keys(mod.run_checks(real))
    assert ("charter-mark", ("R-ON", "R-QG")) not in found
    assert ("charter-mark", ("R-MH", "R-MJ")) not in found
    records = {r["id"]: r for r in real.rulings}
    assert records["R-ON"]["amended_by"] == ["R-QG"] and records["R-MJ"][
        "amended_by"
    ] == ["R-MH"]


def test_every_known_row_points_at_a_file_that_exists(real):
    for row in real.known:
        assert (REPO / str(row["ref"]).split(" ")[0]).is_file(), row


def test_the_readme_template_passes_the_checks_it_documents(real):
    # The template in docs/process/rulings/README.md, added to the real records. R-QS gets the
    # matching `amended_by`, as the template's `amends` requires; nothing else is adjusted.
    text = (REPO / mod.RULINGS_DIR / "README.md").read_text(encoding="utf-8")
    block = text.split("```yaml\n", 1)[1].split("```", 1)[0]
    (template,) = yaml.safe_load(block)
    assert "seed" not in template
    rulings_ = copy.deepcopy(real.rulings)
    next(r for r in rulings_ if r["id"] == "R-QS")["amended_by"] = [template["id"]]
    new, _, _ = mod.judge(real._replace(rulings=rulings_ + [template]))
    # The one thing the template cannot have is a charter row naming it on R-QS.
    assert [f.key[:2] for f in new] == [("charter-mark", ("R-QS", template["id"]))]
