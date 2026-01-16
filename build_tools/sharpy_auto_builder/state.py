"""
State management for Sharpy Auto Builder.

Tracks task progress, execution history, and ground truth.
"""

from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Optional, Any
import json
import re


class TaskStatus(str, Enum):
    """Status of a task in the implementation process."""

    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    AWAITING_VALIDATION = "awaiting_validation"
    AWAITING_HUMAN_REVIEW = "awaiting_human_review"
    AWAITING_DECISION = "awaiting_decision"  # NEW: Agent asked questions
    DEFERRED = "deferred"  # NEW: Explicitly deferred (e.g., optional tasks)
    COMPLETED = "completed"
    FAILED = "failed"
    SKIPPED = "skipped"
    BLOCKED = "blocked"


class ValidationStatus(str, Enum):
    """Status of validation checks."""

    PENDING = "pending"
    PASSED = "passed"
    FAILED = "failed"
    WARNINGS = "warnings"
    MINOR_ISSUES = "minor_issues"
    MAJOR_ISSUES = "major_issues"


@dataclass
class ValidationResult:
    """Result of a validation check."""

    agent: str  # e.g., "spec-adherence", "verification-expert"
    status: ValidationStatus
    timestamp: str = field(default_factory=lambda: datetime.now().isoformat())
    findings: list[str] = field(default_factory=list)
    recommendations: list[str] = field(default_factory=list)
    raw_output: Optional[str] = None

    def to_dict(self) -> dict:
        return {
            "agent": self.agent,
            "status": self.status.value,
            "timestamp": self.timestamp,
            "findings": self.findings,
            "recommendations": self.recommendations,
            "raw_output": self.raw_output,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "ValidationResult":
        return cls(
            agent=data["agent"],
            status=ValidationStatus(data["status"]),
            timestamp=data.get("timestamp", datetime.now().isoformat()),
            findings=data.get("findings", []),
            recommendations=data.get("recommendations", []),
            raw_output=data.get("raw_output"),
        )


@dataclass
class TaskExecution:
    """Record of a task execution attempt."""

    attempt_number: int
    backend: str  # "copilot" or "claude_code"
    started_at: str = field(default_factory=lambda: datetime.now().isoformat())
    completed_at: Optional[str] = None
    success: bool = False
    error_message: Optional[str] = None
    changes_made: list[str] = field(default_factory=list)
    tests_run: bool = False
    tests_passed: bool = False
    validation_results: list[ValidationResult] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "attempt_number": self.attempt_number,
            "backend": self.backend,
            "started_at": self.started_at,
            "completed_at": self.completed_at,
            "success": self.success,
            "error_message": self.error_message,
            "changes_made": self.changes_made,
            "tests_run": self.tests_run,
            "tests_passed": self.tests_passed,
            "validation_results": [v.to_dict() for v in self.validation_results],
        }

    @classmethod
    def from_dict(cls, data: dict) -> "TaskExecution":
        return cls(
            attempt_number=data["attempt_number"],
            backend=data["backend"],
            started_at=data.get("started_at", datetime.now().isoformat()),
            completed_at=data.get("completed_at"),
            success=data.get("success", False),
            error_message=data.get("error_message"),
            changes_made=data.get("changes_made", []),
            tests_run=data.get("tests_run", False),
            tests_passed=data.get("tests_passed", False),
            validation_results=[
                ValidationResult.from_dict(v)
                for v in data.get("validation_results", [])
            ],
        )


@dataclass
class Task:
    """A single task from the implementation plan."""

    id: str  # e.g., "0.1.0.1"
    phase: str  # e.g., "0.1.0"
    title: str
    description: str
    files: list[str] = field(default_factory=list)
    status: TaskStatus = TaskStatus.PENDING
    executions: list[TaskExecution] = field(default_factory=list)
    dependencies: list[str] = field(default_factory=list)
    is_critical: bool = False
    human_question: Optional[str] = None
    human_answer: Optional[str] = None
    notes: list[str] = field(default_factory=list)

    @property
    def current_execution(self) -> Optional[TaskExecution]:
        """Get the most recent execution."""
        return self.executions[-1] if self.executions else None

    @property
    def attempt_count(self) -> int:
        """Number of execution attempts."""
        return len(self.executions)

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "phase": self.phase,
            "title": self.title,
            "description": self.description,
            "files": self.files,
            "status": self.status.value,
            "executions": [e.to_dict() for e in self.executions],
            "dependencies": self.dependencies,
            "is_critical": self.is_critical,
            "human_question": self.human_question,
            "human_answer": self.human_answer,
            "notes": self.notes,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Task":
        return cls(
            id=data["id"],
            phase=data["phase"],
            title=data["title"],
            description=data.get("description", ""),
            files=data.get("files", []),
            status=TaskStatus(data.get("status", "pending")),
            executions=[TaskExecution.from_dict(e) for e in data.get("executions", [])],
            dependencies=data.get("dependencies", []),
            is_critical=data.get("is_critical", False),
            human_question=data.get("human_question"),
            human_answer=data.get("human_answer"),
            notes=data.get("notes", []),
        )


@dataclass
class Phase:
    """A phase containing multiple tasks."""

    id: str  # e.g., "0.1.0"
    name: str  # e.g., "Lexer Foundation"
    goal: str
    tasks: list[Task] = field(default_factory=list)

    @property
    def status(self) -> TaskStatus:
        """Compute phase status from task statuses."""
        if not self.tasks:
            return TaskStatus.PENDING

        statuses = [t.status for t in self.tasks]

        if all(s == TaskStatus.COMPLETED for s in statuses):
            return TaskStatus.COMPLETED
        if any(s == TaskStatus.FAILED for s in statuses):
            return TaskStatus.FAILED
        if any(s == TaskStatus.IN_PROGRESS for s in statuses):
            return TaskStatus.IN_PROGRESS
        if any(s == TaskStatus.AWAITING_HUMAN_REVIEW for s in statuses):
            return TaskStatus.AWAITING_HUMAN_REVIEW
        if any(s == TaskStatus.AWAITING_VALIDATION for s in statuses):
            return TaskStatus.AWAITING_VALIDATION
        return TaskStatus.PENDING

    @property
    def progress(self) -> float:
        """Calculate completion percentage."""
        if not self.tasks:
            return 0.0
        completed = sum(1 for t in self.tasks if t.status == TaskStatus.COMPLETED)
        return (completed / len(self.tasks)) * 100

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "name": self.name,
            "goal": self.goal,
            "status": self.status.value,
            "progress": self.progress,
            "tasks": [t.to_dict() for t in self.tasks],
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Phase":
        return cls(
            id=data["id"],
            name=data["name"],
            goal=data.get("goal", ""),
            tasks=[Task.from_dict(t) for t in data.get("tasks", [])],
        )


@dataclass
class GroundTruth:
    """The ground truth document tracking all implementation state."""

    version: str = "1.0"
    last_updated: str = field(default_factory=lambda: datetime.now().isoformat())
    phases: list[Phase] = field(default_factory=list)
    current_phase_id: Optional[str] = None
    current_task_id: Optional[str] = None
    total_attempts: int = 0
    total_successes: int = 0
    total_failures: int = 0
    backend_stats: dict[str, dict[str, int]] = field(
        default_factory=lambda: {
            "claude_code": {"attempts": 0, "successes": 0, "failures": 0},
            "copilot": {"attempts": 0, "successes": 0, "failures": 0},
        }
    )

    def get_phase(self, phase_id: str) -> Optional[Phase]:
        """Get a phase by ID."""
        for phase in self.phases:
            if phase.id == phase_id:
                return phase
        return None

    def get_task(self, task_id: str) -> Optional[Task]:
        """Get a task by ID."""
        for phase in self.phases:
            for task in phase.tasks:
                if task.id == task_id:
                    return task
        return None

    def get_next_pending_task(self) -> Optional[Task]:
        """Get the next task that should be executed."""
        for phase in self.phases:
            for task in phase.tasks:
                if task.status == TaskStatus.PENDING:
                    # Check dependencies
                    deps_satisfied = all(
                        self.get_task(dep).status
                        == TaskStatus.COMPLETED  # pyright: ignore[reportOptionalMemberAccess]
                        for dep in task.dependencies
                        if self.get_task(dep)
                    )
                    if deps_satisfied:
                        return task
        return None

    def get_tasks_awaiting_review(self) -> list[Task]:
        """Get all tasks awaiting human review."""
        tasks = []
        for phase in self.phases:
            for task in phase.tasks:
                if task.status == TaskStatus.AWAITING_HUMAN_REVIEW:
                    tasks.append(task)
        return tasks

    def add_followup_task(
        self,
        original_task: Task,
        title: str,
        description: str,
        files: list[str] | None = None,
    ) -> Task:
        """Add a follow-up task after an existing task.

        The new task will have an ID like "X.X.X.X-fix-1" and will be
        inserted after the original task in the same phase.
        """
        # Find the phase containing the original task
        phase = self.get_phase(original_task.phase)
        if not phase:
            raise ValueError(f"Phase {original_task.phase} not found")

        # Generate a unique follow-up task ID
        base_id = original_task.id
        suffix = 1
        while self.get_task(f"{base_id}-fix-{suffix}"):
            suffix += 1
        new_task_id = f"{base_id}-fix-{suffix}"

        # Create the new task
        new_task = Task(
            id=new_task_id,
            phase=original_task.phase,
            title=title,
            description=description,
            files=files or original_task.files.copy(),
            status=TaskStatus.PENDING,
            dependencies=[original_task.id],  # Depends on original task
            is_critical=False,  # Follow-up fixes aren't critical
            notes=[f"Auto-generated follow-up from task {original_task.id}"],
        )

        # Insert after the original task in the phase
        original_index = next(
            (i for i, t in enumerate(phase.tasks) if t.id == original_task.id),
            len(phase.tasks) - 1,
        )
        phase.tasks.insert(original_index + 1, new_task)

        return new_task

    @property
    def overall_progress(self) -> float:
        """Calculate overall completion percentage."""
        total_tasks = sum(len(p.tasks) for p in self.phases)
        if total_tasks == 0:
            return 0.0
        completed = sum(
            1 for p in self.phases for t in p.tasks if t.status == TaskStatus.COMPLETED
        )
        return (completed / total_tasks) * 100

    def to_dict(self) -> dict:
        return {
            "version": self.version,
            "last_updated": self.last_updated,
            "phases": [p.to_dict() for p in self.phases],
            "current_phase_id": self.current_phase_id,
            "current_task_id": self.current_task_id,
            "total_attempts": self.total_attempts,
            "total_successes": self.total_successes,
            "total_failures": self.total_failures,
            "backend_stats": self.backend_stats,
            "overall_progress": self.overall_progress,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "GroundTruth":
        return cls(
            version=data.get("version", "1.0"),
            last_updated=data.get("last_updated", datetime.now().isoformat()),
            phases=[Phase.from_dict(p) for p in data.get("phases", [])],
            current_phase_id=data.get("current_phase_id"),
            current_task_id=data.get("current_task_id"),
            total_attempts=data.get("total_attempts", 0),
            total_successes=data.get("total_successes", 0),
            total_failures=data.get("total_failures", 0),
            backend_stats=data.get(
                "backend_stats",
                {
                    "claude_code": {"attempts": 0, "successes": 0, "failures": 0},
                    "copilot": {"attempts": 0, "successes": 0, "failures": 0},
                },
            ),
        )

    def save(self, path: Path) -> None:
        """Save ground truth to JSON file."""
        self.last_updated = datetime.now().isoformat()
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, "w") as f:
            json.dump(self.to_dict(), f, indent=2)

    @classmethod
    def load(cls, path: Path) -> "GroundTruth":
        """Load ground truth from JSON file."""
        with open(path) as f:
            data = json.load(f)
        return cls.from_dict(data)


def parse_task_list(content: str) -> GroundTruth:
    """Parse the task list markdown into a GroundTruth structure."""
    ground_truth = GroundTruth()

    # Phase pattern: # Phase 0.1.X: Name or ## Phase 0.1.X: Name
    phase_pattern = re.compile(r"^#{1,2} Phase (\d+\.\d+\.\d+): (.+)$", re.MULTILINE)
    # Goal pattern: **Goal**: ...
    goal_pattern = re.compile(r"\*\*Goal\*\*: (.+?)(?:\n|$)")
    # Task pattern: ## Task X.X.X.X: Title or ### Task X.X.X.X: Title
    # Also supports optional prefix like "R-" for remediation tasks
    # Last segment can be alphanumeric (e.g., 0.1.10.CG1)
    task_pattern = re.compile(
        r"^#{2,3} Task ([A-Z]+-)?(\d+\.\d+\.\d+\.[A-Za-z0-9]+):?\s*(.*)$", re.MULTILINE
    )
    # File patterns - multiple ways files are referenced:
    # 1. 📁 **Files**: `path` (single file inline)
    # 2. 📁 **Files**:\n- `path` (list format)
    # 3. Multiple backtick-quoted paths anywhere in task
    file_pattern = re.compile(
        r"`(src/[^`]+|tests/[^`]+)`"
    )  # Match paths starting with src/ or tests/

    # Split content by phases
    phase_splits = list(phase_pattern.finditer(content))

    for i, match in enumerate(phase_splits):
        phase_id = match.group(1)
        phase_name = match.group(2).strip()

        # Get content for this phase
        start = match.end()
        end = phase_splits[i + 1].start() if i + 1 < len(phase_splits) else len(content)
        phase_content = content[start:end]

        # Extract goal
        goal_match = goal_pattern.search(phase_content)
        goal = goal_match.group(1) if goal_match else ""

        phase = Phase(id=phase_id, name=phase_name, goal=goal)

        # Find all tasks in this phase
        task_matches = list(task_pattern.finditer(phase_content))

        for j, task_match in enumerate(task_matches):
            # Group 1 is optional prefix (e.g., "R-"), group 2 is task ID, group 3 is title
            prefix = task_match.group(1) or ""
            task_id = prefix + task_match.group(2)  # Include prefix in ID if present
            task_title = task_match.group(3).strip() or f"Task {task_id}"

            # Get content for this task
            task_start = task_match.end()
            task_end = (
                task_matches[j + 1].start()
                if j + 1 < len(task_matches)
                else len(phase_content)
            )
            task_content = phase_content[task_start:task_end]

            # Extract files (deduplicated)
            files = list(dict.fromkeys(file_pattern.findall(task_content)))

            # Determine if task is critical (marked with ⚠️, 🚨, or "Potential Gap")
            is_critical = (
                "⚠️" in task_content
                or "🚨" in task_content
                or "Potential Gap" in task_content
            )

            task = Task(
                id=task_id,
                phase=phase_id,
                title=task_title,
                description=task_content.strip()[:500],  # First 500 chars as summary
                files=files,
                is_critical=is_critical,
            )
            phase.tasks.append(task)

        ground_truth.phases.append(phase)

    return ground_truth
