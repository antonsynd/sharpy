"""Tests for build_tools/process_index.py — the generated one-page charter index (handoff W3.3)."""

from __future__ import annotations

from pathlib import Path

import pytest

from build_tools import process_index as mod

CHARTER = """\
# Remediation round 2099-01-01 — batching charter

**State at call 7 (2099-01-07 @ abc1234; tree clean):** **12 open = 2 parked (§3.last) + 10 batched** (note).

---

## 1. Execution order

| # | Group (§3 section) | Tier | Issues | Constraints |
|---|---|---|---|---|
| 1 | **P10 — first group** (§3.1) | full (b, c) | ~~#100~~ (CLOSED 2099-01-02) · **#101 FIRST** · #102 (R-B) | after nothing (R-E) |
| 2 | ~~**P11**~~ **DONE 2099-01-03** (§2) → **P11b — the residue** (§3.2) | light (e, R-C) | #103 (R-D) FIRST · ~~#104~~ (→ P10 at call 6) | Tests only |
| 3 | **P12 — chores row** (§3.3) | band | #105 (folds #109) FIRST · #110 (R-X; LANDED @ abc) | |
| 4 | **P13 — broken row** (§3.4) | light | #106 |
| 5 | **P14 — no section** (§3.9) | light | #107 | |
| — | parked / roadmap (2) (§3.last) | — | #900 #901 | |
| — | release | — | 1.0.0 on dev | bump first |

## 2. Done ledger

| Round | Groups | Range | Closed | Filed |
|---|---|---|---|---|
| plan-aaa (2099-01-03) | P11 — the first pass | x | #99 | — |
| plan-bbb (2099-01-04) | P12 tranche 1 | y | — | — |

## 3. Open groups

### 3.1 P10 — first group (R-A)

**Tier:** full (b, c). **Class tracker:** #101 (R-Z is not cited).

### 3.2 P11b — the residue

**Tier:** light (e).

### 3.3 P12 — chores row

**Tier:** band.

### 3.4 P13 — broken row

**Tier:** light.

### 3.last Parked / roadmap (2)

#900 #901 — standing. **#900** is R-D's parked door. **#901** (a card) stays parked.

## 4. Rulings

| Ruling | Issue | Content |
|---|---|---|
| ~~R-A~~ | #101 | ~~old text~~ Superseded by R-B. |
| ~~R-B~~ | #102 | ~~newer text~~ superseded by R-C (call 6). |
| R-C | #103 | the current text. |
| R-D | #103 | Supersedes R-E's ordering only; the rest stands. |
| R-E | #101 | the ordering and the steer. |
| not a ruling row |

## 7. History

| Date | sha | Event | Census |
|---|---|---|---|
| 2099-01-06 | 1111111 | call 6: placed | 11 = 2 + 9 |
| 2099-01-07 | abc1234 (dev) | **call 7**: placed P13 | 12 = 2 + 10 |

**Open items pending owner confirmation:** the P13 row (R-D). **Call 6 (2099-01-06):** older.
"""


@pytest.fixture()
def idx() -> mod.Index:
    return mod.parse_charter(
        CHARTER, "docs/process/rounds/remediation-round-2099-01-01-batching.md"
    )


def _group(idx: mod.Index, pos: str) -> mod.Group:
    return next(g for g in idx.groups if g.position == pos)


def test_header_reads_newest_history_row_and_census(idx):
    assert (idx.call, idx.call_date, idx.sha, idx.history_line) == (
        "7",
        "2099-01-07",
        "abc1234",
        CHARTER.splitlines().index(
            "| 2099-01-07 | abc1234 (dev) | **call 7**: placed P13 | 12 = 2 + 10 |"
        )
        + 1,
    )
    assert idx.census == "12 open = 2 parked (§3.last) + 10 batched"
    assert idx.release == "1.0.0 on dev — bump first"


def test_first_issue_is_marked(idx):
    g = _group(idx, "1")
    assert [(r.number, r.first) for r in g.issues] == [
        (100, False),
        (101, True),
        (102, False),
    ]
    # FIRST after a parenthetical still belongs to the issue before it.
    assert [
        (r.number, r.first) for r in _group(idx, "2").issues if r.state == "open"
    ] == [(103, True)]
    # An issue inside the parenthetical is neither listed nor the owner of FIRST.
    assert [(r.number, r.first) for r in _group(idx, "3").issues] == [(105, True), (110, False)]
    assert _group(idx, "3").issues[1].render() == "#110 (landed)"


def test_a_cited_ruling_missing_from_section_4_is_listed(idx):
    assert "**Cited but not in §4:** R-X" in mod.render(idx)


def test_struck_issue_is_closed_and_moved_issue_names_its_group(idx):
    assert _group(idx, "1").issues[0].render() == "~~#100~~ closed"
    moved = next(r for r in _group(idx, "2").issues if r.number == 104)
    assert (moved.state, moved.moved_to, moved.render()) == (
        "moved",
        "P10",
        "~~#104~~ → P10",
    )


def test_tier_with_letters(idx):
    assert (_group(idx, "1").tier, _group(idx, "1").letters) == ("full", "b, c")
    assert (_group(idx, "2").tier, _group(idx, "2").letters) == ("light", "e")
    assert (_group(idx, "3").tier, _group(idx, "3").letters) == ("band", "")


def test_group_identity_and_done_predecessor(idx):
    g = _group(idx, "2")
    assert (g.pids, g.name, g.sref, g.predecessor) == (
        ["P11b"],
        "the residue",
        "3.2",
        "P11 DONE",
    )
    assert g.status == "OPEN"


def test_status_reads_the_done_ledger(idx):
    assert _group(idx, "3").status.startswith(
        "OPEN; part landed: §2 plan-bbb (2099-01-04)"
    )
    # P11 is in §2, but the row's current group is P11b: no word-boundary match.
    assert "§2" not in _group(idx, "2").status


def test_two_step_supersession_chain_resolves_to_its_end(idx):
    assert idx.rulings["R-A"].superseded_by == "R-B"
    assert idx.rulings["R-B"].superseded_by == "R-C"
    assert mod.chain(idx, "R-A") == (["R-A", "R-B", "R-C"], False, "")
    # Row 1 cites R-A (its §3 heading) and R-B (the issues cell): one chain, shown once.
    assert (
        mod.current_rulings(idx, _group(idx, "1").rulings)
        == "R-A → R-B → R-C · R-E (in part → R-D)"
    )


def test_partial_supersession_keeps_the_ruling_active(idx):
    r = idx.rulings["R-E"]
    assert (r.superseded_by, r.partial) == ("", [("R-D", "R-E's ordering only")])
    text = mod.render(idx)
    assert "| R-E | R-D | R-E's ordering only |" in text
    assert "| R-A | superseded by R-B (chain R-A → R-B → R-C) |" in text


def test_rulings_come_from_row_heading_and_tier_line_only(idx):
    # R-Z sits on the Tier line after "Class tracker:" and is not a cite of the row.
    assert _group(idx, "1").rulings == ["R-A", "R-B", "R-E"]


def test_parked_section(idx):
    assert idx.parked == [(900, ["R-D"]), (901, [])]
    assert "#900 (R-D) · #901 — 2 issues" in mod.render(idx)


def test_unparsed_rows_are_listed_by_line_never_dropped(idx):
    lines = CHARTER.splitlines()
    broken = lines.index("| 4 | **P13 — broken row** (§3.4) | light | #106 |") + 1
    nosec = next(i for i, ln in enumerate(lines) if ln.startswith("| 5 |")) + 1
    bad4 = lines.index("| not a ruling row |") + 1
    got = {(u.line, u.where, u.reason) for u in idx.unparsed}
    assert got == {
        (broken, "§1", "4 cells, expected 5"),
        (nosec, "§1", "§3.9 has no §3 section"),
        (bad4, "§4", "not a `| R-xx | issue | text |` row"),
    }
    text = mod.render(idx)
    assert f"| {broken} | §1 | 4 cells, expected 5 |" in text
    assert "P13" not in [g.pids[0] for g in idx.groups if g.pids]


def test_open_items_stop_at_the_per_call_record(idx):
    assert idx.open_items == "the P13 row (R-D)."


def test_missing_section_is_an_error_naming_it():
    with pytest.raises(mod.CharterParseError, match="no `## 4.` section"):
        mod.parse_charter(CHARTER.replace("## 4. Rulings", "## 9. Rulings"), "c.md")


def test_render_is_deterministic(idx):
    assert mod.render(idx) == mod.render(mod.parse_charter(CHARTER, idx.charter))


def test_check_and_write_round_trip(tmp_path: Path, capsys):
    charter = tmp_path / "remediation-round-2099-01-01-batching.md"
    charter.write_text(CHARTER, encoding="utf-8")
    index = tmp_path / "index.md"
    assert mod.main(["--charter", str(charter), "--index", str(index), "--check"]) == 1
    assert "process_index --write" in capsys.readouterr().err
    assert mod.main(["--charter", str(charter), "--index", str(index), "--write"]) == 0
    assert mod.main(["--charter", str(charter), "--index", str(index), "--check"]) == 0
    index.write_text(
        index.read_text(encoding="utf-8") + "hand edit\n", encoding="utf-8"
    )
    assert mod.main(["--charter", str(charter), "--index", str(index), "--check"]) == 1
    assert "is stale" in capsys.readouterr().err


def test_newest_charter_is_chosen_by_the_date_in_its_name(tmp_path: Path):
    for d in ("2026-08-30", "2026-09-20"):
        (tmp_path / f"remediation-round-{d}-batching.md").write_text(
            "x", encoding="utf-8"
        )
    assert (
        mod.newest_charter(tmp_path).name == "remediation-round-2026-09-20-batching.md"
    )


# --------------------------------------------------------------------------- the real charter


def test_real_charter_parses_with_no_unparsed_order_rows():
    charter = mod.newest_charter()
    idx = mod.parse_charter(
        charter.read_text(encoding="utf-8"), mod.display_path(charter)
    )
    assert [u for u in idx.unparsed if u.where == "§1"] == []
    assert idx.call and idx.sha and idx.census
    assert len(idx.groups) > 0 and all(g.tier for g in idx.groups)
    assert len(idx.parked) > 0


def test_committed_index_is_current():
    # The CI step runs the same comparison; this keeps a local run honest too.
    assert mod.main(["--check"]) == 0, f"regenerate with `{mod.REGENERATE}`"


def test_real_charter_write_then_check_is_clean(tmp_path: Path):
    index = tmp_path / "index.md"
    assert mod.main(["--index", str(index), "--write"]) == 0
    assert mod.main(["--index", str(index), "--check"]) == 0
