"""
Model selection framework for intelligent routing of tasks to appropriate AI models.

This module provides a taxonomy of task types and complexity levels, along with
logic to recommend the most cost-effective model for each task category.

Model Routing Philosophy:
- Use the smallest model that can reliably complete the task
- Reserve expensive models (Opus) for tasks that truly require them
- Prefer Sonnet for most code generation and documentation tasks
- Use Haiku for simple classification and validation tasks
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class TaskComplexity(Enum):
    """
    Task complexity levels for model selection.

    Complexity is determined by:
    - Amount of context needed to understand the task
    - Number of steps or decisions required
    - Degree of ambiguity in requirements
    - Need for creative problem-solving

    Examples for each level:

    TRIVIAL:
        - "Is this a rate limit error?" → yes/no classification
        - "Extract the wait time from this error message"
        - "Does this string match the pattern?"

    LOW:
        - "Generate a function that implements this simple interface"
        - "Fix this typo in the code"
        - "Add type hints to this function"

    MEDIUM:
        - "Refactor this function to use async/await"
        - "Write comprehensive tests for this module"
        - "Generate documentation for this API"

    HIGH:
        - "Debug this intermittent test failure"
        - "Design a caching strategy for this service"
        - "Implement this feature with proper error handling"

    VERY_HIGH:
        - "Architect a new subsystem for the compiler"
        - "Refactor this tightly-coupled module into clean abstractions"
        - "Optimize this algorithm while maintaining correctness"
    """

    # Simple pattern matching, classification, yes/no decisions
    TRIVIAL = "trivial"

    # Straightforward implementation, following clear patterns
    LOW = "low"

    # Multi-step reasoning, some ambiguity
    MEDIUM = "medium"

    # Complex reasoning, architecture decisions, novel problems
    HIGH = "high"

    # Extended thinking required, complex tradeoffs
    VERY_HIGH = "very_high"


class TaskType(Enum):
    """
    Categories of tasks for model routing.

    Each task type has different characteristics that affect model selection:

    CLASSIFICATION:
        - Binary or categorical decisions
        - Pattern matching against known criteria
        - Examples: error type detection, file categorization
        - Best handled by: Haiku (trivial-medium), Sonnet (high)

    CODE_GENERATION:
        - Creating new code from specifications
        - Following established patterns and conventions
        - Examples: implementing interfaces, generating boilerplate
        - Best handled by: Sonnet (low-medium), Opus (high-very_high)

    VALIDATION:
        - Checking code or text against rules
        - Verifying correctness without modification
        - Examples: linting, spec compliance checking
        - Best handled by: Haiku (trivial-low), Sonnet (medium-high)

    DOCUMENTATION:
        - Generating explanatory text
        - Creating API docs, tutorials, comments
        - Examples: docstring generation, README updates
        - Best handled by: Sonnet (all levels)

    DEBUGGING:
        - Identifying and fixing issues
        - Root cause analysis
        - Examples: test failure investigation, error diagnosis
        - Best handled by: Sonnet (low-medium), Opus (high-very_high)

    ARCHITECTURE:
        - System design and structure decisions
        - Trade-off analysis
        - Examples: API design, module organization
        - Best handled by: Sonnet (low), Opus (medium-very_high)

    IMPLEMENTATION:
        - Multi-step feature development
        - Combining multiple code changes
        - Examples: adding new compiler features, migrations
        - Best handled by: Sonnet (low-medium), Opus (high-very_high)
    """

    # Simple identification/classification
    CLASSIFICATION = "classification"

    # Code generation following patterns
    CODE_GENERATION = "code_generation"

    # Code review and validation
    VALIDATION = "validation"

    # Documentation generation
    DOCUMENTATION = "documentation"

    # Bug fixing and debugging
    DEBUGGING = "debugging"

    # Architecture and design decisions
    ARCHITECTURE = "architecture"

    # Multi-step implementation
    IMPLEMENTATION = "implementation"


@dataclass
class ModelRecommendation:
    """
    Recommended model for a task.

    Attributes:
        model: The recommended model identifier string
        reasoning: Explanation of why this model was selected
        fallback_model: Alternative model if primary is unavailable
        requires_extended_thinking: Whether the task benefits from extended thinking
    """

    model: str
    reasoning: str
    fallback_model: str | None = None
    requires_extended_thinking: bool = False


# Type alias for the model selection matrix
ModelMatrix = dict[TaskType, dict[TaskComplexity, str]]


class ModelSelector:
    """
    Intelligent model selection based on task characteristics.

    Routing Guidelines:

    Haiku (claude-3-5-haiku-20241022):
    - Classification tasks (is this a rate limit error?)
    - Simple validation (does this match a pattern?)
    - Yes/no decisions with clear criteria
    - Extracting structured data from text

    Sonnet (claude-sonnet-4-5-20250929):
    - Code generation following established patterns
    - Documentation generation
    - Bug fixes with clear reproduction steps
    - Test writing
    - Straightforward refactoring

    Opus (claude-opus-4-5-20251101):
    - Architecture decisions
    - Complex debugging with unclear cause
    - Novel feature implementation
    - Tasks requiring extended reasoning
    - Multi-file refactoring with design decisions

    Usage:
        >>> rec = ModelSelector.select_model(
        ...     TaskType.CODE_GENERATION,
        ...     TaskComplexity.MEDIUM
        ... )
        >>> print(rec.model)
        'claude-sonnet-4-5-20250929'
        >>> print(rec.reasoning)
        'Medium complexity code generation is well-suited for Sonnet'
    """

    # Model identifiers
    HAIKU = "claude-3-5-haiku-20241022"
    SONNET = "claude-sonnet-4-5-20250929"
    OPUS = "claude-opus-4-5-20251101"

    # Model selection matrix: TaskType -> TaskComplexity -> Model
    # This implements the mapping table from the task specification
    _SELECTION_MATRIX: ModelMatrix = {
        TaskType.CLASSIFICATION: {
            TaskComplexity.TRIVIAL: HAIKU,
            TaskComplexity.LOW: HAIKU,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: SONNET,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.CODE_GENERATION: {
            TaskComplexity.TRIVIAL: HAIKU,
            TaskComplexity.LOW: SONNET,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: OPUS,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.VALIDATION: {
            TaskComplexity.TRIVIAL: HAIKU,
            TaskComplexity.LOW: HAIKU,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: SONNET,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.DOCUMENTATION: {
            TaskComplexity.TRIVIAL: SONNET,
            TaskComplexity.LOW: SONNET,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: OPUS,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.DEBUGGING: {
            TaskComplexity.TRIVIAL: SONNET,
            TaskComplexity.LOW: SONNET,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: OPUS,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.ARCHITECTURE: {
            TaskComplexity.TRIVIAL: SONNET,
            TaskComplexity.LOW: SONNET,
            TaskComplexity.MEDIUM: OPUS,
            TaskComplexity.HIGH: OPUS,
            TaskComplexity.VERY_HIGH: OPUS,
        },
        TaskType.IMPLEMENTATION: {
            TaskComplexity.TRIVIAL: SONNET,
            TaskComplexity.LOW: SONNET,
            TaskComplexity.MEDIUM: SONNET,
            TaskComplexity.HIGH: OPUS,
            TaskComplexity.VERY_HIGH: OPUS,
        },
    }

    # Reasoning templates for model selection
    _REASONING_TEMPLATES = {
        HAIKU: "{complexity} complexity {task_type} tasks are efficiently handled by Haiku",
        SONNET: "{complexity} complexity {task_type} is well-suited for Sonnet's balanced capabilities",
        OPUS: "{complexity} complexity {task_type} requires Opus's advanced reasoning",
    }

    # Tasks that benefit from extended thinking (Opus only)
    _EXTENDED_THINKING_TASKS = {
        (TaskType.ARCHITECTURE, TaskComplexity.HIGH),
        (TaskType.ARCHITECTURE, TaskComplexity.VERY_HIGH),
        (TaskType.DEBUGGING, TaskComplexity.VERY_HIGH),
        (TaskType.IMPLEMENTATION, TaskComplexity.VERY_HIGH),
    }

    @classmethod
    def select_model(
        cls,
        task_type: TaskType,
        complexity: TaskComplexity,
        context: dict[str, Any] | None = None,
    ) -> ModelRecommendation:
        """
        Select appropriate model for task.

        Args:
            task_type: The category of task being performed
            complexity: The estimated complexity level
            context: Optional additional context that might influence selection
                     (e.g., {"file_count": 10} for multi-file tasks)

        Returns:
            ModelRecommendation with the selected model and reasoning
        """
        # Look up model from selection matrix
        model = cls._SELECTION_MATRIX[task_type][complexity]

        # Generate reasoning
        reasoning = cls._REASONING_TEMPLATES[model].format(
            complexity=complexity.value.capitalize(),
            task_type=task_type.value.replace("_", " "),
        )

        # Determine fallback model (next tier down, or same if already lowest)
        fallback = cls._get_fallback_model(model)

        # Check if extended thinking would help
        requires_extended = (task_type, complexity) in cls._EXTENDED_THINKING_TASKS

        return ModelRecommendation(
            model=model,
            reasoning=reasoning,
            fallback_model=fallback,
            requires_extended_thinking=requires_extended,
        )

    @classmethod
    def _get_fallback_model(cls, model: str) -> str | None:
        """Get the fallback model for a given model (one tier down)."""
        if model == cls.OPUS:
            return cls.SONNET
        elif model == cls.SONNET:
            return cls.HAIKU
        else:
            return None  # Haiku has no fallback

    @classmethod
    def get_model_for_task(
        cls,
        task_type: TaskType,
        complexity: TaskComplexity,
    ) -> str:
        """
        Convenience method to get just the model string.

        Args:
            task_type: The category of task being performed
            complexity: The estimated complexity level

        Returns:
            The model identifier string
        """
        return cls._SELECTION_MATRIX[task_type][complexity]

    @classmethod
    def get_all_models(cls) -> list[str]:
        """Return list of all available models in order of capability."""
        return [cls.HAIKU, cls.SONNET, cls.OPUS]


# Convenience exports for common model names
HAIKU = ModelSelector.HAIKU
SONNET = ModelSelector.SONNET
OPUS = ModelSelector.OPUS

__all__ = [
    "TaskComplexity",
    "TaskType",
    "ModelRecommendation",
    "ModelSelector",
    "HAIKU",
    "SONNET",
    "OPUS",
]
