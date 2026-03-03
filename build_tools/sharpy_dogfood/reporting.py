"""
Issue reporting for the dogfooding tool.

Creates structured reports for each issue found during testing.
"""

import json
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Optional, Literal
from enum import Enum


class IssueType(str, Enum):
    """Types of issues that can be detected."""

    GENERATION_FAILED = "generation_failed"
    GENERATION_RATE_LIMITED = "generation_rate_limited"  # Rate limited, not a bug
    VALIDATION_FAILED = "validation_failed"
    COMPILATION_FAILED = "compilation_failed"
    EXECUTION_FAILED = "execution_failed"
    OUTPUT_MISMATCH = "output_mismatch"
    TIMEOUT = "timeout"
    INTERNAL_COMPILER_ERROR = "internal_compiler_error"  # SPY09xx - compiler/infrastructure bug, not user code
    SKIPPED = "skipped"  # Generated code uses unsupported features - not a bug


@dataclass
class Skip:
    """Represents a skipped dogfooding iteration (for inspection/prompt improvement)."""

    timestamp: str
    skip_reason: str
    generated_code: str  # For single-file tests, or main.spy content for multi-file
    expected_output: Optional[str] = None
    feature_focus: Optional[str] = None
    complexity: Optional[str] = None
    backend_used: Optional[str] = None
    generation_duration: Optional[float] = None
    # Multi-file support: dict mapping filename -> code content
    source_files: Optional[dict[str, str]] = None
    # Validation details (if skip was due to validation)
    validation_output: Optional[str] = None

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        return asdict(self)

    @property
    def is_multifile(self) -> bool:
        """Check if this is a multi-file test."""
        return self.source_files is not None and len(self.source_files) > 1


@dataclass
class Success:
    """Represents a successful dogfooding iteration."""

    timestamp: str
    generated_code: str  # For single-file tests, or main.spy content for multi-file
    expected_output: str
    actual_output: str
    feature_focus: Optional[str] = None
    complexity: Optional[str] = None
    backend_used: Optional[str] = None
    generation_duration: Optional[float] = None
    execution_duration: Optional[float] = None
    # Multi-file support: dict mapping filename -> code content
    # If None, this is a single-file test
    source_files: Optional[dict[str, str]] = None

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        return asdict(self)

    @property
    def is_multifile(self) -> bool:
        """Check if this is a multi-file test."""
        return self.source_files is not None and len(self.source_files) > 1


@dataclass
class Issue:
    """Represents a single issue found during dogfooding."""

    issue_type: IssueType
    timestamp: str
    generated_code: str  # For single-file tests, or main.spy content for multi-file
    expected_output: Optional[str] = None
    actual_output: Optional[str] = None
    error_message: Optional[str] = None
    compiler_output: Optional[str] = None
    generated_cs: Optional[str] = None
    validation_result: Optional[str] = None
    feature_focus: Optional[str] = None
    complexity: Optional[str] = None
    backend_used: Optional[str] = None
    generation_duration: Optional[float] = None
    compilation_duration: Optional[float] = None
    execution_duration: Optional[float] = None
    # Multi-file support: dict mapping filename -> code content
    source_files: Optional[dict[str, str]] = None

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        d = asdict(self)
        d["issue_type"] = self.issue_type.value
        return d

    @property
    def is_multifile(self) -> bool:
        """Check if this is a multi-file test."""
        return self.source_files is not None and len(self.source_files) > 1


class IssueReporter:
    """Creates and manages issue reports."""

    def __init__(self, issues_dir: Path):
        self.issues_dir = issues_dir
        self.issues_dir.mkdir(parents=True, exist_ok=True)
        self._issue_count = 0

    def _get_issue_dir(self, issue: Issue) -> Path:
        """Create a unique directory for this issue."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        issue_name = f"{timestamp}_{issue.issue_type.value}_{self._issue_count:04d}"
        self._issue_count += 1

        issue_dir = self.issues_dir / issue_name
        issue_dir.mkdir(parents=True, exist_ok=True)
        return issue_dir

    def report(self, issue: Issue) -> Path:
        """Create a full report for an issue."""
        issue_dir = self._get_issue_dir(issue)

        # Write the generated Sharpy code
        (issue_dir / "source.spy").write_text(issue.generated_code)

        # Write the issue metadata
        metadata = issue.to_dict()
        (issue_dir / "metadata.json").write_text(
            json.dumps(metadata, indent=2, default=str)
        )

        # Write generated C# if available
        if issue.generated_cs:
            (issue_dir / "generated.cs").write_text(issue.generated_cs)

        # Write compiler output if available
        if issue.compiler_output:
            (issue_dir / "compiler_output.txt").write_text(issue.compiler_output)

        # Write actual output if available
        if issue.actual_output:
            (issue_dir / "actual_output.txt").write_text(issue.actual_output)

        # Write expected output if available
        if issue.expected_output:
            (issue_dir / "expected_output.txt").write_text(issue.expected_output)

        # Write error details if available
        if issue.error_message:
            (issue_dir / "error.txt").write_text(issue.error_message)

        # Write a human-readable summary
        summary = self._generate_summary(issue)
        (issue_dir / "README.md").write_text(summary)

        return issue_dir

    def _generate_summary(self, issue: Issue) -> str:
        """Generate a human-readable summary of the issue."""
        lines = [
            f"# Issue Report: {issue.issue_type.value}",
            "",
            f"**Timestamp:** {issue.timestamp}",
            f"**Type:** {issue.issue_type.value}",
        ]

        if issue.feature_focus:
            lines.append(f"**Feature Focus:** {issue.feature_focus}")
        if issue.complexity:
            lines.append(f"**Complexity:** {issue.complexity}")
        if issue.backend_used:
            lines.append(f"**Backend:** {issue.backend_used}")

        lines.extend(
            [
                "",
                "## Generated Sharpy Code",
                "",
                "```python",
                issue.generated_code,
                "```",
                "",
            ]
        )

        if issue.error_message:
            lines.extend(
                [
                    "## Error",
                    "",
                    "```",
                    issue.error_message,
                    "```",
                    "",
                ]
            )

        if issue.expected_output and issue.actual_output:
            lines.extend(
                [
                    "## Output Comparison",
                    "",
                    "### Expected",
                    "```",
                    issue.expected_output,
                    "```",
                    "",
                    "### Actual",
                    "```",
                    issue.actual_output,
                    "```",
                    "",
                ]
            )

        if issue.compiler_output:
            lines.extend(
                [
                    "## Compiler Output",
                    "",
                    "```",
                    issue.compiler_output[:2000],  # Truncate long output
                    "```",
                    "",
                ]
            )

        if issue.generated_cs:
            lines.extend(
                [
                    "## Generated C#",
                    "",
                    "```csharp",
                    issue.generated_cs[:2000],  # Truncate long output
                    "```",
                    "",
                ]
            )

        # Timing information
        timing = []
        if issue.generation_duration:
            timing.append(f"- Generation: {issue.generation_duration:.2f}s")
        if issue.compilation_duration:
            timing.append(f"- Compilation: {issue.compilation_duration:.2f}s")
        if issue.execution_duration:
            timing.append(f"- Execution: {issue.execution_duration:.2f}s")

        if timing:
            lines.extend(
                [
                    "## Timing",
                    "",
                    *timing,
                    "",
                ]
            )

        return "\n".join(lines)


class SummaryReporter:
    """Generates summary reports of all dogfooding runs."""

    def __init__(self, output_dir: Path):
        self.output_dir = output_dir
        self.runs: list[dict] = []

    def add_run(
        self,
        iteration: int,
        feature_focus: str,
        complexity: str,
        success: bool,
        issue_type: Optional[IssueType] = None,
        issue_dir: Optional[Path] = None,
        success_dir: Optional[Path] = None,
        skip_dir: Optional[Path] = None,
        duration: float = 0.0,
        skip_reason: Optional[str] = None,
    ) -> None:
        """Record a single run."""
        self.runs.append(
            {
                "iteration": iteration,
                "feature_focus": feature_focus,
                "complexity": complexity,
                "success": success,
                "success_dir": str(success_dir) if success_dir else None,
                "issue_type": issue_type.value if issue_type else None,
                "issue_dir": str(issue_dir) if issue_dir else None,
                "skip_dir": str(skip_dir) if skip_dir else None,
                "duration": duration,
                "timestamp": datetime.now().isoformat(),
                "skip_reason": skip_reason,
            }
        )

    def generate_summary(self) -> str:
        """Generate a summary report of all runs."""
        total = len(self.runs)
        successful = sum(1 for r in self.runs if r["success"])
        skipped = sum(1 for r in self.runs if r["issue_type"] == "skipped")
        failed = total - successful - skipped

        # Count issues by type
        issue_counts = {}
        for run in self.runs:
            if run["issue_type"]:
                issue_counts[run["issue_type"]] = (
                    issue_counts.get(run["issue_type"], 0) + 1
                )

        lines = [
            "# Dogfooding Summary Report",
            "",
            f"**Generated:** {datetime.now().isoformat()}",
            "",
            "## Overall Statistics",
            "",
            f"- **Total Iterations:** {total}",
            (
                f"- **Successful:** {successful} ({100*successful/total:.1f}%)"
                if total > 0
                else "- **Successful:** 0"
            ),
            (
                f"- **Failed:** {failed} ({100*failed/total:.1f}%)"
                if total > 0
                else "- **Failed:** 0"
            ),
            (
                f"- **Skipped:** {skipped} ({100*skipped/total:.1f}%)"
                if total > 0
                else "- **Skipped:** 0"
            ),
            "",
        ]

        if issue_counts:
            lines.extend(
                [
                    "## Issues by Type",
                    "",
                ]
            )
            for issue_type, count in sorted(issue_counts.items()):
                lines.append(f"- **{issue_type}:** {count}")
            lines.append("")

        # Recent failures (excluding skips)
        failures = [
            r for r in self.runs if not r["success"] and r["issue_type"] != "skipped"
        ][-10:]
        if failures:
            lines.extend(
                [
                    "## Recent Failures",
                    "",
                ]
            )
            for f in failures:
                issue_dir = f["issue_dir"] or "N/A"
                lines.append(
                    f"- [{f['issue_type']}]({issue_dir}) - "
                    f"{f['feature_focus']}/{f['complexity']} - "
                    f"{f['duration']:.1f}s"
                )
            lines.append("")

        # Recent skips (for debugging generation quality)
        skips = [r for r in self.runs if r["issue_type"] == "skipped"][-5:]
        if skips:
            lines.extend(
                [
                    "## Recent Skips",
                    "",
                ]
            )
            for s in skips:
                reason = s.get("skip_reason", "Unknown")
                lines.append(f"- {s['feature_focus']}/{s['complexity']} - {reason}")
            lines.append("")

        return "\n".join(lines)

    def save(self) -> None:
        """Save the summary report and run data."""
        # Save run data as JSON
        runs_file = self.output_dir / "runs.json"
        with open(runs_file, "w") as f:
            json.dump(self.runs, f, indent=2)

        # Save human-readable summary
        summary_file = self.output_dir / "SUMMARY.md"
        summary_file.write_text(self.generate_summary())


class SuccessReporter:
    """Creates and manages reports for successful dogfooding iterations."""

    def __init__(self, successes_dir: Path):
        self.successes_dir = successes_dir
        self.successes_dir.mkdir(parents=True, exist_ok=True)
        self._success_count = 0

    def _get_success_dir(self, success: Success) -> Path:
        """Create a unique directory for this successful run."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        feature = success.feature_focus or "unknown"
        # Add marker for multi-file tests
        multifile_marker = "_multifile" if success.is_multifile else ""
        success_name = (
            f"{timestamp}_success_{feature}{multifile_marker}_{self._success_count:04d}"
        )
        self._success_count += 1

        success_dir = self.successes_dir / success_name
        success_dir.mkdir(parents=True, exist_ok=True)
        return success_dir

    def report(self, success: Success) -> Path:
        """Create a full report for a successful iteration."""
        success_dir = self._get_success_dir(success)

        if success.is_multifile:
            # Multi-file test: write each source file separately
            for filename, code in success.source_files.items():
                (success_dir / filename).write_text(code)
            # Also write main.spy as source.spy for backwards compatibility
            if "main.spy" in success.source_files:
                (success_dir / "source.spy").write_text(
                    success.source_files["main.spy"]
                )
        else:
            # Single-file test: write the generated Sharpy code
            (success_dir / "source.spy").write_text(success.generated_code)

        # Write the expected output (for integration tests)
        # For multi-file tests, use main.expected to match the test infrastructure
        if success.is_multifile:
            (success_dir / "main.expected").write_text(success.expected_output)
        (success_dir / "expected_output.txt").write_text(success.expected_output)

        # Write the actual output (should match expected)
        (success_dir / "actual_output.txt").write_text(success.actual_output)

        # Write the success metadata
        metadata = success.to_dict()
        (success_dir / "metadata.json").write_text(
            json.dumps(metadata, indent=2, default=str)
        )

        # Write a human-readable summary
        summary = self._generate_summary(success)
        (success_dir / "README.md").write_text(summary)

        return success_dir

    def _generate_summary(self, success: Success) -> str:
        """Generate a human-readable summary of the successful run."""
        lines = [
            "# Successful Dogfood Run",
            "",
            f"**Timestamp:** {success.timestamp}",
        ]

        if success.feature_focus:
            lines.append(f"**Feature Focus:** {success.feature_focus}")
        if success.complexity:
            lines.append(f"**Complexity:** {success.complexity}")
        if success.backend_used:
            lines.append(f"**Backend:** {success.backend_used}")
        if success.is_multifile:
            lines.append(
                f"**Test Type:** Multi-file ({len(success.source_files)} files)"
            )

        if success.is_multifile:
            lines.extend(
                [
                    "",
                    "## Source Files",
                    "",
                ]
            )
            for filename, code in success.source_files.items():
                lines.extend(
                    [
                        f"### {filename}",
                        "",
                        "```python",
                        code,
                        "```",
                        "",
                    ]
                )
        else:
            lines.extend(
                [
                    "",
                    "## Generated Sharpy Code",
                    "",
                    "```python",
                    success.generated_code,
                    "```",
                    "",
                    "## Output",
                    "",
                    "```",
                    success.actual_output,
                    "```",
                    "",
                ]
            )

        # Timing information
        timing = []
        if success.generation_duration:
            timing.append(f"- Generation: {success.generation_duration:.2f}s")
        if success.execution_duration:
            timing.append(f"- Execution: {success.execution_duration:.2f}s")

        if timing:
            lines.extend(
                [
                    "## Timing",
                    "",
                    *timing,
                    "",
                ]
            )

        lines.extend(
            [
                "## Converting to Integration Test",
                "",
                "To convert this to an integration test, run:",
                "",
                "```bash",
                "python -m sharpy_dogfood convert <this_directory_name>",
                "```",
                "",
            ]
        )

        return "\n".join(lines)


class SkipReporter:
    """Creates and manages reports for skipped dogfooding iterations.

    Skipped iterations are saved for inspection to help improve prompting
    and understand what kind of code the AI is generating that doesn't
    match the spec.
    """

    def __init__(self, skips_dir: Path):
        self.skips_dir = skips_dir
        self.skips_dir.mkdir(parents=True, exist_ok=True)
        self._skip_count = 0

    def _get_skip_dir(self, skip: Skip) -> Path:
        """Create a unique directory for this skipped run."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        feature = skip.feature_focus or "unknown"
        # Add marker for multi-file tests
        multifile_marker = "_multifile" if skip.is_multifile else ""
        skip_name = (
            f"{timestamp}_skip_{feature}{multifile_marker}_{self._skip_count:04d}"
        )
        self._skip_count += 1

        skip_dir = self.skips_dir / skip_name
        skip_dir.mkdir(parents=True, exist_ok=True)
        return skip_dir

    def report(self, skip: Skip) -> Path:
        """Create a full report for a skipped iteration."""
        skip_dir = self._get_skip_dir(skip)

        if skip.is_multifile:
            # Multi-file test: write each source file separately
            for filename, code in skip.source_files.items():
                (skip_dir / filename).write_text(code)
            # Also write main.spy as source.spy for backwards compatibility
            if "main.spy" in skip.source_files:
                (skip_dir / "source.spy").write_text(skip.source_files["main.spy"])
        elif skip.generated_code:
            # Single-file test: write the generated Sharpy code
            (skip_dir / "source.spy").write_text(skip.generated_code)

        # Write expected output if available
        if skip.expected_output:
            (skip_dir / "expected_output.txt").write_text(skip.expected_output)

        # Write validation output if available
        if skip.validation_output:
            (skip_dir / "validation_output.txt").write_text(skip.validation_output)

        # Write the skip reason
        (skip_dir / "skip_reason.txt").write_text(skip.skip_reason)

        # Write the skip metadata
        metadata = skip.to_dict()
        (skip_dir / "metadata.json").write_text(
            json.dumps(metadata, indent=2, default=str)
        )

        # Write a human-readable summary
        summary = self._generate_summary(skip)
        (skip_dir / "README.md").write_text(summary)

        return skip_dir

    def _generate_summary(self, skip: Skip) -> str:
        """Generate a human-readable summary of the skipped run."""
        lines = [
            "# Skipped Dogfood Run",
            "",
            f"**Timestamp:** {skip.timestamp}",
            f"**Skip Reason:** {skip.skip_reason}",
        ]

        if skip.feature_focus:
            lines.append(f"**Feature Focus:** {skip.feature_focus}")
        if skip.complexity:
            lines.append(f"**Complexity:** {skip.complexity}")
        if skip.backend_used:
            lines.append(f"**Backend:** {skip.backend_used}")
        if skip.is_multifile:
            lines.append(f"**Test Type:** Multi-file ({len(skip.source_files)} files)")

        if skip.is_multifile and skip.source_files:
            lines.extend(
                [
                    "",
                    "## Source Files",
                    "",
                ]
            )
            for filename, code in skip.source_files.items():
                lines.extend(
                    [
                        f"### {filename}",
                        "",
                        "```python",
                        code,
                        "```",
                        "",
                    ]
                )
        elif skip.generated_code:
            lines.extend(
                [
                    "",
                    "## Generated Sharpy Code",
                    "",
                    "```python",
                    skip.generated_code,
                    "```",
                    "",
                ]
            )

        if skip.validation_output:
            lines.extend(
                [
                    "## Validation Output",
                    "",
                    "```",
                    skip.validation_output[:2000],  # Truncate long output
                    "```",
                    "",
                ]
            )

        # Timing information
        if skip.generation_duration:
            lines.extend(
                [
                    "## Timing",
                    "",
                    f"- Generation: {skip.generation_duration:.2f}s",
                    "",
                ]
            )

        lines.extend(
            [
                "## Notes",
                "",
                "This iteration was skipped because the generated code didn't pass validation.",
                "This is typically due to the AI generating code with unsupported features",
                "or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).",
                "",
                "This output is saved for inspection to help improve prompting.",
                "",
            ]
        )

        return "\n".join(lines)
