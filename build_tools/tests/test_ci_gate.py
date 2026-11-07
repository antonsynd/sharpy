"""
Harness for ``.claude/scripts/ci-gate`` and the workflow surface it depends on.

The script turns a CI run into the one line a plan's close-out cites, so the failure that matters
is a false green. Three things are pinned:

* **The verdict.** Each probe runs the real script against a fake ``gh`` and ``git`` on ``PATH``
  and asserts its exit code, since the exit code is what chooses between trusting CI and falling
  back to the local gate.
* **The format between the two sides.** ``ci-summary`` in ``dotnet10.yml`` prints ``GATE-COUNTS``
  and ``GATE-FAILED`` lines and the script parses them. ``TestCiSummaryContract`` runs the actual
  inline script from the workflow over fake trx files, then feeds its real output to ``ci-gate``.
* **The triggers.** Every PR and a manual dispatch must reach both gate workflows, and only PR runs
  may share a concurrency group: GitHub keeps one pending run per group, so a shared group would
  silently drop stacked ``mainline`` pushes.
"""

from __future__ import annotations

import json
import os
import re
import stat
import subprocess
import sys
import textwrap
from pathlib import Path
from typing import Dict, List, Optional

import pytest

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / ".claude" / "scripts" / "ci-gate"
DOTNET_YML = REPO / ".github" / "workflows" / "dotnet10.yml"
PYTHON_YML = REPO / ".github" / "workflows" / "python-build-tools.yml"

SHA = "64e3de8a7" + "0" * 31
DOTNET, PYTHON = 100, 200
GREEN_LOG = "CI summary\tcounts\t2026-11-07T00:00:00Z GATE-COUNTS passed=83679 failed=0 skipped=9\n"

FAKE_GH = r"""#!/usr/bin/env python3
import json, os, sys
state_path = os.environ["FAKE_STATE"]
with open(state_path) as f:
    state = json.load(f)
args = sys.argv[1:]
state["calls"].append(args)

def save():
    with open(state_path, "w") as f:
        json.dump(state, f)

def fail(message):
    save()
    sys.stderr.write(message + "\n")
    sys.exit(1)

# "flaky" fails the next N calls of one kind, which is how a transient gh failure looks.
if args[:2] == ["workflow", "run"]:
    kind = "dispatch"
elif args[:2] == ["run", "list"]:
    kind = "list"
elif "--log" in args:
    kind = "log"
elif "jobs" in args:
    kind = "jobs"
else:
    kind = "view"
if state.get("flaky", {}).get(kind, 0) > 0:
    state["flaky"][kind] -= 1
    fail("HTTP 502: Bad Gateway")

if args[:2] == ["workflow", "run"]:
    if state.get("dispatch_error"):
        fail(state["dispatch_error"])
    state["dispatched"].append(args[2])
    save()
elif args[:2] == ["run", "list"]:
    workflow = args[args.index("--workflow") + 1]
    ids = list(state["old_ids"].get(workflow, []))
    if workflow in state["dispatched"]:
        # The real run list lags a dispatch, so the new run can be missing from the first listings.
        state["listed"] = state.get("listed", 0) + 1
        if state["listed"] > state.get("list_lag", 0):
            ids += [state["new_ids"][workflow]]
    save()
    titles = state.get("titles", {})
    print(json.dumps([{"databaseId": i, "displayTitle": titles.get(str(i), "")} for i in ids]))
elif args[:2] == ["run", "view"]:
    run_id = args[2]
    if "--log" in args:
        save()
        sys.stdout.write(state["log"])
        sys.exit(0)
    if run_id not in state["runs"]:
        fail("failed to get run: HTTP 404: Not Found")
    fields = args[args.index("--json") + 1].split(",")
    run = state["runs"][run_id]
    if run.get("status_seq"):
        run["status"] = run["status_seq"].pop(0)
    save()
    out = {k: run[k] for k in fields if k in run}
    if "jobs" in fields:
        out["jobs"] = state["jobs"]
    print(json.dumps(out))
else:
    fail("unexpected gh call: " + " ".join(args))
"""

FAKE_GIT = r"""#!/usr/bin/env python3
import os, sys
args = sys.argv[1:]
if args[:2] == ["branch", "--show-current"]:
    print(os.environ.get("FAKE_BRANCH", "feature"))
elif args[0] == "ls-remote":
    tip = os.environ.get("FAKE_REMOTE_SHA", "")
    if tip:
        print(tip + "\t" + args[2])
elif args[0] == "rev-parse":
    local = os.environ.get("FAKE_LOCAL_SHA", "")
    if not local:
        sys.exit(1)
    print(local)
else:
    sys.stderr.write("unexpected git call: " + " ".join(args) + "\n")
    sys.exit(1)
"""


def run_record(
    conclusion: Optional[str] = "success", status: str = "completed", **extra
) -> Dict:
    return {
        "status": status,
        "conclusion": conclusion,
        "url": "https://example.test/run",
        **extra,
    }


class Rig:
    def __init__(self, tmp: Path) -> None:
        self.bin = tmp / "bin"
        self.bin.mkdir()
        for name, body in (("gh", FAKE_GH), ("git", FAKE_GIT)):
            path = self.bin / name
            path.write_text(body)
            path.chmod(path.stat().st_mode | stat.S_IEXEC)
        self.state_path = tmp / "state.json"
        self.state: Dict = {
            "calls": [],
            "dispatched": [],
            "old_ids": {},
            "new_ids": {"dotnet10.yml": DOTNET, "python-build-tools.yml": PYTHON},
            "runs": {str(DOTNET): run_record(), str(PYTHON): run_record()},
            "jobs": [
                {"name": "build", "databaseId": 1},
                {"name": "CI summary", "databaseId": 2},
            ],
            "log": GREEN_LOG,
        }
        self.env = {
            **os.environ,
            "PATH": f"{self.bin}{os.pathsep}{os.environ['PATH']}",
            "FAKE_STATE": str(self.state_path),
            "FAKE_REMOTE_SHA": SHA,
            "FAKE_LOCAL_SHA": SHA,
            "CI_GATE_POLL_SECONDS": "0",
            "CI_GATE_APPEAR_TIMEOUT": "2",
            "CI_GATE_START_TIMEOUT": "0",
            "CI_GATE_TOTAL_TIMEOUT": "5",
            "CI_GATE_MAX_POLL_FAILURES": "3",
        }

    def gate(self, *args: str) -> subprocess.CompletedProcess:
        self.state_path.write_text(json.dumps(self.state))
        return subprocess.run(
            [sys.executable, str(SCRIPT), *args],
            env=self.env,
            capture_output=True,
            text=True,
            timeout=30,
        )

    def calls(self) -> List[List[str]]:
        return json.loads(self.state_path.read_text())["calls"]

    def dispatched(self) -> List[str]:
        return json.loads(self.state_path.read_text())["dispatched"]


@pytest.fixture()
def rig(tmp_path: Path) -> Rig:
    return Rig(tmp_path)


class TestVerdict:
    def test_green_prints_the_citable_line(self, rig: Rig) -> None:
        result = rig.gate()
        assert result.returncode == 0, result.stderr
        assert (
            f"CI GATE GREEN: 83679/0/9 @ {SHA[:9]} (measured, run {DOTNET})"
            in result.stdout
        )
        assert rig.dispatched() == ["dotnet10.yml", "python-build-tools.yml"]

    def test_red_lists_the_failed_tests(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = run_record("failure")
        rig.state["log"] = (
            "x\ty\t2026-11-07T00:00:00Z GATE-COUNTS passed=5 failed=1 skipped=0\n"
            "x\ty\t2026-11-07T00:00:00Z GATE-FAILED Ns.Class.Method(a: 1)\n"
        )
        result = rig.gate()
        assert result.returncode == 1
        assert f"CI GATE RED: 5/1/0 @ {SHA[:9]}" in result.stdout
        assert "FAILED Ns.Class.Method(a: 1)" in result.stdout

    def test_the_scripts_own_echoed_source_is_not_a_failed_test(self, rig: Rig) -> None:
        # Actions echoes a step's script into its log, so the print statement itself is in there.
        rig.state["runs"][str(DOTNET)] = run_record("failure")
        rig.state["log"] = (
            'x\ty\t2026-11-07T00:00:00Z         print(f"GATE-FAILED {name}")\n'
            'x\ty\t2026-11-07T00:00:00Z         print(f"GATE-COUNTS passed={p} failed={f} skipped={s}")\n'
            + GREEN_LOG
        )
        result = rig.gate()
        assert result.returncode == 1
        assert "FAILED {name}" not in result.stdout
        assert "83679/0/9" in result.stdout

    def test_a_red_python_workflow_reds_a_green_dotnet_one(self, rig: Rig) -> None:
        rig.state["runs"][str(PYTHON)] = run_record("failure")
        result = rig.gate()
        assert result.returncode == 1
        assert "CI GATE RED: 83679/0/9" in result.stdout

    def test_a_success_with_no_counts_is_not_green(self, rig: Rig) -> None:
        rig.state["log"] = "nothing here\n"
        result = rig.gate()
        assert result.returncode == 3
        assert "GREEN" not in result.stdout

    def test_a_success_with_zero_tests_is_not_green(self, rig: Rig) -> None:
        # A README-only PR: ci-summary succeeds with only the windows trx, which the total omits.
        rig.state["log"] = (
            "x\ty\t2026-11-07T00:00:00Z GATE-COUNTS passed=0 failed=0 skipped=0\n"
        )
        result = rig.gate()
        assert result.returncode == 3
        assert "GREEN" not in result.stdout

    def test_a_missing_summary_job_is_not_green(self, rig: Rig) -> None:
        rig.state["jobs"] = [{"name": "build", "databaseId": 1}]
        result = rig.gate()
        assert result.returncode == 3
        assert "GREEN" not in result.stdout

    def test_a_cancelled_run_is_no_verdict(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = run_record("cancelled")
        result = rig.gate()
        assert result.returncode == 3
        assert "NO VERDICT" in result.stdout


class TestRefusals:
    def test_an_unpushed_branch_dispatches_nothing(self, rig: Rig) -> None:
        rig.env["FAKE_REMOTE_SHA"] = ""
        result = rig.gate()
        assert result.returncode == 2
        assert "not on origin" in result.stderr
        assert rig.dispatched() == []

    def test_a_tip_differing_from_local_dispatches_nothing(self, rig: Rig) -> None:
        rig.env["FAKE_LOCAL_SHA"] = "a" * 40
        result = rig.gate()
        assert result.returncode == 2
        assert "on origin" in result.stderr
        assert rig.dispatched() == []

    def test_a_ref_with_no_local_branch_gates_the_origin_tip(self, rig: Rig) -> None:
        # How a control run works: ci/control-* exists only on origin.
        rig.env["FAKE_LOCAL_SHA"] = ""
        result = rig.gate("--ref", "ci/control-abc")
        assert result.returncode == 0, result.stderr
        assert [
            "workflow",
            "run",
            "dotnet10.yml",
            "--ref",
            "ci/control-abc",
        ] in rig.calls()

    def test_a_branch_that_predates_the_trigger_says_so(self, rig: Rig) -> None:
        rig.state["dispatch_error"] = (
            "HTTP 422: Workflow does not have 'workflow_dispatch' trigger"
        )
        result = rig.gate()
        assert result.returncode == 2
        assert "merge origin/mainline" in result.stderr.replace("\n", " ")


class TestCiUnavailable:
    def test_a_run_that_never_starts_falls_back(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = run_record(None, status="queued")
        result = rig.gate()
        assert result.returncode == 3
        assert "did not start" in result.stderr

    def test_a_started_run_going_back_to_queued_is_not_an_outage(
        self, rig: Rig
    ) -> None:
        # Seen on run 42336433586. CI_GATE_START_TIMEOUT is 0 here, so the deadline has passed.
        rig.state["runs"][str(DOTNET)] = run_record(
            "success", status_seq=["in_progress", "queued", "completed"]
        )
        result = rig.gate()
        assert result.returncode == 0, result.stderr
        assert "CI GATE GREEN" in result.stdout

    def test_a_failing_dispatch_falls_back(self, rig: Rig) -> None:
        rig.state["dispatch_error"] = "HTTP 503: service unavailable"
        result = rig.gate()
        assert result.returncode == 3

    def test_a_dispatch_that_never_appears_falls_back(self, rig: Rig) -> None:
        rig.state["list_lag"] = 10**6
        rig.env["CI_GATE_APPEAR_TIMEOUT"] = "0"
        result = rig.gate()
        assert result.returncode == 3
        assert "no run appeared" in result.stderr


class TestTransientFailures:
    """CI_GATE_MAX_POLL_FAILURES is 3 here, so 2 failures are survivable and 3 are not."""

    @pytest.mark.parametrize("kind", ["dispatch", "list", "jobs", "log"])
    def test_a_transient_failure_does_not_lose_the_runs(
        self, rig: Rig, kind: str
    ) -> None:
        rig.state["flaky"] = {kind: 2}
        result = rig.gate()
        assert result.returncode == 0, result.stderr
        assert "CI GATE GREEN" in result.stdout

    @pytest.mark.parametrize("kind", ["dispatch", "list", "jobs", "log"])
    def test_an_outage_is_no_verdict_and_never_reads_as_red(
        self, rig: Rig, kind: str
    ) -> None:
        # The jobs read used to raise uncaught, and Python's exit 1 is this script's RED.
        rig.state["flaky"] = {kind: 99}
        result = rig.gate()
        assert result.returncode == 3, result.stderr
        assert "Traceback" not in result.stderr

    def test_an_unknown_run_id_is_a_usage_error_and_is_not_retried(
        self, rig: Rig
    ) -> None:
        rig.env["CI_GATE_POLL_SECONDS"] = (
            "30"  # A retry would run past the 30 s test timeout.
        )
        result = rig.gate("--run", "999")
        assert result.returncode == 2
        assert "HTTP 404" in result.stderr


def pr_run(conclusion: str = "success", title: str = "this PR") -> Dict:
    return run_record(
        conclusion,
        workflowName=".NET 10",
        headSha=SHA,
        event="pull_request",
        displayTitle=title,
    )


class TestRunSelection:
    def test_an_earlier_run_of_the_same_commit_is_not_taken_for_ours(
        self, rig: Rig
    ) -> None:
        # The old run is listed before ours appears, which is the moment a wrong pick would happen.
        rig.state["old_ids"] = {"dotnet10.yml": [50]}
        rig.state["runs"]["50"] = run_record("failure")
        rig.state["list_lag"] = 1
        result = rig.gate()
        assert result.returncode == 0, result.stdout
        assert f"run {DOTNET})" in result.stdout

    def test_report_mode_labels_a_pr_run_as_the_merge_ref(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = pr_run()
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 0, result.stderr
        assert f"@ the PR merge ref of {SHA[:9]} (measured" in result.stdout
        assert rig.dispatched() == []

    def test_report_mode_reds_on_a_red_python_run(self, rig: Rig) -> None:
        # Seen on PR #2455: run 42338092202 was red while --run printed CI GATE GREEN.
        rig.state["runs"][str(DOTNET)] = pr_run()
        rig.state["old_ids"] = {"python-build-tools.yml": [PYTHON]}
        rig.state["titles"] = {str(PYTHON): "this PR"}
        rig.state["runs"][str(PYTHON)] = run_record("failure")
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 1
        assert "GREEN" not in result.stdout
        assert "CI GATE RED" in result.stdout

    def test_report_mode_says_when_there_is_no_python_run(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = pr_run()
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 0, result.stderr
        assert "no python-build-tools.yml run" in result.stdout

    def test_report_mode_ignores_another_prs_newer_python_run(self, rig: Rig) -> None:
        # Two PRs from one branch share the sha and the event: #2453 and #2454 did.
        other = PYTHON + 100
        rig.state["runs"][str(DOTNET)] = pr_run()
        rig.state["old_ids"] = {"python-build-tools.yml": [PYTHON, other]}
        rig.state["titles"] = {str(PYTHON): "this PR", str(other): "another PR"}
        rig.state["runs"][str(other)] = run_record("failure")
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 0, result.stdout
        assert f"run {PYTHON}: success" in result.stdout
        assert str(other) not in result.stdout

    def test_report_mode_falls_back_to_the_newest_when_no_title_matches(
        self, rig: Rig
    ) -> None:
        rig.state["runs"][str(DOTNET)] = pr_run()
        rig.state["old_ids"] = {"python-build-tools.yml": [PYTHON]}
        rig.state["titles"] = {str(PYTHON): "a retitled PR"}
        rig.state["runs"][str(PYTHON)] = run_record("failure")
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 1
        assert "using this commit's newest" in result.stdout

    def test_report_mode_of_a_push_does_not_compare_titles(self, rig: Rig) -> None:
        # A dispatched run's title is its workflow's name, so titles only identify a PR.
        rig.state["runs"][str(DOTNET)] = run_record(
            workflowName=".NET 10",
            headSha=SHA,
            event="workflow_dispatch",
            displayTitle=".NET 10",
        )
        rig.state["old_ids"] = {"python-build-tools.yml": [PYTHON]}
        rig.state["titles"] = {str(PYTHON): "Python Build Tools"}
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 0, result.stdout
        assert "using this commit's newest" not in result.stdout
        assert f"run {PYTHON}: success" in result.stdout

    def test_a_readme_only_run_has_nothing_to_gate(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = pr_run()
        rig.state["log"] = (
            "x\ty\t2026-11-07T00:00:00Z GATE-COUNTS passed=0 failed=0 skipped=0\n"
            "x\ty\t2026-11-07T00:00:00Z GATE-SCOPE readme-only\n"
        )
        result = rig.gate("--run", str(DOTNET))
        assert result.returncode == 4, result.stdout
        assert "NOTHING TO GATE" in result.stdout
        assert "GREEN" not in result.stdout

    def test_a_red_readme_only_run_is_still_red(self, rig: Rig) -> None:
        # The windows job still runs on a README-only change, and it can fail.
        rig.state["runs"][str(DOTNET)] = pr_run("failure")
        rig.state["log"] = (
            "x\ty\t2026-11-07T00:00:00Z GATE-COUNTS passed=0 failed=0 skipped=0\n"
            "x\ty\t2026-11-07T00:00:00Z GATE-SCOPE readme-only\n"
        )
        assert rig.gate("--run", str(DOTNET)).returncode == 1

    def test_report_mode_refuses_another_workflow(self, rig: Rig) -> None:
        rig.state["runs"][str(DOTNET)] = run_record(
            workflowName="CodeQL", headSha=SHA, event="push"
        )
        assert rig.gate("--run", str(DOTNET)).returncode == 2


def ci_summary_script() -> str:
    """The inline python of dotnet10.yml's ci-summary job, exactly as CI runs it."""
    text = DOTNET_YML.read_text()
    match = re.search(
        r"python3 - <<'PY'\n(.*?)\n\s+PY\n", text[text.index("  ci-summary:") :], re.S
    )
    assert match, "ci-summary's inline python was not found"
    return textwrap.dedent(match.group(1))


def trx(total: int, passed: int, failed_names: List[str], skipped: int = 0) -> str:
    results = "".join(
        f'<UnitTestResult testId="f{i}" testName="{name}" outcome="Failed"/>'
        for i, name in enumerate(failed_names)
    )
    results += "".join(
        f'<UnitTestResult testId="s{i}" testName="skip{i}" outcome="NotExecuted"/>'
        for i in range(skipped)
    )
    return (
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary>'
        f'<Counters total="{total}" passed="{passed}" failed="{len(failed_names)}"/>'
        f"</ResultSummary><Results>{results}</Results></TestRun>"
    )


class TestCiSummaryContract:
    SWEEPS = [
        "interop-conformance",
        "metamorphic-corpus",
        "differential-execution",
        "format-spec-differential",
        "formatter-emit-invariance",
        "in-process-isolation",
        "name-resolution-differential",
    ]

    def summarize(
        self,
        tmp: Path,
        failed: Optional[List[str]] = None,
        windows_failed: Optional[List[str]] = None,
        readme_only: bool = False,
    ) -> subprocess.CompletedProcess:
        directory = tmp / "trx"
        directory.mkdir()
        windows_failures = windows_failed or []
        files = {"windows": trx(14, 14 - len(windows_failures), windows_failures)}
        if not readme_only:
            files.update(
                {
                    "cli": trx(10, 9, [], skipped=1),
                    "core": trx(20, 20, []),
                    "stdlib": trx(30, 30, []),
                    "compiler-shard-1": trx(40, 40 - len(failed or []), failed or []),
                    "lsp-shard-1": trx(50, 50, []),
                }
            )
            files.update({f"sweep-{s}": trx(1, 1, []) for s in self.SWEEPS})
        for name, body in files.items():
            (directory / f"{name}.trx").write_text(body)
        env = {
            **os.environ,
            "CODE": "false" if readme_only else "true",
            "GITHUB_STEP_SUMMARY": str(tmp / "summary.md"),
            "COMPILER_SHARD_1": "x",
            "LSP_SHARD_1": "x",
        }
        return subprocess.run(
            [sys.executable, "-c", ci_summary_script()],
            cwd=tmp,
            env=env,
            capture_output=True,
            text=True,
            timeout=30,
        )

    def test_counts_every_trx_once_and_leave_out_windows(self, tmp_path: Path) -> None:
        output = self.summarize(tmp_path)
        assert output.returncode == 0, output.stderr
        # 9+20+30+40+50 passed, plus 7 sweeps. Windows' 14 re-run tests the shards already count,
        # and the two per-suite "sum of shards" rows must not count anything a second time.
        assert "GATE-COUNTS passed=156 failed=0 skipped=1" in output.stdout

    def test_failed_names_are_printed(self, tmp_path: Path) -> None:
        output = self.summarize(tmp_path, failed=["Ns.Class.Method"])
        assert "GATE-COUNTS passed=155 failed=1 skipped=1" in output.stdout
        assert "GATE-FAILED Ns.Class.Method" in output.stdout

    def test_a_windows_failure_is_labelled_because_the_total_omits_it(
        self, tmp_path: Path
    ) -> None:
        output = self.summarize(tmp_path, windows_failed=["Win.Class.Method"])
        assert "GATE-COUNTS passed=156 failed=0 skipped=1" in output.stdout
        assert "GATE-FAILED [windows] Win.Class.Method" in output.stdout

    def test_only_a_readme_only_run_states_its_scope(self, tmp_path: Path) -> None:
        assert "GATE-SCOPE" not in self.summarize(tmp_path).stdout
        readme_dir = tmp_path / "readme"
        readme_dir.mkdir()
        output = self.summarize(readme_dir, readme_only=True)
        assert output.returncode == 0, output.stderr
        assert "GATE-COUNTS passed=0 failed=0 skipped=0" in output.stdout
        assert "GATE-SCOPE readme-only" in output.stdout

    def test_ci_gate_reads_a_real_readme_only_summary(
        self, tmp_path: Path, rig: Rig
    ) -> None:
        output = self.summarize(tmp_path, readme_only=True)
        rig.state["runs"][str(DOTNET)] = pr_run()
        rig.state["log"] = "".join(
            f"CI summary\tstep\t2026-11-07T00:00:00Z {line}\n"
            for line in (ci_summary_script() + "\n" + output.stdout).splitlines()
        )
        assert rig.gate("--run", str(DOTNET)).returncode == 4

    def test_the_echoed_script_alone_does_not_state_a_scope(self, rig: Rig) -> None:
        # A run that selected nothing, whose log still echoes the print statement: exit 4 here would
        # wave through the case exit 3 exists for.
        lines = [
            *ci_summary_script().splitlines(),
            "GATE-COUNTS passed=0 failed=0 skipped=0",
        ]
        rig.state["log"] = "".join(
            f"CI summary\tstep\t2026-11-07T00:00:00Z {line}\n" for line in lines
        )
        result = rig.gate()
        assert result.returncode == 3, result.stdout
        assert "NOTHING TO GATE" not in result.stdout

    def test_ci_gate_parses_what_ci_summary_really_prints(
        self, tmp_path: Path, rig: Rig
    ) -> None:
        output = self.summarize(tmp_path, failed=["Ns.Class.Method"])
        rig.state["runs"][str(DOTNET)] = run_record("failure")
        # Add the echoed script too, as a real Actions log has it, with the log's line prefix.
        rig.state["log"] = "".join(
            f"CI summary\tstep\t2026-11-07T00:00:00Z {line}\n"
            for line in (ci_summary_script() + "\n" + output.stdout).splitlines()
        )
        result = rig.gate()
        assert result.returncode == 1
        assert f"CI GATE RED: 155/1/1 @ {SHA[:9]}" in result.stdout
        failed = [
            line for line in result.stdout.splitlines() if line.startswith("FAILED ")
        ]
        assert failed == ["FAILED Ns.Class.Method"]


def on_block(path: Path) -> str:
    match = re.search(r"(?ms)^on:\n(.*?)^\S", path.read_text())
    assert match, f"no on: block in {path}"
    return match.group(1)


def event_block(path: Path, event: str) -> str:
    match = re.search(rf"(?ms)^  {event}:.*?(?=^  \S|\Z)", on_block(path))
    assert match, f"{path.name} has no {event} trigger"
    return match.group(0)


@pytest.mark.parametrize("workflow", [DOTNET_YML, PYTHON_YML], ids=lambda p: p.name)
class TestGateTriggers:
    def test_pull_requests_run_for_every_base(self, workflow: Path) -> None:
        assert "branches" not in event_block(workflow, "pull_request")

    def test_pull_requests_keep_their_path_filter(self, workflow: Path) -> None:
        assert "paths:" in event_block(workflow, "pull_request")

    def test_a_manual_dispatch_is_available(self, workflow: Path) -> None:
        assert "workflow_dispatch" in on_block(workflow)

    def test_only_pull_requests_share_a_concurrency_group(self, workflow: Path) -> None:
        text = workflow.read_text()
        assert (
            "group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.run_id }}"
            in text
        )
        assert "cancel-in-progress: ${{ github.event_name == 'pull_request' }}" in text


def test_the_python_job_gives_pytest_a_built_compiler() -> None:
    # Without SHARPYC, test_real_compiler_smoke skips on a clean checkout and guards nothing.
    text = PYTHON_YML.read_text()
    build = text.index("run: dotnet build src/Sharpy.Cli")
    pytest_step = text.index("- name: Run tests with pytest")
    pytest_env = text[pytest_step : text.index("python -m pytest", pytest_step)]
    assert build < pytest_step
    assert (
        "SHARPYC: ${{ github.workspace }}/src/Sharpy.Cli/bin/Debug/net10.0/sharpyc"
        in pytest_env
    )


def test_a_workflow_change_runs_this_harness() -> None:
    # Without this, editing ci-summary would not run the contract test above.
    for event in ("push", "pull_request"):
        assert "'.github/workflows/dotnet10.yml'" in event_block(PYTHON_YML, event)
