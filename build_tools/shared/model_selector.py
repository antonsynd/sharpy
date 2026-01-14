"""
Model selection framework for intelligent routing of tasks to appropriate AI models.

This module provides a taxonomy of task types and complexity levels, along with
logic to recommend the most cost-effective model for each task category.

Model Routing Philosophy:
- Use the smallest model that can reliably complete the task
- Reserve expensive models (Opus) for tasks that truly require them
- Prefer Sonnet for most code generation and documentation tasks
- Use Haiku for simple classification and validation tasks

Cost Tracking Considerations:
------------------------
When tracking costs, consider the following pricing per 1M tokens (approximate):

| Model  | Input     | Output    | Best For                        |
|--------|-----------|-----------|----------------------------------|
| Haiku  | $0.80     | $4.00     | Classification, simple validation|
| Sonnet | $3.00     | $15.00    | Code gen, docs, debugging       |
| Opus   | $15.00    | $75.00    | Architecture, complex reasoning |

Cost optimization strategies:
1. Use classify_task() to automatically route to cheapest capable model
2. Monitor actual model usage via logging callbacks
3. Review tasks using Opus - could any be done by Sonnet?
4. Batch simple tasks to reduce per-request overhead
5. Cache responses for repeated similar tasks

Example cost tracking integration:
    def on_model_selected(recommendation, task_desc):
        log_cost_estimate(recommendation.model, estimated_tokens)
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable
import logging
import re


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
        was_overridden: Whether the model was manually overridden
        original_model: The originally selected model before override (if any)
    """

    model: str
    reasoning: str
    fallback_model: str | None = None
    requires_extended_thinking: bool = False
    was_overridden: bool = False
    original_model: str | None = None


@dataclass
class ModelOverride:
    """
    Configuration for overriding model selection.

    Attributes:
        model: The model to use instead of the auto-selected one
        reason: Explanation for the override (for logging/auditing)
    """

    model: str
    reason: str


# Type alias for the model selection matrix
ModelMatrix = dict[TaskType, dict[TaskComplexity, str]]

# Type alias for logging callback
ModelSelectionCallback = Callable[[ModelRecommendation, str | None], None]

# Module-level logger
_logger = logging.getLogger(__name__)


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

    # =========================================================================
    # Heuristic Task Classification
    # =========================================================================

    # Keywords that suggest specific task types
    _TASK_TYPE_KEYWORDS: dict[TaskType, list[str]] = {
        TaskType.CLASSIFICATION: [
            "classify",
            "categorize",
            "is this",
            "detect",
            "identify type",
            "which kind",
            "yes or no",
            "true or false",
            "determine if",
        ],
        TaskType.CODE_GENERATION: [
            "generate",
            "create",
            "implement",
            "write code",
            "add function",
            "add method",
            "add class",
            "scaffold",
            "boilerplate",
        ],
        TaskType.VALIDATION: [
            "validate",
            "verify",
            "check",
            "lint",
            "review",
            "compliant",
            "correct",
            "matches",
            "conforms",
            "adherence",
        ],
        TaskType.DOCUMENTATION: [
            "document",
            "docstring",
            "readme",
            "explain",
            "tutorial",
            "comment",
            "describe",
            "api docs",
            "documentation",
        ],
        TaskType.DEBUGGING: [
            "debug",
            "fix",
            "bug",
            "error",
            "failing",
            "broken",
            "issue",
            "investigate",
            "diagnose",
            "troubleshoot",
            "root cause",
        ],
        TaskType.ARCHITECTURE: [
            "architect",
            "design",
            "structure",
            "organize",
            "refactor",
            "redesign",
            "api design",
            "schema",
            "pattern",
            "abstraction",
        ],
        TaskType.IMPLEMENTATION: [
            "implement feature",
            "add feature",
            "build",
            "develop",
            "multi-file",
            "migration",
            "integrate",
            "full implementation",
        ],
    }

    # Keywords that suggest complexity levels
    _COMPLEXITY_KEYWORDS: dict[TaskComplexity, list[str]] = {
        TaskComplexity.TRIVIAL: [
            "simple",
            "trivial",
            "quick",
            "easy",
            "basic",
            "one-line",
            "yes/no",
            "binary",
            "straightforward",
        ],
        TaskComplexity.LOW: [
            "small",
            "minor",
            "slight",
            "single file",
            "one function",
            "typo",
            "rename",
            "add hint",
        ],
        TaskComplexity.MEDIUM: [
            "moderate",
            "several",
            "multi-step",
            "refactor function",
            "add tests",
            "update",
            "extend",
        ],
        TaskComplexity.HIGH: [
            "complex",
            "difficult",
            "multiple files",
            "tricky",
            "careful",
            "edge cases",
            "comprehensive",
            "thorough",
        ],
        TaskComplexity.VERY_HIGH: [
            "very complex",
            "architecture",
            "system-wide",
            "redesign",
            "major refactor",
            "novel",
            "from scratch",
            "entire module",
        ],
    }

    @classmethod
    def classify_task(
        cls,
        description: str,
        context: dict[str, Any] | None = None,
    ) -> tuple[TaskType, TaskComplexity]:
        """
        Attempt to classify a task from its description.

        This is a heuristic approach using keyword matching. It may need
        human override for ambiguous cases.

        Args:
            description: Natural language description of the task
            context: Optional context hints, e.g.:
                - file_count: Number of files involved
                - line_count: Approximate lines to change
                - has_tests: Whether tests exist

        Returns:
            Tuple of (TaskType, TaskComplexity) based on heuristics

        Example:
            >>> task_type, complexity = ModelSelector.classify_task(
            ...     "Fix the bug in the parser that causes incorrect token positions"
            ... )
            >>> print(task_type, complexity)
            TaskType.DEBUGGING TaskComplexity.MEDIUM
        """
        description_lower = description.lower()
        context = context or {}

        # Classify task type
        task_type = cls._classify_task_type(description_lower)

        # Classify complexity
        complexity = cls._classify_complexity(description_lower, context)

        _logger.debug(
            f"Classified task: type={task_type.value}, complexity={complexity.value}, "
            f"description={description[:50]}..."
        )

        return task_type, complexity

    @classmethod
    def _classify_task_type(cls, description_lower: str) -> TaskType:
        """Classify task type based on keyword matching."""
        scores: dict[TaskType, int] = {t: 0 for t in TaskType}

        for task_type, keywords in cls._TASK_TYPE_KEYWORDS.items():
            for keyword in keywords:
                if keyword in description_lower:
                    scores[task_type] += 1

        # Return highest scoring type, default to IMPLEMENTATION
        best_type = max(scores, key=lambda t: scores[t])
        if scores[best_type] == 0:
            return TaskType.IMPLEMENTATION  # Default fallback

        return best_type

    @classmethod
    def _classify_complexity(
        cls,
        description_lower: str,
        context: dict[str, Any],
    ) -> TaskComplexity:
        """Classify complexity based on keywords and context."""
        scores: dict[TaskComplexity, float] = {c: 0.0 for c in TaskComplexity}

        # Score based on keywords
        for complexity, keywords in cls._COMPLEXITY_KEYWORDS.items():
            for keyword in keywords:
                if keyword in description_lower:
                    scores[complexity] += 1.0

        # Check if any keyword matched
        keyword_matched = any(scores[c] > 0 for c in TaskComplexity)

        # Adjust based on context (use higher weights if no keywords matched)
        context_weight = 2.0 if not keyword_matched else 1.0

        file_count = context.get("file_count", 1)
        if file_count > 5:
            scores[TaskComplexity.HIGH] += 1.5 * context_weight
            scores[TaskComplexity.VERY_HIGH] += 1.0 * context_weight
        elif file_count > 2:
            scores[TaskComplexity.MEDIUM] += 1.0 * context_weight

        line_count = context.get("line_count", 0)
        if line_count > 500:
            scores[TaskComplexity.VERY_HIGH] += 2.0 * context_weight
        elif line_count > 200:
            scores[TaskComplexity.HIGH] += 1.5 * context_weight
        elif line_count > 50:
            scores[TaskComplexity.MEDIUM] += 1.0 * context_weight

        # Return highest scoring complexity, default to MEDIUM
        best_complexity = max(scores, key=lambda c: scores[c])
        if scores[best_complexity] == 0:
            return TaskComplexity.MEDIUM  # Safe default

        return best_complexity

    # =========================================================================
    # Model Selection with Override Support
    # =========================================================================

    # Class-level callback for logging model selections
    _selection_callback: ModelSelectionCallback | None = None

    @classmethod
    def set_selection_callback(
        cls,
        callback: ModelSelectionCallback | None,
    ) -> None:
        """
        Set a callback to be invoked whenever a model is selected.

        This enables logging, metrics collection, and cost tracking
        without modifying the core selection logic.

        Args:
            callback: Function taking (ModelRecommendation, task_description)
                      Set to None to disable callbacks.

        Example:
            >>> def log_selection(rec, desc):
            ...     print(f"Selected {rec.model} for: {desc}")
            >>> ModelSelector.set_selection_callback(log_selection)
        """
        cls._selection_callback = callback

    @classmethod
    def select_model(
        cls,
        task_type: TaskType,
        complexity: TaskComplexity,
        context: dict[str, Any] | None = None,
        override: ModelOverride | None = None,
        task_description: str | None = None,
    ) -> ModelRecommendation:
        """
        Select appropriate model for task.

        Args:
            task_type: The category of task being performed
            complexity: The estimated complexity level
            context: Optional additional context that might influence selection
                     (e.g., {"file_count": 10} for multi-file tasks)
            override: Optional override to force a specific model
            task_description: Optional description for logging purposes

        Returns:
            ModelRecommendation with the selected model and reasoning
        """
        # Look up model from selection matrix
        auto_selected_model = cls._SELECTION_MATRIX[task_type][complexity]

        # Check if extended thinking would help
        requires_extended = (task_type, complexity) in cls._EXTENDED_THINKING_TASKS

        # Handle override
        if override is not None:
            recommendation = ModelRecommendation(
                model=override.model,
                reasoning=f"Override: {override.reason}",
                fallback_model=cls._get_fallback_model(override.model),
                requires_extended_thinking=requires_extended,
                was_overridden=True,
                original_model=auto_selected_model,
            )
            _logger.info(
                f"Model override applied: {auto_selected_model} -> {override.model}, "
                f"reason: {override.reason}"
            )
        else:
            # Generate reasoning
            reasoning = cls._REASONING_TEMPLATES[auto_selected_model].format(
                complexity=complexity.value.capitalize(),
                task_type=task_type.value.replace("_", " "),
            )

            recommendation = ModelRecommendation(
                model=auto_selected_model,
                reasoning=reasoning,
                fallback_model=cls._get_fallback_model(auto_selected_model),
                requires_extended_thinking=requires_extended,
            )

        # Log selection
        _logger.debug(
            f"Model selected: {recommendation.model} for {task_type.value}/{complexity.value}"
        )

        # Invoke callback if set
        if cls._selection_callback is not None:
            try:
                cls._selection_callback(recommendation, task_description)
            except Exception as e:
                _logger.warning(f"Selection callback failed: {e}")

        return recommendation

    @classmethod
    def select_model_for_description(
        cls,
        description: str,
        context: dict[str, Any] | None = None,
        override: ModelOverride | None = None,
    ) -> ModelRecommendation:
        """
        Convenience method that classifies a task and selects a model.

        Combines classify_task() and select_model() into a single call.

        Args:
            description: Natural language task description
            context: Optional context for classification
            override: Optional model override

        Returns:
            ModelRecommendation for the classified task

        Example:
            >>> rec = ModelSelector.select_model_for_description(
            ...     "Generate unit tests for the parser module",
            ...     context={"file_count": 3}
            ... )
            >>> print(rec.model)
            'claude-sonnet-4-5-20250929'
        """
        task_type, complexity = cls.classify_task(description, context)
        return cls.select_model(
            task_type=task_type,
            complexity=complexity,
            context=context,
            override=override,
            task_description=description,
        )


# Convenience exports for common model names
HAIKU = ModelSelector.HAIKU
SONNET = ModelSelector.SONNET
OPUS = ModelSelector.OPUS

__all__ = [
    "TaskComplexity",
    "TaskType",
    "ModelRecommendation",
    "ModelOverride",
    "ModelSelector",
    "ModelSelectionCallback",
    "HAIKU",
    "SONNET",
    "OPUS",
]
