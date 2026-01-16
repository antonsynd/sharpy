"""
Task execution nodes for the orchestrator.

Contains nodes related to task selection, planning, execution, testing, and fixing.
"""

from pathlib import Path
from typing import TYPE_CHECKING

from ..types import OrchestratorState
from ...state import GroundTruth, TaskStatus
from ...agents import get_specialist_for_task, get_agent_prompt
from ...tasks import run_tests

if TYPE_CHECKING:
    from ..core import Orchestrator


class TaskExecutionNodes:
    """Mixin class providing task execution node implementations."""

    async def _select_task_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Select the next task to work on."""
        # Reload ground truth to get latest state
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))

        # Check for tasks awaiting human review
        tasks_awaiting = self.ground_truth.get_tasks_awaiting_review()
        if tasks_awaiting:
            task = tasks_awaiting[0]
            # Create a review request so wait_for_human has something to check
            review = self.human_loop.create_review_request(
                task_id=task.id,
                title=f"Review: {task.title}",
                summary=f"Task {task.id} is awaiting human review",
                changes=[],
                test_results="See task notes for details",
                validation_results=[],
                concerns=task.notes or [],
            )
            return {
                **state,
                "current_task": task.to_dict(),
                "human_review_id": review.id,
                "awaiting_human_input": True,
                "next_action": "wait_human",
                "messages": [
                    f"Task {task.id} is awaiting human review",
                    f"  Review ID: {review.id}",
                ],
            }

        # Get next pending task
        task = self.ground_truth.get_next_pending_task()
        if not task:
            return {
                **state,
                "current_task": None,
                "next_action": "complete",
                "messages": ["All tasks completed or blocked"],
            }

        # Update task status
        task.status = TaskStatus.IN_PROGRESS
        self.ground_truth.current_task_id = task.id
        self.ground_truth.save(Path(state["ground_truth_path"]))

        return {
            **state,
            "current_task": task.to_dict(),
            "execution_attempt": 0,
            "fix_attempt": 0,
            "validation_fix_attempt": 0,
            "baseline_test_passed": None,
            "baseline_test_output": None,
            "validation_results": [],
            "next_action": "plan",
            "messages": [f"Selected task {task.id}: {task.title}"],
        }

    async def _plan_implementation_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Plan the implementation approach."""
        task_data = state["current_task"]
        self._log_step_start("plan_implementation", task_data["id"], task_data["title"])

        # Determine which specialist to use
        specialist = get_specialist_for_task(task_data["id"], task_data["files"])

        # Get the planning prompt
        planning_prompt = f"""Analyze this task and create an implementation plan:

Task ID: {task_data['id']}
Title: {task_data['title']}
Description: {task_data['description']}
Files: {', '.join(task_data['files'])}

Provide:
1. Step-by-step implementation approach
2. Key files to modify
3. Tests to verify
4. Potential risks or questions
"""

        # Execute planning via backend
        result = await self.backend_manager.execute_with_failover(
            planning_prompt,
            context={"files": task_data["files"]},
        )

        self._log_step_end("plan_implementation", task_data["id"], result.success)
        return {
            **state,
            "messages": [f"Implementation plan created for {task_data['id']}"],
        }

    async def _execute_implementation_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Execute the implementation using an agent via task functions."""
        task_data = state["current_task"]
        attempt = state["execution_attempt"] + 1

        # Get specialist for this task
        specialist = get_specialist_for_task(task_data["id"], task_data["files"])
        self._log_step_start(
            "execute_implementation",
            task_data["id"],
            f"attempt {attempt}, specialist: {specialist.value}",
        )

        # Generate implementation prompt
        prompt = get_agent_prompt(
            specialist,
            task_title=task_data["title"],
            task_description=task_data["description"],
            files=task_data["files"],
            component=task_data["phase"],
        )

        # Add memory context from past implementations and errors
        if self.memory_manager and self.config.memory.enabled:
            task_desc = task_data["description"]

            # Get relevant implementation patterns
            impl_context = self.memory_manager.get_implementation_context(task_desc)
            if impl_context:
                prompt += f"\n\n{impl_context}"

            # Get error avoidance patterns
            error_context = self.memory_manager.get_error_avoidance_context(task_desc)
            if error_context:
                prompt += f"\n\n{error_context}"

        # Add context from previous attempts if any
        if state.get("last_execution_result"):
            prev = state["last_execution_result"]
            if not prev.get("success"):
                prompt += f"\n\nPrevious attempt failed with error:\n{prev.get('error', 'Unknown error')}"

        # FEEDBACK LOOP: Include human review feedback if this is a retry
        human_response = state.get("human_response")
        if (
            human_response
            and human_response.get("retry")
            and human_response.get("feedback")
        ):
            # Include the previous agent output so feedback like "do all 3" makes sense
            prev_output = state.get("last_execution_result", {}).get("output", "")
            if prev_output:
                # Truncate if very long but keep enough context
                max_prev_output = 4000
                if len(prev_output) > max_prev_output:
                    prev_output = prev_output[-max_prev_output:] + "\n... [earlier output truncated]"
                prompt += f"\n\n## Your Previous Response\n{prev_output}"

            prompt += f"\n\n## Human Reviewer Feedback (MUST ADDRESS)\nThe human reviewer requested a retry with the following feedback:\n{human_response['feedback']}\n\nPlease carefully address these concerns in this implementation attempt."

        # FEEDBACK LOOP: Include validation issues if we're retrying after validation failure
        validation_results = state.get("validation_results", [])
        if validation_results and attempt > 1:
            validation_feedback = []
            for vr in validation_results:
                agent = vr.get("agent", "unknown")
                status = vr.get("status", "")
                raw_output = vr.get("raw_output", "")
                if status != "passed" and raw_output:
                    truncated = (
                        raw_output[:1000] if len(raw_output) > 1000 else raw_output
                    )
                    validation_feedback.append(f"### {agent} Report\n{truncated}")

            if validation_feedback:
                prompt += "\n\n## Validation Issues (MUST ADDRESS)\nPrevious validation checks found issues that need to be addressed:\n\n"
                prompt += "\n\n---\n\n".join(validation_feedback[:3])
                prompt += "\n\nPlease fix the issues identified above before completing the task."

        # Log the prompt being sent
        self._log_execution(
            event_type="agent_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"attempt": attempt, "specialist": specialist.value},
        )

        # Execute via task functions with failover
        result = await self._execute_with_task_failover(
            prompt=prompt,
            task_id=task_data["id"],
            attempt=attempt,
            timeout=self.config.agent_execution_timeout,
        )

        # Log the full response
        self._log_execution(
            event_type="agent_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error or "",
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"attempt": attempt, "exit_code": result.exit_code},
        )

        self._log_step_end(
            "execute_implementation",
            task_data["id"],
            result.success,
            f"via {result.backend}",
        )

        execution_result = {
            "success": result.success,
            "output": result.output,
            "error": result.error,
            "backend": result.backend,
            "duration": result.duration_seconds,
        }

        messages = [
            f"Execution attempt {attempt} {'succeeded' if result.success else 'failed'}"
        ]
        if not result.success and result.error:
            error_preview = (
                result.error[:200] if len(result.error) > 200 else result.error
            )
            messages.append(f"  Error: {error_preview}")

        return {
            **state,
            "execution_attempt": attempt,
            "last_execution_result": execution_result,
            "next_action": "test" if result.success else "error",
            "messages": messages,
        }

    async def _run_baseline_tests_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Run tests before implementation to establish baseline."""
        task_data = state["current_task"]

        # Determine which tests to run based on task
        component = self._get_test_component(task_data)
        self._log_step_start(
            "run_baseline_tests",
            task_data["id"],
            f"component: {component}" if component else "all tests",
        )

        # Run baseline tests using task function with timeout
        test_cmd = self._build_test_command(component)
        result = await run_tests(
            test_command=test_cmd,
            working_dir=self.config.project_root,
            timeout=self.config.test_timeout,
            task_id=task_data["id"],
            attempt=0,
            use_fallback_idempotency=True,
        )

        # Log baseline test results
        self._log_execution(
            event_type="baseline_test_run",
            task_id=task_data["id"],
            output=result.output,
            error=result.error or "",
            success=result.success,
            extra={
                "test_command": test_cmd,
                "component": component,
                "exit_code": result.exit_code,
            },
        )

        # Handle timeout
        timed_out = (
            result.exit_code == -1 and "timed out" in (result.error or "").lower()
        )
        if timed_out:
            self._log_step_end("run_baseline_tests", task_data["id"], False, "TIMEOUT")
            return {
                **state,
                "baseline_test_passed": False,
                "baseline_test_output": f"TIMEOUT: Tests timed out after {self.config.test_timeout} seconds. This may indicate a pre-existing infinite loop in the test code.",
                "messages": [
                    f"Baseline tests TIMED OUT after {self.config.test_timeout}s (possible infinite loop)"
                ],
            }

        self._log_step_end("run_baseline_tests", task_data["id"], result.success)
        return {
            **state,
            "baseline_test_passed": result.success,
            "baseline_test_output": result.output if not result.success else None,
            "messages": [
                f"Baseline tests {'passed' if result.success else 'failed (pre-existing failures)'}"
            ],
        }

    async def _run_tests_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Run tests to verify implementation."""
        task_data = state["current_task"]

        component = self._get_test_component(task_data)
        test_cmd = self._build_test_command(component)
        self._log_step_start(
            "run_tests",
            task_data["id"],
            f"component: {component}" if component else "all tests",
        )

        # Run tests using task function
        result = await run_tests(
            test_command=test_cmd,
            working_dir=self.config.project_root,
            timeout=self.config.test_timeout,
            task_id=task_data["id"],
            attempt=state.get("execution_attempt", 1),
            use_fallback_idempotency=True,
        )

        # Detect timeout
        timed_out = (
            result.exit_code == -1 and "timed out" in (result.error or "").lower()
        )

        # Log test results
        self._log_execution(
            event_type="test_run",
            task_id=task_data["id"],
            output=result.output,
            error=result.error or "",
            success=result.success,
            extra={
                "test_command": test_cmd,
                "component": component,
                "exit_code": result.exit_code,
                "timed_out": timed_out,
            },
        )

        # Update execution result
        execution_result = state.get("last_execution_result", {})
        execution_result["tests_run"] = True
        execution_result["tests_passed"] = result.success
        execution_result["test_output"] = result.output
        execution_result["tests_timed_out"] = timed_out

        # Handle timeout case
        if timed_out:
            self._log_step_end("run_tests", task_data["id"], False, "TIMEOUT")
            baseline_timed_out = state.get("baseline_test_output", "").startswith(
                "TIMEOUT:"
            )
            if baseline_timed_out:
                next_action = "preexisting_failure"
                messages = [
                    f"Tests TIMED OUT after {self.config.test_timeout}s, but baseline tests also timed out",
                    "This is a pre-existing infinite loop issue, not caused by this implementation",
                ]
            else:
                next_action = "fix"
                execution_result["test_output"] = (
                    f"INFINITE LOOP DETECTED: Tests timed out after {self.config.test_timeout} seconds.\n"
                    "Your implementation appears to have introduced an infinite loop.\n"
                    "Please review your code for:\n"
                    "- While/for loops without proper termination conditions\n"
                    "- Recursive calls without base cases\n"
                    "- Circular dependencies or references\n"
                )
                messages = [
                    f"Tests TIMED OUT after {self.config.test_timeout}s - likely infinite loop introduced by agent",
                    "Agent will be asked to fix the infinite loop",
                ]

            return {
                **state,
                "last_execution_result": execution_result,
                "next_action": next_action,
                "messages": messages,
            }

        # Determine next action based on test results and baseline
        baseline_passed = state.get("baseline_test_passed")

        if result.success:
            next_action = "validate"
            messages = ["Tests passed"]
        elif baseline_passed is False:
            next_action = "preexisting_failure"
            messages = [
                "Tests failed, but they were already failing before implementation",
                "Proceeding to validation (pre-existing failures)",
            ]
        else:
            next_action = "fix"
            error_output = result.error or result.output or ""
            error_preview = (
                error_output[:300] if len(error_output) > 300 else error_output
            )
            messages = [
                "Tests failed after implementation - agent may have broken something",
                f"  Test output: {error_preview}" if error_preview else "",
            ]

        self._log_step_end("run_tests", task_data["id"], result.success)
        return {
            **state,
            "last_execution_result": execution_result,
            "next_action": next_action,
            "messages": [m for m in messages if m],
        }

    async def _fix_test_failures_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """Let the agent fix test failures they introduced."""
        task_data = state["current_task"]
        fix_attempt = state.get("fix_attempt", 0) + 1
        max_fix_attempts = self.config.max_test_fix_attempts
        self._log_step_start(
            "fix_test_failures",
            task_data["id"],
            f"attempt {fix_attempt}/{max_fix_attempts}",
        )

        if fix_attempt > max_fix_attempts:
            # Max fix attempts reached - create a follow-up task if configured
            if self.config.create_followup_task_on_fix_failure:
                await self._create_test_fix_followup_task(state)

            return {
                **state,
                "fix_attempt": fix_attempt,
                "next_action": "error",
                "error_message": f"Agent failed to fix tests after {max_fix_attempts} attempts",
                "messages": [
                    f"Max fix attempts ({max_fix_attempts}) reached",
                    (
                        "Created follow-up task for test fix"
                        if self.config.create_followup_task_on_fix_failure
                        else "Escalating to error handler"
                    ),
                ],
            }

        # Get the test failure details
        execution_result = state.get("last_execution_result", {})
        test_output = execution_result.get("test_output", "")
        tests_timed_out = execution_result.get("tests_timed_out", False)

        # Build appropriate prompt based on failure type
        if tests_timed_out:
            prompt = f"""CRITICAL: Your implementation caused an infinite loop!

## Task
{task_data['title']}

## Problem
The tests timed out after {self.config.test_timeout} seconds because your code contains an infinite loop.

## Error Details
```
{test_output[:3000]}
```

## Fix Attempt
This is fix attempt {fix_attempt} of {max_fix_attempts}.

## Instructions - FOCUS ON INFINITE LOOP
1. **Find the infinite loop** - Look for:
   - While loops without proper termination conditions
   - For loops that never reach their end condition
   - Recursive functions without base cases or that always recurse
   - Circular method calls (A calls B calls A)
   - Iterator/generator issues that never terminate

2. **Fix the loop termination** - Ensure:
   - All loops have reachable exit conditions
   - Recursive functions have proper base cases
   - No circular dependencies in your logic

3. **Test locally** before committing

4. Do NOT just add a counter/max iterations as a band-aid - fix the root cause

{f"Previous fix attempt {fix_attempt - 1} did not resolve the infinite loop. The issue persists." if fix_attempt > 1 else ""}
"""
        else:
            prompt = f"""You previously implemented a task but it caused test failures.

## Task
{task_data['title']}

## Test Failure Output
```
{test_output[:3000]}
```

## Fix Attempt
This is fix attempt {fix_attempt} of {max_fix_attempts}.

## Instructions
1. Analyze the test failures carefully
2. Identify the root cause - is it in your implementation or an incorrect test?
3. Fix the **implementation** to make the tests pass
4. Do NOT modify test expected values - fix the implementation instead
5. If you added new tests that are failing, you may fix those tests
6. Run the tests after your fix to verify

Focus only on fixing the failing tests. Do not re-implement the entire task.

{f"Previous fix attempt failed. Try a different approach." if fix_attempt > 1 else ""}
"""

        # Log the fix prompt
        self._log_execution(
            event_type="fix_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={
                "fix_attempt": fix_attempt,
                "max_fix_attempts": max_fix_attempts,
                "is_infinite_loop": tests_timed_out,
            },
        )

        # Execute fix via backend
        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the fix response
        self._log_execution(
            event_type="fix_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"fix_attempt": fix_attempt},
        )

        messages = [
            f"Fix attempt {fix_attempt}/{max_fix_attempts} {'succeeded' if result.success else 'failed'}"
        ]
        if not result.success and result.error:
            error_preview = (
                result.error[:200] if len(result.error) > 200 else result.error
            )
            messages.append(f"  Error: {error_preview}")

        self._log_step_end("fix_test_failures", task_data["id"], result.success)
        return {
            **state,
            "fix_attempt": fix_attempt,
            "next_action": "test" if result.success else "error",
            "messages": messages,
        }

    # =========================================================================
    # Helper methods
    # =========================================================================

    def _get_test_component(self: "Orchestrator", task_data: dict) -> str:
        """Determine which test component to filter by based on task."""
        if "lexer" in task_data["id"].lower() or "0.1.0" in task_data["phase"]:
            return "Lexer"
        elif "parser" in task_data["id"].lower() or "0.1.1" in task_data["phase"]:
            return "Parser"
        elif "codegen" in task_data["id"].lower() or "0.1.2" in task_data["phase"]:
            return "CodeGen"
        elif "semantic" in task_data["id"].lower():
            return "Semantic"
        return ""

    def _build_test_command(self: "Orchestrator", component: str) -> str:
        """Build the dotnet test command."""
        base_cmd = 'dotnet test "--logger:console;verbosity=minimal"'
        if component:
            return f'{base_cmd} --filter "FullyQualifiedName~{component}"'
        return base_cmd
