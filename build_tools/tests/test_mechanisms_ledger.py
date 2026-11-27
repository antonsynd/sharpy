"""The process-mechanism ledger (docs/process/rulings/mechanisms.yaml) keeps its shape.

`findings` is the check. The real file must have none; each mutation test edits a copy of the real
data once and must produce exactly the named finding, so every rule has its control and its red.

What this does not check: that a `predicted`, a `value` or a `note` is true. Those are prose, read
by the session that changes a mechanism and by /health-watch.
"""

from __future__ import annotations

import copy
import datetime
import re
from pathlib import Path
from typing import Any, Callable, Dict, List

import pytest
import yaml

REPO = Path(__file__).resolve().parents[2]
LEDGER = REPO / "docs" / "process" / "rulings" / "mechanisms.yaml"

PROBLEM_KEYS = ("id", "name", "measure", "wanted", "attempts", "observed")
ATTEMPT_KEYS = ("date", "what", "where", "source", "predicted", "state", "held")
OBSERVED_KEYS = ("date", "sha", "value", "by")
STATES = {"live", "replaced", "removed"}
HELD = {"yes", "no", "unjudged"}

# An attempt is never deleted. The seed of 2026-11-27: each problem keeps at least this many.
SEED_ATTEMPTS = {"M1": 2, "M2": 2, "M3": 2, "M4": 1, "M5": 1, "M6": 1, "M7": 2}


def _text(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def findings(data: Dict[str, Any], repo: Path) -> List[str]:
    out: List[str] = []
    problems = data.get("problems")
    if not isinstance(problems, list) or not problems:
        return ["ledger: no problems"]
    seen: Dict[str, int] = {}
    for problem in problems:
        pid = str(problem.get("id"))
        if not re.fullmatch(r"M[1-9][0-9]*", pid):
            out.append(f"{pid}: id is not M<n>")
        if pid in seen:
            out.append(f"{pid}: id used twice")
        for key in PROBLEM_KEYS:
            if key not in problem:
                out.append(f"{pid}: missing {key}")
        for key in ("name", "measure", "wanted"):
            if key in problem and not _text(problem[key]):
                out.append(f"{pid}: {key} is empty")
        observed = problem.get("observed") or []
        observed_dates = []
        for n, row in enumerate(observed):
            where = f"{pid} observed {n + 1}"
            for key in OBSERVED_KEYS:
                if key not in row:
                    out.append(f"{where}: missing {key}")
            if not isinstance(row.get("date"), datetime.date):
                out.append(f"{where}: date is not YYYY-MM-DD")
            else:
                observed_dates.append(row["date"])
            if "sha" in row and not re.fullmatch(r"[0-9a-f]{7,40}", str(row["sha"])):
                out.append(f"{where}: sha is not a commit id")
            for key in ("value", "by"):
                if key in row and not _text(row[key]):
                    out.append(f"{where}: {key} is empty")
        if observed_dates != sorted(observed_dates):
            out.append(f"{pid}: observed is not in date order")
        attempts = problem.get("attempts") or []
        seen[pid] = len(attempts)
        if not attempts:
            out.append(f"{pid}: no attempts")
        attempt_dates = []
        for n, attempt in enumerate(attempts):
            where = f"{pid} attempt {n + 1}"
            for key in ATTEMPT_KEYS:
                if key not in attempt:
                    out.append(f"{where}: missing {key}")
            date = attempt.get("date")
            if not isinstance(date, datetime.date):
                out.append(f"{where}: date is not YYYY-MM-DD")
                date = None
            else:
                attempt_dates.append(date)
            for key in ("what", "source", "predicted"):
                if key in attempt and not _text(attempt[key]):
                    out.append(f"{where}: {key} is empty")
            state = attempt.get("state")
            if "state" in attempt and state not in STATES:
                out.append(f"{where}: state is not one of {sorted(STATES)}")
            paths = attempt.get("where")
            if "where" in attempt and not isinstance(paths, list):
                out.append(f"{where}: where is not a list")
                paths = []
            if state == "live":
                if not paths:
                    out.append(f"{where}: a live attempt names no path")
                for path in paths or []:
                    if not (repo / str(path)).exists():
                        out.append(f"{where}: live, but {path} does not exist")
            held = attempt.get("held")
            if "held" in attempt and held not in HELD:
                out.append(f"{where}: held is not one of {sorted(HELD)}")
            judged = attempt.get("judged")
            if held in ("yes", "no"):
                if not isinstance(judged, datetime.date):
                    out.append(f"{where}: held {held} without a judged date")
                else:
                    if date is not None and judged < date:
                        out.append(f"{where}: judged before the attempt")
                    if judged not in observed_dates:
                        out.append(f"{where}: judged on a date with no observation")
            elif held == "unjudged" and judged is not None:
                out.append(f"{where}: unjudged with a judged date")
        if attempt_dates != sorted(attempt_dates):
            out.append(f"{pid}: attempts are not in date order")
    for pid, floor in SEED_ATTEMPTS.items():
        if seen.get(pid, 0) < floor:
            out.append(
                f"{pid}: fewer attempts than the seed's {floor} (an attempt is never deleted)"
            )
    return out


@pytest.fixture(scope="module")
def ledger() -> Dict[str, Any]:
    return yaml.safe_load(LEDGER.read_text(encoding="utf-8"))


def test_real_ledger_is_clean(ledger):
    assert findings(ledger, REPO) == []


def _m1(data):
    return data["problems"][0]


MUTATIONS: List[tuple] = [
    (
        "duplicate id",
        lambda d: d["problems"][1].__setitem__("id", "M1"),
        [
            "M1: id used twice",
            "M2: fewer attempts than the seed's 2 (an attempt is never deleted)",
        ],
    ),
    ("no wanted", lambda d: _m1(d).pop("wanted"), ["M1: missing wanted"]),
    ("empty name", lambda d: _m1(d).__setitem__("name", " "), ["M1: name is empty"]),
    (
        "no predicted",
        lambda d: _m1(d)["attempts"][0].pop("predicted"),
        ["M1 attempt 1: missing predicted"],
    ),
    (
        "empty predicted",
        lambda d: _m1(d)["attempts"][0].__setitem__("predicted", ""),
        ["M1 attempt 1: predicted is empty"],
    ),
    (
        "bad state",
        lambda d: _m1(d)["attempts"][0].__setitem__("state", "gone"),
        ["M1 attempt 1: state is not one of ['live', 'removed', 'replaced']"],
    ),
    (
        "live path missing",
        lambda d: _m1(d)["attempts"][0]["where"].append("build_tools/no_such_check.py"),
        ["M1 attempt 1: live, but build_tools/no_such_check.py does not exist"],
    ),
    (
        "live without a path",
        lambda d: _m1(d)["attempts"][0].__setitem__("where", []),
        ["M1 attempt 1: a live attempt names no path"],
    ),
    (
        "bad held",
        lambda d: _m1(d)["attempts"][0].__setitem__("held", "mostly"),
        ["M1 attempt 1: held is not one of ['no', 'unjudged', 'yes']"],
    ),
    (
        "held without judged",
        lambda d: _m1(d)["attempts"][0].pop("judged"),
        ["M1 attempt 1: held no without a judged date"],
    ),
    (
        "judged before the attempt",
        lambda d: _m1(d)["attempts"][1].__setitem__(
            "judged", datetime.date(2026, 11, 1)
        ),
        [
            "M1 attempt 2: judged before the attempt",
            "M1 attempt 2: judged on a date with no observation",
        ],
    ),
    (
        "judged with no observation",
        lambda d: _m1(d)["attempts"][1].__setitem__(
            "judged", datetime.date(2026, 11, 26)
        ),
        ["M1 attempt 2: judged on a date with no observation"],
    ),
    (
        "unjudged with a date",
        lambda d: _m1(d)["attempts"][0].__setitem__("held", "unjudged"),
        ["M1 attempt 1: unjudged with a judged date"],
    ),
    (
        "attempt deleted",
        lambda d: _m1(d)["attempts"].pop(),
        ["M1: fewer attempts than the seed's 2 (an attempt is never deleted)"],
    ),
    (
        "problem deleted",
        lambda d: d["problems"].pop(3),
        ["M4: fewer attempts than the seed's 1 (an attempt is never deleted)"],
    ),
    (
        "attempts out of order",
        lambda d: _m1(d)["attempts"].reverse(),
        ["M1: attempts are not in date order"],
    ),
    (
        "observed out of order",
        lambda d: _m1(d)["observed"].reverse(),
        ["M1: observed is not in date order"],
    ),
    (
        "observed without a sha",
        lambda d: _m1(d)["observed"][0].pop("sha"),
        ["M1 observed 1: missing sha"],
    ),
    (
        "sha is prose",
        lambda d: _m1(d)["observed"][0].__setitem__("sha", "dev tip"),
        ["M1 observed 1: sha is not a commit id"],
    ),
    (
        "empty value",
        lambda d: _m1(d)["observed"][0].__setitem__("value", ""),
        ["M1 observed 1: value is empty"],
    ),
]


@pytest.mark.parametrize(
    "name,mutate,expected", MUTATIONS, ids=[m[0] for m in MUTATIONS]
)
def test_one_edit_gives_the_named_finding(
    ledger, name: str, mutate: Callable, expected: List[str]
):
    data = copy.deepcopy(ledger)
    mutate(data)
    assert findings(data, REPO) == expected


def test_a_removed_mechanism_keeps_its_record(ledger):
    data = copy.deepcopy(ledger)
    attempt = _m1(data)["attempts"][0]
    attempt["state"] = "removed"
    attempt["where"] = []
    assert findings(data, REPO) == []
