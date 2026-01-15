"""
Human-in-the-loop support for Sharpy Auto Builder.

Handles critical questions, human review, and answer processing.

DEPRECATED: This file-based polling system is deprecated in favor of LangGraph's
native interrupt() function. The new interrupt-based system provides:
- Better integration with LangGraph's state management
- Automatic state persistence across interrupts
- Cleaner code without polling loops
- Interactive CLI prompts instead of file watching

This module is kept for backwards compatibility with batch processing workflows.
For new code, use the interrupt-based system in orchestrator.py and interrupt_handler.py.

See: docs/implementation_planning/implementation_plan_2_native_interrupts.md
"""

import asyncio
import json
import subprocess
import time
import warnings
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Optional, Callable, Any
from enum import Enum
import hashlib
import platform


def send_macos_notification(
    title: str,
    message: str,
    sound: bool = True,
    subtitle: str = "",
) -> bool:
    """
    Send a macOS notification using osascript.

    Returns True if notification was sent successfully, False otherwise.
    """
    if platform.system() != "Darwin":
        return False

    # Escape quotes in the strings
    title = title.replace('"', '\\"')
    message = message.replace('"', '\\"')
    subtitle = subtitle.replace('"', '\\"')

    script = f'display notification "{message}" with title "{title}"'
    if subtitle:
        script = f'display notification "{message}" with title "{title}" subtitle "{subtitle}"'
    if sound:
        script += ' sound name "Glass"'

    try:
        subprocess.run(
            ["osascript", "-e", script],
            capture_output=True,
            timeout=5,
        )
        return True
    except (subprocess.SubprocessError, OSError):
        return False


class QuestionPriority(str, Enum):
    """Priority levels for human questions."""

    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CRITICAL = "critical"


class QuestionStatus(str, Enum):
    """Status of a human question."""

    PENDING = "pending"
    ANSWERED = "answered"
    EXPIRED = "expired"
    WITHDRAWN = "withdrawn"


@dataclass
class HumanQuestion:
    """A question requiring human input."""

    id: str
    task_id: str
    question: str
    context: str
    priority: QuestionPriority
    options: list[str] = field(default_factory=list)
    created_at: str = field(default_factory=lambda: datetime.now().isoformat())
    status: QuestionStatus = QuestionStatus.PENDING
    answer: Optional[str] = None
    answered_at: Optional[str] = None
    answer_source: Optional[str] = None  # "file" or "interactive"

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "task_id": self.task_id,
            "question": self.question,
            "context": self.context,
            "priority": self.priority.value,
            "options": self.options,
            "created_at": self.created_at,
            "status": self.status.value,
            "answer": self.answer,
            "answered_at": self.answered_at,
            "answer_source": self.answer_source,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "HumanQuestion":
        return cls(
            id=data["id"],
            task_id=data["task_id"],
            question=data["question"],
            context=data.get("context", ""),
            priority=QuestionPriority(data.get("priority", "medium")),
            options=data.get("options", []),
            created_at=data.get("created_at", datetime.now().isoformat()),
            status=QuestionStatus(data.get("status", "pending")),
            answer=data.get("answer"),
            answered_at=data.get("answered_at"),
            answer_source=data.get("answer_source"),
        )


@dataclass
class HumanReviewRequest:
    """A request for human review of implementation work."""

    id: str
    task_id: str
    title: str
    summary: str
    changes: list[str]
    test_results: str
    validation_results: list[dict]
    concerns: list[str]
    created_at: str = field(default_factory=lambda: datetime.now().isoformat())
    status: str = "pending"  # pending, approved, rejected, needs_changes
    reviewer_notes: Optional[str] = None
    reviewed_at: Optional[str] = None

    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "task_id": self.task_id,
            "title": self.title,
            "summary": self.summary,
            "changes": self.changes,
            "test_results": self.test_results,
            "validation_results": self.validation_results,
            "concerns": self.concerns,
            "created_at": self.created_at,
            "status": self.status,
            "reviewer_notes": self.reviewer_notes,
            "reviewed_at": self.reviewed_at,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "HumanReviewRequest":
        return cls(
            id=data["id"],
            task_id=data["task_id"],
            title=data["title"],
            summary=data.get("summary", ""),
            changes=data.get("changes", []),
            test_results=data.get("test_results", ""),
            validation_results=data.get("validation_results", []),
            concerns=data.get("concerns", []),
            created_at=data.get("created_at", datetime.now().isoformat()),
            status=data.get("status", "pending"),
            reviewer_notes=data.get("reviewer_notes"),
            reviewed_at=data.get("reviewed_at"),
        )


class HumanLoopManager:
    """
    Manages human-in-the-loop interactions.

    DEPRECATED: This class is deprecated in favor of LangGraph's native interrupt()
    function. Use the interrupt-based system in orchestrator.py and interrupt_handler.py
    instead. This class is kept for backwards compatibility with batch processing workflows.
    """

    def __init__(
        self,
        questions_dir: Path,
        answers_dir: Path,
        review_dir: Path,
        check_interval: float = 5.0,
    ):
        warnings.warn(
            "HumanLoopManager is deprecated. Use LangGraph's native interrupt() "
            "function for human-in-the-loop interactions. See orchestrator.py and "
            "interrupt_handler.py for the new interrupt-based system.",
            DeprecationWarning,
            stacklevel=2,
        )

        self.questions_dir = questions_dir
        self.answers_dir = answers_dir
        self.review_dir = review_dir
        self.check_interval = check_interval

        # Ensure directories exist
        for dir_path in [questions_dir, answers_dir, review_dir]:
            dir_path.mkdir(parents=True, exist_ok=True)

        self._questions: dict[str, HumanQuestion] = {}
        self._reviews: dict[str, HumanReviewRequest] = {}
        self._callbacks: dict[str, Callable] = {}

    def _generate_id(self, prefix: str, content: str) -> str:
        """Generate a unique ID."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        content_hash = hashlib.md5(content.encode()).hexdigest()[:8]
        return f"{prefix}_{timestamp}_{content_hash}"

    def create_question(
        self,
        task_id: str,
        question: str,
        context: str = "",
        priority: QuestionPriority = QuestionPriority.MEDIUM,
        options: list[str] | None = None,
    ) -> HumanQuestion:
        """Create a new question for human input."""
        question_id = self._generate_id("q", question)

        human_question = HumanQuestion(
            id=question_id,
            task_id=task_id,
            question=question,
            context=context,
            priority=priority,
            options=options or [],
        )

        self._questions[question_id] = human_question
        self._save_question(human_question)

        # Send macOS notification
        priority_emoji = {
            QuestionPriority.LOW: "📝",
            QuestionPriority.MEDIUM: "❓",
            QuestionPriority.HIGH: "⚠️",
            QuestionPriority.CRITICAL: "🚨",
        }.get(priority, "❓")

        send_macos_notification(
            title=f"{priority_emoji} Sharpy: Question Pending",
            message=question[:100] + ("..." if len(question) > 100 else ""),
            subtitle=f"Task: {task_id} | Priority: {priority.value}",
        )

        return human_question

    def _save_question(self, question: HumanQuestion) -> None:
        """Save a question to the questions directory."""
        question_file = self.questions_dir / f"{question.id}.json"
        with open(question_file, "w") as f:
            json.dump(question.to_dict(), f, indent=2)

        # Also create a human-readable markdown file
        md_file = self.questions_dir / f"{question.id}.md"
        md_content = self._format_question_md(question)
        with open(md_file, "w") as f:
            f.write(md_content)

    def _format_question_md(self, question: HumanQuestion) -> str:
        """Format a question as readable markdown."""
        lines = [
            f"# Question: {question.id}",
            "",
            f"**Task:** {question.task_id}",
            f"**Priority:** {question.priority.value}",
            f"**Created:** {question.created_at}",
            f"**Status:** {question.status.value}",
            "",
            "## Question",
            "",
            question.question,
            "",
        ]

        if question.context:
            lines.extend(
                [
                    "## Context",
                    "",
                    question.context,
                    "",
                ]
            )

        if question.options:
            lines.extend(
                [
                    "## Options",
                    "",
                ]
            )
            for i, option in enumerate(question.options, 1):
                lines.append(f"{i}. {option}")
            lines.append("")

        lines.extend(
            [
                "## How to Answer",
                "",
                "Create a file in the answers directory with the same ID:",
                f"  `{self.answers_dir}/{question.id}.json`",
                "",
                "With content:",
                "```json",
                "{",
                '  "answer": "Your answer here",',
                '  "notes": "Optional explanation"',
                "}",
                "```",
                "",
                "Or for option selection:",
                "```json",
                "{",
                '  "answer": "1",',
                '  "notes": "Selected option 1 because..."',
                "}",
                "```",
            ]
        )

        return "\n".join(lines)

    def create_review_request(
        self,
        task_id: str,
        title: str,
        summary: str,
        changes: list[str],
        test_results: str,
        validation_results: list[dict],
        concerns: list[str],
    ) -> HumanReviewRequest:
        """Create a request for human review."""
        review_id = self._generate_id("r", title)

        review = HumanReviewRequest(
            id=review_id,
            task_id=task_id,
            title=title,
            summary=summary,
            changes=changes,
            test_results=test_results,
            validation_results=validation_results,
            concerns=concerns,
        )

        self._reviews[review_id] = review
        self._save_review(review)

        # Send macOS notification
        concern_indicator = "⚠️ " if concerns else ""
        send_macos_notification(
            title=f"👀 Sharpy: Review Required",
            message=f"{concern_indicator}{title[:80]}{'...' if len(title) > 80 else ''}",
            subtitle=f"Task: {task_id} | {len(changes)} changes",
        )

        return review

    def _save_review(self, review: HumanReviewRequest) -> None:
        """Save a review request to the review directory."""
        review_file = self.review_dir / f"{review.id}.json"
        with open(review_file, "w") as f:
            json.dump(review.to_dict(), f, indent=2)

        # Create human-readable markdown
        md_file = self.review_dir / f"{review.id}.md"
        md_content = self._format_review_md(review)
        with open(md_file, "w") as f:
            f.write(md_content)

    def _format_review_md(self, review: HumanReviewRequest) -> str:
        """Format a review request as readable markdown."""
        lines = [
            f"# Review Request: {review.title}",
            "",
            f"**ID:** {review.id}",
            f"**Task:** {review.task_id}",
            f"**Created:** {review.created_at}",
            f"**Status:** {review.status}",
            "",
            "## Summary",
            "",
            review.summary,
            "",
            "## Changes Made",
            "",
        ]

        for change in review.changes:
            lines.append(f"- {change}")
        lines.append("")

        lines.extend(
            [
                "## Test Results",
                "",
                "```",
                review.test_results,
                "```",
                "",
                "## Validation Results",
                "",
            ]
        )

        for result in review.validation_results:
            lines.append(f"### {result.get('agent', 'Unknown Agent')}")
            lines.append(f"**Status:** {result.get('status', 'unknown')}")
            if result.get("findings"):
                lines.append("**Findings:**")
                for finding in result["findings"]:
                    lines.append(f"- {finding}")
            lines.append("")

        if review.concerns:
            lines.extend(
                [
                    "## Concerns",
                    "",
                ]
            )
            for concern in review.concerns:
                lines.append(f"- ⚠️ {concern}")
            lines.append("")

        lines.extend(
            [
                "## How to Respond",
                "",
                "Create a file in the review directory:",
                f"  `{self.review_dir}/{review.id}_response.json`",
                "",
                "With content:",
                "```json",
                "{",
                '  "status": "approved",  // or "rejected" or "needs_changes"',
                '  "notes": "Your review notes"',
                "}",
                "```",
            ]
        )

        return "\n".join(lines)

    def check_for_answer(self, question_id: str) -> Optional[str]:
        """Check if an answer exists for a question."""
        answer_file = self.answers_dir / f"{question_id}.json"

        if answer_file.exists():
            try:
                with open(answer_file) as f:
                    data = json.load(f)
                return data.get("answer")
            except Exception:
                pass

        return None

    def check_for_review_response(self, review_id: str) -> Optional[dict]:
        """Check if a response exists for a review request."""
        response_file = self.review_dir / f"{review_id}_response.json"

        if response_file.exists():
            try:
                with open(response_file) as f:
                    return json.load(f)
            except Exception:
                pass

        return None

    async def wait_for_answer(
        self,
        question: HumanQuestion,
        timeout: Optional[float] = None,
    ) -> Optional[str]:
        """Wait for a human answer to a question."""
        start_time = time.time()

        while True:
            answer = self.check_for_answer(question.id)
            if answer is not None:
                question.answer = answer
                question.status = QuestionStatus.ANSWERED
                question.answered_at = datetime.now().isoformat()
                question.answer_source = "file"
                self._save_question(question)
                return answer

            if timeout and (time.time() - start_time) > timeout:
                question.status = QuestionStatus.EXPIRED
                self._save_question(question)
                return None

            await asyncio.sleep(self.check_interval)

    async def wait_for_review(
        self,
        review: HumanReviewRequest,
        timeout: Optional[float] = None,
    ) -> Optional[dict]:
        """Wait for a human review response."""
        start_time = time.time()

        while True:
            response = self.check_for_review_response(review.id)
            if response is not None:
                review.status = response.get("status", "unknown")
                review.reviewer_notes = response.get("notes")
                review.reviewed_at = datetime.now().isoformat()
                self._save_review(review)
                return response

            if timeout and (time.time() - start_time) > timeout:
                return None

            await asyncio.sleep(self.check_interval)

    def get_pending_questions(self) -> list[HumanQuestion]:
        """Get all pending questions."""
        pending = []
        for question_file in self.questions_dir.glob("*.json"):
            try:
                with open(question_file) as f:
                    data = json.load(f)
                question = HumanQuestion.from_dict(data)
                # Check if status is pending AND no answer file exists
                if question.status == QuestionStatus.PENDING:
                    answer_file = self.answers_dir / f"{question.id}.json"
                    if not answer_file.exists():
                        pending.append(question)
            except Exception:
                continue
        return sorted(pending, key=lambda q: q.created_at, reverse=True)

    def get_pending_reviews(self) -> list[HumanReviewRequest]:
        """Get all pending review requests."""
        pending = []
        for review_file in self.review_dir.glob("*.json"):
            if "_response" in review_file.name:
                continue
            try:
                with open(review_file) as f:
                    data = json.load(f)
                review = HumanReviewRequest.from_dict(data)
                # Check if status is pending AND no response file exists
                if review.status == "pending":
                    response_file = self.review_dir / f"{review.id}_response.json"
                    if not response_file.exists():
                        pending.append(review)
            except Exception:
                continue
        return sorted(pending, key=lambda r: r.created_at, reverse=True)

    def generate_status_report(self) -> str:
        """Generate a status report of all human interactions."""
        pending_questions = self.get_pending_questions()
        pending_reviews = self.get_pending_reviews()

        lines = [
            "# Human Interaction Status",
            "",
            f"**Generated:** {datetime.now().isoformat()}",
            "",
            f"## Pending Questions ({len(pending_questions)})",
            "",
        ]

        if pending_questions:
            for q in pending_questions:
                lines.append(
                    f"- [{q.priority.value.upper()}] {q.id}: {q.question[:80]}..."
                )
        else:
            lines.append("*No pending questions*")

        lines.extend(
            [
                "",
                f"## Pending Reviews ({len(pending_reviews)})",
                "",
            ]
        )

        if pending_reviews:
            for r in pending_reviews:
                lines.append(f"- {r.id}: {r.title}")
        else:
            lines.append("*No pending reviews*")

        return "\n".join(lines)
