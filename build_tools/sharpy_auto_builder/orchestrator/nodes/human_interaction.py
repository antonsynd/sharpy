"""
Human interaction nodes for the orchestrator.

Contains nodes related to human review requests, questions, and interrupt handling.
"""

import subprocess
from typing import TYPE_CHECKING, Optional

from langgraph.types import interrupt

from ..types import (
    OrchestratorState,
    HumanQuestionPayload,
    HumanReviewPayload,
    HumanResponse,
)
from ...human_loop import QuestionPriority

if TYPE_CHECKING:
    from ..core import Orchestrator


class HumanInteractionNodes:
    """Mixin class providing human interaction node implementations."""

    async def _request_human_review_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Request human review of the implementation using native LangGraph interrupt."""
        task_data = state["current_task"]
        task_id = task_data["id"]
        execution_result = state.get("last_execution_result", {})
        validation_results = state.get("validation_results", [])

        # Build files_changed list (idempotent - just reads git status)
        files_changed = []
        try:
            result = subprocess.run(
                ["git", "status", "--short"],
                cwd=self.config.repo_root,
                capture_output=True,
                text=True,
                timeout=5,
            )
            if result.returncode == 0:
                for line in result.stdout.strip().split("\n"):
                    if line.strip():
                        parts = line.strip().split(maxsplit=1)
                        if len(parts) == 2:
                            files_changed.append(parts[1])
        except Exception as e:
            self._log_execution(
                event_type="git_status_error",
                task_id=task_id,
                error=str(e),
            )

        # Get diff summary (idempotent)
        diff_summary = ""
        try:
            result = subprocess.run(
                ["git", "diff", "--stat"],
                cwd=self.config.repo_root,
                capture_output=True,
                text=True,
                timeout=10,
            )
            if result.returncode == 0:
                diff_summary = result.stdout.strip()
        except Exception as e:
            self._log_execution(
                event_type="git_diff_error",
                task_id=task_id,
                error=str(e),
            )

        # Get full diff content (idempotent)
        diff_content = ""
        try:
            result = subprocess.run(
                ["git", "diff"],
                cwd=self.config.repo_root,
                capture_output=True,
                text=True,
                timeout=30,
            )
            if result.returncode == 0:
                diff_content = result.stdout.strip()
                max_diff_chars = 50000
                if len(diff_content) > max_diff_chars:
                    diff_content = (
                        diff_content[:max_diff_chars]
                        + "\n\n... [diff truncated, run 'git diff' for full content]"
                    )
        except Exception as e:
            self._log_execution(
                event_type="git_diff_content_error",
                task_id=task_id,
                error=str(e),
            )

        # Build the review payload (idempotent)
        review_payload: HumanReviewPayload = {
            "type": "review",
            "task_id": task_id,
            "task_description": task_data.get(
                "description", task_data.get("title", "")
            ),
            "execution_result": execution_result,
            "validation_results": validation_results,
            "files_changed": files_changed,
            "diff_summary": diff_summary,
            "diff_content": diff_content,
        }

        # Log that we're requesting review
        self._log_execution(
            event_type="human_review_requested",
            task_id=task_id,
            extra={
                "files_changed_count": len(files_changed),
                "validation_count": len(validation_results),
                "diff_content_chars": len(diff_content),
            },
        )

        # Call interrupt - this will pause execution and wait for human response
        human_response: HumanResponse = interrupt(review_payload)

        # After resume: Log the response
        self._log_execution(
            event_type="human_review_received",
            task_id=task_id,
            extra={
                "approved": human_response.get("approved", False),
                "retry": human_response.get("retry", False),
                "has_feedback": bool(human_response.get("feedback")),
            },
        )

        # Route based on response
        if human_response.get("approved"):
            next_action = "commit_changes"
            messages = [f"Human approved task {task_id}"]
            if human_response.get("feedback"):
                messages.append(f"Feedback: {human_response['feedback']}")
        elif human_response.get("retry"):
            next_action = "execute_implementation"
            messages = [f"Human requested retry for task {task_id}"]
            if human_response.get("feedback"):
                messages.append(f"Feedback: {human_response['feedback']}")
        else:
            # Skip this task
            next_action = "update_ground_truth"
            messages = [f"Human skipped task {task_id}"]
            if human_response.get("feedback"):
                messages.append(f"Feedback: {human_response['feedback']}")

        return {
            **state,
            "human_response": human_response,
            "next_action": next_action,
            "messages": messages,
        }

    def _ask_human_question(
        self: "Orchestrator",
        task_id: str,
        question: str,
        priority: str = "medium",
        context: str = "",
        options: Optional[list[str]] = None,
    ) -> dict:
        """
        Ask human a question using native LangGraph interrupt.

        Args:
            task_id: ID of the task this question is about
            question: The question to ask
            priority: Priority level ("high", "medium", "low")
            context: Additional context for the question
            options: Optional list of predefined answer options

        Returns:
            Dictionary containing the human's response
        """
        # Get task description for context
        task_description = ""
        current_task = self.ground_truth.get_task_by_id(task_id)
        if current_task:
            task_description = current_task.description or current_task.title

        # Build the question payload (idempotent)
        question_payload: HumanQuestionPayload = {
            "type": "question",
            "task_id": task_id,
            "task_description": task_description,
            "question": question,
            "priority": priority,
            "context": context,
            "options": options,
        }

        # Log that we're asking a question
        self._log_execution(
            event_type="human_question_asked",
            task_id=task_id,
            extra={
                "question": question[:100],
                "priority": priority,
                "has_options": options is not None,
            },
        )

        # Call interrupt - this will pause execution and wait for human response
        human_response: dict = interrupt(question_payload)

        # After resume: Log the response
        self._log_execution(
            event_type="human_question_answered",
            task_id=task_id,
            extra={
                "answer": str(human_response.get("value", ""))[:100],
                "has_feedback": bool(human_response.get("additional_feedback")),
            },
        )

        return human_response

    @staticmethod
    def _validate_review_response(response: dict) -> tuple[bool, Optional[str]]:
        """
        Validate a human review response.

        Args:
            response: The response dictionary from the human

        Returns:
            Tuple of (is_valid, error_message)
        """
        if not isinstance(response, dict):
            return False, "Response must be a dictionary"

        if "approved" not in response and "retry" not in response:
            return False, "Response must include either 'approved' or 'retry' field"

        if "approved" in response and not isinstance(response["approved"], bool):
            return False, "'approved' must be a boolean (true/false)"

        if "retry" in response and not isinstance(response["retry"], bool):
            return False, "'retry' must be a boolean (true/false)"

        if response.get("approved") and response.get("retry"):
            return False, "Cannot both approve and request retry"

        if "feedback" in response and response["feedback"] is not None:
            if not isinstance(response["feedback"], str):
                return False, "'feedback' must be a string if provided"

        if "modified_value" in response and response["modified_value"] is not None:
            if not isinstance(response["modified_value"], str):
                return False, "'modified_value' must be a string if provided"

        return True, None

    @staticmethod
    def _validate_question_response(
        response: dict, options: Optional[list[str]] = None
    ) -> tuple[bool, Optional[str]]:
        """
        Validate a human question response.

        Args:
            response: The response dictionary from the human
            options: Optional list of valid answer options

        Returns:
            Tuple of (is_valid, error_message)
        """
        if not isinstance(response, dict):
            return False, "Response must be a dictionary"

        if "value" not in response:
            return False, "Response must include 'value' field with the answer"

        answer = response["value"]

        if options:
            if answer not in options:
                valid_opts = ", ".join(f"'{opt}'" for opt in options)
                return False, f"Answer must be one of: {valid_opts}"

        if not answer or (isinstance(answer, str) and not answer.strip()):
            return False, "Answer cannot be empty"

        if (
            "additional_feedback" in response
            and response["additional_feedback"] is not None
        ):
            if not isinstance(response["additional_feedback"], str):
                return False, "'additional_feedback' must be a string if provided"

        return True, None

    def _interrupt_with_validation(
        self: "Orchestrator",
        payload: dict,
        validator: callable,
        max_attempts: int = 3,
    ) -> dict:
        """
        Call interrupt with validation loop, re-prompting on invalid input.

        Args:
            payload: The interrupt payload to send
            validator: Function that validates response
            max_attempts: Maximum number of attempts before giving up

        Returns:
            Valid response dictionary

        Raises:
            RuntimeError: If max attempts exceeded without valid response
        """
        attempt = 0

        while attempt < max_attempts:
            attempt += 1

            response = interrupt(payload)

            is_valid, error_message = validator(response)

            if is_valid:
                return response

            self._log_execution(
                event_type="invalid_interrupt_response",
                task_id=payload.get("task_id", "unknown"),
                extra={
                    "attempt": attempt,
                    "error": error_message,
                    "max_attempts": max_attempts,
                },
            )

            payload["validation_error"] = error_message
            payload["attempt"] = attempt

        error_msg = f"Failed to get valid response after {max_attempts} attempts"
        self._log_execution(
            event_type="interrupt_validation_failed",
            task_id=payload.get("task_id", "unknown"),
            error=error_msg,
        )
        raise RuntimeError(error_msg)
