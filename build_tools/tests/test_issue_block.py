"""Tests for build_tools/issue_block.py: the `sharpy-issues` block of a PR description.

Every rule has a refusal and, beside it, the positive control that differs only in the rule's
subject, so a test cannot pass because the parser refuses everything.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

import pytest
import yaml

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import issue_block as mod

REPO = Path(__file__).resolve().parents[2]
F = "```"


def block(inner: str, info: str = "yaml", fence: str = F) -> str:
    return f"{fence}{info}\nsharpy-issues:\n{inner}\n{fence}\n"


def body(*parts: str) -> str:
    return "## Summary\n\nWhat changed.\n\n" + "\n".join(parts)


VALID = block("  closes: [2680]\n  files: [2741, 2742]")


# --- No block -----------------------------------------------------------------------------------


@pytest.mark.parametrize(
    "text",
    [
        None,
        "",
        "plain text, Refs #2740",
        body(f"{F}yaml\nother: [1]\n{F}"),  # a yaml fence without the key
        body(block("  closes: [1]", info="text")),  # an example of the block, not one
        body(block("  closes: [1]", info="yml")),  # only `yaml` counts
        body(block("  closes: [1]", info="")),
    ],
)
def test_no_block_is_none(text):
    assert mod.parse(text) is None


def test_a_block_inside_an_html_comment_is_not_read():
    assert mod.parse(body("<!--", VALID, "-->")) is None
    assert mod.parse(body("<!-- a template hint:", VALID + "-->")) is None
    # Positive control: the same block after the comment closes.
    assert mod.parse(body("<!-- hint -->", VALID)) == mod.IssueBlock(
        (2680,), (2741, 2742)
    )


# --- Valid blocks -------------------------------------------------------------------------------


def test_the_documented_block_parses():
    assert mod.parse(body(VALID)) == mod.IssueBlock(closes=(2680,), files=(2741, 2742))


@pytest.mark.parametrize("eol", ["\r\n", "\r"])
def test_crlf_and_cr_bodies_parse(eol):
    assert mod.parse(body(VALID).replace("\n", eol)) == mod.IssueBlock(
        (2680,), (2741, 2742)
    )


@pytest.mark.parametrize(
    "inner, expected",
    [
        ("  closes: []\n  files: []", mod.IssueBlock()),
        ("  files: [7]", mod.IssueBlock(files=(7,))),
        ("  closes: [7]", mod.IssueBlock(closes=(7,))),
        ("", mod.IssueBlock()),  # `sharpy-issues:` alone
        (
            "  closes:\n    - 1\n    - 2",
            mod.IssueBlock(closes=(1, 2)),
        ),  # block-style list
    ],
)
def test_omitted_and_empty_lists(inner, expected):
    assert mod.parse(body(block(inner))) == expected


@pytest.mark.parametrize(
    "text",
    [
        block("  closes: [5]", fence="~~~"),
        block("  closes: [5]", fence="````"),
        block("  closes: [5]", info="yaml title"),
        # Indented under a list item, as GitHub still renders it.
        "- item\n\n  ```yaml\n  sharpy-issues:\n    closes: [5]\n  ```\n",
    ],
)
def test_fence_shapes(text):
    assert mod.parse(body(text)) == mod.IssueBlock(closes=(5,))


def test_other_yaml_fences_beside_the_block_are_ignored():
    other = f"{F}yaml\nfoo: [1, 1]\n{F}\n"
    assert mod.parse(body(other, VALID, other)) == mod.IssueBlock((2680,), (2741, 2742))


# --- Malformed blocks ---------------------------------------------------------------------------


def refused(text: str, *needles: str) -> None:
    with pytest.raises(ValueError) as e:
        mod.parse(text)
    for needle in needles:
        assert needle in str(e.value), str(e.value)


@pytest.mark.parametrize(
    "item",
    [
        '"#2680"',
        "'2680'",
        "0",
        "-3",
        "true",
        "2680.0",
        "012",
        "2_680",
        "1:20",
        "0x1f",
        "null",
    ],
)
def test_only_bare_positive_decimal_numbers(item):
    refused(body(block(f"  closes: [{item}]")), "`closes`", "bare positive decimal")
    refused(body(block(f"  files: [{item}]")), "`files`", "bare positive decimal")
    # Positive control: the same position holding a bare number.
    assert mod.parse(body(block("  closes: [2680]"))) == mod.IssueBlock(closes=(2680,))


def test_an_unquoted_hash_is_a_yaml_error():
    refused(body(block("  closes: [#2680]")), "not valid YAML")


@pytest.mark.parametrize("value", ["2680", '"2680"', "{a: 1}"])
def test_a_list_is_required(value):
    refused(body(block(f"  closes: {value}")), "must be a list")
    assert mod.parse(body(block("  closes: [2680]"))) is not None


def test_a_number_twice_in_one_list():
    refused(body(block("  closes: [1, 2, 1]")), "1 more than once")
    refused(body(block("  files: [3, 3]")), "3 more than once")
    assert mod.parse(body(block("  closes: [1, 2]"))) == mod.IssueBlock(closes=(1, 2))


def test_a_number_in_both_lists():
    refused(body(block("  closes: [1, 2]\n  files: [2]")), "2 listed in both")
    assert mod.parse(body(block("  closes: [1]\n  files: [2]"))) == mod.IssueBlock(
        (1,), (2,)
    )


def test_a_duplicate_yaml_key_is_refused_not_overwritten():
    # safe_load alone keeps the last `closes` and silently drops [1].
    refused(body(block("  closes: [1]\n  closes: [2]")), "duplicate key 'closes'")
    assert mod.parse(body(block("  closes: [1, 2]"))) == mod.IssueBlock(closes=(1, 2))


def test_unknown_inner_keys():
    refused(body(block("  closes: [1]\n  fixes: [2]")), "unknown key", "fixes")
    assert mod.parse(body(block("  closes: [1]\n  files: [2]"))) is not None


def test_the_block_has_exactly_one_top_level_key():
    refused(body(f"{F}yaml\nsharpy-issues:\n  closes: [1]\nextra: 2\n{F}"), "one key")
    refused(body(f"{F}yaml\nouter:\n  sharpy-issues:\n    closes: [1]\n{F}"), "one key")
    refused(body(f"{F}yaml\n- sharpy-issues: {{closes: [1]}}\n{F}"), "one key")


def test_the_inner_value_is_a_mapping():
    refused(body(f"{F}yaml\nsharpy-issues: [1]\n{F}"), "must map")


def test_at_most_one_block():
    refused(body(VALID, block("  files: [9]")), "2 `sharpy-issues` blocks")
    assert mod.parse(body(VALID)) is not None


# --- CLI: --check -------------------------------------------------------------------------------


def run_main(capsys, argv, environ=None, fetch=None):
    kwargs = {"environ": environ or {}}
    if fetch is not None:
        kwargs["fetch"] = fetch
    code = mod.main(argv, **kwargs)
    out = capsys.readouterr()
    return code, out.out, out.err


def test_check_passes_a_valid_block_and_says_what_it_found(capsys):
    code, out, _ = run_main(capsys, ["--check"], {"PR_BODY": body(VALID)})
    assert code == 0 and out == "issue-block: closes [2680]; files [2741, 2742]\n"


@pytest.mark.parametrize("environ", [{}, {"PR_BODY": ""}, {"PR_BODY": "no block"}])
def test_check_passes_no_block(capsys, environ):
    code, out, _ = run_main(capsys, ["--check"], environ)
    assert code == 0 and "no `sharpy-issues` block" in out


def test_check_fails_a_malformed_block_with_one_error_line(capsys):
    code, out, _ = run_main(
        capsys, ["--check"], {"PR_BODY": body(block('  closes: ["#1"]'))}
    )
    assert code == 1
    lines = out.splitlines()
    assert len(lines) == 1 and lines[0].startswith("::error::issue-block: "), out


def test_check_reads_the_real_environment_by_default():
    env = {**os.environ, "PR_BODY": body(block("  closes: [1, 1]"))}
    result = subprocess.run(
        [sys.executable, str(REPO / "build_tools/issue_block.py"), "--check"],
        capture_output=True,
        text=True,
        env=env,
        check=False,
    )
    assert result.returncode == 1 and "::error::" in result.stdout, (
        result.stdout + result.stderr
    )


# --- CLI: --pr ----------------------------------------------------------------------------------


def pr_json(text, merged=True, base="dev"):
    return {
        "html_url": "https://github.com/antonsynd/sharpy/pull/2740",
        "base": {"ref": base},
        "merged": merged,
        "merge_commit_sha": "abc123",
        "body": text,
    }


def test_pr_prints_one_json_object(capsys):
    seen = []

    def fetch(n):
        seen.append(n)
        return pr_json(body(VALID))

    code, out, _ = run_main(capsys, ["--pr", "2740"], fetch=fetch)
    assert code == 0 and seen == [2740]
    assert json.loads(out) == {
        "pr": 2740,
        "url": "https://github.com/antonsynd/sharpy/pull/2740",
        "base": "dev",
        "merged": True,
        "merge_commit_sha": "abc123",
        "closes": [2680],
        "files": [2741, 2742],
    }
    assert len(out.splitlines()) == 1


@pytest.mark.parametrize("text", [None, "", "no block"])
def test_pr_without_a_block_lists_nothing(capsys, text):
    code, out, _ = run_main(
        capsys, ["--pr", "1"], fetch=lambda n: pr_json(text, merged=False)
    )
    result = json.loads(out)
    assert code == 0 and result["closes"] == [] and result["files"] == []
    assert result["merged"] is False


def test_pr_with_a_malformed_block_exits_1(capsys):
    code, out, err = run_main(
        capsys, ["--pr", "1"], fetch=lambda n: pr_json(body(block("  closes: [0]")))
    )
    assert code == 1 and out == "" and "PR #1" in err


def test_pr_exits_2_when_gh_fails(capsys):
    def fetch(n):
        raise mod.FetchError("gh api exited 1: HTTP 404")

    code, out, err = run_main(capsys, ["--pr", "1"], fetch=fetch)
    assert code == 2 and out == "" and "HTTP 404" in err


@pytest.mark.parametrize(
    "returncode, stdout, needle",
    [(1, "", "exited 1"), (0, "not json", "non-JSON"), (0, "[1]", "non-object")],
)
def test_fetch_pr_turns_gh_failures_into_fetch_errors(
    monkeypatch, returncode, stdout, needle
):
    calls = []

    def fake_run(cmd, **kwargs):
        calls.append(cmd)
        return subprocess.CompletedProcess(cmd, returncode, stdout, "boom")

    monkeypatch.setattr(mod.subprocess, "run", fake_run)
    with pytest.raises(mod.FetchError, match=needle):
        mod.fetch_pr(7)
    assert calls == [["gh", "api", "-X", "GET", "repos/antonsynd/sharpy/pulls/7"]]


def test_fetch_pr_without_gh_is_a_fetch_error(monkeypatch):
    def fake_run(cmd, **kwargs):
        raise FileNotFoundError("gh")

    monkeypatch.setattr(mod.subprocess, "run", fake_run)
    with pytest.raises(mod.FetchError):
        mod.fetch_pr(7)


# --- The workflow -------------------------------------------------------------------------------


def _issue_block_step():
    wf = yaml.safe_load((REPO / ".github/workflows/review-line.yml").read_text())
    steps = wf["jobs"]["review-line"]["steps"]
    (index,) = [i for i, s in enumerate(steps) if "issue_block.py" in s.get("run", "")]
    return steps, index


def test_the_workflow_passes_the_pr_body_through_env_only():
    steps, index = _issue_block_step()
    step = steps[index]
    assert step["env"] == {"PR_BODY": "${{ github.event.pull_request.body }}"}
    assert "${{" not in step["run"]
    assert "python3 build_tools/issue_block.py --check" in step["run"]


def test_the_workflow_checks_the_block_after_the_review_line_even_when_that_fails():
    steps, index = _issue_block_step()
    (review,) = [
        i for i, s in enumerate(steps) if "check_review_line.py" in s.get("run", "")
    ]
    assert index == review + 1
    assert steps[index]["if"] == "${{ !cancelled() }}"
