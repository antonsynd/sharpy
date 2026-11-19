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
    "1. one\n2. two\n\n### 1.25. Version bump check\n\n## Anchored Heading\n"
)
PLAN_TEMPLATE_TEXT = "# create-plan\n\n```markdown\n## Defect Class\n\n- **Violated contract:** <x>\n```\n"
CLAUDE_TEXT = "# CLAUDE.md\n\n## Testing\n\n**Commit gate** = the solution.\n\n## Critical Rules\n\n1. a\n2. b\n"


def _repo(tmp_path: Path, citing: str) -> Path:
    files = {
        "CLAUDE.md": CLAUDE_TEXT + "\n" + citing + "\n",
        csr.CONTRACT: CONTRACT_TEXT,
        csr.PROCESS: PROCESS_TEXT,
        ".claude/skills/push/SKILL.md": SKILL_TEXT,
        csr.PLAN_TEMPLATE: PLAN_TEMPLATE_TEXT,
        ".claude/skills/verify-plan/SKILL.md": "# v\n\n## Verification Dimensions\n\n### 1. One\n",
        "docs/process/plans/plan-0a1b2c.md": "x\n",
        ".gitignore": ".claude/tmp/\n",
    }
    for rel, text in files.items():
        p = tmp_path / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    subprocess.run(["git", "init", "-q", str(tmp_path)], check=True)
    return tmp_path


def _errors(tmp_path: Path, citing: str, kind: str):
    refs, _ = csr.check(_repo(tmp_path, citing))
    mine = [r for r in refs if r.kind == kind and r.file == "CLAUDE.md"]
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
    ],
)
def test_text_that_is_not_a_checked_reference_extracts_nothing(tmp_path, citing):
    refs, checker = csr.check(_repo(tmp_path, citing))
    last = len(CLAUDE_TEXT.splitlines()) + 1
    mine = [r for r in refs if r.file == "CLAUDE.md" and r.line > last]
    assert mine == []


def test_out_of_scope_forms_are_counted(tmp_path):
    _, checker = csr.check(_repo(tmp_path, "bare §3, step 4, item 2 and charter §3."))
    assert checker.out_of_scope["bare-number"] == 3
    assert checker.out_of_scope["charter"] == 1


def test_main_reports_and_exits_red(tmp_path, capsys):
    repo = _repo(tmp_path, "Then run `/no-such-skill`.")
    assert csr.main(["--repo", str(repo)]) == 1
    out = capsys.readouterr().out
    assert "BROKEN skill: CLAUDE.md:" in out and "RESULT: FAIL" in out
