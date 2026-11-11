"""
Every workflow starts when a file it reads changes, and each gate workflow's Gate job always reports.

A path filter decides whether a workflow runs at all, so a file a workflow reads but its filter
omits is a change whose tests never run: PR #2547 changed ``.python-version``, which every
``setup-python`` step reads, and the Python suite it broke never started. And a filtered workflow
that does not start never reports, so it cannot be a required check, which is how PRs merged while
their own test run was still going (#2521, #2523, #2558).

So ``dotnet10.yml`` and ``python-build-tools.yml`` start on every PR and make the path decision
inside, in a ``changes`` job whose ``RELEVANT_PATHS`` env lists the paths; their ``gate`` job
always runs and is the check the ``dev`` ruleset requires. This file pins:

* **Coverage.** For every workflow under ``.github/workflows/``, each repo file it reads in a way
  we can see mechanically (a ``python-version-file:``, a local action, a script or file named by
  path under ``build_tools/``, ``.claude/scripts/`` or ``.claude/hooks/`` in a ``run:``, a
  ``python -m build_tools...`` module) is matched by its ``RELEVANT_PATHS`` and by every
  ``paths`` filter on its ``push``/``pull_request`` triggers.
* **The gates.** Each gate workflow's pull_request trigger has no path filter, its push ``paths``
  equal ``RELEVANT_PATHS``, every other job hangs off ``changes``, and the Gate job has its exact
  name, ``if: always()``, and ``needs`` every other job. Its script is run over fake ``needs``.
* **The decision.** The ``relevant-changes`` action is run in a scratch git repository, and the
  pattern matcher this file uses is checked against ``git ls-files`` with the pathspecs the action
  builds, so the test and CI agree on what a pattern matches.
"""

from __future__ import annotations

import copy
import json
import re
import subprocess
from pathlib import Path
from typing import Dict, Iterator, List, Optional, Set, Tuple

import pytest
import yaml

REPO = Path(__file__).resolve().parents[2]
WORKFLOWS = REPO / ".github" / "workflows"
ACTION = REPO / ".github" / "actions" / "relevant-changes" / "action.yml"

GATES = {
    "dotnet10.yml": "Gate (.NET 10)",
    "python-build-tools.yml": "Gate (Python build tools)",
}

# A path a `run:` names under one of these roots is a file the workflow reads.
READ_PATH = re.compile(
    r"(?<![\w./-])((?:build_tools|\.claude/scripts|\.claude/hooks)/[\w./-]*[\w-])"
)
# `python -m build_tools.cpython_oracle ...` reads build_tools/cpython_oracle/.
READ_MODULE = re.compile(r"\bpython3?\s+-m\s+(build_tools(?:\.\w+)*)")


# --- GitHub filter patterns ---------------------------------------------------------------------


def pattern_regex(pattern: str) -> "re.Pattern[str]":
    """GitHub's filter-pattern syntax: ``*`` stays in one segment, ``**`` crosses them, and ``**/``
    also matches no directory at all (``docs/**/*.md`` matches ``docs/a.md``)."""
    out, i = [], 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif pattern.startswith("**", i):
            out.append(".*")
            i += 2
        elif pattern[i] == "*":
            out.append("[^/]*")
            i += 1
        elif pattern[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(pattern[i]))
            i += 1
    return re.compile("".join(out))


def matches(path: str, patterns: List[str]) -> bool:
    """Whether ``path`` passes a ``paths`` list: the last pattern that matches decides, and a
    leading ``!`` excludes."""
    result = False
    for p in patterns:
        negated = p.startswith("!")
        if pattern_regex(p[1:] if negated else p).fullmatch(path):
            result = not negated
    return result


def covers(read: str, patterns: List[str]) -> bool:
    """A directory counts as covered when a file inside it would be."""
    target = REPO / read
    return matches(read, patterns) or (
        target.is_dir() and matches(read.rstrip("/") + "/any-file", patterns)
    )


# --- Reading workflows --------------------------------------------------------------------------


def load(path: Path) -> dict:
    return yaml.safe_load(path.read_text())


def triggers(workflow: dict) -> dict:
    # PyYAML reads the `on:` key as True.
    on = workflow.get(True, workflow.get("on"))
    if isinstance(on, str):
        return {on: None}
    if isinstance(on, list):
        return {event: None for event in on}
    return on or {}


def relevant_paths(workflow: dict) -> Optional[List[str]]:
    env = (workflow.get("jobs", {}).get("changes") or {}).get("env") or {}
    text = env.get("RELEVANT_PATHS")
    if text is None:
        return None
    lines = [line.strip() for line in text.splitlines()]
    return [line for line in lines if line and not line.startswith("#")]


def steps_of(doc: dict) -> Iterator[dict]:
    for job in (doc.get("jobs") or {}).values():
        yield from job.get("steps") or []
    yield from (doc.get("runs") or {}).get("steps") or []


def reads(doc: dict, seen: Optional[Set[str]] = None) -> Dict[str, str]:
    """Repo-relative paths ``doc`` (a workflow or a composite action) reads, each with how."""
    seen = set() if seen is None else seen
    found: Dict[str, str] = {}
    for step in steps_of(doc):
        uses = step.get("uses", "")
        if uses.startswith("./"):
            action = uses[2:].rstrip("/")
            name = next(
                (
                    n
                    for n in ("action.yml", "action.yaml")
                    if (REPO / action / n).is_file()
                ),
                "action.yml",
            )
            found[f"{action}/{name}"] = f"uses: {uses}"
            if action not in seen and (REPO / action / name).is_file():
                seen.add(action)
                found.update(reads(load(REPO / action / name), seen))
        with_ = step.get("with") or {}
        if "python-version-file" in with_:
            found[str(with_["python-version-file"])] = "python-version-file"
        # Comment lines (shell or inline python) name files they do not read.
        run = "\n".join(
            line
            for line in (step.get("run") or "").splitlines()
            if not line.lstrip().startswith("#")
        )
        for m in READ_PATH.finditer(run):
            found[m.group(1).rstrip(".")] = "run:"
        for m in READ_MODULE.finditer(run):
            module = m.group(1).replace(".", "/")
            # `-m package` runs the package's __main__.py.
            path = (
                f"{module}/__main__.py" if (REPO / module).is_dir() else f"{module}.py"
            )
            found[path] = "python -m"
    return found


def coverage_problems(name: str, workflow: dict) -> List[str]:
    """Every file ``workflow`` reads that one of its relevance filters misses."""
    filters: List[Tuple[str, List[str], bool]] = []
    listed = relevant_paths(workflow)
    if listed is not None:
        filters.append(("RELEVANT_PATHS", listed, False))
    for event in ("push", "pull_request"):
        spec = triggers(workflow).get(event) or {}
        if "paths" in spec:
            filters.append((f"on.{event}.paths", list(spec["paths"]), False))
        if "paths-ignore" in spec:
            filters.append(
                (f"on.{event}.paths-ignore", list(spec["paths-ignore"]), True)
            )

    found = reads(workflow)
    if listed is not None:
        found[f".github/workflows/{name}"] = "the workflow itself"
    problems = []
    for read, how in sorted(found.items()):
        if not (REPO / read).exists():
            problems.append(f"{name}: reads {read} ({how}), which does not exist")
            continue
        for label, patterns, ignore in filters:
            missed = covers(read, patterns) if ignore else not covers(read, patterns)
            if missed:
                problems.append(
                    f"{name}: reads {read} ({how}) but {label} does not cover it"
                )
    return problems


def needs_of(job: dict) -> List[str]:
    needs = job.get("needs") or []
    return [needs] if isinstance(needs, str) else list(needs)


def ancestors(jobs: dict, job_id: str) -> Set[str]:
    out: Set[str] = set()
    stack = needs_of(jobs[job_id])
    while stack:
        j = stack.pop()
        if j not in out:
            out.add(j)
            stack += needs_of(jobs[j])
    return out


def gate_problems(name: str, workflow: dict) -> List[str]:
    """How a gate workflow could fail to report, or report green over a failure."""
    problems = []
    jobs = workflow["jobs"]
    on = triggers(workflow)
    pr = on.get("pull_request") or {}
    if (
        "pull_request" not in on
        or "paths" in pr
        or "paths-ignore" in pr
        or "branches" in pr
    ):
        problems.append(
            f"{name}: pull_request must be a trigger with no paths or branches filter"
        )
    listed = relevant_paths(workflow)
    if not listed:
        problems.append(f"{name}: the `changes` job has no RELEVANT_PATHS")
        return problems
    first_exclude = next(
        (i for i, p in enumerate(listed) if p.startswith("!")), len(listed)
    )
    if any(not p.startswith("!") for p in listed[first_exclude:]):
        # git pathspecs (the action) apply every exclude after every include; so does this file
        # only when the excludes come last.
        problems.append(f"{name}: RELEVANT_PATHS has an include after an exclude")
    push = on.get("push") or {}
    if push.get("paths") != listed:
        problems.append(f"{name}: on.push.paths differs from RELEVANT_PATHS")

    gates = [j for j, job in jobs.items() if job.get("name") == GATES[name]]
    if len(gates) != 1:
        return problems + [
            f"{name}: expected one job named {GATES[name]!r}, found {gates}"
        ]
    gate_id = gates[0]
    gate = jobs[gate_id]
    if str(gate.get("if", "")).replace(" ", "") not in ("${{always()}}", "always()"):
        problems.append(
            f"{name}: {gate_id} must have `if: always()`, has {gate.get('if')!r}"
        )
    others = set(jobs) - {gate_id}
    missing = others - set(needs_of(gate))
    if missing:
        problems.append(f"{name}: {gate_id} does not need {sorted(missing)}")
    env = gate.get("env") or {}
    if env.get("NEEDS") != "${{ toJSON(needs) }}":
        problems.append(
            f"{name}: {gate_id} must read every result from NEEDS: toJSON(needs)"
        )
    must = str(env.get("MUST_SUCCEED", "")).split()
    vouched = set(must)
    for j in must:
        if j not in jobs:
            problems.append(f"{name}: MUST_SUCCEED names {j}, which is not a job")
        else:
            vouched |= ancestors(jobs, j)
    unvouched = others - vouched - {"changes"}
    if unvouched:
        problems.append(
            f"{name}: on a relevant change nothing requires {sorted(unvouched)} to succeed"
        )
    for j in sorted(others - {"changes"}):
        if "changes" not in ancestors(jobs, j):
            problems.append(
                f"{name}: {j} does not hang off `changes`, so it runs on every PR"
            )
    return problems


# --- Coverage -----------------------------------------------------------------------------------


ALL_WORKFLOWS = sorted(WORKFLOWS.glob("*.y*ml"))


def test_the_workflows_are_found() -> None:
    names = {p.name for p in ALL_WORKFLOWS}
    assert set(GATES) <= names and len(names) >= 10, names


@pytest.mark.parametrize("path", ALL_WORKFLOWS, ids=lambda p: p.name)
def test_every_file_a_workflow_reads_is_in_its_filter(path: Path) -> None:
    assert coverage_problems(path.name, load(path)) == []


def test_python_suite_runs_on_the_files_its_checks_read() -> None:
    # build_tools/tests/ checks read these, which no workflow step names, so the scanner cannot see
    # them: the SemanticInfo scorecard ratchet and the allowlist severity check.
    workflow = load(WORKFLOWS / "python-build-tools.yml")
    listed = relevant_paths(workflow)
    assert listed
    allowlists = [
        str(p.relative_to(REPO)) for p in (REPO / "src").rglob("*allowlist*.txt")
    ]
    assert allowlists
    for read in [
        "src/Sharpy.Compiler/Semantic/SemanticInfo.cs",
        ".python-version",
        "CLAUDE.md",
        ".claude/settings.json",
        "docs/process/README.md",
        *allowlists,
    ]:
        assert matches(read, listed), read
    test = workflow["jobs"]["test"]["steps"]
    assert test[0]["with"]["fetch-depth"] == 0
    assert any("refs/remotes/origin/$BASE" in (step.get("run") or "") for step in test)


def test_the_scanner_reads_what_the_gate_workflows_read() -> None:
    # Positive control: a scanner that found nothing would pass every workflow.
    dotnet = reads(load(WORKFLOWS / "dotnet10.yml"))
    python = reads(load(WORKFLOWS / "python-build-tools.yml"))
    for read in (
        ".python-version",
        "build_tools/coverage_reduce.py",
        "build_tools/check_spy_staleness.sh",
        "build_tools/smoke_hello.sh",
        ".github/actions/relevant-changes/action.yml",
    ):
        assert read in dotnet, read
    for read in (
        ".python-version",
        ".claude/hooks/test-hooks.sh",
        "build_tools/process_index.py",
        "build_tools/cpython_oracle/__main__.py",
        "build_tools/cpython_oracle/dual_run.py",
        "build_tools/__main__.py",
        ".github/actions/relevant-changes/action.yml",
    ):
        assert read in python, read


def mutated(name: str) -> dict:
    return copy.deepcopy(load(WORKFLOWS / name))


def drop_listed(workflow: dict, entry: str) -> None:
    env = workflow["jobs"]["changes"]["env"]
    env["RELEVANT_PATHS"] = "\n".join(
        line for line in env["RELEVANT_PATHS"].splitlines() if line.strip() != entry
    )


@pytest.mark.parametrize("name", sorted(GATES))
def test_dropping_python_version_from_the_list_is_red(name: str) -> None:
    workflow = mutated(name)
    drop_listed(workflow, ".python-version")
    problems = coverage_problems(name, workflow)
    assert any(".python-version" in p and "RELEVANT_PATHS" in p for p in problems), (
        problems
    )


def test_a_trigger_filter_missing_a_script_is_red() -> None:
    workflow = {
        True: {"push": {"paths": ["src/**"]}},
        "jobs": {"j": {"steps": [{"run": "bash build_tools/check_spy_staleness.sh"}]}},
    }
    problems = coverage_problems("x.yml", workflow)
    assert problems == [
        "x.yml: reads build_tools/check_spy_staleness.sh (run:) but on.push.paths does not cover it"
    ]


def test_matcher_follows_github_filter_patterns() -> None:
    assert matches("src/a/b.cs", ["src/**"])
    assert matches("docs/a.md", ["docs/**/*.md"]) and matches(
        "docs/x/a.md", ["docs/**/*.md"]
    )
    assert not matches("build_tools/check_spec_blocks.sh", ["build_tools/spec_blocks*"])
    assert matches("src/A/Conformance/x-allowlist.txt", ["src/**/*allowlist*.txt"])
    assert not matches("src/A/x-allowlist.md", ["src/**/*allowlist*.txt"])
    assert not matches("a/CLAUDE.md", ["CLAUDE.md"])
    assert not matches("build_tools/a.md", ["build_tools/**", "!build_tools/**/*.md"])
    assert matches("build_tools/a.md", ["!build_tools/**/*.md", "build_tools/**"])


# --- The gates ----------------------------------------------------------------------------------


@pytest.mark.parametrize("name", sorted(GATES))
def test_the_gate_workflow_always_reports(name: str) -> None:
    assert gate_problems(name, load(WORKFLOWS / name)) == []


@pytest.mark.parametrize("name", sorted(GATES))
def test_dropping_a_job_from_the_gate_needs_is_red(name: str) -> None:
    workflow = mutated(name)
    gate = next(j for j in workflow["jobs"].values() if j.get("name") == GATES[name])
    dropped = gate["needs"].pop(1)
    problems = gate_problems(name, workflow)
    assert any(f"does not need ['{dropped}']" in p for p in problems), problems


@pytest.mark.parametrize("name", sorted(GATES))
def test_a_path_filter_on_pull_request_is_red(name: str) -> None:
    workflow = mutated(name)
    triggers(workflow)["pull_request"] = {"paths": ["src/**"]}
    assert any("pull_request" in p for p in gate_problems(name, workflow))


@pytest.mark.parametrize("name", sorted(GATES))
def test_a_gate_without_always_is_red(name: str) -> None:
    workflow = mutated(name)
    gate = next(j for j in workflow["jobs"].values() if j.get("name") == GATES[name])
    gate["if"] = "${{ success() }}"
    assert any("always()" in p for p in gate_problems(name, workflow))


def test_a_job_outside_changes_is_red() -> None:
    workflow = mutated("dotnet10.yml")
    del workflow["jobs"]["windows"]["needs"]
    assert any(
        "windows does not hang off" in p
        for p in gate_problems("dotnet10.yml", workflow)
    )


def gate_script(name: str) -> str:
    gate = next(
        j
        for j in load(WORKFLOWS / name)["jobs"].values()
        if j.get("name") == GATES[name]
    )
    return gate["steps"][-1]["run"]


def run_gate(
    name: str, results: Dict[str, str], relevant: str
) -> subprocess.CompletedProcess:
    jobs = load(WORKFLOWS / name)["jobs"]
    gate = next(j for j in jobs.values() if j.get("name") == GATES[name])
    needs = {
        j: {"result": results.get(j, "success"), "outputs": {}} for j in gate["needs"]
    }
    env = {
        "PATH": "/usr/local/bin:/usr/bin:/bin",
        "NEEDS": json.dumps(needs),
        "RELEVANT": relevant,
        "MUST_SUCCEED": gate["env"]["MUST_SUCCEED"],
    }
    return subprocess.run(
        ["bash", "-c", gate_script(name)],
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )


@pytest.mark.parametrize("name", sorted(GATES))
class TestGateScript:
    def all_(self, name: str, result: str) -> Dict[str, str]:
        jobs = load(WORKFLOWS / name)["jobs"]
        gate = next(j for j in jobs.values() if j.get("name") == GATES[name])
        return {j: result for j in gate["needs"]}

    def test_green_when_everything_succeeded(self, name: str) -> None:
        r = run_gate(name, {}, "true")
        assert r.returncode == 0, r.stdout + r.stderr
        assert "gate passed" in r.stdout

    def test_green_when_nothing_relevant_ran(self, name: str) -> None:
        results = self.all_(name, "skipped")
        results["changes"] = "success"
        r = run_gate(name, results, "false")
        assert r.returncode == 0, r.stdout + r.stderr

    @pytest.mark.parametrize("bad", ["failure", "cancelled"])
    def test_red_when_any_job_failed_under_skipped_ones(
        self, name: str, bad: str
    ) -> None:
        # The first job after `changes` fails and everything under it is skipped.
        for relevant in ("true", "false", ""):
            results = self.all_(name, "skipped")
            results["changes"] = "success"
            jobs = load(WORKFLOWS / name)["jobs"]
            failed = next(j for j in jobs if j != "changes")
            results[failed] = bad
            r = run_gate(name, results, relevant)
            assert r.returncode == 1, (relevant, r.stdout)
            assert f"{failed}={bad}" in r.stdout

    def test_red_when_changes_failed(self, name: str) -> None:
        results = self.all_(name, "skipped")
        results["changes"] = "failure"
        assert run_gate(name, results, "").returncode == 1

    def test_red_when_a_relevant_change_skipped_its_tests(self, name: str) -> None:
        results = self.all_(name, "skipped")
        results["changes"] = "success"
        r = run_gate(name, results, "true")
        assert r.returncode == 1
        assert "relevant change, but" in r.stdout


# --- The decision -------------------------------------------------------------------------------


def action_script() -> str:
    return load(ACTION)["runs"]["steps"][0]["run"]


def git(cwd: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(cwd), *args], check=True, capture_output=True, text=True
    ).stdout


@pytest.fixture
def scratch(tmp_path: Path) -> Path:
    repo = tmp_path / "repo"
    repo.mkdir()
    git(repo, "init", "-q", "-b", "main")
    git(repo, "config", "user.email", "t@example.invalid")
    git(repo, "config", "user.name", "t")
    for f in (
        "src/a.cs",
        "docs/a.md",
        "README.md",
        "src/X/Conformance/y-allowlist.txt",
    ):
        (repo / f).parent.mkdir(parents=True, exist_ok=True)
        (repo / f).write_text("0\n")
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", "base")
    return repo


def decide(repo: Path, paths: str, event: str = "pull_request") -> str:
    out = repo.parent / "out.txt"
    out.write_text("")
    env = {
        "PATH": "/usr/local/bin:/usr/bin:/bin",
        "GITHUB_OUTPUT": str(out),
        "PATHS": paths,
        "EVENT": event,
        "BEFORE": "",
    }
    r = subprocess.run(
        ["bash", "-c", action_script()],
        cwd=repo,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )
    assert r.returncode == 0, r.stdout + r.stderr
    values = dict(line.split("=", 1) for line in out.read_text().splitlines())
    return values["relevant"]


def change(repo: Path, *edits: str) -> None:
    for e in edits:
        if "->" in e:
            src, dst = e.split("->")
            (repo / dst).parent.mkdir(parents=True, exist_ok=True)
            git(repo, "mv", src, dst)
        else:
            (repo / e).parent.mkdir(parents=True, exist_ok=True)
            (repo / e).write_text("1\n")
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", "change")


LIST = "# comment\nsrc/**\n\nREADME.md\n"


def test_action_skips_a_change_outside_the_list(scratch: Path) -> None:
    change(scratch, "docs/a.md")
    assert decide(scratch, LIST) == "false"


def test_action_runs_on_a_listed_change(scratch: Path) -> None:
    change(scratch, "docs/a.md", "src/a.cs")
    assert decide(scratch, LIST) == "true"


def test_action_excludes_readme_for_the_code_output(scratch: Path) -> None:
    change(scratch, "README.md")
    assert decide(scratch, LIST) == "true"
    assert decide(scratch, LIST + "!README.md") == "false"


def test_action_sees_a_file_moved_out_of_the_list(scratch: Path) -> None:
    change(scratch, "src/a.cs->docs/b.cs")
    assert decide(scratch, LIST) == "true"


def test_action_matches_nested_globs(scratch: Path) -> None:
    change(scratch, "src/X/Conformance/y-allowlist.txt")
    assert decide(scratch, "src/**/*allowlist*.txt") == "true"
    assert decide(scratch, "src/*allowlist*.txt") == "false"


def test_action_fails_safe_on_an_unknown_range(scratch: Path) -> None:
    change(scratch, "docs/a.md")
    assert decide(scratch, LIST, event="workflow_dispatch") == "true"


def pathspecs(patterns: List[str]) -> List[str]:
    return [
        f":(glob,exclude){p[1:]}" if p.startswith("!") else f":(glob){p}"
        for p in patterns
    ]


@pytest.mark.parametrize("name", sorted(GATES))
def test_the_matcher_agrees_with_git_on_every_list(name: str) -> None:
    listed = relevant_paths(load(WORKFLOWS / name))
    assert listed
    files = git(
        REPO, "ls-files", "--cached", "--others", "--exclude-standard"
    ).splitlines()
    by_git = set(
        git(
            REPO,
            "ls-files",
            "--cached",
            "--others",
            "--exclude-standard",
            "--",
            *pathspecs(listed),
        ).splitlines()
    )
    assert by_git == {f for f in files if matches(f, listed)}
    for p in listed:
        # Each entry matches something today, so a typo is not a silent hole.
        assert any(matches(f, [p.lstrip("!")]) for f in files), p
