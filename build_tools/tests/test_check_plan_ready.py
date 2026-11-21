"""check_plan_ready: `/implement-plan` step 2's decision, one test (or more) per branch.

Every GitHub read goes through a fake `fetch`; `check_plan_refs` through a fake `refs`.
"""

from pathlib import Path

import pytest

from build_tools import check_plan_ready as cpr

REPO_ROOT = Path(__file__).resolve().parents[2]

FULL = "<!-- Verified by /verify-plan on {date} @ abcdef123 -->"
ADEQ = "<!-- Verified by /verify-plan --adequacy (Adequacy-only: Dimensions 4-design, 6, 7; check_plan_refs clean) on {date} @ abcdef123 -->"
DRAFT = "<!-- DRAFT, not verified: written by /create-plan on 2026-11-20 @ abcdef123; no /verify-plan pass has stamped this text. -->"


def plan_text(
    stamp=FULL,
    *,
    date="2026-11-01",
    result=None,
    adequacy="CLASS",
    scope="ONE-GROUP · Generator: existing",
    tier=None,
    issues="- #100 — the input issue; **close criterion:** A1",
    defect_class=True,
    extra="",
    pre="",
):
    head = [pre] if pre else []
    if result is None:  # the result each stamp kind writes by default
        result = "PASS" if stamp == ADEQ else "PASS WITH CORRECTIONS"
    if stamp:
        head.append(stamp.format(date=date))
        if result != "-":  # "-": no result line
            head.append(f"<!-- Verification result: {result} -->")
        if adequacy is not None:
            head.append(f"<!-- Adequacy: {adequacy} -->")
        if scope is not None:
            head.append(f"<!-- Scope: {scope} -->")
    body = ["", "# P99 — a plan", "", "## Scope", "", "- **Group:** P99 (chores: none)"]
    if tier:
        body.append(f"- **Tier:** {tier}")
    body += ["- **Override:** none", ""]
    if defect_class:
        body += ["## Defect Class", "", "- **Violated contract:** x", ""]
    if issues is not None:
        body += ["## Issues to Close", "", issues, ""]
    body += [extra]
    return "\n".join(head + body) + "\n"


def issue(labels=(), title="an issue"):
    return {"title": title, "labels": [{"name": l} for l in labels]}


class Fetch:
    """path -> response; a FetchError instance is raised; unknown paths raise FetchError."""

    def __init__(self, table=None):
        self.table = dict(table or {})
        self.calls = []

    def __call__(self, path):
        self.calls.append(path)
        v = self.table.get(path, cpr.FetchError(f"no fixture for {path}"))
        if isinstance(v, Exception):
            raise v
        return v


FEATURE = Fetch({"repos/antonsynd/sharpy/issues/100": issue(["enhancement"])})
BUG = Fetch({"repos/antonsynd/sharpy/issues/100": issue(["bug"])})
CLEAN_REFS = lambda p: (0, "RESULT: OK\n")


def run(text, fetch=FEATURE, refs=CLEAN_REFS, contains=lambda a, b: None, ci=False):
    return cpr.decide(
        cpr.parse_plan("docs/process/plans/plan-x.md", text),
        fetch=fetch,
        refs=refs,
        contains=contains,
        ci=ci,
    )


def verdicts(report):
    return [f.verdict for f in report.findings]


# ── tier ─────────────────────────────────────────────────────────────────────────────────────────


@pytest.mark.parametrize(
    "tier, want",
    [
        (None, "full"),
        ("light — from charter §3.8", "light"),
        ("full (c: the seam is under `Semantic/`) — from charter §3.20l", "full"),
        ("`tier: light (derived, no §12 letter)`: doors ruled", "light"),
        ("full (a, promoted at /implement-plan: D9), from light", "full"),
    ],
)
def test_tier_line(tier, want):
    assert cpr.parse_plan("p", plan_text(tier=tier)).tier == want


def test_tier_inline_after_group_and_outside_scope_ignored():
    text = plan_text().replace(
        "- **Group:** P99 (chores: none)",
        "- **Group:** P82; no chores. **Tier:** light. **Alone:** no.",
    )
    assert cpr.parse_plan("p", text).tier == "light"
    later = plan_text(
        extra="## Implementation Verification Record\n\n**Tier:** light — Agents 2, 3, 4\n"
    )
    assert (
        cpr.parse_plan("p", later).tier == "full"
    )  # a Tier outside `## Scope` is not the plan's


# ── unverified / DRAFT ───────────────────────────────────────────────────────────────────────────


def test_full_without_stamp_asks():
    r = run(plan_text(stamp=None))
    assert r.verdict == cpr.ASK and cpr.MSG_UNVERIFIED in r.findings[0].message


def test_full_draft_marker_asks():
    r = run(plan_text(stamp=None, pre=DRAFT))
    assert r.verdict == cpr.ASK and r.stamp == "DRAFT marker, no stamp"


def test_draft_marker_beside_a_stamp_is_still_unverified():
    r = run(plan_text(pre=DRAFT))
    assert r.verdict == cpr.ASK and cpr.MSG_UNVERIFIED in r.findings[0].message


@pytest.mark.parametrize("pre", ["", DRAFT])
def test_light_without_adequacy_pass_refuses(pre):
    r = run(plan_text(stamp=None, tier="light", pre=pre))
    assert r.verdict == cpr.REFUSE
    assert "This light plan has no adequacy pass" in r.findings[0].message


# ── NEEDS REVISION ───────────────────────────────────────────────────────────────────────────────


@pytest.mark.parametrize("stamp, tier", [(FULL, None), (ADEQ, "light")])
def test_needs_revision_refuses(stamp, tier):
    r = run(plan_text(stamp=stamp, tier=tier, result="NEEDS REVISION", adequacy="CELL"))
    assert (
        verdicts(r) == [cpr.REFUSE] and r.findings[0].message == cpr.MSG_NEEDS_REVISION
    )


def test_unreadable_result_refuses():
    r = run(plan_text(result="MAYBE"))
    assert r.verdict == cpr.REFUSE and "Verification result" in r.findings[0].message
    assert run(plan_text(result="-")).verdict == cpr.REFUSE


# ── PASS / PASS WITH CORRECTIONS × Adequacy ──────────────────────────────────────────────────────


@pytest.mark.parametrize("result", ["PASS", "PASS WITH CORRECTIONS"])
@pytest.mark.parametrize(
    "adequacy", ["CLASS", "N/A", "CLASS (Batches 0–6); N/A for Batch 7"]
)
def test_pass_class_or_na_is_ready(result, adequacy):
    r = run(plan_text(result=result, adequacy=adequacy), fetch=Fetch())
    assert r.verdict == cpr.READY, r.findings
    assert r.header["Tier"] == "full"


@pytest.mark.parametrize(
    "labels, title",
    [
        (["bug"], "x"),
        (["lane:ice", "reach:program"], "x"),
        (["lane:tooling"], "x"),
        ([], "ICE: crash"),
        ([], "a regression in emit"),
    ],
)
def test_cell_on_bug_fix_plan_asks(labels, title):
    fetch = Fetch({"repos/antonsynd/sharpy/issues/100": issue(labels, title)})
    r = run(plan_text(adequacy="CELL"), fetch=fetch)
    assert verdicts(r) == [cpr.ASK]
    assert cpr.MSG_CELL in r.findings[0].message
    assert "Override (cell)" in r.findings[0].message  # how to record the answer


def test_cell_on_feature_plan_is_ready():
    r = run(plan_text(adequacy="CELL"), fetch=FEATURE)
    assert r.verdict == cpr.READY and any("no bug input" in n for n in r.notes)


def test_cell_any_token_counts():
    # a mixed Adequacy line holding CELL anywhere is read as CELL (fail-safe)
    r = run(plan_text(adequacy="CLASS for D1; CELL for D2"), fetch=BUG)
    assert r.verdict == cpr.ASK


def test_adequacy_line_without_a_verdict_refuses():
    assert run(plan_text(adequacy="see summary")).verdict == cpr.REFUSE


# ── CONVERGED ────────────────────────────────────────────────────────────────────────────────────


def test_converged_class_is_ready_and_header_says_converged():
    r = run(plan_text(result="CONVERGED"))
    assert r.verdict == cpr.READY and r.header["Adequacy"] == "CLASS (converged)"


def test_converged_cell_on_bug_fix_asks_with_deferred_rows():
    extra = (
        "## Verification Summary\n\n### Deferred findings\n\n"
        "1. axis 1 roster ← enumeration — fix: map — proved by roster test\n"
        "2. axis 2 ← enumeration — fix: entry — proved by mutation\n\n### Unchecked Claims\n\n- none\n"
    )
    r = run(plan_text(result="CONVERGED", adequacy="CELL", extra=extra), fetch=BUG)
    assert verdicts(r) == [cpr.ASK]
    msg = r.findings[0].message
    assert msg.startswith(cpr.MSG_CONVERGED_CELL)
    assert "1. axis 1 roster" in msg and "2. axis 2" in msg and "- none" not in msg


def test_converged_cell_on_feature_plan_is_ready():
    assert run(plan_text(result="CONVERGED", adequacy="CELL")).verdict == cpr.READY


# ── a stamp without an Adequacy line ─────────────────────────────────────────────────────────────


def test_pre_adequacy_feature_plan_reads_na():
    r = run(plan_text(date="2026-08-24", adequacy=None, scope=None), fetch=FEATURE)
    assert cpr.ASK in verdicts(r)  # the Scope derivation; Adequacy itself proceeds
    assert all("Adequacy" not in f.message for f in r.findings)
    assert any(n.startswith("Adequacy: N/A") for n in r.notes)


def test_pre_adequacy_bug_fix_plan_without_defect_class_reads_cell():
    r = run(plan_text(date="2026-08-24", adequacy=None, defect_class=False), fetch=BUG)
    assert any(cpr.MSG_CELL in f.message for f in r.findings)


def test_pre_adequacy_bug_fix_plan_with_defect_class_proceeds():
    r = run(plan_text(date="2026-08-24", adequacy=None, defect_class=True), fetch=BUG)
    assert r.verdict == cpr.READY, r.findings


def test_no_input_issue_is_a_feature_plan():
    r = run(
        plan_text(
            date="2026-08-24",
            adequacy=None,
            defect_class=False,
            issues="None pre-existing.",
        ),
        fetch=Fetch(),
    )
    assert r.verdict == cpr.READY


def test_no_issues_section_asks_which_inputs():
    r = run(plan_text(date="2026-08-24", adequacy=None, issues=None), fetch=Fetch())
    assert verdicts(r) == [cpr.ASK] and "which issues" in r.findings[0].message


def test_stay_open_items_are_not_inputs():
    issues = (
        "- #100 — closes; criterion A1\n- **Stay open:** #200 (closes with P78), #201"
    )
    plan = cpr.parse_plan("p", plan_text(issues=issues))
    assert plan.inputs == [100]
    issues2 = "- #300, #301 — **stay open**; each gains a comment\n- #100 — closes"
    assert cpr.parse_plan("p", plan_text(issues=issues2)).inputs == [100]


def test_adequacy_line_missing_after_cutoff_refuses():
    r = run(plan_text(date="2026-09-01", adequacy=None))
    assert r.verdict == cpr.REFUSE and "no Adequacy line" in r.findings[0].message


@pytest.mark.parametrize(
    "has_line, want",
    [(False, cpr.READY), (True, cpr.REFUSE), (None, cpr.ASK)],
)
def test_adequacy_cutoff_day_is_decided_by_the_stamp_sha(has_line, want):
    seen = []

    def contains(a, b):
        seen.append((a, b))
        return has_line

    r = run(
        plan_text(date="2026-08-26", adequacy=None, issues="None."),
        fetch=Fetch(),
        contains=contains,
    )
    assert r.verdict == want
    assert seen == [(cpr.ADEQUACY_LINE_COMMIT, "abcdef123")]


# ── Scope ────────────────────────────────────────────────────────────────────────────────────────


def test_multi_group_asks():
    r = run(plan_text(scope="MULTI-GROUP (override) · Generator: existing"))
    assert verdicts(r) == [cpr.ASK] and cpr.MSG_SCOPE in r.findings[0].message
    assert "Scope: MULTI-GROUP" in r.findings[0].message


def test_generator_missing_asks():
    r = run(plan_text(scope="ONE-GROUP · Generator: MISSING"))
    assert verdicts(r) == [cpr.ASK] and "Generator: MISSING" in r.findings[0].message


@pytest.mark.parametrize(
    "scope",
    [
        "ONE-GROUP · Generator: built-first",
        "N/A · Generator: not-required",
        "ONE-GROUP (ordering override recorded) · Generator: existing",
    ],
)
def test_one_group_is_ready(scope):
    assert run(plan_text(scope=scope)).verdict == cpr.READY


def test_no_scope_line_before_cutoff_asks_for_the_derivation():
    r = run(plan_text(date="2026-09-20", scope=None))
    assert verdicts(r) == [cpr.ASK] and "derive the scope" in r.findings[0].message
    assert "P99" in r.findings[0].message  # the title's group ids are the evidence


def test_no_scope_line_after_cutoff_refuses():
    r = run(plan_text(date="2026-10-01", scope=None))
    assert r.verdict == cpr.REFUSE and "no Scope line" in r.findings[0].message


def test_unreadable_scope_line_refuses():
    assert run(plan_text(scope="maybe")).verdict == cpr.REFUSE


# ── light tier: the Adequacy-only stamp and check_plan_refs ──────────────────────────────────────


@pytest.mark.parametrize("result", ["PASS", "CONVERGED"])
def test_light_adequacy_stamp_with_clean_refs_is_ready(result):
    called = []

    def refs(path):
        called.append(path)
        return (
            0,
            "issues: 1 referenced; 0 CLOSED; 0 lookup errors; 0 unchecked (use --issues)\nRESULT: OK\n",
        )

    r = run(plan_text(stamp=ADEQ, tier="light", result=result), refs=refs)
    assert r.verdict == cpr.READY and called == ["docs/process/plans/plan-x.md"]
    assert r.header["Tier"] == "light"


def test_light_red_refs_refuses():
    r = run(plan_text(stamp=ADEQ, tier="light"), refs=lambda p: (1, "RESULT: FAIL\n"))
    assert verdicts(r) == [cpr.REFUSE] and "check_plan_refs.py" in r.findings[0].message


def test_light_refs_not_run_is_never_ready():
    r = run(plan_text(stamp=ADEQ, tier="light"), refs=None)
    assert r.verdict == cpr.UNVERIFIED


def test_light_refs_lookup_errors_are_a_note_not_red():
    out = "issues: 2 referenced; 0 CLOSED; 2 lookup errors; 0 unchecked (use --issues)\nRESULT: OK\n"
    r = run(plan_text(stamp=ADEQ, tier="light"), refs=lambda p: (0, out))
    assert r.verdict == cpr.READY and any(
        "2 issue lookups failed" in n for n in r.notes
    )


def test_full_plan_never_runs_refs():
    def refs(path):
        raise AssertionError("refs run on a full plan")

    assert run(plan_text(), refs=refs).verdict == cpr.READY


def test_light_cell_and_scope_rules_are_the_full_tiers():
    r = run(
        plan_text(stamp=ADEQ, tier="light", result="CONVERGED", adequacy="CELL"),
        fetch=BUG,
    )
    assert r.verdict == cpr.ASK and r.findings[0].message.startswith(
        cpr.MSG_CONVERGED_CELL
    )
    r = run(
        plan_text(stamp=ADEQ, tier="light", scope="MULTI-GROUP · Generator: existing")
    )
    assert r.verdict == cpr.ASK and cpr.MSG_SCOPE in r.findings[0].message


def test_light_plan_with_full_stamp_is_accepted_with_a_note():
    r = run(plan_text(tier="light", result="PASS"))
    assert r.verdict == cpr.READY and any(
        "full /verify-plan stamp" in n for n in r.notes
    )


def test_full_plan_with_adequacy_only_stamp_refuses_unless_promoted():
    # owner, 2026-11-21 (D2): a full plan needs a full pass
    for tier in (None, "full (c: the seam is under `Semantic/`)"):
        r = run(plan_text(stamp=ADEQ, tier=tier))
        assert verdicts(r) == [cpr.REFUSE]
        assert "a full plan needs a full /verify-plan pass" in r.findings[0].message
    # the 3eba57 shape: promoted in flight, CONVERGED, its Adequacy-only stamp still stands
    promoted = "full (a, promoted at /implement-plan: D9), from light"
    r = run(plan_text(stamp=ADEQ, tier=promoted, result="CONVERGED"))
    assert r.verdict == cpr.READY, r.findings
    assert r.header["Adequacy"] == "CLASS (converged)"


# ── overrides ────────────────────────────────────────────────────────────────────────────────────

CITE = "https://github.com/antonsynd/sharpy/issues/100#issuecomment-7050154806"
COMMENT_PATH = "repos/antonsynd/sharpy/issues/comments/7050154806"


def owner_comment(
    body, assoc="OWNER", issue=100, created="2026-11-06T15:00:00Z", app=None
):
    return {
        "author_association": assoc,
        "body": body,
        "user": {"login": "someone"},
        "issue_url": f"https://api.github.com/repos/antonsynd/sharpy/issues/{issue}",
        "created_at": created,
        "performed_via_github_app": {"slug": app} if app else None,
    }


def cell_plan(record, result="PASS WITH CORRECTIONS"):
    return plan_text(
        adequacy="CELL", result=result, extra=f"## Revision log\n\n{record}\n"
    )


def fetch_with(comment):
    return Fetch(
        {"repos/antonsynd/sharpy/issues/100": issue(["bug"]), COMMENT_PATH: comment}
    )


GOOD = f'- Override (cell): owner 2026-11-06 "implement it as a cell fix" — {CITE}'


def test_verified_owner_override_answers_the_cell_gate():
    r = run(
        cell_plan(GOOD),
        fetch=fetch_with(owner_comment("> Implement it as a **cell fix**, please")),
    )
    assert r.verdict == cpr.READY, r.findings
    assert r.header["Adequacy"] == "CELL (override)"
    assert any("verified" in n for n in r.notes)


def test_verified_override_answers_converged_cell_too():
    r = run(
        cell_plan(GOOD, result="CONVERGED"),
        fetch=fetch_with(owner_comment("implement it as a cell fix")),
    )
    assert (
        r.verdict == cpr.READY and r.header["Adequacy"] == "CELL (override) (converged)"
    )


@pytest.mark.parametrize(
    "assoc", ["COLLABORATOR", "MEMBER", "CONTRIBUTOR", "NONE", None]
)
def test_override_by_non_owner_refuses(assoc):
    r = run(
        cell_plan(GOOD),
        fetch=fetch_with(owner_comment("implement it as a cell fix", assoc=assoc)),
    )
    assert (
        verdicts(r) == [cpr.REFUSE]
        and "not the repository OWNER" in r.findings[0].message
    )


def test_override_words_absent_from_the_comment_refuses():
    r = run(
        cell_plan(GOOD), fetch=fetch_with(owner_comment("revise it, do not override"))
    )
    assert verdicts(r) == [cpr.REFUSE] and "not in comment" in r.findings[0].message


def test_override_comment_on_another_issue_refuses():
    r = run(
        cell_plan(GOOD),
        fetch=fetch_with(owner_comment("implement it as a cell fix", issue=9)),
    )
    assert verdicts(r) == [cpr.REFUSE] and "is not on #100" in r.findings[0].message


def test_override_comment_missing_refuses():
    r = run(cell_plan(GOOD), fetch=fetch_with(None))
    assert verdicts(r) == [cpr.REFUSE] and "does not exist" in r.findings[0].message


def test_override_in_another_repo_refuses():
    rec = '- Override (cell): owner 2026-11-06 "implement it knowingly" — https://github.com/evil/sharpy/issues/1#issuecomment-5'
    r = run(cell_plan(rec), fetch=BUG)
    assert (
        verdicts(r) == [cpr.REFUSE] and "not antonsynd/sharpy" in r.findings[0].message
    )


def test_override_without_citation_refuses():
    r = run(
        cell_plan('- Override (cell): owner 2026-11-06 "implement it knowingly"'), fetch=BUG
    )
    assert (
        verdicts(r) == [cpr.REFUSE]
        and "cites no owner comment" in r.findings[0].message
    )


def test_unreadable_override_is_unverified_and_fails_on_ci():
    fetch = Fetch(
        {
            "repos/antonsynd/sharpy/issues/100": issue(["bug"]),
            COMMENT_PATH: cpr.FetchError("offline"),
        }
    )
    assert run(cell_plan(GOOD), fetch=fetch).verdict == cpr.UNVERIFIED
    r = run(cell_plan(GOOD), fetch=fetch, ci=True)
    assert r.verdict == cpr.REFUSE and "on CI" in r.findings[0].message


def test_legacy_override_prose_is_listed_and_answers_nothing():
    legacy = '- 2026-11-06 @ ee9a007a1 — `/implement-plan` owner override of the `Adequacy: CELL` gate, owner\'s word: "override"'
    r = run(cell_plan(legacy), fetch=BUG)
    assert verdicts(r) == [cpr.ASK]
    assert "legacy override text at line" in r.findings[0].message


def test_override_for_the_other_gate_does_not_answer():
    scope_only = GOOD.replace("(cell)", "(scope)")
    r = run(
        cell_plan(scope_only),
        fetch=fetch_with(owner_comment("implement it as a cell fix")),
    )
    assert verdicts(r) == [cpr.ASK]


def test_scope_override_answers_multi_group():
    rec = f'- **Override (scope):** owner 2026-09-28 "go ahead with both groups" — #100 comment 7050154806'
    text = plan_text(scope="MULTI-GROUP (override) · Generator: existing", extra=rec)
    comment = owner_comment("ok — go ahead with both groups", created="2026-09-28T23:59:00Z")
    r = run(text, fetch=fetch_with(comment))
    assert r.verdict == cpr.READY, r.findings


def test_override_inside_a_fence_is_not_a_record():
    text = cell_plan(f"```\n{GOOD}\n```")
    assert cpr.parse_plan("p", text).overrides == []


def test_overrides_are_read_only_for_a_gate_the_plan_hits():
    fetch = Fetch()  # any read would raise
    r = run(plan_text(extra=GOOD), fetch=fetch)
    assert r.verdict == cpr.READY and fetch.calls == []


@pytest.mark.parametrize(
    "text, want",
    [
        (
            "https://github.com/antonsynd/sharpy/issues/12#issuecomment-34",
            ("issue-comment", "antonsynd/sharpy", 12, 34),
        ),
        (
            "https://github.com/antonsynd/sharpy/pull/12#issuecomment-34",
            ("issue-comment", "antonsynd/sharpy", 12, 34),
        ),
        (
            "https://github.com/antonsynd/sharpy/pull/12/files#discussion_r34",
            ("review-comment", "antonsynd/sharpy", 12, 34),
        ),
        (
            "https://github.com/antonsynd/sharpy/pull/12#pullrequestreview-34",
            ("review", "antonsynd/sharpy", 12, 34),
        ),
        (
            "(#2423 comment 7050154806)",
            ("issue-comment", "antonsynd/sharpy", 2423, 7050154806),
        ),
        ("no citation", None),
    ],
)
def test_citation_forms(text, want):
    assert cpr.parse_cite(text) == want


def test_comment_paths():
    assert (
        cpr.comment_path(("issue-comment", "a/b", 1, 2))
        == "repos/a/b/issues/comments/2"
    )
    assert (
        cpr.comment_path(("review-comment", "a/b", 1, 2))
        == "repos/a/b/pulls/comments/2"
    )
    assert cpr.comment_path(("review", "a/b", 1, 2)) == "repos/a/b/pulls/1/reviews/2"


def test_review_comment_parent_is_checked():
    rec = '- Override (cell): owner 2026-11-06 "yes, implement it knowingly" — https://github.com/antonsynd/sharpy/pull/12#discussion_r34'
    c = {
        "author_association": "OWNER",
        "body": "yes, implement it knowingly",
        "created_at": "2026-11-06T10:00:00Z",
        "pull_request_url": "https://api.github.com/repos/antonsynd/sharpy/pulls/12",
    }
    fetch = Fetch(
        {
            "repos/antonsynd/sharpy/issues/100": issue(["bug"]),
            "repos/antonsynd/sharpy/pulls/comments/34": c,
            # #12 is the pull request that holds the plan file (not an input, not named in the body)
            "repos/antonsynd/sharpy/pulls/12/files?per_page=100&page=1": [
                {"filename": "src/x.cs"},
                {"filename": "docs/process/plans/plan-x.md"},
            ],
        }
    )
    assert run(cell_plan(rec), fetch=fetch).verdict == cpr.READY
    c["pull_request_url"] = c["pull_request_url"].replace("/12", "/13")
    assert run(cell_plan(rec), fetch=fetch).verdict == cpr.REFUSE


def test_normalise():
    assert (
        cpr.normalise("> nix that\n>\n> **Recommendation:** no  fifth pass.")
        == "nix that recommendation: no fifth pass."
    )
    assert cpr.normalise("“Keep light”") == '"keep light"'


# ── labels unreadable ────────────────────────────────────────────────────────────────────────────


def test_unreadable_labels_are_unverified():
    r = run(plan_text(adequacy="CELL"), fetch=Fetch())
    assert (
        r.verdict == cpr.UNVERIFIED and "could not read #100" in r.findings[0].message
    )
    assert run(plan_text(adequacy="CELL"), fetch=Fetch(), ci=True).verdict == cpr.REFUSE


# ── precedence, CI, exit codes ───────────────────────────────────────────────────────────────────


def test_refuse_outranks_ask():
    r = run(
        plan_text(adequacy="CELL", scope="ONE-GROUP · Generator: MISSING", extra=GOOD),
        fetch=fetch_with(owner_comment("other")),
    )
    assert set(verdicts(r)) == {cpr.REFUSE, cpr.ASK} and r.verdict == cpr.REFUSE


@pytest.mark.parametrize(
    "value, want",
    [
        ("true", True),
        ("1", True),
        ("TRUE", True),
        ("false", False),
        ("0", False),
        ("", False),
        ("yes", False),
    ],
)
def test_on_ci(value, want):
    assert cpr.on_ci({"CI": value}) is want


def test_exit_codes_are_distinct():
    assert sorted(cpr.EXIT.values()) == [0, 1, 3, 4]
    assert 2 not in cpr.EXIT.values()  # argparse's usage error


@pytest.mark.parametrize(
    "text, code",
    [
        (plan_text(issues="None."), 0),
        (plan_text(result="NEEDS REVISION"), 1),
        (plan_text(stamp=None), 3),
        (plan_text(adequacy="CELL"), 4),  # --offline: the input's labels cannot be read
    ],
)
def test_main_exit_code(tmp_path, monkeypatch, capsys, text, code):
    monkeypatch.delenv("CI", raising=False)
    p = tmp_path / "plan-x.md"
    p.write_text(text)
    assert cpr.main([str(p), "--repo", str(tmp_path), "--offline"]) == code
    assert (
        capsys.readouterr()
        .out.rstrip()
        .endswith(
            "VERDICT: " + {0: "READY", 1: "REFUSE", 3: "ASK", 4: "UNVERIFIED"}[code]
        )
    )


def test_main_unreadable_plan(tmp_path, capsys):
    assert cpr.main([str(tmp_path / "missing.md"), "--offline"]) == 2


# ── the corpus (a control on real plans; offline, no refs) ───────────────────────────────────────


def corpus():
    return sorted((REPO_ROOT / "docs/process/plans").glob("*.md"))


def test_corpus_parses_and_known_rows_hold():
    plans = {
        p.name: cpr.parse_plan(p.name, p.read_text(encoding="utf-8")) for p in corpus()
    }
    assert plans, "the corpus is empty: the glob is wrong"
    got = {
        name: cpr.decide(
            plan,
            fetch=cpr.offline_fetch,
            refs=lambda p: (0, ""),
            contains=lambda a, b: None,
        ).verdict
        for name, plan in plans.items()
    }
    # one row per verdict the corpus holds, each read by hand against step 2
    if "plan-777361.md" in got:
        assert (
            got["plan-777361.md"] == cpr.ASK
        )  # DRAFT by the owner's ruling (#2423 comment 7050154806)
    if "plan-ed89b6.md" in got:
        assert got["plan-ed89b6.md"] == cpr.REFUSE  # light, NEEDS REVISION
    if "plan-05dd79.md" in got:
        assert (
            got["plan-05dd79.md"] == cpr.READY
        )  # full, PASS WITH CORRECTIONS, CLASS, ONE-GROUP
    if "plan-3eba57.md" in got:
        assert (
            got["plan-3eba57.md"] == cpr.UNVERIFIED
        )  # CONVERGED + CELL: the inputs' labels are on GitHub


def test_script_runs_the_sibling_check_plan_refs(tmp_path):
    # `build_tools` is a namespace package: with another checkout on PYTHONPATH, an import by
    # package name would run that checkout's check_plan_refs. A decoy proves which one runs.
    import os
    import subprocess
    import sys

    decoy = tmp_path / "decoy" / "build_tools"
    decoy.mkdir(parents=True)
    (decoy / "check_plan_refs.py").write_text(
        'raise SystemExit("decoy check_plan_refs")\n'
    )
    plan = tmp_path / "plan-x.md"
    plan.write_text(plan_text(stamp=ADEQ, tier="light"))
    env = dict(os.environ, PYTHONPATH=str(tmp_path / "decoy"))
    env.pop("CI", None)
    proc = subprocess.run(
        [
            sys.executable,
            str(REPO_ROOT / "build_tools/check_plan_ready.py"),
            str(plan),
            "--repo",
            str(tmp_path),
            "--offline",
        ],
        capture_output=True,
        text=True,
        env=env,
    )
    assert "decoy" not in proc.stdout + proc.stderr
    assert "check_plan_refs:" in proc.stdout  # the sibling ran and printed its report
    assert proc.returncode in (0, 1), proc.stdout + proc.stderr


# ── fix round 1: trivial words, date, link, app, malformed light stamp, comments, tiers ─────────


@pytest.mark.parametrize("words", ["**", "`", " ", "o", "ok", "override", "go ahead", "a b c"])
def test_trivial_override_words_are_a_malformed_record(words):
    fetch = fetch_with(owner_comment(f"{words} anything at all, a long comment"))
    rec = f'- Override (cell): owner 2026-11-06 "{words}" — {CITE}'
    r = run(cell_plan(rec), fetch=fetch)
    assert verdicts(r) == [cpr.REFUSE] and "malformed record" in r.findings[0].message
    assert COMMENT_PATH not in fetch.calls  # refused before any read


def test_single_negated_word_fails_and_a_negated_phrase_is_not_judged():
    # "override" inside "do not override" fails on length alone
    rec = f'- Override (cell): owner 2026-11-06 "override" — {CITE}'
    r = run(cell_plan(rec), fetch=fetch_with(owner_comment("do not override")))
    assert r.verdict == cpr.REFUSE
    # a 3-word phrase inside its negation verifies: the checker does not judge meaning (documented)
    rec = f'- Override (cell): owner 2026-11-06 "implement it knowingly" — {CITE}'
    r = run(cell_plan(rec), fetch=fetch_with(owner_comment("do not implement it knowingly")))
    assert r.verdict == cpr.READY
    assert "does not judge meaning" in cpr.verify_override.__doc__
    assert "does not judge meaning" in cpr.__doc__


@pytest.mark.parametrize(
    "created, want",
    [
        ("2026-11-05T00:00:01Z", cpr.READY),
        ("2026-11-06T23:59:59Z", cpr.READY),
        ("2026-11-07T23:59:59Z", cpr.READY),
        ("2026-11-04T23:59:59Z", cpr.REFUSE),
        ("2026-11-08T00:00:00Z", cpr.REFUSE),
        ("", cpr.REFUSE),
    ],
)
def test_override_comment_date_is_the_record_date_within_a_day(created, want):
    comment = owner_comment("implement it as a cell fix", created=created)
    r = run(cell_plan(GOOD), fetch=fetch_with(comment))
    assert r.verdict == want, r.findings


def _unlinked(body, files):
    rec = '- Override (cell): owner 2026-11-06 "implement it as a cell fix" — #2487 comment 7055038414'
    comment = owner_comment(body, issue=2487)
    table = {
        "repos/antonsynd/sharpy/issues/100": issue(["bug"]),
        "repos/antonsynd/sharpy/issues/comments/7055038414": comment,
        "repos/antonsynd/sharpy/pulls/2487/files?per_page=100&page=1": files,
    }
    return run(cell_plan(rec), fetch=Fetch(table))


def test_override_on_an_issue_unrelated_to_the_plan_refuses():
    r = _unlinked("implement it as a cell fix", None)  # #2487: no input, no plan id, no PR
    assert verdicts(r) == [cpr.REFUSE] and "not one of the plan's inputs" in r.findings[0].message


def test_override_comment_naming_the_plan_id_is_linked():
    r = _unlinked("plan-x: implement it as a cell fix", None)
    assert r.verdict == cpr.READY, r.findings
    assert _unlinked("plan-xy: implement it as a cell fix", None).verdict == cpr.REFUSE


def test_override_on_the_pull_request_holding_the_plan_is_linked():
    r = _unlinked("implement it as a cell fix", [{"filename": "docs/process/plans/plan-x.md"}])
    assert r.verdict == cpr.READY, r.findings
    r = _unlinked("implement it as a cell fix", [{"filename": "docs/process/plans/plan-y.md"}])
    assert r.verdict == cpr.REFUSE


def test_override_link_unreadable_is_unverified():
    r = _unlinked("implement it as a cell fix", cpr.FetchError("offline"))
    assert r.verdict == cpr.UNVERIFIED


def test_reviewer_repro_drift_guard_note_no_longer_verifies():
    # the review's live repro: Adequacy CELL, input #2765, `"**"` citing an unrelated note on #2487
    text = plan_text(adequacy="CELL", issues="- #2765 — the input")
    text += '\n## Revision log\n\n- Override (cell): owner 2026-11-21 "**" — #2487 comment 7055038414\n'
    fetch = Fetch(
        {
            "repos/antonsynd/sharpy/issues/2765": issue(["bug"]),
            "repos/antonsynd/sharpy/issues/comments/7055038414": owner_comment(
                "**Drift guard** note", issue=2487, created="2026-11-21T09:00:00Z"
            ),
        }
    )
    assert run(text, fetch=fetch).verdict == cpr.REFUSE


@pytest.mark.parametrize("app, want", [("claude", "relayed via app claude"), (None, "no app")])
def test_override_app_is_printed_not_judged(app, want):
    r = run(cell_plan(GOOD), fetch=fetch_with(owner_comment("implement it as a cell fix", app=app)))
    assert r.verdict == cpr.READY
    assert any(want in n for n in r.notes)
    assert want in r.header["Override"]
    assert want in cpr.render(r)


@pytest.mark.parametrize("stamp, tier", [(ADEQ, "light"), (FULL, "light")])
def test_light_plan_stamped_pass_with_corrections_is_malformed(stamp, tier):
    r = run(plan_text(stamp=stamp, tier=tier, result="PASS WITH CORRECTIONS"))
    assert verdicts(r) == [cpr.REFUSE] and "must read PASS or CONVERGED" in r.findings[0].message


def test_full_plan_stamped_pass_with_corrections_is_ready():
    assert run(plan_text(result="PASS WITH CORRECTIONS")).verdict == cpr.READY


def test_scope_derivation_is_addressed_to_the_lead():
    r = run(plan_text(date="2026-09-20", scope=None))
    assert verdicts(r) == [cpr.ASK]
    msg = r.findings[0].message
    assert "the LEAD answers this" in msg and "One non-chore group → proceed" in msg


def test_override_inside_an_html_comment_is_not_a_record():
    text = cell_plan(f"<!-- a note\n{GOOD}\n-->")
    assert cpr.parse_plan("p", text).overrides == []
    one_line = cell_plan(f"<!-- {GOOD} -->")
    assert cpr.parse_plan("p", one_line).overrides == []
    # positive control: the same record outside the comment is read, and the stamps still are
    plan = cpr.parse_plan("p", cell_plan(f"<!-- a note -->\n{GOOD}"))
    assert [o.line for o in plan.overrides] and plan.stamp is not None
    assert plan.stamp.adequacy == {"CELL"} and plan.stamp.result == "PASS WITH CORRECTIONS"


def test_tier_after_an_open_comment_is_not_read():
    text = plan_text(tier="full (c)").replace("- **Tier:** full (c)", "<!--\n- **Tier:** light\n-->\n- **Tier:** full (c)")
    assert cpr.parse_plan("p", text).tier == "full"


@pytest.mark.parametrize(
    "line, want",
    [
        ("- **Tier:** **light** — from charter §3.8", "light"),
        ("- Tier — light", "light"),
        ("- Tier: _light_", "light"),
        ("- **Tier:** `light`", "light"),
        ("- **Tier**: light", "light"),
    ],
)
def test_tier_spellings(line, want):
    text = plan_text().replace("- **Override:** none", line + "\n- **Override:** none")
    assert cpr.parse_plan("p", text).tier == want


def test_scope_mentioning_an_unparsed_tier_refuses():
    text = plan_text().replace("- **Override:** none", "- **Tier:** lite\n- **Override:** none")
    assert cpr.parse_plan("p", text).tier == "unreadable"
    r = run(text)
    assert verdicts(r) == [cpr.REFUSE] and "mentions a tier" in r.findings[0].message
    assert cpr.parse_plan("p", plan_text()).tier == "full"  # control: no mention, no Tier → full


def test_draft_marker_with_needs_revision_refuses():
    for tier in (None, "light"):
        r = run(plan_text(pre=DRAFT, tier=tier, result="NEEDS REVISION"))
        assert verdicts(r) == [cpr.REFUSE] and r.findings[0].message == cpr.MSG_NEEDS_REVISION


def test_json_includes_the_refs_output(tmp_path, monkeypatch, capsys):
    import json

    monkeypatch.delenv("CI", raising=False)
    p = tmp_path / "plan-x.md"
    p.write_text(plan_text(stamp=ADEQ, tier="light"))
    cpr.main([str(p), "--repo", str(tmp_path), "--offline", "--json"])
    out = json.loads(capsys.readouterr().out)
    assert "RESULT:" in out["refs_output"]
