"""Tests for build_tools/check_class_closure.py on recorded issue JSON (class_closure_fixtures.json)."""

from __future__ import annotations

import copy
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_class_closure as mod

with open(
    os.path.join(os.path.dirname(__file__), "class_closure_fixtures.json"),
    encoding="utf-8",
) as f:
    RECORDED = json.load(f)

AFTER_CUTOFF = "2026-11-12T09:00:00Z"


def with_status(body: str, statuses: list[str]) -> str:
    """Append a `status` column to the first table of `body`, one status per data row."""
    out, rows = [], iter(statuses)
    in_table = False
    for line in body.splitlines():
        if line.startswith("|") and not in_table:
            in_table = True
            out.append(line + " status |")
        elif in_table and line.startswith("|---"):
            out.append(line + "---|")
        elif in_table and line.startswith("|"):
            out.append(line + f" {next(rows)} |")
        else:
            in_table = in_table and line.startswith("|")
            out.append(line)
    return "\n".join(out)


@pytest.fixture
def tracker(monkeypatch):
    """A mutable copy of the recorded tracker, served through `fetch`; returns the copy."""
    data = copy.deepcopy(RECORDED)
    calls: list[str] = []

    def fake_fetch(path: str):
        calls.append(path)
        rest = path.split(f"repos/{mod.REPO}/issues", 1)[1]
        if rest.startswith("?"):
            return [
                i
                for i in data["issues"].values()
                if mod.LABEL in {lb["name"] for lb in i["labels"]}
            ]
        number = rest.lstrip("/").split("/")[0]
        if "/comments" in rest:
            return data["comments"].get(number, [])
        return data["issues"].get(number)

    monkeypatch.setattr(mod, "fetch", fake_fetch)
    data["calls"] = calls
    return data


def close_now(issue: dict) -> None:
    issue["state"] = "closed"
    issue["closed_at"] = AFTER_CUTOFF


def run(capsys, *argv: str) -> tuple[int, str]:
    code = mod.main(list(argv))
    out = capsys.readouterr()
    return code, out.out + out.err


def test_fully_accounted_issue_passes(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = with_status(
        issue["body"],
        [
            "fixed 80b6b9f69",
            "fixed `str_encode_errors_2477`",
            "fixed PR #2493",
            "moved #2457",
            "fixed 80b6b9f69",
            "control",
        ],
    )
    code, out = run(capsys, "2477")
    assert code == 0, out
    assert "#2477 ok" in out and "6 accounted, 0 not" in out


def test_row_with_no_status_fails(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = with_status(
        issue["body"],
        ["fixed 80b6b9f69", "", "fixed 80b6b9f69", "control", "control", "control"],
    )
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '2' (body): status '(empty)'" in out


def test_status_that_is_not_a_disposition_fails(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = with_status(issue["body"], ["fixed 80b6b9f69"] * 5 + ["to measure"])
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '6' (body): status 'to measure'" in out


def test_fixed_without_evidence_fails(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = with_status(issue["body"], ["fixed"] + ["control"] * 5)
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '1' (body): 'fixed' names no commit sha" in out


def test_row_moved_to_a_closed_issue_fails(tracker, capsys):
    issue = tracker["issues"]["2461"]
    close_now(issue)
    issue["body"] = with_status(
        issue["body"], ["fixed 75465d3ee"] * 6 + ["moved to #2438"]
    )
    tracker["comments"]["2461"] = []
    code, out = run(capsys, "2461")
    assert code == 1
    assert "row '7' (body): moved to #2438, which is closed" in out


def test_row_moved_to_an_open_issue_passes(tracker, capsys):
    issue = tracker["issues"]["2461"]
    close_now(issue)
    issue["body"] = with_status(
        issue["body"], ["fixed 75465d3ee"] * 6 + ["moved to #2457"]
    )
    tracker["comments"]["2461"] = []
    code, out = run(capsys, "2461")
    assert code == 0, out
    assert "repos/antonsynd/sharpy/issues/2457" in tracker["calls"]


def test_row_moved_to_a_pull_request_fails(tracker, capsys):
    issue = tracker["issues"]["2461"]
    close_now(issue)
    issue["body"] = with_status(
        issue["body"], ["fixed 75465d3ee"] * 6 + ["moved #2464"]
    )
    tracker["comments"]["2461"] = []
    code, out = run(capsys, "2461")
    assert code == 1
    assert "moved to #2464, which is pull request" in out


def test_comment_row_supersedes_body_row_and_adds_a_cell(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = with_status(issue["body"], ["", "", "", "", "", "control"])
    closing = "| # | cell | status |\n|---|---|---|\n| 1–5 | encode/decode cells | fixed 80b6b9f69 |\n"
    added = "| # | cell | status |\n|---|---|---|\n| 7 | `'x'.encode('idna')` | |\n"
    tracker["comments"]["2477"] = [
        {"created_at": "2026-11-12T08:00:00Z", "body": added}
    ]
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '7' (2026-11-12T08:00:00Z)" in out and "row '1' (body)" in out
    tracker["comments"]["2477"].append(
        {"created_at": "2026-11-12T08:30:00Z", "body": closing.replace("1–5", "1–5, 7")}
    )
    code, out = run(capsys, "2477")
    assert code == 0, out
    assert "7 accounted, 0 not" in out


def test_issue_with_no_table_passes(tracker, capsys):
    close_now(tracker["issues"]["2124"])
    code, out = run(capsys, "2124")
    assert code == 0, out
    assert "#2124 ok" in out and "no cell table" in out


def test_body_cell_table_without_status_column_fails(tracker, capsys):
    close_now(tracker["issues"]["2461"])
    code, out = run(capsys, "2461")
    assert code == 1
    assert (
        "body cell table (# | cell | python | sharpy | where) has no status column"
        in out
    )


def test_closure_before_cutoff_is_skipped_but_reported(tracker, capsys):
    # The real record: #2461 closed 2026-11-07 with no status column.
    code, out = run(capsys, "2461")
    assert code == 0
    assert "#2461 skip (closed 2026-11-07, before the 2026-11-12 cutoff)" in out
    assert not any("/comments" in c for c in tracker["calls"]), (
        "a skipped issue was read"
    )
    code, out = run(capsys, "--report", "2461")
    assert code == 0
    assert "#2461 FAIL" in out and "legacy table" in out
    assert "row '1' (body): no after-row" in out


def test_legacy_report_reads_after_tables(tracker, capsys):
    # The real record: #2477's closing comment restates every row in a before/after table.
    code, out = run(capsys, "--report", "2477")
    assert code == 0
    assert "#2477 ok" in out and "6 accounted, 0 not" in out


def test_open_issue_named_explicitly_is_checked(tracker, capsys):
    issue = tracker["issues"]["2477"]
    issue["state"], issue["closed_at"] = "open", None
    issue["body"] = with_status(issue["body"], ["fixed 80b6b9f69"] * 5 + [""])
    code, out = run(capsys, "2477")
    assert code == 1
    assert "#2477 FAIL (open;" in out


def test_closed_since_lists_only_class_trackers_in_window(tracker, capsys):
    close_now(tracker["issues"]["2124"])
    code, out = run(capsys, "--closed-since", "2026-11-12")
    assert code == 0, out
    assert "#2124 ok" in out
    assert "#2461" not in out and "#2477" not in out  # closed 2026-11-07 / 2026-11-08


def test_unlabelled_issue_is_skipped(tracker, capsys):
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["labels"] = []
    code, out = run(capsys, "2477")
    assert code == 0
    assert "#2477 skip (not labelled class-tracker)" in out


def test_missing_issue_exits_2(tracker, capsys):
    code, out = run(capsys, "99999")
    assert code == 2
    assert "no such issue: #99999" in out


def test_parse_tables_ignores_fenced_tables():
    text = "```\n| cell | status |\n|---|---|\n| a | |\n```\n| cell | status |\n|---|---|\n| b | control |\n"
    tables = mod.parse_tables(text)
    assert len(tables) == 1 and tables[0].rows == [["b", "control"]]


def test_escaped_pipe_stays_in_its_cell():
    (t,) = mod.parse_tables("| cell | status |\n|---|---|\n| `a \\| b` | control |\n")
    assert t.rows == [["`a | b`", "control"]]
