"""
Harness for ``build_tools/coverage_reduce.py`` and the coverage surface of ``dotnet10.yml`` (#2512,
plan-88736c D1, Acceptance cell 1 mutations (3)-(6)).

Coverage is measured and published, never gated, so the failure that matters is a table that is
silently wrong or silently absent: a union counted as a sum, an include that matched nothing, a
covered job that produced no file, or a coverage-off run that collected anyway. Each probe runs the
real script as CI does (``--name`` in a test job, ``--merge`` in ci-summary) over fake cobertura or
fake reduced files and asserts its exit code and its ``::error::`` text.
"""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / "build_tools" / "coverage_reduce.py"

# .claude/scripts/ci-gate's two parsers: no coverage line may read as either.
GATE_COUNTS = re.compile(
    r"(?:^|\s)GATE-COUNTS passed=(\d+) failed=(\d+) skipped=(\d+)\s*$"
)
GATE_FAILED = re.compile(r"(?:^|\s)GATE-FAILED (.+)$")


def cobertura(packages: Dict[str, Dict[str, Dict[int, int]]]) -> str:
    """module -> file -> {line: hits}, as Microsoft.CodeCoverage writes it (class and method lines)."""
    body = ""
    for module, files in packages.items():
        classes = ""
        for name, lines in files.items():
            rows = "".join(
                f'<line number="{n}" hits="{h}" branch="False" />'
                for n, h in lines.items()
            )
            classes += (
                f'<class name="C" filename="{name}"><methods><method name="M"><lines>{rows}</lines>'
                f"</method></methods><lines>{rows}</lines></class>"
            )
        body += f'<package name="{module}"><classes>{classes}</classes></package>'
    return f'<?xml version="1.0" encoding="utf-8"?><coverage><packages>{body}</packages></coverage>'


def run(
    args: List[str], cwd: Path, env: Optional[Dict[str, str]] = None
) -> subprocess.CompletedProcess:
    base = {
        k: v
        for k, v in os.environ.items()
        if not k.startswith(("LSP_SHARD_", "COMPILER_SHARD_"))
    }
    for k in ("COVERAGE_ON", "CODE", "GITHUB_STEP_SUMMARY"):
        base.pop(k, None)
    return subprocess.run(
        [sys.executable, str(SCRIPT), *args],
        cwd=cwd,
        env={**base, **(env or {})},
        capture_output=True,
        text=True,
        timeout=30,
    )


def reduced(modules: Dict[str, Dict[str, List[List[int]]]], name: str) -> str:
    return json.dumps({"name": name, "modules": modules})


ONE = {"Sharpy.Compiler": {"src/a.cs": [[1, 2, 3, 4], [1, 2]]}}


class Merge:
    """A ci-summary workspace: trx/ holds the downloaded coverage-*.json files."""

    def __init__(self, tmp: Path, lsp: int = 2, compiler: int = 2) -> None:
        self.tmp = tmp
        self.dir = tmp / "trx"
        self.dir.mkdir(parents=True)
        self.summary = tmp / "summary.md"
        self.env = {
            "COVERAGE_ON": "true",
            "CODE": "true",
            "GITHUB_STEP_SUMMARY": str(self.summary),
            **{f"LSP_SHARD_{n}": "x" for n in range(1, lsp + 1)},
            **{f"COMPILER_SHARD_{n}": "x" for n in range(1, compiler + 1)},
        }

    def put(self, name: str, modules: Dict[str, Dict[str, List[List[int]]]]) -> None:
        (self.dir / f"coverage-{name}.json").write_text(reduced(modules, name))

    def all_expected(self, lsp: int = 2, compiler: int = 2) -> None:
        for name in ["cli", "core", "stdlib"]:
            self.put(name, ONE)
        for n in range(1, lsp + 1):
            self.put(f"lsp-shard-{n}", ONE)
        for n in range(1, compiler + 1):
            self.put(f"compiler-shard-{n}", ONE)

    def merge(self, **env: str) -> subprocess.CompletedProcess:
        return run(["--merge", "trx"], self.tmp, {**self.env, **env})


class TestReduce:
    def test_class_lines_once_both_copies_unioned_and_consumed(
        self, tmp_path: Path
    ) -> None:
        results = tmp_path / "TestResults"
        # The collector writes the same cobertura twice (the attachment and its In/ copy).
        for sub in ("guid", "run/In/vm"):
            (results / sub).mkdir(parents=True)
            (results / sub / "x.cobertura.xml").write_text(
                cobertura(
                    {
                        "Sharpy.Core": {
                            str(tmp_path / "src" / "a.cs"): {1: 3, 2: 0, 5: 1}
                        }
                    }
                )
            )
        output = run(["--name", "core"], tmp_path)
        assert output.returncode == 0, output.stdout + output.stderr
        data = json.loads((results / "coverage-core.json").read_text())
        # Relative to the workspace; method-level copies and the second file add nothing.
        assert data == {
            "name": "core",
            "modules": {
                "Sharpy.Core": {os.path.join("src", "a.cs"): [[1, 2, 5], [1, 5]]}
            },
        }
        assert not list(results.rglob("*.cobertura.xml")), (
            "the next step of the job must reduce only its own"
        )

    def test_no_cobertura_writes_nothing_and_leaves_the_red_to_the_merge(
        self, tmp_path: Path
    ) -> None:
        (tmp_path / "TestResults").mkdir()
        output = run(["--name", "compiler-shard-2"], tmp_path)
        assert output.returncode == 0
        assert "::warning::COVERAGE compiler-shard-2: no cobertura" in output.stdout
        assert not list((tmp_path / "TestResults").glob("coverage-*.json"))


class TestMerge:
    def test_a_coverage_on_run_publishes_the_union_table(self, tmp_path: Path) -> None:
        m = Merge(tmp_path)
        m.all_expected()
        # Two Lsp shards overlap on lines 1-2 of b.cs: the union counts them once.
        m.put("lsp-shard-1", {"Sharpy.Lsp": {"src/b.cs": [[1, 2, 3, 4], [1, 2]]}})
        m.put("lsp-shard-2", {"Sharpy.Lsp": {"src/b.cs": [[1, 2, 3, 4], [1, 2, 3]]}})
        output = m.merge()
        assert output.returncode == 0, output.stdout
        assert (
            "COVERAGE off" not in output.stdout and "none expected" not in output.stdout
        )
        summary = m.summary.read_text()
        assert "| Lsp (union of 2 shards) | - | 3/4 (75.0%) |" in summary
        assert "| Compiler (union of 2 shards) | 2/4 (50.0%) | - |" in summary
        # The union row: Sharpy.Compiler's one file is shared by cli/core/stdlib/compiler.
        assert "| **union (all projects)** | 2/4 (50.0%) | 3/4 (75.0%) |" in summary
        assert "COVERAGE union Sharpy.Lsp covered=3 valid=4" in output.stdout

    def test_mutation_5_union_is_not_a_sum(self, tmp_path: Path) -> None:
        m = Merge(tmp_path, lsp=2)
        m.all_expected()
        m.put("lsp-shard-1", {"Sharpy.Lsp": {"src/b.cs": [[1, 2], [1, 2]]}})
        m.put("lsp-shard-2", {"Sharpy.Lsp": {"src/b.cs": [[1, 2], [1, 2]]}})
        output = m.merge()
        assert output.returncode == 0, output.stdout
        # A sum would print covered=4 valid=4 (100%, but every count doubled).
        assert "COVERAGE Lsp Sharpy.Lsp covered=2 valid=2" in output.stdout
        assert "COVERAGE union Sharpy.Lsp covered=2 valid=2" in output.stdout

    def test_mutation_2_a_covered_job_with_no_file_is_named(
        self, tmp_path: Path
    ) -> None:
        m = Merge(tmp_path)
        m.all_expected()
        (m.dir / "coverage-compiler-shard-2.json").unlink()
        output = m.merge()
        assert output.returncode == 1
        assert "::error::COVERAGE compiler-shard-2: no coverage" in output.stdout

    def test_mutation_3_an_include_matching_nothing_is_red(
        self, tmp_path: Path
    ) -> None:
        # End to end: the collector's cobertura with no package -> reduce -> merge.
        m = Merge(tmp_path)
        m.all_expected()
        (m.dir / "coverage-core.json").unlink()
        results = tmp_path / "TestResults"
        results.mkdir()
        (results / "x.cobertura.xml").write_text(cobertura({}))
        assert run(["--name", "core"], tmp_path).returncode == 0
        (results / "coverage-core.json").rename(m.dir / "coverage-core.json")
        output = m.merge()
        assert output.returncode == 1
        assert "::error::COVERAGE core: 0 valid lines" in output.stdout

    def test_mutation_4_a_coverage_off_run_with_a_file_is_red(
        self, tmp_path: Path
    ) -> None:
        m = Merge(tmp_path)
        m.put("compiler-shard-1", ONE)
        output = m.merge(COVERAGE_ON="false")
        assert output.returncode == 1
        assert (
            "::error::COVERAGE a coverage-off run carries coverage files: compiler-shard-1"
            in output.stdout
        )

    def test_a_pull_request_run_shows_none_and_is_green(self, tmp_path: Path) -> None:
        # On a PR `inputs.coverage` is null, so COVERAGE_ON is the empty string.
        for value in ("", "false"):
            m = Merge(tmp_path / (value or "empty"))
            output = m.merge(COVERAGE_ON=value)
            assert output.returncode == 0, output.stdout
            assert "COVERAGE off" in output.stdout
            assert not m.summary.exists()

    def test_mutation_6_readme_only_control_is_green(self, tmp_path: Path) -> None:
        # A README-only mainline push is coverage-on and runs only windows: nothing is expected.
        m = Merge(tmp_path)
        output = m.merge(CODE="false")
        assert output.returncode == 0, output.stdout
        # The README arm ran, not the coverage-off arm.
        assert "COVERAGE none expected" in output.stdout
        assert "COVERAGE off" not in output.stdout

    def test_no_line_reads_as_a_gate_line(self, tmp_path: Path) -> None:
        m = Merge(tmp_path)
        m.all_expected()
        (m.dir / "coverage-cli.json").unlink()
        output = m.merge()
        assert "COVERAGE" in output.stdout
        for line in output.stdout.splitlines():
            assert not GATE_COUNTS.search(line) and not GATE_FAILED.search(line), line

