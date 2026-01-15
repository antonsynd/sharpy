"""
Orchestrator package for the Sharpy Auto Builder.

This package contains the modular orchestrator implementation, split across
multiple files for maintainability:

- core.py: Main Orchestrator class that composes all mixins
- types.py: Type definitions (OrchestratorState, payloads, responses)
- graph.py: LangGraph state machine definition and routing functions
- helpers.py: Logging and configuration utilities
- followup.py: Follow-up task creation for failures
- nodes/: Node implementations organized by functionality
  - task_execution.py: Task selection, planning, execution, testing, fixing
  - validation.py: Spec adherence, verification, hallucination defense
  - human_interaction.py: Human review requests and question handling
  - overseer.py: Response analysis and auto-decision making
  - state_management.py: Ground truth updates, commits, error handling

Usage:
    from sharpy_auto_builder import Orchestrator

    async with Orchestrator(config) as orchestrator:
        result = await orchestrator.run(max_tasks=10)
"""

from .core import Orchestrator
from .types import (
    OrchestratorState,
    HumanQuestionPayload,
    HumanReviewPayload,
    HumanResponse,
    create_initial_state,
)
from .graph import build_graph

__all__ = [
    "Orchestrator",
    "OrchestratorState",
    "HumanQuestionPayload",
    "HumanReviewPayload",
    "HumanResponse",
    "create_initial_state",
    "build_graph",
]
