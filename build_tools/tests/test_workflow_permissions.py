"""Every workflow job runs with an explicit GITHUB_TOKEN permission set.

A job with no ``permissions`` of its own, in a workflow with no top-level ``permissions``, gets the
repository's default token scope, which can be read-write. CodeQL reports that per job
("Workflow does not contain permissions"); this keeps the whole class shut.
"""

from pathlib import Path

import yaml

WORKFLOWS = Path(__file__).resolve().parents[2] / ".github" / "workflows"


def jobs_without_permissions(text: str) -> list[str]:
    doc = yaml.safe_load(text)
    if "permissions" in doc:
        return []
    return [name for name, job in doc.get("jobs", {}).items() if "permissions" not in job]


def test_every_job_has_an_explicit_token_scope():
    files = sorted(WORKFLOWS.glob("*.yml")) + sorted(WORKFLOWS.glob("*.yaml"))
    assert files, "no workflow files found"
    bare = {
        f.name: missing for f in files if (missing := jobs_without_permissions(f.read_text()))
    }
    assert not bare, (
        f"jobs with no permissions block and no workflow default: {bare} - add a top-level "
        "`permissions: contents: read` and widen per job only where a step writes"
    )


def test_a_bare_job_is_reported():
    text = "on: push\njobs:\n  a:\n    permissions:\n      contents: read\n  b:\n    runs-on: x\n"
    assert jobs_without_permissions(text) == ["b"]
    assert jobs_without_permissions("permissions: {}\n" + text) == []
