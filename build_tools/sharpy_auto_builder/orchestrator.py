"""
LangGraph-based orchestrator for Sharpy Auto Builder.

Coordinates task execution, validation, and human-in-the-loop interactions.
"""

import asyncio
import json
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Optional, Any, TypedDict, Annotated, Literal
from enum import Enum
import operator

from langgraph.graph import StateGraph, END
from langgraph.checkpoint.memory import MemorySaver

from .config import Config, BackendType
from .state import (
    GroundTruth,
    Task,
    TaskStatus,
    TaskExecution,
    ValidationResult,
    ValidationStatus,
    Phase,
    parse_task_list,
)
from .agents import AgentRole, AGENT_CONFIGS, get_agent_prompt, get_specialist_for_task
from .backends import BackendManager, ExecutionResult
from .human_loop import (
    HumanLoopManager,
    HumanQuestion,
    HumanReviewRequest,
    QuestionPriority,
)


class OrchestratorState(TypedDict):
    """State for the LangGraph orchestrator."""

    # Current task being processed
    current_task: Optional[dict]

    # Ground truth reference
    ground_truth_path: str

    # Execution state
    execution_attempt: int
    last_execution_result: Optional[dict]

    # Validation results
    validation_results: list[dict]

    # Human interaction state
    awaiting_human_input: bool
    human_question_id: Optional[str]
    human_review_id: Optional[str]
    human_response: Optional[dict]

    # Flow control
    next_action: str
    error_message: Optional[str]

    # Accumulated messages/logs
    messages: Annotated[list[str], operator.add]


class Orchestrator:
    """Main orchestrator for automated task implementation."""

    def __init__(self, config: Config):
        self.config = config
        self.config.ensure_directories()

        # Initialize components
        self.backend_manager = BackendManager(config)
        self.human_loop = HumanLoopManager(
            config.questions_dir,
            config.answers_dir,
            config.human_review_dir,
        )

        # Load or initialize ground truth
        self.ground_truth = self._load_or_create_ground_truth()

        # Build the graph
        self.graph = self._build_graph()
        self.memory = MemorySaver()
        self.app = self.graph.compile(checkpointer=self.memory)

    def _load_or_create_ground_truth(self) -> GroundTruth:
        """Load existing ground truth or create from task list."""
        if self.config.ground_truth_path.exists():
            return GroundTruth.load(self.config.ground_truth_path)

        # Parse task list
        task_list_content = self.config.task_list_path.read_text()
        ground_truth = parse_task_list(task_list_content)
        ground_truth.save(self.config.ground_truth_path)
        return ground_truth

    def _build_graph(self) -> StateGraph:
        """Build the LangGraph state machine."""

        # Create the graph
        graph = StateGraph(OrchestratorState)

        # Add nodes
        graph.add_node("select_task", self._select_task_node)
        graph.add_node("plan_implementation", self._plan_implementation_node)
        graph.add_node("execute_implementation", self._execute_implementation_node)
        graph.add_node("run_tests", self._run_tests_node)
        graph.add_node("validate_spec_adherence", self._validate_spec_adherence_node)
        graph.add_node("validate_verification", self._validate_verification_node)
        graph.add_node("check_hallucinations", self._check_hallucinations_node)
        graph.add_node("request_human_review", self._request_human_review_node)
        graph.add_node("wait_for_human", self._wait_for_human_node)
        graph.add_node("process_human_response", self._process_human_response_node)
        graph.add_node("update_ground_truth", self._update_ground_truth_node)
        graph.add_node("handle_error", self._handle_error_node)

        # Set entry point
        graph.set_entry_point("select_task")

        # Add conditional edges
        graph.add_conditional_edges(
            "select_task",
            self._route_after_select_task,
            {
                "plan": "plan_implementation",
                "complete": END,
                "wait_human": "wait_for_human",
            },
        )

        graph.add_edge("plan_implementation", "execute_implementation")

        graph.add_conditional_edges(
            "execute_implementation",
            self._route_after_execution,
            {
                "test": "run_tests",
                "error": "handle_error",
            },
        )

        graph.add_conditional_edges(
            "run_tests",
            self._route_after_tests,
            {
                "validate": "validate_spec_adherence",
                "error": "handle_error",
            },
        )

        graph.add_edge("validate_spec_adherence", "validate_verification")

        graph.add_conditional_edges(
            "validate_verification",
            self._route_after_verification,
            {
                "hallucination_check": "check_hallucinations",
                "human_review": "request_human_review",
                "update": "update_ground_truth",
            },
        )

        graph.add_conditional_edges(
            "check_hallucinations",
            self._route_after_hallucination_check,
            {
                "human_review": "request_human_review",
                "update": "update_ground_truth",
            },
        )

        graph.add_edge("request_human_review", "wait_for_human")

        graph.add_conditional_edges(
            "wait_for_human",
            self._route_after_human_wait,
            {
                "process": "process_human_response",
                "timeout": "handle_error",
            },
        )

        graph.add_conditional_edges(
            "process_human_response",
            self._route_after_human_response,
            {
                "continue": "update_ground_truth",
                "retry": "execute_implementation",
                "skip": "update_ground_truth",
            },
        )

        graph.add_conditional_edges(
            "update_ground_truth",
            self._route_after_update,
            {
                "next_task": "select_task",
                "complete": END,
            },
        )

        graph.add_conditional_edges(
            "handle_error",
            self._route_after_error,
            {
                "retry": "execute_implementation",
                "human": "request_human_review",
                "skip": "update_ground_truth",
                "abort": END,
            },
        )

        return graph

    # =========================================================================
    # Node implementations
    # =========================================================================

    async def _select_task_node(self, state: OrchestratorState) -> OrchestratorState:
        """Select the next task to work on."""
        # Reload ground truth to get latest state
        self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))

        # Check for tasks awaiting human review
        tasks_awaiting = self.ground_truth.get_tasks_awaiting_review()
        if tasks_awaiting:
            task = tasks_awaiting[0]
            return {
                **state,
                "current_task": task.to_dict(),
                "next_action": "wait_human",
                "messages": [f"Task {task.id} is awaiting human review"],
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
            "validation_results": [],
            "next_action": "plan",
            "messages": [f"Selected task {task.id}: {task.title}"],
        }

    async def _plan_implementation_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Plan the implementation approach."""
        task_data = state["current_task"]

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

        return {
            **state,
            "messages": [f"Implementation plan created for {task_data['id']}"],
        }

    async def _execute_implementation_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Execute the implementation using an agent."""
        task_data = state["current_task"]
        attempt = state["execution_attempt"] + 1

        # Get specialist for this task
        specialist = get_specialist_for_task(task_data["id"], task_data["files"])

        # Generate implementation prompt
        prompt = get_agent_prompt(
            specialist,
            task_title=task_data["title"],
            task_description=task_data["description"],
            files=task_data["files"],
            component=task_data["phase"],
        )

        # Add context from previous attempts if any
        if state.get("last_execution_result"):
            prev = state["last_execution_result"]
            if not prev.get("success"):
                prompt += f"\n\nPrevious attempt failed with error:\n{prev.get('error', 'Unknown error')}"

        # Execute via backend
        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
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
            # Include error details in logs for debugging
            error_preview = result.error[:200] if len(result.error) > 200 else result.error
            messages.append(f"  Error: {error_preview}")

        return {
            **state,
            "execution_attempt": attempt,
            "last_execution_result": execution_result,
            "next_action": "test" if result.success else "error",
            "messages": messages,
        }

    async def _run_tests_node(self, state: OrchestratorState) -> OrchestratorState:
        """Run tests to verify implementation."""
        task_data = state["current_task"]

        # Determine which tests to run based on task
        component = ""
        if "lexer" in task_data["id"].lower() or "0.1.0" in task_data["phase"]:
            component = "Lexer"
        elif "parser" in task_data["id"].lower() or "0.1.1" in task_data["phase"]:
            component = "Parser"
        elif "codegen" in task_data["id"].lower() or "0.1.2" in task_data["phase"]:
            component = "CodeGen"
        elif "semantic" in task_data["id"].lower():
            component = "Semantic"

        # Run filtered tests
        if component:
            test_cmd = f'dotnet test --filter "FullyQualifiedName~{component}"'
        else:
            test_cmd = "dotnet test"

        result = await self.backend_manager.execute_command(
            test_cmd,
            cwd=self.config.project_root,
        )

        test_result = {
            "passed": result.success,
            "output": result.output,
            "error": result.error,
        }

        # Update execution result
        execution_result = state.get("last_execution_result", {})
        execution_result["tests_run"] = True
        execution_result["tests_passed"] = result.success
        execution_result["test_output"] = result.output

        return {
            **state,
            "last_execution_result": execution_result,
            "next_action": "validate" if result.success else "error",
            "messages": [f"Tests {'passed' if result.success else 'failed'}"],
        }

    async def _validate_spec_adherence_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Run spec adherence validation."""
        if not self.config.run_spec_adherence_check:
            return state

        task_data = state["current_task"]

        prompt = get_agent_prompt(
            AgentRole.SPEC_ADHERENCE,
            task_title=task_data["title"],
            files=task_data["files"],
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        validation_result = {
            "agent": "spec-adherence",
            "status": "passed" if result.success else "warnings",
            "findings": [],  # Would parse from result.output
            "raw_output": result.output,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        return {
            **state,
            "validation_results": validation_results,
            "messages": ["Spec adherence check completed"],
        }

    async def _validate_verification_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Run verification expert validation."""
        if not self.config.run_verification_after_implementation:
            return {
                **state,
                "next_action": (
                    "update"
                    if not self.config.run_hallucination_defense
                    else "hallucination_check"
                ),
            }

        task_data = state["current_task"]

        prompt = get_agent_prompt(
            AgentRole.VERIFICATION_EXPERT,
            task_title=task_data["title"],
            files=task_data["files"],
            component=task_data["phase"],
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        validation_result = {
            "agent": "verification-expert",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Determine next action
        has_issues = not result.success or any(
            v.get("status") == "failed" for v in validation_results
        )

        if has_issues and task_data.get("is_critical"):
            next_action = "human_review"
        elif self.config.run_hallucination_defense:
            next_action = "hallucination_check"
        else:
            next_action = "update"

        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": ["Verification check completed"],
        }

    async def _check_hallucinations_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Check for potential hallucinations in the implementation."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})

        # Extract claims from implementation output
        claims = execution_result.get("output", "")[:2000]  # First 2000 chars

        prompt = get_agent_prompt(
            AgentRole.HALLUCINATION_DEFENSE,
            claims=claims,
        )

        result = await self.backend_manager.execute_with_failover(prompt)

        validation_result = {
            "agent": "hallucination-defense",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check if human review needed
        has_hallucinations = "INCORRECT" in result.output.upper()

        if has_hallucinations and task_data.get("is_critical"):
            next_action = "human_review"
        else:
            next_action = "update"

        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": ["Hallucination check completed"],
        }

    async def _request_human_review_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Request human review of the implementation."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        validation_results = state.get("validation_results", [])

        # Collect concerns
        concerns = []
        for vr in validation_results:
            if vr.get("status") in ["failed", "warnings"]:
                concerns.append(f"{vr['agent']}: {vr.get('status')}")

        if execution_result.get("error"):
            concerns.append(f"Execution error: {execution_result['error'][:200]}")

        # Create review request
        review = self.human_loop.create_review_request(
            task_id=task_data["id"],
            title=f"Review: {task_data['title']}",
            summary=f"Task {task_data['id']} implementation requires review",
            changes=execution_result.get("output", "").split("\n")[
                :20
            ],  # First 20 lines
            test_results=execution_result.get("test_output", "No test output"),
            validation_results=validation_results,
            concerns=concerns,
        )

        return {
            **state,
            "awaiting_human_input": True,
            "human_review_id": review.id,
            "next_action": "wait",
            "messages": [f"Human review requested: {review.id}"],
        }

    async def _wait_for_human_node(self, state: OrchestratorState) -> OrchestratorState:
        """Wait for human input with polling."""
        question_id = state.get("human_question_id")
        review_id = state.get("human_review_id")

        # Configuration for waiting
        check_interval = getattr(self.config, "human_check_interval", 5.0)
        max_wait_time = getattr(self.config, "human_wait_timeout", 3600.0)

        start_time = time.time()
        check_count = 0

        while (time.time() - start_time) < max_wait_time:
            check_count += 1

            if question_id:
                # Check for answer
                answer = self.human_loop.check_for_answer(question_id)
                if answer:
                    return {
                        **state,
                        "awaiting_human_input": False,
                        "human_response": {"type": "answer", "value": answer},
                        "next_action": "process",
                        "messages": [
                            f"Human answered question {question_id} after {check_count} checks"
                        ],
                    }

            if review_id:
                # Check for review response
                response = self.human_loop.check_for_review_response(review_id)
                if response:
                    return {
                        **state,
                        "awaiting_human_input": False,
                        "human_response": {"type": "review", **response},
                        "next_action": "process",
                        "messages": [
                            f"Human reviewed {review_id}: {response.get('status')} after {check_count} checks"
                        ],
                    }

            # Log progress periodically
            if check_count % 12 == 0:  # Every minute
                elapsed = time.time() - start_time
                print(
                    f"[wait_for_human] Still waiting... ({elapsed:.0f}s elapsed, {check_count} checks)"
                )

            # Wait before next check
            await asyncio.sleep(check_interval)

        # Timeout reached
        return {
            **state,
            "next_action": "timeout",
            "messages": [
                f"Human input timeout after {max_wait_time}s ({check_count} checks)"
            ],
        }

    async def _process_human_response_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Process human response and determine next action."""
        response = state.get("human_response", {})

        if response.get("type") == "review":
            status = response.get("status", "")

            if status == "approved":
                return {
                    **state,
                    "next_action": "continue",
                    "messages": ["Human approved implementation"],
                }
            elif status == "rejected":
                return {
                    **state,
                    "next_action": "retry",
                    "messages": [
                        f"Human rejected: {response.get('notes', 'No notes')}"
                    ],
                }
            elif status == "needs_changes":
                return {
                    **state,
                    "next_action": "retry",
                    "messages": [
                        f"Changes requested: {response.get('notes', 'No notes')}"
                    ],
                }
            else:
                return {
                    **state,
                    "next_action": "skip",
                    "messages": ["Unknown review status, skipping task"],
                }

        elif response.get("type") == "answer":
            # Store answer in task
            task_data = state["current_task"]
            task_data["human_answer"] = response.get("value")
            return {
                **state,
                "current_task": task_data,
                "next_action": "continue",
                "messages": ["Human provided answer"],
            }

        return {
            **state,
            "next_action": "skip",
            "messages": ["Unknown response type"],
        }

    async def _update_ground_truth_node(
        self, state: OrchestratorState
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
            if execution.success and execution.tests_passed:
                task.status = TaskStatus.COMPLETED
            elif state.get("execution_attempt", 0) >= self.config.max_retries_per_task:
                task.status = TaskStatus.FAILED

            # Update stats - normalize backend name and ensure key exists
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

        # Save updated ground truth
        self.ground_truth.save(Path(state["ground_truth_path"]))

        # Check if there are more tasks
        next_task = self.ground_truth.get_next_pending_task()

        return {
            **state,
            "next_action": "next_task" if next_task else "complete",
            "messages": [f"Ground truth updated for task {task_data['id']}"],
        }

    async def _handle_error_node(self, state: OrchestratorState) -> OrchestratorState:
        """Handle errors during execution."""
        attempt = state.get("execution_attempt", 0)
        task_data = state["current_task"]

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

        return {
            **state,
            "next_action": "skip",
            "messages": ["Max retries reached, skipping task"],
        }

    # =========================================================================
    # Routing functions
    # =========================================================================

    def _route_after_select_task(self, state: OrchestratorState) -> str:
        return state.get("next_action", "complete")

    def _route_after_execution(self, state: OrchestratorState) -> str:
        return state.get("next_action", "error")

    def _route_after_tests(self, state: OrchestratorState) -> str:
        return state.get("next_action", "error")

    def _route_after_verification(self, state: OrchestratorState) -> str:
        return state.get("next_action", "update")

    def _route_after_hallucination_check(self, state: OrchestratorState) -> str:
        return state.get("next_action", "update")

    def _route_after_human_wait(self, state: OrchestratorState) -> str:
        return state.get("next_action", "timeout")

    def _route_after_human_response(self, state: OrchestratorState) -> str:
        return state.get("next_action", "continue")

    def _route_after_update(self, state: OrchestratorState) -> str:
        return state.get("next_action", "next_task")

    def _route_after_error(self, state: OrchestratorState) -> str:
        return state.get("next_action", "skip")

    # =========================================================================
    # Public API
    # =========================================================================

    async def run(self, max_tasks: Optional[int] = None) -> dict:
        """Run the orchestrator to process tasks."""
        initial_state: OrchestratorState = {
            "current_task": None,
            "ground_truth_path": str(self.config.ground_truth_path),
            "execution_attempt": 0,
            "last_execution_result": None,
            "validation_results": [],
            "awaiting_human_input": False,
            "human_question_id": None,
            "human_review_id": None,
            "human_response": None,
            "next_action": "",
            "error_message": None,
            "messages": [],
        }

        config = {
            "configurable": {"thread_id": "sharpy-auto-builder"},
            "recursion_limit": 150,  # Allow more iterations for multi-task runs with retries
        }

        tasks_processed = 0

        # Run the graph
        async for event in self.app.astream(initial_state, config):
            # Log progress
            for node_name, node_state in event.items():
                if node_state.get("messages"):
                    for msg in node_state["messages"]:
                        print(f"[{node_name}] {msg}")

            # Check if we've hit the task limit
            if max_tasks and tasks_processed >= max_tasks:
                break

            # Check if task completed
            if event.get("update_ground_truth", {}).get("next_action") == "next_task":
                tasks_processed += 1

        # Get final state
        final_state = self.app.get_state(config)

        return {
            "tasks_processed": tasks_processed,
            "final_progress": self.ground_truth.overall_progress,
            "backend_stats": self.backend_manager.get_status(),
        }

    def get_status(self) -> dict:
        """Get current orchestrator status."""
        return {
            "ground_truth_progress": self.ground_truth.overall_progress,
            "current_task": self.ground_truth.current_task_id,
            "phases": [
                {
                    "id": p.id,
                    "name": p.name,
                    "status": p.status.value,
                    "progress": p.progress,
                }
                for p in self.ground_truth.phases
            ],
            "backend_status": self.backend_manager.get_status(),
            "pending_human_questions": len(self.human_loop.get_pending_questions()),
            "pending_human_reviews": len(self.human_loop.get_pending_reviews()),
        }

    def generate_status_report(self) -> str:
        """Generate a comprehensive status report."""
        status = self.get_status()

        lines = [
            "# Sharpy Auto Builder Status Report",
            "",
            f"**Generated:** {datetime.now().isoformat()}",
            f"**Overall Progress:** {status['ground_truth_progress']:.1f}%",
            f"**Current Task:** {status['current_task'] or 'None'}",
            "",
            "## Phases",
            "",
        ]

        for phase in status["phases"]:
            status_emoji = {
                "completed": "✅",
                "in_progress": "🔄",
                "failed": "❌",
                "pending": "⏳",
            }.get(phase["status"], "❓")
            lines.append(
                f"- {status_emoji} **{phase['id']}**: {phase['name']} ({phase['progress']:.0f}%)"
            )

        lines.extend(
            [
                "",
                "## Backend Status",
                "",
            ]
        )

        for backend, bstatus in status["backend_status"].items():
            avail = "✅" if bstatus["available"] else "⏳"
            lines.append(
                f"- **{backend}**: {avail} (wait: {bstatus['wait_time']:.1f}s, errors: {bstatus['consecutive_errors']})"
            )

        lines.extend(
            [
                "",
                "## Human Interactions",
                "",
                f"- Pending Questions: {status['pending_human_questions']}",
                f"- Pending Reviews: {status['pending_human_reviews']}",
            ]
        )

        return "\n".join(lines)
