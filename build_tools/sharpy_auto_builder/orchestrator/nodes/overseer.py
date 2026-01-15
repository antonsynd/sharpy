"""
Overseer nodes for the orchestrator.

Contains nodes related to response analysis and auto-decision making.
These nodes implement the "Overseer" functionality that detects when agents
ask questions instead of doing work.
"""

from pathlib import Path
from typing import TYPE_CHECKING

from ..types import OrchestratorState
from ...state import GroundTruth, TaskStatus
from ...response_analyzer import ResponseAnalysis, ResponseType, TaskType
from ...auto_decision import DecisionType
from ...human_loop import QuestionPriority

if TYPE_CHECKING:
    from ..core import Orchestrator


class OverseerNodes:
    """Mixin class providing overseer node implementations."""

    async def _analyze_response_node(
        self: "Orchestrator", state: OrchestratorState
    ) -> OrchestratorState:
        """
        Analyze agent response to detect questions vs. actual work done.

        This is the Overseer's primary function - catching cases where agents
        ask questions instead of doing work (like task 0.1.5.8).
        """
        task_data = state["current_task"]
        execution_result = state.get("last_execution_result", {})
        response_output = execution_result.get("output", "")

        # Detect task type from task data
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
                    context=response_output[:2000],
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
                deferral_reason = (
                    analysis.deferral_reason or "Agent recommends deferring this task"
                )
                question = self.human_loop.create_question(
                    task_id=task_data["id"],
                    question=f"Agent recommends deferring task {task_data['id']}: {task_data.get('title', 'Unknown')}",
                    context=f"Reason: {deferral_reason}\n\nThis task is not marked as optional, so automatic deferral is not allowed.",
                    priority=QuestionPriority.HIGH,
                    options=["proceed_anyway", "defer", "skip", "abort"],
                )
                return {
                    **state,
                    "response_analysis": analysis_dict,
                    "human_question_id": question.id,
                    "awaiting_human_input": True,
                    "next_action": "wait_human",
                    "messages": [
                        "Response analysis: Deferral recommended but task is not optional",
                        f"  Question ID: {question.id}",
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
        self: "Orchestrator", state: OrchestratorState
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
