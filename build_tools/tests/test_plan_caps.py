"""check_plan_refs caps (R-LW): plan body <= 20 KB before its summaries; <= 2 verify passes."""

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
