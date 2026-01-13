"""
LangGraph-based orchestrator for Sharpy Auto Builder.

Coordinates task execution, validation, and human-in-the-loop interactions.
"""

import asyncio
import json
import time
from dataclasses import dataclass, field
from datetime import datetime, timedelta
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
from .response_analyzer import (
    ResponseAnalyzer,
    ResponseAnalysis,
    ResponseType,
    TaskType,
)
from .auto_decision import AutoDecisionEngine, AutoDecision, DecisionType


class OrchestratorState(TypedDict):
    """State for the LangGraph orchestrator."""

    # Current task being processed
    current_task: Optional[dict]

    # Ground truth reference
    ground_truth_path: str

    # Execution state
    execution_attempt: int
    fix_attempt: int  # Separate counter for test fix attempts
    validation_fix_attempt: int  # Counter for validation issue fix attempts
    last_execution_result: Optional[dict]

    # Test state
    baseline_test_passed: Optional[bool]  # Did tests pass before agent made changes?
    baseline_test_output: Optional[str]  # Baseline test output for comparison

    # Validation results
    validation_results: list[dict]

    # Human interaction state
    awaiting_human_input: bool
    human_question_id: Optional[str]
    human_review_id: Optional[str]
    human_response: Optional[dict]

    # Response analysis state (NEW: Overseer functionality)
    response_analysis: Optional[dict]  # Result of analyzing agent response
    auto_decision: Optional[dict]  # Auto-decision if made

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

        # Track step start times for duration logging
        self._step_start_times: dict[str, float] = {}

        # Initialize components
        self.backend_manager = BackendManager(config)
        self.human_loop = HumanLoopManager(
            config.questions_dir,
            config.answers_dir,
            config.human_review_dir,
        )

        # Initialize response analyzer and auto-decision engine (Overseer components)
        self.response_analyzer = ResponseAnalyzer(
            min_confidence=getattr(config, "response_analysis_confidence", 0.6)
        )
        self.auto_decision_engine = AutoDecisionEngine(
            auto_defer_optional=getattr(config, "auto_defer_optional", True),
            require_high_confidence=True,
            min_confidence=getattr(config, "auto_decision_confidence", 0.7),
        )

        # Load or initialize ground truth
        self.ground_truth = self._load_or_create_ground_truth()

        # Build the graph
        self.graph = self._build_graph()
        self.memory = MemorySaver()
        self.app = self.graph.compile(checkpointer=self.memory)

    def _log_execution(
        self,
        event_type: str,
        task_id: str,
        prompt: str | None = None,
        output: str | None = None,
        error: str | None = None,
        success: bool | None = None,
        backend: str | None = None,
        duration: float | None = None,
        extra: dict | None = None,
    ) -> None:
        """Append an execution event to the JSONL log file."""
        log_entry = {
            "timestamp": datetime.now().isoformat(),
            "event_type": event_type,
            "task_id": task_id,
        }
        if prompt is not None:
            log_entry["prompt"] = prompt
        if output is not None:
            log_entry["output"] = output
        if error is not None:
            log_entry["error"] = error
        if success is not None:
            log_entry["success"] = success
        if backend is not None:
            log_entry["backend"] = backend
        if duration is not None:
            log_entry["duration_seconds"] = duration
        if extra:
            log_entry.update(extra)

        # Append to JSONL file
        with open(self.config.execution_log_path, "a") as f:
            f.write(json.dumps(log_entry) + "\n")

    def _log_step_start(self, step_name: str, task_id: str, details: str = "") -> None:
        """Log when a potentially long-running step begins."""
        self._step_start_times[step_name] = time.time()
        timestamp = datetime.now().strftime("%H:%M:%S")
        detail_str = f" - {details}" if details else ""
        print(f"⏳ [{timestamp}] Starting: {step_name}{detail_str}")

        # Also log to JSONL
        self._log_execution(
            event_type=f"{step_name}_start",
            task_id=task_id,
            extra={"details": details} if details else None,
        )

    def _log_step_end(
        self, step_name: str, task_id: str, success: bool = True, details: str = ""
    ) -> None:
        """Log when a step completes with duration."""
        duration = None
        if step_name in self._step_start_times:
            duration = time.time() - self._step_start_times.pop(step_name)

        timestamp = datetime.now().strftime("%H:%M:%S")
        status = "✅" if success else "❌"
        duration_str = f" ({duration:.1f}s)" if duration else ""
        detail_str = f" - {details}" if details else ""
        print(
            f"{status} [{timestamp}] Completed: {step_name}{duration_str}{detail_str}"
        )

    def _dump_config(self) -> None:
        """Dump the current configuration at startup for debugging."""
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        config_dict = self.config.to_dict()

        print("\n" + "=" * 70)
        print(f"🔧 ORCHESTRATOR CONFIGURATION (as of {timestamp})")
        print("=" * 70)

        # Key settings summary
        print("\n📋 Key Settings:")
        print(f"  • Project root: {self.config.project_root}")
        print(f"  • Task list: {self.config.task_list_path}")
        print(f"  • Ground truth: {self.config.ground_truth_path}")

        print("\n🔍 Validation Settings:")
        print(f"  • Spec adherence check: {self.config.run_spec_adherence_check}")
        print(
            f"  • Verification after implementation: {self.config.run_verification_after_implementation}"
        )
        print(f"  • Hallucination defense: {self.config.run_hallucination_defense}")

        print("\n🔄 Retry/Fix Settings:")
        print(f"  • Max retries per task: {self.config.max_retries_per_task}")
        print(f"  • Max test fix attempts: {self.config.max_test_fix_attempts}")
        print(
            f"  • Max validation fix attempts: {self.config.max_validation_fix_attempts}"
        )
        print(
            f"  • Create follow-up on fix failure: {self.config.create_followup_task_on_fix_failure}"
        )

        print("\n⚙️ Execution Settings:")
        print(f"  • Auto commit: {self.config.auto_commit}")
        print(f"  • Create PR: {self.config.create_pr}")
        print(f"  • Test timeout: {self.config.test_timeout}s")
        print(
            f"  • Human approval for critical: {self.config.require_human_approval_for_critical}"
        )

        print("\n🤖 Backend Configuration:")
        for name, backend in self.config.backends.items():
            status = "✅ enabled" if backend.enabled else "❌ disabled"
            print(f"  • {name}: {status}")
            if backend.enabled:
                print(f"      Model: {backend.model}")
                print(f"      Max tokens: {backend.max_tokens}")
                print(
                    f"      Rate limit: {backend.rate_limit.max_requests_per_window}/hour"
                )
                print(f"      Cooldown: {backend.rate_limit.request_cooldown}s")

        print(f"\n  Backend priority: {' → '.join(self.config.backend_priority)}")

        print("\n" + "=" * 70 + "\n")

        # Also log to execution log as JSON
        self._log_execution(
            event_type="orchestrator_start",
            task_id="system",
            extra={"config": config_dict},
        )

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
        graph.add_node("run_baseline_tests", self._run_baseline_tests_node)
        graph.add_node("execute_implementation", self._execute_implementation_node)
        graph.add_node("analyze_response", self._analyze_response_node)  # NEW: Overseer
        graph.add_node(
            "handle_auto_decision", self._handle_auto_decision_node
        )  # NEW: Overseer
        graph.add_node("run_tests", self._run_tests_node)
        graph.add_node("fix_test_failures", self._fix_test_failures_node)
        graph.add_node("validate_spec_adherence", self._validate_spec_adherence_node)
        graph.add_node("validate_verification", self._validate_verification_node)
        graph.add_node("check_hallucinations", self._check_hallucinations_node)
        graph.add_node(
            "address_validation_issues", self._address_validation_issues_node
        )
        graph.add_node("request_human_review", self._request_human_review_node)
        graph.add_node("wait_for_human", self._wait_for_human_node)
        graph.add_node("process_human_response", self._process_human_response_node)
        graph.add_node("update_ground_truth", self._update_ground_truth_node)
        graph.add_node("commit_changes", self._commit_changes_node)
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

        # Run baseline tests before implementation to know what was already broken
        graph.add_edge("plan_implementation", "run_baseline_tests")
        graph.add_edge("run_baseline_tests", "execute_implementation")

        # NEW: Route through response analysis (Overseer) after execution
        graph.add_conditional_edges(
            "execute_implementation",
            self._route_after_execution,
            {
                "analyze": "analyze_response",  # NEW: Always analyze first
                "error": "handle_error",
            },
        )

        # NEW: Handle different response types from analysis
        graph.add_conditional_edges(
            "analyze_response",
            self._route_after_analysis,
            {
                "test": "run_tests",  # Implementation done, run tests
                "auto_decide": "handle_auto_decision",  # Can auto-decide
                "wait_human": "wait_for_human",  # Need human input
                "error": "handle_error",
            },
        )

        # NEW: After auto-decision, update ground truth (skip test/validate for deferred)
        graph.add_conditional_edges(
            "handle_auto_decision",
            self._route_after_auto_decision,
            {
                "update": "update_ground_truth",  # Deferred/skipped
                "test": "run_tests",  # Decided to proceed
                "error": "handle_error",
            },
        )

        graph.add_conditional_edges(
            "run_tests",
            self._route_after_tests,
            {
                "validate": "validate_spec_adherence",
                "fix": "fix_test_failures",  # Agent broke tests, let them fix
                "preexisting_failure": "validate_spec_adherence",  # Tests were already failing
                "error": "handle_error",
            },
        )

        # After fix attempt, run tests again
        graph.add_conditional_edges(
            "fix_test_failures",
            self._route_after_fix,
            {
                "test": "run_tests",
                "error": "handle_error",
            },
        )

        graph.add_conditional_edges(
            "validate_spec_adherence",
            self._route_after_spec_adherence,
            {
                "verification": "validate_verification",
                "address_issues": "address_validation_issues",
                "human_review": "request_human_review",
            },
        )

        graph.add_conditional_edges(
            "validate_verification",
            self._route_after_verification,
            {
                "hallucination_check": "check_hallucinations",
                "address_issues": "address_validation_issues",
                "human_review": "request_human_review",
                "commit": "commit_changes",
            },
        )

        graph.add_conditional_edges(
            "check_hallucinations",
            self._route_after_hallucination_check,
            {
                "address_issues": "address_validation_issues",
                "human_review": "request_human_review",
                "commit": "commit_changes",
            },
        )

        # After addressing validation issues, run tests again then re-validate
        graph.add_conditional_edges(
            "address_validation_issues",
            self._route_after_address_issues,
            {
                "test": "run_tests",
                "human_review": "request_human_review",
                "error": "handle_error",
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
                "continue": "commit_changes",
                "retry": "execute_implementation",
                "skip": "update_ground_truth",
            },
        )

        graph.add_conditional_edges(
            "commit_changes",
            self._route_after_commit,
            {
                "update": "update_ground_truth",
                "error": "handle_error",
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
            "fix_attempt": 0,
            "validation_fix_attempt": 0,
            "baseline_test_passed": None,
            "baseline_test_output": None,
            "validation_results": [],
            "next_action": "plan",
            "messages": [f"Selected task {task.id}: {task.title}"],
        }

    async def _plan_implementation_node(
        self, state: OrchestratorState
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
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Execute the implementation using an agent."""
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

        # Add context from previous attempts if any
        if state.get("last_execution_result"):
            prev = state["last_execution_result"]
            if not prev.get("success"):
                prompt += f"\n\nPrevious attempt failed with error:\n{prev.get('error', 'Unknown error')}"

        # Log the prompt being sent
        self._log_execution(
            event_type="agent_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"attempt": attempt, "specialist": specialist.value},
        )

        # Execute via backend
        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the full response
        self._log_execution(
            event_type="agent_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"attempt": attempt},
        )

        self._log_step_end(
            "execute_implementation",
            task_data["id"],
            result.success,
            f"via {result.backend}" if result.backend else "",
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
        self, state: OrchestratorState
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

        # Run baseline tests with timeout to catch infinite loops
        test_cmd = self._build_test_command(component)
        result = await self.backend_manager.execute_command(
            test_cmd,
            cwd=self.config.project_root,
            env_override={"NO_COLOR": "1", "DOTNET_CLI_UI_LANGUAGE": "en"},
            timeout=self.config.test_timeout,
        )

        # Log baseline test results
        self._log_execution(
            event_type="baseline_test_run",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            extra={
                "test_command": test_cmd,
                "component": component,
                "timed_out": result.timed_out,
            },
        )

        # Handle timeout (infinite loop in baseline tests is a pre-existing issue)
        if result.timed_out:
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

    # =========================================================================
    # Overseer nodes - Response analysis and auto-decision
    # =========================================================================

    async def _analyze_response_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """
        Analyze agent response to detect questions vs. actual work done.

        This is the Overseer's primary function - catching cases where agents
        ask questions instead of doing work (like task 0.1.5.8).
        """
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        response_output = execution_result.get("output", "")

        # Detect task type from task data (can be extended to read from task schema)
        task_title = task_data.get("title", "")
        task_type = TaskType.AUTO  # Let analyzer auto-detect based on title

        # Analyze the response with task type context
        analysis = self.response_analyzer.analyze(
            response_output, task_title, task_type
        )

        # Log the analysis
        self._log_execution(
            event_type="response_analysis",
            task_id=task_data["id"],
            extra={
                "response_type": analysis.response_type.value,
                "confidence": analysis.confidence,
                "questions_count": len(analysis.questions),
                "work_indicators_count": len(analysis.work_indicators),
                "audit_indicators_count": len(analysis.audit_indicators),
                "detected_task_type": (
                    analysis.detected_task_type.value
                    if analysis.detected_task_type
                    else None
                ),
                "reasoning": analysis.reasoning,
            },
        )

        # Store analysis in state
        analysis_dict = analysis.to_dict()

        # Determine next action based on response type
        if analysis.response_type == ResponseType.IMPLEMENTATION:
            # Agent did work - proceed to tests
            return {
                **state,
                "response_analysis": analysis_dict,
                "next_action": "test",
                "messages": [
                    f"Response analysis: Implementation detected (confidence: {analysis.confidence:.2f})"
                ],
            }

        elif analysis.response_type == ResponseType.AUDIT:
            # Agent completed an audit/verification task - proceed to tests
            return {
                **state,
                "response_analysis": analysis_dict,
                "next_action": "test",
                "messages": [
                    f"Response analysis: Audit completed (confidence: {analysis.confidence:.2f})"
                ],
            }

        elif analysis.response_type == ResponseType.QUESTION:
            # Agent is asking questions - check if we can auto-decide
            if self.auto_decision_engine.should_auto_decide(task_data, analysis):
                return {
                    **state,
                    "response_analysis": analysis_dict,
                    "next_action": "auto_decide",
                    "messages": [
                        f"Response analysis: Questions detected, can auto-decide",
                        f"  Questions: {analysis.questions[:2]}",
                    ],
                }
            else:
                # Need human input - create question
                question = self.human_loop.create_question(
                    task_id=task_data["id"],
                    question=f"Agent is asking for guidance on task {task_data['id']}: {task_data.get('title', '')}",
                    context=response_output[:2000],  # First 2000 chars
                    priority=QuestionPriority.HIGH,
                    options=analysis.proposed_actions or analysis.questions,
                )

                self._log_execution(
                    event_type="human_question_created",
                    task_id=task_data["id"],
                    extra={
                        "question_id": question.id,
                        "questions": analysis.questions,
                        "proposed_actions": analysis.proposed_actions,
                    },
                )

                return {
                    **state,
                    "response_analysis": analysis_dict,
                    "human_question_id": question.id,
                    "awaiting_human_input": True,
                    "next_action": "wait_human",
                    "messages": [
                        f"Response analysis: Questions detected - awaiting human decision",
                        f"  Question ID: {question.id}",
                    ],
                }

        elif analysis.response_type == ResponseType.DEFERRAL:
            # Agent recommends deferral - check if optional task
            if self.auto_decision_engine.should_auto_decide(task_data, analysis):
                return {
                    **state,
                    "response_analysis": analysis_dict,
                    "next_action": "auto_decide",
                    "messages": [
                        "Response analysis: Deferral recommended, can auto-decide"
                    ],
                }
            else:
                # Deferral for non-optional task needs human review
                return {
                    **state,
                    "response_analysis": analysis_dict,
                    "next_action": "wait_human",
                    "messages": [
                        "Response analysis: Deferral recommended but task is not optional"
                    ],
                }

        elif analysis.response_type == ResponseType.ERROR:
            return {
                **state,
                "response_analysis": analysis_dict,
                "next_action": "error",
                "error_message": "Agent encountered an error during implementation",
                "messages": ["Response analysis: Error detected in response"],
            }

        else:
            # CLARIFICATION, EMPTY, or unknown - treat as needing human input
            return {
                **state,
                "response_analysis": analysis_dict,
                "next_action": "error",
                "error_message": f"Unclear response type: {analysis.response_type.value}",
                "messages": [
                    f"Response analysis: Unclear response ({analysis.response_type.value})"
                ],
            }

    async def _handle_auto_decision_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """
        Handle automatic decisions for well-defined scenarios.

        This node executes auto-decisions (like deferring optional tasks)
        without requiring human intervention.
        """
        task_data = state["current_task"]
        analysis_dict = state.get("response_analysis", {})

        # Reconstruct analysis object
        analysis = ResponseAnalysis(
            response_type=ResponseType(analysis_dict.get("response_type", "question")),
            confidence=analysis_dict.get("confidence", 0.0),
            questions=analysis_dict.get("questions", []),
            proposed_actions=analysis_dict.get("proposed_actions", []),
            work_indicators=analysis_dict.get("work_indicators", []),
            deferral_indicators=analysis_dict.get("deferral_indicators", []),
            reasoning=analysis_dict.get("reasoning", ""),
        )

        # Make the decision
        decision = self.auto_decision_engine.make_decision(task_data, analysis)

        # Log the decision
        self._log_execution(
            event_type="auto_decision",
            task_id=task_data["id"],
            extra=decision.to_dict(),
        )

        if decision.decision_type == DecisionType.DEFER:
            # Update task status to deferred
            self.ground_truth = GroundTruth.load(Path(state["ground_truth_path"]))
            task = self.ground_truth.get_task(task_data["id"])
            if task:
                task.status = (
                    TaskStatus.DEFERRED
                    if hasattr(TaskStatus, "DEFERRED")
                    else TaskStatus.COMPLETED
                )
                task.notes.append(f"Auto-deferred: {decision.reason}")
                if decision.selected_option:
                    task.notes.append(f"Selected option: {decision.selected_option}")
                self.ground_truth.save(Path(state["ground_truth_path"]))

            return {
                **state,
                "auto_decision": decision.to_dict(),
                "next_action": "update",
                "messages": [
                    f"Auto-decision: {decision.decision_type.value}",
                    f"  Reason: {decision.reason}",
                ],
            }

        elif decision.decision_type == DecisionType.PROCEED:
            # Decided to proceed - run tests
            return {
                **state,
                "auto_decision": decision.to_dict(),
                "next_action": "test",
                "messages": [
                    f"Auto-decision: Proceeding with implementation",
                    f"  Selected: {decision.selected_option}",
                ],
            }

        elif decision.decision_type == DecisionType.SKIP:
            # Skip the task entirely
            return {
                **state,
                "auto_decision": decision.to_dict(),
                "next_action": "update",
                "messages": [f"Auto-decision: Skipping task - {decision.reason}"],
            }

        else:  # ESCALATE
            # Couldn't auto-decide, need human
            question = self.human_loop.create_question(
                task_id=task_data["id"],
                question=f"Auto-decision engine could not decide on task {task_data['id']}",
                context=decision.reason,
                priority=QuestionPriority.HIGH,
                options=analysis.proposed_actions,
            )

            return {
                **state,
                "auto_decision": decision.to_dict(),
                "human_question_id": question.id,
                "awaiting_human_input": True,
                "next_action": "wait_human",
                "messages": [f"Auto-decision: Escalating to human - {decision.reason}"],
            }

    # =========================================================================
    # End of Overseer nodes
    # =========================================================================

    def _get_test_component(self, task_data: dict) -> str:
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

    def _build_test_command(self, component: str) -> str:
        """Build the dotnet test command."""
        # Note: Don't use --no-logo as it conflicts with MSBuild's -nologo
        base_cmd = 'dotnet test "--logger:console;verbosity=minimal"'
        if component:
            return f'{base_cmd} --filter "FullyQualifiedName~{component}"'
        return base_cmd

    async def _run_tests_node(self, state: OrchestratorState) -> OrchestratorState:
        """Run tests to verify implementation."""
        task_data = state["current_task"]

        # Use helper methods
        component = self._get_test_component(task_data)
        test_cmd = self._build_test_command(component)
        self._log_step_start(
            "run_tests",
            task_data["id"],
            f"component: {component}" if component else "all tests",
        )

        result = await self.backend_manager.execute_command(
            test_cmd,
            cwd=self.config.project_root,
            env_override={"NO_COLOR": "1", "DOTNET_CLI_UI_LANGUAGE": "en"},
            timeout=self.config.test_timeout,
        )

        # Log test results
        self._log_execution(
            event_type="test_run",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            extra={
                "test_command": test_cmd,
                "component": component,
                "timed_out": result.timed_out,
            },
        )

        # Update execution result
        execution_result = state.get("last_execution_result", {})
        execution_result["tests_run"] = True
        execution_result["tests_passed"] = result.success
        execution_result["test_output"] = result.output
        execution_result["tests_timed_out"] = result.timed_out

        # Handle timeout case - this is likely an infinite loop introduced by the agent
        if result.timed_out:
            self._log_step_end("run_tests", task_data["id"], False, "TIMEOUT")
            baseline_timed_out = state.get("baseline_test_output", "").startswith(
                "TIMEOUT:"
            )
            if baseline_timed_out:
                # Tests were already timing out before agent made changes
                next_action = "preexisting_failure"
                messages = [
                    f"Tests TIMED OUT after {self.config.test_timeout}s, but baseline tests also timed out",
                    "This is a pre-existing infinite loop issue, not caused by this implementation",
                ]
            else:
                # Agent introduced the infinite loop
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
            # Tests pass - proceed to validation
            next_action = "validate"
            messages = ["Tests passed"]
        elif baseline_passed is False:
            # Tests were already failing before agent made changes
            # This is a pre-existing failure, not the agent's fault
            next_action = "preexisting_failure"
            messages = [
                "Tests failed, but they were already failing before implementation",
                "Proceeding to validation (pre-existing failures)",
            ]
        else:
            # Tests were passing before, now failing - agent broke something
            # Give them a chance to fix it
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
            "messages": [m for m in messages if m],  # Filter empty messages
        }

    async def _fix_test_failures_node(
        self, state: OrchestratorState
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
            # Special prompt for infinite loop issues
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
            # Standard test failure prompt
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

    async def _create_test_fix_followup_task(self, state: OrchestratorState) -> None:
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
        self, state: OrchestratorState
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

    async def _validate_spec_adherence_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Run spec adherence validation."""
        if not self.config.run_spec_adherence_check:
            return state

        task_data = state["current_task"]
        self._log_step_start("validate_spec_adherence", task_data["id"])

        prompt = get_agent_prompt(
            AgentRole.SPEC_ADHERENCE,
            task_title=task_data["title"],
            files=task_data["files"],
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "spec_adherence"},
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "spec_adherence"},
        )

        validation_result = {
            "agent": "spec-adherence",
            "status": "passed" if result.success else "warnings",
            "findings": [],  # Would parse from result.output
            "raw_output": result.output,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check for actionable issues that need to be addressed before continuing
        actionable_issues = self._extract_actionable_issues(validation_results)
        critical_issues = [i for i in actionable_issues if i["severity"] == "critical"]

        # Determine next action based on issues found
        validation_fix_attempt = state.get("validation_fix_attempt", 0)
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)

        if critical_issues and validation_fix_attempt < max_attempts:
            # Found critical spec deviations - route to address issues
            next_action = "address_issues"
            messages = [
                "Spec adherence check found critical issues",
                f"  {len(critical_issues)} critical issue(s) to address",
            ]
        elif critical_issues and task_data.get("is_critical"):
            # Max attempts reached on critical task - need human review
            next_action = "human_review"
            messages = [
                "Spec adherence check found unresolved critical issues",
                "Escalating to human review",
            ]
        else:
            # Continue to verification
            next_action = "verification"
            messages = ["Spec adherence check completed"]

        self._log_step_end("validate_spec_adherence", task_data["id"], result.success)
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": messages,
        }

    async def _validate_verification_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Run verification expert validation."""
        if not self.config.run_verification_after_implementation:
            return {
                **state,
                "next_action": (
                    "commit"
                    if not self.config.run_hallucination_defense
                    else "hallucination_check"
                ),
            }

        task_data = state["current_task"]
        self._log_step_start("validate_verification", task_data["id"])

        prompt = get_agent_prompt(
            AgentRole.VERIFICATION_EXPERT,
            task_title=task_data["title"],
            files=task_data["files"],
            component=task_data["phase"],
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "verification_expert"},
        )

        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data["files"]},
        )

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "verification_expert"},
        )

        validation_result = {
            "agent": "verification-expert",
            "status": "passed" if result.success else "warnings",
            "findings": [],
            "raw_output": result.output,
        }

        validation_results = state.get("validation_results", [])
        validation_results.append(validation_result)

        # Check for actionable issues that need to be addressed
        actionable_issues = self._extract_actionable_issues(validation_results)
        critical_or_high_issues = [
            i for i in actionable_issues if i["severity"] in ("critical", "high")
        ]

        # Determine next action
        validation_fix_attempt = state.get("validation_fix_attempt", 0)
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)

        if critical_or_high_issues and validation_fix_attempt < max_attempts:
            # Found issues that need addressing - route back to implementation agent
            next_action = "address_issues"
            messages = [
                "Verification found issues requiring fixes",
                f"  {len(critical_or_high_issues)} issue(s) to address",
            ]
        elif critical_or_high_issues and task_data.get("is_critical"):
            # Max attempts reached on critical task - escalate
            next_action = "human_review"
            messages = [
                "Verification found unresolved issues on critical task",
                "Escalating to human review",
            ]
        elif self.config.run_hallucination_defense:
            next_action = "hallucination_check"
            messages = ["Verification check completed"]
        else:
            next_action = "commit"
            messages = ["Verification check completed"]

        self._log_step_end("validate_verification", task_data["id"], result.success)
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": messages,
        }

    async def _check_hallucinations_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Check for potential hallucinations in the implementation."""
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        self._log_step_start("check_hallucinations", task_data["id"])

        # Extract claims from implementation output
        claims = execution_result.get("output", "")[:2000]  # First 2000 chars

        prompt = get_agent_prompt(
            AgentRole.HALLUCINATION_DEFENSE,
            claims=claims,
        )

        # Log the prompt being sent
        self._log_execution(
            event_type="validation_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={"validation_type": "hallucination_defense"},
        )

        result = await self.backend_manager.execute_with_failover(prompt)

        # Log the response
        self._log_execution(
            event_type="validation_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_type": "hallucination_defense"},
        )

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

        # Check for actionable issues across all validation results
        actionable_issues = self._extract_actionable_issues(validation_results)

        if (
            actionable_issues
            and state.get("validation_fix_attempt", 0)
            < self.config.max_validation_fix_attempts
        ):
            next_action = "address_issues"
        elif has_hallucinations and task_data.get("is_critical"):
            next_action = "human_review"
        else:
            next_action = "commit"

        self._log_step_end(
            "check_hallucinations", task_data["id"], not has_hallucinations
        )
        return {
            **state,
            "validation_results": validation_results,
            "next_action": next_action,
            "messages": ["Hallucination check completed"],
        }

    def _extract_actionable_issues(self, validation_results: list[dict]) -> list[dict]:
        """
        Extract actionable issues from validation results.

        Looks for patterns indicating critical/high priority issues that should
        be addressed by the agent before proceeding.

        Returns a list of issues with structure:
        {
            "agent": str,
            "severity": "critical" | "high" | "medium" | "low",
            "description": str,
            "recommendation": str (optional)
        }
        """
        import re

        actionable_issues = []

        for vr in validation_results:
            raw_output = vr.get("raw_output", "") or ""
            agent = vr.get("agent", "unknown")

            # Skip if validation passed cleanly
            if vr.get("status") == "passed":
                continue

            # Pattern 1: Look for explicit severity markers
            # e.g., "**Impact:** High", "Severity: Critical", "[HIGH]", "[CRITICAL]"
            severity_patterns = [
                (r"\*\*(?:Impact|Severity)\*\*:\s*(?:High|Critical)", "high"),
                (r"(?:Impact|Severity):\s*(?:High|Critical)", "high"),
                (r"\[(?:HIGH|CRITICAL)\]", "high"),
                (r"❌.*(?:deviation|error|incorrect|broken|failed)", "high"),
                (
                    r"- \[ \].*(?:Section|Requirement)",
                    "medium",
                ),  # Unchecked requirements in reports
            ]

            for pattern, severity in severity_patterns:
                matches = re.findall(pattern, raw_output, re.IGNORECASE)
                if matches:
                    # Extract the context around the match
                    for match in re.finditer(pattern, raw_output, re.IGNORECASE):
                        start = max(0, match.start() - 100)
                        end = min(len(raw_output), match.end() + 200)
                        context = raw_output[start:end].strip()

                        actionable_issues.append(
                            {
                                "agent": agent,
                                "severity": severity,
                                "description": context[:300],
                                "pattern_matched": pattern,
                            }
                        )

            # Pattern 2: Look for "Deviations" section with content
            deviations_match = re.search(
                r"### Deviations\s*\n(.*?)(?=###|\Z)",
                raw_output,
                re.DOTALL | re.IGNORECASE,
            )
            if deviations_match:
                deviations_content = deviations_match.group(1).strip()
                # Check if there's actual content (not just whitespace or "None")
                if deviations_content and not re.match(
                    r"^(None|N/A|-|No deviations)?\s*$",
                    deviations_content,
                    re.IGNORECASE,
                ):
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "high",
                            "description": f"Spec deviations found: {deviations_content[:300]}",
                            "pattern_matched": "deviations_section",
                        }
                    )

            # Pattern 3: Look for "Recommendations" section with actionable items
            recommendations_match = re.search(
                r"### (?:Recommendations|Suggestions)\s*\n(.*?)(?=###|\Z)",
                raw_output,
                re.DOTALL | re.IGNORECASE,
            )
            if recommendations_match:
                rec_content = recommendations_match.group(1).strip()
                # Check for actionable recommendations (containing action verbs)
                if re.search(
                    r"\b(fix|update|change|modify|remove|add|implement|correct)\b",
                    rec_content,
                    re.IGNORECASE,
                ):
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "medium",
                            "description": f"Actionable recommendations: {rec_content[:300]}",
                            "pattern_matched": "recommendations_section",
                        }
                    )

            # Pattern 4: Look for failed behavior checks
            behavior_failures = re.findall(
                r"- \[ \].*?(?:has deviation|failed|incorrect|broken).*",
                raw_output,
                re.IGNORECASE,
            )
            for failure in behavior_failures:
                actionable_issues.append(
                    {
                        "agent": agent,
                        "severity": "high",
                        "description": failure[:200],
                        "pattern_matched": "behavior_failure",
                    }
                )

            # Pattern 5: INCORRECT marker from hallucination defense
            if "INCORRECT" in raw_output.upper():
                incorrect_context = re.search(
                    r"INCORRECT[:\s]*(.*?)(?:\n\n|\Z)",
                    raw_output,
                    re.IGNORECASE | re.DOTALL,
                )
                if incorrect_context:
                    actionable_issues.append(
                        {
                            "agent": agent,
                            "severity": "critical",
                            "description": f"Factual incorrectness: {incorrect_context.group(1)[:200]}",
                            "pattern_matched": "incorrect_marker",
                        }
                    )

        # Filter to only critical and high severity issues for automatic remediation
        return [
            issue
            for issue in actionable_issues
            if issue["severity"] in ("critical", "high")
        ]

    async def _address_validation_issues_node(
        self, state: OrchestratorState
    ) -> OrchestratorState:
        """Have the agent address critical/high priority validation issues."""
        task_data = state["current_task"]
        validation_fix_attempt = state.get("validation_fix_attempt", 0) + 1
        max_attempts = getattr(self.config, "max_validation_fix_attempts", 2)
        self._log_step_start(
            "address_validation_issues",
            task_data["id"],
            f"attempt {validation_fix_attempt}/{max_attempts}",
        )

        if validation_fix_attempt > max_attempts:
            # Max attempts reached - create follow-up task if configured
            if self.config.create_followup_task_on_fix_failure:
                await self._create_validation_fix_followup_task(state)

            # Escalate to human review or proceed
            return {
                **state,
                "validation_fix_attempt": validation_fix_attempt,
                "next_action": (
                    "human_review" if task_data.get("is_critical") else "error"
                ),
                "error_message": f"Failed to address validation issues after {max_attempts} attempts",
                "messages": [
                    f"Max validation fix attempts ({max_attempts}) reached",
                    (
                        "Escalating to human review"
                        if task_data.get("is_critical")
                        else "Proceeding with unresolved issues"
                    ),
                    (
                        "Created follow-up task for validation issues"
                        if self.config.create_followup_task_on_fix_failure
                        else ""
                    ),
                ],
            }

        validation_results = state.get("validation_results", [])
        actionable_issues = self._extract_actionable_issues(validation_results)

        # Build detailed prompt for addressing issues
        issues_summary = "\n".join(
            [
                f"- [{issue['severity'].upper()}] ({issue['agent']}): {issue['description']}"
                for issue in actionable_issues[:5]  # Limit to top 5 issues
            ]
        )

        # Collect raw outputs for context
        validation_context = "\n\n---\n\n".join(
            [
                f"## {vr['agent']} Report\n{vr.get('raw_output', 'No output')[:1500]}"
                for vr in validation_results
                if vr.get("status") != "passed"
            ]
        )

        prompt = f"""You previously implemented a task but validation checks found critical issues that need to be addressed.

## Task
{task_data['title']}
{task_data.get('description', '')}

## Actionable Issues Found
{issues_summary}

## Validation Reports
{validation_context[:4000]}

## Fix Attempt
This is validation fix attempt {validation_fix_attempt} of {max_attempts}.

## Instructions
1. Carefully review each issue identified above
2. For spec deviations: Update implementation to match the specification
3. For behavior failures: Fix the code to produce correct behavior
4. For factual errors: Correct any incorrect assumptions or implementations
5. Do NOT modify test expected values to make tests pass - fix the implementation
6. Run tests after your fixes to verify correctness

Focus on addressing the specific issues identified. Do not re-implement the entire task.

{f"Previous fix attempt did not resolve all issues. Please try a different approach." if validation_fix_attempt > 1 else ""}
"""

        # Log the prompt
        self._log_execution(
            event_type="validation_fix_prompt",
            task_id=task_data["id"],
            prompt=prompt,
            extra={
                "validation_fix_attempt": validation_fix_attempt,
                "max_attempts": max_attempts,
                "issues_count": len(actionable_issues),
            },
        )

        # Execute fix via backend
        result = await self.backend_manager.execute_with_failover(
            prompt,
            context={"files": task_data.get("files", [])},
        )

        # Log the response
        self._log_execution(
            event_type="validation_fix_response",
            task_id=task_data["id"],
            output=result.output,
            error=result.error,
            success=result.success,
            backend=result.backend,
            duration=result.duration_seconds,
            extra={"validation_fix_attempt": validation_fix_attempt},
        )

        # Clear validation results to force re-validation
        messages = [
            f"Validation fix attempt {validation_fix_attempt}/{max_attempts} {'succeeded' if result.success else 'failed'}"
        ]
        if not result.success and result.error:
            error_preview = (
                result.error[:200] if len(result.error) > 200 else result.error
            )
            messages.append(f"  Error: {error_preview}")

        self._log_step_end("address_validation_issues", task_data["id"], result.success)
        return {
            **state,
            "validation_fix_attempt": validation_fix_attempt,
            "validation_results": [],  # Clear to force re-validation
            "next_action": "test" if result.success else "error",
            "messages": messages,
        }

    async def _commit_changes_node(self, state: OrchestratorState) -> OrchestratorState:
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

        # Get the list of changed files using git
        try:
            # First, get list of modified/added/deleted files
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
            # Format: "[auto] Task <id>: <title>"
            # Escape any quotes in the title for shell safety
            safe_title = task_title.replace('"', '\\"').replace("'", "\\'")
            commit_message = f"[auto] Task {task_id}: {safe_title}"

            # Create the commit
            commit_result = await self.backend_manager.execute_command(
                f'git commit -m "{commit_message}"',
                cwd=self.config.project_root,
            )

            if not commit_result.success:
                # Check if it's just "nothing to commit"
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

            # Count files changed for message
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
        task_data = state.get("current_task", {})
        task_id = task_data.get("id", "unknown")
        self._log_step_start(
            "wait_for_human",
            task_id,
            f"question={question_id}" if question_id else f"review={review_id}",
        )

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
                    self._log_step_end(
                        "wait_for_human",
                        task_id,
                        True,
                        f"answered after {check_count} checks",
                    )
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
                    self._log_step_end(
                        "wait_for_human",
                        task_id,
                        True,
                        f"reviewed after {check_count} checks",
                    )
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
        self._log_step_end("wait_for_human", task_id, False, "TIMEOUT")
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
            # Task is complete if:
            # 1. Execution succeeded AND tests passed, OR
            # 2. Execution succeeded AND tests were already failing (pre-existing)
            #    AND all validations passed
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
        last_result = state.get("last_execution_result", {}) or {}
        error_msg = last_result.get("error") or ""

        # Check if this is a rate limit exhaustion error
        is_rate_limited = (
            "exhausted" in error_msg.lower() or "rate limit" in error_msg.lower()
        )

        if is_rate_limited and self.config.rate_limit_pause_hours > 0:
            # Long pause when all backends are rate-limited
            pause_seconds = self.config.rate_limit_pause_hours * 3600
            pause_hours = self.config.rate_limit_pause_hours
            print(f"\n{'='*60}")
            print(f"All backends rate-limited. Pausing for {pause_hours} hours...")
            print(
                f"Will resume at: {datetime.now() + timedelta(seconds=pause_seconds)}"
            )
            print(f"{'='*60}\n")
            await asyncio.sleep(pause_seconds)
            return {
                **state,
                "execution_attempt": 0,  # Reset attempt counter after long pause
                "next_action": "retry",
                "messages": [
                    f"Resumed after {pause_hours}h rate-limit pause, retrying task"
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
        # Always route to analysis first (unless error)
        next_action = state.get("next_action", "error")
        if next_action == "test":
            return "analyze"  # Redirect to analysis
        return next_action

    def _route_after_analysis(self, state: OrchestratorState) -> str:
        """Route based on response analysis results."""
        return state.get("next_action", "error")

    def _route_after_auto_decision(self, state: OrchestratorState) -> str:
        """Route based on auto-decision result."""
        return state.get("next_action", "error")

    def _route_after_tests(self, state: OrchestratorState) -> str:
        return state.get("next_action", "error")

    def _route_after_fix(self, state: OrchestratorState) -> str:
        return state.get("next_action", "error")

    def _route_after_spec_adherence(self, state: OrchestratorState) -> str:
        """Route based on spec adherence validation results."""
        return state.get("next_action", "verification")

    def _route_after_verification(self, state: OrchestratorState) -> str:
        return state.get("next_action", "commit")

    def _route_after_hallucination_check(self, state: OrchestratorState) -> str:
        return state.get("next_action", "commit")

    def _route_after_commit(self, state: OrchestratorState) -> str:
        return state.get("next_action", "update")

    def _route_after_address_issues(self, state: OrchestratorState) -> str:
        return state.get("next_action", "error")

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
        # Dump config at startup for debugging
        self._dump_config()

        initial_state: OrchestratorState = {
            "current_task": None,
            "ground_truth_path": str(self.config.ground_truth_path),
            "execution_attempt": 0,
            "fix_attempt": 0,
            "validation_fix_attempt": 0,
            "last_execution_result": None,
            "baseline_test_passed": None,
            "baseline_test_output": None,
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
