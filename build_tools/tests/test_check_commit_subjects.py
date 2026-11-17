"""Tests for build_tools/check_commit_subjects.py — the ≤ 72-char commit-subject gate (W3.4 / D5)."""

from __future__ import annotations

import os
import subprocess
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import check_commit_subjects as mod


def _c(subject: str, parents: int = 1, sha: str = "a" * 40) -> mod.Commit:
    return mod.Commit(sha, subject, parents)


def test_subject_at_the_limit_is_clean():
    assert mod.offences([_c("x" * 72)]) == []


def test_subject_one_over_the_limit_is_an_offence():
    long = _c("x" * 73)
    assert mod.offences([long]) == [long]


def test_merge_commit_is_exempt():
    # GitHub's own merge subject for a long branch name runs past 72.
    merge = _c(
        "Merge pull request #2465 from antonsynd/claude/w1.2-round-python-contract",
        parents=2,
    )
    assert len(merge.subject) > 72
    assert mod.offences([merge]) == []


def test_revert_commit_is_exempt():
    revert = _c('Revert "' + "x" * 80 + '"')
    assert mod.offences([revert]) == []


def test_revert_lookalike_without_quote_is_not_exempt():
    # The exemption is for git's own `Revert "..."` spelling; a hand-written long subject that
    # happens to start with Revert is still an offence (the positive control for is_exempt).
    bad = _c("Revert the " + "x" * 70)
    assert mod.offences([bad]) == [bad]


def test_custom_max():
    assert mod.offences([_c("x" * 50)], max_len=49) != []
    assert mod.offences([_c("x" * 50)], max_len=50) == []


def test_parse_rev_list_counts_parents_and_keeps_subject_verbatim():
    text = (
        "commit 1111\n1111\0p1 p2\0Merge branch 'a' into b\n"
        "commit 2222\n2222\0p1\0fix(core): a: subject with: colons\n"
        "commit 3333\n3333\0\0root commit\n"
    )
    commits = mod.parse_rev_list(text)
    assert [(c.sha, c.parent_count, c.subject) for c in commits] == [
        ("1111", 2, "Merge branch 'a' into b"),
        ("2222", 1, "fix(core): a: subject with: colons"),
        ("3333", 0, "root commit"),
    ]


# ── Against a real repository ─────────────────────────────────────────────────


def _git(repo, *args, env=None):
    base_env = {
        **os.environ,
        "GIT_AUTHOR_NAME": "t",
        "GIT_AUTHOR_EMAIL": "t@example.com",
        "GIT_COMMITTER_NAME": "t",
        "GIT_COMMITTER_EMAIL": "t@example.com",
    }
    if env:
        base_env.update(env)
    return subprocess.run(
        ["git", *args],
        cwd=repo,
        check=True,
        capture_output=True,
        text=True,
        env=base_env,
    ).stdout


@pytest.fixture
def repo(tmp_path):
    _git(tmp_path, "init", "-q", "-b", "main")
    (tmp_path / "f").write_text("0\n")
    _git(tmp_path, "add", "f")
    _git(tmp_path, "commit", "-q", "-m", "chore: base")
    return tmp_path


def _commit(repo, subject: str):
    (repo / "f").write_text(subject + "\n")
    _git(repo, "commit", "-q", "-am", subject)


def test_range_with_a_long_subject_exits_1_and_names_it(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    result = subprocess.run(
        [sys.executable, mod.__file__, f"{base}..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 1, result.stdout + result.stderr
    assert "::error::" in result.stdout and "81 chars" in result.stdout


def test_range_of_short_subjects_and_a_merge_exits_0(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): short")
    _git(repo, "checkout", "-q", "-b", "side", base)
    (repo / "g").write_text("side\n")
    _git(repo, "add", "g")
    _git(repo, "commit", "-q", "-m", "test(side): short")
    _git(repo, "checkout", "-q", "main")
    _git(
        repo,
        "merge",
        "-q",
        "--no-ff",
        "-m",
        "Merge pull request #1 from someone/" + "b" * 60,
        "side",
    )
    result = subprocess.run(
        [sys.executable, mod.__file__, f"{base}..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, result.stdout + result.stderr
    assert (
        "2 subjects checked" in result.stdout
        and "1 merge/revert exempt" in result.stdout
    )


def test_empty_range_is_clean(repo):
    result = subprocess.run(
        [sys.executable, mod.__file__, "HEAD..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0 and "0 subjects checked" in result.stdout


def test_bad_range_exits_2(repo):
    result = subprocess.run(
        [sys.executable, mod.__file__, "nosuchref..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 2


# ── The grandfather line (pre-gate history is not judged) ────────────────────


def _run(repo, *args):
    return subprocess.run(
        [sys.executable, mod.__file__, *args],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )


def test_commits_reachable_from_the_grandfather_are_not_judged(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)  # pre-gate, long
    _commit(repo, "ci: the gate lands")
    gate = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): short, after the gate")
    result = _run(repo, f"{base}..HEAD", "--grandfather", gate)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "1 subjects checked" in result.stdout
    assert f"2 pre-gate, before {gate[:9]}, not judged" in result.stdout


def test_without_the_grandfather_the_same_range_is_red(repo):
    # The positive control for the exemption: the long pre-gate subject is an offence when the
    # whole range is judged.
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    _commit(repo, "ci: the gate lands")
    _commit(repo, "fix(core): short, after the gate")
    result = _run(repo, f"{base}..HEAD", "--grandfather", "none")
    assert result.returncode == 1 and "81 chars" in result.stdout


def test_a_long_subject_after_the_grandfather_is_still_an_offence(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "ci: the gate lands")
    gate = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    result = _run(repo, f"{base}..HEAD", "--grandfather", gate)
    assert result.returncode == 1 and "81 chars" in result.stdout


def test_an_unknown_grandfather_sha_exempts_nothing(repo):
    # A clone without the gate commit (another repository, a shallow clone) judges the whole range
    # rather than failing; this is also what the default GATE_COMMIT does in this test repository.
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    result = _run(repo, f"{base}..HEAD", "--grandfather", "0" * 40)
    assert result.returncode == 1 and "81 chars" in result.stdout
    default = _run(repo, f"{base}..HEAD")
    assert default.returncode == 1 and "81 chars" in default.stdout


# ── Dependabot commits (owner, 2026-11-17) ────────────────────────────────────


def test_dependabot_authored_long_subject_is_exempt_and_a_human_one_is_not():
    long = "chore: Bump Microsoft.AspNetCore.Components.WebAssembly.DevServer from 10.0.14 to 11.0.0"
    bot = mod.Commit("a" * 40, long, 1, mod.DEPENDABOT_EMAIL)
    human = mod.Commit("b" * 40, long, 1, "someone@example.com")
    unknown = mod.Commit("c" * 40, long, 1)
    assert mod.offences([bot]) == []
    assert mod.offences([bot, human, unknown]) == [human, unknown]


def test_parse_rev_list_reads_the_author_email_when_present():
    text = (
        "commit 1111\n1111\0p1\0chore: Bump x\0" + mod.DEPENDABOT_EMAIL + "\n"
        "commit 2222\n2222\0p1\0fix: no email field\n"
    )
    bot, plain = mod.parse_rev_list(text)
    assert bot.author_email == mod.DEPENDABOT_EMAIL and bot.subject == "chore: Bump x"
    assert plain.author_email == ""


def test_range_with_a_long_dependabot_commit_exits_0_but_a_human_one_exits_1(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    subject = "chore: Bump " + "p" * 80

    def run():
        return subprocess.run(
            [sys.executable, mod.__file__, f"{base}..HEAD"],
            cwd=repo,
            capture_output=True,
            text=True,
            check=False,
        )

    (repo / "f").write_text("bot\n")
    _git(
        repo,
        "commit",
        "-q",
        "-am",
        subject,
        env={"GIT_AUTHOR_EMAIL": mod.DEPENDABOT_EMAIL},
    )
    result = run()
    assert result.returncode == 0, result.stdout + result.stderr
    # Control: the same subject from a human on top is still red.
    _commit(repo, subject)
    assert run().returncode == 1


def test_a_human_author_with_a_dependabot_committer_is_still_judged(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    (repo / "f").write_text("x\n")
    _git(
        repo,
        "commit",
        "-q",
        "-am",
        "chore: Bump " + "p" * 80,
        env={"GIT_COMMITTER_EMAIL": mod.DEPENDABOT_EMAIL},
    )
    result = subprocess.run(
        [sys.executable, mod.__file__, f"{base}..HEAD"],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 1, result.stdout + result.stderr


# ── The Dependabot exemption's keys (owner ruling 2026-11-17: PR author login + `chore: Bump` shape) ──

LONG_BUMP = "chore: Bump Microsoft.AspNetCore.Components.WebAssembly.DevServer from 10.0.14 to 11.0.0"
LONG_OTHER = "chore: bump Microsoft.AspNetCore.Components.WebAssembly.DevServer to 11 by hand"
PR_AUTHORS = {
    "dependabot": "dependabot[bot]",
    "human": "someone",
    "lookalike": "dependabot",
    "push": None,
}
SUBJECTS = {"bump": LONG_BUMP, "other": LONG_OTHER}
EMAILS = {"dependabot": mod.DEPENDABOT_EMAIL, "other": "someone@example.com"}


def _expected_exempt(author: str, subject: str, email: str, on_judged: bool) -> bool:
    """The rule, written once: shape and email always; on a PR run also the login or a judged branch."""
    return (
        subject == "bump"
        and email == "dependabot"
        and (author in ("dependabot", "push") or on_judged)
    )


@pytest.mark.parametrize("on_judged", [False, True])
@pytest.mark.parametrize("email", sorted(EMAILS))
@pytest.mark.parametrize("subject", sorted(SUBJECTS))
@pytest.mark.parametrize("author", sorted(PR_AUTHORS))
def test_dependabot_exemption_table(author, subject, email, on_judged):
    c = mod.Commit("d" * 40, SUBJECTS[subject], 1, EMAILS[email])
    judged = frozenset({c.sha}) if on_judged else frozenset()
    want = _expected_exempt(author, subject, email, on_judged)
    assert mod.is_exempt(c, PR_AUTHORS[author], judged) is want
    assert (mod.offences([c], pr_author=PR_AUTHORS[author], judged=judged) == []) is want


@pytest.mark.parametrize(
    "subject",
    [
        # Every shape in dev/mainline history (2026-11-17), the numbers as written there.
        "chore: Bump CsCheck from 4.9.1 to 5.0.0",
        "chore: Bump Microsoft.AspNetCore.Components.WebAssembly.DevServer from 10.0.13 to 10.0.14",
        "chore: Bump the test-dependencies group with 1 update",
        "chore: Bump the roslyn group with 2 updates",
        "chore: Bump qs",
        "chore: Bump @azure/msal-node",
        "chore(deps-dev): Bump the npm_and_yarn group across 1 directory with 3 updates",
    ],
)
def test_every_dependabot_shape_in_history_matches(subject):
    assert mod.DEPENDABOT_SUBJECT.fullmatch(subject)


@pytest.mark.parametrize(
    "subject",
    [
        "chore: bump x from 1 to 2",
        "chore: Bump x from 1 to 2 " + "y" * 60,
        "chore(core): Bump x from 1 to 2",
        "fix: Bump x from 1 to 2",
        "chore: Bump the roslyn group with many updates",
        "chore: Bump " + "x " * 40,
        "chore: Bump x from 1 to 2\n",
    ],
)
def test_lookalike_subjects_do_not_match(subject):
    assert not mod.DEPENDABOT_SUBJECT.fullmatch(subject)


# ── The workflow's own run step, executed against a scratch repository ──────────────────────────

WORKFLOW = os.path.join(
    os.path.dirname(__file__), "..", "..", ".github", "workflows", "commit-subjects.yml"
)


def _workflow_step():
    import yaml

    with open(WORKFLOW) as f:
        wf = yaml.safe_load(f)
    (step,) = [
        s
        for s in wf["jobs"]["subjects"]["steps"]
        if "check_commit_subjects" in s.get("run", "")
    ]
    return step


def _run_workflow_step(repo, env):
    """Run the step's `run:` text with bash as Actions does (`bash -e`), in `repo`, with `env`."""
    tools = repo / "build_tools"
    tools.mkdir(exist_ok=True)
    here = os.path.dirname(os.path.abspath(mod.__file__))
    for name in ("check_commit_subjects.sh", "check_commit_subjects.py"):
        if not (tools / name).exists():
            os.symlink(os.path.join(here, name), tools / name)
    return subprocess.run(
        ["bash", "-e", "-c", _workflow_step()["run"]],
        cwd=repo,
        capture_output=True,
        text=True,
        check=False,
        env={**os.environ, **env},
    )


def test_the_workflow_passes_the_pr_author_through_env_only():
    step = _workflow_step()
    assert step["env"]["PR_AUTHOR"] == "${{ github.event.pull_request.user.login }}"
    assert "${{" not in step["run"]


def _bot_commit(repo, subject, email=mod.DEPENDABOT_EMAIL):
    (repo / "f").write_text(subject + email + "\n")
    _git(repo, "commit", "-q", "-am", subject, env={"GIT_AUTHOR_EMAIL": email})
    return _git(repo, "rev-parse", "HEAD").strip()


def _pr_env(base, head, author):
    return {"EVENT": "pull_request", "BASE_SHA": base, "HEAD_SHA": head, "PR_AUTHOR": author}


def test_a_forged_dependabot_author_in_a_human_pr_is_red(repo):
    # PR #2720's hole: any session could set the author email and exempt its own long subject.
    base = _git(repo, "rev-parse", "HEAD").strip()
    head = _bot_commit(repo, LONG_BUMP)
    result = _run_workflow_step(repo, _pr_env(base, head, "someone"))
    assert result.returncode == 1 and "::error::" in result.stdout, (
        result.stdout + result.stderr
    )
    # Positive control: the same commit in Dependabot's own PR.
    result = _run_workflow_step(repo, _pr_env(base, head, "dependabot[bot]"))
    assert result.returncode == 0 and "1 merge/revert exempt" in result.stdout, (
        result.stdout + result.stderr
    )


def test_a_dependabot_pr_with_a_merge_from_dev_is_green(repo):
    # The shape of PR #2708: Dependabot's commit plus the owner's "Merge branch 'dev' into ..." merge.
    base = _git(repo, "rev-parse", "HEAD").strip()
    _git(repo, "checkout", "-q", "-b", "dependabot/nuget/x")
    _bot_commit(repo, LONG_BUMP)
    _git(repo, "checkout", "-q", "main")
    (repo / "g").write_text("dev moves on\n")
    _git(repo, "add", "g")
    _git(repo, "commit", "-q", "-m", "fix(core): dev moves on")
    _git(repo, "checkout", "-q", "dependabot/nuget/x")
    _git(
        repo,
        "merge",
        "-q",
        "--no-ff",
        "-m",
        "Merge branch 'main' into dependabot/nuget/x",
        "main",
    )
    head = _git(repo, "rev-parse", "HEAD").strip()
    result = _run_workflow_step(repo, _pr_env(base, head, "dependabot[bot]"))
    assert result.returncode == 0, result.stdout + result.stderr
    assert "2 merge/revert exempt" in result.stdout, result.stdout


def test_a_promotion_pr_carrying_a_judged_dependabot_commit_is_green(repo):
    # dev -> mainline (or a mainline -> dev sync) is opened by a human and carries Dependabot's
    # commits that their own PR runs judged: exempt because they are already on origin/dev.
    base = _git(repo, "rev-parse", "HEAD").strip()
    head = _bot_commit(repo, LONG_BUMP)
    assert _run_workflow_step(repo, _pr_env(base, head, "owner")).returncode == 1
    _git(repo, "update-ref", "refs/remotes/origin/dev", head)
    result = _run_workflow_step(repo, _pr_env(base, head, "owner"))
    assert result.returncode == 0, result.stdout + result.stderr
    # Also from origin/mainline (a sync of mainline into dev).
    _git(repo, "update-ref", "-d", "refs/remotes/origin/dev")
    _git(repo, "update-ref", "refs/remotes/origin/mainline", head)
    assert _run_workflow_step(repo, _pr_env(base, head, "owner")).returncode == 0
    # A judged branch does not exempt a long subject under another email.
    head2 = _bot_commit(repo, LONG_BUMP, "x@example.com")
    _git(repo, "update-ref", "refs/remotes/origin/mainline", head2)
    assert _run_workflow_step(repo, _pr_env(base, head2, "owner")).returncode == 1


def test_the_push_run_keys_on_shape_and_email(repo):
    before = _git(repo, "rev-parse", "HEAD").strip()
    after = _bot_commit(repo, LONG_BUMP)
    env = {"EVENT": "push", "BEFORE": before, "AFTER": after}
    result = _run_workflow_step(repo, env)
    assert result.returncode == 0 and "1 merge/revert exempt" in result.stdout, (
        result.stdout + result.stderr
    )
    # Controls: Dependabot's email on another shape, and the shape under another email, are red.
    other_shape = _bot_commit(repo, LONG_OTHER)
    assert (
        _run_workflow_step(repo, {**env, "BEFORE": after, "AFTER": other_shape}).returncode
        == 1
    )
    other_email = _bot_commit(repo, LONG_BUMP, "x@example.com")
    assert (
        _run_workflow_step(
            repo, {**env, "BEFORE": other_shape, "AFTER": other_email}
        ).returncode
        == 1
    )


def test_a_new_branch_push_checks_nothing(repo):
    head = _git(repo, "rev-parse", "HEAD").strip()
    result = _run_workflow_step(repo, {"EVENT": "push", "BEFORE": "0" * 40, "AFTER": head})
    assert result.returncode == 0 and "no range to check" in result.stdout


def test_the_grandfather_still_applies_on_a_pr_run(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    _commit(repo, "fix(core): " + "y" * 70)
    gate = _git(repo, "rev-parse", "HEAD").strip()
    result = _run(repo, f"{base}..HEAD", "--grandfather", gate, "--pr-author", "someone")
    assert result.returncode == 0 and "1 pre-gate" in result.stdout, result.stdout
    none = _run(repo, f"{base}..HEAD", "--grandfather", "none", "--pr-author", "someone")
    assert none.returncode == 1


@pytest.mark.parametrize("shadow", ["refs/tags/origin/dev", "refs/heads/origin/dev"])
def test_a_tag_or_branch_named_origin_dev_does_not_open_the_judged_door(repo, shadow):
    # Review of PR #2726 @ 54991d373: git resolves a short `origin/dev` to `refs/tags/origin/dev`
    # (and `refs/heads/origin/dev`) before `refs/remotes/origin/dev`, and any writer can push a tag.
    base = _git(repo, "rev-parse", "HEAD").strip()
    _git(repo, "update-ref", "refs/remotes/origin/dev", base)
    head = _bot_commit(repo, LONG_BUMP)
    _git(repo, "update-ref", shadow, head)
    result = _run_workflow_step(repo, _pr_env(base, head, "someone"))
    assert result.returncode == 1 and "::error::" in result.stdout, (
        result.stdout + result.stderr
    )
    # Positive control: the real remote-tracking ref on the commit opens it.
    _git(repo, "update-ref", "refs/remotes/origin/dev", head)
    assert _run_workflow_step(repo, _pr_env(base, head, "someone")).returncode == 0


def test_without_the_remote_refs_a_promotion_is_a_clean_red(repo):
    base = _git(repo, "rev-parse", "HEAD").strip()
    head = _bot_commit(repo, LONG_BUMP)
    result = _run_workflow_step(repo, _pr_env(base, head, "owner"))
    assert result.returncode == 1 and "::error::" in result.stdout
    assert "Traceback" not in result.stderr, result.stderr
