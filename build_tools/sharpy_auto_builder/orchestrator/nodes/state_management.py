"""
State management nodes for the orchestrator.

Contains nodes related to ground truth updates, commits, and error handling.
"""

from datetime import datetime, timedelta
from pathlib import Path
from typing import TYPE_CHECKING

from ..types import OrchestratorState
from ...state import (
    GroundTruth,
    TaskStatus,
    TaskExecution,
    ValidationResult,
    ValidationStatus,
)

if TYPE_CHECKING:
    from ..core import Orchestrator


class StateManagementNodes:
    """Mixin class providing state management node implementations."""

    async def _commit_changes_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Commit changes made during task implementation."""
        if not self.config.auto_commit:
            return {
                **state,
                "next_action": "update",
                "messages": ["Auto-commit disabled, skipping commit"],
            }

        task_data = state["current_task"]
        task_id = task_data["id"]
        task_title = task_data["title"]
        self._log_step_start("commit_changes", task_id)

        try:
            # Get list of changed files
            status_result = await self.backend_manager.execute_command(
                "git status --porcelain",
                cwd=self.config.project_root,
            )

            if not status_result.success:
                self._log_execution(
                    event_type="commit_failed",
                    task_id=task_id,
                    error=f"Failed to get git status: {status_result.error}",
                )
                return {
                    **state,
                    "next_action": "update",
                    "messages": ["Failed to get git status, skipping commit"],
                }

            changed_files = status_result.output.strip()
            if not changed_files:
                return {
                    **state,
                    "next_action": "update",
                    "messages": ["No changes to commit"],
                }

            # Stage all changes
            add_result = await self.backend_manager.execute_command(
                "git add -A",
                cwd=self.config.project_root,
            )

            if not add_result.success:
                self._log_execution(
                    event_type="commit_failed",
                    task_id=task_id,
                    error=f"Failed to stage changes: {add_result.error}",
                )
                return {
                    **state,
                    "next_action": "update",
                    "messages": ["Failed to stage changes, skipping commit"],
                }

            # Generate commit message
            safe_title = task_title.replace('"', '\\"').replace("'", "\\'")
            commit_message = f"[auto] Task {task_id}: {safe_title}"

            # Create the commit
            commit_result = await self.backend_manager.execute_command(
                f'git commit -m "{commit_message}"',
                cwd=self.config.project_root,
            )

            if not commit_result.success:
                if "nothing to commit" in (commit_result.output or "").lower():
                    return {
                        **state,
                        "next_action": "update",
                        "messages": ["No changes to commit (already committed)"],
                    }

                self._log_execution(
                    event_type="commit_failed",
                    task_id=task_id,
                    error=f"Failed to commit: {commit_result.error or commit_result.output}",
                )
                return {
                    **state,
                    "next_action": "update",
                    "messages": [
                        f"Failed to commit changes: {commit_result.error or commit_result.output}"
                    ],
                }

            # Log successful commit
            self._log_execution(
                event_type="commit_success",
                task_id=task_id,
                output=commit_result.output,
                extra={
                    "commit_message": commit_message,
                    "files_changed": changed_files,
                },
            )

            # Count files changed
            file_count = len([f for f in changed_files.split("\n") if f.strip()])

            # Push the commit
            push_result = await self.backend_manager.execute_command(
                "git push",
                cwd=self.config.project_root,
            )

            if not push_result.success:
                self._log_execution(
                    event_type="push_failed",
                    task_id=task_id,
                    error=f"Failed to push: {push_result.error or push_result.output}",
                )
                return {
                    **state,
                    "next_action": "update",
                    "messages": [
                        f"Committed {file_count} file(s): {commit_message}",
                        f"Warning: Failed to push: {push_result.error or push_result.output}",
                    ],
                }

            # Log successful push
            self._log_execution(
                event_type="push_success",
                task_id=task_id,
                output=push_result.output,
            )

            self._log_step_end("commit_changes", task_id, True, f"{file_count} file(s)")
            return {
                **state,
                "next_action": "update",
                "messages": [
                    f"Committed and pushed {file_count} file(s): {commit_message}"
                ],
            }

        except Exception as e:
            self._log_step_end("commit_changes", task_id, False, str(e)[:50])
            self._log_execution(
                event_type="commit_failed",
                task_id=task_id,
                error=str(e),
            )
            return {
                **state,
                "next_action": "update",
                "messages": [f"Exception during commit: {e}"],
            }

    async def _update_ground_truth_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Update ground truth with task results."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        validation_results = state.get("validation_results", [])

        # Load current ground truth
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))

        # Find and update task
        task = self.ground_truth.get_task(task_data["id"])
        if task:
            # Create execution record
            execution = TaskExecution(
                attempt_number=state.get("execution_attempt", 1),
                backend=execution_result.get("backend", "unknown"),
                success=execution_result.get("success", False),
                error_message=execution_result.get("error"),
                tests_run=execution_result.get("tests_run", False),
                tests_passed=execution_result.get("tests_passed", False),
                validation_results=[
                    ValidationResult(
                        agent=vr["agent"],
                        status=ValidationStatus(vr.get("status", "pending")),
                        findings=vr.get("findings", []),
                        raw_output=vr.get("raw_output"),
                    )
                    for vr in validation_results
                ],
            )
            execution.completed_at = datetime.now().isoformat()
            task.executions.append(execution)

            # Update task status
            baseline_test_passed = state.get("baseline_test_passed")
            validations_passed = all(
                vr.get("status") == "passed" for vr in validation_results
            )

            tests_ok = execution.tests_passed or (
                baseline_test_passed is False and validations_passed
            )

            if execution.success and tests_ok:
                task.status = TaskStatus.COMPLETED
            elif state.get("execution_attempt", 0) >= self.config.max_retries_per_task:
                task.status = TaskStatus.FAILED

            # Update stats
            backend_key = execution.backend.lower().replace("-", "_")
            if backend_key not in self.ground_truth.backend_stats:
                self.ground_truth.backend_stats[backend_key] = {
                    "attempts": 0,
                    "successes": 0,
                    "failures": 0,
                }

            self.ground_truth.total_attempts += 1
            if execution.success:
                self.ground_truth.total_successes += 1
                self.ground_truth.backend_stats[backend_key]["successes"] += 1
            else:
                self.ground_truth.total_failures += 1
                self.ground_truth.backend_stats[backend_key]["failures"] += 1
            self.ground_truth.backend_stats[backend_key]["attempts"] += 1

        # Store patterns in memory for future learning
        if self.memory_manager and self.config.memory.enabled:
            try:
                task_desc = task_data.get("description", task_data.get("title", ""))
                files = task_data.get("files", [])

                if execution.success and tests_ok:
                    solution = self._extract_solution_summary(execution_result)
                    task_type = task_data.get("phase", "implementation")

                    self.memory_manager.store_implementation_pattern(
                        task_type=task_type,
                        description=task_desc,
                        solution=solution,
                        files=files,
                        tags=[task_type, "success"],
                        task_id=task_data["id"],
                        metadata={
                            "backend": execution.backend,
                            "tests_passed": execution.tests_passed,
                            "attempt_number": execution.attempt_number,
                        },
                    )
                elif execution.error_message:
                    error_type = self._categorize_error(execution.error_message)
                    solution = self._extract_solution_summary(execution_result)

                    self.memory_manager.store_error_pattern(
                        error_type=error_type,
                        description=task_desc,
                        error_message=execution.error_message,
                        solution=solution if solution else "No solution found",
                        files=files,
                        task_id=task_data["id"],
                    )
            except Exception as e:
                print(f"Warning: Failed to store pattern in memory: {e}")

        # Save updated ground truth
        self.ground_truth.save(Path(state["ground_truth_path"]))

        # Check if there are more tasks
        next_task = self.ground_truth.get_next_pending_task()

        return {
            **state,
            "next_action": "next_task" if next_task else "complete",
            "messages": [f"Ground truth updated for task {task_data['id']}"],
        }

    async def _handle_error_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Handle errors during execution."""
        attempt = state.get("execution_attempt", 0)
        task_data = state["current_task"]
        last_result = state.get("last_execution_result", {}) or {}
        error_msg = last_result.get("error") or ""

        # Check if this is a rate limit exhaustion error
        is_rate_limited = (
            "exhausted" in error_msg.lower() or "rate limit" in error_msg.lower()
        )

        if is_rate_limited and self.config.rate_limit_pause_hours > 0:
            pause_hours = self.config.rate_limit_pause_hours
            resume_time = datetime.now() + timedelta(hours=pause_hours)

            print(f"\n{'='*60}")
            print(f"⏸️  SESSION PAUSED - Rate limit reached")
            print(f"{'='*60}")
            print(f"\nAll backends are rate-limited.")
            print(f"Session checkpointed. Resume after {pause_hours} hours.")
            print(
                f"\nEstimated resume time: {resume_time.strftime('%Y-%m-%d %H:%M:%S')}"
            )
            print(f"\n📌 Session saved with thread ID: {self._current_thread_id}")
            print(f"\n▶️  To resume this session, run:")
            print(f"   ./auto_builder.sh run --thread-id {self._current_thread_id}")
            print(f"\n{'='*60}\n")

            return {
                **state,
                "execution_attempt": 0,
                "next_action": "pause_rate_limited",
                "messages": [
                    f"Session paused due to rate limiting. Thread ID: {self._current_thread_id}"
                ],
            }

        if attempt < self.config.max_retries_per_task:
            return {
                **state,
                "next_action": "retry",
                "messages": [
                    f"Error handled, will retry (attempt {attempt}/{self.config.max_retries_per_task})"
                ],
            }

        # Max retries reached
        if (
            task_data.get("is_critical")
            and self.config.require_human_approval_for_critical
        ):
            return {
                **state,
                "next_action": "human",
                "messages": [
                    "Max retries reached for critical task, escalating to human"
                ],
            }

        # Create follow-up task for failed non-critical tasks
        if self.config.create_followup_task_on_fix_failure:
            await self._create_execution_error_followup_task(state)

        return {
            **state,
            "next_action": "skip",
            "messages": [
                (
                    "Max retries reached, skipping task (follow-up task created)"
                    if self.config.create_followup_task_on_fix_failure
                    else "Max retries reached, skipping task"
                )
            ],
        }

    # =========================================================================
    # Helper methods
    # =========================================================================

    def _extract_solution_summary(self: "Orchestrator", result: dict) -> str:
        """
        Extract key solution parts from execution result.

        Args:
            result: Execution result dictionary

        Returns:
            Summary of the solution/approach
        """
        output = result.get("output", "")
        max_length = 500
        if len(output) > max_length:
            return output[:max_length] + "..."
        return output

    def _categorize_error(self: "Orchestrator", error_message: str) -> str:
        """
        Categorize error type from error message.

        Args:
            error_message: The error message

        Returns:
            Error category
        """
        error_lower = error_message.lower()

        if "syntax" in error_lower:
            return "syntax_error"
        elif "type" in error_lower or "typing" in error_lower:
            return "type_error"
        elif "import" in error_lower or "module" in error_lower:
            return "import_error"
        elif "test" in error_lower and "fail" in error_lower:
            return "test_failure"
        elif "timeout" in error_lower:
            return "timeout_error"
        elif "rate limit" in error_lower:
            return "rate_limit_error"
        elif "file not found" in error_lower or "no such file" in error_lower:
            return "file_not_found"
        elif "permission" in error_lower:
            return "permission_error"
        else:
            return "unknown_error"
