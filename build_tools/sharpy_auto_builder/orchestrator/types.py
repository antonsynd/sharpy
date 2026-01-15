"""
Type definitions for the orchestrator.

This module contains all TypedDicts, state definitions, and payload types
used throughout the orchestrator graph.
"""

from typing import Optional, Annotated, Literal, TypedDict
import operator


# =============================================================================
# Interrupt payload data structures for human-in-the-loop
# =============================================================================


class HumanQuestionPayload(TypedDict):
    """Payload for interrupt when asking human a question."""

    type: Literal["question"]
    task_id: str
    task_description: str
    question: str
    priority: str  # "high", "medium", "low"
    context: str
    options: Optional[list[str]]  # Predefined options if any


class HumanReviewPayload(TypedDict):
    """Payload for interrupt when requesting human review."""

    type: Literal["review"]
    task_id: str
    task_description: str
    execution_result: dict
    validation_results: list[dict]
    files_changed: list[str]
    diff_summary: str  # git diff --stat output (file statistics)
    diff_content: str  # Full git diff output (actual code changes)


class HumanResponse(TypedDict):
    """Response structure from human after interrupt."""

    approved: bool
    feedback: Optional[str]
    modified_value: Optional[str]
    retry: bool


# =============================================================================
# Main orchestrator state
# =============================================================================


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

    # Response analysis state (Overseer functionality)
    response_analysis: Optional[dict]  # Result of analyzing agent response
    auto_decision: Optional[dict]  # Auto-decision if made

    # Flow control
    next_action: str
    error_message: Optional[str]

    # Accumulated messages/logs
    messages: Annotated[list[str], operator.add]


def create_initial_state(ground_truth_path: str) -> OrchestratorState:
    """
    Create initial state for a new orchestrator run.

    Args:
        ground_truth_path: Path to the ground truth JSON file

    Returns:
        OrchestratorState: Initial state with all fields set to defaults
    """
    return {
        "current_task": None,
        "ground_truth_path": ground_truth_path,
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
        "response_analysis": None,
        "auto_decision": None,
        "next_action": "",
        "error_message": None,
        "messages": [],
    }
