"""The process-mechanism ledger (docs/process/rulings/mechanisms.yaml) keeps its shape and history.

`findings` checks one copy of the data. `lost` compares two copies: it names every attempt and
every observation of the older one that the newer one dropped or reworded, and every attempt new
in the newer one that states no prediction. The real file must be clean under both, the second
against the merge base with `origin/dev`. The mutation cases run on `sample()`, a small ledger
held here, so that appending to the real one never changes what they expect.

What this does not check: that a `predicted`, a `value` or a `note` is true. Those are prose, read
by the session that changes a mechanism and by /health-watch.
"""

from __future__ import annotations

import copy
import datetime
import os
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
# What an attempt said when it was written, and what was measured. `lost` refuses a change to these.
FROZEN_ATTEMPT = ("date", "what", "source", "predicted")
FROZEN_OBSERVED = ("date", "sha", "value", "by")
# The seed's attempts predate the ledger and may say this. An attempt added later may not.
NO_PREDICTION = "none recorded"


def _is_date(value: Any) -> bool:
    return (
        type(value) is datetime.date
    )  # a datetime passes isinstance and does not compare with a date


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
    if "date" in keys and not _is_date(row.get("date")):
        out.append(f"{where}: date is not YYYY-MM-DD")
    return out


def _dates(rows: List[Any]) -> List[datetime.date]:
    return [r["date"] for r in rows if isinstance(r, dict) and _is_date(r.get("date"))]


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
    outcome = attempt.get("outcome")
    if "outcome" in attempt and outcome not in OUTCOMES:
        out.append(f"{where}: outcome is not one of {sorted(OUTCOMES)}")
    judged = attempt.get("judged")
    if outcome in ("held", "not-held"):
        if not _is_date(judged):
            out.append(f"{where}: {outcome} without a judged date")
        else:
            if _is_date(date) and judged < date:
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


def _rows(data: Any, part: str, keys: tuple) -> List[tuple]:
    problems = data.get("problems") if isinstance(data, dict) else None
    return [
        (str(p.get("id")),) + tuple(str(row.get(key)) for key in keys)
        for p in problems or []
        if isinstance(p, dict)
        for row in p.get(part) or []
        if isinstance(row, dict)
    ]


def lost(base: Any, head: Any) -> List[str]:
    """What `head` dropped or reworded from `base`, and new attempts that predict nothing."""
    out: List[str] = []
    for part, keys, noun in (
        ("attempts", FROZEN_ATTEMPT, "attempt"),
        ("observed", FROZEN_OBSERVED, "observation"),
    ):
        kept = set(_rows(head, part, keys))
        out += [
            f"{r[0]} {noun} of {r[1]}: deleted or reworded"
            for r in _rows(base, part, keys)
            if r not in kept
        ]
    old = set(_rows(base, "attempts", FROZEN_ATTEMPT))
    blank = ("", "none", re.sub(r"[^a-z]", "", NO_PREDICTION))
    for r in _rows(head, "attempts", FROZEN_ATTEMPT):
        if r not in old and re.sub(r"[^a-z]", "", r[4].lower()) in blank:
            out.append(f"{r[0]} attempt of {r[1]}: new, with no prediction")
    return out


def _git(*args: str) -> Optional[str]:
    run = subprocess.run(["git", *args], cwd=REPO, capture_output=True, text=True)
    return run.stdout if run.returncode == 0 else None


@pytest.fixture(scope="module")
def ledger() -> Dict[str, Any]:
    return yaml.safe_load((REPO / LEDGER_PATH).read_text(encoding="utf-8"))


def test_real_ledger_is_clean(ledger):
    assert findings(ledger, REPO) == []


def test_real_ledger_lost_nothing_since_its_base(ledger):
    base_sha = (_git("merge-base", "HEAD", "origin/dev") or "").strip()
    if not base_sha:
        if os.environ.get("GITHUB_ACTIONS"):
            pytest.fail(
                "no merge base with origin/dev: the history of the ledger was not checked"
            )
        pytest.skip("no merge base with origin/dev in this checkout")
    text = _git("show", f"{base_sha}:{LEDGER_PATH}")
    if text is None:
        added = _git(
            "diff",
            "--name-only",
            "--diff-filter=A",
            base_sha,
            "HEAD",
            "--",
            LEDGER_PATH,
        )
        assert (added or "").strip() == LEDGER_PATH, (
            "the base has no ledger and this change does not add it"
        )
        pytest.skip("the base has no ledger: this change adds it")
    assert lost(yaml.safe_load(text), ledger) == []


D = datetime.date


def sample() -> Dict[str, Any]:
    """A clean ledger of two problems; every mutation case starts from a fresh copy."""

    def attempt(day: int, what: str, **more: Any) -> Dict[str, Any]:
        row = {
            "date": D(2026, 11, day),
            "what": what,
            "where": ["CLAUDE.md"],
            "source": "R-ZZ",
            "predicted": "the count falls",
            "state": "live",
            "outcome": "unjudged",
        }
        row.update(more)
        return row

    def seen(day: int, value: str) -> Dict[str, Any]:
        return {
            "date": D(2026, 11, day),
            "sha": "4b1827a8f",
            "value": value,
            "by": "a check",
        }

    return {
        "problems": [
            {
                "id": "M1",
                "name": "Plans repeat.",
                "measure": "a count",
                "wanted": "at most 2",
                "attempts": [
                    attempt(
                        8,
                        "A cap.",
                        predicted=NO_PREDICTION,
                        outcome="not-held",
                        judged=D(2026, 11, 17),
                    ),
                    attempt(17, "A stop rule.", outcome="held", judged=D(2026, 11, 27)),
                ],
                "observed": [seen(17, "6"), seen(27, "2")],
            },
            {
                "id": "M2",
                "name": "Rows pile up.",
                "measure": "a count",
                "wanted": "down",
                "attempts": [attempt(17, "A ratchet.")],
                "observed": [seen(17, "1,243")],
            },
        ]
    }


def _m1(data):
    return data["problems"][0]


def _a1(data):
    return _m1(data)["attempts"][0]


def _a2(data):
    return _m1(data)["attempts"][1]


def _case(name: str, mutate: Callable, *expected: str):
    return pytest.param(mutate, list(expected), id=name)


def test_the_sample_is_clean():
    assert findings(sample(), REPO) == []
    assert lost(sample(), sample()) == []


MUTATIONS = [
    _case(
        "duplicate id", lambda d: d["problems"][1].update(id="M1"), "M1: id used twice"
    ),
    _case("bad id", lambda d: _m1(d).update(id="P1"), "P1: id is not M<n>"),
    _case("no wanted", lambda d: _m1(d).pop("wanted"), "M1: missing wanted"),
    _case("empty name", lambda d: _m1(d).update(name=" "), "M1: name is empty"),
    _case(
        "problem is not a mapping",
        lambda d: d["problems"].append("M3"),
        "problem 3: not a mapping",
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
        "date is text",
        lambda d: _a1(d).update(date="2026-11-08"),
        "M1 attempt 1: date is not YYYY-MM-DD",
    ),
    _case(
        "date has a time",
        lambda d: _a1(d).update(date=datetime.datetime(2026, 11, 8, 10)),
        "M1 attempt 1: date is not YYYY-MM-DD",
    ),
    _case(
        "observed date has a time",
        lambda d: _m1(d)["observed"][0].update(date=datetime.datetime(2026, 11, 17)),
        "M1 observed 1: date is not YYYY-MM-DD",
        "M1 attempt 1: judged on a date with no observation",
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
        "where is text",
        lambda d: _a1(d).update(where="CLAUDE.md"),
        "M1 attempt 1: where is not a list",
        "M1 attempt 1: a live attempt names no path",
    ),
    _case(
        "outcome is a YAML boolean",
        lambda d: _a1(d).update(outcome=True),
        "M1 attempt 1: outcome is not one of ['held', 'not-held', 'unjudged']",
    ),
    _case(
        "outcome without judged",
        lambda d: _a1(d).pop("judged"),
        "M1 attempt 1: not-held without a judged date",
    ),
    _case(
        "judged has a time",
        lambda d: _a1(d).update(judged=datetime.datetime(2026, 11, 17)),
        "M1 attempt 1: not-held without a judged date",
    ),
    _case(
        "judged before the attempt",
        lambda d: _a2(d).update(judged=D(2026, 11, 1)),
        "M1 attempt 2: judged before the attempt",
        "M1 attempt 2: judged on a date with no observation",
    ),
    _case(
        "judged with no observation",
        lambda d: _a2(d).update(judged=D(2026, 11, 26)),
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
    _case(
        "observed row is not a mapping",
        lambda d: _m1(d)["observed"].append("6"),
        "M1 observed 3: not a mapping",
    ),
]


@pytest.mark.parametrize("mutate,expected", MUTATIONS)
def test_one_edit_gives_the_named_finding(mutate: Callable, expected: List[str]):
    data = sample()
    mutate(data)
    assert findings(data, REPO) == expected


def test_a_top_level_list_is_one_finding():
    assert findings([], REPO) == ["ledger: no problems"]


LOSSES = [
    _case(
        "attempt deleted",
        lambda d: _m1(d)["attempts"].pop(0),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
    _case(
        "problem deleted",
        lambda d: d["problems"].pop(1),
        "M2 attempt of 2026-11-17: deleted or reworded",
        "M2 observation of 2026-11-17: deleted or reworded",
    ),
    _case(
        "prediction rewritten",
        lambda d: _a2(d).update(predicted="the count rises"),
        "M1 attempt of 2026-11-17: deleted or reworded",
    ),
    _case(
        "seed attempt given a prediction after the fact",
        lambda d: _a1(d).update(predicted="at most two"),
        "M1 attempt of 2026-11-08: deleted or reworded",
    ),
    _case(
        "attempt swapped for one with no prediction",
        lambda d: _a1(d).update(what="A cap of three."),
        "M1 attempt of 2026-11-08: deleted or reworded",
        "M1 attempt of 2026-11-08: new, with no prediction",
    ),
    _case(
        "attempt redated",
        lambda d: _a2(d).update(date=D(2026, 11, 16)),
        "M1 attempt of 2026-11-17: deleted or reworded",
    ),
    _case(
        "observation deleted",
        lambda d: _m1(d)["observed"].pop(0),
        "M1 observation of 2026-11-17: deleted or reworded",
    ),
    _case(
        "every observation deleted",
        lambda d: [p.update(observed=[]) for p in d["problems"]],
        "M1 observation of 2026-11-17: deleted or reworded",
        "M1 observation of 2026-11-27: deleted or reworded",
        "M2 observation of 2026-11-17: deleted or reworded",
    ),
    _case(
        "measured value rewritten",
        lambda d: _m1(d)["observed"][0].update(value="2"),
        "M1 observation of 2026-11-17: deleted or reworded",
    ),
    _case(
        "new attempt with the seed's sentinel",
        lambda d: _m1(d)["attempts"].append(
            {"date": D(2026, 11, 1), "what": "Backdated.", "predicted": NO_PREDICTION}
        ),
        "M1 attempt of 2026-11-01: new, with no prediction",
    ),
    _case(
        "new attempt with the sentinel respelled",
        lambda d: _m1(d)["attempts"].append(
            {"date": D(2026, 12, 1), "what": "Later.", "predicted": "None recorded."}
        ),
        "M1 attempt of 2026-12-01: new, with no prediction",
    ),
]


@pytest.mark.parametrize("mutate,expected", LOSSES)
def test_one_loss_gives_the_named_finding(mutate: Callable, expected: List[str]):
    data = sample()
    mutate(data)
    assert lost(sample(), data) == expected


def test_appending_judging_and_removing_lose_nothing():
    data = sample()
    _m1(data)["attempts"].append(
        {
            "date": D(2026, 12, 1),
            "what": "A third rule.",
            "predicted": "passes fall to 2",
        }
    )
    _a1(data).update(state="removed", where=[], note="gone")
    _a2(data).update(outcome="not-held", judged=D(2026, 12, 2))
    _m1(data)["observed"].append(
        {"date": D(2026, 12, 2), "sha": "abcdef1", "value": "3", "by": "a check"}
    )
    _m1(data).update(wanted="at most 3")
    assert lost(sample(), data) == []


def test_a_removed_mechanism_needs_no_path():
    data = sample()
    _a1(data).update(state="removed", where=[])
    assert findings(data, REPO) == []
