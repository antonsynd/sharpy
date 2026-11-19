"""Every reference in the procedure text (skills, agents, CLAUDE.md, the process README) resolves.

The tree test is the gate; the synthetic-repo tests pin each reference kind's red and green on a
fixture, so an extractor that stops matching, or a resolver that stops failing, is caught twice.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from build_tools import check_skill_refs as csr

REPO = Path(__file__).resolve().parent.parent.parent


@pytest.fixture(scope="module")
def tree():
    return csr.check(REPO)


def test_every_reference_in_the_procedure_text_resolves(tree):
    refs, _ = tree
    broken = [
        f"{r.file}:{r.line}: [{r.kind}] {r.cite} -- {r.error}" for r in refs if r.error
    ]
    assert not broken, "references that resolve to nothing:\n" + "\n".join(broken)


@pytest.mark.parametrize("kind", csr.KINDS)
def test_each_checked_kind_is_found_in_the_tree(tree, kind):
    # Positive control: an extractor that matches nothing cannot report green.
    refs, _ = tree
    assert sum(1 for r in refs if r.kind == kind) > 0, f"no '{kind}' reference found"


def test_out_of_scope_kinds_are_counted_and_named(tree):
    _, checker = tree
    assert set(checker.out_of_scope) == set(csr.OUT_OF_SCOPE)
    assert checker.out_of_scope["bare-number"] > 0
    assert checker.out_of_scope["charter"] > 0


def test_builtin_skill_set_stays_minimal(tree):
    # Each listed built-in is cited somewhere and is not a repo skill (else it needs no listing).
    _, checker = tree
    assert csr.BUILTIN_SKILLS == checker.cited_builtins
    assert not csr.BUILTIN_SKILLS & checker.skills


def test_scanned_files_are_the_procedure_text():
    files = csr.scanned_files(REPO)
    assert "CLAUDE.md" in files and "docs/process/README.md" in files
    assert ".claude/skills/README.md" in files
    assert any(
        f.startswith(".claude/skills/") and f.endswith("/SKILL.md") for f in files
    )
    assert any(f.startswith(".claude/agents/") for f in files)


# -- a synthetic repository: one citing file, the targets, each reference kind red and green -------

CONTRACT_TEXT = "# Contract\n\n## 1. Class\n\ntext\n\n## 2. Guards\n\ntext\n"
PROCESS_TEXT = (
    "# Process\n\n## The stage trail\n\n1. **File of record.** a\n2. b\n\n"
    "### Every other change, and the release\n\ntext\n"
)
SKILL_TEXT = (
    "# Skill\n\n## Output\n\n### 1. Stamp\n\n### 5. After\n\n## Cleanup and Report\n\n"
    "1. one\n2. two\n   1. nested one\n   5. nested five\n8. eight\n\n"
    "```bash\n3. not a step\n```\n\n### 1.25. Version bump check\n\n4. four, under 1.25\n\n"
    "## Agent Instructions\n\n```\n1. a\n6. six, in a bare fence: a prompt's steps\n```\n\n"
    "## Indented\n\n- list:\n  1. a\n  2. b\n\n## Steps\n\n### Step 2: Do it\n\n"
    "## Anchored Heading\n\n## Anchored Heading\n\n"
    "## Prompt\n\n```\n# Role\n1. a\n2. b\n### 7. a fenced heading\n```\n"
).replace("```", "```")
PLAN_TEMPLATE_TEXT = "# create-plan\n\n```markdown\n## Defect Class\n\n- **Violated contract:** <x>\n```\n"
CLAUDE_TEXT = (
    "# CLAUDE.md\n\n## Testing\n\n**Commit gate** = the solution.\n\n## Critical Rules\n\n1. a\n2. b\n\n"
    "**Charter says** x.\n\n**Do not** y.\n\n**See also** z.\n"
)


def _repo(tmp_path: Path, citing: str, where: str = "CLAUDE.md") -> Path:
    files = {
        "CLAUDE.md": CLAUDE_TEXT,
        csr.CONTRACT: CONTRACT_TEXT,
        csr.PROCESS: PROCESS_TEXT,
        ".claude/skills/push/SKILL.md": SKILL_TEXT,
        csr.PLAN_TEMPLATE: PLAN_TEMPLATE_TEXT,
        ".claude/skills/verify-plan/SKILL.md": "# v\n\n## Verification Dimensions\n\n"
        + "".join(f"### {k}. D{k}\n\n" for k in range(1, 6)),
        "docs/process/plans/plan-0a1b2c.md": "x\n",
        "build_tools/CLAUDE.md": "# build_tools\n\n## Commands\n",
        ".gitignore": ".claude/tmp/\n",
    }
    files[where] += "\n" + citing + "\n"
    for rel, text in files.items():
        p = tmp_path / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    subprocess.run(["git", "init", "-q", str(tmp_path)], check=True)
    return tmp_path


def _errors(tmp_path: Path, citing: str, kind: str, where: str = "CLAUDE.md"):
    refs, _ = csr.check(_repo(tmp_path, citing, where))
    mine = [r for r in refs if r.kind == kind and r.file == where]
    assert mine, f"no {kind} reference extracted from {citing!r}"
    return [r.error for r in mine if r.error]


@pytest.mark.parametrize(
    "kind,citing",
    [
        ("path", "Run `build_tools/no_such_tool.py`."),
        ("path", "Write to `docs/process/no-such-dir/`."),
        ("skill", "Then run `/no-such-skill`."),
        ("skill", "Via `/project:push`."),
        ("section", "See `/push` › No Such Heading."),
        ("section", "See `/push` › Cleanup and Report step 9."),
        ("section", "See `/push` Output §7."),
        (
            "section",
            "See `/push` › 1.2.",
        ),  # `### 1.` and `### 1.25.` exist; `1.2` is neither
        ("section", "As `docs/process/README.md` › The stage trail, item 6."),
        ("section", "As CLAUDE.md › Testing › Merge gate."),
        ("section", "Read Defect Class › Owner rulings."),
        ("section", "As stage trail item 9 says."),
        ("section", "See §No Such Section."),
        ("section", "x › y"),
        ("contract", "Per contract §99."),
        ("contract", "Per contract §1–§9."),
        ("link", "[x](docs/missing.md)"),
        ("link", "[x](.claude/skills/push/SKILL.md#no-such-anchor)"),
        # number lists and ranges are checked whole (fix round, item 2)
        ("section", "Skip Dimensions 1, 3 and 99."),
        ("section", "Skip Dimensions 4–6."),
        ("section", "See `/push` › Cleanup and Report, steps 8 and 9."),
        ("section", "See `/push` › Output §99."),
        ("contract", "Per contract §1, §99."),
        # an own-file Name that is a section, with its number missing, stays red (item 3)
        ("section", "As Critical Rules step 9 says."),
        # skills at a sentence end, in bold, in a double-backtick span (item 4)
        ("skill", "Then run /no-such-skill."),
        ("skill", "Use **/nope** now."),
        ("skill", "See ``/nope`` now."),
        # another file's section is judged against that file, not the citing one (item 5)
        ("section", "See `/push` §Testing."),
        ("section", "Run /push Critical Rules step 1."),  # CLAUDE.md has it; /push not
        # step/item numbers: fenced code, a subsection's items, nested items (item 6)
        ("section", "See `/push` › Cleanup and Report step 3."),
        ("section", "See `/push` › Cleanup and Report step 4."),
        ("section", "See `/push` › Cleanup and Report step 5."),  # a nested list item
        ("section", "See `/push` step 9."),
        # second round: B -- an own-file Name that IS a section, number missing, stays red
        ("section", "As Critical Rules step 9 says, and Charter says §3."),
        # D -- `# Role` in a fence is no subheading; a fenced `### 7.` is no step 7
        ("section", "See `/push` › Prompt step 7."),
        # E -- a bare nested CLAUDE.md is its own file
        ("section", "See build_tools/CLAUDE.md › Testing."),
        # F -- 4-design keeps the list going; 1.3 is not 1; capitalised Step; bare continuation
        ("section", "Run Dimensions 4-design, 6."),
        ("section", "See `/push` step 1.3."),
        ("section", "See `/push` › Cleanup and Report, Step 9."),
        ("contract", "Per contract §1–99."),
        ("contract", "Per contract §1 and 99."),
        # G -- link text that wraps, a target with a title
        ("link", "[a\nb](docs/missing.md)"),
        ("link", '[x](docs/missing.md "a title")'),
        # a citation in a bare fence (a prompt) is read like prose
        ("skill", "```\nrun /no-such-skill\n```"),
    ],
)
def test_a_broken_reference_is_red(tmp_path, kind, citing):
    assert _errors(tmp_path, citing, kind)


@pytest.mark.parametrize(
    "kind,citing",
    [
        ("path", "Run `docs/process/README.md` and `.claude/skills/push/`."),
        ("path", "Logs in `.claude/tmp/last-run.log`."),  # gitignored
        ("path", "The record `docs/process/plans/plan-a1b2c3.md`."),  # a family example
        ("skill", "Then run `/push` or /simplify."),
        ("section", "See `/push` › Output."),
        ("section", "See `/push` › Cleanup and Report step 2."),
        ("section", "See `/push` › 1.25."),
        ("section", "See `/push` Output §5."),
        ("section", "As `docs/process/README.md` › The stage trail, item 1."),
        ("section", "As `docs/process/README.md` › Every other change."),
        ("section", "As CLAUDE.md › Testing › Commit gate."),
        ("section", "Read Defect Class › Violated contract."),
        ("section", "As stage trail item 2 says; CLAUDE.md Rule 2; Dimension 1."),
        ("section", "See §Critical Rules."),
        ("contract", "Per contract §1–§2."),
        ("link", "[x](.claude/skills/push/SKILL.md#anchored-heading)"),
        (
            "link",
            "[x](.claude/skills/push/SKILL.md#anchored-heading-1)",
        ),  # GitHub's duplicate
        ("section", "Run Dimensions 2, 3, 5 and 1–4."),
        ("section", "See `/push` › Cleanup and Report, steps 1, 2 and 8."),
        ("section", "See `/push` › Output §1 and §5."),
        ("contract", "Per contract §1, §2."),
        ("contract", "Contract §2 holds."),
        ("section", "As Critical Rules step 2 says."),
        ("skill", "Then run /push."),
        ("skill", "Use **/push** now; see ``/push``."),
        ("section", "See `/push` §Output."),
        ("section", "Run /push Cleanup and Report step 8."),
        ("section", "See `/push` › Agent Instructions step 6."),
        ("section", "See `/push` › Indented, item 2."),
        ("section", "See `/push` › Steps, step 2."),
        ("section", "See `/push` step 8."),
        ("section", "See `/push` › Prompt step 2."),
        ("section", "Run Dimensions 4-design, 5."),
        ("section", "See `/push` step 1.25; `/push` Step 8."),
        ("contract", "Per contract §1 and 2."),
        ("link", '[a\nb](docs/process/README.md "t")'),
    ],
)
def test_a_resolving_reference_is_green(tmp_path, kind, citing):
    assert _errors(tmp_path, citing, kind) == []


@pytest.mark.parametrize(
    "citing",
    [
        "Paths like `src/Foo/*.cs` and `docs/process/plans/<plan-id>.md`.",  # glob, placeholder
        "A `foo`/bar pair and a/b and https://x.org/push and `/spy-*` skills.",  # not skill cites
        "bare §3 and step 4 and item 2 and charter §3.",  # out of scope, counted
        # ordinary prose is not a citation (item 3)
        "Repeat step 2. See §3 for it. Then item 4 applies. Charter §3 says so.",
        # fenced code and inline code are not prose (item 7)
        "```bash\ncd /tmp && msbuild /p:X=1 /nologo\nDimension 9, Rule 99, /no-such\n```",
        "```markdown\n[plan](docs/process/plans/<plan-id>.md)\n```",
        "Index it as `a[i](x)`.",
        # B -- prose whose first word only STARTS a lead-in; any-case charter
        "Charter §3 says so. Do step 3 first. See §2 of the plan. CHARTER §4.",
        # C -- /word that is no skill citation: mid-span, a filesystem root, a link target
        "Run `msbuild /nologo`, write to `/tmp`.",
    ],
)
def test_text_that_is_not_a_checked_reference_extracts_nothing(tmp_path, citing):
    refs, checker = csr.check(_repo(tmp_path, citing))
    last = len(CLAUDE_TEXT.splitlines()) + 1
    mine = [r for r in refs if r.file == "CLAUDE.md" and r.line > last]
    assert mine == []


def _out_of_scope(tmp_path: Path, citing: str) -> dict:
    """What `citing` adds to the counts (the fixture's targets carry some of their own)."""
    _, base = csr.check(_repo(tmp_path / "base", ""))
    _, checker = csr.check(_repo(tmp_path / "cited", citing))
    return {k: v - base.out_of_scope[k] for k, v in checker.out_of_scope.items()}


def test_out_of_scope_forms_are_counted(tmp_path):
    counts = _out_of_scope(tmp_path, "bare §3, step 4, item 2 and charter §3.")
    assert counts["bare-number"] == 3
    assert counts["charter"] == 1


def test_main_reports_and_exits_red(tmp_path, capsys):
    repo = _repo(tmp_path, "Then run `/no-such-skill`.")
    assert csr.main(["--repo", str(repo)]) == 1
    out = capsys.readouterr().out
    assert "BROKEN skill: CLAUDE.md:" in out and "RESULT: FAIL" in out


def test_a_builtin_cited_at_a_sentence_end_counts_as_cited(tmp_path):
    _, checker = csr.check(_repo(tmp_path, "Then run /simplify."))
    assert checker.cited_builtins == {"simplify"}


def test_prose_numbers_fall_to_the_counted_bare_bucket(tmp_path):
    # Known limit (docstring): a Name that is no section of the citing file is prose, so an own-file
    # citation whose heading was renamed is counted here instead of going red.
    counts = _out_of_scope(
        tmp_path, "Repeat step 2. Charter §3. Critical Ruling step 9."
    )
    assert counts["bare-number"] == 2
    assert counts["charter"] == 1


@pytest.mark.parametrize(
    "citing,where",
    [
        # GitHub resolves a link against the citing file's directory, never the repo root first
        ("[p](docs/process/README.md#the-stage-trail)", csr.CONTRACT),
        ("[b](/push)", "CLAUDE.md"),  # a link is a path, not a skill
        ("[d](../no-such-dir/)", csr.CONTRACT),
    ],
)
def test_a_link_resolves_as_github_does_red(tmp_path, citing, where):
    assert _errors(tmp_path, citing, "link", where)


@pytest.mark.parametrize(
    "citing,where",
    [
        ("[p](../process/README.md#the-stage-trail)", csr.CONTRACT),
        ("[r](/docs/process/README.md#the-stage-trail)", csr.CONTRACT),
        ("[d](../process/)", csr.CONTRACT),  # a directory is fine
    ],
)
def test_a_link_resolves_as_github_does_green(tmp_path, citing, where):
    assert _errors(tmp_path, citing, "link", where) == []


# -- the workflow that runs this test starts when a target changes -------------------------------


def _tracked() -> list:
    out = subprocess.run(
        ["git", "-C", str(REPO), "ls-files"], capture_output=True, text=True, check=True
    ).stdout
    return [l for l in out.splitlines() if l]


def test_every_target_the_check_reads_starts_the_python_workflow(tree):
    """A PR that renames a cited file must run this test, or it merges green and the next PR goes
    red. Every file the checker read (the scanned files, each target, .gitignore) is matched by
    the Python Build Tools workflow's RELEVANT_PATHS and push `paths` (one list, kept equal by
    test_workflow_relevance); see `_uncovered`."""
    from build_tools.tests import test_workflow_relevance as twr

    workflow = twr.load(twr.WORKFLOWS / "python-build-tools.yml")
    lists = {
        "RELEVANT_PATHS": twr.relevant_paths(workflow),
        "on.push.paths": list(twr.triggers(workflow)["push"]["paths"]),
    }
    _, checker = tree
    reads = set(checker.reads)
    assert {".gitignore", "CLAUDE.md", csr.CONTRACT} <= reads  # positive control
    missed = _uncovered(reads, _tracked(), lists, lambda r: (REPO / r).is_dir())
    assert not missed, "targets outside the workflow's paths:\n" + "\n".join(missed)


@pytest.mark.parametrize(
    "citing,kind",
    [
        ("As CLAUDE.md §Testing says.", "section"),
        ("[c](../../../CLAUDE.md) §Testing", "section"),
    ],
)
def test_a_target_ahead_is_the_named_file_green(tmp_path, citing, kind):
    # E: judged against CLAUDE.md, from a file (/push) that has no Testing of its own
    assert _errors(tmp_path, citing, kind, ".claude/skills/push/SKILL.md") == []


def test_a_target_ahead_is_the_named_file_red(tmp_path):
    # E: /push has `## Output`; CLAUDE.md does not
    citing = "As CLAUDE.md §Output says."
    assert _errors(tmp_path, citing, "section", ".claude/skills/push/SKILL.md")


def test_not_skills_stay_filesystem_roots(tree):
    # C: each entry is a directory at `/` and no skill or built-in; the list is never a skip list
    _, checker = tree
    assert csr.NOT_SKILLS and all(Path("/", n).is_dir() for n in csr.NOT_SKILLS)
    assert not csr.NOT_SKILLS & (checker.skills | csr.BUILTIN_SKILLS)


def test_slash_less_file_names_in_code_are_counted(tmp_path, tree):
    # H: not resolved; counted so the gap shows in the report line
    citing = "Build `sharpy.sln`; see `Directory.Build.props`, `.python-version`, `mkdocs.yml`."
    assert _out_of_scope(tmp_path, citing)["filename"] == 4
    assert tree[1].out_of_scope["filename"] > 0


def _uncovered(reads, tracked, lists, is_dir):
    """Reads that no list covers. An untracked read (gitignored `.claude/tmp/`, a local file) is
    skipped: no pull request can rename it. A cited directory goes missing only when every file in
    it moves, so it is covered when one of its tracked files is."""
    missed = []
    from build_tools.tests import test_workflow_relevance as twr

    for read in sorted(reads):
        inside = [
            f for f in tracked if f == read or f.startswith(read.rstrip("/") + "/")
        ]
        if not inside:
            continue
        for label, patterns in lists.items():
            if is_dir(read):
                ok = any(twr.matches(f, patterns) for f in inside)
            else:
                ok = twr.matches(read, patterns)
            if not ok:
                missed.append(f"{read} ({label})")
    return missed


def test_coverage_skips_untracked_reads_green():
    # A: `.claude/tmp/` exists on any machine that ran dotnet-serialized; it is not tracked
    assert (
        _uncovered(
            {".claude/tmp", "docs/x.md"},
            ["docs/x.md"],
            {"L": ["docs/**"]},
            lambda r: r == ".claude/tmp",
        )
        == []
    )


def test_coverage_misses_a_tracked_read_red():
    assert _uncovered(
        {"docs/x.md"}, ["docs/x.md"], {"L": ["src/**"]}, lambda r: False
    ) == ["docs/x.md (L)"]


def test_a_root_relative_link_is_a_link_not_a_skill(tmp_path):
    # C: [d](/docs) is a valid root-relative link and must not also be a missing skill
    refs, _ = csr.check(_repo(tmp_path, "Read [d](/docs)."))
    mine = [r for r in refs if r.file == "CLAUDE.md" and r.cite == "/docs"]
    assert [(r.kind, r.error) for r in mine] == [("link", None)]
