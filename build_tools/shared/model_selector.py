"""
Task taxonomy for AI-backed build tools.

This module provides the task types and complexity levels that callers attach
to a backend request (see ``BackendConfig.task_type`` / ``task_complexity``).
"""

from enum import Enum


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


__all__ = [
    "TaskComplexity",
    "TaskType",
]
