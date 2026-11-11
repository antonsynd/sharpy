"""check_plan_refs caps (R-LW): plan body <= 20 KB before its summaries; <= 2 verify passes."""

import re
from pathlib import Path

import pytest

from build_tools import check_plan_refs as cpr

REPO = Path(__file__).resolve().parent.parent.parent
PLANS = REPO / "docs/process/plans"

STAMP = (
    "<!-- Verified by /verify-plan --adequacy (Adequacy-only: Dimensions 4-design, 6, 7; "
    "check_plan_refs clean) on 2026-11-12 @ abcdef123 -->\n"
    "<!-- Verification result: PASS -->\n<!-- Adequacy: CLASS -->\n"
    "<!-- Scope: ONE-GROUP · Generator: not-required -->\n\n"
)
BODY = (
    "# P99 — sample\n\n## Scope\n\n- **Tier:** light\n\n## Acceptance\n\n- one bullet\n"
)
SUMMARY = "\n## Verification Summary\n\n**Result:** PASS\n\n### Adequacy\n- fine\n"
REVISED = (
    "\n## Revision log\n\n"
    "- @ 1111111 — revised after the 1st pass (NEEDS REVISION, Dimension 6): matrix closed.\n"
)
REVISED_TWICE = (
    REVISED
    + "- @ 2222222 — revised after the 2nd pass (NEEDS REVISION, Dimension 7): guard named.\n"
)


def _plan(body=BODY, log="", summary=SUMMARY, stamp=STAMP) -> str:
    return stamp + body + log + summary


def test_compliant_plan_is_green():
    text = _plan(log=REVISED)
    assert cpr.verify_passes(text) == 2
    assert cpr.body_bytes(text) == len(
        (BODY + REVISED + "\n").encode()
    )  # stamp and summary excluded
    assert cpr.cap_violations("plan-new001.md", text) == []


def test_third_pass_is_red():
    # mutation record: a third pass appended to a compliant plan -> red
    text = _plan(log=REVISED_TWICE)
    assert cpr.verify_passes(text) == 3
    (msg,) = cpr.cap_violations("plan-new001.md", text)
    assert "3 verify passes" in msg and "Extra verify pass N: owner" in msg


def test_third_pass_spelled_as_pass_heading_is_red():
    text = _plan(
        summary=SUMMARY
        + "\n### Pass 2\n\n**Result:** NEEDS REVISION\n\n### Pass 3\n\n**Result:** PASS\n"
    )
    assert cpr.verify_passes(text) == 3
    assert cpr.cap_violations("plan-new001.md", text)


def test_owner_ruling_line_allows_exactly_its_pass():
    text = _plan(
        log=REVISED_TWICE + '\nExtra verify pass 3: owner 2026-11-12 "one more pass"\n'
    )
    assert cpr.cap_violations("plan-new001.md", text) == []
    # a ruling for a later pass does not cover pass 3
    text = _plan(
        log=REVISED_TWICE + '\nExtra verify pass 4: owner 2026-11-12 "later"\n'
    )
    assert cpr.cap_violations("plan-new001.md", text)
    # a ruling line without the owner's words is not a ruling
    text = _plan(log=REVISED_TWICE + "\nExtra verify pass 3: owner 2026-11-12\n")
    assert cpr.cap_violations("plan-new001.md", text)


def test_body_over_cap_is_red_and_summary_bytes_are_not_body():
    filler = "x" * (
        cpr.BODY_CAP_BYTES - len(BODY.encode()) - 1
    )  # + the newline before the summary
    at_cap = _plan(body=BODY + filler)
    assert cpr.body_bytes(at_cap) == cpr.BODY_CAP_BYTES
    assert cpr.cap_violations("plan-new001.md", at_cap) == []
    (msg,) = cpr.cap_violations("plan-new001.md", _plan(body=BODY + filler + "y"))
    assert "20,481 bytes" in msg
    # the same byte in a kept summary is not body
    assert (
        cpr.cap_violations(
            "plan-new001.md", _plan(body=BODY + filler, summary=SUMMARY + "y")
        )
        == []
    )


def test_new_plan_gets_no_exemption():
    assert "plan-new001.md" not in cpr.GRANDFATHERED
    assert all(name.startswith("plan-") for name in cpr.GRANDFATHERED)


def test_grandfathered_plan_may_not_grow(monkeypatch):
    big = _plan(body=BODY + "x" * 30_000, log=REVISED_TWICE)
    monkeypatch.setitem(cpr.GRANDFATHERED, "plan-old001.md", (cpr.body_bytes(big), 3))
    assert cpr.cap_violations("plan-old001.md", big) == []
    # mutation record: one more body byte -> red
    (msg,) = cpr.cap_violations("plan-old001.md", big.replace("# P99", "# P999"))
    assert "grandfathered" in msg and "may not grow" in msg
    # one more pass -> red
    fourth = big.replace(SUMMARY, SUMMARY + "\n### Pass 4\n")
    assert any(
        "4 verify passes" in m for m in cpr.cap_violations("plan-old001.md", fourth)
    )


def test_grandfathered_row_goes_stale_when_compliant(monkeypatch):
    monkeypatch.setitem(cpr.GRANDFATHERED, "plan-old001.md", (40_000, 3))
    (msg,) = cpr.cap_violations("plan-old001.md", _plan())
    assert "delete its GRANDFATHERED row" in msg


def test_grandfathered_real_plan_copy_growing_is_red(monkeypatch):
    # mutation record on a copy of a real plan, frozen at its own measure (not the live record,
    # which moves when the plan legitimately changes below its stop-lines): one more body byte -> red
    text = (PLANS / "plan-638e8b.md").read_text(encoding="utf-8")
    monkeypatch.setitem(
        cpr.GRANDFATHERED,
        "plan-copy001.md",
        (cpr.body_bytes(text), cpr.verify_passes(text)),
    )
    assert cpr.cap_violations("plan-copy001.md", text) == []
    grown = text.replace("## Acceptance\n", "## Acceptance\nx\n", 1)
    assert cpr.body_bytes(grown) == cpr.body_bytes(text) + 2
    assert any(
        "may not grow" in m for m in cpr.cap_violations("plan-copy001.md", grown)
    )
    # text added below the checklist heading is not body, so it does not trip the frozen size
    below = text.replace(
        "## Implementation Checklist\n", "## Implementation Checklist\nx\n", 1
    )
    assert cpr.cap_violations("plan-copy001.md", below) == []


@pytest.mark.parametrize(
    "name, passes",
    [
        ("plan-5cfc2b.md", 1),  # one stamp, no revision
        ("plan-1faae3.md", 1),
        ("plan-638e8b.md", 3),  # revised after the 1st and 2nd pass; CONVERGED stamp
        ("plan-21d2f5.md", 4),  # `pass N:` log entries; "CONVERGED (pass 4"
        ("plan-eaaed2.md", 3),  # `### Pass 3`
        ("plan-88736c.md", 5),  # `### Pass 5`
        ("plan-460cbf.md", 6),  # `## Verification Summary — round 6`
        ("plan-ed89b6.md", 10),  # nine NEEDS REVISION records + the current stamp
        ("plan-16cca3.md", 2),  # `Verified by /verify-plan (round 2`
    ],
)
def test_pass_count_on_committed_plans(name, passes):
    """Positive control for the counter against every spelling the committed plans use."""
    assert cpr.verify_passes((PLANS / name).read_text(encoding="utf-8")) == passes


def test_repository_plans_within_caps():
    """The guard itself: every committed plan is within the caps or its frozen grandfathered values."""
    problems = cpr.check_all_caps(REPO)
    assert problems == [], "\n".join(problems)
    assert (
        len(list(PLANS.glob("plan-*.md"))) > len(cpr.GRANDFATHERED) > 0
    )  # it ran, on real plans


def test_caps_all_cli(tmp_path):
    plans = tmp_path / "docs/process/plans"
    plans.mkdir(parents=True)
    (plans / "plan-new001.md").write_text(_plan(log=REVISED))
    assert (
        cpr.main(["--caps-all", "--repo", str(tmp_path)]) == 1
    )  # every real grandfathered row is missing here
    single = plans / "plan-new001.md"
    assert cpr.main([str(single), "--repo", str(tmp_path)]) == 0
    single.write_text(_plan(log=REVISED_TWICE))
    assert cpr.main([str(single), "--repo", str(tmp_path)]) == 1


# --- reviewer's false-high cases (PR #2562): text that mentions a pass is not a recorded pass ----


def test_design_pass_headings_in_body_are_not_verify_passes():
    body = (
        BODY
        + "\n## Design Decisions\n\n### Pass 1\nbind\n\n### Pass 2\ncheck\n\n### Pass 3\nlower\n"
    )
    text = _plan(body=body)
    assert cpr.verify_passes(text) == 1
    assert cpr.cap_violations("plan-new001.md", text) == []


def test_revision_log_ordinal_prose_is_not_a_pass():
    log = (
        REVISED
        + "- @ 3333333 — D2 moves the walrus check to the third pass of the binder.\n"
    )
    assert cpr.verify_passes(_plan(log=log)) == 2


def test_summary_bullet_quoting_a_verdict_is_not_a_pass():
    summary = SUMMARY + "- previous pass (NEEDS REVISION, Dimension 6) answered by D3\n"
    assert cpr.verify_passes(_plan(log=REVISED, summary=summary)) == 2


def test_prose_quoting_the_verdict_format_is_not_a_pass():
    body = (
        BODY
        + "\nA failed pass is logged as `(NEEDS REVISION, Dimension N)`;\n"
        + "- the revising session writes (NEEDS REVISION, Dimension 7) on its entry.\n"
    )
    assert cpr.verify_passes(_plan(body=body)) == 1


def test_implementation_checklist_is_not_body():
    # /implement-plan adds the checklist after verification; it must not turn a compliant plan red
    filler = "x" * (cpr.BODY_CAP_BYTES - len(BODY.encode()) - 1)
    checklist = "\n## Implementation Checklist\n\n" + "- [x] task\n" * 500
    text = _plan(body=BODY + filler + checklist)
    assert cpr.body_bytes(text) == cpr.BODY_CAP_BYTES
    assert cpr.cap_violations("plan-new001.md", text) == []


# --- section budgets: reported for plans that are not grandfathered, never a failure -------------

SKILL = REPO / ".claude/skills/create-plan/SKILL.md"


def _template_keys() -> set[str]:
    """The `## ` heading names of every plan template in /create-plan (fenced blocks with a title)."""
    blocks = [
        b
        for b in cpr.FENCE_RE.findall(SKILL.read_text(encoding="utf-8"))
        if "\n# <" in b
    ]
    assert blocks, "no plan template found in create-plan/SKILL.md"
    keys = set()
    for b in blocks:
        inner = b.split("\n", 1)[1].rsplit("\n", 1)[
            0
        ]  # drop the fence lines themselves
        for _, heading in cpr.h2_headings(inner):
            key = cpr.section_key(heading)
            assert key, f"template heading `## {heading}` is not in PLAN_SECTIONS"
            keys.add(key)
    return keys


def test_plan_sections_are_the_template_headings():
    assert _template_keys() | {"Revision log"} == set(cpr.PLAN_SECTIONS)


def test_section_key_matches_heading_variants_only():
    assert cpr.section_key("Adversarial Review (pre-mortem)") == "Adversarial Review"
    assert cpr.section_key("Current State — measured @ abc1234") == "Current State"
    assert cpr.section_key("Revision log") == "Revision log"
    for record in (
        "Implementation Checklist",
        "Implementation Verification Record",
        "Verification Summary — round 6",
        "Issues to file during implementation",
    ):
        assert cpr.section_key(record) is None, record


def _budget_plan(context=0, adversarial=0, scope=0) -> str:
    body = (
        "# P99 — sample\n\n## Context\n\n"
        + "c" * context
        + "\n## Scope\n\n"
        + "s" * scope
        + "\n## Adversarial Review (pre-mortem)\n\n"
        + "a" * adversarial
        + "\n## Design Decisions\n\n"
        + "d" * 5_000
        + "\n"
    )
    return _plan(body=body)


def test_section_bytes_measure_each_heading_to_the_next():
    sizes = cpr.section_bytes(_budget_plan(context=100, adversarial=200, scope=300))
    assert sizes["Context"] == len("## Context\n\n") + 101
    assert (
        sizes["Adversarial Review"]
        == len("## Adversarial Review (pre-mortem)\n\n") + 201
    )
    assert sizes["Scope"] == len("## Scope\n\n") + 301
    assert "Verification Summary" not in sizes


def test_budget_over_is_a_note_not_a_failure():
    within = _budget_plan(context=1_000, adversarial=1_000, scope=1_000)
    assert cpr.budget_notes("plan-new001.md", within) == []
    # mutation record: Context past its budget -> one note naming it; the caps stay green
    over = _budget_plan(context=3_000, adversarial=1_000, scope=1_000)
    (note,) = cpr.budget_notes("plan-new001.md", over)
    assert "Context + Current State" in note and "2,560-byte budget" in note
    assert cpr.cap_violations("plan-new001.md", over) == []
    all_over = _budget_plan(context=3_000, adversarial=2_000, scope=3_000)
    assert (
        len(cpr.budget_notes("plan-new001.md", all_over))
        == len(cpr.SECTION_BUDGETS)
        == 3
    )


def test_grandfathered_plan_gets_no_budget_note(monkeypatch):
    over = _budget_plan(context=3_000)
    monkeypatch.setitem(cpr.GRANDFATHERED, "plan-old001.md", (cpr.body_bytes(over), 1))
    assert cpr.budget_notes("plan-old001.md", over) == []


def test_single_plan_cli_reports_budgets_and_exits_on_caps_only(tmp_path, capsys):
    plan = tmp_path / "plan-new001.md"
    plan.write_text(_budget_plan(context=3_000))
    assert cpr.main([str(plan), "--repo", str(tmp_path)]) == 0
    out = capsys.readouterr().out
    assert "section budgets" in out
    assert "NOTE: plan-new001.md: Context + Current State" in out


# --- the body is everything but the skills' record sections -----------------------------------

CHECKLIST = "## Implementation Checklist\n\n- [ ] T1 — lead\n"
TESTING = "## Testing Strategy\n\n" + "t" * 3_000 + "\n"
SKILLS = [
    REPO / f".claude/skills/{n}/SKILL.md"
    for n in ("create-plan", "verify-plan", "implement-plan", "verify-implementation")
]


def test_record_sections_are_the_headings_the_skills_write():
    text = "".join(p.read_text(encoding="utf-8") for p in SKILLS)
    named = {cpr.heading_name(h) for h in re.findall(r"`## ([^`]+)`", text)}
    assert named <= {n.casefold() for n in cpr.PLAN_SECTIONS + cpr.RECORD_SECTIONS}
    # the report /verify-implementation appends inside its record is a heading of its template
    assert "\n## Implementation Verification Report\n" in SKILLS[3].read_text(
        encoding="utf-8"
    )
    written = (named - {n.casefold() for n in cpr.PLAN_SECTIONS}) | {
        "implementation verification report"
    }
    assert written == {n.casefold() for n in cpr.RECORD_SECTIONS}


def test_plan_section_moved_under_the_checklist_still_counts():
    filler = "x" * (cpr.BODY_CAP_BYTES - len(BODY.encode()) - len(TESTING.encode()) - 1)
    in_place = _plan(body=BODY + filler + "\n" + TESTING + "\n" + CHECKLIST)
    moved = _plan(body=BODY + filler + "\n" + CHECKLIST + TESTING)
    assert cpr.body_bytes(moved) == cpr.body_bytes(in_place) == cpr.BODY_CAP_BYTES + 1
    assert cpr.cap_violations("plan-new001.md", in_place)
    assert cpr.cap_violations("plan-new001.md", moved)
    assert cpr.section_bytes(moved)["Testing Strategy"] == len(TESTING.encode()) + 1


ADV_HEAD = "<!-- DRAFT, not verified -->\n# T\n\n## Context\n\nshort\n\n"
ADV_FILL = "x" * 30_000 + "\n"


@pytest.mark.parametrize(
    "below",
    [
        CHECKLIST + "\n## Phases\n\n",
        CHECKLIST + "\n## Implementation Plan\n\n",
        "## Verification Summary\n\n## Notes on design\n\n",
        CHECKLIST + "\n## Design Decisions (cont.)\n\n",
        CHECKLIST + "\n## design decisions\n\n",
        CHECKLIST + "\n## Design Decisions:\n\n",
        CHECKLIST + "\n## Design Decisions — wave 2\n\n",
        CHECKLIST + "\n## Design Decisions - wave 2\n\n",
        CHECKLIST + "\n## Design Decision\n\n",
        CHECKLIST + "\n## Revisions\n\n",
        CHECKLIST + "\n## REVISION LOG\n\n",
        CHECKLIST + "\n## Implementation Record (lead)\n\n",
        CHECKLIST + "\n## Design Decisions\n\nshort\n```\n## not a heading\n```\n",
        "```\n## Verification Summary\n```\n",
    ],
)
def test_any_non_record_section_below_a_record_is_body(below):
    # mutation record: plan_body keeping no section below the first record heading -> red
    text = ADV_HEAD + below + ADV_FILL
    assert cpr.body_bytes(text) > 30_000
    assert cpr.cap_violations("plan-new001.md", text)
    crlf = text.replace("\n", "\r\n")
    assert cpr.body_bytes(crlf) > 30_000


@pytest.mark.parametrize(
    "below",
    [
        CHECKLIST + "\n### Context\n\n",  # a subsection of the checklist
        "## implementation checklist\n\n",
        "## Verification Summary — round 2\n\n### Pass 2\n\n",
        "## Verification Summary - round 2\n\n",  # an ASCII hyphen ends the name like a dash
        "## Implementation Verification Report\n\n### Regressions\n\n",
        "## Implementation Verification Record\n\n## Auditor notes\n\n",
        # a record's own content is not body, whatever it holds (accepted, not a hole the measure
        # can close: a reader of the plan sees it)
        CHECKLIST + "\n### Design notes\n\n",
        CHECKLIST
        + "\n##Design Decisions\n\n",  # no space after the hashes: not a heading
        "## Implementation Checklist - wave 2 design\n\n",
        "## Verification Summary (and design notes)\n\n",
        "## Implementation Checklist ##\n\n",
        "## Implementation Checklist   \n\n",
    ],
)
def test_record_sections_and_the_terminal_record_are_not_body(below):
    # mutation record: plan_body keeping every section below the first record heading -> red
    text = ADV_HEAD + below + ADV_FILL
    assert cpr.body_bytes(text) == len((ADV_HEAD.split("\n", 1)[1]).encode())
    assert cpr.cap_violations("plan-new001.md", text) == []


DD = "## Design Decisions\n\n" + ADV_FILL


@pytest.mark.parametrize(
    "text",
    [
        # a plan section after the terminal record is still body
        ADV_HEAD + "## Implementation Verification Record\n\n(pending)\n\n" + DD,
        ADV_HEAD + "## implementation verification record:\n\n" + DD,
        "## Implementation Verification Report\n\n" + ADV_HEAD + DD,
        ADV_HEAD
        + "## Implementation Verification Record\n\n## Acceptance\n\n"
        + ADV_FILL,
        "## Verification Summary\n\n" + ADV_HEAD + DD,
        "## Implementation Checklist\n\n# T\n\n" + ADV_FILL,
        # a fence left open makes the rest of the file body
        ADV_HEAD + "```\n## Implementation Checklist\n" + ADV_FILL,
        ADV_HEAD + CHECKLIST + "\n```\n## Design Decisions\n\n" + ADV_FILL,
        # a fence closes only on its own character, at least as long, with nothing after it
        ADV_HEAD + CHECKLIST + "\n~~~\n```\n~~~\n" + DD,
        ADV_HEAD + CHECKLIST + "\n````\n```\n````\n" + DD,
        ADV_HEAD
        + "```python\n## Implementation Checklist\n```python\n## still in?\n"
        + ADV_FILL,
        ADV_HEAD + "````\n```\n## Implementation Checklist\n```\n````\n" + ADV_FILL,
        ADV_HEAD + "```\n```python\n## Implementation Checklist\n```\n```\n" + ADV_FILL,
        # headings CommonMark recognises: indented up to 3 spaces, a tab after the hashes, `#`
        ADV_HEAD + CHECKLIST + "\n  ## Design Decisions\n\n" + ADV_FILL,
        ADV_HEAD + CHECKLIST + "\n##\tDesign Decisions\n\n" + ADV_FILL,
        ADV_HEAD + CHECKLIST + "\n# Design Decisions\n\n" + ADV_FILL,
        ADV_HEAD + CHECKLIST + "\n<!--\n## Design Decisions\n-->\n" + ADV_FILL,
        ADV_HEAD + "<!--\n" + ADV_FILL + "-->\n",
    ],
)
def test_early_records_open_fences_and_heading_forms_hide_nothing(text):
    # mutation records: plan sections after the terminal record dropped; the unclosed-fence rule
    # removed; fences closed by any fence line; headings only at column 0 with a space -> each red
    assert cpr.body_bytes(text) > 30_000
    assert cpr.cap_violations("plan-new001.md", text)


def test_revision_log_counts_wherever_it_sits():
    base = _plan(body=BODY)
    logged = _plan(summary=SUMMARY + REVISED)
    assert cpr.body_bytes(logged) == cpr.body_bytes(base) + len(REVISED.encode()) - 1


def test_empty_and_stamp_only_plans_measure_zero():
    assert cpr.body_bytes("") == 0
    assert cpr.body_bytes("<!-- a -->\n<!-- b -->\n") == 0


def test_real_plan_with_a_mid_plan_checklist_counts_its_plan_sections():
    text = (PLANS / "plan-5c21c8.md").read_text(encoding="utf-8")
    first = next(h for _, h in cpr.h2_headings(text) if cpr.record_key(h))
    assert first == "Implementation Checklist"
    body = cpr.plan_body(text)
    for heading in ("Implementation", "Testing Strategy", "Issues to Close"):
        assert f"\n## {heading}\n" in body
    assert "\n## Implementation Checklist" not in body
    assert cpr.body_bytes(text) > cpr.BODY_CAP_BYTES


# --- the numbers the skills state are the checker's constants ----------------------------------

NUMBER_RE = re.compile(
    r"≤ (?P<passes>\d+) verify passes|(?P<sign>[≤+]) ?(?P<n>[\d.,]+) (?P<unit>KB|bytes)"
)
# The section a budget statement belongs to: the last of these named before it.
BUDGET_OWNERS = {
    "Context": "Context + Current State",
    "Current State": "Context + Current State",
    "Adversarial": "Adversarial Review",
    "Issues to Close": "Scope + Issues to Close + Class trackers",
}


def _stated_numbers() -> list[tuple[str, int, int]]:
    """(what, stated value, constant) for every cap or budget number the plan skills state."""
    budgets = {label: cap for label, _, cap in cpr.SECTION_BUDGETS}
    out = []
    for path in SKILLS:
        text = path.read_text(encoding="utf-8")
        for m in NUMBER_RE.finditer(text):
            if m.group("passes"):
                out.append(("verify passes", int(m.group("passes")), cpr.PASS_CAP))
                continue
            n = float(m.group("n").replace(",", ""))
            value = int(n * 1024) if m.group("unit") == "KB" else int(n)
            if m.group("unit") == "bytes":
                out.append(("body cap", value, cpr.BODY_CAP_BYTES))
            elif m.group("sign") == "+":
                out.append(("revision growth", value, cpr.REVISION_GROWTH_BYTES))
            else:
                before = text[: m.start()]
                owner = max(BUDGET_OWNERS, key=before.rfind)
                out.append((BUDGET_OWNERS[owner], value, budgets[BUDGET_OWNERS[owner]]))
    return out


def test_numbers_the_skills_state_equal_the_constants():
    # mutation record: "≤ 2.5 KB" -> "≤ 3 KB" in create-plan/SKILL.md -> red; restored -> green
    stated = _stated_numbers()
    assert [(w, v) for w, v, c in stated if v != c] == []
    kinds = {w for w, _, _ in stated}
    assert kinds == {
        "body cap",
        "verify passes",
        "revision growth",
        *(label for label, _, _ in cpr.SECTION_BUDGETS),
    }
    # the cap is stated once
    assert sum(w == "body cap" for w, _, _ in stated) == 1


def test_stamp_lines_are_blank_or_one_whole_comment():
    from build_tools.check_plan_refs import is_stamp_line

    for line in ("", "   ", "<!---->", "<!-- DRAFT, not verified -->", "  <!-- a -->\t", "<!-- a --> <!-- b -->"):
        assert is_stamp_line(line), line
    for line in ("<!--->", "<!-->", "<!-- a", "a -->", "<!-- a --> x", "x <!-- a -->", "text"):
        assert not is_stamp_line(line), line
