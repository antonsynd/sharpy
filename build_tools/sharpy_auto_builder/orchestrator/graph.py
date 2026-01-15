"""
Graph building and routing functions for the orchestrator.

This module contains the LangGraph state machine definition and all
routing functions that determine state transitions.
"""

from langgraph.graph import StateGraph, END

from .types import OrchestratorState


def build_graph(orchestrator: "Orchestrator") -> StateGraph:
    """
    Build the LangGraph state machine.

    Args:
        orchestrator: The orchestrator instance (provides node implementations)

    Returns:
        StateGraph: The configured state graph (not yet compiled)
    """
    # Create the graph
    graph = StateGraph(OrchestratorState)

    # Add nodes
    graph.add_node("select_task", orchestrator._select_task_node)
    graph.add_node("plan_implementation", orchestrator._plan_implementation_node)
    graph.add_node("run_baseline_tests", orchestrator._run_baseline_tests_node)
    graph.add_node("execute_implementation", orchestrator._execute_implementation_node)
    graph.add_node("analyze_response", orchestrator._analyze_response_node)
    graph.add_node("handle_auto_decision", orchestrator._handle_auto_decision_node)
    graph.add_node("run_tests", orchestrator._run_tests_node)
    graph.add_node("fix_test_failures", orchestrator._fix_test_failures_node)
    graph.add_node(
        "validate_spec_adherence", orchestrator._validate_spec_adherence_node
    )
    graph.add_node("validate_verification", orchestrator._validate_verification_node)
    graph.add_node("check_hallucinations", orchestrator._check_hallucinations_node)
    graph.add_node(
        "address_validation_issues", orchestrator._address_validation_issues_node
    )
    graph.add_node("request_human_review", orchestrator._request_human_review_node)
    graph.add_node("update_ground_truth", orchestrator._update_ground_truth_node)
    graph.add_node("commit_changes", orchestrator._commit_changes_node)
    graph.add_node("handle_error", orchestrator._handle_error_node)

    # Set entry point
    graph.set_entry_point("select_task")

    # Add conditional edges
    graph.add_conditional_edges(
        "select_task",
        route_after_select_task,
        {
            "plan": "plan_implementation",
            "complete": END,
            "wait_human": "request_human_review",
        },
    )

    # Run baseline tests before implementation to know what was already broken
    graph.add_edge("plan_implementation", "run_baseline_tests")
    graph.add_edge("run_baseline_tests", "execute_implementation")

    # Route through response analysis (Overseer) after execution
    graph.add_conditional_edges(
        "execute_implementation",
        route_after_execution,
        {
            "analyze": "analyze_response",
            "error": "handle_error",
        },
    )

    # Handle different response types from analysis
    graph.add_conditional_edges(
        "analyze_response",
        route_after_analysis,
        {
            "test": "run_tests",
            "auto_decide": "handle_auto_decision",
            "wait_human": "request_human_review",
            "error": "handle_error",
        },
    )

    # After auto-decision, update ground truth (skip test/validate for deferred)
    graph.add_conditional_edges(
        "handle_auto_decision",
        route_after_auto_decision,
        {
            "update": "update_ground_truth",
            "test": "run_tests",
            "error": "handle_error",
        },
    )

    graph.add_conditional_edges(
        "run_tests",
        route_after_tests,
        {
            "validate": "validate_spec_adherence",
            "fix": "fix_test_failures",
            "preexisting_failure": "validate_spec_adherence",
            "error": "handle_error",
        },
    )

    # After fix attempt, run tests again
    graph.add_conditional_edges(
        "fix_test_failures",
        route_after_fix,
        {
            "test": "run_tests",
            "error": "handle_error",
        },
    )

    graph.add_conditional_edges(
        "validate_spec_adherence",
        route_after_spec_adherence,
        {
            "verification": "validate_verification",
            "address_issues": "address_validation_issues",
            "human_review": "request_human_review",
        },
    )

    graph.add_conditional_edges(
        "validate_verification",
        route_after_verification,
        {
            "hallucination_check": "check_hallucinations",
            "address_issues": "address_validation_issues",
            "human_review": "request_human_review",
            "commit": "commit_changes",
        },
    )

    graph.add_conditional_edges(
        "check_hallucinations",
        route_after_hallucination_check,
        {
            "address_issues": "address_validation_issues",
            "human_review": "request_human_review",
            "commit": "commit_changes",
        },
    )

    # After addressing validation issues, run tests again then re-validate
    graph.add_conditional_edges(
        "address_validation_issues",
        route_after_address_issues,
        {
            "test": "run_tests",
            "human_review": "request_human_review",
            "error": "handle_error",
        },
    )

    # Native interrupt routing
    graph.add_conditional_edges(
        "request_human_review",
        route_after_human_response,
        {
            "commit_changes": "commit_changes",
            "execute_implementation": "execute_implementation",
            "update_ground_truth": "update_ground_truth",
            "handle_error": "handle_error",
        },
    )

    graph.add_conditional_edges(
        "commit_changes",
        route_after_commit,
        {
            "update": "update_ground_truth",
            "error": "handle_error",
        },
    )

    graph.add_conditional_edges(
        "update_ground_truth",
        route_after_update,
        {
            "next_task": "select_task",
            "complete": END,
        },
    )

    graph.add_conditional_edges(
        "handle_error",
        route_after_error,
        {
            "retry": "execute_implementation",
            "human": "request_human_review",
            "skip": "update_ground_truth",
            "abort": END,
            "pause_rate_limited": END,
        },
    )

    return graph


# =============================================================================
# Routing functions
# =============================================================================


def route_after_select_task(state: OrchestratorState) -> str:
    """Route after task selection."""
    return state.get("next_action", "complete")


def route_after_execution(state: OrchestratorState) -> str:
    """Route after execution - always analyze first unless error."""
    next_action = state.get("next_action", "error")
    if next_action == "test":
        return "analyze"  # Redirect to analysis
    return next_action


def route_after_analysis(state: OrchestratorState) -> str:
    """Route based on response analysis results."""
    return state.get("next_action", "error")


def route_after_auto_decision(state: OrchestratorState) -> str:
    """Route based on auto-decision result."""
    return state.get("next_action", "error")


def route_after_tests(state: OrchestratorState) -> str:
    """Route after test execution."""
    return state.get("next_action", "error")


def route_after_fix(state: OrchestratorState) -> str:
    """Route after fix attempt."""
    return state.get("next_action", "error")


def route_after_spec_adherence(state: OrchestratorState) -> str:
    """Route based on spec adherence validation results."""
    return state.get("next_action", "verification")


def route_after_verification(state: OrchestratorState) -> str:
    """Route after verification."""
    return state.get("next_action", "commit")


def route_after_hallucination_check(state: OrchestratorState) -> str:
    """Route after hallucination check."""
    return state.get("next_action", "commit")


def route_after_commit(state: OrchestratorState) -> str:
    """Route after commit."""
    return state.get("next_action", "update")


def route_after_address_issues(state: OrchestratorState) -> str:
    """Route after addressing validation issues."""
    return state.get("next_action", "error")


def route_after_human_wait(state: OrchestratorState) -> str:
    """Route after human wait."""
    return state.get("next_action", "timeout")


def route_after_human_response(state: OrchestratorState) -> str:
    """Route after human response."""
    return state.get("next_action", "continue")


def route_after_update(state: OrchestratorState) -> str:
    """Route after ground truth update."""
    return state.get("next_action", "next_task")


def route_after_error(state: OrchestratorState) -> str:
    """Route after error handling."""
    return state.get("next_action", "skip")
