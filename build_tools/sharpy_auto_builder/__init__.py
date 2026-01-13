"""
Sharpy Auto Builder - Automated implementation of Sharpy compiler tasks.

Uses LangGraph to orchestrate between GitHub Copilot CLI and Claude Code,
with validation agents to ensure spec adherence and quality.
"""

__version__ = "0.1.0"

from .config import Config, BackendConfig, RateLimitConfig, BackendType
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
from .backends import (
    Backend,
    ClaudeCodeBackend,
    CopilotBackend,
    BackendManager,
    ExecutionResult,
    RateLimitState,
)
from .human_loop import (
    HumanLoopManager,
    HumanQuestion,
    HumanReviewRequest,
    QuestionPriority,
    QuestionStatus,
)
from .response_analyzer import (
    ResponseAnalyzer,
    ResponseAnalysis,
    ResponseType,
    TaskType,
)
from .auto_decision import (
    AutoDecisionEngine,
    AutoDecision,
    DecisionType,
)
from .orchestrator import Orchestrator, OrchestratorState

__all__ = [
    # Config
    "Config",
    "BackendConfig",
    "RateLimitConfig",
    "BackendType",
    # State
    "GroundTruth",
    "Task",
    "TaskStatus",
    "TaskExecution",
    "ValidationResult",
    "ValidationStatus",
    "Phase",
    "parse_task_list",
    # Agents
    "AgentRole",
    "AGENT_CONFIGS",
    "get_agent_prompt",
    "get_specialist_for_task",
    # Backends
    "Backend",
    "ClaudeCodeBackend",
    "CopilotBackend",
    "BackendManager",
    "ExecutionResult",
    "RateLimitState",
    # Human Loop
    "HumanLoopManager",
    "HumanQuestion",
    "HumanReviewRequest",
    "QuestionPriority",
    "QuestionStatus",
    # Response Analysis (Overseer)
    "ResponseAnalyzer",
    "ResponseAnalysis",
    "ResponseType",
    "TaskType",
    # Auto-Decision (Overseer)
    "AutoDecisionEngine",
    "AutoDecision",
    "DecisionType",
    # Orchestrator
    "Orchestrator",
    "OrchestratorState",
]
