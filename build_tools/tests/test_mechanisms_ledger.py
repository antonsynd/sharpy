"""The process-mechanism ledger (docs/process/rulings/mechanisms.yaml) keeps its shape and history.

`findings` checks one copy of the data; `lost` compares two copies and names every attempt of the
older one that the newer one dropped or reworded. The real file must be clean under both, the
second against the merge base with `origin/dev`. Each mutation case edits a copy of the real data
once and must produce exactly the named finding.

What this does not check: that a `predicted`, a `value` or a `note` is true. Those are prose, read
by the session that changes a mechanism and by /health-watch.
"""

from __future__ import annotations

import copy
import datetime
import re
import subprocess
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Dict, List, Optional

import pytest
import yaml

REPO = Path(__file__).resolve().parents[2]
LEDGER_PATH = "docs/process/rulings/mechanisms.yaml"

PROBLEM_KEYS = ("id", "name", "measure", "wanted", "attempts", "observed")
ATTEMPT_KEYS = ("date", "what", "where", "source", "predicted", "state", "outcome")
OBSERVED_KEYS = ("date", "sha", "value", "by")
STATES = {"live", "replaced", "removed"}
OUTCOMES = {"held", "not-held", "unjudged"}
# What an attempt said when it was written. `lost` refuses a change to any of these.
FROZEN_KEYS = ("date", "what", "source", "predicted")
# The seed's attempts predate the ledger. One written after it states its prediction.
SEED_DATE = datetime.date(2026, 11, 27)
NO_PREDICTION = "none recorded"


def _shape(row: Any, where: str, keys: tuple, text_keys: tuple) -> List[str]:
    """Every key present, the text keys non-empty, `date` a date. A non-mapping is one finding."""
    if not isinstance(row, dict):
        return [f"{where}: not a mapping"]
    out = [f"{where}: missing {key}" for key in keys if key not in row]
    out += [
        f"{where}: {key} is empty"
        for key in text_keys
        if key in row and not (isinstance(row[key], str) and row[key].strip())
    ]
    if "date" in keys and not isinstance(row.get("date"), datetime.date):
        out.append(f"{where}: date is not YYYY-MM-DD")
    return out


def _dates(rows: List[Any]) -> List[datetime.date]:
    return [
        r["date"]
        for r in rows
        if isinstance(r, dict) and isinstance(r.get("date"), datetime.date)
    ]


def _attempt(
    attempt: Dict[str, Any], where: str, observed_dates: List[datetime.date], repo: Path
) -> List[str]:
    out: List[str] = []
    date = attempt.get("date")
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
            pure = PurePosixPath(str(path))
            if pure.is_absolute() or ".." in pure.parts or not (repo / pure).is_file():
                out.append(f"{where}: live, but {path} is not a file in the repository")
    if (
        isinstance(date, datetime.date)
        and date > SEED_DATE
        and attempt.get("predicted") == NO_PREDICTION
    ):
        out.append(f"{where}: written after the seed with no prediction")
    outcome = attempt.get("outcome")
    if "outcome" in attempt and outcome not in OUTCOMES:
        out.append(f"{where}: outcome is not one of {sorted(OUTCOMES)}")
    judged = attempt.get("judged")
    if outcome in ("held", "not-held"):
        if not isinstance(judged, datetime.date):
            out.append(f"{where}: {outcome} without a judged date")
        else:
            if isinstance(date, datetime.date) and judged < date:
                out.append(f"{where}: judged before the attempt")
            if judged not in observed_dates:
                out.append(f"{where}: judged on a date with no observation")
    elif outcome == "unjudged" and judged is not None:
        out.append(f"{where}: unjudged with a judged date")
    return out


def findings(data: Any, repo: Path) -> List[str]:
    problems = data.get("problems") if isinstance(data, dict) else None
    if not isinstance(problems, list) or not problems:
        return ["ledger: no problems"]
    out: List[str] = []
    ids = set()
    for n, problem in enumerate(problems):
        pid = (
            str(problem.get("id")) if isinstance(problem, dict) else f"problem {n + 1}"
        )
        shape = _shape(problem, pid, PROBLEM_KEYS, ("name", "measure", "wanted"))
        out += shape
        if shape == [f"{pid}: not a mapping"]:
            continue
        if not re.fullmatch(r"M[1-9][0-9]*", pid):
            out.append(f"{pid}: id is not M<n>")
        if pid in ids:
            out.append(f"{pid}: id used twice")
        ids.add(pid)
        observed = problem.get("observed")
        observed = observed if isinstance(observed, list) else []
        for k, row in enumerate(observed):
            where = f"{pid} observed {k + 1}"
            out += _shape(row, where, OBSERVED_KEYS, ("value", "by"))
            if (
                isinstance(row, dict)
                and "sha" in row
                and not re.fullmatch(r"[0-9a-f]{7,40}", str(row["sha"]))
            ):
                out.append(f"{where}: sha is not a commit id")
        observed_dates = _dates(observed)
        if observed_dates != sorted(observed_dates):
            out.append(f"{pid}: observed is not in date order")
        attempts = problem.get("attempts")
        if not isinstance(attempts, list) or not attempts:
            out.append(f"{pid}: no attempts")
            continue
        for k, attempt in enumerate(attempts):
            where = f"{pid} attempt {k + 1}"
            shape = _shape(
                attempt, where, ATTEMPT_KEYS, ("what", "source", "predicted")
            )
            out += shape
            if isinstance(attempt, dict):
                out += _attempt(attempt, where, observed_dates, repo)
        attempt_dates = _dates(attempts)
        if attempt_dates != sorted(attempt_dates):
            out.append(f"{pid}: attempts are not in date order")
    return out


def _frozen(data: Any) -> List[tuple]:
    problems = data.get("problems") if isinstance(data, dict) else None
    return [
        (str(p.get("id")),) + tuple(str(a.get(key)) for key in FROZEN_KEYS)
        for p in problems or []
        if isinstance(p, dict)
        for a in p.get("attempts") or []
        if isinstance(a, dict)
    ]


def lost(base: Any, head: Any) -> List[str]:
    """Attempts of `base` that `head` no longer carries word for word: deleted, or reworded."""
    kept = set(_frozen(head))
    return [
        f"{row[0]} attempt of {row[1]}: deleted or reworded"
        for row in _frozen(base)
        if row not in kept
    ]


def _git(*args: str) -> Optional[str]:
    run = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    return run.stdout if run.returncode == 0 else None


@pytest.fixture(scope="module")
def ledger() -> Dict[str, Any]:
    return yaml.safe_load((REPO / LEDGER_PATH).read_text(encoding="utf-8"))


def test_real_ledger_is_clean(ledger):
    assert findings(ledger, REPO) == []


def test_no_attempt_of_the_base_is_lost(ledger):
    base_sha = (_git("merge-base", "HEAD", "origin/dev") or "").strip()
    if not base_sha:
        pytest.skip("no merge base with origin/dev in this checkout")
    text = _git("show", f"{base_sha}:{LEDGER_PATH}")
    if text is None:
        pytest.skip("the base has no ledger: this change adds it")
    assert lost(yaml.safe_load(text), ledger) == []


def _m1(data):
    return data["problems"][0]


def _a1(data):
    return _m1(data)["attempts"][0]


def _a2(data):
    return _m1(data)["attempts"][1]


def _case(name: str, mutate: Callable, *expected: str):
    return pytest.param(mutate, list(expected), id=name)


MUTATIONS = [
    _case(
        "duplicate id", lambda d: d["problems"][1].update(id="M1"), "M1: id used twice"
    ),
    _case("bad id", lambda d: _m1(d).update(id="P1"), "P1: id is not M<n>"),
    _case("no wanted", lambda d: _m1(d).pop("wanted"), "M1: missing wanted"),
    _case("empty name", lambda d: _m1(d).update(name=" "), "M1: name is empty"),
    _case(
        "problem is not a mapping",
        lambda d: d["problems"].append("M8"),
        "problem 8: not a mapping",
    ),
    _case("no attempts", lambda d: _m1(d).update(attempts=[]), "M1: no attempts"),
    _case(
        "attempt is not a mapping",
        lambda d: _m1(d)["attempts"].append("a cap"),
        "M1 attempt 3: not a mapping",
    ),
    _case(
        "no predicted",
        lambda d: _a1(d).pop("predicted"),
        "M1 attempt 1: missing predicted",
    ),
    _case(
        "empty predicted",
        lambda d: _a1(d).update(predicted=""),
        "M1 attempt 1: predicted is empty",
    ),
    _case(
        "no prediction after the seed",
        lambda d: _a2(d).update(date=datetime.date(2026, 11, 28), outcome="unjudged", judged=None),
        "M1 attempt 2: written after the seed with no prediction",
    ),
    _case(
        "bad state",
        lambda d: _a1(d).update(state="gone"),
        "M1 attempt 1: state is not one of ['live', 'removed', 'replaced']",
    ),
    _case(
        "live path missing",
        lambda d: _a1(d)["where"].append("build_tools/no_such_check.py"),
        "M1 attempt 1: live, but build_tools/no_such_check.py is not a file in the repository",
    ),
    _case(
        "live path is a directory",
        lambda d: _a1(d).update(where=["build_tools"]),
        "M1 attempt 1: live, but build_tools is not a file in the repository",
    ),
    _case(
        "live path leaves the repository",
        lambda d: _a1(d).update(where=["build_tools/../CLAUDE.md"]),
        "M1 attempt 1: live, but build_tools/../CLAUDE.md is not a file in the repository",
    ),
    _case(
        "live path is absolute",
        lambda d: _a1(d).update(where=[str(REPO / "CLAUDE.md")]),
        f"M1 attempt 1: live, but {REPO / 'CLAUDE.md'} is not a file in the repository",
    ),
    _case(
        "live without a path",
        lambda d: _a1(d).update(where=[]),
        "M1 attempt 1: a live attempt names no path",
    ),
    _case(
        "bad outcome",
        lambda d: _a1(d).update(outcome=True),
        "M1 attempt 1: outcome is not one of ['held', 'not-held', 'unjudged']",
    ),
    _case(
        "outcome without judged",
        lambda d: _a1(d).pop("judged"),
        "M1 attempt 1: not-held without a judged date",
    ),
    _case(
        "judged before the attempt",
        lambda d: _a2(d).update(judged=datetime.date(2026, 11, 1)),
        "M1 attempt 2: judged before the attempt",
        "M1 attempt 2: judged on a date with no observation",
    ),
    _case(
        "judged with no observation",
        lambda d: _a2(d).update(judged=datetime.date(2026, 11, 26)),
        "M1 attempt 2: judged on a date with no observation",
    ),
    _case(
        "unjudged with a date",
        lambda d: _a1(d).update(outcome="unjudged"),
        "M1 attempt 1: unjudged with a judged date",
    ),
    _case(
        "attempts out of order",
        lambda d: _m1(d)["attempts"].reverse(),
        "M1: attempts are not in date order",
    ),
    _case(
        "observed out of order",
        lambda d: _m1(d)["observed"].reverse(),
        "M1: observed is not in date order",
    ),
    _case(
        "observed without a sha",
        lambda d: _m1(d)["observed"][0].pop("sha"),
        "M1 observed 1: missing sha",
    ),
    _case(
        "sha is prose",
        lambda d: _m1(d)["observed"][0].update(sha="dev tip"),
        "M1 observed 1: sha is not a commit id",
    ),
    _case(
        "empty value",
        lambda d: _m1(d)["observed"][0].update(value=""),
        "M1 observed 1: value is empty",
    ),
]


@pytest.mark.parametrize("mutate,expected", MUTATIONS)
def test_one_edit_gives_the_named_finding(
    ledger, mutate: Callable, expected: List[str]
):
    data = copy.deepcopy(ledger)
    mutate(data)
    assert findings(data, REPO) == expected


def test_a_top_level_list_is_one_finding():
    assert findings([], REPO) == ["ledger: no problems"]


def test_a_removed_mechanism_keeps_its_record(ledger):
    data = copy.deepcopy(ledger)
    _a1(data).update(state="removed", where=[])
    assert findings(data, REPO) == []
    assert lost(ledger, data) == []


LOSSES = [
    _case(
        "attempt deleted",
        lambda d: _m1(d)["attempts"].pop(0),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
    _case(
        "problem deleted",
        lambda d: d["problems"].pop(3),
        "M4 attempt of 2026-11-17: deleted or reworded",
    ),
    _case(
        "prediction rewritten",
        lambda d: _a1(d).update(predicted="at most two passes"),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
    _case(
        "attempt swapped for another",
        lambda d: _a1(d).update(what="A cap of three passes."),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
    _case(
        "attempt redated",
        lambda d: _a1(d).update(date=datetime.date(2026, 11, 7)),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
]


@pytest.mark.parametrize("mutate,expected", LOSSES)
def test_one_loss_gives_the_named_finding(
    ledger, mutate: Callable, expected: List[str]
):
    data = copy.deepcopy(ledger)
    mutate(data)
    assert lost(ledger, data) == expected


def test_appending_and_judging_lose_nothing(ledger):
    data = copy.deepcopy(ledger)
    _m1(data)["attempts"].append(
        {"date": datetime.date(2026, 12, 1), "what": "A third rule."}
    )
    _a1(data).update(outcome="held", note="changed", where=[])
    _m1(data)["observed"].append({"date": datetime.date(2026, 12, 2)})
    assert lost(ledger, data) == []
