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
    """A mutable copy of the recorded tracker, served through `api`; returns the copy.

    GET paths land in data["calls"], POSTs in data["posts"] as (path, payload).
    """
    data = copy.deepcopy(RECORDED)
    calls: list[str] = []
    posts: list[tuple[str, dict]] = []

    def fake_api(method: str, path: str, payload=None):
        if method == "POST":
            posts.append((path, payload))
            return {}
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

    monkeypatch.setattr(mod, "api", fake_api)
    data["calls"] = calls
    data["posts"] = posts
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
    statuses = [
        "fixed 80b6b9f69",
        "fixed `str_encode_errors_2477`",
        "fixed PR #2493",
        "moved #2457",
        "✅ fixed 80b6b9f69",
        "control",
    ]
    issue["body"] = with_status(issue["body"], statuses)
    # The real closing comment's before/after cell table, written to the convention.
    closing = tracker["comments"]["2477"][0]
    closing["body"] = with_status(closing["body"], statuses)
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
    assert (
        "row '7' (body): moved to #2438, which is closed (2026-11-07), not open at"
        in out
    )


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
    assert "moved to #2464, which is a pull request" in out


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
        "cell table (# | cell | python | sharpy | where) in body has no status column"
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


# ── Regression cases from the PR #2564 review ────────────────────────────────


def closed_2477_with(tracker, body: str, comments: list[dict] | None = None) -> None:
    issue = tracker["issues"]["2477"]
    close_now(issue)
    issue["body"] = body
    tracker["comments"]["2477"] = comments or []


def table(*rows: str, header: str = "| # | cell | status |") -> str:
    sep = "|" + "---|" * (header.count("|") - 1)
    return "\n".join([header, sep, *rows]) + "\n"


def test_acceptance_and_phase_tables_with_status_are_not_judged(tracker, capsys):
    body = (
        table("| 1 | matrix test | pending |", header="| Acceptance | guard | status |")
        + "\n"
        + table("| 2 | open |", header="| phase | status |")
        + "\n"
        + table("| 1 | `'x'.encode('ascii')` | fixed 80b6b9f69 |")
    )
    closed_2477_with(tracker, body)
    code, out = run(capsys, "2477")
    assert code == 0, out
    assert "1 accounted, 0 not" in out


def test_non_cell_row_never_overwrites_a_cell_by_key(tracker, capsys):
    later = table(
        "| 1 | guard | fixed 80b6b9f69 |", header="| # | acceptance | status |"
    )
    closed_2477_with(
        tracker,
        table("| 1 | `'x'.encode('ascii')` | |"),
        [{"created_at": "2026-11-12T08:00:00Z", "body": later}],
    )
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '1' (body): status '(empty)'" in out


def test_ledger_rows_for_another_tracker_are_skipped(tracker, capsys):
    ledger = table(
        "| W1 | #2461 | numpy reshape | |",
        "| W1 | #2477 | `'x'.encode()` | control |",
        header="| item | tracker | cell | status |",
    )
    closed_2477_with(
        tracker, "", [{"created_at": "2026-11-12T08:00:00Z", "body": ledger}]
    )
    code, out = run(capsys, "2477")
    assert code == 0, out
    assert "1 accounted, 0 not" in out


def test_same_program_different_axes_are_two_cells(tracker, capsys):
    body = table(
        "| class | `self.x += 9` | fixed 80b6b9f69 |",
        "| struct | `self.x += 9` | |",
        header="| axis values | program | status |",
    )
    closed_2477_with(tracker, body)
    code, out = run(capsys, "2477")
    assert code == 1
    assert "1 accounted, 1 not" in out
    assert "struct" in out


def test_comment_cell_table_without_status_fails(tracker, capsys):
    added = table(
        "| 2 | `date.max + timedelta(1)` | OverflowError |",
        header="| # | cell | python |",
    )
    closed_2477_with(
        tracker,
        table("| 1 | a | control |"),
        [{"created_at": "2026-11-12T08:00:00Z", "body": added}],
    )
    code, out = run(capsys, "2477")
    assert code == 1
    assert (
        "cell table (# | cell | python) in 2026-11-12T08:00:00Z has no status column"
        in out
    )


def test_pipe_less_table_is_read(tracker, capsys):
    closed_2477_with(
        tracker,
        "# | cell | status\n---|---|---\n1 | `'x'.encode('ascii')` | to measure\n",
    )
    code, out = run(capsys, "2477")
    assert code == 1
    assert "row '1' (body): status 'to measure'" in out


def test_tilde_fence_hides_a_table_and_a_backtick_fence_inside_does_not_close_it():
    text = "~~~\n```\n" + table("| hidden | |", header="| cell | status |") + "~~~\n"
    text += table("| seen | control |", header="| cell | status |")
    assert [t.rows for t in mod.parse_tables(text)] == [[["seen", "control"]]]


def test_horizontal_rule_is_not_a_table_separator():
    assert mod.parse_tables("a | b\n---\ntext\n") == []


@pytest.mark.parametrize(
    "status", ["fixed — effaced", "fixed `TBD`", "Fixed in 20261112"]
)
def test_bad_fixed_evidence_fails(tracker, capsys, status):
    closed_2477_with(tracker, table(f"| 1 | a | {status} |"))
    code, out = run(capsys, "2477")
    assert code == 1, out
    assert "names no commit sha, backticked test name or PR #N" in out


@pytest.mark.parametrize(
    "status",
    ["fixed 75465d3ee", "✔️ fixed PR #2464", "Fixed: `RoundDifferentialTests`"],
)
def test_good_fixed_evidence_passes(tracker, capsys, status):
    closed_2477_with(tracker, table(f"| 1 | a | {status} |"))
    code, out = run(capsys, "2477")
    assert code == 0, out


def test_moved_target_closed_after_the_tracker_passes(tracker, capsys):
    target = tracker["issues"]["2457"]
    target.update(state="closed", closed_at="2026-11-13T00:00:00Z")
    closed_2477_with(tracker, table("| 1 | a | moved #2457 |"))
    code, out = run(capsys, "2477")
    assert code == 0, out
    target["closed_at"] = "2026-11-12T08:59:59Z"  # one second before the tracker closed
    code, out = run(capsys, "2477")
    assert code == 1
    assert "moved to #2457, which is closed (2026-11-12), not open at" in out


def test_moved_target_closed_fails_for_an_open_tracker(tracker, capsys):
    tracker["issues"]["2457"].update(state="closed", closed_at="2026-11-13T00:00:00Z")
    issue = tracker["issues"]["2477"]
    issue.update(state="open", closed_at=None, body=table("| 1 | a | moved #2457 |"))
    tracker["comments"]["2477"] = []
    code, out = run(capsys, "2477")
    assert code == 1
    assert "not open now" in out
    assert "1 open class issue(s) would close with unaccounted cells" in out
    assert "closed with unaccounted" not in out


def test_deleted_moved_target_is_an_unaccounted_row(tracker, capsys):
    closed_2477_with(tracker, table("| 1 | a | moved #99998 |"))
    code, out = run(capsys, "2477")
    assert code == 1
    assert "moved to #99998, which does not exist (deleted?)" in out


@pytest.mark.parametrize("reason", ["duplicate", "not_planned"])
def test_duplicate_and_not_planned_closures_are_skipped(tracker, capsys, reason):
    closed_2477_with(tracker, table("| 1 | a | |"))
    tracker["issues"]["2477"]["state_reason"] = reason
    code, out = run(capsys, "2477")
    assert code == 0
    assert f"#2477 skip (closed as {reason})" in out


def test_comment_on_failure_posts_one_comment(tracker, capsys):
    closed_2477_with(tracker, table("| 1 | a | |", "| 2 | b | |"))
    code, _ = run(capsys, "--comment-on-failure", "2477")
    assert code == 1
    assert len(tracker["posts"]) == 1
    path, payload = tracker["posts"][0]
    assert path == "repos/antonsynd/sharpy/issues/2477/comments"
    assert "row '1' (body)" in payload["body"] and "row '2' (body)" in payload["body"]
    assert "To fix:" in payload["body"]


def test_comment_on_failure_is_silent_when_accounted(tracker, capsys):
    closed_2477_with(tracker, table("| 1 | a | control |"))
    code, _ = run(capsys, "--comment-on-failure", "2477")
    assert code == 0
    assert tracker["posts"] == []


def test_unreadable_tracker_exits_2_without_commenting(tracker, capsys, monkeypatch):
    def broken(method, path, payload=None):
        if method == "POST":
            tracker["posts"].append((path, payload))
        raise mod.FetchError("HTTP 502")

    monkeypatch.setattr(mod, "api", broken)
    code, out = run(capsys, "--comment-on-failure", "2477")
    assert code == 2
    assert "cannot read the tracker" in out
    assert tracker["posts"] == []


# ── api(): gh first, then REST with GH_TOKEN or GITHUB_TOKEN ─────────────────


class _Resp:
    def __init__(self, body: str):
        self._body = body

    def read(self):
        return self._body.encode()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False


def _rest(monkeypatch, *, gh: bool, gh_result=None, http_error: int | None = None):
    """Wire api()'s two transports; returns the list of REST requests made."""
    requests = []
    monkeypatch.setattr(mod.shutil, "which", lambda name: "/usr/bin/gh" if gh else None)
    if gh_result is not None:
        monkeypatch.setattr(mod.subprocess, "run", lambda *a, **k: gh_result)

    def urlopen(req, timeout):
        requests.append(req)
        if http_error:
            raise mod.urllib.error.HTTPError(req.full_url, http_error, "x", {}, None)
        return _Resp('{"number": 1}')

    monkeypatch.setattr(mod.urllib.request, "urlopen", urlopen)
    for var in ("GH_TOKEN", "GITHUB_TOKEN"):
        monkeypatch.delenv(var, raising=False)
    return requests


@pytest.mark.parametrize("var", ["GH_TOKEN", "GITHUB_TOKEN"])
def test_api_uses_either_token_without_gh(monkeypatch, var):
    requests = _rest(monkeypatch, gh=False)
    monkeypatch.setenv(var, "t0k")
    assert mod.api("GET", "repos/x/y/issues/1") == {"number": 1}
    assert requests[0].get_header("Authorization") == "Bearer t0k"


def test_api_falls_back_to_rest_when_gh_fails(monkeypatch):
    failed = mod.subprocess.CompletedProcess(
        [], 1, stdout="", stderr="HTTP 502: Bad Gateway"
    )
    requests = _rest(monkeypatch, gh=True, gh_result=failed)
    monkeypatch.setenv("GH_TOKEN", "t0k")
    assert mod.api("GET", "repos/x/y/issues/1") == {"number": 1}
    assert len(requests) == 1


def test_api_gh_failure_without_token_is_a_fetch_error(monkeypatch):
    failed = mod.subprocess.CompletedProcess(
        [], 1, stdout="", stderr="HTTP 502: Bad Gateway"
    )
    _rest(monkeypatch, gh=True, gh_result=failed)
    with pytest.raises(mod.FetchError, match="502"):
        mod.api("GET", "repos/x/y/issues/1")


@pytest.mark.parametrize("code", [404, 410])
def test_api_deleted_issue_is_none(monkeypatch, code):
    _rest(monkeypatch, gh=False, http_error=code)
    monkeypatch.setenv("GITHUB_TOKEN", "t0k")
    assert mod.api("GET", "repos/x/y/issues/1") is None


def test_api_gh_gone_is_none(monkeypatch):
    gone = mod.subprocess.CompletedProcess(
        [], 1, stdout='{"message":"This issue was deleted"}', stderr="gh: HTTP 410"
    )
    _rest(monkeypatch, gh=True, gh_result=gone)
    assert mod.api("GET", "repos/x/y/issues/1") is None
