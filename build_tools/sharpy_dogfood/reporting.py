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
    VALIDATION_FAILED = "validation_failed"
    COMPILATION_FAILED = "compilation_failed"
    EXECUTION_FAILED = "execution_failed"
    OUTPUT_MISMATCH = "output_mismatch"
    TIMEOUT = "timeout"
    SKIPPED = "skipped"  # Generated code uses unsupported features - not a bug


@dataclass
class Issue:
    """Represents a single issue found during dogfooding."""

    issue_type: IssueType
    timestamp: str
    generated_code: str
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

    def to_dict(self) -> dict:
        """Convert to dictionary for JSON serialization."""
        d = asdict(self)
        d["issue_type"] = self.issue_type.value
        return d


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
                "issue_type": issue_type.value if issue_type else None,
                "issue_dir": str(issue_dir) if issue_dir else None,
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
            r
            for r in self.runs
            if not r["success"] and r["issue_type"] != "skipped"
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
                lines.append(
                    f"- {s['feature_focus']}/{s['complexity']} - {reason}"
                )
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
