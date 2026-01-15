"""
Follow-up task creation for the orchestrator.

Contains functions for creating follow-up tasks when fixes fail or execution errors occur.
"""

from pathlib import Path
from typing import TYPE_CHECKING

from .types import OrchestratorState
from ..state import GroundTruth

if TYPE_CHECKING:
    from .core import Orchestrator


class FollowupTaskMixin:
    """Mixin class providing follow-up task creation methods."""

    async def _create_test_fix_followup_task(
        self: "Orchestrator", state: OrchestratorState
    ) -> None:
        """Create a follow-up task when agent fails to fix tests."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        test_output = execution_result.get("test_output", "")
        fix_attempt = state.get("fix_attempt", 0)

        # Reload ground truth and get the original task
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))
        original_task = self.ground_truth.get_task(task_data["id"])

        if not original_task:
            return

        # Create description with context about what failed
        description = f"""Fix test failures introduced during task {task_data['id']}: {task_data['title']}

## Context
The original task was implemented but caused test failures that could not be
automatically fixed after {fix_attempt} attempts.

## Test Failure Summary
```
{test_output[:1500]}
```

## Instructions
1. Review the failing tests and the implementation from task {task_data['id']}
2. Identify the root cause of the test failures
3. Fix the implementation (not the test expected values)
4. Ensure all tests pass before marking complete

## Related Files
{chr(10).join(f"- {f}" for f in task_data.get('files', []))}
"""

        # Add the follow-up task
        new_task = self.ground_truth.add_followup_task(
            original_task=original_task,
            title=f"Fix test failures from: {task_data['title'][:50]}",
            description=description,
            files=task_data.get("files"),
        )

        # Add a note to the original task
        original_task.notes.append(
            f"Test fix failed after {fix_attempt} attempts. Follow-up task created: {new_task.id}"
        )

        # Save updated ground truth
        self.ground_truth.save(Path(state["ground_truth_path"]))

        self._log_execution(
            event_type="followup_task_created",
            task_id=task_data["id"],
            extra={
                "followup_task_id": new_task.id,
                "fix_attempts": fix_attempt,
                "reason": "test_fix_failure",
            },
        )

    async def _create_validation_fix_followup_task(
        self: "Orchestrator", state: OrchestratorState
    ) -> None:
        """Create a follow-up task when validation issues can't be fixed."""
        task_data = state["current_task"]
        validation_results = state.get("validation_results", [])
        validation_fix_attempt = state.get("validation_fix_attempt", 0)

        # Reload ground truth and get the original task
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))
        original_task = self.ground_truth.get_task(task_data["id"])

        if not original_task:
            return

        # Extract issues for the follow-up description
        actionable_issues = self._extract_actionable_issues(validation_results)
        issues_summary = "\n".join(
            [
                f"- [{issue['severity'].upper()}] ({issue['agent']}): {issue['description'][:200]}"
                for issue in actionable_issues[:5]
            ]
        )

        # Collect validation context
        validation_context = "\n\n".join(
            [
                f"### {vr['agent']}\n{vr.get('raw_output', 'No output')[:800]}"
                for vr in validation_results
                if vr.get("status") != "passed"
            ]
        )

        # Create description with context about what failed
        description = f"""Address validation issues from task {task_data['id']}: {task_data['title']}

## Context
The original task was implemented but validation checks (spec adherence, verification,
hallucination defense) found issues that could not be automatically resolved after
{validation_fix_attempt} attempts.

## Unresolved Issues
{issues_summary}

## Validation Reports
{validation_context[:2000]}

## Instructions
1. Review each issue identified by the validation agents
2. For spec deviations: Update implementation to exactly match the specification
3. For verification failures: Ensure behavior is correct and consistent
4. For hallucination issues: Correct any factually incorrect implementations
5. Do NOT modify test expected values - fix the implementation
6. Run all validation checks after your fixes

## Related Files
{chr(10).join(f"- {f}" for f in task_data.get('files', []))}
"""

        # Add the follow-up task
        new_task = self.ground_truth.add_followup_task(
            original_task=original_task,
            title=f"Fix validation issues from: {task_data['title'][:50]}",
            description=description,
            files=task_data.get("files"),
        )

        # Add a note to the original task
        original_task.notes.append(
            f"Validation fix failed after {validation_fix_attempt} attempts. "
            f"Follow-up task created: {new_task.id}"
        )

        # Save updated ground truth
        self.ground_truth.save(Path(state["ground_truth_path"]))

        self._log_execution(
            event_type="followup_task_created",
            task_id=task_data["id"],
            extra={
                "followup_task_id": new_task.id,
                "validation_fix_attempts": validation_fix_attempt,
                "reason": "validation_fix_failure",
                "issues_count": len(actionable_issues),
            },
        )

    async def _create_execution_error_followup_task(
        self: "Orchestrator", state: OrchestratorState
    ) -> None:
        """Create a follow-up task when execution fails after max retries.

        FEEDBACK LOOP: This ensures that failed tasks are not forgotten.
        The follow-up task captures the execution context, errors, and any
        validation feedback so the issue can be addressed in a future session.
        """
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        validation_results = state.get("validation_results", [])
        human_response = state.get("human_response")
        attempt = state.get("execution_attempt", 0)

        # Reload ground truth and get the original task
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))
        original_task = self.ground_truth.get_task(task_data["id"])

        if not original_task:
            return

        # Build context about what failed
        error_details = execution_result.get("error", "Unknown error")[:1000]

        # Include validation feedback if any
        validation_context = ""
        if validation_results:
            validation_context = "\n## Validation Feedback\n"
            for vr in validation_results:
                if vr.get("status") != "passed":
                    validation_context += f"\n### {vr['agent']}\n{vr.get('raw_output', 'No output')[:500]}\n"

        # Include human feedback if any
        human_feedback = ""
        if human_response and human_response.get("feedback"):
            human_feedback = (
                f"\n## Human Reviewer Feedback\n{human_response['feedback']}\n"
            )

        description = f"""Resume implementation of task {task_data['id']}: {task_data['title']}

## Context
The original task failed after {attempt} execution attempts and could not be
automatically completed. This follow-up task is created to track the work
and allow resumption in a future session.

## Last Error
```
{error_details}
```
{validation_context}
{human_feedback}
## Instructions
1. Review the error details and any validation feedback above
2. Address the specific issues that caused the failures
3. Complete the original task requirements
4. Run tests to verify the implementation

## Related Files
{chr(10).join(f"- {f}" for f in task_data.get('files', []))}

## Original Task Description
{task_data.get('description', task_data.get('title', ''))}
"""

        # Add the follow-up task
        new_task = self.ground_truth.add_followup_task(
            original_task=original_task,
            title=f"Resume: {task_data['title'][:50]}",
            description=description,
            files=task_data.get("files"),
        )

        # Add a note to the original task
        original_task.notes.append(
            f"Execution failed after {attempt} attempts. Follow-up task created: {new_task.id}"
        )

        # Save updated ground truth
        self.ground_truth.save(Path(state["ground_truth_path"]))

        self._log_execution(
            event_type="followup_task_created",
            task_id=task_data["id"],
            extra={
                "followup_task_id": new_task.id,
                "execution_attempts": attempt,
                "reason": "execution_error",
                "had_validation_feedback": bool(validation_results),
                "had_human_feedback": bool(
                    human_response and human_response.get("feedback")
                ),
            },
        )
